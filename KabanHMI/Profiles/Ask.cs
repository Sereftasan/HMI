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

        /// <summary>A message (a test's Hook sees it and nothing waits).</summary>
        public static void Tell(IWin32Window owner, string title, string text)
        {
            if (Hook != null) { Hook(title, text, ""); return; }
            MessageBox.Show(owner, text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
