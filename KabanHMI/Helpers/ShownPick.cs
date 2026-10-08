using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Helpers;
using Kaban.UI.Dashboard;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// WHAT A PAGE SHOWS OF ITS ROWS (8 October: "a multiple selection combo to the
    /// top of axis setting and axis monitor pages for the user to be able to
    /// hide/show the axis. The shown one should be moved up to fill the hidden
    /// ones place"): a PickList of the page's rows - nothing ticked shows them all,
    /// as the dashboard's filters - kept as the signed-in user's own (View
    /// Preferences, one list for every page of a kind). A page's ticks are read
    /// and written among ITS rows only: another page's ticks in the same list stay
    /// as they are, and a page none of whose rows is ticked shows them all.
    /// </summary>
    public static class ShownPick
    {
        /// <summary>The keys of <paramref name="offered"/> ticked (none: all shown).</summary>
        public static HashSet<string> Ticked(string prefKey, IEnumerable<string> offered)
        {
            var picked = ViewPreferences.PickOf(prefKey);
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (picked == null || offered == null) return set;
            foreach (var k in offered) if (picked.Contains(k)) set.Add(k);
            return set;
        }

        /// <summary>True when the row <paramref name="key"/> of a page offering <paramref name="offered"/> is shown.</summary>
        public static bool Shows(string prefKey, IEnumerable<string> offered, string key)
        {
            var ticked = Ticked(prefKey, offered);
            return ticked.Count == 0 || ticked.Contains(key ?? "");
        }

        /// <summary>Keeps a page's ticks, leaving the other pages' as they are.</summary>
        public static void Keep(string prefKey, IEnumerable<string> offered, IEnumerable<string> chosen)
        {
            var mine = new HashSet<string>(offered ?? new string[0], StringComparer.OrdinalIgnoreCase);
            var picked = ViewPreferences.PickOf(prefKey);
            var keys = new List<string>();
            if (picked != null) keys.AddRange(picked.Where(k => !mine.Contains(k)));
            if (chosen != null) keys.AddRange(chosen.Where(mine.Contains));
            var now = keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            bool same = now.Count == 0 ? picked == null : picked != null && picked.SetEquals(now);
            if (!same) ViewPreferences.SetPick(prefKey, now);
        }

        /// <summary>
        /// A pick list showing a page's rows (<paramref name="items"/>): its ticks as
        /// kept, and kept as ticked. <paramref name="changed"/> after a change of the
        /// user's (once, when the list closes).
        /// </summary>
        public static void Offer(PickList pick, string prefKey, IEnumerable<PickList.Item> items, ref bool syncing)
        {
            var list = (items ?? new PickList.Item[0]).ToList();
            bool was = syncing;
            syncing = true;
            try
            {
                pick.SetItems(list, false);
                var want = Ticked(prefKey, list.Select(i => i.Key));
                if (!pick.Selected.SetEquals(want)) pick.Select(want);
            }
            finally { syncing = was; }
        }

        /// <summary>The pick list's ticks kept (from its Changed).</summary>
        public static void KeepFrom(PickList pick, string prefKey)
        {
            Keep(prefKey, pick.Items.Select(i => i.Key), pick.Selected);
        }
    }
}
