using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// The on-screen keypad, for a panel with no keyboard.
    ///
    /// Modelled on the old program's: the limits and the live entry across the
    /// top in a dark readout, then big keys. On a machine the operator is often
    /// wearing gloves and looking at the profile rather than the screen, so the
    /// keys are large, square and in the phone layout everybody already knows.
    ///
    /// THE LIMITS ARE SHOWN, NOT JUST ENFORCED. "0 &lt;= value &lt;= 100" across the
    /// top means a rejected entry is never a surprise - the operator can see the
    /// range before typing rather than after being refused.
    ///
    /// HEX MODE adds A-F for the drive alarm codes, which are quoted in
    /// hexadecimal in every manual. Without it those codes have to be converted
    /// by hand before they can be typed, which is exactly the sort of arithmetic
    /// nobody should be doing next to a stopped machine.
    /// </summary>
    public class NumpadForm : Form
    {
        private readonly Label _range = new Label();
        private readonly Panel _entry = new Panel();
        private readonly Timer _caretBlink = new Timer { Interval = 500 };
        private bool _caretOn = true;
        private string _text = "";

        /// <summary>
        /// Where the next digit lands. Starts as a full selection: the first key
        /// press replaces the old value, which is what an operator retyping a
        /// setting expects, while an arrow key steps into it to fix one digit.
        /// </summary>
        private int _caret;
        private bool _selectAll = true;

        public string Value { get { return _text; } }

        /// <summary>
        /// Opens the keypad against the control being edited.
        ///
        /// Below and to the right if it fits, above if not. Centre-of-screen put
        /// the keypad over the very field the operator was changing - so they could
        /// not see the value they were typing into, which on a machine parameter is
        /// exactly when they want to.
        /// </summary>
        public void PlaceBeside(Control owner)
        {
            if (owner == null) return;
            var origin = owner.PointToScreen(Point.Empty);
            var screen = Screen.FromControl(owner).WorkingArea;

            int x = origin.X + owner.Width - Width;     // right edges aligned
            int y = origin.Y + owner.Height + 4;        // below by default

            if (y + Height > screen.Bottom) y = origin.Y - Height - 4;   // no room: above
            if (y < screen.Top) y = screen.Top + 4;

            x = Math.Max(screen.Left + 4, Math.Min(x, screen.Right - Width - 4));
            Location = new Point(x, y);
        }

        public double? Minimum { get; set; }
        public double? Maximum { get; set; }
        public bool AllowNegative { get; set; }
        public bool AllowDecimal { get; set; }

        /// <summary>Hexadecimal entry: adds A-F, drops the decimal point and sign.</summary>
        public bool HexMode { get; set; }

        /// <summary>
        /// How many digits the field can display. Entry stops there.
        ///
        /// A DIGIT CAP, not a value limit. A five-digit field cannot show 123456
        /// whatever the units are, so refusing the sixth keystroke is always right
        /// - unlike a "maximum 99999", which stops meaning anything the moment the
        /// display switches from millimetres to inches.
        /// </summary>
        public int MaxDigits { get; set; }

        /// <summary>
        /// Decimal places the field shows; the entry may not exceed them.
        ///
        /// Setting this REDRAWS the range line. It is assigned by the caller after
        /// construction, and the line was built once in the constructor - so the
        /// limits were formatted with zero decimals and never revisited, which is
        /// why they appeared as "-24" and "323" beside a value showing four places.
        /// </summary>
        public int DecimalPlaces
        {
            get { return _decimalPlaces; }
            set { if (_decimalPlaces == value) return; _decimalPlaces = value; Refresh_(); }
        }
        private int _decimalPlaces;



        private static readonly Color Ink = Color.FromArgb(28, 30, 34);
        private static readonly Color Readout = Color.FromArgb(22, 24, 28);

        public NumpadForm(string caption, string initial,
                          double? min = null, double? max = null,
                          bool allowNegative = true, bool allowDecimal = true,
                          bool hex = false)
        {
            Minimum = min; Maximum = max;
            AllowNegative = allowNegative; AllowDecimal = allowDecimal; HexMode = hex;
            _text = hex ? (initial ?? "").Trim() : ToKeypadForm(initial);
            _caret = _text.Length;

            Text = caption ?? LanguageManager.GetString("Sys_InputTitle", "Input");
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;   // placed against its own control
            BackColor = Color.FromArgb(232, 234, 238);
            ShowInTaskbar = false;
            KeyPreview = true;

            // LAYOUT
            //
            // Readout on top with the caret arrows built into it - they move
            // through the DIGITS, so they belong with the number, not among the
            // keys. Putting them in the key grid made a fifth row that pushed the
            // whole pad out of shape and sat them nowhere near what they move.
            //
            // Then the phone grid: 7-8-9 on top, ENT tall down the right, 0 . +/-
            // along the bottom. Hex adds ONE column of A-F on the right of the
            // digits, so the digit block stays exactly where the hand expects it.
            const int key = 74, gap = 5, pad = 8;
            int cols = hex ? 5 : 4;
            int w = pad * 2 + cols * key + (cols - 1) * gap;

            var head = new Panel
            {
                BackColor = Readout,
                Location = new Point(pad, pad),
                Size = new Size(w - pad * 2, 82)
            };
            Controls.Add(head);

            var close = new Label
            {
                Text = "\u2715",
                ForeColor = Color.FromArgb(200, 206, 214),
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(30, 24),
                Location = new Point(head.Width - 32, 3),
                Cursor = Cursors.Hand
            };
            close.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            head.Controls.Add(close);

            _range.SetBounds(10, 3, head.Width - 46, 28);
            _range.ForeColor = Color.FromArgb(170, 178, 190);
            // Bigger: the limits are the thing that stops a rejected entry being a
            // surprise, and at 11pt they were the smallest text on the pad.
            _range.Font = new Font("Consolas", 14F, FontStyle.Bold);
            _range.TextAlign = ContentAlignment.MiddleLeft;
            head.Controls.Add(_range);

            // caret arrows, inside the readout beside the number
            var caretL = CaretKey("<", "\u25C0", 10, 36);
            var caretR = CaretKey(">", "\u25B6", 52, 36);
            head.Controls.Add(caretL);
            head.Controls.Add(caretR);

            _entry.SetBounds(96, 34, head.Width - 108, 44);
            _entry.BackColor = Readout;
            _entry.Font = new Font("Consolas", 22F, FontStyle.Bold);
            _entry.Paint += (s, e) => PaintEntry(e.Graphics);
            head.Controls.Add(_entry);

            // A BLINKING caret, as in any text field. A static bar is easily read as
            // part of the number - a "1" in a Consolas string of digits.
            _caretBlink.Tick += (s, e) => { _caretOn = !_caretOn; _entry.Invalidate(); };
            _caretBlink.Start();

            int top = pad + 90;
            string[][] rows = hex
                ? new[]
                  {
                      new[] { "7", "8", "9", "A", "DEL" },
                      new[] { "4", "5", "6", "B", "AC"  },
                      new[] { "1", "2", "3", "C", "ENT" },
                      new[] { "0", "D", "E", "F", ""    }
                  }
                : new[]
                  {
                      new[] { "7", "8", "9", "DEL" },
                      new[] { "4", "5", "6", "AC"  },
                      new[] { "1", "2", "3", "ENT" },
                      new[] { "0", ".", "+/-", ""  }
                  };

            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < rows[r].Length; c++)
                {
                    var label = rows[r][c];
                    if (label.Length == 0) continue;
                    if (label == "." && !AllowDecimal) continue;
                    if (label == "+/-" && !AllowNegative) continue;

                    bool tall = label == "ENT";
                    Controls.Add(MakeKey(label,
                        pad + c * (key + gap),
                        top + r * (key + gap),
                        key, tall ? key * 2 + gap : key));
                }

            ClientSize = new Size(w, top + rows.Length * (key + gap) - gap + pad);
            Refresh_();
        }

        /// <summary>A small arrow inside the readout, for stepping through digits.</summary>
        /// <param name="face">What is drawn - "&lt;" and "&gt;" read cleaner at this
        /// size than solid triangles, which go blocky.</param>
        /// <param name="action">The key this stands for.</param>
        private Button CaretKey(string face, string action, int x, int y)
        {
            var b = new Button
            {
                Text = face,
                Location = new Point(x, y),
                Size = new Size(38, 40),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 56, 64),
                ForeColor = Color.FromArgb(226, 230, 236),
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                TabStop = false
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(80, 86, 96);
            b.Click += (s, e) => Press(action);
            return b;
        }

        private Button MakeKey(string label, int x, int y, int w, int h)
        {
            bool action = label == "DEL" || label == "AC" || label == "ENT";
            var b = new Button
            {
                Text = label,
                Location = new Point(x, y),
                Size = new Size(w, h),
                FlatStyle = FlatStyle.Flat,
                BackColor = action ? Color.FromArgb(222, 226, 232) : Color.White,
                ForeColor = Ink,
                // A SINGLE CHARACTER is a value key and is drawn large; anything
                // longer is a word key - DEL, AC, ENT - and they must match each
                // other. Keying off "longer than two" made AC large and DEL small.
                Font = new Font("Segoe UI", label.Length == 1 ? 20F : 12F,
                                label.Length == 1 ? FontStyle.Regular : FontStyle.Bold),
                TabStop = false
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(196, 201, 209);
            b.Click += (s, e) => Press(label);
            return b;
        }

        private void Press(string k)
        {
            switch (k)
            {
                case "AC":  _text = ""; _caret = 0; _selectAll = false; break;
                case "DEL":
                    if (_selectAll) { _text = ""; _caret = 0; _selectAll = false; }
                    else if (_caret > 0) { _text = _text.Remove(_caret - 1, 1); _caret--; }
                    break;
                case "ENT": Accept(); return;
                case "\u25C0": if (_caret > 0) _caret--; _selectAll = false; break;
                case "\u25B6": if (_caret < _text.Length) _caret++; _selectAll = false; break;
                case "+/-":
                    if (_text.StartsWith("-")) { _text = _text.Substring(1); _caret = Math.Max(0, _caret - 1); }
                    else { _text = "-" + _text; _caret++; }
                    _selectAll = false;
                    break;
                case ".":
                    // Inserted AT the caret and stepped over, like any other key.
                    // Appending it left the caret to the left of the separator, so
                    // the next digit went in front of the point instead of after it.
                    if (_text.Contains(".")) break;
                    if (_selectAll) { _text = ""; _caret = 0; _selectAll = false; }
                    if (_text.Length == 0) { _text = "0."; _caret = 2; }
                    else { _text = _text.Insert(Math.Min(_caret, _text.Length), "."); _caret++; }
                    break;
                default:
                    // A full selection is replaced by the first key, as in any
                    // other entry field.
                    if (_selectAll) { _text = ""; _caret = 0; _selectAll = false; }
                    if (!RoomFor(k)) { Flash(); return; }
                    _text = _text.Insert(Math.Min(_caret, _text.Length), k);
                    _caret += k.Length;
                    break;
            }
            Refresh_();
        }

        /// <summary>
        /// Accepts only a value that parses AND sits inside the limits. A keypad
        /// that lets a bad number through and relies on the caller to refuse it
        /// makes the operator retype from scratch.
        /// </summary>
        /// <summary>
        /// Whether another character fits: the whole number within MaxDigits, and
        /// the fraction within DecimalPlaces.
        /// </summary>
        private bool RoomFor(string key)
        {
            if (MaxDigits <= 0) return true;

            int digits = 0;
            foreach (char c in _text) if (char.IsLetterOrDigit(c)) digits++;
            if (digits >= MaxDigits) return false;

            int dot = _text.IndexOf('.');
            if (dot >= 0 && DecimalPlaces > 0 && _caret > dot)
            {
                int frac = _text.Length - dot - 1;
                if (frac >= DecimalPlaces) return false;
            }
            return true;
        }

        /// <summary>
        /// A starting value in the keypad's own form: "." as the decimal point.
        /// Callers pass invariant text (NumericInputHMI.KeypadText); a decimal
        /// comma from a regional format is still turned into the point the "."
        /// key and Accept() work with, rather than left as a character the pad
        /// can neither edit nor parse.
        /// </summary>
        internal static string ToKeypadForm(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            return text.Trim().Replace(",", ".");
        }

        private void Accept()
        {
            if (_text.Length == 0) { DialogResult = DialogResult.Cancel; Close(); return; }

            double v;
            if (HexMode)
            {
                int iv;
                if (!int.TryParse(_text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out iv))
                { Flash(); return; }
                v = iv;
            }
            // INVARIANT: the keypad only ever types "." as the decimal point. With
            // the panel's regional settings, a Turkish, German, Russian, Italian or
            // Spanish Windows (decimal ",") refused every decimal entry.
            else if (!double.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
            { Flash(); return; }

            if ((Minimum.HasValue && v < Minimum.Value) ||
                (Maximum.HasValue && v > Maximum.Value)) { Flash(); return; }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void Flash()
        {
            var was = _entry.ForeColor;
            _entry.ForeColor = Color.FromArgb(255, 110, 110);
            var t = new Timer { Interval = 260 };
            t.Tick += (s, e) => { _entry.ForeColor = was; t.Stop(); t.Dispose(); };
            t.Start();
        }

        /// <summary>
        /// Draws the number, the selection and the caret.
        ///
        /// SELECTION IS A HIGHLIGHT BLOCK, the way any editor shows it - the
        /// earlier "[12.50]" put two more characters in a numeric readout and read
        /// as part of the value. A block behind the digits says "type and this all
        /// goes" without adding anything to the number.
        /// </summary>
        /// <summary>
        /// Shrinks the limit line until it fits its box.
        ///
        /// A range can always be made longer by a machine with more travel or more
        /// decimals, so no fixed size is safe - and a clipped limit is worse than a
        /// small one, because the operator cannot tell whether they are reading the
        /// whole number. Floors at 9pt: below that it stops being readable at arm's
        /// length and the caption would be lying about being legible.
        /// </summary>
        private void FitRangeText()
        {
            if (_range == null || _range.Width <= 0) return;

            const float maxPt = 14f, minPt = 9f;
            for (float pt = maxPt; pt >= minPt; pt -= 0.5f)
            {
                using (var f = new Font("Consolas", pt, FontStyle.Bold))
                {
                    var w = TextRenderer.MeasureText(_range.Text, f).Width;
                    if (w <= _range.Width - 4 || pt <= minPt)
                    {
                        if (_range.Font == null || Math.Abs(_range.Font.Size - pt) > 0.01f)
                            _range.Font = new Font("Consolas", pt, FontStyle.Bold);
                        return;
                    }
                }
            }
        }

        private void PaintEntry(Graphics g)
        {
            g.Clear(Readout);
            string shown = _text.Length == 0 ? "0" : _text;

            var size = TextRenderer.MeasureText(g, shown, _entry.Font, Size.Empty,
                                                TextFormatFlags.NoPadding);
            int right = _entry.Width - 6;
            int x = right - size.Width;
            int y = (_entry.Height - size.Height) / 2;

            if (_selectAll && _text.Length > 0)
                using (var sel = new SolidBrush(Color.FromArgb(52, 96, 148)))
                    g.FillRectangle(sel, x - 3, y - 2, size.Width + 6, size.Height + 4);

            TextRenderer.DrawText(g, shown, _entry.Font, new Point(x, y),
                                  Color.White, TextFormatFlags.NoPadding);

            if (_selectAll || !_caretOn) return;

            int upto = Math.Min(_caret, shown.Length);
            var lead = TextRenderer.MeasureText(g, shown.Substring(0, upto), _entry.Font,
                                                Size.Empty, TextFormatFlags.NoPadding);
            int cx = x + lead.Width;
            using (var pen = new Pen(Color.FromArgb(120, 200, 255), 2f))
                g.DrawLine(pen, cx, y + 2, cx, y + size.Height - 2);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            _caretBlink.Stop();
            _caretBlink.Dispose();
        }

        private void Refresh_()
        {
            _caretOn = true;                 // a fresh keypress should show the caret
            _entry.Invalidate();

            // Shown with the FIELD'S decimals, not a fixed two. A limit printed to
            // two places beside a value shown to three is two different numbers in
            // the same box.
            string fmt = "F" + Math.Max(0, DecimalPlaces);

            // THE SIGNS STAY; the WORD goes.
            //
            // "-25.0000 <= value <= 333.0000" does not fit once the numbers carry
            // four decimals, and the word is the part carrying no information - the
            // operator knows what is being bounded, they are typing it. The
            // comparison signs are not decoration though: they say the ends are
            // INCLUSIVE, which a bare range leaves the operator guessing at.
            //
            // So an ellipsis stands in for the value: "-25.0000 <= ... <= 333.0000".
            // It reads the same in every language, needs no translation, and costs
            // three characters instead of the five-to-nine a translated "value"
            // would take.
            if (Minimum.HasValue && Maximum.HasValue)
                _range.Text = Minimum.Value.ToString(fmt) + " \u2264 \u2026 \u2264 " +
                              Maximum.Value.ToString(fmt);
            // THE PLACEHOLDER STAYS on a one-sided limit too. "<= 1700.00" alone
            // reads as though the limit itself were being shown rather than the
            // bound on what is being typed; keeping the ellipsis in the same
            // position as the two-sided form means both say the same thing the
            // same way.
            else if (Minimum.HasValue)
                _range.Text = Minimum.Value.ToString(fmt) + " \u2264 \u2026";
            else if (Maximum.HasValue)
                _range.Text = "\u2026 \u2264 " + Maximum.Value.ToString(fmt);
            else
                _range.Text = HexMode ? "HEX" : "";

            FitRangeText();
        }

        /// <summary>
        /// The PC keyboard's Enter, Escape and arrows.
        ///
        /// Enter arrives as a KEY DOWN, not a KeyPress, on a form with a default
        /// button - which is why pressing it did nothing while the pad's own ENT
        /// worked. The arrows never raise KeyPress at all.
        /// </summary>
        /// <summary>
        /// Enter, Escape and the arrows arrive here and NOWHERE ELSE.
        ///
        /// WinForms routes them through ProcessCmdKey before any KeyDown or
        /// KeyPress is raised - that is why OnKeyDown never saw Enter and the PC
        /// keyboard did nothing while the pad's own ENT worked.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Enter: Press("ENT"); return true;
                case Keys.Escape: DialogResult = DialogResult.Cancel; Close(); return true;
                case Keys.Left: Press("\u25C0"); return true;
                case Keys.Right: Press("\u25B6"); return true;
                case Keys.Back: Press("DEL"); return true;
                case Keys.Delete: Press("AC"); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Enter: Press("ENT"); e.Handled = e.SuppressKeyPress = true; break;
                case Keys.Escape: DialogResult = DialogResult.Cancel; Close(); break;
                case Keys.Left: Press("\u25C0"); e.Handled = e.SuppressKeyPress = true; break;
                case Keys.Right: Press("\u25B6"); e.Handled = e.SuppressKeyPress = true; break;
                case Keys.Delete: Press("AC"); e.Handled = e.SuppressKeyPress = true; break;
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            // A physical keyboard still works - the panel is the common case, not
            // the only one.
            base.OnKeyPress(e);
            char c = char.ToUpperInvariant(e.KeyChar);
            if (char.IsDigit(c)) { Press(c.ToString()); e.Handled = true; }
            else if (HexMode && c >= 'A' && c <= 'F') { Press(c.ToString()); e.Handled = true; }
            else if (c == '.' || c == ',') { Press("."); e.Handled = true; }
            else if (c == '-') { Press("+/-"); e.Handled = true; }
            else if (e.KeyChar == (char)Keys.Back) { Press("DEL"); e.Handled = true; }
            else if (e.KeyChar == (char)Keys.Enter || e.KeyChar == (char)Keys.Return)
            { Press("ENT"); e.Handled = true; }
            else if (e.KeyChar == (char)Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(120, 126, 134), 2))
                e.Graphics.DrawRectangle(pen, 1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
        }
    }
}

