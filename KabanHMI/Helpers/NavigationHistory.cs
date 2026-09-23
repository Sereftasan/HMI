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
        private static int _index = -1;
        private static bool _replaying;

        public static event EventHandler Changed;

        public static bool CanBack => _index > 0;
        public static bool CanForward => _index >= 0 && _index < _entries.Count - 1;

        /// <summary>Record a visit. Ignored while Back/Forward is replaying.</summary>
        public static void Record(Action restore)
        {
            if (_replaying || restore == null) return;

            // moving somewhere new discards the forward branch, like a browser
            if (_index < _entries.Count - 1)
                _entries.RemoveRange(_index + 1, _entries.Count - _index - 1);

            _entries.Add(restore);
            _index = _entries.Count - 1;
            Changed?.Invoke(null, EventArgs.Empty);
        }

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
            _index = -1;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}

