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

        // 23 October: the PLC Connection page's Read sent its block calls back to back - the scanner and the
        // connection beat never found the thread free, gave up after a second, and the link was taken down
        Console.WriteLine("--- a bulk job's calls back to back no longer keep the others out ---");
        int stop = 0, bulk = 0;
        var bulkThread = new Thread(() => {
            while (Volatile.Read(ref stop) == 0) {
                int x; bool st;
                if (w.TryRunPolite(() => { Thread.Sleep(5); return 1; }, 5000, out x, out st)) Interlocked.Increment(ref bulk);
            }
        });
        bulkThread.Start();
        Thread.Sleep(100);
        long worst = 0; int turns = 0;
        for (int i = 0; i < 20; i++) {
            sw.Restart();
            int v;
            if (w.TryRun(() => 9, 1000, out v) && v == 9) turns++;
            worst = Math.Max(worst, sw.ElapsedMilliseconds);
            Thread.Sleep(20);
        }
        Volatile.Write(ref stop, 1);
        bulkThread.Join(3000);
        Check("every one of the screen's reads had its turn while the bulk job ran", turns == 20, turns + " of 20");
        Check("  at once - behind the bulk call in flight at most", worst < 200, worst + " ms");
        Check("  and the bulk job went on between them", bulk > 20, bulk + " bulk calls");
        var s0 = new ManualResetEvent(false);
        Occupy(w, 900, s0);
        bool started0; int r0;
        Check("a bulk call that never has its turn says so (busy - not a dead link)",
              !w.TryRunPolite(() => 1, 300, out r0, out started0) && !started0);
        Thread.Sleep(1000);

        // 5 November, found on the panel (GX Simulator3, the machine's log read beside the scan): a read that waited most of its second
        // behind another call was given what was left of it, gave up - and the driver took the link down (Stalled) every minute or two
        Console.WriteLine("--- a call that waited behind another: its own time once it runs; never run: busy, not dead ---");
        var s3 = new ManualResetEvent(false);
        Occupy(w, 700, s3);
        bool st3; int r3;
        sw.Restart();
        bool ok3 = w.TryRun(() => { Thread.Sleep(500); return 3; }, 1000, out r3, out st3);
        sw.Stop();
        Check("waited 700 ms, ran 500 ms - done (was: given the 300 ms left, abandoned)", ok3 && st3 && r3 == 3, ok3 + " " + st3 + " " + sw.ElapsedMilliseconds + " ms");
        var s4 = new ManualResetEvent(false);
        Occupy(w, 1500, s4);
        sw.Restart();
        ok3 = w.TryRun(() => 4, 500, out r3, out st3);
        sw.Stop();
        Check("  behind a call longer than its bound: gives up at the bound and says it never ran (the driver keeps the link)",
              !ok3 && !st3 && sw.ElapsedMilliseconds < 900, ok3 + " " + st3 + " " + sw.ElapsedMilliseconds + " ms");
        Thread.Sleep(1300);

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

