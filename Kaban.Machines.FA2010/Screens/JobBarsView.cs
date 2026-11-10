using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Machines.FA2010.Jobs;
using Kaban.Profiles.Jobs;
using Kaban.UI.Controls;

namespace Kaban.Machines.FA2010.Screens
{
    /// <summary>
    /// THE JOB'S BARS AS PICTURES (10 November: "the job info given in 'Jobs' page is at the home page at the old program (it draws a bar image
    /// for each job bar with the cut and accessory operation points marked, can you create such a thing as well, it gets easier to see than
    /// reading it what is on the job)" - the old program's ProfileProcess.DrawFromLeftToRight): every bar of the job a strip along its length -
    /// its pieces with their ends' angles and lengths (the piece's position and order above it), the leftover, a dot numbered with the milling
    /// operation's number where each accessory operation is (its distance from the piece's start under it), a V cut green, a special cut red;
    /// what the numbers are under the bar. The selected bar is the Jobs page's; a click on a bar selects it.
    /// </summary>
    internal sealed class JobBarsView : UserControl
    {
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        private List<BarPlan> _plans = new List<BarPlan>();
        private int _selected = -1;
        private readonly List<Rectangle> _rows = new List<Rectangle>();
        /// <summary>A bar clicked: its index.</summary>
        internal event Action<int> BarClicked;
        /// <summary>The bars' strips as last laid out (tests): where each bar's row is, in the control's scrolled coordinates.</summary>
        internal IList<Rectangle> Rows { get { return _rows; } }
        /// <summary>What was drawn last, a line a mark (tests): "bar 1 piece 1 1000", "bar 1 op 3 @ 120" ...</summary>
        internal readonly List<string> Drawn = new List<string>();

        private const int Pad = 12, StripHeight = 26, TitleHeight = 20, AboveHeight = 34, BelowHeight = 30, LegendLine = 17, RowGap = 8;
        private static readonly Color ColPiece = Color.FromArgb(222, 233, 247), ColPieceLine = Color.FromArgb(60, 80, 110), ColLeftover = Color.FromArgb(206, 236, 206),
                                      ColSelected = Color.FromArgb(236, 243, 253), ColOp = Color.FromArgb(30, 34, 40), ColVCut = Color.FromArgb(20, 140, 60),
                                      ColSpecial = Color.FromArgb(200, 40, 40), ColText = Color.FromArgb(50, 58, 70), ColProblem = Color.FromArgb(180, 40, 40);

        public JobBarsView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            AutoScroll = true;
            Font = UiStyle.Small;
        }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        /// <summary>The job's bars (null: none).</summary>
        internal void SetPlans(IEnumerable<BarPlan> plans, int selected)
        {
            _plans = (plans ?? new BarPlan[0]).ToList();
            _selected = selected;
            Relayout();
            Invalidate();
            ShowSelected();
        }

        internal int Selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                Invalidate();
                ShowSelected();
            }
        }

        /// <summary>The selected bar scrolled into sight.</summary>
        private void ShowSelected()
        {
            if (_selected < 0 || _selected >= _rows.Count) return;
            var r = _rows[_selected];
            int top = -AutoScrollPosition.Y;
            if (r.Top < top) AutoScrollPosition = new Point(0, r.Top);
            else if (r.Bottom > top + ClientSize.Height) AutoScrollPosition = new Point(0, Math.Max(0, r.Bottom - ClientSize.Height));
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Invalidate();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Relayout();
        }

        private int StripWidth { get { return Math.Max(100, ClientSize.Width - 2 * Pad); } }

        /// <summary>Each bar's row: its title, the strip, the distances, the legend's lines.</summary>
        private void Relayout()
        {
            _rows.Clear();
            int y = 4;
            using (var g = CreateGraphics())
                foreach (var p in _plans)
                {
                    int legend = LegendOf(p).Count == 0 ? 0 : LegendLines(g, p);
                    int h = TitleHeight + AboveHeight + StripHeight + BelowHeight + legend * LegendLine + RowGap;
                    _rows.Add(new Rectangle(0, y, ClientSize.Width, h));
                    y += h;
                }
            AutoScrollMinSize = new Size(0, y + 4);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int y = e.Y - AutoScrollPosition.Y;
            int i = _rows.FindIndex(r => y >= r.Top && y < r.Bottom);
            if (i < 0) return;
            Selected = i;
            if (BarClicked != null) BarClicked(i);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Drawn.Clear();
            if (_plans.Count == 0)
            {
                TextRenderer.DrawText(g, T("Job_NoBars", "Open a job file: its bars are drawn here."), Font, new Point(Pad, 10), ColText);
                return;
            }
            // scrolled: each row moved up (no transform - the text is drawn by GDI, which does not take one)
            for (int i = 0; i < _plans.Count && i < _rows.Count; i++)
            {
                var r = _rows[i];
                r.Offset(0, AutoScrollPosition.Y);
                if (r.Bottom < 0 || r.Top > ClientSize.Height) continue;
                DrawBar(g, _plans[i], r, i == _selected);
            }
        }

        // ---- one bar ---------------------------------------------------------------------------------------

        private sealed class Mark
        {
            public double Position;
            public string Text = "";
            public int Kind;           // 0 an operation, 1 a V cut, 2 a special cut
            public double FromPiece = double.NaN;
        }

        private void DrawBar(Graphics g, BarPlan plan, Rectangle row, bool selected)
        {
            var bar = plan.Bar;
            if (selected) using (var b = new SolidBrush(ColSelected)) g.FillRectangle(b, row.Left, row.Top, row.Width, row.Height - RowGap / 2);
            using (var pen = new Pen(Color.FromArgb(225, 228, 233))) g.DrawLine(pen, row.Left, row.Bottom - RowGap / 2, row.Right, row.Bottom - RowGap / 2);
            string title = string.Format(T("Job_PicTitle", "Bar {0}: {1} {2} {3} - {4} mm, {5} piece(s), leftover {6} mm"), bar.Title, bar.Code, bar.Name, bar.Color,
                                         bar.Length.ToString("0.#", CI), bar.Pieces.Count, plan.Layout != null ? plan.Layout.Leftover.ToString("0.#", CI) : "-");
            using (var bold = new Font(Font, selected ? FontStyle.Bold : FontStyle.Regular))
                TextRenderer.DrawText(g, title, bold, new Point(Pad - 4, row.Top + 3), ColText);
            if (!plan.Ok)
            {
                string bad = "⚠ " + string.Format(T("Job_StateProblems", "{0} problem(s)"), plan.Problems.Count);
                var sz = TextRenderer.MeasureText(bad, Font);
                TextRenderer.DrawText(g, bad, Font, new Point(row.Right - Pad - sz.Width, row.Top + 3), ColProblem);
            }
            if (bar.Length <= 0) return;
            double scale = StripWidth / bar.Length;
            Func<double, float> X = mm => (float)(Pad + Math.Max(0, Math.Min(bar.Length, mm)) * scale);
            float y1 = row.Top + TitleHeight + AboveHeight, y0 = y1 + StripHeight;          // the strip's top and bottom
            // the bar's outline (what no piece takes is the leftover)
            using (var pen = new Pen(Color.FromArgb(170, 176, 186)))
                g.DrawRectangle(pen, X(0), y1, X(bar.Length) - X(0), StripHeight);
            var steps = plan.Layout != null ? plan.Layout.Ordered : new List<LayoutStep>();
            var starts = new double[bar.Pieces.Count];
            double lastEnd = 0;
            for (int k = 0; k < bar.Pieces.Count; k++)
            {
                var piece = bar.Pieces[k];
                var left = steps.FirstOrDefault(s => s.IsCut && s.Piece == k && s.Kind == StepKind.LeftCut);
                var right = steps.FirstOrDefault(s => s.IsCut && s.Piece == k && s.Kind == StepKind.RightCut);
                double a = left != null ? left.Position : lastEnd;
                double b = right != null ? right.Position : a + piece.CutLength;
                if (b <= a) b = a + piece.CutLength;
                starts[k] = a;
                lastEnd = b;
                DrawPiece(g, k, piece, X(a), X(b), y0, y1);
                Drawn.Add(string.Format(CI, "bar {0} piece {1} {2:0.#}", bar.Title, k + 1, piece.CutLength));
            }
            // the leftover
            if (plan.Layout != null && plan.Layout.Leftover > 0 && lastEnd < bar.Length)
            {
                var rect = new RectangleF(X(lastEnd), y1, X(bar.Length) - X(lastEnd), StripHeight);
                using (var b = new SolidBrush(ColLeftover)) g.FillRectangle(b, rect);
                using (var pen = new Pen(ColPieceLine)) g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                string text = string.Format(T("Job_PicLeftover", "leftover {0}"), plan.Layout.Leftover.ToString("0.#", CI));
                if (TextRenderer.MeasureText(text, Font).Width < rect.Width - 4)
                    TextRenderer.DrawText(g, text, Font, Rectangle.Round(rect), ColText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                Drawn.Add(string.Format(CI, "bar {0} leftover {1:0.#}", bar.Title, plan.Layout.Leftover));
            }
            // the leftover's own cuts (squared, cut into lengths the machine takes)
            using (var pen = new Pen(ColPieceLine) { DashStyle = DashStyle.Dash })
                foreach (var s in steps.Where(x => x.Kind == StepKind.LeftoverCut))
                    g.DrawLine(pen, X(s.Position), y0, X(s.Position), y1);

            // the marks: the accessories' operations, the V cuts, the special cuts
            var marks = new List<Mark>();
            foreach (var st in plan.Steps)
            {
                if (st.Op != null) marks.Add(new Mark { Position = st.Position, Text = st.Op.Index.ToString(CI), Kind = 0 });
                else if (st.Step != null && st.Step.Kind == StepKind.VCut) marks.Add(new Mark { Position = st.Position, Text = "V", Kind = 1 });
                else if (st.Special != null) marks.Add(new Mark { Position = st.Position, Text = st.Special.Number.ToString(CI), Kind = 2 });
                else continue;
                var m = marks[marks.Count - 1];
                if (st.Piece >= 0 && st.Piece < starts.Length) m.FromPiece = st.Position - starts[st.Piece];
            }
            // marks at the same place (two operations of one accessory) drawn as one: "2,3"
            var groups = new List<Mark>();
            foreach (var m in marks.OrderBy(x => x.Kind).ThenBy(x => x.Position))
            {
                var g0 = groups.LastOrDefault(x => x.Kind == m.Kind && Math.Abs(X(x.Position) - X(m.Position)) < 3);
                if (g0 != null) { if (!g0.Text.Split(',').Contains(m.Text)) g0.Text += "," + m.Text; continue; }
                groups.Add(new Mark { Position = m.Position, Text = m.Text, Kind = m.Kind, FromPiece = m.FromPiece });
            }
            foreach (var m in marks) Drawn.Add(string.Format(CI, "bar {0} {1} {2} @ {3:0.#}", bar.Title, m.Kind == 0 ? "op" : m.Kind == 1 ? "vcut" : "special", m.Text, m.Position));
            float lastRight = float.MinValue, lastRight2 = float.MinValue;
            using (var small = new Font(Font.FontFamily, Math.Max(6f, Font.Size - 1.5f), FontStyle.Bold))
                foreach (var m in groups.OrderBy(x => x.Position))
                {
                    float x = X(m.Position), yc = y1 + StripHeight / 2f;
                    float w = Math.Max(14, TextRenderer.MeasureText(m.Text, small, Size.Empty, TextFormatFlags.NoPadding).Width + 6);
                    var rc = new RectangleF(x - w / 2, yc - 7, w, 14);
                    using (var b = new SolidBrush(m.Kind == 2 ? ColSpecial : m.Kind == 1 ? ColVCut : ColOp))
                    {
                        if (m.Kind == 2) g.FillRectangle(b, rc);
                        else if (w <= 14.5f) g.FillEllipse(b, rc);
                        else using (var path = Pill(rc)) g.FillPath(b, path);
                    }
                    TextRenderer.DrawText(g, m.Text, small, Rectangle.Round(rc), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    // its distance from its piece's start under the strip (a second line when it would run into the one before)
                    if (double.IsNaN(m.FromPiece) || m.Kind == 2) continue;
                    string d = m.FromPiece.ToString("0.#", CI);
                    var size = TextRenderer.MeasureText(d, Font);
                    float tx = x - size.Width / 2f;
                    float ty = y0 + 2;
                    if (tx < lastRight + 2) { if (tx < lastRight2 + 2) continue; ty += size.Height - 2; lastRight2 = tx + size.Width; }
                    else lastRight = tx + size.Width;
                    using (var pen = new Pen(Color.FromArgb(150, 156, 166))) g.DrawLine(pen, x, y0, x, ty + 1);
                    TextRenderer.DrawText(g, d, Font, new Point((int)tx, (int)ty), ColText);
                }

            // what the marks are
            var legend = LegendOf(plan);
            float lx = Pad, ly = y0 + BelowHeight;
            foreach (var item in legend)
            {
                var size = TextRenderer.MeasureText(item.Value, Font);
                if (lx + 18 + size.Width > row.Right - Pad && lx > Pad) { lx = Pad; ly += LegendLine; }
                var rc = new RectangleF(lx, ly + 2, 12, 12);
                using (var b = new SolidBrush(item.Key == 2 ? ColSpecial : item.Key == 1 ? ColVCut : ColOp))
                {
                    if (item.Key == 2) g.FillRectangle(b, rc); else g.FillEllipse(b, rc);
                }
                TextRenderer.DrawText(g, item.Value, Font, new Point((int)lx + 15, (int)ly), ColText);
                lx += 15 + size.Width + 14;
            }
        }

        private static GraphicsPath Pill(RectangleF r)
        {
            var p = new GraphicsPath();
            float d = r.Height;
            p.AddArc(r.X, r.Y, d, d, 90, 180);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            p.CloseFigure();
            return p;
        }

        /// <summary>A piece: its ends as their angles make them, its length on it, its position and order over it.</summary>
        private void DrawPiece(Graphics g, int k, KbnPiece piece, float xa, float xb, float y0, float y1)
        {
            // an end of 45 degrees leans the profile's height in (a trapezoid: the old program's "\ /"), 90 is square, over 90 leans out
            Func<double, float> lean = angle =>
            {
                if (angle <= 0 || angle >= 180 || Math.Abs(angle - 90) < 0.01) return 0;
                double d = StripHeight / Math.Tan(angle * Math.PI / 180);
                return (float)Math.Max(-StripHeight * 1.5, Math.Min(StripHeight * 1.5, d));
            };
            float dl = lean(piece.LeftAngle), dr = lean(piece.RightAngle);
            float w = xb - xa;
            if (dl + dr > w - 2) { float f = (w - 2) / Math.Max(1, dl + dr); dl *= f; dr *= f; }
            var pts = new[] { new PointF(xa, y0), new PointF(xb, y0), new PointF(xb - dr, y1), new PointF(xa + dl, y1) };
            using (var b = new SolidBrush(ColPiece)) g.FillPolygon(b, pts);
            using (var pen = new Pen(ColPieceLine)) g.DrawPolygon(pen, pts);
            // above it: its length, and its position / order when there is room
            string len = piece.CutLength.ToString("0.#", CI);
            var size = TextRenderer.MeasureText(len, Font);
            float mid = (xa + xb) / 2f;
            if (size.Width < w + 20)
                TextRenderer.DrawText(g, len, Font, new Point((int)(mid - size.Width / 2f), (int)(y1 - size.Height)), ColText);
            string who = string.Join(" / ", new[] { piece.Position, piece.OrderNo }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (who.Length > 0)
            {
                var ws = TextRenderer.MeasureText(who, Font);
                if (ws.Width < w)
                    TextRenderer.DrawText(g, who, Font, new Point((int)(mid - ws.Width / 2f), (int)(y1 - size.Height - ws.Height + 1)), Color.FromArgb(110, 118, 130));
            }
            // its number in the piece when it fits
            string no = (k + 1).ToString(CI);
            var ns = TextRenderer.MeasureText(no, Font);
            if (ns.Width < w - Math.Abs(dl) - Math.Abs(dr) - 4)
                TextRenderer.DrawText(g, no, Font, new Rectangle((int)xa, (int)y1, (int)w, StripHeight), Color.FromArgb(120, 130, 145),
                                      TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        /// <summary>What the bar's marks are: its operations (by number), a V cut, its special cuts.</summary>
        private static List<KeyValuePair<int, string>> LegendOf(BarPlan plan)
        {
            var list = new List<KeyValuePair<int, string>>();
            foreach (var op in plan.Milling.OrderBy(o => o.Index))
                list.Add(new KeyValuePair<int, string>(0, op.Index.ToString(CI) + " " + op.Accessory + " " + op.Macro + (op.Mirrored ? " (" + T("Job_PicMirrored", "mirrored") + ")" : "") +
                                                          (op.Confirmed ? "" : " - " + T("Job_PicUnconfirmed", "not confirmed"))));
            if (plan.Steps.Any(s => s.Step != null && s.Step.Kind == StepKind.VCut))
                list.Add(new KeyValuePair<int, string>(1, "V " + T("Job_PicVCut", "V cut")));
            foreach (var sp in plan.Special)
                list.Add(new KeyValuePair<int, string>(2, sp.Number.ToString(CI) + " " + T("Job_PicSpecial", "special cut") + " " + (sp.Recipe != null ? sp.Recipe.Title : "")));
            return list;
        }

        private int LegendLines(Graphics g, BarPlan plan)
        {
            int lines = 1;
            float lx = Pad;
            foreach (var item in LegendOf(plan))
            {
                var size = TextRenderer.MeasureText(item.Value, Font);
                if (lx + 18 + size.Width > ClientSize.Width - Pad && lx > Pad) { lx = Pad; lines++; }
                lx += 15 + size.Width + 14;
            }
            return lines;
        }
    }
}
