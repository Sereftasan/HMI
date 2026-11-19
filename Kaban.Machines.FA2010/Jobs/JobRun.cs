using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kaban.Core.Helpers;
using Kaban.Profiles.Jobs;

namespace Kaban.Machines.FA2010.Jobs
{
    /// <summary>
    /// THE JOB RUN (19 November - the old FA2010's job screen, _ZC2010 ucJobPreparations: StartJob, AsyncReadPLC, Check_NextJob): the bars
    /// of a job one after the other without the operator. The PLC says it has finished a bar and wants the next one with "Is Gonder" L0
    /// (while "Makine Aktif" M21 is on); the HMI then writes the next bar of the run (JobSender: its data, L10 another bar after it or not,
    /// L0 off, L1 "Is Geldi" on), and the bar finished goes into the production log with its working time (D91, seconds).
    /// A bar sent by hand is watched the same way (its finish logged), with nothing after it.
    /// A bar that cannot go (problems, an unconfirmed milling position) ends the run there - said on the Jobs page.
    /// </summary>
    internal static class JobRun
    {
        internal const string ActiveDevice = "M21", BarTimeDevice = "D91";
        internal const int PollMs = 250;

        private static readonly object Gate = new object(), PollGate = new object();
        private static System.Threading.Timer _timer;
        private static KbnJob _job;
        private static List<BarPlan> _plans = new List<BarPlan>();
        private static readonly List<int> _queue = new List<int>();
        private static int _current = -1, _station = 1;
        private static bool _on, _trial, _armed;
        private static DateTime _sentAt;
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        /// <summary>A bar sent by the run (its index), a bar finished (its index, its seconds), something to say (any thread).</summary>
        internal static event Action<int> BarSent;
        internal static event Action<int, double> BarFinished;
        internal static event Action<string, bool> Said;

        internal static bool On { get { lock (Gate) return _on; } }
        internal static int Current { get { lock (Gate) return _current; } }
        internal static string JobPath { get { lock (Gate) return _job != null ? _job.Path : ""; } }
        internal static List<int> Queue { get { lock (Gate) return _queue.ToList(); } }

        /// <summary>
        /// A run from bar <paramref name="first"/>: it is sent now, the bars after it that are not sent yet (<paramref name="skip"/>) and can go
        /// follow as the machine asks. Returns what to tell the operator ("" sent).
        /// </summary>
        internal static string Start(KbnJob job, List<BarPlan> plans, int first, ICollection<int> skip, int station, bool trial)
        {
            if (job == null || plans == null || first < 0 || first >= plans.Count) return "no bar";
            var queue = new List<int>();
            string stopNote = "";
            for (int i = first + 1; i < plans.Count; i++)
            {
                if (skip != null && skip.Contains(i)) continue;
                var p = plans[i];
                if (!p.Ok || (p.NeedsTrial && !trial))
                {
                    stopNote = string.Format(CI, "the run stops before bar {0} ({1})", p.Bar.Title, !p.Ok ? "its problems" : "unconfirmed milling positions");
                    break;
                }
                queue.Add(i);
            }
            lock (Gate)
            {
                _job = job; _plans = plans.ToList(); _station = station; _trial = trial;
                _queue.Clear(); _queue.AddRange(queue);
                _on = true;
            }
            AuditLogger.Log(string.Format(CI, "[JOB] automatic run of {0} from bar {1}: {2} more bar(s){3}", job.Name, plans[first].Bar.Title, queue.Count,
                                          stopNote.Length > 0 ? " - " + stopNote : ""));
            ProductionLog.Add(new ProductionEntry { Machine = "FA2010", Kind = ProductionKind.RunStarted, Job = job.Name, Bar = plans[first].Bar.Title,
                                                    Pieces = queue.Count + 1, User = SecurityManager.CurrentUser, Automatic = true, Note = stopNote });
            var why = SendNow(first, queue.Count > 0, true);
            if (why.Length > 0) { Stop("the first bar was not sent"); return why; }
            if (stopNote.Length > 0) Say(stopNote, false);
            return "";
        }

        /// <summary>A bar sent by hand (the Jobs page): watched for its finish; any run ends (nothing follows it).</summary>
        internal static void SentByHand(KbnJob job, List<BarPlan> plans, int index, int station)
        {
            bool wasOn;
            lock (Gate)
            {
                wasOn = _on;
                _job = job; _plans = plans != null ? plans.ToList() : new List<BarPlan>(); _station = station;
                _queue.Clear();
                _on = false;
                _current = index;
                _sentAt = DateTime.Now;
                _armed = false;
            }
            if (wasOn) AuditLogger.Log("[JOB] the automatic run ended: a bar was sent by hand.");
            EnsureTimer();
        }

        /// <summary>The run ended by the operator: the bar in the machine is its last (L10 off).</summary>
        internal static void Stop(string why)
        {
            bool was;
            lock (Gate) { was = _on; _on = false; _queue.Clear(); }
            if (!was) return;
            try
            {
                var driver = PlcManager.Instance.GetDriverForWrite(_station);
                if (driver != null && driver.IsConnected)
                    using (PlcWriteLog.Source("Jobs: the automatic run stopped - the bar in the machine is the last"))
                        driver.Write(JobSender.AddressOf(_station, "Son_Bar_Degil", JobSender.NotLastDevice), 0);
            }
            catch { }
            AuditLogger.Log("[JOB] the automatic run stopped: " + why);
            ProductionLog.Add(new ProductionEntry { Machine = "FA2010", Kind = ProductionKind.RunEnded, Job = _job != null ? _job.Name : "", User = SecurityManager.CurrentUser, Note = why });
            Say(why, false);
        }

        /// <summary>Everything forgotten (tests).</summary>
        internal static void Reset()
        {
            lock (Gate) { _job = null; _plans = new List<BarPlan>(); _queue.Clear(); _current = -1; _on = false; _armed = false; }
        }

        private static string SendNow(int index, bool more, bool automatic)
        {
            BarPlan plan;
            KbnJob job;
            int station;
            bool trial;
            lock (Gate) { plan = _plans[index]; job = _job; station = _station; trial = _trial && plan.NeedsTrial; }
            var r = JobSender.Send(plan, station, job.Name, trial, job, more, automatic);
            if (!r.Ok)
            {
                Say(string.Format(CI, "bar {0} was not sent: {1}", plan.Bar.Title, r.Message), true);
                return r.Message.Length > 0 ? r.Message : "not sent";
            }
            lock (Gate) { _current = index; _sentAt = DateTime.Now; _armed = false; }
            var h = BarSent;
            if (h != null) try { h(index); } catch { }
            EnsureTimer();
            return "";
        }

        private static void Say(string text, bool bad)
        {
            var h = Said;
            if (h != null) try { h(text, bad); } catch { }
        }

        private static void EnsureTimer()
        {
            lock (Gate)
            {
                if (_timer == null) _timer = new System.Threading.Timer(_ => Tick(), null, 500, PollMs);
            }
        }

        private static void Tick()
        {
            if (!System.Threading.Monitor.TryEnter(PollGate)) return;
            try { PollNow(); }
            catch (Exception ex) { AuditLogger.Log("[JOB] the automatic run: " + ex.Message); }
            finally { System.Threading.Monitor.Exit(PollGate); }
        }

        /// <summary>One look at the PLC now (a test calls it).</summary>
        internal static void Poll() { lock (PollGate) PollNow(); }

        private static void PollNow()
        {
            int current, station;
            lock (Gate) { current = _current; station = _station; }
            if (current < 0) return;
            var driver = PlcManager.Instance.GetDriverForWrite(station);
            if (driver == null || !driver.IsConnected) return;
            var devices = new[]
            {
                JobSender.AddressOf(station, "Is_Gonder", JobSender.AskDevice), JobSender.AddressOf(station, "Makine_Aktif", ActiveDevice),
                JobSender.AddressOf(station, "PC_CalismaSuresi_Bar", BarTimeDevice)
            };
            var read = new[] { devices[0], devices[1], devices[2], PlcAddressHelper.OffsetAddress(devices[2], 1) };
            int[] v;
            if (!driver.TryReadMulti(read, out v) || v == null || v.Length < read.Length) return;
            bool ask = v[0] != 0, active = v[1] != 0;
            double seconds = (uint)((v[2] & 0xFFFF) | ((v[3] & 0xFFFF) << 16));
            if (!ask) { lock (Gate) _armed = true; return; }
            // the old Check_NextJob: only while the machine is active, once a request
            if (!active) return;
            int next = -1;
            bool more = false, run;
            lock (Gate)
            {
                if (!_armed) return;
                _armed = false;
                run = _on;
                if (_on && _queue.Count > 0) { next = _queue[0]; _queue.RemoveAt(0); more = _queue.Count > 0; }
            }
            Finished(current, seconds);
            if (next >= 0)
            {
                if (SendNow(next, more, true).Length > 0) Stop("a bar was not sent");
                else if (!more) Say("the run's last bar sent", false);
                return;
            }
            lock (Gate) { _current = -1; }
            if (run) { lock (Gate) _on = false; Say("the run's last bar finished", false); ProductionLog.Add(new ProductionEntry { Machine = "FA2010", Kind = ProductionKind.RunEnded, Job = _job != null ? _job.Name : "", Note = "the last bar finished" }); }
        }

        private static void Finished(int index, double seconds)
        {
            BarPlan plan; KbnJob job; DateTime sent;
            lock (Gate)
            {
                if (index < 0 || index >= _plans.Count) return;
                plan = _plans[index]; job = _job; sent = _sentAt;
            }
            // the PLC's time of the bar (D91); none: the time since it was sent
            if (seconds <= 0) seconds = Math.Round((DateTime.Now - sent).TotalSeconds);
            AuditLogger.Log(string.Format(CI, "[JOB] bar {0} of {1} finished ({2:0} s)", plan.Bar.Title, job != null ? job.Name : "", seconds));
            ProductionLog.Add(new ProductionEntry
            {
                Machine = "FA2010", Kind = ProductionKind.BarFinished, Job = job != null ? job.Name : "", Bar = plan.Bar.Title, Profile = plan.Bar.Code,
                LengthMm = plan.Bar.Length, Pieces = plan.Bar.Pieces.Count, Seconds = seconds, User = SecurityManager.CurrentUser,
                Customer = string.Join(", ", plan.Bar.Pieces.Select(p => p.Customer).Where(c => c.Length > 0).Distinct()),
                Order = string.Join(", ", plan.Bar.Pieces.Select(p => p.OrderNo).Where(c => c.Length > 0).Distinct())
            });
            var h = BarFinished;
            if (h != null) try { h(index, seconds); } catch { }
        }
    }
}
