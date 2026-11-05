using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Kaban.UI.Design
{
    /// <summary>
    /// THE PROPERTIES WINDOW IN THE ORDER OF THE WORK, not of the alphabet.
    ///
    /// Visual Studio lists categories and the properties inside them
    /// alphabetically, so a numeric field read FormulaCheck, MathVariables,
    /// ReadFormula, WriteFormula - the formulas before the variables they use -
    /// and the device came somewhere after the colours. Here every HMI control
    /// has a layout: its categories in the order a page is built (the device
    /// first, then what is shown, what may be entered, the formulas, the
    /// conditions, the security, the help), and inside each category the
    /// properties in the order they are thought about.
    ///
    /// HOW: the Properties window sorts by the text it shows, so each category
    /// and each listed property is shown with leading TAB characters - more
    /// tabs, higher up. Tabs are not drawn. Only what is shown changes: the
    /// property's NAME - what the designer files use - stays as it is, so every
    /// page keeps loading and nothing is renamed in code. Use the Categorized
    /// view (the Alphabetical one mixes the ranks of all categories).
    ///
    /// A property not in its control's layout keeps its own category and comes
    /// after the listed ones; `tools/ui_test/PropertyOrderUi.cs` fails when a
    /// control has a property the table does not place, so a new property is
    /// given its place when it is added.
    /// </summary>
    public static class PropertyOrder
    {
        /// <summary>Every category, in page-building order. A control uses the ones it has.</summary>
        public static readonly string[] CategoryOrder =
        {
            "Valve & Feedback", "Inverter", "Device", "Encoder", "Unit", "Axis", "Action", "Action A", "Action B", "Movement", "Position",
            "Target position", "Calibration", "Positioning tolerance", "Direction ref",
            "Reference", "Measuring tolerance", "Minimum limit", "Unclamping interval", "Value", "Settings",
            "Load Side Encoder", "Commands", "Display", "Input", "Formula", "State", "Meters", "Bands", "Diagnostics", "Parts",
            "Conditions", "Security", "Help"
        };

        private static readonly string[] NumericDevice = { "PlcNumber", "Address", "DataType", "AddressOffset" };
        private static readonly string[] NumericFormat =
        {
            "DescriptionText", "DescriptionColon", "RowWidth", "DataFormat", "Divisor", "UnitType", "Digits", "DecimalPlaces",
            "FillZero", "Suffix", "ShowSuffix"
        };
        private static readonly string[] NumericFrame = { "ShowValueBorder", "ValueBorderColor", "CornerRadius" };

        /// <summary>Control type name -> (category, properties in order).</summary>
        public static readonly Dictionary<string, List<KeyValuePair<string, string[]>>> Layouts =
            new Dictionary<string, List<KeyValuePair<string, string[]>>>(StringComparer.Ordinal)
        {
            ["NumericDisplayHMI"] = L(
                C("Device", NumericDevice),
                C("Display", NumericFormat.Concat(NumericFrame).ToArray()),
                C("Conditions", "VisibilityConditions"),
                C("Security", "RequiredSecurity", "LogAction"),
                C("Help", "ToolTipTextKey")),

            ["NumericInputHMI"] = L(
                C("Device", NumericDevice),
                C("Display", NumericFormat.Concat(new[] { "BarView", "BarHeader", "BarStep" }).Concat(NumericFrame).ToArray()),
                C("Input", "MinLimit", "MaxLimit", "IsAngle", "OutOfLimitsWarning", "ActiveArea"),
                C("Formula", "MathVariables", "ReadFormula", "WriteFormula", "FormulaCheck"),
                C("Conditions", "VisibilityConditions", "EnableConditions", "EnableTrigger"),
                C("Security", "RequiredSecurity", "ConfirmWrite", "LogAction"),
                C("Help", "ToolTipTextKey")),

            ["ButtonHMI"] = L(
                C("Device", "PlcNumber"),
                C("Action", "BitActions", "WordActions", "HmiFunction"),
                C("Display", "DescriptionText", "ButtonText", "FallbackText", "TranslationKey", "ShowCaption", "TwoLineCaption", "Direction",
                             "DisplayAddress", "DisplayOffset", "ImageActive", "ImagePassive", "ColorActive", "ColorPassive",
                             "ColorDisabled", "ForeDisabled", "ActiveBackColor", "IsActivePage", "CornerRadius",
                             "RoundLeftOnly", "RoundRightOnly", "RestingBorderColor", "OutputBorderColor", "HoverBorderColor"),
                C("State", "Lamp1Address", "Lamp1Tip", "Lamp2Address", "Lamp2Tip", "LampDirection", "RelayAddress",
                           "OutputAddress", "ErrorAddress", "BlinkCondition", "AlertBlink"),
                C("Conditions", "VisibilityConditions", "EnableConditions", "EnableTrigger"),
                C("Security", "RequiredSecurity", "ConfirmationNeeded", "ConfirmationAddress", "ConfirmationTextKey", "ConfirmationText", "LogAction",
                              "ResetOnUserChange"),
                C("Help", "ToolTipText")),

            // the machine's speed in percent (a home page's, 12 October)
            ["SpeedOverrideHMI"] = L(
                C("Device", "PlcNumber", "Address"),
                C("Display", "TranslationKey", "FallbackText"),
                C("Input", "Steps"),
                C("Security", "RequiredSecurity", "LogAction")),

            ["CheckBoxHMI"] = L(
                C("Device", "PlcNumber", "Address", "AlertAddress"),
                C("Display", "Look", "ActiveArea", "DescriptionText", "DescriptionSide", "CornerRadius"),
                C("Conditions", "VisibilityConditions", "EnableConditions", "EnableTrigger"),
                C("Security", "RequiredSecurity", "ConfirmWrite", "LogAction"),
                C("Help", "ToolTipCaption")),

            ["CheckListHMI"] = L(
                C("Device", "PlcNumber", "Options"),
                C("Display", "Columns"),
                C("Security", "RequiredSecurity", "LogAction")),

            ["ComboBoxHMI"] = L(
                C("Device", "PlcNumber", "Address", "Options"),
                C("Display", "DescriptionText"),
                C("Security", "RequiredSecurity", "LogAction")),

            ["GroupBoxHMI"] = L(
                C("Device", "PlcNumber"),
                C("Display", "TitleTextKey", "Emoji"),
                C("Conditions", "VisibilityConditions", "VisibleAddress", "EnableConditions", "EnableAddress")),

            ["HexCodeHMI"] = L(
                C("Device", "PlcNumber", "Address"),
                C("Display", "Caption", "CodeKind", "Severity")),

            ["LabelHMI"] = L(
                C("Display", "TranslationKey")),

            ["LampHMI"] = L(
                C("Device", "PlcNumber", "Address", "AddressOffset"),
                C("Display", "DescriptionText", "DescriptionSide", "OnColor", "OffColor", "ImageActive", "ImagePassive", "CornerRadius"),
                C("Conditions", "VisibilityConditions"),
                C("Help", "ToolTipTextKey")),

            ["MeterHMI"] = L(
                C("Device", "PlcNumber", "Address"),
                C("Display", "Caption", "ShowValue", "Divisor", "DecimalPlaces", "Suffix", "Signed", "ScaleMinimum", "ScaleMaximum"),
                C("Bands", "Bands", "LowAlarm", "LowWarning", "HighWarning", "HighAlarm")),

            ["MeterInputHMI"] = L(
                C("Device", "PlcNumber", "Address"),
                C("Display", "Caption", "ShowValue", "Divisor", "DecimalPlaces", "Suffix", "Signed", "ScaleMinimum", "ScaleMaximum"),
                C("Input", "Minimum", "Maximum", "Step"),
                C("Bands", "Bands", "LowAlarm", "LowWarning", "HighWarning", "HighAlarm"),
                C("Security", "RequiredSecurity", "LogAction")),

            ["AxisUnitHMI"] = L(
                C("Device", "PlcNumber", "AxisNumber")),

            ["AxisSettingHMI"] = L(
                C("Device", "PlcNumber", "HMIAxisIndex", "MotionNumber", "MotionAxisIndex"),
                C("Axis", "AxisNameKey", "VerticalMovement", "ReferenceSensorAddress", "ReferenceSensorSide"),
                C("Movement", "CommandBackAddress", "CommandForwardAddress", "PositionAddress", "PositionDataType"),
                C("Value", "SettingDataType", "Divisor", "DecimalPlaces", "Digits", "Suffix",
                           "EntryMinimum", "EntryMaximum"),
                C("Settings", "MinLimitAddress", "MaxLimitAddress", "ReferenceAddress", "CalibrationAddress",
                              "TravelPerRevolutionAddress", "BacklashAddress"),
                C("Load Side Encoder", "LoadSideEncoderAddress", "LoadSideEncoderDivisor"),
                C("Commands", "CalibrateAddress", "GoToReferenceAddress", "HomeRequestAddress"),
                C("Meters", "CurrentAddress", "CurrentDivisor", "CurrentFullScale", "ZDistanceAddress", "ZDistanceDivisor", "ZDistanceFullScale"),
                C("Diagnostics", "ErrorAddress", "ServoAlarmAddress", "AxisAlarmAddress", "AxisWarningAddress"),
                C("Conditions", "VisibilityConditions", "EnableConditions", "BackEnableConditions", "BackEnableTrigger",
                                "ForwardEnableConditions", "ForwardEnableTrigger", "CalibrateEnableConditions", "CalibrateEnableTrigger"),
                C("Security", "RequiredSecurity")),

            // A pneumatic positioning: its valve and feedback first (as a cylinder's),
            // then the row's sections in the row's order.
            ["PneumaticPositioningHMI"] = L(
                C("Valve & Feedback", "ValveType", "FeedbackType", "TypeCheck"),
                C("Device", "PlcNumber"),
                C("Unit", "TitleTextKey", "NameWidth"),
                C("Movement", "CommandBackAddress", "CommandForwardAddress", "VerticalMovement", "BackSensorAddress", "ForwardSensorAddress",
                              "ErrorBackAddress", "ErrorForwardAddress", "HelpBackFiles", "HelpForwardFiles"),
                C("Position", "PositionAddress", "PositionDataType", "PositionDivisor", "PositionDecimalPlaces", "PositionDigits",
                              "PositionSuffix", "PositionUnitType"),
                C("Target position", "TargetPosition", "PositioningStartAddress"),
                C("Calibration", "CalibrationAddress", "CalibrateAddress"),
                C("Positioning tolerance", "PositioningToleranceAddress"),
                C("Direction ref", "PositioningDirectionReferenceAddress"),
                C("Value", "SettingDataType", "Divisor", "DecimalPlaces", "Digits", "Suffix", "UnitType"),
                C("Conditions", "VisibilityConditions", "EnableConditions"),
                C("Security", "RequiredSecurity", "SettingsSecurity", "LogAction")),

            // A cylinder with an encoder, as a row of Machine Parameters > Encoders.
            ["CylinderEncoderSettingHMI"] = L(
                C("Valve & Feedback", "ValveType", "OptionValveType", "OptionValveConditions", "FeedbackType", "TypeCheck"),
                C("Device", "PlcNumber", "EncoderNumber"),
                C("Unit", "TitleTextKey", "NameWidth"),
                C("Movement", "CommandBackAddress", "CommandForwardAddress", "VerticalMovement", "BackSensorAddress", "ForwardSensorAddress",
                              "ErrorBackAddress", "ErrorForwardAddress", "HelpBackFiles", "HelpForwardFiles"),
                C("Position", "PositionAddress", "PositionDataType", "PositionDivisor", "PositionDecimalPlaces", "PositionDigits",
                              "PositionSuffix", "PositionUnitType"),
                C("Reference", "ReferenceAddress"),
                C("Measuring tolerance", "MeasuringToleranceAddress"),
                C("Minimum limit", "MinimumLimitAddress"),
                C("Unclamping interval", "UnclampingIntervalAddress"),
                C("Value", "SettingDataType", "Divisor", "DecimalPlaces", "Digits", "Suffix", "UnitType"),
                C("Conditions", "VisibilityConditions", "EncoderConditions", "MeasuredConditions", "EnableConditions"),
                C("Security", "RequiredSecurity", "SettingsSecurity", "LogAction")),

            // A cylinder with an encoder on a manual page: its encoder's number, the rest from that row.
            ["CylinderEncoderUnitHMI"] = L(
                C("Encoder", "EncoderNumber", "PlcNumber"),
                C("Unit", "TitleTextKey"),
                C("Conditions", "VisibilityConditions", "EnableConditions")),

            ["CylinderUnitHMI"] = L(
                C("Valve & Feedback", "ValveType", "OptionValveType", "OptionValveConditions", "FeedbackType", "TypeCheck"),   // chosen first: they decide what else is needed
                C("Device", "PlcNumber", "BaseOffset"),
                C("Unit", "TitleTextKey", "TitleTipKey"),
                C("Action A", Side("A")),
                C("Action B", Side("B")),
                // (no Position: a position is a pneumatic positioning's or an encoder unit's - 9 October)
                C("Parts", "ButtonA", "ButtonB", "Arrows"),
                C("Conditions", "VisibilityConditions", "EnableConditions"),
                C("Security", "RequiredSecurity", "ConfirmationNeeded", "LogAction")),

            // An inverter's motor (14 October): its fault code first - it is what the unit is for.
            ["InverterUnitHMI"] = L(
                C("Inverter", "FaultCodeAddress", "ErrorAddress", "RunLampAddress", "RunLampTipKey", "CurrentAddress", "CurrentDivisor", "LoadWhileAddress"),
                C("Device", "PlcNumber", "BaseOffset"),
                C("Unit", "TitleTextKey", "TitleTipKey"),
                C("Action A", InverterSide("A")),
                C("Action B", InverterSide("B")),
                C("Value", "PositionAddress", "PositionDataType", "PositionDivisor", "PositionDecimalPlaces", "PositionDigits",
                           "PositionSuffix", "PositionUnitType", "PositionOffset", "PositionStyle", "PositionTipKey"),
                C("Parts", "ButtonA", "ButtonB", "Arrows"),
                C("Conditions", "VisibilityConditions", "EnableConditions"),
                C("Security", "RequiredSecurity", "ConfirmationNeeded", "LogAction")),
        };

        /// <summary>A motor's button: no error bit of its own (the inverter's is ErrorAddress).</summary>
        private static string[] InverterSide(string s)
        {
            return Side(s).Where(p => !p.StartsWith("Error", StringComparison.Ordinal)).ToArray();
        }

        private static string[] Side(string s)
        {
            return new[]
            {
                "Command{0}Address", "Command{0}Action", "Button{0}TextKey", "Button{0}TipKey",
                "Lamp1{0}Address", "Lamp1{0}TipKey", "Lamp2{0}Address", "Lamp2{0}TipKey",
                "Relay{0}Address", "Output{0}Address", "Error{0}Address", "Help{0}Files",
                "Button{0}VisibilityConditions", "Button{0}EnableConditions", "Button{0}EnableTrigger"
            }.Select(f => string.Format(f, s)).ToArray();
        }

        private static List<KeyValuePair<string, string[]>> L(params KeyValuePair<string, string[]>[] c) { return c.ToList(); }
        private static KeyValuePair<string, string[]> C(string category, params string[] props)
        {
            return new KeyValuePair<string, string[]>(category, props);
        }

        /// <summary>The layout of a control type or of the nearest base type that has one; null when none.</summary>
        public static List<KeyValuePair<string, string[]>> LayoutFor(Type t)
        {
            for (; t != null && t != typeof(object); t = t.BaseType)
            {
                List<KeyValuePair<string, string[]>> l;
                if (Layouts.TryGetValue(t.Name, out l)) return l;
            }
            return null;
        }

        /// <summary>Where a property goes: its category and both texts as shown (with their tabs); false when not listed.</summary>
        public static bool Place(Type t, string property, out string category, out string shownCategory, out string shownName)
        {
            category = shownCategory = shownName = null;
            var layout = LayoutFor(t);
            if (layout == null) return false;
            var cats = layout.Select(c => c.Key).OrderBy(c => Array.IndexOf(CategoryOrder, c)).ToList();
            foreach (var c in layout)
            {
                int i = Array.IndexOf(c.Value, property);
                if (i < 0) continue;
                category = c.Key;
                shownCategory = new string('\t', cats.Count - cats.IndexOf(c.Key)) + c.Key;
                shownName = new string('\t', c.Value.Length - i) + property;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Registered on each HMI control ([TypeDescriptionProvider]): the
        /// control's properties as reflection gives them, each listed one with its
        /// category and name shown in order. Same properties, same order in the
        /// collection - the designer writes the files exactly as before.
        /// </summary>
        public sealed class Provider : TypeDescriptionProvider
        {
            private static readonly TypeDescriptionProvider Reflection = TypeDescriptor.GetProvider(typeof(object));

            public Provider() : base(Reflection) { }

            public override ICustomTypeDescriptor GetTypeDescriptor(Type objectType, object instance)
            {
                return new Ordered(base.GetTypeDescriptor(objectType, instance), objectType);
            }

            private sealed class Ordered : CustomTypeDescriptor
            {
                private readonly Type _type;
                public Ordered(ICustomTypeDescriptor parent, Type type) : base(parent) { _type = type; }

                public override PropertyDescriptorCollection GetProperties() { return Order(base.GetProperties()); }
                public override PropertyDescriptorCollection GetProperties(Attribute[] attributes) { return Order(base.GetProperties(attributes)); }

                private PropertyDescriptorCollection Order(PropertyDescriptorCollection props)
                {
                    if (props == null || LayoutFor(_type) == null) return props;
                    var list = new PropertyDescriptor[props.Count];
                    for (int i = 0; i < props.Count; i++)
                    {
                        var p = props[i];
                        string cat, shownCat, shownName;
                        list[i] = Place(_type, p.Name, out cat, out shownCat, out shownName)
                            ? TypeDescriptor.CreateProperty(p.ComponentType, p, new CategoryAttribute(shownCat), new DisplayNameAttribute(shownName))
                            : p;
                    }
                    return new PropertyDescriptorCollection(list, true);
                }
            }
        }
    }
}
