using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// A MACHINE'S LOG FROM ITS PLC, on its Machine Parameters > Alarms and Logs (30 October: "'R27000'
    /// logging device is configured in a JSON file, different machines have different device addresses
    /// in the PLC so we should create a setting inside 'Machine Parameters' for each machine as well btw
    /// (visible when HMI industry 4 logging activated). We can change the alarms setting page name to
    /// 'Alarms and Logs' and put the setting on the page").
    ///
    /// Shown only while HMI Settings > Features has the Industry 4 logging in the HMI on: whether this
    /// machine's log is taken, its PLC log area (empty: the machine's own), the KMSystem lookup of a
    /// record's Process ID, and what its logger does now. Kept in the machine's dashboard settings
    /// (Logs and Reports keeps its database and reports) - the page's Save writes them.
    /// </summary>
    public sealed class MachineLogSettings : FlowLayoutPanel
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private readonly MachineDashboard _machine;
        private readonly ToolTip _tips = UiStyle.Tips();
        private readonly CheckBox _on = new CheckBox { Name = "plcLogging" };
        private readonly TextBox _area = new TextBox { Name = "logArea", Width = 110 };
        private readonly CheckBox _km = new CheckBox { Name = "kmLookup" };
        private readonly TextBox _kmConnection = new TextBox { Name = "kmConnection", Width = 460 };
        private readonly Label _state = new Label { Name = "loggerState" };
        private readonly Label _none = new Label { Name = "noLogDatabase" };
        private readonly Timer _clock = new Timer { Interval = 1000 };
        private readonly EventHandler _onFeatures;
        private bool _filling;

        /// <summary>A field changed (the page marks itself to be saved).</summary>
        public event EventHandler Edited;

        /// <summary>The machine's dashboard (null: the machine keeps no log database).</summary>
        public MachineDashboard Machine { get { return _machine; } }

        public MachineLogSettings(string moduleId)
        {
            _machine = string.IsNullOrEmpty(moduleId) ? null : DashboardService.Find(moduleId);
            Name = "machineLog";
            Dock = DockStyle.Top;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;
            BackColor = Color.FromArgb(240, 246, 252);
            Padding = new Padding(8, 6, 8, 6);
            Font = UiStyle.Body;

            var head = new Label
            {
                Text = "\U0001F4E5  " + T("Plg_Head", "The machine's log from its PLC"), AutoSize = true,
                Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), Margin = new Padding(2, 2, 2, 4)
            };
            Controls.Add(head);

            if (_machine == null)
            {
                _none.Text = T("Plg_NoDatabase", "This machine keeps no log database (HMI Settings > Logs and Reports): nothing is taken from its PLC.");
                _none.AutoSize = true;
                _none.ForeColor = Color.FromArgb(110, 116, 124);
                Controls.Add(_none);
            }
            else
            {
                _on.Text = T("Plg_Switch", "The HMI takes the machine's states, events and alarms from its PLC into its log database");
                _on.AutoSize = true;
                _on.Margin = new Padding(2, 2, 2, 2);
                Controls.Add(_on);

                var row = Row();
                row.Controls.Add(Caption(T("Plg_Area", "PLC log area")));
                row.Controls.Add(_area);
                UiStyle.SetTip(_tips, _area, T("Plg_AreaTip",
                    "The Industry 4 service's device group and reference device (R27000): its first word the records waiting, the next the HMI's answer, the records from its tenth word. Empty: the machine's own."));
                row.Controls.Add(Caption(T("Plg_State", "Now")));
                _state.AutoSize = true;
                _state.MaximumSize = new Size(UiScale.Px(760), 0);
                _state.ForeColor = Color.FromArgb(30, 100, 190);
                _state.Margin = new Padding(2, 6, 2, 2);
                row.Controls.Add(_state);
                Controls.Add(row);

                var km = Row();
                _km.Text = T("Plg_Km", "A record's Process ID: its Profile Code and Profile Data from KMSystem (PostgreSQL)");
                _km.AutoSize = true;
                _km.Margin = new Padding(2, 5, 12, 2);
                km.Controls.Add(_km);
                km.Controls.Add(Caption(T("Plg_KmConnection", "KMSystem connection")));
                km.Controls.Add(_kmConnection);
                Controls.Add(km);
                Controls.Add(new Label
                {
                    Text = T("Plg_KmNote", "Needs Npgsql.dll beside the program, as the Industry 4 service had it."),
                    AutoSize = true, ForeColor = Color.FromArgb(110, 116, 124), Margin = new Padding(2, 0, 2, 2)
                });

                EventHandler edited = (s, e) => { if (!_filling) { var h = Edited; if (h != null) h(this, EventArgs.Empty); } };
                _on.CheckedChanged += edited;
                _area.TextChanged += edited;
                _km.CheckedChanged += edited;
                _kmConnection.TextChanged += edited;
                _clock.Tick += (s, e) => ShowState();
                Fill();
            }

            _onFeatures = (s, e) => { try { if (IsHandleCreated && InvokeRequired) BeginInvoke((Action)Follow); else Follow(); } catch { } };
            HmiFeatures.Changed += _onFeatures;
            VisibleChanged += (s, e) => { if (Visible && _machine != null) { ShowState(); _clock.Start(); } else _clock.Stop(); };
            Disposed += (s, e) => { HmiFeatures.Changed -= _onFeatures; _clock.Dispose(); };
            Follow();
        }

        private static FlowLayoutPanel Row()
        {
            return new FlowLayoutPanel
            {
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false,
                Margin = new Padding(0, 1, 0, 1), BackColor = Color.Transparent
            };
        }

        private static Label Caption(string text)
        {
            return new Label { Text = text + ":", AutoSize = true, Margin = new Padding(14, 6, 4, 2) };
        }

        /// <summary>Shown only while the HMI does the logging (HMI Settings > Features).</summary>
        private void Follow()
        {
            if (IsDisposed) return;
            Visible = HmiFeatures.LoggingOn;
        }

        /// <summary>The fields from the machine's settings (the page's ↺ too).</summary>
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
            _state.Text = l != null ? l.Status : T("Plg_NotStarted", "Not running in this program (it starts with the main window).");
        }

        /// <summary>What the logger does now (tests).</summary>
        public string StateText { get { return _state.Text; } }

        /// <summary>
        /// The fields into the machine's settings, written (the page's Save). False, and why, when the
        /// area is no word device - nothing is written then.
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
