using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.Drivers.Simulation;
using Kaban.UI.Controls;

// 14 October: "We need EnableCondition and EnableTrigger for each AxisSettingHMI button (just like
// CylinderUnitHMI) working with AxisUnitHMI too. CylinderUnitHMI's visibility condition only hides the
// buttons inside, the border and the description stay - it should hide the whole control. AxisSettingHMI's
// visibility condition doesn't work for AxisUnitHMI. And CheckBoxHMI should have visibility condition,
// enabling condition and trigger as well (just like ButtonHMI)."
class CN {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static object Get(object o, string n) { for (var t = o.GetType(); t != null; t = t.BaseType) { var f = t.GetField(n, Any); if (f != null) return f.GetValue(o); var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(o, null); } return null; }
    static bool Soft(ButtonHMI b) { return (bool)Get(b, "_softEnabled"); }

    [STAThread]
    static void Main() {
        AuditLogger.LogDirectory = Path.Combine(Path.GetTempPath(), "cn_" + Guid.NewGuid().ToString("N"));
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        UnitManager.EnsureDefaults();
        PlcManager.Instance.RegisterDriver(1, new SimulationDriver(1));
        var sim = (SimulationDriver)PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(1));
        SecurityManager.Login("admin", SecurityLevel.Admin);
        MachineConditionMap.Clear();
        MachineConditionMap.Register(SystemCondition.ManualMode, 1, "M100", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 0);
        var optA = (SystemCondition)(1L << 16);
        MachineConditionMap.RegisterMachineBit(1L << 16, "T.A", "Option A", 1, "L27", DataTypeEnum.Bin_16, ComparisonOperator.Equal, 1);

        var form = new Form { ClientSize = new Size(1500, 500), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var page = new UserControl { Dock = DockStyle.Fill };
        form.Controls.Add(page);
        var cyl = new CylinderUnitHMI { Name = "cyl", TitleTextKey = "Clamp", CommandAAddress = "M30", CommandBAddress = "M31", VisibilityConditions = optA, Location = new Point(10, 10) };
        page.Controls.Add(cyl);
        var cb = new CheckBoxHMI { Name = "cb", Address = "M40", DescriptionText = "Option", VisibilityConditions = optA, EnableConditions = SystemCondition.ManualMode,
                                   Location = new Point(500, 10) };
        cb.EnableTrigger = new PlcTrigger { TriggerType = TriggerType.ON, MonitorAddress = "M41" };
        page.Controls.Add(cb);
        var row = new AxisSettingHMI { Name = "ax", HMIAxisIndex = 7, PlcNumber = 1, AxisNameKey = "Test X", CommandBackAddress = "M50", CommandForwardAddress = "M51",
                                       PositionAddress = "D50", CalibrateAddress = "M52", VisibilityConditions = optA, EnableConditions = SystemCondition.ManualMode,
                                       ForwardEnableConditions = optA, Location = new Point(10, 120) };
        row.BackEnableTrigger = new PlcTrigger { TriggerType = TriggerType.ON, MonitorAddress = "M53" };
        page.Controls.Add(row);
        var scanner = new Kaban.UI.Plc.PlcScanner(page);
        scanner.RefreshTags();
        form.Show(); Pump(500);
        row.BuildRow(); scanner.RefreshTags(); Pump(300);

        Console.WriteLine("--- CylinderUnitHMI: the whole unit follows its visibility conditions ---");
        sim.Write("M100", 0); sim.Write("L27", 0); sim.Write("M41", 1); sim.Write("M53", 1); Pump(800);
        Check("Option A off: the unit hidden - frame, title and all (it hid only its buttons)", !cyl.Visible && cyl.HiddenByConditions);
        sim.Write("L27", 1); Pump(800);
        Check("  on: shown again, its buttons with it", cyl.Visible && ((Control)Get(cyl, "_btnA")).Visible);
        var plain = new CylinderUnitHMI { CommandAAddress = "M32" };
        plain.Visible = false;
        plain.UpdateState(a => 0, c => false);
        Check("  a unit without conditions is left as its page has it", !plain.Visible && !plain.HiddenByConditions);

        Console.WriteLine("--- CheckBoxHMI: visibility, enable conditions and trigger, as a ButtonHMI's ---");
        Check("the properties, in the designer's Conditions", TypeDescriptorHas(typeof(CheckBoxHMI), "VisibilityConditions") &&
              TypeDescriptorHas(typeof(CheckBoxHMI), "EnableConditions") && TypeDescriptorHas(typeof(CheckBoxHMI), "EnableTrigger"));
        sim.Write("L27", 1); sim.Write("M100", 0); sim.Write("M41", 1); Pump(800);
        Check("Option A on, manual, M41 on: shown and can be ticked", cb.Visible && cb.CanTick);
        sim.Write("M100", 1); Pump(800);
        Check("  not in manual: greyed, cannot be ticked", cb.Visible && !cb.CanTick);
        Check("  its tooltip says what it waits for", cb.TooltipTextForTests().Contains("Manual Mode Active"), cb.TooltipTextForTests());
        sim.Write("M40", 0); Pump(400);
        typeof(CheckBox).GetMethod("OnClick", Any).Invoke(cb, new object[] { EventArgs.Empty }); Pump(300);
        Check("  a click writes nothing", sim.Read("M40") == 0 && !cb.Checked);
        sim.Write("M100", 0); sim.Write("M41", 0); Pump(800);
        Check("  the trigger M41 off: cannot be ticked, waits for the signal", !cb.CanTick && cb.TooltipTextForTests().Contains("M41"), cb.TooltipTextForTests());
        sim.Write("M41", 1); Pump(800);
        Check("  M41 on: can", cb.CanTick);
        sim.Write("L27", 0); Pump(800);
        Check("  Option A off: hidden", !cb.Visible);

        // 2 November: "We should add 'ConfirmWrite' property to 'CheckBoxHMI' controls ... Is it possible if the confirming requirement
        // gets cancelled when the checkbox value changed dragging to left to turn OFF or right to turn ON"
        Console.WriteLine("--- CheckBoxHMI.ConfirmWrite: a tap asks, a slide does not ---");
        var confirmProp = System.ComponentModel.TypeDescriptor.GetProperties(typeof(CheckBoxHMI))["ConfirmWrite"];
        Check("ConfirmWrite: a property of its own, off by default, in the designer's Security part", confirmProp != null && confirmProp.IsBrowsable &&
              confirmProp.Category.TrimStart('\t') == "Security" && !new CheckBoxHMI().ConfirmWrite, confirmProp == null ? "none" : confirmProp.Category);
        var sw = new CheckBoxHMI { Name = "sw", Address = "M60", DescriptionText = "Cooling", ConfirmWrite = true, Location = new Point(500, 300), Size = new Size(400, 40) };
        page.Controls.Add(sw); scanner.RefreshTags(); sim.Write("M60", 0); Pump(600);
        int asked = 0; bool answer = false, askedOn = false;
        var confirmWas = CheckBoxHMI.Confirm;
        CheckBoxHMI.Confirm = (b, on) => { asked++; askedOn = on; return answer; };
        Action<string, object[]> call = (m, a) => typeof(CheckBoxHMI).GetMethod(m, Any).Invoke(sw, a);
        Func<int, int, MouseEventArgs> at = (x, y) => new MouseEventArgs(MouseButtons.Left, 1, x, y, 0);
        // a press, moves, a lift and its click - in either order the framework may give them
        Action<int[], bool> gesture = (xs, clickFirst) =>
        {
            call("OnMouseDown", new object[] { at(xs[0], 20) });
            for (int i = 1; i < xs.Length; i++) call("OnMouseMove", new object[] { at(xs[i], 20) });
            if (clickFirst) { call("OnClick", new object[] { EventArgs.Empty }); call("OnMouseUp", new object[] { at(xs[xs.Length - 1], 20) }); }
            else { call("OnMouseUp", new object[] { at(xs[xs.Length - 1], 20) }); call("OnClick", new object[] { EventArgs.Empty }); }
            Pump(300);
        };
        gesture(new[] { 360 }, false);
        Check("a tap: asked first (Switch ON?) - answered No: nothing written", asked == 1 && askedOn && sim.Read("M60") == 0 && !sw.Checked && sw.LastChange == "refused");
        answer = true;
        gesture(new[] { 360 }, true);
        Check("  answered Yes: ON, written", asked == 2 && sw.Checked && sim.Read("M60") == 1 && sw.LastChange == "tap");
        gesture(new[] { 370, 355, 330, 300 }, false);
        Check("slid to the left: OFF at once, nothing asked", asked == 2 && !sw.Checked && sim.Read("M60") == 0 && sw.LastChange == "slide");
        gesture(new[] { 300, 320, 340, 360 }, true);
        Check("  slid to the right: ON at once, nothing asked (the lift's click changes nothing more)", asked == 2 && sw.Checked && sim.Read("M60") == 1);
        gesture(new[] { 300, 340, 380 }, false);
        Check("  slid to the right while ON: stays ON, nothing asked", asked == 2 && sw.Checked && sim.Read("M60") == 1);
        gesture(new[] { 360, 364, 366 }, false);
        Check("  moved a little (less than a slide): a tap - asked", asked == 3 && !askedOn && !sw.Checked && sim.Read("M60") == 0);
        Check("  a sideways drag on it is its own (no page scrolls under a switch that can change)",
              ((Kaban.UI.Helpers.TouchInput.ISwipes)sw).SwipesNow && !((Kaban.UI.Helpers.TouchInput.ISwipes)new CheckBoxHMI { Look = CheckBoxLook.Box, Address = "M1" }).SwipesNow);
        sw.ConfirmWrite = false; answer = false;
        gesture(new[] { 360 }, false);
        Check("ConfirmWrite off: a tap changes it at once", asked == 3 && sw.Checked && sim.Read("M60") == 1);
        CheckBoxHMI.Confirm = confirmWas;

        // 2 November: "this checkbox HMI control render (making the description area as the control area as well so the click or
        // hovering on there acts same as the control itself) could nice to use at the numerical controls as well. Is it possible to add
        // another property to make numerical and binary controls act these two ways according to the selection of the property"
        Console.WriteLine("--- ActiveArea: the row, or the control alone ---");
        var areaProp = System.ComponentModel.TypeDescriptor.GetProperties(typeof(NumericInputHMI))["ActiveArea"];
        Check("a property of both, Row by default", areaProp != null && areaProp.IsBrowsable && new NumericInputHMI().ActiveArea == ActiveArea.Row &&
              new CheckBoxHMI().ActiveArea == ActiveArea.Row && System.ComponentModel.TypeDescriptor.GetProperties(typeof(CheckBoxHMI))["ActiveArea"] != null);
        var num = new NumericInputHMI { Name = "num", Address = "D70", DescriptionText = "Saw Speed", Location = new Point(1200, 380), Size = new Size(116, 26),
                                        RequiredSecurity = SecurityLevel.Operator };
        page.Controls.Add(num); scanner.RefreshTags(); Pump(500);
        var numCaption = page.Controls.OfType<Label>().First(l => l.Text.StartsWith("Saw Speed"));
        Action<Control, string> raise = (c, m) => typeof(Control).GetMethod(m, Any).Invoke(c, new object[] { m == "OnMouseDown" ? (object)new MouseEventArgs(MouseButtons.Left, 1, 4, 4, 0) : EventArgs.Empty });
        int keypads = 0;
        var closer = new Timer { Interval = 100 };
        closer.Tick += (s, e) => { foreach (Form o in Application.OpenForms.Cast<Form>().ToList()) if (o is Kaban.UI.Forms.NumpadForm) { keypads++; o.DialogResult = DialogResult.Cancel; o.Close(); } };
        closer.Start();
        raise(numCaption, "OnMouseEnter"); Pump(50);
        bool litOnCaption = numCaption.BackColor == Color.FromArgb(236, 242, 250);
        raise(numCaption, "OnMouseLeave"); Pump(50);
        bool darkAgain = numCaption.BackColor == Color.Transparent;
        raise(num, "OnMouseEnter"); Pump(50);
        bool litFromField = numCaption.BackColor == Color.FromArgb(236, 242, 250);
        raise(num, "OnMouseLeave"); Pump(50);
        Check("Row: the pointer on the caption lights it (and the field's border), off it - dark; on the field - the caption lit too",
              litOnCaption && darkAgain && litFromField && numCaption.BackColor == Color.Transparent && numCaption.Cursor == Cursors.Hand);
        raise(numCaption, "OnClick"); Pump(400);
        Check("  a click on the caption opens the keypad, as one on the field", keypads == 1, keypads + " keypads");
        num.ActiveArea = ActiveArea.Control; Pump(50);
        raise(numCaption, "OnMouseEnter"); Pump(50);
        bool stillDark = numCaption.BackColor == Color.Transparent;
        raise(numCaption, "OnMouseLeave");
        raise(numCaption, "OnClick"); Pump(400);
        Check("Control: the caption is only words - not lit, its click opens nothing", stillDark && keypads == 1 && numCaption.Cursor == Cursors.Default, keypads + " keypads");
        closer.Stop();

        // the switch: a row (its caption in it) - Control: only the switch itself
        sw.ActiveArea = ActiveArea.Control; Pump(50);
        sim.Write("M60", 0); Pump(500);
        var track = sw.SwitchTrack;
        call("OnMouseDown", new object[] { at(20, 20) }); call("OnMouseUp", new object[] { at(20, 20) }); call("OnClick", new object[] { EventArgs.Empty }); Pump(300);
        bool besideNothing = !sw.Checked && sim.Read("M60") == 0;
        int tx = (int)(track.Left + track.Width / 2), ty = (int)(track.Top + track.Height / 2);
        call("OnMouseDown", new object[] { at(tx, ty) }); call("OnMouseUp", new object[] { at(tx, ty) }); call("OnClick", new object[] { EventArgs.Empty }); Pump(300);
        Check("Control, a row: a press on its words changes nothing, one on the switch does", besideNothing && sw.Checked && sim.Read("M60") == 1);
        sw.ActiveArea = ActiveArea.Row; Pump(50);
        call("OnMouseDown", new object[] { at(20, 20) }); call("OnMouseUp", new object[] { at(20, 20) }); call("OnClick", new object[] { EventArgs.Empty }); Pump(300);
        Check("  Row: a press anywhere on the row changes it", !sw.Checked && sim.Read("M60") == 0);
        var small = new CheckBoxHMI { Name = "small", Address = "M61", DescriptionText = "Air", Look = CheckBoxLook.Switch, Location = new Point(1200, 300), Size = new Size(44, 32) };
        page.Controls.Add(small); scanner.RefreshTags(); sim.Write("M61", 0); Pump(500);
        var smallCaption = page.Controls.OfType<Label>().First(l => l.Text.StartsWith("Air"));
        raise(smallCaption, "OnClick"); Pump(300);
        bool captionTicks = small.Checked && sim.Read("M61") == 1;
        small.ActiveArea = ActiveArea.Control; Pump(50);
        raise(smallCaption, "OnClick"); Pump(300);
        Check("a switch with its caption beside it - Row: a click on the caption changes it; Control: not", captionTicks && small.Checked && sim.Read("M61") == 1);
        // 3 November: "on the small switches as well" - slid from the caption beside it too
        Action<int[]> captionSlide = xs =>
        {
            Func<int, MouseEventArgs> on = x => new MouseEventArgs(MouseButtons.Left, 1, x, 8, 0);
            typeof(Control).GetMethod("OnMouseDown", Any).Invoke(smallCaption, new object[] { on(xs[0]) });
            for (int i = 1; i < xs.Length; i++) typeof(Control).GetMethod("OnMouseMove", Any).Invoke(smallCaption, new object[] { on(xs[i]) });
            typeof(Control).GetMethod("OnClick", Any).Invoke(smallCaption, new object[] { EventArgs.Empty });
            typeof(Control).GetMethod("OnMouseUp", Any).Invoke(smallCaption, new object[] { on(xs[xs.Length - 1]) });
            Pump(300);
        };
        small.ActiveArea = ActiveArea.Row; small.ConfirmWrite = true; Pump(50);
        asked = 0; answer = false;
        CheckBoxHMI.Confirm = (b, on) => { asked++; return answer; };
        captionSlide(new[] { 40, 30, 15, 5 });
        bool slidOff = !small.Checked && sim.Read("M61") == 0 && small.LastChange == "slide";
        captionSlide(new[] { 5, 20, 35, 50 });
        Check("a small switch slid on the caption beside it: left OFF, right ON - nothing asked (ConfirmWrite), the lift's click changes nothing more",
              slidOff && small.Checked && sim.Read("M61") == 1 && small.LastChange == "slide" && asked == 0, asked + " asked");
        captionSlide(new[] { 20, 22 });
        Check("  a tap on the caption (moved less than a slide): asked", asked == 1 && small.Checked && sim.Read("M61") == 1);
        small.ActiveArea = ActiveArea.Control; Pump(50);
        captionSlide(new[] { 40, 30, 15, 5 });
        Check("  Control: a slide on the caption changes nothing (only words)", small.Checked && sim.Read("M61") == 1 && asked == 1);
        CheckBoxHMI.Confirm = confirmWas; small.ConfirmWrite = false; small.ActiveArea = ActiveArea.Row;

        Console.WriteLine("--- AxisSettingHMI: the row's and each button's own conditions and triggers ---");
        sim.Write("L27", 1); sim.Write("M100", 0); sim.Write("M53", 1); Pump(900);
        var back = (ButtonHMI)Get(row, "_back"); var fwd = (ButtonHMI)Get(row, "_forward"); var cal = (ButtonHMI)Get(row, "_calibrate");
        Check("the jogs and Calibrate carry the row's EnableConditions, each its own added", back.EnableConditions == SystemCondition.ManualMode &&
              fwd.EnableConditions == (SystemCondition.ManualMode | optA) && cal.EnableConditions == SystemCondition.ManualMode && back.EnableTrigger.MonitorAddress == "M53");
        Check("  all met: usable", Soft(back) && Soft(fwd) && Soft(cal));
        sim.Write("M100", 1); Pump(900);
        Check("  not in manual: none usable", !Soft(back) && !Soft(fwd) && !Soft(cal));
        sim.Write("M100", 0); sim.Write("M53", 0); Pump(900);
        Check("  the back jog's trigger M53 off: only the back jog held back", !Soft(back) && Soft(fwd) && Soft(cal));
        sim.Write("M53", 1);

        Console.WriteLine("--- AxisUnitHMI: its row's visibility and the jogs' conditions ---");
        typeof(Kaban.UI.Helpers.AlarmHelpLinks).GetField("_discovered", Any).SetValue(null, true);
        typeof(Kaban.UI.Helpers.AlarmHelpLinks).GetMethod("RegisterAxis", Any).Invoke(null, new object[] { row });
        var unit = new AxisUnitHMI { Name = "unitAx", Location = new Point(10, 200) };
        unit.AxisNumber = 7;
        page.Controls.Add(unit);
        scanner.RefreshTags();
        Check("resolved from the row", unit.IsResolved && unit.Unit.CommandAAddress == "M50");
        Check("  its jogs: the row's conditions and each jog's own, the back jog's trigger",
              unit.Unit.ButtonAEnableConditions == SystemCondition.ManualMode && unit.Unit.ButtonBEnableConditions == (SystemCondition.ManualMode | optA) &&
              unit.Unit.ButtonAEnableTrigger.MonitorAddress == "M53");
        sim.Write("L27", 1); sim.Write("M100", 0); Pump(900);
        Check("  all met: shown, its jogs usable", unit.Visible && Soft(unit.Unit.ButtonA) && Soft(unit.Unit.ButtonB));
        sim.Write("M100", 1); Pump(900);
        Check("  not in manual: its jogs held back", unit.Visible && !Soft(unit.Unit.ButtonA) && !Soft(unit.Unit.ButtonB));
        sim.Write("L27", 0); Pump(900);
        Check("  the row's visibility (Option A) off: the axis unit hidden, whole", !unit.Visible);
        sim.Write("L27", 1); Pump(900);
        Check("  on again: shown", unit.Visible);

        scanner.Stop();
        Console.WriteLine(fail == 0 ? "\nCONTROL CONDITIONS ALL PASS" : "\nCONTROL CONDITIONS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
    static bool TypeDescriptorHas(Type t, string name) {
        var p = System.ComponentModel.TypeDescriptor.GetProperties(t)[name];
        return p != null && p.IsBrowsable && p.Category.TrimStart('\t') == "Conditions";
    }
}
static class CbExt {
    public static string TooltipTextForTests(this CheckBoxHMI cb) {
        return (string)typeof(CheckBoxHMI).GetMethod("TooltipText", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(cb, null);
    }
}
