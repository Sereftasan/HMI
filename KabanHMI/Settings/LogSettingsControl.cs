using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
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
    /// LOG SETTINGS: what an HMI audit log needs day to day -
    ///   - ON/OFF master switch (commissioning sometimes needs silence),
    ///   - retention in days (old daily files are deleted automatically),
    ///   - where the files live + one-click open,
    ///   - export of today's file for support/e-mail,
    ///   - a LIVE viewer of the latest entries so [PLC DIAG] lines and user
    ///     actions can be read on the machine without hunting for files.
    /// Settings persist across restarts.
    /// </summary>
    public class LogSettingsControl : UserControl
    {
        private readonly CheckBox _chkEnabled = new CheckBox();
        private readonly NumericUpDown _numRetain = new NumericUpDown();
        private readonly TextBox _txtView = new TextBox();
        private readonly Timer _refresh = new Timer { Interval = 2000 };
        private readonly Label _lblStatus = new Label();

        public LogSettingsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            var pn = new Panel { Dock = DockStyle.Top, Height = 118, Padding = new Padding(16) };

            _chkEnabled.Text = LanguageManager.GetString("Log_Enabled", "Audit logging enabled");
            _chkEnabled.Left = 16; _chkEnabled.Top = 12; _chkEnabled.Width = 260;
            _chkEnabled.Checked = AuditLogger.Enabled;
            _chkEnabled.CheckedChanged += (s, e) => { AuditLogger.Enabled = _chkEnabled.Checked; LogSettingsStore.Save(); };
            pn.Controls.Add(_chkEnabled);

            pn.Controls.Add(new Label { Text = LanguageManager.GetString("Log_Retain", "Keep logs (days)") + ":", Left = 16, Top = 44, Width = 160, Font = new Font("Segoe UI", 9F) });
            _numRetain.Left = 180; _numRetain.Top = 42; _numRetain.Width = 70;
            _numRetain.Minimum = 1; _numRetain.Maximum = 3650; _numRetain.Value = Math.Min(3650, Math.Max(1, AuditLogger.RetainDays));
            _numRetain.ValueChanged += (s, e) => { AuditLogger.RetainDays = (int)_numRetain.Value; LogSettingsStore.Save(); };
            pn.Controls.Add(_numRetain);

            var tips = Kaban.UI.Controls.UiStyle.Tips();
            Func<string, string, int, Button> tool = (emoji, tip, left) =>
            {
                var b = new Button { Text = emoji, Left = left, Top = 74, Width = 44, Height = 32, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12F) };
                b.FlatAppearance.BorderColor = Color.FromArgb(180, 186, 194);
                tips.SetToolTip(b, tip);
                pn.Controls.Add(b);
                return b;
            };
            tool("\uD83D\uDCC2", "Open the log folder", 16).Click += (s, e) =>
            { try { Process.Start("explorer.exe", AuditLogger.LogDirectory); } catch { } };
            tool("\uD83D\uDCBE", "Export today's log file", 66).Click += (s, e) => ExportToday();
            tool("\uD83D\uDD04", "Refresh the view now", 116).Click += (s, e) => RefreshView();

            _txtView.Dock = DockStyle.Fill;
            _txtView.Multiline = true;
            _txtView.ReadOnly = true;
            _txtView.ScrollBars = ScrollBars.Both;
            _txtView.WordWrap = false;
            _txtView.Font = new Font("Consolas", 8.5F);
            _txtView.BackColor = Color.FromArgb(28, 30, 34);
            _txtView.ForeColor = Color.FromArgb(190, 230, 190);

            _lblStatus.Dock = DockStyle.Bottom;
            _lblStatus.Height = 24;
            _lblStatus.Font = new Font("Segoe UI", 9F);
            _lblStatus.Padding = new Padding(8, 2, 0, 0);
            _lblStatus.Text = AuditLogger.LogDirectory;

            Controls.Add(_txtView);
            Controls.Add(_lblStatus);
            Controls.Add(pn);

            _refresh.Tick += (s, e) => RefreshView();
            VisibleChanged += (s, e) => { _refresh.Enabled = Visible; if (Visible) RefreshView(); };
            RefreshView();
        }

        private void RefreshView()
        {
            var lines = AuditLogger.Tail(200);
            string text = string.Join(Environment.NewLine, lines);
            if (_txtView.Text != text)
            {
                _txtView.Text = text;
                _txtView.SelectionStart = _txtView.TextLength;
                _txtView.ScrollToCaret();
            }
        }

        private void ExportToday()
        {
            string src = Path.Combine(AuditLogger.LogDirectory, DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
            if (!File.Exists(src)) { _lblStatus.Text = "No log file for today yet."; return; }
            using (var dlg = new SaveFileDialog { Filter = "Log file (*.txt)|*.txt", FileName = "KabanHMI-" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt" })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                File.Copy(src, dlg.FileName, true);
                _lblStatus.Text = "Exported: " + dlg.FileName;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _refresh.Stop(); _refresh.Dispose(); }
            base.Dispose(disposing);
        }
    }
}

