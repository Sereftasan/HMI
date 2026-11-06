using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.Drivers.Simulation;

// 6 November: the PLC Monitor's auto-made tab "HMI -> PLC" shows the job list as one table (D5000 / D6000 / D7000, 500 double words
// each) - thousands of devices a read. PlcBlocks.ReadMany reads a table's runs of word devices as blocks (one call each) and the rest
// as before (the random read), each value where it was asked.
class ReadManyBlocks {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }

    /// <summary>The simulator, its calls counted; a block it refuses from a device on.</summary>
    class Counting : IPlcDriver, IPlcBlockAccess {
        public readonly SimulationDriver Sim = new SimulationDriver(1);
        public readonly List<string> Blocks = new List<string>();
        public int Multi, MultiDevices;
        public string RefuseFrom = "";
        public bool IsConnected { get { return true; } }
        public bool IsHealthy { get { return true; } }
        public bool Connect() { return true; }
        public void Disconnect() { }
        public int Read(string a) { return Sim.Read(a); }
        public int[] ReadMulti(string[] a) { return Sim.ReadMulti(a); }
        public int Write(string a, int v) { return Sim.Write(a, v); }
        public bool TryRead(string a, out int v) { return Sim.TryRead(a, out v); }
        public bool TryReadMulti(string[] a, out int[] v) { Multi++; MultiDevices += a.Length; return Sim.TryReadMulti(a, out v); }
        public bool TryReadBlock(string first, int count, out short[] v) {
            Blocks.Add(first + "x" + count);
            if (RefuseFrom.Length > 0 && first == RefuseFrom) { v = null; return false; }
            return Sim.TryReadBlock(first, count, out v);
        }
        public int WriteBlock(string first, short[] v) { return Sim.WriteBlock(first, v); }
        public bool RefusesOutOfRange { get { return false; } }
    }

    static void Main() {
        var d = new Counting();
        d.Sim.Connect();
        for (int i = 0; i < 1000; i++) { d.Sim.Write("D" + (5000 + i), i % 7 == 0 ? -i : i); d.Sim.Write("D" + (6000 + i), 3 * i); }
        d.Sim.Write("M5", 1); d.Sim.Write("D100", 42); d.Sim.Write("R25600", 1234);

        // the job list's IDs and positions (1000 words each), a bit, a word on its own, a structure's word
        var asked = new List<string>();
        for (int i = 0; i < 1000; i++) asked.Add("D" + (5000 + i));
        for (int i = 0; i < 1000; i += 2) asked.Add("D" + (6000 + i));     // every other word - gaps of one
        asked.Add("M5"); asked.Add("D100"); asked.Add("R25600"); asked.Add("D5003");   // D5003 a second time
        int[] v;
        bool ok = PlcBlocks.ReadMany(d, asked.ToArray(), out v);
        Check("read", ok && v.Length == asked.Count);
        Check("D5000 - D5999 and D6000, D6002 ... D6998 (gaps of one): three blocks, 960 words a call at most",
              d.Blocks.SequenceEqual(new[] { "D5000x960", "D5960x959", "D6920x79" }), string.Join(", ", d.Blocks));
        Check("the bit, the single word, the structure's word: the random read (one call)", d.Multi == 1 && d.MultiDevices == 3,
              d.Multi + " calls, " + d.MultiDevices + " devices");
        Check("each value where it was asked (negative words too)", v[0] == 0 && v[7] == -7 && v[999] == 999 && v[1000] == 0 && v[1001] == 6 && v[1499] == 3 * 998 &&
              v[1500] == 1 && v[1501] == 42 && v[1502] == 1234 && v[1503] == 3,
              string.Join(" ", new[] { v[0], v[7], v[999], v[1000], v[1001], v[1499], v[1500], v[1501], v[1502], v[1503] }));

        // a block the controller refuses: its devices asked one by one, nothing lost
        d.Blocks.Clear(); d.Multi = 0; d.MultiDevices = 0; d.RefuseFrom = "D5000";
        ok = PlcBlocks.ReadMany(d, asked.ToArray(), out v);
        Check("a block refused: its devices in the random read (D5003 asked twice), the values still right", ok && d.MultiDevices == 3 + 960 + 1 && v[7] == -7 && v[958] == 958 && v[1501] == 42,
              ok + " " + d.Multi + " calls, " + d.MultiDevices + " devices: " + v[7] + " " + v[958] + " " + v[1501]);

        // a few devices: as before
        d.Blocks.Clear(); d.Multi = 0; d.RefuseFrom = "";
        ok = PlcBlocks.ReadMany(d, new[] { "D5000", "D5001", "D5002" }, out v);
        Check("three devices: one random read, no block", ok && d.Blocks.Count == 0 && d.Multi == 1 && v[2] == 2);
        // devices far apart: no block spans the gap
        var far = Enumerable.Range(0, 40).Select(i => "D" + (100 + 50 * i)).ToArray();
        d.Blocks.Clear(); d.Multi = 0;
        ok = PlcBlocks.ReadMany(d, far, out v);
        Check("40 words 50 apart: no block (the gaps are not read)", ok && d.Blocks.Count == 0 && d.Multi == 1 && v[0] == 42, string.Join(", ", d.Blocks));

        Console.WriteLine(fail == 0 ? "\nREAD MANY ALL PASS" : "\nREAD MANY " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
