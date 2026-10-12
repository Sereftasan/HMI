using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.UI.Controls;

// AXIS ROWS' ENTRY BOUNDS (30 September): one property each, a number or a
// device - EntryMinimum / EntryMaximum as DynamicValues, instead of a number
// and a device that overrode it. And no Address property: the row's fields
// carry their own.
class AE {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static NumericInputHMI Field(AxisSettingHMI a, string n) { return (NumericInputHMI)typeof(AxisSettingHMI).GetField(n, Any).GetValue(a); }
    static AxisSettingHMI Row() {
        return new AxisSettingHMI { AxisNameKey = "Axis_RobotX", PositionAddress = "W0", MinLimitAddress = "D2", MaxLimitAddress = "D4",
                                    ReferenceAddress = "D6", CalibrationAddress = "D8" };
    }

    [STAThread]
    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();

        Console.WriteLine("--- the properties ---");
        var t = typeof(AxisSettingHMI);
        Check("no Address property any more", t.GetProperty("Address") == null);
        Check("no EntryMinimumAddress / EntryMaximumAddress", t.GetProperty("EntryMinimumAddress") == null && t.GetProperty("EntryMaximumAddress") == null);
        Check("EntryMinimum and EntryMaximum are dynamic values", t.GetProperty("EntryMinimum").PropertyType == typeof(DynamicValue) &&
              t.GetProperty("EntryMaximum").PropertyType == typeof(DynamicValue));
        var fresh = new AxisSettingHMI();
        Check("  none by default", fresh.EntryMinimum.Type == LimitSourceType.None && fresh.EntryMaximum.Type == LimitSourceType.None);

        var row = Row();
        var prop = TypeDescriptor.GetProperties(row)["EntryMinimum"];
        var conv = prop.Converter;
        Check("typed in the Properties window: -500 is a number", ((DynamicValue)conv.ConvertFrom(null, null, "-500")).Type == LimitSourceType.Static &&
              ((DynamicValue)conv.ConvertFrom(null, null, "-500")).StaticValue == -500);
        Check("  D318 is a device", ((DynamicValue)conv.ConvertFrom(null, null, "D318")).Type == LimitSourceType.Tag &&
              ((DynamicValue)conv.ConvertFrom(null, null, "D318")).TagAddress == "D318");
        Check("  written as the page's designer file keeps it (its parts)", prop.Attributes.OfType<DesignerSerializationVisibilityAttribute>()
              .Any(a => a.Visibility == DesignerSerializationVisibility.Content));

        Console.WriteLine("--- what the row's fields take ---");
        row.EntryMinimum = new DynamicValue(LimitSourceType.Static, -500, "");
        row.EntryMaximum = new DynamicValue(LimitSourceType.Tag, 0, "D320");
        row.BuildRow();
        var min = Field(row, "_min"); var max = Field(row, "_max"); var refField = Field(row, "_reference");
        Check("the minimum field: from -500 up to the maximum's device", min.MinLimit.Type == LimitSourceType.Static && min.MinLimit.StaticValue == -500 &&
              min.MaxLimit.Type == LimitSourceType.Tag && min.MaxLimit.TagAddress == "D4", min.MinLimit + " .. " + min.MaxLimit);
        Check("the maximum field: from the minimum's device up to D320", max.MinLimit.TagAddress == "D2" && max.MaxLimit.Type == LimitSourceType.Tag &&
              max.MaxLimit.TagAddress == "D320", max.MinLimit + " .. " + max.MaxLimit);
        Check("the reference: inside the travel (the min and max devices)", refField.MinLimit.TagAddress == "D2" && refField.MaxLimit.TagAddress == "D4");
        Check("  each field its own copy (a change to the row's bound does not reach into a built field)",
              !ReferenceEquals(min.MinLimit, row.EntryMinimum));

        // a row with no travel devices: its reference and calibration take the entry bounds
        var loose = new AxisSettingHMI { AxisNameKey = "Axis_RobotY", PositionAddress = "W0", ReferenceAddress = "D6", CalibrationAddress = "D8",
                                         EntryMinimum = new DynamicValue(LimitSourceType.Static, -500, ""), EntryMaximum = new DynamicValue(LimitSourceType.Tag, 0, "D320") };
        loose.BuildRow();
        var cal = Field(loose, "_calibration");
        Check("no travel devices: the calibration takes the entry bounds (-500 .. D320)", cal.MinLimit.StaticValue == -500 && cal.MinLimit.Type == LimitSourceType.Static &&
              cal.MaxLimit.TagAddress == "D320", cal.MinLimit + " .. " + cal.MaxLimit);
        var plain = new AxisSettingHMI { AxisNameKey = "Axis_RobotY", PositionAddress = "W0", CalibrationAddress = "D8" };
        plain.BuildRow();
        Check("no bounds given: none", Field(plain, "_calibration").MinLimit.Type == LimitSourceType.None &&
              Field(plain, "_calibration").MaxLimit.Type == LimitSourceType.None);
        var nan = new AxisSettingHMI { AxisNameKey = "Axis_RobotY", PositionAddress = "W0", CalibrationAddress = "D8",
                                       EntryMinimum = new DynamicValue(LimitSourceType.Static, double.NaN, "") };
        nan.BuildRow();
        Check("a number that is no number: none", Field(nan, "_calibration").MinLimit.Type == LimitSourceType.None);

        Console.WriteLine(fail == 0 ? "\nAXIS ENTRY BOUNDS ALL PASS" : "\nAXIS ENTRY BOUNDS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
