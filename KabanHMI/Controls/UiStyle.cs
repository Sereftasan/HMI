using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// Shared styling helpers so plain WinForms buttons (toolbars, dialogs, the
    /// alarm page) match the rounded look of the HMI control family instead of
    /// looking like raw system buttons.
    /// </summary>
    public static class UiStyle
    {
        /// <summary>
        /// Initialised when first USED, not whenever the runtime likes: the fonts
        /// below are made at the magnifying factor, which must be known by then.
        /// </summary>
        static UiStyle() { }

        public static readonly Color BorderColor = Color.FromArgb(180, 186, 194);
        public static readonly Color AccentBack = Color.FromArgb(200, 224, 248);

        /// <summary>
        /// THE ONLY PLACE BLUE IS USED: the border of the control the pointer is
        /// actually over.
        ///
        /// Blue used to mean several unrelated things at once - the active settings
        /// tab, some buttons' hover, an accent fill - so it told the operator
        /// nothing. It now means exactly one thing: "this is the control you are
        /// about to click, and this is what the tooltip belongs to".
        ///
        /// CONTAINERS DO NOT LIGHT UP. A group box or a function-unit frame is not
        /// clickable, so outlining it says nothing about where the click will land
        /// and competes with the leaf control that IS under the pointer. Only
        /// controls the operator can actually interact with take the border.
        /// </summary>
        public static readonly Color HoverBorderColor = Color.FromArgb(30, 120, 220);

        // ---- shared fonts -------------------------------------------------
        //
        // A Font is a handle to a system resource, and every control was creating
        // its own copy of the same one. Two hundred bound controls on a page meant
        // two hundred identical fonts to allocate at open and to finalise later.
        // They are immutable and safe to share, so the program keeps one of each.
        //
        // At the magnifying factor this run started with (see UiScale): they are
        // used to DRAW as well as given to controls, and a control given one is
        // not scaled a second time.
        public static readonly Font Body = UiScale.NewFont("Segoe UI", 9.5F);
        public static readonly Font Small = UiScale.NewFont("Segoe UI", 9F);
        public static readonly Font Mono = UiScale.NewFont("Consolas", 10F, FontStyle.Bold);
        /// <summary>A page's own heading (About, a machine's Home).</summary>
        public static readonly Font Title = UiScale.NewFont("Segoe UI", 12F, FontStyle.Bold);

        /// <summary>The font every dropdown menu uses (was a new Font per menu opened).</summary>
        public static readonly Font MenuFont = UiScale.NewFont("Segoe UI", 10F);

        private static readonly Dictionary<string, Font> _fontVariants = new Dictionary<string, Font>();

        /// <summary>
        /// The same font in another style (bold for the active page, say), shared.
        /// Toggling IsActivePage created a new Font on every page change and left
        /// the previous one for the finalizer. Never dispose what this returns.
        /// </summary>
        public static Font FontVariant(Font baseFont, FontStyle style)
        {
            if (baseFont == null || baseFont.Style == style) return baseFont;
            string key = baseFont.FontFamily.Name + "|" +
                         baseFont.Size.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" +
                         (int)baseFont.Unit + "|" + (int)style;
            lock (_fontVariants)
            {
                Font f;
                if (!_fontVariants.TryGetValue(key, out f))
                {
                    _fontVariants[key] = f = new Font(baseFont.FontFamily, baseFont.Size, style, baseFont.Unit);
                    if (UiScale.IsScaled(baseFont)) UiScale.Marked(f);     // the same size: scaled if its base is
                }
                return f;
            }
        }

        // ---- the hand cursor, everywhere ---------------------------------
        //
        // "Anything that can be clicked shows the hand" was applied by a sweep of
        // each PAGE when it was shown - so the caption bar, the dropdown menus
        // (connection, alarms, login, language), dialogs, and any control added
        // after the sweep kept the arrow. Menu items are not controls at all, so
        // no sweep could ever reach them. Now:
        //   * every open window is swept once, on the first idle moment after it
        //     appears, and every container is watched for controls added later;
        //   * every menu and toolbar shows the hand over its enabled items.
        // Disabled controls still fall back to the arrow (CursorFollowsEnabled).

        private static bool _handCursorsOn;
        private static readonly HashSet<Control> _sweptForms = new HashSet<Control>(new ReferenceComparer());
        private static readonly HashSet<Control> _watched = new HashSet<Control>(new ReferenceComparer());
        private static readonly HashSet<Control> _handCued = new HashSet<Control>(new ReferenceComparer());
        private static readonly HashSet<Control> _menuCued = new HashSet<Control>(new ReferenceComparer());

        /// <summary>Turns the hand-cursor rule on for every window of the program. Call once.</summary>
        public static void EnableHandCursorsEverywhere()
        {
            if (_handCursorsOn) return;
            _handCursorsOn = true;
            Application.Idle += (s, e) => SweepOpenForms();
            SweepOpenForms();
        }

        /// <summary>
        /// Applies the rule to every open window not swept yet. Runs on each idle
        /// moment (cheap: a handful of windows and a set lookup each); public for
        /// tests, as Mono's DoEvents does not raise Idle.
        /// </summary>
        public static void SweepOpenForms()
        {
            foreach (Form f in Application.OpenForms)
            {
                if (f == null || f.IsDisposed || !_sweptForms.Add(f)) continue;
                var form = f;
                form.Disposed += (s2, e2) => _sweptForms.Remove(form);
                // A window no one scaled as it loaded is scaled now (UiScale).
                UiScale.Apply(form);
                ApplyHandCursors(form);
            }
        }

        /// <summary>Gives every button, combo box, menu and toolbar under root the hand rule, now and later.</summary>
        public static void ApplyHandCursors(Control root)
        {
            if (root == null || root.IsDisposed) return;
            HandCursorFor(root);
            if (_watched.Add(root))
            {
                var r = root;
                r.ControlAdded += (s, e) => ApplyHandCursors(e.Control);
                r.Disposed += (s, e) => _watched.Remove(r);
            }
            foreach (Control c in root.Controls) ApplyHandCursors(c);
        }

        private static void HandCursorFor(Control c)
        {
            if (c.ContextMenuStrip != null) HandOverMenuItems(c.ContextMenuStrip);

            var ts = c as ToolStrip;
            if (ts != null) { HandOverMenuItems(ts); return; }

            if (!(c is ButtonBase || c is ComboBox)) return;
            if (c is IManagesOwnCursor || IsDisplayOnly(c)) return;           // they decide for themselves
            // A cursor somebody chose on purpose (a drag handle, say) is kept.
            if (c.Cursor != Cursors.Default && c.Cursor != Cursors.Hand) return;
            if (_cued.Contains(c) || !_handCued.Add(c)) return;               // already following Enabled
            var ctl = c;
            ctl.Disposed += (s, e) => _handCued.Remove(ctl);
            CursorFollowsEnabled(ctl);
        }

        /// <summary>
        /// The hand over a menu's or toolbar's clickable items, the arrow over its
        /// headings, separators and disabled entries. Submenus get the same as
        /// they open.
        /// </summary>
        public static void HandOverMenuItems(ToolStrip ts)
        {
            if (ts == null || !_menuCued.Add(ts)) return;
            var strip = ts;
            // a greyed entry says why in its tooltip (Unavailable) - a menu's own are off by default
            if (strip is ToolStripDropDown) strip.ShowItemToolTips = true;
            strip.Disposed += (s, e) => _menuCued.Remove(strip);
            strip.MouseMove += (s, e) =>
            {
                var item = strip.GetItemAt(e.Location);
                var want = IsClickable(item) ? Cursors.Hand : Cursors.Default;
                if (strip.Cursor != want) strip.Cursor = want;
            };
            strip.MouseLeave += (s, e) => { if (strip.Cursor != Cursors.Default) strip.Cursor = Cursors.Default; };
            foreach (ToolStripItem it in strip.Items) HookSubMenu(it);
            strip.ItemAdded += (s, e) => HookSubMenu(e.Item);
        }

        private static bool IsClickable(ToolStripItem item)
        {
            if (item == null || !item.Enabled || !item.Available) return false;
            if (item is ToolStripSeparator || item is ToolStripControlHost) return false;
            var label = item as ToolStripLabel;
            if (label != null && !(item is ToolStripStatusLabel) && !label.IsLink) return false;   // a heading
            return true;
        }

        private static void HookSubMenu(ToolStripItem item)
        {
            var dd = item as ToolStripDropDownItem;
            if (dd == null) return;
            dd.DropDownOpened += (s, e) => HandOverMenuItems(dd.DropDown);
        }

        // ---- error border blink ------------------------------------------
        //
        // One shared timer for every control whose border is blinking an error
        // (a unit's position display, say): a page with twenty faulted units must
        // not run twenty timers. 500 ms, in step with the red "?" on the frame.
        private static readonly List<Control> _alerting = new List<Control>();
        private static Timer _alertTimer;

        /// <summary>True during the "red" half of the shared error blink.</summary>
        public static bool AlertPhase { get; private set; }

        /// <summary>Starts or stops a control's share of the error blink.</summary>
        public static void SetAlerting(Control c, bool on)
        {
            if (c == null) return;
            if (on)
            {
                if (_alerting.Contains(c)) return;
                _alerting.Add(c);
                if (_alertTimer == null)
                {
                    _alertTimer = new Timer { Interval = 500 };
                    _alertTimer.Tick += (s, e) =>
                    {
                        // Blinking off (View Preferences): the red half, steady.
                        AlertPhase = !Kaban.Core.Helpers.ViewPreferences.BlinkingOn || !AlertPhase;
                        for (int i = _alerting.Count - 1; i >= 0; i--)
                        {
                            if (_alerting[i].IsDisposed) { _alerting.RemoveAt(i); continue; }
                            _alerting[i].Invalidate();
                        }
                        if (_alerting.Count == 0) _alertTimer.Stop();
                    };
                }
                _alertTimer.Start();
            }
            else
            {
                _alerting.Remove(c);
                if (_alerting.Count == 0 && _alertTimer != null) _alertTimer.Stop();
            }
            c.Invalidate();
        }

        // ---- menus built per click ---------------------------------------
        //
        // The top-bar, alarm-help and column menus are built fresh on every click
        // (their items depend on the moment) and were never disposed: each one
        // kept a window handle, its items, images and renderer until the
        // finalizer got to them. On a panel that runs for weeks that is a steady
        // leak of USER and GDI handles.
        //
        // Disposing a menu the moment it closes is NOT safe: the clicked item's
        // Click handler runs after Closed, and one that opens a dialog would be
        // running inside a disposed menu. So the previous menu of the SAME control
        // is disposed when the next one is shown (it is certainly closed by then),
        // and the last one goes with the control.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, ToolStripDropDown[]> _lastPopup =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Control, ToolStripDropDown[]>();

        /// <summary>Shows a per-click menu below/at a point of its owner and takes care of disposing it.</summary>
        public static void ShowPopup(ToolStripDropDown menu, Control owner, Point at)
        {
            if (menu == null) return;
            Adopt(menu, owner);
            if (owner != null) menu.Show(owner, at); else menu.Show(at);
        }

        /// <summary>Same, at a screen position (the cursor, for a right-click).</summary>
        public static void ShowPopupAt(ToolStripDropDown menu, Control owner, Point screenPoint)
        {
            if (menu == null) return;
            Adopt(menu, owner);
            menu.Show(screenPoint);
        }

        private static void Adopt(ToolStripDropDown menu, Control owner)
        {
            HandOverMenuItems(menu);
            if (owner == null) return;
            ToolStripDropDown[] slot;
            if (!_lastPopup.TryGetValue(owner, out slot))
            {
                slot = new ToolStripDropDown[1];
                _lastPopup.Add(owner, slot);
                owner.Disposed += (s, e) =>
                {
                    var last = slot[0];
                    slot[0] = null;
                    if (last != null && !last.IsDisposed) last.Dispose();
                };
            }
            var old = slot[0];
            slot[0] = menu;
            // a menu made for one showing goes; a table's headings menu is kept (it is
            // the headings' own, shown again on the next right-click)
            if (old != null && old != menu && !old.IsDisposed && !(old.Tag is Kaban.UI.Helpers.ColumnChooser)) old.Dispose();
        }

        /// <summary>
        /// Makes a control repaint on enter/leave and reports whether the pointer is
        /// over it, so its own Paint handler can pick HoverBorderColor.
        ///
        /// For controls that PAINT THEIR OWN border (ButtonHMI, BaseNumericHMI).
        /// Anything else should use HoverOutline, which needs no cooperation from
        /// the control at all.
        ///
        /// Tracked with an explicit flag rather than by hit-testing the cursor
        /// during paint: a control repaints for many reasons the mouse has nothing
        /// to do with, and testing MousePosition mid-paint made borders flicker blue
        /// when a value changed under a stationary pointer.
        /// </summary>
        public static Func<bool> HoverBorder(Control c)
        {
            if (c == null) return () => false;
            var state = new bool[1];
            c.MouseEnter += (s, e) => { state[0] = true; c.Invalidate(); };
            c.MouseLeave += (s, e) => { state[0] = false; c.Invalidate(); };
            return () => state[0];
        }

        /// <summary>The border colour a control should draw right now.</summary>
        /// <summary>
        /// Washes out a display control whose PLC is not answering.
        ///
        /// A meter or a code cell is never DISABLED for a lost connection -
        /// Enabled = false switches off its tooltip and its hover, which is what
        /// left them looking dead - but its last value must not pass for a live
        /// one either. A pale wash keeps the stale reading legible and plainly
        /// not current, and the tooltip says why.
        /// </summary>
        public static void DrawStale(Graphics g, Rectangle r)
        {
            using (var wash = new SolidBrush(Color.FromArgb(150, 255, 255, 255)))
                g.FillRectangle(wash, r);
        }

        /// <summary>The heading every dropdown menu opens with.</summary>
        public static ToolStripLabel MenuHeader(string text)
        {
            return new ToolStripLabel(text)
            {
                Font = MenuFont,
                ForeColor = Color.FromArgb(40, 46, 56),
                Margin = new Padding(-34, 2, 2, 2)
            };
        }

        /// <summary>
        /// A menu entry that cannot be used now: SHOWN, greyed, with why in its tooltip (24 October: "The
        /// disabled dropdown menu items should still be seen on the menu as disabled instead of removed (all
        /// around the application) so the user could understand they are checking the right place for the
        /// function but there is some reason for it to disable so they won't search it elsewhere").
        /// <paramref name="enabled"/> true: the entry as it is.
        /// </summary>
        public static T Unavailable<T>(T item, bool enabled, string why) where T : ToolStripItem
        {
            if (item == null) return null;
            item.Enabled = enabled;
            if (!enabled && !string.IsNullOrEmpty(why)) item.ToolTipText = why;
            var owner = item.Owner as ToolStripDropDown;
            if (owner != null) owner.ShowItemToolTips = true;
            return item;
        }

        /// <summary>A dropdown built the same way as every other in the program.</summary>
        public static ContextMenuStrip Menu()
        {
            var m = new ContextMenuStrip { ShowImageMargin = true, Font = MenuFont, ShowItemToolTips = true };
            m.Renderer = new HmiMenuRenderer();
            m.ImageScalingSize = UiScale.Px(new Size(22, 22));
            HandOverMenuItems(m);
            return m;
        }

        public static Color BorderFor(bool hovering, Color normal)
        {
            return hovering ? HoverBorderColor : normal;
        }

        /// <summary>
        /// Implemented by a control that already draws its own hover border, so the
        /// shared walk does not outline it a second time.
        /// </summary>
        public interface IDrawsOwnHoverBorder { }

        /// <summary>
        /// A control that SHOWS something and cannot be clicked - a meter, an alarm
        /// code cell. It keeps the blue hover outline, because that says "there is
        /// a tooltip here", but never the hand: a hand promises a click, and on a
        /// meter or a blank code cell there is nothing to click.
        /// </summary>
        public interface IDisplayOnly { }

        /// <summary>A display (IDisplayOnly), or a checkbox that only shows its bit.</summary>
        public static bool IsDisplayOnly(Control c)
        {
            var box = c as CheckBoxHMI;
            return c is IDisplayOnly || (box != null && box.DisplayOnly);
        }

        /// <summary>
        /// A control that sets its own cursor from its own state (a button that
        /// greys itself through soft enable). The sweep leaves its cursor alone.
        /// </summary>
        public interface IManagesOwnCursor { }

        /// <summary>
        /// Gives a STANDARD WinForms control (TextBox, ComboBox, CheckBox, grid...)
        /// the same blue hover border, without needing to subclass it.
        ///
        /// The outline is drawn by the PARENT, just outside the control's bounds.
        /// A TextBox with a system border paints its own frame and gives us no way
        /// in; a ComboBox's drop-down is a separate window; a DataGridView repaints
        /// its own edges. Drawing on the parent sidesteps all of it and works for
        /// any control, including ones we did not write.
        /// </summary>
        public static void HoverOutline(Control c)
        {
            if (c == null || c.Parent == null) return;

            var parent = c.Parent;
            bool[] on = { false };

            PaintEventHandler paint = (s, e) =>
            {
                if (!on[0] || !c.Visible) return;
                var r = c.Bounds;
                r.Inflate(2, 2);
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(HoverBorderColor, 1.6f))
                    e.Graphics.DrawRectangle(pen, r);
            };

            Action refresh = () =>
            {
                var r = c.Bounds;
                r.Inflate(4, 4);              // a little wider than the pen, so nothing is clipped
                parent.Invalidate(r, false);
            };

            c.MouseEnter += (s, e) => { on[0] = true; refresh(); };
            c.MouseLeave += (s, e) => { on[0] = false; refresh(); };
            // A control that loses Enabled or Visible under a stationary pointer
            // would otherwise keep a stale outline.
            c.EnabledChanged += (s, e) => { if (!c.Enabled) { on[0] = false; refresh(); } };
            c.VisibleChanged += (s, e) => { if (!c.Visible) { on[0] = false; refresh(); } };
            c.Disposed += (s, e) => { parent.Paint -= paint; };
            parent.Paint += paint;
        }

        /// <summary>
        /// The cursor a control should show when it is ENABLED. A text box keeps its
        /// I-beam; anything the operator clicks gets the hand.
        /// </summary>
        private static Cursor EnabledCursorFor(Control c)
        {
            if (c is TextBoxBase) return Cursors.IBeam;
            return Cursors.Hand;
        }

        /// <summary>
        /// Hand while the control is usable, plain arrow once it is disabled - so a
        /// button greyed out by an interlock is obvious BEFORE it is clicked rather
        /// than after nothing happens.
        ///
        /// WinForms does not do this: Cursor is independent of Enabled, so a
        /// disabled control keeps whatever hand it was given.
        /// </summary>
        public static void CursorFollowsEnabled(Control c)
        {
            if (c == null) return;
            var enabledCursor = EnabledCursorFor(c);
            EventHandler apply = (s, e) => c.Cursor = c.Enabled ? enabledCursor : Cursors.Default;
            c.EnabledChanged += apply;
            apply(c, EventArgs.Empty);
        }

        /// <summary>
        /// True for a control that only GROUPS other controls. These get no hover
        /// border and no hand: the pointer is really over whatever sits inside them.
        /// </summary>
        private static bool IsContainer(Control c)
        {
            return c is Panel || c is GroupBox || c is TableLayoutPanel || c is FlowLayoutPanel
                || c is SplitContainer || c is TabControl || c is TabPage || c is UserControl
                || c is Form || c is ToolStrip || c is Label;
        }

        /// <summary>
        /// Walks a page and gives every INTERACTIVE control the two shared cues: a
        /// blue border under the pointer, and a hand that disappears when the
        /// control is disabled.
        ///
        /// Call it once after a page is put on screen. Applying it centrally rather
        /// than control by control is the point - the previous rule ("some controls
        /// turn blue") was really "whoever remembered to implement it".
        /// </summary>
        public static void ApplyInteractionCues(Control root)
        {
            if (root == null) return;

            foreach (Control child in root.Controls)
            {
                if (IsContainer(child))
                {
                    ApplyInteractionCues(child);          // the cue belongs to what is inside
                    continue;
                }

                if (_cued.Add(child))
                {
                    child.Disposed += (s, e) => _cued.Remove(child);
                    // No outline round a whole grid: the pointer is over a cell,
                    // not over the table as one thing.
                    if (!(child is IDrawsOwnHoverBorder) && !(child is DataGridView)) HoverOutline(child);
                    ApplyCursorRule(child);
                }

                // a leaf may still nest (a grid's editing controls, a spin box)
                if (child.Controls.Count > 0) ApplyInteractionCues(child);
            }
        }

        /// <summary>
        /// The cursor a control gets from the sweep.
        ///
        /// The sweep used to hand EVERY leaf control the hand cursor, on the theory
        /// that anything not a container was interactive. Meters, alarm code cells
        /// and grids are not: they showed a hand over nothing clickable. Only a
        /// control that can actually be operated gets the hand, and only while it
        /// can be.
        /// </summary>
        private static void ApplyCursorRule(Control c)
        {
            // FIRST: a display that turns into a link while it has something to
            // open (an axis code cell) sets its own cursor as that changes.
            if (c is IManagesOwnCursor) return;
            if (IsDisplayOnly(c) || c is DataGridView) { c.Cursor = Cursors.Default; return; }
            CursorFollowsEnabled(c);
        }

        /// <summary>
        /// Controls already wired, so re-showing a page does not stack a second set
        /// of handlers on the same control - a settings tab is revisited constantly
        /// and the outlines would thicken each time.
        ///
        /// A side set rather than control.Tag: Tag belongs to whoever built the
        /// screen, and quietly taking it over is the kind of thing that breaks a
        /// machine page months later. Reference identity, so two controls that
        /// compare equal are still tracked separately.
        /// </summary>
        private static readonly HashSet<Control> _cued =
            new HashSet<Control>(new ReferenceComparer());

        private class ReferenceComparer : IEqualityComparer<Control>
        {
            public bool Equals(Control a, Control b) { return ReferenceEquals(a, b); }
            public int GetHashCode(Control c)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(c);
            }
        }

        /// <summary>Applies the standard 4px corner radius, tracking size changes.</summary>
        public static Button Round(Button b, int radius = 4)
        {
            b.FlatStyle = FlatStyle.Flat;
            // WinForms draws the FlatAppearance border as a square rectangle ON the
            // control bounds, which the rounded Region then clips - that is why the
            // corners looked square and only two edges showed. Draw the border
            // ourselves, 1px inside the region, so all four sides and the radius render.
            b.FlatAppearance.BorderSize = 0;
            ApplyRegion(b, radius);
            b.Resize += (s, e) => ApplyRegion(b, radius);

            var hovering = HoverBorder(b);
            b.Paint += (s, e) =>
            {
                var c = (Control)s;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int d = Math.Max(2, (radius - 1) * 2);
                var r = new Rectangle(1, 1, c.Width - 3, c.Height - 3);
                using (var path = new GraphicsPath())
                {
                    path.AddArc(r.X, r.Y, d, d, 180, 90);
                    path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                    path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                    path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                    path.CloseFigure();
                    using (var pen = new Pen(BorderFor(hovering(), BorderColor), 1.6f))
                        e.Graphics.DrawPath(pen, path);
                }
            };
            return b;
        }

        private static void ApplyRegion(Control c, int radius)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            int d = radius * 2;
            using (var path = new GraphicsPath())
            {
                var r = new Rectangle(0, 0, c.Width, c.Height);
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d - 1, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d - 1, r.Bottom - d - 1, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d - 1, d, d, 90, 90);
                path.CloseFigure();
                c.Region = new Region(path);
            }
        }

        // ==================================================================
        // FORM ROWS  (label : field)
        // ==================================================================

        /// <summary>
        /// Formats a parameter caption: the text, then a colon, with NO space before
        /// it and one after - "Cut depth (mm): ".
        ///
        /// Three styles were in use across the pages at once (" : ", " :" and ":"),
        /// which on a screen full of parameters reads as sloppy alignment even when
        /// the controls line up perfectly. English, Turkish, German, Italian and
        /// Spanish all set the colon tight to the word; French would want a space,
        /// and if the HMI ever ships in French this is the one place to change.
        /// </summary>
        public static string Caption(string text)
        {
            text = (text ?? "").Trim();
            if (text.Length == 0) return "";
            text = text.TrimEnd(':', ' ');           // tolerate a caption that already has one
            return text + ":";
        }

        /// <summary>Standard label width for a parameter caption column.</summary>
        public const int CaptionWidth = 150;

        /// <summary>Vertical pitch of one parameter row.</summary>
        public const int RowPitch = 34;

        /// <summary>
        /// Builds one parameter row - caption RIGHT-aligned against its field.
        ///
        /// Right alignment is the point: a left-aligned caption column leaves a
        /// ragged gap of whitespace between each word and the box it belongs to, and
        /// on a dense settings page the eye stops being able to pair them. Every
        /// other parameter page in the program already reads this way.
        /// </summary>
        public static Label CaptionLabel(string text, int x, int y, int width = CaptionWidth)
        {
            var lbl = new Label
            {
                Text = Caption(text),
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleRight,
                Width = width,
                Height = 24
            };
            lbl.Location = new Point(x, y);
            return lbl;
        }

        // ==================================================================
        // GRIDS
        // ==================================================================
        /// <summary>The program's table look (the PLC Monitor's): white, thin rules, bold headings, full-row selection.</summary>
        public static void StyleGrid(DataGridView grid) { Kaban.UI.Forms.SimulationMonitorForm.StyleGrid(grid); }

        // The table colours, one set for the whole program.
        public static readonly Color GridRule = Color.FromArgb(228, 231, 235);
        public static readonly Color GridHeadBack = Color.FromArgb(245, 246, 248);
        public static readonly Color GridInk = Color.FromArgb(25, 28, 34);
        public static readonly Color GridSelection = Color.FromArgb(218, 232, 250);

        /// <summary>
        /// THE LOOK of a table, without what it lets the user do (adding rows,
        /// selecting cells...): the PLC Monitor's - white, thin horizontal rules,
        /// bold headings on a light grey, rows of 26 px, the pale blue selection.
        /// Every table of the program has it (EnableTableTools puts it on).
        /// </summary>
        public static void GridLook(DataGridView grid)
        {
            if (grid == null) return;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = GridRule;
            // The rows and letters as View Preferences say (normal: 26 px, the body font).
            var font = TableFont;
            int rowHeight = Kaban.Core.Helpers.ViewPreferences.TableRowHeight;
            grid.Font = font;
            grid.RowTemplate.Height = rowHeight;
            Remember(grid);
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = GridInk;
            grid.DefaultCellStyle.SelectionBackColor = GridSelection;
            grid.DefaultCellStyle.SelectionForeColor = GridInk;
            grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.White;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.Font = FontVariant(font, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.BackColor = GridHeadBack;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = GridInk;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = GridHeadBack;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = GridInk;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 2, 4, 5);
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.RowHeadersDefaultCellStyle.BackColor = GridHeadBack;
            grid.RowHeadersDefaultCellStyle.SelectionBackColor = GridSelection;
            foreach (DataGridViewRow r in grid.Rows) if (r.Height < rowHeight && !r.IsNewRow) r.Height = rowHeight;
        }

        // ---- the tables' size, as View Preferences say -------------------------------

        private static readonly Dictionary<float, Font> _tableFonts = new Dictionary<float, Font>();

        /// <summary>A table's font: the body font, or a size smaller or larger (View Preferences).</summary>
        public static Font TableFont
        {
            get
            {
                float size = Kaban.Core.Helpers.ViewPreferences.TableFontSize;
                if (Math.Abs(size - 9.5f) < 0.01f) return Body;
                Font f;
                if (!_tableFonts.TryGetValue(size, out f)) _tableFonts[size] = f = UiScale.NewFont("Segoe UI", size);
                return f;
            }
        }

        private static readonly List<WeakReference> _tables = new List<WeakReference>();
        private static bool _tablesHooked;

        /// <summary>A table with the program's look, kept so a change of size reaches it at once.</summary>
        private static void Remember(DataGridView grid)
        {
            lock (_tables)
            {
                _tables.RemoveAll(w => !w.IsAlive || ((DataGridView)w.Target).IsDisposed);
                if (!_tables.Any(w => ReferenceEquals(w.Target, grid))) _tables.Add(new WeakReference(grid));
                if (_tablesHooked) return;
                _tablesHooked = true;
            }
            Kaban.Core.Helpers.ViewPreferences.Changed += (s, e) => ApplyTablePreferences();
        }

        // ---- dates and times, as View Preferences say --------------------------------

        private sealed class DateCol { public WeakReference Column; public bool Seconds; public int Fraction; }
        private static readonly List<DateCol> _dateColumns = new List<DateCol>();
        private static bool _datesHooked;

        /// <summary>
        /// A table column bound to a DateTime (an alarm's time): written as View
        /// Preferences say - and again, in the open table, when they change.
        /// </summary>
        public static void DateColumn(DataGridViewColumn column, bool seconds = true, int fraction = 0)
        {
            if (column == null) return;
            ApplyDateFormat(column, seconds, fraction);
            lock (_dateColumns)
            {
                _dateColumns.RemoveAll(d => !d.Column.IsAlive);
                _dateColumns.Add(new DateCol { Column = new WeakReference(column), Seconds = seconds, Fraction = fraction });
                if (_datesHooked) return;
                _datesHooked = true;
            }
            Kaban.Core.Helpers.ViewPreferences.Changed += (s, e) =>
            {
                List<DateCol> live;
                lock (_dateColumns) live = _dateColumns.Where(d => d.Column.IsAlive).ToList();
                foreach (var d in live)
                {
                    var col = d.Column.Target as DataGridViewColumn;
                    if (col == null) continue;
                    var dc = d;
                    var grid = col.DataGridView;
                    Action apply = () => ApplyDateFormat(col, dc.Seconds, dc.Fraction);
                    try
                    {
                        if (grid != null && !grid.IsDisposed && grid.InvokeRequired) grid.BeginInvoke(apply);
                        else apply();
                    }
                    catch { /* a table closing meanwhile */ }
                }
            };
        }

        private static void ApplyDateFormat(DataGridViewColumn col, bool seconds, int fraction)
        {
            col.DefaultCellStyle.Format = Kaban.Core.Helpers.ViewPreferences.DateTimePattern(seconds, fraction);
            // "PM", not the Windows language's designator: as every other time on screen.
            col.DefaultCellStyle.FormatProvider = System.Globalization.CultureInfo.InvariantCulture;
            var grid = col.DataGridView;
            if (grid != null && !grid.IsDisposed && col.Index >= 0) grid.InvalidateColumn(col.Index);
        }

        /// <summary>
        /// Runs <paramref name="apply"/> whenever View Preferences change, on the
        /// control's thread, until the control is disposed - for a page that writes
        /// dates or numbers itself (pickers, lists, a bar of text).
        /// </summary>
        public static void OnViewPreferences(Control owner, Action apply)
        {
            if (owner == null || apply == null) return;
            EventHandler h = null;
            h = (s, e) =>
            {
                if (owner.IsDisposed) { Kaban.Core.Helpers.ViewPreferences.Changed -= h; return; }
                Action run = () => { if (!owner.IsDisposed) apply(); };
                try
                {
                    if (owner.IsHandleCreated && owner.InvokeRequired) owner.BeginInvoke(run);
                    else run();
                }
                catch { /* closing meanwhile */ }
            };
            Kaban.Core.Helpers.ViewPreferences.Changed += h;
            owner.Disposed += (s, e) => Kaban.Core.Helpers.ViewPreferences.Changed -= h;
        }

        /// <summary>The rows and letters of every open table, again (View Preferences changed).</summary>
        public static void ApplyTablePreferences()
        {
            List<DataGridView> live;
            lock (_tables) live = _tables.Select(w => w.Target as DataGridView).Where(g => g != null && !g.IsDisposed).ToList();
            foreach (var grid in live)
            {
                var g = grid;
                Action apply = () =>
                {
                    if (g.IsDisposed) return;
                    var font = TableFont;
                    if (!ReferenceEquals(g.Font, font)) g.Font = font;
                    g.ColumnHeadersDefaultCellStyle.Font = FontVariant(font, FontStyle.Bold);
                    int h = UiScale.For(g, Kaban.Core.Helpers.ViewPreferences.TableRowHeight);
                    if (g.RowTemplate.Height == h) return;
                    g.RowTemplate.Height = h;
                    if (g.VirtualMode)
                    {
                        // Rows made from the template: made again (thousands of rows, one call).
                        int n = g.RowCount;
                        g.RowCount = 0;
                        g.RowCount = n;
                    }
                    else foreach (DataGridViewRow r in g.Rows) if (!r.IsNewRow) r.Height = h;
                };
                try
                {
                    if (g.InvokeRequired) g.BeginInvoke(apply);
                    else apply();
                }
                catch { /* a table closing meanwhile */ }
            }
        }

        /// <summary>
        /// Raises every column's MinimumWidth to fit its own HEADING.
        ///
        /// DataGridView's default minimum is 5px, so a column narrower than its
        /// caption shows an ellipsis and the operator has to drag every one before
        /// the table can be read - which on an alarm page is exactly when nobody
        /// has time to. Called after a grid's columns are built; a column may still
        /// be made wider, never narrower than its own name.
        /// </summary>
        /// <summary>
        /// Sizes columns and the header row to their content, and lets a
        /// double-click on a divider re-fit at any time.
        ///
        /// DisplayedCells, NOT AllCells: measuring every row of a long history
        /// table on every fill is work proportional to the data, and the operator
        /// only ever sees a screenful. Measuring the visible rows costs the same
        /// whether the table holds fifty rows or fifty thousand.
        ///
        /// The header row auto-sizes too, which is what stops a two-word caption
        /// being clipped to one line.
        /// </summary>
/// <summary>
        /// Remembers a table's column widths and order between sessions.
        ///
        /// An engineer who widens a column has made a decision about their own
        /// screen and their own work; throwing it away on every restart means
        /// making it again every morning. Saved per grid NAME under the user's
        /// application data, so two machines on one panel do not fight over it.
        ///
        /// Restoring is best-effort: a column that no longer exists is skipped and
        /// a corrupt file is ignored, because a remembered layout must never be
        /// able to stop a page opening.
        /// </summary>
/// <summary>
        /// Multi-level sorting, column hiding and reordering - remembered.
        ///
        /// SORTING KEEPS WHAT CAME BEFORE, like a spreadsheet. Clicking Severity
        /// then Time gives "by time within severity", not "by time"; the earlier
        /// key is demoted rather than discarded. That is how anyone who uses Excel
        /// expects a table to behave, and re-establishing a two-level sort by hand
        /// every time is tedious enough that people stop sorting at all.
        ///
        /// COLUMNS CAN BE HIDDEN. Code and Severity mean nothing to most operators
        /// and take the width the alarm text needs. Right-click any heading to pick
        /// what shows; the choice is saved with the widths.
        /// </summary>
        public static void EnableTableTools(DataGridView grid, string key, bool sorting = true)
        {
            if (grid == null) return;

            // ONE LOOK AND ONE COLUMN MENU FOR EVERY TABLE (2 October): the PLC
            // Monitor's - its headings, rows and selection, and its right-click on a
            // heading (the columns to tick, their widths, fit, save as the default,
            // the default column settings).
            GridLook(grid);
            // the columns are kept by name: a column made without one gets its place
            foreach (DataGridViewColumn c in grid.Columns)
                if (string.IsNullOrEmpty(c.Name)) c.Name = "c" + c.Index;

            var order = new System.Collections.Generic.List<string>();       // sort keys, most recent first
            var descending = new System.Collections.Generic.Dictionary<string, bool>();

            grid.AllowUserToOrderColumns = true;

            if (sorting)
            {
                foreach (DataGridViewColumn c in grid.Columns)
                    c.SortMode = DataGridViewColumnSortMode.Programmatic;

                grid.ColumnHeaderMouseClick += (s, e) =>
                {
                    if (e.Button != MouseButtons.Left) return;
                    var col = grid.Columns[e.ColumnIndex];
                    if (col == null) return;

                    bool desc;
                    descending.TryGetValue(col.Name, out desc);
                    descending[col.Name] = order.Count > 0 && order[0] == col.Name ? !desc : false;

                    order.Remove(col.Name);
                    order.Insert(0, col.Name);          // newest key wins, the rest demote
                    if (order.Count > 3) order.RemoveRange(3, order.Count - 3);

                    ApplySort(grid, order, descending);
                    SaveLayout(grid, key, order, descending);
                };
            }

            // AUTO-SIZING OFF from here on. EnableAutoFit leaves the grid in
            // DisplayedCells mode, which recomputes every width on each fill - so a
            // width the user set was overwritten by the next refresh and the saved
            // file just recorded whatever the grid had recalculated. The headings
            // have already been fitted by that point; from now on the widths belong
            // to the user. (A table that fills its width keeps doing so.)
            if (grid.AutoSizeColumnsMode != DataGridViewAutoSizeColumnsMode.Fill)
                grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            bool restoring = true;
            // The headings' menu - its "original" is the table as made here, before
            // what was kept is put back on it.
            var chooser = Kaban.UI.Helpers.ColumnChooser.Attach(grid);
            chooser.DefaultKey = key;
            chooser.Changed = () => { if (!restoring) SaveLayout(grid, key, order, descending); };
            grid.ColumnDisplayIndexChanged += (s, e) => { if (!restoring) SaveLayout(grid, key, order, descending); };
            grid.ColumnWidthChanged += (s, e) => { if (!restoring) SaveLayout(grid, key, order, descending); };

            RestoreLayout(grid, key, order, descending);
            restoring = false;
            if (sorting && order.Count > 0) ApplySort(grid, order, descending);
        }

        /// <summary>
        /// Puts a table's kept columns (widths, order, shown) on it again - for a
        /// table that builds its columns anew (another language, other contents).
        /// </summary>
        public static void ReapplyLayout(DataGridView grid, string key)
        {
            if (grid == null || string.IsNullOrEmpty(key)) return;
            RestoreLayout(grid, key, new System.Collections.Generic.List<string>(),
                          new System.Collections.Generic.Dictionary<string, bool>());
        }

        /// <summary>
        /// THE PROGRAM'S TABS, as the PLC Monitor's sheets: the chosen one white with
        /// a dark rule under its bold name, the others light grey.
        /// </summary>
        public static void StyleTabs(TabControl tabs)
        {
            if (tabs == null) return;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.SizeMode = TabSizeMode.Normal;
            tabs.Padding = new Point(16, 5);
            tabs.Font = Body;
            tabs.DrawItem += (s, e) =>
            {
                if (e.Index < 0 || e.Index >= tabs.TabCount) return;
                var r = tabs.GetTabRect(e.Index);
                bool sel = e.Index == tabs.SelectedIndex;
                using (var back = new SolidBrush(sel ? Color.White : Color.FromArgb(238, 240, 243)))
                    e.Graphics.FillRectangle(back, r);
                if (sel)
                    using (var rule = new Pen(Color.FromArgb(60, 66, 76), 2f))
                        e.Graphics.DrawLine(rule, r.Left + 4, r.Bottom - 2, r.Right - 4, r.Bottom - 2);
                var font = sel ? FontVariant(Body, FontStyle.Bold) : Body;
                TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, font, r,
                    sel ? GridInk : Color.FromArgb(80, 86, 96),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            };
        }

        /// <summary>
        /// A virtual table's number of rows, set quickly. Lowering RowCount removes
        /// the rows ONE BY ONE, each with its events - thousands take seconds (the
        /// Dashboard's log tab took 3 to 28 s to change, a search 3 to 10 s). Clearing
        /// them at once and setting the new count takes milliseconds. The cells are
        /// asked for again when they are drawn (CellValueNeeded).
        /// </summary>
        public static void SetRowCount(DataGridView grid, int count)
        {
            if (grid == null) return;
            if (count < grid.RowCount) grid.Rows.Clear();
            if (grid.RowCount != count) grid.RowCount = count;
            grid.Invalidate();
        }

        /// <summary>The headings' menu of a table that has the table tools (null when it has none).</summary>
        public static Kaban.UI.Helpers.ColumnChooser ChooserOf(DataGridView grid)
        {
            if (grid == null || grid.Columns.Count == 0) return null;
            var menu = grid.Columns[0].HeaderCell.ContextMenuStrip;
            return menu == null ? null : menu.Tag as Kaban.UI.Helpers.ColumnChooser;
        }

        /// <summary>
        /// Auto-fits one column, then adds about two characters of air.
        ///
        /// A bare auto-fit leaves the longest value touching both cell borders,
        /// which reads as clipped even when nothing is missing - and the next value
        /// one character longer really will be.
        /// </summary>
        private static void FitColumn(DataGridView grid, int index)
        {
            if (index < 0 || index >= grid.Columns.Count) return;
            grid.AutoResizeColumn(index, DataGridViewAutoSizeColumnMode.DisplayedCells);

            var col = grid.Columns[index];
            int pad = TextRenderer.MeasureText("00", col.DefaultCellStyle.Font ?? grid.Font).Width;
            col.Width = col.Width + pad;
        }

        private static int VisibleCount(DataGridView grid)
        {
            int n = 0;
            foreach (DataGridViewColumn c in grid.Columns) if (c.Visible) n++;
            return n;
        }

        /// <summary>
        /// Sorts by every remembered key at once, least significant applied first.
        ///
        /// A DataGridView sorts on ONE column, so a stable sort run from the last
        /// key backwards produces the same result as a true multi-key sort - and
        /// needs no custom comparer for the bound types.
        /// </summary>
        private static void ApplySort(DataGridView grid,
                                      System.Collections.Generic.List<string> order,
                                      System.Collections.Generic.Dictionary<string, bool> descending)
        {
            // SORTED HERE, not handed to the binding.
            //
            // BindingList<T> - which is what the alarm list and every other table
            // in this program is bound to - reports SupportsSorting == false, so
            // grid.Sort() and IBindingList.ApplySort do nothing at all and fail
            // silently. Reordering the list ourselves is the only thing that
            // actually works, and it works for any list type.
            if (order.Count == 0) { ShowGlyphs(grid, order, descending); return; }

            // AN UNBOUND GRID SORTS ITS OWN ROWS.
            //
            // The alarm history adds rows directly rather than binding a list, so
            // DataSource is null and the bound path below did nothing at all - the
            // headings responded, the arrow moved, and the rows stayed put. A grid
            // that looks like it sorted but did not is worse than one that plainly
            // cannot.
            if (grid.DataSource == null)
            {
                SortRows(grid, order, descending);
                ShowGlyphs(grid, order, descending);
                return;
            }

            var list = grid.DataSource as System.Collections.IList;
            if (list == null || list.Count < 2) { ShowGlyphs(grid, order, descending); return; }

            var items = new System.Collections.Generic.List<object>();
            foreach (var o in list) items.Add(o);

            var keys = new System.Collections.Generic.List<string>();
            foreach (var name in order)
            {
                var col = grid.Columns[name];
                if (col != null && col.Visible && !string.IsNullOrEmpty(col.DataPropertyName))
                    keys.Add(col.DataPropertyName);
                else if (col != null && col.Visible)
                    keys.Add(col.Name);
            }
            if (keys.Count == 0) { ShowGlyphs(grid, order, descending); return; }

            items.Sort((a, b) =>
            {
                for (int i = 0; i < keys.Count; i++)
                {
                    int cmp = CompareBy(a, b, keys[i]);
                    if (cmp == 0) continue;
                    bool desc;
                    descending.TryGetValue(order[i], out desc);
                    return desc ? -cmp : cmp;
                }
                return 0;
            });

            try
            {
                // RaiseListChangedEvents and ResetBindings live on BindingList<T>,
                // not on IBindingList, and the element type is not known here - so
                // they are reached by reflection. Suppressing the events matters:
                // without it the grid rebuilds on every single Add and a modest
                // table visibly redraws itself dozens of times per sort.
                var src = grid.DataSource;
                var raise = src.GetType().GetProperty("RaiseListChangedEvents");
                var reset = src.GetType().GetMethod("ResetBindings");

                if (raise != null && raise.CanWrite) raise.SetValue(src, false, null);

                list.Clear();
                foreach (var o in items) list.Add(o);

                if (raise != null && raise.CanWrite) raise.SetValue(src, true, null);
                if (reset != null) reset.Invoke(src, null);
                else grid.Refresh();
            }
            catch { }

            ShowGlyphs(grid, order, descending);
        }

        /// <summary>
        /// Reorders the rows of an unbound grid by the same key list.
        ///
        /// Cell values are compared as numbers or dates when both sides parse as
        /// one, and as text otherwise - so a Time column orders chronologically
        /// rather than alphabetically, which for "2026-09-17 9:05" versus
        /// "2026-09-17 10:05" are different answers.
        /// </summary>
        private static void SortRows(DataGridView grid,
                                     System.Collections.Generic.List<string> order,
                                     System.Collections.Generic.Dictionary<string, bool> descending)
        {
            if (grid.Rows.Count < 2) return;

            // THE VALUES ARE SORTED AND WRITTEN BACK - the row objects never move.
            //
            // The previous version did Rows.Clear() then AddRange(rows). A
            // DataGridViewRow that has been through Clear() is detached and cannot
            // be re-added intact, so the grid came back with cells from one record
            // sitting beside cells from another: dates ordered correctly while the
            // alarm text stayed put. That is the worst kind of table bug, because
            // every row still looks plausible.
            //
            // Copying the cell VALUES into a list, sorting that, and writing them
            // back into the rows that are already there cannot mismatch: a row is
            // rewritten as one unit.
            //
            // A ROW MOVES WHOLE (22 October): its values AND what rides on it - the row's Tag
            // (the alarm definition of Alarm Settings, the change of the translations' review,
            // a profile's id) and each cell's tag, read-only, tooltip and style. Only the values
            // moved before: a sorted Alarm Settings table saved one alarm's severity and delay
            // into another's definition, and the review of a translation import took one row's
            // text for another key ("An unexpected error" after it, as it fired its events then).
            var cols = new System.Collections.Generic.List<DataGridViewColumn>();
            foreach (DataGridViewColumn c in grid.Columns) cols.Add(c);

            var records = new System.Collections.Generic.List<object[]>();
            var moved = new System.Collections.Generic.List<SortedRow>();
            foreach (DataGridViewRow r in grid.Rows)
            {
                if (r.IsNewRow) continue;
                var values = new object[cols.Count];
                var rec = new SortedRow { Tag = r.Tag, Cells = new SortedCell[cols.Count] };
                for (int i = 0; i < cols.Count; i++)
                {
                    var cell = r.Cells[cols[i].Index];
                    values[i] = cell.Value;
                    rec.Cells[i] = new SortedCell { Tag = cell.Tag, ReadOnly = cell.ReadOnly && !cols[i].ReadOnly && !r.ReadOnly,
                                                    Tip = cell.ToolTipText, Style = cell.HasStyle ? cell.Style : null };
                }
                records.Add(values);
                moved.Add(rec);
            }
            if (records.Count < 2) return;
            var whole = new System.Collections.Generic.Dictionary<object[], SortedRow>();
            for (int k = 0; k < records.Count; k++) whole[records[k]] = moved[k];

            var keyIndex = new System.Collections.Generic.List<int>();
            var keyDesc = new System.Collections.Generic.List<bool>();
            foreach (var name in order)
            {
                var col = grid.Columns[name];
                if (col == null || !col.Visible) continue;
                keyIndex.Add(cols.IndexOf(col));
                bool d; descending.TryGetValue(name, out d);
                keyDesc.Add(d);
            }
            if (keyIndex.Count == 0) return;

            records.Sort((a, b) =>
            {
                for (int i = 0; i < keyIndex.Count; i++)
                {
                    int cmp = CompareCells(a[keyIndex[i]], b[keyIndex[i]]);
                    if (cmp == 0) continue;
                    return keyDesc[i] ? -cmp : cmp;
                }
                return 0;
            });

            grid.SuspendLayout();
            try
            {
                int row = 0;
                foreach (DataGridViewRow r in grid.Rows)
                {
                    if (r.IsNewRow) continue;
                    var values = records[row++];
                    var rec = whole[values];
                    // what rides on the row first: a value's event finds the row's own Tag
                    r.Tag = rec.Tag;
                    for (int i = 0; i < cols.Count; i++)
                    {
                        var cell = r.Cells[cols[i].Index];
                        var c = rec.Cells[i];
                        cell.Tag = c.Tag;
                        if (!cols[i].ReadOnly && !r.ReadOnly) cell.ReadOnly = c.ReadOnly;
                        cell.ToolTipText = c.Tip;
                        // a copy: a style object is the grid's for one cell (its repaints follow it)
                        if (c.Style != null) cell.Style = c.Style.Clone();
                        else if (cell.HasStyle) cell.Style = null;
                    }
                    for (int i = 0; i < cols.Count; i++)
                        r.Cells[cols[i].Index].Value = values[i];
                }
            }
            finally { grid.ResumeLayout(); }
        }

        private sealed class SortedCell { public object Tag; public bool ReadOnly; public string Tip; public DataGridViewCellStyle Style; }
        private sealed class SortedRow { public object Tag; public SortedCell[] Cells; }

        private static int CompareCells(object a, object b)
        {
            string sa = a == null ? "" : a.ToString();
            string sb = b == null ? "" : b.ToString();

            double da, db;
            if (double.TryParse(sa, out da) && double.TryParse(sb, out db))
                return da.CompareTo(db);

            DateTime ta, tb;
            if (DateTime.TryParse(sa, out ta) && DateTime.TryParse(sb, out tb))
                return ta.CompareTo(tb);

            return string.Compare(sa, sb, StringComparison.CurrentCultureIgnoreCase);
        }

        private static int CompareBy(object a, object b, string property)
        {
            var pa = a == null ? null : a.GetType().GetProperty(property);
            var pb = b == null ? null : b.GetType().GetProperty(property);
            if (pa == null || pb == null) return 0;

            var va = pa.GetValue(a, null) as IComparable;
            var vb = pb.GetValue(b, null) as IComparable;
            if (va == null && vb == null) return 0;
            if (va == null) return -1;
            if (vb == null) return 1;
            try { return va.CompareTo(vb); } catch { return 0; }
        }

        /// <summary>
        /// The arrow marks the PRIMARY key only; three arrows would suggest three
        /// independent sorts rather than one ordered set.
        /// </summary>
        private static void ShowGlyphs(DataGridView grid,
                                       System.Collections.Generic.List<string> order,
                                       System.Collections.Generic.Dictionary<string, bool> descending)
        {
            foreach (DataGridViewColumn c in grid.Columns) c.HeaderCell.SortGlyphDirection = SortOrder.None;
            if (order.Count == 0) return;
            var col = grid.Columns[order[0]];
            if (col == null) return;
            bool d; descending.TryGetValue(order[0], out d);
            col.HeaderCell.SortGlyphDirection = d ? SortOrder.Descending : SortOrder.Ascending;
        }

        private static void SaveLayout(DataGridView grid, string key,
                                       System.Collections.Generic.List<string> order,
                                       System.Collections.Generic.Dictionary<string, bool> descending)
        {
            try
            {
                var lines = new System.Collections.Generic.List<string>();
                foreach (DataGridViewColumn c in grid.Columns)
                    lines.Add(string.Join("\t", new[]
                    {
                        "col", c.Name, c.Width.ToString(), c.DisplayIndex.ToString(),
                        c.Visible ? "1" : "0"
                    }));
                foreach (var name in order)
                {
                    bool d; descending.TryGetValue(name, out d);
                    lines.Add(string.Join("\t", new[] { "sort", name, d ? "desc" : "asc" }));
                }

                var path = LayoutPath(key);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, lines);
            }
            catch { }
        }

        private static void RestoreLayout(DataGridView grid, string key,
                                          System.Collections.Generic.List<string> order,
                                          System.Collections.Generic.Dictionary<string, bool> descending)
        {
            try
            {
                var path = LayoutPath(key);
                if (!File.Exists(path)) path = OldLayoutPath(key);
                if (!File.Exists(path)) return;

                foreach (var line in File.ReadAllLines(path))
                {
                    var p = line.Split('\t');
                    if (p.Length >= 5 && p[0] == "col")
                    {
                        var col = grid.Columns[p[1]];
                        if (col == null) continue;
                        int w, idx;
                        if (int.TryParse(p[2], out w) && w > 10)
                        {
                            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                            col.Width = w;
                        }
                        if (int.TryParse(p[3], out idx) && idx >= 0 && idx < grid.Columns.Count)
                            col.DisplayIndex = idx;
                        col.Visible = p[4] == "1";
                    }
                    else if (p.Length >= 3 && p[0] == "sort")
                    {
                        order.Add(p[1]);
                        descending[p[1]] = p[2] == "desc";
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// A table's kept columns: for this panel (beside the defaults, as the PLC
        /// Monitor's are), whoever is signed in. A layout kept by an earlier version
        /// under the Windows user's folder is read once from there.
        /// </summary>
        private static string LayoutPath(string key)
        {
            return Path.Combine(Kaban.UI.Helpers.ColumnChooser.Folder, key + ".cols");
        }

        private static string OldLayoutPath(string key)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "KabanHMI", "Layouts", key + ".cols");
        }

        public static void EnableAutoFit(DataGridView grid)
        {
            if (grid == null) return;

            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            grid.AllowUserToResizeColumns = true;
            grid.AllowUserToResizeRows = true;

            // Double-clicking a divider is the built-in gesture for "fit this
            // column"; it only works while the column is user-resizable, which
            // DisplayedCells mode would otherwise take away.
            grid.ColumnDividerDoubleClick += (s, e) =>
            {
                FitColumn(grid, e.ColumnIndex);
                e.Handled = true;
            };
            grid.RowDividerDoubleClick += (s, e) =>
            {
                grid.AutoResizeRow(e.RowIndex, DataGridViewAutoSizeRowMode.AllCells);
                e.Handled = true;
            };
            // Double-clicking a HEADING fits every column at once.
            grid.ColumnHeaderMouseDoubleClick += (s, e) =>
            {
                for (int i = 0; i < grid.Columns.Count; i++) FitColumn(grid, i);
            };

            FitColumnsToHeadings(grid);
        }

        public static void FitColumnsToHeadings(DataGridView grid, int extra = 28)
        {
            if (grid == null || grid.Columns.Count == 0) return;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;

            // MeasureText without a Graphics: CreateGraphics on a control that has
            // no handle yet returns a useless device context, and a grid built in a
            // constructor has no handle. That is why the widths never changed.
            {
                var font = grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font;
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    var size = TextRenderer.MeasureText(col.HeaderText ?? "", font);
                    // + sort glyph, cell padding and both grid lines
                    int need = size.Width + extra;
                    if (col.MinimumWidth < need) col.MinimumWidth = need;
                    if (col.Width < need) col.Width = need;
                }
            }
        }

        // ==================================================================
        // TOOLTIPS
        // ==================================================================

        /// <summary>Pointer must rest this long before a tooltip appears.</summary>
        public const int TooltipInitialDelayMs = 1000;

        /// <summary>How long a tooltip stays up if the pointer never moves.</summary>
        public const int TooltipAutoPopMs = 10000;

        /// <summary>
        /// The ONE tooltip configuration for the whole program. Use this instead of
        /// `new ToolTip()`.
        ///
        /// WHY A DELAY: WinForms defaults to a ~half-second delay and, because a
        /// screenful of HMI controls all carry tips, popups appeared the instant the
        /// pointer crossed anything - the screen flickered with yellow boxes while
        /// the operator was only moving toward a button. A full second means a tip
        /// appears when someone has actually STOPPED on a control to ask what it is.
        ///
        /// WHY IT EXPIRES: on a panel PC the pointer is often parked and left. A tip
        /// that never times out sits over live values indefinitely, hiding exactly
        /// the readings it was explaining, so it retires after ten seconds.
        /// </summary>
        /// <summary>
        /// THE shared tooltip host. One for the whole program, not one per control.
        ///
        /// A ToolTip is a native window component, and every HMI control was
        /// constructing its own: a page with two hundred bound controls created two
        /// hundred of them, which is most of the time such a page took to open.
        /// They all carry identical settings, so there was never a reason for more
        /// than one - a single ToolTip serves any number of controls through
        /// SetToolTip.
        ///
        /// Callers keep using Tips() exactly as before; they simply get the shared
        /// instance now.
        /// </summary>
        private static ToolTip _shared;

        public static ToolTip Tips()
        {
            if (_shared == null)
            {
                _shared = new SharedToolTip();
                Style(_shared);
            }
            return _shared;
        }

        /// <summary>A tooltip styled like the shared one, owned by the caller - one per window.</summary>
        public static ToolTip CreateTips() { return NewTips(); }

        /// <summary>
        /// The program-wide tooltip. It IGNORES Dispose: many controls hold it,
        /// none owns it, and one control's cleanup disposing it ended tooltips for
        /// every other control - the login crash (ToolTip.CreateHandle) and pages
        /// with no tips. It lives as long as the program.
        /// </summary>
        private sealed class SharedToolTip : ToolTip
        {
            protected override void Dispose(bool disposing) { /* shared - never ended by a borrower */ }
        }

        private static ToolTip NewTips()
        {
            var t = new ToolTip();
            Style(t);
            return t;
        }

        /// <summary>How soon a tooltip comes: View Preferences (normal: a second).</summary>
        public static int TooltipDelayMs { get { return Kaban.Core.Helpers.ViewPreferences.TooltipDelayMs; } }

        private static readonly List<WeakReference> _styledTips = new List<WeakReference>();
        private static bool _tipsHooked;

        /// <summary>The delay on a tooltip (AutomaticDelay first: setting it resets the others).</summary>
        private static void ApplyDelay(ToolTip t)
        {
            int d = TooltipDelayMs;
            t.AutomaticDelay = d;
            t.InitialDelay = d;
            t.ReshowDelay = d;                       // no instant re-pop moving along a row
            t.AutoPopDelay = TooltipAutoPopMs;
        }

        private static void Style(ToolTip t)
        {
            lock (_styledTips)
            {
                _styledTips.RemoveAll(w => !w.IsAlive);
                _styledTips.Add(new WeakReference(t));
                if (!_tipsHooked)
                {
                    _tipsHooked = true;
                    Kaban.Core.Helpers.ViewPreferences.Changed += (s, e) =>
                    {
                        List<ToolTip> live;
                        lock (_styledTips) live = _styledTips.Select(w => w.Target as ToolTip).Where(x => x != null).ToList();
                        foreach (var x in live) ApplyDelay(x);
                    };
                }
            }
            ApplyDelay(t);
            // SHOWN WHETHER OR NOT ITS WINDOW IS ACTIVE. With ShowAlways off a tip
            // appears only while the window the tooltip belongs to is the active
            // one - and the program's ONE shared tooltip belongs to whichever
            // window its first control was on, often a page that is not on screen
            // (a machine tab, a page built to be searched). The pointer rested on a
            // control, its border turned blue, and no tip came. A tip still needs
            // the pointer to REST on a control of this program for a second.
            t.ShowAlways = true;
            t.UseAnimation = false;                  // fade made the 1s delay feel like 1.3s;
            t.UseFading = false;
            // THE MAGNIFIER: Windows draws tips in its own small font, which is
            // exactly what an operator who needs the factor cannot read. Drawn
            // here instead, in the program's font at the factor.
            if (UiScale.Active)
            {
                t.OwnerDraw = true;
                t.Popup += (s, e) =>
                {
                    var text = t.GetToolTip(e.AssociatedControl) ?? "";
                    // A tip shown by the control itself (HoverTip) is not registered.
                    if (text.Length == 0 && e.AssociatedControl != null && e.AssociatedControl == LastShownOn) text = LastShownTip ?? "";
                    var size = TextRenderer.MeasureText(text, TipFont, new Size(UiScale.Px(520), int.MaxValue), TextFormatFlags.WordBreak);
                    e.ToolTipSize = new Size(size.Width + UiScale.Px(12), size.Height + UiScale.Px(8));
                };
                t.Draw += (s, e) =>
                {
                    e.Graphics.FillRectangle(SystemBrushes.Info, e.Bounds);
                    using (var pen = new Pen(Color.FromArgb(118, 118, 118)))
                        e.Graphics.DrawRectangle(pen, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
                    var r = Rectangle.Inflate(e.Bounds, -UiScale.Px(6), -UiScale.Px(4));
                    TextRenderer.DrawText(e.Graphics, e.ToolTipText, TipFont, r, SystemColors.InfoText, TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top);
                };
            }
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, Func<string>> _hoverBuilders =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Control, Func<string>>();

        /// <summary>
        /// The tooltip a control shows, as it would show it now: the one it builds
        /// itself (HoverTip), else the one any of the program's tooltips has for it.
        /// "" when it has none (the notes page shows it for the control picked).
        /// </summary>
        public static string TipOf(Control c)
        {
            if (c == null) return "";
            Func<string> build;
            if (_hoverBuilders.TryGetValue(c, out build))
            {
                try { var s = build(); if (!string.IsNullOrEmpty(s)) return s; } catch { }
            }
            List<ToolTip> live;
            lock (_styledTips) live = _styledTips.Select(w => w.Target as ToolTip).Where(x => x != null).ToList();
            foreach (var t in live)
            {
                string s;
                try { s = t.GetToolTip(c); } catch { s = null; }
                if (!string.IsNullOrEmpty(s)) return s;
            }
            return "";
        }

        /// <summary>The tooltips' font when the magnifier is on.</summary>
        private static readonly Font TipFont = UiScale.NewFont("Segoe UI", 9F);

        /// <summary>
        /// Forces a tooltip DOWN the moment the pointer leaves the control.
        ///
        /// WinForms hides a tip on leave only if it decides it noticed - a tip whose
        /// text was re-set while it was on screen (which ButtonHMI does on every
        /// hover, to rebuild the admin wiring text) can outlive the pointer and hang
        /// over the next control. Hiding explicitly is the only reliable cure.
        /// </summary>
        public static void HideTooltipOnLeave(ToolTip tips, Control c)
        {
            if (tips == null || c == null) return;
            c.MouseLeave += (s, e) => { try { tips.Hide(c); } catch { } };
        }

        /// <summary>
        /// A control's tooltip, BUILT AND SHOWN BY THE CONTROL ITSELF: when the
        /// pointer has rested on it for the tooltip delay, the text is made (so it
        /// says what is true now - the value, a block reason, a login since) and
        /// shown beside the pointer; leaving, pressing or hiding the control puts
        /// it down.
        ///
        /// NOT REGISTERED WITH THE TOOLTIP. The program has one tooltip, and
        /// WinForms ties it to the window of a control it serves; when that window
        /// closes - the keypad, a dialog, a side window - the registrations it held
        /// are dropped. Registering again on every visit (the previous version,
        /// with a Hide() to register afresh) still left the numeric input fields
        /// silent on the panel. Shown explicitly (ToolTip.Show), a tip depends on
        /// nothing but the pointer resting on the control.
        /// </summary>
        private sealed class OwnTip
        {
            public string Text = "";
            /// <summary>The text without its shortcut line ("Ctrl+S\n"), the language it was set in.</summary>
            public string Body = "", Language = "";
            public bool Keyed;
            /// <summary>Its translation key, looked for when first needed (null: not yet; "": none).</summary>
            public string Key;
        }

        /// <summary>
        /// THE TIP IN THE LANGUAGE SHOWN (23 October: "most of the control tooltips (apart from the HMI controls)
        /// doesn't have translation keys so they are shown in English only"): a tip is set as a text - T(key,
        /// English) already looked up - so a page built before a language change kept its tips in the language it
        /// was built in. The text's key is found again (the key with that text in the language it was set in) and
        /// the tip shown in the language now; the notes page lists it with the control's texts (TipKeyOf).
        /// </summary>
        private static string ShownTip(Control c, OwnTip own)
        {
            if (own == null || string.IsNullOrEmpty(own.Text)) return own == null ? "" : own.Text;
            var lang = Kaban.Core.Helpers.LanguageManager.CurrentLanguage ?? "";
            if (string.Equals(lang, own.Language, StringComparison.OrdinalIgnoreCase)) return own.Text;
            var key = KeyOfTip(own);
            if (string.IsNullOrEmpty(key)) return own.Text;
            var body = Kaban.Core.Helpers.LanguageManager.GetString(key, own.Body);
            return own.Keyed ? Kaban.UI.Helpers.Shortcuts.With(Kaban.UI.Helpers.Shortcuts.Of(c), body) : body;
        }

        private static string KeyOfTip(OwnTip own)
        {
            if (own.Key == null)
                own.Key = Kaban.Core.Helpers.LanguageManager.KeyOfTextIn(own.Body, own.Language) ?? Kaban.Core.Helpers.LanguageManager.KeyOfTextIn(own.Body, "EN") ?? "";
            return own.Key;
        }

        /// <summary>The translation key of a control's own tip (null: no tip, or a text of no key).</summary>
        public static string TipKeyOf(Control c)
        {
            OwnTip own;
            if (c == null || !_ownTips.TryGetValue(c, out own) || string.IsNullOrEmpty(own.Text)) return null;
            var key = KeyOfTip(own);
            return key.Length > 0 ? key : null;
        }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, OwnTip> _ownTips =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Control, OwnTip>();

        /// <summary>
        /// A control's tooltip, in place of <c>tips.SetToolTip(c, text)</c>: SHOWN BY
        /// THE CONTROL ITSELF (HoverTip) once the pointer rests on it, with a text
        /// that may be set again at any time (a language change, a state).
        ///
        /// WHY NOT SetToolTip: a registration lives in the tooltip's own window,
        /// which WinForms ties to one of the program's windows; when that window
        /// closes - the keypad, a dialog, a side window - the registrations go
        /// with it. The numeric fields, lamps and checkboxes were moved off it for
        /// that; the toolbar buttons (and the caption bar's, the side bar's) still
        /// used it and were silent on the panel (6 October): no tooltip, so no key
        /// to learn from it either.
        /// </summary>
        public static void SetTip(ToolTip tips, Control c, string text)
        {
            if (c == null) return;
            OwnTip own;
            if (!_ownTips.TryGetValue(c, out own))
            {
                own = new OwnTip();
                _ownTips.Add(c, own);
                var t = tips ?? Tips();
                // never registered as well: two of them would fight over one window
                try { if (!string.IsNullOrEmpty(t.GetToolTip(c))) t.SetToolTip(c, null); } catch { }
                HoverTip(t, c, () => ShownTip(c, own));
                // greyed out it gets no pointer: its parent shows the tip for it
                EventHandler toParent = (s, e) => { if (c.Parent != null) DisabledTips(t, c.Parent); };
                c.ParentChanged += toParent;
                toParent(c, EventArgs.Empty);
            }
            own.Text = text ?? "";
            own.Language = Kaban.Core.Helpers.LanguageManager.CurrentLanguage ?? "";
            own.Key = null;
            own.Body = own.Text;
            own.Keyed = false;
            // a shortcut's line first (Shortcuts.Set): the text after it is the one translated
            var keys = Kaban.UI.Helpers.Shortcuts.Of(c);
            if (keys != Keys.None)
            {
                var head = Kaban.UI.Helpers.Shortcuts.Text(keys) + "\n";
                if (own.Text.StartsWith(head, StringComparison.Ordinal)) { own.Body = own.Text.Substring(head.Length); own.Keyed = true; }
            }
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> _disabledTipHosts =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();

        /// <summary>
        /// THE TIP OF A GREYED-OUT BUTTON: a disabled control gets no pointer at all
        /// (Windows gives it to the window under it), so its own HoverTip never
        /// starts - the floppy before anything was changed, the bin on your own
        /// account. Its parent watches the pointer instead and, once it rests on
        /// such a child, shows the child's tip: what the button does and its key
        /// are worth knowing before it can be pressed.
        /// </summary>
        private static void DisabledTips(ToolTip tips, Control host)
        {
            object unused;
            if (host == null || _disabledTipHosts.TryGetValue(host, out unused)) return;
            _disabledTipHosts.Add(host, host);
            Timer timer = null;
            Control over = null;
            bool shown = false;
            IWin32Window shownOn = null;
            Point nowhere = new Point(int.MinValue, int.MinValue), at = nowhere;
            Action putDown = () =>
            {
                if (timer != null) timer.Stop();
                if (!shown) return;
                shown = false;
                try { tips.Hide(shownOn ?? host); } catch { }
            };
            host.MouseMove += (s, e) =>
            {
                Control child;
                try { child = host.GetChildAtPoint(e.Location, GetChildAtPointSkip.Invisible); } catch { child = null; }
                bool greyed = child != null && !child.Enabled && HasOwnTip(child);
                if (!greyed) { if (over != null) { putDown(); over = null; at = nowhere; } return; }
                if (child != over) { putDown(); over = child; at = nowhere; }
                if (shown || e.Location == at) return;
                at = e.Location;
                if (timer == null)
                {
                    timer = new Timer();
                    timer.Tick += (s2, e2) =>
                    {
                        timer.Stop();
                        var c = over;
                        if (c == null || shown || c.Enabled || c.IsDisposed || host.IsDisposed || !host.IsHandleCreated || !c.Visible) return;
                        OwnTip own;
                        string text = _ownTips.TryGetValue(c, out own) ? ShownTip(c, own) : "";
                        if (string.IsNullOrEmpty(text)) return;
                        try
                        {
                            var p = new Point(at.X + 2, at.Y + (host.Cursor ?? Cursors.Default).Size.Height - 8);
                            LastShownTip = text;
                            LastShownFor = c;
                            // the window it is shown on (the magnified tip's size)
                            shownOn = ShowTip(tips, text, host, p);
                            shown = true;
                        }
                        catch { LastShownTip = null; LastShownOn = null; LastShownFor = null; }
                    };
                }
                timer.Stop();
                timer.Interval = TooltipDelayMs;
                timer.Start();
            };
            host.MouseLeave += (s, e) => { putDown(); over = null; at = nowhere; };
            host.MouseDown += (s, e) => putDown();
            host.VisibleChanged += (s, e) => { if (!host.Visible) putDown(); };
            host.Disposed += (s, e) => { if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; } };
        }

        /// <summary>True when the control's tip is its own (SetTip).</summary>
        public static bool HasOwnTip(Control c)
        {
            OwnTip own;
            return c != null && _ownTips.TryGetValue(c, out own);
        }

        public static void HoverTip(ToolTip tips, Control c, Func<string> build)
        {
            if (tips == null || c == null || build == null) return;
            _hoverBuilders.Remove(c);
            _hoverBuilders.Add(c, build);
            Timer timer = null;
            bool inside = false, shown = false;
            Point at = Point.Empty;
            IWin32Window shownOn = null;

            Action putDown = () =>
            {
                if (timer != null) timer.Stop();
                if (!shown) return;
                shown = false;
                try { tips.Hide(shownOn ?? c); } catch { }
            };

            Point nowhere = new Point(int.MinValue, int.MinValue);
            at = nowhere;
            // A new visit starts afresh, even when the pointer comes back to the very
            // point it left from: only a repeated move WITHIN a visit is ignored.
            c.MouseEnter += (s, e) => { inside = true; at = nowhere; };
            c.MouseMove += (s, e) =>
            {
                inside = true;
                if (shown || e.Location == at) return;
                at = e.Location;
                if (timer == null)
                {
                    timer = new Timer { Interval = TooltipDelayMs };
                    timer.Tick += (s2, e2) =>
                    {
                        timer.Stop();
                        if (!inside || shown || c.IsDisposed || !c.IsHandleCreated || !c.Visible) return;
                        string text;
                        try { text = build() ?? ""; } catch { text = ""; }
                        if (text.Length == 0) return;
                        try
                        {
                            // Set BEFORE Show: the magnified tip measures itself (Popup)
                            // while Show runs, and takes the text from here when the
                            // tooltip has none registered for the control.
                            // Below the pointer, as Windows places its own tips.
                            var p = new Point(at.X + 2, at.Y + (c.Cursor ?? Cursors.Default).Size.Height - 8);
                            LastShownTip = text;
                            LastShownFor = c;
                            shownOn = ShowTip(tips, text, c, p);
                            shown = true;
                        }
                        catch { LastShownTip = null; LastShownOn = null; }
                    };
                }
                timer.Stop();
                timer.Interval = TooltipDelayMs;     // as View Preferences say now
                timer.Start();          // shown once the pointer RESTS for the delay
            };
            c.MouseLeave += (s, e) => { inside = false; at = nowhere; putDown(); };
            c.MouseDown += (s, e) => putDown();
            c.VisibleChanged += (s, e) => { if (!c.Visible) { inside = false; putDown(); } };
            c.Disposed += (s, e) => { if (timer != null) { timer.Stop(); timer.Dispose(); timer = null; } };
        }

        /// <summary>
        /// THE WINDOW A TIP IS SHOWN ON (8 October: "the extended toolbar buttons -
        /// shown at the additional toolbar which appears with the down arrow -
        /// don't show tooltips"). ToolTip.Show shows nothing unless the control's
        /// top window is the ACTIVE one - and a toolbar's overflow (any dropdown) is
        /// a window of its own that never becomes active: the buttons moved into it
        /// were silent. Shown on the dropdown itself, which ToolTip lets through
        /// (it opens without taking the focus), at the same point.
        /// </summary>
        internal static Control TipWindowFor(Control c, ref Point at)
        {
            Control root = c;
            while (root.Parent != null) root = root.Parent;
            var dropDown = root as ToolStripDropDown;
            if (dropDown == null || dropDown == c || !dropDown.IsHandleCreated) return c;
            try { at = dropDown.PointToClient(c.PointToScreen(at)); } catch { return c; }
            return dropDown;
        }

        /// <summary>
        /// Shows a control's tip at a point of its own: on the dropdown it is in, when
        /// it is in one (TipWindowFor), else - or when that fails - on the control.
        /// The window it went on (for Hide). Throws when it could not be shown at all.
        /// </summary>
        private static Control ShowTip(ToolTip tips, string text, Control c, Point p)
        {
            var q = p;
            var on = TipWindowFor(c, ref q);
            LastTipWindow = on;
            if (on != c)
            {
                try
                {
                    LastShownOn = on;
                    tips.Show(text, on, q.X, q.Y, TooltipAutoPopMs);
                    return on;
                }
                catch { }       // (Mono's ToolTip takes a form's window only)
            }
            LastShownOn = c;
            tips.Show(text, c, p.X, p.Y, TooltipAutoPopMs);
            return c;
        }

        /// <summary>The window the last tip was meant for (tests: a dropdown for a button under a toolbar's arrow).</summary>
        internal static Control LastTipWindow;

        /// <summary>The last tip HoverTip showed, and on which control (the magnified tip's size; tests).</summary>
        internal static string LastShownTip;
        internal static Control LastShownOn;
        /// <summary>Whose tip it was: the control itself, or a greyed-out button its parent showed it for.</summary>
        internal static Control LastShownFor;

        /// <summary>Standard emoji tool button: 44x32, rounded, tooltip carries the label.</summary>
        /// <summary>
        /// A BUTTON WITH WORDS, as every one in the program: rounded, 30 px high, the
        /// body font, as wide as its words (at least 96) unless a width is given.
        /// </summary>
        public static Button TextButton(string text, int width = 0, System.EventHandler onClick = null)
        {
            var b = new Button
            {
                Text = text ?? "",
                Font = Body,
                Height = UiScale.Px(30),
                Cursor = Cursors.Hand,
                BackColor = Color.White,
                UseVisualStyleBackColor = false
            };
            b.Width = width > 0 ? UiScale.Px(width) : Math.Max(UiScale.Px(96), TextRenderer.MeasureText(b.Text, Body).Width + UiScale.Px(28));
            Round(b);
            if (onClick != null) b.Click += onClick;
            return b;
        }

        /// <summary>A dialog's OK / Cancel: a TextButton 96 wide that closes it with its answer.</summary>
        public static Button DialogButton(string text, DialogResult result)
        {
            var b = TextButton(text, 96);
            b.DialogResult = result;
            return b;
        }

        public static Button ToolButton(string emoji, string tip, ToolTip tips, System.EventHandler onClick = null)
        {
            // In colour (EmojiArt): the emoji's picture, or undo's curl; a sign
            // that is neither (the turn and mirror arrows) in the toolbar's blue.
            var b = new EmojiButton
            {
                Text = emoji,
                Width = 44,
                Height = 32,
                Font = new Font("Segoe UI", 12F),
                Cursor = Cursors.Hand
            };
            if (!string.IsNullOrEmpty(emoji) && b.Look == "text") b.ForeColor = ToolGlyphs.Ink;
            Round(b);
            // its usual key (the bin Delete, the floppy Ctrl+S ...), first in its tooltip
            var keys = Kaban.UI.Helpers.Shortcuts.Usual(emoji);
            if (keys != Keys.None) Kaban.UI.Helpers.Shortcuts.Set(b, keys, tips, tip);
            else if (tips != null) SetTip(tips, b, tip);
            if (onClick != null) b.Click += onClick;
            return b;
        }

        /// <summary>
        /// A toolbar button with a DRAWN picture (see ToolGlyphs): the size, the
        /// rounded frame and the hover of the emoji buttons beside it. The name is
        /// what a screen reader - and a test - knows it by.
        /// </summary>
        public static Button IconButton(Action<Graphics, Rectangle, bool> draw, string name, string tip, ToolTip tips,
                                        System.EventHandler onClick = null)
        {
            var b = ToolButton("", tip, tips, onClick);
            b.AccessibleName = name;
            b.Paint += (s, e) =>
            {
                // Drawn for factor 1, at the magnifying factor.
                var g = e.Graphics;
                var state = g.Save();
                var r = b.ClientRectangle;
                if (UiScale.Active)
                {
                    float cx = r.Width / 2f, cy = r.Height / 2f;
                    g.TranslateTransform(cx, cy);
                    g.ScaleTransform(UiScale.Factor, UiScale.Factor);
                    g.TranslateTransform(-cx, -cy);
                }
                draw(g, r, b.Enabled);
                g.Restore(state);
            };
            return b;
        }

        /// <summary>
        /// Draws the "you are here" mark under a caption: a rule spanning the TEXT
        /// (not the whole row), sitting clear of the deepest descender the FONT can
        /// produce - not the deepest descender this one string happens to use, so
        /// switching between two active captions never makes the rule jump up and
        /// down depending on which letters are in the word.
        ///
        /// The vertical position comes from the font's own line box, not from a
        /// hand-picked "height minus N". That offset is what put an underline
        /// through p/g/q tails in the first place: it was tuned against one
        /// renderer's idea of where text sits, and a different font size, DPI or
        /// renderer puts the glyph somewhere else.
        /// </summary>
        public static void DrawActiveCaptionRule(Graphics g, Rectangle textBounds, string text,
                                                 Font font, Color color)
        {
            if (g == null || string.IsNullOrEmpty(text) || font == null) return;

            var size = TextRenderer.MeasureText(g, text, font, textBounds.Size,
                                                TextFormatFlags.Left | TextFormatFlags.NoPadding);
            int left = textBounds.Left + Math.Max(0, (textBounds.Width - size.Width) / 2);
            int right = left + Math.Max(size.Width, 4);

            // The caption is vertically centred, so its line box starts here...
            float lineHeight = font.GetHeight(g);
            float lineTop = textBounds.Top + (textBounds.Height - lineHeight) / 2f;

            // ...and the descender bottom is the line box bottom minus the font's
            // internal leading. Two pixels of air below that keeps the rule off the
            // anti-aliased edge of a tail without letting it drift away from the word.
            int y = (int)Math.Ceiling(lineTop + lineHeight) + 1;
            y = Math.Min(y, textBounds.Bottom - 1);   // never leave the control

            using (var pen = new Pen(color, 2f))
                g.DrawLine(pen, left, y, right, y);
        }
    }

    /// <summary>
    /// Keeps a Save button greyed until something has actually changed.
    ///
    /// The Station Connection page already worked this way and it is the right
    /// behaviour everywhere: an enabled Save invites a press, and a press that
    /// writes an unchanged file still rewrites timestamps, still logs an edit, and
    /// on an alarm file still makes the operator wonder what they just changed.
    ///
    /// Reset() after a successful save, MarkDirty() from any edit.
    /// </summary>
    public class DirtyTracker
    {
        private readonly System.Collections.Generic.List<Control> _buttons =
            new System.Collections.Generic.List<Control>();
        private bool _dirty;

        public bool IsDirty { get { return _dirty; } }

        /// <summary>Buttons that only make sense once there is a change.</summary>
        public DirtyTracker Guard(params Control[] buttons)
        {
            foreach (var b in buttons)
                if (b != null) { _buttons.Add(b); b.Enabled = _dirty; }
            return this;
        }

        /// <summary>Wires a grid so any edit marks the page dirty.</summary>
        public DirtyTracker Watch(DataGridView grid)
        {
            if (grid == null) return this;
            grid.CellValueChanged += (s, e) => MarkDirty();
            grid.RowsAdded += (s, e) => MarkDirty();
            grid.RowsRemoved += (s, e) => MarkDirty();
            return this;
        }

        public void MarkDirty()
        {
            if (_dirty) return;
            _dirty = true;
            foreach (var b in _buttons) b.Enabled = true;
        }

        public void Reset()
        {
            _dirty = false;
            foreach (var b in _buttons) b.Enabled = false;
        }
    }

    /// <summary>
    /// The drawn icons the caption bar and the alarm pages share.
    ///
    /// Drawn rather than typed as emoji: a font emoji arrives coloured by the
    /// font, at whatever weight the font chooses, and differs between Windows
    /// versions. These have to sit next to each other and blink in step, so they
    /// are strokes this program controls.
    /// </summary>
    public static class HmiGlyphs
    {
        /// <summary>Circular refresh arrows, outline only.</summary>
        public static void Reset(Graphics g, Rectangle area, Color colour)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = area.Left + area.Width / 2f;
            float cy = area.Top + area.Height / 2f;
            float r = Math.Min(area.Width, area.Height) * 0.30f;

            using (var pen = new Pen(colour, Math.Max(2f, r * 0.34f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, -35, 300);
            }

            double a = -35 * Math.PI / 180.0;
            float hx = cx + (float)(Math.Cos(a) * r);
            float hy = cy + (float)(Math.Sin(a) * r);
            float h = Math.Max(3f, r * 0.55f);
            using (var pen = new Pen(colour, Math.Max(1.8f, r * 0.30f)))
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLines(pen, new[]
                {
                    new PointF(hx - h * 0.15f, hy - h * 0.95f),
                    new PointF(hx + h * 0.85f, hy - h * 0.05f),
                    new PointF(hx - h * 0.25f, hy + h * 0.60f)
                });
            }
        }

        /// <summary>Warning triangle with an exclamation, outline only.</summary>
        public static void Warning(Graphics g, Rectangle area, Color colour)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float side = Math.Min(area.Width, area.Height) * 0.78f;
            float cx = area.Left + area.Width / 2f;
            float cy = area.Top + area.Height / 2f;
            float h = side * 0.88f;

            var top = new PointF(cx, cy - h / 2f);
            var left = new PointF(cx - side / 2f, cy + h / 2f);
            var right = new PointF(cx + side / 2f, cy + h / 2f);

            using (var pen = new Pen(colour, Math.Max(1.8f, side * 0.10f)))
            {
                pen.LineJoin = LineJoin.Round;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawPolygon(pen, new[] { top, right, left });
            }

            using (var pen = new Pen(colour, Math.Max(1.8f, side * 0.11f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx, cy - h * 0.20f, cx, cy + h * 0.04f);
            }

            float dot = Math.Max(1.6f, side * 0.10f);
            using (var pen = new Pen(colour, dot))
                // Clear of the bar: at 34px a dot placed any closer merged with
                // it and the mark read as a solid stroke rather than an exclamation.
                g.DrawEllipse(pen, cx - dot / 2f, cy + h * 0.20f, dot, dot);
        }

        /// <summary>A drawn glyph as a button face, for toolbars that take images.</summary>
        public static Button ToolButton(Action<Graphics, Rectangle, Color> glyph,
                                        string tip, ToolTip tips, EventHandler onClick,
                                        Color colour)
        {
            var b = new Button
            {
                Width = 36, Height = 30, FlatStyle = FlatStyle.Flat,
                BackColor = Color.White, Cursor = Cursors.Hand, Text = ""
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(205, 210, 217);
            b.Paint += (s, e) => glyph(e.Graphics, b.ClientRectangle, colour);
            if (onClick != null) b.Click += onClick;
            if (tips != null && !string.IsNullOrEmpty(tip)) UiStyle.SetTip(tips, b, tip);
            return b;
        }
    }

    /// <summary>
    /// Draws menu items the way every other control in this program shows hover:
    /// a BLUE BORDER, not a filled bar.
    ///
    /// The default renderer floods the whole row, which is the one hover style the
    /// program does not use anywhere else - buttons, numeric fields and grid cells
    /// all outline. A menu that fills instead of outlines reads as a different
    /// kind of surface, and on a dark caption bar the filled bar is the brightest
    /// thing on screen for something as ordinary as passing the mouse over.
    /// </summary>
    public class HmiMenuRenderer : ToolStripProfessionalRenderer
    {
        public HmiMenuRenderer() : base(new HmiMenuColours()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var item = e.Item;
            var g = e.Graphics;
            var r = new Rectangle(1, 0, e.Item.Width - 3, e.Item.Height - 1);

            using (var back = new SolidBrush(Color.White))
                g.FillRectangle(back, 0, 0, e.Item.Width, e.Item.Height);

            if (!item.Selected || !item.Enabled) return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(UiStyle.HoverBorderColor, 1.6f))
                g.DrawRectangle(pen, r);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            // The text colour must not change on hover either - a border is the
            // whole signal, and a colour shift on top of it says it twice.
            if (e.Item.Enabled) e.TextColor = Color.FromArgb(35, 40, 48);
            base.OnRenderItemText(e);
        }
    }

    internal class HmiMenuColours : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.White;
        public override Color MenuItemSelectedGradientBegin => Color.White;
        public override Color MenuItemSelectedGradientEnd => Color.White;
        public override Color MenuItemBorder => UiStyle.HoverBorderColor;
        public override Color MenuBorder => Color.FromArgb(205, 210, 217);
        public override Color ToolStripDropDownBackground => Color.White;
        public override Color ImageMarginGradientBegin => Color.White;
        public override Color ImageMarginGradientMiddle => Color.White;
        public override Color ImageMarginGradientEnd => Color.White;
    }
}

