using System;
using Kaban.Drivers.Delta;

class TDelta {
    static int fail = 0;
    static void Check(string what, bool ok, string d="") {
        Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+what+(d.Length>0?"   ["+d+"]":""));
        if (!ok) fail++;
    }
    static void Main(string[] a) {
        var drv = new DeltaModbusTcp("127.0.0.1:" + a[0] + ":1");
        Check("connects", drv.Connect(), drv.LastConnectError);

        int v;
        Check("M0 reads 1 (preloaded coil)", drv.TryRead("M0", out v) && v==1, "v="+v);
        Check("M5 reads 1",                  drv.TryRead("M5", out v) && v==1, "v="+v);
        Check("M1 reads 0",                  drv.TryRead("M1", out v) && v==0, "v="+v);
        Check("D100 reads 1234",             drv.TryRead("D100", out v) && v==1234, "v="+v);
        // signed decode: 0xFFFE must be -2, not 65534
        Check("D101 reads -2 (signed)",      drv.TryRead("D101", out v) && v==-2, "v="+v);
        // X is OCTAL: X10 is the 8th input, i.e. coil base+8
        Check("X10 is OCTAL (coil +8)",      drv.TryRead("X10", out v) && v==1, "v="+v);
        Check("X18 rejected (not octal)",    !drv.TryRead("X18", out v));

        Check("write D200",  drv.Write("D200", 4321)==0);
        Check("read back",   drv.TryRead("D200", out v) && v==4321, "v="+v);
        Check("write M20 on", drv.Write("M20", 1)==0);
        Check("M20 reads 1",  drv.TryRead("M20", out v) && v==1, "v="+v);
        Check("write M20 off",drv.Write("M20", 0)==0);
        Check("M20 reads 0",  drv.TryRead("M20", out v) && v==0, "v="+v);

        // the batch path the scanner actually uses
        int[] vals;
        bool ok = drv.TryReadMulti(new[]{"D100","D101","M0","M1","D200"}, out vals);
        Check("batch read ok", ok);
        Check("batch values right",
              vals[0]==1234 && vals[1]==-2 && vals[2]==1 && vals[3]==0 && vals[4]==4321,
              string.Join(",", vals));

        Check("bad address fails, not silently 0", !drv.TryRead("ZZ9", out v));
        Check("IsHealthy after a good read", drv.TryRead("D100", out v) && drv.IsHealthy);

        drv.Disconnect();
        Console.WriteLine(fail==0 ? "\nDELTA ALL PASS" : "\nDELTA " + fail + " FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

