using System;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Forms;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// A SIMPLE MOTION MODULE'S PARAMETER CHANGED BY AN ADMIN, AND KEPT (22 October: "some
    /// parameters need EPROM write as well"). From the module's manual: what is written into its
    /// buffer memory is used at the axis's next start (Pr.8 - Pr.10) or when the PLC READY signal
    /// turns on again (Pr.1 - Pr.7, Pr.11 - Pr.24, Pr.43 - Pr.57 ...), and is lost at the next
    /// power off unless the module's flash ROM is written - which it does only while PLC READY is
    /// off, at most 25 times per power on. So after each parameter written the Admin is told when
    /// it takes effect and offered the flash ROM; the parameter cells' menu offers it too.
    /// </summary>
    public static class MotionFlash
    {
        /// <summary>For the tests: answers a question or a message (its text, its buttons) instead of a dialog.</summary>
        internal static Func<string, MessageBoxButtons, DialogResult> Ask;

        /// <summary>The last question or message (the tests read it).</summary>
        public static string LastMessage = "";

        private static string T(string key, string en) { return LanguageManager.GetString(key, en); }

        private static DialogResult Say(IWin32Window owner, string text, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            LastMessage = text ?? "";
            if (Ask != null) return Ask(text, buttons);
            return MessageBox.Show(owner, text, T("Ttl_MotionFlash", "Simple motion module"), buttons, icon);
        }

        /// <summary>When a parameter takes effect: at the next start, or when PLC READY turns on again.</summary>
        public static string WhenUsed(SimpleMotion.Item item)
        {
            return item != null && item.AtPlcReady
                ? T("Msg_MotionAtPlcReady", "The module takes it when its PLC READY signal (Cd.190) turns on again - the next time the PLC starts its program.")
                : T("Msg_MotionAtStart", "The module uses it from the axis's next start.");
        }

        /// <summary>
        /// After an Admin wrote <paramref name="item"/> of an axis: when it takes effect, that it is
        /// lost at power off unless the flash ROM is written - and the flash ROM offered.
        /// </summary>
        public static void AfterParameterWritten(IWin32Window owner, int station, int module, int axis, SimpleMotion.Item item, string shown)
        {
            string text = string.Format(T("Msg_MotionWritten", "{0} ({1}) of axis {2} (module {3}): {4} written."),
                                        T(item.TitleKey, item.Title), item.Ref, axis, module, shown) +
                          "\r\n\r\n" + WhenUsed(item) +
                          "\r\n" + T("Msg_MotionKeptUntilOff", "The module keeps it until it is switched off - after that only what its flash ROM holds.") +
                          "\r\n\r\n" + T("Msg_MotionFlashNow", "Write the module's flash ROM now?");
            if (Say(owner, text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                Write(owner, station, module);
        }

        /// <summary>
        /// Writes module <paramref name="module"/>'s flash ROM on <paramref name="station"/>, with a
        /// window while the module writes (up to 5 s), and says how it went.
        /// </summary>
        public static SimpleMotion.FlashResult Write(IWin32Window owner, int station, int module)
        {
            var driver = PlcManager.Instance.GetDriverForWrite(station);
            if (driver == null)
            {
                Say(owner, T("Msg_MotionFlashNoPlc", "The PLC is not connected (or your user level may not write to it): the flash ROM was not written."),
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return SimpleMotion.FlashResult.NotConnected;
            }
            int writes = -1;
            var result = WorkForm.Run(owner, T("Ttl_MotionFlash", "Simple motion module"),
                T("Msg_MotionFlashWriting", "Writing the module's flash ROM (up to 5 seconds) - do not switch the machine off."),
                p =>
                {
                    using (WriteSource.For(string.Format("Simple motion module {0}: flash ROM write request (Cd.1)", module)))
                        return SimpleMotion.WriteFlashRom(driver, module, out writes);
                });
            string text;
            switch (result)
            {
                case SimpleMotion.FlashResult.Written:
                    text = string.Format(T("Msg_MotionFlashDone", "Module {0}'s flash ROM written ({1} of {2} writes allowed since the power was switched on)."),
                                         module, writes < 0 ? "?" : writes.ToString(), SimpleMotion.MaxFlashWritesPerPowerOn);
                    break;
                case SimpleMotion.FlashResult.PlcReadyOn:
                    text = string.Format(T("Msg_MotionFlashReadyOn",
                        "Not written: the module writes its flash ROM only while its PLC READY signal (Cd.190, {0}) is off - and it is on.\r\n" +
                        "Turn it off (the PLC in STOP, or its program's PLC READY off - the axes stop), then write the flash ROM again from a parameter's menu."),
                        SimpleMotion.Device(module, SimpleMotion.PlcReadyAddress));
                    break;
                case SimpleMotion.FlashResult.TooManyWrites:
                    text = string.Format(T("Msg_MotionFlashTooMany",
                        "Not written: the flash ROM has been written {0} times since the power was switched on - the module allows {0}. Switch it off and on first."),
                        SimpleMotion.MaxFlashWritesPerPowerOn);
                    break;
                case SimpleMotion.FlashResult.TimedOut:
                    text = T("Msg_MotionFlashTimedOut", "The module did not report the flash ROM written within 8 seconds - check its error code (Md.23) before switching it off.");
                    break;
                case SimpleMotion.FlashResult.Refused:
                    text = T("Msg_MotionFlashRefused", "The PLC refused the flash ROM write request.");
                    break;
                default:
                    text = T("Msg_MotionFlashNoPlc", "The PLC is not connected (or your user level may not write to it): the flash ROM was not written.");
                    break;
            }
            AuditLogger.Log(string.Format("simple motion module {0} (station {1}): flash ROM write - {2}{3}", module, station, result,
                                          writes >= 0 ? " (" + writes + " since power on)" : ""));
            Say(owner, text, MessageBoxButtons.OK, result == SimpleMotion.FlashResult.Written ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            return result;
        }
    }
}
