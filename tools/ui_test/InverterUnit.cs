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
using Kaban.Core.Models;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

// 14 October: "We should have a control for inverter as well (just like CylinderUnitHMI) so we could
// automatically set its error and open a proper helper for it (using its error code) ... The error should
// not be displayed on the screen ... create a helper according to the alarm code (at the same format as the
// servo axis error troubleshooting page), give the whole document as the second helper element (so there is
// one helper at least even when the code is not found on the list)."
class INV {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static T Field<T>(object o, string n) {
        for (var t = o.GetType(); t != null; t = t.BaseType) {
            var f = t.GetField(n, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) return (T)f.GetValue(o);
        }
        return default(T);
    }
    static Dictionary<string, int> mem = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    static readonly List<string> writes = new List<string>();
    sealed class Plc : Kaban.Core.Interfaces.IPlcDriver {
        public bool IsConnected { get { return true; } }
        public bool IsHealthy { get { return true; } }
        public bool Connect() { return true; }
        public void Disconnect() { }
        public int Read(string a) { int v; return mem.TryGetValue(a, out v) ? v : 0; }
        public int[] ReadMulti(string[] a) { return a.Select(Read).ToArray(); }
        public int Write(string a, int v) { writes.Add(a + "=" + v); mem[a] = v; return 0; }
        public bool TryRead(string a, out int v) { v = Read(a); return true; }
        public bool TryReadMulti(string[] a, out int[] v) { v = ReadMulti(a); return true; }
    }
    static int Read(string a) { int v; return mem.TryGetValue(a, out v) ? v : 0; }
    static DateTime now = new DateTime(2026, 10, 15, 8, 0, 0);
    static void Scan() { now = now.AddSeconds(0.1); AlarmGeneration.Evaluate(1, Read, now); AlarmManager.Evaluate(1, Read); }
    static List<AlarmItem> Gen() { return AlarmManager.ActiveAlarms.Where(a => a.Generated).ToList(); }
    static string Codes() { return string.Join(",", AlarmManager.ActiveAlarms.Select(a => a.ShownCode)); }
    static IEnumerable<Control> All(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var g in All(k)) yield return g; } }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "inv_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        ViewPreferences.SettingsPath = Path.Combine(dir, "View.cfg");
        AlarmGeneration.SettingsPath = Path.Combine(dir, "AlarmGeneration.cfg");
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        LanguageManager.LoadMachineTranslations("Translations.csv");
        LanguageManager.ChangeLanguage("EN");
        SecurityManager.Login("m", SecurityLevel.Maintenance);
        var manualPdf = Path.Combine(TroubleshootingCatalog.HelpFolder, AxisCodeHelp.InverterManualFile);
        if (File.Exists(manualPdf)) File.Delete(manualPdf);

        Console.WriteLine("--- the fault list (FR-A800 6.4 data codes, 6.5 causes and corrective actions) ---");
        var K = AxisCodeKind.InverterFault;
        Check("the data code as the manual's table gives it - decimal: 16", AxisCodeHelp.Format(K, 16) == "16" && AxisCodeHelp.Key(K, 16) == "16");
        Check("  named as the inverter's panel shows it: E.OC1; 128 is E.GF", AxisCodeHelp.Display(K, 16) == "E.OC1" && AxisCodeHelp.Display(K, 128) == "E.GF",
              AxisCodeHelp.Display(K, 16) + " " + AxisCodeHelp.Display(K, 128));
        var said = AxisCodeHelp.Describe(K, 16);
        Check("  described: 'Inverter fault E.OC1 (16) - Overcurrent trip during acceleration'", said == "Inverter fault E.OC1 (16) - Overcurrent trip during acceleration", said);
        var doc = AxisCodeHelp.Document(K, 16);
        Check("its entry: Inverter_Fault_Codes, code 16", doc != null && doc.FileName == AxisCodeHelp.InverterFaultDocument && doc.Code == "16" && doc.Exists);
        var page = File.ReadAllText(AxisCodeHelp.Extract(doc));
        Check("  the page holds that fault alone - its description, check points and corrective actions",
              System.Text.RegularExpressions.Regex.Matches(page, "<section class=\"code\"").Count == 1 && page.Contains("E.OC1") &&
              page.Contains("Check point") && page.Contains("Corrective action") && page.Contains("Set a longer acceleration time"));
        Check("  and the link to the whole manual", page.Contains(AxisCodeHelp.InverterManualUrl));
        var sameAll = new[] { 241, 242, 243 }.Select(c => AxisCodeHelp.Display(K, c)).ToArray();
        Check("a fault of several data codes (E.1 to E.3): one entry each", sameAll.SequenceEqual(new[] { "E.1", "E.2", "E.3" }), string.Join(",", sameAll));
        var html = File.ReadAllText(doc.FullPath);
        int sections = System.Text.RegularExpressions.Regex.Matches(html, "<section class=\"code\"").Count;
        Check("every fault of 6.4 is in the list", sections >= 70 && new[] { 16, 17, 18, 32, 33, 34, 48, 49, 64, 80, 81, 82, 96, 112, 128, 129, 144, 160, 176, 192, 201, 208, 230, 251 }
              .All(c => AxisCodeHelp.Find(K, c) != null), sections + " sections");
        // 15 October, checked against the user's copy of the manual (IB-0600503ENG-N): E.PE6 and E.ECA have data codes,
        // E.SCF (output short-circuit, 20) was missing, E.EHR is an Ethernet model's fault this manual does not have
        Check("  E.SCF 20, E.PE6 172, E.ECA 212 by their data codes; E.EHR (not in the manual) gone; every fault by a number",
              AxisCodeHelp.Display(K, 20) == "E.SCF" && AxisCodeHelp.Display(K, 172) == "E.PE6" && AxisCodeHelp.Display(K, 212) == "E.ECA" &&
              !html.Contains("E.EHR") && !html.Contains("data-code=\"E."),
              AxisCodeHelp.Display(K, 20) + " " + AxisCodeHelp.Display(K, 172) + " " + AxisCodeHelp.Display(K, 212));
        Check("  each fault links its page of the manual: E.OC1 page 805 (the PDF's 806), the Help folder's copy and online",
              page.Contains("page 805") && page.Contains(AxisCodeHelp.InverterManualFile + "#page=806") && page.Contains(AxisCodeHelp.InverterManualUrl + "#page=806") &&
              System.Text.RegularExpressions.Regex.Matches(html, "#page=\\d+\"").Count == 2 * sections);
        var unknown = AxisCodeHelp.Document(K, 999);
        var up = unknown == null ? "" : File.ReadAllText(AxisCodeHelp.Extract(unknown));
        Check("a code the list does not have: the list says so and links the manual", unknown != null && up.Contains("not listed") && up.Contains(AxisCodeHelp.InverterManualUrl));
        var man = AxisCodeHelp.ManualDocument(K);
        Check("the whole manual: the manufacturer's link while there is no copy in the Help folder", man != null && man.IsWebLink && man.FileName == AxisCodeHelp.InverterManualUrl);
        File.WriteAllText(manualPdf, "%PDF-1.4 test");
        man = AxisCodeHelp.ManualDocument(K);
        Check("  the copy in the Help folder once it is there (" + AxisCodeHelp.InverterManualFile + ")", man != null && !man.IsWebLink && man.FileName == AxisCodeHelp.InverterManualFile);
        File.Delete(manualPdf);
        Check("the axis kinds have no such entry (their manuals are their alarms')", AxisCodeHelp.ManualDocument(AxisCodeKind.ServoAlarm) == null);

        Console.WriteLine("--- the alarm the HMI raises itself ---");
        var plc = new Plc();
        AlarmGeneration.DriverForWrite = st => st == 1 ? plc : null;
        AlarmManager.RegisterDefinition("T.Saw", "Saw Inverter Alarm", AlarmSeverity.Fault, plcNumber: 1, address: "M830");
        AlarmGeneration.ResetForTests();
        AlarmManager.ActiveAlarms.Clear();
        var unit = new InverterUnitHMI { TitleTextKey = "Saw Motor", CommandAAddress = "M636", CommandAAction = BitActionType.Alternate,
                                         FaultCodeAddress = "D669", ErrorAddress = "M830" };
        var watch = unit.BuildInverterWatch();
        Check("the unit's watch: its fault code word and its alarm bit", watch != null && watch.CodeAddress == "D669" && watch.ErrorAddress == "M830" && watch.Name == "Saw Motor");
        watch.ModuleId = "T";
        AlarmGeneration.Register(watch);
        Check("  the code word is read", AlarmGeneration.Addresses().Any(k => k.Key == 1 && k.Value == "D669"));
        Scan();
        Check("code 0: nothing", Gen().Count == 0);
        writes.Clear();
        mem["D669"] = 16; Scan();
        var g = Gen().SingleOrDefault();
        Check("E.OC1: its alarm at once, named by the inverter's panel (M830.E.OC1), the fault in words",
              g != null && g.ShownCode == "M830.E.OC1" && g.Description.Contains("Overcurrent trip during acceleration"), Codes() + " / " + (g != null ? g.Description : ""));
        Check("  its alarm bit SET in the PLC", writes.Contains("M830=1"), string.Join(",", writes));
        mem["M830"] = 1; Scan();
        Check("  the PLC's own alarm for M830 is not listed beside it", AlarmManager.ActiveAlarms.Count == 1, Codes());
        var docs = AlarmHelpLinks.DocumentsFor(g);
        Check("its help: the fault's entry first, the whole manual second", docs.Count == 2 && docs[0].Code == "16" && docs[0].FileName == AxisCodeHelp.InverterFaultDocument &&
              docs[1].FileName == AxisCodeHelp.InverterManualUrl, string.Join(" | ", docs.Select(d => d.Title + " " + d.FileName)));
        AlarmAiPrompt.ReadNow = (st, a) => Read(a);
        var brief = AlarmAiPrompt.Build(g, null, docs);
        Check("its AI briefing: an inverter's motor, the fault's data code now and its name, the manual, the fault's own troubleshooting as text",
              brief.Contains("Kind: a motor driven by a Mitsubishi FR-A800 inverter") && brief.Contains("The inverter's fault code word: D669, now 16 - Inverter fault E.OC1 (16)") &&
              brief.Contains("the inverter's own panel shows E.OC1") && brief.Contains(AxisCodeHelp.InverterManualUrl) && brief.Contains("Set a longer acceleration time"),
              string.Join(" / ", brief.Split('\n').Where(l => l.Contains("Kind:") || l.Contains("code word"))));
        mem["D669"] = 999; Scan();
        g = Gen().SingleOrDefault();
        docs = AlarmHelpLinks.DocumentsFor(g);
        Check("a code not in the list: the alarm all the same (M830.999), and the manual is still there",
              g != null && g.ShownCode == "M830.999" && docs.Any(d => d.FileName == AxisCodeHelp.InverterManualUrl), Codes() + " / " + string.Join(" | ", docs.Select(d => d.FileName)));
        mem["D669"] = 0; mem["M830"] = 0; Scan();
        Check("the code clears: the line goes", Gen().Count == 0 && AlarmManager.ActiveAlarms.Count == 0, Codes());
        AlarmGeneration.Save("T", new AlarmGeneration.Settings { Enabled = false, AxisAlarms = false });
        mem["D669"] = 16; Scan();
        Check("with the axes' switch off: neither read nor raised", Gen().Count == 0 && !AlarmGeneration.Addresses().Any(k => k.Value == "D669"));
        AlarmGeneration.Save("T", new AlarmGeneration.Settings { Enabled = false, AxisAlarms = true });
        mem["D669"] = 0; Scan();

        Console.WriteLine("--- the unit ---");
        var host = new Form { ClientSize = new Size(900, 300) };
        var conv = new InverterUnitHMI { TitleTextKey = "Conveyor Motor", CommandAAddress = "M640", CommandBAddress = "M639", Lamp1BAddress = "M221",
                                         FaultCodeAddress = "D666", ErrorAddress = "M827", RunLampAddress = "M203", Location = new Point(10, 10) };
        host.Controls.Add(conv); host.Show(); Application.DoEvents();
        var props = TypeDescriptor.GetProperties(conv).Cast<PropertyDescriptor>().Where(p => p.IsBrowsable).Select(p => p.Name).ToList();
        Check("its own properties: FaultCodeAddress, ErrorAddress, RunLampAddress", new[] { "FaultCodeAddress", "ErrorAddress", "RunLampAddress", "RunLampTipKey" }.All(props.Contains));
        Check("  a cylinder's are not offered: no valve, feedback, type check, per-side error bits",
              !props.Contains("ValveType") && !props.Contains("FeedbackType") && !props.Contains("TypeCheck") && !props.Contains("ErrorAAddress") && !props.Contains("ErrorBAddress"),
              string.Join(",", props.Where(p => p.StartsWith("Valve") || p.StartsWith("Feedback") || p.StartsWith("Error") || p == "TypeCheck")));
        Check("  and nothing to report against a valve", conv.TypeProblems().Count == 0);
        var badge = Field<AlarmHelpBadge>(conv, "_helpB");
        var mark = Field<UnitHelpBadge>(conv, "_help");
        Check("the fault code word is read with the unit (not shown anywhere)", badge.ExtraAddresses.Contains("D666") &&
              !All(conv).OfType<NumericDisplayHMI>().Any(n => n.Visible && n.Address == "D666"), string.Join(",", badge.ExtraAddresses));
        Check("  its alarm bit is the one the '?' watches too", badge.Address == "M827");
        var lamp = conv.RunLamp;
        Check("the run lamp M203 before the unit's name, on the frame", lamp.Parent != null && lamp.Address == "M203" && lamp.Top <= 2 && lamp.Left < 20,
              lamp.Parent + " " + lamp.Bounds);
        int m827 = 0, d666 = 0, m203 = 0;
        Func<string, int> look = a => a == "M827" ? m827 : a == "D666" ? d666 : a == "M203" ? m203 : 0;
        badge.UpdateState(look, c => true);
        Check("no fault: no '?', no blinking", !mark.Visible && !conv.ButtonA.AlertBlink && !conv.ButtonB.AlertBlink);
        m203 = 1; lamp.UpdateState(look, c => true);
        Check("  running: the lamp lit", Field<bool>(lamp, "_isOn"));
        d666 = 128; badge.UpdateState(look, c => true);
        Check("E.GF (128): the '?' shows, both buttons blink red", mark.Visible && mark.State == UnitHelpBadge.BadgeState.Error &&
              conv.ButtonA.AlertBlink && conv.ButtonB.AlertBlink, mark.State.ToString());
        Check("  the value beside them does not blink", !conv.PositionDisplay.AlertBlink);
        Check("  its tooltip names the fault", badge.TipText.Contains("E.GF") && badge.TipText.Contains("earth"), badge.TipText);
        docs = conv.HelpDocuments();
        Check("  its list: the E.GF entry, then the whole manual", docs.Count == 2 && docs[0].Code == "128" && docs[1].FileName == AxisCodeHelp.InverterManualUrl,
              string.Join(" | ", docs.Select(d => d.Title)));
        Check("  the entry is named by the fault, not the unit", docs[0].Title.Contains("E.GF") && !docs[0].Title.Contains("Conveyor"), docs[0].Title);
        d666 = 0; m827 = 1; badge.UpdateState(look, c => true);
        docs = conv.HelpDocuments();
        Check("its alarm bit with no code: the '?' still offers the manual", mark.Visible && docs.Count == 1 && docs[0].FileName == AxisCodeHelp.InverterManualUrl,
              string.Join(" | ", docs.Select(d => d.FileName)));
        m827 = 0; badge.UpdateState(look, c => true);
        Check("all clear: the '?' goes, the blinking stops", !mark.Visible && !conv.ButtonA.AlertBlink);
        var links = conv.HelpLinks().ToList();
        Check("the alarm list's help knows it: an inverter's link, with its code word", links.Count == 1 && links[0].Kind == TroubleshootingCatalog.ErrorType.InverterMotor &&
              links[0].InverterFault == "D666" && links[0].Address == "M827");
        conv.RunLampAddress = "";
        Check("no run lamp set: none on the frame", conv.RunLamp.Parent == null);
        Check("an inverter's motor is not coupled with others: no chain", !Field<Control>(conv, "_couple").Visible);

        Console.WriteLine("--- FA2010's six inverters ---");
        var fa = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly;
        Func<string, Control> Page = n => (Control)Activator.CreateInstance(fa.GetTypes().First(t => t.Name == n));
        var units = new[] { "ManualEntry_Conveyor", "ManualMilling", "ManualCutting", "ManualExit" }
            .SelectMany(n => { var p = Page(n); return All(p).OfType<InverterUnitHMI>().Select(u => n + ":" + u.Name + ":" + u.FaultCodeAddress + ":" + u.ErrorAddress); }).ToList();
        Check("one unit each, its fault word and its alarm bit", units.OrderBy(x => x).SequenceEqual(new[] {
              "ManualCutting:unitSaw_Motor:D669:M830", "ManualCutting:unitV_Cutting_Saw_Motor:D670:", "ManualEntry_Conveyor:unitConveyor_Motor:D666:M827",
              "ManualExit:unitConveyor_Motor:D671:M831", "ManualMilling:unitUnit_1_Milling_Motor:D667:M828", "ManualMilling:unitUnit_2_Milling_Motor:D668:M829" }),
              string.Join(" ", units));
        var shown = new[] { "ManualEntry_Conveyor", "ManualMilling", "ManualCutting", "ManualExit" }
            .SelectMany(n => All(Page(n)).OfType<NumericDisplayHMI>().Where(d => new[] { "D666", "D667", "D668", "D669", "D670", "D671" }.Contains(d.Address))).Count();
        Check("  no error code displayed on the pages any more", shown == 0, shown.ToString());
        var mill = Page("ManualMilling");
        var m1 = All(mill).OfType<InverterUnitHMI>().First(u => u.Name == "unitUnit_1_Milling_Motor");
        var cool = mill.Controls.Find("btnUnit_1_Tool_1_Cooling", true).First();
        Check("Milling: the motor's unit where its button was, the coolings under it", m1.Top == 710 && cool.Top >= m1.Bottom + 6 && cool.Top - m1.Bottom <= 12,
              m1.Bounds + " / " + cool.Top);
        AlarmGeneration.ResetForTests();
        var harvest = typeof(AlarmHelpLinks).GetMethod("HarvestControls", BindingFlags.NonPublic | BindingFlags.Static);
        harvest.Invoke(null, new object[] { Page("ManualCutting") });
        Check("discovery registers the inverters' watches", AlarmGeneration.Inverters.Select(v => v.CodeAddress).OrderBy(x => x).SequenceEqual(new[] { "D669", "D670" }),
              string.Join(",", AlarmGeneration.Inverters.Select(v => v.CodeAddress)));

        Console.WriteLine(fail == 0 ? "\nINVERTER ALL PASS" : "\nINVERTER " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
