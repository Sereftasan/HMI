using System;
using System.IO;
using System.Linq;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;

class L {
    class MainMod : IMachineModule, Kaban.UI.Screens.IMachineDisplayName {
        public string ModuleId { get { return "FA2010"; } }
        public string DisplayNameKey { get { return "Machine_FA2010"; } }
        public string DisplayName { get { return LanguageManager.Localize(DisplayNameKey); } }
        public void RegisterPlc(IMachineMountContext c) {
            PlcManager.Instance.RegisterStation(c.Station(1), "FA2010 PLC", SecurityLevel.Operator, c.ModuleId, 1);
        }
        public void RegisterConditions(IMachineMountContext c) {}
        public void RegisterData(IMachineMountContext c) {
            // exactly what FA2010Module.RegisterData does
            string csv = c.ResolveFile("Translations.csv");
            Console.WriteLine("  [FA2010.RegisterData] resolved path = " + csv + "  exists=" + File.Exists(csv));
            LanguageManager.LoadMachineTranslations(csv);
        }
        public System.Collections.Generic.IEnumerable<MachineScreen> GetScreens(IMachineMountContext c) { yield break; }
    }

    static void Main() {
        Environment.CurrentDirectory = "/tmp/langbug";
        Console.WriteLine("=== step 1: MountMachines(), exactly as Program.cs does it ===");
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new MainMod());
        MachineRegistry.MountSub(new Kaban.Machines.H500.H500Module(), stationMap: new[]{3});
        MachineRegistry.MountSub(new Kaban.Machines.H800.H800Module(), stationMap: new[]{4});

        var h500 = MachineRegistry.Modules.First(m => m.Context.ModuleId == "H500");
        var h800 = MachineRegistry.Modules.First(m => m.Context.ModuleId == "H800");
        Console.WriteLine("  DisplayNameOf(H500) right after mounting = '" + Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(h500) + "'");
        Console.WriteLine("  DisplayNameOf(H800) right after mounting = '" + Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(h800) + "'");

        Console.WriteLine();
        Console.WriteLine("=== step 2: new MainForm()'s OWN translation reload (line ~264) ===");
        string commonCsv = Path.Combine("/tmp/langbug", "CommonTranslations.csv");
        string machineCsv = Path.Combine("/tmp/langbug", "Translations.csv");
        Console.WriteLine("  common path exists=" + File.Exists(commonCsv));
        Console.WriteLine("  machine path exists=" + File.Exists(machineCsv));
        LanguageManager.LoadCommonTranslations(commonCsv);
        LanguageManager.AddMachineTranslations(machineCsv);   // matches MainForm.cs

        Console.WriteLine("  DisplayNameOf(H500) after MainForm's reload = '" + Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(h500) + "'");
        Console.WriteLine("  DisplayNameOf(H800) after MainForm's reload = '" + Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(h800) + "'");

        Console.WriteLine();
        Console.WriteLine("=== step 3: BuildNavigation() runs (first tab build) - what the operator sees at launch ===");
        Console.WriteLine("  GetString(Machine_H500, fallback) = '" + LanguageManager.GetString("Machine_H500", Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(h500)) + "'");

        Console.WriteLine();
        Console.WriteLine("=== step 4: simulate login (SecurityManager.UserChanged fires, BuildTabs() reruns) ===");
        SecurityManager.Login("test", SecurityLevel.Admin);
        Console.WriteLine("  DisplayNameOf(H500) after login = '" + Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(h500) + "'");
        Console.WriteLine("  GetString(Machine_H500, fallback) after login = '" + LanguageManager.GetString("Machine_H500", Kaban.UI.Screens.MachineTabScreen.DisplayNameOf(h500)) + "'");
    }
}

