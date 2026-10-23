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
        public List<int> AllAxes = new List<int>(), AllEncoders = new List<int>();
        public bool HasCommon;

        /// <summary>What is chosen.</summary>
        public readonly SortedSet<int> Axes = new SortedSet<int>(), Encoders = new SortedSet<int>();
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
                var place = PlcBackup.PlaceOf(a.First);
                if (place == null || place.Module != module || place.Group != group) continue;
                g.Areas.Add(new KeyValuePair<PlcBackup.Area, PlcBackup.ModulePlace>(a, place));
            }
            g.AllAxes = g.Areas.Where(kv => kv.Value.Axis > 0).Select(kv => kv.Value.Axis).Distinct().OrderBy(x => x).ToList();
            g.AllEncoders = g.Areas.Where(kv => kv.Value.Encoder > 0).Select(kv => kv.Value.Encoder).Distinct().OrderBy(x => x).ToList();
            g.HasCommon = g.Areas.Any(kv => kv.Value.Axis == 0 && kv.Value.Encoder == 0);
            return g;
        }

        /// <summary>The parts of it the details window lists (more than one: it has details).</summary>
        public int Total { get { return AllAxes.Count + AllEncoders.Count + (HasCommon ? 1 : 0); } }
        public int Chosen { get { return Axes.Count + Encoders.Count + (Common ? 1 : 0); } }
        public bool HasDetails { get { return Total > 1 || IsPositioning; } }

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
            Axes.Clear(); Encoders.Clear(); Numbers.Clear();
            Common = on && HasCommon;
            if (!on) return;
            foreach (var ax in AllAxes) Axes.Add(ax);
            foreach (var e in AllEncoders) Encoders.Add(e);
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
                bool chosen = place.Encoder > 0 ? Encoders.Contains(place.Encoder) : place.Axis > 0 ? Axes.Contains(place.Axis) : Common;
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
            var c = new PlcBackupGroup { Module = Module, Group = Group, AllAxes = AllAxes, AllEncoders = AllEncoders, HasCommon = HasCommon, Common = Common };
            c.Areas.AddRange(Areas);
            foreach (var x in Axes) c.Axes.Add(x);
            foreach (var x in Encoders) c.Encoders.Add(x);
            foreach (var kv in Numbers) c.Numbers[kv.Key] = (int[])kv.Value.Clone();
            return c;
        }

        public void TakeFrom(PlcBackupGroup c)
        {
            Common = c.Common;
            Axes.Clear(); foreach (var x in c.Axes) Axes.Add(x);
            Encoders.Clear(); foreach (var x in c.Encoders) Encoders.Add(x);
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
        private readonly Dictionary<int, CheckBox> _axes = new Dictionary<int, CheckBox>(), _encoders = new Dictionary<int, CheckBox>();
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
            _work.Numbers.Clear();
            foreach (var kv in _from)
            {
                int a = (int)kv.Value.Value, b = (int)_to[kv.Key].Value;
                if (a != 1 || b != _work.MaxNumber(kv.Key)) _work.Numbers[kv.Key] = new[] { Math.Min(a, b), Math.Max(a, b) };
            }
        }
    }
}
