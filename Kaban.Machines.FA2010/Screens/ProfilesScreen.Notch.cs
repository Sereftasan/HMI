using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Machines.FA2010.Jobs;
using Kaban.Profiles.Data;
using Kaban.UI.Controls;

namespace Kaban.Machines.FA2010.Screens
{
    /// <summary>
    /// THE PROFILE'S MULLION NOTCHING STEPS (7 November, the answer "Yes it should and yes values per profile" - the old program's
    /// ProfileSeryNotchSetting): a tab of the profile's details beside its machine settings - a step a line (X1 X2 Z1 Z2 Y1 Y2 Step Speed, mm),
    /// sent with each bar of the profile into R25700 (MullionNotch). Kept at once, as the machine settings are; a profile without its own
    /// shows its group's template's (a change makes them its own, "Use the group's steps" gives them back).
    /// </summary>
    public partial class ProfilesScreen
    {
        private TabPage _notchTab;
        private DataGridView _notchGrid;
        private Label _notchHead;
        private Button _btnNotchAdd, _btnNotchRemove, _btnNotchUp, _btnNotchDown, _btnNotchGroup;
        private bool _fillingNotch, _notchOwn, _notchEdit;
        /// <summary>The steps on display as the library has them.</summary>
        private List<double[]> _notchShown = new List<double[]>();
        private long _notchTemplate;

        private void BuildNotchTab()
        {
            _notchTab = new TabPage(T("Prf_TabNotch", "Mullion notching")) { BackColor = Color.White, Name = "notch" };
            _detailTabs.TabPages.Add(_notchTab);
            var label = PlcLabelTable.ByLabel(1, MullionNotch.Label);
            _notchHead = new Label { Dock = DockStyle.Top, Height = 52, Font = UiStyle.Small, ForeColor = Color.FromArgb(70, 80, 95), Padding = new Padding(8, 4, 8, 0) };
            Func<string, EventHandler, Button> btn = (text, click) =>
            {
                var b = UiStyle.TextButton(text, 0, click);
                b.Font = UiStyle.Small;
                b.Height = 28;
                b.Width = TextRenderer.MeasureText(text, b.Font).Width + 26;
                b.Margin = new Padding(0, 0, 6, 0);
                return b;
            };
            _btnNotchAdd = btn(T("Prf_NotchAdd", "+ Step"), (s, e) => NotchAdd());
            _btnNotchRemove = btn(T("Prf_NotchRemove", "− Step"), (s, e) => NotchRemove());
            _btnNotchUp = btn("▲", (s, e) => NotchMove(-1));
            _btnNotchDown = btn("▼", (s, e) => NotchMove(1));
            _btnNotchGroup = btn(T("Prf_NotchUseGroup", "Use the group's steps"), (s, e) => NotchUseGroup());
            _tips.SetToolTip(_btnNotchUp, T("Prf_NotchUpTip", "Move the step up"));
            _tips.SetToolTip(_btnNotchDown, T("Prf_NotchDownTip", "Move the step down"));
            _tips.SetToolTip(_btnNotchGroup, T("Prf_NotchUseGroupTip", "Forget this profile's own steps - its group's template's are taken again"));
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(6, 4, 0, 0), BackColor = Color.FromArgb(246, 247, 249), WrapContents = false };
            bar.Controls.AddRange(new Control[] { _btnNotchAdd, _btnNotchRemove, _btnNotchUp, _btnNotchDown, _btnNotchGroup });
            _notchGrid = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.CellSelect, MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, EditMode = DataGridViewEditMode.EditOnEnter
            };
            UiStyle.StyleGrid(_notchGrid);
            _notchGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "no", HeaderText = T("Prf_ColNo", "No"), ReadOnly = true, Width = 44, SortMode = DataGridViewColumnSortMode.NotSortable });
            var st = StructuredTypes.Find(label != null && !string.IsNullOrWhiteSpace(label.DataType) ? StructureName(label.DataType) : MullionNotch.Type, 1);
            foreach (var f in MullionNotch.Fields)
            {
                string head = f == "Step" ? T("Prf_NotchStep", "Step") : f == "Speed" ? T("Prf_NotchSpeed", "Speed") : f;
                var c = new DataGridViewTextBoxColumn { Name = f, HeaderText = head, Width = 74, SortMode = DataGridViewColumnSortMode.NotSortable };
                c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                var m = st != null ? st.Members.FirstOrDefault(x => string.Equals(x.Name, f, StringComparison.OrdinalIgnoreCase)) : null;
                if (m != null) c.ToolTipText = m.Comment ?? "";
                _notchGrid.Columns.Add(c);
            }
            _notchGrid.Columns["no"].DefaultCellStyle.ForeColor = Color.Gray;
            _notchGrid.CellValueChanged += (s, e) => { if (!_fillingNotch && e.RowIndex >= 0 && e.ColumnIndex > 0) NotchCellChanged(e.RowIndex, e.ColumnIndex); };
            _notchGrid.CurrentCellChanged += (s, e) => { if (!_fillingNotch) NotchButtons(); };
            _notchGrid.DataError += (s, e) => { e.ThrowException = false; };
            _notchTab.Controls.Add(_notchGrid);
            _notchTab.Controls.Add(bar);
            _notchTab.Controls.Add(_notchHead);
        }

        /// <summary>"Mullion Notching Step(0..29)" -> "Mullion Notching Step" (the label's data type, an array of it).</summary>
        private static string StructureName(string dataType)
        {
            int i = dataType.IndexOf('(');
            return (i > 0 ? dataType.Substring(0, i) : dataType).Trim();
        }

        /// <summary>The steps of the profile on display (its own, else its group's template's).</summary>
        private void FillNotch()
        {
            if (_notchGrid == null || _notchGrid.IsDisposed) return;
            _fillingNotch = true;
            try
            {
                int keepRow = _notchGrid.CurrentCell != null ? _notchGrid.CurrentCell.RowIndex : -1, keepCol = _notchGrid.CurrentCell != null ? _notchGrid.CurrentCell.ColumnIndex : 1;
                _notchGrid.Rows.Clear();
                _notchShown = new List<double[]>();
                _notchOwn = false;
                _notchTemplate = 0;
                Library.MachineValue v = null;
                if (_current != null && _libraryOk && _instance > 0)
                {
                    Library.MachineSettings(_current.Id, _instance).TryGetValue(MullionNotch.Code, out v);
                    _notchShown = MullionNotch.Parse(v != null ? v.Value : null);
                    _notchOwn = v != null && v.Own;
                    _notchTemplate = Library.GroupTemplateOf(_current.Id);
                }
                for (int i = 0; i < _notchShown.Count; i++)
                {
                    var cells = new object[MullionNotch.Fields.Length + 1];
                    cells[0] = (i + 1).ToString(CultureInfo.InvariantCulture);
                    for (int f = 0; f < MullionNotch.Fields.Length; f++) cells[f + 1] = NotchText(_notchShown[i][f]);
                    _notchGrid.Rows.Add(cells);
                }
                ShowNotchHead(v);
                if (keepRow >= 0 && _notchGrid.Rows.Count > 0)
                {
                    int r = Math.Min(keepRow, _notchGrid.Rows.Count - 1);
                    _notchGrid.CurrentCell = _notchGrid.Rows[r].Cells[Math.Max(1, Math.Min(keepCol, _notchGrid.Columns.Count - 1))];
                }
            }
            finally
            {
                _fillingNotch = false;
                NotchButtons();
            }
        }

        private static string NotchText(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

        /// <summary>What is sent and where the steps come from (the profile's own, its group's, none).</summary>
        private void ShowNotchHead(Library.MachineValue v)
        {
            var label = PlcLabelTable.ByLabel(1, MullionNotch.Label);
            string head = string.Format(T("Prf_NotchHead",
                "Sent to the PLC with each bar of this profile, before its parameters ({0}, {1}): a step a line in mm, {2} steps at most. A change is kept at once."),
                MullionNotch.Label, label != null ? label.Address : "R25700", MullionNotch.MaxSteps);
            if (_current == null || !_libraryOk || _instance <= 0) { _notchHead.Text = head; return; }
            string from;
            if (v == null) from = T("Prf_NotchNone", "No mullion notching steps: nothing goes to R25700 with this profile's bars.");
            else if (!v.Own) from = string.Format(T("Prf_NotchFromGroup", "These are its group's steps ({0}) - a change makes them this profile's own."), v.From);
            else if (_notchShown.Count == 0) from = T("Prf_NotchOwnNone", "This profile has no mullion notching steps of its own (its group's are not taken).");
            else from = T("Prf_NotchOwn", "These steps are this profile's own.");
            _notchHead.Text = head + "\r\n" + from;
        }

        /// <summary>Editing follows the user's level (below Maintenance the steps are only shown).</summary>
        private void ApplyNotchAccess(bool edit)
        {
            _notchEdit = edit;
            if (_notchGrid == null) return;
            _notchGrid.ReadOnly = !edit || _current == null;
            _notchGrid.Columns["no"].ReadOnly = true;
            NotchButtons();
        }

        private void NotchButtons()
        {
            if (_btnNotchAdd == null) return;
            bool edit = _notchEdit && _current != null && _libraryOk && _instance > 0;
            int row = _notchGrid.CurrentCell != null ? _notchGrid.CurrentCell.RowIndex : -1;
            _btnNotchAdd.Enabled = edit && _notchGrid.Rows.Count < MullionNotch.MaxSteps;
            _btnNotchRemove.Enabled = edit && row >= 0;
            _btnNotchUp.Enabled = edit && row > 0;
            _btnNotchDown.Enabled = edit && row >= 0 && row < _notchGrid.Rows.Count - 1;
            _btnNotchGroup.Enabled = edit && _notchOwn && _notchTemplate > 0 && _current != null && _notchTemplate != _current.Id;
        }

        /// <summary>The steps as shown (a copy to change).</summary>
        internal List<double[]> NotchSteps()
        {
            return (_notchShown ?? new List<double[]>()).Select(x => (double[])x.Clone()).ToList();
        }

        /// <summary>The steps kept as the profile's own; the table shown again (<paramref name="refill"/>), else only its head.</summary>
        private void KeepNotch(List<double[]> steps, string what, bool refill = true)
        {
            if (_current == null || !_libraryOk || _instance <= 0 || !CanEdit) return;
            string text = MullionNotch.Format(steps);
            Library.SetMachineSetting(_current.Id, _instance, MullionNotch.Code, text);
            AuditLogger.Log(string.Format(CultureInfo.InvariantCulture, "profile library: {0} - mullion notching steps {1} ({2} step(s): {3})", _current.Code, what, steps.Count, text));
            if (refill) { FillNotch(); return; }
            _notchShown = steps;
            _notchOwn = true;
            Library.MachineValue v;
            Library.MachineSettings(_current.Id, _instance).TryGetValue(MullionNotch.Code, out v);
            ShowNotchHead(v);
            NotchButtons();
        }

        private void NotchCellChanged(int rowIndex, int column)
        {
            if (_notchShown == null || rowIndex >= _notchShown.Count) return;
            var cell = _notchGrid.Rows[rowIndex].Cells[column];
            string t = Convert.ToString(cell.Value ?? "").Trim().Replace(',', '.');
            double d;
            if (t.Length == 0) t = "0";
            double was = _notchShown[rowIndex][column - 1];
            Action shown = () =>
            {
                // the value as kept (12,5 -> 12.5; a word or a number too big back to what it was)
                if (_notchGrid.IsDisposed || rowIndex >= _notchGrid.Rows.Count || rowIndex >= _notchShown.Count) return;
                _fillingNotch = true;
                try
                {
                    var c = _notchGrid.Rows[rowIndex].Cells[column];
                    c.Value = NotchText(_notchShown[rowIndex][column - 1]);
                    // still being edited: the box shows it too (else what was typed would come back as the cell is left)
                    if (_notchGrid.IsCurrentCellInEditMode && _notchGrid.CurrentCell == c) _notchGrid.RefreshEdit();
                }
                finally { _fillingNotch = false; }
            };
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            {
                Status(string.Format(T("Prf_MachineNotNumber", "{0}: \"{1}\" is not a number."), _notchGrid.Columns[column].HeaderText, t), true);
                Later(shown);
                return;
            }
            if (Math.Abs(d * 100) > short.MaxValue)
            {
                Status(string.Format(T("Prf_NotchTooBig", "{0}: {1} does not fit the PLC's word (x100: -327.68 - 327.67)."), _notchGrid.Columns[column].HeaderText, t), true);
                Later(shown);
                return;
            }
            if (d == was) { Later(shown); return; }      // the grid says a cell changed as it is left too - nothing to keep then
            Later(() =>
            {
                var steps = NotchSteps();           // as they are when it is kept
                if (rowIndex >= steps.Count) return;
                steps[rowIndex][column - 1] = d;
                KeepNotch(steps, "changed", false);
                shown();
            });
        }

        /// <summary>After the grid's own event (a change of its rows in its CellValueChanged would come back into it).</summary>
        private void Later(Action a)
        {
            if (IsHandleCreated && !IsDisposed) BeginInvoke(a); else a();
        }

        internal void NotchAdd()
        {
            var steps = NotchSteps();
            if (steps.Count >= MullionNotch.MaxSteps) return;
            int at = _notchGrid.CurrentCell != null ? _notchGrid.CurrentCell.RowIndex + 1 : steps.Count;
            // a new step starts as the one before it (most steps differ in a value or two)
            var step = at > 0 && at <= steps.Count ? (double[])steps[at - 1].Clone() : new double[MullionNotch.Fields.Length];
            steps.Insert(at, step);
            KeepNotch(steps, "added");
            if (at < _notchGrid.Rows.Count) _notchGrid.CurrentCell = _notchGrid.Rows[at].Cells[1];
            NotchButtons();
        }

        internal void NotchRemove()
        {
            if (_notchGrid.CurrentCell == null) return;
            int at = _notchGrid.CurrentCell.RowIndex;
            var steps = NotchSteps();
            if (at < 0 || at >= steps.Count) return;
            steps.RemoveAt(at);
            KeepNotch(steps, "removed");
        }

        internal void NotchMove(int by)
        {
            if (_notchGrid.CurrentCell == null) return;
            int at = _notchGrid.CurrentCell.RowIndex, to = at + by, col = _notchGrid.CurrentCell.ColumnIndex;
            var steps = NotchSteps();
            if (at < 0 || to < 0 || to >= steps.Count) return;
            var s = steps[at];
            steps[at] = steps[to];
            steps[to] = s;
            KeepNotch(steps, "moved");
            _notchGrid.CurrentCell = _notchGrid.Rows[to].Cells[Math.Max(1, col)];
            NotchButtons();
        }

        internal void NotchUseGroup()
        {
            if (_current == null || !_libraryOk || _instance <= 0 || !CanEdit) return;
            Library.SetMachineSetting(_current.Id, _instance, MullionNotch.Code, "");
            AuditLogger.Log("profile library: " + _current.Code + " - mullion notching steps: its own forgotten (its group's taken)");
            FillNotch();
        }
    }
}
