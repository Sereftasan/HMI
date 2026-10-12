using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Forms;
using Kaban.UI.Screens;

class R2 {
    static int fail = 0;
    static void Check(string w, bool ok, string d="") {
        Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":"")); if(!ok) fail++;
    }
    class Mod : IMachineModule, IMachineDisplayName {
        string _id; public Mod(string id){_id=id;}
        public string ModuleId { get { return _id; } }
        public string DisplayNameKey { get { return "Machine_"+_id; } }
        public string DisplayName { get { return LanguageManager.GetString(DisplayNameKey, ModuleId); } }
        public void RegisterPlc(IMachineMountContext c){ PlcManager.Instance.RegisterStation(c.Station(1),_id,SecurityLevel.Operator,c.ModuleId,1); }
        public void RegisterConditions(IMachineMountContext c){}
        public void RegisterData(IMachineMountContext c){}
        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext c){
            return StandardScreens.Build(c, new StandardScreens.Options {
                Home = () => new UserControl(),
                ManualPages = new Kaban.UI.Settings.ISettingsPage[0],
                MachineSettings = new Kaban.UI.Settings.ISettingsPage[0] });
        }
    }
    static IEnumerable<Control> All(Control r){ foreach(Control c in r.Controls){ yield return c; foreach(var d in All(c)) yield return d; } }

    [STAThread]
    static void Main(){
        Environment.CurrentDirectory = "/tmp/real";
        Application.EnableVisualStyles();
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod("FA2010"));
        MachineRegistry.MountSub(new Mod("H500"), stationMap:new[]{3});
        MachineRegistry.MountSub(new Mod("H800"), stationMap:new[]{4});

        var form = new MainForm();
        form.BuildNavigation();
        form.Show();
        form.PerformLayout();

        var strip = All(form).OfType<MachineSelectorStrip>().First();
        var menu  = strip.Parent;
        var navButtons = All(form).OfType<Kaban.UI.Controls.ButtonHMI>()
                          .Where(b => b.Parent == menu).ToList();

        Console.WriteLine("--- the page buttons are laid out EXACTLY as before ---");
        Check("buttons live directly on the menu panel (not a flow host)",
              navButtons.Count >= 4, navButtons.Count + " buttons on pnlMenu");
        Check("every button is docked Top, full width of the bar",
              navButtons.All(b => b.Dock == DockStyle.Top),
              "docks: " + string.Join(",", navButtons.Select(b => b.Dock.ToString()).Distinct()));
        Check("all share one left margin (6)",
              navButtons.Select(b => b.Margin.Left).Distinct().Count() == 1,
              "left margins: " + string.Join(",", navButtons.Select(b => b.Margin.Left.ToString()).Distinct()));
        Check("all the same height (45)",
              navButtons.All(b => b.Height == 45));
        Check("uniform vertical spacing between them",
              navButtons.Select(b => b.Margin.Top).Distinct().Count() == 1);

        Console.WriteLine("--- the machine strip is BELOW every page button ---");
        int stripTop = menu.PointToScreen(strip.Location).Y;
        int lowest = navButtons.Max(b => b.Parent.PointToScreen(new Point(b.Left, b.Bottom)).Y);
        Check("strip sits below the lowest button", stripTop >= lowest,
              "strip=" + stripTop + " buttons end=" + lowest);
        Check("strip is visible at first start", strip.Visible, "Visible=" + strip.Visible);

        Console.WriteLine("--- its position does NOT move with the logged-in user ---");
        var refresh = typeof(MainForm).GetMethod("RefreshNavVisibility", BindingFlags.NonPublic|BindingFlags.Instance);
        SecurityManager.Login("eng", SecurityLevel.Maintenance);
        refresh.Invoke(form, null); form.PerformLayout();
        int topMaint = menu.PointToScreen(strip.Location).Y;
        int visMaint = navButtons.Count(b => b.Visible);

        SecurityManager.Logout();
        refresh.Invoke(form, null); form.PerformLayout();
        int topOut = menu.PointToScreen(strip.Location).Y;
        int visOut = navButtons.Count(b => b.Visible);

        Check("fewer buttons are shown when logged out", visOut < visMaint,
              visMaint + " -> " + visOut + " buttons");
        Check("but the machine list stays at the SAME y", topMaint == topOut,
              "maintenance=" + topMaint + "  logged out=" + topOut);

        Console.WriteLine("--- it does not blink on every navigation click ---");
        var groups = MachineRegistry.NavigationGroups();
        var home = groups.First(g => g.Role == ScreenRoles.Home);
        var alarms = groups.FirstOrDefault(g => g.Role == ScreenRoles.Alarms);
        strip.ShowFor(home);
        var before = strip.Controls.OfType<Kaban.UI.Controls.ButtonHMI>().ToList();
        strip.ShowFor(alarms ?? home);      // same machines -> must not rebuild
        var after = strip.Controls.OfType<Kaban.UI.Controls.ButtonHMI>().ToList();
        Check("same machines -> the very same button objects are kept",
              before.Count > 0 && before.Count == after.Count &&
              before.Zip(after, (x,y) => ReferenceEquals(x,y)).All(z => z),
              before.Count + " buttons, rebuilt=" + !before.Zip(after,(x,y)=>ReferenceEquals(x,y)).All(z=>z));
        Check("and it stays visible throughout", strip.Visible);

        var hmi = groups.First(g => g.Role == ScreenRoles.HmiSettings);
        strip.ShowFor(hmi);
        Check("hidden only when the page really has one machine", !strip.Visible);

        Console.WriteLine(fail==0 ? "\nNAV LAYOUT ALL PASS" : "\nNAV LAYOUT " + fail + " FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

