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

// 23 October: "We should add PLC device write function to the PLC monitor labels (selected label or all the labels in
// selected tabs). The backup selection page should open (just for the backup selection btw, not for the writing device
// value interval selection) when labels or tabs selected and 'Write to PLC from backup' button clicked, then only the
// selected label devices are written to PLC from the selected backup (it should be controlled if there is a backup read
// at the directory even then and the backup should be taken first before actually writing the selected labels here as
// well to prevent accidental device value loss (this preventive data backup function should only be considered for the
// real PLCs btw, not for the simulation driver))"
class MBW {
    /// <summary>A real PLC (not the simulator - its class's name is another): the simulator's memory behind it.</summary>
    class RealPlc : Kaban.Core.Interfaces.IPlcDriver, Kaban.Core.Interfaces.IPlcBlockAccess, Kaban.Core.Interfaces.INonBlockingDriver {
        readonly Kaban.Drivers.Simulation.SimulationDriver _s;
        public RealPlc(Kaban.Drivers.Simulation.SimulationDriver s) { _s = s; }
        public bool IsConnected { get { return _s.IsConnected; } }
        public bool IsHealthy { get { return true; } }
        public bool Connect() { return _s.Connect(); }
        public void Disconnect() { }
        public int Read(string a) { return _s.Read(a); }
        public int[] ReadMulti(string[] a) { return _s.ReadMulti(a); }
        public bool TryRead(string a, out int v) { return _s.TryRead(a, out v); }
        public bool TryReadMulti(string[] a, out int[] v) { return _s.TryReadMulti(a, out v); }
        public int Write(string a, int v) { return _s.Write(a, v); }
        public bool RefusesOutOfRange { get { return false; } }
        public bool TryReadBlock(string f, int c, out short[] v) { return _s.TryReadBlock(f, c, out v); }
        public int WriteBlock(string f, short[] v) { return _s.WriteBlock(f, v); }
    }

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

    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { d = d ?? ""; Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static Type MonType = typeof(SimulationMonitorForm);
    static Type RowType = MonType.GetNestedType("WatchRow", Any);
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
        var p = t.GetProperty(n, Any); return p.GetValue(o is Type ? null : o, null);
    }
    static void Set(object o, string n, object v) { o.GetType().GetField(n, Any).SetValue(o, v); }
    static object Call(object o, string n, params object[] a) {
        var t = o as Type ?? o.GetType();
        var mi = t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length >= a.Length &&
                                              m.GetParameters().Skip(a.Length).All(p => p.IsOptional));
        var args = a.Concat(mi.GetParameters().Skip(a.Length).Select(p => p.DefaultValue)).ToArray();
        return mi.Invoke(o is Type ? null : o, args);
    }
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }
    static IList Rows(object sheet) { return (IList)Get(sheet, "Rows"); }
    static object Row(string address, string type, string label) {
        var r = Activator.CreateInstance(RowType);
        Set(r, "Address", address); Set(r, "DataTypeOverride", type); Set(r, "LabelOverride", label);
        Set(r, "ArrayCount", 1); Set(r, "Station", 1);
        return r;
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static IList Typed(IEnumerable<object> rows) {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(RowType));
        foreach (var r in rows) list.Add(r);
        return list;
    }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "mbw_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        MonType.GetField("WatchFolder", Any).SetValue(null, dir);
        StructuredTypes.FilePath = Path.Combine(dir, "StructuredTypes.cfg");
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

        var recipe = new StructuredTypes.Type { Name = "Recipe Step", Size = 4 };
        recipe.Members.Add(new StructuredTypes.Member { Name = "Ready", DataType = "Bit", Offset = 0, Bit = 3 });
        recipe.Members.Add(new StructuredTypes.Member { Name = "Speed", DataType = "Word [Signed]", Offset = 1 });
        StructuredTypes.Save(new[] { recipe });

        // the values a backup is read of
        sim.Write("D100", 11); sim.Write("D101", 22); sim.Write("M10", 1); sim.Write("X5", 1);
        sim.Write("D300", 0x18); sim.Write("D301", 55); sim.Write("D302", 66); sim.Write("D303", 77);
        sim.Write("D500", 1 << 2);
        sim.Write("LCN2", 70000);
        sim.Write(@"U1\G4", 20000); sim.Write(@"U1\G5", 0);       // axis 1 of module 1: Pr.3
        var axes = (IList)typeof(AlarmHelpLinks).GetField("_axisList", Any).GetValue(null);
        axes.Add(new AlarmHelpLinks.AxisInfo { Station = 1, NameKey = "Axis_X", Row = new Dictionary<string, object> { { "MotionNumber", 1 }, { "MotionAxisIndex", 2 } } });
        typeof(WorkForm).GetField("Inline", Any).SetValue(null, true);
        var said = new List<string>();
        DialogResult answer = DialogResult.Yes;
        Func<string, MessageBoxButtons, DialogResult> ask = (text, buttons) => { said.Add(text); return buttons == MessageBoxButtons.YesNo ? answer : DialogResult.OK; };
        typeof(PlcBackupUi).GetField("Ask", Any).SetValue(null, ask);
        typeof(MotionFlash).GetField("Ask", Any).SetValue(null, ask);
        SecurityManager.Login("boss", SecurityLevel.Admin);

        var backup = new PlcBackup.Backup { Machine = "", Plc = "", User = "boss", Date = DateTime.Now, Station = 1, Description = "before the change",
                                            Modules = PlcBackupUi.ModulesOf(1) };
        string machine, plc;
        PlcBackupUi.Names(ep, out machine, out plc);
        backup.Machine = machine; backup.Plc = plc;
        var link = new PlcBackup.Link(PlcManager.Instance.GetDriverForMonitoring(1), (Kaban.Core.Interfaces.IPlcBlockAccess)PlcManager.Instance.GetDriverForMonitoring(1));
        bool read = (bool)typeof(PlcBackupUi).GetMethod("ReadInto", Any).Invoke(null, new object[] { null, "read", link, backup, backup.Modules, new List<int>() });
        var path = PlcBackup.FileFor(backup);
        PlcBackup.Save(backup, path);
        Check("(a backup of the PLC read: D, M, X, LCN and module 1)", read && backup.Areas.Any(a => a.First == "D0") && backup.Areas.Any(a => a.First == @"U1\G0" || a.IsModule),
              backup.Areas.Count + " areas");

        // the values then changed
        sim.Write("D100", 0); sim.Write("D101", 0); sim.Write("M10", 0); sim.Write("X5", 0);
        sim.Write("D300", 0x01); sim.Write("D301", 0); sim.Write("D302", 0); sim.Write("D303", 0);
        sim.Write("D500", 1 << 7);
        sim.Write("LCN2", 5); sim.Write("LCN3", 9);
        sim.Write(@"U1\G4", 1);

        var form = new SimulationMonitorForm();
        form.Show(); Application.DoEvents();
        var monitors = (IList)Get(form, "_monitors");
        var tabs = (TabControl)Get(form, "_tabs");
        var mon = monitors.Cast<object>().First(m => (int)Get(m, "Station") == 1);
        tabs.SelectedIndex = monitors.IndexOf(mon);
        var sh = Call(form, "EnsureSheet", mon);
        var speedA = Row("D100", "Word [Signed]", "Speed A");
        var speedB = Row("D101", "Word [Signed]", "Speed B");
        var lamp = Row("M10", "Bit", "Lamp");
        var step = Row("D300", "Recipe Step", "Step");
        var input = Row("X5", "Bit", "Input");
        var count = Row("LCN2", "Double Word [Signed]", "Long counter");
        var bitOfWord = Row("D500.2", "Bit", "Flag");
        var axis = Row(@"U1\G4", "Double Word [Signed]", "Axis 1 Pr.3");
        var none = Row("D9990", "Word [Signed]", "Beyond the backup");
        foreach (var r in new[] { speedA, speedB, lamp, step, input, count, bitOfWord, axis, none }) Rows(sh).Insert(Rows(sh).Count - 1, r);
        MonType.GetMethod("EnsureElements", Any).Invoke(null, new[] { sh });
        Call(form, "RefreshValues"); Pump(50);
        Call(form, "RefreshValues"); Pump(50);

        Console.WriteLine("--- the button ---");
        var bar = (ToolStrip)Get(form, "_bar");
        var button = bar.Items.OfType<ToolStripControlHost>().Select(h => h.Control).OfType<Button>().FirstOrDefault(b => b.Name == "monWriteBackup");
        Check("on the toolbar: Write to PLC from backup", button != null);
        var grid = (DataGridView)Get(sh, "Grid");
        grid.ClearSelection();
        var menu = (ContextMenuStrip)Call(form, "BackupMenu");
        var items = menu.Items.OfType<ToolStripMenuItem>().ToList();
        Check("  its menu: the selected labels (none selected: off), every label of this tab, of every tab of the controller",
              items.Count == 3 && !items[0].Enabled && items[1].Enabled && items[1].Text.Contains((string)Get(sh, "Name")) && items[2].Enabled,
              string.Join(" | ", items.Select(i => i.Text + (i.Enabled ? "" : " (off)"))));

        Console.WriteLine("--- the backup chosen ---");
        int shown = 0;
        bool listed = false, onlyThis = false;
        PlcBackupChooseForm.Showing = f => {
            shown++;
            var l = (List<PlcBackup.Backup>)Get(f, "Listed");
            listed = l.Any(b => b.Path == path);
            onlyThis = (bool)Get(f, "OnlyThis");
            Call(f, "Select", path);
            Call(f, "Take");
            return f.DialogResult;
        };
        said.Clear();
        var result = (PlcBackup.WriteResult)Call(form, "WriteFromBackup", mon, Typed(new[] { speedA }), "Speed A", null);
        Check("a label: the backup selection page (this PLC's backups, the one read listed)", shown == 1 && listed && onlyThis);
        Check("  asked first: its device and the backup's value", said.Count >= 1 && said[0].StartsWith("Write 1 device(s) of Speed A from the backup of") && said[0].Contains("D100: 11"),
              said.FirstOrDefault());
        Check("  written: D100 back, nothing else (D101 still 0)", result != null && result.Ok && sim.Read("D100") == 11 && sim.Read("D101") == 0, sim.Read("D100") + " / " + sim.Read("D101"));
        Check("  the simulator: no safety backup", PlcBackup.List().Count == 1);
        var log = PlcWriteLog.Read(DateTime.Now.AddMinutes(-2), DateTime.Now.AddMinutes(1));
        Check("  in the write log as the PLC Monitor's from the backup", log.Any(e => e.Device.StartsWith("D100") && e.Source.Contains("PLC Monitor") && e.Source.Contains("from the backup of")),
              string.Join(" | ", log.Select(e => e.Device + " " + e.Source)));

        Console.WriteLine("--- not now ---");
        sim.Write("D100", 0);
        PlcBackupChooseForm.Showing = f => DialogResult.Cancel;
        said.Clear();
        result = (PlcBackup.WriteResult)Call(form, "WriteFromBackup", mon, Typed(new[] { speedA }), "Speed A", null);
        Check("the backup page cancelled: nothing asked, nothing written", result == null && said.Count == 0 && sim.Read("D100") == 0);
        PlcBackupChooseForm.Showing = f => { Call(f, "Select", path); Call(f, "Take"); return f.DialogResult; };
        answer = DialogResult.No;
        result = (PlcBackup.WriteResult)Call(form, "WriteFromBackup", mon, Typed(new[] { speedA }), "Speed A", null);
        Check("  the question answered No: nothing written", result == null && sim.Read("D100") == 0);
        answer = DialogResult.Yes;
        SecurityManager.Login("maint", SecurityLevel.Maintenance);
        said.Clear();
        result = (PlcBackup.WriteResult)Call(form, "WriteFromBackup", mon, Typed(new[] { speedA }), "Speed A", null);
        Check("  below Admin: said, nothing written", result == null && said.Count == 1 && said[0].Contains("Admin") && sim.Read("D100") == 0, said.FirstOrDefault());
        SecurityManager.Login("boss", SecurityLevel.Admin);

        Console.WriteLine("--- a structure's bit ---");
        var members = ((IList)Get(step, "Members")).Cast<object>().ToList();
        said.Clear();
        result = (PlcBackup.WriteResult)Call(form, "WriteFromBackup", mon, Typed(new[] { members[0] }), "Step.Ready", null);
        Check("only that bit: the backup's bit 3 into D300, its other bits as they are now (bit 0), the next words untouched",
              result != null && result.Ok && sim.Read("D300") == 0x09 && sim.Read("D301") == 0, "D300 = 0x" + sim.Read("D300").ToString("X") + ", D301 = " + sim.Read("D301"));

        Console.WriteLine("--- every label of the tab ---");
        sim.Write("D300", 0x01);
        Call(form, "RefreshValues"); Pump(50);
        var mine = new[] { speedA, speedB, lamp, step, input, count, bitOfWord, axis, none };
        var tabLabels = ((IList)MonType.GetMethod("TabLabels", Any).Invoke(null, new[] { sh })).Cast<object>().ToList();
        Check("the tab's labels: every one, each item once (a structure without its members)", mine.All(r => tabLabels.Contains(r)) && !members.Any(m => tabLabels.Contains(m)),
              tabLabels.Count + " labels");
        // (the simulator lists every device written to it on the first tab by itself: only the labels made here)
        var all = Typed(tabLabels.Where(r => mine.Contains(r)));
        said.Clear();
        result = (PlcBackup.WriteResult)Call(form, "WriteFromBackup", mon, all, "the labels of the tab", null);
        string q = said.FirstOrDefault() ?? "";
        Check("asked: every device, the one not in the backup said (left as it is)", q.Contains("Not in the backup - left as they are:") && q.Contains("D9990") && !q.Contains("Not written:"), q);
        Check("  written: the words, the bit, the structure's whole length, the input (the simulator)",
              result != null && result.Ok && sim.Read("D100") == 11 && sim.Read("D101") == 22 && sim.Read("M10") == 1 && sim.Read("X5") == 1 &&
              sim.Read("D300") == 0x18 && sim.Read("D301") == 55 && sim.Read("D302") == 66 && sim.Read("D303") == 77,
              string.Join(" ", new[] { "D100", "D101", "M10", "X5", "D300", "D301", "D302", "D303" }.Select(a => a + "=" + sim.Read(a))));
        Check("  D500.2: its bit back, bit 7 kept", sim.Read("D500") == ((1 << 2) | (1 << 7)), "D500 = " + sim.Read("D500"));
        Check("  the module's words (Pr.3, two words)", sim.Read(@"U1\G4") == 20000 && sim.Read(@"U1\G5") == 0, sim.Read(@"U1\G4") + " " + sim.Read(@"U1\G5"));
        var lcnArea = backup.Areas.FirstOrDefault(a => a.Prefix == "LCN");
        if (lcnArea != null)
            Check("  a 32-bit LCN2: that device alone (LCN3 untouched)", sim.Read("LCN2") == 70000 && sim.Read("LCN3") == 9, sim.Read("LCN2") + " / " + sim.Read("LCN3"));
        Check("  the module's data counts as changed (its flash ROM)", MotionFlash.IsChanged(1, 1));

        Console.WriteLine("--- a real PLC ---");
        PlcManager.Instance.RegisterDriver(1, new RealPlc(sim));
        ConnectionMonitor.ReportRead(1, true);
        foreach (var f in Directory.GetFiles(PlcBackup.Folder, "*" + PlcBackup.Extension))
        {
            var old = PlcBackup.Load(f);
            old.Date = old.Date.AddDays(-1);
            File.Delete(f);
            PlcBackup.Save(old, PlcBackup.FileFor(old));
        }
        path = PlcBackup.List().First().Path;
        sim.Write("D100", 0); sim.Write("X5", 0); sim.Write("D101", 5);
        said.Clear();
        result = (PlcBackup.WriteResult)Call(form, "WriteFromBackup", mon, Typed(new[] { speedA, input }), "Speed A, Input", null);
        var today = PlcBackup.List().FirstOrDefault(b => b.Date.Date == DateTime.Today);
        q = said.FirstOrDefault() ?? "";
        Check("the input not written (as the PLC Connection page's Write), said", q.Contains("Not written:") && q.Contains("X5") && q.Contains("This is the machine's PLC"), q);
        Check("  no backup of today: the PLC read first (D101 5 kept in it), described as taken before this", today != null &&
              today.Description.StartsWith("Taken by itself before writing Speed A, Input from the backup of") &&
              PlcBackup.Load(today.Path).Areas.First(a => a.First == "D0").Value(101) == 5, today == null ? "none" : today.Description);
        Check("  then written: D100 back, X5 not", result != null && result.Ok && sim.Read("D100") == 11 && sim.Read("X5") == 0);

        Console.WriteLine("--- the backup selection page ---");
        PlcBackupChooseForm.Showing = null;
        var other = PlcBackup.Load(path);
        other.Machine = "Another machine";
        var otherPath = Path.Combine(dir, "usb.plcbak");
        PlcBackup.Save(other, otherPath);
        var chooser = new PlcBackupChooseForm(ep, "Speed A");
        var l0 = (List<PlcBackup.Backup>)Get(chooser, "Listed");
        Check("this PLC's backups, the newest first, the newest selected", l0.Count == 2 && l0[0].Date >= l0[1].Date &&
              Tree(chooser).OfType<DataGridView>().First().SelectedRows.Count == 1, l0.Count + " listed");
        Set(chooser, "_opened", null);
        Call(chooser, "Choose", otherPath);
        var l1 = (List<PlcBackup.Backup>)Get(chooser, "Listed");
        Check("  a file opened (a USB stick's): listed and selected though another machine's", l1.Any(b => b.Path == otherPath) &&
              ((PlcBackup.Backup)Tree(chooser).OfType<DataGridView>().First().SelectedRows[0].Tag).Path == otherPath);
        Call(chooser, "Take");
        Check("  OK: the backup read whole", chooser.DialogResult == DialogResult.OK && chooser.Result != null && chooser.Result.Areas.Count > 0 && chooser.Result.Machine == "Another machine");
        chooser.Dispose();

        PlcManager.Instance.RegisterDriver(1, sim);
        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nMONITOR BACKUP WRITE ALL PASS" : "\nMONITOR BACKUP WRITE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
