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
        private readonly Label _layout = new Label();
        private readonly Label _problem = new Label();
        private readonly Button _new, _copy, _delete, _addMember, _removeMember, _up, _down;
        private readonly List<StructuredTypes.Type> _work;
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
        public StructuredTypesForm(string select = null)
        {
            UiScale.ApplyOnLoad(this);
            Text = T("Ttl_StructTypes", "Structured data types");
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = UiStyle.Body;
            ClientSize = new Size(980, 560);
            MinimumSize = new Size(820, 460);

            _work = StructuredTypes.All.Select(t => t.Clone()).ToList();

            // ---- the list ----
            var listCaption = new Label { Text = T("Lbl_StructList", "Structures"), Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), AutoSize = true, Location = new Point(14, 12) };
            Controls.Add(listCaption);
            _types.SetBounds(14, 36, 210, ClientSize.Height - 36 - 96);
            _types.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            _types.IntegralHeight = false;
            Controls.Add(_types);
            _new = Btn(T("Btn_StructNew", "New"), 14, ClientSize.Height - 90, 66);
            _copy = Btn(T("Btn_StructCopy", "Copy"), 86, ClientSize.Height - 90, 66);
            _delete = Btn(T("Btn_StructDelete", "Delete"), 158, ClientSize.Height - 90, 66);
            foreach (var b in new[] { _new, _copy, _delete }) b.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            // ---- the structure ----
            int x = 244, w = ClientSize.Width - x - 14;
            AddField(T("Lbl_StructName", "Name"), _name, x, 12, 260);
            AddField(T("Lbl_StructSize", "Length (devices)"), _size, x + 290, 12, 90);
            _size.Minimum = 1; _size.Maximum = 4096;
            var sizeNote = new Label { Text = T("Lbl_StructSizeNote", "an array's next element starts this far on"), AutoSize = true,
                                       ForeColor = Color.FromArgb(90, 96, 106), Location = new Point(x + 290 + 96, 40) };
            Controls.Add(sizeNote);
            AddField(T("Lbl_StructComment", "Comment"), _comment, x, 64, w);
            _comment.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            // ---- its members ----
            var membersCaption = new Label { Text = T("Lbl_StructMembers", "Members"), Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), AutoSize = true, Location = new Point(x, 120) };
            Controls.Add(membersCaption);
            _members.SetBounds(x, 144, w, ClientSize.Height - 144 - 132);
            _members.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _members.AllowUserToAddRows = false;
            _members.AllowUserToDeleteRows = false;
            _members.AllowUserToResizeRows = false;
            _members.RowHeadersVisible = false;
            _members.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _members.MultiSelect = false;
            // a tap (or a key) edits a cell; moving onto one does not - a member just added is not
            // put in an editor that a second "Add member" would commit empty
            _members.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            _members.BackgroundColor = Color.White;
            _members.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _members.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = T("Col_StructMember", "Member"), FillWeight = 26 });
            var type = new DataGridViewComboBoxColumn { Name = "DataType", HeaderText = T("Col_DataType", "DataType"), FillWeight = 26, FlatStyle = FlatStyle.Flat };
            type.Items.AddRange(MemberTypes);
            _members.Columns.Add(type);
            _members.Columns.Add(new DataGridViewTextBoxColumn { Name = "Offset", HeaderText = T("Col_StructOffset", "Offset (words)"), FillWeight = 16 });
            _members.Columns.Add(new DataGridViewTextBoxColumn { Name = "Bit", HeaderText = T("Col_StructBit", "Bit"), FillWeight = 9, ToolTipText = T("Tip_StructBit", "A Bit member: its bit of the word, 0 - F") });
            _members.Columns.Add(new DataGridViewTextBoxColumn { Name = "Comment", HeaderText = T("Col_Comment", "Comment"), FillWeight = 30 });
            foreach (DataGridViewColumn c in _members.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            try { UiStyle.GridLook(_members); } catch { }
            Controls.Add(_members);

            int by = ClientSize.Height - 124;
            _addMember = Btn(T("Btn_StructAddMember", "Add member"), x, by, 120);
            _removeMember = Btn(T("Btn_StructRemoveMember", "Remove member"), x + 126, by, 130);
            _up = Btn(T("Btn_StructUp", "Up"), x + 262, by, 60);
            _down = Btn(T("Btn_StructDown", "Down"), x + 328, by, 60);
            foreach (var b in new[] { _addMember, _removeMember, _up, _down }) b.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

            _layout.AutoSize = false;
            _layout.SetBounds(x, by + 38, w, 22);
            _layout.ForeColor = Color.FromArgb(70, 76, 86);
            _layout.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_layout);
            _problem.AutoSize = false;
            _problem.SetBounds(x, by + 60, w, 22);
            _problem.ForeColor = Color.FromArgb(190, 30, 30);
            _problem.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(_problem);

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
            _members.SelectionChanged += (s, e) => UpdateButtons();
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
                StructuredTypes.Save(_work);
            };

            RefreshList();
            int at = select == null ? -1 : _work.FindIndex(t => string.Equals(t.Name, StructuredTypes.BaseName(select), StringComparison.OrdinalIgnoreCase));
            if (_work.Count > 0) _types.SelectedIndex = Math.Max(0, at);
            ShowCurrent();
        }

        private static string T(string key, string fallback) { return LanguageManager.GetString(key, fallback); }

        private Button Btn(string text, int x, int y, int w)
        {
            var b = UiStyle.Round(new Button { Text = text, Width = w, Height = 30, Location = new Point(x, y) });
            Controls.Add(b);
            return b;
        }

        private void AddField(string caption, Control field, int x, int y, int w)
        {
            Controls.Add(new Label { Text = caption + ":", AutoSize = true, Location = new Point(x, y) });
            field.SetBounds(x, y + 22, w, 24);
            Controls.Add(field);
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
            // the structure grows with its members
            if (t.Size < t.MinimumSize) { _filling = true; t.Size = t.MinimumSize; _size.Value = Math.Min(_size.Maximum, t.Size); _filling = was; }
            UpdateState();
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
            if (t.Size < t.MinimumSize) t.Size = t.MinimumSize;
            _filling = true;
            AddMemberRow(m);
            _size.Value = Math.Min(_size.Maximum, t.Size);
            _filling = false;
            _members.ClearSelection();
            _members.CurrentCell = _members.Rows[_members.Rows.Count - 1].Cells["Name"];
            UpdateState();
        }

        private void RemoveMember()
        {
            var t = Current;
            int i = _members.CurrentRow != null ? _members.CurrentRow.Index : -1;
            if (t == null || i < 0 || i >= t.Members.Count) return;
            t.Members.RemoveAt(i);
            ShowCurrent();
            if (_members.Rows.Count > 0) _members.CurrentCell = _members.Rows[Math.Min(i, _members.Rows.Count - 1)].Cells["Name"];
        }

        private void MoveMember(int by)
        {
            var t = Current;
            int i = _members.CurrentRow != null ? _members.CurrentRow.Index : -1;
            int j = i + by;
            if (t == null || i < 0 || j < 0 || j >= t.Members.Count) return;
            var m = t.Members[i];
            t.Members.RemoveAt(i);
            t.Members.Insert(j, m);
            ShowCurrent();
            _members.CurrentCell = _members.Rows[j].Cells["Name"];
        }

        // ---- what it says -----------------------------------------------------------------

        private void UpdateState()
        {
            var t = Current;
            _name.Enabled = _size.Enabled = _comment.Enabled = _members.Enabled = t != null;
            _layout.Text = t == null ? T("Lbl_StructNone", "No structure yet: New makes one.")
                : string.Format(T("Lbl_StructLayout", "{0} members, {1} of {2} devices used - in an array starting at R0 the elements are R0, R{2}, R{3} ..."),
                                t.Members.Count, t.Members.Count == 0 ? 0 : t.MinimumSize, t.Size, 2 * t.Size);
            var p = t == null ? null : StructuredTypes.Problem(t, _work);
            _problem.Text = p ?? "";
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            var t = Current;
            int i = _members.CurrentRow != null ? _members.CurrentRow.Index : -1;
            _copy.Enabled = _delete.Enabled = _addMember.Enabled = t != null;
            _removeMember.Enabled = t != null && i >= 0;
            _up.Enabled = t != null && i > 0;
            _down.Enabled = t != null && i >= 0 && i < _members.Rows.Count - 1;
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
