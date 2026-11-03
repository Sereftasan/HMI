using System;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// Gives any control an optional DESCRIPTION LABEL attached to its LEFT edge
    /// (image-7 style: "Maintenance Period :  [ 0 ]") without dropping a second
    /// control on the screen.
    ///
    /// MATCHING DESIGN: the label AUTO-SIZES to its text and its RIGHT edge hugs
    /// the control with a constant 8px gap. Short and long descriptions therefore
    /// both sit tight against the field - unlike a fixed label column, where a
    /// short text drifts far from its control (the problem visible in the old
    /// screenshot). Alignment across rows comes from aligning the CONTROLS, which
    /// is what the eye follows anyway.
    ///
    /// The label is created/positioned/translated/disposed by the host control;
    /// nothing extra to manage.
    /// </summary>
    internal class AttachedDescription : IDisposable
    {
        private readonly Control _owner;
        private Label _label;
        private string _text = "";

        // Every caption, so a change of View Preferences (captions on the left or
        // the right) reaches the ones on screen at once, and a column of captions
        // can be lined up.
        private static readonly System.Collections.Generic.List<AttachedDescription> _all =
            new System.Collections.Generic.List<AttachedDescription>();

        static AttachedDescription()
        {
            ViewPreferences.Changed += (s, e) => RepositionAll();
        }

        /// <summary>Places every caption again (View Preferences changed).</summary>
        public static void RepositionAll()
        {
            AttachedDescription[] all;
            lock (_all) all = _all.ToArray();
            foreach (var d in all)
            {
                var l = d._label;
                if (l == null || l.IsDisposed || l.Parent == null) continue;
                try
                {
                    if (l.InvokeRequired) l.BeginInvoke(new Action(d.Position));
                    else d.Position();
                }
                catch { /* a page closing meanwhile */ }
            }
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Label, Control> _owners =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Label, Control>();

        /// <summary>The control a caption belongs to (a tap on the caption is a tap on its control), or null.</summary>
        public static Control OwnerOf(Control caption)
        {
            var l = caption as Label;
            Control owner;
            return l != null && _owners.TryGetValue(l, out owner) ? owner : null;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Label, AttachedDescription> _byLabel =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Label, AttachedDescription>();

        /// <summary>The control a caption stands for - its row is the control's (ActiveArea.Row) - or null (TouchInput: its tooltip).</summary>
        public static Control ActingOwnerOf(Control caption)
        {
            var l = caption as Label;
            AttachedDescription d;
            return l != null && _byLabel.TryGetValue(l, out d) && d._acts ? d._owner : null;
        }

        // ---- the caption as a part of its control (ActiveArea.Row) ----

        private bool _acts, _rowHover;

        /// <summary>The caption is part of its control: the pointer over it lights both, a click on it is a click on the control.</summary>
        public bool ActsAsOwner
        {
            get { return _acts; }
            set { _acts = value; ApplyActs(); }
        }

        /// <summary>A click (a tap) on the caption, while it acts as its control.</summary>
        public event EventHandler CaptionClick;

        /// <summary>The pointer came onto the caption (true) or left it (false), while it acts as its control.</summary>
        public event Action<bool> CaptionHover;

        /// <summary>
        /// A press, a move and a lift on the caption, while it acts as its control (the caption's own coordinates) - a switch
        /// slid from the caption beside it (3 November).
        /// </summary>
        public event MouseEventHandler CaptionMouseDown, CaptionMouseMove, CaptionMouseUp;

        private void Relay(MouseEventHandler h, MouseEventArgs e) { if (_acts && h != null) h(_owner, e); }    // (the control decides whether it can change)

        /// <summary>The caption's light while its row is under the pointer (the control or the caption).</summary>
        public static readonly Color RowHoverBack = Color.FromArgb(236, 242, 250);

        /// <summary>The row under the pointer: the caption lit (only while it acts as its control).</summary>
        public void SetRowHover(bool on)
        {
            _rowHover = on;
            if (_label == null || _label.IsDisposed) return;
            var want = _acts && on ? (IsRow ? UiStyle.RowHoverFill : RowHoverBack) : Color.Transparent;
            if (_label.BackColor != want) _label.BackColor = want;
        }

        /// <summary>Lit now (tests).</summary>
        public bool RowLit { get { return _label != null && _acts && _rowHover; } }

        /// <summary>The caption's label (tests).</summary>
        public Label CaptionLabel { get { return _label; } }

        // ---- a settings row (3 November: "make all the other machine setting pages controls just same as the ones at this page
        // (the controls are divided with a light line in between them)") ----

        private int _rowWidth;

        /// <summary>
        /// 0: the caption beside its control. A width (factor 1): a settings row that wide, as the Options page's switch rows -
        /// the caption is the row itself, behind its control at the row's right end (8 px in): its words at the row's left,
        /// with no colon, a light line under it (UiStyle.RowRule), UiStyle.SettingRowHeight high; the pointer on the row lights
        /// it all (ActiveArea.Row).
        /// </summary>
        public int RowWidth
        {
            get { return _rowWidth; }
            set { int v = Math.Max(0, value); if (v == _rowWidth) return; _rowWidth = v; Sync(); }
        }

        /// <summary>Drawn as a settings row now (a width given, the caption on the left).</summary>
        public bool IsRow { get { return _rowWidth > 0 && _side == DescriptionSide.Left; } }

        private bool _placedAsRow;

        /// <summary>
        /// The row: from (its control's right end + 8) - RowWidth to its control's right end + 8, as high as a settings row and
        /// centred on its control; the words kept clear of the control (the label's right padding), cut with "..." if long.
        /// Measured at the screen scale (Px): the page is laid out once more after it is scaled.
        /// </summary>
        private void PositionRow()
        {
            int pad = UiScale.Px(8), h = UiScale.Px(UiStyle.SettingRowHeight), w = UiScale.Px(_rowWidth);
            if (_label.AutoSize) _label.AutoSize = false;
            if (!_label.AutoEllipsis) _label.AutoEllipsis = true;
            if (_label.TextAlign != ContentAlignment.MiddleLeft) _label.TextAlign = ContentAlignment.MiddleLeft;
            var p = new Padding(pad, 0, _owner.Width + 2 * pad, 1);
            if (_label.Padding != p) _label.Padding = p;
            int right = _owner.Right + pad;
            _label.SetBounds(right - w, _owner.Top + (_owner.Height - h) / 2, w, h);
            // behind its control (it lies under it)
            var parent = _label.Parent;
            int li = parent.Controls.GetChildIndex(_label), oi = parent.Controls.GetChildIndex(_owner, false);
            if (oi >= 0 && li < oi) parent.Controls.SetChildIndex(_label, oi);
            _placedAsRow = true;
            _label.Invalidate();
        }

        /// <summary>The light line under the row.</summary>
        private void PaintRow(object sender, PaintEventArgs e)
        {
            if (!IsRow || _label == null) return;
            using (var pen = new Pen(UiStyle.RowRule)) e.Graphics.DrawLine(pen, 0, _label.Height - 1, _label.Width, _label.Height - 1);
        }

        private void ApplyActs()
        {
            if (_label == null) return;
            _label.Cursor = _acts && _owner.Enabled ? _owner.Cursor : Cursors.Default;
            SetRowHover(_rowHover);
        }

        public AttachedDescription(Control owner)
        {
            _owner = owner;
            lock (_all) _all.Add(this);
            _owner.ParentChanged += (s, e) => Sync();
            _owner.LocationChanged += (s, e) => Position();
            _owner.SizeChanged += (s, e) => Position();
            // The designer sets properties BEFORE the final Location/parenting, and
            // DPI autoscaling moves controls after creation - re-anchor once the
            // handle exists and whenever the label's own AutoSize width settles,
            // otherwise the label could be left at a stale position (bug report:
            // description rendered far from its button).
            _owner.HandleCreated += (s, e) =>
            {
                Sync();
                if (_owner.IsHandleCreated)
                    _owner.BeginInvoke(new Action(Position));   // after the layout pass
            };
            _owner.VisibleChanged += (s, e) => { Follow(); Position(); };
            _owner.ParentChanged += (s, e) => WatchParent();
            LanguageManager.LanguageChanged += OnLanguage;
        }

        /// <summary>
        /// The caption is hidden with its control. A field hidden by its
        /// VisibilityConditions (a setting shown only in auto mode while the
        /// machine runs, say) left its caption on the page - "Saw Diameter:"
        /// beside nothing, which read as a broken field.
        ///
        /// Decided only while the page itself is on show: then Visible is the
        /// control's own state. A hidden page hides both anyway, and its showing
        /// again decides once more.
        /// </summary>
        private void Follow()
        {
            if (_label == null || _label.Parent == null || !_label.Parent.Visible) return;
            if (_label.Visible != _owner.Visible) _label.Visible = _owner.Visible;
        }

        private Control _watched;
        private void WatchParent()
        {
            if (_watched != null) _watched.VisibleChanged -= OnParentVisible;
            _watched = _owner.Parent;
            if (_watched != null) _watched.VisibleChanged += OnParentVisible;
        }
        private void OnParentVisible(object s, EventArgs e) { Follow(); }

        /// <summary>
        /// Translation key OR literal text: looked up in the translation files for
        /// the active language; when the key is absent it is rendered as-is.
        /// </summary>
        public string Text { get { return _text; } set { _text = value ?? ""; Sync(); } }

        /// <summary>
        /// Hidden while the owner shows the text itself - a numeric field in bar
        /// view carries its name in its own heading strip.
        /// </summary>
        public bool Suppressed
        {
            get { return _suppressed; }
            set { if (_suppressed == value) return; _suppressed = value; Sync(); }
        }
        private bool _suppressed;

        private bool _appendColon = true;

        /// <summary>Appends " :" to the caption unless it already ends with one.</summary>
        public bool AppendColon
        {
            get { return _appendColon; }
            set { _appendColon = value; Sync(); }
        }

        private void OnLanguage(object s, EventArgs e) { Sync(); }

        private void Sync()
        {
            bool wanted = !string.IsNullOrEmpty(_text) && !_suppressed;
            if (!wanted || _owner.Parent == null)
            {
                if (_label != null && _label.Parent != null) _label.Parent.Controls.Remove(_label);
                return;
            }

            if (_label == null)
            {
                _label = new Label
                {
                    AutoSize = true,
                    AutoEllipsis = false,
                    TextAlign = ContentAlignment.MiddleRight,
                    Padding = new Padding(2, 0, 2, 0),
                    Font = UiStyle.Body,
                    ForeColor = Color.FromArgb(40, 44, 52),
                    BackColor = Color.Transparent
                };
                _label.SizeChanged += (s, e) => Position();   // AutoSize settled -> re-anchor
                _owners.Add(_label, _owner);
                _byLabel.Add(_label, this);
                _label.MouseEnter += (s, e) => { if (_acts) { var h = CaptionHover; if (h != null) h(true); } };
                _label.MouseLeave += (s, e) => { if (_acts) { var h = CaptionHover; if (h != null) h(false); } };
                _label.Click += (s, e) => { if (_acts && _owner.Enabled && _owner.Visible) { var h = CaptionClick; if (h != null) h(_owner, EventArgs.Empty); } };
                _label.Paint += PaintRow;
                _label.MouseDown += (s, e) => Relay(CaptionMouseDown, e);
                _label.MouseMove += (s, e) => Relay(CaptionMouseMove, e);
                _label.MouseUp += (s, e) => Relay(CaptionMouseUp, e);
                ApplyActs();
            }
            // Key first, literal fallback - so DescriptionText is BOTH the key and
            // the text shown when no translation exists; there is no second property
            // to keep in step.
            string shown = LanguageManager.GetString(_text, _text);

            // A trailing colon is added automatically: every caption in the HMI reads
            // "Width :", and typing it on each control invites inconsistency.
            if (AppendColon && _side == DescriptionSide.Left && !IsRow &&
                shown.Length > 0 && !shown.TrimEnd().EndsWith(":"))
                shown = shown.TrimEnd() + ":";
            _label.Text = shown;

            if (_label.Parent != _owner.Parent)
            {
                if (_label.Parent != null) _label.Parent.Controls.Remove(_label);
                _owner.Parent.Controls.Add(_label);
            }
            Follow();
            Position();
        }

        private DescriptionSide _side = DescriptionSide.Left;

        /// <summary>
        /// Which side of the control the caption sits on. Small square controls
        /// (lamps, checkboxes) read better with the caption on the RIGHT: the
        /// control then lines up with the left edge of everything else on the page
        /// instead of floating in the middle of its label's space.
        /// </summary>
        public DescriptionSide Side
        {
            get { return _side; }
            set { _side = value; Sync(); }      // (a row's caption has no colon)
        }

        private bool _positioning;

        private void Position()
        {
            if (_label == null || _label.Parent == null) return;

            // RE-ENTRANCY GUARD: re-measuring the label raises SizeChanged, which is
            // wired back to Position() - without this the pair recursed until the
            // stack overflowed as soon as a caption needed re-measuring.
            if (_positioning) return;
            _positioning = true;
            try
            {
                PositionCore();
            }
            finally { _positioning = false; }
        }

        /// <summary>
        /// The captions of a column: on the left of controls that start at the
        /// same edge (within 3 px) on the same page.
        /// </summary>
        private System.Collections.Generic.List<AttachedDescription> Column()
        {
            var col = new System.Collections.Generic.List<AttachedDescription>();
            var parent = _label != null ? _label.Parent : null;
            if (parent == null) return col;
            lock (_all)
                foreach (var d in _all)
                    if (d._side == DescriptionSide.Left && d._label != null && d._label.Parent == parent && !d._label.IsDisposed &&
                        Math.Abs(d._owner.Left - _owner.Left) <= 3)
                        col.Add(d);
            return col;
        }

        private void PositionCore()
        {
            if (_label == null || _label.Parent == null) return;
            // 2px. A caption is part of the control it names, and at any real
            // distance the eye has to decide which control a label belongs to -
            // which on a row of lamps is exactly the wrong question to raise.
            const int gap = 2;

            if (IsRow) { PositionRow(); return; }
            if (_placedAsRow)
            {
                // a row no more: a caption beside its control again
                _placedAsRow = false;
                _label.Padding = new Padding(2, 0, 2, 0);
                _label.AutoEllipsis = false;
                _label.AutoSize = true;
            }

            if (_side == DescriptionSide.Right)
            {
                _label.Left = _owner.Right + gap;
                _label.Top = _owner.Top + (_owner.Height - _label.Height) / 2;
                return;
            }

            // CAPTIONS ON THE LEFT (View Preferences): the captions of a column of
            // fields start at one edge - the widest one's - and read as a list down
            // the page; each still ends at its own field.
            if (ViewPreferences.CaptionsLeft)
            {
                var col = Column();
                int widest = 0;
                foreach (var d in col) widest = Math.Max(widest, d._label.PreferredWidth);
                int origin = _label.Parent is ScrollableControl ? ((ScrollableControl)_label.Parent).AutoScrollPosition.X : 0;
                foreach (var d in col)
                {
                    int at = Math.Max(origin, d._owner.Left - widest - gap);
                    // AS HIGH AS ITS TEXT NEEDS, measured now: a caption no longer sized by
                    // itself kept the height it had, and at a smaller screen scale (80%) the
                    // scaled height was less than the scaled font needs - the bottom of the
                    // letters was cut off (12 October: "the description texts' bottom side
                    // doesn't get rendered").
                    int h = Math.Max(d._label.PreferredHeight,
                                     TextRenderer.MeasureText(d._label.Text ?? "", d._label.Font).Height + d._label.Padding.Vertical);
                    d._label.AutoSize = false;
                    d._label.TextAlign = ContentAlignment.MiddleLeft;
                    d._label.SetBounds(at, d._owner.Top + (d._owner.Height - h) / 2, Math.Max(10, d._owner.Left - gap - at), h);
                }
                return;
            }
            if (_label.TextAlign != ContentAlignment.MiddleRight) _label.TextAlign = ContentAlignment.MiddleRight;

            // A LEFT caption stays RIGHT-ALIGNED against its control. When it is too
            // long it loses its START, keeping " ... Position :" beside the box -
            // which reads far better than shifting the label and leaving a ragged
            // left edge down the page.
            //
            // SCROLLING: an AutoScroll panel MOVES its children's Location, so a
            // control scrolled past the top-left goes negative. Measuring the clip
            // against Left alone therefore shrank the label while scrolled and left
            // it shrunken when scrolled back - which is how the first control's
            // description ended up detached. Everything below is measured against
            // the SCROLLED ORIGIN, and AutoSize is restored whenever there is room.
            var parent = _label.Parent;
            int originX = 0;
            var scrollable = parent as ScrollableControl;
            if (scrollable != null) originX = scrollable.AutoScrollPosition.X;   // <= 0

            if (!_label.AutoSize)
            {
                _label.AutoSize = true;                 // re-measure at full width
                _label.PerformLayout();
            }

            int left = _owner.Left - _label.Width - gap;
            if (left < originX)
            {
                // genuinely no room, even accounting for the scroll offset
                int room = _owner.Left - gap - originX;
                if (room > 20)
                {
                    _label.AutoSize = false;
                    _label.TextAlign = ContentAlignment.MiddleRight;
                    _label.Width = room;
                }
                left = originX;
            }
            _label.Left = left;
            _label.Top = _owner.Top + (_owner.Height - _label.Height) / 2;
        }

        public void Dispose()
        {
            lock (_all) _all.Remove(this);
            LanguageManager.LanguageChanged -= OnLanguage;
            if (_watched != null) { _watched.VisibleChanged -= OnParentVisible; _watched = null; }
            if (_label != null) { _label.Dispose(); _label = null; }
        }
    }
}

