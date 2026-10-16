using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.UI.Controls;
using Kaban.UI.Plc;

// 17 October: "What the HMI saw" showed Robot X jogged both ways, its limits 0 and its
// reference sensor ON - none of it true. A read in flight when the page or a tab changed
// came back with the OLD batch's values, looked up by the NEW batch's index.
class S {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }

    /// <summary>Reads one device, as a meter does.</summary>
    class Probe : Control, IPlcBoundControl
    {
        public string Address { get; set; }
        public int PlcNumber { get { return 1; } }
        public int Seen { get; set; } = -1;
        public void UpdateState(Func<string, int> lookup, Func<Kaban.Core.Enums.SystemCondition, bool> checkGlobal) { Seen = lookup(Address); }
    }

    static readonly BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Instance;

    static Dictionary<int, string[]> Batch(PlcScanner s)
    {
        return new Dictionary<int, string[]>((Dictionary<int, string[]>)typeof(PlcScanner).GetField("_plcBatch", Any).GetValue(s));
    }

    static void Apply(PlcScanner s, Dictionary<int, string[]> work, Dictionary<int, int[]> fresh, Dictionary<int, string> failed)
    {
        typeof(PlcScanner).GetMethod("ApplyScan", Any).Invoke(s, new object[] { work, fresh, failed });
    }

    /// <summary>The values the PLC would give for that batch: D10 = 111, D20 = 222, D30 = 333.</summary>
    static int[] Values(string[] batch)
    {
        var plc = new Dictionary<string, int> { { "D10", 111 }, { "D20", 222 }, { "D30", 333 } };
        return batch.Select(a => plc.ContainsKey(a) ? plc[a] : 0).ToArray();
    }

    [STAThread] static void Main() {
        var page = new UserControl();
        var a = new Probe { Address = "D10" };
        var b = new Probe { Address = "D20" };
        page.Controls.Add(a);
        page.Controls.Add(b);
        var scanner = new PlcScanner(page);
        scanner.RefreshTags(true);
        scanner.Stop();

        Console.WriteLine("--- a scan read with the batch it was read with ---");
        var work = Batch(scanner);
        Check("the page's two devices are in station 1's batch", work.ContainsKey(1) && work[1].Contains("D10") && work[1].Contains("D20"),
              work.ContainsKey(1) ? string.Join(",", work[1]) : "none");
        Apply(scanner, work, new Dictionary<int, int[]> { { 1, Values(work[1]) } }, new Dictionary<int, string>());
        Check("each reads its own value", a.Seen == 111 && b.Seen == 222, a.Seen + " " + b.Seen);

        Console.WriteLine("--- the page changed while the read was in flight ---");
        var inFlight = Batch(scanner);                      // the worker's snapshot
        var readBack = Values(inFlight[1]);                 // what the PLC answered for it
        var c = new Probe { Address = "D30" };
        page.Controls.Add(c);
        page.Controls.SetChildIndex(c, 0);                 // its device first: every place moves
        scanner.RefreshTags();
        scanner.Stop();
        var now = Batch(scanner);
        int before = Array.IndexOf(inFlight[1], "D10"), after = Array.IndexOf(now[1], "D10");
        Check("the new batch has another order (D10 was at " + before + ", is at " + after + ")", before != after);
        a.Seen = b.Seen = c.Seen = -1;
        Apply(scanner, inFlight, new Dictionary<int, int[]> { { 1, readBack } }, new Dictionary<int, string>());
        Check("the old read is looked up by ITS batch: D10 = 111, D20 = 222 (another device's value before)", a.Seen == 111 && b.Seen == 222,
              a.Seen + " " + b.Seen);
        Check("the device that was not in it yet: 0 until the next read (never another's value)", c.Seen == 0, c.Seen.ToString());

        Console.WriteLine("--- the next read, with the new batch ---");
        Apply(scanner, now, new Dictionary<int, int[]> { { 1, Values(now[1]) } }, new Dictionary<int, string>());
        Check("all three read their own values", a.Seen == 111 && b.Seen == 222 && c.Seen == 333, a.Seen + " " + b.Seen + " " + c.Seen);

        Console.WriteLine("--- a failed read: the last good values, by the batch THEY came with ---");
        page.Controls.Remove(c);
        scanner.RefreshTags();
        scanner.Stop();
        var shorter = Batch(scanner);
        a.Seen = b.Seen = -1;
        Apply(scanner, shorter, new Dictionary<int, int[]>(), new Dictionary<int, string> { { 1, "read failed" } });
        Check("stale but real: D10 = 111, D20 = 222", a.Seen == 111 && b.Seen == 222, a.Seen + " " + b.Seen);

        Console.WriteLine(fail == 0 ? "ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
