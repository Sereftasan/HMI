using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Kaban.Core.Helpers;
using Kaban.Drivers.Siemens;

class Z {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}
    static void Main(){
        const string dead = "10.255.255.1";              // routes nowhere: TCP waits out its timeout
        Console.WriteLine("--- the raw Siemens driver, called from the UI thread ---");
        var raw = new SiemensS7(dead);
        var sw = Stopwatch.StartNew(); raw.Connect(); sw.Stop();
        Console.WriteLine("  one connect attempt held the caller for {0} ms  <- the freeze, every second", sw.ElapsedMilliseconds);

        Console.WriteLine("--- the same driver, registered as the program registers it ---");
        PlcManager.Instance.RegisterDriver(41, new SiemensS7(dead));
        var drv = PlcManager.Instance.GetDriverForMonitoring(41);
        Check("wrapped on registration", drv is BoundedDriver);
        ConnectionMonitor.Start(PlcManager.Instance.GetDriverForMonitoring, new List<int> { 41 });

        var worst = 0L; var total = Stopwatch.StartNew();
        for (int i = 0; i < 8; i++) {                     // eight polls, as the timer would run them
            sw.Restart(); ConnectionMonitor.Beat(); sw.Stop();
            worst = Math.Max(worst, sw.ElapsedMilliseconds);
            System.Threading.Thread.Sleep(250);
        }
        Check("no poll holds the UI thread for long", worst < 120, "worst poll " + worst + " ms");
        Check("the station is still reported down, with a code",
              ConnectionMonitor.IsKnownDown(41) && ConnectionMonitor.Get(41).ErrorCode.Length == 3, ConnectionMonitor.Get(41).ErrorCode);

        sw.Restart(); int v; bool got = drv.TryRead("DB1.DBW0", out v); sw.Stop();
        Check("a read on the dead link is refused at once", !got && sw.ElapsedMilliseconds < 20, sw.ElapsedMilliseconds + " ms");
        sw.Restart(); int rc = drv.Write("DB1.DBW0", 1); sw.Stop();
        Check("so is a write - the button-press path", rc != 0 && sw.ElapsedMilliseconds < 20, sw.ElapsedMilliseconds + " ms");

        Console.WriteLine(fail==0?"\nFREEZE ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

