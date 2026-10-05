using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Dashboard;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// THE TWO PICK LISTS above axis rows - on an Axis Settings page and, for an
    /// Admin, on the Axis Monitor:
    ///
    ///   PLC axis items: [the row's groups of columns ...]   Motion monitor: [the module's values ...]
    ///
    /// PLC axis items: what the row shows of the PLC's - Manual movement, Torque
    /// monitor, Axis setting parameters, Reference activation, the Z distance,
    /// Alarm codes, Travel per revolution, Backlash - "All" (nothing ticked) is
    /// the whole row. Motion monitor: the simple motion module's values as more
    /// columns - "None" (nothing ticked) adds none. Both kept as the signed-in
    /// user's own (View Preferences); the rows follow at once.
    ///
    /// On a page the two are two bars: PLC axis items scrolls away with the
    /// columns it hides (they are at the left); the Motion monitor STAYS IN SIGHT
    /// while the page is scrolled sideways (Pin) - its values are ticked where
    /// their columns are, at the right end. On the Axis Monitor one bar has both.
    /// </summary>
    public class AxisPickBar : FlowLayoutPanel
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private readonly string _itemsKey, _motionKey;
        private readonly Label _itemsCaption, _motionCaption;
        private readonly bool _hasItems, _hasMotion;
        private bool _syncing;

        public PickList ItemsPick { get; private set; }
        public PickList MotionPick { get; private set; }

        /// <param name="items">With the PLC axis items list.</param>
        /// <param name="motion">With the Motion monitor list.</param>
        public AxisPickBar(string itemsKey, string motionKey, bool items = true, bool motion = true)
        {
            _itemsKey = itemsKey;
            _motionKey = motionKey;
            _hasItems = items;
            _hasMotion = motion;
            WrapContents = false;
            AutoSize = false;
            Padding = new Padding(0);
            Margin = new Padding(0);
            BackColor = Color.White;

            _itemsCaption = Caption(T("AxIt_Pick", "PLC axis items:"));
            ItemsPick = new PickList(UiStyle.Tips())
            {
                Name = "itemsPick", Size = new Size(300, 28), Margin = new Padding(0, 4, 18, 0),
                AllText = T("AxIt_All", "All")
            };
            ItemsPick.Changed += (s, e) => { if (!_syncing) KeepItems(); };

            _motionCaption = Caption(T("Mot_Pick", "Motion monitor:"));
            MotionPick = new PickList(UiStyle.Tips())
            {
                Name = "motionPick", Size = new Size(420, 28), Margin = new Padding(0, 4, 8, 0),
                AllText = T("Mot_PickNone", "None")
            };
            MotionPick.SetItems(SimpleMotion.Items.Select(i => new PickList.Item(
                i.Key, LanguageManager.GetString(i.TitleKey, i.Title) + "  (" + i.Ref + ")")), false);
            MotionPick.Changed += (s, e) => { if (!_syncing) KeepMotion(); };

            if (items) { Controls.Add(_itemsCaption); Controls.Add(ItemsPick); }
            if (motion) { Controls.Add(_motionCaption); Controls.Add(MotionPick); }
        }

        /// <summary>As wide as its lists (a bar of one list beside another).</summary>
        public int NaturalWidth { get { return GetPreferredSize(Size.Empty).Width; } }

        private static Label Caption(string text)
        {
            return new Label
            {
                AutoSize = true, Text = text, Font = UiStyle.Body, ForeColor = Color.FromArgb(60, 66, 76), Margin = new Padding(0, 9, 6, 0)
            };
        }

        /// <summary>What the rows have to offer: their items, and whether any is on a module (the Motion monitor).</summary>
        public void Offer(IList<string> items, bool motion)
        {
            _syncing = true;
            try
            {
                ItemsPick.SetItems(AxisSettingHMI.ItemKeys.Where(items.Contains).Select(k => new PickList.Item(k, AxisSettingHMI.ItemText(k))), false);
                _motionCaption.Visible = MotionPick.Visible = motion && _hasMotion;
                _itemsCaption.Visible = ItemsPick.Visible = _hasItems;
            }
            finally { _syncing = false; }
            Sync();
        }

        /// <summary>The ticks as kept (another user signed in, a pick changed elsewhere).</summary>
        public void Sync()
        {
            _syncing = true;
            try
            {
                var picked = ViewPreferences.PickOf(_itemsKey);
                var want = picked == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                          : new HashSet<string>(ItemsPick.Items.Select(i => i.Key).Where(picked.Contains), StringComparer.OrdinalIgnoreCase);
                if (!ItemsPick.Selected.SetEquals(want)) ItemsPick.Select(want);
                var motion = ViewPreferences.ListOf(_motionKey);
                if (!MotionPick.Selected.SetEquals(motion)) MotionPick.Select(motion);
            }
            finally { _syncing = false; }
        }

        private void KeepItems()
        {
            var chosen = ItemsPick.Selected;
            var kept = ViewPreferences.PickOf(_itemsKey);
            var offered = new HashSet<string>(ItemsPick.Items.Select(i => i.Key), StringComparer.OrdinalIgnoreCase);
            // Nothing ticked: All. Otherwise the ticks - and what another page's rows
            // had ticked that these rows do not offer stays ticked for that page.
            IEnumerable<string> keys = chosen.Count == 0 ? new string[0]
                : AxisSettingHMI.ItemKeys.Where(k => chosen.Contains(k) || (kept != null && !offered.Contains(k) && kept.Contains(k)));
            var list = keys.ToList();
            bool same = list.Count == 0 ? kept == null : kept != null && kept.SetEquals(list);
            if (!same) ViewPreferences.SetPick(_itemsKey, list);
        }

        private void KeepMotion()
        {
            var chosen = MotionPick.Selected;
            var keys = SimpleMotion.Items.Where(i => chosen.Contains(i.Key)).Select(i => i.Key).ToList();
            if (keys.SequenceEqual(ViewPreferences.ListOf(_motionKey), StringComparer.OrdinalIgnoreCase)) return;
            ViewPreferences.SetList(_motionKey, keys);
        }

        // ---- in sight while the page scrolls sideways --------------------------------

        private Control _page;
        private int _pinX;
        private bool _pinning;

        /// <summary>
        /// Keeps the bar at <paramref name="clientX"/> of the page while the page
        /// scrolls sideways (a scroll moves every child: the bar is put back), and
        /// as wide as the page shows - so pinning it never stretches the page.
        /// </summary>
        public void Pin(Control page, int clientX)
        {
            _page = page;
            _pinX = clientX;
            LocationChanged += (s, e) => KeepPinned();
            page.Resize += (s, e) => FitWidth();
            FitWidth();
        }

        /// <summary>Where the bar is kept (tests).</summary>
        public int PinnedX { get { return _pinX; } }

        private void KeepPinned()
        {
            if (_pinning || _page == null || Left == _pinX) return;
            _pinning = true;
            try { Left = _pinX; }
            finally { _pinning = false; }
        }

        private void FitWidth()
        {
            if (_page == null) return;
            int want = Math.Max(NaturalWidth, UiScale.For(_page, 200));
            int room = _page.ClientSize.Width - _pinX - UiScale.For(_page, 4);
            Width = Math.Max(UiScale.For(_page, 200), Math.Min(want, room));
        }
    }
}
