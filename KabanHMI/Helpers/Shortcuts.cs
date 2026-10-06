using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// THE PROGRAM'S BUTTONS ON THE KEYBOARD: a key for each tool button that has
    /// a usual one - Delete for a bin, Ctrl+S for the floppy, Insert for "+",
    /// Ctrl+E / Ctrl+I for export and import, Ctrl+O
    /// for a folder, F2 for the pencil, F5 to read again, Esc to clear - and on the
    /// main window Alt+Left / Alt+Right for Back and Forward, Ctrl+L to sign in or
    /// out. The button's tooltip says the key on its first line, what the button
    /// does on the second.
    ///
    /// A key presses the button that is IN SIGHT and enabled, on the window that
    /// has the keyboard, nearest to where the focus is: on a page with two such
    /// buttons equally near (two pages open, side by side) a key does nothing
    /// rather than guess. A key that a text box or a table cell being typed into
    /// needs (Delete, Insert, the arrows, F2, Ctrl+C / V / X / Z / Y / A) is left to
    /// it. The machine's own buttons (ButtonHMI: jogs, starts, resets) have no
    /// keys: a key is pressed by mistake too easily to move a machine.
    /// </summary>
    public static class Shortcuts
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private sealed class Entry
        {
            public WeakReference Button;
            public Keys Keys;
            public bool Dispatch;     // false: the window handles the key itself (the PLC Monitor); shown only
        }

        private static readonly List<Entry> _entries = new List<Entry>();

        /// <summary>
        /// Gives <paramref name="button"/> a key and its tooltip the key on the first
        /// line, <paramref name="tip"/> on the second. <paramref name="dispatch"/>
        /// false: the window presses it itself (only shown).
        /// </summary>
        public static void Set(Control button, Keys keys, ToolTip tips, string tip, bool dispatch = true)
        {
            if (button == null) return;
            lock (_entries)
            {
                _entries.RemoveAll(e => !e.Button.IsAlive || ReferenceEquals(e.Button.Target, button) || ((Control)e.Button.Target).IsDisposed);
                if (keys != Keys.None) _entries.Add(new Entry { Button = new WeakReference(button), Keys = keys, Dispatch = dispatch });
            }
            if (tips != null && tip != null) tips.SetToolTip(button, With(keys, tip));
        }

        /// <summary>The key of a button, or None.</summary>
        public static Keys Of(Control button)
        {
            lock (_entries)
            {
                var e = _entries.FirstOrDefault(x => ReferenceEquals(x.Button.Target, button));
                return e != null ? e.Keys : Keys.None;
            }
        }

        /// <summary>A tooltip with the key on its first line: "Ctrl+S\nSave the settings".</summary>
        public static string With(Keys keys, string tip)
        {
            if (keys == Keys.None) return tip ?? "";
            return Text(keys) + "\n" + (tip ?? "");
        }

        /// <summary>A key as the keyboard says it, in the language: "Ctrl+S", "Strg+S", "Del", "Alt+\u2190".</summary>
        public static string Text(Keys keys)
        {
            if (keys == Keys.None) return "";
            var parts = new List<string>();
            if ((keys & Keys.Control) != 0) parts.Add(T("Key_Ctrl", "Ctrl"));
            if ((keys & Keys.Shift) != 0) parts.Add(T("Key_Shift", "Shift"));
            if ((keys & Keys.Alt) != 0) parts.Add(T("Key_Alt", "Alt"));
            parts.Add(KeyName(keys & Keys.KeyCode));
            return string.Join("+", parts);
        }

        private static string KeyName(Keys k)
        {
            switch (k)
            {
                case Keys.Delete: return T("Key_Delete", "Del");
                case Keys.Insert: return T("Key_Insert", "Insert");
                case Keys.Escape: return "Esc";
                case Keys.Enter: return T("Key_Enter", "Enter");
                case Keys.Space: return T("Key_Space", "Space");
                case Keys.Left: return "\u2190";
                case Keys.Right: return "\u2192";
                case Keys.Up: return "\u2191";
                case Keys.Down: return "\u2193";
                case Keys.PageUp: return "PgUp";
                case Keys.PageDown: return "PgDn";
                default:
                    if (k >= Keys.D0 && k <= Keys.D9) return ((char)('0' + (k - Keys.D0))).ToString();
                    return k.ToString();
            }
        }

        /// <summary>
        /// The usual key for a tool button's picture: the bin Delete, the floppy
        /// Ctrl+S, "+" Insert, export Ctrl+E, import Ctrl+I,
        /// the folder Ctrl+O, the pencil F2, the curl (read again) F5, the cross
        /// (clear) Esc, the clipboard Ctrl+Shift+C, a new page Ctrl+Shift+S. None for
        /// the rest (the magnifier finds on one page and zooms on another) - and for
        /// what acts on the machine (an alarm reset, acknowledging).
        /// </summary>
        public static Keys Usual(string glyph)
        {
            switch ((glyph ?? "").Replace("\uFE0F", ""))
            {
                case "\uD83D\uDDD1": return Keys.Delete;                       // bin
                case "\uD83D\uDCBE": return Keys.Control | Keys.S;             // floppy
                case "\u2795": return Keys.Insert;                             // +
                case "\uD83D\uDCE4": return Keys.Control | Keys.E;             // export (out-tray)
                case "\uD83D\uDCE5": return Keys.Control | Keys.I;             // import (in-tray)
                case "\uD83D\uDCC2": return Keys.Control | Keys.O;             // folder
                case "\u270F": return Keys.F2;                                 // pencil
                case "\u21BA": case "\u21BB": return Keys.F5;                  // curl: read again / throw the changes away
                case "\u2716": return Keys.Escape;                             // cross: clear
                case "\uD83D\uDCCB": return Keys.Control | Keys.Shift | Keys.C; // clipboard: copy all of it
                case "\uD83D\uDCC4": return Keys.Control | Keys.Shift | Keys.S; // a new page: save as
                default: return Keys.None;
            }
        }

        // ==================================================================
        // PRESSING
        // ==================================================================

        /// <summary>The windows that press their buttons from the keyboard.</summary>
        public static void Attach(Form form)
        {
            if (form == null) return;
            form.KeyPreview = true;
            form.KeyDown += (s, e) =>
            {
                if (e.Handled) return;
                if (Press(form, e.KeyData)) { e.Handled = true; e.SuppressKeyPress = true; }
            };
        }

        /// <summary>The keys a box or a cell being typed into needs for itself.</summary>
        private static bool TypingKey(Keys keys)
        {
            var code = keys & Keys.KeyCode;
            bool ctrl = (keys & Keys.Control) != 0, alt = (keys & Keys.Alt) != 0;
            if (alt) return false;
            if (!ctrl)
                switch (code)
                {
                    case Keys.Delete: case Keys.Insert: case Keys.Back: case Keys.F2: case Keys.Home: case Keys.End:
                    case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: case Keys.Enter: case Keys.Space:
                        return true;
                    default:
                        return false;
                }
            switch (code)
            {
                case Keys.C: case Keys.V: case Keys.X: case Keys.Z: case Keys.Y: case Keys.A:
                case Keys.Left: case Keys.Right: case Keys.Home: case Keys.End: case Keys.Back: case Keys.Delete:
                    return (keys & Keys.Shift) == 0 || code == Keys.Z;
                default:
                    return false;
            }
        }

        /// <summary>The control with the keyboard in <paramref name="form"/>, innermost.</summary>
        private static Control Focused(Form form)
        {
            Control c = form;
            for (int i = 0; i < 64; i++)
            {
                var cc = c as ContainerControl;
                if (cc == null || cc.ActiveControl == null || cc.ActiveControl == c) break;
                c = cc.ActiveControl;
            }
            return c;
        }

        /// <summary>True while it takes typed keys: a text box, an editable combo, a cell being typed into.</summary>
        private static bool Typing(Control c)
        {
            for (var p = c; p != null; p = p.Parent)
            {
                if (p is TextBoxBase || p is NumericUpDown || p is DateTimePicker) return true;
                var combo = p as ComboBox;
                if (combo != null && (combo.DropDownStyle != ComboBoxStyle.DropDownList || combo.DroppedDown)) return true;
                var grid = p as DataGridView;
                if (grid != null && grid.IsCurrentCellInEditMode) return true;
                if (p is Form) break;
            }
            return false;
        }

        /// <summary>A table that takes the key itself (Delete in a table whose rows the user may delete).</summary>
        private static bool TableTakes(Control c, Keys keys)
        {
            var grid = c as DataGridView;
            return grid != null && keys == Keys.Delete && grid.AllowUserToDeleteRows && !grid.ReadOnly;
        }

        /// <summary>On the screen: shown, and every one it is in shown.</summary>
        private static bool InSight(Control c)
        {
            for (var p = c; p != null; p = p.Parent) if (!p.Visible) return false;
            return c.IsHandleCreated;
        }

        private static int Depth(Control c) { int d = 0; for (var p = c; p != null; p = p.Parent) d++; return d; }

        /// <summary>How deep the nearest container that holds both is.</summary>
        private static int Nearness(Control a, Control b)
        {
            var ups = new HashSet<Control>();
            for (var p = a; p != null; p = p.Parent) ups.Add(p);
            for (var p = b; p != null; p = p.Parent) if (ups.Contains(p)) return Depth(p);
            return 0;
        }

        /// <summary>
        /// Presses the button of <paramref name="keys"/> on <paramref name="form"/>:
        /// in sight, enabled, nearest the focus; none when two are equally near.
        /// True when one was pressed.
        /// </summary>
        public static bool Press(Form form, Keys keys)
        {
            if (form == null || keys == Keys.None) return false;
            var focus = Focused(form);
            if (TypingKey(keys) && (Typing(focus) || TableTakes(focus, keys))) return false;
            if (keys == Keys.Escape && focus is ComboBox && ((ComboBox)focus).DroppedDown) return false;

            List<Control> candidates;
            lock (_entries)
            {
                _entries.RemoveAll(e => !e.Button.IsAlive || ((Control)e.Button.Target).IsDisposed);
                candidates = _entries.Where(e => e.Keys == keys && e.Dispatch).Select(e => (Control)e.Button.Target).ToList();
            }
            candidates = candidates.Where(b => b.FindForm() == form || IsInside(b, form)).Where(b => InSight(b) && b.Enabled && Enabled(b)).ToList();
            if (candidates.Count == 0) return false;
            var ranked = candidates.Select(b => new { B = b, N = Nearness(b, focus) }).OrderByDescending(x => x.N).ToList();
            if (ranked.Count > 1 && ranked[0].N == ranked[1].N) return false;       // two equally near: no guess
            var button = ranked[0].B as Button;
            if (button != null) button.PerformClick();
            else typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                                .Invoke(ranked[0].B, new object[] { EventArgs.Empty });
            return true;
        }

        private static bool IsInside(Control c, Control root)
        {
            for (var p = c; p != null; p = p.Parent) if (p == root) return true;
            return false;
        }

        private static bool Enabled(Control c)
        {
            for (var p = c; p != null; p = p.Parent) if (!p.Enabled) return false;
            return true;
        }
    }
}
