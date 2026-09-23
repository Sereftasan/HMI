using System;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// PLC DIAGNOSTICS (admin): answers "why will station 1 not connect?" ON the
    /// machine, without a debugger.
    ///   - the MX Component environment report (COM registration per bitness and
    ///     the logical stations actually stored in the registry),
    ///   - a single-PLC connection TEST showing the raw return code and its
    ///     meaning,
    ///   - a station SCAN that opens every logical station number in a range and
    ///     reports which ones answer - this finds the right number when the one
    ///     configured in the Communication Setting Utility is not the expected 1.
    /// The driver type is discovered from the registered station-1 driver, so this
    /// page works for any driver without the UI referencing it directly.
    /// </summary>
    public class PlcDiagnosticsControl : UserControl
    {
        private readonly TextBox _out = new TextBox();
        private readonly NumericUpDown _station = new NumericUpDown { Minimum = 0, Maximum = 1023, Value = 1, Width = 70 };
        private readonly NumericUpDown _scanTo = new NumericUpDown { Minimum = 0, Maximum = 1023, Value = 20, Width = 70 };

        public PlcDiagnosticsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 6, 8, 6) };
            var tips = Kaban.UI.Controls.UiStyle.Tips();

            bar.Controls.Add(new Label { Text = "Station:", AutoSize = true, Padding = new Padding(2, 9, 4, 0), Font = new Font("Segoe UI", 9F) });
            _station.TextAlign = HorizontalAlignment.Right;
            bar.Controls.Add(_station);
            bar.Controls.Add(UiStyle.ToolButton("\u25B6", "Test this logical station now", tips, (s, e) => TestStation()));

            bar.Controls.Add(new Label { Text = "Scan 0 -", AutoSize = true, Padding = new Padding(14, 9, 4, 0), Font = new Font("Segoe UI", 9F) });
            _scanTo.TextAlign = HorizontalAlignment.Right;
            bar.Controls.Add(_scanTo);
            bar.Controls.Add(UiStyle.ToolButton("\uD83D\uDD0D", "Try every logical station in the range and list the ones that answer", tips, (s, e) => ScanStations()));

            bar.Controls.Add(UiStyle.ToolButton("\uD83D\uDD04", "Refresh the environment report", tips, (s, e) => ShowEnvironment()));
            bar.Controls.Add(UiStyle.ToolButton("\uD83D\uDCCB", "Copy this report to the clipboard", tips, (s, e) =>
            { try { Clipboard.SetText(_out.Text); } catch { } }));

            _out.Dock = DockStyle.Fill;
            _out.Multiline = true;
            _out.ReadOnly = true;
            _out.ScrollBars = ScrollBars.Both;
            _out.WordWrap = false;
            _out.Font = new Font("Consolas", 9F);
            _out.BackColor = Color.FromArgb(28, 30, 34);
            _out.ForeColor = Color.FromArgb(190, 230, 190);

            Controls.Add(_out);
            Controls.Add(bar);
            ShowEnvironment();
        }

        private void Append(string text)
        {
            _out.Text += (_out.TextLength > 0 ? Environment.NewLine : "") + text;
            _out.SelectionStart = _out.TextLength;
            _out.ScrollToCaret();
        }

        private void ShowEnvironment()
        {
            _out.Text = "";
            Append("process: " + (Environment.Is64BitProcess ? "64-bit" : "32-bit") +
                   " on " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit") + " Windows");
            Append(FindDiagnosticsReport() ?? "(MX Component diagnostics not available in this build)");
        }

        /// <summary>Calls MxDiagnostics.BuildReport() if the driver assembly is loaded.</summary>
        private static string FindDiagnosticsReport()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("Kaban.Drivers.MitsubishiActUtl.MxDiagnostics");
                    if (t == null) continue;
                    var m = t.GetMethod("BuildReport", BindingFlags.Public | BindingFlags.Static);
                    if (m != null) return Convert.ToString(m.Invoke(null, null));
                }
            }
            catch (Exception ex) { return "diagnostics failed: " + ex.Message; }
            return null;
        }

        /// <summary>The concrete driver type registered for station 1 (e.g. MitsubishiActUtl).</summary>
        private static Type RealDriverType()
        {
            var drv = PlcManager.Instance.GetDriverForMonitoring(1);
            return drv == null ? null : drv.GetType();
        }

        private void TestStation()
        {
            int n = (int)_station.Value;
            Append("");
            Append("--- testing logical station " + n + " ---");
            string detail;
            bool ok = TryStation(n, out detail);
            Append(ok ? "  CONNECTED on station " + n : "  failed: " + detail);
            AuditLogger.Log($"[PLC DIAG] manual test of station {n}: {(ok ? "connected" : detail)}");
        }

        private void ScanStations()
        {
            var type = RealDriverType();
            if (type == null) { Append("no driver registered for station 1"); return; }

            Append("");
            Append("--- scanning logical stations 0.." + (int)_scanTo.Value + " ---");
            Application.DoEvents();
            var found = new StringBuilder();
            for (int n = 0; n <= (int)_scanTo.Value; n++)
            {
                string detail;
                bool ok = TryStation(n, out detail);
                if (ok)
                {
                    Append("  station " + n + ": CONNECTED");
                    found.Append(found.Length > 0 ? ", " : "").Append(n);
                }
                Application.DoEvents();
            }
            Append(found.Length > 0
                ? "answering stations: " + found
                : "no station answered - the logical stations above are what MX Component has stored");
            AuditLogger.Log("[PLC DIAG] station scan result: " + (found.Length > 0 ? found.ToString() : "none"));
        }

        /// <summary>Creates a temporary driver for one station and tries to open it.</summary>
        private static bool TryStation(int station, out string detail)
        {
            detail = "";
            var type = RealDriverType();
            if (type == null) { detail = "no driver registered"; return false; }
            object drv = null;
            try
            {
                drv = Activator.CreateInstance(type, station);
                var connect = type.GetMethod("Connect");
                bool ok = connect != null && (bool)connect.Invoke(drv, null);
                if (!ok)
                {
                    var err = type.GetProperty("LastConnectError");
                    detail = err != null ? Convert.ToString(err.GetValue(drv, null)) : "connect returned false";
                }
                return ok;
            }
            catch (Exception ex) { detail = ex.Message; return false; }
            finally
            {
                try
                {
                    var disp = drv as IDisposable;
                    if (disp != null) disp.Dispose();
                    else if (drv != null)
                    {
                        var d = drv.GetType().GetMethod("Disconnect");
                        if (d != null) d.Invoke(drv, null);
                    }
                }
                catch { }
            }
        }
    }

    public class PlcDiagnosticsPage : ISettingsPage
    {
        public string TitleKey => "Set_PlcDiag";
        public string TitleFallback => "PLC Diagnostics";
        public SecurityLevel RequiredLevel => SecurityLevel.Admin;
        public UserControl GetControl() => new PlcDiagnosticsControl();
    }
}

