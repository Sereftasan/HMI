using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;
using Kaban.Profiles.Geometry;
using Kaban.UI.Controls;
using Kaban.UI.Profiles;

namespace Kaban.Machines.FA2010.Screens
{
    /// <summary>
    /// DETAILS: what the profile is (series, group, code, what it is based on,
    /// its size), how its drawing is read (the file, turned, mirrored, the layers
    /// that are not the profile) - and its REFERENCE POINTS: found on the drawing,
    /// moved or picked by hand, confirmed by a person.
    /// </summary>
    public partial class ProfilesScreen
    {
        private sealed class Choice
        {
            public long Id;
            public string Text = "";
            public override string ToString() { return Text; }
        }

        private bool _detailsDirty, _filling;
        private ComboBox _cmbVariant, _cmbGroup, _cmbBase, _cmbFamily, _cmbTurn;
        private TextBox _txtCode, _txtDescription, _txtWidth, _txtHeight, _txtNote, _txtDxf, _txtIgnored;
        private Label _lblWidthCue, _lblHeightCue, _lblSize;
        private CheckBox _chkMirror;
        private Button _btnSizeFix, _btnSaveDetails, _btnRevertDetails, _btnBrowse, _btnLayers;
        private Action _sizeFix;
        private readonly List<Control> _detailInputs = new List<Control>();

        private DataGridView _refGrid;
        private Label _refHow;
        private Button _btnFindRefs, _btnConfirmRefs, _btnPickRef, _btnRemoveRef;
        /// <summary>How each reference was found by the last search (not stored: the source is).</summary>
        private readonly Dictionary<string, string> _lastFound = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private string _pickCode;
        private TabControl _detailTabs;

        private void BuildDetails()
        {
            _detailTabs = new TabControl { Dock = DockStyle.Fill, Font = UiStyle.Small, Padding = new Point(12, 4) };
            var profileTab = new TabPage(T("Prf_TabProfile", "Profile")) { BackColor = Color.White };
            var refsTab = new TabPage(T("Prf_TabRefs", "Reference points")) { BackColor = Color.White };
            _detailTabs.TabPages.Add(profileTab);
            _detailTabs.TabPages.Add(refsTab);

            // ---- the profile ----------------------------------------------------------------
            var form = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoScroll = true, Padding = new Padding(6, 6, 6, 6) };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            int row = 0;
            Func<string, Control, Control, Control> line = (caption, input, extra) =>
            {
                var lbl = new Label { Text = caption, AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = UiStyle.Small, Height = 28 };
                form.Controls.Add(lbl, 0, row);
                input.Dock = DockStyle.Fill;
                input.Margin = new Padding(3, 3, 3, 3);
                form.Controls.Add(input, 1, row);
                if (extra != null) { extra.Margin = new Padding(3, 3, 3, 3); form.Controls.Add(extra, 2, row); }
                form.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
                row++;
                return input;
            };
            Func<string, Control> heading = text =>
            {
                var h = new Label { Text = text, AutoSize = false, Dock = DockStyle.Fill, Font = UiStyle.FontVariant(UiStyle.Small, FontStyle.Bold), ForeColor = Color.FromArgb(30, 55, 88), TextAlign = ContentAlignment.BottomLeft, Height = 26 };
                form.Controls.Add(h, 0, row);
                form.SetColumnSpan(h, 3);
                form.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
                row++;
                return h;
            };
            Func<string, string, Button> small = (text, tip) =>
            {
                var b = UiStyle.TextButton(text, 88);
                _tips.SetToolTip(b, tip);
                return b;
            };
            EventHandler dirty = (s, e) => { if (!_filling) { _detailsDirty = true; UpdateDetailButtons(); } };
            EventHandler drawingDirty = (s, e) => { if (!_filling) { _detailsDirty = true; UpdateDetailButtons(); PreviewDrawing(); } };

            heading(T("Prf_HeadIdentity", "What the profile is"));
            _cmbVariant = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = UiStyle.Small };
            var libSeries = small("\u2026", T("Prf_TipLibSeries", "Vendors, series and variants: in the library"));
            libSeries.Click += (s, e) => OpenLibraryEditor("series");
            line(T("Prf_Series", "Series :"), _cmbVariant, libSeries);
            _cmbVariant.SelectedIndexChanged += (s, e) => { if (_filling) return; FillGroups(null); dirty(s, e); };
            _cmbGroup = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = UiStyle.Small };
            var libGroups = small("\u2026", T("Prf_TipLibGroups", "The series' groups (frame, sash ...): in the library"));
            libGroups.Click += (s, e) => OpenLibraryEditor("groups");
            line(T("Prf_GroupLbl", "Group :"), _cmbGroup, libGroups);
            _cmbGroup.SelectedIndexChanged += (s, e) => { if (_filling) return; UpdateSizeCues(); dirty(s, e); };
            _txtCode = (TextBox)line(T("Prf_Code", "Code :"), new TextBox { Font = UiStyle.Small }, null);
            _txtDescription = (TextBox)line(T("Prf_Description", "Description :"), new TextBox { Font = UiStyle.Small }, null);
            _cmbBase = (ComboBox)line(T("Prf_BasedOn", "Based on :"), new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = UiStyle.Small }, null);
            _tips.SetToolTip(_cmbBase, T("Prf_TipBasedOn", "Another profile this one is a variant of: every reference point this profile does not set itself is that one's."));
            _cmbFamily = (ComboBox)line(T("Prf_Family", "Machining family :"), new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = UiStyle.Small }, null);
            _lblWidthCue = new Label { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = UiStyle.Small, ForeColor = Color.DimGray };
            _txtWidth = (TextBox)line(T("Prf_Width", "Width (mm) :"), new TextBox { Font = UiStyle.Small, TextAlign = HorizontalAlignment.Right }, _lblWidthCue);
            _lblHeightCue = new Label { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = UiStyle.Small, ForeColor = Color.DimGray };
            _txtHeight = (TextBox)line(T("Prf_Height", "Height (mm) :"), new TextBox { Font = UiStyle.Small, TextAlign = HorizontalAlignment.Right }, _lblHeightCue);
            _tips.SetToolTip(_txtWidth, T("Prf_TipSize", "Empty: the group's size. Across the profile (width, Y) and up from the bed (height, Z)."));
            _tips.SetToolTip(_txtHeight, T("Prf_TipSize", "Empty: the group's size. Across the profile (width, Y) and up from the bed (height, Z)."));
            _txtNote = (TextBox)line(T("Prf_Note", "Note :"), new TextBox { Font = UiStyle.Small }, null);

            heading(T("Prf_HeadDrawing", "The drawing (DXF)"));
            _btnBrowse = small("\u2026", T("Prf_TipBrowse", "Choose the profile's DXF file"));
            _btnBrowse.Click += (s, e) => BrowseDxf();
            _txtDxf = (TextBox)line(T("Prf_DxfFile", "File :"), new TextBox { Font = UiStyle.Small }, _btnBrowse);
            _cmbTurn = (ComboBox)line(T("Prf_Turned", "Turned :"), new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = UiStyle.Small }, null);
            _cmbTurn.Items.AddRange(new object[] { T("Prf_Turn0", "as drawn"), T("Prf_Turn90", "90\u00B0 clockwise"), T("Prf_Turn180", "180\u00B0"), T("Prf_Turn270", "90\u00B0 counter-clockwise") });
            _tips.SetToolTip(_cmbTurn, T("Prf_TipTurned", "How the drawing is turned to read in profile coordinates: the outside face on the left (Y = 0), the bed below (Z = 0)."));
            _chkMirror = (CheckBox)line(T("Prf_Mirrored", "Mirrored :"), new CheckBox { Font = UiStyle.Small, Text = T("Prf_MirrorText", "left and right swapped") }, null);
            _btnLayers = small("\u2026", T("Prf_TipLayers", "Choose from the drawing's layers"));
            _btnLayers.Click += (s, e) => ChooseIgnoredLayers();
            _txtIgnored = (TextBox)line(T("Prf_Ignored", "Not the profile :"), new TextBox { Font = UiStyle.Small }, _btnLayers);
            _tips.SetToolTip(_txtIgnored, T("Prf_TipIgnored", "Layers of the drawing that are not the profile (dimensions, a gasket drawn beside it), comma separated: drawn grey, never cut through. Dimension and hatch layers are left out by themselves."));
            _lblSize = new Label { AutoSize = false, Dock = DockStyle.Fill, Font = UiStyle.Small, TextAlign = ContentAlignment.MiddleLeft };
            _btnSizeFix = small("", "");
            _btnSizeFix.Width = 90;
            _btnSizeFix.Click += (s, e) => { if (_sizeFix != null) _sizeFix(); };
            form.Controls.Add(_lblSize, 0, row);
            form.SetColumnSpan(_lblSize, 2);
            form.Controls.Add(_btnSizeFix, 2, row);
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            row++;

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Height = 44 };
            _btnSaveDetails = EditTool("\uD83D\uDCBE", T("Prf_TipSave", "Save the profile"), (s, e) => SaveDetails());
            _btnRevertDetails = UiStyle.ToolButton("\u21BA", T("Prf_TipRevert", "Throw the changes away"), _tips, (s, e) => { _detailsDirty = false; LoadProfile(_current != null ? _current.Id : 0, false); });
            buttons.Controls.Add(_btnSaveDetails);
            buttons.Controls.Add(_btnRevertDetails);
            form.Controls.Add(buttons, 1, row);
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            row++;
            form.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            foreach (var tb in new[] { _txtCode, _txtDescription, _txtWidth, _txtHeight, _txtNote }) tb.TextChanged += dirty;
            _txtWidth.TextChanged += (s, e) => { if (!_filling) UpdateSizeCheck(); };
            _txtHeight.TextChanged += (s, e) => { if (!_filling) UpdateSizeCheck(); };
            _cmbBase.SelectedIndexChanged += dirty;
            _cmbFamily.SelectedIndexChanged += dirty;
            _txtDxf.Leave += (s, e) => { if (_detailsDirty) PreviewDrawing(); };
            _txtDxf.TextChanged += dirty;
            _cmbTurn.SelectedIndexChanged += drawingDirty;
            _chkMirror.CheckedChanged += drawingDirty;
            _txtIgnored.Leave += (s, e) => { if (_detailsDirty) PreviewDrawing(); };
            _txtIgnored.TextChanged += dirty;
            _detailInputs.AddRange(new Control[] { _cmbVariant, _cmbGroup, _txtCode, _txtDescription, _cmbBase, _cmbFamily, _txtWidth, _txtHeight, _txtNote,
                                                   _txtDxf, _btnBrowse, _cmbTurn, _chkMirror, _txtIgnored, _btnLayers, _btnSizeFix });
            profileTab.Controls.Add(form);

            // ---- the reference points ---------------------------------------------------------
            var refTools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(4, 3, 4, 3), WrapContents = false };
            _btnFindRefs = EditTool("\uD83D\uDD0D", T("Prf_TipFindRefs",
                "Find the reference points on the drawing: layers named after them (REF_HARDWARE_AXIS), texts (\"HARDWARE_AXIS 22 35\"), the outline, the hardware groove, " +
                "and a similar profile's. Points confirmed or set by hand are kept."), (s, e) => FindReferences());
            _btnConfirmRefs = EditTool("\u2714", T("Prf_TipConfirmRefs", "Confirm the selected points: checked against the drawing (select several with Ctrl)"), (s, e) => ConfirmReferences(SelectedRefCodes()));
            _btnPickRef = EditTool("\uD83C\uDFAF", T("Prf_TipPickRef", "Set the selected point by clicking it on the drawing (it snaps to the nearest end of a line)"), (s, e) => StartPick());
            _btnRemoveRef = EditTool("\uD83D\uDDD1", T("Prf_TipRemoveRef", "Remove the selected points of this profile (an inherited one comes back)"), (s, e) => RemoveReferences(SelectedRefCodes()));
            refTools.Controls.AddRange(new Control[] { _btnFindRefs, _btnConfirmRefs, _btnPickRef, _btnRemoveRef });

            _refGrid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, MultiSelect = true, EditMode = DataGridViewEditMode.EditOnF2 };
            UiStyle.StyleGrid(_refGrid);
            _refGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _refGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = T("Prf_RefPoint", "Point"), Width = 150, ReadOnly = true });
            _refGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "y", HeaderText = "Y (mm)", Width = 70 });
            _refGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "z", HeaderText = "Z (mm)", Width = 70 });
            _refGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "source", HeaderText = T("Prf_RefSource", "Found by"), Width = 170, ReadOnly = true });
            _refGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "sure", HeaderText = T("Prf_RefSure", "Sure"), Width = 55, ReadOnly = true });
            _refGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "confirmed", HeaderText = T("Prf_RefConfirmed", "Confirmed"), Width = 150, ReadOnly = true });
            _refGrid.Columns["y"].DefaultCellStyle.Alignment = _refGrid.Columns["z"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            UiStyle.EnableTableTools(_refGrid, "ProfileReferences", sorting: false);
            _refGrid.CellEndEdit += (s, e) => EditReferenceCell(e.RowIndex);
            _refGrid.SelectionChanged += (s, e) => { UpdateRefHow(); _viewer.Invalidate(); };
            _refGrid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0 && (e.ColumnIndex == 1 || e.ColumnIndex == 2) && CanEdit) _refGrid.BeginEdit(true); };
            _refHow = new Label { Dock = DockStyle.Bottom, Height = 44, Font = UiStyle.Small, ForeColor = Color.FromArgb(70, 76, 86), Padding = new Padding(6, 4, 6, 2) };
            refsTab.Controls.Add(_refGrid);
            refsTab.Controls.Add(_refHow);
            refsTab.Controls.Add(refTools);

            AddPane("details", T("Prf_PaneDetails", "Details")).Controls.Add(_detailTabs);
        }

        private void ApplyDetailsAccess(bool edit)
        {
            bool has = _current != null;
            foreach (var c in _detailInputs) c.Enabled = edit && has;
            if (_btnSaveDetails != null) _btnSaveDetails.Enabled = edit && has && _detailsDirty;
            if (_btnRevertDetails != null) _btnRevertDetails.Enabled = has && _detailsDirty;
            if (_refGrid != null)
            {
                _refGrid.ReadOnly = !(edit && has);
                foreach (var name in new[] { "name", "source", "sure", "confirmed" })
                    if (_refGrid.Columns[name] != null) _refGrid.Columns[name].ReadOnly = true;
            }
            foreach (var b in new[] { _btnFindRefs, _btnConfirmRefs, _btnPickRef, _btnRemoveRef })
                if (b != null) b.Enabled = edit && has;
            if (_btnFindRefs != null) _btnFindRefs.Enabled = edit && has && _section != null;
            if (_btnPickRef != null) _btnPickRef.Enabled = edit && has && _section != null;
        }

        private void UpdateDetailButtons() { ApplyDetailsAccess(CanEdit && _libraryOk); }

        // ---- filling -----------------------------------------------------------------------

        private void FillDetails()
        {
            _filling = true;
            try
            {
                var p = _current;
                _cmbVariant.Items.Clear();
                _cmbBase.Items.Clear();
                _cmbFamily.Items.Clear();
                if (_libraryOk)
                {
                    foreach (var r in Library.Rows(@"SELECT pv.id, v.name || ' ' || s.name || ' - ' || pv.name AS t FROM profile_variant pv
                                                    JOIN profile_series s ON s.id = pv.series_id JOIN profile_vendor v ON v.id = s.vendor_id ORDER BY 2"))
                        _cmbVariant.Items.Add(new Choice { Id = Library.Id(r, "id").Value, Text = Library.Text(r, "t") });
                    _cmbBase.Items.Add(new Choice { Id = 0, Text = T("Prf_None", "(none)") });
                    foreach (var r in Library.Rows("SELECT p.id, p.code || '  ' || COALESCE(p.description, '') AS t FROM profile p WHERE p.is_active = 1 ORDER BY p.code"))
                        if (p == null || Library.Id(r, "id").Value != p.Id) _cmbBase.Items.Add(new Choice { Id = Library.Id(r, "id").Value, Text = Library.Text(r, "t").Trim() });
                    _cmbFamily.Items.Add(new Choice { Id = 0, Text = T("Prf_None", "(none)") });
                    foreach (var r in Library.Rows("SELECT id, name FROM machining_family ORDER BY name"))
                        _cmbFamily.Items.Add(new Choice { Id = Library.Id(r, "id").Value, Text = Library.Text(r, "name") });
                }
                if (p == null)
                {
                    foreach (var tb in new[] { _txtCode, _txtDescription, _txtWidth, _txtHeight, _txtNote, _txtDxf, _txtIgnored }) tb.Text = "";
                    _cmbGroup.Items.Clear();
                    _cmbTurn.SelectedIndex = 0;
                    _chkMirror.Checked = false;
                    _lblWidthCue.Text = _lblHeightCue.Text = "";
                }
                else
                {
                    Select(_cmbVariant, p.VariantId);
                    FillGroups(p.GroupId);
                    _txtCode.Text = p.Code;
                    _txtDescription.Text = p.Description;
                    Select(_cmbBase, p.BaseProfileId ?? 0);
                    Select(_cmbFamily, p.FamilyId ?? 0);
                    _txtWidth.Text = p.Width.HasValue ? Mm(p.Width.Value) : "";
                    _txtHeight.Text = p.Height.HasValue ? Mm(p.Height.Value) : "";
                    _txtNote.Text = p.Note;
                    _txtDxf.Text = p.DxfPath;
                    _cmbTurn.SelectedIndex = ((p.Rotation % 4) + 4) % 4;
                    _chkMirror.Checked = p.Mirror;
                    _txtIgnored.Text = p.IgnoredLayers;
                    UpdateSizeCues();
                }
            }
            finally { _filling = false; }
            _detailsDirty = false;
            UpdateSizeCheck();
            UpdateDetailButtons();
        }

        private static void Select(ComboBox c, long id)
        {
            foreach (Choice ch in c.Items) if (ch.Id == id) { c.SelectedItem = ch; return; }
            c.SelectedIndex = c.Items.Count > 0 ? 0 : -1;
        }

        private static long SelectedId(ComboBox c)
        {
            var ch = c.SelectedItem as Choice;
            return ch != null ? ch.Id : 0;
        }

        private void FillGroups(long? select)
        {
            bool was = _filling;
            _filling = true;
            _cmbGroup.Items.Clear();
            long variant = SelectedId(_cmbVariant);
            if (variant > 0)
                foreach (var r in Library.Rows("SELECT id, name, profile_type FROM profile_group WHERE variant_id = @p0 ORDER BY name", variant))
                    _cmbGroup.Items.Add(new Choice { Id = Library.Id(r, "id").Value, Text = Library.Text(r, "name") + "  (" + Library.Text(r, "profile_type") + ")" });
            if (select.HasValue) Select(_cmbGroup, select.Value);
            else if (_cmbGroup.Items.Count > 0) _cmbGroup.SelectedIndex = 0;
            _filling = was;
            UpdateSizeCues();
        }

        /// <summary>The group's size, shown beside an empty width or height.</summary>
        private void UpdateSizeCues()
        {
            long g = SelectedId(_cmbGroup);
            var r = g > 0 ? Library.Rows("SELECT default_width_mm, default_height_mm FROM profile_group WHERE id=@p0", g).FirstOrDefault() : null;
            var w = Library.Real(r, "default_width_mm");
            var h = Library.Real(r, "default_height_mm");
            _lblWidthCue.Text = w.HasValue ? T("Prf_GroupCue", "group:") + " " + Mm(w.Value) : "";
            _lblHeightCue.Text = h.HasValue ? T("Prf_GroupCue", "group:") + " " + Mm(h.Value) : "";
        }

        private static double? ParseMm(string text)
        {
            var t = (text ?? "").Trim().Replace(',', '.');
            double v;
            return t.Length > 0 && double.TryParse(t, NumberStyles.Float, CI, out v) ? v : (double?)null;
        }

        /// <summary>
        /// Does the drawing agree with the profile's size? A drawing lying on its
        /// side (82 x 115 read as 115 x 82) is the common case, and turning it is
        /// one click.
        /// </summary>
        private void UpdateSizeCheck()
        {
            _sizeFix = null;
            _btnSizeFix.Visible = false;
            _lblSize.ForeColor = Color.FromArgb(70, 76, 86);
            if (_current == null) { _lblSize.Text = ""; return; }
            if (_section == null)
            {
                _lblSize.Text = string.IsNullOrWhiteSpace(_txtDxf.Text) ? T("Prf_SizeNoDrawing", "No drawing: reference points and walls come from the drawing - choose its DXF file.")
                                                                         : T("Prf_SizeNoFile", "The drawing's file is not found.");
                _lblSize.ForeColor = Color.FromArgb(170, 40, 40);
                return;
            }
            double sw = _section.Width, sh = _section.Height;
            var cw = ParseMm(_lblWidthCue.Text.Split(' ').LastOrDefault());
            var ch = ParseMm(_lblHeightCue.Text.Split(' ').LastOrDefault());
            double pw = ParseMm(_txtWidth.Text) ?? cw ?? 0, ph = ParseMm(_txtHeight.Text) ?? ch ?? 0;
            string drawn = string.Format(T("Prf_SizeDrawn", "The drawing is {0} x {1} mm"), Mm(sw), Mm(sh));
            Func<double, double, bool> near = (a, b) => Math.Abs(a - b) <= Math.Max(0.6, b * 0.01);
            if (pw <= 0 || ph <= 0)
            {
                _lblSize.Text = drawn + ".";
                SizeFix(T("Prf_UseSize", "Use its size"), () => { _txtWidth.Text = Mm(sw); _txtHeight.Text = Mm(sh); });
            }
            else if (near(sw, pw) && near(sh, ph))
            {
                _lblSize.Text = "\u2714 " + drawn + ", " + T("Prf_SizeAgrees", "as the profile.");
                _lblSize.ForeColor = Color.FromArgb(20, 120, 50);
            }
            else if (near(sw, ph) && near(sh, pw))
            {
                _lblSize.Text = drawn + string.Format(T("Prf_SizeSideways", ", the profile {0} x {1}: it lies on its side. Turn it 90\u00B0 (outside face on the left, bed below)."), Mm(pw), Mm(ph));
                _lblSize.ForeColor = Color.FromArgb(190, 110, 0);
                SizeFix(T("Prf_TurnIt", "Turn it"), () => { _cmbTurn.SelectedIndex = (_cmbTurn.SelectedIndex + 1) % 4; });
            }
            else
            {
                _lblSize.Text = drawn + string.Format(T("Prf_SizeDiffers", ", the profile's size {0} x {1} mm. Check the file, or use the drawing's size."), Mm(pw), Mm(ph));
                _lblSize.ForeColor = Color.FromArgb(190, 110, 0);
                SizeFix(T("Prf_UseSize", "Use its size"), () => { _txtWidth.Text = Mm(sw); _txtHeight.Text = Mm(sh); });
            }
        }

        private void SizeFix(string text, Action fix)
        {
            _btnSizeFix.Text = text;
            _sizeFix = fix;
            _btnSizeFix.Visible = true;
        }

        /// <summary>The drawing as the details say, before they are saved (turn, mirror, layers, file).</summary>
        private void PreviewDrawing()
        {
            if (_current == null) return;
            var p = Pending();
            try { _section = Library.SectionOf(p); }
            catch (Exception ex) { _section = null; Status(T("Prf_DxfFailed", "The drawing could not be read:") + " " + ex.Message, true); }
            ShowDrawing();
            UpdateSizeCheck();
            if (_section != null) Status(T("Prf_Preview", "Shown as it will be saved - press \uD83D\uDCBE to keep it. Reference points found before are not moved by this: find them again after saving."));
        }

        /// <summary>The profile as the details pane says (not saved).</summary>
        private Library.ProfileInfo Pending()
        {
            var c = _current;
            return new Library.ProfileInfo
            {
                Id = c.Id, GroupId = SelectedId(_cmbGroup) > 0 ? SelectedId(_cmbGroup) : c.GroupId, VariantId = c.VariantId, SeriesId = c.SeriesId, VendorId = c.VendorId,
                FamilyId = SelectedId(_cmbFamily) > 0 ? SelectedId(_cmbFamily) : (long?)null,
                BaseProfileId = SelectedId(_cmbBase) > 0 ? SelectedId(_cmbBase) : (long?)null, InterfaceId = c.InterfaceId,
                Code = _txtCode.Text.Trim(), Description = _txtDescription.Text.Trim(), Vendor = c.Vendor, Series = c.Series, Variant = c.Variant,
                Group = c.Group, Type = c.Type, Material = c.Material, Family = c.Family, Interface = c.Interface,
                Width = ParseMm(_txtWidth.Text), Height = ParseMm(_txtHeight.Text), GroupWidth = c.GroupWidth, GroupHeight = c.GroupHeight,
                DxfPath = _txtDxf.Text.Trim(), DxfStatus = c.DxfStatus, IgnoredLayers = _txtIgnored.Text.Trim(), Note = _txtNote.Text.Trim(),
                Rotation = Math.Max(0, _cmbTurn.SelectedIndex), Mirror = _chkMirror.Checked, Active = c.Active
            };
        }

        // ---- saving ------------------------------------------------------------------------

        /// <summary>Saves the details; a changed drawing gets its reference points looked for again.</summary>
        internal bool SaveDetails()
        {
            if (_current == null || !CanEdit) return false;
            var p = Pending();
            if (p.Code.Length == 0) { Status(T("Prf_NeedCode", "The profile needs a code."), true); return false; }
            if (SelectedId(_cmbGroup) <= 0) { Status(T("Prf_NeedGroup", "Choose the profile's group (add one in the library first)."), true); return false; }
            foreach (var tb in new[] { _txtWidth, _txtHeight })
                if (tb.Text.Trim().Length > 0 && !ParseMm(tb.Text).HasValue) { Status("\"" + tb.Text + "\" " + T("Tbl_NotNumber", "is not a number"), true); return false; }
            bool drawingChanged = p.DxfPath != _current.DxfPath || p.Rotation != _current.Rotation || p.Mirror != _current.Mirror ||
                                  (p.IgnoredLayers ?? "") != (_current.IgnoredLayers ?? "");
            if (drawingChanged) p.DxfStatus = string.IsNullOrEmpty(p.DxfPath) ? "NONE" : "IMPORTED";
            try { Library.SaveProfile(p, User); }
            catch (Exception ex)
            {
                Status(ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0
                    ? string.Format(T("Prf_CodeTaken", "The group already has a profile {0}."), p.Code) : ex.Message, true);
                return false;
            }
            AuditLogger.Log("profile library: saved profile " + p.Code + (drawingChanged ? " (its drawing changed)" : "") + ".");
            _detailsDirty = false;
            long id = p.Id;
            _current = Library.Profile(id);
            if (drawingChanged)
            {
                try { _section = Library.SectionOf(_current); } catch { _section = null; }
                if (_section != null)
                {
                    _references = Library.References(id);
                    FindReferences();                 // it reloads the profile
                }
                else LoadProfile(id, true);
            }
            else LoadProfile(id, true);
            RefreshRow();
            if (!drawingChanged) Status(string.Format(T("Prf_Saved", "{0} saved."), p.Code));
            return true;
        }

        private void BrowseDxf()
        {
            using (var dlg = new OpenFileDialog { Filter = T("Prf_DxfFilter", "DXF drawings") + " (*.dxf)|*.dxf|" + T("Prf_AllFiles", "All files") + " (*.*)|*.*" })
            {
                var cur = _current != null ? Library.DxfFile(_current) : null;
                if (cur != null) dlg.InitialDirectory = Path.GetDirectoryName(cur);
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                // kept relative when the file is under the program's folder (portable between PCs)
                _txtDxf.Text = dlg.FileName.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase) ? dlg.FileName.Substring(baseDir.Length) : dlg.FileName;
                PreviewDrawing();
            }
        }

        /// <summary>The drawing's layers, ticked to leave out of the profile.</summary>
        private void ChooseIgnoredLayers()
        {
            if (_current == null) return;
            var p = Pending();
            var file = Library.DxfFile(p);
            if (file == null) { Status(T("Prf_SizeNoFile", "The drawing's file is not found."), true); return; }
            Section all;
            try { all = Section.Load(file, p.Rotation, p.Mirror, ""); }
            catch (Exception ex) { Status(ex.Message, true); return; }
            var current = new HashSet<string>(Section.SplitLayers(_txtIgnored.Text), StringComparer.OrdinalIgnoreCase);
            using (var dlg = new Form
            {
                Text = T("Prf_LayersTitle", "Layers that are not the profile"), Width = 380, Height = 420, FormBorderStyle = FormBorderStyle.SizableToolWindow,
                StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false
            })
            {
                UiScale.ApplyOnLoad(dlg);
                var list = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, Font = UiStyle.Small };
                foreach (var kv in all.Layers.OrderBy(k => k.Key))
                {
                    bool auto = Section.IsReferenceLayer(kv.Key) || Section.LooksLikeAnnotation(kv.Key);
                    list.Items.Add(kv.Key + "   (" + kv.Value + (auto ? ", " + T("Prf_LayerAuto", "left out anyway") : "") + ")", current.Contains(kv.Key));
                }
                var ok = UiStyle.DialogButton(T("Btn_Ok", "OK"), DialogResult.OK);
                var okRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
                okRow.Controls.Add(ok);
                dlg.Controls.Add(list);
                dlg.Controls.Add(okRow);
                dlg.AcceptButton = ok;
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                var chosen = list.CheckedItems.Cast<string>().Select(t => t.Substring(0, t.IndexOf("   (", StringComparison.Ordinal))).ToList();
                _txtIgnored.Text = string.Join(",", chosen);
                PreviewDrawing();
            }
        }

        // ---- new, based on, out of use -------------------------------------------------------------

        private void AddProfile()
        {
            EnsureLibrary();
            if (!_libraryOk || !CanEdit) return;
            long group = _current != null ? _current.GroupId : Convert.ToInt64(Library.Scalar("SELECT id FROM profile_group ORDER BY id LIMIT 1") ?? 0L);
            if (group <= 0)
            {
                Ask.Tell(FindForm(), T("Prf_Title", "Profiles"), T("Prf_NoGroups", "The library has no group to put a profile in. Add a vendor, a series, a variant and a group first (the library opens now)."));
                OpenLibraryEditor("vendors");
                return;
            }
            var groupName = Convert.ToString(Library.Scalar("SELECT name FROM profile_group WHERE id=@p0", group));
            string code = Ask.Text(FindForm(), T("Prf_NewTitle", "New profile"), string.Format(T("Prf_NewPrompt", "Code of the new profile (group {0} - it can be changed in Details):"), groupName));
            if (string.IsNullOrWhiteSpace(code)) return;
            try
            {
                long id = Library.SaveProfile(new Library.ProfileInfo { GroupId = group, Code = code.Trim(), DxfStatus = "NONE" }, User);
                AuditLogger.Log("profile library: added profile " + code.Trim() + ".");
                _pendingSelectId = id.ToString(CI);
                ReloadAll();
                _detailTabs.SelectedIndex = 0;
                Status(T("Prf_NewAdded", "New profile added: choose its drawing and save."));
            }
            catch (Exception ex)
            {
                Status(ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0
                    ? string.Format(T("Prf_CodeTaken", "The group already has a profile {0}."), code.Trim()) : ex.Message, true);
            }
        }

        private void CopyProfile()
        {
            if (_current == null || !CanEdit) return;
            string code = Ask.Text(FindForm(), T("Prf_CopyTitle", "New profile based on another"),
                string.Format(T("Prf_CopyPrompt", "Code of the new profile, based on {0}:"), _current.Code), _current.Code + "-2");
            if (string.IsNullOrWhiteSpace(code)) return;
            string from = _current.Code;
            try
            {
                long id = Library.CopyProfile(_current.Id, code.Trim(), User);
                AuditLogger.Log("profile library: added profile " + code.Trim() + " based on " + from + ".");
                _pendingSelectId = id.ToString(CI);
                ReloadAll();
                Status(string.Format(T("Prf_CopyDone", "{0} is based on {1}: its reference points are {1}'s until it sets its own."), code.Trim(), from));
            }
            catch (Exception ex)
            {
                Status(ex.Message.IndexOf("UNIQUE", StringComparison.OrdinalIgnoreCase) >= 0
                    ? string.Format(T("Prf_CodeTaken", "The group already has a profile {0}."), code.Trim()) : ex.Message, true);
            }
        }

        private void DeactivateProfile()
        {
            if (_current == null || !CanEdit) return;
            if (!_current.Active)
            {
                if (!Ask.YesNo(FindForm(), T("Prf_Title", "Profiles"), string.Format(T("Prf_AskActivate", "Take {0} back into use?"), _current.Code))) return;
                Library.Exec("UPDATE profile SET is_active = 1 WHERE id = @p0", _current.Id);
            }
            else
            {
                if (!Ask.YesNo(FindForm(), T("Prf_Title", "Profiles"), string.Format(T("Prf_AskDeactivate",
                    "Take {0} out of use? It stays in the library with its history (\uD83D\uDC41 shows it again)."), _current.Code))) return;
                Library.DeactivateProfile(_current.Id);
            }
            AuditLogger.Log("profile library: " + _current.Code + (_current.Active ? " taken out of use." : " back in use."));
            if (_current.Active && !_showInactive)
            {
                // gone from the table: the next profile is shown, not this one brought back
                _pendingSelectId = null;
                _current = null;
                _shownId = -1;
                _detailsDirty = false;
                LoadRows();
            }
            else ReloadAll();
        }

        // =====================================================================
        // REFERENCE POINTS
        // =====================================================================

        internal static string RefName(string code)
        {
            switch (code)
            {
                case "OUTSIDE_SURFACE": return T("Ref_Outside", "Outside surface");
                case "INSIDE_SURFACE": return T("Ref_Inside", "Inside surface");
                case "CENTER": return T("Ref_Center", "Profile centre");
                case "HARDWARE_AXIS": return T("Ref_Hardware", "Hardware axis");
                case "GLAZING_REFERENCE": return T("Ref_Glazing", "Glazing reference");
                case "REBATE_LINE": return T("Ref_Rebate", "Rebate line");
                case "BED_DATUM": return T("Ref_Bed", "Bed datum");
                case "CHAMBER_CENTRE": return T("Ref_Chamber", "Chamber centre");
                default: return code;
            }
        }

        /// <summary>A few letters for the drawing.</summary>
        internal static string RefShort(string code)
        {
            switch (code)
            {
                case "OUTSIDE_SURFACE": return "OUT";
                case "INSIDE_SURFACE": return "IN";
                case "CENTER": return "C";
                case "HARDWARE_AXIS": return "HW";
                case "GLAZING_REFERENCE": return "GL";
                case "REBATE_LINE": return "RB";
                case "BED_DATUM": return "BED";
                case "CHAMBER_CENTRE": return "CH";
                default: return code;
            }
        }

        private static string SourceText(Library.ReferenceInfo r)
        {
            if (r.Inherited) return string.Format(T("Prf_SrcInherited", "from {0}"), r.InheritedFrom);
            return LibraryEditorForm.SourceText(r.Source);
        }

        private void FillReferences()
        {
            var keep = SelectedRefCodes();
            _refGrid.Rows.Clear();
            if (_current == null) { UpdateRefHow(); return; }
            var byCode = _references.ToDictionary(r => r.Code, StringComparer.OrdinalIgnoreCase);
            foreach (var code in ReferenceFinder.Codes.Concat(_references.Select(r => r.Code)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Library.ReferenceInfo r;
                byCode.TryGetValue(code, out r);
                int i = _refGrid.Rows.Add(RefName(code), r != null ? Mm(r.Y) : "", r != null ? Mm(r.Z) : "",
                                          r != null ? SourceText(r) : T("Prf_RefNotSet", "not set"),
                                          r != null && r.Confidence.HasValue && !r.Confirmed ? (r.Confidence.Value * 100).ToString("0", CI) + " %" : "",
                                          r == null ? "" : r.Confirmed ? r.ConfirmedBy + "  " + ShortTime(r.ConfirmedAt) : r.Inherited ? "" : T("Prf_RefToCheck", "to check"));
                var gr = _refGrid.Rows[i];
                gr.Tag = code;
                Color fore = r == null ? Color.FromArgb(160, 160, 160) : r.Inherited ? Color.FromArgb(60, 90, 140)
                           : r.Confirmed ? Color.FromArgb(20, 110, 45) : Color.FromArgb(170, 100, 0);
                gr.DefaultCellStyle.ForeColor = fore;
                if (keep.Contains(code)) gr.Selected = true;
            }
            if (keep.Count == 0) _refGrid.ClearSelection();
            UpdateRefHow();
        }

        private static string ShortTime(string iso)
        {
            DateTime t;
            return DateTime.TryParse(iso, CI, DateTimeStyles.RoundtripKind, out t) ? Kaban.Core.Helpers.ViewPreferences.DateTimeText(t.ToLocalTime(), false) : iso ?? "";
        }

        internal List<string> SelectedRefCodes()
        {
            if (_refGrid == null) return new List<string>();
            return _refGrid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag as string).Where(c => c != null).ToList();
        }

        private void UpdateRefHow()
        {
            var codes = SelectedRefCodes();
            if (codes.Count != 1) { _refHow.Text = _current != null ? T("Prf_RefHint", "Select a point to see how it was found. Double-click Y or Z to type it.") : ""; return; }
            var r = _references.FirstOrDefault(x => string.Equals(x.Code, codes[0], StringComparison.OrdinalIgnoreCase));
            string how;
            _lastFound.TryGetValue(codes[0], out how);
            _refHow.Text = RefName(codes[0]) + ": " + (r == null ? T("Prf_RefNotSetLong", "not set - find it, pick it on the drawing or type Y and Z.")
                : (how != null && !r.Inherited ? how : SourceText(r)) + (r.Confirmed ? "" : r.Inherited ? "" : "  -  " + T("Prf_RefUnconfirmed", "not confirmed: placements measured from it stay proposals.")));
        }

        /// <summary>
        /// The reference points found on the drawing - the profile's own, never
        /// over one a person confirmed or set by hand, never over an inherited one
        /// with a guess from a similar profile.
        /// </summary>
        internal void FindReferences()
        {
            if (_current == null || _section == null || !CanEdit) return;
            var fp = Fingerprint.Of(_section);
            Library.SaveFingerprint(_current.Id, fp);
            var template = Library.TemplateFor(_current, fp);
            var found = ReferenceFinder.Find(_section, ReferenceFinder.GrooveWidthOf(_current.Interface), template);
            var mine = _references.ToDictionary(r => r.Code, StringComparer.OrdinalIgnoreCase);
            int set = 0, kept = 0, proposed = 0;
            var moved = new List<string>();
            foreach (var f in found)
            {
                Library.ReferenceInfo had;
                mine.TryGetValue(f.Code, out had);
                if (had != null && !had.Inherited && (had.Confirmed || had.Source == "MANUAL"))
                {
                    kept++;
                    if (Math.Abs(had.Y - f.Y) > 0.2 || Math.Abs(had.Z - f.Z) > 0.2) moved.Add(RefName(f.Code) + " (" + Mm(f.Y) + " / " + Mm(f.Z) + ")");
                    continue;
                }
                if (had != null && had.Inherited && f.Source == "AI_PROPOSED") continue;
                Library.SetReference(_current.Id, f.Code, f.Y, f.Z, f.Source, f.Confidence, null);
                _lastFound[f.Code] = f.How;
                set++;
                if (f.Source == "AI_PROPOSED") proposed++;
            }
            Library.Exec("UPDATE profile SET dxf_status = 'PROPOSED' WHERE id = @p0 AND dxf_status IN ('NONE','IMPORTED')", _current.Id);
            AuditLogger.Log("profile library: reference points of " + _current.Code + " looked for on the drawing: " + set + " set, " + kept + " kept.");
            LoadProfile(_current.Id, true);
            _detailTabs.SelectedIndex = 1;
            string text = string.Format(T("Prf_FoundRefs", "{0} reference point(s) found on the drawing"), set) +
                          (proposed > 0 ? string.Format(T("Prf_FoundProposed", ", {0} of them proposed from a similar profile"), proposed) : "") +
                          (kept > 0 ? string.Format(T("Prf_FoundKept", ", {0} confirmed or set by hand kept"), kept) : "") + ". " +
                          T("Prf_FoundCheck", "Check them on the drawing and confirm (\u2714).");
            if (moved.Count > 0) text += " " + T("Prf_FoundMoved", "The drawing puts these elsewhere:") + " " + string.Join(", ", moved) + ".";
            Status(text, moved.Count > 0);
        }

        internal void ConfirmReferences(List<string> codes)
        {
            if (_current == null || !CanEdit || codes.Count == 0) return;
            int n = 0;
            var inherited = new List<string>();
            foreach (var code in codes)
            {
                var r = _references.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
                if (r == null) continue;
                if (r.Inherited) { if (!r.Confirmed) inherited.Add(r.InheritedFrom); continue; }
                if (r.Confirmed) continue;
                Library.ConfirmReference(_current.Id, code, User);
                n++;
            }
            if (n > 0)
            {
                AuditLogger.Log("profile library: " + n + " reference point(s) of " + _current.Code + " confirmed.");
                Library.Exec(@"UPDATE profile SET dxf_status = 'CONFIRMED' WHERE id = @p0 AND NOT EXISTS
                               (SELECT 1 FROM profile_reference r WHERE r.profile_id = @p0 AND r.confirmed_by IS NULL)", _current.Id);
            }
            LoadProfile(_current.Id, true);
            RefreshRow();
            Status(string.Format(T("Prf_RefsConfirmed", "{0} reference point(s) confirmed."), n) +
                   (inherited.Count > 0 ? " " + string.Format(T("Prf_RefsInherited", "An inherited point is confirmed on the profile it comes from ({0})."), string.Join(", ", inherited.Distinct())) : ""));
        }

        internal void RemoveReferences(List<string> codes)
        {
            if (_current == null || !CanEdit || codes.Count == 0) return;
            var own = codes.Where(c => _references.Any(r => !r.Inherited && string.Equals(r.Code, c, StringComparison.OrdinalIgnoreCase))).ToList();
            if (own.Count == 0) return;
            if (!Ask.YesNo(FindForm(), T("Prf_Title", "Profiles"), string.Format(T("Prf_AskRemoveRefs", "Remove {0} of {1}?"), string.Join(", ", own.Select(RefName)), _current.Code))) return;
            foreach (var c in own) Library.RemoveReference(_current.Id, c);
            AuditLogger.Log("profile library: reference point(s) " + string.Join(", ", own) + " of " + _current.Code + " removed.");
            LoadProfile(_current.Id, true);
            RefreshRow();
        }

        /// <summary>Y or Z typed in the table: the point is set by hand (and waits for a confirmation).</summary>
        private void EditReferenceCell(int rowIndex)
        {
            if (_current == null || rowIndex < 0 || !CanEdit) return;
            var code = _refGrid.Rows[rowIndex].Tag as string;
            var y = ParseMm(Convert.ToString(_refGrid.Rows[rowIndex].Cells["y"].Value));
            var z = ParseMm(Convert.ToString(_refGrid.Rows[rowIndex].Cells["z"].Value));
            var r = _references.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));
            if (!y.HasValue && !z.HasValue) { BeginInvoke(new Action(FillReferences)); return; }
            double ny = y ?? (r != null ? r.Y : 0), nz = z ?? (r != null ? r.Z : 0);
            if (r != null && Math.Abs(r.Y - ny) < 1e-6 && Math.Abs(r.Z - nz) < 1e-6) return;
            SetReferenceByHand(code, ny, nz);
        }

        internal void SetReferenceByHand(string code, double y, double z)
        {
            if (_current == null || !CanEdit) return;
            Library.SetReference(_current.Id, code, y, z, "MANUAL", 1, null);
            _lastFound.Remove(code);
            AuditLogger.Log("profile library: " + code + " of " + _current.Code + " set by hand to " + Mm(y) + " / " + Mm(z) + ".");
            long id = _current.Id;
            BeginInvokeOrRun(() => { LoadProfile(id, true); RefreshRow(); Status(string.Format(T("Prf_RefSet", "{0} set to {1} / {2} - confirm it when it is right."), RefName(code), Mm(y), Mm(z))); });
        }

        private void BeginInvokeOrRun(Action a)
        {
            if (IsHandleCreated) BeginInvoke(a); else a();
        }

        /// <summary>The next click on the drawing sets the selected point.</summary>
        private void StartPick()
        {
            var codes = SelectedRefCodes();
            if (_current == null || _section == null || codes.Count != 1)
            { Status(T("Prf_PickSelect", "Select ONE point in the table first."), true); return; }
            _pickCode = codes[0];
            _viewer.PickMode = true;
            _viewer.Cursor = Cursors.Cross;
            Status(string.Format(T("Prf_PickNow", "Click {0} on the drawing (Esc: cancel)."), RefName(_pickCode)));
        }

        private void OnPointPicked(Kaban.UI.Dxf.DxfDocument.PointD p)
        {
            _viewer.Cursor = Cursors.Default;
            if (_pickCode == null) return;
            var code = _pickCode;
            _pickCode = null;
            SetReferenceByHand(code, Math.Round(p.X, 2), Math.Round(p.Y, 2));
        }
    }
}
