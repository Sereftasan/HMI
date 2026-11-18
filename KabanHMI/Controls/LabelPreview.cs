using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Labels;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// A LABEL DRAWN AS THE PRINTER WILL PRINT IT (the old KabanLabelDesigner's DrawArea, to look at): its size, every field where
    /// it is with its font's size (the printer's fonts 1 - 5: 12, 16, 20, 24, 48 dots high), a barcode's bars (Code 128, as the
    /// printer draws it) and its text under it, a QR code's place, a picture. A field picked on the table is framed; a click on a
    /// field picks it (FieldClicked).
    /// </summary>
    public sealed class LabelPreview : Control
    {
        private LabelTemplate _t;
        private LabelValues _v;
        private int _selected = -1;
        private readonly List<Rectangle> _boxes = new List<Rectangle>();

        /// <summary>A field clicked (its index).</summary>
        public event Action<int> FieldClicked;

        public LabelPreview()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(236, 238, 241);
            Name = "labelPreview";
        }

        public void Show(LabelTemplate t, LabelValues v, int selected = -1)
        {
            _t = t; _v = v; _selected = selected;
            Invalidate();
        }

        /// <summary>The fields' boxes on the screen (tests).</summary>
        public IList<Rectangle> Boxes { get { return _boxes.ToList(); } }

        private static readonly int[] FontDots = { 0, 12, 16, 20, 24, 48 };
        private static readonly int[] FontWidthDots = { 0, 8, 10, 12, 14, 32 };

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            _boxes.Clear();
            var g = e.Graphics;
            if (_t == null) return;
            // the label as large as the room gives, its sides kept
            int wDots = Math.Max(8, _t.WidthMm * LabelWriter.DotsPerMm), hDots = Math.Max(8, _t.HeightMm * LabelWriter.DotsPerMm);
            float k = Math.Min((Width - 24f) / wDots, (Height - 44f) / hDots);
            if (k <= 0) return;
            var paper = new RectangleF((Width - wDots * k) / 2, (Height - 20 - hDots * k) / 2, wDots * k, hDots * k);
            using (var shadow = new SolidBrush(Color.FromArgb(200, 203, 208))) g.FillRectangle(shadow, paper.X + 3, paper.Y + 3, paper.Width, paper.Height);
            g.FillRectangle(Brushes.White, paper);
            g.DrawRectangle(Pens.Gray, paper.X, paper.Y, paper.Width, paper.Height);
            var now = DateTime.Now;
            var clip = g.Clip;
            g.SetClip(paper);
            for (int i = 0; i < _t.Fields.Count; i++)
            {
                var f = _t.Fields[i];
                float x = paper.X + f.X * LabelWriter.DotsPerMm * k, y = paper.Y + f.Y * LabelWriter.DotsPerMm * k;
                int font = Math.Max(1, Math.Min(5, f.FontSize));
                RectangleF box;
                string data = LabelWriter.DataOf(f, _v, now);
                switch (f.Type)
                {
                    case LabelFieldType.Barcode:
                        box = Barcode(g, data.Length > 0 ? data : "<" + f.Key + ">", x, y, (40 + font * 10) * k, 2 * k);
                        break;
                    case LabelFieldType.QRCode:
                        {
                            float s = 25 * 2 * k * 2;
                            box = new RectangleF(x, y, s, s);
                            Qr(g, box);
                        }
                        break;
                    case LabelFieldType.Picture:
                        box = new RectangleF(x, y, f.Width * LabelWriter.DotsPerMm * k, f.Height * LabelWriter.DotsPerMm * k);
                        Picture(g, box, f);
                        break;
                    default:
                        {
                            float px = Math.Max(4f, FontDots[font] * k);
                            using (var fnt = new Font(font == 5 ? "Arial Black" : "Arial", px, FontStyle.Regular, GraphicsUnit.Pixel))
                            {
                                var text = data.Length > 0 || f.IsDataField ? (data.Length > 0 ? data : "<" + f.Key + ">") : "";
                                var size = g.MeasureString(text, fnt);
                                // as wide as the printer's font (its characters' width)
                                float w = Math.Max(size.Width, text.Length * FontWidthDots[font] * k * 0.9f);
                                box = new RectangleF(x, y, Math.Max(w, 6), Math.Max(size.Height, px));
                                using (var b = new SolidBrush(data.Length > 0 ? Color.Black : Color.Gray)) g.DrawString(text, fnt, b, x, y);
                            }
                        }
                        break;
                }
                var r = Rectangle.Round(box);
                _boxes.Add(r);
                if (i == _selected)
                    using (var pen = new Pen(Color.FromArgb(30, 120, 220), 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
                        g.DrawRectangle(pen, r.X - 2, r.Y - 2, r.Width + 4, r.Height + 4);
            }
            g.Clip = clip;
            using (var f = new Font("Segoe UI", 8.5f))
                g.DrawString(_t.WidthMm + " x " + _t.HeightMm + " mm", f, Brushes.DimGray, paper.X, paper.Bottom + 2);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            for (int i = _boxes.Count - 1; i >= 0; i--)
            {
                var r = _boxes[i];
                r.Inflate(3, 3);
                if (!r.Contains(e.Location)) continue;
                var h = FieldClicked;
                if (h != null) h(i);
                return;
            }
        }

        /// <summary>A Code 128 (set B) barcode, its text under it; its box.</summary>
        private static RectangleF Barcode(Graphics g, string data, float x, float y, float height, float module)
        {
            var widths = Code128.Widths(data);
            float cx = x;
            bool bar = true;
            foreach (var w in widths)
            {
                float ww = w * module;
                if (bar) g.FillRectangle(Brushes.Black, cx, y, Math.Max(0.6f, ww), height);
                cx += ww;
                bar = !bar;
            }
            using (var f = new Font("Arial", Math.Max(5f, height / 5), GraphicsUnit.Pixel))
            {
                var s = g.MeasureString(data, f);
                g.DrawString(data, f, Brushes.Black, x + Math.Max(0, (cx - x - s.Width) / 2), y + height + 1);
                return new RectangleF(x, y, Math.Max(cx - x, 6), height + s.Height + 1);
            }
        }

        /// <summary>A QR code's place: its three corner squares and a pattern (the printer makes the real one).</summary>
        private static void Qr(Graphics g, RectangleF r)
        {
            g.FillRectangle(Brushes.White, r);
            float c = r.Width / 7 * 2;
            foreach (var p in new[] { new PointF(r.X, r.Y), new PointF(r.Right - c, r.Y), new PointF(r.X, r.Bottom - c) })
            {
                g.FillRectangle(Brushes.Black, p.X, p.Y, c, c);
                g.FillRectangle(Brushes.White, p.X + c / 7, p.Y + c / 7, c * 5 / 7, c * 5 / 7);
                g.FillRectangle(Brushes.Black, p.X + c * 2 / 7, p.Y + c * 2 / 7, c * 3 / 7, c * 3 / 7);
            }
            var rnd = new Random(7);
            float m = r.Width / 21;
            for (int i = 0; i < 21; i++)
                for (int j = 0; j < 21; j++)
                {
                    if ((i < 8 && j < 8) || (i > 12 && j < 8) || (i < 8 && j > 12)) continue;
                    if (rnd.Next(2) == 0) g.FillRectangle(Brushes.Black, r.X + i * m, r.Y + j * m, m, m);
                }
        }

        private void Picture(Graphics g, RectangleF r, LabelField f)
        {
            string file = _v != null && f.IsDataField ? _v[f.Key] : f.Text;
            string path = string.IsNullOrWhiteSpace(file) ? "" : Path.IsPathRooted(file) ? file : Path.Combine(_v != null ? _v.PictureFolder ?? "" : "", file);
            try
            {
                if (path.Length > 0 && File.Exists(path))
                    using (var img = Image.FromFile(path))
                    {
                        float k = Math.Min(r.Width / img.Width, r.Height / img.Height);
                        g.DrawImage(img, r.X, r.Y, img.Width * k, img.Height * k);
                        return;
                    }
            }
            catch { }
            using (var pen = new Pen(Color.Gray) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot })
            {
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                g.DrawLine(pen, r.X, r.Y, r.Right, r.Bottom);
                g.DrawLine(pen, r.Right, r.Y, r.X, r.Bottom);
            }
        }
    }

    /// <summary>Code 128 set B (the printer's barcode 1 of EPL, ^BC of ZPL): the widths of its bars and spaces.</summary>
    public static class Code128
    {
        private static readonly string[] Patterns = ("212222 222122 222221 121223 121322 131222 122213 122312 132212 221213 221312 231212 112232 122132 122231 113222 " +
            "123122 123221 223211 221132 221231 213212 223112 312131 311222 321122 321221 312212 322112 322211 212123 212321 232121 111323 131123 131321 " +
            "112313 132113 132311 211313 231113 231311 112133 112331 132131 113123 113321 133121 313121 211331 231131 213113 213311 213131 311123 311321 " +
            "331121 312113 312311 332111 314111 221411 431111 111224 111422 121124 121421 141122 141221 112214 112412 122114 122411 142112 142211 241211 " +
            "221114 413111 241112 134111 111242 121142 121241 114212 124112 124211 411212 421112 421211 212141 214121 412121 111143 111341 131141 114113 " +
            "114311 411113 411311 113141 114131 311141 411131 211412 211214 211232").Split(' ');
        private const string Stop = "2331112";
        private const int StartB = 104;

        /// <summary>The bar, space, bar ... widths (modules) of the code, its quiet zones left out; a character set B lacks as '?'.</summary>
        public static List<int> Widths(string data)
        {
            var values = new List<int> { StartB };
            foreach (char ch in data ?? "")
                values.Add(ch >= 32 && ch <= 127 ? ch - 32 : '?' - 32);
            int check = StartB;
            for (int i = 1; i < values.Count; i++) check += values[i] * i;
            values.Add(check % 103);
            var w = new List<int>();
            foreach (var v in values) w.AddRange(Patterns[v].Select(c => c - '0'));
            w.AddRange(Stop.Select(c => c - '0'));
            return w;
        }
    }
}
