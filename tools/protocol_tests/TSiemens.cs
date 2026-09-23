using System;
using Kaban.Drivers.Siemens;

class TSiemens {
    static int fail = 0;
    static void Check(string what, bool ok, string d="") {
        Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+what+(d.Length>0?"   ["+d+"]":""));
        if (!ok) fail++;
    }
    static void Main(string[] a) {
        // rack 0 slot 1 -> destination TSAP 0x0101, the S7-1200 default
        var drv = new SiemensS7("127.0.0.1:0:1");
        Console.WriteLine("(the mock listens on 102 via socat; endpoint " + drv.Endpoint + ")");
        Check("connects (COTP + setup)", drv.Connect(), drv.LastConnectError);

        int v;
        Check("write MW20 = 1234", drv.Write("MW20", 1234)==0);
        Check("read  MW20 = 1234", drv.TryRead("MW20", out v) && v==1234, "v="+v);

        // big-endian is the easiest thing to get backwards in this driver
        Check("write MW22 = -2",   drv.Write("MW22", -2)==0);
        Check("read  MW22 = -2",   drv.TryRead("MW22", out v) && v==-2, "v="+v);

        Check("write MD30 = 70000", drv.Write("MD30", 70000)==0);
        Check("read  MD30 = 70000", drv.TryRead("MD30", out v) && v==70000, "v="+v);

        Check("write M10.3 = 1",   drv.Write("M10.3", 1)==0);
        Check("read  M10.3 = 1",   drv.TryRead("M10.3", out v) && v==1, "v="+v);
        Check("M10.2 still 0",     drv.TryRead("M10.2", out v) && v==0, "v="+v);
        Check("write M10.3 = 0",   drv.Write("M10.3", 0)==0);
        Check("read  M10.3 = 0",   drv.TryRead("M10.3", out v) && v==0, "v="+v);

        Check("DB write DB10.DBW4", drv.Write("DB10.DBW4", 999)==0);
        Check("DB read  DB10.DBW4", drv.TryRead("DB10.DBW4", out v) && v==999, "v="+v);
        Check("DB bit DB10.DBX8.5",  drv.Write("DB10.DBX8.5", 1)==0
                                     && drv.TryRead("DB10.DBX8.5", out v) && v==1, "v="+v);

        // bare M10 is ambiguous - must be refused, not read as a byte
        Check("bare M10 refused",   !drv.TryRead("M10", out v));
        Check("M10.9 refused",      !drv.TryRead("M10.9", out v));
        Check("German E0.1 accepted (input)", drv.TryRead("E0.1", out v));

        int[] vals;
        bool ok = drv.TryReadMulti(new[]{"MW20","MW22","MD30","M10.3","DB10.DBW4"}, out vals);
        Check("batch read ok", ok);
        Check("batch values right",
              vals[0]==1234 && vals[1]==-2 && vals[2]==70000 && vals[3]==0 && vals[4]==999,
              string.Join(",", vals));

        drv.Disconnect();
        Console.WriteLine(fail==0 ? "\nSIEMENS ALL PASS" : "\nSIEMENS " + fail + " FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

