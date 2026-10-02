using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.UI.Controls;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// The dashboard's charts, drawn here - no chart package: a pie of the time in
    /// each machine state, the states on a time line, and a bar per event kind on a
    /// logarithmic scale. Each shows the value under the pointer in a tip.
    /// </summary>
    public abstract class ChartBase : Control
    {
        protected static readonly Font Plain = UiScale.NewFont("Segoe UI", 9F);
        protected static readonly Font Strong = UiScale.NewFont("Segoe UI", 9F, FontStyle.Bold);
        protected static readonly Color Ink = Color.FromArgb(49, 51, 63);
        protected static readonly Color Grid = Color.FromArgb(226, 229, 234);
        protected static readonly Color Muted = Color.FromArgb(110, 114, 125);

        private readonly ToolTip _tip = new ToolTip { InitialDelay = 0, ReshowDelay = 0, AutoPopDelay = 30000, ShowAlways = true };
        private string _tipText = "";

        /// <summary>Shown when there is nothing to draw.</summary>
        public string EmptyText { get; set; }

        protected ChartBase()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            EmptyText = "";
        }

        protected abstract bool HasData { get; }
        protected abstract void Draw(Graphics g, Rectangle r);

        /// <summary>The tip for a point ("" for none).</summary>
        protected virtual string TipAt(Point p) { return ""; }

        protected static int Px(int v) { return UiScale.Px(v); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            var r = ClientRectangle;
            if (!HasData)
            {
                TextRenderer.DrawText(g, EmptyText ?? "", Plain, r, Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
                return;
            }
            try { Draw(g, r); }
            catch (Exception ex) { TextRenderer.DrawText(g, ex.Message, Plain, r, Color.Firebrick, TextFormatFlags.WordBreak); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            string t = HasData ? TipAt(e.Location) ?? "" : "";
            if (t == _tipText) return;
            _tipText = t;
            if (t.Length == 0) _tip.Hide(this);
            else _tip.Show(t, this, e.X + Px(14), e.Y + Px(16));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _tipText = "";
            _tip.Hide(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _tip.Dispose();
            base.Dispose(disposing);
        }

        /// <summary>The text the tip shows at a point (tests).</summary>
        public string TipFor(Point p) { return HasData ? TipAt(p) ?? "" : ""; }

        protected static string Fmt(double v, string f) { return v.ToString(f, CultureInfo.CurrentCulture); }
    }

    // ======================================================================
    // THE PIE
    // ======================================================================

    /// <summary>The time in each state: a slice each, its share written on it, a legend beside it.</summary>
    public sealed class PieChart : ChartBase
    {
        public sealed class Slice
        {
            public string Label = "";
            public double Value;
            public Color Color;
        }

        private List<Slice> _slices = new List<Slice>();
        private RectangleF _pie;
        private readonly List<KeyValuePair<Rectangle, Slice>> _legend = new List<KeyValuePair<Rectangle, Slice>>();

        /// <summary>The unit written after a value in the tip (" min").</summary>
        public string Unit = " min";

        public List<Slice> Slices
        {
            get { return _slices; }
            set { _slices = (value ?? new List<Slice>()).Where(s => s.Value > 0).ToList(); Invalidate(); }
        }

        private double Total { get { return _slices.Sum(s => s.Value); } }
        protected override bool HasData { get { return _slices.Count > 0 && Total > 0; } }

        protected override void Draw(Graphics g, Rectangle r)
        {
            int pad = Px(8);
            // the legend's width: the longest label and its figures
            int legendW = 0, rowH = Px(22);
            foreach (var s in _slices)
                legendW = Math.Max(legendW, TextRenderer.MeasureText(LegendText(s), Plain).Width);
            legendW += Px(26);
            legendW = Math.Min(legendW, Math.Max(Px(120), r.Width / 2));
            int d = Math.Min(r.Height - 2 * pad, r.Width - legendW - 3 * pad);
            if (d < Px(40)) d = Math.Max(Px(20), Math.Min(r.Height - 2 * pad, r.Width - 2 * pad));
            int totalW = d + pad * 2 + legendW;
            int x0 = r.Left + Math.Max(pad, (r.Width - totalW) / 2);
            _pie = new RectangleF(x0, r.Top + (r.Height - d) / 2f, d, d);

            double total = Total;
            float start = -90f;
            foreach (var s in _slices)
            {
                float sweep = (float)(s.Value / total * 360.0);
                using (var b = new SolidBrush(s.Color))
                {
                    if (sweep >= 359.99f) g.FillEllipse(b, _pie);
                    else if (sweep > 0) g.FillPie(b, _pie.X, _pie.Y, _pie.Width, _pie.Height, start, sweep);
                }
                using (var p = new Pen(Color.White, Px(2)))
                    if (sweep > 0 && sweep < 359.99f) g.DrawPie(p, _pie.X, _pie.Y, _pie.Width, _pie.Height, start, sweep);
                // the share, on the slice when it is wide enough
                double pct = s.Value / total * 100.0;
                if (pct >= 4 && d > Px(90))
                {
                    double mid = (start + sweep / 2f) * Math.PI / 180.0;
                    float rr = d * 0.34f;
                    var c = new PointF(_pie.X + d / 2f + (float)(Math.Cos(mid) * rr), _pie.Y + d / 2f + (float)(Math.Sin(mid) * rr));
                    string t = Fmt(pct, "0.0") + "%";
                    var sz = TextRenderer.MeasureText(t, Strong);
                    var ink = s.Color.GetBrightness() > 0.6f || (s.Color.R > 200 && s.Color.G > 200) ? Ink : Color.White;
                    TextRenderer.DrawText(g, t, Strong, new Point((int)(c.X - sz.Width / 2f), (int)(c.Y - sz.Height / 2f)), ink);
                }
                start += sweep;
            }

            // the legend
            _legend.Clear();
            int lx = (int)_pie.Right + pad * 2;
            int ly = r.Top + Math.Max(pad, (r.Height - rowH * _slices.Count) / 2);
            foreach (var s in _slices)
            {
                var sw = new Rectangle(lx, ly + (rowH - Px(12)) / 2, Px(12), Px(12));
                using (var b = new SolidBrush(s.Color)) g.FillRectangle(b, sw);
                var tr = new Rectangle(lx + Px(18), ly, Math.Max(10, r.Right - lx - Px(18) - pad), rowH);
                TextRenderer.DrawText(g, LegendText(s), Plain, tr, Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                _legend.Add(new KeyValuePair<Rectangle, Slice>(new Rectangle(lx, ly, tr.Right - lx, rowH), s));
                ly += rowH;
            }
        }

        private string LegendText(Slice s)
        {
            return s.Label + "  " + Fmt(s.Value / Total * 100.0, "0.0") + "%";
        }

        protected override string TipAt(Point p)
        {
            foreach (var kv in _legend)
                if (kv.Key.Contains(p)) return Tip(kv.Value);
            float cx = _pie.X + _pie.Width / 2f, cy = _pie.Y + _pie.Height / 2f;
            double dx = p.X - cx, dy = p.Y - cy;
            if (_pie.Width <= 0 || dx * dx + dy * dy > _pie.Width * _pie.Width / 4.0) return "";
            double ang = Math.Atan2(dy, dx) * 180.0 / Math.PI + 90.0;   // 0 at the top, clockwise
            if (ang < 0) ang += 360.0;
            double total = Total, at = 0;
            foreach (var s in _slices)
            {
                double sweep = s.Value / total * 360.0;
                if (ang >= at && ang < at + sweep) return Tip(s);
                at += sweep;
            }
            return "";
        }

        private string Tip(Slice s)
        {
            return s.Label + "\n" + Fmt(s.Value, "0.000") + Unit + "\n" + Fmt(s.Value / Total * 100.0, "0.000") + "%";
        }
    }

    // ======================================================================
    // THE TIME LINE
    // ======================================================================

    /// <summary>
    /// The states on a time line, a row per state. The wheel zooms around the
    /// pointer, dragging moves along, a double click shows the whole range again.
    /// </summary>
    public sealed class TimelineChart : ChartBase
    {
        public sealed class Bar
        {
            public string Row = "";
            public DateTime Start, End;
            public Color Color;
            public string Tip = "";
        }

        private List<Bar> _bars = new List<Bar>();
        private List<string> _rows = new List<string>();
        private DateTime _from, _to;          // the whole range
        private DateTime _viewFrom, _viewTo;  // what is shown
        private Rectangle _plot;
        private Point? _drag;
        private DateTime _dragFrom, _dragTo;
        private int? _pickFrom, _pickTo;    // a right-drag marking an interval to zoom to

        public TimelineChart()
        {
            // Ctrl + the wheel over the chart zooms it, wherever the focus is (WinForms
            // gives the wheel to the focused control - the page around it scrolled instead).
            lock (_charts)
            {
                _charts.RemoveAll(w => !w.IsAlive);
                _charts.Add(new WeakReference(this));
                if (!_filterAdded)
                {
                    _filterAdded = true;
                    try { Application.AddMessageFilter(new CtrlWheel()); } catch { }
                }
            }
        }

        private static readonly List<WeakReference> _charts = new List<WeakReference>();
        private static bool _filterAdded;

        /// <summary>WM_MOUSEWHEEL with Ctrl over a time line on show: that time line zooms; nothing else sees it.</summary>
        private sealed class CtrlWheel : IMessageFilter
        {
            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != 0x020A || (Control.ModifierKeys & Keys.Control) == 0) return false;
                var at = Control.MousePosition;
                TimelineChart hit = null;
                lock (_charts)
                    foreach (var w in _charts)
                    {
                        var c = w.Target as TimelineChart;
                        if (c == null || c.IsDisposed || !c.IsHandleCreated || !c.Visible) continue;
                        if (c.RectangleToScreen(c.ClientRectangle).Contains(at)) { hit = c; break; }
                    }
                if (hit == null) return false;
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                hit.WheelAt(hit.PointToClient(at), delta, true);
                return true;
            }
        }

        public List<Bar> Bars
        {
            get { return _bars; }
            set
            {
                _bars = (value ?? new List<Bar>()).Where(b => b.End > b.Start).ToList();
                _rows = new List<string>();
                foreach (var b in _bars) if (!_rows.Contains(b.Row)) _rows.Add(b.Row);
                if (_bars.Count > 0)
                {
                    _from = _bars.Min(b => b.Start);
                    _to = _bars.Max(b => b.End);
                }
                ResetView();
            }
        }

        /// <summary>Shows the whole range.</summary>
        public void ResetView()
        {
            _viewFrom = _from;
            _viewTo = _to;
            Invalidate();
        }

        /// <summary>How to zoom, written small above the time line.</summary>
        public string Hint = "";

        public DateTime ViewFrom { get { return _viewFrom; } }
        public DateTime ViewTo { get { return _viewTo; } }

        protected override bool HasData { get { return _bars.Count > 0 && _to > _from; } }

        private float X(DateTime t)
        {
            double span = (_viewTo - _viewFrom).TotalSeconds;
            return _plot.Left + (float)((t - _viewFrom).TotalSeconds / span * _plot.Width);
        }

        private DateTime TimeAt(int x)
        {
            double span = (_viewTo - _viewFrom).TotalSeconds;
            return _viewFrom.AddSeconds((x - _plot.Left) / (double)Math.Max(1, _plot.Width) * span);
        }

        protected override void Draw(Graphics g, Rectangle r)
        {
            int labelW = 0;
            foreach (var row in _rows) labelW = Math.Max(labelW, TextRenderer.MeasureText(row, Plain).Width);
            labelW = Math.Min(labelW + Px(12), r.Width / 3);
            int axisH = Px(40), hintH = string.IsNullOrEmpty(Hint) ? Px(6) : Px(20);
            _plot = new Rectangle(r.Left + labelW, r.Top + hintH, Math.Max(10, r.Width - labelW - Px(16)), Math.Max(10, r.Height - axisH - hintH));
            if (!string.IsNullOrEmpty(Hint))
                TextRenderer.DrawText(g, Hint, Plain, new Rectangle(_plot.Left, r.Top + Px(1), _plot.Width, hintH - Px(2)), Muted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            float rowH = _plot.Height / (float)Math.Max(1, _rows.Count);

            // the rows
            for (int i = 0; i < _rows.Count; i++)
            {
                float y = _plot.Top + i * rowH;
                if (i % 2 == 1) using (var b = new SolidBrush(Color.FromArgb(248, 249, 251))) g.FillRectangle(b, _plot.Left, y, _plot.Width, rowH);
                TextRenderer.DrawText(g, _rows[i], Plain, new Rectangle(r.Left, (int)y, labelW - Px(6), (int)rowH), Ink,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            // the time grid and its labels
            var ticks = Ticks(_viewFrom, _viewTo, Math.Max(2, _plot.Width / Px(110)));
            DateTime? lastDay = null;
            using (var pen = new Pen(Grid))
                foreach (var t in ticks)
                {
                    float x = X(t);
                    if (x < _plot.Left - 1 || x > _plot.Right + 1) continue;
                    g.DrawLine(pen, x, _plot.Top, x, _plot.Bottom);
                    string top = Kaban.Core.Helpers.ViewPreferences.TimeText(t, false);
                    string bottom = lastDay == null || lastDay.Value != t.Date ? Kaban.Core.Helpers.ViewPreferences.DateText(t) : "";
                    lastDay = t.Date;
                    var s1 = TextRenderer.MeasureText(top, Plain);
                    TextRenderer.DrawText(g, top, Plain, new Point(Inside(x - s1.Width / 2f, s1.Width, r), _plot.Bottom + Px(3)), Muted);
                    if (bottom.Length > 0)
                    {
                        var s2 = TextRenderer.MeasureText(bottom, Plain);
                        TextRenderer.DrawText(g, bottom, Plain, new Point(Inside(x - s2.Width / 2f, s2.Width, r), _plot.Bottom + Px(3) + s1.Height), Muted);
                    }
                }

            // the bars
            var clip = g.Clip;
            g.SetClip(_plot);
            g.SmoothingMode = SmoothingMode.None;
            foreach (var b in _bars)
            {
                if (b.End <= _viewFrom || b.Start >= _viewTo) continue;
                int i = _rows.IndexOf(b.Row);
                float y = _plot.Top + i * rowH + rowH * 0.18f;
                float x1 = X(b.Start), x2 = X(b.End);
                using (var br = new SolidBrush(b.Color)) g.FillRectangle(br, x1, y, Math.Max(1f, x2 - x1), rowH * 0.64f);
            }
            // the interval being marked (right-drag)
            if (_pickFrom.HasValue && _pickTo.HasValue && _pickFrom != _pickTo)
            {
                int a = Math.Min(_pickFrom.Value, _pickTo.Value), w = Math.Abs(_pickTo.Value - _pickFrom.Value);
                using (var br = new SolidBrush(Color.FromArgb(60, 31, 95, 168))) g.FillRectangle(br, a, _plot.Top, w, _plot.Height);
                using (var pen = new Pen(Color.FromArgb(31, 95, 168))) g.DrawRectangle(pen, a, _plot.Top, w, _plot.Height - 1);
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clip = clip;
            using (var pen = new Pen(Color.FromArgb(200, 204, 211))) g.DrawRectangle(pen, _plot);
        }

        /// <summary>A label's left edge, moved in so the label stays whole.</summary>
        private static int Inside(float left, int width, Rectangle r)
        {
            return (int)Math.Max(r.Left, Math.Min(r.Right - width - 2, left));
        }

        /// <summary>Round times across the range, about <paramref name="want"/> of them.</summary>
        internal static List<DateTime> Ticks(DateTime from, DateTime to, int want)
        {
            var steps = new[] { 1, 2, 5, 10, 15, 30, 60, 120, 180, 360, 720, 1440, 2880, 10080, 20160, 43200, 86400, 129600, 525600 };
            double minutes = (to - from).TotalMinutes / Math.Max(1, want);
            int step = steps.FirstOrDefault(s => s >= minutes);
            if (step == 0) step = steps[steps.Length - 1];
            var list = new List<DateTime>();
            DateTime t;
            if (step >= 43200) t = new DateTime(from.Year, from.Month, 1);
            else if (step >= 1440) t = from.Date;
            else t = from.Date.AddMinutes(Math.Floor((from - from.Date).TotalMinutes / step) * step);
            for (int guard = 0; t <= to && guard < 400; guard++)
            {
                if (t >= from) list.Add(t);
                t = step >= 43200 ? t.AddMonths(Math.Max(1, step / 43200)) : t.AddMinutes(step);
            }
            return list;
        }

        protected override string TipAt(Point p)
        {
            if (!_plot.Contains(p) || _rows.Count == 0) return "";
            float rowH = _plot.Height / (float)_rows.Count;
            int i = (int)((p.Y - _plot.Top) / rowH);
            if (i < 0 || i >= _rows.Count) return "";
            var t = TimeAt(p.X);
            var b = _bars.FirstOrDefault(x => x.Row == _rows[i] && x.Start <= t && x.End > t);
            return b == null ? "" : b.Tip;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            WheelAt(e.Location, e.Delta, (ModifierKeys & Keys.Control) != 0);
        }

        /// <summary>
        /// The wheel over the chart. With Ctrl: zooms around the pointer - turned
        /// back (towards you) in, forward out. Without: the page scrolls on, as
        /// anywhere else.
        /// </summary>
        public void WheelAt(Point p, int delta, bool ctrl)
        {
            if (ctrl)
            {
                if (!HasData || delta == 0) return;
                if (!_plot.Contains(p)) p = new Point(Math.Max(_plot.Left, Math.Min(_plot.Right, p.X)), _plot.Top + 1);
                Zoom(TimeAt(p.X), Math.Pow(1.25, delta / 120.0));
                return;
            }
            for (var c = Parent; c != null; c = c.Parent)
            {
                var sc = c as ScrollableControl;
                if (sc == null || !sc.AutoScroll || !sc.VerticalScroll.Visible) continue;
                int y = -sc.AutoScrollPosition.Y - delta;
                sc.AutoScrollPosition = new Point(-sc.AutoScrollPosition.X, Math.Max(0, y));
                return;
            }
        }

        /// <summary>Shows an interval (at least a minute, inside the range).</summary>
        public void ZoomTo(DateTime from, DateTime to)
        {
            if (!HasData) return;
            if (to < from) { var t = from; from = to; to = t; }
            if (from < _from) from = _from;
            if (to > _to) to = _to;
            if ((to - from).TotalSeconds < 60)
            {
                var mid = from.AddSeconds((to - from).TotalSeconds / 2);
                from = mid.AddSeconds(-30);
                to = mid.AddSeconds(30);
                if (from < _from) { to = to.Add(_from - from); from = _from; }
                if (to > _to) { from = from.Add(-(to - _to)); to = _to; }
            }
            _viewFrom = from;
            _viewTo = to;
            Invalidate();
        }

        /// <summary>Zooms around a time (a factor below 1 zooms in); never beyond the range, never below a minute.</summary>
        public void Zoom(DateTime around, double factor)
        {
            double span = (_viewTo - _viewFrom).TotalSeconds * factor;
            double whole = (_to - _from).TotalSeconds;
            span = Math.Max(60, Math.Min(whole, span));
            double left = (around - _viewFrom).TotalSeconds / Math.Max(1, (_viewTo - _viewFrom).TotalSeconds);
            var from = around.AddSeconds(-span * left);
            if (from < _from) from = _from;
            var to = from.AddSeconds(span);
            if (to > _to) { to = _to; from = to.AddSeconds(-span); }
            _viewFrom = from;
            _viewTo = to;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _plot.Contains(e.Location))
            {
                _drag = e.Location;
                _dragFrom = _viewFrom;
                _dragTo = _viewTo;
                Focus();
            }
            else if (e.Button == MouseButtons.Right && _plot.Contains(e.Location) && HasData)
            {
                _pickFrom = _pickTo = e.X;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_pickFrom.HasValue && e.Button == MouseButtons.Right)
            {
                _pickTo = Math.Max(_plot.Left, Math.Min(_plot.Right, e.X));
                Invalidate();
                return;
            }
            if (_drag.HasValue && e.Button == MouseButtons.Left)
            {
                double secPerPx = (_dragTo - _dragFrom).TotalSeconds / Math.Max(1, _plot.Width);
                double shift = -(e.X - _drag.Value.X) * secPerPx;
                var from = _dragFrom.AddSeconds(shift);
                var to = _dragTo.AddSeconds(shift);
                if (from < _from) { to = to.Add(_from - from); from = _from; }
                if (to > _to) { from = from.Add(-(to - _to)); to = _to; }
                _viewFrom = from;
                _viewTo = to;
                Invalidate();
                return;
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _drag = null;
            if (_pickFrom.HasValue && e.Button == MouseButtons.Right)
            {
                int a = _pickFrom.Value, b = _pickTo ?? a;
                _pickFrom = _pickTo = null;
                if (Math.Abs(b - a) >= Px(6)) ZoomTo(TimeAt(Math.Min(a, b)), TimeAt(Math.Max(a, b)));
                else Invalidate();
            }
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            ResetView();
        }
    }

    // ======================================================================
    // THE BARS
    // ======================================================================

    /// <summary>A bar per kind, on a logarithmic scale (a count of 1 and one of 10 000 both show), its count on top.</summary>
    public sealed class BarChart : ChartBase
    {
        public sealed class Item
        {
            public string Label = "";
            public double Value;
        }

        private List<Item> _items = new List<Item>();
        private readonly List<KeyValuePair<RectangleF, Item>> _hits = new List<KeyValuePair<RectangleF, Item>>();

        public Color BarColor = Color.FromArgb(99, 110, 250);

        public List<Item> Items
        {
            get { return _items; }
            set { _items = (value ?? new List<Item>()).Where(i => i.Value > 0).ToList(); Invalidate(); }
        }

        protected override bool HasData { get { return _items.Count > 0; } }

        protected override void Draw(Graphics g, Rectangle r)
        {
            _hits.Clear();
            double max = _items.Max(i => i.Value);
            int decades = Math.Max(1, (int)Math.Ceiling(Math.Log10(Math.Max(1.0, max)) + 0.05));
            // labels under the bars: level when they fit, slanted when they do not
            int axisW = TextRenderer.MeasureText(Pow10Text(decades), Plain).Width + Px(10);
            int plotW = Math.Max(10, r.Width - axisW - Px(12));
            float slot = plotW / (float)_items.Count;
            int longest = _items.Max(i => TextRenderer.MeasureText(i.Label, Plain).Width);
            bool slant = longest > slot - Px(6);
            int labelH = slant ? Math.Min(r.Height / 2, (int)(longest * 0.72) + Px(14)) : Px(24);
            var plot = new Rectangle(r.Left + axisW, r.Top + Px(22), plotW, Math.Max(10, r.Height - labelH - Px(28)));

            Func<double, float> y = v => plot.Bottom - (float)(Math.Log10(Math.Max(1.0, v)) / decades * plot.Height);

            // decades
            using (var pen = new Pen(Grid))
                for (int d = 0; d <= decades; d++)
                {
                    float yy = y(Math.Pow(10, d));
                    g.DrawLine(pen, plot.Left, yy, plot.Right, yy);
                    string t = Pow10Text(d);
                    var sz = TextRenderer.MeasureText(t, Plain);
                    TextRenderer.DrawText(g, t, Plain, new Point(plot.Left - sz.Width - Px(4), (int)(yy - sz.Height / 2f)), Muted);
                }

            // the bars: narrow when there are few (as the old chart's)
            float barW = Math.Min(slot * 0.7f, _items.Count < 5 ? Math.Max(Px(18), slot * 0.15f) : slot * 0.7f);
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                float cx = plot.Left + slot * (i + 0.5f);
                float top = y(it.Value);
                // a count of 1 is a sliver on a log scale: at least a few pixels
                if (plot.Bottom - top < Px(3)) top = plot.Bottom - Px(3);
                var rect = new RectangleF(cx - barW / 2f, top, barW, plot.Bottom - top);
                using (var b = new SolidBrush(BarColor)) g.FillRectangle(b, rect);
                _hits.Add(new KeyValuePair<RectangleF, Item>(new RectangleF(cx - slot / 2f, plot.Top, slot, plot.Height + labelH), it));
                string n = it.Value.ToString("0", CultureInfo.CurrentCulture);
                var ns = TextRenderer.MeasureText(n, Strong);
                TextRenderer.DrawText(g, n, Strong, new Point((int)(cx - ns.Width / 2f), (int)top - ns.Height), Ink);

                if (!slant)
                    TextRenderer.DrawText(g, it.Label, Plain, new Rectangle((int)(cx - slot / 2f), plot.Bottom + Px(4), (int)slot, labelH),
                        Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                else
                {
                    var state = g.Save();
                    g.TranslateTransform(cx, plot.Bottom + Px(6));
                    g.RotateTransform(-40);
                    var sz = g.MeasureString(it.Label, Plain);
                    using (var b = new SolidBrush(Ink)) g.DrawString(it.Label, Plain, b, -sz.Width, -sz.Height / 2f);
                    g.Restore(state);
                }
            }
            using (var pen = new Pen(Color.FromArgb(200, 204, 211))) g.DrawLine(pen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
        }

        private static string Pow10Text(int d)
        {
            return Math.Pow(10, d).ToString("#,0", CultureInfo.CurrentCulture);
        }

        protected override string TipAt(Point p)
        {
            foreach (var h in _hits)
                if (h.Key.Contains(p)) return h.Value.Label + "\n" + h.Value.Value.ToString("#,0", CultureInfo.CurrentCulture);
            return "";
        }
    }
}
