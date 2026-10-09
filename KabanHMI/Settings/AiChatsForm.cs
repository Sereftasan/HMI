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
    /// THE PANEL'S AI CHATS (an Admin's, from the caption bar's "?"): a name and a
    /// web address a line, in the order "Ask AI for help" offers them. Each user
    /// picks theirs in Users and Preferences (Help and AI: Preferred AI); none
    /// picked, the first of the list. "The usual list" puts the program's own
    /// back (Claude, ChatGPT, Gemini, Microsoft Copilot, Grok, DeepSeek, Perplexity).
    /// </summary>
    public class AiChatsForm : Form
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private readonly DataGridView _grid = new DataGridView();
        private readonly Label _status = new Label();

        /// <summary>The table (tests).</summary>
        internal DataGridView Grid { get { return _grid; } }

        public AiChatsForm()
        {
            Text = T("Hlp_AiChats", "AI chats and their links...").TrimEnd('.', ' ');
            Name = "aiChatsWindow";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
            ClientSize = UiScale.Px(new Size(620, 380));
            MinimumSize = UiScale.Px(new Size(480, 300));
            BackColor = Color.White;

            var note = new Label
            {
                Dock = DockStyle.Top, Height = UiScale.Px(40), Padding = new Padding(UiScale.Px(8), UiScale.Px(6), UiScale.Px(8), 0),
                ForeColor = Color.FromArgb(90, 96, 106),
                Text = T("AiC_Note", "The AI chats 'Ask AI for help' offers, in this order: a name and its web address (https://...). " +
                                     "Each user picks theirs in Users and Preferences; none picked, the first.")
            };

            _grid.Dock = DockStyle.Fill;
            _grid.Name = "aiChatsGrid";
            _grid.AllowUserToAddRows = true;
            _grid.AllowUserToDeleteRows = true;
            _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.BackgroundColor = Color.White;
            _grid.Columns.Add("name", T("AiC_Name", "Name"));
            _grid.Columns.Add("link", T("AiC_Link", "Web address"));
            _grid.Columns[0].FillWeight = 30;
            _grid.Columns[1].FillWeight = 70;
            UiStyle.StyleGrid(_grid);
            Fill(AiChats.All);

            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = UiScale.Px(44), FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(UiScale.Px(6)) };
            var save = new Button { Text = "💾  " + T("AiC_Save", "Keep the list"), AutoSize = true, Name = "aiChatsSave" };
            save.Click += (s, e) => { if (Keep()) { DialogResult = DialogResult.OK; Close(); } };
            var cancel = new Button { Text = T("Btn_Cancel", "Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
            var usual = new Button { Text = T("AiC_Usual", "The usual list"), AutoSize = true, Name = "aiChatsUsual" };
            usual.Click += (s, e) => Fill(AiChats.Defaults);
            var up = new Button { Text = "▲", AutoSize = true };
            up.Click += (s, e) => Move(-1);
            var down = new Button { Text = "▼", AutoSize = true };
            down.Click += (s, e) => Move(1);
            UiStyle.SetTip(UiStyle.Tips(), up, T("AiC_Up", "Up the list"));
            UiStyle.SetTip(UiStyle.Tips(), down, T("AiC_Down", "Down the list"));
            bar.Controls.Add(save);
            bar.Controls.Add(cancel);
            bar.Controls.Add(usual);
            bar.Controls.Add(down);
            bar.Controls.Add(up);
            _status.AutoSize = true;
            _status.ForeColor = Color.FromArgb(160, 40, 40);
            _status.Margin = new Padding(0, UiScale.Px(8), UiScale.Px(12), 0);
            bar.Controls.Add(_status);

            Controls.Add(_grid);
            Controls.Add(note);
            Controls.Add(bar);
            CancelButton = cancel;
        }

        private void Fill(IEnumerable<AiChats.Chat> chats)
        {
            _grid.Rows.Clear();
            foreach (var c in chats) _grid.Rows.Add(c.Name, c.Link);
        }

        private void Move(int by)
        {
            var cell = _grid.CurrentCell;
            if (cell == null) return;
            int i = cell.RowIndex, j = i + by;
            if (i < 0 || j < 0 || i >= _grid.Rows.Count || j >= _grid.Rows.Count || _grid.Rows[i].IsNewRow || _grid.Rows[j].IsNewRow) return;
            var a = new[] { _grid.Rows[i].Cells[0].Value, _grid.Rows[i].Cells[1].Value };
            _grid.Rows[i].Cells[0].Value = _grid.Rows[j].Cells[0].Value;
            _grid.Rows[i].Cells[1].Value = _grid.Rows[j].Cells[1].Value;
            _grid.Rows[j].Cells[0].Value = a[0];
            _grid.Rows[j].Cells[1].Value = a[1];
            _grid.CurrentCell = _grid.Rows[j].Cells[cell.ColumnIndex];
        }

        /// <summary>The list as typed: checked (a name, a web address, each name once) and kept; false with the reason shown.</summary>
        internal bool Keep()
        {
            _grid.EndEdit();
            var list = new List<AiChats.Chat>();
            foreach (DataGridViewRow r in _grid.Rows)
            {
                if (r.IsNewRow) continue;
                string name = Convert.ToString(r.Cells[0].Value ?? "").Trim(), link = Convert.ToString(r.Cells[1].Value ?? "").Trim();
                if (name.Length == 0 && link.Length == 0) continue;
                if (name.Length == 0) return Fail(string.Format(T("AiC_NoName", "Row {0}: a name, please."), r.Index + 1));
                if (!AiChats.IsLink(link)) return Fail(string.Format(T("AiC_BadLink", "{0}: its web address must start with https:// (or http://)."), name));
                if (list.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
                    return Fail(string.Format(T("AiC_Twice", "{0} is in the list twice."), name));
                list.Add(new AiChats.Chat(name.Replace(";", ","), link));
            }
            if (list.Count == 0) return Fail(T("AiC_Empty", "At least one AI chat, please."));
            try { AiChats.Save(list); }
            catch (Exception ex) { return Fail(ex.Message); }
            _status.Text = "";
            return true;
        }

        private bool Fail(string why)
        {
            _status.Text = why;
            return false;
        }
    }
}
