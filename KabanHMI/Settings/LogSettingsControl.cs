using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Settings
{
    /// <summary>Persists the audit-log options under CommonAppData\KabanHMI.</summary>
    public static class LogSettingsStore
    {
        private static readonly string Path0 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "LogSettings.cfg");

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path0));
                File.WriteAllText(Path0, "Enabled=" + AuditLogger.Enabled + "\nRetainDays=" + AuditLogger.RetainDays + "\n");
            }
            catch { }
        }

        /// <summary>Loads stored options into AuditLogger (call at startup).</summary>
        public static void Apply()
        {
            try
            {
                if (!File.Exists(Path0)) return;
                foreach (var line in File.ReadAllLines(Path0))
                {
                    var kv = line.Split('=');
                    if (kv.Length != 2) continue;
                    if (kv[0] == "Enabled") AuditLogger.Enabled = kv[1].Trim() == "True";
                    if (kv[0] == "RetainDays") { int d; if (int.TryParse(kv[1], out d)) AuditLogger.RetainDays = Math.Max(1, d); }
                }
            }
            catch { }
        }
    }

    /// <summary>
    /// LOG SETTINGS: the audit log's switches, and the log itself to read.
    ///
    ///   * On/off and how many days are kept - ADMIN ONLY. This is the record of
    ///     who did what on the machine; a page open to Maintenance let the people
    ///     it records switch it off, silently and for good (the choice is saved).
    ///     Now only an Admin can, after a confirmation, and the switching itself
    ///     is written to the log; while it is off the page says so in red.
    ///   * The log folder, one press away.
    ///   * The log, in a table that can be narrowed by time, words, user and kind
    ///     (LogViewerControl) - it was the last 200 raw lines of today's file.
    /// Settings persist across restarts (LogSettingsStore).
    ///
    /// LOGS AND REPORTS (2 October): the HMI's log and the machines' log databases
    /// are one subject, set here - SETTINGS ONLY: the HMI log's switches, then a
    /// machine to choose (MachineLinks) and its log database and reports (the
    /// dashboard's settings, once a Machine Parameters tab and then a window). The
    /// logs themselves are read on the Dashboard page, in its tabs; only a program
    /// whose machines keep no log database shows the HMI's log here.
    /// </summary>
    public class LogSettingsControl : UserControl
    {
        private readonly CheckBox _chkEnabled = new CheckBox();
        private readonly NumericUpDown _numRetain = new NumericUpDown();
        private readonly Label _off = new Label();
        private readonly Label _adminNote = new Label();
        private readonly Timer _retainLog = new Timer { Interval = 1500 };
        private readonly LogViewerControl _viewer;      // only when no machine keeps a log database
        private readonly EventHandler _onUserChanged;
        private bool _setting;

        // ---- the machines' logs
        private readonly FlowLayoutPanel _machineRow = new FlowLayoutPanel();
        private readonly ComboBox _machine = new ComboBox();
        private readonly List<Kaban.UI.Helpers.MachineLink> _logMachines;

        public LogSettingsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            var tips = Kaban.UI.Controls.UiStyle.Tips();

            var pn = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Padding = new Padding(0, 0, 0, 4),
                BackColor = Color.White
            };

            _chkEnabled.Text = LanguageManager.GetString("Log_Enabled", "Audit logging enabled");
            _chkEnabled.AutoSize = true;
            _chkEnabled.Margin = new Padding(2, 9, 16, 3);
            _chkEnabled.Font = Kaban.UI.Controls.UiStyle.Body;
            _chkEnabled.Checked = AuditLogger.Enabled;
            _chkEnabled.CheckedChanged += (s, e) => OnEnabledChanged();
            pn.Controls.Add(_chkEnabled);

            pn.Controls.Add(new Label
            {
                Text = LanguageManager.GetString("Log_Retain", "Keep logs (days)") + ":",
                AutoSize = true, Margin = new Padding(2, 10, 4, 3), Font = Kaban.UI.Controls.UiStyle.Body
            });
            _numRetain.Width = 70;
            _numRetain.Margin = new Padding(2, 6, 12, 3);
            _numRetain.Minimum = 1; _numRetain.Maximum = 3650;
            _numRetain.Value = Math.Min(3650, Math.Max(1, AuditLogger.RetainDays));
            _numRetain.ValueChanged += (s, e) =>
            {
                if (_setting) return;
                AuditLogger.RetainDays = (int)_numRetain.Value;
                LogSettingsStore.Save();
                // Written once the value has settled - not once per arrow press.
                _retainLog.Stop(); _retainLog.Start();
            };
            _retainLog.Tick += (s, e) =>
            {
                _retainLog.Stop();
                AuditLogger.Log("set the audit log to keep " + AuditLogger.RetainDays + " days.");
            };
            pn.Controls.Add(_numRetain);

            var folder = Kaban.UI.Controls.UiStyle.ToolButton("\uD83D\uDCC2",
                LanguageManager.GetString("Log_OpenFolder", "Open the log folder") + "\n" + AuditLogger.LogDirectory, tips,
                (s, e) => { try { Process.Start("explorer.exe", AuditLogger.LogDirectory); } catch { } });
            folder.Margin = new Padding(2, 1, 8, 1);
            pn.Controls.Add(folder);

            _adminNote.AutoSize = true;
            _adminNote.Margin = new Padding(4, 10, 3, 3);
            _adminNote.ForeColor = Color.FromArgb(120, 128, 138);
            _adminNote.Text = LanguageManager.GetString("Log_AdminOnly", "Only an Admin can change the log settings.");
            pn.Controls.Add(_adminNote);

            _off.Dock = DockStyle.Top;
            _off.Height = 30;
            _off.TextAlign = ContentAlignment.MiddleLeft;
            _off.Padding = new Padding(8, 0, 0, 0);
            _off.BackColor = Color.FromArgb(253, 226, 226);
            _off.ForeColor = Color.FromArgb(160, 20, 20);
            _off.Font = Kaban.UI.Controls.UiStyle.FontVariant(Kaban.UI.Controls.UiStyle.Body, FontStyle.Bold);
            _off.Text = LanguageManager.GetString("Log_Off", "Audit logging is OFF - nothing is being recorded.");

            // ---- each machine's log database and reports: the dashboard's settings ----
            // SETTINGS ONLY (2 October): the logs themselves are read on the Dashboard
            // page (its log tabs); this page sets how they are kept.
            _logMachines = Kaban.UI.Helpers.MachineLinks.WithLogs();
            _machineRow.Dock = DockStyle.Top;
            _machineRow.AutoSize = true;
            _machineRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _machineRow.WrapContents = true;
            _machineRow.Padding = new Padding(0, 6, 0, 2);
            _machineRow.BackColor = Color.FromArgb(246, 248, 251);
            _machineRow.Controls.Add(new Label
            {
                Text = "\U0001F5C4\uFE0F " + LanguageManager.GetString("Log_MachineSettingsTitle", "Log database and reports") + ":",
                AutoSize = true, Margin = new Padding(6, 8, 6, 3),
                Font = Kaban.UI.Controls.UiStyle.FontVariant(Kaban.UI.Controls.UiStyle.Body, FontStyle.Bold)
            });
            _machine.DropDownStyle = ComboBoxStyle.DropDownList;
            _machine.Width = 220;
            _machine.Margin = new Padding(2, 4, 8, 3);
            foreach (var m in _logMachines) _machine.Items.Add(m);
            _machine.SelectedIndexChanged += (s, e) => ShowMachineSettings();
            _machineRow.Controls.Add(_machine);
            _machineRow.Visible = _logMachines.Count > 0;

            _machinePanel.Dock = DockStyle.Fill;
            _machinePanel.BackColor = Color.White;

            var gap = new Panel { Dock = DockStyle.Top, Height = 6, BackColor = Color.White };
            if (_logMachines.Count > 0)
            {
                Controls.Add(_machinePanel);
                Controls.Add(_machineRow);
                Controls.Add(gap);
                if (_machine.Items.Count > 0) _machine.SelectedIndex = 0;
            }
            else
            {
                // No machine keeps a log database: the HMI's log is read here (there is no
                // Dashboard page to read it on).
                _viewer = new LogViewerControl(null, LogViewerControl.Period.Today);
                Controls.Add(_viewer);
                Controls.Add(gap);
            }
            Controls.Add(_off);
            Controls.Add(pn);

            _onUserChanged = (s, e) => ApplyAccess();
            SecurityManager.UserChanged += _onUserChanged;
            ApplyAccess();
        }

        private readonly Panel _machinePanel = new Panel();
        private Kaban.UI.Dashboard.DashboardSettingsControl _machineSettings;

        /// <summary>The chosen machine, or null.</summary>
        private Kaban.UI.Helpers.MachineLink ChosenMachine { get { return _machine.SelectedItem as Kaban.UI.Helpers.MachineLink; } }

        /// <summary>The chosen machine's log database and reports (the dashboard's settings), on the page.</summary>
        private void ShowMachineSettings()
        {
            var m = ChosenMachine;
            if (_machineSettings != null) { _machinePanel.Controls.Remove(_machineSettings); _machineSettings.Dispose(); _machineSettings = null; }
            if (m == null || m.Dashboard == null) return;
            _machineSettings = new Kaban.UI.Dashboard.DashboardSettingsControl(m.Dashboard) { Dock = DockStyle.Fill };
            _machinePanel.Controls.Add(_machineSettings);
        }

        /// <summary>The chosen machine's settings on the page (tests).</summary>
        public Kaban.UI.Dashboard.DashboardSettingsControl MachineSettings { get { return _machineSettings; } }
        public ComboBox MachineBox { get { return _machine; } }
        /// <summary>The HMI's log - only on a program whose machines keep no log database.</summary>
        public LogViewerControl Viewer { get { return _viewer; } }

        /// <summary>The switches are an Admin's; everyone who may open the page may read the log.</summary>
        private void ApplyAccess()
        {
            bool admin = SecurityManager.HasAccess(SecurityLevel.Admin);
            _chkEnabled.Enabled = admin;
            _numRetain.Enabled = admin;
            _adminNote.Visible = !admin;
            _off.Visible = !AuditLogger.Enabled;
        }

        private void OnEnabledChanged()
        {
            if (_setting) return;
            if (!SecurityManager.HasAccess(SecurityLevel.Admin)) { Revert(); return; }

            if (!_chkEnabled.Checked)
            {
                var answer = MessageBox.Show(FindForm(),
                    LanguageManager.GetString("Log_ConfirmOff",
                        "Switch audit logging off?\r\n\r\nNothing will be recorded - sign-ins, values written to the " +
                        "controllers, connection losses - until an Admin switches it on again."),
                    LanguageManager.GetString("Set_Logs", "Log Settings"),
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes) { Revert(); return; }
                // Written BEFORE it goes quiet: the last entry says who silenced it.
                AuditLogger.Log("switched audit logging OFF.");
                AuditLogger.Enabled = false;
            }
            else
            {
                AuditLogger.Enabled = true;
                AuditLogger.Log("switched audit logging ON.");
            }
            LogSettingsStore.Save();
            ApplyAccess();
        }

        private void Revert()
        {
            _setting = true;
            try { _chkEnabled.Checked = AuditLogger.Enabled; }
            finally { _setting = false; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SecurityManager.UserChanged -= _onUserChanged;
                _retainLog.Stop(); _retainLog.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
