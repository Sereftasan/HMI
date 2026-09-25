using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// PLC DIAGNOSTICS: "why is this controller not answering, and since when?"
    /// answered on the machine, without a laptop. Three tabs:
    ///
    ///   * CONTROLLERS - every controller, its driver and address, whether it
    ///     answers now, when it last did, the last error in words, and how often
    ///     it dropped today (a link that drops forty times a day is a cable or a
    ///     switch, not a program).
    ///   * CONNECTION EVENTS - the audit log's connection entries (lost, back,
    ///     driver reports), with the log viewer's time and text filters.
    ///   * MX COMPONENT (Admin) - the environment report and the logical-station
    ///     test and scan, for when the Communication Setting Utility and the HMI
    ///     disagree.
    ///
    /// The page was the MX tools alone, and they misled: the test built a whole
    /// driver per station (each leaving a thread behind), gave it 150 ms to open
    /// and called a slower station "failed"; the scan froze the screen while it
    /// ran; it opened a second control on the station the machine was using;
    /// and it took its driver from station 1 - with the simulator there, every
    /// station "answered".
    /// </summary>
    public class PlcDiagnosticsControl : UserControl
    {
        public PlcDiagnosticsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            var tabs = new SettingsScreen(new ISettingsPage[]
            {
                new SimplePage("Diag_TabControllers", "Controllers", SecurityLevel.Maintenance,
                               () => new ControllerStatusView()),
                new SimplePage("Diag_TabEvents", "Connection events", SecurityLevel.Maintenance,
                               () => new LogViewerControl(AuditKind.Connection, LogViewerControl.Period.Today)),
                new SimplePage("Diag_TabMx", "MX Component", SecurityLevel.Admin,
                               () => new MxToolsView())
            });
            tabs.Dock = DockStyle.Fill;
            Controls.Add(tabs);
        }
    }

    /// <summary>
    /// One row per controller, refreshed every second - the live state from
    /// ConnectionMonitor, the connection from the drivers actually registered
    /// (not from the settings file, which only takes effect after a restart).
    /// </summary>
    public class ControllerStatusView : UserControl
    {
        private readonly DataGridView _grid = new DataGridView();
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer { Interval = ConnectionMonitor.BeatMs };
        private readonly Label _note = new Label();
        private Dictionary<int, int> _drops = new Dictionary<int, int>();
        private DateTime _dropsRead = DateTime.MinValue;

        private static readonly Color Ok = Color.FromArgb(0, 120, 40);
        private static readonly Color Bad = Color.FromArgb(180, 40, 20);
        private static readonly Color Dim = Color.FromArgb(120, 128, 138);

        public ControllerStatusView()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5F);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.BackgroundColor = Color.White;
            _grid.BorderStyle = BorderStyle.FixedSingle;
            _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _grid.GridColor = Color.FromArgb(228, 231, 236);
            _grid.RowTemplate.Height = 30;
            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(242, 244, 247);
            _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 2, 4, 5);
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 228, 248);
            _grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            Add("Plc", "Diag_ColPlc", "PLC", 45);
            Add("Machine", "Diag_ColMachine", "Machine", 150);
            Add("Driver", "Diag_ColDriver", "Driver", 150);
            Add("Address", "Diag_ColAddress", "Connection", 130);
            Add("State", "Diag_ColState", "State", 170);
            Add("Last", "Diag_ColLast", "Last answer", 110);
            Add("Drops", "Diag_ColDrops", "Drops today", 90);
            var err = Add("Error", "Diag_ColError", "Last error", 200);
            err.MinimumWidth = 120;
            err.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            Add("Version", "Diag_ColVersion", "Program", 70);

            _note.Dock = DockStyle.Bottom;
            _note.Height = 40;
            _note.Padding = new Padding(2, 6, 0, 0);
            _note.ForeColor = Dim;
            _note.Text = LanguageManager.GetString("Diag_ControllersNote",
                "Drivers and addresses as running now - a change on the PLC Connection page takes effect after a restart. " +
                "Drops today: how often the link was lost since midnight (see Connection events).");

            Controls.Add(_grid);
            Controls.Add(_note);

            _timer.Tick += (s, e) => RefreshRows();
            VisibleChanged += (s, e) => { _timer.Enabled = Visible; if (Visible) RefreshRows(); };
            RefreshRows();
        }

        private DataGridViewTextBoxColumn Add(string name, string key, string fallback, int width)
        {
            var c = new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = LanguageManager.GetString(key, fallback),
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            _grid.Columns.Add(c);
            return c;
        }

        /// <summary>A controller's row: what the page shows for it.</summary>
        public sealed class Row
        {
            public int Station;
            public string Machine = "", Driver = "", Address = "", State = "", Last = "", Error = "", Version = "";
            public int Drops;
            public bool Live, Registered;
        }

        /// <summary>The rows as of now - public for the tests.</summary>
        public static List<Row> BuildRows(IDictionary<int, int> drops)
        {
            var rows = new List<Row>();
            var seen = new HashSet<int>();
            List<PlcConnectionSettings.Endpoint> endpoints;
            try { endpoints = PlcConnectionSettings.Endpoints(); }
            catch { endpoints = new List<PlcConnectionSettings.Endpoint>(); }

            foreach (var ep in endpoints)
            {
                if (!seen.Add(ep.StationId)) continue;
                rows.Add(RowFor(ep.StationId, ep.StationName, ep, drops));
            }
            // A station the program registered without an endpoint (a legacy exe).
            foreach (var st in ConnectionMonitor.Stations.ToList())
                if (seen.Add(st.StationId)) rows.Add(RowFor(st.StationId, st.Name, null, drops));
            return rows.OrderBy(r => r.Station).ToList();
        }

        private static Row RowFor(int station, string name, PlcConnectionSettings.Endpoint ep, IDictionary<int, int> drops)
        {
            var r = new Row { Station = station };
            var state = ConnectionMonitor.Get(station);
            r.Machine = !string.IsNullOrWhiteSpace(name) ? name : state.Name;
            int d; r.Drops = drops != null && drops.TryGetValue(station, out d) ? d : 0;

            var registered = PlcManager.Instance.GetDriverForMonitoring(station);
            var drv = Unwrapped(registered);
            r.Registered = drv != null;
            if (drv == null)
            {
                r.Driver = LanguageManager.GetString("Sta_NotRegistered", "not registered");
                r.State = LanguageManager.GetString("Diag_NotConnected", "not connected (none set)");
                return r;
            }

            var kind = KindOf(drv);
            r.Driver = kind != null ? kind.DisplayName : drv.GetType().Name;
            r.Address = AddressOf(drv, ep, kind);

            bool connected = false;
            try { connected = registered.IsConnected; } catch { }   // the wrapper's: it never blocks
            r.Live = connected && state.IsLive;
            var stale = state.StaleFor;
            if (r.Live) r.State = "● " + LanguageManager.GetString("Sta_Connected", "Connected");
            else if (!state.EverConnected)
                r.State = "● " + LanguageManager.GetString("Diag_NeverAnswered", "has not answered since the start");
            else
                r.State = "● " + string.Format(LanguageManager.GetString("Diag_NoAnswerFor", "no answer for {0}"), Span(stale));

            r.Last = state.EverConnected && stale != TimeSpan.MaxValue
                ? DateTime.Now.Subtract(stale).ToString("HH:mm:ss", CultureInfo.InvariantCulture) +
                  (stale.TotalDays >= 1 ? "  (" + DateTime.Now.Subtract(stale).ToString("dd.MM", CultureInfo.InvariantCulture) + ")" : "")
                : "—";
            if (!r.Live && !string.IsNullOrEmpty(state.ErrorCode))
                r.Error = state.ErrorCode + "  " + ConnectionMonitor.DescribeCode(state.ErrorCode);
            else if (!r.Live) r.Error = state.LastError;
            r.Version = state.Version;
            return r;
        }

        /// <summary>
        /// The driver itself: a driver that could block the screen is registered
        /// inside a BoundedDriver (PlcManager.RegisterDriver), whose class name
        /// says nothing about the controller.
        /// </summary>
        public static IPlcDriver Unwrapped(IPlcDriver drv)
        {
            var b = drv as BoundedDriver;
            return b != null && b.Inner != null ? b.Inner : drv;
        }

        /// <summary>"12 s", "4 min", "2 h 5 min" - how long a controller has been silent.</summary>
        public static string Span(TimeSpan t)
        {
            if (t == TimeSpan.MaxValue) return "—";
            if (t.TotalSeconds < 90) return Math.Max(0, (int)t.TotalSeconds) + " s";
            if (t.TotalMinutes < 90) return (int)t.TotalMinutes + " min";
            if (t.TotalHours < 48) return (int)t.TotalHours + " h " + t.Minutes + " min";
            return (int)t.TotalDays + " d";
        }

        /// <summary>The catalogue entry of a running driver, found by its class name.</summary>
        private static PlcDriverCatalog.DriverKind KindOf(IPlcDriver drv)
        {
            string tn = drv.GetType().Name;
            return PlcDriverCatalog.Kinds.FirstOrDefault(k => tn.IndexOf(k.Id, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string AddressOf(IPlcDriver drv, PlcConnectionSettings.Endpoint ep, PlcDriverCatalog.DriverKind kind)
        {
            // MX Component: the logical station the RUNNING driver opens.
            var p = drv.GetType().GetProperty("LogicalStation");
            if (p != null)
            {
                try { return LanguageManager.GetString("Diag_LogicalStation", "logical station") + " " + Convert.ToString(p.GetValue(drv, null)); }
                catch { }
            }
            if (kind != null && kind.UsesNetworkEndpoint && ep != null && PlcConnectionSettings.IsConfigured(ep))
                return PlcConnectionSettings.For(ep).Endpoint;
            return "";
        }

        /// <summary>
        /// How often each station was lost today, counted from the log's
        /// "station N lost" entries - read at most every 30 s.
        /// </summary>
        public static Dictionary<int, int> DropsToday()
        {
            var result = new Dictionary<int, int>();
            foreach (var e in AuditLogReader.Read(DateTime.Today, DateTime.Now))
            {
                if (e.Kind != AuditKind.Connection) continue;
                var m = System.Text.RegularExpressions.Regex.Match(e.Text, @"^station (\d+) lost");
                int n;
                if (m.Success && int.TryParse(m.Groups[1].Value, out n))
                {
                    int c; result.TryGetValue(n, out c); result[n] = c + 1;
                }
            }
            return result;
        }

        private void RefreshRows()
        {
            if ((DateTime.Now - _dropsRead).TotalSeconds >= 30)
            {
                _drops = DropsToday();
                _dropsRead = DateTime.Now;
            }
            var rows = BuildRows(_drops);
            if (_grid.Rows.Count != rows.Count)
            {
                _grid.Rows.Clear();
                foreach (var r in rows) _grid.Rows.Add();
            }
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                var cells = _grid.Rows[i].Cells;
                Set(cells["Plc"], r.Station.ToString(CultureInfo.InvariantCulture));
                Set(cells["Machine"], r.Machine);
                Set(cells["Driver"], r.Driver);
                Set(cells["Address"], r.Address);
                Set(cells["State"], r.State);
                cells["State"].Style.ForeColor = !r.Registered ? Dim : r.Live ? Ok : Bad;
                Set(cells["Last"], r.Last);
                Set(cells["Drops"], r.Drops == 0 ? "0" : r.Drops.ToString(CultureInfo.InvariantCulture));
                cells["Drops"].Style.ForeColor = r.Drops > 0 ? Bad : Dim;
                Set(cells["Error"], r.Error);
                Set(cells["Version"], r.Version);
            }
        }

        private static void Set(DataGridViewCell c, string v)
        {
            if (!Equals(c.Value, v)) c.Value = v;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _timer.Stop(); _timer.Dispose(); }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// MX COMPONENT TOOLS (Admin): the environment report, and a test of one
    /// logical station or a scan of a range - run in the background with a Stop
    /// button, each station opened on a control of its own that lives exactly as
    /// long as the test (MxDiagnostics.TestStation). A logical station the
    /// machine is connected through is NOT opened a second time: its live state
    /// is reported instead.
    /// </summary>
    public class MxToolsView : UserControl
    {
        public const int TestTimeoutMs = 10000;

        private readonly TextBox _out = new TextBox();
        private readonly NumericUpDown _station = new NumericUpDown { Minimum = 0, Maximum = 1023, Value = 1, Width = 70 };
        private readonly NumericUpDown _scanFrom = new NumericUpDown { Minimum = 0, Maximum = 1023, Value = 0, Width = 70 };
        private readonly NumericUpDown _scanTo = new NumericUpDown { Minimum = 0, Maximum = 1023, Value = 20, Width = 70 };
        private readonly Button _btnTest, _btnScan, _btnStop;
        private readonly Label _progress = new Label();
        private volatile bool _stop;
        private Thread _worker;

        public MxToolsView()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5F);
            var tips = UiStyle.Tips();

            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true, Padding = new Padding(0, 2, 0, 6), BackColor = Color.White
            };
            Func<string, Label> caption = t => new Label { Text = t, AutoSize = true, Margin = new Padding(8, 9, 3, 3) };
            foreach (var n in new[] { _station, _scanFrom, _scanTo })
            {
                n.Margin = new Padding(2, 5, 3, 3);
            }

            bar.Controls.Add(caption(LanguageManager.GetString("Diag_LogicalStationLbl", "Logical station")));
            bar.Controls.Add(_station);
            _btnTest = UiStyle.ToolButton("▶", LanguageManager.GetString("Diag_TestTip", "Open this logical station once and report the answer"),
                                          tips, (s, e) => Start(new[] { (int)_station.Value }, false));
            bar.Controls.Add(_btnTest);

            bar.Controls.Add(caption("   " + LanguageManager.GetString("Diag_Scan", "Scan")));
            bar.Controls.Add(_scanFrom);
            bar.Controls.Add(caption("–"));
            bar.Controls.Add(_scanTo);
            _btnScan = UiStyle.ToolButton("🔍", LanguageManager.GetString("Diag_ScanTip",
                "Try every logical station in the range and list the ones that open"), tips, (s, e) =>
                {
                    int a = (int)Math.Min(_scanFrom.Value, _scanTo.Value), b = (int)Math.Max(_scanFrom.Value, _scanTo.Value);
                    Start(Enumerable.Range(a, b - a + 1).ToArray(), true);
                });
            bar.Controls.Add(_btnScan);
            _btnStop = UiStyle.ToolButton("■", LanguageManager.GetString("Diag_StopTip", "Stop after the station being tried"),
                                          tips, (s, e) => { _stop = true; _btnStop.Enabled = false; });
            _btnStop.Enabled = false;
            bar.Controls.Add(_btnStop);

            var env = UiStyle.ToolButton("🔄", LanguageManager.GetString("Diag_EnvTip", "Show the MX Component environment report again"),
                                         tips, (s, e) => ShowEnvironment());
            env.Margin = new Padding(16, 1, 3, 1);
            bar.Controls.Add(env);
            bar.Controls.Add(UiStyle.ToolButton("📋", LanguageManager.GetString("Diag_CopyTip", "Copy this report to the clipboard"),
                                                tips, (s, e) => { try { Clipboard.SetText(_out.Text); } catch { } }));

            _progress.Dock = DockStyle.Top;
            _progress.Height = 24;
            _progress.Padding = new Padding(2, 4, 0, 0);
            _progress.ForeColor = Color.FromArgb(90, 96, 106);

            _out.Dock = DockStyle.Fill;
            _out.Multiline = true;
            _out.ReadOnly = true;
            _out.ScrollBars = ScrollBars.Both;
            _out.WordWrap = false;
            _out.Font = new Font("Consolas", 9F);
            _out.BackColor = Color.FromArgb(28, 30, 34);
            _out.ForeColor = Color.FromArgb(190, 230, 190);

            Controls.Add(_out);
            Controls.Add(_progress);
            Controls.Add(bar);
            ShowEnvironment();
        }

        public string Output { get { return _out.Text; } }
        public bool Running { get { return _worker != null && _worker.IsAlive; } }

        private void Append(string text)
        {
            _out.AppendText((_out.TextLength > 0 ? Environment.NewLine : "") + text);
        }

        private void ShowEnvironment()
        {
            if (Running) return;
            _out.Text = "";
            Append("process: " + (Environment.Is64BitProcess ? "64-bit" : "32-bit") +
                   " on " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit") + " Windows");
            var t = DiagnosticsType();
            if (t == null) { Append(LanguageManager.GetString("Diag_NoMx", "(the MX Component driver is not part of this program)")); return; }
            try
            {
                var m = t.GetMethod("BuildReport", BindingFlags.Public | BindingFlags.Static);
                Append(Convert.ToString(m.Invoke(null, null)).TrimEnd());
            }
            catch (Exception ex) { Append("environment report failed: " + (ex.InnerException ?? ex).Message); }
        }

        /// <summary>The MX driver's diagnostics class, when the driver ships with this program.</summary>
        private static Type DiagnosticsType()
        {
            try
            {
                return Type.GetType("Kaban.Drivers.MitsubishiActUtl.MxDiagnostics, Kaban.Drivers.MitsubishiActUtl", false);
            }
            catch { return null; }
        }

        /// <summary>
        /// Logical stations the machine is connected through now, with the
        /// controller using each - not to be opened a second time.
        /// </summary>
        public static Dictionary<int, int> StationsInUse()
        {
            var used = new Dictionary<int, int>();
            var ids = new HashSet<int>(ConnectionMonitor.Stations.Select(s => s.StationId));
            try { foreach (var ep in PlcConnectionSettings.Endpoints()) ids.Add(ep.StationId); } catch { }
            foreach (int id in ids)
            {
                var drv = ControllerStatusView.Unwrapped(PlcManager.Instance.GetDriverForMonitoring(id));
                var p = drv == null ? null : drv.GetType().GetProperty("LogicalStation");
                if (p == null) continue;
                try { int n = Convert.ToInt32(p.GetValue(drv, null)); if (!used.ContainsKey(n)) used[n] = id; } catch { }
            }
            return used;
        }

        private void Start(int[] stations, bool scan)
        {
            if (Running) return;
            var t = DiagnosticsType();
            var test = t == null ? null : t.GetMethod("TestStation", BindingFlags.Public | BindingFlags.Static);
            var describe = t == null ? null : t.GetMethod("DescribeOpenError", BindingFlags.Public | BindingFlags.Static);
            if (test == null)
            {
                Append(LanguageManager.GetString("Diag_NoMx", "(the MX Component driver is not part of this program)"));
                return;
            }
            var inUse = StationsInUse();
            _stop = false;
            _btnTest.Enabled = _btnScan.Enabled = false;
            _btnStop.Enabled = true;
            Append("");
            Append(scan ? "--- scanning logical stations " + stations.First() + " - " + stations.Last() + " ---"
                        : "--- testing logical station " + stations[0] + " ---");

            _worker = new Thread(() =>
            {
                var opened = new List<int>();
                int done = 0;
                foreach (int n in stations)
                {
                    if (_stop) break;
                    Post(() => _progress.Text = string.Format(LanguageManager.GetString("Diag_Trying",
                        "trying logical station {0} ({1} of {2})..."), n, done + 1, stations.Length));
                    string line;
                    int owner;
                    if (inUse.TryGetValue(n, out owner))
                    {
                        // THE MACHINE'S OWN LINK: reported, not opened again.
                        var st = ConnectionMonitor.Get(owner);
                        bool live = st.IsLive;
                        line = "  station " + n + ": in use by PLC " + owner +
                               (string.IsNullOrEmpty(st.Name) ? "" : " (" + st.Name + ")") + " - " +
                               (live ? "CONNECTED now" : "not answering" + (string.IsNullOrEmpty(st.ErrorCode) ? "" : " (" + st.ErrorCode + ")"));
                        if (live) opened.Add(n);
                    }
                    else
                    {
                        var args = new object[] { n, TestTimeoutMs, false };
                        int code;
                        try { code = (int)test.Invoke(null, args); }
                        catch (Exception ex) { code = unchecked((int)0x80040154); line = ex.Message; }
                        bool timedOut = (bool)args[2];
                        if (timedOut)
                            line = "  station " + n + ": no answer within " + (TestTimeoutMs / 1000) + " s";
                        else if (code == 0)
                        {
                            line = "  station " + n + ": OPENS";
                            opened.Add(n);
                        }
                        else
                        {
                            string meaning = "";
                            try { meaning = Convert.ToString(describe.Invoke(null, new object[] { code })); } catch { }
                            line = "  station " + n + ": 0x" + code.ToString("X8") + " " + meaning;
                        }
                        // A scan lists the stations that are there; the rest is noise.
                        if (scan && !timedOut && unchecked((uint)code) == 0xF0000002) line = null;
                    }
                    done++;
                    if (line != null) { var l = line; Post(() => Append(l)); }
                }
                bool stopped = _stop;
                Post(() =>
                {
                    string result = opened.Count > 0 ? "opens: " + string.Join(", ", opened) : "no logical station opened";
                    if (stopped) result += "  (stopped after " + done + " of " + stations.Length + ")";
                    Append(scan ? "  " + result : "");
                    _progress.Text = "";
                    _btnTest.Enabled = _btnScan.Enabled = true;
                    _btnStop.Enabled = false;
                    AuditLogger.Log("[PLC DIAG] " + (scan ? "logical station scan " + stations.First() + "-" + stations.Last()
                                                         : "test of logical station " + stations[0]) + ": " + result);
                });
            })
            {
                IsBackground = true,
                Name = "PLC diagnostics"
            };
            _worker.Start();
        }

        /// <summary>To the screen, if the page is still there.</summary>
        private void Post(Action a)
        {
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke(a); } catch { }
        }

        protected override void Dispose(bool disposing)
        {
            _stop = true;       // a running scan ends after the station being tried
            base.Dispose(disposing);
        }
    }

    public class PlcDiagnosticsPage : ISettingsPage
    {
        public string TitleKey => "Set_PlcDiag";
        public string TitleFallback => "PLC Diagnostics";
        // Reading the controllers' state and the connection events is for
        // Maintenance too; the MX tools inside stay Admin.
        public SecurityLevel RequiredLevel => SecurityLevel.Maintenance;
        public UserControl GetControl() => new PlcDiagnosticsControl();
    }
}
