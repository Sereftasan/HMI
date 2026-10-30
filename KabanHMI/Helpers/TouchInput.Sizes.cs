using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Kaban.UI.Controls;

namespace Kaban.UI.Helpers
{
    // ======================================================================
    // A FINGER'S SIZE (29 October, the plan's 2: "a Touch option making
    // menus, rows, checkboxes and toolbar buttons larger")
    // ----------------------------------------------------------------------
    // On the machines' 17" and 19" screens at 1920 x 1080 a pixel is about
    // 0.2 mm: a 24 px menu entry is 5 mm, a 32 px toolbar button 6.5 mm - a
    // finger (8 - 10 mm) takes the one beside it too. In touch mode:
    //   - a menu's entries 42 px high (every menu of the program: they are
    //     handed over to UiStyle as they open);
    //   - a table's rows 40 px (34 / 46 for Compact / Large - ViewPreferences);
    //   - a tick box's or a round option's mark 22 px, the box as tall;
    //   - the toolbars' buttons 40 px high - where nothing under a button is
    //     covered by it (a bar laid out by hand keeps its own);
    //   - a tab 8 px taller.
    // All of it back as it was in mouse mode.
    // ======================================================================
    public static partial class TouchInput
    {
        /// <summary>A toolbar button's height for a finger, at factor 1.</summary>
        public const int ButtonHeight = 40;
        /// <summary>A tick box's mark for a finger, at factor 1.</summary>
        public const int BoxSize = 22;
        /// <summary>How much taller a tab is for a finger, each side, at factor 1.</summary>
        public const int TabExtra = 4;

        // ---- menus ----------------------------------------------------------------------------

        private sealed class Kept<T> { public T Value; public Kept(T v) { Value = v; } }
        private static readonly ConditionalWeakTable<ToolStripItem, Kept<Padding>> _itemPadding = new ConditionalWeakTable<ToolStripItem, Kept<Padding>>();

        /// <summary>
        /// A menu's entries at the mode's size: a finger's height in touch mode, their own in
        /// mouse mode. Called as a menu opens (UiStyle hands every menu over).
        /// </summary>
        public static void SizeMenu(ToolStrip strip)
        {
            if (strip == null || strip.IsDisposed || !(strip is ToolStripDropDown)) return;
            bool on = On;
            int want = UiScale.Px(MenuEntryHeight);
            foreach (ToolStripItem item in strip.Items)
            {
                if (!(item is ToolStripMenuItem || item is ToolStripButton)) continue;
                Kept<Padding> kept;
                if (!_itemPadding.TryGetValue(item, out kept))
                {
                    if (!on) continue;                       // never changed: nothing to give back
                    kept = new Kept<Padding>(item.Padding);
                    _itemPadding.Add(item, kept);
                }
                var own = kept.Value;
                if (item.Padding != own) item.Padding = own;
                if (!on) continue;
                int now = item.GetPreferredSize(Size.Empty).Height;
                int extra = want - now;
                if (extra > 0) item.Padding = new Padding(own.Left, own.Top + extra / 2, own.Right, own.Bottom + extra - extra / 2);
            }
        }

        /// <summary>An entry's height as the menu lays it out (tests).</summary>
        public static int EntryHeight(ToolStripItem item) { return item.GetPreferredSize(Size.Empty).Height; }

        // ---- the controls met ---------------------------------------------------------------

        private static readonly ConditionalWeakTable<Control, Kept<Size>> _buttons = new ConditionalWeakTable<Control, Kept<Size>>();
        private static readonly List<WeakReference> _sized = new List<WeakReference>();
        private static readonly ConditionalWeakTable<Control, Kept<int>> _grown = new ConditionalWeakTable<Control, Kept<int>>();
        private static readonly ConditionalWeakTable<TabControl, Kept<Point>> _tabPadding = new ConditionalWeakTable<TabControl, Kept<Point>>();
        private static readonly ConditionalWeakTable<ButtonBase, Kept<Padding>> _boxPadding = new ConditionalWeakTable<ButtonBase, Kept<Padding>>();

        /// <summary>
        /// A toolbar's button (UiStyle.ToolButton, IconButton, TextButton): its height for a finger in
        /// touch mode - once it is on screen, laid out, and again when the mode changes.
        /// </summary>
        public static void ToolbarButton(Button b)
        {
            if (b == null) return;
            lock (_sized) _sized.Add(new WeakReference(b));
            b.HandleCreated += (s, e) =>
            {
                // after the window is laid out and scaled (UiScale works as it loads)
                try { b.BeginInvoke((Action)(() => SizeButton(b))); } catch { }
            };
        }

        private static void PrepareSizes(Control c)
        {
            var tabs = c as TabControl;
            if (tabs != null) { SizeTabs(tabs); return; }
            var box = c as ButtonBase;
            Kept<Padding> seen;
            if (box != null && IsPlainBox(box) && !_boxPadding.TryGetValue(box, out seen))
            {
                _boxPadding.Add(box, new Kept<Padding>(box.Padding));
                // a tick box is an on / off switch, with a mouse too (30 October); a round option a larger circle by touch
                if (box is CheckBox) box.Paint += PaintSwitchBox; else box.Paint += PaintBigBox;
                SizeBox(box);
            }
        }

        private static void ApplySizesToAll()
        {
            List<WeakReference> live;
            lock (_sized) { _sized.RemoveAll(w => !w.IsAlive || ((Control)w.Target).IsDisposed); live = new List<WeakReference>(_sized); }
            foreach (var w in live) { var b = w.Target as Button; if (b != null && b.IsHandleCreated) SizeButton(b); }
            foreach (var c in AllControls())
            {
                var tabs = c as TabControl;
                if (tabs != null) SizeTabs(tabs);
                var box = c as ButtonBase;
                Kept<Padding> kept;
                if (box != null && _boxPadding.TryGetValue(box, out kept)) { SizeBox(box); box.Invalidate(); }
            }
        }

        // ---- toolbar buttons --------------------------------------------------------------------

        /// <summary>A button's height as the mode wants it (grown only where it covers nothing).</summary>
        internal static void SizeButton(Button b)
        {
            if (b == null || b.IsDisposed) return;
            Kept<Size> kept;
            bool known = _buttons.TryGetValue(b, out kept);
            if (!On)
            {
                if (!known) return;
                _buttons.Remove(b);
                if (b.Height != kept.Value.Height) b.Height = kept.Value.Height;
                Kept<int> grew;
                var flow = b.Parent;
                if (flow != null && _grown.TryGetValue(flow, out grew)) { _grown.Remove(flow); flow.Height = Math.Max(1, flow.Height - grew.Value); }
                return;
            }
            if (known) return;
            int want = UiScale.Px(ButtonHeight);
            var parent = b.Parent;
            if (b.Height >= want || parent == null || parent is TableLayoutPanel) return;
            var grown = new Rectangle(b.Left, b.Top, b.Width, want);
            bool flowing = parent is FlowLayoutPanel;
            // inside its window: a dialog's OK at the bottom is not pushed out of it
            if (!flowing && grown.Bottom > parent.DisplayRectangle.Bottom) return;
            if (!flowing)
                foreach (Control sib in parent.Controls)
                    if (!ReferenceEquals(sib, b) && sib.Visible && sib.Bounds.IntersectsWith(grown) && !sib.Bounds.IntersectsWith(b.Bounds))
                        return;                              // it would cover something: as it is
            _buttons.Add(b, new Kept<Size>(b.Size));
            b.Height = want;
            // a bar of its own height: as tall as its buttons, when it can be
            Kept<int> before;
            if (flowing && !parent.AutoSize && !_grown.TryGetValue(parent, out before))
            {
                int need = b.Bottom + b.Margin.Bottom + parent.Padding.Bottom - parent.ClientSize.Height;
                if (need > 0 && (parent.Dock == DockStyle.Top || parent.Dock == DockStyle.Bottom || !Covers(parent, need)))
                {
                    _grown.Add(parent, new Kept<int>(need));
                    parent.Height += need;
                }
            }
        }

        private static bool Covers(Control c, int extra)
        {
            if (c.Parent == null) return false;
            var grown = new Rectangle(c.Left, c.Top, c.Width, c.Height + extra);
            foreach (Control sib in c.Parent.Controls)
                if (!ReferenceEquals(sib, c) && sib.Visible && sib.Bounds.IntersectsWith(grown) && !sib.Bounds.IntersectsWith(c.Bounds)) return true;
            return false;
        }

        // ---- tabs ---------------------------------------------------------------------------------

        private static void SizeTabs(TabControl tabs)
        {
            if (tabs.Alignment != TabAlignment.Top || tabs.Appearance != TabAppearance.Normal) return;
            Kept<Point> kept;
            bool known = _tabPadding.TryGetValue(tabs, out kept);
            if (!On)
            {
                if (known && tabs.Padding != kept.Value) tabs.Padding = kept.Value;
                return;
            }
            if (!known) { kept = new Kept<Point>(tabs.Padding); _tabPadding.Add(tabs, kept); }
            var p = new Point(kept.Value.X, kept.Value.Y + UiScale.Px(TabExtra));
            if (tabs.Padding != p) tabs.Padding = p;
        }

        // ---- tick boxes and round options ---------------------------------------------------------

        /// <summary>A plain CheckBox or RadioButton (the program's own CheckBoxHMI is a finger's size already).</summary>
        private static bool IsPlainBox(ButtonBase b)
        {
            var cb = b as CheckBox;
            if (cb != null) return !(cb is CheckBoxHMI) && cb.Appearance == Appearance.Normal && cb.CheckAlign == ContentAlignment.MiddleLeft &&
                                   cb.RightToLeft != RightToLeft.Yes && cb.GetType().Namespace != null && cb.GetType().Namespace.StartsWith("System.Windows.Forms");
            var rb = b as RadioButton;
            if (rb != null) return rb.Appearance == Appearance.Normal && rb.CheckAlign == ContentAlignment.MiddleLeft && rb.RightToLeft != RightToLeft.Yes &&
                                   rb.GetType().Namespace != null && rb.GetType().Namespace.StartsWith("System.Windows.Forms");
            return false;
        }

        /// <summary>The box's mark for a finger, at the screen scale.</summary>
        public static int BoxPx { get { return UiScale.Px(BoxSize); } }

        // ---- a tick box drawn as a switch (30 October: "you can make all the checkbox and the checkbox HMI default
        // render this way as well" - the rows of Machine Parameters > Options: the name, an on / off switch) ----------------

        /// <summary>A tick box's switch: its height at factor 1, with a mouse and by touch (its width 1.75 times that).</summary>
        public const int SwitchHeight = 18, SwitchHeightTouch = 26;

        /// <summary>The switch's height and width now (the mode and the screen scale).</summary>
        public static int SwitchH { get { return UiScale.Px(On ? SwitchHeightTouch : SwitchHeight); } }
        public static int SwitchW { get { return (int)Math.Round(SwitchH * 1.75); } }

        /// <summary>
        /// Room for the switch: a box that sizes itself (AutoSize) gets its switch where the tick was, its text after it -
        /// its place and its column with the others kept; a box of a width of its own is a row: the text on the left,
        /// the switch at its right end.
        /// </summary>
        private static void SizeSwitchBox(CheckBox box, Padding own)
        {
            if (!box.AutoSize) { if (box.Padding != own) box.Padding = own; return; }
            int h = SwitchH, w = SwitchW, gap = UiScale.Px(6), system = 13 + 3;     // Windows' own mark and its gap, as its size counts them
            int top = 0;
            int ph = box.GetPreferredSize(Size.Empty).Height - box.Padding.Vertical + own.Vertical;
            if (ph < h + 4) top = (h + 4 - ph + 1) / 2;
            var p = new Padding(own.Left + Math.Max(0, w + gap - system), own.Top + top, own.Right, own.Bottom + top);
            if (box.Padding != p) box.Padding = p;
        }

        /// <summary>The switch and the text, drawn over Windows' tick box.</summary>
        private static void PaintSwitchBox(object sender, PaintEventArgs e)
        {
            var cb = sender as CheckBox;
            if (cb == null) return;
            var g = e.Graphics;
            Color back = cb.BackColor;
            for (var p = cb.Parent; back.A < 255 && p != null; p = p.Parent) back = p.BackColor;
            if (back.A < 255) back = SystemColors.Control;
            using (var brush = new SolidBrush(back)) g.FillRectangle(brush, cb.ClientRectangle);
            Kept<Padding> kept;
            var own = _boxPadding.TryGetValue(cb, out kept) ? kept.Value : Padding.Empty;
            int W = cb.ClientSize.Width, H = cb.ClientSize.Height, gap = UiScale.Px(6);
            float h = Math.Min(SwitchH, Math.Max(10, H - 2)), w = h * 1.75f;
            RectangleF track;
            Rectangle text;
            if (cb.AutoSize)
            {
                track = new RectangleF(own.Left + 1, (H - h) / 2f, w, h);
                text = new Rectangle((int)Math.Ceiling(track.Right) + gap, 0, Math.Max(1, W - (int)Math.Ceiling(track.Right) - gap), H);
            }
            else
            {
                track = new RectangleF(Math.Max(own.Left, W - own.Right - w - 2), (H - h) / 2f, w, h);
                text = new Rectangle(own.Left, 0, Math.Max(1, (int)track.Left - gap - own.Left), H);
            }
            bool live = cb.Enabled;
            Kaban.UI.Controls.ToggleSwitch.Draw(g, track, cb.CheckState == CheckState.Checked, live);
            if (cb.CheckState == CheckState.Indeterminate)
                using (var pen = new Pen(Color.FromArgb(116, 119, 117), Math.Max(2f, h / 8f)))
                    g.DrawLine(pen, track.Left + w * 0.35f, track.Top + h / 2f, track.Right - w * 0.35f, track.Top + h / 2f);
            TextRenderer.DrawText(g, cb.Text, cb.Font, text, live ? cb.ForeColor : SystemColors.GrayText,
                                  TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | (cb.UseMnemonic ? 0 : TextFormatFlags.NoPrefix) |
                                  (cb.AutoSize ? TextFormatFlags.SingleLine : TextFormatFlags.WordBreak));
        }

        /// <summary>Room for the larger mark: its text moved over, the box as tall as the mark.</summary>
        private static void SizeBox(ButtonBase box)
        {
            Kept<Padding> kept;
            if (!_boxPadding.TryGetValue(box, out kept)) return;
            var own = kept.Value;
            if (box is CheckBox) { SizeSwitchBox((CheckBox)box, own); return; }
            if (!On) { if (box.Padding != own) box.Padding = own; return; }
            int mark = BoxPx, system = 13;
            int top = 0;
            if (box.AutoSize)
            {
                int h = box.GetPreferredSize(Size.Empty).Height - box.Padding.Vertical + own.Vertical;
                if (h < mark + 4) top = (mark + 4 - h + 1) / 2;
            }
            var p = new Padding(own.Left + mark - system + UiScale.Px(2), own.Top + top, own.Right, own.Bottom + top);
            if (box.Padding != p) box.Padding = p;
        }

        /// <summary>Drawn over Windows' small mark: a finger's box (or circle), its tick, the text beside it.</summary>
        private static void PaintBigBox(object sender, PaintEventArgs e)
        {
            var b = sender as ButtonBase;
            if (b == null || !On) return;
            var cb = b as CheckBox;
            var g = e.Graphics;
            Color back = b.BackColor;
            for (var p = b.Parent; back.A < 255 && p != null; p = p.Parent) back = p.BackColor;
            if (back.A < 255) back = SystemColors.Control;
            using (var brush = new SolidBrush(back)) g.FillRectangle(brush, b.ClientRectangle);
            Kept<Padding> kept;
            var own = _boxPadding.TryGetValue(b, out kept) ? kept.Value : Padding.Empty;
            int mark = Math.Min(BoxPx, Math.Max(10, b.ClientSize.Height - 2));
            var r = new Rectangle(own.Left, (b.ClientSize.Height - mark) / 2, mark, mark);
            bool live = b.Enabled;
            var edge = live ? Color.FromArgb(120, 128, 140) : Color.FromArgb(205, 210, 217);
            var ink = live ? Color.FromArgb(25, 28, 34) : Color.FromArgb(170, 176, 184);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (cb != null)
            {
                using (var white = new SolidBrush(Color.White)) g.FillRectangle(white, r);
                using (var pen = new Pen(edge, Math.Max(1f, UiScale.Px(1)))) g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                if (cb.CheckState == CheckState.Checked)
                    using (var pen = new Pen(ink, Math.Max(2f, mark / 8f)) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                        g.DrawLines(pen, new[]
                        {
                            new PointF(r.Left + mark * 0.22f, r.Top + mark * 0.52f),
                            new PointF(r.Left + mark * 0.42f, r.Top + mark * 0.72f),
                            new PointF(r.Left + mark * 0.78f, r.Top + mark * 0.30f)
                        });
                else if (cb.CheckState == CheckState.Indeterminate)
                    using (var fill = new SolidBrush(ink)) g.FillRectangle(fill, Rectangle.Inflate(r, -mark / 4, -mark / 4));
            }
            else
            {
                var rb = (RadioButton)b;
                using (var white = new SolidBrush(Color.White)) g.FillEllipse(white, r);
                using (var pen = new Pen(edge, Math.Max(1f, UiScale.Px(1)))) g.DrawEllipse(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                if (rb.Checked) using (var fill = new SolidBrush(ink)) g.FillEllipse(fill, Rectangle.Inflate(r, -mark / 4, -mark / 4));
            }
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.Default;
            var text = new Rectangle(r.Right + UiScale.Px(5), 0, Math.Max(1, b.ClientSize.Width - r.Right - UiScale.Px(5)), b.ClientSize.Height);
            TextRenderer.DrawText(g, b.Text, b.Font, text, live ? b.ForeColor : SystemColors.GrayText,
                                  TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | (b.UseMnemonic ? 0 : TextFormatFlags.NoPrefix) |
                                  (b.AutoSize ? TextFormatFlags.SingleLine : TextFormatFlags.WordBreak));
        }
    }
}
