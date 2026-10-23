using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// ONE VALUE OF A SIMPLE MOTION MODULE for one axis, read straight from the
    /// module's buffer memory (Un\G...) - a column of the Axis Settings page
    /// (SimpleMotion): a number in its unit, the operation status in words, or a
    /// flag as a lamp.
    ///
    /// A PARAMETER IS AN ADMIN'S FIELD (22 October: "Simple motion write device should be changed
    /// to numerical input instead of display so the admin user could change them"): white, a click
    /// opens the keypad in the cell's unit (the unit setting a list of the four units), the value
    /// is checked against the parameter's setting range and written into the module's buffer
    /// memory - and the module counts as changed (MotionFlash): the page's blinking button writes
    /// its flash ROM, without which it forgets the value at power off. For anyone else a display.
    /// </summary>
    public class MotionValueHMI : Control, IPlcExtraAddresses, UiStyle.IDisplayOnly
    {
        private readonly SimpleMotion.Item _item;
        private readonly SimpleMotion.RowUnit _rowUnit;
        private int _low, _high, _unitSetting;        // the last words read (shown again in another unit)
        private readonly int _module, _axis, _decimals;
        private readonly string _first, _unit;
        private string _text = "";
        private bool _on, _known;
        private readonly ToolTip _tips = UiStyle.Tips();

        /// <summary>The value box's background: a light grey (a display, not a field).</summary>
        public static readonly Color DisplayBack = Color.FromArgb(234, 236, 239);

        public MotionValueHMI(int plcNumber, SimpleMotion.Item item, int module, int axis, int decimals)
            : this(plcNumber, item, module, axis, decimals, SimpleMotion.RowUnit.Module) { }

        /// <param name="rowUnit">
        /// How the row shows its position: a length row's positions and speeds in the
        /// HMI's length unit (they follow Users and Preferences), an angle row's in degrees.
        /// </param>
        public MotionValueHMI(int plcNumber, SimpleMotion.Item item, int module, int axis, int decimals, SimpleMotion.RowUnit rowUnit)
        {
            PlcNumber = plcNumber;
            _item = item;
            _rowUnit = rowUnit;
            _module = module;
            _axis = axis;
            _decimals = Math.Max(0, decimals);
            _first = SimpleMotion.Device(module, SimpleMotion.AddressOf(item, axis));
            _unit = SimpleMotion.NeedsUnit(item) ? SimpleMotion.UnitDevice(module, axis) : null;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            Font = UiStyle.Body;
            Cursor = Cursors.Default;
            RefreshTip();
            if (rowUnit == SimpleMotion.RowUnit.Length && SimpleMotion.NeedsUnit(item))
                UnitManager.UnitChanged += OnUnitChanged;
        }

        /// <summary>Another length unit (a sign-in, Users and Preferences): the last reading in it.</summary>
        private void OnUnitChanged(object sender, EventArgs e)
        {
            if (IsDisposed || !_known) return;
            if (InvokeRequired) { try { BeginInvoke(new EventHandler(OnUnitChanged), sender, e); } catch { } return; }
            _text = SimpleMotion.Format(_item, _low, _high, _unitSetting, _axis, _decimals, _rowUnit);
            if (IsHandleCreated) Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) UnitManager.UnitChanged -= OnUnitChanged;
            base.Dispose(disposing);
        }

        public int PlcNumber { get; private set; }
        /// <summary>The value's (first) word; the scanner reads the one after it too.</summary>
        public string Address { get { return _first; } }
        /// <summary>The axis's unit setting, for a position or a speed.</summary>
        public IEnumerable<string> ExtraAddresses { get { return _unit != null ? new[] { _unit } : new string[0]; } }

        public SimpleMotion.Item Item { get { return _item; } }

        /// <summary>A parameter, an Admin signed in, its value read and its PLC there: a click changes it.</summary>
        public bool Editable
        {
            get
            {
                return _item.Writable && _known && SecurityManager.HasAccess(SecurityLevel.Admin) &&
                       !ConnectionMonitor.IsKnownDown(PlcNumber);
            }
        }
        /// <summary>How the row shows its positions (tests).</summary>
        public SimpleMotion.RowUnit RowUnit { get { return _rowUnit; } }
        /// <summary>What it shows ("123.46 mm", "ON") - for the tests.</summary>
        public string ShownText { get { return _text; } }
        public bool IsOn { get { return _on; } }

        public void UpdateState(Func<string, int> lookup, Func<SystemCondition, bool> checkGlobal)
        {
            int low = lookup(_first);
            int high = _item.Double ? lookup(SimpleMotion.Device(_module, SimpleMotion.AddressOf(_item, _axis) + 1)) : 0;
            int unit = _unit != null ? lookup(_unit) : 0;
            _low = low; _high = high; _unitSetting = unit;
            string text = SimpleMotion.Format(_item, low, high, unit, _axis, _decimals, _rowUnit);
            bool on = _item.Kind == SimpleMotion.Kind.Flag && SimpleMotion.FlagOn(_item, low, _axis);
            if (text == _text && on == _on && _known) return;
            _text = text;
            _on = on;
            _known = true;
            if (IsHandleCreated && !IsDisposed) Invalidate();
        }

        /// <summary>The flags that are bad news when on: red; a request (home): amber; the rest green.</summary>
        private Color LampColor
        {
            get
            {
                switch (_item.Key)
                {
                    case "ErrorDetected": case "AbsoluteLost": return Color.FromArgb(220, 53, 69);
                    case "WarningDetected": case "ServoWarning": case "HomeRequest": case "TorqueLimiting":
                    case "LowerLimit": case "UpperLimit": return Color.FromArgb(255, 170, 0);
                    default: return Color.FromArgb(76, 175, 80);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            bool stale = ConnectionMonitor.IsKnownDown(PlcNumber);
            if (_item.Kind == SimpleMotion.Kind.Flag)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int d = Math.Max(10, Math.Min(Height, Width) - UiScale.Px(14));
                var r = new Rectangle((Width - d) / 2, (Height - d) / 2, d, d);
                var fill = !_known || stale ? Color.FromArgb(240, 240, 240) : _on ? LampColor : Color.FromArgb(225, 225, 225);
                using (var b = new SolidBrush(fill)) g.FillEllipse(b, r);
                using (var pen = new Pen(Color.FromArgb(stale ? 175 : 25, stale ? 181 : 28, stale ? 190 : 34), Math.Max(1.2f, d / 10f)))
                    g.DrawEllipse(pen, r);
                return;
            }
            var box = new Rectangle(0, 0, Width - 1, Height - 1);
            // A value to read, not to type: on light grey like the other displays
            // (lighter than the position's, so the plain figures stay crisp). An Admin's
            // parameter is a field: white, its border darker.
            bool field = Editable;
            using (var fill = new SolidBrush(field ? Color.White : DisplayBack)) g.FillRectangle(fill, box);
            using (var pen = new Pen(field ? Color.FromArgb(150, 158, 170) : Color.FromArgb(205, 210, 217))) g.DrawRectangle(pen, box);
            var ink = stale || !_known ? Color.FromArgb(130, 136, 146)
                    : _item.Kind == SimpleMotion.Kind.Code && _text != "0" ? Color.FromArgb(200, 30, 30)
                    : UiStyle.GridInk;
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                        (_item.Kind == SimpleMotion.Kind.Status ? TextFormatFlags.Left : TextFormatFlags.Right);
            TextRenderer.DrawText(g, _known ? _text : "-", Font, Rectangle.Inflate(box, -UiScale.Px(4), 0), ink, flags);
        }

        private void RefreshTip()
        {
            var text = LanguageManager.GetString(_item.TitleKey, _item.Title) + " (" + _item.Ref + ")";
            // The device: for an Admin, as every field's tooltip says it.
            if (SecurityManager.HasAccess(SecurityLevel.Admin))
                text += "\n" + _first + (_item.Double ? " / " + SimpleMotion.Device(_module, SimpleMotion.AddressOf(_item, _axis) + 1) : "") +
                        (_item.Kind == SimpleMotion.Kind.Flag ? (_item.BitPerAxis ? " bit " + (_axis - 1) : " bit " + _item.Bit) : "");
            if (_item.Writable && SecurityManager.HasAccess(SecurityLevel.Admin))
                text += "\n" + LanguageManager.GetString("Tip_MotionParamEdit",
                            "Click: change it. Kept after power off once the module's flash ROM is written (the blinking button above the page).") +
                        "\n" + MotionFlash.WhenUsed(_item);
            _tips.SetToolTip(this, text);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            RefreshTip();
            Cursor = Editable ? Cursors.Hand : Cursors.Default;
            base.OnMouseEnter(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (!Editable) return;
            if (e.Button == MouseButtons.Left) Edit();
            else if (e.Button == MouseButtons.Right) ShowMenu(e.Location);
        }

        private string T(string key, string en) { return LanguageManager.GetString(key, en); }

        /// <summary>The cell's menu: change it, write the module's flash ROM.</summary>
        private void ShowMenu(Point at)
        {
            var m = new ContextMenuStrip();
            m.Items.Add(new ToolStripMenuItem(T("Mnu_MotionChange", "Change..."), null, (s, e) => Edit()));
            m.Items.Add(new ToolStripMenuItem(string.Format(T("Mnu_MotionFlash", "Write module {0}'s flash ROM..."), _module), null,
                                              (s, e) => MotionFlash.Write(FindForm(), PlcNumber, _module)));
            m.Closed += (s, e) => BeginInvoke((Action)(() => m.Dispose()));
            m.Show(this, at);
        }

        /// <summary>The number shown now, as the keypad takes it (invariant, the cell's decimals).</summary>
        internal string KeypadText()
        {
            long raw = SimpleMotion.Raw(_item, _low, _high);
            int d = SimpleMotion.ShownDecimals(_item, _unitSetting, _decimals, _rowUnit);
            return SimpleMotion.Shown(_item, raw, _unitSetting, _rowUnit).ToString("F" + d, System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>A click on a parameter: the four units to choose from, or the keypad within its setting range.</summary>
        private void Edit()
        {
            if (!Editable) return;
            if (_item.Kind == SimpleMotion.Kind.Unit)
            {
                var m = new ContextMenuStrip();
                long now = SimpleMotion.Raw(_item, _low, _high);
                for (int u = 0; u <= 3; u++)
                {
                    int unit = u;
                    m.Items.Add(new ToolStripMenuItem(SimpleMotion.UnitName(u), null, (s, e) => WriteRaw(unit)) { Checked = now == u });
                }
                m.Closed += (s, e) => BeginInvoke((Action)(() => m.Dispose()));
                m.Show(this, new Point(0, Height));
                return;
            }
            int d = SimpleMotion.ShownDecimals(_item, _unitSetting, _decimals, _rowUnit);
            double min = SimpleMotion.Shown(_item, _item.Min, _unitSetting, _rowUnit), max = SimpleMotion.Shown(_item, _item.Max, _unitSetting, _rowUnit);
            using (var keypad = new Kaban.UI.Forms.NumpadForm(T(_item.TitleKey, _item.Title) + " (" + _item.Ref + ")", KeypadText(),
                                                              Math.Min(min, max), Math.Max(min, max), allowNegative: _item.Signed, allowDecimal: d > 0))
            {
                keypad.DecimalPlaces = d;
                keypad.PlaceBeside(this);
                if (keypad.ShowDialog() != DialogResult.OK) return;
                ApplyTyped(keypad.Value);
            }
        }

        /// <summary>
        /// A number typed in the cell's unit: checked against the parameter's setting range and
        /// written (then the flash ROM offered). False, with a message, when it is not a number or
        /// out of range.
        /// </summary>
        internal bool ApplyTyped(string text)
        {
            double v;
            if (!double.TryParse((text ?? "").Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v))
            {
                Tell(T("Msg_MotionNotNumber", "Please enter a number."));
                return false;
            }
            long raw;
            if (!SimpleMotion.TryToRaw(_item, v, _unitSetting, _rowUnit, out raw))
            {
                int d = SimpleMotion.ShownDecimals(_item, _unitSetting, _decimals, _rowUnit);
                var nf = System.Globalization.CultureInfo.InvariantCulture;
                Tell(string.Format(T("Msg_MotionOutOfRange", "{0} ({1}) is set between {2} and {3} - not written."),
                                   T(_item.TitleKey, _item.Title), _item.Ref,
                                   SimpleMotion.Shown(_item, _item.Min, _unitSetting, _rowUnit).ToString("F" + d, nf),
                                   SimpleMotion.Shown(_item, _item.Max, _unitSetting, _rowUnit).ToString("F" + d, nf)));
                return false;
            }
            return WriteRaw(raw);
        }

        /// <summary>The module's count written into its word(s) - logged as the Admin's - then the flash ROM offered.</summary>
        internal bool WriteRaw(long raw)
        {
            var driver = PlcManager.Instance.GetDriverForWrite(PlcNumber);
            if (driver == null)
            {
                Tell(T("Msg_MotionNoPlc", "The PLC is not connected (or your user level may not write to it): not written."));
                return false;
            }
            int low, high;
            SimpleMotion.ToWords(_item, raw, out low, out high);
            int rc;
            using (WriteSource.For(this, _item.Ref + " typed in"))
            {
                rc = driver.Write(_first, low);
                if (rc == 0 && _item.Double) rc = driver.Write(SimpleMotion.Device(_module, SimpleMotion.AddressOf(_item, _axis) + 1), high);
            }
            if (rc != 0)
            {
                Tell(T("Msg_MotionRefused", "The PLC refused the write: not written."));
                return false;
            }
            _low = low; _high = high;
            _text = SimpleMotion.Format(_item, low, high, _unitSetting, _axis, _decimals, _rowUnit);
            Invalidate();
            // the module changed: the page's button blinks until its flash ROM is written
            MotionFlash.MarkChanged(PlcNumber, _module, _axis, _item, _text);
            return true;
        }

        private void Tell(string text)
        {
            MotionFlash.LastMessage = text;
            if (MotionFlash.Ask != null) { MotionFlash.Ask(text, MessageBoxButtons.OK); return; }
            MessageBox.Show(FindForm(), text, LanguageManager.GetString("Ttl_MotionFlash", "Simple motion module"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
