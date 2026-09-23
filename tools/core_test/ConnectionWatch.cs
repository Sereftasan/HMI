using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;

/// <summary>A driver whose link can be pulled and restored, like a real cable.</summary>
class Cable : IPlcDriver {
    public bool Plugged = true;
    public bool IsConnected { get; private set; }
    public bool IsHealthy { get { return IsConnected; } }
    public bool Connect(){ IsConnected = Plugged; return IsConnected; }
    public void Disconnect(){ IsConnected = false; }
    public bool TryRead(string a, out int v){ v = 102; if (!Plugged) { IsConnected = false; return false; } return IsConnected; }
    public int Read(string a){ int v; TryRead(a, out v); return v; }
    public bool TryReadMulti(string[] a, out int[] v){ v = new int[a.Length]; return Plugged && IsConnected; }
    public int[] ReadMulti(string[] a){ int[] v; TryReadMulti(a, out v); return v; }
    public int Write(string a, int v){ return Plugged ? 0 : -1; }
}

class W {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    // Stands in for MainForm's Forms timer: the monitor no longer owns a thread,
    // so whoever hosts it drives Beat() - on the UI thread in the real program.
    static void Poll(int ms){
        var end = DateTime.Now.AddMilliseconds(ms);
        while (DateTime.Now < end) { ConnectionMonitor.Beat(); Thread.Sleep(250); }
    }

    static void Main(){
        ConnectionMonitor.GraceTime = TimeSpan.FromMilliseconds(600);   // deliberately too short
        var cable = new Cable();
        cable.Connect();

        ConnectionMonitor.SetVersionDevice(11, "D36");
        ConnectionMonitor.Start(id => cable, new List<int> { 11 });

        Check("Start raised the grace above the beat period",
              ConnectionMonitor.GraceTime.TotalMilliseconds >= ConnectionMonitor.BeatMs * 3,
              ConnectionMonitor.GraceTime.TotalMilliseconds + " ms");

        Console.WriteLine("--- polled by its host, with no page open ---");
        Poll(1000);
        Check("station reports live", ConnectionMonitor.Get(11).IsLive);
        Check("and the version was read", ConnectionMonitor.Get(11).Version == "1.02", ConnectionMonitor.Get(11).Version);

        Console.WriteLine("--- a re-entrant poll is refused ---");
        // Beat is guarded; calling it from inside itself must be a no-op, not a
        // second sweep over the drivers.
        ConnectionMonitor.Beat(); ConnectionMonitor.Beat();
        Check("repeated polls are harmless", ConnectionMonitor.Get(11).IsLive);

        Console.WriteLine("--- pull the cable ---");
        cable.Plugged = false;
        Poll(4500);
        Check("goes down by itself", !ConnectionMonitor.Get(11).IsLive);
        Check("known down, so controls lock", ConnectionMonitor.IsKnownDown(11));
        Check("with a code to show", ConnectionMonitor.Get(11).ErrorCode.Length == 3, ConnectionMonitor.Get(11).ErrorCode);

        Console.WriteLine("--- plug it back in ---");
        cable.Plugged = true;
        Poll(2000);
        Check("RECOVERS with no operator action", ConnectionMonitor.Get(11).IsLive, ConnectionMonitor.Get(11).ErrorCode);
        Check("controls unlock", !ConnectionMonitor.IsKnownDown(11));
        Check("the code clears", ConnectionMonitor.Get(11).ErrorCode == "");
        Check("and it leaves the failure list", !ConnectionMonitor.Down.Any(x => x.StationId == 11));

        Console.WriteLine(fail==0?"\nWATCH ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

