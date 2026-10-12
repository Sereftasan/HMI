using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Helpers;
using Kaban.Core.Models;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// A SOUND FOR A NEW ALARM, when View Preferences ask for one (Sign-in and
    /// alerts > Alarm sound: On). Once per alarm that comes onto the list - not
    /// again while it stays, not for one that goes - so a page full of standing
    /// alarms does not keep ringing. Off by default: the panel was silent.
    /// </summary>
    public static class AlarmSound
    {
        private static readonly HashSet<AlarmItem> _known = new HashSet<AlarmItem>();
        private static bool _started;

        /// <summary>What sounds (replaceable for the tests: the box has no speaker).</summary>
        public static Action Play = () => System.Media.SystemSounds.Exclamation.Play();

        /// <summary>How many times it sounded (for the tests).</summary>
        public static int Played { get; private set; }

        /// <summary>Listens to the alarm list; the alarms already on it make no sound.</summary>
        public static void Start()
        {
            lock (_known)
            {
                if (_started) return;
                _started = true;
                foreach (var a in AlarmManager.ActiveAlarms.ToList()) _known.Add(a);
            }
            AlarmManager.AlarmsChanged += OnChanged;
        }

        private static void OnChanged(object sender, EventArgs e)
        {
            List<AlarmItem> now;
            try { now = AlarmManager.ActiveAlarms.ToList(); }
            catch { return; }              // the list changing under us: the next change tells
            bool fresh;
            lock (_known)
            {
                fresh = now.Any(a => !_known.Contains(a));
                _known.Clear();
                foreach (var a in now) _known.Add(a);
            }
            if (!fresh || !ViewPreferences.AlarmSoundOn) return;
            Played++;
            try { Play(); } catch { /* no sound device */ }
        }
    }
}
