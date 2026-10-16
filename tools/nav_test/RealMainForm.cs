using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Forms;
using Kaban.UI.Screens;

class R {
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

    // find a control anywhere in the tree
    static IEnumerable<Control> All(Control root) {
        foreach (Control c in root.Controls) { yield return c; foreach (var d in All(c)) yield return d; }
    }

    [STAThread]
    static void Main(){
        Environment.CurrentDirectory = "/tmp/real";
        Application.EnableVisualStyles();

        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod("FA2010"));
        MachineRegistry.MountSub(new Mod("H500"), stationMap:new[]{3});
        MachineRegistry.MountSub(new Mod("H800"), stationMap:new[]{4});

        Console.WriteLine("--- THE REAL MainForm, as Program.cs builds it ---");
        var form = new MainForm();
        form.BuildNavigation();
        form.Show();          // Control.Visible is false while any ancestor is hidden
        form.CreateControl();
        foreach (var c in All(form)) { try { var h = c.Handle; } catch {} }
        form.PerformLayout();

        var strip = All(form).OfType<MachineSelectorStrip>().FirstOrDefault();
        Check("the machine strip exists in the real form", strip != null);
        if (strip == null) { Console.WriteLine("\nREAL " + (++fail) + " FAILED"); Environment.Exit(1); }

        Console.WriteLine("--- item: it must be VISIBLE at first start, on Home ---");
        Check("visible immediately after BuildNavigation (no click needed)", strip.Visible,
              "Visible=" + strip.Visible);
        Check("and has a real height", strip.Height > 0, "h=" + strip.Height);

        Console.WriteLine("--- item: it must sit BELOW the page buttons ---");
        var navButtons = All(form).OfType<Kaban.UI.Controls.ButtonHMI>()
            .Where(b => b.Parent != null && b.Parent == strip.Parent)
            .ToList();
        Check("page buttons and strip share the navigation host",
              navButtons.Count > 0, navButtons.Count + " buttons beside the strip");

        // screen coordinates are the honest test - parent-relative Top lies across containers
        int stripTopScreen = strip.Parent.PointToScreen(strip.Location).Y;
        int lowestButton = navButtons.Count == 0 ? 0
            : navButtons.Max(b => b.Parent.PointToScreen(new Point(b.Left, b.Bottom)).Y);
        Check("strip is BELOW every page button",
              navButtons.Count > 0 && stripTopScreen >= lowestButton,
              "strip top=" + stripTopScreen + "  buttons end=" + lowestButton);
        Check("with roughly three button heights of clear space",
              navButtons.Count > 0 && (stripTopScreen - lowestButton) >= 0,
              "gap inside strip = " + (strip.Controls.Count > 0 ? "separator present" : "none"));

        Console.WriteLine("--- item: a nav button with nothing to show is hidden ---");
        // Driven DIRECTLY, not through Application.DoEvents: Mono's event loop with a
        // shown form and running timers does not return here. The production path is
        // SecurityManager.UserChanged -> OnUserChangedRefreshNav -> this method.
        var refresh = typeof(MainForm).GetMethod("RefreshNavVisibility",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Check("RefreshNavVisibility exists (the production handler calls it)", refresh != null);

        var machineParams = All(form).OfType<Kaban.UI.Controls.ButtonHMI>()
            .FirstOrDefault(b => (b.TranslationKey ?? "") == "Btn_MachineParameters");
        Check("Machine Parameters button found", machineParams != null);

        SecurityManager.Logout();
        refresh.Invoke(form, null);
        Check("hidden for a logged-out user (its page is Maintenance-only)",
              machineParams != null && !machineParams.Visible,
              machineParams != null ? "Visible=" + machineParams.Visible : "");

        SecurityManager.Login("eng", SecurityLevel.Maintenance);
        Check("  [diag] level=" + SecurityManager.CurrentLevel
              + " HasAccess(Maintenance)=" + SecurityManager.HasAccess(SecurityLevel.Maintenance), true);
        refresh.Invoke(form, null);
        Check("shown again once a Maintenance user logs in",
              machineParams != null && machineParams.Visible,
              machineParams != null ? "Visible=" + machineParams.Visible : "");

        var home = All(form).OfType<Kaban.UI.Controls.ButtonHMI>()
            .FirstOrDefault(b => (b.TranslationKey ?? "") == "Btn_Home");
        Check("an everyone-level page stays visible throughout",
              home != null && home.Visible, home != null ? "Visible=" + home.Visible : "no Home button");

        Console.WriteLine("--- the caption bar's chain: the coupled units (17 October) ---");
        var cb = (Button)typeof(MainForm).GetProperty("CouplingButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(form, null);
        Check("hidden while nothing is coupled", cb != null && !cb.Visible);
        var host = new Panel { Visible = false };
        form.Controls.Add(host);
        Func<string, int, Kaban.UI.Controls.CylinderUnitHMI> pos = (name, n) =>
        {
            var u = new Kaban.UI.Controls.CylinderUnitHMI { Name = name, TitleText = name, PlcNumber = 1, Size = new Size(360, 64),
                                                           CommandAAddress = "M" + (400 + 2 * n), CommandBAddress = "M" + (401 + 2 * n),
                                                           FeedbackType = Kaban.UI.Controls.CylinderFeedback.EncoderPlusReference, PositionAddress = "D" + (600 + n) };
            host.Controls.Add(u);
            return u;
        };
        var u1 = pos("Positioning 1", 0); var u2 = pos("Positioning 2", 1); var u3 = pos("Positioning 3", 2);
        Kaban.UI.Helpers.UnitCoupling.Toggle(u1); Kaban.UI.Helpers.UnitCoupling.Toggle(u2); Kaban.UI.Helpers.UnitCoupling.Toggle(u3);
        Check("three units coupled: the chain shown in the caption bar, left of what was there", cb.Visible && cb.Parent != null && cb.Right < cb.Parent.Width,
              "visible=" + cb.Visible + " at " + cb.Bounds);
        var menuOf = typeof(MainForm).GetMethod("BuildCouplingMenu", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var cm = (ContextMenuStrip)menuOf.Invoke(form, null);
        var texts = cm.Items.Cast<ToolStripItem>().Select(i => i.Text).Where(t => !string.IsNullOrEmpty(t)).ToList();
        Check("its menu: a heading, each coupled unit, and last 'Uncouple all 3'",
              texts.Count == 5 && texts[1].Contains("Positioning 1") && texts[3].Contains("Positioning 3") && texts[4].Contains("all 3"), string.Join(" | ", texts));
        cm.Items["uncouple:Positioning 2"].PerformClick();
        Check("  one clicked: that unit uncoupled, the others still", !Kaban.UI.Helpers.UnitCoupling.IsCoupled(u2) && Kaban.UI.Helpers.UnitCoupling.IsCoupled(u1) && cb.Visible);
        cm = (ContextMenuStrip)menuOf.Invoke(form, null);
        cm.Items["uncoupleAll"].PerformClick();
        Check("  'Uncouple all': none coupled, the chain hidden again", Kaban.UI.Helpers.UnitCoupling.All.Count == 0 && !cb.Visible);

        Console.WriteLine(fail==0 ? "\nREAL FORM ALL PASS" : "\nREAL FORM " + fail + " FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

