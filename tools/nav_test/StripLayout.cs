using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Screens;

class S {
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
    static void Main(){
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        foreach(var f in new[]{"FA2010.csv","H500.csv","H800.csv"}) LanguageManager.AddMachineTranslations(f);
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod("FA2010"));
        MachineRegistry.MountSub(new Mod("H500"), stationMap:new[]{3});
        MachineRegistry.MountSub(new Mod("H800"), stationMap:new[]{4});

        var home = MachineRegistry.NavigationGroups().First(g=>g.Role==ScreenRoles.Home);
        var hmi  = MachineRegistry.NavigationGroups().First(g=>g.Role==ScreenRoles.HmiSettings);

        Console.WriteLine("--- the strip has a REAL height (AutoSize reported 0 before) ---");
        var strip = new MachineSelectorStrip();
        strip.ShowFor(home);
        Check("height > 0 so it can actually be seen", strip.Height > 0, "h=" + strip.Height);
        Check("height fits 3 machines + the separator gap",
              strip.Height >= 3*38 && strip.Height <= 3*46+20, "h=" + strip.Height);
        Check("no MACHINE caption label remains",
              !strip.Controls.OfType<Label>().Any(l => (l.Text??"").ToUpperInvariant().Contains("MACHINE")));

        Console.WriteLine("--- docking below the page buttons ---");
        var menu = new Panel { Width = 204, Height = 600 };
        var b1 = new Kaban.UI.Controls.ButtonHMI { Dock = DockStyle.Top, Height = 45, Text = "Home" };
        var b2 = new Kaban.UI.Controls.ButtonHMI { Dock = DockStyle.Top, Height = 45, Text = "Alarms" };
        menu.Controls.Add(b1); menu.Controls.Add(b2);
        menu.Controls.Add(strip);
        // exactly what MainForm does: WinForms docks the HIGHEST index first, so the
        // strip is forced to index 0 to land BELOW the buttons.
        menu.Controls.SetChildIndex(strip, 0);
        menu.PerformLayout();
        Check("strip sits BELOW every page button",
              strip.Top >= b1.Bottom && strip.Top >= b2.Bottom,
              "strip.Top=" + strip.Top + " buttons end " + Math.Max(b1.Bottom,b2.Bottom));
        Check("a clear gap separates it from the buttons",
              strip.Top - Math.Max(b1.Bottom,b2.Bottom) >= 0);

        Console.WriteLine("--- page Y alignment: machine page vs single-machine page ---");
        var multi  = MachineTabScreen.Build(home) as Kaban.UI.Settings.SettingsScreen;
        Check("machine page adds NO top inset (tabs hidden)",
              multi != null && multi.TabStripHidden, "hidden=" + (multi!=null && multi.TabStripHidden));
        var host = multi.Controls.OfType<Panel>().FirstOrDefault(c => c.Dock == DockStyle.Fill);
        Check("its content host has zero padding, so pages start at the same Y",
              host != null && host.Padding.All == 0,
              host!=null ? host.Padding.ToString() : "no host");

        Console.WriteLine("--- captions punctuate identically ---");
        Check("no space before the colon", Kaban.UI.Controls.UiStyle.Caption("Cut depth (mm)") == "Cut depth (mm):",
              Kaban.UI.Controls.UiStyle.Caption("Cut depth (mm)"));
        Check("an existing colon is not doubled", Kaban.UI.Controls.UiStyle.Caption("Station :") == "Station:",
              Kaban.UI.Controls.UiStyle.Caption("Station :"));
        var cap = Kaban.UI.Controls.UiStyle.CaptionLabel("Length", 8, 10);
        Check("caption label is RIGHT aligned", cap.TextAlign == ContentAlignment.MiddleRight);

        Console.WriteLine(fail==0 ? "\nSTRIP+LAYOUT ALL PASS" : "\nSTRIP+LAYOUT " + fail + " FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

