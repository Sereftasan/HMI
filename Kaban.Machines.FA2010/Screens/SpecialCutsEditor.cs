using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Profiles.Jobs;
using Kaban.UI.Controls;
using Kaban.UI.Profiles;

namespace Kaban.Machines.FA2010.Screens
{
    /// <summary>
    /// THE SPECIAL CUT RECIPES (6 November: "L105/L106 with D65 (the special-cut recipes) and V-cuts ... yes please") - the old program's
    /// saw receipts (its Saw Process page): a recipe each joint of special ends, its steps (clamp, unclamp, the robots, a normal cut, the V
    /// saw, the exit robot, a merge), its formulas of T D W O L R. Entered here, taken from the old program's tables (CSV) or from the
    /// device list's samples (its "Kırlangıç Kesim" sheet). On the Profiles page (7 November: "The special cut recipes should be edited in
    /// the profiles page ... just like the accessories, they are defined as independent from the profiles but they have their values
    /// calculated according the machine and profile parameters dynamically with the job operation"): a tab beside the accessories, the
    /// values of the selected profile shown under the recipe. Built in code (no designer file).
    /// </summary>
    internal sealed class SpecialCutsEditor : UserControl
    {
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        private readonly DataGridView _recipes = new DataGridView(), _steps = new DataGridView();
        private readonly Label _problem = new Label(), _preview = new Label(), _state = new Label();
        private bool _canEdit;
        private bool _filling;
        private readonly List<Button> _editButtons = new List<Button>();
        private Button _btnSave, _btnRevert;

        /// <summary>The recipes as edited (a copy of the kept ones until saved).</summary>
        internal List<SpecialCutRecipe> Recipes = new List<SpecialCutRecipe>();
        /// <summary>Changed since loaded or saved.</summary>
        internal bool Dirty { get; private set; }
        /// <summary>The recipes to keep (the Profiles page saves them in the library); true when kept.</summary>
        internal Func<List<SpecialCutRecipe>, bool> SaveHook;
        /// <summary>The kept recipes, read again (Revert).</summary>
        internal Func<List<SpecialCutRecipe>> LoadHook;
        /// <summary>The values of the selected profile (null: none) and its name - the preview under the recipe.</summary>
        private FormulaValues _values;
        private string _valuesOf = "";
        /// <summary>The device list's sample recipes (SpecialCutSamples.csv beside the program).</summary>
        internal static string SamplesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SpecialCutSamples.csv");

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        internal static string CommandName(int command)
        {
            switch ((SpecialCutCommand)command)
            {
                case SpecialCutCommand.Clamp: return T("Job_Cmd1", "Clamp");
                case SpecialCutCommand.Unclamp: return T("Job_Cmd2", "Unclamp");
                case SpecialCutCommand.RobotsMove: return T("Job_Cmd3", "Robots move");
                case SpecialCutCommand.NormalCut: return T("Job_Cmd4", "Normal cut");
                case SpecialCutCommand.VCut: return T("Job_Cmd5", "V cut");
                case SpecialCutCommand.ExitRobotPulls: return T("Job_Cmd6", "Exit robot pulls the profile");
                case SpecialCutCommand.Merge: return T("Job_Cmd7", "Merge the recipes");
                default: return command.ToString(CI);
            }
        }

        private static string CommandItem(int c) { return c.ToString(CI) + " " + CommandName(c); }

        public SpecialCutsEditor(bool canEdit)
        {
            _canEdit = canEdit;
            BackColor = Color.White;
            Dock = DockStyle.Fill;

            var help = new Label
            {
                Dock = DockStyle.Top, Height = 48, Font = UiStyle.Small, ForeColor = Color.FromArgb(60, 70, 85), Padding = new Padding(6, 4, 6, 0),
                Text = T("Job_RecipesHelp2", "A recipe makes the joint of a piece's right end (its right code) and the next piece's left end (its left code) - " +
                         "an ordinary end's code is its angle, 0 any: a recipe of one special end is merged with one of the other end when no recipe has both. " +
                         "Formulas: T the saw blade, D the saw - V cut distance, W the profile's Cut Full Width, O the cut length offset, L and R the job file's left and right values.")
            };

            // ---- the recipes ------------------------------------------------------------------------------
            Grid(_recipes);
            AddText(_recipes, "no", T("Job_RcNo", "No"), 52, true);
            AddText(_recipes, "name", T("Job_RcName", "Name"), 190, false);
            AddText(_recipes, "left", T("Job_RcLeft", "Left code"), 78, true);
            AddText(_recipes, "right", T("Job_RcRight", "Right code"), 82, true);
            AddText(_recipes, "angle", T("Job_RcAngle", "Starting angle °"), 104, true);
            AddText(_recipes, "holding", T("Job_RcHolding", "Exit robot holding"), 130, false);
            AddText(_recipes, "offset", T("Job_RcOffset", "Robot X offset"), 116, false);
            AddText(_recipes, "dust", T("Job_RcDust", "Saw dust width"), 116, false);
            _recipes.Columns.Add(new DataGridViewCheckBoxColumn { Name = "offL", HeaderText = T("Job_RcOffL", "With L value"), Width = 92, SortMode = DataGridViewColumnSortMode.NotSortable });
            _recipes.Columns.Add(new DataGridViewCheckBoxColumn { Name = "offR", HeaderText = T("Job_RcOffR", "With R value"), Width = 92, SortMode = DataGridViewColumnSortMode.NotSortable });
            _recipes.Columns["offL"].ToolTipText = T("Job_TipRcOffL", "The recipe for a left end that has a value (L not 0)");
            _recipes.Columns["offR"].ToolTipText = T("Job_TipRcOffR", "The recipe for a right end that has a value (R not 0)");
            _recipes.SelectionChanged += (s, e) => { if (!_filling) { FillSteps(); ShowPreview(); } };
            _recipes.CellValueChanged += (s, e) => { if (!_filling && e.RowIndex >= 0) TakeRecipe(e.RowIndex); };
            _recipes.CurrentCellDirtyStateChanged += (s, e) => { if (_recipes.IsCurrentCellDirty && _recipes.CurrentCell is DataGridViewCheckBoxCell) _recipes.CommitEdit(DataGridViewDataErrorContexts.Commit); };

            // ---- the selected recipe's steps ---------------------------------------------------------------
            Grid(_steps);
            AddText(_steps, "no", "#", 40, true);
            _steps.Columns["no"].ReadOnly = true;
            var cmd = new DataGridViewComboBoxColumn { Name = "command", HeaderText = T("Job_StCommand", "Step"), Width = 220, FlatStyle = FlatStyle.Flat, SortMode = DataGridViewColumnSortMode.NotSortable };
            for (int c = 1; c <= 7; c++) cmd.Items.Add(CommandItem(c));
            _steps.Columns.Add(cmd);
            _steps.Columns.Add(new DataGridViewCheckBoxColumn { Name = "lclamp", HeaderText = T("Job_StLeftClamp", "Left clamp"), Width = 84, SortMode = DataGridViewColumnSortMode.NotSortable });
            _steps.Columns.Add(new DataGridViewCheckBoxColumn { Name = "rclamp", HeaderText = T("Job_StRightClamp", "Right clamp"), Width = 88, SortMode = DataGridViewColumnSortMode.NotSortable });
            AddText(_steps, "robot1", T("Job_StRobot1", "Robot 1"), 130, false);
            AddText(_steps, "robot2", T("Job_StRobot2", "Robot 2"), 130, false);
            AddText(_steps, "sangle", T("Job_StAngle", "Angle °"), 80, true);
            AddText(_steps, "vcut", T("Job_StVCut", "V cut Y"), 130, false);
            _steps.CellValueChanged += (s, e) => { if (!_filling && e.RowIndex >= 0) TakeStep(e.RowIndex); };
            _steps.SelectionChanged += (s, e) => { if (!_filling) ShowPreview(); };
            _steps.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_steps.IsCurrentCellDirty && (_steps.CurrentCell is DataGridViewCheckBoxCell || _steps.CurrentCell is DataGridViewComboBoxCell))
                    _steps.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _steps.DataError += (s, e) => { e.ThrowException = false; };

            // ---- the buttons ------------------------------------------------------------------------------
            _btnSave = Btn("\uD83D\uDCBE " + T("Job_RcSave", "Save"), (s, e) => Save(), true);
            _btnRevert = Btn("\u21A9 " + T("Job_RcRevert", "Undo the changes"), (s, e) => Revert(), true);
            var bar1 = Buttons(new[]
            {
                _btnSave, _btnRevert,
                Btn("+ " + T("Job_RcAdd", "Recipe"), (s, e) => AddRecipe(), true),
                Btn("\u2212 " + T("Job_RcDelete", "Recipe"), (s, e) => DeleteRecipe(), true),
                Btn("\u29C9 " + T("Job_RcCopy", "Copy"), (s, e) => CopyRecipe(), true),
                Btn(T("Job_RcSamples", "Device list samples"), (s, e) => AddSamples(), true),
                Btn(T("Job_RcImport", "Import from the old program..."), (s, e) => ImportOld(), true)
            });
            _editButtons.AddRange(bar1.Controls.OfType<Button>().Where(b => b != _btnRevert));
            _state.AutoSize = true;
            _state.Font = UiStyle.Small;
            _state.ForeColor = Color.FromArgb(190, 110, 0);
            _state.Margin = new Padding(6, 8, 0, 0);
            bar1.Controls.Add(_state);
            var bar2 = Buttons(new[]
            {
                Btn("+ " + T("Job_StAdd", "Step"), (s, e) => AddStep(), true),
                Btn("\u2212 " + T("Job_StDelete", "Step"), (s, e) => DeleteStep(), true),
                Btn("\u25B2", (s, e) => MoveStep(-1), true),
                Btn("\u25BC", (s, e) => MoveStep(1), true)
            });
            _editButtons.AddRange(bar2.Controls.OfType<Button>());
            // under the recipe: its values for the profile selected on the page (the job works them out with each bar's)
            _preview.Dock = DockStyle.Bottom;
            _preview.Height = 40;
            _preview.Font = UiStyle.Small;
            _preview.ForeColor = Color.FromArgb(30, 90, 170);
            _preview.Padding = new Padding(6, 2, 6, 2);
            _preview.AutoEllipsis = true;
            _problem.Dock = DockStyle.Bottom;
            _problem.Height = 22;
            _problem.ForeColor = Color.FromArgb(180, 40, 40);
            _problem.Font = UiStyle.Small;
            _problem.Padding = new Padding(6, 2, 6, 2);

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
            split.Panel1.Controls.Add(_recipes);
            split.Panel1.Controls.Add(bar1);
            split.Panel2.Controls.Add(_steps);
            split.Panel2.Controls.Add(bar2);
            split.Panel2.Controls.Add(_preview);
            Controls.Add(split);
            Controls.Add(_problem);
            Controls.Add(help);
            HandleCreated += (s, e) => { try { split.SplitterDistance = Math.Max(80, split.Height * 2 / 5); } catch { } };
            ApplyAccess(canEdit);
            FillRecipes(0);
            // taken apart: the grids' events then are not the operator's
            Disposed += (s, e) => _filling = true;
        }

        /// <summary>The recipes kept, shown (what is being changed and not saved is forgotten).</summary>
        internal void LoadRecipes(IEnumerable<SpecialCutRecipe> recipes)
        {
            Recipes = (recipes ?? new SpecialCutRecipe[0]).Select(r => r.Clone()).OrderBy(r => r.No).ToList();
            SetDirty(false);
            FillRecipes(0);
        }

        /// <summary>Changes for the Maintenance level; the others see them.</summary>
        internal void ApplyAccess(bool canEdit)
        {
            _canEdit = canEdit;
            foreach (var b in _editButtons) b.Enabled = canEdit && (b != _btnSave || Dirty);
            _btnRevert.Enabled = Dirty;
            _recipes.ReadOnly = !canEdit;
            _steps.ReadOnly = !canEdit;
            for (int i = 0; i < _steps.Rows.Count; i++) Shade(i);
        }

        private void SetDirty(bool dirty)
        {
            Dirty = dirty;
            _state.Text = dirty ? T("Job_RcUnsaved", "changed - not saved yet") : "";
            if (_btnSave != null) _btnSave.Enabled = _canEdit && dirty;
            if (_btnRevert != null) _btnRevert.Enabled = dirty;
        }

        /// <summary>Kept (when every formula can be worked out).</summary>
        internal bool Save()
        {
            if (!_canEdit || !Check()) return false;
            if (SaveHook != null && !SaveHook(Recipes.Select(r => r.Clone()).ToList())) return false;
            SetDirty(false);
            return true;
        }

        internal void Revert()
        {
            if (LoadHook != null) LoadRecipes(LoadHook());
        }

        /// <summary>The values the formulas take for the profile selected on the page (null: none) - shown under the recipe.</summary>
        internal void SetValues(FormulaValues values, string of)
        {
            _values = values;
            _valuesOf = of ?? "";
            ShowPreview();
        }

        /// <summary>What the preview shows now (tests).</summary>
        internal string PreviewText { get { return _preview.Text; } }

        private void ShowPreview()
        {
            var r = Current;
            if (_values == null) { _preview.Text = T("Job_RcNoProfile", "Select a profile to see the recipe's values for it."); return; }
            if (r == null) { _preview.Text = ""; return; }
            Func<string, string> v = f =>
            {
                double x;
                string why;
                return SpecialCuts.Eval(f, _values, out x, out why) ? x.ToString("0.##", CI) : "?";
            };
            var sb = new System.Text.StringBuilder();
            sb.Append(string.Format(CI, T("Job_RcValuesOf", "For {0} (W {1}, T {2}, O {3}, D {4}; L = R = 0):"), _valuesOf, _values.W.ToString("0.##", CI),
                                    _values.T.ToString("0.##", CI), _values.O.ToString("0.##", CI), _values.D.ToString("0.##", CI)));
            sb.Append(string.Format(CI, "  {0} {1}, {2} {3}, {4} {5}", T("Job_RcHolding", "Exit robot holding"), v(r.HoldingPosition), T("Job_RcOffset", "Robot X offset"),
                                    v(r.RobotXOffset), T("Job_RcDust", "Saw dust width"), v(r.SawDustWidth)));
            int k = _steps.CurrentCell != null ? _steps.CurrentCell.RowIndex : -1;
            if (k >= 0 && k < r.Steps.Count)
            {
                var st = r.Steps[k];
                sb.Append(string.Format(CI, "\r\n{0} {1}: {2}", T("Job_StCommand", "Step"), k + 1, CommandName(st.Command)));
                if (SpecialCutStep.Uses(st.Command, "Robot1")) sb.Append(", " + T("Job_StRobot1", "Robot 1") + " " + v(st.Robot1));
                if (SpecialCutStep.Uses(st.Command, "Robot2")) sb.Append(", " + T("Job_StRobot2", "Robot 2") + " " + v(st.Robot2));
                if (SpecialCutStep.Uses(st.Command, "VCutY")) sb.Append(", " + T("Job_StVCut", "V cut Y") + " " + v(st.VCutY));
            }
            _preview.Text = sb.ToString();
        }

        // ---- building -----------------------------------------------------------------------------------

        private static void Grid(DataGridView g)
        {
            g.Dock = DockStyle.Fill;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.RowHeadersVisible = false;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.CellSelect;
            g.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            UiStyle.StyleGrid(g);
        }

        private static void AddText(DataGridView g, string name, string header, int width, bool right)
        {
            var c = new DataGridViewTextBoxColumn { Name = name, HeaderText = header, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable };
            if (right) c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            g.Columns.Add(c);
        }

        private static Button Btn(string text, EventHandler click, bool enabled)
        {
            var b = UiStyle.TextButton(text, 0, click);
            b.Font = UiStyle.Small;
            b.Height = 28;
            b.Width = TextRenderer.MeasureText(text, b.Font).Width + 26;
            b.Margin = new Padding(0, 0, 6, 0);
            b.Enabled = enabled;
            return b;
        }

        private static FlowLayoutPanel Buttons(Control[] buttons)
        {
            var p = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(6, 4, 0, 0), BackColor = Color.FromArgb(246, 247, 249), WrapContents = false };
            p.Controls.AddRange(buttons);
            return p;
        }

        // ---- the grids and the model ---------------------------------------------------------------------

        internal SpecialCutRecipe Current
        {
            get
            {
                int i = _recipes.CurrentCell != null ? _recipes.CurrentCell.RowIndex : -1;
                return i >= 0 && i < Recipes.Count ? Recipes[i] : null;
            }
        }

        private static string N(double v) { return v.ToString("0.###", CI); }

        private void FillRecipes(int select)
        {
            _filling = true;
            try
            {
                _recipes.Rows.Clear();
                foreach (var r in Recipes)
                    _recipes.Rows.Add(r.No.ToString(CI), r.Name, N(r.LeftCode), N(r.RightCode), N(r.StartingAngle), r.HoldingPosition, r.RobotXOffset, r.SawDustWidth,
                                      r.WithOffsetLeft, r.WithOffsetRight);
                if (_recipes.Rows.Count > 0) _recipes.CurrentCell = _recipes.Rows[Math.Max(0, Math.Min(select, _recipes.Rows.Count - 1))].Cells["name"];
            }
            finally { _filling = false; }
            FillSteps();
        }

        private void FillSteps()
        {
            if (IsDisposed || Disposing || _steps.Columns.Count == 0) return;
            _filling = true;
            try
            {
                _steps.Rows.Clear();
                var r = Current;
                if (r == null) return;
                for (int k = 0; k < r.Steps.Count; k++)
                {
                    var st = r.Steps[k];
                    int row = _steps.Rows.Add((k + 1).ToString(CI), st.Command >= 1 && st.Command <= 7 ? CommandItem(st.Command) : null, st.LeftClamp, st.RightClamp,
                                              st.Robot1, st.Robot2, N(st.Angle), st.VCutY);
                    Shade(row);
                }
            }
            finally { _filling = false; }
        }

        /// <summary>A step's cells its command does not use: greyed and not typed in (the old program's editor did the same).</summary>
        private void Shade(int row)
        {
            var r = Current;
            if (r == null || row < 0 || row >= r.Steps.Count) return;
            int c = r.Steps[row].Command;
            foreach (var kv in new[] { new[] { "lclamp", "LeftClamp" }, new[] { "rclamp", "RightClamp" }, new[] { "robot1", "Robot1" }, new[] { "robot2", "Robot2" },
                                       new[] { "sangle", "Angle" }, new[] { "vcut", "VCutY" } })
            {
                var cell = _steps.Rows[row].Cells[kv[0]];
                bool used = SpecialCutStep.Uses(c, kv[1]);
                cell.ReadOnly = !used || !_canEdit;
                cell.Style.BackColor = used ? Color.Empty : Color.FromArgb(238, 240, 243);
            }
        }

        private static double Num(object v, double fallback = 0)
        {
            double d;
            return double.TryParse(Convert.ToString(v, CI).Trim().Replace(',', '.'), NumberStyles.Float, CI, out d) ? d : fallback;
        }

        private static string Str(object v) { return (Convert.ToString(v, CI) ?? "").Trim(); }

        private void TakeRecipe(int i)
        {
            if (i < 0 || i >= Recipes.Count) return;
            var r = Recipes[i];
            var row = _recipes.Rows[i];
            int no;
            if (int.TryParse(Str(row.Cells["no"].Value), NumberStyles.Integer, CI, out no)) r.No = no;
            r.Name = Str(row.Cells["name"].Value).Replace(";", ",");
            r.LeftCode = Num(row.Cells["left"].Value);
            r.RightCode = Num(row.Cells["right"].Value);
            r.StartingAngle = Num(row.Cells["angle"].Value);
            r.HoldingPosition = Str(row.Cells["holding"].Value);
            r.RobotXOffset = Str(row.Cells["offset"].Value);
            r.SawDustWidth = Str(row.Cells["dust"].Value);
            r.WithOffsetLeft = Convert.ToBoolean(row.Cells["offL"].Value ?? false);
            r.WithOffsetRight = Convert.ToBoolean(row.Cells["offR"].Value ?? false);
            SetDirty(true);
            Check();
            ShowPreview();
        }

        private void TakeStep(int i)
        {
            var r = Current;
            if (r == null || i < 0 || i >= r.Steps.Count) return;
            var st = r.Steps[i];
            var row = _steps.Rows[i];
            string c = Str(row.Cells["command"].Value);
            int cmd;
            if (c.Length > 0 && int.TryParse(c.Split(' ')[0], NumberStyles.Integer, CI, out cmd)) st.Command = cmd;
            st.LeftClamp = Convert.ToBoolean(row.Cells["lclamp"].Value ?? false);
            st.RightClamp = Convert.ToBoolean(row.Cells["rclamp"].Value ?? false);
            st.Robot1 = Str(row.Cells["robot1"].Value);
            st.Robot2 = Str(row.Cells["robot2"].Value);
            st.Angle = Num(row.Cells["sangle"].Value);
            st.VCutY = Str(row.Cells["vcut"].Value);
            Shade(i);
            SetDirty(true);
            Check();
            ShowPreview();
        }

        // ---- the buttons ----------------------------------------------------------------------------------

        internal void AddRecipe()
        {
            int no = Recipes.Count == 0 ? 1 : Recipes.Max(r => r.No) + 1;
            Recipes.Add(new SpecialCutRecipe { No = no, Name = T("Job_RcNew", "New recipe"), StartingAngle = 90 });
            SetDirty(true);
            FillRecipes(Recipes.Count - 1);
        }

        internal void CopyRecipe()
        {
            var r = Current;
            if (r == null) return;
            var c = r.Clone();
            c.No = Recipes.Max(x => x.No) + 1;
            c.Name = r.Name + " (2)";
            Recipes.Add(c);
            SetDirty(true);
            FillRecipes(Recipes.Count - 1);
        }

        internal void DeleteRecipe()
        {
            var r = Current;
            if (r == null) return;
            if (!Ask.YesNo(this, T("Job_RecipesTitle", "Special cut recipes"), string.Format(T("Job_RcAskDelete", "Delete recipe {0}?"), r.Title))) return;
            int i = Recipes.IndexOf(r);
            Recipes.Remove(r);
            SetDirty(true);
            FillRecipes(i);
        }

        internal void AddStep()
        {
            var r = Current;
            if (r == null) return;
            int at = _steps.CurrentCell != null ? _steps.CurrentCell.RowIndex + 1 : r.Steps.Count;
            r.Steps.Insert(Math.Min(at, r.Steps.Count), new SpecialCutStep { Command = (int)SpecialCutCommand.Clamp, LeftClamp = true, RightClamp = true });
            SetDirty(true);
            FillSteps();
            if (_steps.Rows.Count > 0) _steps.CurrentCell = _steps.Rows[Math.Min(at, _steps.Rows.Count - 1)].Cells["command"];
            Check();
        }

        internal void DeleteStep()
        {
            var r = Current;
            if (r == null || _steps.CurrentCell == null) return;
            int i = _steps.CurrentCell.RowIndex;
            if (i < 0 || i >= r.Steps.Count) return;
            r.Steps.RemoveAt(i);
            SetDirty(true);
            FillSteps();
            if (_steps.Rows.Count > 0) _steps.CurrentCell = _steps.Rows[Math.Min(i, _steps.Rows.Count - 1)].Cells["command"];
            Check();
        }

        internal void MoveStep(int by)
        {
            var r = Current;
            if (r == null || _steps.CurrentCell == null) return;
            int i = _steps.CurrentCell.RowIndex, j = i + by;
            if (i < 0 || j < 0 || j >= r.Steps.Count) return;
            var st = r.Steps[i];
            r.Steps.RemoveAt(i);
            r.Steps.Insert(j, st);
            SetDirty(true);
            int col = _steps.CurrentCell.ColumnIndex;
            FillSteps();
            _steps.CurrentCell = _steps.Rows[j].Cells[col];
        }

        /// <summary>The old program's recipes: its tables sawreceipts and sawreceiptdetails exported as CSV (pgAdmin, with the headers).</summary>
        private void ImportOld()
        {
            string recipes, steps;
            using (var d = new OpenFileDialog { Filter = "CSV (*.csv)|*.csv|*.*|*.*", Title = T("Job_RcImportRecipes", "The old program's recipes: the table sawreceipts as CSV") })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                recipes = d.FileName;
            }
            using (var d = new OpenFileDialog { Filter = "CSV (*.csv)|*.csv|*.*|*.*", Title = T("Job_RcImportSteps", "Their steps: the table sawreceiptdetails as CSV"),
                                                 InitialDirectory = Path.GetDirectoryName(recipes) })
            {
                steps = d.ShowDialog(this) == DialogResult.OK ? d.FileName : "";
            }
            ImportFrom(recipes, steps);
        }

        /// <summary>
        /// The device list's sample recipes ("Kırlangıç Kesim": First End Arrow, Back Open Y ...) added after the ones here, numbered on -
        /// their special cut codes, starting angle and saw dust width are not in the sheet: to be typed in.
        /// </summary>
        internal int AddSamples()
        {
            string title = T("Job_RecipesTitle", "Special cut recipes");
            List<SpecialCutRecipe> list;
            try { list = File.Exists(SamplesPath) ? SpecialCuts.Parse(File.ReadAllText(SamplesPath)) : new List<SpecialCutRecipe>(); }
            catch (Exception ex) { Ask.Tell(this, title, ex.Message); return 0; }
            if (list.Count == 0) { Ask.Tell(this, title, T("Job_RcNoSamples", "The samples' file (SpecialCutSamples.csv) is not beside the program.")); return 0; }
            int no = Recipes.Count == 0 ? 0 : Recipes.Max(r => r.No);
            var names = new HashSet<string>(Recipes.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
            int added = 0;
            foreach (var r in list.Where(x => !names.Contains(x.Name)))
            {
                r.No = ++no;
                Recipes.Add(r);
                added++;
            }
            if (added == 0) { Ask.Tell(this, title, T("Job_RcSamplesThere", "The device list's samples are here already.")); return 0; }
            SetDirty(true);
            FillRecipes(Recipes.Count - added);
            Ask.Tell(this, title, string.Format(T("Job_RcSamplesAdded",
                "{0} sample recipe(s) of the device list added. Their left and right codes, starting angle and saw dust width are not in the device list - type them in, then Save."), added));
            return added;
        }

        /// <summary>The import (a test calls it with its files): asked before the recipes here are replaced.</summary>
        internal bool ImportFrom(string recipesCsv, string stepsCsv)
        {
            var problems = new List<string>();
            List<SpecialCutRecipe> list;
            try { list = SpecialCuts.ImportOld(recipesCsv, stepsCsv, problems); }
            catch (Exception ex) { Ask.Tell(this, T("Job_RecipesTitle", "Special cut recipes"), ex.Message); return false; }
            if (list.Count == 0) { Ask.Tell(this, T("Job_RecipesTitle", "Special cut recipes"), T("Job_RcImportNone", "No recipe was found in the file.") + (problems.Count > 0 ? "\r\n" + string.Join("\r\n", problems.Distinct()) : "")); return false; }
            string q = string.Format(T("Job_RcImportAsk", "{0} recipe(s) with {1} step(s) were read. Replace the {2} recipe(s) here with them?"),
                                     list.Count, list.Sum(r => r.Steps.Count), Recipes.Count);
            if (problems.Count > 0) q += "\r\n\r\n" + string.Join("\r\n", problems.Distinct().Take(8));
            if (Recipes.Count > 0 && !Ask.YesNo(this, T("Job_RecipesTitle", "Special cut recipes"), q)) return false;
            Recipes = list;
            SetDirty(true);
            FillRecipes(0);
            AuditLogger.Log(string.Format(CI, "[JOB] special cut recipes imported from {0}{1}: {2} recipe(s)", recipesCsv, stepsCsv.Length > 0 ? " and " + stepsCsv : "", list.Count));
            return true;
        }

        /// <summary>What keeps the recipes from being saved (a formula that cannot be worked out, a number twice, too many steps) - shown.</summary>
        internal bool Check()
        {
            var bad = new List<string>();
            foreach (var g in Recipes.GroupBy(r => r.No).Where(g => g.Count() > 1))
                bad.Add(string.Format(T("Job_RcTwice", "recipe number {0} twice"), g.Key));
            for (int i = 0; i < Recipes.Count; i++)
            {
                var r = Recipes[i];
                var row = i < _recipes.Rows.Count ? _recipes.Rows[i] : null;
                foreach (var f in new[] { new[] { "holding", r.HoldingPosition }, new[] { "offset", r.RobotXOffset }, new[] { "dust", r.SawDustWidth } })
                {
                    string why;
                    bool ok = SpecialCuts.Valid(f[1], out why);
                    if (!ok) bad.Add(r.Title + ": " + why);
                    if (row != null) row.Cells[f[0]].Style.ForeColor = ok ? Color.Empty : Color.FromArgb(190, 30, 30);
                }
                if (r.Steps.Count > SpecialCuts.MaxSteps) bad.Add(string.Format(T("Job_RcTooMany", "{0}: {1} steps - the PLC takes {2}"), r.Title, r.Steps.Count, SpecialCuts.MaxSteps));
                for (int k = 0; k < r.Steps.Count; k++)
                {
                    var st = r.Steps[k];
                    foreach (var f in new[] { new[] { "robot1", st.Robot1 }, new[] { "robot2", st.Robot2 }, new[] { "vcut", st.VCutY } })
                    {
                        string why;
                        bool ok = SpecialCuts.Valid(f[1], out why);
                        if (!ok) bad.Add(r.Title + string.Format(CI, ", step {0}: ", k + 1) + why);
                        if (r == Current && k < _steps.Rows.Count) _steps.Rows[k].Cells[f[0]].Style.ForeColor = ok ? Color.Empty : Color.FromArgb(190, 30, 30);
                    }
                }
            }
            _problem.Text = bad.Count == 0 ? "" : "⚠ " + bad[0] + (bad.Count > 1 ? string.Format(CI, "  (+{0})", bad.Count - 1) : "");
            return bad.Count == 0;
        }
    }
}
