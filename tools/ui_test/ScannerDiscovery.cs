using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.UI.Controls;

class S {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    [STAThread] static void Main(){
        Console.WriteLine("--- the new controls declare themselves as PLC-bound ---");
        var meter = new MeterHMI { Address = "D100", PlcNumber = 1, Divisor = 10, DecimalPlaces = 2,
                                   ScaleMinimum = 150, ScaleMaximum = 500, Suffix = "%" };
        var code  = new HexCodeHMI { Address = "D100", PlcNumber = 1 };
        Check("MeterHMI implements IPlcBoundControl", meter is IPlcBoundControl);
        Check("HexCodeHMI implements IPlcBoundControl", code is IPlcBoundControl);
        Check("both are still IHmiControl", meter is IHmiControl && code is IHmiControl);

        Console.WriteLine("--- a scan actually reaches them ---");
        // Stand in for the scanner's lookup: D100 holds 3000.
        Func<string,int> lookup = a => a == "D100" ? 3000 : 0;
        Func<Kaban.Core.Enums.SystemCondition,bool> cond = c => true;

        meter.UpdateState(lookup, cond);
        code.UpdateState(lookup, cond);
        Check("the meter now has a value", Math.Abs(meter.DisplayValue - 300.0) < 0.001,
              meter.DisplayValue.ToString());
        Check("the hex code formats 3000 (0xBB8) as 'BB.8'",
              HexCodeHMI.Format(3000) == "BB.8", HexCodeHMI.Format(3000));

        Console.WriteLine("--- band colours respond to the value ---");
        var band = new MeterHMI(); band.UseCurrentBands();
        Check("40% green", band.BandColourAt(40) == MeterHMI.GreenBand);
        Check("70% amber", band.BandColourAt(70) == MeterHMI.AmberBand);
        Check("90% red",   band.BandColourAt(90) == MeterHMI.RedBand);

        Console.WriteLine("--- the scanner's own walk finds them ---");
        // The real failure was discovery: a control outside the hardcoded type
        // list was never collected, so its address never joined the batch read.
        var page = new UserControl();
        page.Controls.Add(meter);
        page.Controls.Add(code);
        page.Controls.Add(new LampHMI { Address = "M10" });

        var scanner = new Kaban.UI.Plc.PlcScanner(page);
        var find = typeof(Kaban.UI.Plc.PlcScanner).GetMethod("FindControls",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        find.Invoke(scanner, new object[]{ page });
        var others = (System.Collections.IList)typeof(Kaban.UI.Plc.PlcScanner)
            .GetField("_others", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(scanner);
        Check("the meter and the code were both discovered", others.Count == 2,
              others.Count + " collected");

        Console.WriteLine("--- a zero code is BLANK, not 0.0 ---");
        Check("zero prints nothing", HexCodeHMI.Format(0) == "", "[" + HexCodeHMI.Format(0) + "]");
        Check("a real code still prints", HexCodeHMI.Format(0x502) == "50.2");

        Console.WriteLine("--- the station line only appears for an auxiliary PLC ---");
        Check("PLC 1 says nothing",
              Kaban.Core.Helpers.PlcAddressHelper.StationHeader(1) == "",
              "[" + Kaban.Core.Helpers.PlcAddressHelper.StationHeader(1) + "]");
        Check("PLC 2 announces itself",
              Kaban.Core.Helpers.PlcAddressHelper.StationHeader(2).StartsWith("PLC 2"));

        Console.WriteLine("--- a sign appears only on a BARE button ---");
        var bare = new ButtonHMI { FallbackText = "", Direction = ButtonGlyphs.Direction.Up };
        bare.ApplyTranslation();
        Check("no caption, no image -> the arrow is drawn", bare.DisplayText == ButtonGlyphs.Up,
              "[" + bare.DisplayText + "]");

        // The caption no longer suppresses the sign: Direction alone decides, and a
        // button may carry both - sign on the left, words beside it.
        var titled = new ButtonHMI { FallbackText = "Jog up", Direction = ButtonGlyphs.Direction.Up };
        titled.ApplyTranslation();
        Check("a captioned button keeps its words", titled.DisplayText == "Jog up",
              "[" + titled.DisplayText + "]");

        var plain = new ButtonHMI { FallbackText = "Jog up" };
        plain.ApplyTranslation();
        Check("with Direction None nothing is added to it",
              plain.DisplayText == "Jog up", "[" + plain.DisplayText + "]");

        var latch = new ButtonHMI { FallbackText = "" };
        latch.BitActions.Add(new Kaban.Core.Models.BitAction {
            Address = "M1", Action = Kaban.Core.Enums.BitActionType.SET });
        latch.ApplyTranslation();
        Check("a bare latch button shows the SET mark",
              latch.DisplayText == ButtonGlyphs.SetMark);

        var latchNamed = new ButtonHMI { FallbackText = "Clamp" };
        latchNamed.BitActions.Add(new Kaban.Core.Models.BitAction {
            Address = "M1", Action = Kaban.Core.Enums.BitActionType.SET });
        latchNamed.ApplyTranslation();
        Check("a named latch button still reports its caption",
              latchNamed.DisplayText == "Clamp", "[" + latchNamed.DisplayText + "]");

        Console.WriteLine("--- the reported buttonHMI4 case ---");
        // FallbackText "buttonHMI4", Direction None, one Alternate bit action.
        // The latch mark was appearing beside the button's own name.
        var b4 = new ButtonHMI { FallbackText = "buttonHMI4",
                                 Direction = ButtonGlyphs.Direction.None };
        b4.BitActions.Add(new Kaban.Core.Models.BitAction {
            Address = "m100", Action = Kaban.Core.Enums.BitActionType.Alternate });
        b4.ApplyTranslation();
        Check("a captioned Alternate button shows NO latch mark",
              b4.DisplayText == "buttonHMI4", "[" + b4.DisplayText + "]");

        var b4bare = new ButtonHMI { FallbackText = "" };
        b4bare.BitActions.Add(new Kaban.Core.Models.BitAction {
            Address = "m100", Action = Kaban.Core.Enums.BitActionType.Alternate });
        b4bare.ApplyTranslation();
        Check("an uncaptioned one still does", b4bare.DisplayText == ButtonGlyphs.SetMark);

        var b4arrow = new ButtonHMI { FallbackText = "Back",
                                      Direction = ButtonGlyphs.Direction.Left };
        b4arrow.ApplyTranslation();
        Check("an explicit Direction still draws beside a caption",
              b4arrow.DisplayText == "Back", "[" + b4arrow.DisplayText + "]");

        Console.WriteLine("--- ButtonText is the caption, same as FallbackText ---");
        var bt = new ButtonHMI { ButtonText = "Start" };
        Check("ButtonText sets the caption", bt.FallbackText == "Start" && bt.DisplayText == "Start");

        Console.WriteLine("--- a new BitAction is MOMENTARY ---");
        Check("default action is Momentary",
              new Kaban.Core.Models.BitAction().Action == Kaban.Core.Enums.BitActionType.Momentary,
              new Kaban.Core.Models.BitAction().Action.ToString());

        Console.WriteLine("--- a DESIGNER-placed button gets its sign without any call ---");
        // The reported failure: a button dropped on a form and never re-translated
        // kept _glyph null, so no arrow or latch mark was ever drawn.
        using (var host2 = new Form())
        {
            var designed = new ButtonHMI { FallbackText = "", Direction = ButtonGlyphs.Direction.Left };
            var designedSet = new ButtonHMI { FallbackText = "" };
            designedSet.BitActions.Add(new Kaban.Core.Models.BitAction {
                Address = "M9", Action = Kaban.Core.Enums.BitActionType.SET });
            host2.Controls.Add(designed);
            host2.Controls.Add(designedSet);
            host2.Show();
            Check("the arrow appears with no ApplyTranslation() call",
                  designed.DisplayText == ButtonGlyphs.Left, "[" + designed.DisplayText + "]");
            Check("so does the latch mark",
                  designedSet.DisplayText == ButtonGlyphs.SetMark);
            host2.Close();
        }

        Console.WriteLine("--- lamp is smaller than the controls beside it ---");
        var lamp2 = new LampHMI(); var chk2 = new CheckBoxHMI();
        Check("lamp is square and smaller", lamp2.Width == lamp2.Height && lamp2.Width < chk2.Height,
              lamp2.Width + " vs checkbox height " + chk2.Height);

        Console.WriteLine(fail==0?"\nSCANNER ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

