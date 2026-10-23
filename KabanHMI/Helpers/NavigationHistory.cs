using System;
using System.Collections.Generic;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// Browser-style navigation history for the HMI. Both PAGE changes (left nav)
    /// and settings TAB changes record an entry, so Back/Forward step through the
    /// exact places the operator has been.
    ///
    /// Each entry is a restore action; replaying one suppresses recording so the
    /// history is not modified while walking it.
    /// </summary>
    public static class NavigationHistory
    {
        private static readonly List<Action> _entries = new List<Action>();
        // What each entry restores (the page and the tab path), compared by
        // reference. Parallel to _entries; null for an entry recorded without one.
        private static readonly List<object[]> _keys = new List<object[]>();
        private static int _index = -1;
        private static bool _replaying;

        public static event EventHandler Changed;

        public static bool CanBack => _index > 0;
        public static bool CanForward => _index >= 0 && _index < _entries.Count - 1;

        /// <summary>Record a visit. Ignored while Back/Forward is replaying.</summary>
        public static void Record(Action restore)
        {
            Record(restore, null);
        }

        /// <summary>
        /// Record a visit to the place <paramref name="key"/> describes.
        ///
        /// The SAME place twice in a row is one visit. Re-clicking the machine or
        /// tab already on show recorded a second, identical step, so the next
        /// Back "did nothing" - it went to exactly where the operator already was.
        /// </summary>
        public static void Record(Action restore, object[] key)
        {
            if (_replaying || _muted > 0 || restore == null) return;
            if (key != null && _index >= 0 && _index < _keys.Count && SameKey(_keys[_index], key)) return;

            // moving somewhere new discards the forward branch, like a browser
            if (_index < _entries.Count - 1)
            {
                _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);
                _keys.RemoveRange(_index + 1, _keys.Count - _index - 1);
            }

            _entries.Add(restore);
            _keys.Add(key);
            _index = _entries.Count - 1;

            // Bounded, like any history: a panel left running for weeks must not
            // keep every page change since start-up (each step holds its screens).
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveAt(0);
                _keys.RemoveAt(0);
                _index--;
            }
            Changed?.Invoke(null, EventArgs.Empty);
        }

        private const int MaxEntries = 100;

        private static bool SameKey(object[] a, object[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (!ReferenceEquals(a[i], b[i])) return false;
            return true;
        }

        private static int _muted;

        /// <summary>
        /// A LINK'S WAY (23 October: "Previous page button doesn't make the control's original page open when
        /// translation page opened from Notes and Feedback page, the page history should be recorded from any
        /// link opens any page"): while a link opens its page - the page, then a tab, then another tab on it -
        /// nothing is recorded; the shell records the place it led to once, after it (MainForm.AsOneStep). It
        /// recorded every page and tab on the way, and Back went to one of them - HMI Settings on the tab it
        /// was left on - instead of the page the link was followed from.
        /// </summary>
        public static IDisposable Muted()
        {
            _muted++;
            return new Unmute();
        }

        private sealed class Unmute : IDisposable
        {
            private bool _done;
            public void Dispose() { if (_done) return; _done = true; if (_muted > 0) _muted--; }
        }

        /// <summary>True while a Back/Forward step is being replayed.</summary>
        public static bool IsReplaying { get { return _replaying; } }

        public static void Back()
        {
            if (!CanBack) return;
            _index--;
            Replay(_entries[_index]);
        }

        public static void Forward()
        {
            if (!CanForward) return;
            _index++;
            Replay(_entries[_index]);
        }

        private static void Replay(Action a)
        {
            _replaying = true;
            try { a(); }
            finally
            {
                _replaying = false;
                Changed?.Invoke(null, EventArgs.Empty);
            }
        }

        /// <summary>For tests / machine switch: forget everything.</summary>
        public static void Clear()
        {
            _entries.Clear();
            _keys.Clear();
            _index = -1;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}

