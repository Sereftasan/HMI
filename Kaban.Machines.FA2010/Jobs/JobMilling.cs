using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.Profiles.Machining;

namespace Kaban.Machines.FA2010.Jobs
{
    /// <summary>
    /// THE MILLING STEPS ON THE PLC'S REQUEST (6 November: the movement codes of FA2010_DeviceList; the old program's
    /// TransferProvider_FXCpu.WriteGCodes_ByUnitNo): a milling unit about to machine an operation of the bar asks for its steps -
    ///
    ///   unit 1: L101 on, the operation's code (1 - 100) in D60  ->  its steps into R0 (16 words a step, code 0 after the last; at least
    ///           the old program's 25 steps written, the PLC's area takes 400),
    ///           then L102 on ("sent"); L101 off -> L102 off
    ///   unit 2: L103, D61, R12800, L104 the same
    ///
    /// The devices are the label list's (PC_Freze_Unite1_G_Code_Gonder ...). The steps are those of the bar sent last (JobSender), kept in
    /// C:\ProgramData\KabanHMI\JobMilling.cfg - a restart of the HMI in the middle of a bar still answers. An operation the bar does not
    /// have is not answered (the PLC waits; the log says why) - never a program of another bar.
    /// </summary>
    internal static class JobMilling
    {
        internal const int Station = 1;
        /// <summary>How often the requests are looked at (only while a bar's operations are kept).</summary>
        internal const int PollMs = 250;
        internal static string StatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "JobMilling.cfg");
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        private static readonly object Gate = new object();

        internal sealed class Unit
        {
            public int No;
            public string Ask = "", Sent = "", Code = "", Area = "";
            /// <summary>The request answered (L102 set) - until the PLC takes it away.</summary>
            public bool Answered;
            /// <summary>The request of an operation not had, said once.</summary>
            public int Refused;
        }

        internal static readonly Unit[] Units =
        {
            new Unit { No = 1, Ask = "PC_Freze_Unite1_G_Code_Gonder|L101", Sent = "PC_Freze_Unite1_G_Code_Gonderildi|L102", Code = "PC_Freze_Unite1_G_Code_No|D60", Area = "PC_Unite1_FrezeAdimlari|R0" },
            new Unit { No = 2, Ask = "PC_Freze_Unite2_G_Code_Gonder|L103", Sent = "PC_Freze_Unite2_G_Code_Gonderildi|L104", Code = "PC_Freze_Unite2_G_Code_No|D61", Area = "PC_Unite2_FrezeAdimlari|R12800" }
        };

        /// <summary>The bar's operations: code -> its words (400), its name.</summary>
        private static Dictionary<int, KeyValuePair<string, short[]>> _ops = new Dictionary<int, KeyValuePair<string, short[]>>();
        private static string _bar = "";
        private static System.Threading.Timer _timer;

        internal static string Device(string spec)
        {
            var p = spec.Split('|');
            return JobSender.AddressOf(Station, p[0], p[1]);
        }

        /// <summary>The operations the PLC can ask for now (tests, the page).</summary>
        internal static int Count { get { lock (Gate) return _ops.Count; } }
        internal static string Bar { get { lock (Gate) return _bar; } }

        /// <summary>Listening (once): the last bar's steps read back, the PLC's requests looked at every 250 ms while there are any.</summary>
        internal static void Start()
        {
            lock (Gate)
            {
                if (_timer != null) return;
                Load();
                _timer = new System.Threading.Timer(_ => Tick(), null, 500, PollMs);
            }
        }

        /// <summary>The bar just sent: its operations' steps are the ones the PLC gets from now on.</summary>
        internal static void Take(BarPlan plan, string title)
        {
            var ops = new Dictionary<int, KeyValuePair<string, short[]>>();
            foreach (var op in plan.Milling)
                ops[op.Index] = new KeyValuePair<string, short[]>(op.Label, PlcSteps.Words(op.Steps));
            lock (Gate)
            {
                _ops = ops;
                _bar = title ?? "";
                foreach (var u in Units) { u.Answered = false; u.Refused = 0; }
                Save();
            }
            Start();
        }

        private static readonly object PollGate = new object();

        /// <summary>The timer's look: skipped while one is still going on.</summary>
        private static void Tick()
        {
            if (!System.Threading.Monitor.TryEnter(PollGate)) return;
            try { PollNow(); }
            finally { System.Threading.Monitor.Exit(PollGate); }
        }

        /// <summary>One look at the requests now (after one going on - a test calls it).</summary>
        internal static void Poll()
        {
            lock (PollGate) PollNow();
        }

        private static void PollNow()
        {
            try
            {
                if (Count == 0) return;
                var driver = PlcManager.Instance.GetDriverForWrite(Station);
                if (driver == null || !driver.IsConnected) return;
                // both units' request and operation in one read
                var devices = Units.SelectMany(u => new[] { Device(u.Ask), Device(u.Code) }).ToArray();
                int[] v;
                if (!driver.TryReadMulti(devices, out v) || v == null || v.Length < devices.Length) return;
                for (int i = 0; i < Units.Length; i++) PollUnit(driver, Units[i], v[2 * i] != 0, v[2 * i + 1]);
            }
            catch (Exception ex) { AuditLogger.Log("[JOB] the milling steps' requests: " + ex.Message); }
        }

        private static void PollUnit(IPlcDriver driver, Unit u, bool asked, int no)
        {
            string ask = Device(u.Ask), sent = Device(u.Sent), code = Device(u.Code);
            if (!asked)
            {
                // the PLC took the answer away: "sent" off for the next request
                if (u.Answered) using (PlcWriteLog.Source("Jobs: milling unit " + u.No + "'s request ended")) driver.Write(sent, 0);
                u.Answered = false;
                u.Refused = 0;
                return;
            }
            if (u.Answered) return;
            KeyValuePair<string, short[]> op;
            bool have;
            string bar;
            lock (Gate) { have = _ops.TryGetValue(no, out op); bar = _bar; }
            if (!have)
            {
                if (u.Refused != no)
                    AuditLogger.Log(string.Format(CI, "[JOB] milling unit {0} asked ({1}) for operation {2} ({3}) - the bar sent last ({4}) has no such operation: not answered",
                                                  u.No, ask, no, code, bar));
                u.Refused = no;
                return;
            }
            string area = Device(u.Area);
            var block = driver as IPlcBlockAccess;
            int rc;
            using (PlcWriteLog.Source(string.Format(CI, "Jobs: milling unit {0} asked for operation {1} ({2})", u.No, no, op.Key)))
            {
                rc = block != null ? block.WriteBlock(area, op.Value) : PlcBlocks.WriteByDevice(driver, area, op.Value);
                if (rc == 0) rc = driver.Write(sent, 1);
            }
            if (rc != 0)
            {
                AuditLogger.Log(string.Format(CI, "[JOB] milling unit {0}: operation {1}'s steps could not be written to {2} (code {3}) - asked again", u.No, no, area, rc));
                return;
            }
            u.Answered = true;
            int steps = 0;
            while ((steps + 1) * PlcSteps.WordsPerStep <= op.Value.Length && op.Value[steps * PlcSteps.WordsPerStep] != 0) steps++;
            AuditLogger.Log(string.Format(CI, "[JOB] milling unit {0} asked for operation {1} ({2} of {3}): {4} step(s) written to {5}, {6} on",
                                          u.No, no, op.Key, bar, steps, area, sent));
        }

        // ---- kept over a restart ---------------------------------------------------------------------------

        private static void Save()
        {
            try
            {
                var lines = new List<string> { "bar;" + _bar.Replace(";", ",") };
                foreach (var kv in _ops.OrderBy(k => k.Key))
                    lines.Add("op;" + kv.Key.ToString(CI) + ";" + kv.Value.Key.Replace(";", ",") + ";" + string.Join(",", kv.Value.Value.Select(x => x.ToString(CI))));
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
                File.WriteAllLines(StatePath, lines);
            }
            catch (Exception ex) { AuditLogger.Log("[JOB] " + StatePath + " could not be written: " + ex.Message); }
        }

        private static void Load()
        {
            try
            {
                if (!File.Exists(StatePath)) return;
                var ops = new Dictionary<int, KeyValuePair<string, short[]>>();
                string bar = "";
                foreach (var line in File.ReadAllLines(StatePath))
                {
                    var t = line.Split(';');
                    if (t.Length >= 2 && t[0] == "bar") bar = t[1];
                    int no;
                    if (t.Length >= 4 && t[0] == "op" && int.TryParse(t[1], NumberStyles.Integer, CI, out no))
                    {
                        var words = t[3].Split(',').Select(x => short.Parse(x, NumberStyles.Integer, CI)).ToArray();
                        if (words.Length > 0 && words.Length % PlcSteps.WordsPerStep == 0 && words.Length <= (PlcSteps.MaxSteps + 1) * PlcSteps.WordsPerStep) ops[no] = new KeyValuePair<string, short[]>(t[2], words);
                    }
                }
                _ops = ops;
                _bar = bar;
            }
            catch (Exception ex) { AuditLogger.Log("[JOB] " + StatePath + " could not be read: " + ex.Message); }
        }

        /// <summary>A test's fresh start.</summary>
        internal static void Reset()
        {
            lock (Gate)
            {
                if (_timer != null) { _timer.Dispose(); _timer = null; }
                _ops = new Dictionary<int, KeyValuePair<string, short[]>>();
                _bar = "";
                foreach (var u in Units) { u.Answered = false; u.Refused = 0; }
            }
        }
    }
}
