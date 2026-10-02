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

        public AttachedDescription(Control owner)
        {
            _owner = owner;
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
            }
            // Key first, literal fallback - so DescriptionText is BOTH the key and
            // the text shown when no translation exists; there is no second property
            // to keep in step.
            string shown = LanguageManager.GetString(_text, _text);

            // A trailing colon is added automatically: every caption in the HMI reads
            // "Width :", and typing it on each control invites inconsistency.
            if (AppendColon && _side == DescriptionSide.Left &&
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
            set { _side = value; Position(); }
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

        private void PositionCore()
        {
            if (_label == null || _label.Parent == null) return;
            // 2px. A caption is part of the control it names, and at any real
            // distance the eye has to decide which control a label belongs to -
            // which on a row of lamps is exactly the wrong question to raise.
            const int gap = 2;

            if (_side == DescriptionSide.Right)
            {
                _label.Left = _owner.Right + gap;
                _label.Top = _owner.Top + (_owner.Height - _label.Height) / 2;
                return;
            }

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
            LanguageManager.LanguageChanged -= OnLanguage;
            if (_watched != null) { _watched.VisibleChanged -= OnParentVisible; _watched = null; }
            if (_label != null) { _label.Dispose(); _label = null; }
        }
    }
}

