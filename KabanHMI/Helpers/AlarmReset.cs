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
    /// The one the machine's module DECLARES for its PLC
    /// (PlcManager.SetAlarmResetDevice, next to the program-version device):
    /// M102 on FA2010's PLC1, a different bit on every other machine. There is
    /// no default - a machine that has not declared its bit is not pulsed, and
    /// the menu and the Alarms page say so. It used to be M102 everywhere, and
    /// on a mounted H500 that is MAN_Lubrication.
    ///
    /// THE PULSE - OR SET
    /// ------------------
    /// As the module declares it (PlcManager.AlarmResetAction). Momentary: on,
    /// then off 300 ms later - a press of a physical reset button; the OFF goes
    /// to the same driver the ON went to, whatever happens meanwhile (a logout,
    /// a page change), and is retried: a reset bit left on would hold the
    /// machine in reset. SET: on only - the PLC switches the bit off itself
    /// once every device is reset (FA2010's M102); switching it off from here
    /// could cut that short.
    /// </summary>
    public static class AlarmReset
    {
        public const int PulseMilliseconds = 300;
        private const int ReleaseAttempts = 3;

        /// <summary>
        /// The reset bit of the machine on a station, or null with the reason.
        /// </summary>
        public static string AddressFor(int station, out string why)
        {
            why = "";
            var device = PlcManager.Instance.AlarmResetDevice(station);
            if (!string.IsNullOrWhiteSpace(device)) return device;
            why = LanguageManager.GetString("Msg_ResetNoBit", "no alarm reset bit is set up for this machine yet");
            return null;
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
            if (PlcManager.Instance.AlarmResetAction(station) == BitActionType.SET)
            {
                AuditLogger.Log("alarm reset SET from the alarm menu: " + machine + ", station " + station + ", " + address + " (the PLC switches it off).");
                return true;
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
