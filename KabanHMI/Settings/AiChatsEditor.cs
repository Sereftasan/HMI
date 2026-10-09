using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// THE PANEL'S AI CHATS, at the top of HMI Settings > AI Prompt (an Admin's
    /// page; 9 October: "move this part on top of the AI Prompt page as a table,
    /// formatted the same as the other tables - the buttons' style as well - and
    /// add Add and Remove"): a name and a web address a row, in the order "Ask
    /// &lt;AI&gt;" offers them. The bar as every settings page's: keep, throw the changes
    /// away, add, remove, up, down - and the usual list. Each user picks theirs in
    /// Users and Preferences (Help and AI: Preferred AI); none picked, the first.
    /// </summary>
    public class AiChatsEditor : UserControl
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _status = new Label();
        private readonly DirtyTracker _dirty = new DirtyTracker();
        private readonly Button _save, _revert, _add, _remove, _up, _down, _usual;
        private bool _filling;

        /// <summary>The table and its buttons (tests).</summary>
        public DataGridView Grid { get { return _grid; } }
        public Button SaveButton { get { return _save; } }
        public Button AddButton { get { return _add; } }
        public Button RemoveButton { get { return _remove; } }
        public string Status { get { return _status.Text; } }

        public AiChatsEditor()
        {
            Dock = DockStyle.Top;
            BackColor = Color.White;
            Font = UiStyle.Body;
            Name = "aiChatsEditor";
            var tips = UiStyle.Tips();

            var head = new Label
            {
                Dock = DockStyle.Top, Height = 26, Padding = new Padding(8, 6, 0, 0), Name = "aiChatsHead",
                Text = T("AiC_Head", "AI chats"), Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), ForeColor = Color.FromArgb(40, 46, 56)
            };
            var note = new Label
            {
                Dock = DockStyle.Top, Height = 22, Padding = new Padding(8, 2, 8, 0), ForeColor = Color.FromArgb(110, 116, 124), Font = UiStyle.Small,
                AutoEllipsis = true,
                Text = T("AiC_Note", "The AI chats 'Ask <AI>' offers, in this order: a name and its web address (https://...). Each user picks theirs in Users and Preferences - none picked, the first.")
            };

            // ---- the bar, as every settings page's ----
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, BackColor = Color.White, WrapContents = false, Padding = new Padding(4, 4, 0, 0) };
            _save = UiStyle.ToolButton("💾", T("AiC_Save", "Keep the list"), tips, (s, e) => Keep());
            _revert = UiStyle.ToolButton("↺", T("AiC_Revert", "Throw the changes away and show the kept list"), tips, (s, e) => Fill(AiChats.All));
            _add = UiStyle.ToolButton("➕", T("AiC_Add", "Add an AI chat to the list"), tips, (s, e) => AddRow());
            _remove = UiStyle.ToolButton("🗑", T("AiC_Remove", "Remove the selected AI chat from the list"), tips, (s, e) => RemoveRow());
            _up = UiStyle.ToolButton("▲", T("AiC_Up", "Up the list"), tips, (s, e) => Move(-1));
            _down = UiStyle.ToolButton("▼", T("AiC_Down", "Down the list"), tips, (s, e) => Move(1));
            _usual = UiStyle.ToolButton("↻", T("AiC_Usual", "The usual list"), tips, (s, e) => { Fill(AiChats.Defaults); _dirty.MarkDirty(); });
            _save.Name = "aiChatsSave"; _add.Name = "aiChatsAdd"; _remove.Name = "aiChatsRemove"; _usual.Name = "aiChatsUsual";
            foreach (var b in new[] { _save, _revert, _add, _remove, _up, _down, _usual }) { b.Margin = new Padding(2, 0, 2, 0); bar.Controls.Add(b); }
            _status.AutoSize = true;
            _status.Margin = new Padding(14, 9, 0, 0);
            _status.ForeColor = Color.FromArgb(160, 40, 40);
            bar.Controls.Add(_status);
            _dirty.Guard(_save, _revert);

            // ---- the table, as every table ----
            _grid.Dock = DockStyle.Top;
            _grid.Name = "aiChatsGrid";
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.ScrollBars = ScrollBars.Vertical;          // its two columns fill its width
            _grid.BackgroundColor = Color.White;
            _grid.Columns.Add("name", T("AiC_Name", "Name"));
            _grid.Columns.Add("link", T("AiC_Link", "Web address"));
            _grid.Columns[0].FillWeight = 30;
            _grid.Columns[1].FillWeight = 70;
            foreach (DataGridViewColumn c in _grid.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;
            UiStyle.EnableTableTools(_grid, "AiChats", sorting: false);
            _grid.CellValueChanged += (s, e) => { if (!_filling) _dirty.MarkDirty(); };
            _grid.RowHeightChanged += (s, e) => FitHeight();
            _grid.ColumnHeadersHeightChanged += (s, e) => FitHeight();
            _grid.CurrentCellChanged += (s, e) => UpdateButtons();

            var rule = new Panel { Dock = DockStyle.Top, Height = 1, BackColor = UiStyle.GridRule };
            Controls.Add(rule);
            Controls.Add(_grid);
            Controls.Add(note);
            Controls.Add(bar);
            Controls.Add(head);

            Fill(AiChats.All);
            AiChats.Changed += OnChanged;
            Disposed += (s, e) => AiChats.Changed -= OnChanged;
        }

        private void OnChanged(object sender, EventArgs e)
        {
            if (IsDisposed || _dirty.IsDirty) return;
            if (InvokeRequired) { try { BeginInvoke(new EventHandler(OnChanged), sender, e); } catch { } return; }
            Fill(AiChats.All);
        }

        private void Fill(IEnumerable<AiChats.Chat> chats)
        {
            _filling = true;
            try
            {
                _grid.Rows.Clear();
                foreach (var c in chats) _grid.Rows.Add(c.Name, c.Link);
            }
            finally { _filling = false; }
            _dirty.Reset();
            _status.Text = "";
            FitHeight();
            UpdateButtons();
        }

        /// <summary>As tall as its rows (room for one more), at most twelve.</summary>
        private void FitHeight()
        {
            int rows = Math.Min(12, Math.Max(3, _grid.Rows.Count + 1));
            int h = _grid.ColumnHeadersHeight + rows * _grid.RowTemplate.Height + 3;
            if (_grid.Height != h) _grid.Height = h;
            int total = 0;
            foreach (Control c in Controls) total += c.Height;
            if (Height != total) Height = total;
        }

        private void UpdateButtons()
        {
            var cell = _grid.CurrentCell;
            int i = cell != null ? cell.RowIndex : -1;
            _remove.Enabled = i >= 0;
            _up.Enabled = i > 0;
            _down.Enabled = i >= 0 && i < _grid.Rows.Count - 1;
        }

        /// <summary>A new row at the end, its name to type.</summary>
        internal void AddRow()
        {
            int i = _grid.Rows.Add("", "https://");
            _dirty.MarkDirty();
            FitHeight();
            try
            {
                _grid.CurrentCell = _grid.Rows[i].Cells[0];
                if (_grid.IsHandleCreated) _grid.BeginEdit(true);
            }
            catch { }
            UpdateButtons();
        }

        /// <summary>The selected row out of the list (kept with 💾).</summary>
        internal void RemoveRow()
        {
            var cell = _grid.CurrentCell;
            if (cell == null || cell.RowIndex < 0) return;
            int i = cell.RowIndex;
            _grid.Rows.RemoveAt(i);
            _dirty.MarkDirty();
            FitHeight();
            if (_grid.Rows.Count > 0) _grid.CurrentCell = _grid.Rows[Math.Min(i, _grid.Rows.Count - 1)].Cells[0];
            UpdateButtons();
        }

        private void Move(int by)
        {
            var cell = _grid.CurrentCell;
            if (cell == null) return;
            int i = cell.RowIndex, j = i + by;
            if (i < 0 || j < 0 || j >= _grid.Rows.Count) return;
            _grid.EndEdit();
            var a = new[] { _grid.Rows[i].Cells[0].Value, _grid.Rows[i].Cells[1].Value };
            _grid.Rows[i].Cells[0].Value = _grid.Rows[j].Cells[0].Value;
            _grid.Rows[i].Cells[1].Value = _grid.Rows[j].Cells[1].Value;
            _grid.Rows[j].Cells[0].Value = a[0];
            _grid.Rows[j].Cells[1].Value = a[1];
            _grid.CurrentCell = _grid.Rows[j].Cells[cell.ColumnIndex];
            _dirty.MarkDirty();
        }

        /// <summary>The list as typed: checked (a name, a web address, each name once) and kept; false with the reason shown.</summary>
        internal bool Keep()
        {
            _grid.EndEdit();
            var list = new List<AiChats.Chat>();
            foreach (DataGridViewRow r in _grid.Rows)
            {
                string name = Convert.ToString(r.Cells[0].Value ?? "").Trim(), link = Convert.ToString(r.Cells[1].Value ?? "").Trim();
                if (name.Length == 0 && (link.Length == 0 || link == "https://")) continue;
                if (name.Length == 0) return Fail(string.Format(T("AiC_NoName", "Row {0}: a name, please."), r.Index + 1));
                if (!AiChats.IsLink(link)) return Fail(string.Format(T("AiC_BadLink", "{0}: its web address must start with https:// (or http://)."), name));
                if (list.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                    return Fail(string.Format(T("AiC_Twice", "{0} is in the list twice."), name));
                list.Add(new AiChats.Chat(name.Replace(";", ","), link));
            }
            if (list.Count == 0) return Fail(T("AiC_Empty", "At least one AI chat, please."));
            try { AiChats.Save(list); }
            catch (Exception ex) { return Fail(ex.Message); }
            Fill(AiChats.All);
            _status.ForeColor = Color.FromArgb(40, 110, 50);
            _status.Text = T("AiC_Kept", "The list is kept.");
            return true;
        }

        private bool Fail(string why)
        {
            _status.ForeColor = Color.FromArgb(160, 40, 40);
            _status.Text = why;
            return false;
        }
    }
}
