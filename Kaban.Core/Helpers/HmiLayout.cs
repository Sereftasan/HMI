using System;
using System.IO;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// The usable page height, used to decide how many controls fit in a column
    /// before the next one starts.
    ///
    /// It is DETECTED from the primary screen's working area at startup - the
    /// generator cannot know the panel a machine will run on - minus the caption,
    /// the tab strips and the status line. An operator can override it in HMI
    /// Settings for an unusual panel, and the value is remembered.
    /// </summary>
    public static class HmiLayout
    {
        /// <summary>Space taken by the caption bar, tab strips and status line.</summary>
        public const int Chrome = 150;

        private static int _pageHeight;

        public static int PageHeight
        {
            get
            {
                if (_pageHeight <= 0) _pageHeight = 700;   // sane default until measured
                return _pageHeight;
            }
            set
            {
                _pageHeight = value > 0 ? value : 0;
                _explicitlySet = value > 0;
                Save();
            }
        }

        /// <summary>
        /// Called by the shell with the usable height of the screen; ignored when the
        /// operator has set an explicit value.
        /// </summary>
        public static void DetectFrom(int workingAreaHeight)
        {
            if (_explicitlySet) return;
            _pageHeight = Math.Max(300, workingAreaHeight - Chrome);
        }

        private static bool _explicitlySet;

        /// <summary>How many rows of the given pitch fit in one column.</summary>
        public static int RowsPerColumn(int rowPitch)
        {
            if (rowPitch <= 0) return 1;
            return Math.Max(1, PageHeight / rowPitch);
        }

        private static string SettingsPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "KabanHMI", "Layout.cfg");
            }
        }

        public static void Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                int v;
                if (int.TryParse(File.ReadAllText(SettingsPath).Trim(), out v) && v > 200)
                {
                    _pageHeight = v;
                    _explicitlySet = true;
                }
            }
            catch { }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath, _pageHeight.ToString());
            }
            catch { }
        }
    }
}

