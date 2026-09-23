using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Kaban.UI.Design
{
    /// <summary>
    /// Picks a unit's help files in the designer.
    ///
    /// Stores file NAMES, ";"-separated, not paths: the files themselves go into
    /// the machine's Help folder, which ships beside the program - a path on the
    /// engineer's PC would point nowhere on the machine.
    /// </summary>
    public class HelpFilesEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            using (var dlg = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Help files for this unit - copy them into the machine's Help folder",
                Filter = "Help files|*.html;*.htm;*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return value;
                return string.Join(";", dlg.FileNames.Select(Path.GetFileName));
            }
        }
    }
}

