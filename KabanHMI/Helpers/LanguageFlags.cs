using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using Kaban.Core.Helpers;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// THE LANGUAGES' FLAGS (23 October: "We need a language emoji addition tool so we can add flags to the new
    /// added languages"). Windows does not draw a country's flag emoji (🇵🇱 is shown as the letters "PL"), so the
    /// program paints its flags: the seven languages it came with have pictures of their own (Resources\Flags); a
    /// language added since was a grey box. Now:
    ///   * a flag MADE FOR A LANGUAGE on the panel (HMI Settings > Translation, the flag button) - a country's
    ///     flag from the list below, stripes or a cross in the colours chosen, or a picture - is kept as a picture,
    ///     Translations\Flags\&lt;code&gt;.png beside the languages' list, and shown for it everywhere (the caption
    ///     bar, the language menu, the preferences);
    ///   * a language with none: its country's flag when the language says which (PL - Poland, SV - Sweden, JA -
    ///     Japan ...), drawn; else the grey box.
    /// The countries' flags are drawn as they look at the caption bar's size - a coat of arms left out.
    /// </summary>
    public static class LanguageFlags
    {
        /// <summary>The pictures' size: as Resources\Flags' (3:2).</summary>
        public const int Width = 60, Height = 40;

        /// <summary>Where the flags made on the panel are kept.</summary>
        public static string Folder { get { return Path.Combine(TranslationEdits.Folder, "Flags"); } }

        public static string FileOf(string code) { return Path.Combine(Folder, Clean(code) + ".png"); }

        /// <summary>A flag was made or taken away.</summary>
        public static event EventHandler Changed;

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, Image> _pictures = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        private static string Clean(string code) { return (code ?? "").Trim().ToUpperInvariant(); }

        /// <summary>The flag made for a language on the panel (null: none).</summary>
        public static Image Picture(string code)
        {
            code = Clean(code);
            if (code.Length == 0) return null;
            lock (_lock)
            {
                Image img;
                if (_pictures.TryGetValue(code, out img)) return img;
                img = null;
                try
                {
                    var path = FileOf(code);
                    if (File.Exists(path))
                        using (var ms = new MemoryStream(File.ReadAllBytes(path)))      // the file not held open
                        using (var read = Image.FromStream(ms))
                            img = new Bitmap(read);
                }
                catch (Exception ex) { AuditLogger.Log("language flag " + code + ": " + ex.Message); img = null; }
                _pictures[code] = img;
                return img;
            }
        }

        /// <summary>A language's flag kept (fitted to 3:2, <see cref="Width"/> x <see cref="Height"/>).</summary>
        public static void Save(string code, Image flag, string what)
        {
            code = Clean(code);
            if (code.Length == 0 || flag == null) return;
            Directory.CreateDirectory(Folder);
            using (var fitted = Fit(flag))
            {
                var path = FileOf(code);
                SafetyCopies.FirstOfDay("Translations", path);
                var tmp = path + ".tmp";
                fitted.Save(tmp, ImageFormat.Png);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            lock (_lock) _pictures.Remove(code);
            AuditLogger.Log(string.Format("language flag of {0} made by {1}: {2}", code, SecurityManager.CurrentUser, what ?? ""));
            var h = Changed; if (h != null) h(null, EventArgs.Empty);
        }

        /// <summary>The flag made for a language taken away - its country's (or the program's own) shown again. False: none was made.</summary>
        public static bool Remove(string code)
        {
            code = Clean(code);
            var path = FileOf(code);
            if (!File.Exists(path)) return false;
            SafetyCopies.FirstOfDay("Translations", path);
            File.Delete(path);
            lock (_lock) _pictures.Remove(code);
            AuditLogger.Log(string.Format("language flag of {0} taken away by {1}", code, SecurityManager.CurrentUser));
            var h = Changed; if (h != null) h(null, EventArgs.Empty);
            return true;
        }

        /// <summary>A picture fitted to the flags' 3:2: its middle, cut at the sides or the top and bottom.</summary>
        public static Bitmap Fit(Image src)
        {
            var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float want = (float)Width / Height, has = (float)src.Width / Math.Max(1, src.Height);
                RectangleF from = has > want
                    ? new RectangleF((src.Width - src.Height * want) / 2f, 0, src.Height * want, src.Height)
                    : new RectangleF(0, (src.Height - src.Width / want) / 2f, src.Width, src.Width / want);
                g.DrawImage(src, new RectangleF(0, 0, Width, Height), from, GraphicsUnit.Pixel);
            }
            return bmp;
        }

        /// <summary>A drawing at the flags' size, drawn four times larger and made smaller (smooth edges).</summary>
        public static Bitmap Render(Action<Graphics, RectangleF> draw, int width = Width, int height = Height)
        {
            const int SS = 4;
            var big = new Bitmap(width * SS, height * SS, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(big))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                draw(g, new RectangleF(0, 0, big.Width, big.Height));
            }
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(big, new Rectangle(0, 0, width, height));
            }
            big.Dispose();
            return bmp;
        }

        // ==================================================================
        // A COUNTRY'S FLAG
        // ==================================================================

        /// <summary>A country's flag: its code (ISO 3166), its name, how it is drawn.</summary>
        public sealed class Country
        {
            public string Code = "", Name = "";
            public Action<Graphics, RectangleF> Draw;
            public override string ToString() { return Name + " (" + Code + ")"; }
        }

        private static List<Country> _countries;

        /// <summary>The countries whose flags the program draws, by name.</summary>
        public static IList<Country> Countries
        {
            get { if (_countries == null) _countries = MakeCountries().OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList(); return _countries; }
        }

        public static Country CountryOf(string code)
        {
            code = Clean(code);
            return Countries.FirstOrDefault(c => c.Code == code);
        }

        /// <summary>
        /// The country of a language - the one it is spoken in first (SV Sweden, JA Japan, UK Ukraine, EL Greece, PL
        /// Poland). Null: none known - a language's code is not its country's (AR is Arabic, not Argentina; GA Irish,
        /// not Gabon; ML Malayalam, not Mali), so only the languages named here have one.
        /// </summary>
        public static Country CountryOfLanguage(string language)
        {
            string country;
            return LanguageCountry.TryGetValue(Clean(language), out country) ? CountryOf(country) : null;
        }

        private static readonly Dictionary<string, string> LanguageCountry = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "EN", "GB" }, { "TR", "TR" }, { "DE", "DE" }, { "FR", "FR" }, { "IT", "IT" }, { "ES", "ES" }, { "RU", "RU" }, { "PL", "PL" },
            { "NL", "NL" }, { "SV", "SE" }, { "DA", "DK" }, { "NB", "NO" }, { "NN", "NO" }, { "NO", "NO" }, { "FI", "FI" }, { "IS", "IS" },
            { "GA", "IE" }, { "PT", "PT" }, { "EL", "GR" }, { "CS", "CZ" }, { "HU", "HU" }, { "RO", "RO" }, { "BG", "BG" }, { "UK", "UA" },
            { "LT", "LT" }, { "LV", "LV" }, { "ET", "EE" }, { "LB", "LU" }, { "JA", "JP" }, { "ZH", "CN" }, { "VI", "VN" }, { "TH", "TH" },
            { "ID", "ID" }, { "HI", "IN" }, { "HY", "AM" }, { "KA", "GE" }, { "AZ", "AZ" }, { "HE", "IL" }, { "KO", "KR" }, { "BN", "BD" }
        };

        /// <summary>"🇵🇱" (two regional indicator letters, as a flag emoji is written) - "PL"; null: no flag emoji in it.</summary>
        public static string CodeOfEmoji(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var letters = new List<char>();
            for (int i = 0; i < text.Length; i++)
            {
                int cp = char.ConvertToUtf32(text, i);
                if (char.IsSurrogatePair(text, i)) i++;
                if (cp >= 0x1F1E6 && cp <= 0x1F1FF) letters.Add((char)('A' + (cp - 0x1F1E6)));
                if (letters.Count == 2) return new string(letters.ToArray());
            }
            return null;
        }

        /// <summary>A country's code as its flag emoji: "PL" - "🇵🇱".</summary>
        public static string EmojiOf(string code)
        {
            code = Clean(code);
            if (code.Length != 2 || code.Any(c => c < 'A' || c > 'Z')) return "";
            return char.ConvertFromUtf32(0x1F1E6 + code[0] - 'A') + char.ConvertFromUtf32(0x1F1E6 + code[1] - 'A');
        }

        // ==================================================================
        // STRIPES OR A CROSS (a flag of no country of the list: a region's)
        // ==================================================================

        public enum Pattern { Plain, TwoBands, ThreeBands, ThreeVertical, NordicCross, Cross }

        public static string PatternName(Pattern p)
        {
            switch (p)
            {
                case Pattern.Plain: return LanguageManager.GetString("Flg_Plain", "One colour");
                case Pattern.TwoBands: return LanguageManager.GetString("Flg_TwoBands", "Two bands");
                case Pattern.ThreeBands: return LanguageManager.GetString("Flg_ThreeBands", "Three bands");
                case Pattern.ThreeVertical: return LanguageManager.GetString("Flg_ThreeVertical", "Three upright bands");
                case Pattern.NordicCross: return LanguageManager.GetString("Flg_NordicCross", "A Nordic cross");
                default: return LanguageManager.GetString("Flg_Cross", "A cross");
            }
        }

        /// <summary>How many colours a pattern takes.</summary>
        public static int ColoursOf(Pattern p)
        {
            switch (p)
            {
                case Pattern.Plain: return 1;
                case Pattern.TwoBands: case Pattern.NordicCross: case Pattern.Cross: return 2;
                default: return 3;
            }
        }

        public static void DrawPattern(Graphics g, RectangleF f, Pattern p, Color[] c)
        {
            Func<int, Color> at = i => c != null && i < c.Length ? c[i] : Color.Gray;
            switch (p)
            {
                case Pattern.Plain: Fill(g, f, at(0)); break;
                case Pattern.TwoBands: HBands(g, f, at(0), at(1)); break;
                case Pattern.ThreeBands: HBands(g, f, at(0), at(1), at(2)); break;
                case Pattern.ThreeVertical: VBands(g, f, at(0), at(1), at(2)); break;
                case Pattern.NordicCross: Nordic(g, f, at(0), at(1), Color.Empty, 12 / 28f, 4 / 28f, 0); break;
                default: Fill(g, f, at(0)); CenterCross(g, f, at(1), f.Height * 0.2f); break;
            }
        }

        // ---- drawing --------------------------------------------------------

        private static Color C(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
        private static readonly Color White = Color.White, Black = Color.Black;

        private static void Fill(Graphics g, RectangleF r, Color c) { using (var b = new SolidBrush(c)) g.FillRectangle(b, r); }

        /// <summary>Bands from the top, as wide as their weights (all 1 when none given).</summary>
        private static void HBands(Graphics g, RectangleF f, params Color[] c) { HBandsW(g, f, null, c); }
        private static void HBandsW(Graphics g, RectangleF f, float[] w, params Color[] c)
        {
            float total = w == null ? c.Length : w.Sum(), y = f.Top;
            for (int i = 0; i < c.Length; i++)
            {
                float h = f.Height * (w == null ? 1 : w[i]) / total;
                Fill(g, new RectangleF(f.Left, y, f.Width, h + 1), c[i]);
                y += h;
            }
        }

        private static void VBands(Graphics g, RectangleF f, params Color[] c) { VBandsW(g, f, null, c); }
        private static void VBandsW(Graphics g, RectangleF f, float[] w, params Color[] c)
        {
            float total = w == null ? c.Length : w.Sum(), x = f.Left;
            for (int i = 0; i < c.Length; i++)
            {
                float wd = f.Width * (w == null ? 1 : w[i]) / total;
                Fill(g, new RectangleF(x, f.Top, wd + 1, f.Height), c[i]);
                x += wd;
            }
        }

        /// <summary>A Nordic cross: its upright bar from <paramref name="x"/> (of the height) from the hoist, <paramref name="w"/> wide; an inner cross <paramref name="wi"/>.</summary>
        private static void Nordic(Graphics g, RectangleF f, Color field, Color cross, Color inner, float x, float w, float wi)
        {
            Fill(g, f, field);
            float H = f.Height, bx = f.Left + x * H, bw = w * H, by = f.Top + (H - bw) / 2f;
            Fill(g, new RectangleF(bx, f.Top, bw, H), cross);
            Fill(g, new RectangleF(f.Left, by, f.Width, bw), cross);
            if (inner.IsEmpty || wi <= 0) return;
            float iw = wi * H, ix = bx + (bw - iw) / 2f, iy = f.Top + (H - iw) / 2f;
            Fill(g, new RectangleF(ix, f.Top, iw, H), inner);
            Fill(g, new RectangleF(f.Left, iy, f.Width, iw), inner);
        }

        private static void CenterCross(Graphics g, RectangleF f, Color c, float w)
        {
            Fill(g, new RectangleF(f.Left + (f.Width - w) / 2f, f.Top, w, f.Height), c);
            Fill(g, new RectangleF(f.Left, f.Top + (f.Height - w) / 2f, f.Width, w), c);
        }

        private static void Disc(Graphics g, float cx, float cy, float r, Color c) { using (var b = new SolidBrush(c)) g.FillEllipse(b, cx - r, cy - r, 2 * r, 2 * r); }

        private static void Ring(Graphics g, float cx, float cy, float r, float width, Color c) { using (var p = new Pen(c, width)) g.DrawEllipse(p, cx - r, cy - r, 2 * r, 2 * r); }

        private static void Star(Graphics g, float cx, float cy, float r, Color c, double turn = 0, int points = 5, float inner = 0.38f)
        {
            var pts = new PointF[points * 2];
            for (int i = 0; i < pts.Length; i++)
            {
                double a = -Math.PI / 2 + turn + i * Math.PI / points;
                double rad = i % 2 == 0 ? r : r * inner;
                pts[i] = new PointF((float)(cx + Math.Cos(a) * rad), (float)(cy + Math.Sin(a) * rad));
            }
            using (var b = new SolidBrush(c)) g.FillPolygon(b, pts);
        }

        private static IEnumerable<Country> MakeCountries()
        {
            Func<string, string, Action<Graphics, RectangleF>, Country> k = (code, name, d) => new Country { Code = code, Name = name, Draw = d };

            // ---- bands from the top ----
            yield return k("DE", "Germany", (g, f) => HBands(g, f, Black, C(0xDD0000), C(0xFFCE00)));
            yield return k("RU", "Russia", (g, f) => HBands(g, f, White, C(0x0039A6), C(0xD52B1E)));
            yield return k("NL", "Netherlands", (g, f) => HBands(g, f, C(0xAE1C28), White, C(0x21468B)));
            yield return k("AT", "Austria", (g, f) => HBands(g, f, C(0xC8102E), White, C(0xC8102E)));
            yield return k("HU", "Hungary", (g, f) => HBands(g, f, C(0xCD2A3E), White, C(0x436F4D)));
            yield return k("BG", "Bulgaria", (g, f) => HBands(g, f, White, C(0x00966E), C(0xD62612)));
            yield return k("LT", "Lithuania", (g, f) => HBands(g, f, C(0xFDB913), C(0x006A44), C(0xC1272D)));
            yield return k("EE", "Estonia", (g, f) => HBands(g, f, C(0x0072CE), Black, White));
            yield return k("LU", "Luxembourg", (g, f) => HBands(g, f, C(0xEA141D), White, C(0x51ADDA)));
            yield return k("AM", "Armenia", (g, f) => HBands(g, f, C(0xD90012), C(0x0033A0), C(0xF2A800)));
            yield return k("YE", "Yemen", (g, f) => HBands(g, f, C(0xCE1126), White, Black));
            yield return k("GA", "Gabon", (g, f) => HBands(g, f, C(0x009E60), C(0xFCD116), C(0x3A75C4)));
            yield return k("PL", "Poland", (g, f) => HBands(g, f, White, C(0xDC143C)));
            yield return k("UA", "Ukraine", (g, f) => HBands(g, f, C(0x0057B7), C(0xFFD700)));
            yield return k("ID", "Indonesia", (g, f) => HBands(g, f, C(0xCE1126), White));
            yield return k("MC", "Monaco", (g, f) => HBands(g, f, C(0xCE1126), White));
            yield return k("LV", "Latvia", (g, f) => HBandsW(g, f, new[] { 2f, 1f, 2f }, C(0x9E3039), White, C(0x9E3039)));
            yield return k("CO", "Colombia", (g, f) => HBandsW(g, f, new[] { 2f, 1f, 1f }, C(0xFCD116), C(0x003893), C(0xCE1126)));
            yield return k("ES", "Spain", (g, f) => HBandsW(g, f, new[] { 1f, 2f, 1f }, C(0xAA151B), C(0xF1BF00), C(0xAA151B)));
            yield return k("TH", "Thailand", (g, f) => HBandsW(g, f, new[] { 1f, 1f, 2f, 1f, 1f }, C(0xA51931), C(0xF4F5F8), C(0x2D2A4A), C(0xF4F5F8), C(0xA51931)));
            yield return k("AR", "Argentina", (g, f) =>
            {
                HBands(g, f, C(0x74ACDF), White, C(0x74ACDF));
                Disc(g, f.Left + f.Width / 2, f.Top + f.Height / 2, f.Height * 0.11f, C(0xF6B40E));
            });
            yield return k("IN", "India", (g, f) =>
            {
                HBands(g, f, C(0xFF9933), White, C(0x138808));
                Ring(g, f.Left + f.Width / 2, f.Top + f.Height / 2, f.Height * 0.13f, f.Height * 0.03f, C(0x000080));
            });
            yield return k("AZ", "Azerbaijan", (g, f) =>
            {
                HBands(g, f, C(0x00B5E2), C(0xEF3340), C(0x509E2F));
                float cx = f.Left + f.Width * 0.47f, cy = f.Top + f.Height / 2;
                Disc(g, cx, cy, f.Height * 0.15f, White);
                Disc(g, cx + f.Height * 0.035f, cy, f.Height * 0.125f, C(0xEF3340));
                Star(g, cx + f.Height * 0.16f, cy, f.Height * 0.06f, White, 0, 8, 0.45f);
            });
            yield return k("CZ", "Czechia", (g, f) =>
            {
                HBands(g, f, White, C(0xD7141A));
                using (var b = new SolidBrush(C(0x11457E)))
                    g.FillPolygon(b, new[] { new PointF(f.Left, f.Top), new PointF(f.Left + f.Width / 2, f.Top + f.Height / 2), new PointF(f.Left, f.Bottom) });
            });
            yield return k("AE", "United Arab Emirates", (g, f) =>
            {
                HBands(g, new RectangleF(f.Left + f.Width / 4, f.Top, f.Width * 3 / 4, f.Height), C(0x00732F), White, Black);
                Fill(g, new RectangleF(f.Left, f.Top, f.Width / 4, f.Height), C(0xFF0000));
            });
            yield return k("IL", "Israel", (g, f) =>
            {
                HBandsW(g, f, new[] { 1.5f, 1f, 5.5f, 1f, 1.5f }, White, C(0x0038B8), White, C(0x0038B8), White);
                float cx = f.Left + f.Width / 2, cy = f.Top + f.Height / 2, r = f.Height * 0.2f;
                using (var p = new Pen(C(0x0038B8), f.Height * 0.03f))
                    for (int t = 0; t < 2; t++)
                    {
                        var tri = new PointF[3];
                        for (int i = 0; i < 3; i++)
                        {
                            double a = -Math.PI / 2 + t * Math.PI + i * 2 * Math.PI / 3;
                            tri[i] = new PointF((float)(cx + Math.Cos(a) * r), (float)(cy + Math.Sin(a) * r));
                        }
                        g.DrawPolygon(p, tri);
                    }
            });
            yield return k("US", "United States", (g, f) =>
            {
                var stripes = Enumerable.Range(0, 13).Select(i => i % 2 == 0 ? C(0xB22234) : White).ToArray();
                HBands(g, f, stripes);
                var canton = new RectangleF(f.Left, f.Top, f.Width * 0.4f, f.Height * 7 / 13f);
                Fill(g, canton, C(0x3C3B6E));
                for (int row = 0; row < 5; row++)
                    for (int col = 0; col < 6; col++)
                        Disc(g, canton.Left + canton.Width * (col + 0.5f) / 6, canton.Top + canton.Height * (row + 0.5f) / 5, canton.Height * 0.045f, White);
            });
            yield return k("GR", "Greece", (g, f) =>
            {
                var stripes = Enumerable.Range(0, 9).Select(i => i % 2 == 0 ? C(0x0D5EAF) : White).ToArray();
                HBands(g, f, stripes);
                float s = f.Height * 5 / 9f;
                Fill(g, new RectangleF(f.Left, f.Top, s, s), C(0x0D5EAF));
                float w = f.Height / 9f;
                Fill(g, new RectangleF(f.Left + (s - w) / 2, f.Top, w, s), White);
                Fill(g, new RectangleF(f.Left, f.Top + (s - w) / 2, s, w), White);
            });

            // ---- upright bands ----
            yield return k("FR", "France", (g, f) => VBands(g, f, C(0x0055A4), White, C(0xEF4135)));
            yield return k("IT", "Italy", (g, f) => VBands(g, f, C(0x009246), White, C(0xCE2B37)));
            yield return k("BE", "Belgium", (g, f) => VBands(g, f, Black, C(0xFDDA24), C(0xEF3340)));
            yield return k("RO", "Romania", (g, f) => VBands(g, f, C(0x002B7F), C(0xFCD116), C(0xCE1126)));
            yield return k("IE", "Ireland", (g, f) => VBands(g, f, C(0x169B62), White, C(0xFF883E)));
            yield return k("ML", "Mali", (g, f) => VBands(g, f, C(0x14B53A), C(0xFCD116), C(0xCE1126)));
            yield return k("NG", "Nigeria", (g, f) => VBands(g, f, C(0x008751), White, C(0x008751)));
            yield return k("CI", "Côte d'Ivoire", (g, f) => VBands(g, f, C(0xF77F00), White, C(0x009E60)));
            yield return k("PE", "Peru", (g, f) => VBands(g, f, C(0xD91023), White, C(0xD91023)));
            yield return k("PT", "Portugal", (g, f) =>
            {
                VBandsW(g, f, new[] { 2f, 3f }, C(0x046A38), C(0xDA291C));
                Ring(g, f.Left + f.Width * 0.4f, f.Top + f.Height / 2, f.Height * 0.2f, f.Height * 0.05f, C(0xFFE900));
                Disc(g, f.Left + f.Width * 0.4f, f.Top + f.Height / 2, f.Height * 0.12f, White);
            });

            // ---- crosses ----
            yield return k("SE", "Sweden", (g, f) => Nordic(g, f, C(0x006AA7), C(0xFECC00), Color.Empty, 0.5f, 0.2f, 0));
            yield return k("DK", "Denmark", (g, f) => Nordic(g, f, C(0xC8102E), White, Color.Empty, 12 / 28f, 4 / 28f, 0));
            yield return k("NO", "Norway", (g, f) => Nordic(g, f, C(0xBA0C2F), White, C(0x00205B), 6 / 16f, 4 / 16f, 2 / 16f));
            yield return k("IS", "Iceland", (g, f) => Nordic(g, f, C(0x02529C), White, C(0xDC1E35), 7 / 18f, 4 / 18f, 2 / 18f));
            yield return k("FI", "Finland", (g, f) => Nordic(g, f, White, C(0x002F6C), Color.Empty, 5 / 11f, 3 / 11f, 0));
            yield return k("CH", "Switzerland", (g, f) =>
            {
                Fill(g, f, C(0xDA291C));
                float cx = f.Left + f.Width / 2, cy = f.Top + f.Height / 2, arm = f.Height * 0.31f, w = f.Height * 0.19f;
                Fill(g, new RectangleF(cx - w / 2, cy - arm, w, 2 * arm), White);
                Fill(g, new RectangleF(cx - arm, cy - w / 2, 2 * arm, w), White);
            });
            yield return k("GE", "Georgia", (g, f) =>
            {
                Fill(g, f, White);
                CenterCross(g, f, C(0xFF0000), f.Height * 0.2f);
                float qx = f.Width / 4f, qy = f.Height / 4f, s = f.Height * 0.12f, w = f.Height * 0.045f;
                foreach (var p in new[] { new PointF(qx, qy), new PointF(3 * qx, qy), new PointF(qx, 3 * qy), new PointF(3 * qx, 3 * qy) })
                {
                    Fill(g, new RectangleF(f.Left + p.X - w / 2, f.Top + p.Y - s / 2, w, s), C(0xFF0000));
                    Fill(g, new RectangleF(f.Left + p.X - s / 2, f.Top + p.Y - w / 2, s, w), C(0xFF0000));
                }
            });
            yield return k("GB", "United Kingdom", (g, f) =>
            {
                Fill(g, f, C(0x012169));
                using (var wide = new Pen(White, f.Height * 0.2f)) { g.DrawLine(wide, f.Left, f.Top, f.Right, f.Bottom); g.DrawLine(wide, f.Right, f.Top, f.Left, f.Bottom); }
                using (var thin = new Pen(C(0xC8102E), f.Height * 0.067f)) { g.DrawLine(thin, f.Left, f.Top, f.Right, f.Bottom); g.DrawLine(thin, f.Right, f.Top, f.Left, f.Bottom); }
                CenterCross(g, f, White, f.Height / 3f);
                CenterCross(g, f, C(0xC8102E), f.Height / 5f);
            });

            // ---- a disc, stars, a crescent ----
            yield return k("JP", "Japan", (g, f) => { Fill(g, f, White); Disc(g, f.Left + f.Width / 2, f.Top + f.Height / 2, f.Height * 0.3f, C(0xBC002D)); });
            yield return k("BD", "Bangladesh", (g, f) => { Fill(g, f, C(0x006A4E)); Disc(g, f.Left + f.Width * 0.45f, f.Top + f.Height / 2, f.Height * 0.3f, C(0xF42A41)); });
            yield return k("KR", "South Korea", (g, f) =>
            {
                Fill(g, f, White);
                float cx = f.Left + f.Width / 2, cy = f.Top + f.Height / 2, r = f.Height * 0.25f;
                Disc(g, cx, cy, r, C(0x0047A0));
                using (var red = new SolidBrush(C(0xCD2E3A))) g.FillPie(red, cx - r, cy - r, 2 * r, 2 * r, 180, 180);
                Disc(g, cx - r / 2, cy, r / 2, C(0xCD2E3A));
                Disc(g, cx + r / 2, cy, r / 2, C(0x0047A0));
            });
            yield return k("VN", "Vietnam", (g, f) => { Fill(g, f, C(0xDA251D)); Star(g, f.Left + f.Width / 2, f.Top + f.Height / 2, f.Height * 0.3f, C(0xFFFF00)); });
            yield return k("CN", "China", (g, f) =>
            {
                Fill(g, f, C(0xEE1C25));
                float u = f.Height / 20f;
                Star(g, f.Left + 5 * u, f.Top + 5 * u, 3 * u, C(0xFFFF00));
                foreach (var p in new[] { new PointF(10, 2), new PointF(12, 4), new PointF(12, 7), new PointF(10, 9) })
                    Star(g, f.Left + p.X * u, f.Top + p.Y * u, u, C(0xFFFF00), Math.Atan2(5 - p.Y, 5 - p.X) + Math.PI / 2);
            });
            yield return k("TR", "Türkiye", (g, f) =>
            {
                Fill(g, f, C(0xE30A17));
                float outer = f.Height * 0.25f, cx = f.Left + f.Width * 0.36f, cy = f.Top + f.Height / 2;
                Disc(g, cx, cy, outer, White);
                Disc(g, cx + outer * 0.25f, cy, outer * 0.8f, C(0xE30A17));
                Star(g, cx + outer * 1.05f, cy, f.Height * 0.125f, White, -Math.PI / 2);
            });
            yield return k("BR", "Brazil", (g, f) =>
            {
                Fill(g, f, C(0x009C3B));
                float mx = f.Width * 0.085f, my = f.Height * 0.085f;
                using (var y = new SolidBrush(C(0xFFDF00)))
                    g.FillPolygon(y, new[] { new PointF(f.Left + f.Width / 2, f.Top + my), new PointF(f.Right - mx, f.Top + f.Height / 2),
                                             new PointF(f.Left + f.Width / 2, f.Bottom - my), new PointF(f.Left + mx, f.Top + f.Height / 2) });
                Disc(g, f.Left + f.Width / 2, f.Top + f.Height / 2, f.Height * 0.25f, C(0x002776));
            });
        }
    }
}
