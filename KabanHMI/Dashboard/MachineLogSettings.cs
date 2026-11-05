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
    /// this machine's log from its PLC, its PLC log area (empty: the machine's own), what the logger does now, and the KMSystem
    /// lookup of a record's Process ID with its connection. The page has no Save: each is kept as it is changed - a switch at
    /// once, a field when it is left or Enter is pressed (a log area that is no word device is not kept, said under it).
    /// Kept in the machine's dashboard settings (Logs and Reports keeps its database and reports).
    ///
    /// Its frame is shown only while HMI Settings > Options has the Industry 4 logging in the HMI on (ShownWhileLogging).
    /// </summary>
    public sealed class MachineLogSettings : Panel
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        /// <summary>A row: as the Options page's (450 x SettingRowHeight).</summary>
        public const int RowW = 450;

        private readonly MachineDashboard _machine;
        private readonly ToolTip _tips = UiStyle.Tips();
        private readonly CheckBoxHMI _on = new CheckBoxHMI { Name = "plcLogging" };
        private readonly TextBox _area = new TextBox { Name = "logArea", Width = 110 };
        private readonly CheckBoxHMI _km = new CheckBoxHMI { Name = "kmLookup" };
        private readonly TextBox _kmConnection = new TextBox { Name = "kmConnection", Width = 250 };
        private readonly Label _state = new Label { Name = "loggerState" };
        private readonly Label _problem = new Label { Name = "logProblem" };
        private readonly Label _none = new Label { Name = "noLogDatabase" };
        private readonly Timer _clock = new Timer { Interval = 1000 };
        private bool _filling;

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
                rows.Add(new SettingRow(T("Plg_Area", "PLC log area"), _area));
                UiStyle.SetTip(_tips, _area, T("Plg_AreaTip",
                    "The Industry 4 service's device group and reference device (R27000): its first word the records waiting, the next the HMI's answer, the records from its tenth word. Empty: the machine's own."));
                _state.ForeColor = Color.FromArgb(30, 100, 190);
                rows.Add(Words(_state));
                Switch(_km, T("Plg_KmRow", "Process ID: its profile from KMSystem"),
                       T("Plg_Km", "A record's Process ID: its Profile Code and Profile Data from KMSystem (PostgreSQL)") + "\n" +
                       T("Plg_KmNote", "Needs Npgsql.dll beside the program, as the Industry 4 service had it."));
                rows.Add(_km);
                rows.Add(new SettingRow(T("Plg_KmConnection", "KMSystem connection"), _kmConnection));
                _problem.ForeColor = Color.Firebrick;
                _problem.Visible = false;
                rows.Add(Words(_problem));

                _on.CheckedChanged += (s, e) => Keep();
                _km.CheckedChanged += (s, e) => Keep();
                foreach (var box in new[] { _area, _kmConnection })
                {
                    box.Leave += (s, e) => Keep();
                    box.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Keep(); } };
                }
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
            Disposed += (s, e) => _clock.Dispose();
        }

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

            public SettingRow(string caption, Control field)
            {
                _field = field;
                Size = new Size(RowW, UiStyle.SettingRowHeight);
                BackColor = Color.White;
                _caption = new Label { Text = caption, AutoSize = false, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
                Controls.Add(_caption);
                Controls.Add(field);
                Layout += (s, e) =>
                {
                    int pad = UiScale.For(this, 8);
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
                _area.Text = MachineLogger.Area(c, _machine);
                _km.Checked = c.KmSystemLookup;
                _kmConnection.Text = c.KmSystemConnection;
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

        /// <summary>Why the last change was not kept ("" when it was) - tests.</summary>
        public string ProblemText { get { return _problem.Visible ? _problem.Text : ""; } }

        /// <summary>A change kept at once: written, or why not said under the rows.</summary>
        private void Keep()
        {
            if (_filling || _machine == null) return;
            string problem;
            bool ok = Save(out problem);
            _problem.Text = problem;
            _problem.Visible = !ok;
            PerformLayout();
            ShowState();
        }

        /// <summary>
        /// The fields into the machine's settings, written. False, and why, when the area is no word device - nothing is
        /// written then.
        /// </summary>
        public bool Save(out string problem)
        {
            problem = "";
            if (_machine == null) return true;
            string area = (_area.Text ?? "").Trim().ToUpperInvariant();
            string prefix;
            long number;
            if (area.Length > 0 && !(PlcAddressHelper.TrySplit(area, out prefix, out number) && !PlcAddressHelper.IsBitDevice(area)))
            {
                problem = T("Plg_BadArea", "The PLC log area is a word device (R27000, D7000):") + " \"" + area + "\".";
                return false;
            }
            bool on = _on.Checked, km = _km.Checked;
            string connection = (_kmConnection.Text ?? "").Trim();
            string kept = area == (_machine.LogArea ?? "").ToUpperInvariant() ? "" : area;   // the machine's own: follows the machine
            var c0 = _machine.Config;
            if (c0.PlcLogging == on && c0.PlcLogArea == kept && c0.KmSystemLookup == km && c0.KmSystemConnection == connection) return true;
            _machine.Change(c =>
            {
                c.PlcLogging = on;
                c.PlcLogArea = kept;
                c.KmSystemLookup = km;
                c.KmSystemConnection = connection;
            });
            AuditLogger.Log("Machine log (" + _machine.ModuleId + "): " + (on ? "taken" : "not taken") + " from its PLC, log area " +
                            (kept.Length > 0 ? kept : MachineLogger.Area(_machine.Config, _machine) + " (the machine's own)") +
                            ", KMSystem lookup " + (km ? "on" : "off") + ".");
            return true;
        }
    }
}
