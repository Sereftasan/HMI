using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;
using Kaban.UI.Controls;

namespace Kaban.UI.Profiles
{
    /// <summary>
    /// One table of the profile library as a grid: rows added, changed and
    /// removed in place, saved together (all or nothing). The library's catalogue
    /// tables - vendors, series, groups, accessories, their operations, macros,
    /// the machine's tools and feeds - are all edited through this.
    ///
    /// A column is text, a number, a tick, one of a fixed list, or a row of
    /// another table (Lookup). A filter column (the accessory an operation belongs
    /// to, the machine a tool is on) is not shown and is filled in on new rows.
    /// </summary>
    public class TableEditor : UserControl
    {
        public enum Kind { Text, Real, Int, Bool, Choice, Lookup }

        public sealed class Column
        {
            public string Name = "", Caption = "", Tip = "";
            public Kind Kind = Kind.Text;
            /// <summary>Lookup: "SELECT value, text FROM ..." (value first). Choice: "value|text" items.</summary>
            public string LookupSql;
            public string[] Choices;
            public bool Required, ReadOnly, Nullable = true;
            public int Width = 110;
            public object Default;
            /// <summary>The value a new row starts with, worked out when it is added (the next seq).</summary>
            public Func<object> NewValue;
        }

        public string Table = "";
        public string Key = "id";
        public string OrderBy = "";
        public string FilterColumn;
        public object FilterValue;
        public readonly List<Column> Columns = new List<Column>();
        /// <summary>Values every new row gets (created_at, ...): column -> producer.</summary>
        public readonly Dictionary<string, Func<object>> InsertValues = new Dictionary<string, Func<object>>();

        /// <summary>Asked before rows are removed (how many); a test answers without a dialog.</summary>
        public Func<int, bool> ConfirmDelete;

        public event EventHandler Saved;
        public event EventHandler RowChanged;

        private readonly DataGridView _grid = new DataGridView();
        private readonly FlowLayoutPanel _bar = new FlowLayoutPanel();
        private readonly Label _status = new Label();
        private readonly ToolTip _tips = UiStyle.Tips();
        private readonly List<long> _deleted = new List<long>();
        private bool _canEdit = true, _loading;
        private Button _add, _del, _save, _reload;

        private sealed class RowState
        {
            public long? Id;
            public Dictionary<string, object> Original;
        }

        public TableEditor()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            _bar.Dock = DockStyle.Top;
            _bar.Height = 40;
            _bar.Padding = new Padding(4, 3, 4, 3);
            _add = UiStyle.ToolButton("\u2795", T("Tbl_Add", "Add a row"), _tips, (s, e) => AddRow());
            _del = UiStyle.ToolButton("\uD83D\uDDD1", T("Tbl_Delete", "Remove the selected rows (saved with the rest)"), _tips, (s, e) => DeleteRows());
            _save = UiStyle.ToolButton("\uD83D\uDCBE", T("Tbl_Save", "Save the changes"), _tips, (s, e) => { string err; Save(out err); });
            _reload = UiStyle.ToolButton("\u21BA", T("Tbl_Reload", "Throw the changes away and read the table again"), _tips, (s, e) => Reload());
            _bar.Controls.AddRange(new Control[] { _add, _del, _save, _reload });
            _status.AutoSize = true;
            _status.Margin = new Padding(10, 9, 3, 3);
            _status.Font = UiStyle.Small;
            _status.ForeColor = Color.FromArgb(90, 96, 106);
            _bar.Controls.Add(_status);

            _grid.Dock = DockStyle.Fill;
            UiStyle.StyleGrid(_grid);
            _grid.AllowUserToAddRows = false;
            _grid.MultiSelect = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            // Editing starts with the user - a key, F2, a double-click, a click on a
            // list or a tick (below). EditOnEnter opened an editor on the first row
            // every time the table was filled, before its values were in, and that
            // empty editor was then written back into the row.
            _grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            _grid.CellClick += (s, e) =>
            {
                if (!_canEdit || _loading || e.RowIndex < 0 || e.ColumnIndex < 0) return;
                var col = _grid.Columns[e.ColumnIndex];
                var spec = col.Tag as Column;
                if (spec == null || spec.ReadOnly) return;
                if (col is DataGridViewCheckBoxColumn)
                {
                    // a tick is switched here (the column itself is read only, so the grid never switches it too)
                    var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                    cell.Value = !(cell.Value is bool && (bool)cell.Value);
                }
                else if (col is DataGridViewComboBoxColumn && _grid.BeginEdit(true))
                {
                    var box = _grid.EditingControl as ComboBox;
                    if (box != null) box.DroppedDown = true;
                }
            };
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.DataError += (s, e) => { e.ThrowException = false; };
            _grid.CellValueChanged += (s, e) => { if (!_loading) { MarkDirty(); } };
            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.SelectionChanged += (s, e) => { if (!_loading && RowChanged != null) RowChanged(this, EventArgs.Empty); };
            Controls.Add(_grid);
            Controls.Add(_bar);
        }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        /// <summary>Editing allowed (the page's security level): off, the grid is read only.</summary>
        public bool CanEdit
        {
            get { return _canEdit; }
            set
            {
                _canEdit = value;
                _grid.ReadOnly = !value;
                _add.Enabled = _del.Enabled = _save.Enabled = value;
                foreach (DataGridViewColumn c in _grid.Columns)
                {
                    var spec = c.Tag as Column;
                    c.ReadOnly = !value || (spec != null && (spec.ReadOnly || spec.Kind == Kind.Bool));
                }
            }
        }

        public DataGridView Grid { get { return _grid; } }
        public bool Dirty { get; private set; }

        private void MarkDirty()
        {
            Dirty = true;
            _status.Text = T("Tbl_Unsaved", "changed - not saved yet");
            _status.ForeColor = Color.FromArgb(170, 90, 20);
        }

        /// <summary>The id of the selected row (null: none, or a new row not saved yet).</summary>
        public long? SelectedId
        {
            get
            {
                var r = _grid.CurrentRow ?? (_grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0] : null);
                var st = r != null ? r.Tag as RowState : null;
                return st != null ? st.Id : null;
            }
        }

        public object ValueOf(long id, string column)
        {
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var st = r.Tag as RowState;
                if (st != null && st.Id == id && _grid.Columns.Contains(column)) return Parse(r.Cells[column].OwningColumn.Tag as Column, r.Cells[column].Value);
            }
            return null;
        }

        /// <summary>Builds the grid's columns from Columns; call once after setting them up.</summary>
        public void Build()
        {
            _grid.Columns.Clear();
            foreach (var c in Columns)
            {
                DataGridViewColumn col;
                if (c.Kind == Kind.Bool) col = new DataGridViewCheckBoxColumn();
                else if (c.Kind == Kind.Choice || c.Kind == Kind.Lookup)
                    col = new DataGridViewComboBoxColumn { DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton, FlatStyle = FlatStyle.Flat };
                else col = new DataGridViewTextBoxColumn();
                col.Name = c.Name;
                col.HeaderText = c.Caption;
                col.Width = c.Width;
                col.ToolTipText = c.Tip;
                col.Tag = c;
                col.SortMode = DataGridViewColumnSortMode.Automatic;
                if (c.Kind == Kind.Real || c.Kind == Kind.Int) col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                col.ReadOnly = c.ReadOnly || !_canEdit || c.Kind == Kind.Bool;
                _grid.Columns.Add(col);
            }
            FillChoices();
        }

        private sealed class Item
        {
            public object Value;
            public string Text = "";
            public override string ToString() { return Text; }
        }

        /// <summary>Reads the lookups again (a vendor added on another tab shows up in the series' list).</summary>
        public void FillChoices()
        {
            foreach (DataGridViewColumn col in _grid.Columns)
            {
                var spec = col.Tag as Column;
                var combo = col as DataGridViewComboBoxColumn;
                if (spec == null || combo == null) continue;
                var items = new List<Item>();
                if (spec.Nullable && !spec.Required) items.Add(new Item { Value = null, Text = "" });
                try
                {
                    if (spec.Kind == Kind.Lookup && !string.IsNullOrEmpty(spec.LookupSql))
                        foreach (var r in Library.Rows(spec.LookupSql))
                        {
                            var vals = r.Values.ToList();
                            items.Add(new Item { Value = Norm(vals[0]), Text = vals.Count > 1 ? Convert.ToString(vals[1], CultureInfo.InvariantCulture) : Convert.ToString(vals[0], CultureInfo.InvariantCulture) });
                        }
                    else if (spec.Choices != null)
                        foreach (var ch in spec.Choices)
                        {
                            var parts = ch.Split(new[] { '|' }, 2);
                            items.Add(new Item { Value = spec.Kind == Kind.Choice && IsNumber(parts[0]) ? (object)long.Parse(parts[0], CultureInfo.InvariantCulture) : parts[0], Text = parts.Length > 1 ? parts[1] : parts[0] });
                        }
                }
                catch (Exception ex) { _status.Text = ex.Message; }
                combo.Items.Clear();
                foreach (var it in items) combo.Items.Add(it.Text);
                combo.Tag = spec;
                _choices[spec.Name] = items;
            }
        }

        private readonly Dictionary<string, List<Item>> _choices = new Dictionary<string, List<Item>>();

        private static bool IsNumber(string s) { long v; return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v); }

        private static object Norm(object v)
        {
            if (v == null) return null;
            if (v is int || v is long || v is short) return Convert.ToInt64(v);
            return v;
        }

        private string TextOfChoice(Column spec, object value)
        {
            List<Item> items;
            if (!_choices.TryGetValue(spec.Name, out items)) return Convert.ToString(value, CultureInfo.InvariantCulture);
            var v = Norm(value);
            var hit = items.FirstOrDefault(i => (i.Value == null && v == null) || (i.Value != null && v != null &&
                                                string.Equals(Convert.ToString(i.Value, CultureInfo.InvariantCulture), Convert.ToString(v, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)));
            if (hit != null) return hit.Text;
            // a value not in the list (an old code) is shown as it is, and kept
            if (v != null)
            {
                var extra = new Item { Value = v, Text = Convert.ToString(v, CultureInfo.InvariantCulture) };
                items.Add(extra);
                ((DataGridViewComboBoxColumn)_grid.Columns[spec.Name]).Items.Add(extra.Text);
                return extra.Text;
            }
            return "";
        }

        private object ValueOfChoice(Column spec, string text)
        {
            List<Item> items;
            if (!_choices.TryGetValue(spec.Name, out items)) return text;
            var hit = items.FirstOrDefault(i => i.Text == (text ?? ""));
            return hit != null ? hit.Value : null;
        }

        public void Reload()
        {
            _loading = true;
            try
            {
                // Out of any cell first: an editor left open would write its text
                // into whichever row is added in its place.
                _grid.CancelEdit();
                try { _grid.CurrentCell = null; } catch (InvalidOperationException) { }
                _grid.Rows.Clear();
                _deleted.Clear();
                FillChoices();
                if (FilterColumn != null && FilterValue == null) { _status.Text = ""; return; }
                string sql = "SELECT " + Key + ", " + string.Join(", ", Columns.Select(c => c.Name)) + " FROM " + Table +
                             (FilterColumn != null ? " WHERE " + FilterColumn + " = @p0" : "") +
                             (string.IsNullOrEmpty(OrderBy) ? "" : " ORDER BY " + OrderBy);
                var rows = FilterColumn != null ? Library.Rows(sql, FilterValue) : Library.Rows(sql);
                foreach (var r in rows)
                {
                    var st = new RowState { Id = Library.Id(r, Key), Original = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) };
                    var cells = new object[Columns.Count];
                    for (int k = 0; k < Columns.Count; k++)
                    {
                        var c = Columns[k];
                        object v; r.TryGetValue(c.Name, out v);
                        st.Original[c.Name] = Norm(v is float ? (object)Db.Real(v) : v);
                        cells[k] = Display(c, v);
                    }
                    // the row with its values in one step (a cell set afterwards can be under an open editor)
                    int i = _grid.Rows.Add(cells);
                    _grid.Rows[i].Tag = st;
                }
                _grid.ClearSelection();
                Dirty = false;
                _status.Text = rows.Count + " " + T("Tbl_Rows", "rows");
                _status.ForeColor = Color.FromArgb(90, 96, 106);
            }
            catch (Exception ex)
            {
                _status.Text = ex.Message;
                _status.ForeColor = Color.FromArgb(170, 30, 30);
            }
            finally { _loading = false; }
            if (RowChanged != null) RowChanged(this, EventArgs.Empty);
        }

        private object Display(Column c, object v)
        {
            if (c.Kind == Kind.Bool) return v != null && Convert.ToInt64(v, CultureInfo.InvariantCulture) != 0;
            if (c.Kind == Kind.Choice || c.Kind == Kind.Lookup) return TextOfChoice(c, v);
            if (v == null) return "";
            if (c.Kind == Kind.Real) return Db.Real(v).ToString("0.####", CultureInfo.InvariantCulture);
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        /// <summary>A cell's value as the database stores it (throws FormatException with the column's name).</summary>
        private object Parse(Column c, object cell)
        {
            if (c == null) return cell;
            switch (c.Kind)
            {
                case Kind.Bool: return cell is bool && (bool)cell ? 1L : 0L;
                case Kind.Choice:
                case Kind.Lookup: return ValueOfChoice(c, Convert.ToString(cell));
                case Kind.Real:
                {
                    var s = Convert.ToString(cell, CultureInfo.InvariantCulture).Trim().Replace(',', '.');
                    if (s.Length == 0) return null;
                    double d;
                    if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) throw new FormatException(c.Caption + ": \"" + s + "\" " + T("Tbl_NotNumber", "is not a number"));
                    return d;
                }
                case Kind.Int:
                {
                    var s = Convert.ToString(cell, CultureInfo.InvariantCulture).Trim();
                    if (s.Length == 0) return null;
                    long l;
                    if (!long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out l)) throw new FormatException(c.Caption + ": \"" + s + "\" " + T("Tbl_NotWhole", "is not a whole number"));
                    return l;
                }
                default:
                {
                    var s = Convert.ToString(cell, CultureInfo.InvariantCulture).Trim();
                    return s.Length == 0 ? null : s;
                }
            }
        }

        public void AddRow()
        {
            if (!_canEdit) return;
            if (FilterColumn != null && FilterValue == null) return;
            _loading = true;
            _grid.CancelEdit();
            int i = _grid.Rows.Add(Columns.Select(c => Display(c, c.NewValue != null ? c.NewValue() : c.Default)).ToArray());
            var gr = _grid.Rows[i];
            gr.Tag = new RowState();
            _loading = false;
            MarkDirty();
            _grid.CurrentCell = gr.Cells[Columns.First(c => !c.ReadOnly).Name];
            _grid.BeginEdit(true);
        }

        public void DeleteRows()
        {
            if (!_canEdit) return;
            var rows = _grid.SelectedRows.Cast<DataGridViewRow>().ToList();
            if (rows.Count == 0 && _grid.CurrentRow != null) rows.Add(_grid.CurrentRow);
            // out of any cell first: an edit left open would be written into the row that takes its place
            _grid.CancelEdit();
            try { _grid.CurrentCell = null; } catch (InvalidOperationException) { }
            foreach (var r in rows)
            {
                var st = r.Tag as RowState;
                if (st != null && st.Id.HasValue) _deleted.Add(st.Id.Value);
                _grid.Rows.Remove(r);
            }
            if (rows.Count > 0) MarkDirty();
        }

        /// <summary>Writes every change in one transaction; on an error nothing is written and it is shown.</summary>
        public bool Save(out string error)
        {
            error = "";
            if (!_canEdit) return false;
            _grid.EndEdit();
            if (_deleted.Count > 0)
            {
                var ask = ConfirmDelete ?? (n => MessageBox.Show(FindForm(),
                    string.Format(T("Tbl_ConfirmDelete", "{0} row(s) will be removed, with what belongs only to them. Save?"), n),
                    T("Tbl_Remove", "Remove"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
                if (!ask(_deleted.Count)) return false;
            }
            try
            {
                var inserts = new List<Dictionary<string, object>>();
                var updates = new List<KeyValuePair<long, Dictionary<string, object>>>();
                foreach (DataGridViewRow gr in _grid.Rows)
                {
                    var st = gr.Tag as RowState;
                    var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    foreach (var c in Columns)
                    {
                        var v = Parse(c, gr.Cells[c.Name].Value);
                        if (c.Required && (v == null || (v is string && ((string)v).Length == 0)))
                            throw new FormatException(c.Caption + ": " + T("Tbl_Required", "needs a value") + " (" + T("Tbl_Row", "row") + " " + (gr.Index + 1) + ")");
                        values[c.Name] = v;
                    }
                    if (st == null || !st.Id.HasValue) inserts.Add(values);
                    else
                    {
                        var changed = values.Where(kv => !Same(kv.Value, st.Original.ContainsKey(kv.Key) ? st.Original[kv.Key] : null))
                                            .ToDictionary(kv => kv.Key, kv => kv.Value);
                        if (changed.Count > 0) updates.Add(new KeyValuePair<long, Dictionary<string, object>>(st.Id.Value, changed));
                    }
                }
                Library.Transaction(c =>
                {
                    foreach (var id in _deleted) Db.Execute(c, "DELETE FROM " + Table + " WHERE " + Key + " = @p0", id);
                    foreach (var u in updates)
                    {
                        var cols = u.Value.Keys.ToList();
                        var args = new List<object> { u.Key };
                        args.AddRange(cols.Select(k => u.Value[k]));
                        Db.Execute(c, "UPDATE " + Table + " SET " + string.Join(", ", cols.Select((k, i) => k + " = @p" + (i + 1))) + " WHERE " + Key + " = @p0", args.ToArray());
                    }
                    foreach (var ins in inserts)
                    {
                        if (FilterColumn != null) ins[FilterColumn] = FilterValue;
                        foreach (var kv in InsertValues) ins[kv.Key] = kv.Value();
                        var cols = ins.Keys.ToList();
                        Db.Execute(c, "INSERT INTO " + Table + " (" + string.Join(", ", cols) + ") VALUES (" + string.Join(", ", cols.Select((k, i) => "@p" + i)) + ")",
                                   cols.Select(k => ins[k]).ToArray());
                    }
                });
                AuditLogger.Log("saved the profile library's " + Table + ": " + inserts.Count + " added, " + updates.Count + " changed, " + _deleted.Count + " removed.");
                Reload();
                _status.Text = T("Tbl_Saved", "Saved.");
                _status.ForeColor = Color.FromArgb(20, 120, 40);
                if (Saved != null) Saved(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message.Contains("FOREIGN KEY") ? T("Tbl_InUse", "Not saved: a row is still used elsewhere (or refers to a row that does not exist).") : ex.Message;
                _status.Text = error;
                _status.ForeColor = Color.FromArgb(170, 30, 30);
                return false;
            }
        }

        private static bool Same(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if ((a is double || a is long) && (b is double || b is long))
                return Math.Abs(Convert.ToDouble(a, CultureInfo.InvariantCulture) - Convert.ToDouble(b, CultureInfo.InvariantCulture)) < 1e-9;
            return string.Equals(Convert.ToString(a, CultureInfo.InvariantCulture), Convert.ToString(b, CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        // ---- building columns -------------------------------------------------

        public TableEditor Text(string name, string caption, int width = 120, bool required = false)
        { Columns.Add(new Column { Name = name, Caption = caption, Width = width, Required = required }); return this; }

        public TableEditor Real(string name, string caption, int width = 80, bool required = false, object def = null)
        { Columns.Add(new Column { Name = name, Caption = caption, Kind = Kind.Real, Width = width, Required = required, Default = def }); return this; }

        public TableEditor Int(string name, string caption, int width = 70, bool required = false, object def = null)
        { Columns.Add(new Column { Name = name, Caption = caption, Kind = Kind.Int, Width = width, Required = required, Default = def }); return this; }

        public TableEditor Bool(string name, string caption, int width = 60, bool def = true)
        { Columns.Add(new Column { Name = name, Caption = caption, Kind = Kind.Bool, Width = width, Default = def ? 1L : 0L }); return this; }

        public TableEditor Choice(string name, string caption, string[] choices, int width = 120, bool required = false, object def = null)
        { Columns.Add(new Column { Name = name, Caption = caption, Kind = Kind.Choice, Choices = choices, Width = width, Required = required, Nullable = !required, Default = def }); return this; }

        public TableEditor Lookup(string name, string caption, string sql, int width = 160, bool required = false)
        { Columns.Add(new Column { Name = name, Caption = caption, Kind = Kind.Lookup, LookupSql = sql, Width = width, Required = required, Nullable = !required }); return this; }

        public TableEditor Tip(string tip) { Columns[Columns.Count - 1].Tip = tip; return this; }

        public TableEditor Starts(Func<object> value) { Columns[Columns.Count - 1].NewValue = value; return this; }

        public TableEditor Fixed() { Columns[Columns.Count - 1].ReadOnly = true; return this; }

        /// <summary>The largest value of a numeric column on the grid (for "the next seq").</summary>
        public long MaxOf(string column)
        {
            long max = 0;
            foreach (DataGridViewRow r in _grid.Rows)
            {
                long v;
                if (long.TryParse(Convert.ToString(r.Cells[column].Value, CultureInfo.InvariantCulture), out v) && v > max) max = v;
            }
            return max;
        }
    }
}
