using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// A MACHINE'S LOG FROM ITS PLC - its settings, in the Industry 4 Logging frame of the machine's Machine Parameters >
    /// Options page, under the switches of the records the PLC logs (5 November: "We should separate alarms and Industry4 logs
    /// from each other as the different pages ... create a group box inside 'Options' page (just like 'Mullion Notching' group
    /// box) at the right end column and put these controls and database connection setting inside it"). Before, it was under
    /// the alarms of Machine Parameters > Alarms and Logs (30 October).
    ///
    /// Settings rows as the page's (450 wide, UiStyle.SettingRowHeight high, a light line under each): whether the HMI takes
    /// this machine's log from its PLC, what the logger does now (its PLC log area in it: the machine program's, set by the PLC
    /// programmer - 5 November: not a setting on the screen), and the KMSystem lookup of a record's Process ID with its
    /// connection. The page has no Save: each is kept as it is changed - a switch at once, the connection when it is left or
    /// Enter is pressed. Kept in the machine's dashboard settings (Logs and Reports keeps its database and reports).
    ///
    /// Its frame is shown only while HMI Settings > Options has the Industry 4 logging in the HMI on (ShownWhileLogging).
    ///
    /// A new machine (HMI Settings > Options: "Machine details from the HMI", 5 November): under them, what the HMI takes itself
    /// (MachineRecords) - a switch row a record, a row a limit, and "Learn the normal times again" (after a repair).
    /// </summary>
    public sealed class MachineLogSettings : Panel
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        /// <summary>A row: as the Options page's (450 x SettingRowHeight).</summary>
        public const int RowW = 450;

        private readonly MachineDashboard _machine;
        private readonly ToolTip _tips = UiStyle.Tips();
        private readonly CheckBoxHMI _on = new CheckBoxHMI { Name = "plcLogging" };
        private readonly CheckBoxHMI _km = new CheckBoxHMI { Name = "kmLookup" };
        private readonly TextBox _kmConnection = new TextBox { Name = "kmConnection", Width = 250 };
        private readonly Label _state = new Label { Name = "loggerState" };
        private readonly Label _none = new Label { Name = "noLogDatabase" };
        private readonly Timer _clock = new Timer { Interval = 1000 };
        private bool _filling;

        // ---- what the HMI takes itself (a new machine) ----
        private readonly List<Control> _recordRows = new List<Control>();
        private readonly Dictionary<string, CheckBoxHMI> _kinds = new Dictionary<string, CheckBoxHMI>(StringComparer.Ordinal);
        private readonly Dictionary<string, NumericUpDown> _limits = new Dictionary<string, NumericUpDown>(StringComparer.Ordinal);
        private readonly Label _normals = new Label { Name = "normalsState" };
        private EventHandler _onFeatures;

        /// <summary>The machine's dashboard (null: the machine keeps no log database).</summary>
        public MachineDashboard Machine { get { return _machine; } }

        public MachineLogSettings(string moduleId)
        {
            _machine = string.IsNullOrEmpty(moduleId) ? null : DashboardService.Find(moduleId);
            Name = "machineLog";
            Width = RowW;
            BackColor = Color.White;
            Font = UiStyle.Body;

            var rows = new List<Control>();
            if (_machine == null)
            {
                _none.Text = T("Plg_NoDatabase", "This machine keeps no log database (HMI Settings > Logs and Reports): nothing is taken from its PLC.");
                _none.ForeColor = Color.FromArgb(110, 116, 124);
                rows.Add(Words(_none));
            }
            else
            {
                Switch(_on, T("Plg_SwitchRow", "The HMI takes the log from the PLC"),
                       T("Plg_Switch", "The HMI takes the machine's states, events and alarms from its PLC into its log database"));
                rows.Add(_on);
                _state.ForeColor = Color.FromArgb(30, 100, 190);
                rows.Add(Words(_state));
                Switch(_km, T("Plg_KmRow", "Process ID: its profile from KMSystem"),
                       T("Plg_Km", "A record's Process ID: its Profile Code and Profile Data from KMSystem (PostgreSQL)") + "\n" +
                       T("Plg_KmNote", "Needs Npgsql.dll beside the program, as the Industry 4 service had it."));
                rows.Add(_km);
                rows.Add(new SettingRow(T("Plg_KmConnection", "KMSystem connection"), _kmConnection));

                // what the HMI takes itself - a new machine (HMI Settings > Options: "Machine details from the HMI")
                var head = Words(new Label { Name = "recordsHead", Text = T("Rec_Head", "Taken by the HMI (a new machine)"), Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold) });
                head.Padding = new Padding(8, 14, 8, 4);
                _recordRows.Add(head);
                _recordRows.Add(KindRow(MachineRecords.CylinderSlow,
                    T("Rec_CylinderSlowTip", "A cylinder's movement slower than its normal time by more than the limit - wear before it is a fault " +
                      "(seals, a sticking valve, low air). Its normal: learned from its first 20 movements.")));
                _recordRows.Add(LimitRow("slow_percent", T("Rec_SlowPercent", "Slow: slower than its normal by (%)"), MachineRecords.DefaultSlowPercent, 10, 500,
                    T("Rec_SlowPercentTip", "How much slower than its normal time a movement must be to be recorded (and by 0.3 s at least).")));
                _recordRows.Add(KindRow(MachineRecords.CylinderNotConfirmed,
                    T("Rec_CylinderNotConfirmedTip", "A cylinder's movement its sensors did not confirm within its delay - which signal failed.")));
                _recordRows.Add(KindRow(MachineRecords.CylinderTimes,
                    T("Rec_CylinderTimesTip", "At the end of each shift: each movement's count, average and longest time, with its normal.")));
                var learn = UiStyle.TextButton(T("Rec_Learn", "Learn the normal times again"), 0, (s, e) => LearnAgain());
                learn.Name = "learnNormals";
                UiStyle.SetTip(_tips, learn, T("Rec_LearnTip", "After a repair: the normal times are learned again from the next movements."));
                _recordRows.Add(new SettingRow("", learn, true));
                _normals.ForeColor = Color.FromArgb(110, 116, 124);
                _recordRows.Add(Words(_normals));
                // the axes
                _recordRows.Add(KindRow(MachineRecords.AxisAlarm,
                    T("Rec_AxisAlarmTip", "A servo alarm or an axis error: its code, the position, what the axis was doing, its loads.")));
                _recordRows.Add(KindRow(MachineRecords.AxisOverload,
                    T("Rec_AxisOverloadTip", "An axis's effective load over its limit for a time, or its peak torque over its limit (% of the rated) - " +
                      "a sticking guide, a dry ball screw, a crash.")));
                _recordRows.Add(LimitRow("overload_percent", T("Rec_OverloadPercent", "Overload: effective load over (%)"), MachineRecords.DefaultOverloadPercent, 50, 300,
                    T("Rec_OverloadPercentTip", "The motion module's effective load torque (Md.110), % of the rated.")));
                _recordRows.Add(LimitRow("overload_seconds", T("Rec_OverloadSeconds", "Overload: for at least (s)"), MachineRecords.DefaultOverloadSeconds, 1, 120,
                    T("Rec_OverloadSecondsTip", "How long the effective load must stay over its limit.")));
                _recordRows.Add(LimitRow("peak_percent", T("Rec_PeakPercent", "Overload: peak torque over (%)"), MachineRecords.DefaultPeakPercent, 100, 500,
                    T("Rec_PeakPercentTip", "The motion module's peak torque ratio (Md.111), % of the rated - at once.")));
                // the inverters
                _recordRows.Add(KindRow(MachineRecords.InverterFault,
                    T("Rec_InverterFaultTip", "An inverter's fault: the fault, its current and value then, how long the motor had been running.")));
                _recordRows.Add(KindRow(MachineRecords.HighMotorLoad,
                    T("Rec_HighMotorLoadTip", "A run's peak current over the motor's normal peak by more than the limit - a dull saw blade or tool, " +
                      "a tight bearing. Needs the inverter's current word (its unit's CurrentAddress).")));
                _recordRows.Add(LimitRow("load_percent", T("Rec_LoadPercent", "High load: over its normal peak by (%)"), MachineRecords.DefaultLoadPercent, 10, 500,
                    T("Rec_LoadPercentTip", "How much higher than its normal peak current a run's peak must be to be recorded.")));
                _recordRows.Add(KindRow(MachineRecords.MotorLoads,
                    T("Rec_MotorLoadsTip", "At the end of each shift: each motor's runs, its average and highest peak current, its normal.")));
                rows.AddRange(_recordRows);
                _onFeatures = (s, e) =>
                {
                    try { if (IsHandleCreated && InvokeRequired) BeginInvoke((Action)ShowRecords); else ShowRecords(); } catch { }
                };
                HmiFeatures.Changed += _onFeatures;

                _on.CheckedChanged += (s, e) => Keep();
                _km.CheckedChanged += (s, e) => Keep();
                _kmConnection.Leave += (s, e) => Keep();
                _kmConnection.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Keep(); } };
                _clock.Tick += (s, e) => ShowState();
                Fill();
            }
            foreach (var r in rows) Controls.Add(r);
            // one under the other, as tall as they need (the logger's state may take two lines)
            Layout += (s, e) =>
            {
                int y = 0;
                foreach (var r in rows)
                {
                    if (!r.Visible) continue;
                    r.SetBounds(0, y, Width, r.Height);
                    y += r.Height;
                }
                if (Height != y) Height = y;
            };
            VisibleChanged += (s, e) => { if (Visible && _machine != null) { ShowState(); _clock.Start(); } else _clock.Stop(); };
            Disposed += (s, e) => { _clock.Dispose(); if (_onFeatures != null) HmiFeatures.Changed -= _onFeatures; };
            ShowRecords();
        }

        // ---- what the HMI takes itself -------------------------------------------------------------------

        /// <summary>A record's switch row (on until switched off), kept at once.</summary>
        private CheckBoxHMI KindRow(int code, string tip)
        {
            var kind = MachineRecords.KindOf(code);
            var box = new CheckBoxHMI { Name = "rec:" + kind.Key };
            Switch(box, kind.Shown, tip);
            _kinds[kind.Key] = box;
            box.CheckedChanged += (s, e) =>
            {
                if (_filling || _machine == null) return;
                bool on = box.Checked;
                _machine.Change(c => c.SetRecordOn(kind.Key, on));
                AuditLogger.Log("Machine records (" + _machine.ModuleId + "): " + kind.Text + " " + (on ? "taken" : "not taken") + ".");
            };
            return box;
        }

        /// <summary>A limit's row: its caption at the left, the number at the right end - kept as it is changed.</summary>
        private SettingRow LimitRow(string key, string caption, double fallback, int min, int max, string tip)
        {
            var n = new NumericUpDown { Name = "lim:" + key, Width = 80, Minimum = min, Maximum = max, Tag = fallback };
            _limits[key] = n;
            n.ValueChanged += (s, e) =>
            {
                if (_filling || _machine == null) return;
                double v = (double)n.Value;
                _machine.Change(c => c.SetRecordLimit(key, v));
                AuditLogger.Log("Machine records (" + _machine.ModuleId + "): " + key + " = " + v.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
            };
            var row = new SettingRow(caption, n);
            UiStyle.SetTip(_tips, row, tip);
            UiStyle.SetTip(_tips, n, tip);
            return row;
        }

        /// <summary>The HMI's records shown only for a new machine (HMI Settings > Options).</summary>
        private void ShowRecords()
        {
            if (IsDisposed || _machine == null) return;
            bool on = HmiFeatures.RecordsOn;
            foreach (var r in _recordRows) r.Visible = on;
            ShowNormals();
            PerformLayout();
        }

        private void ShowNormals()
        {
            if (_machine == null) return;
            int n = MachineRecords.Normals.CountOf(_machine.ModuleId);
            _normals.Text = n == 0 ? T("Rec_NormalsNone", "No normal time learned yet: each movement's comes with its first 20.")
                                   : string.Format(T("Rec_Normals", "Normal times learned: {0}."), n);
        }

        /// <summary>After a repair: the machine's normal times forgotten, learned again from the next movements.</summary>
        public void LearnAgain()
        {
            if (_machine == null) return;
            int n = MachineRecords.Normals.Forget(_machine.ModuleId);
            AuditLogger.Log("Machine records (" + _machine.ModuleId + "): the normal times (" + n + ") to be learned again.");
            ShowNormals();
        }

        /// <summary>A record's switch (tests).</summary>
        public CheckBoxHMI RecordSwitch(string kind) { CheckBoxHMI b; return _kinds.TryGetValue(kind, out b) ? b : null; }

        /// <summary>A limit's field (tests).</summary>
        public NumericUpDown LimitField(string key) { NumericUpDown n; return _limits.TryGetValue(key, out n) ? n : null; }

        /// <summary>The line under the button (tests).</summary>
        public string NormalsText { get { return _normals.Text; } }

        /// <summary>A switch row of its own (no PLC bit: a setting of the HMI's).</summary>
        private void Switch(CheckBoxHMI box, string text, string tip)
        {
            box.Size = new Size(RowW, UiStyle.SettingRowHeight);
            box.DescriptionText = text;
            UiStyle.SetTip(_tips, box, tip);
        }

        /// <summary>Words as a row: from the rows' left, wrapped, as tall as they are.</summary>
        private Label Words(Label l)
        {
            l.AutoSize = true;
            l.MaximumSize = new Size(RowW - 16, 0);
            l.Padding = new Padding(8, 6, 8, 6);
            return l;
        }

        /// <summary>
        /// A field's row: its caption at the left, the field at the right end (8 in), a light line under it - as the Machine Settings
        /// pages' value rows.
        /// </summary>
        private sealed class SettingRow : Panel
        {
            private readonly Label _caption;
            private readonly Control _field;

            public SettingRow(string caption, Control field, bool fieldAtLeft = false)
            {
                _field = field;
                Size = new Size(RowW, UiStyle.SettingRowHeight + (fieldAtLeft ? 6 : 0));
                BackColor = Color.White;
                _caption = new Label { Text = caption, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
                Controls.Add(_caption);
                Controls.Add(field);
                Layout += (s, e) =>
                {
                    int pad = UiScale.For(this, 8);
                    if (fieldAtLeft)
                    {
                        // a button: at the rows' left, as wide as its words
                        if (_field is Button) _field.Width = Math.Max(_field.Width, TextRenderer.MeasureText(_field.Text, _field.Font).Width + UiScale.For(this, 28));
                        _field.Location = new Point(pad, (Height - _field.Height) / 2);
                        _caption.SetBounds(_field.Right + pad, 0, Math.Max(10, Width - _field.Right - 2 * pad), Height - 1);
                        return;
                    }
                    _field.Location = new Point(Width - pad - _field.Width, (Height - _field.Height) / 2);
                    _caption.SetBounds(pad, 0, Math.Max(10, _field.Left - 2 * pad), Height - 1);
                };
                Paint += (s, e) => { using (var pen = new Pen(UiStyle.RowRule)) e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1); };
            }
        }

        /// <summary>
        /// Shown only while the HMI does the Industry 4 logging (HMI Settings > Options) - the frame of these settings and of the
        /// records' switches (the Industry 4 service sets the PLC's bits itself otherwise).
        /// </summary>
        public static void ShownWhileLogging(Control c)
        {
            if (c == null) return;
            EventHandler follow = (s, e) =>
            {
                try
                {
                    if (c.IsDisposed) return;
                    if (c.IsHandleCreated && c.InvokeRequired) c.BeginInvoke((Action)(() => c.Visible = HmiFeatures.LoggingOn));
                    else c.Visible = HmiFeatures.LoggingOn;
                }
                catch { }
            };
            HmiFeatures.Changed += follow;
            c.Disposed += (s, e) => HmiFeatures.Changed -= follow;
            follow(null, EventArgs.Empty);
        }

        /// <summary>The fields from the machine's settings.</summary>
        public void Fill()
        {
            if (_machine == null) return;
            _filling = true;
            try
            {
                var c = _machine.Config;
                _on.Checked = c.PlcLogging;
                _km.Checked = c.KmSystemLookup;
                _kmConnection.Text = c.KmSystemConnection;
                foreach (var kv in _kinds) kv.Value.Checked = c.RecordOn(kv.Key);
                foreach (var kv in _limits)
                {
                    double v = c.RecordLimit(kv.Key, (double)kv.Value.Tag);
                    kv.Value.Value = Math.Max(kv.Value.Minimum, Math.Min(kv.Value.Maximum, (decimal)v));
                }
            }
            finally { _filling = false; }
            ShowState();
        }

        private void ShowState()
        {
            if (_machine == null) return;
            var l = MachineLoggers.Of(_machine.ModuleId);
            _state.Text = T("Plg_State", "Now") + ": " + (l != null ? l.Status : T("Plg_NotStarted", "Not running in this program (it starts with the main window)."));
        }

        /// <summary>What the logger does now (tests).</summary>
        public string StateText { get { return _state.Text; } }

        /// <summary>A change kept at once.</summary>
        private void Keep()
        {
            if (_filling || _machine == null) return;
            Save();
            ShowState();
        }

        /// <summary>The fields into the machine's settings, written (when they changed).</summary>
        public void Save()
        {
            if (_machine == null) return;
            bool on = _on.Checked, km = _km.Checked;
            string connection = (_kmConnection.Text ?? "").Trim();
            var c0 = _machine.Config;
            if (c0.PlcLogging == on && c0.KmSystemLookup == km && c0.KmSystemConnection == connection) return;
            _machine.Change(c =>
            {
                c.PlcLogging = on;
                c.KmSystemLookup = km;
                c.KmSystemConnection = connection;
            });
            AuditLogger.Log("Machine log (" + _machine.ModuleId + "): " + (on ? "taken" : "not taken") + " from its PLC (log area " +
                            MachineLogger.Area(_machine) + "), KMSystem lookup " + (km ? "on" : "off") + ".");
        }
    }
}
