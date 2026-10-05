using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Enums;

namespace Kaban.Core.Helpers
{
    public class UnitDefinition
    {
        public string Name { get; set; }
        public double Factor { get; set; } = 1.0;
        public double Offset { get; set; } = 0.0;
        public int ExtraDecimals { get; set; }
        public override string ToString() => Name; // Critical for ComboBox display
    }

    public static class UnitManager
    {
        public static event EventHandler UnitChanged;

        // Stores the currently active definition
        private static Dictionary<UnitType, UnitDefinition> _activeUnits = new Dictionary<UnitType, UnitDefinition>();

        // Stores ALL possible options (for the ComboBox)
        private static Dictionary<UnitType, List<UnitDefinition>> _availableUnits = new Dictionary<UnitType, List<UnitDefinition>>();

        // Stores the "Base" suffix (e.g. "mm") to know what to swap out
        public static Dictionary<UnitType, string> ReferenceSuffixes = new Dictionary<UnitType, string>()
        {
            { UnitType.Length, "mm" },
            { UnitType.Weight, "gr" },
            { UnitType.Temperature, "C" },
            { UnitType.Pressure, "bar" },
            { UnitType.Time, "sec" }
        };

        static UnitManager()
        {
            // 1. Initialize Active Defaults
            foreach (UnitType t in Enum.GetValues(typeof(UnitType)))
            {
                _activeUnits[t] = new UnitDefinition { Name = "", Factor = 1.0 };
                _availableUnits[t] = new List<UnitDefinition>();
            }

            // 2. Register Standard Units 
            // Format: RegisterUnit(Type, Name, Factor, Offset, Decimals)

            // LENGTH
            RegisterUnit(UnitType.Length, "mm", 1.0, 0.0, 0);
            RegisterUnit(UnitType.Length, "cm", 10.0, 0.0, 1);
            RegisterUnit(UnitType.Length, "m", 1000.0, 0.0, 3);
            RegisterUnit(UnitType.Length, "inch", 25.4, 0.0, 2);
            RegisterUnit(UnitType.Length, "ft", 304.8, 0.0, 2);

            // WEIGHT
            RegisterUnit(UnitType.Weight, "gr", 1.0, 0.0, 0);
            RegisterUnit(UnitType.Weight, "kg", 1000.0, 0.0, 2);
            RegisterUnit(UnitType.Weight, "lbs", 453.592, 0.0, 2);

            // TEMPERATURE
            RegisterUnit(UnitType.Temperature, "C", 1.0, 0.0, 0);
            // C = F*(5/9) - 17.7778  ->  F = (C + 17.7778) / (5/9)
            // (was registered as 1.8/32.0, which is the INVERSE direction and
            //  disagreed with every other unit's convention)
            RegisterUnit(UnitType.Temperature, "F", 5.0 / 9.0, -160.0 / 9.0, 0);

            // --- PRESSURE (Add these to fix Pressure showing up) ---
            RegisterUnit(UnitType.Pressure, "bar", 1.0, 0.0, 1);
            RegisterUnit(UnitType.Pressure, "psi", 0.0689, 0.0, 1);

            // --- TIME (Add these to fix Time showing up) ---
            RegisterUnit(UnitType.Time, "sec", 1.0, 0.0, 1);
            RegisterUnit(UnitType.Time, "min", 60.0, 0.0, 2);
        }

        public static void RegisterUnit(UnitType type, string name, double factor, double offset, int decimals)
        {
            _availableUnits[type].Add(new UnitDefinition { Name = name, Factor = factor, Offset = offset, ExtraDecimals = decimals });
        }

        // Used by ComboBox to list options
        public static List<UnitDefinition> GetAvailable(UnitType type)
        {
            if (_availableUnits.ContainsKey(type)) return _availableUnits[type];
            return new List<UnitDefinition>();
        }

        // Used by Controls to do math
        public static UnitDefinition GetActive(UnitType type)
        {
            EnsureLoaded();
            UnitDefinition active;
            if (_activeUnits.TryGetValue(type, out active) && active != null &&
                !string.IsNullOrEmpty(active.Name))
                return active;

            // NEVER hand back a nameless placeholder: an unnamed unit has no suffix
            // and cannot be matched in the settings combo, which is what made a page
            // look blank until the operator picked a unit by hand. Fall back to the
            // FIRST unit defined for the family and remember it, so the whole HMI
            // agrees on the default from the first read.
            List<UnitDefinition> options;
            if (_availableUnits.TryGetValue(type, out options) && options != null && options.Count > 0)
            {
                _activeUnits[type] = options[0];
                return options[0];
            }
            return new UnitDefinition { Name = "", Factor = 1.0 };
        }

        /// <summary>
        /// Gives every unit family a real active unit. Called at startup so nothing
        /// depends on a settings page having been opened first.
        /// </summary>
        public static void EnsureDefaults()
        {
            EnsureLoaded();
            foreach (var kv in _availableUnits)
            {
                if (kv.Value == null || kv.Value.Count == 0) continue;
                UnitDefinition active;
                if (!_activeUnits.TryGetValue(kv.Key, out active) || active == null ||
                    string.IsNullOrEmpty(active.Name))
                    _activeUnits[kv.Key] = kv.Value[0];
            }
        }

        /// <summary>
        /// THE PANEL'S UNIT for a family (HMI Settings > Users and Preferences >
        /// Everyone): kept in Units.cfg, and in force unless the signed-in user has
        /// a unit of their own (View Preferences "Unit_Length" ...), taken at sign-in.
        /// </summary>
        public static void SetActive(UnitType type, string name)
        {
            if (!_availableUnits.ContainsKey(type)) return;

            EnsureLoaded();
            var unit = _availableUnits[type].FirstOrDefault(u => u.Name == name);
            if (unit == null) return;
            _panelUnits[type] = unit.Name;
            if (_loadingUnits) return;
            PersistUnits();
            ApplyInForce(always: true);
        }

        // The panel's units (Units.cfg) - apart from the ones in force, which may be a user's.
        private static readonly Dictionary<UnitType, string> _panelUnits = new Dictionary<UnitType, string>();

        /// <summary>The View Preferences key of a user's own unit: "Unit_Length".</summary>
        public static string UnitKey(UnitType type) { return "Unit_" + type; }

        /// <summary>The families that have units to choose from (Length, Weight ...).</summary>
        public static IList<UnitType> Families
        {
            get { return _availableUnits.Where(kv => kv.Key != UnitType.None && kv.Value != null && kv.Value.Count > 0).Select(kv => kv.Key).ToList(); }
        }

        /// <summary>The panel's unit of a family (everyone's), by name.</summary>
        public static string PanelUnitName(UnitType type)
        {
            EnsureLoaded();
            string n;
            if (_panelUnits.TryGetValue(type, out n) && !string.IsNullOrEmpty(n)) return n;
            List<UnitDefinition> options;
            return _availableUnits.TryGetValue(type, out options) && options.Count > 0 ? options[0].Name : "";
        }

        /// <summary>A unit of a family by name, or null.</summary>
        public static UnitDefinition Find(UnitType type, string name)
        {
            List<UnitDefinition> options;
            return name != null && _availableUnits.TryGetValue(type, out options) ? options.FirstOrDefault(u => u.Name == name) : null;
        }

        /// <summary>The signed-in user's own unit of a family, or null.</summary>
        public static string UserUnitName(UnitType type)
        {
            var user = ViewPreferences.ActiveUser;
            if (user == null) return null;
            var own = ViewPreferences.UserValue(user, UnitKey(type));
            return Find(type, own) != null ? own : null;
        }

        private static bool _followsPreferences;

        /// <summary>
        /// The units in force: the signed-in user's own, else the panel's. Told
        /// (UnitChanged) only when one changed - View Preferences change often.
        /// </summary>
        private static void ApplyInForce(bool always = false)
        {
            bool changed = false;
            foreach (var type in Families)
            {
                string want = UserUnitName(type);
                if (want == null)
                {
                    string panel;
                    if (!_panelUnits.TryGetValue(type, out panel)) continue;     // none kept: as it is
                    want = panel;
                }
                var unit = Find(type, want);
                UnitDefinition now;
                if (unit == null || (_activeUnits.TryGetValue(type, out now) && now != null && now.Name == unit.Name)) continue;
                _activeUnits[type] = unit;
                changed = true;
            }
            if (changed || always) UnitChanged?.Invoke(null, EventArgs.Empty);
        }


        // ==================================================================
        // CONVERSION
        // ------------------------------------------------------------------
        // CONVENTION:  reference = display * Factor + Offset
        //         =>   display   = (reference - Offset) / Factor
        //
        // The PLC always stores the REFERENCE unit (mm, gr, C, bar, sec).
        // Controls display in the ACTIVE unit and must convert back before
        // writing - otherwise typing "10" while showing inches would write 10 mm.
        // ==================================================================

        /// <summary>PLC/reference value -> value to show the operator.</summary>
        // ==================================================================
        // PERSISTENCE
        // ------------------------------------------------------------------
        // The chosen units were never saved at all - every start began from the
        // defaults. They are kept beside Users.dat in ProgramData\KabanHMI: both
        // are the operator's settings for this PC rather than part of a machine
        // program, and living outside the program folder they survive an upgrade
        // with no import step.
        // ==================================================================
        private static readonly string UnitStorePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Units.cfg");
        private static bool _unitsLoaded, _loadingUnits;

        private static void EnsureLoaded()
        {
            if (_unitsLoaded) return;
            _unitsLoaded = true;
            try
            {
                if (System.IO.File.Exists(UnitStorePath))
                {
                    _loadingUnits = true;
                    foreach (var line in System.IO.File.ReadAllLines(UnitStorePath))
                    {
                        var p = line.Split(';');
                        UnitType type;
                        if (p.Length == 2 && Enum.TryParse(p[0], out type)) SetActive(type, p[1]);
                    }
                }
            }
            catch { }
            finally { _loadingUnits = false; }
            // A sign-in brings the user's own units, a sign-out the panel's.
            if (!_followsPreferences)
            {
                _followsPreferences = true;
                ViewPreferences.Changed += (s, e) => ApplyInForce();
            }
            ApplyInForce();
        }

        private static void PersistUnits()
        {
            if (_loadingUnits) return;
            try
            {
                // All-or-nothing (SafeFile): a power cut mid-write must not leave
                // the units file empty, which silently reverted every unit choice.
                // The PANEL's units: a user's own (in force while they are signed in)
                // are theirs, kept in View.cfg.
                var lines = _panelUnits
                    .Where(kv => !string.IsNullOrEmpty(kv.Value))
                    .Select(kv => kv.Key + ";" + kv.Value + Environment.NewLine);
                SafeFile.WriteAllText(UnitStorePath, string.Concat(lines), new System.Text.UTF8Encoding(false));
            }
            catch { /* keep running even if the store is unwritable */ }
        }

        public static double ToDisplay(UnitType type, double referenceValue)
        {
            if (type == UnitType.None) return referenceValue;
            var u = GetActive(type);
            if (u == null || u.Factor == 0) return referenceValue;
            return (referenceValue - u.Offset) / u.Factor;
        }

        /// <summary>Value the operator typed -> value to write to the PLC.</summary>
        public static double ToReference(UnitType type, double displayValue)
        {
            if (type == UnitType.None) return displayValue;
            var u = GetActive(type);
            if (u == null) return displayValue;
            return displayValue * u.Factor + u.Offset;
        }

        /// <summary>Suffix to show for the active unit (e.g. "inch"), or "" for None.</summary>
        public static string GetActiveSuffix(UnitType type)
        {
            if (type == UnitType.None) return "";
            var u = GetActive(type);
            return u != null && !string.IsNullOrEmpty(u.Name) ? u.Name : GetReferenceSuffix(type);
        }

        /// <summary>Extra decimals the active unit needs (inch needs more than mm).</summary>
        public static int GetActiveExtraDecimals(UnitType type)
        {
            if (type == UnitType.None) return 0;
            var u = GetActive(type);
            return u != null ? u.ExtraDecimals : 0;
        }

        public static string GetReferenceSuffix(UnitType type)
        {
            return ReferenceSuffixes.ContainsKey(type) ? ReferenceSuffixes[type] : "";
        }
    }
}
