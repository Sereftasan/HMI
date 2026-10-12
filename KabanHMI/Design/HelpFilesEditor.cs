using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace Kaban.UI.Design
{
    /// <summary>
    /// Edits a unit's help files in the designer: a LIST - add, remove, reorder.
    ///
    /// It used to be a file dialog whose choice REPLACED the value, so picking a
    /// second file dropped the first. Now the files already listed stay; Add
    /// appends the ones picked (several at once), and the order is the order of
    /// the unit's help list.
    ///
    /// Stores file NAMES, ";"-separated, not paths: the files themselves go into
    /// the machine's Help folder, which ships beside the program - a path on the
    /// engineer's PC would point nowhere on the machine. A name with a language
    /// ("Unit_Clamp.en.html") is stored without it ("Unit_Clamp.html"): the
    /// program opens the file in the language on screen, English when there is
    /// none (see TroubleshootingCatalog.ResolveLanguageFile).
    /// </summary>
    public class HelpFilesEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            var svc = provider == null ? null : provider.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
            using (var form = new HelpFilesForm(Split(value as string)))
            {
                var result = svc != null ? svc.ShowDialog(form) : form.ShowDialog();
                return result == DialogResult.OK ? string.Join(";", form.Files) : value;
            }
        }

        public static List<string> Split(string value)
        {
            return (value ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(f => f.Trim()).Where(f => f.Length > 0).ToList();
        }

        /// <summary>"Unit_Clamp.en.html" -> "Unit_Clamp.html"; any other name as it is.</summary>
        public static string NameToStore(string path)
        {
            // Either separator: a name typed or pasted may carry a Windows or a Unix path.
            var p = path ?? "";
            var name = p.Substring(Math.Max(p.LastIndexOf('\\'), p.LastIndexOf('/')) + 1);
            var m = Regex.Match(name, @"^(.+)\.[A-Za-z]{2}(\.[^.]+)$");
            return m.Success ? m.Groups[1].Value + m.Groups[2].Value : name;
        }

        /// <summary>The list dialog (public for tests).</summary>
        public sealed class HelpFilesForm : Form
        {
            private readonly ListBox _list = new ListBox();

            public IEnumerable<string> Files { get { return _list.Items.Cast<string>(); } }

            public HelpFilesForm(IEnumerable<string> files)
            {
                Text = "Help files of this unit";
                FormBorderStyle = FormBorderStyle.SizableToolWindow;
                StartPosition = FormStartPosition.CenterParent;
                ClientSize = new Size(460, 300);
                MinimumSize = new Size(360, 240);
                ShowInTaskbar = false;

                var note = new Label
                {
                    Dock = DockStyle.Top, Height = 44, Padding = new Padding(8, 6, 8, 0),
                    Text = "Names only. Put the files in the machine's Help folder (Copy to Output Directory: " +
                           "Copy if newer). The order here is the order of the unit's help list."
                };
                _list.Dock = DockStyle.Fill;
                _list.IntegralHeight = false;
                _list.SelectionMode = SelectionMode.MultiExtended;
                foreach (var f in files) _list.Items.Add(f);

                var side = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 104, FlowDirection = FlowDirection.TopDown, Padding = new Padding(6) };
                Func<string, EventHandler, Button> btn = (text, click) =>
                {
                    var b = new Button { Text = text, Width = 90, Height = 28 };
                    b.Click += click;
                    side.Controls.Add(b);
                    return b;
                };
                btn("Add...", (s, e) => AddFiles());
                btn("Remove", (s, e) => RemoveSelected());
                btn("Up", (s, e) => Move(-1));
                btn("Down", (s, e) => Move(+1));

                var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(6) };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90, Height = 28 };
                var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Width = 90, Height = 28 };
                bottom.Controls.Add(cancel);
                bottom.Controls.Add(ok);
                AcceptButton = ok;
                CancelButton = cancel;

                Controls.Add(_list);
                Controls.Add(side);
                Controls.Add(note);
                Controls.Add(bottom);
                _list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };
            }

            private void AddFiles()
            {
                using (var dlg = new OpenFileDialog
                {
                    Multiselect = true,
                    Title = "Add help files - from the machine's Help folder",
                    Filter = "Help files|*.html;*.htm;*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*"
                })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    Add(dlg.FileNames);
                }
            }

            /// <summary>Appends files not listed yet, by the name they are stored under.</summary>
            public void Add(IEnumerable<string> paths)
            {
                foreach (var p in paths)
                {
                    var name = NameToStore(p);
                    if (name.Length > 0 && !_list.Items.Cast<string>().Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
                        _list.Items.Add(name);
                }
            }

            private void RemoveSelected()
            {
                foreach (var i in _list.SelectedIndices.Cast<int>().OrderByDescending(i => i).ToList()) _list.Items.RemoveAt(i);
            }

            private void Move(int direction)
            {
                if (_list.SelectedIndices.Count != 1) return;
                int i = _list.SelectedIndex, j = i + direction;
                if (j < 0 || j >= _list.Items.Count) return;
                var item = _list.Items[i];
                _list.Items.RemoveAt(i);
                _list.Items.Insert(j, item);
                _list.SelectedIndex = j;
            }
        }
    }
}
