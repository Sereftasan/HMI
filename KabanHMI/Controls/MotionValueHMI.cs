using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// ONE VALUE OF A SIMPLE MOTION MODULE for one axis, read straight from the
    /// module's buffer memory (Un\G...) - a column of the Axis Settings page
    /// (SimpleMotion): a number in its unit, the operation status in words, or a
    /// flag as a lamp. Read only.
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
            // (lighter than the position's, so the plain figures stay crisp).
            using (var fill = new SolidBrush(DisplayBack)) g.FillRectangle(fill, box);
            using (var pen = new Pen(Color.FromArgb(205, 210, 217))) g.DrawRectangle(pen, box);
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
            _tips.SetToolTip(this, text);
        }

        protected override void OnMouseEnter(EventArgs e) { RefreshTip(); base.OnMouseEnter(e); }
    }
}
