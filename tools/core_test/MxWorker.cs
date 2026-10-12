using System;
using System.Diagnostics;
using System.Threading;
using Kaban.Drivers.MitsubishiActUtl;

class T {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    /// Occupies the worker from another thread, as a scan read or an Open() would.
    static void Occupy(StaWorker w, int ms, ManualResetEvent started){
        new Thread(() => { bool x; w.TryRun(() => { started.Set(); Thread.Sleep(ms); return true; }, ms + 2000, out x); }).Start();
        started.WaitOne(2000);
    }

    [STAThread] static void Main(){
        var ui = Thread.CurrentThread;
        var w = new StaWorker("MX test");

        Console.WriteLine("--- the COM object's thread is its own ---");
        Thread ranOn = null; ApartmentState apt = ApartmentState.Unknown;
        bool r;
        Check("a call runs", w.TryRun(() => { ranOn = Thread.CurrentThread; apt = ranOn.GetApartmentState(); return true; }, 2000, out r) && r);
        Check("NOT on the caller's (UI) thread", ranOn != null && ranOn != ui, ranOn == null ? "null" : ranOn.Name);
        Check("on an STA thread, as MX Component requires", apt == ApartmentState.STA, apt.ToString());

        Console.WriteLine("--- THE REPORTED BUG: a click while a scan read is in flight ---");
        var s1 = new ManualResetEvent(false);
        Occupy(w, 150, s1);                                  // an ordinary scan read
        var sw = Stopwatch.StartNew();
        int limit;
        bool got = w.TryRun(() => 3333, 1000, out limit);    // the click reading a limit tag
        sw.Stop();
        Check("the click's read WAITS ITS TURN and succeeds (was: refused)", got && limit == 3333, got + " / " + limit);
        Check("  costing only the in-flight read's time", sw.ElapsedMilliseconds < 600, sw.ElapsedMilliseconds + " ms");

        Console.WriteLine("--- a STUCK call still cannot freeze the screen ---");
        var s2 = new ManualResetEvent(false);
        Occupy(w, 2500, s2);                                 // Open() to a PLC that is not there
        Check("IsBusy shows it, so Connect can refuse at once", w.IsBusy);
        sw.Restart();
        int rc;
        got = w.TryRun(() => 0, 800, out rc);
        sw.Stop();
        Check("a read behind it gives up", !got);
        Check("  at its own bound, not after the stuck call",
              sw.ElapsedMilliseconds >= 750 && sw.ElapsedMilliseconds < 1300, sw.ElapsedMilliseconds + " ms");

        Console.WriteLine("--- and the worker recovers afterwards ---");
        Thread.Sleep(2000);
        Check("usable again once the stuck call has finished", w.TryRun(() => 7, 1000, out rc) && rc == 7, rc.ToString());

        Console.WriteLine("--- a call that itself hangs is abandoned on time ---");
        sw.Restart();
        got = w.TryRun(() => { Thread.Sleep(3000); return 1; }, 300, out rc);
        sw.Stop();
        Check("returns false at the bound", !got && sw.ElapsedMilliseconds < 800, sw.ElapsedMilliseconds + " ms");
        Thread.Sleep(3000);

        Console.WriteLine("--- a driver method calling another runs inline, no deadlock ---");
        int inner = 0;
        sw.Restart();
        Check("nested call completes",
              w.TryRun(() => { int v; w.TryRun(() => 42, 1000, out v); inner = v; return true; }, 2000, out r) && inner == 42,
              inner.ToString());
        Check("  immediately", sw.ElapsedMilliseconds < 500, sw.ElapsedMilliseconds + " ms");

        Console.WriteLine("--- a throwing call does not kill the worker ---");
        Check("the failure is reported as false", !w.TryRun<int>(() => { throw new InvalidOperationException("MX error"); }, 1000, out rc));
        Check("and the next call still works", w.TryRun(() => 5, 1000, out rc) && rc == 5);

        Console.WriteLine(fail==0?"\nSTA WORKER ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

