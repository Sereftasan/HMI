using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Screens;

class N2 {
    static int fail = 0;
    static void Check(string what, bool ok, string d="") {
        Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+what+(d.Length>0?"   ["+d+"]":""));
        if (!ok) fail++;
    }

    class Mod : IMachineModule, IMachineDisplayName {
        string _id; public Mod(string id){_id=id;}
        public string ModuleId { get { return _id; } }
        public string DisplayNameKey { get { return "Machine_" + _id; } }
        public string DisplayName { get { return LanguageManager.GetString(DisplayNameKey, ModuleId); } }
        public void RegisterPlc(IMachineMountContext c) {
            PlcManager.Instance.RegisterStation(c.Station(1), _id, SecurityLevel.Operator, c.ModuleId, 1);
        }
        public void RegisterConditions(IMachineMountContext c) {}
        public void RegisterData(IMachineMountContext c) {}
        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext c) {
            return Kaban.UI.Screens.StandardScreens.Build(c, new Kaban.UI.Screens.StandardScreens.Options {
                Home = () => new UserControl(),
                ManualPages = new Kaban.UI.Settings.ISettingsPage[0],
                MachineSettings = new Kaban.UI.Settings.ISettingsPage[0]
            });
        }
    }

    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        foreach (var f in new[]{"FA2010.csv","H500.csv","H800.csv"}) LanguageManager.AddMachineTranslations(f);

        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod("FA2010"));
        MachineRegistry.MountSub(new Mod("H500"), stationMap: new[]{3});
        MachineRegistry.MountSub(new Mod("H800"), stationMap: new[]{4});

        var groups = MachineRegistry.NavigationGroups();
        var home = groups.First(g => g.Role == ScreenRoles.Home);
        var hmi  = groups.First(g => g.Role == ScreenRoles.HmiSettings);

        var strip = new MachineSelectorStrip();

        Console.WriteLine("--- the strip appears only when there IS a choice ---");
        strip.ShowFor(home);
        Check("visible for a 3-machine page", strip.Visible);
        strip.ShowFor(hmi);
        Check("HIDDEN for HMI Settings (one page for the whole program)", !strip.Visible);
        strip.ShowFor(null);
        Check("hidden when the page has no machines at all", !strip.Visible);

        Console.WriteLine("--- selection ---");
        strip.ShowFor(home);
        int picked = -1;
        strip.MachineSelected += (s, i) => picked = i;
        Check("first machine selected by default", strip.SelectedIndex == 0, "index=" + strip.SelectedIndex);
        strip.Select(2, notify: true);
        Check("selecting raises the event with the index", picked == 2, "picked=" + picked);
        Check("selection is remembered", strip.SelectedIndex == 2);

        Console.WriteLine("--- machine buttons carry a distinct icon for collapsed mode ---");
        var mounts = MachineRegistry.Modules.ToList();
        var icons = mounts.Select(m => MachineIconProbe(m)).ToList();
        Check("every machine has an icon", icons.All(i => i != null));
        Check("icons are NOT all identical",
              !(ImagesEqual(icons[0], icons[1]) && ImagesEqual(icons[1], icons[2])),
              "distinct tiles");

        Console.WriteLine("--- the page's own machine tabs are gone (choice offered once) ---");
        var view = MachineTabScreen.Build(home) as Kaban.UI.Settings.SettingsScreen;
        Check("multi-machine page hides its top tab strip", view != null && view.TabStripHidden);
        // HMI Settings legitimately IS a SettingsScreen - of its OWN sub-pages
        // (Units, Users, Station Connection), which are not machines. What matters
        // is that it still SHOWS those tabs: only machine tabs were suppressed.
        var single = MachineTabScreen.Build(hmi) as Kaban.UI.Settings.SettingsScreen;
        Check("single-machine page keeps its own (non-machine) tabs visible",
              single != null && !single.TabStripHidden);

        Console.WriteLine(fail==0 ? "\nMACHINE STRIP ALL PASS" : "\nMACHINE STRIP " + fail + " FAILED");
        Environment.Exit(fail==0?0:1);
    }

    static Image MachineIconProbe(MachineRegistry.MountedModule m) {
        var t = typeof(MachineSelectorStrip).Assembly.GetType("Kaban.UI.Screens.MachineIcon");
        return t.GetMethod("For").Invoke(null, new object[]{ m }) as Image;
    }
    static bool ImagesEqual(Image a, Image b) {
        if (a == null || b == null) return a == b;
        var ba = new Bitmap(a); var bb = new Bitmap(b);
        if (ba.Width != bb.Width) return false;
        for (int x = 0; x < ba.Width; x += 3)
            for (int y = 0; y < ba.Height; y += 3)
                if (ba.GetPixel(x,y) != bb.GetPixel(x,y)) return false;
        return true;
    }
}

