using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;
using Kaban.Profiles.Machining;
using Kaban.UI.Controls;
using Kaban.UI.Profiles;

namespace Kaban.Machines.FA2010.Screens
{
    /// <summary>
    /// ACCESSORIES: what the profile's GROUP takes (given once, to the group -
    /// every profile of it takes the same), and where each operation lands on
    /// THIS profile: reference point + the operation's offset + the corrections of
    /// the series, the group and the profile.
    ///
    /// TRIAL AND TUNE: a placement is moved in small steps and the move kept as a
    /// CORRECTION - for this profile, its whole group or its whole series; the
    /// accessory's own definition is never touched. A moved position is to be
    /// confirmed again (after a trial cut); only confirmed placements go into a
    /// production program.
    /// </summary>
    public partial class ProfilesScreen
    {
        private DataGridView _accGrid, _plGrid;
        private Label _lblGroupAcc, _lblTune, _lblMove, _lblResolve;
        private ComboBox _cmbScope;
        private CheckBox _chkWholeAccessory;
        private Button _btnAddAcc, _btnRemoveAcc, _btnSaveMove, _btnCancelMove, _btnConfirmPl, _btnRejectPl, _btnReopenPl, _btnHistoryPl, _btnCorrections;
        private readonly List<Button> _nudgeButtons = new List<Button>();
        private double _pendingDy, _pendingDz;
        private List<string> _resolveProblems = new List<string>();
        private ProgramSettings _settings;
        private bool _fillingAcc;

        private void BuildAccessories()
        {
            var root = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            // ---- the group's accessories --------------------------------------------------
            var accTools = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(4, 3, 4, 3), WrapContents = false };
            _btnAddAcc = EditTool("\u2795", T("Prf_TipAddAcc", "Give an accessory to the profile's GROUP (every profile of the group takes it) - those that fit its hardware interface come first"), null);
            _btnAddAcc.Click += (s, e) => ShowAddAccessoryMenu();
            _btnRemoveAcc = EditTool("\uD83D\uDDD1", T("Prf_TipRemoveAcc", "Take the selected accessory off the group (its placements go, a confirmed one keeps its history)"), (s, e) => RemoveAccessory());
            var lib = UiStyle.ToolButton("\uD83D\uDCDA", T("Prf_TipLibAcc", "Accessories, their operations and macros: in the library"), _tips, (s, e) => OpenLibraryEditor("accessories"));
            _lblGroupAcc = new Label { AutoSize = true, Font = UiStyle.Small, ForeColor = Color.FromArgb(70, 76, 86), Margin = new Padding(8, 10, 3, 3) };
            accTools.Controls.AddRange(new Control[] { _btnAddAcc, _btnRemoveAcc, lib, _lblGroupAcc });

            _accGrid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, MultiSelect = false, ReadOnly = true, EditMode = DataGridViewEditMode.EditProgrammatically };
            UiStyle.StyleGrid(_accGrid);
            _accGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _accGrid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "on", HeaderText = T("Prf_AccOn", "On"), Width = 40, ToolTipText = T("Prf_TipAccOn", "Off: the group keeps the accessory but it is not machined") });
            _accGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "type", HeaderText = T("Prf_AccType", "Type"), Width = 120, ReadOnly = true });
            _accGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "code", HeaderText = T("Prf_AccCode", "Accessory"), Width = 130, ReadOnly = true });
            _accGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "desc", HeaderText = T("Prf_AccDesc", "Description"), Width = 220, ReadOnly = true });
            _accGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ops", HeaderText = T("Prf_AccOps", "Operations"), Width = 80, ReadOnly = true });
            UiStyle.EnableTableTools(_accGrid, "ProfileAccessories", sorting: false);
            // A click on the tick switches it: no cell editing (entering a check box cell
            // to edit it could write its value back unasked, and switch an accessory off).
            _accGrid.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || _accGrid.Columns[e.ColumnIndex].Name != "on" || !CanEdit || _current == null) return;
                var row = _accGrid.Rows[e.RowIndex];
                if (!(row.Tag is long)) return;
                bool on = !(row.Cells["on"].Value is bool && (bool)row.Cells["on"].Value);
                long id = (long)row.Tag;
                BeginInvokeOrRun(() => SetAccessoryOn(id, on));
            };
            _accGrid.SelectionChanged += (s, e) => { if (!_fillingAcc) SelectPlacementsOf(SelectedAccessoryId()); };

            // ---- the placements on this profile ----------------------------------------------
            _plGrid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, MultiSelect = false, ReadOnly = true };
            UiStyle.StyleGrid(_plGrid);
            _plGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            foreach (var c in new[]
            {
                new[] { "acc", T("Prf_PlAccessory", "Accessory"), "120" }, new[] { "seq", "#", "34" }, new[] { "shape", T("Prf_PlShape", "Cut"), "150" },
                new[] { "face", T("Prf_PlFace", "Face"), "110" }, new[] { "y", "Y (mm)", "64" }, new[] { "z", "Z (mm)", "64" },
                new[] { "corr", T("Prf_PlCorrection", "Correction"), "90" }, new[] { "x", T("Prf_PlAlong", "Along the bar"), "170" },
                new[] { "depth", T("Prf_PlDepth", "Depth and walls"), "240" }, new[] { "status", T("Prf_PlStatus", "Status"), "130" }
            })
                _plGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = c[0], HeaderText = c[1], Width = int.Parse(c[2]) });
            _plGrid.Columns["y"].DefaultCellStyle.Alignment = _plGrid.Columns["z"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            UiStyle.EnableTableTools(_plGrid, "ProfilePlacements", sorting: false);
            _plGrid.SelectionChanged += (s, e) =>
            {
                if (_fillingAcc) return;
                _pendingDy = _pendingDz = 0;
                UpdateTune();
                _viewer.Invalidate();
            };
            _plGrid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) ShowHistory(); };
            _lblResolve = new Label { Dock = DockStyle.Bottom, Height = 0, Font = UiStyle.Small, ForeColor = Color.FromArgb(170, 40, 40), Padding = new Padding(6, 2, 6, 2) };

            // ---- trial and tune ------------------------------------------------------------------
            var tune = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 134, ColumnCount = 1, Padding = new Padding(4, 2, 4, 2), BackColor = Color.FromArgb(246, 248, 251) };
            _lblTune = new Label { Dock = DockStyle.Fill, Font = UiStyle.Small, Height = 22, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            tune.Controls.Add(_lblTune, 0, 0);

            var nudges = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 32, WrapContents = false, Margin = new Padding(0) };
            foreach (var axis in new[] { "Y", "Z" })
            {
                nudges.Controls.Add(new Label
                {
                    Text = axis, AutoSize = false, Width = 18, Height = 28, TextAlign = ContentAlignment.MiddleCenter, Font = UiStyle.FontVariant(UiStyle.Small, FontStyle.Bold)
                });
                foreach (var step in new[] { -1.0, -0.5, -0.1, 0.1, 0.5, 1.0 })
                {
                    var a = axis; var st = step;
                    var b = UiStyle.TextButton((st > 0 ? "+" : "\u2212") + Math.Abs(st).ToString("0.#", CI), 44);
                    b.Margin = new Padding(1, 2, 1, 2);
                    Kaban.UI.Controls.UiStyle.SetTip(_tips, b, a == "Y" ? T("Prf_TipNudgeY", "Move across the profile (+: towards the inside face)") : T("Prf_TipNudgeZ", "Move up or down (+: up, away from the bed)"));
                    b.Click += (s, e) => Nudge(a == "Y" ? st : 0, a == "Z" ? st : 0);
                    nudges.Controls.Add(b);
                    _nudgeButtons.Add(b);
                }
                nudges.Controls.Add(new Label { Width = 10 });
            }
            _lblMove = new Label { AutoSize = true, Font = UiStyle.FontVariant(UiStyle.Small, FontStyle.Bold), Margin = new Padding(6, 8, 3, 3), ForeColor = Color.FromArgb(20, 90, 190) };
            nudges.Controls.Add(_lblMove);
            tune.Controls.Add(nudges, 0, 1);

            var keep = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 32, WrapContents = false, Margin = new Padding(0) };
            keep.Controls.Add(new Label { Text = T("Prf_KeepFor", "Keep the move for"), AutoSize = true, Font = UiStyle.Small, Margin = new Padding(3, 8, 3, 3) });
            _cmbScope = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230, Font = UiStyle.Small, Margin = new Padding(3, 3, 3, 3) };
            _chkWholeAccessory = new CheckBox { Text = T("Prf_WholeAccessory", "all its operations"), AutoSize = true, Font = UiStyle.Small, Margin = new Padding(6, 6, 3, 3) };
            Kaban.UI.Controls.UiStyle.SetTip(_tips, _chkWholeAccessory, T("Prf_TipWholeAccessory", "The whole accessory moves (a hinge's slot and its holes together), not just this operation"));
            _btnSaveMove = EditTool("\uD83D\uDCBE", T("Prf_TipSaveMove", "Keep the move as a correction (the reason is asked) - the position is then to be confirmed again"), (s, e) => SaveMove(null));
            _btnCancelMove = UiStyle.ToolButton("\u21A9", T("Prf_TipCancelMove", "Forget the move"), _tips, (s, e) => { _pendingDy = _pendingDz = 0; UpdateTune(); _viewer.Invalidate(); });
            keep.Controls.AddRange(new Control[] { _cmbScope, _chkWholeAccessory, _btnSaveMove, _btnCancelMove });
            tune.Controls.Add(keep, 0, 2);

            var decide = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 40, WrapContents = false, Margin = new Padding(0) };
            _btnConfirmPl = EditTool("\u2714", T("Prf_TipConfirmPl", "Confirm: the position is right (checked on the drawing or a trial cut) - it goes into the production program"), (s, e) => ConfirmPlacement(null));
            _btnRejectPl = EditTool("\u2716", T("Prf_TipRejectPl", "Not on this profile: never machined on it (the reason is kept)"), (s, e) => RejectPlacement(null));
            _btnReopenPl = EditTool("\u21BA", T("Prf_TipReopenPl", "Reopen: back to worked out, to be tuned and confirmed again"), (s, e) => ReopenPlacement());
            _btnHistoryPl = UiStyle.ToolButton("\uD83D\uDD58", T("Prf_TipHistoryPl", "The placement's history: every position it had, who changed it and why"), _tips, (s, e) => ShowHistory());
            _btnCorrections = UiStyle.ToolButton("\uD83E\uDDEE", T("Prf_TipCorrections", "The corrections that reach this profile (its own, its group's, its series'), and taking one away"), _tips, null);
            _btnCorrections.Click += (s, e) => ShowCorrectionsMenu();
            decide.Controls.AddRange(new Control[] { _btnConfirmPl, _btnRejectPl, _btnReopenPl, _btnHistoryPl, _btnCorrections });
            tune.Controls.Add(decide, 0, 3);
            foreach (RowStyle rs in tune.RowStyles) rs.SizeType = SizeType.AutoSize;

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 5, BackColor = Color.White };
            split.Panel1.Controls.Add(_accGrid);
            split.Panel1.Controls.Add(accTools);
            split.Panel2.Controls.Add(_plGrid);
            split.Panel2.Controls.Add(_lblResolve);
            split.HandleCreated += (s, e) => { try { split.SplitterDistance = Math.Max(90, split.Height / 3); } catch { } };
            root.Controls.Add(split);
            root.Controls.Add(tune);
            AddPane("accessories", T("Prf_PaneAccessories", "Accessories")).Controls.Add(root);
        }

        private void ApplyAccessoriesAccess(bool edit)
        {
            if (_accGrid == null) return;
            bool has = _current != null;
            var p = SelectedPlacement;
            bool moved = Math.Abs(_pendingDy) > 1e-9 || Math.Abs(_pendingDz) > 1e-9;
            _btnAddAcc.Enabled = edit && has;
            _btnRemoveAcc.Enabled = edit && has && SelectedAccessoryId() > 0;
            foreach (var b in _nudgeButtons) b.Enabled = edit && p != null && p.Status != "REJECTED";
            _cmbScope.Enabled = _chkWholeAccessory.Enabled = edit && p != null;
            _btnSaveMove.Enabled = edit && p != null && moved;
            _btnCancelMove.Enabled = moved;
            _btnConfirmPl.Enabled = edit && p != null && p.Status != "CONFIRMED" && p.Status != "OVERRIDDEN" && p.Status != "REJECTED" && !moved;
            _btnRejectPl.Enabled = edit && p != null && p.Status != "REJECTED";
            _btnReopenPl.Enabled = edit && p != null && (p.Status == "CONFIRMED" || p.Status == "OVERRIDDEN" || p.Status == "REJECTED");
            _btnHistoryPl.Enabled = p != null;
            _btnCorrections.Enabled = has;
        }

        // ---- filling -------------------------------------------------------------------------

        private void FillAccessories()
        {
            _fillingAcc = true;
            long keepPl = SelectedPlacement != null ? SelectedPlacement.Id : 0;
            long keepAcc = SelectedAccessoryId();
            try
            {
                _accGrid.Rows.Clear();
                _plGrid.Rows.Clear();
                _cmbScope.Items.Clear();
                _settings = _current != null && _libraryOk && _instance > 0 ? Library.ProgramSettings(_instance, _current) : null;
                if (_current == null) { _lblGroupAcc.Text = ""; _lblResolve.Height = 0; return; }
                _lblGroupAcc.Text = string.Format(T("Prf_GroupAccessories", "of the group {0}"), _current.Group);
                foreach (var r in Library.GroupAccessories(_current.GroupId))
                {
                    int i = _accGrid.Rows.Add((Library.Id(r, "is_enabled") ?? 1) != 0, Library.Text(r, "accessory_type"), Library.Text(r, "code"),
                                              Library.Text(r, "description"), Library.Text(r, "operations"));
                    _accGrid.Rows[i].Tag = Library.Id(r, "accessory_id").Value;
                    if (Library.Id(r, "accessory_id").Value == keepAcc) _accGrid.Rows[i].Selected = true;
                }
                if (keepAcc == 0) _accGrid.ClearSelection();

                double W = _section != null ? _section.Width : _current.EffectiveWidth, H = _section != null ? _section.Height : _current.EffectiveHeight;
                foreach (var p in _placements)
                {
                    var d = SideProgram.Depths(p.ToOperation(), _section, W, H, _settings != null ? _settings.Breakthrough : 0.5);
                    string corr = Math.Abs(p.CorrY) > 1e-9 || Math.Abs(p.CorrZ) > 1e-9 ? Signed(p.CorrY) + " / " + Signed(p.CorrZ) : "";
                    int i = _plGrid.Rows.Add(p.Accessory, p.Seq, p.Macro + "  " + p.Shape().Describe(), SideText(p.Side), Mm(p.Y), Mm(p.Z), corr, p.XText,
                                             d.Ok ? d.Text : "\u26A0 " + d.Problem, LibraryEditorForm.StatusText(p.Status));
                    var row = _plGrid.Rows[i];
                    row.Tag = p;
                    row.DefaultCellStyle.ForeColor = StatusInk(p.Status);
                    if (!d.Ok) row.Cells["depth"].Style.ForeColor = Color.FromArgb(170, 40, 40);
                    row.Cells["status"].ToolTipText = StatusTip(p);
                    if (p.Id == keepPl) row.Selected = true;
                }
                if (keepPl == 0 || SelectedPlacement == null) _plGrid.ClearSelection();

                _cmbScope.Items.Add(new Choice { Id = 0, Text = string.Format(T("Prf_ScopeProfile", "this profile ({0})"), _current.Code) });
                _cmbScope.Items.Add(new Choice { Id = 1, Text = string.Format(T("Prf_ScopeGroup", "every profile of the group {0}"), _current.Group) });
                _cmbScope.Items.Add(new Choice { Id = 2, Text = string.Format(T("Prf_ScopeSeries", "every profile of the series {0}"), _current.Series) });
                _cmbScope.SelectedIndex = 0;

                var problems = _resolveProblems ?? new List<string>();
                _lblResolve.Text = problems.Count == 0 ? "" : "\u26A0 " + string.Join("   \u2022 ", problems.Take(3)) + (problems.Count > 3 ? "  (+" + (problems.Count - 3) + ")" : "");
                _lblResolve.Height = problems.Count == 0 ? 0 : 36;
            }
            finally { _fillingAcc = false; }
            UpdateTune();
        }

        private static string Signed(double v) { return (v >= 0 ? "+" : "") + v.ToString("0.##", CI); }

        internal static string SideText(string side)
        {
            switch ((side ?? "TOP").ToUpperInvariant())
            {
                case "BOTTOM": return T("Lib_SideBottom", "bottom (up)");
                case "OUTSIDE": return T("Lib_SideOutside", "outside face (in)");
                case "INSIDE": return T("Lib_SideInside", "inside face (out)");
                default: return T("Lib_SideTop", "top (down)");
            }
        }

        private static Color StatusInk(string status)
        {
            switch ((status ?? "").ToUpperInvariant())
            {
                case "CONFIRMED": case "OVERRIDDEN": return Color.FromArgb(20, 120, 50);
                case "PROPOSED": return Color.FromArgb(190, 110, 0);
                case "REJECTED": return Color.FromArgb(150, 150, 150);
                default: return Color.FromArgb(30, 100, 200);
            }
        }

        private static string StatusTip(Library.PlacementInfo p)
        {
            switch ((p.Status ?? "").ToUpperInvariant())
            {
                case "AUTO": return T("Prf_TipAuto", "Worked out from confirmed reference points: fine for a trial piece, not for production until it is confirmed.");
                case "PROPOSED": return T("Prf_TipProposed", "Worked out from a reference point nobody has confirmed (found on the drawing or taken from a similar profile).");
                case "CONFIRMED": case "OVERRIDDEN": return string.Format(T("Prf_TipConfirmed", "Confirmed by {0} at {1}."), p.ConfirmedBy, ShortTime(p.ConfirmedAt));
                case "REJECTED": return string.Format(T("Prf_TipRejected", "Not on this profile ({0}, {1})."), p.ConfirmedBy, ShortTime(p.ConfirmedAt));
                default: return p.Status;
            }
        }

        internal Library.PlacementInfo SelectedPlacement
        {
            get
            {
                if (_plGrid == null || _plGrid.SelectedRows.Count == 0) return null;
                return _plGrid.SelectedRows[0].Tag as Library.PlacementInfo;
            }
        }

        internal void SelectPlacement(long placementId)
        {
            foreach (DataGridViewRow r in _plGrid.Rows)
            {
                var p = r.Tag as Library.PlacementInfo;
                if (p != null && p.Id == placementId) { r.Selected = true; _plGrid.CurrentCell = r.Cells[0]; return; }
            }
        }

        private long SelectedAccessoryId()
        {
            if (_accGrid == null || _accGrid.SelectedRows.Count == 0) return 0;
            return _accGrid.SelectedRows[0].Tag is long ? (long)_accGrid.SelectedRows[0].Tag : 0;
        }

        /// <summary>The accessory chosen above: its first placement below.</summary>
        private void SelectPlacementsOf(long accessoryId)
        {
            if (accessoryId <= 0) return;
            var first = _plGrid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Tag is Library.PlacementInfo && ((Library.PlacementInfo)r.Tag).AccessoryId == accessoryId);
            if (first != null) { first.Selected = true; _plGrid.CurrentCell = first.Cells[0]; }
            ApplyAccess();
        }

        /// <summary>The selected placement in words: where it is and where that comes from.</summary>
        private void UpdateTune()
        {
            var p = SelectedPlacement;
            if (p == null)
            {
                _lblTune.Text = _current == null ? "" : _placements.Count == 0
                    ? T("Prf_NoPlacements", "No placements: give the group an accessory (\u2795), and set the reference points it is measured from.")
                    : T("Prf_SelectPlacement", "Select a placement to see it on the drawing, move it, confirm it.");
            }
            else
            {
                string from = p.PositionMode == "FIXED" ? T("Prf_FromFixed", "a fixed position")
                            : RefName(p.Reference) + " " + Mm(p.BaseY - p.OffsetY) + " / " + Mm(p.BaseZ - p.OffsetZ);
                _lblTune.Text = p.Accessory + " #" + p.Seq + ":  Y " + Mm(p.Y) + "  Z " + Mm(p.Z) + "   =   " + from +
                                "  " + T("Prf_Offset", "+ offset") + " " + Signed(p.OffsetY) + " / " + Signed(p.OffsetZ) +
                                (Math.Abs(p.CorrY) > 1e-9 || Math.Abs(p.CorrZ) > 1e-9 ? "  " + T("Prf_Correction", "+ correction") + " " + Signed(p.CorrY) + " / " + Signed(p.CorrZ) : "") +
                                "   (" + SideText(p.Side) + ")";
            }
            bool moved = Math.Abs(_pendingDy) > 1e-9 || Math.Abs(_pendingDz) > 1e-9;
            _lblMove.Text = moved ? T("Prf_Move", "move") + " " + Signed(_pendingDy) + " / " + Signed(_pendingDz) : "";
            ApplyAccess();
        }

        // ---- trial and tune -------------------------------------------------------------------

        /// <summary>Moves the selected placement a little - shown on the drawing, kept only when saved.</summary>
        internal void Nudge(double dy, double dz)
        {
            if (SelectedPlacement == null || !CanEdit) return;
            _pendingDy = Math.Round(_pendingDy + dy, 3);
            _pendingDz = Math.Round(_pendingDz + dz, 3);
            UpdateTune();
            _viewer.Invalidate();
        }

        /// <summary>The move kept as a correction at the chosen scope (0 profile, 1 group, 2 series).</summary>
        internal bool SaveMove(string reason)
        {
            var p = SelectedPlacement;
            if (p == null || _current == null || !CanEdit) return false;
            if (Math.Abs(_pendingDy) < 1e-9 && Math.Abs(_pendingDz) < 1e-9) return false;
            long scopeIndex = SelectedId(_cmbScope);
            string scope = scopeIndex == 2 ? "SERIES" : scopeIndex == 1 ? "GROUP" : "PROFILE";
            if (reason == null)
                reason = Ask.Text(FindForm(), T("Prf_MoveTitle", "Keep the move"),
                    string.Format(T("Prf_MovePrompt", "Why is {0} moved {1} / {2} mm? (what the trial piece showed)"), p.Accessory + " #" + p.Seq, Signed(_pendingDy), Signed(_pendingDz)));
            if (reason == null) return false;
            long? op = _chkWholeAccessory.Checked ? (long?)null : p.OperationId;
            Library.AddCorrection(_current.Id, scope, p.AccessoryId, op, _pendingDy, _pendingDz, reason, User);
            AuditLogger.Log("profile library: " + p.Accessory + (op.HasValue ? " #" + p.Seq : "") + " moved " + Signed(_pendingDy) + " / " + Signed(_pendingDz) +
                            " mm for " + scope.ToLowerInvariant() + " of " + _current.Code + ": " + reason);
            string text = string.Format(T("Prf_Moved", "{0} moved {1} / {2} mm ({3}). Confirm it again once it is right."),
                p.Accessory, Signed(_pendingDy), Signed(_pendingDz), Convert.ToString(_cmbScope.SelectedItem));
            _pendingDy = _pendingDz = 0;
            long keep = p.Id;
            LoadProfile(_current.Id, false);
            SelectPlacement(keep);
            RefreshRow();
            Status(text);
            return true;
        }

        internal bool ConfirmPlacement(string reason)
        {
            var p = SelectedPlacement;
            if (p == null || _current == null || !CanEdit) return false;
            if (p.Status == "PROPOSED" && !Ask.YesNo(FindForm(), T("Prf_Title", "Profiles"),
                    string.Format(T("Prf_AskConfirmProposed", "{0} is measured from a reference point nobody has confirmed. Confirm this position anyway (checked on a trial piece)?"), p.Accessory + " #" + p.Seq)))
                return false;
            Library.ConfirmPlacement(p.Id, User, reason ?? "confirmed");
            AuditLogger.Log("profile library: " + p.Accessory + " #" + p.Seq + " on " + _current.Code + " confirmed at " + Mm(p.Y) + " / " + Mm(p.Z) + ".");
            AfterDecision(p.Id, string.Format(T("Prf_PlConfirmed", "{0} confirmed: it goes into the production program."), p.Accessory + " #" + p.Seq));
            return true;
        }

        internal bool RejectPlacement(string reason)
        {
            var p = SelectedPlacement;
            if (p == null || _current == null || !CanEdit) return false;
            if (reason == null)
                reason = Ask.Text(FindForm(), T("Prf_RejectTitle", "Not on this profile"), string.Format(T("Prf_RejectPrompt", "Why is {0} not on {1}?"), p.Accessory + " #" + p.Seq, _current.Code));
            if (reason == null) return false;
            Library.RejectPlacement(p.Id, User, reason);
            AuditLogger.Log("profile library: " + p.Accessory + " #" + p.Seq + " not on " + _current.Code + ": " + reason);
            AfterDecision(p.Id, string.Format(T("Prf_PlRejected", "{0} is not machined on this profile."), p.Accessory + " #" + p.Seq));
            return true;
        }

        private void ReopenPlacement()
        {
            var p = SelectedPlacement;
            if (p == null || _current == null || !CanEdit) return;
            Library.ReopenPlacement(p.Id, User, "reopened");
            AuditLogger.Log("profile library: " + p.Accessory + " #" + p.Seq + " on " + _current.Code + " reopened.");
            AfterDecision(p.Id, string.Format(T("Prf_PlReopened", "{0} is open again: tune it and confirm it."), p.Accessory + " #" + p.Seq));
        }

        private void AfterDecision(long placementId, string text)
        {
            LoadProfile(_current.Id, false);
            SelectPlacement(placementId);
            RefreshRow();
            Status(text);
        }

        private void ShowHistory()
        {
            var p = SelectedPlacement;
            if (p == null) return;
            var rows = Library.PlacementHistory(p.Id);
            using (var dlg = new Form
            {
                Text = T("Prf_HistoryTitle", "History") + " - " + p.Accessory + " #" + p.Seq + " - " + (_current != null ? _current.Code : ""),
                Width = 760, Height = 360, StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.SizableToolWindow
            })
            {
                UiScale.ApplyOnLoad(dlg);
                var g = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false };
                UiStyle.StyleGrid(g);
                foreach (var c in new[] { T("Prf_HWhen", "When"), T("Prf_HWho", "Who"), T("Prf_HStatus", "Status before"), "Y", "Z", T("Prf_HWhy", "Why") })
                    g.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = c, Width = c.Length > 2 ? 130 : 60 });
                g.Columns[g.Columns.Count - 1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                UiStyle.EnableTableTools(g, "ProfileHistory");
                g.Rows.Add(T("Prf_HNow", "now"), p.ConfirmedBy, LibraryEditorForm.StatusText(p.Status), Mm(p.Y), Mm(p.Z), "");
                foreach (var r in rows)
                    g.Rows.Add(ShortTime(Library.Text(r, "changed_at")), Library.Text(r, "changed_by"), LibraryEditorForm.StatusText(Library.Text(r, "status")),
                               Library.Real(r, "y_mm").HasValue ? Mm(Library.Real(r, "y_mm").Value) : "", Library.Real(r, "z_mm").HasValue ? Mm(Library.Real(r, "z_mm").Value) : "",
                               Library.Text(r, "reason"));
                dlg.Controls.Add(g);
                dlg.ShowDialog(FindForm());
            }
        }

        private void ShowCorrectionsMenu()
        {
            if (_current == null) return;
            var menu = UiStyle.Menu();
            var list = Library.Corrections(_current.Id);
            if (list.Count == 0) menu.Items.Add(new ToolStripMenuItem(T("Prf_NoCorrections", "No correction reaches this profile.")) { Enabled = false });
            foreach (var r in list)
            {
                long id = Library.Id(r, "id").Value;
                string scope = Library.Text(r, "scope_type");
                string text = (scope == "SERIES" ? T("Prf_CorrSeries", "series") : scope == "GROUP" ? T("Prf_CorrGroup", "group") : T("Prf_CorrProfile", "profile")) + ":  " +
                              (Library.Text(r, "accessory").Length > 0 ? Library.Text(r, "accessory") : T("Prf_CorrAll", "every accessory")) +
                              (Library.Text(r, "seq").Length > 0 ? " #" + Library.Text(r, "seq") : "") + "   " +
                              Signed(Library.Real(r, "correction_y_mm") ?? 0) + " / " + Signed(Library.Real(r, "correction_z_mm") ?? 0) + " mm   " +
                              Library.Text(r, "created_by") + " " + ShortTime(Library.Text(r, "created_at")) +
                              (Library.Text(r, "reason").Length > 0 ? "   \"" + Library.Text(r, "reason") + "\"" : "");
                var item = new ToolStripMenuItem(text);
                var remove = new ToolStripMenuItem(T("Prf_CorrRemove", "Take this correction away"), null, (s, e) => RemoveCorrection(id)) { Enabled = CanEdit };
                item.DropDownItems.Add(remove);
                menu.Items.Add(item);
            }
            UiStyle.ShowPopup(menu, _btnCorrections, new Point(0, _btnCorrections.Height));
        }

        internal void RemoveCorrection(long correctionId)
        {
            if (_current == null || !CanEdit) return;
            if (!Ask.YesNo(FindForm(), T("Prf_Title", "Profiles"), T("Prf_AskRemoveCorrection",
                "Take the correction away? The placements it moved go back, on every profile it reached - and are to be confirmed again."))) return;
            Library.RemoveCorrection(correctionId, _current.Id, User);
            AuditLogger.Log("profile library: correction " + correctionId + " taken away (" + _current.Code + ").");
            LoadProfile(_current.Id, false);
            RefreshRow();
            Status(T("Prf_CorrRemoved", "Correction taken away."));
        }

        // ---- the group's accessories -------------------------------------------------------------

        private void ShowAddAccessoryMenu()
        {
            if (_current == null || !CanEdit) return;
            var menu = UiStyle.Menu();
            var list = Library.AccessoriesFor(_current.GroupId);
            if (list.Count == 0) menu.Items.Add(new ToolStripMenuItem(T("Prf_NoMoreAcc", "Every accessory of the library is on the group already.")) { Enabled = false });
            string lastType = null;
            bool compatibleShown = false;
            foreach (var r in list)
            {
                bool fits = (Library.Id(r, "compatible") ?? 0) != 0;
                if (fits && !compatibleShown)
                {
                    menu.Items.Add(UiStyle.MenuHeader(string.Format(T("Prf_AccFits", "Fit the group's hardware interface ({0})"), _current.Interface)));
                    compatibleShown = true;
                }
                string type = Library.Text(r, "accessory_type");
                if (!fits && type != lastType) { menu.Items.Add(UiStyle.MenuHeader(type)); lastType = type; }
                long id = Library.Id(r, "id").Value;
                string code = Library.Text(r, "code");
                menu.Items.Add(new ToolStripMenuItem((fits ? "\u2605 " : "") + code + "   " + Library.Text(r, "description"), null, (s, e) => AddAccessory(id, code)));
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(T("Prf_AccLibrary", "New accessory... (library)"), null, (s, e) => OpenLibraryEditor("accessories")));
            UiStyle.ShowPopup(menu, _btnAddAcc, new Point(0, _btnAddAcc.Height));
        }

        internal void AddAccessory(long accessoryId, string code)
        {
            if (_current == null || !CanEdit) return;
            Library.AssignAccessory(_current.GroupId, accessoryId, true);
            AuditLogger.Log("profile library: " + code + " given to the group " + _current.Group + ".");
            LoadProfile(_current.Id, true);
            RefreshRow();
            Status(string.Format(T("Prf_AccAdded", "{0} is on every profile of the group {1}: its placements are worked out - check and confirm them."), code, _current.Group));
        }

        private void RemoveAccessory()
        {
            long id = SelectedAccessoryId();
            if (_current == null || id <= 0 || !CanEdit) return;
            string code = Convert.ToString(_accGrid.SelectedRows[0].Cells["code"].Value);
            if (!Ask.YesNo(FindForm(), T("Prf_Title", "Profiles"), string.Format(T("Prf_AskRemoveAcc",
                "Take {0} off the group {1}? It goes from every profile of the group (switch it off instead to keep it)."), code, _current.Group))) return;
            Library.UnassignAccessory(_current.GroupId, id);
            AuditLogger.Log("profile library: " + code + " taken off the group " + _current.Group + ".");
            LoadProfile(_current.Id, true);
            RefreshRow();
        }

        private void SetAccessoryOn(long accessoryId, bool on)
        {
            if (_current == null || accessoryId <= 0 || !CanEdit) return;
            Library.AssignAccessory(_current.GroupId, accessoryId, on);
            AuditLogger.Log("profile library: accessory " + accessoryId + " switched " + (on ? "on" : "off") + " for the group " + _current.Group + ".");
            LoadProfile(_current.Id, true);
            RefreshRow();
        }
    }
}
