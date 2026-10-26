using System;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Profiles
{
    /// <summary>
    /// One line of text asked for in a small dialog (a new profile's code, why a
    /// placement is corrected). Hook answers instead of the dialog - that is how a
    /// test gets past it.
    /// </summary>
    public static class Ask
    {
        /// <summary>(title, prompt, initial) -> answer, or null for "cancelled".</summary>
        public static Func<string, string, string, string> Hook;

        public static string Text(IWin32Window owner, string title, string prompt, string initial = "")
        {
            if (Hook != null) return Hook(title, prompt, initial);
            using (var dlg = new Form
            {
                Text = title, Width = 460, Height = 170, FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false
            })
            {
                UiScale.ApplyOnLoad(dlg);
                var lbl = new Label { Text = prompt, Left = 12, Top = 12, Width = 420, Height = 36, Font = UiStyle.Body };
                var tb = new TextBox { Left = 12, Top = 52, Width = 420, Font = UiStyle.Body, Text = initial ?? "" };
                var ok = UiStyle.DialogButton(LanguageManager.GetString("Btn_Ok", "OK"), DialogResult.OK);
                var cancel = UiStyle.DialogButton(LanguageManager.GetString("Btn_Cancel", "Cancel"), DialogResult.Cancel);
                ok.Location = new Point(232, 88);
                cancel.Location = new Point(336, 88);
                dlg.Controls.AddRange(new Control[] { lbl, tb, ok, cancel });
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;
                return dlg.ShowDialog(owner) == DialogResult.OK ? tb.Text.Trim() : null;
            }
        }

        /// <summary>Yes / No (a test's Hook answers "yes" or anything else).</summary>
        public static bool YesNo(IWin32Window owner, string title, string question)
        {
            if (Hook != null) return string.Equals(Hook(title, question, "yes"), "yes", StringComparison.OrdinalIgnoreCase);
            return MessageBox.Show(owner, question, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        /// <summary>
        /// A question with its own buttons ("Save it", "Discard it", "Cancel"): the index of the one pressed,
        /// -1 when the window is closed. A test's Hook gets the buttons as "A|B|C" and answers one of them.
        /// </summary>
        public static int Choose(IWin32Window owner, string title, string question, params string[] buttons)
        {
            if (buttons == null || buttons.Length == 0) return -1;
            if (Hook != null)
            {
                var answer = Hook(title, question, string.Join("|", buttons));
                return Array.FindIndex(buttons, b => string.Equals(b, answer, StringComparison.OrdinalIgnoreCase));
            }
            using (var dlg = new Form
            {
                Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12), Font = UiStyle.Body, BackColor = Color.White
            })
            {
                UiScale.ApplyOnLoad(dlg);
                var layout = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, Dock = DockStyle.Fill };
                layout.Controls.Add(new Label { Text = question, AutoSize = true, MaximumSize = new Size(420, 0), Margin = new Padding(0, 0, 0, 14) });
                var row = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Anchor = AnchorStyles.Right };
                int chosen = -1;
                for (int i = 0; i < buttons.Length; i++)
                {
                    int index = i;
                    var b = UiStyle.TextButton(buttons[i], 110);
                    b.Margin = new Padding(6, 0, 0, 0);
                    b.Click += (s, e) => { chosen = index; dlg.Close(); };
                    row.Controls.Add(b);
                    if (i == 0) dlg.AcceptButton = b;
                    if (i == buttons.Length - 1) dlg.CancelButton = b;      // Esc: the last (Cancel)
                }
                layout.Controls.Add(row);
                dlg.Controls.Add(layout);
                dlg.ShowDialog(owner);
                return chosen;
            }
        }

        /// <summary>A message (a test's Hook sees it and nothing waits).</summary>
        public static void Tell(IWin32Window owner, string title, string text)
        {
            if (Hook != null) { Hook(title, text, ""); return; }
            MessageBox.Show(owner, text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
