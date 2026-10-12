using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

class F {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}
    static int Blocks(string html, string variant){ return System.Text.RegularExpressions.Regex.Matches(html, "data-variant=\"" + variant + "\"").Count; }

    [STAThread] static void Main(){
        Console.WriteLine("--- the manual, tailored to one cylinder ---");
        var doc = TC.Resolve("1.1.1").Single();                     // double sol. 5/2 + single magnetic
        var path = TC.ExtractSection(doc);
        var html = File.ReadAllText(path);
        Check("a tailored copy, not the whole manual", path != doc.FullPath, Path.GetFileName(path));
        Check("its valve block kept", Blocks(html, "valve:1") == 1);
        Check("the other two valve blocks gone", Blocks(html, "valve:2") == 0 && Blocks(html, "valve:3") == 0);
        Check("its feedback block kept", Blocks(html, "feedback:1") == 1);
        Check("the other five feedback blocks gone",
              new[]{2,3,4,5,6}.All(f => Blocks(html, "feedback:" + f) == 0));
        foreach (var d in new[]{"1.1","1.2","1.3","1.5","1.6","1.8"})
            Check("  detail " + d + " kept whole", html.Contains("data-detail=\"" + d + "\""));
        Check("the caution box kept", html.Contains("CAUTION"));
        Check("a note says what it was prepared for",
              html.Contains("Prepared for this cylinder") && html.Contains("Double solenoid 5/2"));

        var valveOnly = File.ReadAllText(TC.ExtractSection(TC.Resolve("1.2.0").Single()));
        Check("valve given, feedback not: all six feedback blocks stay",
              new[]{1,2,3,4,5,6}.All(f => Blocks(valveOnly, "feedback:" + f) == 1) && Blocks(valveOnly, "valve:1") == 0);
        var none = TC.Resolve("1.0.0").Single();
        Check("neither given: the full manual, untouched", TC.ExtractSection(none) == none.FullPath);

        Console.WriteLine("--- the picker offers exactly the manual's variants ---");
        var v = TC.Parameter1(TC.ErrorType.CylinderMovement);
        var fb = TC.Parameter2(TC.ErrorType.CylinderMovement);
        // 8 October: "most of the 3 way valves we use are closed center valves" - the 5/3's two
        // kinds are offered apart (the manual's part for both is its 5/3).
        Check("four valves (+ unspecified): 5/2 double, 5/2 single, 5/3 closed and 5/3 open centre", v.Options.Length == 5 &&
              v.Options.Count(o => o.Contains("5/3")) == 2, string.Join(" | ", v.Options));
        Check("six feedbacks (+ unspecified), encoder included",
              fb.Options.Length == 7 && fb.Options.Any(o => o.Contains("encoder")), string.Join(" | ", fb.Options.Skip(1)));
        Check("no 'timed' feedback - the manual has none", !fb.Options.Any(o => o.Contains("timed")));
        Check("digits are explicit: 'Two end' is 2 though listed first",
              TC.ValueOf(fb, TC.Describe(TC.FeedbackType.TwoMagnetic)) == 2);
        Check("  and a stored 1 finds 'Single magnetic' at its own place",
              fb.Options[TC.IndexOfValue(fb, 1)] == TC.Describe(TC.FeedbackType.SingleMagnetic));
        Check("an 'open centre' code is the open-centre 5/3, read from the manual's 5/3 part", TC.Resolve("1.4.0").Single().Valve == 4 &&
              TC.ManualValve(4) == 3, TC.Resolve("1.4.0").Single().Valve.ToString());

        Console.WriteLine("--- a double word is one value ---");
        var sim = typeof(Kaban.UI.Forms.SimulationMonitorForm);
        Func<int,int,bool,long> combine = (lo,hi,u) => (long)sim.GetMethod("CombineDouble", BindingFlags.Static|BindingFlags.NonPublic).Invoke(null, new object[]{lo,hi,u});
        Func<string,bool> isDw = s => (bool)sim.GetMethod("IsDoubleWord", BindingFlags.Static|BindingFlags.NonPublic).Invoke(null, new object[]{s});
        Check("D318=3296, D319=339 is 22,220,000 (the 2222.00 on screen)", combine(3296, 339, false) == 22220000, combine(3296,339,false).ToString());
        Check("a negative value survives", combine(0xFFFF, 0xFFFF, false) == -1);
        Check("'Double Word [Signed]' is recognised", isDw("Double Word [Signed]"));
        Check("'Word [Signed]' is not", !isDw("Word [Signed]"));

        Console.WriteLine("--- cursors: a hand only where there is something to click ---");
        var page = new Panel();
        var meter = new MeterHMI(); var code = new HexCodeHMI(); var box = new CheckBoxHMI(); var grid = new DataGridView();
        page.Controls.AddRange(new Control[]{ meter, code, box, grid });
        UiStyle.ApplyInteractionCues(page);
        Check("meter: arrow", meter.Cursor == Cursors.Default);
        Check("alarm code cell: arrow", code.Cursor == Cursors.Default);
        Check("grid: arrow", grid.Cursor == Cursors.Default);
        Check("checkbox (something to click): hand", box.Cursor == Cursors.Hand);
        box.Enabled = false;
        Check("  and an arrow once it is disabled", box.Cursor == Cursors.Default);

        Console.WriteLine("--- stations mounted AFTER the watch starts are polled ---");
        var live = new List<int> { 31 };
        ConnectionMonitor.Start(id => null, live);                  // before the sub-machine exists
        live.Add(32);                                               // ... the sub-machine mounts
        ConnectionMonitor.Beat();
        Check("the late station was polled", ConnectionMonitor.Get(32).LastAttempt != DateTime.MinValue);
        ConnectionMonitor.Get(33);                                  // known of, never polled
        Check("a station never polled is NOT listed as failed", !ConnectionMonitor.Down.Any(s => s.StationId == 33));

        Console.WriteLine(fail==0?"\nFIXES ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

