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
    /// <summary>A unit by name: a cylinder unit, or the unit inside a cylinder with an encoder (9 October).</summary>
    static CylinderUnitHMI Unit(Control page, string name) {
        var c = page.Controls.Find(name, true).FirstOrDefault();
        return c is CylinderEncoderUnitHMI ? ((CylinderEncoderUnitHMI)c).Unit : c as CylinderUnitHMI;
    }

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
        AlarmHelpLinks.Harvest(Page("ParamEncoders"));

        Console.WriteLine("--- the pages ---");
        var manual = new[] { "ManualEntry_Conveyor", "ManualRobot", "ManualProfile_Recognition", "ManualMilling", "ManualCutting", "ManualExit", "ManualOther", "ManualClamping_Moulds", "ManualYZ_Cross_Rollers" };
        // 9 October: the old Entry and Process pages two tabs each (Conveyor, Robot; Milling, Cutting)
        var settings = new[] { "AxisSettings", "ParamConveyor", "ParamRobot", "ParamMilling", "ParamCutting", "ParamEncoders", "ParamOther" };
        Check("every page of the old program is a page here", manual.Concat(settings).All(n => fa.GetTypes().Any(t => t.Name == n)));
        Check("the device-list pages it replaced are gone, and Entry and Process as one page each",
              new[] { "ParamEntry", "ParamProcess", "ParamEntry_Conveyor", "ParamExit_Conveyor", "ParamMachine_Lubrication", "ParamDistances_Beetwen_Units" }.All(n => !fa.GetTypes().Any(t => t.Name == n)));

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
            var lostEnc = All(page).OfType<CylinderEncoderUnitHMI>().Where(a => !a.IsResolved).Select(a => a.Name + " #" + a.EncoderNumber).ToList();
            Check("  every encoder unit finds its row on Encoders", lostEnc.Count == 0, string.Join(",", lostEnc));
            // (an inverter's unit shows its motor's value there - a speed, not a position: 14 October)
            var stray = All(page).OfType<CylinderUnitHMI>().Where(u => !(u.Parent is AxisUnitHMI) && !(u.Parent is CylinderEncoderUnitHMI) && !(u.Parent is CylinderRowHMI) &&
                                                                        !(u is InverterUnitHMI) && !string.IsNullOrEmpty(u.PositionAddress)).Select(u => u.Name).ToList();
            Check("  no cylinder unit reads a position itself (an encoder's, a pneumatic positioning's)", stray.Count == 0, string.Join(",", stray));
        }

        Console.WriteLine("--- Entry Conveyor ---");
        var entry = Page("ManualEntry_Conveyor");
        var rollers = Find<CylinderUnitHMI>(entry, "unitRoller_Up_Down");
        Check("Rollers Up/Down: Back M745 (the Rolls Down valve) with the down sensors M346, Forward M744 - the drawings' side, not the old page's",
              rollers.CommandAAddress == "M745" && rollers.CommandBAddress == "M744" && rollers.Lamp1AAddress == "M346" && string.IsNullOrEmpty(rollers.Lamp1BAddress),
              rollers.Lamp1AAddress + "/" + rollers.Lamp1BAddress);
        Check("  and its valve and feedback, added by hand, kept", rollers.ValveType == CylinderValve.DoubleSolenoid && rollers.FeedbackType == CylinderFeedback.SingleInductive);
        var pc7e = Find<CylinderEncoderUnitHMI>(entry, "unitProfile_Clamping_7");
        var pc7 = pc7e.Unit;
        Check("Profile Clamping 7: encoder 10 (Left Profile Recognition Front) - its encoder D4 in 0.1 mm, release M696, clamp M689, its title kept",
              pc7e.EncoderNumber == 10 && pc7.PositionAddress == "D4" && pc7.PositionDivisor == 10 && pc7.PositionDecimalPlaces == 1 &&
              pc7.CommandAAddress == "M696" && pc7.CommandBAddress == "M689" && pc7.TitleTextKey == "Profile Clamping 7");
        // 14 October: the motor, its run lamp and its inverter's error - one InverterUnitHMI
        var inv = Find<InverterUnitHMI>(entry, "unitConveyor_Motor");
        Check("the conveyor motor is an inverter's unit: its fault code D666 (not shown), its run lamp M203, its alarm bit M827",
              inv != null && inv.FaultCodeAddress == "D666" && inv.RunLampAddress == "M203" && inv.ErrorAddress == "M827" &&
              inv.CommandAAddress == "M640" && inv.CommandBAddress == "M639" && inv.Lamp1BAddress == "M221");
        Check("  no error display, no lamp of its own beside it", Find<Control>(entry, "dispConveyor_Inverter_Error") == null && Find<Control>(entry, "lampConveyor_Motor_Run") == null);
        Check("the rows in the old page's order", entry.Controls.Cast<Control>().Where(c => c.Left < 380 && c.Name.Length > 0).OrderBy(c => c.Top).Select(c => c.Name).Take(3).SequenceEqual(
              new[] { "unitConveyor_Motor", "lampConveyor_Limit_SN", "unitProfile_Clamping_1" }),
              string.Join(",", entry.Controls.Cast<Control>().Where(c => c.Left < 380 && c.Name.Length > 0).OrderBy(c => c.Top).Select(c => c.Name).Take(3)));

        Console.WriteLine("--- Robot ---");
        var robot = Page("ManualRobot");
        var x = Find<AxisUnitHMI>(robot, "unitAxis_X");
        Check("Robot X jogs as on the old pages: Back M604, Forward M605", x.Unit.CommandAAddress == "M604" && x.Unit.CommandBAddress == "M605",
              x.Unit.CommandAAddress + "/" + x.Unit.CommandBAddress);
        Check("  its reference lamp M330 on Forward, as on the old Axis Parameters page", x.Unit.Lamp1BAddress == "M330" && string.IsNullOrEmpty(x.Unit.Lamp1AAddress));
        // (14 October: the user set the axes' divisor to 10000 in the designer)
        Check("  its position W0 / 10000, as the user set it", x.Unit.PositionAddress == "W0" && x.Unit.PositionDivisor == 10000, x.Unit.PositionDivisor.ToString());
        var blow = Find<ButtonHMI>(robot, "btnSawdust_Blowing");
        Check("Sawdust Blowing M697 is on the Robot page (it was on Cutting)", blow != null && Bits(blow) == "M697:Momentary");
        Check("  and not on Cutting any more", !All(Page("ManualCutting")).OfType<ButtonHMI>().Any(b => b.BitActions.Any(a => a.Address == "M697")));
        var angle = Find<CylinderUnitHMI>(robot, "unitGripper_Angle");
        Check("Gripper Angle: 0 degrees (M752) with its sensor M262, 90 (M751) with M263 - the drawings' pairs", angle.Lamp1AAddress == "M262" && angle.Lamp1BAddress == "M263");

        var prFront = Unit(Page("ManualProfile_Recognition"), "unitFront_Clamping");
        Check("Profile Recognition Front Clamping, Forward: the pressure switch M240 the drawings wire (the old page: M348, no input)",
              prFront.Lamp1BAddress == "M240" && string.IsNullOrEmpty(prFront.Lamp2BAddress) && prFront.Lamp1AAddress == "M225" && prFront.TypeCheck == "OK", prFront.TypeCheck);

        Console.WriteLine("--- Milling ---");
        var milling = Page("ManualMilling");
        var mFront = Unit(milling, "unitFront_Clamping");
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
        var motor = Find<InverterUnitHMI>(milling, "unitUnit_1_Milling_Motor");
        Check("the milling motor (an inverter's unit, 14 October): M649 toggled, its lamp M230, its value D18 with its two implied decimals, fault D667",
              motor != null && motor.CommandAAddress == "M649" && motor.CommandAAction == BitActionType.Alternate && motor.Lamp1AAddress == "M230" &&
              motor.PositionAddress == "D18" && motor.PositionDivisor == 100 && motor.PositionDecimalPlaces == 2 && motor.FaultCodeAddress == "D667");
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
        var saw = Find<InverterUnitHMI>(cutting, "unitSaw_Motor");
        Check("the saw motor (an inverter's unit) has no lamp (M235 is the foil roll's sensor)", saw != null && saw.CommandAAddress == "M636" &&
              saw.CommandAAction == BitActionType.Alternate && string.IsNullOrEmpty(saw.Lamp1AAddress) && saw.FaultCodeAddress == "D669");
        // (14 October: the user put V Cutting in a group box of its own, the headings gone)
        var vGroup = All(cutting).OfType<GroupBoxHMI>().FirstOrDefault(g => g.TitleTextKey == "V Cutting");
        Check("'V Cutting' a group of its own, its axes and its saw in it", vGroup != null && All(vGroup).OfType<AxisUnitHMI>().Count() == 3 &&
              All(vGroup).OfType<InverterUnitHMI>().Any(u => u.FaultCodeAddress == "D670"));
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
        var top = Unit(exit, "unitRobot_Top_Clamping");
        Check("what was set on Robot Top Clamping in the designer is kept (error bits, help, valve, conditions) - from its encoder's row and itself",
              top.ErrorAAddress == "M854" && top.ErrorBAddress == "M840" && top.HelpAFiles == "c# learning structure.png" &&
              top.ValveType == CylinderValve.ThreeWayClosedCenter && top.VisibilityConditions == SystemCondition.ManualMode &&
              top.PositionAddress == "D17" && top.PositionDivisor == 10);

        Console.WriteLine("--- Clamping Moulds: the old page's columns, a pneumatic positioning a mould (9 October) ---");
        var moulds = Page("ManualClamping_Moulds");
        var first = Find<PneumaticPositioningHMI>(moulds, "ppLeft_Profile_Recognition_Front_Vertical");
        Check("the manual movement: Back M541, Forward M540, position D110 / 100 with 2 decimals (14 October: as its sections)", first.CommandBackAddress == "M541" && first.CommandForwardAddress == "M540" &&
              first.PositionAddress == "D110" && first.PositionDivisor == 100 && first.PositionDecimalPlaces == 2 &&
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
        var noValve = All(moulds).OfType<PneumaticPositioningHMI>().Where(r => r.ValveType != CylinderValve.ThreeWayClosedCenter).Select(r => r.Name).ToList();
        Check("the old valve and feedback of each mould kept", All(moulds).OfType<PneumaticPositioningHMI>().All(r => r.FeedbackType != CylinderFeedback.Unspecified) &&
              noValve.Count == 0, string.Join(",", noValve));

        Console.WriteLine("--- YZ Cross Rollers: the machine's three ---");
        var yz = Page("ManualYZ_Cross_Rollers");
        // (14 October: the user took the activation sensor's lamp off the page)
        Check("Profile Recognition, Milling, Cutting Right", string.Join(",", All(yz).OfType<CylinderUnitHMI>().Select(u => u.CommandAAddress).OrderBy(a => a)) == "M574,M576,M580",
              string.Join(",", All(yz).OfType<CylinderUnitHMI>().Select(u => u.CommandAAddress)));

        Console.WriteLine("--- Machine Parameters ---");
        var process = Page("ParamMilling");
        var cuttingP = Page("ParamCutting");
        Func<Control, string, NumericInputHMI> at = (p, a) => All(p).OfType<NumericInputHMI>().FirstOrDefault(n => n.Address == a);
        var d62 = at(process, "D62"); var d63 = at(process, "D63"); var d68 = at(cuttingP, "D68"); var d69 = at(cuttingP, "D69");
        Func<Control, bool> oneColumn = pg => All(pg).OfType<NumericInputHMI>().Select(n => n.Left).Distinct().Count() == 1 && !All(pg).OfType<LabelHMI>().Any();
        Check("Milling (11 + 2) and Cutting (9 + 4): one column each, no heading - the tab names it", oneColumn(process) && oneColumn(cuttingP) &&
              All(process).OfType<NumericInputHMI>().Count() == 13 && All(cuttingP).OfType<NumericInputHMI>().Count() == 13 &&
              at(process, "D674") != null && at(cuttingP, "D686") != null);
        Check("Tool Cooling Time D62 and Period D63, in seconds to 0.1", d62.DescriptionText == "Tool Cooling Time" && d63.DescriptionText == "Tool Cooling Period" && d62.Divisor == 10 && d62.Suffix == "s");
        Check("Saw Cooling Time D68 and Period D69 (they were swapped)", d68.DescriptionText == "Saw Cooling Time" && d69.DescriptionText == "Saw Cooling Period" && d69.Divisor == 10);
        Check("the cutting angles' targets in 0.00001 degree", at(cuttingP, "D620").Divisor == 100000 && at(cuttingP, "D622").Suffix == "°");
        var sawd = at(cuttingP, "D524");
        // (14 October: the user took its formula and bar view off; its limit stays)
        Check("Saw Diameter keeps what was set on it in the designer (its limit; no formula, no bar view - the user's)", string.IsNullOrEmpty(sawd.ReadFormula) && sawd.MaxLimit.StaticValue == 700 && !sawd.BarView,
              sawd.ReadFormula);
        Check("  and gets the old page's divisor and decimals", sawd.Divisor == 10000 && sawd.DecimalPlaces == 1);
        Check("Clampless Speed Override: one word, in %", at(process, "D510").DataType == DataTypeEnum.Bin_16 && at(process, "D510").Divisor == 1 && at(process, "D510").Suffix == "%");
        var entryP = Page("ParamConveyor");
        var robotP = Page("ParamRobot");
        Check("Conveyor: the clamping positions in 0.0001 mm (they were divided by 100)", at(entryP, "D452").Divisor == 10000 && at(entryP, "D464").Divisor == 10000);
        // 12 October: the encoder reference D516 is the Robot Gripper encoder row's Reference only
        Check("  no encoder reference D516 on Robot (the Encoders page's Robot Gripper row has it)", !All(robotP).OfType<NumericInputHMI>().Any(n => n.Address == "D516"));
        Check("Conveyor (8) and Robot (9 + 10, the right column under the left): one column each, no heading", oneColumn(entryP) && oneColumn(robotP) &&
              All(entryP).OfType<NumericInputHMI>().Count() == 8 && All(robotP).OfType<NumericInputHMI>().Count() == 19 &&
              All(robotP).OfType<NumericInputHMI>().OrderBy(n => n.Top).Last().Address == "D784");
        var other = Page("ParamOther");
        // (14 October: the user moved the machine's options to an Options page of their own, 20 of them)
        var optionsPage = Page("Options");
        var opts = All(optionsPage).OfType<CheckBoxHMI>().ToList();
        Check("Machine Options on the Options page: 20 switches, Mullion Notching L5 and the Clamping Mould option L27 among them; none left on Others",
              opts.Count == 20 && opts.Any(o => o.Address == "L5") && opts.Any(o => o.Address == "L27") && !All(other).OfType<CheckBoxHMI>().Any(),
              opts.Count + ": " + string.Join(",", opts.Select(o => o.Address)));
        Check("Middle Register Notch Parameters (D840...)", at(other, "D840") != null && at(other, "D850") != null);
        Check("Lubrication Period in minutes (/60)", at(other, "D485").Divisor == 60);
        Check("the parameters the machine no longer has are gone (D696, D698, D700, D548, D618)", new[] { "D696", "D698", "D700", "D548", "D618" }.All(a =>
              new[] { process, cuttingP, entryP, robotP, other }.All(pg => at(pg, a) == null)));
        var enc = Page("ParamEncoders");
        var encRows = CylinderEncoderSettingHMI.RowsOf(enc);
        Check("Encoders: 11 rows (CylinderEncoderSettingHMI), numbered 1-11; no Robot Position (Robot X's axis unit)", encRows.Count == 11 &&
              encRows.Select(r => r.EncoderNumber).SequenceEqual(Enumerable.Range(1, 11)) && !encRows.Any(r => r.CommandBackAddress == "M604"), string.Join(",", encRows.Select(r => r.TitleTextKey)));
        var grip = Find<CylinderEncoderSettingHMI>(enc, "encRobot_Gripper");
        Check("  the Robot Gripper jogs the gripper (M642/M641; the old page had the Exit's M657/M656)", grip.CommandBackAddress == "M642" && grip.CommandForwardAddress == "M641" && grip.PositionAddress == "D21");
        var prf = Find<CylinderEncoderSettingHMI>(enc, "encProfile_Recognition_Front");
        Check("  Profile Recognition Front: reference D477, measuring tolerance D479, minimum limit D483 (Start Limit), unclamping interval D51",
              prf.ReferenceAddress == "D477" && prf.MeasuringToleranceAddress == "D479" && prf.MinimumLimitAddress == "D483" && prf.UnclampingIntervalAddress == "D51" &&
              prf.ReferenceField.Divisor == 10 && prf.ReferenceField.DecimalPlaces == 1);
        Check("  its movement as the manual page had it: lamps M225 / M240, error bits M845 / M844, 5/3, back sensor + pressure switch",
              prf.BackSensorAddress == "M225" && prf.ForwardSensorAddress == "M240" && prf.ErrorBackAddress == "M845" && prf.ErrorForwardAddress == "M844" &&
              prf.ValveType == CylinderValve.ThreeWayClosedCenter && prf.FeedbackType == CylinderFeedback.MagneticPlusPressureSwitch && prf.TypeCheck == "OK", prf.TypeCheck);
        var leftTop = Find<CylinderEncoderSettingHMI>(enc, "encLeft_Profile_Recognition_Top");
        Check("  the left encoders' buttons as the manual pages have them: up M741 (with M345), down M740", leftTop.TitleTextKey == "Left Profile Recognition Top" &&
              leftTop.CommandBackAddress == "M741" && leftTop.BackSensorAddress == "M345" && leftTop.CommandForwardAddress == "M740");

        Console.WriteLine("--- Axis Parameters ---");
        var axes = Page("AxisSettings");
        var rows = All(axes).OfType<AxisSettingHMI>().OrderBy(r => r.Top).ToList();
        Check("the rows in the old page's order: Robot, Milling 1, Milling 2, Cutting, Exit Robot, V Cutting",
              string.Join(",", rows.Select(r => r.HMIAxisIndex)) == "1,2,3,5,6,7,4,9,10,11,8,13,12,16,14,15,17", string.Join(",", rows.Select(r => r.HMIAxisIndex)));
        var vcx = rows.Last();
        Check("V Cutting X: limits D830/D828, reference activation B16", vcx.MinLimitAddress == "D830" && vcx.MaxLimitAddress == "D828" && vcx.GoToReferenceAddress == "B16");
        // (14 October: the user set the positions' divisors - 10000 for the mm axes, 100000 for the angles)
        var scales = string.Join(",", rows.Select(r => r.HMIAxisIndex + ":" + r.PositionScale));
        Check("positions divided as the user set them: 10000 (mm), 100000 (angles)",
              rows.Where(r => r.Divisor == 10000 && r != vcx).All(r => r.PositionScale == 10000) &&
              rows.Where(r => r.Divisor == 100000).All(r => r.PositionScale == 100000), scales);
        var send = Find<ButtonHMI>(axes, "btnSend_Axes_To_Reference");
        Check("'Send Axes to Reference' (M22, SET) below the rows", send != null && Bits(send) == "M22:SET" && send.Top > rows.Last().Bottom);
        var axForm = new Form { ClientSize = new Size(1400, 900) };
        axes.Dock = DockStyle.Fill; axForm.Controls.Add(axes); axForm.Show(); Application.DoEvents();
        var pos = All(rows[0]).OfType<NumericDisplayHMI>().FirstOrDefault(n => n.Address == "W0");
        axForm.Close();
        Check("  the row's own position display divides as its row (Robot X: 10000)", pos != null && pos.Divisor == rows[0].PositionScale && pos.Divisor == 10000, pos == null ? "none" : pos.Divisor.ToString());

        Console.WriteLine("--- the captions in the user's language ---");
        LanguageManager.ChangeLanguage("TR");
        var form = new Form { ClientSize = new Size(900, 700) };
        var m2 = Page("ManualMilling"); m2.Dock = DockStyle.Fill; form.Controls.Add(m2); form.Show(); Application.DoEvents();
        Check("a heading shown in Turkish as the page opens (Unit 1: Ünite 1)", Find<LabelHMI>(m2, "lblUnit_1").Text == "Ünite 1", Find<LabelHMI>(m2, "lblUnit_1").Text);
        Check("a unit's title (Top Clamping: Üst Sıkma)", LanguageManager.GetString("Top Clamping", "") == "Üst Sıkma");
        Check("the tabs without 'Parameters' (Milling: Freze, Others: Diğerleri)", LanguageManager.GetString("Tab_ParamMilling", "") == "Freze" &&
              LanguageManager.GetString("Tab_ParamOther", "") == "Diğerleri");
        LanguageManager.ChangeLanguage("DE");
        Application.DoEvents();
        Check("and again when the language changes (Einheit 1)", Find<LabelHMI>(m2, "lblUnit_1").Text == "Einheit 1");
        LanguageManager.ChangeLanguage("EN");
        form.Close();

        Console.WriteLine(fail == 0 ? "\nLEGACY PAGES ALL PASS" : "\nLEGACY PAGES " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
