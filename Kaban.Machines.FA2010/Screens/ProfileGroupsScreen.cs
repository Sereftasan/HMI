using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;
using Kaban.UI.Controls;
using Kaban.UI.Profiles;

namespace Kaban.Machines.FA2010.Screens
{
    /// <summary>
    /// THE PROFILE GROUPS AND THEIR ACCESSORIES (11 November: "I don't quite understand how to use the profile and accessory entry pages, can
    /// you check the old program and create the screens closer to it (with the profile group and accessory group relations)" - "I want you to
    /// check them from the program"): as the old program's Preparation > Group Mapping (ucGroupMapping / ucTreeGroups: a tree of the profile
    /// groups, a right click giving a group its receipts from a ticked list - ucCheckedList) and its Company Profiles toolbar (Profile Brands,
    /// Profile Series: dialogs of their own).
    ///
    ///   the tree       brand (vendor) > series > profile group > its profiles / its accessory groups (their accessories) / its accessories
    ///                  one by one
    ///   a group        two ticked lists: the accessory groups it has (all their accessories, those added later too) and the accessories
    ///                  one by one (one of an attached accessory group comes with it - unticked there: off for this group only)
    ///   an accessory   its operations (macro, where, along the bar)
    ///   a profile      what it is and how far its accessories' places are confirmed
    ///
    /// Every profile of a group takes the group's accessories; their places on each profile are on the Profiles page. Changed at the
    /// Maintenance level; the library's tables (brands, series, groups, accessories, their operations) from the buttons above.
    /// </summary>
    public class ProfileGroupsScreen : UserControl
    {
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        private readonly TreeView _tree = new TreeView();
        private readonly Label _head = new Label(), _status = new Label();
        private readonly DataGridView _accGroups = new DataGridView(), _accessories = new DataGridView(), _ops = new DataGridView();
        private readonly Panel _groupView = new Panel(), _accessoryView = new Panel(), _profileView = new Panel();
        private readonly Label _profileText = new Label();
        private readonly ToolTip _tips = UiStyle.Tips();
        private readonly ContextMenuStrip _menu = new ContextMenuStrip();
        private bool _libraryTried, _libraryOk, _filling;
        private string _libraryWhy = "";
        private long _instance;
        /// <summary>The profile group shown on the right (0: none).</summary>
        internal long GroupId { get; private set; }
        private long _accessoryId;
        private readonly List<Button> _editButtons = new List<Button>();

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }
        private static bool CanEdit { get { return SecurityManager.HasAccess(SecurityLevel.Maintenance); } }

        public ProfileGroupsScreen()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = UiStyle.Body;

            // ---- the old program's Company Profiles toolbar: the library's tables, each a dialog of its own -----------------------
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, WrapContents = false, Padding = new Padding(6, 6, 0, 0), BackColor = Color.FromArgb(246, 247, 249) };
            Func<string, string, string, Button> lib = (key, text, section) =>
            {
                var b = UiStyle.TextButton(T(key, text), 0, (s, e) => OpenLibrary(section));
                b.Margin = new Padding(0, 0, 6, 0);
                bar.Controls.Add(b);
                return b;
            };
            lib("Grp_BtnBrands", "Profile brands", "vendors");
            lib("Grp_BtnSeries", "Profile series", "series");
            lib("Grp_BtnGroups", "Profile groups", "groups");
            lib("Grp_BtnAccGroups", "Accessory groups", "acc_groups");
            lib("Grp_BtnAccessories", "Accessories and their operations", "accessories");
            var refresh = UiStyle.TextButton("↻ " + T("Grp_BtnRefresh", "Read again"), 0, (s, e) => Reload());
            refresh.Margin = new Padding(12, 0, 0, 0);
            bar.Controls.Add(refresh);

            // ---- the tree --------------------------------------------------------------------------------------------------------
            _tree.Dock = DockStyle.Fill;
            _tree.HideSelection = false;
            _tree.Font = UiStyle.Body;
            _tree.ItemHeight = Math.Max(_tree.ItemHeight, UiScale.Px(24));
            _tree.AfterSelect += (s, e) => { if (!_filling) ShowNode(e.Node); };
            _tree.NodeMouseClick += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                _tree.SelectedNode = e.Node;
                if (GroupOf(e.Node) > 0) _menu.Show(_tree, e.Location);
            };
            // a right click on a group (the old program's "Receipts" - a ticked list)
            _menu.Items.Add(T("Grp_MenuAccGroups", "Its accessory groups..."), null, (s, e) => { SelectGroupNode(); if (_accGroups.Rows.Count > 0) _accGroups.Focus(); });
            _menu.Items.Add(T("Grp_MenuAccessories", "Its accessories one by one..."), null, (s, e) => { SelectGroupNode(); if (_accessories.Rows.Count > 0) _accessories.Focus(); });
            _menu.Items.Add(T("Grp_MenuEditGroup", "The group in the library (its name, type, reference profile)..."), null, (s, e) => OpenLibrary("groups"));

            // ---- a group: its accessory groups and its accessories one by one ---------------------------------------------------
            Grid(_accGroups);
            _accGroups.Columns.Add(new DataGridViewCheckBoxColumn { Name = "on", HeaderText = "", Width = 36 });
            Text(_accGroups, "name", T("Grp_ColAccGroup", "Accessory group"), 200);
            Text(_accGroups, "type", T("Grp_ColType", "Type"), 120);
            Text(_accGroups, "count", T("Grp_ColAccessories", "Accessories"), 90);
            _accGroups.Columns["on"].ToolTipText = T("Grp_TipAccGroupOn", "Ticked: every accessory of the group - those added to it later too - on every profile of this profile group");
            _accGroups.CellValueChanged += (s, e) => { if (!_filling && e.RowIndex >= 0 && e.ColumnIndex == 0) AccessoryGroupTicked(e.RowIndex); };
            Grid(_accessories);
            _accessories.Columns.Add(new DataGridViewCheckBoxColumn { Name = "on", HeaderText = "", Width = 36 });
            Text(_accessories, "code", T("Grp_ColCode", "Accessory"), 150);
            Text(_accessories, "description", T("Grp_ColDescription", "Description"), 220);
            Text(_accessories, "group", T("Grp_ColAccGroup", "Accessory group"), 140);
            Text(_accessories, "how", T("Grp_ColHow", "On this profile group"), 230);
            Text(_accessories, "ops", T("Grp_ColOps", "Operations"), 80);
            _accessories.CellValueChanged += (s, e) => { if (!_filling && e.RowIndex >= 0 && e.ColumnIndex == 0) AccessoryTicked(e.RowIndex); };
            _accessories.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) ShowAccessory((long)_accessories.Rows[e.RowIndex].Tag); };
            foreach (var g in new[] { _accGroups, _accessories })
            {
                var grid = g;
                grid.CurrentCellDirtyStateChanged += (s, e) => { if (grid.IsCurrentCellDirty && grid.CurrentCell is DataGridViewCheckBoxCell) grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            }
            var groupSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
            groupSplit.Panel1.Controls.Add(_accGroups);
            groupSplit.Panel1.Controls.Add(Caption(T("Grp_HeadAccGroups", "Its accessory groups - ticked: all their accessories, those added to them later too")));
            groupSplit.Panel2.Controls.Add(_accessories);
            groupSplit.Panel2.Controls.Add(Caption(T("Grp_HeadAccessories", "Its accessories - ticked: on every profile of the group (one of a ticked accessory group comes with it, unticked: not on this group)")));
            _groupView.Dock = DockStyle.Fill;
            _groupView.Controls.Add(groupSplit);
            HandleCreated += (s, e) => { try { groupSplit.SplitterDistance = Math.Max(120, groupSplit.Height / 3); } catch { } };

            // ---- an accessory: its operations -------------------------------------------------------------------------------
            Grid(_ops);
            _ops.ReadOnly = true;
            Text(_ops, "seq", "#", 40);
            Text(_ops, "macro", T("Grp_ColMacro", "Macro"), 160);
            Text(_ops, "where", T("Grp_ColWhere", "Where on the profile"), 260);
            Text(_ops, "along", T("Grp_ColAlong", "Along the bar"), 260);
            var edit = UiStyle.TextButton(T("Grp_BtnEditAccessory", "Its operations in the library..."), 0, (s, e) => OpenLibrary("accessories"));
            edit.Dock = DockStyle.Bottom;
            _accessoryView.Dock = DockStyle.Fill;
            _accessoryView.Controls.Add(_ops);
            _accessoryView.Controls.Add(edit);
            _accessoryView.Visible = false;

            // ---- a profile -----------------------------------------------------------------------------------------------------
            _profileText.Dock = DockStyle.Fill;
            _profileText.Padding = new Padding(10);
            _profileText.Font = UiStyle.Body;
            _profileView.Dock = DockStyle.Fill;
            _profileView.Controls.Add(_profileText);
            _profileView.Visible = false;

            _head.Dock = DockStyle.Top;
            _head.Height = 48;
            _head.Font = new Font(UiStyle.Body, FontStyle.Bold);
            _head.ForeColor = Color.FromArgb(40, 50, 65);
            _head.Padding = new Padding(8, 4, 8, 0);
            var right = new Panel { Dock = DockStyle.Fill };
            right.Controls.Add(_groupView);
            right.Controls.Add(_accessoryView);
            right.Controls.Add(_profileView);
            right.Controls.Add(_head);
            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 6 };
            split.Panel1.Controls.Add(_tree);
            split.Panel2.Controls.Add(right);
            HandleCreated += (s, e) => { try { split.SplitterDistance = Math.Max(240, split.Width * 3 / 10); } catch { } };
            _status.Dock = DockStyle.Bottom;
            _status.Height = 24;
            _status.Font = UiStyle.Small;
            _status.ForeColor = Color.FromArgb(70, 76, 86);
            Controls.Add(split);
            Controls.Add(bar);
            Controls.Add(_status);
            _editButtons.AddRange(bar.Controls.OfType<Button>().Where(b => b != refresh));

            EventHandler userChanged = (s, e) => { if (IsHandleCreated) BeginInvoke((Action)ApplyAccess); };
            SecurityManager.UserChanged += userChanged;
            Disposed += (s, e) => SecurityManager.UserChanged -= userChanged;
            VisibleChanged += (s, e) => { if (Visible) EnsureLibrary(); };
            ApplyAccess();
        }

        private static void Grid(DataGridView g)
        {
            g.Dock = DockStyle.Fill;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.RowHeadersVisible = false;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            UiStyle.StyleGrid(g);
        }

        private static void Text(DataGridView g, string name, string header, int width)
        {
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = header, Width = width, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable });
        }

        private static Label Caption(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Top, Height = 26, Font = UiStyle.Small, ForeColor = Color.FromArgb(60, 70, 85), Padding = new Padding(6, 6, 6, 0),
                               BackColor = Color.FromArgb(246, 247, 249) };
        }

        internal void Status(string text, bool problem = false)
        {
            _status.Text = text ?? "";
            _status.ForeColor = problem ? Color.FromArgb(180, 40, 40) : Color.FromArgb(70, 76, 86);
        }

        private void ApplyAccess()
        {
            if (IsDisposed) return;
            bool edit = CanEdit && _libraryOk;
            foreach (var b in _editButtons) b.Enabled = edit;
            _accGroups.Columns["on"].ReadOnly = !edit;
            _accessories.Columns["on"].ReadOnly = !edit;
            _menu.Enabled = edit;
        }

        // =====================================================================
        // THE LIBRARY
        // =====================================================================

        internal void EnsureLibrary()
        {
            if (_libraryTried) return;
            _libraryTried = true;
            string why;
            _libraryOk = Library.Open(() => Ask.YesNo(FindForm(), T("Btn_ProfileGroups", "Profile groups"),
                T("Prf_AskExamples", "The profile library is new. Load the example catalogue?\r\n\r\n" +
                  "It holds hardware of several makers with EXAMPLE positions, and the three profiles this page had. " +
                  "Nothing in it is confirmed, and nothing unconfirmed goes into a production program.")), out why);
            _libraryWhy = why ?? "";
            if (_libraryOk)
            {
                try { _instance = Library.EnsureMachine("FA2010", "FA2010", ProfilesScreen.ToolPlaces, ProfilesScreen.MachineFaces()); }
                catch (Exception ex) { _libraryWhy = ex.Message; }
            }
            if (!_libraryOk) Status(T("Prf_NoLibrary", "The profile library could not be opened:") + " " + _libraryWhy, true);
            ApplyAccess();
            Reload();
        }

        private void OpenLibrary(string section)
        {
            EnsureLibrary();
            if (!_libraryOk) return;
            bool changed;
            using (var f = new LibraryEditorForm(_instance, section))
            {
                f.ShowDialog(FindForm());
                changed = f.Changed;
            }
            if (changed) Reload();
        }

        // =====================================================================
        // THE TREE: brand > series > profile group > its profiles / accessory groups / accessories
        // =====================================================================

        private sealed class NodeTag
        {
            public string Kind = "";        // vendor, series, group, profiles, accgroups, accessories, profile, accgroup, accessory
            public long Id, Group;
        }

        /// <summary>The tree read again - the node shown kept.</summary>
        internal void Reload()
        {
            if (!_libraryOk) return;
            var keep = _tree.SelectedNode != null ? (NodeTag)_tree.SelectedNode.Tag : null;
            _filling = true;
            try
            {
                _tree.BeginUpdate();
                _tree.Nodes.Clear();
                var groups = Library.Rows(@"SELECT g.id, g.name, g.profile_type, vr.name AS variant, s.id AS series_id, s.name AS series, ven.id AS vendor_id, ven.name AS vendor
                                            FROM profile_group g JOIN profile_variant vr ON vr.id = g.variant_id JOIN profile_series s ON s.id = vr.series_id
                                            JOIN profile_vendor ven ON ven.id = s.vendor_id ORDER BY ven.name, s.name, vr.name, g.name");
                var profiles = Library.Profiles(true);
                foreach (var vendor in groups.GroupBy(r => Convert.ToInt64(r["vendor_id"])))
                {
                    var vn = Add(_tree.Nodes, Str(vendor.First()["vendor"]), "vendor", vendor.Key, 0);
                    foreach (var series in vendor.GroupBy(r => Convert.ToInt64(r["series_id"])))
                    {
                        var sn = Add(vn.Nodes, Str(series.First()["series"]), "series", series.Key, 0);
                        foreach (var g in series)
                        {
                            long gid = Convert.ToInt64(g["id"]);
                            string variant = Str(g["variant"]);
                            var mine = profiles.Where(p => p.GroupId == gid).ToList();
                            var accs = Library.GroupAccessories(gid).Where(a => Convert.ToInt32(a["is_enabled"]) == 1).ToList();
                            string text = Str(g["name"]) + (variant.Length > 0 && !string.Equals(variant, "Standard", StringComparison.OrdinalIgnoreCase) ? " (" + variant + ")" : "") +
                                          "   " + string.Format(T("Grp_NodeCounts", "{0} profile(s), {1} accessory(ies)"), mine.Count, accs.Count);
                            var gn = Add(sn.Nodes, text, "group", gid, gid);
                            var pn = Add(gn.Nodes, string.Format(T("Grp_NodeProfiles", "Profiles ({0})"), mine.Count), "profiles", 0, gid);
                            foreach (var p in mine) Add(pn.Nodes, p.Code + (string.IsNullOrWhiteSpace(p.Description) ? "" : "  " + p.Description) + (p.Active ? "" : "  (" + T("Grp_Inactive", "put aside") + ")"),
                                                        "profile", p.Id, gid);
                            var attached = Library.AccessoryGroupsOf(gid).Where(a => Convert.ToInt32(a["attached"]) == 1).ToList();
                            var an = Add(gn.Nodes, string.Format(T("Grp_NodeAccGroups", "Accessory groups ({0})"), attached.Count), "accgroups", 0, gid);
                            foreach (var ag in attached)
                            {
                                long agid = Convert.ToInt64(ag["id"]);
                                var agn = Add(an.Nodes, Str(ag["name"]), "accgroup", agid, gid);
                                foreach (var a in accs.Where(x => Str(x["via_group"]) == Str(ag["name"])))
                                    Add(agn.Nodes, Str(a["code"]), "accessory", Convert.ToInt64(a["accessory_id"]), gid);
                            }
                            var single = accs.Where(x => Str(x["via_group"]).Length == 0).ToList();
                            var on = Add(gn.Nodes, string.Format(T("Grp_NodeSingle", "Accessories one by one ({0})"), single.Count), "accessories", 0, gid);
                            foreach (var a in single) Add(on.Nodes, Str(a["code"]), "accessory", Convert.ToInt64(a["accessory_id"]), gid);
                        }
                        sn.Expand();
                    }
                    vn.Expand();
                }
                _tree.EndUpdate();
            }
            finally { _filling = false; }
            var again = keep != null ? Find(_tree.Nodes, keep) : null;
            if (again == null) again = FirstGroup(_tree.Nodes);
            if (again != null) _tree.SelectedNode = again;
            else ShowNode(null);
            Status(string.Format(T("Grp_Status", "{0} profile group(s). A right click on a group gives it its accessory groups and accessories, the places on each profile are on the Profiles page."),
                                 CountKind(_tree.Nodes, "group")));
        }

        private static string Str(object v) { return v == null || v == DBNull.Value ? "" : Convert.ToString(v, CI); }

        private static TreeNode Add(TreeNodeCollection to, string text, string kind, long id, long group)
        {
            var n = new TreeNode(text) { Tag = new NodeTag { Kind = kind, Id = id, Group = group } };
            to.Add(n);
            return n;
        }

        private static TreeNode Find(TreeNodeCollection nodes, NodeTag tag)
        {
            foreach (TreeNode n in nodes)
            {
                var t = (NodeTag)n.Tag;
                if (t.Kind == tag.Kind && t.Id == tag.Id && t.Group == tag.Group) return n;
                var f = Find(n.Nodes, tag);
                if (f != null) return f;
            }
            return null;
        }

        private static TreeNode FirstGroup(TreeNodeCollection nodes)
        {
            foreach (TreeNode n in nodes)
            {
                if (((NodeTag)n.Tag).Kind == "group") return n;
                var f = FirstGroup(n.Nodes);
                if (f != null) return f;
            }
            return null;
        }

        private static int CountKind(TreeNodeCollection nodes, string kind)
        {
            int k = 0;
            foreach (TreeNode n in nodes) k += (((NodeTag)n.Tag).Kind == kind ? 1 : 0) + CountKind(n.Nodes, kind);
            return k;
        }

        private static long GroupOf(TreeNode n) { return n != null ? ((NodeTag)n.Tag).Group : 0; }

        /// <summary>The selected node's group node selected (a right click on one of its rows).</summary>
        private void SelectGroupNode()
        {
            var n = _tree.SelectedNode;
            while (n != null && ((NodeTag)n.Tag).Kind != "group") n = n.Parent;
            if (n != null) _tree.SelectedNode = n;
        }

        /// <summary>The group with this id selected (tests, and the Profiles page).</summary>
        internal bool SelectGroup(long groupId)
        {
            var n = Find(_tree.Nodes, new NodeTag { Kind = "group", Id = groupId, Group = groupId });
            if (n == null) return false;
            _tree.SelectedNode = n;
            return true;
        }

        // =====================================================================
        // THE RIGHT SIDE
        // =====================================================================

        private void ShowNode(TreeNode node)
        {
            var tag = node != null ? (NodeTag)node.Tag : null;
            _groupView.Visible = _accessoryView.Visible = _profileView.Visible = false;
            if (tag == null) { _head.Text = T("Grp_Choose", "Choose a profile group on the left."); GroupId = 0; return; }
            switch (tag.Kind)
            {
                case "accessory": ShowAccessory(tag.Id); return;
                case "profile": ShowProfile(tag.Id); return;
                case "vendor":
                case "series":
                    GroupId = 0;
                    _head.Text = node.Text + " - " + T("Grp_ChooseGroup", "choose one of its profile groups.");
                    return;
                default: ShowGroup(tag.Group); return;
            }
        }

        internal void ShowGroup(long groupId)
        {
            GroupId = groupId;
            _accessoryId = 0;
            var g = Library.Rows(@"SELECT g.name, g.profile_type, s.name AS series, ven.name AS vendor,
                                         (SELECT code FROM profile WHERE id = g.reference_profile_id) AS reference,
                                         (SELECT COUNT(*) FROM profile p WHERE p.group_id = g.id AND p.is_active = 1) AS profiles
                                  FROM profile_group g JOIN profile_variant vr ON vr.id = g.variant_id JOIN profile_series s ON s.id = vr.series_id
                                  JOIN profile_vendor ven ON ven.id = s.vendor_id WHERE g.id = @p0", groupId).FirstOrDefault();
            if (g == null) { _head.Text = ""; return; }
            _head.Text = string.Format(T("Grp_HeadGroup", "{0} / {1} / {2} ({3}) - {4} profile(s): every one takes the accessories below{5}"),
                Str(g["vendor"]), Str(g["series"]), Str(g["name"]), Str(g["profile_type"]), Str(g["profiles"]),
                Str(g["reference"]).Length > 0 ? string.Format(T("Grp_HeadReference", ", its reference profile {0}"), Str(g["reference"])) : "");
            _filling = true;
            try
            {
                _accGroups.Rows.Clear();
                var attached = new HashSet<long>();
                foreach (var r in Library.AccessoryGroupsOf(groupId))
                {
                    bool on = Convert.ToInt32(r["attached"]) == 1;
                    if (on) attached.Add(Convert.ToInt64(r["id"]));
                    int i = _accGroups.Rows.Add(on, Str(r["name"]), Str(r["accessory_type"]), Str(r["accessories"]));
                    _accGroups.Rows[i].Tag = Convert.ToInt64(r["id"]);
                }
                _accessories.Rows.Clear();
                var mine = Library.GroupAccessories(groupId).ToDictionary(r => Convert.ToInt64(r["accessory_id"]), r => r);
                foreach (var a in Library.Rows(@"SELECT a.id, a.code, a.description, a.group_id, ag.name AS acc_group,
                                                        (SELECT COUNT(*) FROM accessory_operation op WHERE op.accessory_id = a.id) AS ops
                                                 FROM accessory a JOIN accessory_group ag ON ag.id = a.group_id WHERE a.is_active = 1 ORDER BY ag.name, a.code"))
                {
                    long id = Convert.ToInt64(a["id"]);
                    bool viaGroup = attached.Contains(Convert.ToInt64(a["group_id"]));
                    Dictionary<string, object> m;
                    bool hasRow = mine.TryGetValue(id, out m);
                    bool on = hasRow && Convert.ToInt32(m["is_enabled"]) == 1;
                    bool own = hasRow && Str(m["via_group"]).Length == 0;
                    string how = on ? (own ? T("Grp_HowOwn", "given one by one") : string.Format(T("Grp_HowVia", "with its accessory group {0}"), Str(a["acc_group"])))
                               : viaGroup ? T("Grp_HowOff", "off for this group (its accessory group is ticked)") : "";
                    int i = _accessories.Rows.Add(on, Str(a["code"]), Str(a["description"]), Str(a["acc_group"]), how, Str(a["ops"]));
                    var row = _accessories.Rows[i];
                    row.Tag = id;
                    if (on && !own) row.DefaultCellStyle.ForeColor = Color.FromArgb(30, 90, 170);
                    if (!on && viaGroup) row.DefaultCellStyle.ForeColor = Color.FromArgb(150, 90, 0);
                }
            }
            finally { _filling = false; }
            _groupView.Visible = true;
        }

        private void AccessoryGroupTicked(int row)
        {
            if (!CanEdit || GroupId <= 0) { ShowGroup(GroupId); return; }
            long ag = (long)_accGroups.Rows[row].Tag;
            bool on = Convert.ToBoolean(_accGroups.Rows[row].Cells["on"].Value ?? false);
            Library.AttachAccessoryGroup(GroupId, ag, on);
            AuditLogger.Log(string.Format(CI, "profile library: profile group {0} - accessory group {1} {2}", GroupId, _accGroups.Rows[row].Cells["name"].Value, on ? "attached" : "taken off"));
            Status(string.Format(on ? T("Grp_AttachedGroup", "{0}: all its accessories on every profile of the group.") : T("Grp_DetachedGroup", "{0}: taken off the group."),
                                 _accGroups.Rows[row].Cells["name"].Value));
            Later(Reload);
        }

        /// <summary>
        /// An accessory ticked or unticked for the group: one of a ticked accessory group is on with it - unticked it is switched off for this
        /// group only (ticked again: with its group once more); another is given one by one, or taken off.
        /// </summary>
        private void AccessoryTicked(int row)
        {
            if (!CanEdit || GroupId <= 0) { ShowGroup(GroupId); return; }
            long acc = (long)_accessories.Rows[row].Tag;
            bool on = Convert.ToBoolean(_accessories.Rows[row].Cells["on"].Value ?? false);
            long accGroup = Convert.ToInt64(Library.Rows("SELECT group_id FROM accessory WHERE id=@p0", acc).First()["group_id"]);
            bool viaGroup = Library.AccessoryGroupsOf(GroupId).Any(r => Convert.ToInt64(r["id"]) == accGroup && Convert.ToInt32(r["attached"]) == 1);
            if (viaGroup)
            {
                if (on) Library.UnassignAccessory(GroupId, acc);          // with its accessory group again
                else Library.AssignAccessory(GroupId, acc, false);       // off for this group only
            }
            else
            {
                if (on) Library.AssignAccessory(GroupId, acc, true);
                else Library.UnassignAccessory(GroupId, acc);
            }
            string code = Convert.ToString(_accessories.Rows[row].Cells["code"].Value);
            AuditLogger.Log(string.Format(CI, "profile library: profile group {0} - accessory {1} {2}", GroupId, code, on ? "on" : "off"));
            Status(string.Format(on ? T("Grp_AccOn", "{0}: on every profile of the group - its places are worked out on the Profiles page.") : T("Grp_AccOff", "{0}: not on this group."), code));
            Later(Reload);
        }

        private void Later(Action a)
        {
            if (IsHandleCreated && !IsDisposed) BeginInvoke(a); else a();
        }

        private void ShowAccessory(long id)
        {
            _accessoryId = id;
            var a = Library.Rows(@"SELECT a.code, a.description, ag.name AS acc_group, ag.accessory_type FROM accessory a JOIN accessory_group ag ON ag.id = a.group_id
                                   WHERE a.id = @p0", id).FirstOrDefault();
            if (a == null) return;
            _head.Text = string.Format(T("Grp_HeadAccessory", "{0} {1} - {2} ({3}): its operations, made on every profile of the groups it is given to"),
                                       Str(a["code"]), Str(a["description"]), Str(a["acc_group"]), Str(a["accessory_type"]));
            _ops.Rows.Clear();
            foreach (var op in Library.Rows(@"SELECT op.seq, m.code AS macro, op.position_mode, op.reference_code, op.offset_y_mm, op.offset_z_mm,
                                                     op.x_strategy, op.x_base_mm, op.x_pitch_mm, op.x_count, op.mirror
                                              FROM accessory_operation op JOIN macro m ON m.id = op.macro_id WHERE op.accessory_id = @p0 ORDER BY op.seq", id))
            {
                Func<object, string> mm = v => v == null || v == DBNull.Value ? "" : Convert.ToDouble(v).ToString("0.##", CI);
                string where = Str(op["reference_code"]) + (Str(op["reference_code"]).Length > 0 ? " " : "") +
                               string.Format(CI, "Y {0} Z {1}", mm(op["offset_y_mm"]), mm(op["offset_z_mm"]));
                string along = Str(op["x_strategy"]) + " " + mm(op["x_base_mm"]) + (Str(op["x_pitch_mm"]).Length > 0 ? " / " + mm(op["x_pitch_mm"]) : "") +
                               (Str(op["x_count"]).Length > 0 ? " x" + Str(op["x_count"]) : "") + (Str(op["mirror"]) == "1" ? "  " + T("Grp_Mirrored", "mirrored at the far end") : "");
                _ops.Rows.Add(Str(op["seq"]), Str(op["macro"]), where, along.Trim());
            }
            _groupView.Visible = _profileView.Visible = false;
            _accessoryView.Visible = true;
        }

        private void ShowProfile(long id)
        {
            var p = Library.Profiles(true).FirstOrDefault(x => x.Id == id);
            if (p == null) return;
            _head.Text = string.Format(T("Grp_HeadProfile", "{0} {1} - a profile of the group {2}"), p.Code, p.Description, p.Group);
            _profileText.Text = string.Format(T("Grp_ProfileText",
                "Brand {0}, series {1}, group {2} ({3}).\r\nSize {4} x {5} mm.\r\nIts accessories are the group's: their places on it {6} - confirmed / placed (the Profiles page shows and confirms them)."),
                p.Vendor, p.Series, p.Group, p.Type, p.EffectiveWidth.ToString("0.#", CI), p.EffectiveHeight.ToString("0.#", CI),
                p.PlacementText.Length > 0 ? p.PlacementText : T("Grp_NoPlaces", "not worked out yet"));
            _groupView.Visible = _accessoryView.Visible = false;
            _profileView.Visible = true;
        }
    }
}
