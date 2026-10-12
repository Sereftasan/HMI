using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Kaban.UI.Dxf
{
    /// <summary>
    /// DXF workbench view.
    ///
    /// Mouse map:
    ///   wheel            zoom about the cursor          middle-drag  pan
    ///   left click       select the highlighted element (CTRL adds to the selection)
    ///   left drag        rubber-band region select      DEL key      delete selection
    ///   measure mode     RIGHT click sets the two measure points,
    ///                    LEFT click opens the copy menu for the cursor's X / Z
    ///   add-line mode    two LEFT clicks, each SNAPPING to the nearest element
    ///                    endpoint, so new lines actually join the geometry
    ///
    /// Every geometry change is snapshot-based, so Undo/Redo covers rotate, mirror,
    /// delete and add-line alike. The drawing is referenced to the machine origin
    /// (X0 left, Z0 bottom) on load and again after each rotate/mirror.
    /// </summary>
    public class DxfViewerControl : Control
    {
        private DxfDocument _doc;
        public DxfDocument Document => _doc;

        private float _scale = 1;
        private PointF _offset;

        private DxfDocument.PointD? _measureA, _measureB;
        private DxfDocument.PointD? _pendingLineStart, _hover, _snapPreview;
        private int _hoverIndex = -1;

        private readonly HashSet<int> _selected = new HashSet<int>();
        private Point? _dragStart;
        private Point _dragCurrent;
        private bool _panning;
        private Point _panAnchor;
        private bool _justDragged;      // suppresses the Click that follows a rubber band

        private readonly Stack<DxfDocument> _undo = new Stack<DxfDocument>();
        private readonly Stack<DxfDocument> _redo = new Stack<DxfDocument>();

        public bool MeasureMode { get; set; }
        public bool ShowBorderRoute { get; set; }
        public bool AddLineMode { get; set; }

        /// <summary>Next click splits the element under the cursor into two.</summary>
        public bool SplitMode { get; set; }

        /// <summary>Cutter diameter in drawing units - the green path is drawn this
        /// wide so the material actually removed is visible, not just a centre line.</summary>
        public double ToolDiameter { get; set; }

        /// <summary>Route point currently selected in the G-code list (-1 = none).</summary>
        public int HighlightedRoutePoint { get; set; } = -1;

        /// <summary>Draws the machine X / Z axes and the origin marker.</summary>
        public bool ShowAxes { get; set; } = true;

        /// <summary>Tool-centre path of the stored G-code program, drawn in green.</summary>
        public List<DxfDocument.PointD> Toolpath { get; set; }
        public bool ShowToolpath { get; set; }

        public event EventHandler<double> MeasureTaken;

        /// <summary>Raised with a route-point index when the user clicks the tool path.</summary>
        public event EventHandler<int> ToolpathPicked;
        public event EventHandler GeometryChanged;
        public event EventHandler SelectionChanged;

        /// <summary>Drawn over the drawing, after it: the profile library's references, operations and rays.</summary>
        public event PaintEventHandler PaintOverlay;

        /// <summary>The next left click picks a POINT (PointPicked) instead of selecting.</summary>
        public bool PickMode { get; set; }

        /// <summary>A point picked in PickMode, in drawing units - snapped to an element's end when one is close.</summary>
        public event EventHandler<DxfDocument.PointD> PointPicked;

        /// <summary>The names of the horizontal and vertical axes ("X", "Z"; a profile section is "Y", "Z").</summary>
        public string[] AxisNames { get; set; } = { "X", "Z" };

        /// <summary>Draws the points and texts of the drawing.</summary>
        public bool ShowMarkers { get; set; } = true;

        /// <summary>Screen point of a drawing point (for an overlay).</summary>
        public PointF ScreenOf(double x, double y) { return ToScreen(new DxfDocument.PointD(x, y)); }

        /// <summary>Drawing point under a screen point.</summary>
        public DxfDocument.PointD DrawingOf(Point p) { return ToDrawing(p); }

        /// <summary>Pixels per drawing unit.</summary>
        public float Scale { get { return _scale; } }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public int SelectionCount => _selected.Count;

        public DxfViewerControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Color.Black;
            Size = new Size(500, 500);
            TabStop = true;                     // needed for the DEL key
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // The transform must follow the CURRENT size: a viewer docked after
            // LoadFile used to keep its construction-size transform, which made
            // measure clicks land on the wrong drawing coordinates.
            if (_doc != null) ZoomFit();
        }

        // ---------------------------------------------------------------- loading
        public void LoadFile(string path)
        {
            _doc = DxfDocument.Load(path);
            _doc.NormalizeToOrigin();           // X0 left, Z0 bottom
            ResetHistory();
            ClearSelectionSilent();
            ZoomFit();
        }

        public void SetDocument(DxfDocument doc)
        {
            _doc = doc;
            if (_doc != null) _doc.NormalizeToOrigin();
            ResetHistory();
            ClearSelectionSilent();
            ZoomFit();
        }

        private void ResetHistory() { _undo.Clear(); _redo.Clear(); }

        // ---------------------------------------------------------------- history
        /// <summary>Snapshots the drawing before a mutating operation.</summary>
        private void PushUndo()
        {
            if (_doc == null) return;
            _undo.Push(_doc.Clone());
            _redo.Clear();                      // a new edit invalidates the redo branch
        }

        public void Undo()
        {
            if (_doc == null || _undo.Count == 0) return;
            _redo.Push(_doc.Clone());
            _doc = _undo.Pop();
            ClearSelection();
            GeometryChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        public void Redo()
        {
            if (_doc == null || _redo.Count == 0) return;
            _undo.Push(_doc.Clone());
            _doc = _redo.Pop();
            ClearSelection();
            GeometryChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        // ------------------------------------------------------------ transforms
        public void Rotate90() { if (_doc == null) return; PushUndo(); _doc.Rotate90(); _doc.NormalizeToOrigin(); AfterGeometryChange(); }
        public void Rotate90CW() { if (_doc == null) return; PushUndo(); _doc.Rotate90CW(); _doc.NormalizeToOrigin(); AfterGeometryChange(); }
        public void Mirror() { if (_doc == null) return; PushUndo(); _doc.MirrorX(); _doc.NormalizeToOrigin(); AfterGeometryChange(); }

        private void AfterGeometryChange()
        {
            ClearSelection();
            GeometryChanged?.Invoke(this, EventArgs.Empty);
            ZoomFit();
        }

        // ----------------------------------------------------------------- zoom
        public void ZoomFit()
        {
            _measureA = _measureB = null;
            if (_doc != null && _doc.Polylines.Count > 0 && Width > 0 && Height > 0)
            {
                double minX, minY, maxX, maxY;
                _doc.GetBounds(out minX, out minY, out maxX, out maxY);
                double w = Math.Max(1e-6, maxX - minX), h = Math.Max(1e-6, maxY - minY);
                float pad = 40;
                _scale = (float)Math.Min((Width - 2 * pad) / w, (Height - 2 * pad) / h);
                // Y grows UP in drawings, DOWN on screen -> flipped in ToScreen
                _offset = new PointF((float)(pad - minX * _scale), (float)(Height - pad + minY * _scale));
            }
            Invalidate();
        }

        public void ZoomIn() { ZoomAt(new Point(Width / 2, Height / 2), 1.25f); }
        public void ZoomOut() { ZoomAt(new Point(Width / 2, Height / 2), 1 / 1.25f); }

        /// <summary>Zooms around a screen point, keeping the drawing point under it fixed.</summary>
        private void ZoomAt(Point screenPoint, float factor)
        {
            if (_doc == null) return;
            var before = ToDrawing(screenPoint);
            _scale = Math.Max(0.001f, Math.Min(10000f, _scale * factor));
            var after = ToDrawing(screenPoint);
            _offset.X += (float)((after.X - before.X) * _scale);
            _offset.Y -= (float)((after.Y - before.Y) * _scale);
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_doc == null) return;
            ZoomAt(e.Location, e.Delta > 0 ? 1.15f : 1 / 1.15f);
        }

        private PointF ToScreen(DxfDocument.PointD p)
        {
            return new PointF((float)(p.X * _scale + _offset.X), (float)(-p.Y * _scale + _offset.Y));
        }

        private DxfDocument.PointD ToDrawing(Point s)
        {
            return new DxfDocument.PointD((s.X - _offset.X) / _scale, -(s.Y - _offset.Y) / _scale);
        }

        // ------------------------------------------------------------- selection
        public IEnumerable<int> SelectedElements { get { return _selected; } }

        private void ClearSelectionSilent() { _selected.Clear(); }

        public void ClearSelection()
        {
            if (_selected.Count == 0) return;
            _selected.Clear();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            Invalidate();
        }

        /// <summary>
        /// Grows the selection to the whole connected run - one click to grab a
        /// contour or an offcut instead of picking every segment.
        /// </summary>
        public int SelectConnected()
        {
            if (_doc == null || _selected.Count == 0) return 0;
            var seeds = new List<int>(_selected);
            foreach (var seed in seeds)
                foreach (var i in _doc.FindConnectedElements(seed))
                    _selected.Add(i);
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            Invalidate();
            return _selected.Count;
        }

        /// <summary>
        /// With exactly two elements selected, adds everything between them along
        /// the contour - "from here to there".
        /// </summary>
        public int SelectBetween()
        {
            if (_doc == null || _selected.Count != 2) return 0;
            var two = new List<int>(_selected);
            var path = _doc.FindPathBetween(two[0], two[1]);
            foreach (var i in path) _selected.Add(i);
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            Invalidate();
            return _selected.Count;
        }

        /// <summary>Takes the selected elements in or out of the cut.</summary>
        public int SetSelectedIncluded(bool included)
        {
            if (_doc == null || _selected.Count == 0) return 0;
            _doc.EnsureMeta();
            int n = 0;
            foreach (var i in _selected)
                if (i >= 0 && i < _doc.Meta.Count) { _doc.Meta[i].Included = included; n++; }
            if (GeometryChanged != null) GeometryChanged(this, EventArgs.Empty);
            Invalidate();
            return n;
        }

        /// <summary>Fixes the current selection as the border route.</summary>
        public int SetBorderFromSelection()
        {
            if (_doc == null || _selected.Count == 0) return 0;
            int n = _doc.SetBorderFromElements(_selected);
            Invalidate();
            return n;
        }

        /// <summary>Deletes every selected element in one step (undoable).</summary>
        public int DeleteSelected()
        {
            if (_doc == null || _selected.Count == 0) return 0;
            PushUndo();
            int n = _doc.RemoveElements(_selected);
            _selected.Clear();
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            if (GeometryChanged != null) GeometryChanged(this, EventArgs.Empty);
            Invalidate();
            return n;
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Delete || keyData == Keys.Escape || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                // release everything: selection, a half-drawn line and measure points
                _pendingLineStart = null;
                _measureA = _measureB = null;
                ClearSelection();
                Invalidate();
            }
            else if (e.KeyCode == Keys.Delete) DeleteSelected();
            else if (e.Control && e.KeyCode == Keys.Z) Undo();
            else if (e.Control && e.KeyCode == Keys.Y) Redo();
        }

        // ----------------------------------------------------------------- mouse
        private double HitTolerance { get { return 6 / Math.Max(0.0001f, _scale); } }   // ~6 px in drawing units

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (_doc == null) return;

            if (e.Button == MouseButtons.Middle)
            {
                _panning = true;
                _panAnchor = e.Location;
                return;
            }
            if (e.Button == MouseButtons.Left && !AddLineMode)
                _dragStart = e.Location;        // possible rubber band (works while measuring too)
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_doc == null) return;

            if (_panning)
            {
                _offset.X += e.X - _panAnchor.X;
                _offset.Y += e.Y - _panAnchor.Y;
                _panAnchor = e.Location;
                Invalidate();
                return;
            }

            _hover = ToDrawing(e.Location);
            _dragCurrent = e.Location;

            // hover highlight so the user can see what a click would pick
            _hoverIndex = _doc.FindNearestElement(_hover.Value, HitTolerance);

            // snap preview while joining elements
            _snapPreview = null;
            if (AddLineMode)
            {
                DxfDocument.PointD snapped;
                if (_doc.TrySnapToEndpoint(_hover.Value, HitTolerance * 2, out snapped)) _snapPreview = snapped;
            }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Middle) { _panning = false; return; }
            if (_doc == null || _dragStart == null) return;

            var start = _dragStart.Value;
            _dragStart = null;
            if (Math.Abs(e.X - start.X) < 4 && Math.Abs(e.Y - start.Y) < 4) return;   // a click, not a drag

            // WinForms raises Click after MouseUp even when the mouse travelled, and
            // that handler clears the selection - which is why a region select never
            // survived. Flag the drag so the click that follows is ignored.
            _justDragged = true;

            // REGION SELECT, CAD convention:
            //   drag LEFT -> RIGHT  = window   : only fully enclosed elements
            //   drag RIGHT -> LEFT  = crossing : anything the box touches
            // Crossing makes cleaning a busy drawing practical.
            var a = ToDrawing(start);
            var b = ToDrawing(e.Location);
            bool crossing = e.X < start.X;
            double x0 = Math.Min(a.X, b.X), y0 = Math.Min(a.Y, b.Y);
            double x1 = Math.Max(a.X, b.X), y1 = Math.Max(a.Y, b.Y);
            var hits = crossing ? _doc.FindElementsCrossingRect(x0, y0, x1, y1)
                                : _doc.FindElementsInRect(x0, y0, x1, y1);
            if ((ModifierKeys & Keys.Control) != Keys.Control) _selected.Clear();
            foreach (var i in hits) _selected.Add(i);
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = null;
            _hoverIndex = -1;
            _snapPreview = null;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (_justDragged) { _justDragged = false; return; }   // the drag already decided
            if (_doc == null) return;
            var p = ToDrawing(e.Location);

            if (PickMode && e.Button == MouseButtons.Left)
            {
                DxfDocument.PointD snapped;
                var picked = _doc.TrySnapToEndpoint(p, HitTolerance, out snapped) ? snapped : p;
                PickMode = false;
                Cursor = Cursors.Default;
                if (PointPicked != null) PointPicked(this, picked);
                Invalidate();
                return;
            }

            if (SplitMode && e.Button == MouseButtons.Left)
            {
                int idx = _doc.FindNearestElement(p, HitTolerance);
                if (idx >= 0)
                {
                    PushUndo();
                    int second = _doc.SplitElement(idx, p);
                    SplitMode = false;
                    ClearSelection();
                    if (second >= 0 && GeometryChanged != null) GeometryChanged(this, EventArgs.Empty);
                    Invalidate();
                }
                return;
            }

            if (AddLineMode && e.Button == MouseButtons.Left)
            {
                // snap to an existing endpoint so the new line JOINS the geometry
                DxfDocument.PointD snapped;
                var point = _doc.TrySnapToEndpoint(p, HitTolerance * 2, out snapped) ? snapped : p;
                if (_pendingLineStart == null) _pendingLineStart = point;
                else
                {
                    PushUndo();
                    _doc.AddLine(_pendingLineStart.Value, point);
                    _pendingLineStart = null;
                    if (GeometryChanged != null) GeometryChanged(this, EventArgs.Empty);
                }
                Invalidate();
                return;
            }

            if (MeasureMode)
            {
                if (e.Button == MouseButtons.Right)
                {
                    if (_measureA == null || _measureB != null) { _measureA = p; _measureB = null; }
                    else
                    {
                        _measureB = p;
                        if (MeasureTaken != null)
                            MeasureTaken(this, DxfDocument.Distance(_measureA.Value, _measureB.Value));
                    }
                    Invalidate();
                }
                else if (e.Button == MouseButtons.Left)
                {
                    var menu = Kaban.UI.Controls.UiStyle.Menu();
                    string x = p.X.ToString("F3"), z = p.Y.ToString("F3");
                    menu.Items.Add("Copy X  (" + x + ")", null, (s2, e2) => TrySetClipboard(x));
                    menu.Items.Add("Copy Z  (" + z + ")", null, (s2, e2) => TrySetClipboard(z));
                    menu.Items.Add("Copy X ; Z", null, (s2, e2) => TrySetClipboard(x + ";" + z));
                    menu.Show(this, e.Location);
                }
                return;
            }

            // clicking the GREEN path picks the matching program line - but ONLY when
            // no drawing element is under the cursor, otherwise the path (which
            // covers the whole border) would swallow every selection click.
            if (e.Button == MouseButtons.Left && _hoverIndex < 0 &&
                ShowToolpath && Toolpath != null && Toolpath.Count > 1)
            {
                int rp = NearestToolpathPoint(p);
                if (rp >= 0)
                {
                    HighlightedRoutePoint = rp;
                    if (ToolpathPicked != null) ToolpathPicked(this, rp);
                    Invalidate();
                    return;
                }
            }

            // plain SELECT
            if (e.Button == MouseButtons.Left)
            {
                if ((ModifierKeys & Keys.Control) != Keys.Control) _selected.Clear();
                if (_hoverIndex >= 0)
                {
                    if (!_selected.Add(_hoverIndex)) _selected.Remove(_hoverIndex);   // ctrl-click toggles
                }
                if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
                Invalidate();
            }
        }

        /// <summary>Index of the tool-path point nearest the cursor, or -1 if far.</summary>
        private int NearestToolpathPoint(DxfDocument.PointD p)
        {
            if (Toolpath == null) return -1;
            // generous: the green band is drawn at tool width, so anywhere on the
            // band (plus a few pixels) should pick - it was fiddly before
            double tol = Math.Max(HitTolerance * 3, ToolDiameter);
            int best = -1; double bestD = tol;
            for (int i = 0; i < Toolpath.Count; i++)
            {
                double d = DxfDocument.Distance(p, Toolpath[i]);
                if (d <= bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private static void TrySetClipboard(string text)
        {
            try { Clipboard.SetText(text); } catch { /* clipboard may be busy */ }
        }

        // ----------------------------------------------------------------- paint
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (_doc == null) return;

            if (ShowAxes) DrawAxes(g);

            // geometry, colour-coded by origin so an edited drawing reads at a glance:
            //   manually ADDED  -> bright cyan, heavier line (most prominent)
            //   from the FILE   -> white
            //   DELETED         -> very dim grey, still visible so it can be undone
            using (var pen = new Pen(Color.White, 1f))
            using (var addedPen = new Pen(Color.FromArgb(0, 230, 255), 2.2f))
            using (var deletedPen = new Pen(Color.FromArgb(70, 74, 80), 1f))
            using (var skippedPen = new Pen(Color.FromArgb(140, 110, 40), 1.6f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
            using (var otherPen = new Pen(Color.FromArgb(95, 105, 120), 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
            using (var hoverPen = new Pen(Color.FromArgb(255, 230, 120), 2f))
            using (var selPen = new Pen(Color.FromArgb(255, 140, 40), 2.5f))
            {
                _doc.EnsureMeta();
                for (int i = 0; i < _doc.Polylines.Count; i++)
                {
                    var pl = _doc.Polylines[i];
                    Pen use;
                    if (_selected.Contains(i)) use = selPen;
                    else if (i == _hoverIndex) use = hoverPen;
                    else if (_doc.IsDeleted(i)) use = deletedPen;
                    else if (!_doc.IsMaterial(i)) use = otherPen;          // a reference, a dimension, an ignored layer
                    else if (!_doc.IsActive(i)) use = skippedPen;   // in the drawing, out of the cut
                    else if (_doc.IsAdded(i)) use = addedPen;
                    else use = pen;
                    for (int j = 1; j < pl.Count; j++)
                        g.DrawLine(use, ToScreen(pl[j - 1]), ToScreen(pl[j]));
                }
            }

            if (ShowBorderRoute)
            {
                var route = _doc.GetBorderRoute();
                if (route.Count > 1)
                    using (var pen = new Pen(Color.Red, 2f))
                        for (int i = 1; i < route.Count; i++)
                            g.DrawLine(pen, ToScreen(route[i - 1]), ToScreen(route[i]));
            }

            // TOOL PATH of the stored program, in green
            if (ShowToolpath && Toolpath != null && Toolpath.Count > 1)
            {
                // WIDTH = the cutter: a translucent band as wide as the tool shows
                // what material is actually removed, with a thin centre line on top.
                float bandPx = (float)Math.Max(2.0, ToolDiameter * _scale);
                using (var band = new Pen(Color.FromArgb(90, 0, 230, 118), bandPx))
                using (var centre = new Pen(Color.FromArgb(0, 230, 118), 1.2f))
                using (var br = new SolidBrush(Color.FromArgb(0, 230, 118)))
                {
                    band.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    band.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                    band.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;
                    for (int i = 1; i < Toolpath.Count; i++)
                    {
                        var p0 = ToScreen(Toolpath[i - 1]);
                        var p1 = ToScreen(Toolpath[i]);
                        g.DrawLine(band, p0, p1);
                        g.DrawLine(centre, p0, p1);
                    }
                    var startPt = ToScreen(Toolpath[0]);
                    g.FillEllipse(br, startPt.X - 4, startPt.Y - 4, 8, 8);

                    // the point selected in the G-code list
                    if (HighlightedRoutePoint >= 0 && HighlightedRoutePoint < Toolpath.Count)
                    {
                        var hp = ToScreen(Toolpath[HighlightedRoutePoint]);
                        using (var hpen = new Pen(Color.Yellow, 2f))
                        {
                            g.DrawEllipse(hpen, hp.X - 7, hp.Y - 7, 14, 14);
                            if (HighlightedRoutePoint > 0)
                                g.DrawLine(hpen, ToScreen(Toolpath[HighlightedRoutePoint - 1]), hp);
                        }
                    }
                }
            }

            // rubber band
            if (_dragStart != null)
            {
                var r = Rectangle.FromLTRB(
                    Math.Min(_dragStart.Value.X, _dragCurrent.X), Math.Min(_dragStart.Value.Y, _dragCurrent.Y),
                    Math.Max(_dragStart.Value.X, _dragCurrent.X), Math.Max(_dragStart.Value.Y, _dragCurrent.Y));
                using (var br = new SolidBrush(Color.FromArgb(40, 120, 200, 255))) g.FillRectangle(br, r);
                using (var pen = new Pen(Color.FromArgb(120, 200, 255), 1f)) g.DrawRectangle(pen, r);
            }

            if (_snapPreview != null)
            {
                var sp = ToScreen(_snapPreview.Value);
                using (var pen = new Pen(Color.Magenta, 1.5f)) g.DrawRectangle(pen, sp.X - 5, sp.Y - 5, 10, 10);
            }
            if (_pendingLineStart != null)
            {
                var a = ToScreen(_pendingLineStart.Value);
                using (var b = new SolidBrush(Color.Cyan)) g.FillEllipse(b, a.X - 3, a.Y - 3, 6, 6);
                if (_hover != null)
                    using (var pen = new Pen(Color.FromArgb(120, 0, 255, 255), 1f))
                    {
                        pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                        g.DrawLine(pen, a, ToScreen(_snapPreview ?? _hover.Value));
                    }
            }

            if (_measureA != null)
            {
                var a = ToScreen(_measureA.Value);
                using (var b = new SolidBrush(Color.Yellow)) g.FillEllipse(b, a.X - 3, a.Y - 3, 6, 6);
                if (_measureB != null)
                {
                    var b2 = ToScreen(_measureB.Value);
                    using (var pen = new Pen(Color.Yellow, 1.5f)) g.DrawLine(pen, a, b2);
                    double d = DxfDocument.Distance(_measureA.Value, _measureB.Value);
                    using (var br = new SolidBrush(Color.Yellow))
                        g.DrawString(d.ToString("F2"), Font, br, (a.X + b2.X) / 2 + 6, (a.Y + b2.Y) / 2);
                }
            }

            if (ShowMarkers && _doc.Markers.Count > 0)
                using (var br = new SolidBrush(Color.FromArgb(150, 160, 175)))
                using (var pen = new Pen(Color.FromArgb(150, 160, 175), 1f))
                using (var small = new Font(Font.FontFamily, Math.Max(6f, Font.Size - 1f)))
                    foreach (var m in _doc.Markers)
                    {
                        var sp = ToScreen(new DxfDocument.PointD(m.X, m.Y));
                        if (m.Kind == "TEXT") g.DrawString(m.Text, small, br, sp.X, sp.Y - small.Height);
                        else { g.DrawLine(pen, sp.X - 3, sp.Y - 3, sp.X + 3, sp.Y + 3); g.DrawLine(pen, sp.X - 3, sp.Y + 3, sp.X + 3, sp.Y - 3); }
                    }

            var overlay = PaintOverlay;
            if (overlay != null) overlay(this, e);

            DrawStatusOverlay(g);
        }

        /// <summary>Machine axes: X to the right, Z upward, origin at the profile corner.</summary>
        private void DrawAxes(Graphics g)
        {
            var origin = ToScreen(new DxfDocument.PointD(0, 0));
            using (var axisPen = new Pen(Color.FromArgb(90, 100, 110), 1f))
            using (var br = new SolidBrush(Color.FromArgb(150, 165, 180)))
            {
                g.DrawLine(axisPen, 0, origin.Y, Width, origin.Y);          // X (Z = 0)
                g.DrawLine(axisPen, origin.X, 0, origin.X, Height);         // Z (X = 0)

                if (_scale > 0.05f)
                {
                    double step = 10;
                    while (step * _scale < 8) step *= 5;
                    for (double v = 0; v <= 100000; v += step)
                    {
                        var px = ToScreen(new DxfDocument.PointD(v, 0));
                        if (px.X > Width) break;
                        g.DrawLine(axisPen, px.X, origin.Y - 3, px.X, origin.Y + 3);
                        if (v > 0 && (v % (step * 5)) < 1e-6)
                            g.DrawString(v.ToString("0"), Font, br, px.X - 8, origin.Y + 5);
                    }
                    for (double v = 0; v <= 100000; v += step)
                    {
                        var pz = ToScreen(new DxfDocument.PointD(0, v));
                        if (pz.Y < 0) break;
                        g.DrawLine(axisPen, origin.X - 3, pz.Y, origin.X + 3, pz.Y);
                        if (v > 0 && (v % (step * 5)) < 1e-6)
                            g.DrawString(v.ToString("0"), Font, br, origin.X + 5, pz.Y - 7);
                    }
                }
                g.DrawString(AxisNames[0], Font, br, Width - 16, origin.Y + 5);
                g.DrawString(AxisNames[1], Font, br, origin.X + 5, 4);
            }
        }

        private void DrawStatusOverlay(Graphics g)
        {
            using (var br = new SolidBrush(Color.FromArgb(0, 220, 120)))
            {
                if (_hover != null)
                    g.DrawString(AxisNames[0] + ": " + _hover.Value.X.ToString("F2") + "   " + AxisNames[1] + ": " + _hover.Value.Y.ToString("F2"),
                                 Font, br, 6, 6);
                string info = "zoom " + (_scale < 1 ? _scale.ToString("F2") : _scale.ToString("F1")) + "x";
                if (_selected.Count > 0) info += "   |   " + _selected.Count + " selected";
                g.DrawString(info, Font, br, 6, 22);
            }
        }
    }
}

