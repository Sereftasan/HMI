using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Forms;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// A SIMPLE MOTION MODULE'S PARAMETERS CHANGED, AND KEPT (22 October: "some parameters need
    /// EPROM write as well"; 23 October: "You just need to write 1 to Un\G5900 (n is the simple
    /// motion number, whichever the changed parameter's axis motion number is) for flash ROM write,
    /// that is all. We need to have a button on the axis setting page (visible when there is any
    /// parameter changed that needs flash ROM write), the user clicks it after changing the
    /// parameters (the button should blink as well to warn the user not to forget clicking)").
    ///
    /// A parameter written into a module's buffer memory is used at the axis's next start
    /// (Pr.8 - Pr.10) or when the module's PLC READY signal turns on again (the others) - and is gone
    /// at the next power off unless the module's flash ROM is written. So every parameter written
    /// marks its module as CHANGED here; the Axis Settings page shows a blinking button for each
    /// changed module of its axes (FlashRomButton) until its flash ROM is written: "[Cd.1] Flash ROM
    /// write request", Un\G5900 = 1 - the module sets it back to 0 when it has written (up to 5 s).
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

        // ---- the modules changed since their flash ROM was written ---------------------------------------

        private static readonly object _gate = new object();
        /// <summary>"station|module" - what was changed in it ("Pr.3 of axis 2: 12.5000 mm").</summary>
        private static readonly Dictionary<string, List<string>> _changed = new Dictionary<string, List<string>>();

        /// <summary>A module was changed, or its flash ROM written (the buttons look again).</summary>
        public static event EventHandler PendingChanged;

        private static string Key(int station, int module) { return station + "|" + module; }

        private static void Raise()
        {
            var h = PendingChanged;
            if (h != null) try { h(null, EventArgs.Empty); } catch { }
        }

        /// <summary>A parameter of the module written: its flash ROM to be written before the power goes off.</summary>
        public static void MarkChanged(int station, int module, string what)
        {
            if (module <= 0) return;
            lock (_gate)
            {
                List<string> list;
                if (!_changed.TryGetValue(Key(station, module), out list)) _changed[Key(station, module)] = list = new List<string>();
                list.RemoveAll(w => w == what);
                list.Add(what ?? "");
            }
            Raise();
        }

        /// <summary>A parameter of an axis written: as <see cref="MarkChanged(int,int,string)"/>, named "Pr.3 of axis 2: 12.5000 mm".</summary>
        public static void MarkChanged(int station, int module, int axis, SimpleMotion.Item item, string shown)
        {
            MarkChanged(station, module, string.Format(T("Msg_MotionChangedItem", "{0} ({1}) of axis {2}: {3}"),
                                                      T(item.TitleKey, item.Title), item.Ref, axis, shown));
        }

        public static bool IsChanged(int station, int module)
        {
            lock (_gate) return _changed.ContainsKey(Key(station, module));
        }

        /// <summary>What was changed in a module since its flash ROM was written.</summary>
        public static IList<string> ChangesOf(int station, int module)
        {
            lock (_gate)
            {
                List<string> list;
                return _changed.TryGetValue(Key(station, module), out list) ? list.ToList() : new List<string>();
            }
        }

        /// <summary>The module's flash ROM written: nothing waiting any more.</summary>
        public static void Clear(int station, int module)
        {
            bool had;
            lock (_gate) had = _changed.Remove(Key(station, module));
            if (had) Raise();
        }

        /// <summary>For the tests: nothing changed anywhere.</summary>
        internal static void ClearAll()
        {
            lock (_gate) _changed.Clear();
            Raise();
        }

        // ---- writing the flash ROM -----------------------------------------------------------------------

        /// <summary>
        /// Writes module <paramref name="module"/>'s flash ROM on <paramref name="station"/> (Un\G5900 = 1),
        /// with a window while the module writes (up to 5 s), and says how it went. Done: the module no
        /// longer counts as changed.
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
                string.Format(T("Msg_MotionFlashWriting", "Writing simple motion module {0}'s flash ROM (up to 5 seconds) - do not switch the machine off."), module),
                p =>
                {
                    using (WriteSource.For(string.Format("Simple motion module {0}: flash ROM write request (Cd.1)", module)))
                        return SimpleMotion.WriteFlashRom(driver, module, out writes);
                });
            string text;
            switch (result)
            {
                case SimpleMotion.FlashResult.Written:
                    Clear(station, module);
                    text = string.Format(T("Msg_MotionFlashDone", "Module {0}'s flash ROM written ({1} of {2} writes allowed since the power was switched on)."),
                                         module, writes < 0 ? "?" : writes.ToString(), SimpleMotion.MaxFlashWritesPerPowerOn);
                    break;
                case SimpleMotion.FlashResult.TooManyWrites:
                    text = string.Format(T("Msg_MotionFlashTooMany",
                        "Not written: the flash ROM has been written {0} times since the power was switched on - the module allows {0}. Switch it off and on first."),
                        SimpleMotion.MaxFlashWritesPerPowerOn);
                    break;
                case SimpleMotion.FlashResult.TimedOut:
                    text = string.Format(T("Msg_MotionFlashTimedOut",
                        "Asked for ({0} = 1), but the module did not report its flash ROM written within 8 seconds. It does not write while its " +
                        "PLC READY signal (Cd.190) is on (warning 0905H) - check its warning and error codes, then write it again."),
                        SimpleMotion.Device(module, SimpleMotion.FlashWriteRequestAddress));
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
