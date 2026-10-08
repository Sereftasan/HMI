using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Forms;
using Kaban.UI.Helpers;

// 8 October: "We should add "PopupTriggerAddress" property to each machine ... "D71" in "FA2010" ...
// The HMI should read the entered address value to show the PLC's messages to the user. We should also
// have "PopupResponseAddress" property to enter the user's answer ... ("CANCEL" is coded as "0", "OK" is
// coded as "1", etc.). Any click on the popup buttons should write "0" to "PopupTriggerAddress" value and
// user's answer code to "PopupResponseAddress" value." - on the real FA2010 program, its simulation PLC.
class PPU {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d.Replace("\n", " / ") + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); } }
    static Kaban.Drivers.Simulation.SimulationDriver plc;
    static int Read(string a) { int v; return plc.TryRead(a, out v) ? v : -1; }
    static void Scan() { PlcPopups.Evaluate(1, a => Read(a), DateTime.Now); Pump(250); }
    static Control Find(Control root, string name) {
        if (root.Name == name) return root;
        foreach (Control c in root.Controls) { var f = Find(c, name); if (f != null) return f; }
        return null;
    }
    static string Buttons(Form f) {
        var bar = Find(f, "buttons");
        return bar == null ? "" : string.Join(",", bar.Controls.Cast<Control>().Where(c => c.Visible && c.Name != "btnSignIn").Reverse().Select(c => c.Text));
    }

    // PPU_SNAP=<folder>: pictures of the messages (the manual's, the previews)
    static void Snap(Form f, string name) {
        var dir = Environment.GetEnvironmentVariable("PPU_SNAP");
        if (string.IsNullOrEmpty(dir) || f == null) return;
        f.Activate(); Pump(400);
        var b = f.Bounds;
        var p = System.Diagnostics.Process.Start("import", "-window root -crop " + b.Width + "x" + b.Height + "+" + b.X + "+" + b.Y + " " + Path.Combine(dir, name + ".png"));
        p.WaitForExit();
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "ppu_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        typeof(SimulationMonitorForm).GetField("WatchFolder", Any).SetValue(null, dir);
        typeof(MainForm).GetField("SidePanelPath", Any).SetValue(null, Path.Combine(dir, "SidePanel.cfg"));
        DisplaySettings.SettingsPath = Path.Combine(dir, "Display.cfg");
        AlarmGeneration.SettingsPath = Path.Combine(dir, "AlarmGeneration.cfg");
        NotesStore.Folder = Path.Combine(dir, "Notes");
        var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
        Func<string, MethodInfo> m = n => prog.GetMethod(n, BindingFlags.NonPublic | BindingFlags.Static);
        UnitManager.EnsureDefaults();
        m("RegisterDriverKinds").Invoke(null, null);
        m("MountMachines").Invoke(null, null);
        var sim = new Dictionary<int, Kaban.Drivers.Simulation.SimulationDriver>();
        foreach (int st in new[] { 1, 2, 3, 4 }) { sim[st] = new Kaban.Drivers.Simulation.SimulationDriver(st); PlcManager.Instance.RegisterDriver(st, sim[st]); }
        plc = sim[1];
        SecurityManager.Login("admin", SecurityLevel.Admin);
        var form = new MainForm();
        form.BuildNavigation();
        form.Load += (s, e) => SecurityManager.Login("admin", SecurityLevel.Admin);
        form.Show();
        Pump(400);
        AlarmHelpLinks.EnsureDiscovered();

        Console.WriteLine("--- the machine's devices and messages ---");
        Check("FA2010 declares PopupTriggerAddress D71 and PopupResponseAddress D72 (beside its program version D36)",
              Kaban.Machines.FA2010.FA2010Module.PopupTriggerAddress == "D71" && Kaban.Machines.FA2010.FA2010Module.PopupResponseAddress == "D72");
        var dev = PlcPopups.DevicesOf(1);
        Check("  station 1 reads D71 and answers in D72", dev != null && dev.Trigger == "D71" && dev.Response == "D72" && dev.ModuleId == "FA2010");
        Check("  the trigger is on the scanner's list", PlcPopups.Addresses().Any(kv => kv.Key == 1 && kv.Value == "D71"));
        var all = PlcPopups.All(1);
        Check("its messages (Popups.json): 1-16 and 20-23, in six languages",
              all.Select(p => p.Code).SequenceEqual(Enumerable.Range(1, 16).Concat(Enumerable.Range(20, 4))) &&
              all.All(p => new[] { "EN", "TR", "RU", "DE", "IT", "ES" }.All(l => p.Title.ContainsKey(l) && p.Text.ContainsKey(l))),
              string.Join(",", all.Select(p => p.Code)));
        Check("  the answers: Cancel / No 0, OK / Yes / Continue 1, Restart / Retry 2",
              PlcPopups.DefaultCode("Cancel") == 0 && PlcPopups.DefaultCode("No") == 0 && PlcPopups.DefaultCode("OK") == 1 &&
              PlcPopups.DefaultCode("Yes") == 1 && PlcPopups.DefaultCode("Continue") == 1 && PlcPopups.DefaultCode("Restart") == 2);

        Console.WriteLine("--- the PLC asks: D71 = 2 ---");
        plc.Write("D72", 77);
        plc.Write("D71", 2);
        Scan();
        var pop = PlcPopupHost.OpenFor(1);
        Check("the message on show, over the main window, its title and text", pop != null && pop.Visible && pop.Owner == form &&
              pop.Text == "Entry conveyor empty" && Find(pop, "message").Text.StartsWith("No profile has reached the profile recognition unit!"),
              pop == null ? "none" : pop.Text);
        Check("  its machine and number on the band", pop != null && Find(pop, "machine").Text.EndsWith("PLC message 2"), pop == null ? "" : Find(pop, "machine").Text);
        Snap(pop, "popup2");
        Check("  its buttons: Yes, No", pop != null && Buttons(pop) == "Yes,No", pop == null ? "" : Buttons(pop));
        Check("  not modal: the main window still works", form.Enabled && pop != null && !pop.Modal);
        Check("  a scan more changes nothing", ((Func<bool>)(() => { Scan(); return PlcPopupHost.OpenFor(1) == pop; }))());

        Console.WriteLine("--- signed out: only after signing in ---");
        SecurityManager.Logout();
        Pump(200);
        Snap(pop, "popup2_signin");
        Check("the buttons off, Sign in shown", pop != null && !Find(pop, "btnYes").Enabled && Find(pop, "btnSignIn").Visible && Find(pop, "note").Visible);
        ((Button)Find(pop, "btnYes")).PerformClick();
        Pump(100);
        Check("  nothing written", Read("D72") == 77 && Read("D71") == 2);
        SecurityManager.Login("admin", SecurityLevel.Admin);
        Pump(200);
        Check("signed in: the buttons on", Find(pop, "btnYes").Enabled && !Find(pop, "btnSignIn").Visible);

        Console.WriteLine("--- the answer ---");
        ((Button)Find(pop, "btnYes")).PerformClick();
        Pump(200);
        Check("Yes: 1 to D72, then 0 to D71, the message closed", Read("D72") == 1 && Read("D71") == 0 && PlcPopupHost.OpenFor(1) == null && pop.IsDisposed,
              "D72=" + Read("D72") + " D71=" + Read("D71"));
        Scan();
        Check("  the next scan: nothing shown again", PlcPopupHost.OpenFor(1) == null && PlcPopups.Shown(1) == 0);
        var log = File.ReadAllText(Directory.GetFiles(dir, "*.log", SearchOption.AllDirectories).Concat(Directory.GetFiles(dir, "*.txt", SearchOption.AllDirectories)).First());
        Check("  in the log: shown and answered by whom", log.Contains("PLC message 2 on station 1 shown.") && log.Contains("PLC message 2 on station 1 answered 1 (D72) by admin"));

        Console.WriteLine("--- 8: Restart / Cancel with the measures ---");
        plc.Write("D8", 60); plc.Write("D9", 40); plc.Write("D481", 62); plc.Write("D482", 41);
        plc.Write("D71", 8);
        Scan();
        pop = PlcPopupHost.OpenFor(1);
        pop.RefreshValues();
        Check("Wrong profile: Restart, Cancel", pop != null && pop.Text == "Wrong profile" && Buttons(pop) == "Restart,Cancel", pop == null ? "" : Buttons(pop));
        Check("  the defined and the measured width and height, read from the PLC",
              Find(pop, "valueD8").Text == "60 mm" && Find(pop, "valueD481").Text == "62 mm" && Find(pop, "valueD9").Text == "40 mm" && Find(pop, "valueD482").Text == "41 mm",
              Find(pop, "valueD8").Text + " / " + Find(pop, "valueD481").Text);
        plc.Write("D481", 65);
        Pump(700);
        Snap(pop, "popup8");
        Check("  read again while open", Find(pop, "valueD481").Text == "65 mm", Find(pop, "valueD481").Text);
        var keys = new KeyEventArgs(Keys.Escape);
        typeof(Control).GetMethod("OnKeyDown", Any).Invoke(pop, new object[] { keys });
        Pump(200);
        Check("Esc: Cancel - 0 to D72, 0 to D71", Read("D72") == 0 && Read("D71") == 0 && PlcPopupHost.OpenFor(1) == null, "D72=" + Read("D72"));
        plc.Write("D71", 8);
        Scan();
        ((Button)Find(PlcPopupHost.OpenFor(1), "btnRestart")).PerformClick();
        Pump(200);
        Check("Restart: 2 to D72", Read("D72") == 2 && Read("D71") == 0);

        Console.WriteLine("--- 1: the PLC's own state, taken back by the PLC ---");
        plc.Write("D72", 55);
        plc.Write("D71", 1);
        Scan();
        pop = PlcPopupHost.OpenFor(1);
        Check("Referencing: only Hide (nothing to answer)", pop != null && Buttons(pop) == "Hide", pop == null ? "" : Buttons(pop));
        plc.Write("D71", 0);
        Scan();
        Check("  D71 = 0 from the PLC: closed, nothing written", PlcPopupHost.OpenFor(1) == null && pop.IsDisposed && Read("D72") == 55);

        Console.WriteLine("--- another number while one is open ---");
        plc.Write("D71", 3);
        Scan();
        var first = PlcPopupHost.OpenFor(1);
        plc.Write("D71", 4);
        Scan();
        var second = PlcPopupHost.OpenFor(1);
        Check("the new one replaces it", first != null && first.IsDisposed && second != null && second.Text == "Start warning", second == null ? "" : second.Text);
        ((Button)Find(second, "btnOK")).PerformClick();
        Pump(200);

        Console.WriteLine("--- 5: the axes ---");
        plc.Write("D71", 5);
        Scan();
        pop = PlcPopupHost.OpenFor(1);
        var axes = AlarmHelpLinks.AllAxes.Where(a => a.Station == 1).ToList();
        var lamps = new List<Control>();
        Action<Control> walk = null; walk = c => { if (c.Name.StartsWith("lamp")) lamps.Add(c); foreach (Control k in c.Controls) walk(k); };
        walk(pop);
        Check("Servo out of limits: a row per axis, its reference and fault lamps", axes.Count > 0 && lamps.Count == axes.Count * 2,
              axes.Count + " axes, " + lamps.Count + " lamps");
        var ax = axes.First(a => !string.IsNullOrEmpty(a.Error));
        plc.Write(ax.Error, 1);
        pop.RefreshValues();
        var lit = lamps.Where(l => l.Name == "lamp" + ax.Error.ToUpperInvariant()).Select(l => ((Func<bool>)l.Tag)()).FirstOrDefault();
        Check("  a fault lit from the PLC", lit, ax.Error);
        Snap(pop, "popup5");
        Check("  it fits on the screen", pop.Height <= Screen.PrimaryScreen.WorkingArea.Height, pop.Height.ToString());
        plc.Write(ax.Error, 0);
        ((Button)Find(pop, "btnOK")).PerformClick();
        Pump(200);

        Console.WriteLine("--- a number the file does not have ---");
        plc.Write("D71", 99);
        Scan();
        pop = PlcPopupHost.OpenFor(1);
        Check("shown all the same, with OK", pop != null && Find(pop, "message").Text.Contains("message 99") && Buttons(pop) == "OK", pop == null ? "" : Find(pop, "message").Text);
        ((Button)Find(pop, "btnOK")).PerformClick();
        Pump(200);
        Check("  OK: 1 to D72, 0 to D71", Read("D72") == 1 && Read("D71") == 0);

        Console.WriteLine("--- in Turkish ---");
        LanguageManager.ChangeLanguage("TR");
        plc.Write("D71", 14);
        Scan();
        pop = PlcPopupHost.OpenFor(1);
        Snap(pop, "popup14_tr");
        Check("the Turkish title and buttons", pop != null && pop.Text == PlcPopups.Find(1, 14).TitleIn("TR") && Buttons(pop) == "Evet,Hayır", pop == null ? "" : pop.Text + " " + Buttons(pop));
        ((Button)Find(pop, "btnNo")).PerformClick();
        Pump(200);
        Check("  No: 0 to D72", Read("D72") == 0 && Read("D71") == 0);
        LanguageManager.ChangeLanguage("EN");
        Check("the texts in every language", new[] { "Pop_OK", "Pop_Cancel", "Pop_Yes", "Pop_No", "Pop_Continue", "Pop_Restart", "Pop_Retry", "Pop_Hide", "Pop_Number", "Pop_NotSent", "Pop_SignIn", "Pop_Unknown" }
              .All(k => new[] { "EN", "TR", "RU", "DE", "IT", "ES" }.All(l => !string.IsNullOrEmpty(LanguageManager.GetStringIn(l, k)))));

        form.Close();
        Console.WriteLine(fail == 0 ? "\nPLC POPUPS ALL PASS" : "\nPLC POPUPS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
