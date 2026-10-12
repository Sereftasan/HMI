using System;
using System.Linq;
using System.Threading;
using Kaban.Core.Helpers;
using Kaban.Drivers.Simulation;

class S {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    static void Main(){
        ConnectionMonitor.GraceTime = TimeSpan.FromMilliseconds(300);

        var sim = new SimulationDriver(1);
        Check("simulation connects", sim.Connect() && sim.IsConnected);

        sim.Write("D100", 1234);
        int v;
        Check("a written value reads back", sim.TryRead("D100", out v) && v == 1234, v.ToString());

        Console.WriteLine("--- ONE bad address must not condemn the batch ---");
        int[] values;
        // "D100" is real; the blank and the odd one are what a page can easily hold
        var batch = new[] { "D100", "", "ZZZ9" };
        bool all = sim.TryReadMulti(batch, out values);
        Check("TryReadMulti reports partial failure", !all);
        Check("but it still RETURNS the values", values != null && values.Length == 3,
              values == null ? "null" : values.Length.ToString());
        Check("  and the real address still read correctly", values[0] == 1234, values[0].ToString());

        // this is exactly the judgement the scanner now makes
        bool answered = sim.IsConnected && values != null;
        Check("so the station counts as ANSWERING", answered);

        Console.WriteLine("--- and the monitor agrees ---");
        ConnectionMonitor.ReportRead(9, answered);
        Check("station is live", ConnectionMonitor.Get(9).IsLive);

        Console.WriteLine("--- loss and recovery ---");
        ConnectionMonitor.ReportRead(9, false, "not connected");
        Thread.Sleep(400);
        Check("goes down after the grace time", !ConnectionMonitor.Get(9).IsLive);
        Check("with code C02", ConnectionMonitor.Get(9).ErrorCode == "C02",
              ConnectionMonitor.Get(9).ErrorCode);

        ConnectionMonitor.ReportRead(9, true);
        Check("RECOVERS on the next good read", ConnectionMonitor.Get(9).IsLive);
        Check("  and the code clears", ConnectionMonitor.Get(9).ErrorCode == "");
        Check("  and it leaves the down list", !ConnectionMonitor.Down.Any(x => x.StationId == 9));

        Console.WriteLine("--- the help document has a section per code ---");
        var doc = TroubleshootingCatalog.ResolveConnection("C02");
        Check("resolves with the code as its section", doc.Section == "C02", doc.Section);
        var html = System.IO.File.ReadAllText(doc.FullPath);
        foreach (var c in new[]{"C01","C02","C03","C04"})
            Check("  the page has an anchor for " + c, html.Contains("id=\"" + c + "\""));
        Check("  and highlights the linked one", html.Contains("h2:target"));

        Console.WriteLine(fail==0?"\nSIMULATION ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

