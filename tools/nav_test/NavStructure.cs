using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;

class N {
    static int fail = 0;
    static void Check(string what, bool ok, string d="") {
        Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+what+(d.Length>0?"   ["+d+"]":""));
        if (!ok) fail++;
    }

    class Mod : IMachineModule, Kaban.UI.Screens.IMachineDisplayName {
        string _id; bool _main;
        public Mod(string id) { _id = id; }
        public string ModuleId { get { return _id; } }
        public string DisplayNameKey { get { return "Machine_" + _id; } }
        public string DisplayName { get { return LanguageManager.Localize(DisplayNameKey); } }
        public void RegisterPlc(IMachineMountContext c) {
            PlcManager.Instance.RegisterStation(c.Station(1), _id+" PLC", SecurityLevel.Operator, c.ModuleId, 1);
        }
        public void RegisterConditions(IMachineMountContext c) {}
        public void RegisterData(IMachineMountContext c) {}
        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext c) {
            return Kaban.UI.Screens.StandardScreens.Build(c, new Kaban.UI.Screens.StandardScreens.Options {
                Home = () => null,
                ManualPages = new Kaban.UI.Settings.ISettingsPage[0],
                MachineSettings = new Kaban.UI.Settings.ISettingsPage[0]
            });
        }
    }

    static void Dump(string title) {
        Console.WriteLine("  " + title);
        foreach (var g in MachineRegistry.NavigationGroups()) {
            var caption = LanguageManager.GetString(g.TitleKey, g.TitleFallback);
            var tabs = string.Join(" | ", g.Pages.Select(p =>
                LanguageManager.GetString(
                    p.Key.Module.DisplayNameKey ?? "", Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(p.Key))));
            Console.WriteLine("     " + caption.PadRight(20) + (g.IsTabbed ? "tabs: " + tabs : "(single)"));
        }
    }

    static void Main() {
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        foreach (var f in new[]{"FA2010.csv","H500.csv","H800.csv"})
            LanguageManager.AddMachineTranslations(f);

        Console.WriteLine("--- main machine alone ---");
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod("FA2010"));
        Dump("nav:");
        var groups = MachineRegistry.NavigationGroups();
        Check("HMI Settings present exactly once",
              groups.Count(g => g.Role == ScreenRoles.HmiSettings) == 1);
        Check("Machine Parameters present", groups.Any(g => g.Role == ScreenRoles.MachineParameters));
        Check("no old 'Settings' entry", !groups.Any(g => g.TitleKey == "Btn_Settings"));

        Console.WriteLine("--- with two sub-machines ---");
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod("FA2010"));
        MachineRegistry.MountSub(new Mod("H500"), stationMap: new[]{3});
        MachineRegistry.MountSub(new Mod("H800"), stationMap: new[]{4});
        Dump("nav:");
        groups = MachineRegistry.NavigationGroups();

        var hmi = groups.Where(g => g.Role == ScreenRoles.HmiSettings).ToList();
        Check("STILL exactly one HMI Settings", hmi.Count == 1, "count=" + hmi.Count);
        Check("HMI Settings has NO tab strip", hmi.Count == 1 && !hmi[0].IsTabbed,
              hmi.Count == 1 ? hmi[0].Pages.Count + " page(s)" : "");

        var mp = groups.First(g => g.Role == ScreenRoles.MachineParameters);
        Check("Machine Parameters is tabbed per machine", mp.IsTabbed && mp.Pages.Count == 3,
              mp.Pages.Count + " tabs");

        // the names the operator actually reads on the tabs
        var names = mp.Pages.Select(p => LanguageManager.GetString(
                        p.Key.Module.DisplayNameKey, Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(p.Key))).ToList();
        Check("main machine tab is NOT 'Main'", !names.Contains("Main"), string.Join(" | ", names));
        Check("tabs read the translated machine names",
              names[0]=="Processing Center" && names[1]=="Screwing_Holing" && names[2]=="3D_HingeDrilling",
              string.Join(" | ", names));

        LanguageManager.ChangeLanguage("TR");
        var tr = mp.Pages.Select(p => LanguageManager.GetString(
                     p.Key.Module.DisplayNameKey, "")).ToList();
        Check("Turkish tab names",
              tr[0]=="İşleme Merkezi" && tr[1]=="Vidalama_Delme" && tr[2]=="3D_MenteseMatkap",
              string.Join(" | ", tr));

        Console.WriteLine(fail==0 ? "\nNAV ALL PASS" : "\nNAV " + fail + " FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

