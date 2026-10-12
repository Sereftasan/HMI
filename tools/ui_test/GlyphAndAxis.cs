using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.UI.Controls;
using Kaban.Core.Enums;

class G {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}
    [STAThread] static void Main(){
        // The harness must load the translation file, or every key resolves to
        // itself and the key-based tests below prove nothing.
        Kaban.Core.Helpers.LanguageManager.LoadCommonTranslations("CommonTranslations.csv");

        Console.WriteLine("--- glyph detection ---");
        Check("an arrow is a glyph", ButtonGlyphs.IsGlyphOnly(ButtonGlyphs.Left));
        Check("a word is not", !ButtonGlyphs.IsGlyphOnly("Start"));
        Check("a word with an arrow is not", !ButtonGlyphs.IsGlyphOnly("Jog " + ButtonGlyphs.Right));
        Check("a number is not", !ButtonGlyphs.IsGlyphOnly("100"));

        Console.WriteLine("--- sizing ---");
        var basef = new Font("Segoe UI", 9.5f);
        var glyph = ButtonGlyphs.FontFor(ButtonGlyphs.Left, basef, 38);
        var word  = ButtonGlyphs.FontFor("Start", basef, 38);
        Check("arrow is enlarged, but modestly",
              glyph.Size > basef.Size * 1.15f && glyph.Size < basef.Size * 1.45f,
              basef.Size + " -> " + glyph.Size);
        Check("word keeps its size", Math.Abs(word.Size - basef.Size) < 0.01f, word.Size.ToString());
        var tight = ButtonGlyphs.FontFor(ButtonGlyphs.Left, basef, 20);
        Check("a short button clips the glyph rather than overflowing", tight.Size < glyph.Size,
              "38px->" + glyph.Size + "  20px->" + tight.Size);

        Console.WriteLine("--- action marks are distinct ---");
        Check("momentary has no mark", ButtonGlyphs.MarkFor(BitActionType.Momentary) == "");
        // Alternate deliberately REUSES the SET/RESET marks - it shows whichever
        // one describes the next press, so a latch always says what it will do.
        Check("SET and RESET are distinct, and neither is empty",
              ButtonGlyphs.SetMark != ButtonGlyphs.ResetMark &&
              ButtonGlyphs.SetMark != "" && ButtonGlyphs.ResetMark != "");

        Console.WriteLine("--- a glyph caption is never translated ---");
        var b = new ButtonHMI { FallbackText = ButtonGlyphs.Right, TranslationKey = "Btn_DoesNotExist" };
        b.Size = new Size(40, 34);
        b.ApplyTranslation();
        Check("shows the arrow, not the missing key", b.DisplayText == ButtonGlyphs.Right, b.DisplayText);
        // The FONT must be untouched: enlarging it changed every button's height.
        Check("the control font is NOT modified", Math.Abs(b.Font.Size - 9.5f) < 0.01f, b.Font.Size.ToString());
        var before = b.Height; b.ApplyTranslation();
        Check("and the height does not move when a glyph is applied", b.Height == before,
              before + " -> " + b.Height);

        var w2 = new ButtonHMI { FallbackText = "Start", TranslationKey = "Btn_Start" };
        w2.Size = new Size(112, 40); w2.ApplyTranslation();
        Check("a word keeps the normal font", Math.Abs(w2.Font.Size - 9.5f) < 0.01f, w2.Font.Size.ToString());
        Check("and a word still uses Text (translated)",
              !string.IsNullOrEmpty(w2.DisplayText) &&
              !ButtonGlyphs.IsGlyphOnly(w2.DisplayText), w2.DisplayText);

        Console.WriteLine("--- AxisSettingHMI: empty address hides the field ---");
        var ax = new AxisSettingHMI {
            AxisNameKey="X1",
            CommandBackAddress="M600", CommandForwardAddress="M601", PositionAddress="W0",
            MinLimitAddress="D320", MaxLimitAddress="D318", ReferenceAddress="D322",
            CalibrateAddress="M140",
            GoToReferenceAddress="", CurrentAddress="", ZDistanceAddress="", };
        ax.BuildRow();
        int numerics = ax.Controls.OfType<NumericInputHMI>().Count();
        int buttons  = ax.Controls.OfType<ButtonHMI>().Count();
        Check("3 settings drawn (min/max/ref)", numerics == 3, numerics.ToString());
        Check("3 buttons (back, fwd, calibrate), no go-to-ref", buttons == 3, buttons.ToString());
        Check("no meters when their devices are empty",
              ax.Controls.OfType<MeterHMI>().Count() == 0);
        Check("position present", ax.Controls.OfType<NumericDisplayHMI>().Count() == 1);

        var full = new AxisSettingHMI {
            AxisNameKey="X", CommandBackAddress="M1", CommandForwardAddress="M2", PositionAddress="W0", MinLimitAddress="D2", MaxLimitAddress="D3",
            ReferenceAddress="D4", CalibrationAddress="D5", CalibrateAddress="M3",
            GoToReferenceAddress="M4", CurrentAddress="D6", ZDistanceAddress="D7" };
        full.BuildRow();
        Check("a fully-equipped axis draws 4 settings (min/max/ref/cal)",
              full.Controls.OfType<NumericInputHMI>().Count()==4,
              full.Controls.OfType<NumericInputHMI>().Count().ToString());
        Check("  and 2 meters", full.Controls.OfType<MeterHMI>().Count()==2,
              full.Controls.OfType<MeterHMI>().Count().ToString());
        // back, forward, calibrate - the reference is a CHECKBOX, because it is a
        // latched selection the operator arms and leaves armed.
        Check("  3 buttons plus a reference checkbox",
              full.Controls.OfType<ButtonHMI>().Count()==3 &&
              full.Controls.OfType<CheckBoxHMI>().Count()==1,
              full.Controls.OfType<ButtonHMI>().Count() + " buttons, " +
              full.Controls.OfType<CheckBoxHMI>().Count() + " checkbox");

        Console.WriteLine("--- one value spec drives every field on the row ---");
        var vs = new AxisSettingHMI {
            AxisNameKey="Y", CommandBackAddress="M1", CommandForwardAddress="M2", PositionAddress="W0",
            MinLimitAddress="D2", MaxLimitAddress="D3", ReferenceAddress="D4", CalibrationAddress="D5",
            Divisor = 1000D, DecimalPlaces = 3, Digits = 6 };
        vs.BuildRow();
        var nums = vs.Controls.OfType<NumericInputHMI>().ToList();
        Check("every setting shares the row divisor", nums.All(n => n.Divisor == 1000D));
        Check("  and its decimals", nums.All(n => n.DecimalPlaces == 3));
        Check("  and its digit count", nums.All(n => n.Digits == 6));
        var pos = vs.Controls.OfType<NumericDisplayHMI>().First();
        Check("the position matches too", pos.Divisor == 1000D && pos.DecimalPlaces == 3);

        Console.WriteLine("--- reference/calibration limits come from the travel words ---");
        var byAddr = nums.ToDictionary(n => n.Address);
        Check("reference min reads the axis MIN word",
              byAddr["D4"].MinLimit.Type == Kaban.Core.Enums.LimitSourceType.Tag &&
              byAddr["D4"].MinLimit.TagAddress == "D2",
              byAddr["D4"].MinLimit.TagAddress);
        Check("reference max reads the axis MAX word",
              byAddr["D4"].MaxLimit.TagAddress == "D3");
        Check("calibration is bounded the same way",
              byAddr["D5"].MinLimit.TagAddress == "D2" && byAddr["D5"].MaxLimit.TagAddress == "D3");
        Check("the MIN word itself is NOT bounded by itself",
              byAddr["D2"].MinLimit.Type == Kaban.Core.Enums.LimitSourceType.Static);

        Console.WriteLine("--- action marks reflect the CURRENT bit state ---");
        Check("SET is the filled mark",
              ButtonGlyphs.MarkFor(BitActionType.SET, false) == ButtonGlyphs.SetMark);
        Check("RESET is the hollow mark",
              ButtonGlyphs.MarkFor(BitActionType.RESET, false) == ButtonGlyphs.ResetMark);
        Check("Alternate shows SET while the bit is OFF",
              ButtonGlyphs.MarkFor(BitActionType.Alternate, false) == ButtonGlyphs.SetMark);
        Check("  and RESET while it is ON",
              ButtonGlyphs.MarkFor(BitActionType.Alternate, true) == ButtonGlyphs.ResetMark);
        Check("filled and hollow differ", ButtonGlyphs.SetMark != ButtonGlyphs.ResetMark);
        Check("up/down arrows exist for vertical movement",
              ButtonGlyphs.ArrowFor(ButtonGlyphs.Direction.Up) == ButtonGlyphs.Up &&
              ButtonGlyphs.ArrowFor(ButtonGlyphs.Direction.Down) == ButtonGlyphs.Down);

        Console.WriteLine("--- meter bands ---");
        var cur = new MeterHMI(); cur.UseCurrentBands();
        Check("current: green below 60", cur.BandColourAt(40) == MeterHMI.GreenBand, "40");
        Check("current: amber 60-80", cur.BandColourAt(70) == MeterHMI.AmberBand, "70");
        Check("current: red above 80", cur.BandColourAt(90) == MeterHMI.RedBand, "90");

        var z = new MeterHMI(); z.UseZDistanceBands();
        Check("Z: red at 5 (too close to the pulse)", z.BandColourAt(5) == MeterHMI.RedBand);
        Check("Z: amber at 15", z.BandColourAt(15) == MeterHMI.AmberBand);
        Check("Z: green at 50 (mid revolution)", z.BandColourAt(50) == MeterHMI.GreenBand);
        Check("Z: amber at 85", z.BandColourAt(85) == MeterHMI.AmberBand);
        Check("Z: red at 95 (past the far side)", z.BandColourAt(95) == MeterHMI.RedBand);
        Check("the two shapes differ", cur.Bands != z.Bands,
              cur.Bands + " vs " + z.Bands);

        Console.WriteLine("--- backlash and the meters keep their OWN scaling ---");
        var sep = new AxisSettingHMI {
            AxisNameKey="Z", PositionAddress="W0", MinLimitAddress="D2", MaxLimitAddress="D3",
            CurrentAddress="WA", CurrentDivisor=1D, CurrentFullScale=100D,
            ZDistanceAddress="D73", ZDistanceDivisor=1000D, ZDistanceFullScale=10D,
            Divisor=10000D, DecimalPlaces=3 };
        sep.BuildRow();
        var mainv = sep.Controls.OfType<NumericInputHMI>().First(n => n.Address=="D2");
        Check("the travel limits do use it", mainv.Divisor == 10000D && mainv.DecimalPlaces == 3);
        var meters = sep.Controls.OfType<MeterHMI>().OrderBy(m2 => m2.Left).ToList();
        Check("two meters drawn", meters.Count == 2);
        Check("motor current uses the RISING shape",
              meters[0].Bands == MeterBaseHMI.BandShape.Rising, meters[0].Bands.ToString());
        Check("Z distance uses the WINDOW shape (both ends bad)",
              meters[1].Bands == MeterBaseHMI.BandShape.Window, meters[1].Bands.ToString());

        Console.WriteLine("--- the four settings convert but do NOT print a unit ---");
        var un = new AxisSettingHMI {
            AxisNameKey="Axis_RobotX", PositionAddress="W0",
            MinLimitAddress="D2", MaxLimitAddress="D3",
            ReferenceAddress="D4", CalibrationAddress="D5", Suffix="mm" };
        un.BuildRow();
        var setFields = un.Controls.OfType<NumericInputHMI>().ToList();
        Check("settings carry the position's unit (so inch converts them)",
              setFields.All(n => n.UnitType == Kaban.Core.Enums.UnitType.Length));
        Check("but draw no suffix", setFields.All(n => !n.ShowSuffix));
        var posField = un.Controls.OfType<NumericDisplayHMI>().First();
        Check("the position DOES show it", posField.ShowSuffix && posField.UnitType == Kaban.Core.Enums.UnitType.Length);

        Console.WriteLine("--- reference/calibration limits are armed ---");
        var byA = setFields.ToDictionary(n => n.Address);
        Check("reference warns when outside travel", byA["D4"].OutOfLimitsWarning);
        Check("  its min reads the axis MIN word", byA["D4"].MinLimit.TagAddress == "D2");
        Check("  its max reads the axis MAX word", byA["D4"].MaxLimit.TagAddress == "D3");
        Check("calibration likewise", byA["D5"].OutOfLimitsWarning &&
              byA["D5"].MinLimit.TagAddress == "D2" && byA["D5"].MaxLimit.TagAddress == "D3");

        Console.WriteLine("--- the axis name is a translation KEY ---");
        Check("a known key resolves to its text",
              Kaban.Core.Helpers.LanguageManager.GetString("Axis_Milling1Angle","Axis_Milling1Angle")
              == "Milling 1 Angle",
              Kaban.Core.Helpers.LanguageManager.GetString("Axis_Milling1Angle","Axis_Milling1Angle"));
        Check("an unknown one is shown as typed",
              Kaban.Core.Helpers.LanguageManager.GetString("Milling 9 Angle","Milling 9 Angle")
              == "Milling 9 Angle");
        Check("the name column fits 'Milling 1 Angle'", AxisSettingHMI.WAxis >= 120,
              AxisSettingHMI.WAxis.ToString());

        Console.WriteLine("--- a TAG limit is read as a 32-bit PAIR and scaled ---");
        // Reproduces the reported failure exactly. Min D314 holds -6,020,000 and
        // Max D312 holds 82,000,000 (i.e. -602.00 and 8200.00 at divisor 10000).
        // Read as ONE word those become 9312 and 14464 - which is the nonsense the
        // keypad was offering.
        var words = new System.Collections.Generic.Dictionary<string,int> {
            {"D314", -6020000 & 0xFFFF}, {"D315", (-6020000 >> 16) & 0xFFFF},
            {"D312",  82000000 & 0xFFFF}, {"D313", ( 82000000 >> 16) & 0xFFFF},
        };
        Func<string,int> plc = a => { int v; return words.TryGetValue(a, out v) ? v : 0; };

        Check("the raw low words are the reported nonsense",
              plc("D314") == 9312 && plc("D312") == 14464,
              plc("D314") + " / " + plc("D312"));

        var lim = Kaban.Core.Helpers.InputLimitsCalculation.GetEffectiveLimits(
            Kaban.Core.Enums.DataTypeEnum.Bin_32,
            Kaban.Core.Enums.DisplayFormatEnum.Signed_Decimal,
            new Kaban.Core.Models.DynamicValue(Kaban.Core.Enums.LimitSourceType.Tag, 0, "D314"),
            new Kaban.Core.Models.DynamicValue(Kaban.Core.Enums.LimitSourceType.Tag, 0, "D312"),
            "val", plc, (f, v) => v, 10000D);     // "val" is the DEFAULT ReadFormula
        Check("min resolves to -602.00", Math.Abs(lim.Min - (-602.0)) < 0.001, lim.Min.ToString());
        Check("max resolves to 8200.00", Math.Abs(lim.Max - 8200.0) < 0.001, lim.Max.ToString());
        Check("a reference of 8156.86 is now INSIDE the travel",
              8156.86 >= lim.Min && 8156.86 <= lim.Max);
        Check("and 9000.00 is outside it", !(9000.0 <= lim.Max));

        // The default ReadFormula is "val", not "". Testing only for an empty
        // string treated that identity formula as custom scaling and skipped the
        // divisor entirely - which is why the keypad still showed raw counts.
        var blank = Kaban.Core.Helpers.InputLimitsCalculation.GetEffectiveLimits(
            Kaban.Core.Enums.DataTypeEnum.Bin_32,
            Kaban.Core.Enums.DisplayFormatEnum.Signed_Decimal,
            new Kaban.Core.Models.DynamicValue(Kaban.Core.Enums.LimitSourceType.Tag, 0, "D314"),
            new Kaban.Core.Models.DynamicValue(Kaban.Core.Enums.LimitSourceType.Tag, 0, "D312"),
            "", plc, (f, v) => v, 10000D);
        Check("an empty formula scales the same way",
              Math.Abs(blank.Max - 8200.0) < 0.001, blank.Max.ToString());

        var custom = Kaban.Core.Helpers.InputLimitsCalculation.GetEffectiveLimits(
            Kaban.Core.Enums.DataTypeEnum.Bin_32,
            Kaban.Core.Enums.DisplayFormatEnum.Signed_Decimal,
            new Kaban.Core.Models.DynamicValue(Kaban.Core.Enums.LimitSourceType.Tag, 0, "D312"),
            new Kaban.Core.Models.DynamicValue(Kaban.Core.Enums.LimitSourceType.Tag, 0, "D312"),
            "val/1000", plc, (f, v) => v / 1000.0, 10000D);
        Check("a REAL formula keeps control of the scaling",
              Math.Abs(custom.Max - 82000.0) < 0.001, custom.Max.ToString());

        Console.WriteLine("--- device is written without the word 'station' ---");
        Check("single PLC shows the bare device",
              Kaban.Core.Helpers.PlcAddressHelper.Describe(1, "D215") == "D215",
              Kaban.Core.Helpers.PlcAddressHelper.Describe(1, "D215"));

        Console.WriteLine("--- checkbox, lamp and button share a footprint ---");
        var cb = new CheckBoxHMI(); var lp = new LampHMI();
        // The checkbox matches a COMMAND BUTTON (44x32) so the two line up where
        // they stand side by side; the tick box itself is drawn square inside it.
        Check("checkbox matches a command button footprint",
              cb.Width == 44 && cb.Height == 32, cb.Width + "x" + cb.Height);
        // A lamp is a status mark, not something to aim at, so it is deliberately
        // smaller than the controls around it.
        Check("lamp is square and smaller than the checkbox",
              lp.Width == lp.Height && lp.Height < cb.Height,
              lp.Width + "x" + lp.Height + " vs " + cb.Height);
        Check("the designer's default agrees with the constructor",
              new CheckBoxHMI().Size == cb.Size && new LampHMI().Size == lp.Size);

        Console.WriteLine("--- alarm codes are coloured by what they mean ---");
        Check("a warning column is marked as a warning",
              new HexCodeHMI { Severity = HexCodeHMI.CodeSeverity.Warning }.Severity
              == HexCodeHMI.CodeSeverity.Warning);
        Check("hex formatting keeps the manual's form",
              HexCodeHMI.Format(0x502) == "50.2" && HexCodeHMI.Format(0x1A5) == "1A.5",
              HexCodeHMI.Format(0x502) + " / " + HexCodeHMI.Format(0x1A5));

        Console.WriteLine("--- a designer-dropped control builds itself ---");
        var dropped = new AxisSettingHMI { AxisNameKey="A", PositionAddress="W0",
                                           CommandBackAddress="M1", CommandForwardAddress="M2" };
        using (var host = new Form()) {
            host.Controls.Add(dropped);
            host.Show();
            Check("children exist without a BuildRow() call", dropped.Controls.Count > 0,
                  dropped.Controls.Count + " children");
            host.Close();
        }

        Console.WriteLine(fail==0?"\nGLYPH+AXIS ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

