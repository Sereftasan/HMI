using System;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// The machine's ALARM RESET, pulsed from the caption bar's alarm menu.
    ///
    /// WHY THE MENU PULSES IT
    /// ----------------------
    /// "Reset (4) Active Alarms" used to clear the list on screen only. An alarm
    /// whose cause was still latched in the PLC came straight back, and the
    /// operator had to find the Reset button on the Alarms page to do what the
    /// menu entry already said it did. It now does both: pulses the reset bit,
    /// then clears the list.
    ///
    /// WHICH BIT
    /// ---------
    /// The bit is the machine's own "PC_AlarmReset" label (FA2010 and H800:
    /// M102). A machine without that label uses M102 - what its Alarms page
    /// button pulses - UNLESS its label list gives M102 another job: on H500,
    /// M102 is MAN_Lubrication, and an alarm reset must not start the
    /// lubrication. Such a machine is not pulsed at all, and the menu says why.
    ///
    /// THE PULSE
    /// ---------
    /// On, then off 300 ms later - a press of a physical reset button. The OFF
    /// goes to the same driver the ON went to, whatever happens meanwhile (a
    /// logout, a page change), and is retried: a reset bit left on would hold
    /// the machine in reset.
    /// </summary>
    public static class AlarmReset
    {
        public const string ResetLabel = "PC_AlarmReset";
        public const int PulseMilliseconds = 300;
        private const int ReleaseAttempts = 3;

        /// <summary>
        /// The reset bit of the machine on a station, or null with the reason.
        /// </summary>
        public static string AddressFor(int station, out string why)
        {
            why = "";
            var own = PlcLabelTable.ByLabel(station, ResetLabel);
            if (own != null && !string.IsNullOrWhiteSpace(own.Address)) return own.Address.Trim();

            string fallback = Kaban.UI.Forms.MainForm.AlarmResetAddress;
            var other = PlcLabelTable.ByAddress(station, fallback);
            if (other != null && other.Label.Length > 0 &&
                other.Label.IndexOf("Reset", StringComparison.OrdinalIgnoreCase) < 0)
            {
                why = string.Format(LanguageManager.GetString("Msg_ResetNoBit",
                          "{0} is {1} on this machine, not an alarm reset - add a PC_AlarmReset label to its PLC label list"),
                          fallback, other.Label);
                return null;
            }
            return fallback;
        }

        /// <summary>
        /// Pulses the reset of the machine on a station. False, with the reason,
        /// when nothing was sent.
        /// </summary>
        public static bool Pulse(int station, string machine, out string why)
        {
            why = "";
            if (!SecurityManager.HasAccess(SecurityLevel.Operator))
            {
                why = LanguageManager.GetString("Msg_ResetNeedsLogin", "sign in as Operator or higher to reset the machine");
                return false;
            }

            string address = AddressFor(station, out why);
            if (address == null) return false;

            IPlcDriver driver = PlcManager.Instance.GetDriverForWrite(station);
            if (driver == null)
            {
                why = PlcManager.Instance.DescribeAccess(station);
                return false;
            }

            int result;
            try { result = driver.Write(address, 1); }
            catch (Exception ex) { result = -1; why = ex.Message; }
            if (result != 0)
            {
                if (why.Length == 0)
                    why = LanguageManager.GetString("Msg_ResetRefused", "the controller did not accept the write");
                AuditLogger.Log("alarm reset NOT sent to " + machine + " (" + address + "): " + why);
                return false;
            }
            AuditLogger.Log("alarm reset pulsed from the alarm menu: " + machine + ", station " + station + ", " + address + ".");
            Release(driver, address, machine, ReleaseAttempts);
            return true;
        }

        private static void Release(IPlcDriver driver, string address, string machine, int attemptsLeft)
        {
            var t = new Timer { Interval = PulseMilliseconds };
            t.Tick += (s, e) =>
            {
                t.Stop();
                t.Dispose();
                bool ok;
                try { ok = driver.Write(address, 0) == 0; }
                catch { ok = false; }
                if (ok) return;
                if (attemptsLeft > 1) { Release(driver, address, machine, attemptsLeft - 1); return; }
                AuditLogger.Log("ALARM RESET BIT LEFT ON: " + address + " on " + machine +
                                " could not be switched off after " + ReleaseAttempts + " attempts.");
            };
            t.Start();
        }
    }
}
