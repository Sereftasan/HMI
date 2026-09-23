using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// PLC CONNECTION (admin): which driver each machine controller talks
    /// through, and on which station number.
    ///
    /// One row per controller, named as the engineer names it - "FA2010 PLC1",
    /// "FA2010 PLC2", "Screwing PLC1" - so this page and the monitor's tabs read
    /// identically.
    ///
    /// It exists because the mapping was previously written into each machine's
    /// Program.cs. The same exe is deployed to a bench (everything simulated) and to
    /// the machine (CPU1 on MX Component, CPU2 on the simulator until the second
    /// panel is wired), and changing which was which meant a rebuild, on a panel PC,
    /// in a factory.
    ///
    /// STATION NUMBER means the MX Component LOGICAL STATION NUMBER - the one the
    /// Communication Setting Utility stores, which ActUtlType.ActLogicalStationNumber
    /// wants. It is NOT the HMI's own station id (the PlcNumber on a control): that
    /// belongs to the machine module and is shown greyed beside each row so the two
    /// are never confused. PLC Diagnostics on the neighbouring tab will scan for the
    /// number that actually answers.
    ///
    /// Changes take effect at the NEXT START. Swapping a driver under screens that
    /// are already bound to the running one would leave half the HMI reading from a
    /// disposed COM object, so the page saves and says so rather than pretending.
    /// </summary>
    public class StationConnectionControl : UserControl
    {
        private class Row
        {
            public PlcConnectionSettings.Endpoint Endpoint;
            public ComboBox Driver;
            public NumericUpDown Station;
            public TextBox Address;
            public Label Status;
        }

        private readonly List<Row> _rows = new List<Row>();
        private readonly Timer _statusTimer;
        private readonly EventHandler _onConnChanged;

        private void RefreshAllStatus()
        {
            if (IsDisposed || !Visible) return;
            foreach (var r in _rows) RefreshStatus(r);
        }
        private Button _btnSave;
        private bool _building;   // suppresses change events while the page is built
        private readonly TableLayoutPanel _table = new TableLayoutPanel();
        private readonly Label _footer = new Label();

        private static readonly Color ColHeader = Color.FromArgb(245, 246, 248);
        private static readonly Color ColDim = Color.FromArgb(110, 116, 124);

        public StationConnectionControl()
        {
            // LIVE, not a picture of the moment the page was built. The state was
            // worked out once, so a link that dropped later still read "connected"
            // for as long as the page stayed open.
            _statusTimer = new Timer { Interval = ConnectionMonitor.BeatMs };
            _statusTimer.Tick += (s, e) => RefreshAllStatus();
            _statusTimer.Start();
            _onConnChanged = (s, e) =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                if (InvokeRequired) BeginInvoke((Action)RefreshAllStatus); else RefreshAllStatus();
            };
            ConnectionMonitor.ConnectionChanged += _onConnChanged;
            Disposed += (s, e) =>
            {
                _statusTimer.Stop();
                _statusTimer.Dispose();
                ConnectionMonitor.ConnectionChanged -= _onConnChanged;
            };

            Dock = DockStyle.Fill;
            BackColor = Color.White;

            var tips = Kaban.UI.Controls.UiStyle.Tips();

            // ---- toolbar ----
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(8, 6, 8, 6),
                BackColor = Color.White
            };
            _btnSave = UiStyle.ToolButton("\uD83D\uDCBE", LanguageManager.GetString(
                "Tip_SaveConnections", "Save these connections"), tips, (s, e) => Save());
            // DISABLED until something actually differs from what is saved. Saving an
            // unchanged page still wrote the file and still asked to restart, so the
            // operator was offered a restart for having looked at the page.
            _btnSave.Enabled = false;
            bar.Controls.Add(_btnSave);
            bar.Controls.Add(UiStyle.ToolButton("\uD83D\uDD04", LanguageManager.GetString(
                "Tip_ReloadConnections", "Discard changes and re-read the saved file"), tips, (s, e) => Reload()));

            // ---- scrolling body ----
            var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8, 4, 8, 8) };

            _table.Dock = DockStyle.Top;
            _table.AutoSize = true;
            _table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _table.ColumnCount = 5;
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));  // controller
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));  // driver
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));  // station number
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));  // network address
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));       // status
            body.Controls.Add(_table);

            _footer.Dock = DockStyle.Bottom;
            _footer.Height = 40;
            _footer.Padding = new Padding(10, 6, 10, 6);
            _footer.ForeColor = ColDim;
            _footer.Text = LanguageManager.GetString("Msg_ConnRestart",
                "Changes take effect the next time the HMI starts. "
              + "Use PLC Diagnostics to find which logical station actually answers.");

            Controls.Add(body);
            Controls.Add(_footer);
            Controls.Add(bar);

            Build();
        }

        private void Build()
        {
            _building = true;
            try { BuildCore(); }
            finally { _building = false; }
            if (_btnSave != null) _btnSave.Enabled = false;   // freshly built == unchanged
        }

        private void BuildCore()
        {
            _table.Controls.Clear();
            _table.RowStyles.Clear();
            _rows.Clear();

            AddHeader();

            var endpoints = PlcConnectionSettings.Endpoints();
            if (endpoints.Count == 0)
            {
                _table.Controls.Add(new Label
                {
                    Text = LanguageManager.GetString("Msg_NoControllers",
                        "No machine module has declared a PLC station."),
                    AutoSize = true,
                    ForeColor = ColDim,
                    Padding = new Padding(4, 10, 4, 4)
                }, 0, _table.RowCount);
                _table.SetColumnSpan(_table.GetControlFromPosition(0, _table.RowCount), 5);
                _table.RowCount++;
                return;
            }

            // NO per-machine heading rows: the controller name in each row already
            // carries the machine ("Processing Center PLC1"), so a heading above it
            // repeats what the row says and turns four rows into seven.
            foreach (var endpoint in endpoints)
                AddRow(endpoint);
        }

        private void AddHeader()
        {
            int r = _table.RowCount;
            _table.Controls.Add(HeaderCell(LanguageManager.GetString("Col_Plc", "PLC")), 0, r);
            _table.Controls.Add(HeaderCell(LanguageManager.GetString("Col_Driver", "Driver")), 1, r);
            _table.Controls.Add(HeaderCell(LanguageManager.GetString("Col_StationNo", "Station no.")), 2, r);
            _table.Controls.Add(HeaderCell(LanguageManager.GetString("Col_NetAddress", "Address")), 3, r);
            _table.Controls.Add(HeaderCell(LanguageManager.GetString("Col_ConnState", "Connection State")), 4, r);
            _table.RowCount = r + 1;
        }

        private static Label HeaderCell(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = ColHeader,
                Padding = new Padding(6, 6, 6, 6),
                Margin = new Padding(0, 0, 0, 4)
            };
        }

        private void AddRow(PlcConnectionSettings.Endpoint endpoint)
        {
            int r = _table.RowCount;
            var saved = PlcConnectionSettings.For(endpoint);

            var name = new Label
            {
                UseMnemonic = false,   // "Screwing & Holing", not "Screwing  Holing"
                Text = PlcEndpointNaming.Caption(endpoint),
                AutoSize = true,
                Padding = new Padding(16, 8, 6, 6)
            };

            var driver = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 210,
                Margin = new Padding(3, 4, 3, 4)
            };

            var station = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 1023,
                Width = 84,
                TextAlign = HorizontalAlignment.Right,
                Margin = new Padding(3, 4, 3, 4)
            };

            var address = new TextBox
            {
                Width = 180,
                Margin = new Padding(3, 4, 3, 4),
                Text = saved.Endpoint ?? ""
            };

            var status = new Label
            {
                AutoSize = true,
                ForeColor = ColDim,
                Padding = new Padding(8, 8, 6, 6)
            };

            _table.Controls.Add(name, 0, r);
            _table.Controls.Add(driver, 1, r);
            _table.Controls.Add(station, 2, r);

            _table.Controls.Add(address, 3, r);
            _table.Controls.Add(status, 4, r);
            _table.RowCount = r + 1;

            // Items are populated and the selection made AFTER the combo is in its
            // parent. A ComboBox added to a parent has its selection reset by the
            // CurrencyManager, which silently turned every row back to the first
            // driver in the list.
            foreach (var kind in PlcDriverCatalog.Kinds) driver.Items.Add(kind);

            var current = PlcDriverCatalog.Kinds.FirstOrDefault(k =>
                string.Equals(k.Id, saved.DriverId, StringComparison.OrdinalIgnoreCase));
            if (current != null) driver.SelectedItem = current;
            else if (driver.Items.Count > 0) driver.SelectedIndex = 0;

            station.Value = Math.Max(station.Minimum,
                            Math.Min(station.Maximum, saved.LogicalStation));

            var row = new Row
            {
                Endpoint = endpoint,
                Driver = driver,
                Station = station,
                Address = address,
                Status = status
            };
            _rows.Add(row);

            driver.SelectedIndexChanged += (s, e) => { UpdateRowState(row); MarkDirty(); };
            station.ValueChanged += (s, e) => MarkDirty();
            address.TextChanged += (s, e) => MarkDirty();
            UpdateRowState(row);

            var tips = Kaban.UI.Controls.UiStyle.Tips();
            tips.SetToolTip(name, PlcEndpointNaming.CaptionWithStation(endpoint) +
                "\r\n" + LanguageManager.GetString("Tip_HmiStationId",
                    "Controls bind to this machine with PlcNumber ") + endpoint.StationId + ".");
            tips.SetToolTip(station, LanguageManager.GetString("Tip_LogicalStation",
                "MX Component LOGICAL station number - what the Communication Setting Utility stores. "
              + "Not the same as the HMI's own station id."));
            tips.SetToolTip(address, LanguageManager.GetString("Tip_NetAddress",
                "Where the CPU is, for the drivers that reach it over the network.\r\n"
              + "Siemens:  host:rack:slot   e.g. 192.168.0.1:0:1\r\n"
              + "Delta:    host:port:unit   e.g. 192.168.1.5:502:1"));
        }

        /// <summary>
        /// Greys the station box for a driver that has no logical station (the
        /// simulator, "not connected"), and reports what the driver is doing NOW -
        /// which is the saved state, not the edited one, until the HMI restarts.
        /// </summary>
        private void UpdateRowState(Row row)
        {
            var kind = row.Driver.SelectedItem as PlcDriverCatalog.DriverKind;
            row.Station.Enabled = kind == null || kind.UsesLogicalStation;
            row.Address.Enabled = kind != null && kind.UsesNetworkEndpoint;

            // An empty address box on a network driver is the commonest way to get a
            // silent no-connect, so it offers the shape rather than staying blank.
            if (row.Address.Enabled && row.Address.Text.Trim().Length == 0 &&
                !string.IsNullOrEmpty(kind.EndpointHint))
                row.Address.Text = kind.EndpointHint;

            RefreshStatus(row);
        }

        /// <summary>
        /// The Connection State cell alone. Split from UpdateRowState so the live
        /// refresh can run every second without touching the address box - which
        /// would mark the page dirty and light the Save button for nothing.
        /// </summary>
        private void RefreshStatus(Row row)
        {
            var live = PlcManager.Instance.GetDriverForMonitoring(row.Endpoint.StationId);
            if (live == null)
            {
                row.Status.Text = LanguageManager.GetString("Sta_NotRegistered", "not registered");
                row.Status.ForeColor = ColDim;
                return;
            }

            bool connected = false;
            try { connected = live.IsConnected; } catch { }

            // THE VERSION, NOT THE DRIVER CLASS NAME. The driver is chosen two
            // columns to the left, so repeating it here says nothing; what an
            // engineer cannot otherwise see is which ladder is running.
            var state = ConnectionMonitor.Get(row.Endpoint.StationId);
            bool live_ = connected && state.IsLive;

            // ONE SENTENCE, and the bracket only when it has something to say.
            // "Connection failed ()" is worse than "Connection failed".
            string text;
            if (live_)
            {
                text = LanguageManager.GetString("Sta_Connected", "Connected");
                if (!string.IsNullOrEmpty(state.Version))
                    text += " (" + LanguageManager.GetString("Sta_Ver", "Ver.:") + " " + state.Version + ")";
            }
            else
            {
                text = LanguageManager.GetString("Sta_Failed", "Connection failed");
                if (!string.IsNullOrEmpty(state.ErrorCode))
                    text += " (" + LanguageManager.GetString("Conn_ErrorCode", "Error Code") +
                            ": " + state.ErrorCode + ")";
            }

            row.Status.Text = text;
            row.Status.ForeColor = live_ ? Color.FromArgb(0, 120, 40) : Color.FromArgb(170, 70, 0);
        }

        /// <summary>
        /// Enables Save only when the page differs from the SAVED state - compared
        /// value by value rather than tracked with a flag, so returning a control to
        /// its original setting correctly disables the button again.
        /// </summary>
        private void MarkDirty()
        {
            if (_building || _btnSave == null) return;
            _btnSave.Enabled = HasChanges();
        }

        private bool HasChanges()
        {
            foreach (var row in _rows)
            {
                var kind = row.Driver.SelectedItem as PlcDriverCatalog.DriverKind;
                if (kind == null) continue;
                var saved = PlcConnectionSettings.For(row.Endpoint);

                if (!string.Equals(kind.Id, saved.DriverId, StringComparison.OrdinalIgnoreCase)) return true;
                if (kind.UsesLogicalStation && (int)row.Station.Value != saved.LogicalStation) return true;
                if (kind.UsesNetworkEndpoint &&
                    !string.Equals((row.Address.Text ?? "").Trim(), (saved.Endpoint ?? "").Trim(),
                                   StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private void Save()
        {
            foreach (var row in _rows)
            {
                var kind = row.Driver.SelectedItem as PlcDriverCatalog.DriverKind;
                if (kind == null) continue;
                PlcConnectionSettings.Set(row.Endpoint, kind.Id, (int)row.Station.Value,
                                          row.Address.Text);
            }

            bool ok = PlcConnectionSettings.Save();
            string title = LanguageManager.GetString("Set_StationConn", "PLC Connection");

            if (!ok)
            {
                MessageBox.Show(this,
                    LanguageManager.GetString("Msg_ConnSaveFailed",
                        "Could not write the settings file. See the audit log."),
                    title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_btnSave != null) _btnSave.Enabled = false;   // saved == no longer dirty

            // The shared restart notice - the same message box any page uses when a
            // setting needs a restart, so the operator meets one behaviour whether
            // they changed a station, a language or a unit.
            Kaban.UI.Forms.MainForm.NotifyRestartRequired(this,
                LanguageManager.GetString("Msg_ConnSaved",
                    "PLC connections saved."));
        }

        private void Reload()
        {
            PlcConnectionSettings.Load();
            Build();
        }
    }

    public class StationConnectionPage : ISettingsPage
    {
        public string TitleKey { get { return "Set_StationConn"; } }
        public string TitleFallback { get { return "PLC Connection"; } }
        public SecurityLevel RequiredLevel { get { return SecurityLevel.Admin; } }
        public UserControl GetControl() { return new StationConnectionControl(); }
    }
}

