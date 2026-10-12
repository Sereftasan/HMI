using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// THE PREVIEW of Users and Preferences, shown beside a list while it is open
    /// (50 px to its right), for the entry under the pointer, and gone when the
    /// list closes: the preference's name, the value, a small drawing of what it
    /// changes (fields with their captions, an alarm row with its date and time,
    /// a table at its rows and letters, a blinking or steady mark, a value in a
    /// unit ...) and where in the program it acts. The value drawn is the one
    /// under the pointer, not the one in force: the point is to see it before
    /// choosing it. The tooltip delay is played out (the pointer rests, the time
    /// runs, the tip shows); the alarm sound is heard as the pointer comes onto
    /// "On" (the page plays it). For "Read from" it
    /// is the user's preferences, line by line (ShowSummary).
    /// </summary>
    public class PreferencePreview : Control
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private string _key = ViewPreferences.CaptionAlign;
        private Func<string, string> _valueOf = k => ViewPreferences.PanelValue(k);
        private bool _asEveryone;
        private double? _scale;
        private double _panelScale = 1.0;
        private string _title;
        private IList<Line> _lines = new Line[0];
        private bool _phase = true;
        private readonly Timer _blink = new Timer { Interval = 500 };
        // the tooltip's delay played out: the pointer rests, the time runs, the tip shows
        private readonly Timer _tipClock = new Timer { Interval = 100 };
        private DateTime _tipStart = DateTime.Now;
        private string _tipValue;

        /// <summary>What is shown (tests): the preference's key and the value drawn.</summary>
        public string Key { get { return _key; } }
        public string Value { get { return _key == UsersPreferencesControl.ScaleKey ? (_scale.HasValue ? DisplaySettings.Percent(_scale.Value) : "") : _valueOf(_key); } }
        /// <summary>The tooltip delay played out in the drawing (tests); 0 for another preference.</summary>
        public int TryDelay { get { return _key == ViewPreferences.TooltipDelay ? ViewPreferences.DelayOf(_valueOf(_key)) : 0; } }
        /// <summary>The "where it acts" text on show (tests).</summary>
        public string Description { get { return Describe(); } }
        /// <summary>A summary's lines on show (tests).</summary>
        public IList<Line> Lines { get { return _lines; } }

        /// <summary>The key of a summary (Read from).</summary>
        public const string SummaryKey = "Summary";

        /// <summary>A line of a summary: a preference, its value, the user's own or "As for everyone".</summary>
        public sealed class Line
        {
            public string Name;
            public string Value;
            public bool Own;
        }

        /// <summary>How tall the preview wants to be, unscaled.</summary>
        public int WantedHeight { get { return _key == SummaryKey ? Math.Max(200, 70 + _lines.Count * 18 + 52) : 360; } }

        public PreferencePreview()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(250, 251, 252);
            Font = UiStyle.Body;
            _blink.Tick += (s, e) => { _phase = !_phase; Invalidate(); };
            _tipClock.Tick += (s, e) => { if (Visible) Invalidate(Area); };
            // Nothing in it to click or rest the pointer on: it is there only while a list is
            // open, and a click outside an open list closes it. The tooltip delay is played out
            // in the drawing; the sound is heard as the pointer comes onto "On" (the page).
            Disposed += (s, e) => { _blink.Dispose(); _tipClock.Dispose(); };
        }

        /// <summary>A view preference, as the page's combos have it now.</summary>
        public void ShowPreference(string key, Func<string, string> valueOf, bool asEveryone)
        {
            _key = key;
            _valueOf = valueOf ?? (k => ViewPreferences.PanelValue(k));
            _asEveryone = asEveryone;
            Changed();
        }

        /// <summary>The screen scale: the request (null: none) and the panel's.</summary>
        public void ShowScale(double? value, double panel)
        {
            _key = UsersPreferencesControl.ScaleKey;
            _scale = value;
            _panelScale = panel;
            _asEveryone = !value.HasValue;
            Changed();
        }

        /// <summary>A user's preferences, line by line (Read from).</summary>
        public void ShowSummary(string title, IList<Line> lines)
        {
            _key = SummaryKey;
            _title = title;
            _lines = lines ?? new Line[0];
            _asEveryone = false;
            Changed();
        }

        private void Changed()
        {
            bool blink = _key == ViewPreferences.Blinking && _valueOf(_key) != "Off";
            _blink.Enabled = blink;
            if (!blink) _phase = true;
            if (_key != ViewPreferences.TooltipDelay) _tipValue = null;
            _tipClock.Enabled = _key == ViewPreferences.TooltipDelay;
            // played from the start for each value shown
            if (_tipClock.Enabled && _tipValue != _valueOf(_key)) { _tipValue = _valueOf(_key); _tipStart = DateTime.Now; }
            Invalidate();
        }

        private static string Seconds(int ms) { return (ms / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + " s"; }

        private int S(int logical) { return UiScale.For(this, logical); }
        private float K { get { return UiScale.For(this, 1000) / 1000f; } }

        private Rectangle Area { get { return new Rectangle(S(14), S(92), Math.Max(S(120), Width - S(28)), S(178)); } }


        // ---- what it says ---------------------------------------------------------

        private string ValueLine()
        {
            string v;
            if (_key == SummaryKey) return T("PV_SummaryHint", "Choose to take these into the page - nothing is kept until Save.");
            if (_key == UsersPreferencesControl.ScaleKey)
                v = _scale.HasValue ? DisplaySettings.Percent(_scale.Value)
                                    : Kaban.UI.Forms.MainForm.NoScaleRequest + "  (" + DisplaySettings.Percent(_panelScale) + ")";
            else
            {
                v = PrefTexts.ValueText(_key, _valueOf(_key));
                if (_asEveryone) v = T("VP_AsEveryone", "As for everyone") + ":  " + v;
            }
            return v;
        }

        private string Describe()
        {
            switch (_key)
            {
                case UsersPreferencesControl.ScaleKey:
                    return T("PV_Scale", "Text, fields, buttons, menus and pages drawn this much larger or smaller. Taken after a restart - offered at once for your own, and when you sign in.");
                case ViewPreferences.CaptionAlign:
                    return T("PV_Captions", "The captions of number fields, combos and buttons, the axis names on Axis Settings and on the Axis Monitor.");
                case ViewPreferences.DecimalSeparator:
                    return T("PV_Decimal", "Every number on screen and the keypad's key. The keypad takes a point or a comma either way.");
                case ViewPreferences.DateFormat:
                case ViewPreferences.TimeFormat:
                    return T("PV_Dates", "Alarm lists and history, the logs, the Dashboard (its pickers, shifts and timeline), the shifts in Logs and Reports, the step-by-step pages.");
                case ViewPreferences.TableRows:
                case ViewPreferences.TableFont:
                    return T("PV_Tables", "Every table of the program, the open ones at once.");
                case ViewPreferences.StartPage:
                    return T("PV_Start", "Where the screen goes when this user signs in. 'My last page' is the page they were on last.");
                case ViewPreferences.AlarmSound:
                    return T("PV_Sound", "Windows' alert sound, once for each alarm that comes onto the list - not again while it stays.");
                case ViewPreferences.Blinking:
                    return T("PV_Blink", "Codes, lamps, borders, the alarm and connection marks on the top bar, the sign-in reminder.");
                case ViewPreferences.TooltipDelay:
                    return T("PV_Tooltip", "How long the pointer rests on a control before its tooltip shows - played out above.");
                case ViewPreferences.AiPromptAs:
                    return T("PV_AiAs", "How 'Create AI prompt' and 'Ask <AI>' put the prompt on the clipboard: as a file - pasted into the chat (Ctrl+V) it is attached, however long - or as its text.");
                case SummaryKey:
                    return "";
                default:
                    if (UnitOfKey(_key) != UnitType.None)
                        return T("PV_Unit", "Every value of this kind on screen, and what is typed into it - the PLC keeps its own unit, converted both ways.");
                    return "";
            }
        }

        // ---- the drawing ------------------------------------------------------------

        private static readonly Color Ink = Color.FromArgb(25, 28, 34);
        private static readonly Color Grey = Color.FromArgb(110, 116, 124);
        private static readonly Color Rule = Color.FromArgb(205, 210, 217);
        private static readonly Color Blue = Color.FromArgb(30, 100, 190);

        private Font F(float size, FontStyle style = FontStyle.Regular) { return new Font("Segoe UI", size * K, style); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            int x = S(14), w = Width - S(28);
            using (var pen = new Pen(Rule)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            if (_key == SummaryKey) { PaintSummary(g, x, w); return; }
            using (var f = F(8.5f)) TextRenderer.DrawText(g, T("PV_Title", "Preview"), f, new Rectangle(x, S(10), w, S(18)), Grey, TextFormatFlags.Left);
            using (var f = F(10.5f, FontStyle.Bold))
                TextRenderer.DrawText(g, PrefTexts.NameOf(_key), f, new Rectangle(x, S(30), w, S(24)), Ink, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            using (var f = F(9.5f))
                TextRenderer.DrawText(g, ValueLine(), f, new Rectangle(x, S(56), w, S(30)), Blue, TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);

            var a = Area;
            using (var back = new SolidBrush(Color.White)) g.FillRectangle(back, a);
            using (var pen = new Pen(Rule)) g.DrawRectangle(pen, a);
            var inner = Rectangle.Inflate(a, -S(10), -S(10));
            var state = g.Save();
            g.SetClip(a);
            try { Draw(g, inner); }
            finally { g.Restore(state); }

            using (var f = F(9f))
                TextRenderer.DrawText(g, Describe(), f, new Rectangle(x, a.Bottom + S(10), w, Math.Max(S(20), Height - a.Bottom - S(14))), Grey,
                                      TextFormatFlags.Left | TextFormatFlags.WordBreak);
        }

        /// <summary>Read from: the user's name, then each preference and its value (theirs in ink, "As for everyone" grey).</summary>
        private void PaintSummary(Graphics g, int x, int w)
        {
            using (var f = F(8.5f)) TextRenderer.DrawText(g, T("PV_SummaryTitle", "Their preferences"), f, new Rectangle(x, S(10), w, S(18)), Grey, TextFormatFlags.Left);
            using (var f = F(10.5f, FontStyle.Bold))
                TextRenderer.DrawText(g, _title ?? "", f, new Rectangle(x, S(30), w, S(24)), Ink, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            int y = S(60);
            using (var pen = new Pen(Rule)) g.DrawLine(pen, x, y, x + w, y);
            y += S(6);
            int nameW = w / 2;
            using (var f = F(8f))
            using (var own = F(8f, FontStyle.Bold))
                foreach (var line in _lines)
                {
                    TextRenderer.DrawText(g, line.Name ?? "", f, new Rectangle(x, y, nameW - S(4), S(18)), Grey,
                                          TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(g, line.Value ?? "", line.Own ? own : f, new Rectangle(x + nameW, y, w - nameW, S(18)), line.Own ? Ink : Grey,
                                          TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    y += S(18);
                }
            using (var f = F(8.5f))
                TextRenderer.DrawText(g, ValueLine(), f, new Rectangle(x, y + S(6), w, Math.Max(S(20), Height - y - S(8))), Blue,
                                      TextFormatFlags.Left | TextFormatFlags.WordBreak);
        }

        /// <summary>"Unit_Length" -> Length; anything else -> None.</summary>
        private static UnitType UnitOfKey(string key)
        {
            UnitType t;
            return key != null && key.StartsWith("Unit_") && Enum.TryParse(key.Substring(5), out t) ? t : UnitType.None;
        }

        // A value of each family as the PLC keeps it (the reference unit).
        private static double SampleOf(UnitType t)
        {
            switch (t)
            {
                case UnitType.Length: return 1234.5;
                case UnitType.Weight: return 2500;
                case UnitType.Temperature: return 65;
                case UnitType.Pressure: return 6.5;
                case UnitType.Time: return 90;
                default: return 100;
            }
        }

        /// <summary>A unit: a field with a value in it, and what the PLC keeps for it.</summary>
        private void DrawUnit(Graphics g, Rectangle r)
        {
            var type = UnitOfKey(_key);
            var unit = UnitManager.Find(type, _valueOf(_key));
            double reference = SampleOf(type);
            double shown = unit == null || unit.Factor == 0 ? reference : (reference - unit.Offset) / unit.Factor;
            int decimals = Math.Min(4, 1 + (unit != null ? unit.ExtraDecimals : 0));
            var nf = ViewPreferences.NumberFormat;
            string name = unit != null ? unit.Name : UnitManager.GetReferenceSuffix(type);
            string refText = reference.ToString("F1", nf) + " " + UnitManager.GetReferenceSuffix(type);
            using (var f = F(9f))
            using (var bf = F(12f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, PrefTexts.NameOf(_key) + ":", f, new Rectangle(r.Left, r.Top, r.Width, S(20)), Ink, TextFormatFlags.Left);
                var b = new Rectangle(r.Left, r.Top + S(24), S(150), S(34));
                Box(g, b, shown.ToString("F" + decimals, nf), bf);
                TextRenderer.DrawText(g, name, f, new Rectangle(b.Right + S(8), b.Top, r.Right - b.Right - S(8), b.Height), Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
                using (var pen = new Pen(Rule)) g.DrawLine(pen, r.Left, r.Top + S(74), r.Right, r.Top + S(74));
                TextRenderer.DrawText(g, T("PV_UnitPlc", "The PLC keeps") + ":  " + refText, f, new Rectangle(r.Left, r.Top + S(82), r.Width, S(20)), Grey, TextFormatFlags.Left);
            }
        }

        private void Draw(Graphics g, Rectangle r)
        {
            if (UnitOfKey(_key) != UnitType.None) { DrawUnit(g, r); return; }
            switch (_key)
            {
                case UsersPreferencesControl.ScaleKey: DrawScale(g, r); break;
                case ViewPreferences.CaptionAlign: DrawCaptions(g, r); break;
                case ViewPreferences.DecimalSeparator: DrawDecimal(g, r); break;
                case ViewPreferences.DateFormat:
                case ViewPreferences.TimeFormat: DrawDates(g, r); break;
                case ViewPreferences.TableRows:
                case ViewPreferences.TableFont: DrawTable(g, r); break;
                case ViewPreferences.StartPage: DrawStart(g, r); break;
                case ViewPreferences.AlarmSound: DrawSound(g, r); break;
                case ViewPreferences.Blinking: DrawBlink(g, r); break;
                case ViewPreferences.TooltipDelay: DrawTooltip(g, r); break;
            }
        }

        private void Box(Graphics g, Rectangle b, string text, Font f, bool right = true, Color? ink = null)
        {
            using (var back = new SolidBrush(Color.White)) g.FillRectangle(back, b);
            using (var pen = new Pen(Color.FromArgb(160, 166, 176))) g.DrawRectangle(pen, b);
            TextRenderer.DrawText(g, text, f, Rectangle.Inflate(b, -S(4), 0), ink ?? Ink,
                                  TextFormatFlags.VerticalCenter | (right ? TextFormatFlags.Right : TextFormatFlags.Left) | TextFormatFlags.EndEllipsis);
        }

        private void DrawCaptions(Graphics g, Rectangle r)
        {
            bool left = _valueOf(ViewPreferences.CaptionAlign) == "Left";
            string dec = ViewPreferences.NumberFormat.NumberDecimalSeparator;
            string[] caps = { T("PV_CapDiameter", "Saw diameter:"), T("PV_CapSpeed", "Speed:"), T("PV_CapFeed", "Feed rate:") };
            string[] vals = { "600" + dec + "00", "2400", "1" + dec + "50" };
            using (var f = F(9f))
            {
                int fieldW = S(80), fieldX = r.Right - fieldW, y = r.Top;
                int widest = 0;
                foreach (var c in caps) widest = Math.Max(widest, TextRenderer.MeasureText(c, f).Width);
                for (int i = 0; i < caps.Length; i++)
                {
                    var fb = new Rectangle(fieldX, y, fieldW, S(24));
                    var cb = left ? new Rectangle(fieldX - S(10) - widest, y, widest, S(24))
                                  : new Rectangle(r.Left, y, fieldX - S(6) - r.Left, S(24));
                    TextRenderer.DrawText(g, caps[i], f, cb, Ink, TextFormatFlags.VerticalCenter | (left ? TextFormatFlags.Left : TextFormatFlags.Right));
                    Box(g, fb, vals[i], f);
                    y += S(30);
                }
                // The axis row: the name in its cell, the position beside it.
                y += S(8);
                using (var pen = new Pen(Rule)) g.DrawLine(pen, r.Left, y - S(4), r.Right, y - S(4));
                var nameCell = new Rectangle(r.Left, y + S(4), S(90), S(26));
                using (var shade = new SolidBrush(Color.FromArgb(245, 246, 248))) g.FillRectangle(shade, nameCell);
                using (var nf = F(10f))
                    TextRenderer.DrawText(g, "Robot Y", nf, Rectangle.Inflate(nameCell, -S(4), 0), Ink,
                                          TextFormatFlags.VerticalCenter | (left ? TextFormatFlags.Left : TextFormatFlags.Right));
                using (var bf = F(11f, FontStyle.Bold))
                    Box(g, new Rectangle(nameCell.Right + S(8), y + S(2), S(110), S(30)), "1234" + dec + "56", bf);
            }
        }

        private void DrawDecimal(Graphics g, Rectangle r)
        {
            string v = _valueOf(ViewPreferences.DecimalSeparator);
            string dec = v == "Comma" ? "," : v == "Point" ? "." : CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            using (var bf = F(12f, FontStyle.Bold))
                Box(g, new Rectangle(r.Left, r.Top, S(150), S(32)), "1234" + dec + "56 mm", bf);
            // The keypad's last rows, the decimal key marked.
            string[,] keys = { { "7", "8", "9" }, { "4", "5", "6" }, { "1", "2", "3" }, { "0", dec, "+/-" } };
            int k = S(30);
            using (var f = F(10f))
                for (int row = 0; row < 4; row++)
                    for (int col = 0; col < 3; col++)
                    {
                        var b = new Rectangle(r.Left + col * (k + S(3)), r.Top + S(44) + row * (k + S(3)), k, k);
                        bool mark = row == 3 && col == 1;
                        using (var back = new SolidBrush(mark ? Color.FromArgb(218, 232, 250) : Color.White)) g.FillRectangle(back, b);
                        using (var pen = new Pen(mark ? Blue : Rule, mark ? 2f : 1f)) g.DrawRectangle(pen, b);
                        TextRenderer.DrawText(g, keys[row, col], f, b, Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
        }

        private void DrawDates(Graphics g, Rectangle r)
        {
            string dp = ViewPreferences.DatePatternOf(_valueOf(ViewPreferences.DateFormat));
            string tv = _valueOf(ViewPreferences.TimeFormat);
            var t1 = new DateTime(2026, 10, 2, 14, 31, 7);
            var t2 = new DateTime(2026, 10, 2, 9, 5, 12);
            Func<DateTime, string> dt = t => t.ToString(dp + " " + ViewPreferences.TimePatternOf(tv), CultureInfo.InvariantCulture);
            using (var f = F(8.5f))
            using (var hf = F(8.5f, FontStyle.Bold))
            {
                int timeW = Math.Max(S(120), TextRenderer.MeasureText(dt(t1), f).Width + S(8));
                int y = r.Top;
                var head = new Rectangle(r.Left, y, r.Width, S(22));
                using (var b = new SolidBrush(UiStyle.GridHeadBack)) g.FillRectangle(b, head);
                TextRenderer.DrawText(g, T("Alarm_Col_TriggerTime", "Time"), hf, new Rectangle(r.Left + S(4), y, timeW, S(22)), Ink, TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, T("Alarm_Col_Description", "Alarm"), hf, new Rectangle(r.Left + timeW + S(4), y, r.Width - timeW, S(22)), Ink, TextFormatFlags.VerticalCenter);
                y += S(22);
                foreach (var row in new[] { new[] { dt(t1), "1A18" }, new[] { dt(t2), "M840.2" } })
                {
                    TextRenderer.DrawText(g, row[0], f, new Rectangle(r.Left + S(4), y, timeW, S(24)), Ink, TextFormatFlags.VerticalCenter);
                    TextRenderer.DrawText(g, row[1], f, new Rectangle(r.Left + timeW + S(4), y, r.Width - timeW, S(24)), Color.FromArgb(200, 30, 30), TextFormatFlags.VerticalCenter);
                    y += S(24);
                    using (var pen = new Pen(UiStyle.GridRule)) g.DrawLine(pen, r.Left, y, r.Right, y);
                }
                y += S(12);
                string shift = DateTime.Today.AddHours(6).ToString(ViewPreferences.TimePatternOf(tv, false), CultureInfo.InvariantCulture) + " - " +
                               DateTime.Today.AddHours(14).ToString(ViewPreferences.TimePatternOf(tv, false), CultureInfo.InvariantCulture);
                TextRenderer.DrawText(g, T("PV_Shift", "Shift") + ":  " + shift, f, new Rectangle(r.Left, y, r.Width, S(20)), Ink, TextFormatFlags.Left);
                y += S(22);
                TextRenderer.DrawText(g, "[" + dt(t2) + "]  admin: " + T("PV_SignedIn", "signed in"), f, new Rectangle(r.Left, y, r.Width, S(20)), Grey,
                                      TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }
        }

        private void DrawTable(Graphics g, Rectangle r)
        {
            int h = S(ViewPreferences.RowHeightOf(_valueOf(ViewPreferences.TableRows)));
            float size = ViewPreferences.FontSizeOf(_valueOf(ViewPreferences.TableFont));
            using (var f = F(size))
            using (var hf = F(size, FontStyle.Bold))
            {
                int y = r.Top;
                int c1 = S(30), c2 = r.Width - S(90);
                var head = new Rectangle(r.Left, y, r.Width, h);
                using (var b = new SolidBrush(UiStyle.GridHeadBack)) g.FillRectangle(b, head);
                TextRenderer.DrawText(g, "#", hf, new Rectangle(r.Left + S(4), y, c1, h), Ink, TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, T("PV_State", "State"), hf, new Rectangle(r.Left + c1, y, c2 - c1, h), Ink, TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, T("PV_Minutes", "Minutes"), hf, new Rectangle(r.Left + c2, y, r.Width - c2 - S(4), h), Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
                y += h;
                string[] states = { T("PV_StRunning", "Running"), T("PV_StWaiting", "Waiting for material"), T("PV_StAlarm", "Alarm"), T("PV_StRunning", "Running"), T("PV_StStopped", "Stopped") };
                for (int i = 0; i < states.Length && y + h <= r.Bottom; i++)
                {
                    if (i == 1) using (var sel = new SolidBrush(UiStyle.GridSelection)) g.FillRectangle(sel, r.Left, y, r.Width, h);
                    TextRenderer.DrawText(g, (i + 1).ToString(CultureInfo.InvariantCulture), f, new Rectangle(r.Left + S(4), y, c1, h), Ink, TextFormatFlags.VerticalCenter);
                    TextRenderer.DrawText(g, states[i], f, new Rectangle(r.Left + c1, y, c2 - c1, h), Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(g, (12.5 * (i + 1)).ToString("0.0", ViewPreferences.NumberFormat), f, new Rectangle(r.Left + c2, y, r.Width - c2 - S(4), h), Ink,
                                          TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
                    y += h;
                    using (var pen = new Pen(UiStyle.GridRule)) g.DrawLine(pen, r.Left, y - 1, r.Right, y - 1);
                }
            }
        }

        private void Flow(Graphics g, Rectangle r, int y, string from, string to)
        {
            using (var f = F(9f))
            {
                var a = new Rectangle(r.Left, y, S(84), S(30));
                var b = new Rectangle(r.Left + S(118), y, r.Width - S(118), S(30));
                using (var back = new SolidBrush(Color.FromArgb(245, 246, 248))) { g.FillRectangle(back, a); g.FillRectangle(back, b); }
                using (var pen = new Pen(Rule)) { g.DrawRectangle(pen, a); g.DrawRectangle(pen, b); }
                TextRenderer.DrawText(g, from, f, a, Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(g, to, f, b, Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                using (var pen = new Pen(Blue, 2f) { EndCap = LineCap.ArrowAnchor })
                    g.DrawLine(pen, a.Right + S(6), y + S(15), b.Left - S(6), y + S(15));
            }
        }

        private void DrawStart(Graphics g, Rectangle r)
        {
            string v = _valueOf(ViewPreferences.StartPage);
            string signIn = T("PV_SignIn", "Sign in");
            string to = v == "Home" ? T("VP_Home", "Home")
                      : v == "Last" ? T("PV_LastExample", "Alarms (where I was)")
                      : T("PV_StayExample", "the page on show");
            Flow(g, r, r.Top + S(20), signIn, to);
            using (var f = F(8.5f))
                TextRenderer.DrawText(g, v == "Last" ? T("PV_LastNote", "Kept for each user as they move about; a page above their level is not opened.")
                                       : v == "Home" ? T("PV_HomeNote", "The first page of the menu.")
                                       : T("PV_StayNote", "Nothing moves: as the program has always been."),
                                      f, new Rectangle(r.Left, r.Top + S(66), r.Width, S(60)), Grey, TextFormatFlags.Left | TextFormatFlags.WordBreak);
        }

        private void DrawSound(Graphics g, Rectangle r)
        {
            bool on = _valueOf(ViewPreferences.AlarmSound) == "On";
            float s = S(40);
            float x = r.Left + S(20), y = r.Top + S(40);
            using (var b = new SolidBrush(on ? Ink : Grey))
                g.FillPolygon(b, new[]
                {
                    new PointF(x, y + s * 0.35f), new PointF(x + s * 0.3f, y + s * 0.35f), new PointF(x + s * 0.65f, y),
                    new PointF(x + s * 0.65f, y + s), new PointF(x + s * 0.3f, y + s * 0.65f), new PointF(x, y + s * 0.65f)
                });
            using (var pen = new Pen(on ? Blue : Color.FromArgb(200, 30, 30), S(3)))
            {
                if (on)
                {
                    g.DrawArc(pen, x + s * 0.45f, y + s * 0.15f, s * 0.55f, s * 0.7f, -45, 90);
                    g.DrawArc(pen, x + s * 0.40f, y - s * 0.05f, s * 0.9f, s * 1.1f, -45, 90);
                }
                else
                {
                    g.DrawLine(pen, x + s * 0.85f, y + s * 0.3f, x + s * 1.25f, y + s * 0.7f);
                    g.DrawLine(pen, x + s * 1.25f, y + s * 0.3f, x + s * 0.85f, y + s * 0.7f);
                }
            }
            using (var f = F(8.5f))
                TextRenderer.DrawText(g, on ? T("PV_SoundHeard", "Heard once as the pointer comes onto On.") : "", f,
                                      new Rectangle(r.Left, r.Top + S(100), r.Width, S(44)), Grey, TextFormatFlags.Left | TextFormatFlags.WordBreak);
        }

        private void DrawBlink(Graphics g, Rectangle r)
        {
            bool shown = _phase || _valueOf(ViewPreferences.Blinking) == "Off";
            var red = Color.FromArgb(232, 60, 60);
            // The top bar: the alarm mark and the connection mark.
            var bar = new Rectangle(r.Left, r.Top, r.Width, S(40));
            using (var b = new SolidBrush(Color.Black)) g.FillRectangle(b, bar);
            float cx = bar.Left + S(30), cy = bar.Top + S(20), side = S(26);
            using (var pen = new Pen(shown ? red : Color.White, S(2)) { LineJoin = LineJoin.Round })
            {
                g.DrawPolygon(pen, new[] { new PointF(cx, cy - side * 0.45f), new PointF(cx + side / 2, cy + side * 0.4f), new PointF(cx - side / 2, cy + side * 0.4f) });
                g.DrawLine(pen, cx, cy - side * 0.15f, cx, cy + side * 0.1f);
                g.DrawEllipse(pen, cx - 1, cy + side * 0.22f, 2, 2);
            }
            using (var pen = new Pen(shown ? red : Color.White, S(3)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float lx = bar.Left + S(80), ly = cy;
                g.DrawArc(pen, lx - S(10), ly - S(6), S(14), S(12), 40, 280);
                g.DrawArc(pen, lx + S(2), ly - S(6), S(14), S(12), 220, 280);
            }
            using (var f = F(8.5f))
                TextRenderer.DrawText(g, T("PV_TopBar", "the top bar"), f, new Rectangle(bar.Left + S(110), bar.Top, bar.Width - S(110), bar.Height), Color.White, TextFormatFlags.VerticalCenter);
            // A code cell and a lamp.
            using (var bf = F(11f, FontStyle.Bold))
                Box(g, new Rectangle(r.Left, r.Top + S(56), S(80), S(30)), shown ? "1A18" : "", bf, false, Color.FromArgb(200, 30, 30));
            var lamp = new Rectangle(r.Left + S(100), r.Top + S(59), S(24), S(24));
            using (var b = new SolidBrush(shown ? Color.FromArgb(255, 170, 0) : Color.FromArgb(225, 225, 225))) g.FillEllipse(b, lamp);
            using (var pen = new Pen(Color.FromArgb(25, 28, 34), S(2))) g.DrawEllipse(pen, lamp);
        }

        private void DrawTooltip(Graphics g, Rectangle r)
        {
            int d = ViewPreferences.DelayOf(_valueOf(ViewPreferences.TooltipDelay));
            using (var f = F(9f))
            {
                Box(g, new Rectangle(r.Left, r.Top + S(10), S(100), S(26)), "2400", f);
                // the pointer resting, the time, the tip
                using (var b = new SolidBrush(Ink))
                {
                    float px = r.Left + S(70), py = r.Top + S(26);
                    g.FillPolygon(b, new[] { new PointF(px, py), new PointF(px, py + S(16)), new PointF(px + S(4), py + S(12)), new PointF(px + S(10), py + S(12)) });
                }
                // played out again and again: the pointer rests for the delay (the bar fills), the tip shows for a while
                double t = (DateTime.Now - _tipStart).TotalMilliseconds % (d + 1800);
                bool shown = t >= d;
                var tip = new Rectangle(r.Left + S(86), r.Top + S(44), Math.Min(r.Width - S(86), S(150)), S(26));
                if (shown)
                {
                    using (var b = new SolidBrush(Color.FromArgb(255, 255, 225))) g.FillRectangle(b, tip);
                    using (var pen = new Pen(Grey)) g.DrawRectangle(pen, tip);
                    TextRenderer.DrawText(g, T("PV_TipText", "Speed of the saw"), f, tip, Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
                }
                var run = new Rectangle(r.Left + S(10), r.Top + S(72), S(66), S(5));
                using (var b = new SolidBrush(Rule)) g.FillRectangle(b, run);
                using (var b = new SolidBrush(Blue)) g.FillRectangle(b, run.Left, run.Top, (int)(run.Width * Math.Min(1.0, t / Math.Max(1, d))), run.Height);
                using (var bf = F(9f, FontStyle.Bold))
                    TextRenderer.DrawText(g, Seconds(d), bf, new Rectangle(r.Left + S(10), r.Top + S(48), S(70), S(20)), Blue, TextFormatFlags.Left);
            }
        }

        private void DrawScale(Graphics g, Rectangle r)
        {
            double f = _scale ?? _panelScale;
            int y = r.Top;
            foreach (var pair in new[] { new Tuple<double, string>(1.0, "100%"), new Tuple<double, string>(f, DisplaySettings.Percent(f)) })
            {
                float k = (float)pair.Item1;
                using (var small = F(8f)) TextRenderer.DrawText(g, pair.Item2, small, new Rectangle(r.Left, y, S(44), S(20)), Grey, TextFormatFlags.Left);
                using (var font = new Font("Segoe UI", 9.5f * K * k))
                {
                    int h = (int)(S(24) * k);
                    var cap = new Rectangle(r.Left + S(44), y, (int)(S(56) * k), h);
                    TextRenderer.DrawText(g, T("PV_CapSpeed", "Speed:"), font, cap, Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
                    Box(g, new Rectangle(cap.Right + S(4), y, (int)(S(70) * k), h), "2400", font);
                    var btn = new Rectangle(cap.Right + S(8) + (int)(S(70) * k), y, (int)(S(28) * k), h);
                    using (var b = new SolidBrush(Color.FromArgb(230, 232, 236))) g.FillRectangle(b, btn);
                    using (var pen = new Pen(Rule)) g.DrawRectangle(pen, btn);
                    TextRenderer.DrawText(g, "\u25B6", font, btn, Ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    y += h + S(16);
                }
            }
        }
    }
}
