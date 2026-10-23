using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// ONE GROUP OF A MODULE'S DATA TO WRITE (23 October: "It could be useful to group the write selection so
    /// the user could select them multiple and easier (the attached image show GX Works way for it)"): GX Works3's
    /// Positioning Data, Block Start Data, Parameter ... of a module - its areas in the backup, and what of them is
    /// chosen: the module's own (common parameters), its axes, its synchronous encoder axes, and an axis's
    /// positioning data numbers (No.1 - 100: the buffer memory's).
    /// </summary>
    public sealed class PlcBackupGroup
    {
        public int Module;
        public PlcBackup.ModuleGroup Group;
        /// <summary>The group's areas in the backup, each with where it belongs.</summary>
        public readonly List<KeyValuePair<PlcBackup.Area, PlcBackup.ModulePlace>> Areas = new List<KeyValuePair<PlcBackup.Area, PlcBackup.ModulePlace>>();

        /// <summary>What the backup has of the group.</summary>
        public List<int> AllAxes = new List<int>(), AllEncoders = new List<int>(), AllCams = new List<int>();
        public bool HasCommon;

        /// <summary>What is chosen.</summary>
        public readonly SortedSet<int> Axes = new SortedSet<int>(), Encoders = new SortedSet<int>(), Cams = new SortedSet<int>();
        public bool Common;
        /// <summary>Positioning data: an axis's numbers written, {first, last}.</summary>
        public readonly Dictionary<int, int[]> Numbers = new Dictionary<int, int[]>();

        public string Title { get { return PlcBackup.GroupTitle(Group); } }
        public bool IsPositioning { get { return Group == PlcBackup.ModuleGroup.Positioning; } }

        /// <summary>The positioning data an axis has in the backup (100).</summary>
        public int MaxNumber(int axis)
        {
            var a = Areas.FirstOrDefault(kv => kv.Value.Axis == axis && !kv.Value.Interpolation).Key;
            return a == null ? PlcBackup.PositioningData : Math.Max(1, a.Points / PlcBackup.PositioningWords);
        }

        public static PlcBackupGroup For(int module, PlcBackup.ModuleGroup group, IEnumerable<PlcBackup.Area> areas)
        {
            var g = new PlcBackupGroup { Module = module, Group = group };
            foreach (var a in areas)
            {
                var place = PlcBackup.PlaceOf(a);
                if (place == null || place.Module != module || place.Group != group) continue;
                g.Areas.Add(new KeyValuePair<PlcBackup.Area, PlcBackup.ModulePlace>(a, place));
            }
            g.AllAxes = g.Areas.Where(kv => kv.Value.Axis > 0).Select(kv => kv.Value.Axis).Distinct().OrderBy(x => x).ToList();
            g.AllEncoders = g.Areas.Where(kv => kv.Value.Encoder > 0).Select(kv => kv.Value.Encoder).Distinct().OrderBy(x => x).ToList();
            g.AllCams = g.Areas.Where(kv => kv.Value.Cam > 0).Select(kv => kv.Value.Cam).Distinct().OrderBy(x => x).ToList();
            g.HasCommon = g.Areas.Any(kv => kv.Value.Axis == 0 && kv.Value.Encoder == 0 && kv.Value.Cam == 0);
            return g;
        }

        /// <summary>The parts of it the details window lists (more than one: it has details).</summary>
        public int Total { get { return AllAxes.Count + AllEncoders.Count + AllCams.Count + (HasCommon ? 1 : 0); } }
        public int Chosen { get { return Axes.Count + Encoders.Count + Cams.Count + (Common ? 1 : 0); } }
        public bool HasDetails { get { return Total > 1 || IsPositioning || Group == PlcBackup.ModuleGroup.Cam; } }

        /// <summary>Every part chosen, every positioning data number.</summary>
        public bool Full
        {
            get
            {
                if (Chosen != Total) return false;
                return !IsPositioning || Axes.All(ax => { int[] n; return !Numbers.TryGetValue(ax, out n) || (n[0] == 1 && n[1] == MaxNumber(ax)); });
            }
        }

        public CheckState State { get { return Chosen == 0 ? CheckState.Unchecked : Full ? CheckState.Checked : CheckState.Indeterminate; } }

        /// <summary>Everything, or nothing.</summary>
        public void All(bool on)
        {
            Axes.Clear(); Encoders.Clear(); Cams.Clear(); Numbers.Clear();
            Common = on && HasCommon;
            if (!on) return;
            foreach (var ax in AllAxes) Axes.Add(ax);
            foreach (var e in AllEncoders) Encoders.Add(e);
            foreach (var c in AllCams) Cams.Add(c);
        }

        public int[] NumbersOf(int axis)
        {
            int[] n;
            return Numbers.TryGetValue(axis, out n) ? n : new[] { 1, MaxNumber(axis) };
        }

        /// <summary>"axes 1 - 8, No.1 - 100"; "common, axes 1, 2, encoder axes 1 - 4"; "" - nothing.</summary>
        public string Summary()
        {
            if (Chosen == 0) return "";
            var bits = new List<string>();
            if (Common) bits.Add(LanguageManager.GetString("Bak_SumCommon", "common"));
            if (Axes.Count > 0)
                bits.Add((Axes.Count == 1 ? LanguageManager.GetString("Bak_SumAxis", "axis") : LanguageManager.GetString("Bak_SumAxes", "axes")) + " " + Runs(Axes));
            if (Encoders.Count > 0)
                bits.Add((Encoders.Count == 1 ? LanguageManager.GetString("Bak_SumEncoder", "encoder axis") : LanguageManager.GetString("Bak_SumEncoders", "encoder axes")) + " " + Runs(Encoders));
            if (Cams.Count > 0)
                bits.Add((Cams.Count == 1 ? LanguageManager.GetString("Bak_SumCam", "cam No.") : LanguageManager.GetString("Bak_SumCams", "cams No.")) + Runs(Cams));
            if (IsPositioning && Axes.Count > 0)
            {
                var ranges = Axes.Select(ax => NumbersOf(ax)).Select(n => n[0] + " - " + n[1]).Distinct().ToList();
                bits.Add(ranges.Count == 1 ? "No." + ranges[0] : LanguageManager.GetString("Bak_SumNumbersEach", "No. as chosen for each"));
            }
            return string.Join(", ", bits);
        }

        /// <summary>"1 - 3, 5".</summary>
        internal static string Runs(IEnumerable<int> values)
        {
            var list = values.OrderBy(x => x).ToList();
            var parts = new List<string>();
            for (int i = 0; i < list.Count; )
            {
                int j = i;
                while (j + 1 < list.Count && list[j + 1] == list[j] + 1) j++;
                parts.Add(j - i >= 2 ? list[i] + " - " + list[j] : j > i ? list[i] + ", " + list[j] : list[i].ToString());
                i = j + 1;
            }
            return string.Join(", ", parts);
        }

        /// <summary>The parts written - into module <paramref name="target"/> (its own, or another).</summary>
        public List<PlcBackup.WriteRange> Parts(int target)
        {
            var list = new List<PlcBackup.WriteRange>();
            foreach (var kv in Areas)
            {
                var a = kv.Key; var place = kv.Value;
                bool chosen = place.Cam > 0 ? Cams.Contains(place.Cam) : place.Encoder > 0 ? Encoders.Contains(place.Encoder) : place.Axis > 0 ? Axes.Contains(place.Axis) : Common;
                if (!chosen) continue;
                int from = 0, count = a.Points;
                if (IsPositioning && place.Axis > 0)
                {
                    var n = NumbersOf(place.Axis);
                    from = Math.Min(a.Points, (n[0] - 1) * PlcBackup.PositioningWords);
                    count = Math.Max(0, Math.Min(a.Points - from, (n[1] - n[0] + 1) * PlcBackup.PositioningWords));
                }
                if (count <= 0) continue;
                list.Add(new PlcBackup.WriteRange { Source = a, From = from, Count = count, Target = PlcBackup.InModule(a.Device(from), target) });
            }
            return list;
        }

        /// <summary>A copy (the details window works on one: Cancel leaves the group as it was).</summary>
        public PlcBackupGroup Copy()
        {
            var c = new PlcBackupGroup { Module = Module, Group = Group, AllAxes = AllAxes, AllEncoders = AllEncoders, AllCams = AllCams, HasCommon = HasCommon, Common = Common };
            c.Areas.AddRange(Areas);
            foreach (var x in Axes) c.Axes.Add(x);
            foreach (var x in Encoders) c.Encoders.Add(x);
            foreach (var x in Cams) c.Cams.Add(x);
            foreach (var kv in Numbers) c.Numbers[kv.Key] = (int[])kv.Value.Clone();
            return c;
        }

        public void TakeFrom(PlcBackupGroup c)
        {
            Common = c.Common;
            Axes.Clear(); foreach (var x in c.Axes) Axes.Add(x);
            Encoders.Clear(); foreach (var x in c.Encoders) Encoders.Add(x);
            Cams.Clear(); foreach (var x in c.Cams) Cams.Add(x);
            Numbers.Clear(); foreach (var kv in c.Numbers) Numbers[kv.Key] = (int[])kv.Value.Clone();
        }
    }

    /// <summary>
    /// A GROUP'S DETAILS (GX Works3's "Positioning Data Detail Setting"): its parts ticked one by one, an axis's
    /// positioning data from - to; Select all, Deselect all.
    /// </summary>
    public sealed class PlcBackupDetailsForm : Form
    {
        private readonly PlcBackupGroup _work;
        private readonly CheckBox _common;
        private readonly Dictionary<int, CheckBox> _axes = new Dictionary<int, CheckBox>(), _encoders = new Dictionary<int, CheckBox>(), _cams = new Dictionary<int, CheckBox>();
        private readonly Dictionary<int, NumericUpDown> _from = new Dictionary<int, NumericUpDown>(), _to = new Dictionary<int, NumericUpDown>();

        /// <summary>What was chosen (the group's copy) - taken by the caller on OK.</summary>
        public PlcBackupGroup Result { get { return _work; } }

        private static string T(string key, string en) { return LanguageManager.GetString(key, en); }

        public PlcBackupDetailsForm(PlcBackupGroup group)
        {
            _work = group.Copy();
            UiScale.ApplyOnLoad(this);
            Text = string.Format(T("Ttl_BakDetails", "{0} - simple motion module {1}"), group.Title, group.Module);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            BackColor = Color.White;
            Font = UiStyle.Body;

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 8, 8, 2), WrapContents = false };
            var all = UiStyle.TextButton(T("Btn_BakSelectAll", "Select all"), 0, (s, e) => SetAll(true));
            var none = UiStyle.TextButton(T("Btn_BakDeselectAll", "Deselect all"), 0, (s, e) => SetAll(false));
            all.Name = "detAll"; none.Name = "detNone";
            top.Controls.Add(all); top.Controls.Add(none);

            var table = new TableLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(14, 6, 14, 6), ColumnCount = 4 };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Func<string, string, bool, CheckBox> box = (name, text, on) => new CheckBox { Name = name, Text = text, Checked = on, AutoSize = true, Margin = new Padding(3, 6, 16, 3) };
            int rows = 0;
            if (group.HasCommon)
            {
                _common = box("detCommon", group.Group == PlcBackup.ModuleGroup.Parameters ? T("Bak_DetCommon", "Common parameters") : T("Bak_DetModule", "The module's own"), _work.Common);
                table.Controls.Add(_common, 0, rows++);
            }
            foreach (var ax in group.AllAxes)
            {
                var cb = box("detAxis" + ax, string.Format(T("Bak_DetAxis", "Axis {0} - {1}"), ax, group.Title.ToLowerInvariant()), _work.Axes.Contains(ax));
                _axes[ax] = cb;
                table.Controls.Add(cb, 0, rows);
                if (group.IsPositioning)
                {
                    var n = _work.NumbersOf(ax);
                    int max = _work.MaxNumber(ax);
                    var from = new NumericUpDown { Name = "detFrom" + ax, Minimum = 1, Maximum = max, Width = 80, Margin = new Padding(3, 4, 3, 3) };
                    var to = new NumericUpDown { Name = "detTo" + ax, Minimum = 1, Maximum = max, Width = 80, Margin = new Padding(3, 4, 3, 3) };
                    from.Value = n[0];
                    to.Value = n[1];
                    from.ValueChanged += (s, e) => { if (to.Value < from.Value) to.Value = from.Value; };
                    to.ValueChanged += (s, e) => { if (from.Value > to.Value) from.Value = to.Value; };
                    _from[ax] = from; _to[ax] = to;
                    table.Controls.Add(from, 1, rows);
                    table.Controls.Add(new Label { Text = T("Lbl_BakTo", "to"), AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 2, rows);
                    table.Controls.Add(to, 3, rows);
                    Action enable = () => { from.Enabled = to.Enabled = cb.Checked; };
                    cb.CheckedChanged += (s, e) => enable();
                    enable();
                }
                rows++;
            }
            foreach (var j in group.AllEncoders)
            {
                var cb = box("detEncoder" + j, string.Format(T("Bak_DetEncoder", "Synchronous encoder axis {0}"), j), _work.Encoders.Contains(j));
                _encoders[j] = cb;
                table.Controls.Add(cb, 0, rows++);
            }
            foreach (var k in group.AllCams)
            {
                // the cam's own title: "cam No.3 (stroke ratio data, resolution 256)"
                var a = group.Areas.First(kv => kv.Value.Cam == k).Key;
                string title = a.Title.Contains(": ") ? a.Title.Substring(a.Title.IndexOf(": ", StringComparison.Ordinal) + 2) : a.Title;
                var cb = box("detCam" + k, title, _work.Cams.Contains(k));
                _cams[k] = cb;
                table.Controls.Add(cb, 0, rows++);
                table.SetColumnSpan(cb, 4);
            }
            table.RowCount = rows;

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(8, 10, 8, 6), FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            var cancel = UiStyle.DialogButton(T("Btn_Cancel", "Cancel"), DialogResult.Cancel);
            var ok = UiStyle.TextButton(T("Btn_OK", "OK"), 96, (s, e) => { Take(); DialogResult = DialogResult.OK; Close(); });
            ok.Name = "detOk";
            bottom.Controls.Add(cancel);
            bottom.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = cancel;

            Controls.Add(table);
            Controls.Add(top);
            Controls.Add(bottom);
            ClientSize = new Size(group.IsPositioning ? 520 : 420, Math.Min(620, 44 + 52 + 24 + rows * 34));
        }

        private void SetAll(bool on)
        {
            if (_common != null) _common.Checked = on;
            foreach (var cb in _axes.Values) cb.Checked = on;
            foreach (var cb in _encoders.Values) cb.Checked = on;
            foreach (var cb in _cams.Values) cb.Checked = on;
            if (!on) return;
            foreach (var kv in _from) { kv.Value.Value = 1; _to[kv.Key].Value = _to[kv.Key].Maximum; }
        }

        /// <summary>The boxes into the group's copy.</summary>
        internal void Take()
        {
            _work.Common = _common != null && _common.Checked;
            _work.Axes.Clear();
            foreach (var kv in _axes) if (kv.Value.Checked) _work.Axes.Add(kv.Key);
            _work.Encoders.Clear();
            foreach (var kv in _encoders) if (kv.Value.Checked) _work.Encoders.Add(kv.Key);
            _work.Cams.Clear();
            foreach (var kv in _cams) if (kv.Value.Checked) _work.Cams.Add(kv.Key);
            _work.Numbers.Clear();
            foreach (var kv in _from)
            {
                int a = (int)kv.Value.Value, b = (int)_to[kv.Key].Value;
                if (a != 1 || b != _work.MaxNumber(kv.Key)) _work.Numbers[kv.Key] = new[] { Math.Min(a, b), Math.Max(a, b) };
            }
        }
    }

    /// <summary>
    /// A BACKUP CHOSEN (23 October: the PLC Monitor's "Write to PLC from backup" - "The backup selection page should
    /// open (just for the backup selection btw, not for the writing device value interval selection)"): the PLC's
    /// backups, the newest first (every one's with the box unticked), another file opened; OK takes the one chosen.
    /// </summary>
    public sealed class PlcBackupChooseForm : Form
    {
        private readonly DataGridView _list = new DataGridView { Name = "chooseList" };
        private readonly CheckBox _onlyThis = new CheckBox { Name = "chooseOnlyThis", AutoSize = true, Checked = true };
        private readonly Button _ok;
        private readonly string _machine, _plc;
        private List<PlcBackup.Backup> _all = new List<PlcBackup.Backup>();
        private string _opened;

        /// <summary>The backup chosen, read whole (null until OK).</summary>
        public PlcBackup.Backup Result { get; private set; }

        private static string T(string key, string en) { return LanguageManager.GetString(key, en); }

        /// <summary>Tests: the window given to it instead of shown (it chooses, takes, and gives the result back).</summary>
        public static Func<PlcBackupChooseForm, DialogResult> Showing;

        /// <summary>The backup chosen to write <paramref name="what"/> from, read whole - null: none chosen.</summary>
        public static PlcBackup.Backup Choose(IWin32Window owner, PlcConnectionSettings.Endpoint endpoint, string what)
        {
            using (var f = new PlcBackupChooseForm(endpoint, what))
            {
                var r = Showing != null ? Showing(f) : f.ShowDialog(owner);
                return r == DialogResult.OK ? f.Result : null;
            }
        }

        /// <summary>The backups listed (tests).</summary>
        internal List<PlcBackup.Backup> Listed
        {
            get { return _list.Rows.Cast<DataGridViewRow>().Select(r => r.Tag as PlcBackup.Backup).Where(b => b != null).ToList(); }
        }

        /// <summary>The listed backup of <paramref name="path"/> selected (tests).</summary>
        internal bool Select(string path)
        {
            foreach (DataGridViewRow r in _list.Rows)
            {
                var b = r.Tag as PlcBackup.Backup;
                if (b == null || !string.Equals(b.Path, path, StringComparison.OrdinalIgnoreCase)) continue;
                _list.ClearSelection();
                r.Selected = true;
                _list.CurrentCell = r.Cells[0];
                return true;
            }
            return false;
        }

        /// <summary>"Only this PLC's backups" (tests).</summary>
        internal bool OnlyThis { get { return _onlyThis.Checked; } set { _onlyThis.Checked = value; } }

        public PlcBackupChooseForm(PlcConnectionSettings.Endpoint endpoint, string what)
        {
            PlcBackupUi.Names(endpoint, out _machine, out _plc);
            UiScale.ApplyOnLoad(this);
            Text = string.Format(T("Ttl_BakChoose", "The backup to write {0} from - {1}"), what, PlcEndpointNaming.Caption(endpoint));
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false; MinimizeBox = false;
            BackColor = Color.White;
            Font = UiStyle.Body;
            Size = new Size(900, 460);
            MinimumSize = new Size(600, 300);

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 8, 8, 2), WrapContents = false };
            _onlyThis.Text = string.Format(T("Chk_BakOnlyThis", "Only {0}'s backups"), (_machine + " " + _plc).Trim());
            _onlyThis.Margin = new Padding(3, 6, 16, 3);
            _onlyThis.CheckedChanged += (s, e) => Fill();
            top.Controls.Add(_onlyThis);
            top.Controls.Add(UiStyle.TextButton(T("Btn_BakOpenFile", "Open a file..."), 0, (s, e) => OpenFile()));

            _list.Dock = DockStyle.Fill;
            _list.AllowUserToAddRows = false; _list.AllowUserToDeleteRows = false; _list.RowHeadersVisible = false;
            _list.ReadOnly = true; _list.MultiSelect = false; _list.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            UiStyle.GridLook(_list);
            foreach (var c in new[] { new[] { "Date", T("Col_BakDate", "Date"), "140" }, new[] { "Machine", T("Col_BakMachine", "Machine"), "150" },
                                      new[] { "Plc", T("Col_BakPlc", "PLC"), "150" }, new[] { "User", T("Col_BakUser", "User"), "90" },
                                      new[] { "Description", T("Col_BakDescription", "Description"), "330" } })
                _list.Columns.Add(new DataGridViewTextBoxColumn { Name = c[0], HeaderText = c[1], Width = int.Parse(c[2]) });
            _list.SelectionChanged += (s, e) => { if (_ok != null) _ok.Enabled = _list.SelectedRows.Count > 0; };
            _list.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Take(); };

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(8, 10, 8, 6), FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
            var cancel = UiStyle.DialogButton(T("Btn_Cancel", "Cancel"), DialogResult.Cancel);
            _ok = UiStyle.TextButton(T("Btn_BakChooseOk", "Write from it..."), 0, (s, e) => Take());
            _ok.Name = "chooseOk";
            bottom.Controls.Add(cancel);
            bottom.Controls.Add(_ok);
            CancelButton = cancel;

            Controls.Add(_list);
            Controls.Add(top);
            Controls.Add(bottom);
            Fill();
        }

        /// <summary>The backups listed (the one opened from a file too), the newest first.</summary>
        internal void Fill()
        {
            _all = PlcBackup.List();
            if (_opened != null && !_all.Any(b => string.Equals(b.Path, _opened, StringComparison.OrdinalIgnoreCase)))
            {
                var other = PlcBackup.Load(_opened, headerOnly: true);
                if (other != null) _all.Insert(0, other);
            }
            var shown = _all.Where(b => !_onlyThis.Checked || (b.Machine == _machine && b.Plc == _plc) || string.Equals(b.Path, _opened, StringComparison.OrdinalIgnoreCase)).ToList();
            _list.Rows.Clear();
            foreach (var b in shown)
            {
                int r = _list.Rows.Add(b.Date.ToString("yyyy-MM-dd HH:mm:ss"), b.Machine, b.Plc, b.User, b.Description.Replace("\r\n", " / "));
                _list.Rows[r].Tag = b;
            }
            _list.ClearSelection();
            int at = _opened == null ? (shown.Count > 0 ? 0 : -1) : shown.FindIndex(b => string.Equals(b.Path, _opened, StringComparison.OrdinalIgnoreCase));
            if (at >= 0) { _list.Rows[at].Selected = true; _list.CurrentCell = _list.Rows[at].Cells[0]; }
            if (_ok != null) _ok.Enabled = _list.SelectedRows.Count > 0;
        }

        private void OpenFile()
        {
            using (var dlg = new OpenFileDialog { Filter = T("Flt_PlcBackup", "PLC backups") + " (*" + PlcBackup.Extension + ")|*" + PlcBackup.Extension,
                                                  InitialDirectory = System.IO.Directory.Exists(PlcBackup.Folder) ? PlcBackup.Folder : "" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                Choose(dlg.FileName);
            }
        }

        /// <summary>A file chosen (a USB stick's): listed and selected.</summary>
        internal void Choose(string path)
        {
            if (PlcBackup.Load(path, headerOnly: true) == null) return;
            _opened = path;
            Fill();
        }

        /// <summary>The selected backup read whole - the window closed with OK.</summary>
        internal void Take()
        {
            var row = _list.SelectedRows.Count > 0 ? _list.SelectedRows[0] : null;
            var head = row == null ? null : row.Tag as PlcBackup.Backup;
            if (head == null) return;
            Result = PlcBackup.Load(head.Path);
            if (Result == null) return;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
