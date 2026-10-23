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
        var read = Tree(page).OfType<Button>().FirstOrDefault(x => x.Name == "plcRead");
        var write = Tree(page).OfType<Button>().FirstOrDefault(x => x.Name == "plcWrite");
        form.Show(); Application.DoEvents();
        Call(page, "RefreshAllStatus"); Application.DoEvents();
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
        var inactive = (HashSet<int>)typeof(PlcConnectionSettings).GetField("_inactiveRunning", Any).GetValue(null);
        inactive.Add(1);
        Call(page, "RefreshAllStatus"); Application.DoEvents();
        bool inactiveOff = !read.Enabled && !write.Enabled;
        inactive.Remove(1);
        Call(page, "RefreshAllStatus"); Application.DoEvents();
        // 23 October: "Read and Write buttons should only be enabled when the connection is established"
        Check("  a PLC not active in this run: neither (they stayed as made - enabled)", inactiveOff && read.Enabled);

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
        var items = ((IEnumerable)Get(wf, "_items")).Cast<object>().ToList();
        Func<object, string> kindOf = it => Get(it, "Kind").ToString();
        Func<object, DataGridViewRow> rowOf = it => (DataGridViewRow)Get(it, "Row");
        Func<object, CheckState> tick = it => { var v = rowOf(it).Cells["Write"].Value; return v is CheckState ? (CheckState)v : v is bool && (bool)v ? CheckState.Checked : CheckState.Unchecked; };
        Func<string, object> item = first => items.First(it => Get(it, "Area") is PlcBackup.Area && ((PlcBackup.Area)Get(it, "Area")).First == first);
        Func<string, DataGridViewRow> row = first => rowOf(item(first));
        Func<string, object> group = title => items.First(it => kindOf(it) == "Group" && ((PlcBackupGroup)Get(it, "Group")).Title == title);
        Func<string, string, string> cell = (first, col) => Convert.ToString(row(first).Cells[col].Value);
        Check("Write: this PLC's backups - the newest chosen, its areas listed, none ticked",
              wf != null && list.Rows.Count == 1 && Convert.ToString(list.Rows[0].Cells["Serial"].Value) == "2SZ0123456" &&
              Convert.ToString(list.Rows[0].Cells["Description"].Value) == "before the program change" && items.All(it => tick(it) == CheckState.Unchecked));
        // 23 October: "It could be useful to group the write selection so the user could select them multiple and easier (the attached image show GX Works way for it)"
        var tops = items.Where(it => Get(it, "Parent") == null).Select(it => Convert.ToString(rowOf(it).Cells["Item"].Value)).ToList();
        var under = items.Where(it => kindOf(it) == "Group" || kindOf(it) == "Flash").Select(it => kindOf(it) == "Flash" ? "flash" : ((PlcBackupGroup)Get(it, "Group")).Title).ToList();
        Check("a tree as GX Works3's: the CPU's devices, then the module - its groups in GX Works3's order, its flash ROM last",
              tops.Count == 2 && tops[0].StartsWith("▼ PLC CPU: devices") && tops[1] == "▼ Simple motion module 1 (4 axes)" &&
              string.Join(", ", under) == "Positioning data, Block start data, Parameters, Servo parameters, Mark detection, Synchronous control parameters, PLC CPU memo area, flash" &&
              items.Count(it => kindOf(it) == "Device") == b.Areas.Count(a => !a.IsModule), string.Join(" | ", tops) + " // " + string.Join(", ", under));
        Check("  the groups with axes have Details; the memo area none", rowOf(group("Positioning data")).Cells["Details"] is DataGridViewButtonCell &&
              rowOf(group("Synchronous control parameters")).Cells["Details"] is DataGridViewButtonCell && !(rowOf(group("PLC CPU memo area")).Cells["Details"] is DataGridViewButtonCell));
        // 23 October: "write inputs and outputs as well if the selected PLC driver is simulator"
        Check("  each device with its devices in Read (R0 - R32767), Write to empty; the simulator: X and Y written too, said so",
              cell("R0", "Read") == "R0 - R32767" && cell("R0", "Target") == "" && !row("X0").ReadOnly && cell("X0", "Note") == "" && !row("SD0").ReadOnly &&
              Tree(wf).OfType<Label>().Any(l => l.Text.Contains("The simulator: every area is written, the inputs and outputs too.")));
        var writeButton = (Button)Get(wf, "_write");
        Check("  nothing ticked: Write off", !writeButton.Enabled);

        // a row with rows under it ticks them all; the rows above follow
        var module = items.First(it => kindOf(it) == "Module");
        rowOf(module).Cells["Write"].Value = CheckState.Checked;
        Check("the module ticked: every group whole, its flash ROM too", items.Where(it => Get(it, "Parent") == module).All(it => tick(it) == CheckState.Checked) &&
              Convert.ToString(rowOf(group("Positioning data")).Cells["Read"].Value) == "axes 1 - 4, No.1 - 100" &&
              Convert.ToString(rowOf(group("Parameters")).Cells["Read"].Value) == "common, axes 1 - 4" &&
              Convert.ToString(rowOf(group("Synchronous control parameters")).Cells["Read"].Value) == "axes 1 - 4, encoder axes 1 - 4" && writeButton.Enabled,
              Convert.ToString(rowOf(group("Parameters")).Cells["Read"].Value));
        rowOf(module).Cells["Write"].Value = CheckState.Indeterminate;   // the cell's own next state after Checked
        Check("  ticked again: all off", items.Where(it => Get(it, "Parent") == module).All(it => tick(it) == CheckState.Unchecked) && tick(module) == CheckState.Unchecked);

        // a group's details: the axes, an axis's positioning data numbers
        var pos = group("Positioning data");
        typeof(PlcBackupWriteForm).GetField("ShowDetails", Any).SetValue(null, (Func<PlcBackupDetailsForm, DialogResult>)(f => {
            f.Show(); Application.DoEvents();
            ((Button)Tree(f).First(c => c.Name == "detNone")).PerformClick();
            ((CheckBox)Tree(f).First(c => c.Name == "detAxis2")).Checked = true;
            ((NumericUpDown)Tree(f).First(c => c.Name == "detFrom2")).Value = 5;
            ((NumericUpDown)Tree(f).First(c => c.Name == "detTo2")).Value = 7;
            Call(f, "Take");
            f.Hide();
            return DialogResult.OK;
        }));
        Call(wf, "Details", pos);
        List<PlcBackup.WriteRange> wparts;
        var partsArgs = new object[] { null };
        typeof(PlcBackupWriteForm).GetMethods(Any).First(m => m.Name == "Parts" && m.GetParameters().Length == 1).Invoke(wf, partsArgs);
        wparts = (List<PlcBackup.WriteRange>)partsArgs[0];
        Check("Details: axis 2, No.5 - 7 - its data's words and their interpolation axes, the row partly ticked, the module too",
              Convert.ToString(rowOf(pos).Cells["Read"].Value) == "axis 2, No.5 - 7" && tick(pos) == CheckState.Indeterminate && tick(module) == CheckState.Indeterminate &&
              wparts.Count == 2 && wparts.Any(p => p.Target == @"U1\G7040" && p.Count == 30) && wparts.Any(p => p.Target == @"U1\G72040" && p.Count == 30),
              Convert.ToString(rowOf(pos).Cells["Read"].Value) + " / " + string.Join(", ", wparts.Select(p => p.Target + "+" + p.Count)));
        rowOf(module).Cells["Target"].Value = "U2";
        typeof(PlcBackupWriteForm).GetMethods(Any).First(m => m.Name == "Parts" && m.GetParameters().Length == 1).Invoke(wf, partsArgs);
        wparts = (List<PlcBackup.WriteRange>)partsArgs[0];
        Check("  the module's Write to U2: into module 2's buffer memory", wparts.All(p => p.Target.StartsWith(@"U2\G")) &&
              Convert.ToString(rowOf(module).Cells["Note"].Value).Contains("module 2"), string.Join(", ", wparts.Select(p => p.Target)));
        rowOf(module).Cells["Target"].Value = "X";
        Check("  a Write to that is no module: said, Write off", Convert.ToString(rowOf(module).Cells["Note"].Value).Contains("U2") && !writeButton.Enabled);
        rowOf(module).Cells["Target"].Value = "";
        Call(wf, "SetAll", true);
        Check("Select all: every row that can be written (the simulator: all)", items.All(it => tick(it) == CheckState.Checked));
        Call(wf, "SetAll", false);
        Check("Deselect all: none", items.All(it => tick(it) == CheckState.Unchecked) && !writeButton.Enabled);
        var cpu = items.First(it => kindOf(it) == "Cpu");
        Call(wf, "Fold", cpu, false);
        bool folded = !row("D0").Visible && rowOf(cpu).Visible && Convert.ToString(rowOf(cpu).Cells["Item"].Value).StartsWith("►");
        Call(wf, "Fold", cpu, true);
        Check("a top row folds and opens the rows under it", folded && row("D0").Visible);

        // R1000 - R1999 into D2500 - D3499
        row("R0").Cells["Read"].Value = "R1000 - R1999";
        row("R0").Cells["Target"].Value = "D2500";
        row("R0").Cells["Write"].Value = true;
        Check("  R1000 - R1999 to D2500: 1000 devices, the note says where; the CPU's row partly ticked", cell("R0", "Devices") == "1,000" &&
              cell("R0", "Note") == "R1000 - R1999 → D2500 - D3499" && writeButton.Enabled && tick(cpu) == CheckState.Indeterminate,
              cell("R0", "Devices") + " | " + cell("R0", "Note"));
        row("M0").Cells["Read"].Value = "M100 - M109";
        row("M0").Cells["Target"].Value = "D0";
        row("M0").Cells["Write"].Value = true;
        Check("  bits to a word device: refused, Write off", cell("M0", "Note").Contains("bit devices") && !writeButton.Enabled, cell("M0", "Note"));
        row("M0").Cells["Target"].Value = "";
        row("D0").Cells["Write"].Value = true;
        typeof(PlcBackupWriteForm).GetField("ShowDetails", Any).SetValue(null, (Func<PlcBackupDetailsForm, DialogResult>)(f => {
            f.Show(); Application.DoEvents();
            ((Button)Tree(f).First(c => c.Name == "detNone")).PerformClick();
            ((CheckBox)Tree(f).First(c => c.Name == "detAxis1")).Checked = true;
            Call(f, "Take");
            f.Hide();
            return DialogResult.OK;
        }));
        Call(wf, "Details", group("Parameters"));
        sim.Write("X5", 0);
        row("X0").Cells["Read"].Value = "X0 - X7";
        row("X0").Cells["Write"].Value = true;
        var flashItem = items.First(it => kindOf(it) == "Flash");
        rowOf(flashItem).Cells["Write"].Value = true;
        said.Clear();
        answer = DialogResult.No;
        Call(wf, "DoWrite");
        Check("Write asks first - the parts, the module's group and flash ROM, that the machine should be stopped; No: nothing written", said.Count == 1 &&
              said[0].Contains("R1000 - R1999 → D2500 - D3499") && said[0].Contains("Simple motion module 1: parameters - axis 1") &&
              said[0].Contains("Then the flash ROM of: U1") && said[0].Contains("machine should be stopped") && sim.Read("D2500") == 0, said.FirstOrDefault());
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
        items = ((IEnumerable)Get(wf, "_items")).Cast<object>().ToList();
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
