using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

// 8 October: "These are our current actively using HMI program manual movement
// pages... there are some page layout problems and... some device address
// mistakes. Can you correct them using these files" - and the Machine
// Parameters pages from the same program. tools/legacy_fa2010_pages.py builds
// the pages from the old designers (its --check compares them address by
// address); this test looks at what came out, as the running program sees it.
class LP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static System.Reflection.Assembly fa;
    static Control Page(string name) {
        var t = fa.GetTypes().First(x => x.Name == name);
        return (Control)Activator.CreateInstance(t);
    }
    static T Find<T>(Control page, string name) where T : Control {
        return page.Controls.Find(name, true).OfType<T>().FirstOrDefault();
    }
    static IEnumerable<Control> All(Control c) {
        foreach (Control k in c.Controls) { yield return k; foreach (var g in All(k)) yield return g; }
    }
    static string Bits(ButtonHMI b) { return string.Join(",", b.BitActions.Select(a => a.Address + ":" + a.Action)); }

    [STAThread]
    static void Main() {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "legacy_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        ViewPreferences.SettingsPath = System.IO.Path.Combine(dir, "View.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.LoadMachineTranslations("Translations.csv");
        LanguageManager.ChangeLanguage("EN");
        UnitManager.EnsureDefaults();
        fa = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly;
        AlarmHelpLinks.EnsureDiscovered();
        AlarmHelpLinks.Harvest(Page("AxisSettings"));

        Console.WriteLine("--- the pages ---");
        var manual = new[] { "ManualEntry_Conveyor", "ManualRobot", "ManualProfile_Recognition", "ManualMilling", "ManualCutting", "ManualExit", "ManualOther", "ManualClamping_Moulds", "ManualYZ_Cross_Rollers" };
        var settings = new[] { "AxisSettings", "ParamEntry", "ParamProcess", "ParamEncoders", "ParamOther" };
        Check("every page of the old program is a page here", manual.Concat(settings).All(n => fa.GetTypes().Any(t => t.Name == n)));
        Check("the device-list pages it replaces are gone",
              new[] { "ParamCutting", "ParamMilling", "ParamRobot", "ParamEntry_Conveyor", "ParamExit_Conveyor", "ParamMachine_Lubrication", "ParamDistances_Beetwen_Units" }.All(n => !fa.GetTypes().Any(t => t.Name == n)));

        Console.WriteLine("--- every page: nothing on top of anything else ---");
        foreach (var n in manual.Concat(settings.Skip(1))) {
            var page = Page(n);
            var tops = page.Controls.Cast<Control>().ToList();
            var overlaps = new List<string>();
            for (int i = 0; i < tops.Count; i++)
                for (int j = i + 1; j < tops.Count; j++)
                    if (tops[i].Bounds.IntersectsWith(tops[j].Bounds)) overlaps.Add(tops[i].Name + "/" + tops[j].Name);
            Check(n + ": " + tops.Count + " controls, none overlapping", overlaps.Count == 0, string.Join(" ", overlaps.Take(4)));
            var unbound = All(page).OfType<CylinderUnitHMI>().Where(u => !(u.Parent is AxisUnitHMI) && string.IsNullOrEmpty(u.CommandAAddress) && string.IsNullOrEmpty(u.CommandBAddress)).Select(u => u.Name).ToList();
            Check("  every unit moves something", unbound.Count == 0, string.Join(",", unbound));
            var lost = All(page).OfType<AxisUnitHMI>().Where(a => !a.IsResolved).Select(a => a.Name + " #" + a.AxisNumber).ToList();
            Check("  every axis unit finds its row on Axis Parameters", lost.Count == 0, string.Join(",", lost));
        }

        Console.WriteLine("--- Entry Conveyor ---");
        var entry = Page("ManualEntry_Conveyor");
        var rollers = Find<CylinderUnitHMI>(entry, "unitRoller_Up_Down");
        Check("Rollers Up/Down: Back M745 (the Rolls Down valve) with the down sensors M346, Forward M744 - the drawings' side, not the old page's",
              rollers.CommandAAddress == "M745" && rollers.CommandBAddress == "M744" && rollers.Lamp1AAddress == "M346" && string.IsNullOrEmpty(rollers.Lamp1BAddress),
              rollers.Lamp1AAddress + "/" + rollers.Lamp1BAddress);
        Check("  and its valve and feedback, added by hand, kept", rollers.ValveType == CylinderValve.DoubleSolenoid && rollers.FeedbackType == CylinderFeedback.SingleInductive);
        var pc7 = Find<CylinderUnitHMI>(entry, "unitProfile_Clamping_7");
        Check("Profile Clamping 7: its encoder D4 in 0.1 mm (the divisor was 0)", pc7.PositionAddress == "D4" && pc7.PositionDivisor == 10 && pc7.PositionDecimalPlaces == 1);
        var inv = Find<NumericDisplayHMI>(entry, "dispConveyor_Inverter_Error");
        Check("the inverter's error in hexadecimal, as the old page showed it", inv != null && inv.Address == "D666" && inv.DataFormat == DisplayFormatEnum.Hexadecimal);
        Check("the rows in the old page's order", entry.Controls.Cast<Control>().Where(c => c.Left < 380 && c.Name.Length > 0).OrderBy(c => c.Top).Select(c => c.Name).Take(5).SequenceEqual(
              new[] { "unitConveyor_Motor", "lampConveyor_Motor_Run", "dispConveyor_Inverter_Error", "lampConveyor_Limit_SN", "unitProfile_Clampings" }),
              string.Join(",", entry.Controls.Cast<Control>().Where(c => c.Left < 380 && c.Name.Length > 0).OrderBy(c => c.Top).Select(c => c.Name).Take(5)));

        Console.WriteLine("--- Robot ---");
        var robot = Page("ManualRobot");
        var x = Find<AxisUnitHMI>(robot, "unitAxis_X");
        Check("Robot X jogs as on the old pages: Back M604, Forward M605", x.Unit.CommandAAddress == "M604" && x.Unit.CommandBAddress == "M605",
              x.Unit.CommandAAddress + "/" + x.Unit.CommandBAddress);
        Check("  its reference lamp M330 on Forward, as on the old Axis Parameters page", x.Unit.Lamp1BAddress == "M330" && string.IsNullOrEmpty(x.Unit.Lamp1AAddress));
        Check("  its position W0 in 0.001 mm (the old pages divide it by 1000; the limits stay 10000)", x.Unit.PositionAddress == "W0" && x.Unit.PositionDivisor == 1000, x.Unit.PositionDivisor.ToString());
        var blow = Find<ButtonHMI>(robot, "btnSawdust_Blowing");
        Check("Sawdust Blowing M697 is on the Robot page (it was on Cutting)", blow != null && Bits(blow) == "M697:Momentary");
        Check("  and not on Cutting any more", !All(Page("ManualCutting")).OfType<ButtonHMI>().Any(b => b.BitActions.Any(a => a.Address == "M697")));
        var angle = Find<CylinderUnitHMI>(robot, "unitGripper_Angle");
        Check("Gripper Angle: 0 degrees (M752) with its sensor M262, 90 (M751) with M263 - the drawings' pairs", angle.Lamp1AAddress == "M262" && angle.Lamp1BAddress == "M263");

        var prFront = Find<CylinderUnitHMI>(Page("ManualProfile_Recognition"), "unitFront_Clamping");
        Check("Profile Recognition Front Clamping, Forward: the pressure switch M240 the drawings wire (the old page: M348, no input)",
              prFront.Lamp1BAddress == "M240" && string.IsNullOrEmpty(prFront.Lamp2BAddress) && prFront.Lamp1AAddress == "M225" && prFront.TypeCheck == "OK", prFront.TypeCheck);

        Console.WriteLine("--- Milling ---");
        var milling = Page("ManualMilling");
        var mFront = Find<CylinderUnitHMI>(milling, "unitFront_Clamping");
        Check("Milling Front Clamping, Forward: the pressure switch M243 (the old page: M349)", mFront.Lamp1BAddress == "M243" && string.IsNullOrEmpty(mFront.Lamp2BAddress));
        var mRoller = Find<CylinderUnitHMI>(milling, "unitRoller_Up_Down");
        Check("the milling roller: no lamp (the old page's M229 reads X55, wired to nothing)", string.IsNullOrEmpty(mRoller.Lamp1AAddress) && string.IsNullOrEmpty(mRoller.Lamp1BAddress) &&
              mRoller.FeedbackType == CylinderFeedback.NoSensor);
        var u1 = Find<LabelHMI>(milling, "lblUnit_1"); var u2 = Find<LabelHMI>(milling, "lblUnit_2");
        Check("Unit 1 and Unit 2 side by side, each under its heading", u1 != null && u2 != null && u1.Top == u2.Top && u1.Left < u2.Left);
        var drills = Find<GroupBoxHMI>(milling, "grpDrills_to_Changing_Pos");
        Check("Drills to Changing Pos.: buttons 1-4 (M176-M179, SET) in one group",
              drills != null && string.Join(",", drills.Controls.OfType<ButtonHMI>().OrderBy(b => b.Left).Select(Bits)) == "M176:SET,M177:SET,M178:SET,M179:SET");
        var deg = Find<GroupBoxHMI>(milling, "grpUnit_2_Change_Degree");
        Check("Unit 2's Change Degree: 0/90/180/270 on M677-M680", deg != null && string.Join(",", deg.Controls.OfType<ButtonHMI>().OrderBy(b => b.Left).Select(b => b.BitActions[0].Address)) == "M677,M678,M679,M680");
        var motor = Find<NumericDisplayHMI>(milling, "btnUnit_1_Milling_Motor_Value");
        Check("the milling motor's value D18 with its two implied decimals", motor != null && motor.Address == "D18" && motor.Divisor == 100 && motor.DecimalPlaces == 2);
        Check("no 'Front Height Setting' (M748/M747 is the Mullion End Milling Unit now)", Find<Control>(milling, "unitFront_Height_Setting") == null);

        Console.WriteLine("--- Cutting ---");
        var cutting = Page("ManualCutting");
        var vx = All(cutting).OfType<AxisUnitHMI>().FirstOrDefault(a => a.AxisNumber == 17);
        Check("V Cutting X (new): position D838, Back M631, Forward M630, sensor M269", vx != null && vx.IsResolved && vx.Unit.PositionAddress == "D838" &&
              vx.Unit.CommandAAddress == "M631" && vx.Unit.CommandBAddress == "M630" && vx.Unit.Lamp1BAddress == "M269");
        var vz = All(cutting).OfType<AxisUnitHMI>().First(a => a.AxisNumber == 15);
        Check("V Cutting Z's reference sensor M259 (it lit the Exit Robot's M258)", vz.Unit.Lamp1BAddress == "M259");
        var hap = Find<ButtonHMI>(cutting, "btnHigh_Air_Pressure");
        Check("Cutting's High Air Pressure is momentary there (milling's toggles)", Bits(hap) == "M651:Momentary" && Bits(Find<ButtonHMI>(milling, "btnHigh_Air_Pressure")) == "M716:Alternate");
        var saw = Find<ButtonHMI>(cutting, "btnSaw_Motor");
        Check("the saw motor has no lamp (M235 is the foil roll's sensor)", Bits(saw) == "M636:Alternate" && string.IsNullOrEmpty(saw.Lamp1Address));
        Check("'Cutting' and 'V Cutting' headings", Find<LabelHMI>(cutting, "lblCutting") != null && Find<LabelHMI>(cutting, "lblV_Cutting") != null);
        var mull = Find<CylinderUnitHMI>(cutting, "unitMullion_End_Milling_Unit");
        Check("Mullion End Milling Unit: down M748 with SN106 (M271), up M747 with SN105 (M270), 5/2 with a sensor at each end",
              mull.CommandAAddress == "M748" && mull.Lamp1AAddress == "M271" && mull.CommandBAddress == "M747" && mull.Lamp1BAddress == "M270" &&
              mull.ValveType == CylinderValve.DoubleSolenoid && mull.FeedbackType == CylinderFeedback.TwoEndMagnetic && mull.TypeCheck == "OK", mull.TypeCheck);
        var blade = Find<ButtonHMI>(cutting, "btnMullion_Blade_Change");
        Check("Mullion Blade Change: M749, its down sensor M273 as the lamp", Bits(blade) == "M749:Momentary" && blade.Lamp1Address == "M273");

        Console.WriteLine("--- Exit ---");
        var exit = Page("ManualExit");
        var ex = Find<AxisUnitHMI>(exit, "unitRobot_Movement");
        Check("Exit Robot: Back M731, Forward M732 (they were the other way round), sensor M343", ex.Unit.CommandAAddress == "M731" && ex.Unit.CommandBAddress == "M732" && ex.Unit.Lamp1BAddress == "M343");
        var top = Find<CylinderUnitHMI>(exit, "unitRobot_Top_Clamping");
        Check("what was set on Robot Top Clamping in the designer is kept (error bits, help, valve, conditions)",
              top.ErrorAAddress == "M854" && top.ErrorBAddress == "M840" && top.HelpAFiles == "c# learning structure.png" &&
              top.ValveType == CylinderValve.ThreeWayClosedCenter && top.VisibilityConditions == SystemCondition.ManualMode &&
              top.PositionAddress == "D17" && top.PositionDivisor == 10);

        Console.WriteLine("--- Clamping Moulds: the old page's columns, a pneumatic positioning a mould (9 October) ---");
        var moulds = Page("ManualClamping_Moulds");
        var first = Find<PneumaticPositioningHMI>(moulds, "ppLeft_Profile_Recognition_Front_Vertical");
        Check("the manual movement: Back M541, Forward M540, position D110 / 10 with 2 decimals", first.CommandBackAddress == "M541" && first.CommandForwardAddress == "M540" &&
              first.PositionAddress == "D110" && first.PositionDivisor == 10 && first.PositionDecimalPlaces == 2 &&
              first.Unit.CommandAAddress == "M541" && first.Unit.PositionAddress == "D110");
        var target = first.TargetField;
        Check("Target Position D150 (/100, 2 decimals), for the operator", target.Address == "D150" && target.Divisor == 100 && target.DecimalPlaces == 2 && target.RequiredSecurity == SecurityLevel.Operator);
        Check("Send To Position M50 (SET)", Bits(first.StartButton) == "M50:SET");
        Check("Calibration Pos. D130, Calibrate M30 (SET) - Maintenance", first.CalibrationField.Address == "D130" &&
              Bits(first.CalibrateButton) == "M30:SET" && first.CalibrateButton.RequiredSecurity == SecurityLevel.Maintenance);
        Check("Tolerance D750, Stopping Offset (direction ref) D790", first.ToleranceField.Address == "D750" && first.DirectionRefField.Address == "D790");
        Check("16 moulds, the last the Right Front Vertical Clamping (M585/M584, D128)", All(moulds).OfType<PneumaticPositioningHMI>().Count() == 16 &&
              Find<PneumaticPositioningHMI>(moulds, "ppRight_Front_Vertical_Clamping").CommandBackAddress == "M585");
        Check("the Cutting/Exit small clampings are not moulds", !All(moulds).OfType<CylinderUnitHMI>().Any(u => u.CommandAAddress == "M699" || u.CommandAAddress == "M701"));
        Check("the old valve and feedback of each mould kept", All(moulds).OfType<PneumaticPositioningHMI>().All(r => r.FeedbackType != CylinderFeedback.Unspecified &&
              r.ValveType == CylinderValve.ThreeWayClosedCenter));

        Console.WriteLine("--- YZ Cross Rollers: the machine's three ---");
        var yz = Page("ManualYZ_Cross_Rollers");
        Check("Profile Recognition, Milling, Cutting Right and the activation sensor", string.Join(",", All(yz).OfType<CylinderUnitHMI>().Select(u => u.CommandAAddress)) == "M574,M576,M580" &&
              All(yz).OfType<LampHMI>().Single().Address == "M265");

        Console.WriteLine("--- Machine Parameters ---");
        var process = Page("ParamProcess");
        Func<Control, string, NumericInputHMI> at = (p, a) => All(p).OfType<NumericInputHMI>().FirstOrDefault(n => n.Address == a);
        var d62 = at(process, "D62"); var d63 = at(process, "D63"); var d68 = at(process, "D68"); var d69 = at(process, "D69");
        Check("Tool Cooling Time D62 and Period D63, in seconds to 0.1", d62.DescriptionText == "Tool Cooling Time" && d63.DescriptionText == "Tool Cooling Period" && d62.Divisor == 10 && d62.Suffix == "s");
        Check("Saw Cooling Time D68 and Period D69 (they were swapped)", d68.DescriptionText == "Saw Cooling Time" && d69.DescriptionText == "Saw Cooling Period" && d69.Divisor == 10);
        Check("the cutting angles' targets in 0.00001 degree", at(process, "D620").Divisor == 100000 && at(process, "D622").Suffix == "°");
        var sawd = at(process, "D524");
        Check("Saw Diameter keeps what was set on it in the designer (formulas, limits, bar view)", sawd.ReadFormula == "(a/10000)+1" && sawd.MaxLimit.StaticValue == 700 && sawd.BarView,
              sawd.ReadFormula);
        Check("  and gets the old page's divisor and decimals", sawd.Divisor == 10000 && sawd.DecimalPlaces == 1);
        Check("Clampless Speed Override: one word, in %", at(process, "D510").DataType == DataTypeEnum.Bin_16 && at(process, "D510").Divisor == 1 && at(process, "D510").Suffix == "%");
        var entryP = Page("ParamEntry");
        Check("Entry: the clamping positions in 0.0001 mm (they were divided by 100)", at(entryP, "D452").Divisor == 10000 && at(entryP, "D464").Divisor == 10000);
        Check("  the encoder reference D516 one word / 10", at(entryP, "D516").DataType == DataTypeEnum.Bin_16 && at(entryP, "D516").Divisor == 10);
        var other = Page("ParamOther");
        var opts = All(other).OfType<CheckBoxHMI>().ToList();
        Check("Machine Options: 16 switches, Mullion Notching L5 the first", opts.Count == 16 && opts.OrderBy(o => o.Top).First().Address == "L5" && opts.All(o => o.RequiredSecurity == SecurityLevel.Maintenance));
        Check("Middle Register Notch Parameters (D840...)", at(other, "D840") != null && at(other, "D850") != null);
        Check("Lubrication Period in minutes (/60)", at(other, "D485").Divisor == 60);
        Check("the parameters the machine no longer has are gone (D696, D698, D700, D548, D618)", new[] { "D696", "D698", "D700", "D548", "D618" }.All(a => at(process, a) == null && at(entryP, a) == null && at(other, a) == null));
        var enc = Page("ParamEncoders");
        var grip = Find<CylinderUnitHMI>(enc, "encRobot_Gripper");
        Check("Encoders: the Robot Gripper jogs the gripper (M642/M641; the old page had the Exit's M657/M656)", grip.CommandAAddress == "M642" && grip.CommandBAddress == "M641" && grip.PositionAddress == "D21");
        var prf = Find<CylinderUnitHMI>(enc, "encProfile_Recognition_Front");
        Check("  Profile Recognition Front: reference D477, tolerance D479, start limit D483, unclamping D51",
              new[] { "Reference", "Tolerance", "Start_Limit", "Unclamping_Interval" }.Select(c => Find<NumericInputHMI>(enc, "numProfile_Recognition_Front_" + c).Address).SequenceEqual(new[] { "D477", "D479", "D483", "D51" }));

        Console.WriteLine("--- Axis Parameters ---");
        var axes = Page("AxisSettings");
        var rows = All(axes).OfType<AxisSettingHMI>().OrderBy(r => r.Top).ToList();
        Check("the rows in the old page's order: Robot, Milling 1, Milling 2, Cutting, Exit Robot, V Cutting",
              string.Join(",", rows.Select(r => r.HMIAxisIndex)) == "1,2,3,5,6,7,4,9,10,11,8,13,12,16,14,15,17", string.Join(",", rows.Select(r => r.HMIAxisIndex)));
        var vcx = rows.Last();
        Check("V Cutting X: limits D830/D828, reference activation B16", vcx.MinLimitAddress == "D830" && vcx.MaxLimitAddress == "D828" && vcx.GoToReferenceAddress == "B16");
        Check("positions divided by 1000 (mm) and 10000 (angles); V Cutting X's D838 by its 10000",
              rows.Where(r => r.Divisor == 10000 && r != vcx).All(r => r.PositionScale == 1000) && rows.Where(r => r.Divisor == 100000).All(r => r.PositionScale == 10000) && vcx.PositionScale == 10000);
        var send = Find<ButtonHMI>(axes, "btnSend_Axes_To_Reference");
        Check("'Send Axes to Reference' (M22, SET) below the rows", send != null && Bits(send) == "M22:SET" && send.Top > rows.Last().Bottom);
        var axForm = new Form { ClientSize = new Size(1400, 900) };
        axes.Dock = DockStyle.Fill; axForm.Controls.Add(axes); axForm.Show(); Application.DoEvents();
        var pos = All(rows[0]).OfType<NumericDisplayHMI>().FirstOrDefault(n => n.Address == "W0");
        axForm.Close();
        Check("  the row's own position display divides by 1000", pos != null && pos.Divisor == 1000, pos == null ? "none" : pos.Divisor.ToString());

        Console.WriteLine("--- the captions in the user's language ---");
        LanguageManager.ChangeLanguage("TR");
        var form = new Form { ClientSize = new Size(900, 700) };
        var m2 = Page("ManualMilling"); m2.Dock = DockStyle.Fill; form.Controls.Add(m2); form.Show(); Application.DoEvents();
        Check("a heading shown in Turkish as the page opens (Unit 1: Ünite 1)", Find<LabelHMI>(m2, "lblUnit_1").Text == "Ünite 1", Find<LabelHMI>(m2, "lblUnit_1").Text);
        Check("a unit's title (Top Clamping: Üst Sıkma)", LanguageManager.GetString("Top Clamping", "") == "Üst Sıkma");
        Check("the new tabs (Process Parameters: Operasyon Parametreleri)", LanguageManager.GetString("Tab_ParamProcess", "") == "Operasyon Parametreleri");
        LanguageManager.ChangeLanguage("DE");
        Application.DoEvents();
        Check("and again when the language changes (Einheit 1)", Find<LabelHMI>(m2, "lblUnit_1").Text == "Einheit 1");
        LanguageManager.ChangeLanguage("EN");
        form.Close();

        Console.WriteLine(fail == 0 ? "\nLEGACY PAGES ALL PASS" : "\nLEGACY PAGES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
