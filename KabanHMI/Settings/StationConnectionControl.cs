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
    /// PLC CONNECTION - CONTROLLERS: which driver each machine controller talks
    /// through, and on which station number - and, beside it, how it is doing
    /// now: state, last answer, drops today, the last error in words, the program
    /// version. (The live columns were a second table on a PLC Diagnostics tab,
    /// the same controllers twice; see PlcConnectionScreen.) Everyone from
    /// Maintenance reads it; only an Admin changes a connection.
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
    /// are never confused. The MX Component tab beside this one scans for the
    /// number that actually answers.
    ///
    /// Changes take effect at the NEXT START. Swapping a driver under screens that
    /// are already bound to the running one would leave half the HMI reading from a
    /// disposed COM object, so the page saves and says so rather than pretending.
    /// </summary>
    public class StationConnectionControl : UserControl, IRebuildForLanguage
    {
        // its texts are put on when it is built: built again in another language (SettingsScreen)
        object IRebuildForLanguage.Place { get { return null; } }
        void IRebuildForLanguage.ReturnTo(object place) { }

        private class Row
        {
            public PlcConnectionSettings.Endpoint Endpoint;
            /// <summary>Active (22 October): the controller's name ticked - used, its machine's pages shown.</summary>
            public CheckBox Active;
            public ComboBox Driver;
            public NumericUpDown Station;
            public TextBox Address;
            public Label Status;
            public Label Last, Drops, Error, Version;
            /// <summary>The PLC's data into a backup, and a backup back into the PLC (22 October).</summary>
            public Button Read, Write;
        }

        private Dictionary<int, int> _drops = new Dictionary<int, int>();
        private DateTime _dropsRead = DateTime.MinValue;
        private bool _canEdit;
        private Button _btnReload;
        private Label _adminNote;
        private readonly EventHandler _onUserChanged;

        private static readonly Color ColOk = Color.FromArgb(0, 120, 40);
        private static readonly Color ColBad = Color.FromArgb(180, 40, 20);

        private readonly List<Row> _rows = new List<Row>();
        private readonly Timer _statusTimer;
        private readonly EventHandler _onConnChanged;

        private void RefreshAllStatus()
        {
            if (IsDisposed || !Visible) return;
            // NOT WHILE A DRIVER LIST IS OPEN. The live cells change every second
            // ("last answer"), the table lays itself out again, and a list moved
            // by as much as a pixel is closed by Windows - the Driver box closed
            // the moment it was opened, before a driver could be picked.
            if (_rows.Any(r => r.Driver.DroppedDown)) return;
            // Drops are counted from the log: read at most every 30 s.
            if ((DateTime.Now - _dropsRead).TotalSeconds >= 30)
            {
                _drops = ControllerStatus.DropsToday();
                _dropsRead = DateTime.Now;
            }
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
            // shown again: the state at once, not up to a second later (the refresh skips a hidden page)
            VisibleChanged += (s, e) => { if (Visible) RefreshAllStatus(); };
            _onUserChanged = (s, e) => { if (!IsDisposed) ApplyAccess(); };
            SecurityManager.UserChanged += _onUserChanged;
            Disposed += (s, e) =>
            {
                _statusTimer.Stop();
                _statusTimer.Dispose();
                ConnectionMonitor.ConnectionChanged -= _onConnChanged;
                SecurityManager.UserChanged -= _onUserChanged;
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
            _btnReload = UiStyle.ToolButton("\u21BA", LanguageManager.GetString(
                "Tip_ReloadConnections", "Discard changes and re-read the saved file"), tips, (s, e) => Reload());
            bar.Controls.Add(_btnReload);
            _adminNote = new Label
            {
                AutoSize = true, Margin = new Padding(10, 9, 3, 3), ForeColor = ColDim,
                Text = LanguageManager.GetString("Msg_ConnAdminOnly", "Only an Admin can change the connections.")
            };
            bar.Controls.Add(_adminNote);

            // ---- scrolling body ----
            var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8, 4, 8, 8) };

            // NOT DOCKED: as wide as its columns, so the page scrolls sideways when
            // it is wider than the room (a side page open, a larger magnifying
            // factor). Docked, it was squeezed to the page's width - the last
            // columns cut off with no way to reach them, and its columns moving
            // under an open list.
            _table.Dock = DockStyle.None;
            _table.Location = new Point(8, 4);
            _table.AutoSize = true;
            _table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            // EVERY COLUMN AS WIDE AS WHAT IT HOLDS. The first four had fixed
            // widths chosen for the longest thing that might ever be typed, which
            // left gaps down the table - before the controller names, right of a
            // one-digit station, after a short address - and pushed the last
            // column off the page. The boxes are sized to their content instead
            // (see AddRow), and the program version moved beside "Connected".
            _table.ColumnCount = 9;
            for (int i = 0; i < 9; i++) _table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            body.Controls.Add(_table);

            _footer.Dock = DockStyle.Bottom;
            _footer.Height = 40;
            _footer.Padding = new Padding(10, 6, 10, 6);
            _footer.ForeColor = ColDim;
            _footer.Text = LanguageManager.GetString("Msg_ConnFooter",
                "Driver, station and address: as saved - a change takes effect the next time the HMI starts. "
              + "State, last answer and errors: the connection running now. The MX Component tab finds the logical station that answers.");

            Controls.Add(body);
            Controls.Add(_footer);
            Controls.Add(bar);

            Build();
            ApplyAccess();
        }

        /// <summary>Everyone may read the page; only an Admin may change a connection.</summary>
        private void ApplyAccess()
        {
            _canEdit = SecurityManager.HasAccess(SecurityLevel.Admin);
            if (_btnSave != null) _btnSave.Visible = _canEdit;
            if (_btnReload != null) _btnReload.Visible = _canEdit;
            if (_adminNote != null) _adminNote.Visible = !_canEdit;
            foreach (var r in _rows) UpdateRowState(r);
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
            // Disposed, not just taken off: a control taken off keeps its window until it is.
            foreach (Control old in _table.Controls.Cast<Control>().ToList()) { _table.Controls.Remove(old); old.Dispose(); }
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
                _table.SetColumnSpan(_table.GetControlFromPosition(0, _table.RowCount), 9);
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
            _table.Controls.Add(HeaderCell(LanguageManager.GetString("Col_PlcActive", "PLC (active)")), 0, r);
            _table.Controls.Add(HeaderCell(LanguageManager.GetString("Col_Driver", "Driver")), 1, r);
            _table.Controls.Add(HeaderCell(LanguageManager.GetString("Col_StationNo", "Station no.")), 2, r);
            _table.Controls.Add(HeaderCell(LanguageManager.GetString("Col_NetAddress", "Address")), 3, r);
            _table.Controls.Add(HeaderCell(LanguageManager.GetString("Diag_ColState", "State")), 4, r);
            _table.Controls.Add(HeaderCell(LanguageManager.GetString("Diag_ColLast", "Last answer")), 5, r);
            _table.Controls.Add(HeaderCell(LanguageManager.GetString("Diag_ColDrops", "Drops today")), 6, r);
            _table.Controls.Add(HeaderCell(LanguageManager.GetString("Diag_ColError", "Last error")), 7, r);
            _table.Controls.Add(HeaderCell(LanguageManager.GetString("Col_PlcData", "PLC data")), 8, r);
            _table.RowCount = r + 1;
        }

        private static Label HeaderCell(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = Kaban.UI.Controls.UiStyle.FontVariant(Kaban.UI.Controls.UiStyle.Small, FontStyle.Bold),
                BackColor = ColHeader,
                Padding = new Padding(6, 6, 6, 6),
                Margin = new Padding(0, 0, 0, 4)
            };
        }

        private void AddRow(PlcConnectionSettings.Endpoint endpoint)
        {
            int r = _table.RowCount;
            var saved = PlcConnectionSettings.For(endpoint);

            // ITS NAME, TICKED WHEN ACTIVE (22 October: "an activation checkbox for each PLC - the
            // machine pages only shown when any PLC of the related machine is activated")
            var name = new CheckBox
            {
                UseMnemonic = false,   // "Screwing & Holing", not "Screwing  Holing"
                Text = PlcEndpointNaming.Caption(endpoint),
                AutoSize = true,
                Checked = saved.Active,
                Padding = new Padding(6, 4, 6, 2),
                Margin = new Padding(3, 3, 3, 3)
            };

            // Sized to what they hold: the longest driver name, a four-digit
            // station, the longest address shape ("192.168.100.200:502:1").
            var driver = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = TextWidth(PlcDriverCatalog.Kinds.Select(k => k.DisplayName).DefaultIfEmpty("Simulation (in-memory)")
                                                          .OrderByDescending(n => n.Length).First()) + 34,
                Margin = new Padding(3, 4, 3, 4)
            };

            var station = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 1023,
                Width = TextWidth("0000") + 30,
                TextAlign = HorizontalAlignment.Right,
                Margin = new Padding(3, 4, 3, 4)
            };

            var address = new TextBox
            {
                Width = TextWidth("192.168.100.200:502:1") + 10,
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
            Func<Label> cell = () => new Label { AutoSize = true, ForeColor = ColDim, Padding = new Padding(8, 8, 6, 6), UseMnemonic = false };
            var last = cell();
            var drops = cell();
            var error = cell();
            // The meaning of an error code can be long: it wraps rather than
            // pushing the version off the page.
            error.MaximumSize = new Size(320, 0);
            var version = cell();        // kept for the row; shown in the State cell now
            _table.Controls.Add(last, 5, r);
            _table.Controls.Add(drops, 6, r);
            _table.Controls.Add(error, 7, r);

            // READ AND WRITE (22 October: "add Read and Write button to the end of PLC Connections table
            // (enabled when the connection established)"): the PLC's data into a backup; a backup back in
            var data = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(3, 3, 3, 3) };
            var read = UiStyle.TextButton(LanguageManager.GetString("Btn_PlcRead", "Read..."), 0);
            var write = UiStyle.TextButton(LanguageManager.GetString("Btn_PlcWrite", "Write..."), 0);
            read.Name = "plcRead"; write.Name = "plcWrite";
            // off until the row's state says the PLC is connected (23 October: a PLC not active in this run kept
            // the buttons as they were made - enabled)
            read.Enabled = false; write.Enabled = false;
            read.Margin = new Padding(0, 0, 6, 0); write.Margin = Padding.Empty;
            data.Controls.Add(read);
            data.Controls.Add(write);
            _table.Controls.Add(data, 8, r);
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
                Active = name,
                Driver = driver,
                Station = station,
                Address = address,
                Status = status,
                Last = last,
                Drops = drops,
                Error = error,
                Version = version,
                Read = read,
                Write = write
            };
            _rows.Add(row);
            read.Click += (s, e) => ReadPlc(row);
            write.Click += (s, e) => WritePlc(row);

            driver.SelectedIndexChanged += (s, e) => { UpdateRowState(row); MarkDirty(); };
            name.CheckedChanged += (s, e) => { UpdateRowState(row); MarkDirty(); };
            station.ValueChanged += (s, e) => MarkDirty();
            address.TextChanged += (s, e) => MarkDirty();
            UpdateRowState(row);

            var tips = Kaban.UI.Controls.UiStyle.Tips();
            Kaban.UI.Controls.UiStyle.SetTip(tips, name, PlcEndpointNaming.CaptionWithStation(endpoint) +
                "\r\n" + LanguageManager.GetString("Tip_HmiStationId",
                    "Controls bind to this machine with PlcNumber ") + endpoint.StationId + "." +
                "\r\n\r\n" + LanguageManager.GetString("Tip_PlcActive",
                    "Ticked: active - connected, its machine's pages shown. Not ticked: not connected; a machine none of whose PLCs is active " +
                    "has no pages (the menu, its tabs, the machine list, the PLC Monitor). From the next start."));
            Kaban.UI.Controls.UiStyle.SetTip(tips, station, LanguageManager.GetString("Tip_LogicalStation",
                "MX Component LOGICAL station number - what the Communication Setting Utility stores. "
              + "Not the same as the HMI's own station id."));
            Kaban.UI.Controls.UiStyle.SetTip(tips, address, LanguageManager.GetString("Tip_NetAddress",
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
            bool active = row.Active == null || row.Active.Checked;
            if (row.Active != null) row.Active.Enabled = _canEdit;
            row.Driver.Enabled = _canEdit && active;
            row.Station.Enabled = _canEdit && active && (kind == null || kind.UsesLogicalStation);
            row.Address.Enabled = _canEdit && active && kind != null && kind.UsesNetworkEndpoint;

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
            // The same reading as the rest of the program (ControllerStatus): the
            // running driver, ConnectionMonitor's state, the log's drops.
            if (PlcConnectionSettings.RunsInactive(row.Endpoint.StationId))
            {
                // not active in this run: no connection to tell of
                Set(row.Status, LanguageManager.GetString("Diag_NotActive", "Not active"), ColDim);
                Set(row.Last, "", ColDim); Set(row.Drops, "", ColDim); Set(row.Error, "", ColDim); Set(row.Version, "", ColDim);
                SetDataButtons(row, false);
                return;
            }
            var st = ControllerStatus.RowFor(row.Endpoint.StationId, row.Endpoint.StationName, row.Endpoint, _drops);
            SetDataButtons(row, st.Live);
            Set(row.Status, st.State, !st.Registered ? ColDim : st.Live ? ColOk : ColBad);
            Set(row.Last, st.Last, ColDim);
            Set(row.Drops, st.Drops.ToString(System.Globalization.CultureInfo.InvariantCulture), st.Drops > 0 ? ColBad : ColDim);
            Set(row.Error, st.Error, st.Live ? ColDim : ColBad);
            Set(row.Version, st.Version, ColDim);
        }

        /// <summary>
        /// Read and Write only while the PLC is connected: Read from Maintenance on (it changes
        /// nothing in the PLC), Write for an Admin.
        /// </summary>
        private void SetDataButtons(Row row, bool live)
        {
            if (row.Read == null) return;
            bool read = live && SecurityManager.HasAccess(SecurityLevel.Maintenance);
            bool write = live && SecurityManager.HasAccess(SecurityLevel.Admin);
            string state = live + "|" + SecurityManager.CurrentLevel;
            if (row.Read.Tag as string == state) return;   // the tips as they are: told again only when it changes
            row.Read.Tag = state;
            row.Read.Enabled = read;
            row.Write.Enabled = write;
            var tips = Kaban.UI.Controls.UiStyle.Tips();
            Kaban.UI.Controls.UiStyle.SetTip(tips, row.Read, !live ? LanguageManager.GetString("Tip_PlcDataNotConnected", "Not connected: nothing to read or write.")
                : LanguageManager.GetString("Tip_PlcRead", "Reads the PLC's devices (and its simple motion modules' data) into a backup, with your description."));
            Kaban.UI.Controls.UiStyle.SetTip(tips, row.Write, !live ? LanguageManager.GetString("Tip_PlcDataNotConnected", "Not connected: nothing to read or write.")
                : !SecurityManager.HasAccess(SecurityLevel.Admin) ? LanguageManager.GetString("Tip_PlcWriteAdmin", "An Admin writes a backup into the PLC.")
                : LanguageManager.GetString("Tip_PlcWrite", "Writes a backup's devices (all, or the ones chosen - to the same or other devices) into the PLC."));
        }

        /// <summary>Hooks for the tests: the backup windows opened (shown by the test, not as dialogs).</summary>
        internal static Action<Form> ShowWindow;

        private void ReadPlc(Row row)
        {
            var f = new PlcBackupReadForm(row.Endpoint);
            if (ShowWindow != null) { ShowWindow(f); return; }
            using (f) f.ShowDialog(FindForm());
        }

        private void WritePlc(Row row)
        {
            var f = new PlcBackupWriteForm(row.Endpoint);
            if (ShowWindow != null) { ShowWindow(f); return; }
            using (f) f.ShowDialog(FindForm());
        }

        /// <summary>
        /// The width of a text - the boxes are sized by it. Measured in a font a
        /// little larger than the page's (it is scaled with the page later), so
        /// the longest driver name is never cut.
        /// </summary>
        private static int TextWidth(string text)
        {
            return TextRenderer.MeasureText(text ?? "", MeasureFont).Width;
        }

        private static readonly Font MeasureFont = new Font("Segoe UI", 10F);

        private static void Set(Label l, string text, Color colour)
        {
            text = text ?? "";
            if (l.Text != text) l.Text = text;
            if (l.ForeColor != colour) l.ForeColor = colour;
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

                if (row.Active != null && row.Active.Checked != saved.Active) return true;
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
                                          row.Address.Text, row.Active == null || row.Active.Checked);
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

        /// <summary>
        /// DISCARD AND RE-READ (8 October: "it flutters for a few seconds until
        /// reloading the previous parameters"). It built the whole table anew - every
        /// box disposed, made again, scaled for the magnifier and laid out one by one
        /// while on screen. The rows are the same PLCs as before: only their saved
        /// values are put back in them, in one go. (Other PLCs in the file - a
        /// machine added meanwhile - build the table anew, out of sight.)
        /// </summary>
        private void Reload()
        {
            PlcConnectionSettings.Load();
            var endpoints = PlcConnectionSettings.Endpoints();
            bool same = endpoints.Count == _rows.Count && _rows.Count > 0 &&
                        endpoints.Select(e => e.StationId).SequenceEqual(_rows.Select(r => r.Endpoint.StationId));
            if (!same)
            {
                _table.SuspendLayout();
                try { Build(); }
                finally { _table.ResumeLayout(true); }
                ApplyAccess();
                return;
            }
            _building = true;
            try
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    var row = _rows[i];
                    row.Endpoint = endpoints[i];
                    var saved = PlcConnectionSettings.For(row.Endpoint);
                    var kind = PlcDriverCatalog.Kinds.FirstOrDefault(k => string.Equals(k.Id, saved.DriverId, StringComparison.OrdinalIgnoreCase));
                    if (kind != null) { if (row.Driver.SelectedItem != kind) row.Driver.SelectedItem = kind; }
                    else if (row.Driver.Items.Count > 0 && row.Driver.SelectedIndex != 0) row.Driver.SelectedIndex = 0;
                    var value = Math.Max(row.Station.Minimum, Math.Min(row.Station.Maximum, saved.LogicalStation));
                    if (row.Station.Value != value) row.Station.Value = value;
                    var text = saved.Endpoint ?? "";
                    if (row.Address.Text != text) row.Address.Text = text;
                    if (row.Active != null && row.Active.Checked != saved.Active) row.Active.Checked = saved.Active;
                    UpdateRowState(row);
                }
            }
            finally { _building = false; }
            if (_btnSave != null) _btnSave.Enabled = false;   // as saved == unchanged
            AuditLogger.Log("PLC Connection: changes discarded, the saved connections read again.");
        }
    }

    public class StationConnectionPage : ISettingsPage
    {
        public string TitleKey { get { return "Set_StationConn"; } }
        public string TitleFallback { get { return "PLC Connections"; } }
        // Maintenance reads it (the controllers' state); an Admin changes the
        // connections - see PlcConnectionScreen.
        public SecurityLevel RequiredLevel { get { return SecurityLevel.Maintenance; } }
        public UserControl GetControl() { return PlcConnectionScreen.Build(); }
    }
}

