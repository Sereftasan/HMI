using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

namespace Kaban.UI.Screens
{
    /// <summary>
    /// WHERE IS IT USED? (23 October: "There should also be find function (with button and Ctrl+F) at Notes
    /// and Feedback page for all the element (device addresses or translation keys) so the user can check where
    /// they are used to understand what difference their suggestions will make"): the picked control's devices
    /// and texts, every place each one is used - the menu, a tab, a page's controls, an alarm, a line of the
    /// program (the Translation page's "Where is it used?" for a text, the PLC Monitor's Find for a device) - in
    /// one list; any other device ("X12", "X12@2") or translation key typed in. A double click opens a page's use
    /// and points at it.
    /// </summary>
    public sealed class NotesFindForm : Form
    {
        /// <summary>A device or a text to find.</summary>
        public sealed class Element
        {
            public bool IsText;
            /// <summary>A text's key; a device's address on its controller.</summary>
            public string Name = "";
            public int Station;
            /// <summary>"D100", "X12@2", "Btn_Start".</summary>
            public string Shown = "";
        }

        /// <summary>A use found.</summary>
        public sealed class Found
        {
            public Element Of;
            public string Kind = "", Place = "", What = "";
            public int Line;
            /// <summary>The page's use to open (null: not on a page - an alarm, a program line).</summary>
            public DeviceCollector.Use Way;
        }

        private readonly List<Element> _elements;
        private readonly int _station;
        private readonly TextBox _query = new TextBox { Name = "notesFindQuery" };
        private readonly DataGridView _grid = new DataGridView { Name = "notesFindList" };
        private readonly Label _status = new Label { Name = "notesFindStatus" };
        private List<Found> _found = new List<Found>();

        /// <summary>The use chosen to open (null: none).</summary>
        public DeviceCollector.Use Chosen { get; private set; }

        /// <summary>The uses listed (tests).</summary>
        internal List<Found> Listed { get { return _found; } }

        /// <summary>What the status line says (tests).</summary>
        internal string Status { get { return _status.Text; } }

        private static string T(string key, string fallback) { return LanguageManager.GetString(key, fallback); }

        /// <param name="title">The picked control's name ("" - none picked).</param>
        /// <param name="elements">Its devices and texts.</param>
        /// <param name="station">The controller of a device typed without "@n".</param>
        public NotesFindForm(string title, List<Element> elements, int station)
        {
            _elements = elements ?? new List<Element>();
            _station = station;
            UiScale.ApplyOnLoad(this);
            Text = T("Ttl_NtsFind", "Where is it used?") + (string.IsNullOrWhiteSpace(title) ? "" : " - " + title);
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 560);
            MinimumSize = new Size(560, 300);
            ShowInTaskbar = false;
            MinimizeBox = false;
            BackColor = Color.White;
            Font = UiStyle.Body;
            KeyPreview = true;

            var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 46, ColumnCount = 4, Padding = new Padding(8, 8, 8, 2) };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            top.Controls.Add(new Label { Text = T("Lbl_NtsFindWhat", "Device or translation key:"), AutoSize = true, Margin = new Padding(0, 7, 6, 0) }, 0, 0);
            _query.Dock = DockStyle.Fill;
            _query.Margin = new Padding(0, 3, 6, 0);
            top.Controls.Add(_query, 1, 0);
            var go = UiStyle.TextButton(T("Btn_NtsFind", "Find"), 0, (s, e) => Search(_query.Text));
            go.Name = "notesFindGo";
            top.Controls.Add(go, 2, 0);
            var mine = UiStyle.TextButton(T("Btn_NtsFindPicked", "The control's"), 0, (s, e) => { _query.Text = ""; Search(""); });
            mine.Name = "notesFindPicked";
            mine.Enabled = _elements.Count > 0;
            _tips.SetToolTip(mine, T("Tip_NtsFindPicked", "Every device and text of the control picked, where each one is used"));
            top.Controls.Add(mine, 3, 0);
            _query.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { Search(_query.Text); e.Handled = e.SuppressKeyPress = true; } };

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Element", HeaderText = T("Col_NtsFindElement", "Device or text"), Width = 170 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Kind", HeaderText = T("Col_FndKind", "Where"), Width = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Place", HeaderText = T("Col_FndPlace", "Page or file"), Width = 300 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "What", HeaderText = T("Col_FndWhat", "What uses it"), Width = 440 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Line", HeaderText = T("Col_FndLine", "Line"), Width = 60 });
            UiStyle.EnableTableTools(_grid, "NotesFind");
            _grid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Open(e.RowIndex); };
            _grid.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter && _grid.CurrentRow != null) { Open(_grid.CurrentRow.Index); e.Handled = true; } };

            _status.Dock = DockStyle.Bottom;
            _status.Height = 44;
            _status.Padding = new Padding(8, 4, 8, 0);
            _status.ForeColor = Color.FromArgb(70, 76, 86);

            var close = UiStyle.DialogButton(T("Btn_Close", "Close"), DialogResult.Cancel);
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 44, Padding = new Padding(6) };
            bottom.Controls.Add(close);
            CancelButton = close;
            Controls.Add(_grid);
            Controls.Add(_status);
            Controls.Add(bottom);
            Controls.Add(top);
            Shown += (s, e) => _query.Focus();
            Search("");
        }

        private readonly ToolTip _tips = UiStyle.Tips();

        /// <summary>
        /// The uses of <paramref name="text"/> - a translation key, else a device ("X12@2": controller 2's) - or,
        /// nothing typed, of every device and text of the control picked.
        /// </summary>
        internal void Search(string text)
        {
            text = (text ?? "").Trim();
            List<Element> wanted;
            if (text.Length == 0) wanted = _elements;
            else
            {
                string why;
                var e = Parse(text, _station, out why);
                if (e == null) { Show(new List<Found>(), why); return; }
                wanted = new List<Element> { e };
            }
            var found = new List<Found>();
            var cursor = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;
            try { foreach (var e in wanted) found.AddRange(UsesOf(e)); }
            catch (Exception ex) { AuditLogger.Log("notes find: " + ex.Message); }
            finally { Cursor.Current = cursor; }
            string note;
            if (wanted.Count == 0) note = T("Msg_NtsFindNothing", "The control picked has no device or text of its own - type a device or a translation key above.");
            else if (found.Count == 0) note = T("Msg_NtsFindNone", "Not used on a page, in the menu, by an alarm or in the program's lines.");
            else
            {
                var unused = wanted.Where(e => !found.Any(f => ReferenceEquals(f.Of, e))).Select(e => e.Shown).ToList();
                note = string.Format(T("Msg_NtsFindCount", "{0} use(s) of {1}. A double click opens a page and points at what uses it."),
                                     found.Count, string.Join(", ", wanted.Select(e => e.Shown).Take(8)) + (wanted.Count > 8 ? ", ..." : ""));
                if (unused.Count > 0) note += "  " + string.Format(T("Msg_NtsFindUnused", "Used nowhere else: {0}."), string.Join(", ", unused));
            }
            Show(found, note);
        }

        private void Show(List<Found> found, string note)
        {
            _found = found;
            _grid.Rows.Clear();
            foreach (var f in found)
                _grid.Rows.Add(f.Of.Shown, f.Kind, f.Place, f.What, f.Line > 0 ? f.Line.ToString(CultureInfo.InvariantCulture) : "");
            _status.Text = note ?? "";
        }

        /// <summary>A translation key (one of the files' first), else a device address ("D100", "X12@2"); null and why: neither.</summary>
        internal static Element Parse(string text, int station, out string why)
        {
            why = "";
            text = (text ?? "").Trim();
            if (LanguageManager.HasKey(text)) return new Element { IsText = true, Name = text, Shown = text };
            string device; int st;
            if (!PlcAddressHelper.TrySplitStation(text, out device, out st)) { device = text; st = station; }
            device = device.Trim().ToUpperInvariant();
            string prefix; long n;
            if (PlcAddressHelper.TrySplit(device, out prefix, out n))
                return new Element { Name = device, Station = st, Shown = st == station ? device : device + PlcAddressHelper.StationMark + st };
            why = string.Format(T("Msg_NtsFindUnknown", "{0} is no translation key and no device address."), text);
            return null;
        }

        /// <summary>Where one element is used.</summary>
        internal static List<Found> UsesOf(Element e)
        {
            var list = new List<Found>();
            if (e.IsText)
            {
                foreach (var u in TextUses.Find(e.Name))
                    list.Add(new Found { Of = e, Kind = TranslationUsesForm.KindName(u), Place = u.Place,
                                         What = u.What + (u.ByText ? "  " + T("Fnd_ByText", "(shows the same text)") : ""), Line = u.Line, Way = u.Way });
                return list;
            }
            foreach (var u in DeviceCollector.UsesOf(e.Station).Where(u => string.Equals((u.Address ?? "").Trim(), e.Name, StringComparison.OrdinalIgnoreCase)))
                list.Add(new Found { Of = e, Kind = u.IsAlarm ? T("Fnd_KindAlarm", "Alarm") : T("Fnd_KindPage", "Page"), Place = u.PageTitle, What = u.UsedAs,
                                     Way = u.IsAlarm || u.Screen == null ? null : u });
            return list;
        }

        /// <summary>A use opened: its page's - the window closes; else said why not.</summary>
        internal void Open(int row)
        {
            if (row < 0 || row >= _found.Count) return;
            var f = _found[row];
            if (f.Way == null)
            {
                _status.Text = f.Line > 0
                    ? string.Format(T("Msg_FndCodeLine", "A line of the program: {0}, line {1}."), f.Place, f.Line)
                    : T("Msg_FindNoPage", "It is not on a page (an alarm definition).");
                return;
            }
            Chosen = f.Way;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.F)) { _query.Focus(); _query.SelectAll(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
