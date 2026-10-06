using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// THE NOTES PAGE's place in the main window: a side page (Notes and
    /// feedback); while it is open a tap on a control of the page - in the
    /// content, or on another side page - picks that control for it instead of
    /// operating it. The menu, the caption bar, the side bar, a page's tabs and
    /// the scroll bars work as ever, so every page can still be reached.
    /// </summary>
    public partial class MainForm
    {
        internal const string NotesKey = "Notes";

        private void RegisterNotesPage()
        {
            SidePages.Register(new SidePage
            {
                Key = NotesKey,
                TitleKey = "Ttl_Notes", TitleFallback = "Notes and feedback",
                Glyph = DrawNotesGlyph,
                Create = () => new Screens.NotesPage(IsPickable, PlaceOf, PanelName),
                RequiredLevel = SecurityLevel.None,
                SortOrder = 900
            });
        }

        /// <summary>The panel's name in a feedback file: the main machine's.</summary>
        private string PanelName()
        {
            var main = Kaban.Core.Machines.MachineRegistry.Main;
            return main != null ? main.Module.ModuleId : "HMI";
        }

        /// <summary>
        /// True for a control a tap picks while the notes page is open: on the page
        /// on show, or on another side page - not on the notes page, the menu, the
        /// caption bar or the side bar; not a page's tabs or a scroll bar.
        /// </summary>
        internal bool IsPickable(Control c)
        {
            if (c == null || c.IsDisposed) return false;
            for (var p = c; p != null; p = p.Parent)
            {
                if (p is Screens.NotesPage) return false;
                if (p is ScrollBar || p is Splitter || p is TabControl && p == c) return false;
                if (p.GetType().Name == "TabRowStrip") return false;          // a page's tabs (SettingsScreen)
            }
            for (var p = c; p != null; p = p.Parent)
            {
                if (p == pnlContent) return true;
                if (p == _sideHost) return true;
                if (p is SidePageWindow) return true;
            }
            return false;
        }

        /// <summary>Where a control is, as the operator went there: "FA2010 / Machine Parameters / Axis Settings".</summary>
        internal string PlaceOf(Control c)
        {
            if (c == null) return "";
            var parts = new List<string>();
            for (var p = c; p != null; p = p.Parent)
            {
                var tabs = p as Kaban.UI.Settings.SettingsScreen;
                if (tabs != null && tabs.ActivePage != null)
                    parts.Add(LanguageManager.GetString(tabs.ActivePage.TitleKey, tabs.ActivePage.TitleFallback));
                if (p == _currentScreen)
                {
                    var nav = _menuButtons.FirstOrDefault(kv => ReferenceEquals(kv.Value, _currentScreen)).Key;
                    if (nav != null) parts.Add(LanguageManager.GetString(nav.TranslationKey ?? "", nav.FallbackText ?? ""));
                    break;
                }
                var window = p as SidePageWindow;
                if (window != null) { parts.Add(window.Text); break; }
                if (p.Parent == _sideHost && _sideOpen != null) { parts.Add(_sideOpen.Title); break; }
            }
            parts.Reverse();
            return string.Join(" / ", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        /// <summary>The notes page's picture: a note with a pencil.</summary>
        internal static void DrawNotesGlyph(Graphics g, Rectangle r, Color colour)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float s = Math.Min(r.Width, r.Height) / 24f;
            float x = r.Left + (r.Width - 24 * s) / 2, y = r.Top + (r.Height - 24 * s) / 2;
            using (var pen = new Pen(colour, 1.8f * s) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                // the sheet with a folded corner
                g.DrawLines(pen, new[]
                {
                    new PointF(x + 15 * s, y + 3 * s), new PointF(x + 5 * s, y + 3 * s), new PointF(x + 5 * s, y + 21 * s),
                    new PointF(x + 19 * s, y + 21 * s), new PointF(x + 19 * s, y + 13 * s)
                });
                g.DrawLine(pen, x + 8 * s, y + 9 * s, x + 13 * s, y + 9 * s);
                g.DrawLine(pen, x + 8 * s, y + 13 * s, x + 12 * s, y + 13 * s);
                g.DrawLine(pen, x + 8 * s, y + 17 * s, x + 14 * s, y + 17 * s);
                // the pencil
                g.DrawLine(pen, x + 21 * s, y + 4 * s, x + 14 * s, y + 11 * s);
                g.DrawLine(pen, x + 14 * s, y + 11 * s, x + 13 * s, y + 14 * s);
                g.DrawLine(pen, x + 13 * s, y + 14 * s, x + 16 * s, y + 13 * s);
                g.DrawLine(pen, x + 16 * s, y + 13 * s, x + 23 * s, y + 6 * s);
            }
        }
    }
}
