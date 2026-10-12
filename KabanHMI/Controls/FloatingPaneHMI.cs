using System;
using System.Drawing;
using System.Windows.Forms;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// A free-floating pane inside a normal panel: draggable by its caption,
    /// resizable from any edge, minimisable to its caption bar, and raised above
    /// its neighbours when clicked. Panes may overlap, so the operator can arrange
    /// the workspace however the current job needs.
    ///
    /// Put the real content in <see cref="Content"/>; the surrounding border is
    /// deliberately left as pane padding so it can receive the resize drags.
    /// </summary>
    public class FloatingPaneHMI : Panel
    {
        private const int Grip = 6;          // resize border thickness
        private const int TitleH = 26;

        private readonly Panel _title = new Panel();
        private readonly Label _caption = new Label();
        private readonly Button _btnMin = new Button();
        private readonly Button _btnMax = new Button();
        private readonly Button _btnReset = new Button();

        private bool _dragging, _resizing;
        private Point _mouseDown;
        private Rectangle _startBounds;
        private ResizeEdge _edge;
        private int _restoreHeight;

        // The bounds to come back to. Held for BOTH minimise and maximise, because
        // a pane can be maximised, then minimised, then restored - and it has to
        // land back where the operator last put it, not where it was maximised to.
        private Rectangle _restoreBounds = Rectangle.Empty;
        private PaneState _state = PaneState.Normal;

        private enum ResizeEdge { None, L, R, T, B, TL, TR, BL, BR }

        /// <summary>Where a pane is: floating, filling the page, or parked.</summary>
        public enum PaneState { Normal, Maximized, Minimized }

        /// <summary>Host for the pane's real content.</summary>
        public Panel Content { get; private set; }

        /// <summary>Stable identifier used when a layout is saved.</summary>
        public string Key { get; set; }

        public event EventHandler Activated;

        /// <summary>Raised by the pane's reset button (grouping, sorting, columns...).</summary>
        public event EventHandler ResetRequested;

        /// <summary>Shows the reset button - only useful on panes that hold a table.</summary>
        public bool ShowReset
        {
            get { return _btnReset.Visible; }
            set { _btnReset.Visible = value; LayoutTitleButtons(); }
        }

        public FloatingPaneHMI(string key, string caption)
        {
            Key = key;
            BackColor = Color.FromArgb(206, 212, 220);       // frame colour
            Padding = new Padding(Grip);
            MinimumSize = new Size(160, TitleH + 2 * Grip);

            _title.Dock = DockStyle.Top;
            _title.Height = TitleH;
            _title.BackColor = Color.FromArgb(232, 236, 241);
            _title.Cursor = Cursors.SizeAll;

            _caption.Text = caption;
            _caption.AutoSize = true;
            _caption.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            _caption.ForeColor = Color.FromArgb(35, 40, 48);
            _caption.Location = new Point(8, 6);
            _caption.Cursor = Cursors.SizeAll;
            _title.Controls.Add(_caption);

            _btnMin.Text = "\u2013";
            _btnMin.Width = 26;
            _btnMin.Height = 20;
            _btnMin.FlatStyle = FlatStyle.Flat;
            _btnMin.FlatAppearance.BorderSize = 0;
            _btnMin.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnMin.Click += (s, e) => State = (State == PaneState.Minimized)
                                              ? PaneState.Normal : PaneState.Minimized;
            Kaban.UI.Controls.UiStyle.SetTip(UiStyle.Tips(), _btnMin, "Minimize this pane to the bottom-left");

            StyleTitleButton(_btnMax, "\u25A1");
            _btnMax.Click += (s, e) => State = (State == PaneState.Maximized)
                                              ? PaneState.Normal : PaneState.Maximized;
            Kaban.UI.Controls.UiStyle.SetTip(UiStyle.Tips(), _btnMax, "Maximize this pane to the whole page");
            _title.Controls.Add(_btnMax);
            _title.Controls.Add(_btnMin);

            _btnReset.Text = "\u21BA";
            _btnReset.Width = 26;
            _btnReset.Height = 20;
            _btnReset.FlatStyle = FlatStyle.Flat;
            _btnReset.FlatAppearance.BorderSize = 0;
            _btnReset.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnReset.Visible = false;
            _btnReset.Click += (s, e) => { if (ResetRequested != null) ResetRequested(this, EventArgs.Empty); };
            Kaban.UI.Controls.UiStyle.SetTip(UiStyle.Tips(), _btnReset, "Reset grouping, sorting and columns");
            _title.Controls.Add(_btnReset);

            _title.Resize += (s, e) => LayoutTitleButtons();

            Content = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            Controls.Add(Content);
            Controls.Add(_title);

            // dragging by the caption
            _title.MouseDown += TitleMouseDown;
            _caption.MouseDown += TitleMouseDown;
            _title.MouseMove += TitleMouseMove;
            _caption.MouseMove += (s, e) => TitleMouseMove(s, new MouseEventArgs(e.Button, e.Clicks,
                                                            e.X + _caption.Left, e.Y + _caption.Top, e.Delta));
            _title.MouseUp += (s, e) => { _dragging = false; };
            _caption.MouseUp += (s, e) => { _dragging = false; };
            // Double-click maximises, the way every other title bar behaves.
            _title.DoubleClick += (s, e) => State = (State == PaneState.Maximized)
                                                   ? PaneState.Normal : PaneState.Maximized;

            // resizing from the padding border
            MouseDown += PaneMouseDown;
            MouseMove += PaneMouseMove;
            MouseUp += (s, e) => { _resizing = false; Cursor = Cursors.Default; };
        }

        private static void StyleTitleButton(Button b, string glyph)
        {
            b.Text = glyph;
            b.Width = 26;
            b.Height = 20;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        }

        private void LayoutTitleButtons()
        {
            // right to left: minimise, maximise, reset
            _btnMin.Location   = new Point(_title.Width - _btnMin.Width - 3, 3);
            _btnMax.Location   = new Point(_btnMin.Left - _btnMax.Width - 2, 3);
            _btnReset.Location = new Point(_btnMax.Left - _btnReset.Width - 2, 3);
        }

        /// <summary>
        /// Where this pane is: floating, filling the page, or parked at the
        /// bottom-left.
        ///
        /// Held in an explicit FIELD, not derived from Content.Visible: a control
        /// reports Visible = false whenever any ancestor is hidden, so navigating
        /// away from the page made every pane look minimised - and the layout was
        /// then SAVED that way, which is why the page sometimes reopened with
        /// everything collapsed.
        /// </summary>
        public PaneState State
        {
            get { return _state; }
            set
            {
                if (value == _state) return;

                // Capture the floating bounds BEFORE leaving Normal, and only then:
                // going Maximized -> Minimized must not record the maximised size as
                // the place to come back to.
                if (_state == PaneState.Normal) _restoreBounds = Bounds;

                _state = value;

                switch (value)
                {
                    case PaneState.Minimized:
                        Content.Visible = false;
                        Height = TitleH + 2 * Grip;
                        _btnMin.Text = "\u25B2";      // up: bring me back
                        _btnMax.Visible = false;      // meaningless while parked
                        break;

                    case PaneState.Maximized:
                        Content.Visible = true;
                        _btnMin.Text = "\u2013";
                        _btnMax.Visible = true;
                        _btnMax.Text = "\u2750";      // restore-down glyph
                        FillParent();
                        break;

                    default:                          // Normal
                        Content.Visible = true;
                        _btnMin.Text = "\u2013";
                        _btnMax.Visible = true;
                        _btnMax.Text = "\u25A1";
                        if (_restoreBounds != Rectangle.Empty) Bounds = _restoreBounds;
                        break;
                }

                if (StateChanged != null) StateChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>Raised on every state change, so the host can re-park the tray.</summary>
        public event EventHandler StateChanged;

        /// <summary>Kept for older call sites; minimise now parks the pane.</summary>
        public bool Minimized
        {
            get { return _state == PaneState.Minimized; }
            set { State = value ? PaneState.Minimized : PaneState.Normal; }
        }

        public bool IsMaximized { get { return _state == PaneState.Maximized; } }

        /// <summary>The bounds this pane returns to when restored.</summary>
        public Rectangle RestoreBounds
        {
            get { return _state == PaneState.Normal ? Bounds : _restoreBounds; }
            set { _restoreBounds = value; if (_state == PaneState.Normal) Bounds = value; }
        }

        private void FillParent()
        {
            if (Parent == null) return;
            Bounds = new Rectangle(0, 0, Parent.ClientSize.Width, Parent.ClientSize.Height);
            BringToFront();
        }

        /// <summary>Keeps a maximised pane filling the page when the window resizes.</summary>
        internal void ReflowForParent()
        {
            if (_state == PaneState.Maximized) FillParent();
        }

        public void Raise()
        {
            BringToFront();
            if (Activated != null) Activated(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e) { Raise(); base.OnMouseDown(e); }

        /// <summary>
        /// True while the pane may be dragged or resized. A maximised pane fills the
        /// page by definition, and a parked one is positioned by the tray - letting
        /// either be dragged would leave it somewhere neither state can explain.
        /// </summary>
        private bool CanMove { get { return _state == PaneState.Normal; } }

        // clicking anywhere inside the content should also raise the pane
        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            HookRaise(e.Control);
        }

        private void HookRaise(Control c)
        {
            c.MouseDown += (s, ev) => Raise();
            c.ControlAdded += (s, ev) => HookRaise(ev.Control);
            foreach (Control child in c.Controls) HookRaise(child);
        }

        private void TitleMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            Raise();
            if (!CanMove) return;      // maximised fills the page; parked is tray-placed
            _dragging = true;
            _mouseDown = Control.MousePosition;
            _startBounds = Bounds;
        }

        private void TitleMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            var now = Control.MousePosition;
            int dx = now.X - _mouseDown.X, dy = now.Y - _mouseDown.Y;

            // Clamp the TARGET first and assign once. Moving out and being pulled
            // back was two position changes per mouse move, which is what flickered.
            int x = _startBounds.Left + dx, y = _startBounds.Top + dy;
            if (Parent != null)
            {
                x = Math.Max(0, Math.Min(x, Math.Max(0, Parent.ClientSize.Width - Width)));
                y = Math.Max(0, Math.Min(y, Math.Max(0, Parent.ClientSize.Height - Height)));
            }
            if (Left != x || Top != y) Location = new Point(x, y);
        }

        private void PaneMouseDown(object sender, MouseEventArgs e)
        {
            _edge = EdgeAt(e.Location);
            if (_edge == ResizeEdge.None || e.Button != MouseButtons.Left || !CanMove) return;
            _resizing = true;
            _mouseDown = Control.MousePosition;
            _startBounds = Bounds;
        }

        private void PaneMouseMove(object sender, MouseEventArgs e)
        {
            if (!_resizing)
            {
                Cursor = CursorFor(EdgeAt(e.Location));
                return;
            }
            var now = Control.MousePosition;
            int dx = now.X - _mouseDown.X, dy = now.Y - _mouseDown.Y;
            var b = _startBounds;

            switch (_edge)
            {
                case ResizeEdge.L: b = new Rectangle(b.Left + dx, b.Top, b.Width - dx, b.Height); break;
                case ResizeEdge.R: b = new Rectangle(b.Left, b.Top, b.Width + dx, b.Height); break;
                case ResizeEdge.T: b = new Rectangle(b.Left, b.Top + dy, b.Width, b.Height - dy); break;
                case ResizeEdge.B: b = new Rectangle(b.Left, b.Top, b.Width, b.Height + dy); break;
                case ResizeEdge.TL: b = new Rectangle(b.Left + dx, b.Top + dy, b.Width - dx, b.Height - dy); break;
                case ResizeEdge.TR: b = new Rectangle(b.Left, b.Top + dy, b.Width + dx, b.Height - dy); break;
                case ResizeEdge.BL: b = new Rectangle(b.Left + dx, b.Top, b.Width - dx, b.Height + dy); break;
                case ResizeEdge.BR: b = new Rectangle(b.Left, b.Top, b.Width + dx, b.Height + dy); break;
            }
            if (b.Width < MinimumSize.Width) b.Width = MinimumSize.Width;
            if (b.Height < MinimumSize.Height) b.Height = MinimumSize.Height;
            if (Parent != null)
            {
                if (b.Left < 0) { b.Width += b.Left; b.X = 0; }
                if (b.Top < 0) { b.Height += b.Top; b.Y = 0; }
                if (b.Right > Parent.ClientSize.Width) b.Width = Parent.ClientSize.Width - b.Left;
                if (b.Bottom > Parent.ClientSize.Height) b.Height = Parent.ClientSize.Height - b.Top;
                if (b.Width < MinimumSize.Width) b.Width = MinimumSize.Width;
                if (b.Height < MinimumSize.Height) b.Height = MinimumSize.Height;
            }
            Bounds = b;
        }

        /// <summary>
        /// Keeps the pane WHOLLY inside the page. The previous version allowed the
        /// pane to hang past the edge and then pulled it back, so a drag beyond the
        /// border oscillated between the two positions and flickered. Clamping the
        /// full rectangle up-front means a drag simply stops at the edge.
        /// </summary>
        private void ClampIntoParent()
        {
            if (Parent == null) return;
            int maxW = Math.Max(MinimumSize.Width, Parent.ClientSize.Width);
            int maxH = Math.Max(MinimumSize.Height, Parent.ClientSize.Height);
            int w = Math.Min(Width, maxW), h = Math.Min(Height, maxH);
            int x = Math.Max(0, Math.Min(Left, maxW - w));
            int y = Math.Max(0, Math.Min(Top, maxH - h));
            var target = new Rectangle(x, y, w, h);
            if (Bounds != target) Bounds = target;
        }

        private ResizeEdge EdgeAt(Point p)
        {
            bool l = p.X <= Grip, r = p.X >= Width - Grip;
            bool t = p.Y <= Grip, b = p.Y >= Height - Grip;
            if (l && t) return ResizeEdge.TL;
            if (r && t) return ResizeEdge.TR;
            if (l && b) return ResizeEdge.BL;
            if (r && b) return ResizeEdge.BR;
            if (l) return ResizeEdge.L;
            if (r) return ResizeEdge.R;
            if (t) return ResizeEdge.T;
            if (b) return ResizeEdge.B;
            return ResizeEdge.None;
        }

        private static Cursor CursorFor(ResizeEdge e)
        {
            switch (e)
            {
                case ResizeEdge.L:
                case ResizeEdge.R: return Cursors.SizeWE;
                case ResizeEdge.T:
                case ResizeEdge.B: return Cursors.SizeNS;
                case ResizeEdge.TL:
                case ResizeEdge.BR: return Cursors.SizeNWSE;
                case ResizeEdge.TR:
                case ResizeEdge.BL: return Cursors.SizeNESW;
                default: return Cursors.Default;
            }
        }

        /// <summary>"x,y,w,h,minimised" - one line of a saved layout.</summary>
        /// <summary>
        /// "left,top,width,height,state" - always the RESTORE bounds, so a layout
        /// saved while a pane was maximised or parked still reopens it where the
        /// operator had actually placed it.
        /// </summary>
        public string Serialize()
        {
            var r = RestoreBounds;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                                 "{0},{1},{2},{3},{4}", r.Left, r.Top, r.Width, r.Height, (int)_state);
        }

        public void Deserialize(string text)
        {
            var parts = (text ?? "").Split(',');
            if (parts.Length < 5) return;
            int x, yy, w, h, m;
            if (!int.TryParse(parts[0], out x) || !int.TryParse(parts[1], out yy) ||
                !int.TryParse(parts[2], out w) || !int.TryParse(parts[3], out h) ||
                !int.TryParse(parts[4], out m)) return;
            // A saved height smaller than a usable pane means the entry was written
            // before the page was laid out; fall back to a sensible size instead of
            // restoring something the operator cannot see into.
            const int usable = 120;
            _state = PaneState.Normal;              // set the field, not the property:
            Content.Visible = true;                 // we are restoring, not transitioning
            int useW = Math.Max(MinimumSize.Width, w);
            int useH = Math.Max(usable, h);
            Bounds = new Rectangle(x, yy, useW, useH);
            _restoreBounds = Bounds;
            _restoreHeight = useH;

            // Older layouts wrote 0/1 for "minimised"; 1 still means minimised under
            // the new enum, so both shapes load correctly.
            if (m == (int)PaneState.Minimized) State = PaneState.Minimized;
            else if (m == (int)PaneState.Maximized) State = PaneState.Maximized;
        }
    }
}

