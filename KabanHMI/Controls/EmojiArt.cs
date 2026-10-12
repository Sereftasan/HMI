using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// The colour pictures of the emoji buttons.
    ///
    /// Windows Forms draws a button's text with GDI, and GDI knows one colour per
    /// letter: an emoji came out as a grey, soft outline of itself (the export
    /// tray, the floppy disk...), hard to tell apart at a glance. These are the
    /// emojis Windows 11 shows - Microsoft's Fluent Emoji, flat, MIT licence -
    /// drawn as pictures, a little paler than the originals so a grey page stays
    /// calm (tools/build_emoji_art.py makes them: Resources\Emoji).
    ///
    /// The arrows of undo and redo (and of "throw the changes away") are not
    /// pictures but the PLC Monitor's own drawn curls (ToolGlyphs), so every undo
    /// of the program looks the same.
    /// </summary>
    public static class EmojiArt
    {
        private static readonly object Gate = new object();
        private static Dictionary<string, int> _index;          // code points -> place in the strip
        private static int[] _sizes;
        private static readonly Dictionary<int, Bitmap> _strips = new Dictionary<int, Bitmap>();

        /// <summary>
        /// The code points of a text in lower-case hex, joined by '-', without the
        /// emoji presentation selector (FE0F) and without spaces: "1f4e4" for the
        /// outbox tray, "2699" for the gear with or without its FE0F.
        /// </summary>
        public static string KeyOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                int cp;
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    cp = char.ConvertToUtf32(text[i], text[i + 1]);
                    i++;
                }
                else cp = text[i];
                if (cp == 0xFE0F || cp == 0xFE0E || (cp < 0x10000 && char.IsWhiteSpace((char)cp))) continue;
                if (sb.Length > 0) sb.Append('-');
                sb.Append(cp.ToString("x", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>True when the text is one emoji that has a colour picture.</summary>
        public static bool Has(string text)
        {
            var key = KeyOf(text);
            return key.Length > 0 && Index().ContainsKey(key);
        }

        /// <summary>The sizes the pictures are drawn at (pixels), smallest first.</summary>
        public static int[] Sizes { get { Index(); return (int[])_sizes.Clone(); } }

        /// <summary>
        /// The drawn curl for an undo-like arrow (undo, throw away, cancel the
        /// move) or a redo-like one; null for anything else.
        /// </summary>
        public static Action<Graphics, Rectangle, bool> GlyphFor(string text)
        {
            switch (KeyOf(text))
            {
                case "21b6":      // anticlockwise top semicircle arrow: Undo
                case "21ba":      // anticlockwise open circle arrow: throw the changes away, read again
                case "21a9":      // leftwards arrow with hook: forget the move
                    return ToolGlyphs.Undo;
                case "21b7":      // clockwise top semicircle arrow: Redo
                case "21bb":
                case "21aa":
                    return ToolGlyphs.Redo;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The picture's size for a button: about the height of a line of its
        /// font (as the emoji letter was), never more than the room there is.
        /// </summary>
        public static int SizeFor(Font font, int room)
        {
            int px = font != null ? (int)Math.Round(font.Height * 0.95) : 20;
            if (room > 0) px = Math.Min(px, room);
            return Math.Max(12, px);
        }

        /// <summary>
        /// Draws the text's picture centred in the area, px across; disabled, it is
        /// grey and faint, as a disabled button's text is. False when the text has
        /// no picture (nothing is drawn).
        /// </summary>
        public static bool Draw(Graphics g, Rectangle area, string text, bool enabled, int px)
        {
            int place;
            if (g == null || !Index().TryGetValue(KeyOf(text), out place)) return false;
            int size = PickSize(px);
            var strip = Strip(size);
            if (strip == null) return false;

            var dest = new Rectangle(area.X + (area.Width - px) / 2, area.Y + (area.Height - px) / 2, px, px);
            var state = g.Save();
            try
            {
                g.InterpolationMode = size == px ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                if (enabled)
                    g.DrawImage(strip, dest, place * size, 0, size, size, GraphicsUnit.Pixel);
                else
                    using (var faint = Faint())
                        g.DrawImage(strip, dest, place * size, 0, size, size, GraphicsUnit.Pixel, faint);
            }
            finally { g.Restore(state); }
            return true;
        }

        /// <summary>Grey, 40 % strong: the look of a disabled button's text.</summary>
        private static ImageAttributes Faint()
        {
            var m = new ColorMatrix(new[]
            {
                new[] { 0.30f, 0.30f, 0.30f, 0f, 0f },
                new[] { 0.59f, 0.59f, 0.59f, 0f, 0f },
                new[] { 0.11f, 0.11f, 0.11f, 0f, 0f },
                new[] { 0f, 0f, 0f, 0.40f, 0f },
                new[] { 0.12f, 0.12f, 0.12f, 0f, 1f }
            });
            var a = new ImageAttributes();
            a.SetColorMatrix(m);
            return a;
        }

        /// <summary>The smallest size at least px across, else the largest.</summary>
        private static int PickSize(int px)
        {
            Index();
            foreach (var s in _sizes) if (s >= px) return s;
            return _sizes[_sizes.Length - 1];
        }

        private static Bitmap Strip(int size)
        {
            lock (Gate)
            {
                Bitmap b;
                if (_strips.TryGetValue(size, out b)) return b;
                b = null;
                using (var s = Resource("emoji_" + size.ToString(CultureInfo.InvariantCulture) + ".png"))
                    if (s != null)
                        using (var loaded = new Bitmap(s))
                            b = new Bitmap(loaded);           // a copy: the stream may close
                _strips[size] = b;
                return b;
            }
        }

        private static Stream Resource(string name)
        {
            return typeof(EmojiArt).Assembly.GetManifestResourceStream("Kaban.UI.Emoji." + name);
        }

        private static Dictionary<string, int> Index()
        {
            lock (Gate)
            {
                if (_index != null) return _index;
                var index = new Dictionary<string, int>(StringComparer.Ordinal);
                var sizes = new List<int>();
                using (var s = Resource("emoji.txt"))
                    if (s != null)
                        using (var r = new StreamReader(s, Encoding.ASCII))
                        {
                            string line;
                            int place = 0;
                            while ((line = r.ReadLine()) != null)
                            {
                                line = line.Trim();
                                if (line.StartsWith("# sizes", StringComparison.Ordinal))
                                {
                                    foreach (var p in line.Substring(7).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        int v;
                                        if (int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) sizes.Add(v);
                                    }
                                    continue;
                                }
                                if (line.Length == 0 || line[0] == '#') continue;
                                foreach (var key in line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                                    if (!index.ContainsKey(key)) index[key] = place;
                                place++;
                            }
                        }
                if (sizes.Count == 0) { index.Clear(); sizes.Add(20); }
                sizes.Sort();
                _sizes = sizes.ToArray();
                _index = index;
                return _index;
            }
        }
    }

    /// <summary>
    /// A toolbar button with an emoji for its text, drawn in colour (EmojiArt):
    /// the picture, the PLC Monitor's curl for undo and redo, or - for a sign
    /// that is no emoji (the turn and mirror arrows) - the sign itself in the
    /// toolbar's blue. Text keeps the emoji, so the button is found by it as
    /// before; only the painting leaves it out.
    /// </summary>
    public class EmojiButton : Button
    {
        private bool _hideText;

        public override string Text
        {
            get { return _hideText ? "" : base.Text; }
            set { base.Text = value; }
        }

        /// <summary>How the text is drawn: "art", "glyph" or "text".</summary>
        public string Look
        {
            get
            {
                if (EmojiArt.GlyphFor(base.Text) != null) return "glyph";
                return EmojiArt.Has(base.Text) ? "art" : "text";
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            string text = base.Text;
            var glyph = EmojiArt.GlyphFor(text);
            bool art = glyph == null && EmojiArt.Has(text);
            if (glyph == null && !art) { base.OnPaint(e); return; }

            _hideText = true;
            try { base.OnPaint(e); }
            finally { _hideText = false; }

            var g = e.Graphics;
            var r = ClientRectangle;
            int px = EmojiArt.SizeFor(Font, r.Height - 6);
            if (art)
            {
                EmojiArt.Draw(g, r, text, Enabled, px);
                return;
            }
            // The curls are drawn for 20 pixels: scaled to the button's size.
            var state = g.Save();
            try
            {
                float k = px / 20f;
                if (Math.Abs(k - 1f) > 0.01f)
                {
                    float cx = r.Width / 2f, cy = r.Height / 2f;
                    g.TranslateTransform(cx, cy);
                    g.ScaleTransform(k, k);
                    g.TranslateTransform(-cx, -cy);
                }
                glyph(g, r, Enabled);
            }
            finally { g.Restore(state); }
        }
    }
}
