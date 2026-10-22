using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.UI.Controls;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;

namespace Kaban.UI.Settings
{
    /// <summary>What the two backup windows share: their questions and messages (a test answers them).</summary>
    public static class PlcBackupUi
    {
        /// <summary>For the tests: answers a question or a message (its text, its buttons) instead of a dialog.</summary>
        internal static Func<string, MessageBoxButtons, DialogResult> Ask;
        public static string LastMessage = "";

        internal static string T(string key, string en) { return LanguageManager.GetString(key, en); }

        internal static DialogResult Say(IWin32Window owner, string text, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            LastMessage = text ?? "";
            if (Ask != null) return Ask(text, buttons);
            return MessageBox.Show(owner, text, T("Ttl_PlcBackup", "PLC data"), buttons, icon);
        }

        /// <summary>
        /// The simple motion modules of a PLC - those its axes on the Axis Settings pages are on - and
        /// each one's axes: 4 (FX5-40SSC-S) unless an axis above 4 is set (FX5-80SSC-S: 8).
        /// </summary>
        public static List<KeyValuePair<int, int>> ModulesOf(int station)
        {
            var most = new SortedDictionary<int, int>();
            foreach (var a in AlarmHelpLinks.AllAxes)
            {
                if (a.Station != station || a.Row == null) continue;
                object m, x;
                if (!a.Row.TryGetValue("MotionNumber", out m) || !a.Row.TryGetValue("MotionAxisIndex", out x)) continue;
                int module, axis;
                try { module = Convert.ToInt32(m); axis = Convert.ToInt32(x); } catch { continue; }
                if (module <= 0 || axis < 1 || axis > SimpleMotion.MaxAxes) continue;
                int had;
                most[module] = most.TryGetValue(module, out had) ? Math.Max(had, axis) : axis;
            }
            return most.Select(kv => new KeyValuePair<int, int>(kv.Key, kv.Value <= 4 ? 4 : 8)).ToList();
        }

        /// <summary>"Simple motion module 1 (4 axes)", or that none is set on the Axis Settings pages.</summary>
        public static string ModulesText(List<KeyValuePair<int, int>> modules)
        {
            if (modules == null || modules.Count == 0)
                return T("Msg_BakNoModules", "no simple motion module - no axis of this PLC is set on one on the Axis Settings pages");
            return string.Join(", ", modules.Select(m => string.Format(T("Msg_BakModuleAxes", "simple motion module {0} ({1} axes)"), m.Key, m.Value)));
        }

        /// <summary>The machine and the PLC as a backup names them.</summary>
        public static void Names(PlcConnectionSettings.Endpoint endpoint, out string machine, out string plc)
        {
            machine = PlcEndpointNaming.MachineName(endpoint);
            plc = !string.IsNullOrWhiteSpace(endpoint.StationName) ? endpoint.StationName : PlcEndpointNaming.Caption(endpoint);
        }
    }

    /// <summary>
    /// READ (22 October): the PLC's data into a backup - "save it with the machine name, PLC
    /// description, PLC serial number (read it from the PLC while reading the data if you can or
    /// leave empty), user name, date and the user's read description at the confirmation page".
    /// MX Component does not give a CPU's serial number (only its model): it is typed in - the
    /// last one typed for this PLC offered again.
    /// </summary>
    public sealed class PlcBackupReadForm : Form
    {
        private readonly PlcConnectionSettings.Endpoint _endpoint;
        private readonly List<KeyValuePair<int, int>> _modules;
        private readonly TextBox _serial = new TextBox { Name = "bakSerial" };
        private readonly TextBox _description = new TextBox { Name = "bakDescription", Multiline = true, ScrollBars = ScrollBars.Vertical };
        private readonly Label _cpu = new Label(), _date = new Label();
        private readonly string _machine, _plc;

        /// <summary>The backup saved (null until then) and its file.</summary>
        public PlcBackup.Backup Result { get; private set; }
        public string SavedPath { get; private set; }

        private static string T(string key, string en) { return PlcBackupUi.T(key, en); }

        public PlcBackupReadForm(PlcConnectionSettings.Endpoint endpoint)
        {
            _endpoint = endpoint;
            _modules = PlcBackupUi.ModulesOf(endpoint.StationId);
            PlcBackupUi.Names(endpoint, out _machine, out _plc);
            UiScale.ApplyOnLoad(this);
            Text = string.Format(T("Ttl_BakRead", "Read the PLC's data - {0}"), PlcEndpointNaming.Caption(endpoint));
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            BackColor = Color.White;
            Font = UiStyle.Body;
            ClientSize = new Size(620, 470);

            var driver = PlcManager.Instance.GetDriverForMonitoring(endpoint.StationId);
            var id = driver as IPlcIdentity;
            _cpu.Text = id != null && !string.IsNullOrEmpty(id.Identity) ? id.Identity : T("Lbl_BakCpuUnknown", "(the PLC does not say)");
            _date.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            var last = PlcBackup.List().FirstOrDefault(b => b.Machine == _machine && b.Plc == _plc && b.Serial.Length > 0);
            if (last != null) _serial.Text = last.Serial;

            var grid = new TableLayoutPanel { Location = new Point(14, 12), Size = new Size(592, 360), ColumnCount = 2 };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Action<string, Control, int> add = (caption, c, height) =>
            {
                int r = grid.RowCount++;
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
                grid.Controls.Add(new Label { Text = caption, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(90, 96, 106) }, 0, r);
                c.Dock = DockStyle.Fill;
                if (c is Label) { ((Label)c).TextAlign = ContentAlignment.MiddleLeft; ((Label)c).UseMnemonic = false; }
                grid.Controls.Add(c, 1, r);
            };
            add(T("Lbl_BakMachine", "Machine"), new Label { Text = _machine, Name = "bakMachine" }, 30);
            add(T("Lbl_BakPlc", "PLC"), new Label { Text = _plc, Name = "bakPlc" }, 30);
            add(T("Lbl_BakCpu", "CPU"), _cpu, 30);
            add(T("Lbl_BakSerial", "Serial number"), _serial, 32);
            add(T("Lbl_BakUser", "User"), new Label { Text = SecurityManager.CurrentUser, Name = "bakUser" }, 30);
            add(T("Lbl_BakDate", "Date"), _date, 30);
            add(T("Lbl_BakDescription", "Description"), _description, 84);
            var what = new Label
            {
                Name = "bakWhat", AutoSize = false,
                Text = string.Format(T("Msg_BakWhatIsRead", "Read: the CPU's devices (X, Y, M, L, B, F, S, T, ST, C, D, W, R - as far as the CPU has them) and {0}."),
                                     PlcBackupUi.ModulesText(_modules))
            };
            add(T("Lbl_BakWhat", "What is read"), what, 72);
            Controls.Add(grid);

            var tips = UiStyle.Tips();
            UiStyle.SetTip(tips, _serial, T("Tip_BakSerial", "The CPU's serial number, from its label - MX Component does not read it. The last one typed for this PLC is offered."));

            var read = UiStyle.TextButton(T("Btn_BakRead", "Read"), 110, (s, e) => DoRead());
            read.Name = "bakRead";
            var cancel = UiStyle.DialogButton(T("Btn_Cancel", "Cancel"), DialogResult.Cancel);
            read.Location = new Point(ClientSize.Width - 14 - cancel.Width - 10 - read.Width, ClientSize.Height - 14 - read.Height);
            cancel.Location = new Point(ClientSize.Width - 14 - cancel.Width, ClientSize.Height - 14 - cancel.Height);
            Controls.Add(read);
            Controls.Add(cancel);
            AcceptButton = read;
            CancelButton = cancel;
        }

        /// <summary>Reads the devices and the modules (a window with Cancel while it does), saves, says where.</summary>
        internal bool DoRead()
        {
            var driver = PlcManager.Instance.GetDriverForMonitoring(_endpoint.StationId);
            var block = driver as IPlcBlockAccess;
            if (driver == null || block == null || !driver.IsConnected || ConnectionMonitor.IsKnownDown(_endpoint.StationId))
            {
                PlcBackupUi.Say(this, T("Msg_BakNotConnected", "The PLC is not connected."), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            var backup = new PlcBackup.Backup
            {
                Machine = _machine, Plc = _plc, Cpu = _cpu.Text, Serial = _serial.Text.Trim(), User = SecurityManager.CurrentUser,
                Date = DateTime.Now, Station = _endpoint.StationId, Description = _description.Text.Trim(), Modules = _modules
            };
            if (backup.Cpu.StartsWith("(")) backup.Cpu = "";
            bool finished = WorkForm.Run(this, Text, T("Msg_BakFinding", "Asking the CPU how many of each device it has..."), p =>
            {
                var areas = PlcBackup.PlanDevices(driver, block);
                foreach (var m in _modules) areas.AddRange(PlcBackup.ModuleAreas(m.Key, m.Value));
                return PlcBackup.ReadAreas(block, backup, areas, (d, t, what) => { p.Report(d, t, what); return !p.Cancelled; });
            }, cancellable: true);
            if (!finished)
            {
                PlcBackupUi.Say(this, T("Msg_BakReadCancelled", "Cancelled - nothing was saved."), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            string path = PlcBackup.FileFor(backup);
            try { PlcBackup.Save(backup, path); }
            catch (Exception ex)
            {
                PlcBackupUi.Say(this, string.Format(T("Msg_BakSaveFailed", "The backup could not be saved: {0}"), ex.Message), MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            Result = backup;
            SavedPath = path;
            AuditLogger.Log(string.Format("PLC data read (station {0}, {1} {2}): {3} areas, {4} words into {5}{6}", _endpoint.StationId, _machine, _plc,
                                          backup.Areas.Count, backup.WordTotal, path, backup.Skipped.Count > 0 ? " - not read: " + string.Join(", ", backup.Skipped) : ""));
            string text = string.Format(T("Msg_BakSaved", "Saved: {0}\r\n{1} areas, {2} words."), Path.GetFileName(path), backup.Areas.Count, backup.WordTotal);
            if (backup.Skipped.Count > 0)
                text += "\r\n\r\n" + T("Msg_BakSkipped", "The PLC refused these - not in the backup:") + "\r\n" + string.Join("\r\n", backup.Skipped);
            PlcBackupUi.Say(this, text, MessageBoxButtons.OK, backup.Skipped.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
            return true;
        }
    }

    /// <summary>
    /// WRITE (22 October): "open the read backup list and selection of the devices to be written to
    /// the PLC (and another column for the entry if the read device interval and write device
    /// interval is different, e.g writing to D2500 to D3499 of the read data from R1000 to R1999 ...
    /// leaving this side empty is gonna mean that reading and writing interval is same)".
    /// The backups (this PLC's, or every one; another file opened); a backup's areas, each ticked to
    /// be written - all of it or the devices typed in "Read", to its own devices or to those typed in
    /// "Write to". A module's data written can be kept in its flash ROM at once.
    /// </summary>
    public sealed class PlcBackupWriteForm : Form
    {
        private readonly PlcConnectionSettings.Endpoint _endpoint;
        private readonly string _machine, _plc;
        private readonly DataGridView _backups = new DataGridView { Name = "bakList" };
        private readonly DataGridView _areas = new DataGridView { Name = "bakAreas" };
        private readonly CheckBox _onlyThis = new CheckBox { Name = "bakOnlyThis", AutoSize = true, Checked = true };
        private readonly CheckBox _flash = new CheckBox { Name = "bakFlash", AutoSize = true };
        private readonly Button _write;
        private readonly Label _summary = new Label { Name = "bakSummary", AutoSize = true, ForeColor = Color.FromArgb(90, 96, 106) };
        private List<PlcBackup.Backup> _list = new List<PlcBackup.Backup>();
        private PlcBackup.Backup _shown;
        private bool _filling;

        private static string T(string key, string en) { return PlcBackupUi.T(key, en); }

        /// <summary>The backup whose areas are shown (all its words read).</summary>
        public PlcBackup.Backup Shown { get { return _shown; } }

        public PlcBackupWriteForm(PlcConnectionSettings.Endpoint endpoint)
        {
            _endpoint = endpoint;
            PlcBackupUi.Names(endpoint, out _machine, out _plc);
            UiScale.ApplyOnLoad(this);
            Text = string.Format(T("Ttl_BakWrite", "Write a backup into the PLC - {0}"), PlcEndpointNaming.Caption(endpoint));
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1080, 720);
            MinimumSize = new Size(820, 520);
            ShowInTaskbar = false; MinimizeBox = false;
            BackColor = Color.White;
            Font = UiStyle.Body;

            // ---- the backups ----
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 8, 8, 2), WrapContents = false };
            _onlyThis.Text = string.Format(T("Chk_BakOnlyThis", "Only {0}'s backups"), (_machine + " " + _plc).Trim());
            _onlyThis.Margin = new Padding(3, 6, 16, 3);
            _onlyThis.CheckedChanged += (s, e) => FillList();
            top.Controls.Add(_onlyThis);
            top.Controls.Add(UiStyle.TextButton(T("Btn_BakOpenFile", "Open a file..."), 0, (s, e) => OpenFile()));
            top.Controls.Add(UiStyle.TextButton(T("Btn_BakFolder", "The backups' folder"), 0, (s, e) =>
            {
                try { Directory.CreateDirectory(PlcBackup.Folder); System.Diagnostics.Process.Start("explorer.exe", "\"" + PlcBackup.Folder + "\""); } catch { }
            }));

            Grid(_backups);
            _backups.Dock = DockStyle.Top;
            _backups.Height = 210;
            _backups.ReadOnly = true;
            _backups.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _backups.MultiSelect = false;
            foreach (var c in new[] { new[] { "Date", T("Col_BakDate", "Date"), "140" }, new[] { "Machine", T("Col_BakMachine", "Machine"), "150" },
                                      new[] { "Plc", T("Col_BakPlc", "PLC"), "150" }, new[] { "Cpu", T("Col_BakCpu", "CPU"), "120" },
                                      new[] { "Serial", T("Col_BakSerial", "Serial number"), "110" }, new[] { "User", T("Col_BakUser", "User"), "90" },
                                      new[] { "Description", T("Col_BakDescription", "Description"), "300" } })
                _backups.Columns.Add(new DataGridViewTextBoxColumn { Name = c[0], HeaderText = c[1], Width = int.Parse(c[2]) });
            _backups.SelectionChanged += (s, e) => { if (!_filling) ShowSelected(); };

            // ---- its areas ----
            var mid = new Label
            {
                Dock = DockStyle.Top, Height = 46, Padding = new Padding(10, 8, 10, 0), ForeColor = Color.FromArgb(70, 76, 86),
                Text = T("Msg_BakAreasHelp", "Tick what is written. Read: all of the area, or the devices typed (R1000 - R1999). " +
                                             "Write to: empty - the same devices; or where they go (D2500, or D2500 - D3499).")
            };
            Grid(_areas);
            _areas.Dock = DockStyle.Fill;
            _areas.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _areas.Columns.Add(new DataGridViewCheckBoxColumn { Name = "Write", HeaderText = T("Col_BakWrite", "Write"), Width = 56 });
            _areas.Columns.Add(new DataGridViewTextBoxColumn { Name = "Area", HeaderText = T("Col_BakArea", "Area"), Width = 330, ReadOnly = true });
            _areas.Columns.Add(new DataGridViewTextBoxColumn { Name = "Read", HeaderText = T("Col_BakRead", "Read"), Width = 170 });
            _areas.Columns.Add(new DataGridViewTextBoxColumn { Name = "Target", HeaderText = T("Col_BakTarget", "Write to"), Width = 170 });
            _areas.Columns.Add(new DataGridViewTextBoxColumn { Name = "Devices", HeaderText = T("Col_BakDevices", "Devices"), Width = 80, ReadOnly = true,
                                                               DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _areas.Columns.Add(new DataGridViewTextBoxColumn { Name = "Note", HeaderText = T("Col_BakNote", "Note"), Width = 240, ReadOnly = true });
            _areas.CurrentCellDirtyStateChanged += (s, e) => { if (_areas.IsCurrentCellDirty && _areas.CurrentCell is DataGridViewCheckBoxCell) _areas.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _areas.CellValueChanged += (s, e) => { if (!_filling && e.RowIndex >= 0) Check(_areas.Rows[e.RowIndex]); };

            // ---- writing ----
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(8, 10, 8, 6), FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            var close = UiStyle.DialogButton(T("Btn_Close", "Close"), DialogResult.Cancel);
            _write = UiStyle.TextButton(T("Btn_BakWrite", "Write..."), 110, (s, e) => DoWrite());
            _write.Name = "bakWrite";
            _flash.Text = T("Chk_BakFlash", "Then write the simple motion modules' flash ROM");
            _flash.Margin = new Padding(3, 7, 16, 3);
            bottom.Controls.Add(close);
            bottom.Controls.Add(_write);
            bottom.Controls.Add(_flash);
            bottom.Controls.Add(_summary);
            _summary.Margin = new Padding(3, 7, 16, 3);
            CancelButton = close;

            Controls.Add(_areas);
            Controls.Add(mid);
            Controls.Add(_backups);
            Controls.Add(top);
            Controls.Add(bottom);

            var tips = UiStyle.Tips();
            UiStyle.SetTip(tips, _flash, T("Tip_BakFlash", "The module keeps what is written until it is switched off - only what its flash ROM holds after that. Written only while its PLC READY signal is off."));
            FillList();
        }

        private static void Grid(DataGridView g)
        {
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.RowHeadersVisible = false;
            g.BackgroundColor = Color.White;
            UiStyle.GridLook(g);
        }

        /// <summary>The backups of the folder (this PLC's, or every one), the newest first.</summary>
        private void FillList(string select = null)
        {
            _filling = true;
            try
            {
                var all = PlcBackup.List();
                if (select != null && !all.Any(b => string.Equals(b.Path, select, StringComparison.OrdinalIgnoreCase)))
                {
                    var other = PlcBackup.Load(select, headerOnly: true);
                    if (other != null) all.Insert(0, other);
                }
                _list = all.Where(b => !_onlyThis.Checked || (b.Machine == _machine && b.Plc == _plc) ||
                                       string.Equals(b.Path, select, StringComparison.OrdinalIgnoreCase)).ToList();
                _backups.Rows.Clear();
                foreach (var b in _list)
                {
                    int r = _backups.Rows.Add(b.Date.ToString("yyyy-MM-dd HH:mm:ss"), b.Machine, b.Plc, b.Cpu, b.Serial, b.User, b.Description.Replace("\r\n", " / "));
                    _backups.Rows[r].Tag = b;
                }
                _backups.ClearSelection();
                int at = select == null ? (_list.Count > 0 ? 0 : -1) : _list.FindIndex(b => string.Equals(b.Path, select, StringComparison.OrdinalIgnoreCase));
                if (at >= 0) { _backups.Rows[at].Selected = true; _backups.CurrentCell = _backups.Rows[at].Cells[0]; }
            }
            finally { _filling = false; }
            ShowSelected();
        }

        private void OpenFile()
        {
            using (var dlg = new OpenFileDialog { Filter = T("Flt_PlcBackup", "PLC backups") + " (*" + PlcBackup.Extension + ")|*" + PlcBackup.Extension,
                                                  InitialDirectory = Directory.Exists(PlcBackup.Folder) ? PlcBackup.Folder : "" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Choose(dlg.FileName);
            }
        }

        /// <summary>A backup's file chosen (a USB stick's): listed and shown.</summary>
        internal void Choose(string path)
        {
            if (PlcBackup.Load(path, headerOnly: true) == null)
            {
                PlcBackupUi.Say(this, T("Msg_BakNotABackup", "This file is not a PLC backup."), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            FillList(path);
        }

        /// <summary>The selected backup read whole, its areas listed - none ticked.</summary>
        private void ShowSelected()
        {
            var row = _backups.SelectedRows.Count > 0 ? _backups.SelectedRows[0] : null;
            var head = row == null ? null : row.Tag as PlcBackup.Backup;
            _shown = head == null ? null : PlcBackup.Load(head.Path);
            _filling = true;
            try
            {
                _areas.Rows.Clear();
                if (_shown != null)
                    foreach (var a in _shown.Areas)
                    {
                        int r = _areas.Rows.Add(false, a.Title, a.Range, "", a.Points.ToString("N0"), a.Writable ? "" : T("Msg_BakNotWritable", "Inputs and outputs are not written back."));
                        _areas.Rows[r].Tag = a;
                        if (!a.Writable)
                        {
                            _areas.Rows[r].ReadOnly = true;
                            _areas.Rows[r].DefaultCellStyle.ForeColor = Color.FromArgb(150, 156, 166);
                        }
                    }
            }
            finally { _filling = false; }
            _flash.Visible = _shown != null && _shown.Areas.Any(a => a.IsModule);
            _flash.Checked = false;
            UpdateWrite();
        }

        /// <summary>A row's Read and Write to checked: its device count, or what is wrong, in its note.</summary>
        private PlcBackup.WriteRange Check(DataGridViewRow row)
        {
            var a = row.Tag as PlcBackup.Area;
            if (a == null) return null;
            int from, count; string error, target;
            PlcBackup.WriteRange part = null;
            if (!PlcBackup.TryParseRange(a, Convert.ToString(row.Cells["Read"].Value), out from, out count, out error) ||
                !PlcBackup.TryParseTarget(a, from, count, Convert.ToString(row.Cells["Target"].Value), out target, out error))
            {
                _filling = true;
                row.Cells["Note"].Value = error;
                row.Cells["Note"].Style.ForeColor = Color.FromArgb(190, 40, 30);
                _filling = false;
            }
            else
            {
                part = new PlcBackup.WriteRange { Source = a, From = from, Count = count, Target = target };
                _filling = true;
                row.Cells["Devices"].Value = count.ToString("N0");
                row.Cells["Note"].Value = string.Equals(target, a.Device(from), StringComparison.OrdinalIgnoreCase) ? "" : part.ToString();
                row.Cells["Note"].Style.ForeColor = Color.FromArgb(90, 96, 106);
                _filling = false;
            }
            UpdateWrite();
            return part;
        }

        /// <summary>The ticked rows as parts to write; false when a ticked row is wrong.</summary>
        internal bool Parts(out List<PlcBackup.WriteRange> parts)
        {
            parts = new List<PlcBackup.WriteRange>();
            bool ok = true;
            foreach (DataGridViewRow row in _areas.Rows)
            {
                if (!Convert.ToBoolean(row.Cells["Write"].Value)) continue;
                var a = row.Tag as PlcBackup.Area;
                int from, count; string error, target;
                if (a == null || !PlcBackup.TryParseRange(a, Convert.ToString(row.Cells["Read"].Value), out from, out count, out error) ||
                    !PlcBackup.TryParseTarget(a, from, count, Convert.ToString(row.Cells["Target"].Value), out target, out error))
                { ok = false; continue; }
                parts.Add(new PlcBackup.WriteRange { Source = a, From = from, Count = count, Target = target });
            }
            return ok;
        }

        private void UpdateWrite()
        {
            List<PlcBackup.WriteRange> parts;
            bool ok = Parts(out parts);
            _write.Enabled = ok && parts.Count > 0 && SecurityManager.HasAccess(Kaban.Core.Enums.SecurityLevel.Admin);
            _summary.Text = parts.Count == 0 ? "" : string.Format(T("Lbl_BakSummary", "{0} ticked, {1} devices"), parts.Count, parts.Sum(p => (long)p.Count).ToString("N0"));
        }

        /// <summary>The parts ticked written, after a question (and the modules' flash ROM, when ticked).</summary>
        internal PlcBackup.WriteResult DoWrite()
        {
            List<PlcBackup.WriteRange> parts;
            if (_shown == null || !Parts(out parts) || parts.Count == 0) return null;
            var driver = PlcManager.Instance.GetDriverForWrite(_endpoint.StationId);
            var block = driver as IPlcBlockAccess;
            if (driver == null || block == null || !driver.IsConnected)
            {
                PlcBackupUi.Say(this, T("Msg_BakNotConnectedWrite", "The PLC is not connected (or your user level may not write to it)."), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            string from = _shown.Date.ToString("yyyy-MM-dd HH:mm") + " - " + (_shown.Machine + " " + _shown.Plc).Trim();
            string question = string.Format(T("Msg_BakWriteAsk", "Write {0} part(s), {1} devices, of the backup of {2} into {3}?"),
                                            parts.Count, parts.Sum(p => (long)p.Count).ToString("N0"), from, PlcEndpointNaming.Caption(_endpoint)) +
                              "\r\n\r\n" + string.Join("\r\n", parts.Take(12).Select(p => "  " + p)) + (parts.Count > 12 ? "\r\n  ..." : "") +
                              "\r\n\r\n" + T("Msg_BakWriteWarn", "The PLC's values are overwritten at once - the machine should be stopped.");
            if (_shown.Machine != _machine || _shown.Plc != _plc)
                question += "\r\n\r\n" + string.Format(T("Msg_BakOtherPlc", "This backup is of another PLC ({0}) - not of {1}."),
                                                       (_shown.Machine + " " + _shown.Plc).Trim(), (_machine + " " + _plc).Trim());
            if (PlcBackupUi.Say(this, question, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return null;

            string source = string.Format("PLC Connection: the backup of {0} written", from);
            var result = WorkForm.Run(this, Text, T("Msg_BakWritingStart", "Writing..."), p =>
            {
                using (WriteSource.For(source))
                    return PlcBackup.Write(block, driver, parts, (d, t, what) => { p.Report(d, t, what); return !p.Cancelled; });
            }, cancellable: true);
            AuditLogger.Log(string.Format("PLC data written (station {0}) from {1}: {2} devices in {3} parts{4}{5}", _endpoint.StationId, _shown.Path,
                                          result.Points, parts.Count, result.Cancelled ? " - cancelled" : "",
                                          result.Failed.Count > 0 ? " - failed: " + string.Join(", ", result.Failed) : ""));
            string text = string.Format(T("Msg_BakWritten", "{0} devices written."), result.Points.ToString("N0"));
            if (result.Cancelled) text += "\r\n" + T("Msg_BakWriteCancelled", "Cancelled - the parts after it were not written.");
            if (result.Failed.Count > 0) text += "\r\n\r\n" + T("Msg_BakWriteFailed", "The PLC refused:") + "\r\n" + string.Join("\r\n", result.Failed);
            PlcBackupUi.Say(this, text, MessageBoxButtons.OK, result.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            // a module's data is gone at power off unless its flash ROM holds it
            if (_flash.Visible && _flash.Checked && result.Ok)
                foreach (var module in parts.Where(p => p.Source.IsModule).Select(p => ModuleOf(p.Target)).Where(m => m > 0).Distinct())
                    MotionFlash.Write(this, _endpoint.StationId, module);
            return result;
        }

        /// <summary>"U1\G150" - 1.</summary>
        private static int ModuleOf(string address)
        {
            var m = System.Text.RegularExpressions.Regex.Match(address ?? "", @"^U([0-9A-Fa-f]+)\\G", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            int n;
            return m.Success && int.TryParse(m.Groups[1].Value, out n) ? n : 0;
        }
    }
}
