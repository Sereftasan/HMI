using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Dashboard;
using Kaban.UI.Forms;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// Asks why a machine stood still (StopReasons, 5 November) over the main window: a window a machine (StopReasonForm), opened
    /// when its stop has lasted the limit (looked at every 2 seconds) or has ended unanswered, closed when it is answered, put off
    /// ("Later") or not to be recorded after all.
    /// </summary>
    public static class StopReasonHost
    {
        private static Form _owner;
        private static Timer _clock;
        private static readonly Dictionary<string, StopReasonForm> _open = new Dictionary<string, StopReasonForm>(StringComparer.OrdinalIgnoreCase);

        /// <summary>A machine's window on show (tests).</summary>
        public static StopReasonForm OpenFor(string moduleId)
        {
            StopReasonForm f;
            return _open.TryGetValue(moduleId ?? "", out f) && !f.IsDisposed ? f : null;
        }

        /// <summary>Asks for the main window (once).</summary>
        public static void Attach(Form owner)
        {
            if (owner == null) return;
            bool first = _owner == null;
            _owner = owner;
            owner.FormClosed += (s, e) => { if (_owner == owner) { CloseAll(); _owner = null; if (_clock != null) _clock.Stop(); } };
            if (!first) return;
            StopReasons.Asking += s => OnUi(() => Show(s));
            StopReasons.Closed += s => OnUi(() => Close(s.ModuleId));
            _clock = new Timer { Interval = 2000 };
            _clock.Tick += (s, e) => { try { StopReasons.Check(); } catch (Exception ex) { AuditLogger.Log("stop reasons: " + ex.Message); } };
            _clock.Start();
        }

        private static void OnUi(Action a)
        {
            var owner = _owner;
            if (owner == null || owner.IsDisposed) return;
            if (owner.InvokeRequired) { try { owner.BeginInvoke(a); } catch { } }
            else a();
        }

        /// <summary>A machine's window shown (or brought up to date).</summary>
        public static void Show(StopReasons.Stop stop)
        {
            if (stop == null || _owner == null || _owner.IsDisposed) return;
            var open = OpenFor(stop.ModuleId);
            if (open != null) { open.ShowStop(stop); return; }
            var form = new StopReasonForm(stop);
            form.Answered += r => StopReasons.Answer(form.Stop, r);
            form.LaterAsked += () => StopReasons.Later(form.Stop);
            _open[stop.ModuleId ?? ""] = form;
            form.Show(_owner);
            form.PlaceOver(_owner);
        }

        public static void Close(string moduleId)
        {
            StopReasonForm f;
            if (!_open.TryGetValue(moduleId ?? "", out f)) return;
            _open.Remove(moduleId ?? "");
            try { if (!f.IsDisposed) f.Close(); } catch { }
        }

        private static void CloseAll()
        {
            foreach (var f in new List<StopReasonForm>(_open.Values)) try { if (!f.IsDisposed) f.Close(); } catch { }
            _open.Clear();
        }
    }
}
