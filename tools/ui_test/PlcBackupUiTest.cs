using System;
using System.Collections;
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
using Kaban.UI.Settings;

// 22 October: "I need you to add Read and Write button to the end of PLC Connections table (enabled when the
// connection established). The read button should read all the PLC data (and connected simple motion data when
// available) and save it with the machine name, PLC description, PLC serial number (read it from the PLC while
// reading the data if you can or leave empty), user name, date and the user's read description at the confirmation
// page. And Write button is gonna open the read backup list and selection of the devices to be written to the PLC
// (and another column for the entry if the read device interval and write device interval is different ...)"
class PBU {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { d = d ?? ""; Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) { var t = o as Type ?? o.GetType(); var pr = t.GetProperty(n, Any); return pr != null ? pr.GetValue(o is Type ? null : o, null) : t.GetField(n, Any).GetValue(o is Type ? null : o); }
    static object Call(object o, string n, params object[] a) {
        var t = o as Type ?? o.GetType();
        return t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length).Invoke(o is Type ? null : o, a);
    }
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }

    class Mod : IMachineModule {
        public string ModuleId { get { return "MAIN"; } }
        public string DisplayNameKey { get { return "Bench machine"; } }
        public void RegisterPlc(IMachineMountContext ctx) { ctx.Station(1); }
        public void RegisterConditions(IMachineMountContext ctx) { }
        public void RegisterData(IMachineMountContext ctx) { }
        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx)
        {
            yield return new MachineScreen { TitleKey = "Home", TitleFallback = "Home", Role = ScreenRoles.Home, SortOrder = 1, CreateView = () => new UserControl() };
        }
    }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "pbu_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        PlcBackup.Folder = Path.Combine(dir, "PlcBackups");
        PlcConnectionSettings.SettingsPath = Path.Combine(dir, "PlcConnections.cfg");
        PlcDriverCatalog.RegisterNone();
        PlcDriverCatalog.Register(PlcDriverCatalog.SimulationId, "Drv_Simulation", "Simulation (in-memory)",
                                  (station, endpoint) => new Kaban.Drivers.Simulation.SimulationDriver(station), usesLogicalStation: false);
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod(), null, dir);
        var ep = PlcConnectionSettings.Endpoints().First();
        PlcConnectionSettings.Set(ep, PlcDriverCatalog.SimulationId, 1, "");
        PlcConnectionSettings.Save();
        PlcConnectionSettings.ApplyAll();
        var sim = (Kaban.Drivers.Simulation.SimulationDriver)PlcManager.Unwrap(PlcManager.Instance.GetDriverForMonitoring(1));
        ConnectionMonitor.ReportRead(1, true);
        for (int i = 0; i < 1000; i++) sim.Write("R" + (1000 + i), 7 * i);
        for (int i = 0; i < 10; i++) sim.Write("M" + (100 + i), i % 2);
        sim.Write("D0", 42);
        sim.Write("X5", 1);
        sim.Write(@"U1\G4", 20000); sim.Write(@"U1\G5", 0);       // axis 1 of module 1: Pr.3
        // an axis of this PLC on module 1, axis 2 (as the Axis Settings pages' discovery lists it)
        var axes = (IList)typeof(AlarmHelpLinks).GetField("_axisList", Any).GetValue(null);
        axes.Add(new AlarmHelpLinks.AxisInfo { Station = 1, NameKey = "Axis_X", Row = new Dictionary<string, object> { { "MotionNumber", 1 }, { "MotionAxisIndex", 2 } } });
        typeof(WorkForm).GetField("Inline", Any).SetValue(null, true);
        var said = new List<string>();
        DialogResult answer = DialogResult.Yes;
        Func<string, MessageBoxButtons, DialogResult> ask = (text, buttons) => { said.Add(text); return buttons == MessageBoxButtons.YesNo ? answer : DialogResult.OK; };
        typeof(PlcBackupUi).GetField("Ask", Any).SetValue(null, ask);
        typeof(MotionFlash).GetField("Ask", Any).SetValue(null, ask);
        var opened = new List<Form>();
        typeof(StationConnectionControl).GetField("ShowWindow", Any).SetValue(null, (Action<Form>)(f => { opened.Add(f); f.Show(); Application.DoEvents(); }));

        Console.WriteLine("--- the PLC Connection page ---");
        SecurityManager.Login("maint", SecurityLevel.Maintenance);
        var form = new Form { Width = 1500, Height = 400 };
        var page = new StationConnectionControl();
        form.Controls.Add(page);
        form.Show(); Application.DoEvents();
        Call(page, "RefreshAllStatus"); Application.DoEvents();
        var read = Tree(page).OfType<Button>().FirstOrDefault(x => x.Name == "plcRead");
        var write = Tree(page).OfType<Button>().FirstOrDefault(x => x.Name == "plcWrite");
        var table = (TableLayoutPanel)Get(page, "_table");
        var head = table.GetControlFromPosition(8, 0) as Label;
        Check("a ninth column, PLC data: Read... and Write... at the end of each row", head != null && head.Text == "PLC data" && read != null && write != null &&
              table.GetPositionFromControl(read.Parent).Column == 8 && read.Text == "Read..." && write.Text == "Write...", head == null ? "none" : head.Text);
        Check("  connected - Maintenance: Read (it changes nothing in the PLC), not Write", read.Enabled && !write.Enabled);
        SecurityManager.Login("boss", SecurityLevel.Admin);
        Call(page, "RefreshAllStatus"); Application.DoEvents();
        Check("  an Admin: both", read.Enabled && write.Enabled);
        sim.Disconnect();
        Call(page, "RefreshAllStatus"); Application.DoEvents();
        bool downOff = !read.Enabled && !write.Enabled;
        sim.Connect();
        ConnectionMonitor.ReportRead(1, true);
        Call(page, "RefreshAllStatus"); Application.DoEvents();
        Check("  not connected: neither (back when it is)", downOff && read.Enabled && write.Enabled, downOff + " " + read.Enabled);

        Console.WriteLine("--- Read ---");
        read.PerformClick(); Application.DoEvents();
        var rf = opened.OfType<PlcBackupReadForm>().FirstOrDefault();
        Check("Read: its confirmation page", rf != null);
        Func<Control, string, Control> find = (c, n) => Tree(c).FirstOrDefault(x => x.Name == n);
        Check("  the machine, the PLC, the user, the date; the serial number and a description to type",
              find(rf, "bakMachine").Text == PlcEndpointNaming.MachineName(ep) && find(rf, "bakPlc").Text.Length > 0 && find(rf, "bakUser").Text == "boss" &&
              find(rf, "bakSerial") is TextBox && find(rf, "bakDescription") is TextBox, find(rf, "bakMachine").Text + " / " + find(rf, "bakPlc").Text);
        Check("  what is read: the CPU's devices and simple motion module 1 (4 axes - its axis 2 is set on a page)",
              find(rf, "bakWhat").Text.Contains("X, Y, M, L, B, F, SB, S, T, ST, C, LC, D, W, SW, R, Z, LZ, SM, SD") && find(rf, "bakWhat").Text.Contains("simple motion module 1 (4 axes)"),
              find(rf, "bakWhat").Text);
        find(rf, "bakSerial").Text = "2SZ0123456";
        find(rf, "bakDescription").Text = "before the program change";
        said.Clear();
        bool done = (bool)Call(rf, "DoRead");
        var saved = (string)Get(rf, "SavedPath");
        Check("Read: saved in the backups' folder, said so", done && File.Exists(saved) && said.Count == 1 && said[0].StartsWith("Saved: ") && rf.DialogResult == DialogResult.OK,
              said.FirstOrDefault());
        var b = PlcBackup.Load(saved);
        Check("  with the facts typed and known", b.Serial == "2SZ0123456" && b.Description == "before the program change" && b.User == "boss" &&
              b.Machine == PlcEndpointNaming.MachineName(ep) && (DateTime.Now - b.Date).TotalMinutes < 2 && b.Modules.Count == 1 && b.Modules[0].Value == 4);
        Check("  the CPU's devices and the module's data in it", b.Areas.Any(a => a.First == "R0" && a.Value(1999) == 7 * 999) &&
              b.Areas.Any(a => a.First == @"U1\G0" && a.Value(4) == 20000) && b.Areas.Count(a => a.IsModule) == 2 + 4 * 9 + 4 + 2, b.Areas.Count.ToString());
        rf.Close();

        var again = new PlcBackupReadForm(ep);
        Check("the next Read offers the serial number typed for this PLC", ((TextBox)find(again, "bakSerial")).Text == "2SZ0123456");
        again.Dispose();

        Console.WriteLine("--- Write ---");
        sim.Write("D0", 0);
        for (int i = 0; i < 10; i++) sim.Write("M" + (100 + i), 0);
        write.PerformClick(); Application.DoEvents();
        var wf = opened.OfType<PlcBackupWriteForm>().FirstOrDefault();
        var list = (DataGridView)Get(wf, "_backups");
        var areas = (DataGridView)Get(wf, "_areas");
        Check("Write: this PLC's backups - the newest chosen, its areas listed, none ticked",
              wf != null && list.Rows.Count == 1 && Convert.ToString(list.Rows[0].Cells["Serial"].Value) == "2SZ0123456" &&
              Convert.ToString(list.Rows[0].Cells["Description"].Value) == "before the program change" && areas.Rows.Count == b.Areas.Count &&
              areas.Rows.Cast<DataGridViewRow>().All(r => !Convert.ToBoolean(r.Cells["Write"].Value)));
        Func<string, DataGridViewRow> row = first => areas.Rows.Cast<DataGridViewRow>().First(r => ((PlcBackup.Area)r.Tag).First == first);
        // 23 October: "write inputs and outputs as well if the selected PLC driver is simulator"
        Check("  each with its devices in Read (R0 - R32767), Write to empty; the simulator: X and Y written too, said so",
              Convert.ToString(row("R0").Cells["Read"].Value) == "R0 - R32767" && Convert.ToString(row("R0").Cells["Target"].Value) == "" &&
              !row("X0").ReadOnly && Convert.ToString(row("X0").Cells["Note"].Value) == "" && !row("SD0").ReadOnly &&
              Tree(wf).OfType<Label>().Any(l => l.Text.Contains("The simulator: every area is written, the inputs and outputs too.")));
        var writeButton = (Button)Get(wf, "_write");
        Check("  nothing ticked: Write off", !writeButton.Enabled);
        // R1000 - R1999 into D2500 - D3499
        row("R0").Cells["Read"].Value = "R1000 - R1999";
        row("R0").Cells["Target"].Value = "D2500";
        row("R0").Cells["Write"].Value = true;
        Check("  R1000 - R1999 to D2500: 1000 devices, the note says where", Convert.ToString(row("R0").Cells["Devices"].Value) == "1,000" &&
              Convert.ToString(row("R0").Cells["Note"].Value) == "R1000 - R1999 → D2500 - D3499" && writeButton.Enabled,
              Convert.ToString(row("R0").Cells["Devices"].Value) + " | " + Convert.ToString(row("R0").Cells["Note"].Value));
        row("M0").Cells["Read"].Value = "M100 - M109";
        row("M0").Cells["Target"].Value = "D0";
        row("M0").Cells["Write"].Value = true;
        Check("  bits to a word device: refused, Write off", Convert.ToString(row("M0").Cells["Note"].Value).Contains("bit devices") && !writeButton.Enabled,
              Convert.ToString(row("M0").Cells["Note"].Value));
        row("M0").Cells["Target"].Value = "";
        row("D0").Cells["Write"].Value = true;
        row(@"U1\G0").Cells["Write"].Value = true;
        sim.Write("X5", 0);
        row("X0").Cells["Read"].Value = "X0 - X7";
        row("X0").Cells["Write"].Value = true;
        var flash = (CheckBox)Get(wf, "_flash");
        Check("  a module's area in the backup: its flash ROM offered", flash.Visible && !flash.Checked);
        flash.Checked = true;
        said.Clear();
        answer = DialogResult.No;
        Call(wf, "DoWrite");
        Check("Write asks first - the parts, that the machine should be stopped; No: nothing written", said.Count == 1 &&
              said[0].Contains("R1000 - R1999 → D2500 - D3499") && said[0].Contains("machine should be stopped") && sim.Read("D2500") == 0, said.FirstOrDefault());
        said.Clear();
        answer = DialogResult.Yes;
        var result = (PlcBackup.WriteResult)Call(wf, "DoWrite");
        Check("Yes: written - R's values in D2500 on, M100 - M109 back, D0 back, axis 1's parameters, the simulator's input X5 back",
              result != null && result.Ok && sim.Read("D2500") == 0 && sim.Read("D2501") == 7 && sim.Read("D3499") == 7 * 999 &&
              Enumerable.Range(0, 10).All(i => sim.Read("M" + (100 + i)) == i % 2) && sim.Read("D0") == 42 && sim.Read(@"U1\G4") == 20000 &&
              sim.Read("X5") == 1,
              result == null ? "null" : string.Join(" | ", result.Failed));
        Check("  said; then the module's flash ROM written (PLC READY off)", said.Any(s => s.Contains("devices written")) &&
              said.Any(s => s.Contains("Module 1's flash ROM written")) && sim.Read(@"U1\G4224") == 1, string.Join(" // ", said));
        PlcWriteLog.Flush();
        var log = PlcWriteLog.Read(DateTime.Now.AddMinutes(-2), DateTime.Now.AddMinutes(1));
        Check("  in the write log as the backup's, the Admin's", log.Any(e => e.Device == "D2500 - D3459" && e.User == "boss" && e.Source.Contains("the backup of")),
              string.Join(" | ", log.Where(e => e.Device.StartsWith("D25")).Select(e => e.Device + " " + e.Source)));

        Console.WriteLine("--- another file ---");
        var other = PlcBackup.Load(saved);
        other.Machine = "Another machine"; other.Date = other.Date.AddDays(-1);
        string usb = Path.Combine(dir, "usb.plcbak");
        PlcBackup.Save(other, usb);
        Call(wf, "Choose", usb);
        Check("a file opened (a USB stick's): listed and chosen though another machine's", list.Rows.Count == 2 &&
              ((PlcBackup.Backup)Get(wf, "Shown")).Machine == "Another machine");
        row("D0").Cells["Write"].Value = true;
        said.Clear();
        answer = DialogResult.No;
        Call(wf, "DoWrite");
        Check("  writing it: the question says it is another PLC's", said.Count == 1 && said[0].Contains("This backup is of another PLC"), said.FirstOrDefault());

        wf.Close();
        form.Close();
        SecurityManager.Logout();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nPLC BACKUP UI ALL PASS" : "\nPLC BACKUP UI " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
