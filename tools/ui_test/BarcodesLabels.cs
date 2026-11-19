using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Kaban.Core.Barcodes;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Labels;
using Kaban.Profiles.Jobs;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

// 17 November: "take barcode reading and writing from KabanMachineSystem as well" - the barcode readers (the old
// BarcodeReaderSettings, MD3030's barcode fields), a code's size into MK2010's / MD3020's job, the labels (the old
// KabanLabelDesigner's EPL, its printers), the PLC's label requests (the old Check_PrintBarcode: L2 + D386, L24 + D388),
// FA2010's labels of the bars sent, and the two HMI Settings pages.
class BL {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }

    static void Shot(Control page, string path, int w, int h) {
        var f = new Form { ClientSize = new Size(w, h), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        page.Dock = DockStyle.Fill;
        f.Controls.Add(page);
        f.Show();
        Pump(500);
        using (var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height)) {
            using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(f.PointToScreen(Point.Empty), Point.Empty, bmp.Size);
            bmp.Save(path);
        }
        f.Controls.Remove(page);
        f.Close();
    }

    [STAThread]
    static void Main(string[] args) {
        string shots = args.Length > 0 ? args[0] : "";
        var dir = Path.Combine(Path.GetTempPath(), "bl_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        BarcodeReaders.SettingsPath = Path.Combine(dir, "Barcodes.cfg");
        BarcodeReaders.Reload();
        LabelStore.Folder = Path.Combine(dir, "Labels");
        LabelTriggers.SettingsPath = Path.Combine(dir, "LabelTriggers.cfg");
        LabelService.Synchronous = true;
        var sent = new List<KeyValuePair<string, byte[]>>();
        LabelPrinters.Send = (printer, baud, data) => { sent.Add(new KeyValuePair<string, byte[]>(printer, data)); return printer == "BROKEN" ? "the printer does not answer" : ""; };

        // ================================================================ a code read
        Console.WriteLine("--- a code read: cleaned, split ---");
        Check("CR, LF, STX, ETX out (the old program's cleaning)", BarcodeReaders.Clean("\u0002ABC123\u0003\r\n") == "ABC123");
        var layout = BarcodeReaders.ProfileSizeLayout();
        var s1 = BarcodeReaders.Parse("P123456789" + "1200" + "0800", layout);
        Check("the usual parts (MK2010's FillByBarcode): code 1 - 10, width 11 - 14, height 15 - 18",
              s1.ProfileCode == "P123456789" && s1.Width == 1200 && s1.Height == 800 && s1.Problems.Count == 0,
              s1.ProfileCode + " " + s1.Width + " " + s1.Height);
        var s2 = BarcodeReaders.Parse("P1234", layout);
        Check("  a code too short for a part: said, the rest read", s2.ProfileCode == "" && s2.Problems.Count == 3 && s2.Problems[0].Contains("too short"), string.Join("; ", s2.Problems));
        var s3 = BarcodeReaders.Parse("AB00125", new[] { new BarcodeFieldConfig { Name = "W", Kind = BarcodeFieldKind.Width, Start = 3, Length = 5, Divisor = 10 } });
        Check("  a part divided (tenths): 00125 / 10 = 12.5", s3.Width == 12.5, s3.Width + "");

        BarcodeReaders.Save(new[] { new BarcodeReaderConfig { Name = "Line 1", Port = "4", BaudRate = 19200, Parity = System.IO.Ports.Parity.Even, Enabled = false, Machine = "MK2010" },
                                    new BarcodeReaderConfig { Name = "USB", Kind = BarcodeReaderKind.Keyboard, Enabled = true } },
                            new[] { new BarcodeFieldConfig { Name = "Code", Kind = BarcodeFieldKind.ProfileCode, Start = 2, Length = 4 } }, "ABC", "");
        BarcodeReaders.Reload();
        var rr = BarcodeReaders.Readers;
        Check("the settings kept and read back (Barcodes.cfg): the readers, their port 4 = COM4, baud, parity, machine",
              rr.Count == 2 && rr[0].PortName == "COM4" && rr[0].BaudRate == 19200 && rr[0].Parity == System.IO.Ports.Parity.Even && rr[0].Machine == "MK2010" &&
              !rr[0].Enabled && rr[1].Kind == BarcodeReaderKind.Keyboard && BarcodeReaders.KeyboardReading,
              string.Join(" | ", rr.Select(r => r.Name + " " + r.PortName + " " + r.BaudRate + " " + r.Machine)));
        Check("  the parts and the prefix taken off (MD3030 took \"ABC\" out)", BarcodeReaders.Fields.Count == 1 && BarcodeReaders.Clean("ABCX1234") == "X1234" &&
              BarcodeReaders.Parse("ABCX1234Z").ProfileCode == "1234");
        BarcodeReaders.Save(new BarcodeReaderConfig[0], new BarcodeFieldConfig[0], "", "");

        Console.WriteLine("--- who takes a code ---");
        var order = new List<string>();
        var h1 = BarcodeReaders.Take("first", sc => { order.Add("first"); return true; });
        var h2 = BarcodeReaders.Take("second", sc => { order.Add("second"); return sc.Code.StartsWith("S"); });
        var t1 = BarcodeReaders.Feed("X1", "Test");
        Check("the newest page asked first; the next when it does not take it", order.SequenceEqual(new[] { "second", "first" }) && t1.TakenBy == "first", string.Join(",", order));
        order.Clear();
        var t2 = BarcodeReaders.Feed("S1", "Test");
        Check("  taken by the newest: the older not asked", order.SequenceEqual(new[] { "second" }) && t2.TakenBy == "second");
        h1.Dispose(); h2.Dispose();
        var t3 = BarcodeReaders.Feed("Z9", "Test");
        Check("  no page: not taken, in Recent (newest first)", t3.TakenBy == "" && BarcodeReaders.Recent[0].Code == "Z9" && BarcodeReaders.Recent[1].Code == "S1");

        var cfg = new BarcodeReaderConfig { Name = "Sim", Machine = "M1" };
        BarcodeReaders.Simulate(cfg, "AB12");
        BarcodeReaders.Simulate(cfg, "34\r\nCD");
        Check("a reader's code over two reads, ended by CR LF: one code", BarcodeReaders.Recent[0].Code == "AB1234" && BarcodeReaders.Recent[0].Source == "Sim" &&
              BarcodeReaders.Recent[0].Machine == "M1", BarcodeReaders.Recent[0].Code);
        Pump(400);
        Check("  what came after it, no CR: a code after a pause (120 ms)", BarcodeReaders.Recent[0].Code == "CD", BarcodeReaders.Recent[0].Code);

        // ================================================================ a code's size into a machine's job
        Console.WriteLine("--- a code's size into the job (MK2010, MD3020) ---");
        var sim = new Kaban.Drivers.Simulation.SimulationDriver(1);
        PlcManager.Instance.RegisterDriver(1, sim);
        PlcManager.Instance.RegisterStation(1, "Test", SecurityLevel.Operator);
        sim.Connect();
        SecurityManager.Login("op", SecurityLevel.Operator);
        BarcodeReaders.DefaultFields(BarcodeReaders.ProfileSizeLayout());
        var notes = new List<string>();
        BarcodeTargets.Done += (tg, sc, text, warn) => notes.Add((warn ? "!" : "") + text);
        BarcodeTargets.Register(new BarcodeTarget { ModuleId = "MK2010", Name = "MK2010", Station = 1, WidthAddress = "D1200", HeightAddress = "D1202", Scale = 10, WiderFirst = true, BusyBit = "M26" });
        var f1 = BarcodeReaders.Feed("0003102210" + "0708" + "1256", "Test");
        Check("width 708, height 1256: the wider into Width X - D1200/D1201 = 12560, D1202/D1203 = 7080 (x10, two words)",
              f1.TakenBy.Contains("MK2010") && sim.Read("D1200") == 12560 && sim.Read("D1201") == 0 && sim.Read("D1202") == 7080,
              sim.Read("D1200") + " " + sim.Read("D1202") + " " + string.Join(" / ", notes));
        Check("  the operator told (a note)", notes.Count == 1 && notes[0].Contains("1256") && !notes[0].StartsWith("!"), string.Join(" / ", notes));
        BarcodeReaders.Feed("0003102210" + "9999" + "8888", "Test");
        Check("  a large value: in two words (99990 = 1 x 65536 + 34454)", (sim.Read("D1200") & 0xFFFF) == 34454 && sim.Read("D1201") == 1, sim.Read("D1200") + " " + sim.Read("D1201"));
        sim.Write("M26", 1);
        BarcodeReaders.Feed("0003102210" + "0500" + "0400", "Test");
        Check("  while a job runs (M26): refused, said", (sim.Read("D1200") & 0xFFFF) == 34454 && notes.Last().StartsWith("!") && notes.Last().Contains("M26"), notes.Last());
        sim.Write("M26", 0);
        var f2 = BarcodeReaders.Feed("ABC", "Test");
        Check("  a code without a size: not the machine's", f2.TakenBy == "");
        var other = (BarcodeScan)typeof(BarcodeReaders).GetMethod("Parse").Invoke(null, new object[] { "0003102210" + "0500" + "0400", null, "Sim" });
        other.Machine = "MD3020";
        Check("  a code of a reader set for another machine: not taken", !BarcodeTargets.Take(BarcodeTargets.All[0], other));
        BarcodeTargets.Clear();

        // ================================================================ the labels
        Console.WriteLine("--- a label in the printer's language ---");
        var t = new LabelTemplate { Name = "Piece", WidthMm = 100, HeightMm = 50, Language = "turkish" };
        t.Fields.Add(new LabelField { Key = LabelKeys.ProfileCode, X = 3, Y = 2, FontSize = 3 });
        t.Fields.Add(new LabelField { Key = LabelKeys.CutLength, Text = "L: ", X = 3, Y = 9, FontSize = 4 });
        t.Fields.Add(new LabelField { Text = "Kaban \"A\"", X = 50, Y = 2, FontSize = 2 });
        t.Fields.Add(new LabelField { Key = LabelKeys.KabanBarcode, Type = LabelFieldType.Barcode, X = 3, Y = 27, FontSize = 1 });
        t.Fields.Add(new LabelField { Key = "Glazed", Type = LabelFieldType.YesNo, X = 60, Y = 9, ValueOnTrue = "GLASS", ValueOnFalse = "-" });
        t.Fields.Add(new LabelField { Key = LabelKeys.PrintTime, Type = LabelFieldType.DateTime, X = 60, Y = 15, ValueOnTrue = "dd.MM.yyyy" });
        t.Fields.Add(new LabelField { Key = LabelKeys.Customer, X = 3, Y = 20, FontSize = 5 });
        t.Fields.Add(new LabelField { Key = LabelKeys.OrderNo, Type = LabelFieldType.QRCode, X = 80, Y = 20 });
        var v = new LabelValues();
        v[LabelKeys.ProfileCode] = "P1234"; v[LabelKeys.CutLength] = "1250.5"; v[LabelKeys.KabanBarcode] = LabelKeys.KabanCode("P1234", 12, 3);
        v["Glazed"] = "1"; v[LabelKeys.Customer] = "kaban"; v[LabelKeys.OrderNo] = "ORD-1";
        var at = new DateTime(2026, 11, 17, 10, 30, 0);
        var epl = Encoding.GetEncoding(28591).GetString(LabelWriter.Build(t, v, at)).Split(new[] { "\r\n" }, StringSplitOptions.None).ToList();
        Check("EPL as the old program: I8,E,001 (Turkish), N, D15, OD (no ribbon), ZB, R16,0 (100 mm in the 104 mm head), P1",
              epl[1] == "I8,E,001" && epl[3] == "N" && epl[4] == "D15" && epl[5] == "OD" && epl[6] == "ZB" && epl[7] == "R16,0" && epl.Contains("P1"),
              string.Join(" | ", epl.Take(9)));
        Check("  a text: A x8, y8, font", epl.Contains("A24,16,0,3,1,1,N,\"P1234\""), epl[8]);
        Check("  a value's own text in front of it", epl.Contains("A24,72,0,4,1,1,N,\"L: 1250.5\""));
        Check("  a fixed text, its quotes escaped", epl.Contains("A400,16,0,2,1,1,N,\"Kaban \\\"A\\\"\""));
        Check("  the barcode: Code 128, height 40 + 10 x font, its text under it (B)", epl.Contains("B24,216,0,1,2,2,50,B,\"P1234-0012-03\""), epl.FirstOrDefault(l => l.StartsWith("B")));
        Check("  Yes / No: the yes text for 1; a date in its format", epl.Contains("A480,72,0,2,1,1,N,\"GLASS\"") && epl.Contains("A480,120,0,2,1,1,N,\"17.11.2026\""));
        Check("  font 5 upper case only; a QR code b...Q,m2", epl.Contains("A24,160,0,5,1,1,N,\"KABAN\"") && epl.Contains("b640,160,Q,m2,\"ORD-1\""));
        t.HasRibbon = true;
        Check("  with a ribbon: O (not OD)", Encoding.ASCII.GetString(LabelWriter.Build(t, v, at)).Contains("\r\nO\r\n"));
        t.Copies = 3;
        Check("  3 copies (the old Barcode Count): P3", Encoding.ASCII.GetString(LabelWriter.Build(t, v, at)).Contains("\r\nP3\r\n"));
        t.PrinterLanguage = LabelPrinterLanguage.ZPL;
        Check("  in ZPL: ^PQ3", Encoding.UTF8.GetString(LabelWriter.Build(t, v, at)).Contains("^PQ3"));
        t.PrinterLanguage = LabelPrinterLanguage.EPL;
        t.Copies = 1;
        t.PrinterLanguage = LabelPrinterLanguage.ZPL;
        var zpl = Encoding.UTF8.GetString(LabelWriter.Build(t, v, at));
        Check("ZPL: ^XA ... ^XZ, UTF-8, the size, a ribbon ^MTT, the fields at ^FO, Code 128 ^BC, a QR ^BQ",
              zpl.StartsWith("^XA") && zpl.TrimEnd().EndsWith("^XZ") && zpl.Contains("^CI28") && zpl.Contains("^PW800") && zpl.Contains("^LL400") && zpl.Contains("^MTT") &&
              zpl.Contains("^FO24,16^A0N,24,24^FH_^FDP1234^FS") && zpl.Contains("^BCN,50,Y,N,N^FH_^FDP1234-0012-03^FS") && zpl.Contains("^BQN,2,4^FH_^FDQA,ORD-1^FS"), zpl);
        v[LabelKeys.ProfileCode] = "A_B^C";
        Check("  ^ _ ~ in a value escaped (^FH)", Encoding.UTF8.GetString(LabelWriter.Build(t, v, at)).Contains("^FDA_5FB_5EC^FS"));
        t.PrinterLanguage = LabelPrinterLanguage.EPL; t.HasRibbon = false;
        v[LabelKeys.ProfileCode] = "P1234";
        Check("a language's character set: English A, Russian C, Greek D, unknown -> Turkish E",
              LabelWriter.SetOf("english") == "A" && LabelWriter.SetOf("Russian") == "C" && LabelWriter.SetOf("greek") == "D" && LabelWriter.SetOf("xx") == "E");

        // a picture (the old GM / GG, here GW with the label)
        var pic = Path.Combine(dir, "frame.png");
        using (var bmp = new Bitmap(40, 20)) { using (var g = Graphics.FromImage(bmp)) { g.Clear(Color.White); g.FillRectangle(Brushes.Black, 0, 0, 8, 20); } bmp.Save(pic); }
        LabelWriter.LoadPicture = BarcodeHost.Raster;
        var pt = new LabelTemplate { Name = "Pic" };
        pt.Fields.Add(new LabelField { Key = LabelKeys.Picture, Type = LabelFieldType.Picture, X = 1, Y = 1, Width = 5, Height = 5 });
        var pv = new LabelValues { PictureFolder = dir };
        pv[LabelKeys.Picture] = "frame.png";
        var pb = LabelWriter.Build(pt, pv);
        var ps = Encoding.GetEncoding(28591).GetString(pb);
        int gw = ps.IndexOf("GW8,8,5,20,", StringComparison.Ordinal);
        Check("a picture in the job's folder: GW x, y, bytes a row, rows (40 x 20 fitted into 5 x 5 mm: 40 x 20 dots)", gw > 0, new string(ps.Take(160).Select(c => c < 32 || c > 126 ? '.' : c).ToArray()));
        Check("  its dots: the black left 8 columns - a 0 bit prints (EPL), the white 1", gw > 0 && pb[gw + 11] == 0x00 && pb[gw + 12] == 0xFF);
        pv[LabelKeys.Picture] = "missing.png";
        Check("  a picture not there: the label printed without it", !Encoding.ASCII.GetString(LabelWriter.Build(pt, pv)).Contains("GW"));
        Check("LabelWriter.Text: the picture's dots left out", LabelWriter.Text(pt, new LabelValues { PictureFolder = dir }).Contains("P1"));

        Console.WriteLine("--- the labels kept, printed ---");
        Directory.CreateDirectory(LabelStore.Folder);
        LabelStore.Save(t);
        var back = LabelStore.Find("Piece");
        Check("a label kept (Labels\\Piece.label) and read back: its header and fields", back != null && back.Fields.Count == t.Fields.Count && back.WidthMm == 100 &&
              back.Fields[1].Text == "L: " && back.Fields[3].Type == LabelFieldType.Barcode && back.Fields[4].ValueOnTrue == "GLASS" && back.Language == "turkish");
        back.Name = "Piece 2";
        LabelStore.Save(back, "Piece");
        Check("  renamed: the old file gone", LabelStore.All().Select(x => x.Name).SequenceEqual(new[] { "Piece 2" }) && !File.Exists(Path.Combine(LabelStore.Folder, "Piece.label")));
        back.Printer = "COM3";
        LabelStore.Save(back);
        var left = LabelTemplate.Sample(LabelUse.Leftover);
        left.Printer = "BROKEN";
        LabelStore.Save(left);
        sent.Clear();
        int n = LabelService.Print(LabelUse.Piece, "FA2010", v, "Test");
        Check("a piece's label printed with the piece label on (not the leftover's)", n == 1 && sent.Count == 1 && sent[0].Key == "COM3" &&
              Encoding.ASCII.GetString(sent[0].Value).Contains("P1234"), n + " " + sent.Count);
        var lr = LabelService.Recent[0];
        Check("  in Recent: printed, its main values", lr.Ok && lr.Template == "Piece 2" && lr.Summary.Contains("P1234") && lr.Summary.Contains("1250.5"), lr.Summary);
        LabelService.Print(LabelUse.Leftover, "FA2010", v, "Test");
        Check("  a printer that does not answer: not printed, why", !LabelService.Recent[0].Ok && LabelService.Recent[0].Message.Contains("does not answer"));
        back.Machine = "MD3020";
        LabelStore.Save(back);
        Check("  a label set for another machine: not printed for this one", LabelService.Print(LabelUse.Piece, "FA2010", v, "Test") == 0);
        back.Machine = ""; back.Enabled = false;
        LabelStore.Save(back);
        Check("  a label switched off: not printed", LabelService.Print(LabelUse.Piece, "FA2010", v, "Test") == 0);
        back.Enabled = true; LabelStore.Save(back);

        string host; int port;
        Check("the printers: COM3 serial, 10.0.0.5:9100 / 10.0.0.5 network (9100), a Windows printer's name neither",
              LabelPrinters.IsSerial("COM3") && !LabelPrinters.IsSerial("COMPUTER") && LabelPrinters.IsNetwork("10.0.0.5:9100", out host, out port) && host == "10.0.0.5" && port == 9100 &&
              LabelPrinters.IsNetwork("10.0.0.5", out host, out port) && port == 9100 && !LabelPrinters.IsNetwork("ZDesigner GK420d", out host, out port));
        var realSend = (Func<string, int, byte[], string>)typeof(LabelPrinters).GetMethod("SendNow", Any).CreateDelegate(typeof(Func<string, int, byte[], string>));
        var prn = Path.Combine(dir, "out", "label.prn");
        Check("  file:path - the label written to the file", realSend("file:" + prn, 0, new byte[] { 65, 66 }) == "" && File.ReadAllBytes(prn).Length == 2);

        var lv = new LabelValues(); lv["A"] = "x;y\r\nz"; lv.PictureFolder = @"C:\Jobs";
        var un = LabelValues.Unpack(lv.Pack());
        Check("label values packed and read back (kept between restarts)", un["A"] == "x;y  z" && un.PictureFolder == @"C:\Jobs");

        // ================================================================ the PLC asks for a label
        Console.WriteLine("--- the PLC asks for a label (L2 + D386, L24 + D388) ---");
        var asked = new List<double>();
        var pieceTrig = new LabelTrigger { ModuleId = "FA2010", Name = "FA2010", Station = 1, Use = LabelUse.Piece, Bit = "L2", Value = "D386", DoubleWord = true,
                                           Resolve = id => { asked.Add(id); var x = new LabelValues(); x[LabelKeys.ProfileCode] = "P77"; x[LabelKeys.CutLength] = "900"; return x; } };
        var leftTrig = new LabelTrigger { ModuleId = "FA2010", Name = "FA2010", Station = 1, Use = LabelUse.Leftover, Bit = "L24", Value = "D388", DoubleWord = true, Divisor = 10,
                                          Resolve = len => { var x = new LabelValues(); x[LabelKeys.ProfileCode] = "P77"; return x; } };
        LabelTriggers.Register(pieceTrig);
        LabelTriggers.Register(leftTrig);
        Check("registered off (until switched on)", !pieceTrig.IsOn && !leftTrig.IsOn && LabelTriggers.All.Count == 2);
        sent.Clear();
        sim.Write("D386", 70000 & 0xFFFF); sim.Write("D387", 70000 >> 16); sim.Write("L2", 1);
        LabelTriggers.Poll();
        Check("  off: the bit left on, nothing printed", sim.Read("L2") == 1 && sent.Count == 0);
        LabelTriggers.Set(pieceTrig, true, "L2", "D386", 1);
        LabelTriggers.Stop();
        LabelTriggers.Poll();
        Check("on: the piece's ID read (32-bit 70000), the bit turned off, its label printed", asked.SequenceEqual(new[] { 70000.0 }) && sim.Read("L2") == 0 && sent.Count == 1 &&
              Encoding.ASCII.GetString(sent[0].Value).Contains("P77"), string.Join(",", asked) + " L2=" + sim.Read("L2") + " sent " + sent.Count);
        Check("  in Recent: for piece 70000 (L2)", LabelService.Recent[0].Source.Contains("piece 70000") && LabelService.Recent[0].Source.Contains("L2"), LabelService.Recent[0].Source);
        LabelTriggers.Poll();
        sim.Write("D386", 70001 & 0xFFFF); sim.Write("D387", 1); sim.Write("L2", 1);
        LabelTriggers.Poll();
        Check("  the next request: the next label", asked.Count == 2 && asked[1] == 70001 && sent.Count == 2);
        LabelTriggers.Set(leftTrig, true, "L24", "D388", 10);
        LabelTriggers.Stop();
        sim.Write("D388", 6555); sim.Write("D389", 0); sim.Write("L24", 1);
        LabelTriggers.Poll();
        var leftText = Encoding.ASCII.GetString(sent.Last().Value);
        Check("a leftover: its length 6555 / 10 = 655.5 mm on the leftover's label, its code P77-L0656, L24 off",
              sim.Read("L24") == 0 && leftText.Contains("655.5") && leftText.Contains("P77-L0656") && sent.Last().Key == "BROKEN", leftText.Replace("\r\n", " | "));
        LabelTriggers.Clear();
        LabelTriggers.Register(new LabelTrigger { ModuleId = "FA2010", Name = "FA2010", Station = 1, Use = LabelUse.Piece, Bit = "L2", Value = "D386" });
        Check("  switched and its devices kept over a restart (LabelTriggers.cfg)", LabelTriggers.All[0].IsOn && File.ReadAllText(LabelTriggers.SettingsPath).Contains("FA2010|1|Leftover\ton\tL24\tD388\t10"));
        LabelTriggers.Clear();

        // ================================================================ FA2010: the labels of the bars sent
        Console.WriteLine("--- FA2010: the labels of the bars sent ---");
        var asm = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly;
        var jl = asm.GetType("Kaban.Machines.FA2010.Jobs.JobLabels");
        jl.GetField("StatePath", Any).SetValue(null, Path.Combine(dir, "JobLabels.cfg"));
        jl.GetMethod("Reset", Any).Invoke(null, null);
        var job = KbnFile.Parse(new[] {
            "Bar;3;ML8-F-4501;Frame 70;1;White;65000;;",
            "Profile;12505;11605;450;450;T1;4;ORD-9;2;KABAN;L;;7;R70;11500;USR123;c1;c2;c3;frame.png;Dealer;0;0;0;1;;0",
            "Profile;8000;7100;450;900;T1;5;ORD-9;3;KABAN;R;;7;;0;;;;;;;0;0;0;1;;0" }, "Job 9");
        job.Path = Path.Combine(dir, "Job 9.kbn");
        var planT = asm.GetType("Kaban.Machines.FA2010.Jobs.BarPlan");
        var stepT = asm.GetType("Kaban.Machines.FA2010.Jobs.PlanStep");
        var plan = Activator.CreateInstance(planT, true);
        planT.GetField("Bar").SetValue(plan, job.Bars[0]);
        var steps = (IList)planT.GetField("Steps").GetValue(plan);
        foreach (var st in new[] { new[] { 21, 0 }, new[] { 22, 0 }, new[] { 23, 1 }, new[] { 24, -1 } }) {
            var x = Activator.CreateInstance(stepT, true);
            stepT.GetField("No").SetValue(x, st[0]); stepT.GetField("Piece").SetValue(x, st[1]);
            steps.Add(x);
        }
        jl.GetMethod("Take", Any).Invoke(null, new[] { plan, job });
        Func<double, LabelValues> piece = id => (LabelValues)jl.GetMethod("Piece", Any).Invoke(null, new object[] { id });
        var p21 = piece(21); var p23 = piece(23);
        Check("a bar sent: each step's ID gives its piece's values (the job file's)",
              p21 != null && p21[LabelKeys.ProfileCode] == "ML8-F-4501" && p21[LabelKeys.CutLength] == "1250.5" && p21[LabelKeys.OtherLength] == "1160.5" &&
              p21[LabelKeys.LeftAngle] == "45" && p21[LabelKeys.OrderNo] == "ORD-9" && p21[LabelKeys.Customer] == "KABAN" && p21[LabelKeys.Color] == "White" &&
              p21[LabelKeys.TrolleyName] == "T1" && p21[LabelKeys.TrolleySection] == "4" && p21[LabelKeys.UserBarcode] == "USR123" && p21[LabelKeys.ReinforcementLength] == "1150" &&
              p21[LabelKeys.JobName] == "Job 9" && p21.PictureFolder == dir && p21[LabelKeys.Picture] == "frame.png",
              p21 == null ? "null" : string.Join(", ", p21.Keys.Select(k => k + "=" + p21[k])));
        Check("  the piece's code: profile, bar, piece (P-0003-01 / -02)", p21[LabelKeys.KabanBarcode] == "ML8-F-4501-0003-01" && piece(22)[LabelKeys.KabanBarcode] == "ML8-F-4501-0003-01" &&
              p23[LabelKeys.KabanBarcode] == "ML8-F-4501-0003-02" && p23[LabelKeys.RightAngle] == "90");
        Check("  a step of no piece, an ID never sent: none", piece(24) == null && piece(99) == null);
        jl.GetField("_loaded", Any).SetValue(null, false);
        ((IDictionary)jl.GetField("_byId", Any).GetValue(null)).Clear();
        ((IList)jl.GetField("_order", Any).GetValue(null)).Clear();
        Check("  kept over a restart (JobLabels.cfg)", piece(23) != null && piece(23)[LabelKeys.CutLength] == "800");
        var lo = (LabelValues)jl.GetMethod("Leftover", Any).Invoke(null, new object[] { 655.0 });
        Check("  a leftover's values: the bar sent last", lo[LabelKeys.ProfileCode] == "ML8-F-4501" && lo[LabelKeys.Color] == "White" && lo[LabelKeys.CutLength] == "");

        File.Delete(LabelTriggers.SettingsPath);
        var module = new Kaban.Machines.FA2010.FA2010Module();
        module.RegisterPlc(new Kaban.Core.Machines.MachineRegistry.MountContext("FA2010", true, null, AppDomain.CurrentDomain.BaseDirectory));
        var reg = LabelTriggers.All;
        Check("FA2010 asks for its labels: a piece L2 + D386, a leftover L24 + D388 / 10 - both off until switched on",
              reg.Count == 2 && reg.Any(x => x.Use == LabelUse.Piece && x.Bit == "L2" && x.Value == "D386" && x.Resolve != null) &&
              reg.Any(x => x.Use == LabelUse.Leftover && x.Bit == "L24" && x.Value == "D388" && x.Divisor == 10) && reg.All(x => !x.IsOn),
              string.Join(" | ", reg.Select(x => LabelTriggers.Describe(x) + (x.IsOn ? " on" : " off"))));
        Check("  the piece's request gives the piece of the bar sent", reg.First(x => x.Use == LabelUse.Piece).Resolve(23)[LabelKeys.CutLength] == "800");

        // ================================================================ the pages
        Console.WriteLine("--- HMI Settings > Barcodes ---");
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        var bpage = new BarcodeSettingsControl();
        var bform = new Form { ClientSize = new Size(1000, 800) };
        bform.Controls.Add(bpage);
        bform.Show();
        Pump(200);
        BarcodeReaders.Start();
        bpage.ReadersGrid.Rows.Add("Scanner", "Serial", "5", "9600", "8", "None", "One", "", true, "");
        bpage.Save();
        Check("a reader added and kept: COM5 - its state shown (it cannot open here)", BarcodeReaders.Readers.Count == 1 && BarcodeReaders.Readers[0].PortName == "COM5" &&
              BarcodeReaders.State.Count == 1 && !BarcodeReaders.State[0].EndsWith("open"), bpage.Status + " / " + string.Join(" | ", BarcodeReaders.State));
        bpage.ReadersGrid.Rows[0].Cells["port"].Value = "LPT1";
        bpage.Save();
        Check("  a serial reader on no COM port: not kept, said", bpage.Status.Contains("COM"), bpage.Status);
        BarcodeReaders.Save(new BarcodeReaderConfig[0], new BarcodeFieldConfig[0], "", "");
        BarcodeReaders.Stop();
        bform.Close();
        bpage = new BarcodeSettingsControl();
        bpage.TryText("P123456789" + "1200" + "0800");
        Check("Try a code: its parts as the machine takes them (none set: the machine's own)", bpage.TryResult.Contains("W 1200") && bpage.TryResult.Contains("H 800"), bpage.TryResult);
        BarcodeReaders.Feed("SHOWN1", "Test");
        bpage = new BarcodeSettingsControl();
        Check("  read last: the codes read", bpage.RecentGrid.Rows.Count > 0 && Convert.ToString(bpage.RecentGrid.Rows[0].Cells["code"].Value) == "SHOWN1");
        if (shots.Length > 0) { Directory.CreateDirectory(shots); Shot(bpage, Path.Combine(shots, "barcodes.png"), 1000, 900); }

        Console.WriteLine("--- HMI Settings > Labels ---");
        foreach (var f in Directory.GetFiles(LabelStore.Folder)) File.Delete(f);
        var lpage = new LabelSettingsControl();
        var lform = new Form { ClientSize = new Size(1000, 900) };
        lform.Controls.Add(lpage);
        lform.Show();
        Pump(200);
        lpage.NewLabel(LabelUse.Piece);
        Pump(100);
        Check("a new label: the usual fields, on (the first for the pieces), drawn", lpage.List.Items.Count == 1 && lpage.FieldsGrid.Rows.Count == 11 &&
              LabelStore.All()[0].Enabled && lpage.Preview.Boxes.Count == 11, lpage.List.Items.Count + " " + lpage.FieldsGrid.Rows.Count + " " + lpage.Preview.Boxes.Count);
        Check("  the triggers' table lists FA2010's two requests", lpage.TriggersGrid.Rows.Count == 2);
        lpage.PrinterBox.Text = "file:" + Path.Combine(dir, "test.prn");
        lpage.Save();
        Check("  its printer kept", LabelStore.All()[0].Printer.StartsWith("file:"), LabelStore.All()[0].Printer);
        LabelPrinters.Send = realSend;
        lpage.Test();
        var testPrn = File.Exists(Path.Combine(dir, "test.prn")) ? File.ReadAllText(Path.Combine(dir, "test.prn"), Encoding.GetEncoding(28591)) : "";
        Check("Test print: the label with sample values on its printer (a file here)", testPrn.Contains("I8,E,001") && testPrn.Contains("P1234-0012-03") && testPrn.Contains("P1"),
              lpage.Status);
        lpage.TriggersGrid.Rows[0].Cells["on"].Value = true;
        lpage.TriggersGrid.Rows[0].Cells["bit"].Value = "L3";
        lpage.Save();
        var t0 = LabelTriggers.All.First(x => x.Use == LabelUse.Piece);
        Check("  a request switched on, its bit changed (kept)", t0.IsOn && t0.Bit == "L3", LabelTriggers.Describe(t0));
        if (shots.Length > 0) { lform.Controls.Remove(lpage); Shot(lpage, Path.Combine(shots, "labels.png"), 1000, 1000); }
        LabelTriggers.Set(t0, false, "L2", "D386", 1);
        LabelTriggers.Stop();

        Console.WriteLine("--- Code 128 (the drawing) ---");
        var w128 = Kaban.UI.Controls.Code128.Widths("ABC");
        Check("ABC: start B, 3 characters, its check (1), stop - 68 modules", w128.Sum() == 11 * 5 + 13 &&
              string.Join("", w128.Take(6)) == "211214" && string.Join("", w128.Skip(24).Take(6)) == "222122", string.Join("", w128));

        Console.WriteLine(fail == 0 ? "ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
