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
    /// columns it hides (they are at the left); the Motion monitor scrolls with
    /// the page until it reaches the page's left side and STAYS THERE while the
    /// page scrolls on (Pin) - its values are ticked where their columns are, at
    /// the right end. On the Axis Monitor one bar has both.
    /// </summary>
    /// <summary>
    /// THE AXIS COLUMN'S HEADING AS THE AXES FILTER (8 October: "a multiple
    /// selection combo ... to hide/show the axis ... just like filtered tables
    /// do"): a click on "Axis" opens the list of the page's axes to tick; the
    /// heading shows a drop-down arrow, and - while some axes are hidden - how many
    /// of them are shown, in the colour of a filter that is on.
    /// </summary>
    public static class AxisHeadingFilter
    {
        private sealed class Hook { public PickList Pick; public Action BeforeOpen; public Func<string> Tip; public bool Count = true; public int MinItems = 1; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Label, Hook> _hooks =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Label, Hook>();

        /// <summary>The list the heading opens (tests).</summary>
        public static PickList PickOf(Label heading)
        {
            Hook h;
            return heading != null && _hooks.TryGetValue(heading, out h) ? h.Pick : null;
        }

        /// <summary>
        /// Makes <paramref name="heading"/> open <paramref name="pick"/> (again: the list it opens now).
        /// <paramref name="beforeOpen"/>: the axes offered again just before it opens (16 October: an
        /// axis whose visibility conditions are not met is not offered - ticked, it could not be seen).
        /// </summary>
        /// <param name="tip">What the list is, before what it shows in the heading's tooltip (null: the axes').</param>
        /// <param name="showCount">"2/3" before the arrow while some are ticked (not on a narrow heading: the positions').</param>
        /// <param name="minItems">A list of fewer: no arrow, no click (the positions' list of the Position alone).</param>
        public static void Attach(Label heading, PickList pick, Action beforeOpen = null, Func<string> tip = null, bool showCount = true, int minItems = 1)
        {
            if (heading == null || pick == null) return;
            Hook hook;
            if (_hooks.TryGetValue(heading, out hook))
            {
                if (hook.Pick != pick) { hook.Pick = pick; pick.Changed += (s, e) => { if (!heading.IsDisposed) heading.Invalidate(); }; }
                if (beforeOpen != null) hook.BeforeOpen = beforeOpen;
                if (tip != null) hook.Tip = tip;
                hook.Count = showCount;
                hook.MinItems = minItems;
                heading.Cursor = pick.Items.Count >= Math.Max(1, minItems) ? Cursors.Hand : Cursors.Default;
                heading.Invalidate();
                return;
            }
            hook = new Hook { Pick = pick, BeforeOpen = beforeOpen, Tip = tip, Count = showCount, MinItems = minItems };
            _hooks.Add(heading, hook);
            pick.Changed += (s, e) => { if (!heading.IsDisposed) heading.Invalidate(); };
            heading.Cursor = pick.Items.Count >= Math.Max(1, minItems) || pick.Items.Count == 0 && minItems <= 1 ? Cursors.Hand : Cursors.Default;
            heading.Click += (s, e) =>
            {
                var before = hook.BeforeOpen;
                if (before != null) { try { before(); } catch (Exception ex) { AuditLogger.Log("the axes list: " + ex.Message); } }
                var p = hook.Pick;
                if (p != null && p.Items.Count > 0 && p.Items.Count >= hook.MinItems && !heading.IsDisposed) p.DropUnder(heading);
            };
            UiStyle.HoverTip(UiStyle.Tips(), heading, () =>
            {
                var p = hook.Pick;
                if (p == null || p.Items.Count < hook.MinItems) return "";
                var shown = p.Selected.Count == 0 ? p.AllText : string.Join("\n", p.SelectedTexts);
                return (hook.Tip != null ? hook.Tip()
                        : LanguageManager.GetString("Tip_AxShow", "The axes shown - a click: tick the ones to show (nothing ticked: all of them). The ones shown move up together.")) +
                       "\n\n" + shown;
            });
            heading.Paint += (s, e) =>
            {
                var p = hook.Pick;
                bool offered = p != null && p.Items.Count > 0 && p.Items.Count >= hook.MinItems;
                var cursor = offered ? Cursors.Hand : Cursors.Default;
                if (heading.Cursor != cursor) heading.Cursor = cursor;
                if (!offered) return;
                bool on = p.Selected.Count > 0;
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                int a = Math.Max(3, UiScale.For(heading, 4));
                float cx = heading.ClientSize.Width - heading.Padding.Right - a - UiScale.For(heading, 4), cy = heading.ClientSize.Height / 2f;
                var colour = on ? Color.FromArgb(0, 120, 215) : Color.FromArgb(110, 116, 124);
                using (var pen = new Pen(colour, Math.Max(1.5f, a / 2f)) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round })
                    g.DrawLines(pen, new[] { new PointF(cx - a, cy - a / 2f), new PointF(cx, cy + a / 2f), new PointF(cx + a, cy - a / 2f) });
                if (!on || !hook.Count) return;
                // some hidden: how many shown, before the arrow
                var count = p.Selected.Count + "/" + p.Items.Count;
                var f = UiStyle.Small;
                var size = TextRenderer.MeasureText(count, f);
                TextRenderer.DrawText(g, count, f, new Point((int)(cx - a - UiScale.For(heading, 4) - size.Width), (int)(cy - size.Height / 2f)), colour,
                                      TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            };
            heading.Invalidate();
        }
    }

    public class AxisPickBar : FlowLayoutPanel
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private readonly string _itemsKey, _motionKey, _axesKey, _positionsKey;
        private readonly bool _hasItems, _hasMotion;
        private bool _syncing, _listsHidden;

        public PickList ItemsPick { get; private set; }
        public PickList MotionPick { get; private set; }
        /// <summary>
        /// The axes shown (8 October): nothing ticked, all; null on a bar without it.
        /// Not on the bar - three lists in one line did not fit a page at a larger
        /// screen scale - but opened from the Axis column's heading, as a filtered
        /// table's column (AxisHeadingFilter).
        /// </summary>
        public PickList AxesPick { get; private set; }

        /// <summary>The axes shown were changed by the user (once, when the list closes).</summary>
        public event EventHandler AxesChanged;

        /// <summary>
        /// THE POSITIONS SHOWN SIDE BY SIDE (22 October): the Position, the servo encoder's value,
        /// the load-side encoder's - the ones the rows have; nothing ticked: the Position. Opened
        /// from the Position column's heading, as the Axes list from the Axis column's; null on a
        /// bar without it.
        /// </summary>
        public PickList PositionsPick { get; private set; }

        /// <param name="items">With the PLC axis items list.</param>
        /// <param name="motion">With the Motion monitor list.</param>
        /// <param name="axesKey">With the Axes list, kept under this View Preferences key (null: none).</param>
        /// <param name="positionsKey">With the Positions list, kept under this View Preferences key (null: none).</param>
        public AxisPickBar(string itemsKey, string motionKey, bool items = true, bool motion = true, string axesKey = null, string positionsKey = null)
        {
            _itemsKey = itemsKey;
            _motionKey = motionKey;
            _axesKey = axesKey;
            _positionsKey = positionsKey;
            _hasItems = items;
            _hasMotion = motion;
            WrapContents = false;
            AutoSize = false;
            Padding = new Padding(0);
            Margin = new Padding(0);
            BackColor = Color.White;

            // No caption beside the lists (8 October: "they get detached from their titles
            // when the bottom scroll bar is moved ... show these descriptions as the
            // combo's first member"): each list names itself (PickList.Heading).
            ItemsPick = new PickList(UiStyle.Tips())
            {
                Name = "itemsPick", Size = new Size(240, 28), Margin = new Padding(0, 4, 18, 0),
                AllText = T("AxIt_All", "All"),
                Heading = T("AxIt_Pick", "PLC axis items"),
                // The rows are laid out again once, when the list closes - not at every tick.
                ApplyOnClose = true
            };
            ItemsPick.Changed += (s, e) => { if (!_syncing) KeepItems(); };

            MotionPick = new PickList(UiStyle.Tips())
            {
                Name = "motionPick", Size = new Size(260, 28), Margin = new Padding(0, 4, 8, 0),
                AllText = T("Mot_PickNone", "None"),
                Heading = T("Mot_Pick", "Motion monitor items"),
                ApplyOnClose = true
            };
            MotionPick.SetItems(SimpleMotion.MonitorItems.Select(i => new PickList.Item(
                i.Key, LanguageManager.GetString(i.TitleKey, i.Title) + "  (" + i.Ref + ")")), false);
            MotionPick.Changed += (s, e) => { if (!_syncing) KeepMotion(); };
            if (positionsKey != null)
            {
                PositionsPick = new PickList(null)
                {
                    Name = "positionsPick", Size = new Size(220, 28),
                    AllText = AxisSettingHMI.PositionText("Position"),
                    ApplyOnClose = true
                };
                // the Position alone until the rows say what they have (OfferPositions)
                PositionsPick.SetItems(new[] { new PickList.Item("Position", AxisSettingHMI.PositionText("Position")) }, false);
                PositionsPick.Changed += (s, e) =>
                {
                    if (_syncing) return;
                    // the ones ticked; what is kept but not offered here (another page's) stays
                    var chosen = PositionsPick.Selected;
                    var offered = new HashSet<string>(PositionsPick.Items.Select(i => i.Key), StringComparer.OrdinalIgnoreCase);
                    var kept = ViewPreferences.ListOf(_positionsKey);
                    var keys = AxisSettingHMI.PositionKinds.Where(k => chosen.Contains(k) || (!offered.Contains(k) && kept.Contains(k))).ToList();
                    // only the Position: as nothing ticked
                    if (keys.Count == 1 && keys[0] == "Position") keys.Clear();
                    if (!keys.SequenceEqual(kept, StringComparer.OrdinalIgnoreCase)) ViewPreferences.SetList(_positionsKey, keys);
                };
                Disposed += (s, e) => PositionsPick.Dispose();
            }
            FitLists();

            // both in one bar (the Axis Monitor): the same room between them as on a page
            if (items && motion) MotionPick.Margin = new Padding(GapAfterItems, 4, 8, 0);
            if (axesKey != null)
            {
                AxesPick = new PickList(null)
                {
                    Name = "axesPick", Size = new Size(220, 28),
                    AllText = T("AxShow_All", "All axes"),
                    ApplyOnClose = true
                };
                AxesPick.Changed += (s, e) =>
                {
                    if (_syncing) return;
                    Kaban.UI.Helpers.ShownPick.KeepFrom(AxesPick, _axesKey);
                    var h = AxesChanged;
                    if (h != null) h(this, EventArgs.Empty);
                };
                Disposed += (s, e) => AxesPick.Dispose();
            }
            if (items) Controls.Add(ItemsPick);
            if (motion) Controls.Add(MotionPick);
            LanguageManager.LanguageChanged += OnLanguageChanged;
            Disposed += (s, e) => LanguageManager.LanguageChanged -= OnLanguageChanged;
        }

        /// <summary>The lists' headings and words in the language now shown.</summary>
        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { try { BeginInvoke(new EventHandler(OnLanguageChanged), sender, e); } catch { } return; }
            _syncing = true;
            try
            {
                ItemsPick.Heading = T("AxIt_Pick", "PLC axis items");
                ItemsPick.AllText = T("AxIt_All", "All");
                ItemsPick.SetItems(ItemsPick.Items.Select(i => new PickList.Item(i.Key, AxisSettingHMI.ItemText(i.Key))), true);
                MotionPick.Heading = T("Mot_Pick", "Motion monitor items");
                MotionPick.AllText = T("Mot_PickNone", "None");
                MotionPick.SetItems(SimpleMotion.MonitorItems.Select(i => new PickList.Item(
                    i.Key, LanguageManager.GetString(i.TitleKey, i.Title) + "  (" + i.Ref + ")")), true);
                if (AxesPick != null) AxesPick.AllText = T("AxShow_All", "All axes");
                if (PositionsPick != null)
                {
                    PositionsPick.AllText = AxisSettingHMI.PositionText("Position");
                    PositionsPick.SetItems(PositionsPick.Items.Select(i => new PickList.Item(i.Key, AxisSettingHMI.PositionText(i.Key))), true);
                }
            }
            finally { _syncing = false; }
            FitLists();
            Place();
        }

        /// <summary>Each list as wide as its heading and its state need (at this screen scale).</summary>
        private void FitLists()
        {
            ItemsPick.Width = Math.Max(UiScale.Px(140), ItemsPick.NaturalWidth);
            MotionPick.Width = Math.Max(UiScale.Px(140), MotionPick.NaturalWidth);
        }

        /// <summary>As wide as its lists (a bar of one list beside another).</summary>
        public int NaturalWidth { get { return GetPreferredSize(Size.Empty).Width; } }

        /// <summary>What the rows have to offer: their items, and whether any is on a module (the Motion monitor).</summary>
        public void Offer(IList<string> items, bool motion)
        {
            _syncing = true;
            try
            {
                ItemsPick.SetItems(AxisSettingHMI.ItemKeys.Where(items.Contains).Select(k => new PickList.Item(k, AxisSettingHMI.ItemText(k))), false);
                MotionPick.Visible = motion && _hasMotion && !_listsHidden;
                ItemsPick.Visible = _hasItems && !_listsHidden;
                FitLists();
            }
            finally { _syncing = false; }
            Sync();
        }

        /// <summary>
        /// The PLC axis items and the Motion monitor hidden (the Axis Monitor for an
        /// Operator: what the rows show is an Admin's choice), the Axes list left.
        /// </summary>
        public void HideLists(bool hide)
        {
            _listsHidden = hide;
            if (hide) ItemsPick.Visible = MotionPick.Visible = false;
        }

        private bool _listsLocked;

        /// <summary>
        /// The PLC axis items and the Motion monitor shown greyed, not opened, saying why (26 October: the
        /// Axis Monitor for Maintenance - "The maintenance level should see it greyed, not the operator
        /// though"): ticked as the rows show them - the torque monitor, no motion monitor item - not as the
        /// Admin chose for himself.
        /// </summary>
        public void LockLists(bool locked, string why)
        {
            _listsLocked = locked;
            ItemsPick.LockedWhy = locked ? why : "";
            MotionPick.LockedWhy = locked ? why : "";
            Sync();
        }

        /// <summary>The rows the Axes list offers (key: name@station, text: the name).</summary>
        public void OfferAxes(IEnumerable<PickList.Item> axes)
        {
            if (AxesPick == null) return;
            Kaban.UI.Helpers.ShownPick.Offer(AxesPick, _axesKey, axes, ref _syncing);
        }

        /// <summary>
        /// The positions the rows have (the Position always; the servo encoder's value with an
        /// axis on a simple motion module, the load position with a load-side encoder), ticked as kept.
        /// </summary>
        public void OfferPositions(IEnumerable<string> kinds)
        {
            if (PositionsPick == null) return;
            var want = AxisSettingHMI.PositionKinds.Where(k => k == "Position" || kinds.Contains(k)).ToList();
            _syncing = true;
            try
            {
                if (!want.SequenceEqual(PositionsPick.Items.Select(i => i.Key)))
                    PositionsPick.SetItems(want.Select(k => new PickList.Item(k, AxisSettingHMI.PositionText(k))), false);
                var ticked = TickedPositions(want);
                if (!PositionsPick.Selected.SetEquals(ticked)) PositionsPick.Select(ticked);
            }
            finally { _syncing = false; }
        }

        /// <summary>The kept positions among the ones offered (the Position alone: nothing ticked).</summary>
        private List<string> TickedPositions(IList<string> offered)
        {
            var kept = ViewPreferences.ListOf(_positionsKey);
            var ticked = offered.Where(kept.Contains).ToList();
            return ticked.Count == 1 && ticked[0] == "Position" ? new List<string>() : ticked;
        }

        /// <summary>True when the row <paramref name="key"/> is shown (nothing ticked: all are).</summary>
        public bool ShowsAxis(string key)
        {
            if (AxesPick == null) return true;
            var ticked = AxesPick.Selected;
            return ticked.Count == 0 || ticked.Contains(key ?? "");
        }

        /// <summary>The ticks as kept (another user signed in, a pick changed elsewhere).</summary>
        public void Sync()
        {
            _syncing = true;
            try
            {
                // locked (the Axis Monitor below Admin): what the rows show - the torque monitor, as it was
                var picked = _listsLocked ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Torque" } : ViewPreferences.PickOf(_itemsKey);
                var want = picked == null ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                                          : new HashSet<string>(ItemsPick.Items.Select(i => i.Key).Where(picked.Contains), StringComparer.OrdinalIgnoreCase);
                if (!ItemsPick.Selected.SetEquals(want)) ItemsPick.Select(want);
                var motion = _listsLocked ? new List<string>() : ViewPreferences.ListOf(_motionKey);
                if (!MotionPick.Selected.SetEquals(motion)) MotionPick.Select(motion);
                if (AxesPick != null)
                {
                    var axes = Kaban.UI.Helpers.ShownPick.Ticked(_axesKey, AxesPick.Items.Select(i => i.Key));
                    if (!AxesPick.Selected.SetEquals(axes)) AxesPick.Select(axes);
                }
                if (PositionsPick != null)
                {
                    var ticked = TickedPositions(PositionsPick.Items.Select(i => i.Key).ToList());
                    if (!PositionsPick.Selected.SetEquals(ticked)) PositionsPick.Select(ticked);
                }
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
            var keys = SimpleMotion.MonitorItems.Where(i => chosen.Contains(i.Key)).Select(i => i.Key).ToList();
            if (keys.SequenceEqual(ViewPreferences.ListOf(_motionKey), StringComparer.OrdinalIgnoreCase)) return;
            ViewPreferences.SetList(_motionKey, keys);
        }

        // ---- in sight while the page scrolls sideways --------------------------------

        private Control _page;                // where the bar is (its width: the room it shows)
        private ScrollableControl _scroller;  // what scrolls (the page, or a frame's body)
        private int _homeX, _stickX;
        private bool _pinning, _hooked;
        private EventHandler _onScroll, _onResize;
        private ScrollEventHandler _onScrollBar;

        /// <summary>
        /// Keeps the bar in sight while the page scrolls sideways, as a heading
        /// that sticks: it scrolls with the page from where it stands
        /// (<paramref name="homeX"/>, in the page's unscrolled terms) until it
        /// reaches the page's left side (<paramref name="stickX"/>, where the
        /// page's content starts) and stays there while the page scrolls on -
        /// at <paramref name="homeX"/> plus the scroll beyond that point. No
        /// wider than the page shows from where it is, so it never stretches
        /// the page.
        /// </summary>
        public void Pin(Control page, int homeX, int stickX)
        {
            Pin(page, page as ScrollableControl, homeX, stickX);
        }

        /// <summary>
        /// As Pin(page ...), the bar being on <paramref name="host"/> and the scroll
        /// that of <paramref name="scroller"/> - a page in a frame (AxisPageFrame):
        /// the bar in the heading, which does not scroll, the rows in the body,
        /// which does (the frame calls Place as it scrolls). Pinned again, it
        /// leaves what it followed before.
        /// </summary>
        public void Pin(Control host, ScrollableControl scroller, int homeX, int stickX)
        {
            if (_page != null && _onResize != null) _page.Resize -= _onResize;
            if (_scroller != null && _onScrollBar != null) _scroller.Scroll -= _onScrollBar;
            _page = host;
            _scroller = scroller;
            _homeX = homeX;
            _stickX = Math.Min(stickX, homeX);
            if (!_hooked)
            {
                _hooked = true;
                // a scroll moves every child (the bar too, on a page): it is put where it belongs
                _onScroll = (s, e) => Place();
                LocationChanged += _onScroll;
            }
            _onResize = (s, e) => Place();
            _onScrollBar = (s, e) => Place();
            host.Resize += _onResize;
            if (scroller != null) scroller.Scroll += _onScrollBar;
            Place();
        }

        private AxisPickBar _follow;

        /// <summary>The room between the PLC axis items list and the Motion monitor items list (factor 1).</summary>
        public const int GapAfterItems = 28;

        /// <summary>
        /// Stands right of <paramref name="left"/> (the PLC axis items list), with
        /// room between (GapAfterItems): its place is taken from that list
        /// whenever it is placed - not kept in pixels, which at a larger screen
        /// scale were the unscaled ones and put it over that list (6 October).
        /// </summary>
        public void StandRightOf(AxisPickBar left)
        {
            _follow = left;
            Place();
        }

        /// <summary>Where the list it stands right of ends, in the page's unscrolled terms, and the room after it.</summary>
        private bool FollowHome(out int home)
        {
            home = 0;
            if (_follow == null || _follow.IsDisposed || _page == null || !_follow.Visible) return false;
            // as wide as what it holds: a bar measured before it was scaled was left too wide
            int natural = _follow.NaturalWidth;
            if (natural > 0 && _follow.Width != natural) _follow.Width = natural;
            int x = 0;
            Control c = _follow;
            for (; c != null && c != _page; c = c.Parent) x += c.Left;
            if (c == null) return false;
            // where it is shown now, plus the scroll: on the page and in a frame's
            // heading (whose content follows the rows) alike, the unscrolled place
            int scrolled = _scroller != null ? -_scroller.AutoScrollPosition.X : 0;
            home = x + natural + scrolled + UiScale.For(_page, GapAfterItems);
            return true;
        }

        /// <summary>Where the bar stands with the page unscrolled, and where it sticks (tests).</summary>
        public int HomeX { get { int h; return FollowHome(out h) ? h : _homeX; } }
        public int StickX { get { return _stickX; } }

        /// <summary>Where the bar belongs now, in the page's window: with the page until the left side, then there.</summary>
        public int PlaceX
        {
            get
            {
                int scrolled = _scroller != null ? -_scroller.AutoScrollPosition.X : 0;
                int home;
                if (!FollowHome(out home)) home = _homeX;
                return Math.Max(Math.Min(_stickX, home), home - scrolled);
            }
        }

        /// <summary>The bar where it belongs now (the page scrolled, or was laid out).</summary>
        public void Place()
        {
            if (_pinning || _page == null || _page.IsDisposed) return;
            int x = PlaceX;
            // the list as wide as it needs, narrowed when the window shows less (a
            // larger screen scale): its arrow stays in sight rather than cut off
            int design = Math.Max(UiScale.Px(140), MotionPick.NaturalWidth);
            bool narrowable = _hasMotion && MotionPick.Visible;
            int full = narrowable ? NaturalWidth - MotionPick.Width + design : NaturalWidth;
            int want = Math.Max(full, UiScale.For(_page, 200));
            int room = _page.ClientSize.Width - x - UiScale.For(_page, 4);
            int w = Math.Max(UiScale.For(_page, 200), Math.Min(want, room));
            if (narrowable)
            {
                int pick = Math.Max(UiScale.For(this, 160), design - Math.Max(0, full - w));
                if (MotionPick.Width != pick) MotionPick.Width = pick;
            }
            if (Left == x && Width == w) return;
            _pinning = true;
            // Without laying the page out again: a layout scrolls a page back to
            // the field that has the focus (seen in Mono), and the bar, inside the
            // window, changes nothing of the page's extent.
            _page.SuspendLayout();
            try { SetBounds(x, Top, w, Height, BoundsSpecified.X | BoundsSpecified.Width); }
            finally { _page.ResumeLayout(false); _pinning = false; }
        }
    }
}
