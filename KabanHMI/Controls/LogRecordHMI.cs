using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Drawing;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// ONE VALUE OF A LOG RECORD (LogRecordHMI.Values): read from the PLC the moment the record's trigger comes - a lamp's bit
    /// (RecordLamp) or a numeric display's word (RecordNumber), as the PLC program MOVed them into its log area.
    /// </summary>
    public abstract class RecordValue
    {
        [Description("The value's name in the record (a translation key, or the English text) - as the dashboard shows it. " +
                     "When the record replaces one of the PLC's (its code in the state map), use the state map's name of the value " +
                     "(e.g. 'Bar Length'); empty: the state map's name at this place.")]
        public string TextKey { get; set; } = "";

        [Description("The PLC device read (D89, M100, D10.5; X12@2: of the machine's other PLC).")]
        public string Address { get; set; } = "";

        /// <summary>The value read from the PLC (null: not read), in its unit.</summary>
        public abstract double? ReadFrom(Func<string, int> lookup);

        /// <summary>The devices it reads (a 32-bit value: both words).</summary>
        public abstract IEnumerable<string> Devices();

        public override string ToString()
        {
            return (string.IsNullOrWhiteSpace(TextKey) ? "(name)" : TextKey) + " = " + (string.IsNullOrWhiteSpace(Address) ? "?" : Address);
        }
    }

    /// <summary>A bit of the record: 1 while it is on (the PLC program's MOV K1 / K0 on a contact) - as a LampHMI shows it.</summary>
    [TypeConverter(typeof(RecordValueConverter))]
    public class RecordLamp : RecordValue
    {
        public RecordLamp() { }
        public RecordLamp(string textKey, string address) { TextKey = textKey ?? ""; Address = address ?? ""; }

        public override double? ReadFrom(Func<string, int> lookup)
        {
            if (lookup == null || string.IsNullOrWhiteSpace(Address)) return null;
            try { return lookup(Address.Trim()) != 0 ? 1 : 0; } catch { return null; }
        }

        public override IEnumerable<string> Devices()
        {
            if (!string.IsNullOrWhiteSpace(Address)) yield return Address.Trim();
        }
    }

    /// <summary>A number of the record: a word or two (the PLC program's MOV / DMOV), divided by its Divisor - as a NumericDisplayHMI shows it.</summary>
    [TypeConverter(typeof(RecordValueConverter))]
    public class RecordNumber : RecordValue
    {
        public RecordNumber() { }
        public RecordNumber(string textKey, string address, DataTypeEnum dataType, double divisor)
        {
            TextKey = textKey ?? ""; Address = address ?? ""; DataType = dataType; Divisor = divisor;
        }

        [Description("One word (Bin_16: MOV) or two (Bin_32: DMOV, the low word first).")]
        public DataTypeEnum DataType { get; set; } = DataTypeEnum.Bin_16;

        [Description("What the value read is divided by (10: 1234 is 123.4).")]
        public double Divisor { get; set; } = 1;

        public override double? ReadFrom(Func<string, int> lookup)
        {
            if (lookup == null || string.IsNullOrWhiteSpace(Address)) return null;
            try
            {
                string a = Address.Trim();
                int lo = lookup(a);
                long raw;
                if (DataType == DataTypeEnum.Bin_32)
                {
                    int hi = lookup(PlcAddressHelper.GetNextAddress(a));
                    raw = (int)(((uint)(hi & 0xFFFF) << 16) | (uint)(lo & 0xFFFF));
                }
                else raw = (short)(lo & 0xFFFF);
                return raw / (Divisor != 0 ? Divisor : 1);
            }
            catch { return null; }
        }

        public override IEnumerable<string> Devices()
        {
            if (string.IsNullOrWhiteSpace(Address)) yield break;
            yield return Address.Trim();
            if (DataType == DataTypeEnum.Bin_32)
            {
                string next = null;
                try { next = PlcAddressHelper.GetNextAddress(Address.Trim()); } catch { }
                if (!string.IsNullOrEmpty(next)) yield return next;
            }
        }
    }

    /// <summary>
    /// A value in the designer's code: "new RecordNumber("Bar Length", "D89", DataTypeEnum.Bin_32, 1D)" - a plain object in a
    /// list is written so (not into the page's resources), and shown in the Properties window with its own fields.
    /// </summary>
    public class RecordValueConverter : ExpandableObjectConverter
    {
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(InstanceDescriptor) || base.CanConvertTo(context, destinationType);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(InstanceDescriptor))
            {
                var n = value as RecordNumber;
                if (n != null)
                    return new InstanceDescriptor(typeof(RecordNumber).GetConstructor(new[] { typeof(string), typeof(string), typeof(DataTypeEnum), typeof(double) }),
                                                  new object[] { n.TextKey ?? "", n.Address ?? "", n.DataType, n.Divisor });
                var l = value as RecordLamp;
                if (l != null)
                    return new InstanceDescriptor(typeof(RecordLamp).GetConstructor(new[] { typeof(string), typeof(string) }),
                                                  new object[] { l.TextKey ?? "", l.Address ?? "" });
            }
            if (destinationType == typeof(string) && value is RecordValue) return value.ToString();
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    /// <summary>
    /// A RECORD OF THE MACHINE'S LOG, TAKEN BY THE HMI WHEN ITS TRIGGER COMES (5 November: "We should create a tool to create a
    /// similar structure. We need to define a trigger device and additional info devices for each one to the read when it is
    /// triggered by PLC. The trigger device could be set just like 'EnableTrigger' property for the input controls and the
    /// info should be a collection lamp HMI and numerical display HMI controls") - what the PLC program does for an older machine
    /// (L1016 and the rising BoyOkundu: MOV K16 to the log area, DMOV the bar's length after it, CALL MakineLogKayit), done by
    /// the HMI for a new one:
    ///
    ///   RecordCode   the record's code (K16) - the state map's when it replaces one of the PLC's records (the dashboard then
    ///                treats it as that record, its name and values the map's), else one of its own (1200 - 1299: named by TextKey).
    ///   Trigger      when it is taken: the moment the trigger becomes true (as EnableTrigger: a bit ON or OFF, a value in a range)
    ///                - the PLC program's rising contact. Not again until it has been false.
    ///   Values       what is read then: RecordNumber (a numeric display's word or two, divided) and RecordLamp (a lamp's bit),
    ///                in their order - the record's values, as the PLC's were from its eighth word on.
    ///
    /// Placed on a page (FA2010: the Industry 4 Logging frame of Machine Parameters > Options), found as the pages are discovered,
    /// and taken only for a new machine (HMI Settings > Options: Machine details from the HMI) keeping a log database. On the page
    /// it is the record's switch row - on, taken (on until switched off) - shown only for a new machine; the PLC's switch of the
    /// record it replaces is hidden then (MachineLogSettings.Arrange).
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(CheckBox))]
    [Description("A record the HMI writes into the machine's log when its trigger comes (a new machine): its code, its trigger, its values.")]
    public class LogRecordHMI : Control
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private readonly ToggleSwitch _switch = new ToggleSwitch { Name = "switch", Size = new Size(48, 28), TabStop = true };
        private bool _filling;

        public LogRecordHMI()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.White;
            Font = UiStyle.Body;
            Size = new Size(450, UiStyle.SettingRowHeight);
            Controls.Add(_switch);
            _switch.CheckedChanged += (s, e) => { if (!_filling) Kept(_switch.Checked); };
            Click += (s, e) => _switch.Toggle();
            Cursor = Cursors.Hand;
        }

        /// <summary>Its switch at the right end, 8 in, in the middle of its height - as a switch row's.</summary>
        private Point SwitchPlace { get { return new Point(Math.Max(0, Width - UiScale.For(this, 8) - _switch.Width), Math.Max(0, (Height - _switch.Height) / 2)); } }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            if (_switch.Location != SwitchPlace) _switch.Location = SwitchPlace;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_switch.Location != SwitchPlace) _switch.Location = SwitchPlace;
            Invalidate();
        }

        // ---- the record ----------------------------------------------------------------------------

        [Category(" Record"), DefaultValue(0)]
        [Description("Its code in the log (the PLC program's MOV K16): the state map's when it replaces one of the PLC's records - the " +
                     "dashboard then shows it as that record - else one of its own, 1200 - 1299.")]
        public int RecordCode { get; set; }

        [Category(" Record"), DefaultValue("")]
        [Description("Its name (a translation key, or the English text) - used when its code is not in the state map; its switch row's words.")]
        public string TextKey { get; set; } = "";

        [Category(" Record"), DefaultValue(1)]
        [Description("The PLC its trigger and its values are read from (1: the machine's first).")]
        public int PlcNumber { get; set; } = 1;

        [Category(" Record")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [TypeConverter(typeof(PlcTriggerSorter))]
        [Description("When it is taken: the moment this becomes true (a bit ON or OFF, a value in a range - as EnableTrigger) - the PLC " +
                     "program's rising contact. Not again until it has been false.")]
        public PlcTrigger Trigger { get; set; } = new PlcTrigger();

        [Category(" Record")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Editor(typeof(Kaban.UI.Design.RecordValuesEditor), typeof(UITypeEditor))]
        [Description("What is read when it comes, in order: RecordNumber (a numeric display's word, or two, divided) and RecordLamp (a lamp's bit).")]
        public List<RecordValue> Values { get; } = new List<RecordValue>();

        private string _module;

        /// <summary>The machine it belongs to: the module of its PLC (a test may name it).</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ModuleId
        {
            get { return string.IsNullOrEmpty(_module) ? AlarmGeneration.ModuleOfStation(PlcNumber) : _module; }
            set { _module = value; }
        }

        /// <summary>Its name as the dashboard shows it: the state map's for its code, else its own.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Shown
        {
            get
            {
                string own = string.IsNullOrWhiteSpace(TextKey) ? "" : T(TextKey, TextKey);
                if (own.Length > 0) return own;
                var board = Kaban.UI.Dashboard.DashboardService.Find(ModuleId);
                if (board != null && board.Map.Find(RecordCode) != null)
                {
                    var map = board.Map;
                    return map.Translate(map.RawDescription(RecordCode), Kaban.UI.Dashboard.StateMap.ColumnOf(LanguageManager.CurrentLanguage));
                }
                return RecordCode > 0 ? T("Rcd_Record", "Record") + " " + RecordCode : T("Rcd_Record", "Record");
            }
        }

        /// <summary>Its switch (tests).</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ToggleSwitch Switch { get { return _switch; } }

        /// <summary>Its settings key: taken or not, in the machine's dashboard settings ("hmi_records").</summary>
        public static string SettingKey(int code) { return "record_" + code.ToString(CultureInfo.InvariantCulture); }

        // ---- on the page: its switch row ----------------------------------------------------------------

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Fill();
        }

        /// <summary>The switch as the machine's settings have it.</summary>
        public void Fill()
        {
            var board = Kaban.UI.Dashboard.DashboardService.Find(ModuleId);
            _filling = true;
            try { _switch.Checked = board == null || board.Config.RecordOn(SettingKey(RecordCode)); }
            finally { _filling = false; }
            Invalidate();
        }

        private void Kept(bool on)
        {
            var board = Kaban.UI.Dashboard.DashboardService.Find(ModuleId);
            if (board == null) return;
            board.Change(c => c.SetRecordOn(SettingKey(RecordCode), on));
            AuditLogger.Log("Machine records (" + board.ModuleId + "): record " + RecordCode + " (" + Shown + ") " + (on ? "taken" : "not taken") + " by the HMI.");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            int pad = UiScale.For(this, 8);
            string words = Shown;
            if (DesignMode)
                words += "   (" + T("Rcd_Hmi", "HMI record") + " " + RecordCode + ": " + Trigger + ", " + Values.Count + " " + T("Rcd_Values", "values") + ")";
            var r = new Rectangle(pad, 0, Math.Max(10, SwitchPlace.X - 2 * pad), Height - 1);
            TextRenderer.DrawText(g, words, Font, r, Enabled ? Color.FromArgb(32, 33, 36) : Color.Gray,
                                  TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            using (var pen = new Pen(UiStyle.RowRule)) g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        }
    }
}
