using Kaban.Core.Machines;

namespace Kaban.Machines.FA2010
{
    /// <summary>
    /// DESIGN-TIME condition setup. Kept only so the Visual Studio Designer can
    /// populate condition dropdowns in the property grid (HomeScreen calls this
    /// exclusively under DesignMode - never at runtime).
    ///
    /// It now DELEGATES to FA2010Module through the registry, so the designer sees
    /// exactly the conditions/bits that actually run. Previously this file kept its
    /// own hardcoded copy (1L &lt;&lt; 16, ...) which could silently drift from the
    /// module's allocated bits.
    /// </summary>
    public class MachineConfig
    {
        private static bool _done;

        public static void Initialize()
        {
            if (_done) return;   // designer may construct several screens
            _done = true;

            // Reset + mount the real module so the design-time condition map matches
            // runtime. Safe at design time; the runtime path in Program.cs does its
            // own Reset()+Mount() before the shell starts.
            MachineRegistry.Reset();
            MachineRegistry.MountMain(new FA2010Module());
        }
    }
}

