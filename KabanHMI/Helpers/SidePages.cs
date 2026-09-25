using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// A page for the SIDE PANEL: opened from the bar at the right edge of the
    /// window, shown beside the page on display - which narrows to make room, so
    /// nothing is covered.
    /// </summary>
    public sealed class SidePage
    {
        /// <summary>Unique: remembers the page's panel and window between openings.</summary>
        public string Key = "";
        public string TitleKey = "";
        public string TitleFallback = "";
        /// <summary>A sub-machine's page carries the machine's name in its title.</summary>
        public string MachineName = "";
        /// <summary>The bar's picture, drawn (see ToolGlyphs) - or Icon, an image.</summary>
        public Action<Graphics, Rectangle, Color> Glyph;
        public Func<object> Icon;
        /// <summary>Builds the page, once; it is kept while the program runs (closing the panel only hides it).</summary>
        public Func<Control> Create;
        public SecurityLevel RequiredLevel = SecurityLevel.None;
        /// <summary>Lower numbers are higher on the bar.</summary>
        public int SortOrder = 100;
        /// <summary>Can be taken out of the panel into a window of its own (a second screen).</summary>
        public bool CanPopOut = true;
        /// <summary>A machine's page (ScreenRoles.SidePage): what Find opens.</summary>
        public MachineScreen Screen;

        public string Title
        {
            get
            {
                string t = LanguageManager.GetString(TitleKey ?? "", TitleFallback ?? "");
                return string.IsNullOrEmpty(MachineName) ? t : MachineName + " - " + t;
            }
        }
    }

    /// <summary>
    /// The side pages: the program's own (registered in code - the PLC Monitor,
    /// Inputs / Outputs) and each mounted machine's (a MachineScreen with
    /// Role = ScreenRoles.SidePage, designed in Visual Studio like any page).
    /// </summary>
    public static class SidePages
    {
        private static readonly List<SidePage> _registered = new List<SidePage>();

        /// <summary>Adds a page from code; a page with the same key is replaced.</summary>
        public static void Register(SidePage page)
        {
            if (page == null || string.IsNullOrEmpty(page.Key)) throw new ArgumentException("A side page needs a key.");
            _registered.RemoveAll(p => string.Equals(p.Key, page.Key, StringComparison.OrdinalIgnoreCase));
            _registered.Add(page);
        }

        public static void Unregister(string key)
        {
            _registered.RemoveAll(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Every side page, in bar order.</summary>
        public static List<SidePage> All()
        {
            var list = new List<SidePage>(_registered);
            foreach (var kv in MachineRegistry.SidePages())
            {
                var mounted = kv.Key;
                var screen = kv.Value;
                if (screen.CreateView == null) continue;
                list.Add(new SidePage
                {
                    Key = mounted.Module.ModuleId + "." + (string.IsNullOrEmpty(screen.TitleKey) ? screen.TitleFallback : screen.TitleKey),
                    TitleKey = screen.TitleKey,
                    TitleFallback = screen.TitleFallback,
                    MachineName = mounted.IsMain ? "" : Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(mounted),
                    Icon = screen.Icon,
                    Create = () => screen.CreateView() as Control,
                    RequiredLevel = screen.RequiredLevel,
                    SortOrder = screen.SortOrder,
                    Screen = screen
                });
            }
            // Stable: pages with the same order keep the order they were added in.
            return list.Select((p, i) => new { p, i }).OrderBy(x => x.p.SortOrder).ThenBy(x => x.i).Select(x => x.p).ToList();
        }

        /// <summary>The page with this key, or null.</summary>
        public static SidePage Find(string key)
        {
            return All().FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
        }
    }
}
