using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// THE NOTES' HELPERS ON THE SCREEN (8 October: "It doesn't change anything
    /// when I add helper file to a control at this notes page, even though I am
    /// an admin level user. The admin and maintenance level user translations
    /// and helpers should be reflected to the HMI"). A helper in force on a
    /// control is offered with it: a unit (a cylinder, an axis unit) lists it in
    /// its own "?", any other control gets a small "?" on its corner - a click
    /// opens the helper (its file, or a page of its words).
    /// </summary>
    public static class NoteHelpers
    {
        /// <summary>A control's helpers (and those of what is inside it), as documents to open.</summary>
        public static List<TC.HelpDocument> DocumentsFor(Control c)
        {
            if (c == null) return new List<TC.HelpDocument>();
            return DocumentsFor(NoteTarget.KeyOf(c));
        }

        public static List<TC.HelpDocument> DocumentsFor(string key)
        {
            var list = new List<TC.HelpDocument>();
            foreach (var n in NotesStore.HelpersOn(key))
            {
                var path = NotesStore.DocumentOf(n);
                if (path == null) continue;
                list.Add(new TC.HelpDocument { FileName = path, Title = TitleOf(n) });
            }
            return list;
        }

        /// <summary>A helper's name in a list: its first words, else its file's name.</summary>
        public static string TitleOf(Note n)
        {
            var text = (n.Text ?? "").Trim();
            if (text.Length > 0)
            {
                var line = text.Split('\n')[0].Trim();
                return line.Length > 70 ? line.Substring(0, 67) + "..." : line;
            }
            return string.IsNullOrEmpty(n.FileName) ? LanguageManager.GetString("Nts_KindHelper", "Helper") : n.FileName;
        }

        private sealed class Mark { public UnitHelpBadge Badge; public Control For; public EventHandler Follow; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, Mark> _marks =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Control, Mark>();
        private static readonly List<WeakReference> _marked = new List<WeakReference>();

        /// <summary>The marks of a page put right: on every control with a helper in force, none on the others.</summary>
        public static void Refresh(Control root)
        {
            if (root == null || root.IsDisposed) return;
            var keys = new HashSet<string>(NotesStore.All.Where(n => n.Kind == NoteKind.Helper && n.State == NoteState.Applied && !string.IsNullOrEmpty(n.ControlKey))
                                                         .Select(n => n.ControlKey), StringComparer.Ordinal);
            var want = new HashSet<Control>();
            if (keys.Count > 0) Walk(root, keys, want);
            // marks no longer wanted (under this root)
            lock (_marked)
            {
                foreach (var w in _marked.ToList())
                {
                    var c = w.Target as Control;
                    if (c == null || c.IsDisposed) { _marked.Remove(w); continue; }
                    if (!want.Contains(c) && (c == root || root.Contains(c))) { Unmark(c); _marked.Remove(w); }
                }
            }
            foreach (var c in want) MarkOn(c);
        }

        private static void Walk(Control c, HashSet<string> keys, HashSet<Control> want)
        {
            foreach (Control k in c.Controls)
            {
                if (k is UnitHelpBadge || k is Kaban.UI.Screens.NotesPage) continue;
                // a unit lists its helpers (and its fields') in its own "?"
                if (k is CylinderUnitHMI) continue;
                if (keys.Contains(NoteTarget.KeyOf(k))) want.Add(k);
                if (k.Controls.Count > 0) Walk(k, keys, want);
            }
        }

        /// <summary>The control's mark (tests), or null.</summary>
        public static UnitHelpBadge MarkOf(Control c)
        {
            Mark m;
            return c != null && _marks.TryGetValue(c, out m) && !m.Badge.IsDisposed ? m.Badge : null;
        }

        private static void MarkOn(Control c)
        {
            Mark m;
            if (_marks.TryGetValue(c, out m) && !m.Badge.IsDisposed) { Place(m); return; }
            var badge = new UnitHelpBadge { Name = "noteHelperMark" };
            badge.Documents = () => DocumentsFor(c);
            badge.State = UnitHelpBadge.BadgeState.Info;
            m = new Mark { Badge = badge, For = c };
            m.Follow = (s, e) => Place(m);
            _marks.Remove(c);
            _marks.Add(c, m);
            lock (_marked) _marked.Add(new WeakReference(c));
            c.LocationChanged += m.Follow;
            c.SizeChanged += m.Follow;
            c.VisibleChanged += m.Follow;
            c.ParentChanged += m.Follow;
            c.Disposed += (s, e) => { if (!badge.IsDisposed) badge.Dispose(); };
            UiStyle.SetTip(UiStyle.Tips(), badge, LanguageManager.GetString("Tip_NoteHelper", "Helpers for this control (from the notes page)") + "\n" +
                                               string.Join("\n", DocumentsFor(c).Select(d => d.Title)));
            Place(m);
        }

        /// <summary>
        /// On the control's top right corner: beside it on its parent where the
        /// parent places its children itself, else on the control (a parent that
        /// lays its children out would move a stranger among them).
        /// </summary>
        private static void Place(Mark m)
        {
            var c = m.For;
            var b = m.Badge;
            if (c.IsDisposed || b.IsDisposed) return;
            int size = UiScale.For(c, UnitHelpBadge.BadgeSize);
            if (b.Width != size) b.Size = new Size(size, size);
            var parent = c.Parent;
            bool beside = parent != null && !(parent is FlowLayoutPanel) && !(parent is TableLayoutPanel) && !(parent is DataGridView);
            var host = beside ? parent : c;
            if (b.Parent != host) { if (b.Parent != null) b.Parent.Controls.Remove(b); host.Controls.Add(b); }
            if (beside) b.Location = new Point(Math.Max(0, c.Right - size + size / 3), Math.Max(0, c.Top - size / 3));
            else b.Location = new Point(Math.Max(0, c.ClientSize.Width - size - 1), 1);
            b.Visible = c.Visible;
            b.BringToFront();
        }

        private static void Unmark(Control c)
        {
            Mark m;
            if (!_marks.TryGetValue(c, out m)) return;
            c.LocationChanged -= m.Follow;
            c.SizeChanged -= m.Follow;
            c.VisibleChanged -= m.Follow;
            c.ParentChanged -= m.Follow;
            if (!m.Badge.IsDisposed) { if (m.Badge.Parent != null) m.Badge.Parent.Controls.Remove(m.Badge); m.Badge.Dispose(); }
            _marks.Remove(c);
        }
    }
}
