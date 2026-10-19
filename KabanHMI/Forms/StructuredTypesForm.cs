using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// THE STRUCTURED DATA TYPES (19 October: "a Structured Data Type in Mitsubishi literature:
    /// each member of the array is a combination of data - each with its own data type and its
    /// index inside it - and the structure has its own length, so the next array member follows
    /// the previous one that many devices on; it should be saved, and selected just like a simple
    /// data type later").
    ///
    /// The list of structures on the left; the chosen one's name, its length in devices and its
    /// members on the right: each member's name, simple data type, offset (the words from the
    /// structure's start) and - a Bit - its bit of that word. A new member goes right after the
    /// last one. Nothing is kept until OK, and OK keeps only structures that are complete.
    /// </summary>
    public class StructuredTypesForm : Form
    {
        private readonly ListBox _types = new ListBox();
        private readonly TextBox _name = new TextBox();
        private readonly NumericUpDown _size = new NumericUpDown();
        private readonly TextBox _comment = new TextBox();
        private readonly DataGridView _members = new DataGridView();
        private readonly Label _problem = new Label();
        private readonly ToolTip _tips = new ToolTip();
        private readonly Button _new, _copy, _delete, _addMember, _removeMember, _up, _down;
        private readonly List<StructuredTypes.Type> _work;
        /// <summary>The station whose machine's types these are (each machine keeps its own).</summary>
        private readonly int _station;
        private bool _filling;

        /// <summary>The member types offered: the simple ones, strings in a few lengths.</summary>
        internal static readonly string[] MemberTypes =
            StructuredTypes.SimpleTypes.Where(t => t != "String")
                .Concat(new[] { "String(8)", "String(16)", "String(32)", "String(64)" }).ToArray();

        /// <summary>The structures as edited (the tests read them).</summary>
        internal List<StructuredTypes.Type> Working { get { return _work; } }

        /// <summary>The structure chosen on the left.</summary>
        internal StructuredTypes.Type Current { get { return _types.SelectedIndex >= 0 && _types.SelectedIndex < _work.Count ? _work[_types.SelectedIndex] : null; } }

        /// <param name="select">The structure to show first (a row's data type), or null.</param>
        /// <param name="station">The station whose machine's types are edited (0: the main machine's).</param>
        public StructuredTypesForm(string select = null, int station = 0)
        {
            UiScale.ApplyOnLoad(this);
            _station = station;
            var machine = StructuredTypes.MachineOf(station);
            Text = T("Ttl_StructTypes", "Structured data types") + (machine.Length > 0 ? " - " + machine : "");
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = UiStyle.Body;
            ClientSize = new Size(980, 560);
            MinimumSize = new Size(820, 460);

            _work = StructuredTypes.All(station).Select(t => t.Clone()).ToList();
            Kaban.UI.Helpers.Shortcuts.Attach(this);      // Insert / Delete / Ctrl+Up / Ctrl+Down: the buttons nearest the focus

            // 19 October: "remove the captions - the page's name says it; the buttons on top of
            // the list and of the labels, with emojis instead of words, just like the other pages;
            // Name, Length and Comment each on its own line, the label left of its box"
            const int bottom = 56;                         // OK / Cancel under both halves

            // ---- the list: its buttons above it ----
            var left = new Panel { Bounds = new Rectangle(0, 0, 238, ClientSize.Height - bottom), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left };
            Controls.Add(left);
            _new = UiStyle.ToolButton(E(0x2795), T("Tip_StructNew", "New structure"), _tips);
            _copy = UiStyle.IconButton(ToolGlyphs.Copy, "Copy", T("Tip_StructCopy", "A copy of the structure chosen"), _tips);
            _delete = UiStyle.ToolButton(E(0x1F5D1), T("Tip_StructDelete", "Delete the structure chosen"), _tips);
            Bar(left, 14, 10, _new, _copy, _delete);
            _types.SetBounds(14, 50, 210, left.Height - 50);
            _types.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            _types.IntegralHeight = false;
            left.Controls.Add(_types);

            // ---- the structure: a line each, the label left of its box ----
            var right = new Panel { Bounds = new Rectangle(left.Right, 0, ClientSize.Width - left.Right, ClientSize.Height - bottom),
                                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(right);
            int x = 6, w = right.Width - x - 14;
            var nameCaption = Caption(right, T("Lbl_StructName", "Name"));
            var sizeCaption = Caption(right, T("Lbl_StructSize", "Length (devices)"));
            var commentCaption = Caption(right, T("Lbl_StructComment", "Comment"));
            int boxX = x + new[] { nameCaption, sizeCaption, commentCaption }.Max(l => l.PreferredWidth) + 10;
            Line(right, nameCaption, _name, x, boxX, 12, 260);
            Line(right, sizeCaption, _size, x, boxX, 46, 90);
            _size.Minimum = 1; _size.Maximum = 4096;
            var sizeNote = new Label { Text = T("Lbl_StructSizeNote", "an array's next element starts this far on"), AutoSize = true,
                                       ForeColor = Color.FromArgb(90, 96, 106) };
            right.Controls.Add(sizeNote);
            sizeNote.Location = new Point(_size.Right + 10, sizeCaption.Top);
            Line(right, commentCaption, _comment, x, boxX, 80, right.Width - boxX - 14);
            _comment.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            // ---- its members (labels inside the label): their buttons above them ----
            _addMember = UiStyle.ToolButton(E(0x2795), T("Tip_StructAddMember", "Add a label after the last one"), _tips);
            _removeMember = UiStyle.ToolButton(E(0x1F5D1), T("Tip_StructRemoveMember", "Remove the label chosen"), _tips);
            _up = UiStyle.IconButton(ToolGlyphs.MoveUp, "Move up", T("Tip_StructUp", "Move the label chosen up"), _tips);
            _down = UiStyle.IconButton(ToolGlyphs.MoveDown, "Move down", T("Tip_StructDown", "Move the label chosen down"), _tips);
            Kaban.UI.Helpers.Shortcuts.Set(_up, Keys.Control | Keys.Up, _tips, T("Tip_StructUp", "Move the label chosen up"));
            Kaban.UI.Helpers.Shortcuts.Set(_down, Keys.Control | Keys.Down, _tips, T("Tip_StructDown", "Move the label chosen down"));
            // 19 October: "leave some space between the comment line and the table's buttons"
            Bar(right, x, 136, _addMember, _removeMember, _up, _down);

            _members.SetBounds(x, 176, w, right.Height - 176 - 30);
            _members.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _members.AllowUserToAddRows = false;
            _members.AllowUserToDeleteRows = false;
            _members.AllowUserToResizeRows = false;
            _members.RowHeadersVisible = false;
            _members.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _members.MultiSelect = false;
            // a tap (or a key) edits a cell; moving onto one does not - a member just added is not
            // put in an editor that a second "Add" would commit empty
            _members.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            _members.BackgroundColor = Color.White;
            _members.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _members.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = T("Col_Label", "Label"), FillWeight = 26 });
            var type = new DataGridViewComboBoxColumn { Name = "DataType", HeaderText = T("Col_DataType", "DataType"), FillWeight = 26, FlatStyle = FlatStyle.Flat };
            type.Items.AddRange(MemberTypes);
            _members.Columns.Add(type);
            _members.Columns.Add(new DataGridViewTextBoxColumn { Name = "Offset", HeaderText = T("Col_StructOffset", "Offset (words)"), FillWeight = 16 });
            _members.Columns.Add(new DataGridViewTextBoxColumn { Name = "Bit", HeaderText = T("Col_StructBit", "Bit"), FillWeight = 9, ToolTipText = T("Tip_StructBit", "A Bit member: its bit of the word, 0 - F") });
            _members.Columns.Add(new DataGridViewTextBoxColumn { Name = "Comment", HeaderText = T("Col_Comment", "Comment"), FillWeight = 30 });
            foreach (DataGridViewColumn c in _members.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            try { UiStyle.GridLook(_members); } catch { }
            right.Controls.Add(_members);

            // what is wrong with the structure, under its labels
            _problem.AutoSize = false;
            _problem.SetBounds(x, right.Height - 26, w, 22);
            _problem.ForeColor = Color.FromArgb(190, 30, 30);
            _problem.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            right.Controls.Add(_problem);

            var ok = Btn(T("Btn_Ok", "OK"), ClientSize.Width - 212, ClientSize.Height - 44, 96);
            var cancel = Btn(T("Btn_Cancel", "Cancel"), ClientSize.Width - 108, ClientSize.Height - 44, 96);
            ok.DialogResult = DialogResult.OK;
            cancel.DialogResult = DialogResult.Cancel;
            ok.Anchor = cancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            AcceptButton = null;          // Enter edits the table's cells
            CancelButton = cancel;

            // ---- what each does ----
            _types.SelectedIndexChanged += (s, e) => ShowCurrent();
            _new.Click += (s, e) => NewType(null);
            _copy.Click += (s, e) => { if (Current != null) NewType(Current); };
            _delete.Click += (s, e) => DeleteType();
            _name.TextChanged += (s, e) => { if (_filling || Current == null) return; Current.Name = _name.Text.Trim(); RefreshList(); UpdateState(); };
            _size.ValueChanged += (s, e) => { if (_filling || Current == null) return; Current.Size = (int)_size.Value; UpdateState(); };
            _comment.TextChanged += (s, e) => { if (_filling || Current == null) return; Current.Comment = _comment.Text.Trim(); };
            _addMember.Click += (s, e) => AddMember();
            _removeMember.Click += (s, e) => RemoveMember();
            _up.Click += (s, e) => MoveMember(-1);
            _down.Click += (s, e) => MoveMember(+1);
            _members.CellValueChanged += (s, e) => { if (!_filling) TakeMember(e.RowIndex); };
            _members.CurrentCellDirtyStateChanged += (s, e) =>
            {
                // a data type chosen in the list is taken at once (not when the cell is left)
                if (_members.IsCurrentCellDirty && _members.CurrentCell is DataGridViewComboBoxCell) _members.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _members.DataError += (s, e) => { e.ThrowException = false; };
            // the row chosen moves Up / Down: both events, as either may come first (Windows changes
            // the selection before the current cell)
            _members.SelectionChanged += (s, e) => QueueButtons();
            _members.CurrentCellChanged += (s, e) => QueueButtons();
            _members.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0 || _members.Rows[e.RowIndex].Cells[e.ColumnIndex].ReadOnly) return;
                _members.CurrentCell = _members.Rows[e.RowIndex].Cells[e.ColumnIndex];
                _members.BeginEdit(true);
            };
            FormClosing += (s, e) =>
            {
                if (DialogResult != DialogResult.OK) return;
                if (_members.IsCurrentCellInEditMode) _members.EndEdit();
                var bad = FirstProblem();
                if (bad != null)
                {
                    e.Cancel = true;
                    MessageBox.Show(this, bad, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                StructuredTypes.Save(_work, _station);
            };

            RefreshList();
            int at = select == null ? -1 : _work.FindIndex(t => string.Equals(t.Name, StructuredTypes.BaseName(select), StringComparison.OrdinalIgnoreCase));
            if (_work.Count > 0) _types.SelectedIndex = Math.Max(0, at);
            ShowCurrent();
        }

        private static string T(string key, string fallback) { return LanguageManager.GetString(key, fallback); }

        private static string E(int codePoint) { return char.ConvertFromUtf32(codePoint); }

        private Button Btn(string text, int x, int y, int w)
        {
            var b = UiStyle.Round(new Button { Text = text, Width = w, Height = 30, Location = new Point(x, y) });
            Controls.Add(b);
            return b;
        }

        /// <summary>Toolbar buttons side by side from (x, y).</summary>
        private static void Bar(Control into, int x, int y, params Button[] buttons)
        {
            foreach (var b in buttons)
            {
                b.Location = new Point(x, y);
                b.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                into.Controls.Add(b);
                x += b.Width + 4;
            }
        }

        private static Label Caption(Control into, string text)
        {
            var l = new Label { Text = text + ":", AutoSize = true };
            into.Controls.Add(l);
            return l;
        }

        /// <summary>A property's line: its label on the left, its box on the right of it.</summary>
        private static void Line(Control into, Label caption, Control field, int x, int boxX, int y, int w)
        {
            field.SetBounds(boxX, y, w, 24);
            into.Controls.Add(field);
            caption.Location = new Point(x, y + Math.Max(0, (field.Height - caption.PreferredHeight) / 2));
        }

        // ---- the list --------------------------------------------------------------------

        private void RefreshList()
        {
            bool was = _filling;
            _filling = true;
            int sel = _types.SelectedIndex;
            _types.BeginUpdate();
            while (_types.Items.Count > _work.Count) _types.Items.RemoveAt(_types.Items.Count - 1);
            for (int i = 0; i < _work.Count; i++)
            {
                var caption = _work[i].Name.Length > 0 ? _work[i].Name : T("Lbl_StructUnnamed", "(no name)");
                if (i < _types.Items.Count) { if (!Equals(_types.Items[i], caption)) _types.Items[i] = caption; }
                else _types.Items.Add(caption);
            }
            _types.EndUpdate();
            if (sel >= 0 && sel < _types.Items.Count && _types.SelectedIndex != sel) _types.SelectedIndex = sel;
            _filling = was;
        }

        private void NewType(StructuredTypes.Type from)
        {
            var t = from != null ? from.Clone() : new StructuredTypes.Type { Size = 1 };
            string stem = from != null ? from.Name + "_Copy" : "Structure";
            string name = stem;
            for (int n = 2; _work.Any(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)); n++) name = stem + n;
            t.Name = name;
            _work.Add(t);
            RefreshList();
            _types.SelectedIndex = _work.Count - 1;
            ShowCurrent();
            _name.Focus();
            _name.SelectAll();
        }

        private void DeleteType()
        {
            var t = Current;
            if (t == null) return;
            if (MessageBox.Show(this, string.Format(T("Msg_StructDelete", "Delete the structure '{0}'? A monitor row of this type shows its first word again."), t.Name),
                                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            int at = _types.SelectedIndex;
            _work.RemoveAt(at);
            RefreshList();
            if (_work.Count > 0) _types.SelectedIndex = Math.Min(at, _work.Count - 1);
            ShowCurrent();
        }

        // ---- the structure shown ---------------------------------------------------------

        private void ShowCurrent()
        {
            if (_filling) return;
            _filling = true;
            var t = Current;
            _name.Text = t != null ? t.Name : "";
            _size.Value = t != null ? Math.Max(_size.Minimum, Math.Min(_size.Maximum, t.Size)) : 1;
            _comment.Text = t != null ? t.Comment : "";
            _members.Rows.Clear();
            if (t != null)
                foreach (var m in t.Members) AddMemberRow(m);
            _filling = false;
            UpdateState();
        }

        private void AddMemberRow(StructuredTypes.Member m)
        {
            var typeColumn = (DataGridViewComboBoxColumn)_members.Columns["DataType"];
            if (!typeColumn.Items.Contains(m.DataType)) typeColumn.Items.Add(m.DataType);     // a length the list does not offer
            int i = _members.Rows.Add(m.Name, m.DataType, m.Offset.ToString(CultureInfo.InvariantCulture),
                                      m.IsBit ? m.Bit.ToString("X", CultureInfo.InvariantCulture) : "", m.Comment);
            _members.Rows[i].Cells["Bit"].ReadOnly = !m.IsBit;
        }

        /// <summary>A member's row as typed, into the structure.</summary>
        private void TakeMember(int row)
        {
            var t = Current;
            if (t == null || row < 0 || row >= _members.Rows.Count || row >= t.Members.Count) return;
            var m = t.Members[row];
            var cells = _members.Rows[row].Cells;
            m.Name = Convert.ToString(cells["Name"].Value ?? "").Trim();
            m.DataType = Convert.ToString(cells["DataType"].Value ?? "").Trim();
            int n;
            if (int.TryParse(Convert.ToString(cells["Offset"].Value ?? ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) m.Offset = Math.Max(0, n);
            var bit = Convert.ToString(cells["Bit"].Value ?? "").Trim();
            if (bit.Length > 0 && int.TryParse(bit, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n)) m.Bit = Math.Max(0, Math.Min(15, n));
            m.Comment = Convert.ToString(cells["Comment"].Value ?? "").Trim();
            bool was = _filling;
            _filling = true;
            cells["Bit"].ReadOnly = !m.IsBit;
            if (!m.IsBit && Convert.ToString(cells["Bit"].Value ?? "") != "") cells["Bit"].Value = "";
            if (m.IsBit && Convert.ToString(cells["Bit"].Value ?? "") == "") cells["Bit"].Value = m.Bit.ToString("X", CultureInfo.InvariantCulture);
            _filling = was;
            // a label past the length does not lengthen it: the problem line says it, OK refuses it
            UpdateState(fromTable: true);
        }

        internal void AddMember()
        {
            var t = Current;
            if (t == null) return;
            if (_members.IsCurrentCellInEditMode) _members.EndEdit();
            string type = "Word [Signed]";
            int offset, bit;
            t.NextPlace(type, out offset, out bit);
            string name = "Member" + (t.Members.Count + 1);
            for (int n = t.Members.Count + 2; t.Members.Any(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase)); n++) name = "Member" + n;
            var m = new StructuredTypes.Member { Name = name, DataType = type, Offset = offset, Bit = bit };
            t.Members.Add(m);
            // past the length: the length stays (19 October: "it should give an error instead, just like
            // lowering the length below the labels") - the problem line says it, OK refuses it
            _filling = true;
            AddMemberRow(m);
            _filling = false;
            UpdateState();
            Choose(_members.Rows.Count - 1);
        }

        /// <summary>
        /// The member chosen: the selected row (the current cell follows it a moment later on
        /// Windows - Up and Down took the row chosen before), else the current cell's.
        /// </summary>
        internal int SelectedMember
        {
            get
            {
                if (_members.SelectedRows.Count > 0) return _members.SelectedRows[0].Index;
                return _members.CurrentCell != null ? _members.CurrentCell.RowIndex : -1;
            }
        }

        /// <summary>Makes a member's row the chosen one (selected and current).</summary>
        private void Choose(int row)
        {
            if (row < 0 || row >= _members.Rows.Count) { UpdateButtons(); return; }
            _members.CurrentCell = _members.Rows[row].Cells["Name"];
            _members.ClearSelection();
            _members.Rows[row].Selected = true;
            UpdateButtons();
        }

        // Removed and moved in place - the rows are not made again: making them again moved the
        // table's current cell, its events disabled the button pressed, and Windows refused to hand
        // the focus on in the middle of it ("a reentrant call to SetCurrentCellAddressCore").

        internal void RemoveMember()
        {
            var t = Current;
            int i = SelectedMember;
            if (t == null || i < 0 || i >= t.Members.Count || i >= _members.Rows.Count) return;
            if (_members.IsCurrentCellInEditMode) _members.EndEdit();
            t.Members.RemoveAt(i);
            _filling = true;
            _members.Rows.RemoveAt(i);
            _filling = false;
            UpdateState();
            Choose(Math.Min(i, _members.Rows.Count - 1));
        }

        internal void MoveMember(int by)
        {
            var t = Current;
            int i = SelectedMember;
            int j = i + by;
            if (t == null || i < 0 || i >= t.Members.Count || j < 0 || j >= t.Members.Count || j >= _members.Rows.Count) return;
            if (_members.IsCurrentCellInEditMode) _members.EndEdit();
            var m = t.Members[i];
            t.Members[i] = t.Members[j];
            t.Members[j] = m;
            _filling = true;
            FillRow(i, t.Members[i]);
            FillRow(j, t.Members[j]);
            _filling = false;
            UpdateState();
            Choose(j);
        }

        /// <summary>A member's row shown anew (after a move).</summary>
        private void FillRow(int row, StructuredTypes.Member m)
        {
            var typeColumn = (DataGridViewComboBoxColumn)_members.Columns["DataType"];
            if (!typeColumn.Items.Contains(m.DataType)) typeColumn.Items.Add(m.DataType);
            var cells = _members.Rows[row].Cells;
            cells["Name"].Value = m.Name;
            cells["DataType"].Value = m.DataType;
            cells["Offset"].Value = m.Offset.ToString(CultureInfo.InvariantCulture);
            cells["Bit"].Value = m.IsBit ? m.Bit.ToString("X", CultureInfo.InvariantCulture) : "";
            cells["Bit"].ReadOnly = !m.IsBit;
            cells["Comment"].Value = m.Comment;
        }

        // ---- what it says -----------------------------------------------------------------

        /// <param name="fromTable">Called from the table's own event: the buttons after it (see QueueButtons).</param>
        private void UpdateState(bool fromTable = false)
        {
            var t = Current;
            _name.Enabled = _size.Enabled = _comment.Enabled = _members.Enabled = t != null;
            var p = t == null ? null : StructuredTypes.Problem(t, _work);
            _problem.Text = p ?? "";
            if (fromTable) QueueButtons(); else UpdateButtons();
        }

        private bool _buttonsQueued;

        /// <summary>
        /// The buttons, from the table's own events: right after them. A button disabled while it
        /// has the focus hands the focus on - to the table, which then moves its current cell; in
        /// the middle of the table's own change of it Windows refuses that ("Operation is not valid
        /// because it results in a reentrant call to the SetCurrentCellAddressCore function").
        /// </summary>
        private void QueueButtons()
        {
            if (!IsHandleCreated || IsDisposed) return;      // not shown yet: ShowCurrent sets them
            if (_buttonsQueued) return;
            _buttonsQueued = true;
            BeginInvoke((Action)(() => { _buttonsQueued = false; if (!IsDisposed) UpdateButtons(); }));
        }

        private void UpdateButtons()
        {
            var t = Current;
            int i = SelectedMember;
            bool chosen = t != null && i >= 0 && i < _members.Rows.Count;
            Enable(_copy, t != null);
            Enable(_delete, t != null);
            Enable(_addMember, t != null);
            Enable(_removeMember, chosen);
            Enable(_up, chosen && i > 0);
            Enable(_down, chosen && i < _members.Rows.Count - 1);
        }

        /// <summary>A button turned on or off; turned off with the focus, it hands the focus to the labels' table.</summary>
        private void Enable(Button b, bool on)
        {
            if (b.Enabled == on) return;
            if (!on && b.Focused && _members.CanFocus) _members.Focus();
            b.Enabled = on;
        }

        /// <summary>The first structure that is not complete, and why (null: all are).</summary>
        internal string FirstProblem()
        {
            foreach (var t in _work)
            {
                var p = StructuredTypes.Problem(t, _work);
                if (p != null) return p;
            }
            return null;
        }
    }
}
