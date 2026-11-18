using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Kaban.Core.Barcodes;
using Kaban.Core.Helpers;
using Kaban.Core.Labels;
using Kaban.UI.Controls;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// THE BARCODES AND LABELS IN THE PROGRAM'S WINDOW (the old machines' main forms: InitBarcodeReader, the printer's thread):
    /// the serial readers opened, a keyboard reader's codes caught, the label printing's pictures, the PLC's label requests
    /// watched, and a short note (Toast) of what was done with a code or a label that could not be printed.
    /// </summary>
    public static class BarcodeHost
    {
        private static Form _form;
        private static KeyboardReader _keys;

        public static void Attach(Form form)
        {
            if (form == null || _form != null) return;
            _form = form;
            LabelWriter.LoadPicture = Raster;
            try { BarcodeReaders.Start(); } catch (Exception ex) { AuditLogger.Log("[BARCODE] the readers: " + ex.Message); }
            _keys = new KeyboardReader();
            Application.AddMessageFilter(_keys);
            LabelTriggers.Start();
            Action<BarcodeTarget, BarcodeScan, string, bool> done = (t, scan, text, warning) => Note(text, warning);
            Action<BarcodeScan> scanned = scan =>
            {
                if (scan.TakenBy.Length == 0)
                    Note(string.Format(LanguageManager.GetString("Bc_ReadNotTaken", "Barcode read: {0} (no page of the machine uses it)."), scan.Code), false);
            };
            Action<LabelPrint> printed = r => { if (!r.Ok) Note(string.Format(LanguageManager.GetString("Lbl_NotPrinted", "Label {0} not printed: {1}"), r.Template, r.Message), true); };
            BarcodeTargets.Done += done;
            BarcodeReaders.Scanned += scanned;
            LabelService.Done += printed;
            form.FormClosed += (s, e) =>
            {
                BarcodeTargets.Done -= done;
                BarcodeReaders.Scanned -= scanned;
                LabelService.Done -= printed;
                Application.RemoveMessageFilter(_keys);
                LabelTriggers.Stop();
                BarcodeReaders.Stop();
                _form = null;
            };
        }

        /// <summary>A short note over the window (from any thread).</summary>
        private static void Note(string text, bool warning)
        {
            var f = _form;
            if (f == null || f.IsDisposed) return;
            Action show = () => { try { Toast.Show(f, text, warning); } catch { } };
            try { if (f.InvokeRequired) f.BeginInvoke(show); else show(); } catch { }
        }

        /// <summary>A picture's file in dots, fitted into the field (its sides kept), dark dots black - for the label printer.</summary>
        public static LabelRaster Raster(string path, int widthDots, int heightDots)
        {
            using (var src = Image.FromFile(path))
            {
                double k = Math.Min((double)widthDots / src.Width, (double)heightDots / src.Height);
                int w = Math.Max(1, (int)(src.Width * k)), h = Math.Max(1, (int)(src.Height * k));
                using (var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.Clear(Color.White);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(src, 0, 0, w, h);
                    }
                    int wb = (w + 7) / 8;
                    var bits = new byte[wb * h];
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            var c = bmp.GetPixel(x, y);
                            if (c.R * 299 + c.G * 587 + c.B * 114 < 128000) bits[y * wb + x / 8] |= (byte)(0x80 >> (x % 8));
                        }
                    return new LabelRaster { WidthBytes = wb, Height = h, Bits = bits };
                }
            }
        }

        /// <summary>
        /// A SCANNER THAT TYPES (a USB reader set as a keyboard): its characters come much faster than a person types (under 40 ms
        /// apart) and end with Enter (or Tab). While a keyboard reader is on (HMI Settings > Barcodes) and the focus is NOT in a
        /// box to type into, the characters are kept here and the code is read at its Enter - the Enter not given to the page (it
        /// would press the button that has the focus). In a box to type into the code is typed into the box, as before.
        /// </summary>
        internal sealed class KeyboardReader : IMessageFilter
        {
            private const int WM_KEYDOWN = 0x100, WM_CHAR = 0x102;
            private const int GapMs = 40, IdleMs = 120, Least = 4;
            private readonly List<char> _chars = new List<char>();
            private DateTime _first, _last;

            [DllImport("user32.dll")]
            private static extern IntPtr GetFocus();

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != WM_KEYDOWN && m.Msg != WM_CHAR) return false;
                if (!BarcodeReaders.KeyboardReading) return false;
                if (TypingBox()) { _chars.Clear(); return false; }
                var now = DateTime.UtcNow;
                if (_chars.Count > 0 && (now - _last).TotalMilliseconds > IdleMs) _chars.Clear();
                if (m.Msg == WM_KEYDOWN)
                {
                    var key = (Keys)(int)m.WParam & Keys.KeyCode;
                    if (key != Keys.Return && key != Keys.Tab && key != Keys.Space) return false;
                    if (!Burst(now)) return false;
                    if (key == Keys.Space) { Add(' ', now); return true; }
                    var code = new string(_chars.ToArray());
                    _chars.Clear();
                    var reader = BarcodeReaders.Readers.FirstOrDefault(r => r.Enabled && r.Kind == BarcodeReaderKind.Keyboard);
                    BarcodeReaders.Feed(code, reader != null ? reader.Name : "Keyboard", reader != null ? reader.Machine : "");
                    return true;
                }
                char ch = (char)(int)m.WParam;
                if (char.IsControl(ch)) return false;
                Add(ch, now);
                return true;
            }

            private void Add(char ch, DateTime now)
            {
                if (_chars.Count == 0) _first = now;
                _chars.Add(ch);
                _last = now;
            }

            /// <summary>The characters kept came as a scanner types them.</summary>
            private bool Burst(DateTime now)
            {
                if (_chars.Count < Least) return false;
                double avg = (_last - _first).TotalMilliseconds / Math.Max(1, _chars.Count - 1);
                return avg <= GapMs && (now - _last).TotalMilliseconds <= IdleMs;
            }

            /// <summary>The focus is in a box to type into (a text box, an editable list, a value).</summary>
            private static bool TypingBox()
            {
                Control c = null;
                try { c = Control.FromChildHandle(GetFocus()); } catch { }
                for (; c != null; c = c.Parent)
                {
                    if (c is TextBoxBase || c is UpDownBase) return true;
                    var cb = c as ComboBox;
                    if (cb != null) return cb.DropDownStyle != ComboBoxStyle.DropDownList;
                    if (c is DataGridView && ((DataGridView)c).IsCurrentCellInEditMode) return true;
                }
                return false;
            }
        }
    }
}
