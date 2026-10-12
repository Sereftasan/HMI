using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

// 25 September: an axis unit laid out like the function units around it, a
// steady red "?", the designer's Help properties (list of files, no Kind, the
// category name), condition sets and the reason naming only what is not met,
// and help PDFs opened from a copy.
class UD {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static T Field<T>(object o, string n) { return (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o); }

    [STAThread]
    static void Main() {
        var host = new Form { ClientSize = new Size(800, 300) };

        Console.WriteLine("--- an axis unit's parts are as wide as a function unit's ---");
        AlarmHelpLinks.DiscoverAll();
        var rowForm = new Form();
        var row = new AxisSettingHMI { PlcNumber = 1, HMIAxisIndex = 9, AxisNameKey = "Cutting Angle",
                                       CommandBackAddress = "M620", CommandForwardAddress = "M621", PositionAddress = "D300",
                                       Digits = 5, DecimalPlaces = 3, Suffix = "\u00B0" };
        rowForm.Controls.Add(row);
        AlarmHelpLinks.Harvest(rowForm);
        var axis = new AxisUnitHMI { AxisNumber = 9 };
        var fu = new CylinderUnitHMI { TitleTextKey = "Left Top Clamping", CommandAAddress = "M672", CommandBAddress = "M673",
                                       PositionAddress = "D15", PositionDigits = 6, PositionDecimalPlaces = 1, PositionSuffix = "mm" };
        var fu0 = new CylinderUnitHMI { TitleTextKey = "Saw Inverter", CommandAAddress = "M1", CommandBAddress = "M2",
                                        PositionAddress = "D669", PositionDigits = 6, PositionDecimalPlaces = 0, PositionSuffix = "mm" };
        axis.SetBounds(10, 10, 360, 64); fu.SetBounds(10, 90, 360, 64); fu0.SetBounds(10, 170, 360, 64);
        host.Controls.AddRange(new Control[] { axis, fu, fu0 });
        host.Show(); Application.DoEvents();
        axis.Unit.Relayout(); fu.Relayout(); fu0.Relayout(); Application.DoEvents();
        Check("the axis unit resolved its row", axis.IsResolved);
        Check("same position width as the function unit", axis.Unit.PositionDisplay.Width == fu.PositionDisplay.Width,
              axis.Unit.PositionDisplay.Width + " vs " + fu.PositionDisplay.Width);
        Check("  so the same button widths", axis.Unit.ButtonA.Width == fu.ButtonA.Width && axis.Unit.ButtonB.Width == fu.ButtonB.Width,
              axis.Unit.ButtonA.Width + "/" + axis.Unit.ButtonB.Width + " vs " + fu.ButtonA.Width + "/" + fu.ButtonB.Width);
        Check("a unit with no decimals too - one width for every unit", fu0.PositionDisplay.Width == fu.PositionDisplay.Width,
              fu0.PositionDisplay.Width + " vs " + fu.PositionDisplay.Width);

        Console.WriteLine("--- the '?' stays put, and blue ---");
        Check("the unit's '?' has no blink timer of its own", typeof(UnitHelpBadge).GetField("_blink", BindingFlags.NonPublic | BindingFlags.Static) == null);
        var cyl = new CylinderUnitHMI { CommandAAddress = "M5", ErrorAAddress = "M890" };
        cyl.SetBounds(400, 10, 360, 64); host.Controls.Add(cyl); Application.DoEvents();
        Field<AlarmHelpBadge>(cyl, "_helpA").UpdateState(a => a == "M890" ? 1 : 0, c => true);
        var mark = Field<UnitHelpBadge>(cyl, "_help");
        Check("  while the error is set it offers the fault's help - BLUE, never red (the button shows the error)",
              mark.Visible && mark.State == UnitHelpBadge.BadgeState.Error && mark.MarkColor.ToArgb() == UnitHelpBadge.Blue.ToArgb());
        Check("  UnitHelpBadge has no red or amber any more", typeof(UnitHelpBadge).GetField("Red") == null && typeof(UnitHelpBadge).GetField("Amber") == null);
        Check("  and the button of the movement still blinks", cyl.ButtonA.AlertBlink);

        Console.WriteLine("--- the designer: Help properties ---");
        var props = TypeDescriptor.GetProperties(typeof(CylinderUnitHMI));
        Check("'Kind' is not offered: an axis on a page is an AxisUnitHMI", !props["Kind"].IsBrowsable &&
              props["Kind"].SerializationVisibility == DesignerSerializationVisibility.Hidden);
        Check("each movement's error and help files are under its action; valve and feedback head the list",
              props["ErrorAAddress"].Category.TrimStart('\t') == "Action A" && props["HelpAFiles"].Category.TrimStart('\t') == "Action A" &&
              props["ErrorBAddress"].Category.TrimStart('\t') == "Action B" && props["HelpBFiles"].Category.TrimStart('\t') == "Action B" &&
              props["ValveType"].Category.TrimStart('\t') == "Valve & Feedback" && props["FeedbackType"].Category.TrimStart('\t') == "Valve & Feedback",
              props["HelpAFiles"].Category + " / " + props["ValveType"].Category);
        Check("  the single HelpFiles of old pages is kept hidden (not written again)",
              !props["HelpFiles"].IsBrowsable && props["HelpFiles"].SerializationVisibility == DesignerSerializationVisibility.Hidden);
        Check("  no ' Help' category left", props.Cast<PropertyDescriptor>().All(p => !p.IsBrowsable || p.Category != " Help"));
        var ax = TypeDescriptor.GetProperties(typeof(AxisSettingHMI));
        Check("  and on the axis row", ax.Cast<PropertyDescriptor>().Where(p => p.Category.Trim() == "Help").All(p => p.Category == " Help"));

        var form = new Kaban.UI.Design.HelpFilesEditor.HelpFilesForm(new[] { "Unit_Clamp.html" });
        form.Add(new[] { "/work/Help/Clamp_Drawing.pdf", "/work/Help/Unit_Clamp.en.html", "/work/Help/Clamp_Photo.tr.png" });
        Check("HelpFiles is a list: adding keeps what was there", string.Join(";", form.Files) == "Unit_Clamp.html;Clamp_Drawing.pdf;Clamp_Photo.png",
              string.Join(";", form.Files));
        Check("  a file with a language is stored by its plain name, and not twice",
              Kaban.UI.Design.HelpFilesEditor.NameToStore(@"C:\x\Unit_Robot_Top_Clamping.en.html") == "Unit_Robot_Top_Clamping.html");
        form.Dispose();

        Console.WriteLine("--- condition SETS, and what a blocked control says ---");
        foreach (var n in new[] { "EnableConditions", "VisibilityConditions" })
        {
            var ed = (EditorAttribute)props[n].Attributes[typeof(EditorAttribute)];
            Check("CylinderUnitHMI." + n + " is ticked in a list, not one pick from a drop-down",
                  ed != null && ed.EditorTypeName.Contains("UnifiedConditionEditor"));
        }
        var gb = TypeDescriptor.GetProperties(typeof(GroupBoxHMI));
        Check("GroupBoxHMI's too", gb["EnableConditions"].Attributes[typeof(EditorAttribute)] != null &&
              gb["VisibilityConditions"].Attributes[typeof(EditorAttribute)] != null);
        var unmet = MachineConditionMap.Unmet(SystemCondition.SafetyOK | SystemCondition.ManualMode, c => c == SystemCondition.SafetyOK);
        Check("the names of the conditions NOT met - and only those", unmet.Count == 1 && unmet[0] == "Manual Mode Active", string.Join(",", unmet));
        SecurityManager.Login("op", SecurityLevel.Operator);
        var btn = new ButtonHMI { PlcNumber = 1, EnableConditions = SystemCondition.SafetyOK | SystemCondition.ManualMode };
        btn.UpdateState(a => 0, c => c == SystemCondition.None || c == SystemCondition.SafetyOK);
        var reason = (string)typeof(ButtonHMI).GetProperty("DisabledReasonOrDefault", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(btn, null);
        Check("a blocked button names the missing condition, not the whole set",
              reason.Contains("Manual Mode Active") && !reason.Contains("Safety Relays OK"), reason.Replace("\n", " | "));

        Console.WriteLine("--- a help PDF opens from a copy ---");
        var pdf = Path.Combine(TroubleshootingCatalog.HelpFolder, "MR_J4_TroubleShooting.pdf");
        var copy = (string)typeof(AlarmHelpLinks).GetMethod("ViewerCopy", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { pdf });
        Check("so a viewer holding it open never locks the program's own file",
              File.Exists(pdf) && copy != pdf && File.Exists(copy) && new FileInfo(copy).Length == new FileInfo(pdf).Length, copy);
        var page = Path.Combine(TroubleshootingCatalog.HelpFolder, "Cylinder_Troubleshooting.html");
        Check("  an HTML page opens where it is (it may link files beside it)",
              (string)typeof(AlarmHelpLinks).GetMethod("ViewerCopy", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { page }) == page);

        host.Close();
        Console.WriteLine(fail == 0 ? "\nUNITS AND DESIGNER ALL PASS" : "\nUNITS AND DESIGNER " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
