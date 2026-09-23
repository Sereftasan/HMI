using System;
using System.Linq;
using System.Threading;
using Kaban.Core.Helpers;

class C {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    static void Main(){
        ConnectionMonitor.GraceTime = TimeSpan.FromMilliseconds(400);   // keep the test quick

        Console.WriteLine("--- a station that has never answered is DOWN, not unknown ---");
        var s = ConnectionMonitor.Get(7);
        Check("never-seen station counts as down", !s.IsLive);
        // Listed once it has been POLLED and stayed silent. A station nobody has
        // asked yet is not a failure - listing it produced "Connection failed"
        // rows with no code for stations that had simply never been polled.
        Check("not listed before anyone has polled it", !ConnectionMonitor.Down.Any(x => x.StationId == 7));
        ConnectionMonitor.ReportRead(7, false, "not connected");
        Check("listed once polled and silent", ConnectionMonitor.Down.Any(x => x.StationId == 7));

        Console.WriteLine("--- a good read brings it up ---");
        ConnectionMonitor.ReportRead(7, true);
        Check("live after a successful read", ConnectionMonitor.Get(7).IsLive);
        Check("no longer listed", !ConnectionMonitor.Down.Any(x => x.StationId == 7));

        Console.WriteLine("--- one missed read is a hiccup, not a fault ---");
        ConnectionMonitor.ReportRead(7, false, "read failed");
        Check("still live inside the grace time", ConnectionMonitor.Get(7).IsLive);

        Console.WriteLine("--- but silence past the grace time is a fault ---");
        Thread.Sleep(500);
        Check("now down", !ConnectionMonitor.Get(7).IsLive);
        Check("with the read-failure code C03", ConnectionMonitor.Get(7).ErrorCode == "C03",
              ConnectionMonitor.Get(7).ErrorCode);
        Check("and the screen values are known to be stale",
              ConnectionMonitor.Get(7).StaleFor.TotalMilliseconds > 400,
              (int)ConnectionMonitor.Get(7).StaleFor.TotalMilliseconds + " ms");

        Console.WriteLine("--- codes are stable, whatever the driver says ---");
        Check("no driver -> C01", ConnectionMonitor.CodeFor("no driver registered") == "C01");
        Check("not connected -> C02", ConnectionMonitor.CodeFor("not connected") == "C02");
        Check("read failed -> C03", ConnectionMonitor.CodeFor("read failed") == "C03");
        Check("anything else -> C04", ConnectionMonitor.CodeFor("garbled frame") == "C04");
        Check("each code explains itself",
              new[]{"C01","C02","C03","C04"}.All(c => ConnectionMonitor.DescribeCode(c).Length > 10));

        Console.WriteLine("--- the version device reads as a human version ---");
        ConnectionMonitor.ReportVersion(7, 102);
        Check("102 shows as 1.02", ConnectionMonitor.Get(7).Version == "1.02",
              ConnectionMonitor.Get(7).Version);
        ConnectionMonitor.ReportVersion(7, 1015);
        Check("1015 shows as 10.15", ConnectionMonitor.Get(7).Version == "10.15",
              ConnectionMonitor.Get(7).Version);
        ConnectionMonitor.ReportVersion(7, 0);
        Check("no version device leaves it blank", ConnectionMonitor.Get(7).Version == "");

        Console.WriteLine("--- recovery clears it ---");
        ConnectionMonitor.ReportRead(7, true);
        Check("back up after a good read", ConnectionMonitor.Get(7).IsLive);
        Check("and the code is cleared", ConnectionMonitor.Get(7).ErrorCode == "");

        Console.WriteLine("--- the help document exists ---");
        var doc = TroubleshootingCatalog.ResolveConnection("C02");
        Check("a code resolves to a document", doc != null, doc == null ? "null" : doc.FileName);
        Check("  which is on disk", doc.Exists, doc.FullPath);

        Console.WriteLine(fail==0?"\nCONNECTION ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

