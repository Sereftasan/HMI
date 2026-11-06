using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;
using Kaban.Profiles.Machining;
using Kaban.UI.Controls;

namespace Kaban.UI.Profiles
{
    /// <summary>
    /// THE PROFILE LIBRARY'S TABLES, one editor: the catalogue (vendors, series,
    /// groups), the accessories with their operations, the macros, and the
    /// machine's own layer (tools, faces, feeds, program settings) - plus the
    /// review queue of everything nobody has confirmed yet.
    ///
    /// Everything here is DATA for the Profiles page and its programs: nothing is
    /// sent to the PLC. Editing needs the Maintenance level; below it the tables
    /// are shown read only.
    /// </summary>
    public class LibraryEditorForm : Form
    {
        private readonly long _instance;
        private readonly TreeView _tree = new TreeView();
        private readonly Panel _host = new Panel();
        private readonly Label _title = new Label();
        private readonly Label _hint = new Label();
        private readonly Dictionary<string, Control> _built = new Dictionary<string, Control>();
        private readonly Dictionary<string, Func<Control>> _makers = new Dictionary<string, Func<Control>>();
        private readonly Dictionary<string, string> _hints = new Dictionary<string, string>();
        private readonly List<TableEditor> _editors = new List<TableEditor>();
        private readonly bool _canEdit;

        /// <summary>Something was saved: the page resolves its placements again.</summary>
        public bool Changed { get; private set; }

        /// <summary>A profile chosen from the review queue, to be opened by the page (0: none).</summary>
        public long ProfileToOpen { get; private set; }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        public LibraryEditorForm(long machineInstance, string startSection = null)
        {
            _instance = machineInstance;
            _canEdit = SecurityManager.HasAccess(SecurityLevel.Maintenance);
            Kaban.UI.Helpers.Shortcuts.Attach(this);     // the tables' buttons from the keyboard
            Text = T("Lib_Title", "Profile library");
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1280, 780);
            MinimumSize = new Size(900, 560);
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = UiStyle.Body;
            UiScale.ApplyOnLoad(this);

            _tree.Dock = DockStyle.Left;
            _tree.Width = 250;
            _tree.HideSelection = false;
            _tree.FullRowSelect = true;
            _tree.ShowLines = false;
            _tree.ItemHeight = 26;
            _tree.BorderStyle = BorderStyle.None;
            _tree.BackColor = Color.FromArgb(244, 246, 249);
            _tree.AfterSelect += (s, e) => ShowSection(e.Node != null ? e.Node.Name : null);

            var head = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Color.White, Padding = new Padding(12, 6, 12, 4) };
            _title.Dock = DockStyle.Top;
            _title.Height = 24;
            _title.Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold);
            _hint.Dock = DockStyle.Fill;
            _hint.Font = UiStyle.Small;
            _hint.ForeColor = Color.FromArgb(90, 96, 106);
            head.Controls.Add(_hint);
            head.Controls.Add(_title);

            _host.Dock = DockStyle.Fill;
            _host.Padding = new Padding(8, 0, 8, 8);
            var right = new Panel { Dock = DockStyle.Fill };
            right.Controls.Add(_host);
            right.Controls.Add(head);
            if (!_canEdit)
                right.Controls.Add(new Label
                {
                    Dock = DockStyle.Top, Height = 26, BackColor = Color.FromArgb(255, 244, 214), ForeColor = Color.FromArgb(120, 80, 0),
                    TextAlign = ContentAlignment.MiddleCenter, Font = UiStyle.Small,
                    Text = T("Lib_ReadOnly", "Read only: changing the library needs the Maintenance level.")
                });

            Controls.Add(right);
            Controls.Add(new Panel { Dock = DockStyle.Left, Width = 1, BackColor = UiStyle.BorderColor });
            Controls.Add(_tree);

            BuildTree();
            FormClosing += (s, e) =>
            {
                var dirty = _editors.Where(ed => ed.Dirty).ToList();
                if (dirty.Count == 0 || !_canEdit) return;
                var answer = MessageBox.Show(this, T("Lib_Unsaved", "Some tables have changes that are not saved. Save them now?"),
                    Text, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel) { e.Cancel = true; return; }
                if (answer == DialogResult.Yes)
                    foreach (var ed in dirty)
                    {
                        string err;
                        if (!ed.Save(out err)) { e.Cancel = true; Ask.Tell(this, Text, err); return; }
                    }
            };

            var start = startSection != null ? _tree.Nodes.Find(startSection, true).FirstOrDefault() : null;
            _tree.SelectedNode = start ?? _tree.Nodes[0].Nodes[0];
            _tree.ExpandAll();
        }

        // ---- the sections ------------------------------------------------------------

        private void Section(TreeNode parent, string key, string text, string hint, Func<Control> make)
        {
            parent.Nodes.Add(new TreeNode(text) { Name = key });
            _makers[key] = make;
            _hints[key] = hint;
        }

        private TreeNode Heading(string key, string text)
        {
            var n = new TreeNode(text) { Name = key, NodeFont = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold) };
            _tree.Nodes.Add(n);
            return n;
        }

        private const string SeriesText = "SELECT s.id, v.name || ' ' || s.name FROM profile_series s JOIN profile_vendor v ON v.id = s.vendor_id ORDER BY 2";
        private const string VariantText = "SELECT pv.id, v.name || ' ' || s.name || ' - ' || pv.name FROM profile_variant pv JOIN profile_series s ON s.id = pv.series_id JOIN profile_vendor v ON v.id = s.vendor_id ORDER BY 2";
        private static readonly string[] Materials = { "PVC", "ALU", "WOOD", "COMPOSITE" };
        private static readonly string[] ProfileTypes = { "FRAME", "SASH", "MULLION", "TRANSOM", "DOOR_LEAF", "BEAD", "ADAPTER", "REINFORCEMENT", "CONNECTION", "SILL" };
        private static readonly string[] AccessoryTypes = { "WATER_DRAINAGE", "HINGE", "HANDLE", "LOCK", "STRIKER", "ESPAGNOLETTE", "CORNER_DRIVE", "STAY", "RESTRICTOR", "OTHER" };
        private static readonly string[] XStrategies = { "SINGLE", "FROM_START", "FROM_END", "CENTRED", "DISTRIBUTED", "FROM_BOTH_ENDS" };

        internal static string[] Sides
        {
            get
            {
                return new[] { "TOP|" + T("Lib_SideTop", "top (down)"), "BOTTOM|" + T("Lib_SideBottom", "bottom (up)"),
                               "OUTSIDE|" + T("Lib_SideOutside", "outside face (in)"), "INSIDE|" + T("Lib_SideInside", "inside face (out)") };
            }
        }

        internal static string[] DepthModes
        {
            get
            {
                return new[] { "0|" + T("Lib_DepthMacro", "the macro's depth"), "1|" + T("Lib_DepthWall", "through the first wall"),
                               "2|" + T("Lib_DepthWhole", "through the whole profile") };
            }
        }

        private void BuildTree()
        {
            var prof = Heading("h_profiles", T("Lib_Profiles", "Profiles"));
            Section(prof, "vendors", T("Lib_Vendors", "Vendors"), T("Lib_HintVendors", "Who makes the profiles."), () => Table("profile_vendor", "name", ed => ed
                .Text("code", T("LCol_Code", "Code"), 110, true).Text("name", T("LCol_Name", "Name"), 240, true)));
            Section(prof, "series", T("Lib_Series", "Series"), T("Lib_HintSeries", "A vendor's system (Elegant, TecnoCorr2): its material decides the feeds."), () => Table("profile_series", "name", ed => ed
                .Lookup("vendor_id", T("LCol_Vendor", "Vendor"), "SELECT id, name FROM profile_vendor ORDER BY name", 170, true)
                .Text("code", T("LCol_Code", "Code"), 100, true).Text("name", T("LCol_Name", "Name"), 180, true)
                .Choice("material", T("LCol_Material", "Material"), Materials, 100, true, "PVC")
                .Real("system_depth_mm", T("LCol_SystemDepth", "System depth (mm)"), 120)));
            Section(prof, "variants", T("Lib_Variants", "Variants"), T("Lib_HintVariants", "A series' variants (standard, hidden vent). A series without any has one, 'Standard'."), () => Table("profile_variant", "series_id, name", ed => ed
                .Lookup("series_id", T("LCol_Series", "Series"), SeriesText, 220, true)
                .Text("code", T("LCol_Code", "Code"), 100, true).Text("name", T("LCol_Name", "Name"), 200, true)));
            Section(prof, "groups", T("Lib_Groups", "Groups"), T("Lib_HintGroups", "What a profile IS (frame, sash, door leaf): accessories are given to a group, and every profile of it takes them."), () => Table("profile_group", "variant_id, name", ed => ed
                .Lookup("variant_id", T("LCol_Variant", "Series / variant"), VariantText, 260, true)
                .Text("name", T("LCol_Name", "Name"), 220, true)
                .Choice("profile_type", T("LCol_Type", "Type"), ProfileTypes, 120, true, "FRAME")
                .Lookup("hardware_interface_id", T("LCol_Interface", "Hardware interface"), "SELECT id, name FROM hardware_interface ORDER BY name", 170)
                .Tip(T("Lib_TipInterface", "The groove the hardware fits. Accessories of the same interface are offered first."))
                .Real("default_width_mm", T("LCol_DefWidth", "Width (mm)"), 90).Real("default_height_mm", T("LCol_DefHeight", "Height (mm)"), 90)
                .Lookup("reference_profile_id", T("LCol_RefProfile", "Reference profile"), "SELECT id, code FROM profile ORDER BY code", 140)
                .Tip(T("Lib_TipRefProfile", "The measured, confirmed member: a new profile of the group takes its reference points as proposals."))));
            Section(prof, "families", T("Lib_Families", "Machining families"), T("Lib_HintFamilies", "How profiles are machined, across vendors and groups."), () => Table("machining_family", "code", ed => ed
                .Text("code", T("LCol_Code", "Code"), 200, true).Text("name", T("LCol_Name", "Name"), 240, true).Text("note", T("LCol_Note", "Note"), 300)));
            Section(prof, "interfaces", T("Lib_Interfaces", "Hardware interfaces"), T("Lib_HintInterfaces", "The groove a profile offers the hardware (Euro groove 16, 13 ...)."), () => Table("hardware_interface", "code", ed => ed
                .Text("code", T("LCol_Code", "Code"), 180, true).Text("name", T("LCol_Name", "Name"), 220, true).Text("note", T("LCol_Note", "Note"), 300)));

            var acc = Heading("h_accessories", T("Lib_Accessories", "Accessories"));
            Section(acc, "accessories", T("Lib_AccessoriesOps", "Accessories and operations"),
                T("Lib_HintAccessories", "An accessory is one or more operations: a macro, the reference point it is measured from, the offset, the face it is cut from and where along the bar."),
                BuildAccessories);
            Section(acc, "acc_groups", T("Lib_AccGroups", "Accessory groups"), T("Lib_HintAccGroups", "Hinge, handle, drainage ..."), () => Table("accessory_group", "name", ed => ed
                .Text("name", T("LCol_Name", "Name"), 220, true).Choice("accessory_type", T("LCol_Type", "Type"), AccessoryTypes, 160, true, "OTHER")));
            // 6 November: "Why can't we attach accessory groups to the profile groups directly so any accessory would be attached to all
            // the profiles as soon as they are defined using their accessory and profile groups"
            Section(acc, "acc_group_links", T("Lib_AccGroupLinks", "Accessory groups of the profile groups"),
                T("Lib_HintAccGroupLinks", "An accessory group attached to a profile group: every accessory of it - those added to it later too - goes on every profile of the profile group."),
                () => Table("group_accessory_group", "profile_group_id, accessory_group_id", ed => ed
                .Lookup("profile_group_id", T("LCol_ProfileGroup", "Profile group"), "SELECT id, name FROM profile_group ORDER BY name", 200, true)
                .Lookup("accessory_group_id", T("LCol_AccGroup", "Accessory group"), "SELECT id, name FROM accessory_group ORDER BY name", 200, true)
                .Bool("is_enabled", T("LCol_On", "On"), 60, true)));
            Section(acc, "acc_vendors", T("Lib_AccVendors", "Hardware vendors"), T("Lib_HintAccVendors", "Who makes the hardware."), () => Table("accessory_vendor", "name", ed => ed
                .Text("code", T("LCol_Code", "Code"), 110, true).Text("name", T("LCol_Name", "Name"), 240, true)));
            Section(acc, "acc_series", T("Lib_AccSeries", "Hardware series"), T("Lib_HintAccSeries", "A hardware vendor's range, and the interface it fits."), () => Table("accessory_series", "name", ed => ed
                .Lookup("vendor_id", T("LCol_Vendor", "Vendor"), "SELECT id, name FROM accessory_vendor ORDER BY name", 170, true)
                .Text("code", T("LCol_Code", "Code"), 100, true).Text("name", T("LCol_Name", "Name"), 200, true)
                .Lookup("hardware_interface_id", T("LCol_Interface", "Hardware interface"), "SELECT id, name FROM hardware_interface ORDER BY name", 170)));
            Section(acc, "macros", T("Lib_Macros", "Macros"),
                T("Lib_HintMacros", "The SHAPE of a cut (a 5 x 30 slot, 6 deep) - no tool, no feed: the machine chooses its own tool and feeds from its tools and the profile's walls."),
                BuildMacros);
            Section(acc, "defaults", T("Lib_TypeDefaults", "Type defaults"), T("Lib_HintTypeDefaults", "Where an accessory type goes on a profile type when nothing more is known."), () => Table("operation_type_default", "profile_type, accessory_type", ed => ed
                .Choice("profile_type", T("LCol_ProfileType", "Profile type"), ProfileTypes, 120, true, "FRAME")
                .Choice("accessory_type", T("LCol_AccType", "Accessory type"), AccessoryTypes, 140, true, "OTHER")
                .Lookup("reference_code", T("LCol_Reference", "Reference"), "SELECT code, name FROM reference_type ORDER BY name", 150, true)
                .Real("offset_y_mm", T("LCol_OffsetY", "Offset Y"), 80, true, 0.0).Real("offset_z_mm", T("LCol_OffsetZ", "Offset Z"), 80, true, 0.0)
                .Choice("x_strategy", T("LCol_XStrategy", "Along the bar"), XStrategies, 130, true, "SINGLE")
                .Text("note", T("LCol_Note", "Note"), 200)));

            var mach = Heading("h_machine", T("Lib_Machine", "This machine"));
            Section(mach, "tools", T("Lib_Tools", "Machine tools"),
                T("Lib_HintTools", "The tools mounted on the milling units, with their measured size: the program chooses among these. A tool with no diameter is never chosen."),
                () => Table("mounted_tool", "unit_no, position", ed =>
                {
                    ed.FilterColumn = "instance_id"; ed.FilterValue = _instance;
                    ed.Int("unit_no", T("LCol_Unit", "Unit"), 60, true, 1L).Text("position", T("LCol_Position", "Place"), 90, true)
                      .Text("tool_code", T("LCol_ToolCode", "Tool"), 110, true)
                      .Choice("tool_type", T("LCol_ToolType", "Kind"), new[] { "END_MILL|" + T("Lib_EndMill", "end mill"), "DRILL|" + T("Lib_Drill", "drill") }, 100, true, "END_MILL")
                      .Real("diameter_mm", T("LCol_Diameter", "Diameter (mm)"), 100).Real("usable_length_mm", T("LCol_UsableLength", "Usable length (mm)"), 120)
                      .Real("max_rpm", T("LCol_MaxRpm", "Max rpm"), 80).Int("flute_count", T("LCol_Flutes", "Flutes"), 60)
                      .Bool("is_active", T("LCol_Active", "In use"), 60).Text("note", T("LCol_Note", "Note"), 200);
                }));
            Section(mach, "faces", T("Lib_Faces", "Machine faces"),
                T("Lib_HintFaces", "Which unit, at which angle, machines which face of the profile. Check these on the machine before the first program runs."),
                () => Table("machine_face", "unit_no, angle_deg", ed =>
                {
                    ed.FilterColumn = "instance_id"; ed.FilterValue = _instance;
                    ed.Int("unit_no", T("LCol_Unit", "Unit"), 60, true, 1L).Choice("entry_side", T("LCol_Face", "Face"), Sides, 170, true, "TOP")
                      .Real("angle_deg", T("LCol_Angle", "Angle (\u00B0)"), 80, true, 0.0).Bool("is_enabled", T("LCol_Enabled", "Enabled"), 70)
                      .Text("note", T("LCol_Note", "Note"), 260);
                }));
            Section(mach, "feeds", T("Lib_Feeds", "Feeds"),
                T("Lib_HintFeeds", "Per material class and tool kind: the entry speed through a wall, the cutting feed, and how much slower a thin wall is entered."),
                () => Table("feed_strategy", "milling_class_id, tool_type", ed =>
                {
                    ed.FilterColumn = "series_id"; ed.FilterValue = Library.SeriesOfInstance(_instance);
                    ed.Lookup("milling_class_id", T("LCol_MillingClass", "Material class"), "SELECT id, name FROM milling_class ORDER BY name", 170, true)
                      .Choice("tool_type", T("LCol_ToolType", "Kind"), new[] { "END_MILL|" + T("Lib_EndMill", "end mill"), "DRILL|" + T("Lib_Drill", "drill") }, 100, true, "END_MILL")
                      .Real("entry_speed", T("LCol_Entry", "Entry (mm/min)"), 100, true).Real("feed_speed", T("LCol_Feed", "Feed (mm/min)"), 100, true)
                      .Real("spindle_rpm", T("LCol_Rpm", "Spindle rpm"), 90).Real("plunge_rate", T("LCol_Plunge", "Drill plunge"), 90)
                      .Real("thin_wall_factor", T("LCol_ThinFactor", "Thin wall x"), 80, true, 1.0).Real("thin_wall_mm", T("LCol_ThinMm", "Thin below (mm)"), 100, true, 1.5);
                }));
            Section(mach, "classes", T("Lib_Classes", "Material classes"), T("Lib_HintClasses", "PVC outer wall, PVC chamber, steel reinforcement ... - what a wall is made of, for its feeds."), () => Table("milling_class", "code", ed => ed
                .Text("code", T("LCol_Code", "Code"), 160, true).Choice("material", T("LCol_Material", "Material"), new[] { "PVC", "ALU", "STEEL", "COMPOSITE" }, 100, true, "PVC")
                .Text("name", T("LCol_Name", "Name"), 200, true).Text("note", T("LCol_Note", "Note"), 220)));
            Section(mach, "layers", T("Lib_Layers", "Drawing layers"),
                T("Lib_HintLayers", "A DXF layer that is not the series' ordinary wall (a steel reinforcement drawn on layer STEEL): its material class. Other walls are the outer wall (the first) or a chamber wall."),
                () => Table("layer_milling_map", "profile_series_id, dxf_layer_name", ed => ed
                    .Lookup("profile_series_id", T("LCol_Series", "Series"), SeriesText, 220, true)
                    .Text("dxf_layer_name", T("LCol_Layer", "Layer"), 160, true)
                    .Lookup("milling_class_id", T("LCol_MillingClass", "Material class"), "SELECT id, name FROM milling_class ORDER BY name", 180, true)));
            Section(mach, "settings", T("Lib_Settings", "Program settings"), T("Lib_HintSettings", "How the side machining program moves: safe height, the feed through air, peck depth, and the program's form."), BuildSettings);

            var rev = Heading("h_review", T("Lib_ReviewHead", "Review"));
            Section(rev, "review", T("Lib_Review", "Not confirmed yet"),
                T("Lib_HintReview", "Reference points and placements nobody has confirmed, the least trusted first. Double-click one to open its profile."),
                BuildReview);
        }

        private void ShowSection(string key)
        {
            if (key == null || !_makers.ContainsKey(key))
            {
                var node = _tree.SelectedNode;
                if (node != null && node.Nodes.Count > 0) _tree.SelectedNode = node.Nodes[0];
                return;
            }
            Control c;
            if (!_built.TryGetValue(key, out c))
            {
                try { c = _makers[key](); }
                catch (Exception ex) { c = new Label { Dock = DockStyle.Fill, Text = ex.Message, ForeColor = Color.FromArgb(170, 30, 30) }; }
                c.Dock = DockStyle.Fill;
                _built[key] = c;
            }
            else foreach (var ed in Editors(c)) if (!ed.Dirty) ed.FillChoices();   // a vendor added on another tab is in the list
            _host.Controls.Clear();
            _host.Controls.Add(c);
            _title.Text = _tree.SelectedNode != null ? _tree.SelectedNode.Text : key;
            string hint; _hint.Text = _hints.TryGetValue(key, out hint) ? hint : "";
        }

        private static IEnumerable<TableEditor> Editors(Control c)
        {
            if (c is TableEditor) yield return (TableEditor)c;
            foreach (Control child in c.Controls)
                foreach (var e in Editors(child)) yield return e;
        }

        /// <summary>The section's grid, for a test.</summary>
        internal TableEditor EditorOf(string key)
        {
            _tree.SelectedNode = _tree.Nodes.Find(key, true).FirstOrDefault();
            Control c;
            return _built.TryGetValue(key, out c) ? Editors(c).FirstOrDefault() : null;
        }

        private TableEditor Table(string table, string orderBy, Action<TableEditor> columns)
        {
            var ed = new TableEditor { Table = table, OrderBy = orderBy };
            columns(ed);
            ed.Build();
            ed.CanEdit = _canEdit;
            ed.Saved += (s, e) => Changed = true;
            _editors.Add(ed);
            ed.Reload();
            UiStyle.EnableTableTools(ed.Grid, "ProfileLibrary." + table);
            return ed;
        }

        // ---- accessories and their operations -------------------------------------------

        private Control BuildAccessories()
        {
            var split = new SplitContainer { Orientation = Orientation.Horizontal, SplitterDistance = 260, SplitterWidth = 6 };
            var acc = Table("accessory", "group_id, code", ed => ed
                .Lookup("group_id", T("LCol_Group", "Group"), "SELECT id, name FROM accessory_group ORDER BY name", 150, true)
                .Lookup("series_id", T("LCol_HwSeries", "Hardware series"), "SELECT s.id, v.name || ' ' || s.name FROM accessory_series s JOIN accessory_vendor v ON v.id = s.vendor_id ORDER BY 2", 180)
                .Text("code", T("LCol_Code", "Code"), 140, true).Text("description", T("LCol_Description", "Description"), 260)
                .Lookup("base_accessory_id", T("LCol_BasedOn", "Based on"), "SELECT id, code FROM accessory ORDER BY code", 130)
                .Bool("is_active", T("LCol_Active", "In use"), 60));
            TableEditor ops = null;
            ops = Table("accessory_operation", "seq", ed =>
            {
                ed.FilterColumn = "accessory_id";
                ed.Int("seq", "#", 40, true).Starts(() => ops.MaxOf("seq") + 1)
                  .Lookup("macro_id", T("LCol_Macro", "Macro"), "SELECT id, code FROM macro ORDER BY code", 130, true)
                  .Choice("entry_side", T("LCol_Face", "Face"), Sides, 140, true, "TOP")
                  .Tip(T("Lib_TipEntrySide", "The face the tool comes in by. The operation's point is where it meets the profile."))
                  .Choice("depth_mode", T("LCol_Depth", "Depth"), DepthModes, 160, true, 0L)
                  .Lookup("position_mode", T("LCol_PositionMode", "Measured"), "SELECT code, code FROM position_mode ORDER BY code", 140, true)
                  .Lookup("reference_code", T("LCol_Reference", "Reference"), "SELECT code, name FROM reference_type ORDER BY name", 140)
                  .Real("offset_y_mm", T("LCol_OffsetY", "Offset Y"), 70, true, 0.0).Real("offset_z_mm", T("LCol_OffsetZ", "Offset Z"), 70, true, 0.0)
                  .Real("ratio", T("LCol_Ratio", "Ratio"), 60)
                  .Choice("x_strategy", T("LCol_XStrategy", "Along the bar"), XStrategies, 130, true, "SINGLE")
                  .Real("x_base_mm", T("LCol_XBase", "X (mm)"), 70, false, 0.0).Real("x_pitch_mm", T("LCol_XPitch", "Pitch (mm)"), 80).Int("x_count", T("LCol_XCount", "Count"), 60)
                  .Real("rotation_deg", T("LCol_Rotation", "Turn (\u00B0)"), 70, true, 0.0).Bool("mirror", T("LCol_Mirror", "Mirror"), 60, false);
                ed.Columns.First(c => c.Name == "position_mode").Default = "REFERENCE_OFFSET";
            });
            acc.RowChanged += (s, e) =>
            {
                var id = acc.SelectedId;
                if (ops.Dirty && ops.FilterValue != null && !Equals(ops.FilterValue, id))
                {
                    string err;
                    if (Ask.YesNo(this, Text, T("Lib_SaveOps", "Save the changed operations first?"))) ops.Save(out err);
                }
                ops.FilterValue = id;
                ops.Reload();
            };
            ops.FilterValue = acc.SelectedId;
            ops.Reload();
            var lbl = new Label { Dock = DockStyle.Top, Height = 24, Font = UiStyle.FontVariant(UiStyle.Small, FontStyle.Bold), Text = T("Lib_Operations", "Operations of the selected accessory"), TextAlign = ContentAlignment.BottomLeft };
            split.Panel1.Controls.Add(acc);
            split.Panel2.Controls.Add(ops);
            split.Panel2.Controls.Add(lbl);
            return split;
        }

        // ---- macros, and their parameters by type -------------------------------------------

        private Control BuildMacros()
        {
            var split = new SplitContainer { Orientation = Orientation.Vertical, SplitterWidth = 6 };
            var macros = Table("macro", "code", ed => ed
                .Text("code", T("LCol_Code", "Code"), 140, true)
                .Lookup("macro_type", T("LCol_Type", "Type"), "SELECT code, name FROM macro_type ORDER BY name", 120, true)
                .Text("name", T("LCol_Name", "Name"), 170).Text("params_json", T("LCol_Params", "Shape"), 260, true)
                .Bool("through_profile", T("LCol_Through", "Through"), 70, false).Text("note", T("LCol_Note", "Note"), 180));
            macros.Columns.First(c => c.Name == "params_json").Default = "{}";
            split.Panel1.Controls.Add(macros);

            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoScroll = true, Padding = new Padding(8) };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var fields = new Dictionary<string, TextBox>();
            var apply = UiStyle.TextButton(T("Lib_ApplyShape", "Use these"), 120);
            apply.Enabled = _canEdit;
            Action fill = () =>
            {
                panel.SuspendLayout();
                panel.Controls.Clear();
                panel.RowStyles.Clear();
                fields.Clear();
                var row = macros.Grid.CurrentRow;
                if (row == null) { panel.ResumeLayout(); return; }
                string typeText = Convert.ToString(row.Cells["macro_type"].Value);
                var typeCode = Library.Scalar("SELECT code FROM macro_type WHERE name = @p0 OR code = @p0", typeText);
                var schema = FlatJson.Parse(Convert.ToString(Library.Scalar("SELECT param_schema FROM macro_type WHERE code = @p0", typeCode)));
                var values = FlatJson.ParseDictionary(Convert.ToString(row.Cells["params_json"].Value));
                var keys = schema.Select(k => k.Key).Concat(values.Keys).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                panel.Controls.Add(new Label { Text = T("Lib_Shape", "Shape of") + " " + Convert.ToString(row.Cells["code"].Value), AutoSize = true, Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), Margin = new Padding(3, 3, 3, 10) }, 0, 0);
                panel.SetColumnSpan(panel.GetControlFromPosition(0, 0), 3);
                int r = 1;
                foreach (var k in keys)
                {
                    panel.Controls.Add(new Label { Text = ParamCaption(k), AutoSize = true, Margin = new Padding(3, 7, 3, 3) }, 0, r);
                    object v; values.TryGetValue(k, out v);
                    var tb = new TextBox { Width = 100, Text = v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture), ReadOnly = !_canEdit, TextAlign = k == "path" ? HorizontalAlignment.Left : HorizontalAlignment.Right };
                    panel.Controls.Add(tb, 1, r);
                    panel.Controls.Add(new Label { Text = k.EndsWith("_mm") ? "mm" : k.EndsWith("_deg") ? "\u00B0" : "", AutoSize = true, Margin = new Padding(3, 7, 3, 3), ForeColor = Color.DimGray }, 2, r);
                    fields[k] = tb;
                    r++;
                }
                panel.Controls.Add(apply, 1, r);
                panel.ResumeLayout();
            };
            apply.Click += (s, e) =>
            {
                var row = macros.Grid.CurrentRow;
                if (row == null) return;
                var list = new List<KeyValuePair<string, object>>();
                foreach (var kv in fields)
                {
                    var t = kv.Value.Text.Trim().Replace(',', '.');
                    double d;
                    if (t.Length == 0) continue;
                    if (kv.Key != "path" && double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) list.Add(new KeyValuePair<string, object>(kv.Key, d));
                    else if (kv.Key != "path") { Ask.Tell(this, Text, ParamCaption(kv.Key) + ": \"" + t + "\" " + T("Tbl_NotNumber", "is not a number")); return; }
                    else list.Add(new KeyValuePair<string, object>(kv.Key, t.ToUpperInvariant()));
                }
                row.Cells["params_json"].Value = FlatJson.Write(list);
            };
            macros.RowChanged += (s, e) => fill();
            macros.Grid.CellEndEdit += (s, e) => { if (macros.Grid.Columns[e.ColumnIndex].Name != "params_json") fill(); };
            split.Panel2.Controls.Add(panel);
            split.SizeChanged += (s, e) => { if (split.Width > 500) split.SplitterDistance = Math.Max(200, split.Width - 380); };
            fill();
            return split;
        }

        private static string ParamCaption(string key)
        {
            switch (key)
            {
                case "diameter_mm": return T("Par_Diameter", "Diameter");
                case "depth_mm": return T("Par_Depth", "Depth");
                case "length_mm": return T("Par_Length", "Length (along the bar)");
                case "width_mm": return T("Par_Width", "Width");
                case "angle_deg": return T("Par_Angle", "Angle");
                case "corner_r_mm": return T("Par_Corner", "Corner radius");
                case "path": return T("Par_Path", "Path (LINE, CONTOUR)");
                default: return key;
            }
        }

        // ---- program settings ---------------------------------------------------------------

        internal static readonly string[,] SettingCodes =
        {
            { "safe_z_mm", "30" }, { "approach_feed", "3000" }, { "peck_depth_mm", "4" }, { "breakthrough_mm", "0.5" }, { "incremental", "1" }, { "program_folder", "" }
        };

        private Control BuildSettings()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(8), AutoScroll = true };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var boxes = new Dictionary<string, Control>();
            string[] captions =
            {
                T("Set_SafeZ", "Safe height above the face (mm)"), T("Set_Approach", "Feed through air inside the profile (mm/min)"),
                T("Set_Peck", "Most a tool goes down in one pass (mm)"), T("Set_Breakthrough", "Past the wall when cutting through (mm)"),
                T("Set_Incremental", "Incremental moves (G91)"), T("Set_Folder", "Folder for program files")
            };
            for (int i = 0; i < SettingCodes.GetLength(0); i++)
            {
                string code = SettingCodes[i, 0];
                string value = Library.Setting(_instance, code, SettingCodes[i, 1]);
                panel.Controls.Add(new Label { Text = captions[i], AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, i);
                Control box;
                if (code == "incremental") box = new CheckBox { Checked = value != "0", AutoSize = true, Enabled = _canEdit, Margin = new Padding(3, 6, 3, 3) };
                else box = new TextBox { Text = value, Width = code == "program_folder" ? 300 : 100, ReadOnly = !_canEdit, TextAlign = code == "program_folder" ? HorizontalAlignment.Left : HorizontalAlignment.Right };
                panel.Controls.Add(box, 1, i);
                boxes[code] = box;
            }
            // the program's "choose a folder" button, as everywhere
            var browse = UiStyle.ToolButton("\uD83D\uDCC2", T("Dsh_Browse", "Browse..."), UiStyle.Tips());
            browse.Enabled = _canEdit;
            browse.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog { SelectedPath = boxes["program_folder"].Text })
                    if (dlg.ShowDialog(this) == DialogResult.OK) boxes["program_folder"].Text = dlg.SelectedPath;
            };
            panel.Controls.Add(browse, 2, SettingCodes.GetLength(0) - 1);
            var save = UiStyle.TextButton(T("Btn_Save", "Save"), 110);
            save.Enabled = _canEdit;
            var status = new Label { AutoSize = true, Margin = new Padding(3, 10, 3, 3), Font = UiStyle.Small };
            save.Click += (s, e) =>
            {
                foreach (var kv in boxes)
                {
                    string v = kv.Value is CheckBox ? (((CheckBox)kv.Value).Checked ? "1" : "0") : kv.Value.Text.Trim();
                    double d;
                    if (kv.Key != "program_folder" && kv.Key != "incremental" &&
                        !double.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                    { status.Text = "\"" + v + "\" " + T("Tbl_NotNumber", "is not a number"); status.ForeColor = Color.FromArgb(170, 30, 30); return; }
                    Library.SetSetting(_instance, kv.Key, kv.Key == "program_folder" || kv.Key == "incremental" ? v : v.Replace(',', '.'));
                }
                Changed = true;
                AuditLogger.Log("saved the side machining program settings.");
                status.Text = T("Tbl_Saved", "Saved.");
                status.ForeColor = Color.FromArgb(20, 120, 40);
            };
            int last = SettingCodes.GetLength(0);
            panel.Controls.Add(save, 1, last);
            panel.Controls.Add(status, 1, last + 1);
            return panel;
        }

        // ---- the review queue -----------------------------------------------------------------

        private Control BuildReview()
        {
            var split = new SplitContainer { Orientation = Orientation.Horizontal, SplitterDistance = 220, SplitterWidth = 6 };
            Func<string, DataGridView> grid = caption =>
            {
                var g = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false };
                UiStyle.StyleGrid(g);
                return g;
            };
            var refs = grid("refs");
            foreach (var c in new[] { T("LCol_Profile", "Profile"), T("LCol_Group", "Group"), T("LCol_Reference", "Reference"), "Y", "Z", T("LCol_Source", "Found by"), T("LCol_Confidence", "Confidence") })
                refs.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = c, Width = c.Length > 3 ? 140 : 70 });
            foreach (var r in Library.Rows(@"SELECT p.id AS pid, p.code, pg.name AS grp, rt.name AS ref, r.y_mm, r.z_mm, r.source, r.confidence
                                            FROM profile_reference r JOIN profile p ON p.id = r.profile_id JOIN profile_group pg ON pg.id = p.group_id
                                            JOIN reference_type rt ON rt.code = r.reference_code
                                            WHERE p.is_active = 1 AND r.confirmed_by IS NULL ORDER BY COALESCE(r.confidence, 0), p.code"))
            {
                int i = refs.Rows.Add(Library.Text(r, "code"), Library.Text(r, "grp"), Library.Text(r, "ref"), Mm(Library.Real(r, "y_mm")), Mm(Library.Real(r, "z_mm")),
                                      SourceText(Library.Text(r, "source")), Pct(Library.Real(r, "confidence")));
                refs.Rows[i].Tag = Library.Id(r, "pid");
            }
            var pls = grid("placements");
            foreach (var c in new[] { T("LCol_Profile", "Profile"), T("LCol_Group", "Group"), T("LCol_Accessory", "Accessory"), "#", T("LCol_Macro", "Macro"), T("LCol_Status", "Status"), T("LCol_Confidence", "Confidence"), T("LCol_Deviation", "Off the reference profile"), T("LCol_Updated", "Changed") })
                pls.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = c, Width = c.Length > 2 ? 130 : 40 });
            foreach (var r in Library.Rows(@"SELECT p.id AS pid, p.code, pg.name AS grp, a.code AS acc, op.seq, m.code AS macro, rp.status, rp.confidence, rp.deviation_mm, rp.updated_at
                                            FROM resolved_placement rp JOIN profile p ON p.id = rp.profile_id JOIN profile_group pg ON pg.id = p.group_id
                                            JOIN accessory_operation op ON op.id = rp.operation_id JOIN accessory a ON a.id = op.accessory_id JOIN macro m ON m.id = op.macro_id
                                            WHERE p.is_active = 1 AND rp.status IN ('PROPOSED','AUTO')
                                            ORDER BY CASE rp.status WHEN 'PROPOSED' THEN 0 ELSE 1 END, COALESCE(rp.confidence, 1), p.code, a.code, op.seq"))
            {
                int i = pls.Rows.Add(Library.Text(r, "code"), Library.Text(r, "grp"), Library.Text(r, "acc"), Library.Text(r, "seq"), Library.Text(r, "macro"),
                                     StatusText(Library.Text(r, "status")), Pct(Library.Real(r, "confidence")), Mm(Library.Real(r, "deviation_mm")), Library.Text(r, "updated_at"));
                pls.Rows[i].Tag = Library.Id(r, "pid");
            }
            DataGridViewCellEventHandler open = (s, e) =>
            {
                var g = (DataGridView)s;
                if (e.RowIndex < 0 || !(g.Rows[e.RowIndex].Tag is long)) return;
                ProfileToOpen = (long)g.Rows[e.RowIndex].Tag;
                DialogResult = DialogResult.OK;
                Close();
            };
            refs.CellDoubleClick += open;
            pls.CellDoubleClick += open;
            UiStyle.EnableTableTools(refs, "ProfileLibraryReviewRefs");
            UiStyle.EnableTableTools(pls, "ProfileLibraryReviewPlacements");
            var l1 = new Label { Dock = DockStyle.Top, Height = 24, Font = UiStyle.FontVariant(UiStyle.Small, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft, Text = T("Lib_RefsToCheck", "Reference points to check") + " (" + refs.Rows.Count + ")" };
            var l2 = new Label { Dock = DockStyle.Top, Height = 24, Font = UiStyle.FontVariant(UiStyle.Small, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft, Text = T("Lib_PlacementsToCheck", "Placements to confirm") + " (" + pls.Rows.Count + ")" };
            split.Panel1.Controls.Add(refs);
            split.Panel1.Controls.Add(l1);
            split.Panel2.Controls.Add(pls);
            split.Panel2.Controls.Add(l2);
            return split;
        }

        internal static string Mm(double? v) { return v.HasValue ? v.Value.ToString("0.##", CultureInfo.InvariantCulture) : ""; }
        internal static string Pct(double? v) { return v.HasValue ? (v.Value * 100).ToString("0", CultureInfo.InvariantCulture) + " %" : ""; }

        /// <summary>How a reference point was found, in words.</summary>
        public static string SourceText(string source)
        {
            switch ((source ?? "").ToUpperInvariant())
            {
                case "DXF_LAYER": return T("Lib_SrcLayer", "a layer of the drawing");
                case "DXF_MARKER": return T("Lib_SrcMarker", "a text on the drawing");
                case "DXF_GEOMETRY": return T("Lib_SrcGeometry", "the outline");
                case "AI_PROPOSED": return T("Lib_SrcSimilar", "like a similar profile");
                case "MANUAL": return T("Lib_SrcManual", "set by hand");
                default: return source ?? "";
            }
        }

        /// <summary>A placement's status in words (the code stays in the database).</summary>
        public static string StatusText(string status)
        {
            switch ((status ?? "").ToUpperInvariant())
            {
                case "AUTO": return T("St_Auto", "worked out");
                case "PROPOSED": return T("St_Proposed", "proposed");
                case "CONFIRMED": return T("St_Confirmed", "confirmed");
                case "OVERRIDDEN": return T("St_Overridden", "confirmed (set by hand)");
                case "REJECTED": return T("St_Rejected", "not on this profile");
                default: return status ?? "";
            }
        }
    }
}
