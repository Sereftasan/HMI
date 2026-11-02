using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// WINDOWS' ON-SCREEN KEYBOARD (osk.exe) ALONG THE BOTTOM OF THE SCREEN (2 November: "The original windows on screen
    /// keyboard is very nice btw, it has all more functions and all punctuation marks as well, is it possible to just use it
    /// instead of creating a personal one (we would just need to land it to the bottom of our screen instead if it is
    /// possible)").
    ///
    /// The keyboard on the screen when it is chosen (the top bar's mouse / finger menu: "Keyboard on the screen"; the
    /// default): a tap on a text box in touch mode starts it - when it is not on the screen already - along the bottom of the
    /// screen, as wide as the screen and as tall as the HMI's own keyboard. Its place is written where it keeps it
    /// (HKCU\Software\Microsoft\Osk: WindowLeft, WindowTop, WindowWidth, WindowHeight) BEFORE it starts: once it runs,
    /// Windows lets no ordinary program move it (it is an accessibility tool - uiAccess - running above the programs' level).
    /// While it is along the bottom of the screen the program is laid out above it as above the HMI's own (KeyboardRoom);
    /// dragged up to float over the page, it is left to float.
    ///
    /// It types into the box with the focus as a keyboard does, never taking the focus. When the typing is done (no text box
    /// has the focus any more) it is asked to close: Windows lets a program that runs as an administrator close it, an
    /// ordinary one not - then it stays until its own ✕ is tapped, and the program takes the whole screen back when it goes.
    ///
    /// Not on this computer, or it could not start: the HMI's own keyboard (TouchKeyboard), as before.
    /// </summary>
    public static class WindowsKeyboard
    {
        private const string OskClass = "OSKMainClass";
        private const string OskKey = @"Software\Microsoft\Osk";

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string cls, string name);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern int GetDeviceCaps(IntPtr dc, int index);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool Wow64DisableWow64FsRedirection(ref IntPtr old);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool Wow64RevertWow64FsRedirection(IntPtr old);

        [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

        private const int WM_SYSCOMMAND = 0x0112, SC_CLOSE = 0xF060, HORZRES = 8, DESKTOPHORZRES = 118;

        private static readonly Timer _watch = new Timer { Interval = 300 };
        private static Control _target;
        private static int _away;
        private static bool _askedToClose, _closeRefused;
        private static DateTime _started = DateTime.MinValue;
        private static Rectangle _band;

        static WindowsKeyboard() { _watch.Tick += (s, e) => Tick(); }

        /// <summary>Windows' keyboard chosen for the screen (the HMI's own: "Own").</summary>
        public static bool Chosen { get { return TouchKeyboard.Choice == "Windows"; } }

        /// <summary>
        /// osk.exe on this computer. The program runs 32-bit (MX Component): on a 64-bit Windows its System32 is SysWOW64,
        /// which has no On-Screen Keyboard - the real one is reached through Sysnative.
        /// </summary>
        public static string ProgramPath
        {
            get
            {
                string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string native = Path.Combine(Path.Combine(windows, "Sysnative"), "osk.exe");
                if (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess && File.Exists(native)) return native;
                return Path.Combine(Path.Combine(windows, "System32"), "osk.exe");
            }
        }

        /// <summary>It can be used here: Windows, osk.exe there.</summary>
        public static bool Available
        {
            get
            {
                if (!TouchInput.IsWindows) return false;
                try { return File.Exists(ProgramPath); } catch { return false; }
            }
        }

        private static IntPtr Window()
        {
            try { return FindWindow(OskClass, null); }
            catch { return IntPtr.Zero; }
        }

        /// <summary>It is on the screen (not minimised).</summary>
        public static bool IsShown
        {
            get
            {
                if (!TouchInput.IsWindows) return false;
                var h = Window();
                try { return h != IntPtr.Zero && IsWindowVisible(h) && !IsIconic(h); } catch { return false; }
            }
        }

        /// <summary>For a text box: started along the bottom of the screen when it is not on it. False: it could not be (the HMI's own then).</summary>
        public static bool ShowFor(Control target)
        {
            if (!Available || target == null || target.IsDisposed) return false;
            _target = target;
            _away = 0;
            _askedToClose = false;
            if (Window() == IntPtr.Zero && (DateTime.Now - _started).TotalSeconds > 4)      // not again while it is starting
            {
                Place(target);
                string why = Start();
                if (why != null)
                {
                    AuditLogger.Log("keyboard: Windows' On-Screen Keyboard not started (" + why + ") - the HMI's own instead.");
                    return false;
                }
                _started = DateTime.Now;
                AuditLogger.Log("keyboard: Windows' On-Screen Keyboard started along the bottom of the screen.");
            }
            if (!Follow() && !_band.IsEmpty) KeyboardRoom.Make(target);
            _watch.Start();
            return true;
        }

        /// <summary>
        /// osk.exe started through the shell - Windows starts an accessibility tool (uiAccess) for a program only so. A 32-bit
        /// program on a 64-bit Windows: with its System32 its own for the moment (not SysWOW64, which has none), else through
        /// Sysnative. Null: started; else why not.
        /// </summary>
        private static string Start()
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string system = Path.Combine(Path.Combine(windows, "System32"), "osk.exe");
            if (!Environment.Is64BitOperatingSystem || Environment.Is64BitProcess)
            {
                try { Process.Start(new ProcessStartInfo(system) { UseShellExecute = true }); return null; }
                catch (Exception ex) { return ex.Message; }
            }
            string first;
            var old = IntPtr.Zero;
            bool off = false;
            try
            {
                off = Wow64DisableWow64FsRedirection(ref old);
                Process.Start(new ProcessStartInfo(system) { UseShellExecute = true });
                return null;
            }
            catch (Exception ex) { first = ex.Message; }
            finally { if (off) try { Wow64RevertWow64FsRedirection(old); } catch { } }
            try { Process.Start(new ProcessStartInfo(ProgramPath) { UseShellExecute = true }); return null; }
            catch (Exception ex) { return first + "; " + ex.Message; }
        }

        /// <summary>The typing done: asked to close (Windows lets only a program running as an administrator close it).</summary>
        public static void HideNow()
        {
            if (!_watch.Enabled) return;
            _target = null;
            var h = Window();
            if (h != IntPtr.Zero && !_askedToClose)
            {
                _askedToClose = true;
                bool asked;
                try { asked = PostMessage(h, WM_SYSCOMMAND, (IntPtr)SC_CLOSE, IntPtr.Zero); } catch { asked = false; }
                if (!asked && !_closeRefused)
                {
                    _closeRefused = true;
                    AuditLogger.Log("keyboard: Windows did not let the program close its On-Screen Keyboard (" + Marshal.GetLastWin32Error() +
                                    ") - it stays until its own close button is tapped (a program run as an administrator may close it).");
                }
            }
            Follow();
        }

        /// <summary>
        /// Its place written where it keeps it: along the bottom of the box's screen, as wide as the screen, as tall as the
        /// HMI's own keyboard - in the screen's own pixels (Windows scales this program's when the screen is set larger than 100 %).
        /// </summary>
        private static void Place(Control target)
        {
            try
            {
                var area = Screen.FromControl(target).WorkingArea;
                int tall = Math.Min(TouchKeyboard.BarHeight, area.Height / 2);
                double k = ScreenPixels();
                using (var key = Registry.CurrentUser.CreateSubKey(OskKey))
                {
                    if (key == null) return;
                    key.SetValue("WindowLeft", (int)Math.Round(area.Left * k), RegistryValueKind.DWord);
                    key.SetValue("WindowTop", (int)Math.Round((area.Bottom - tall) * k), RegistryValueKind.DWord);
                    key.SetValue("WindowWidth", (int)Math.Round(area.Width * k), RegistryValueKind.DWord);
                    key.SetValue("WindowHeight", (int)Math.Round(tall * k), RegistryValueKind.DWord);
                }
            }
            catch (Exception ex) { AuditLogger.Log("keyboard: the On-Screen Keyboard's place not written (" + ex.Message + ")."); }
        }

        /// <summary>The screen's pixels to one of this program's (1 at 100 %; 1.25 at 125 %, this program not scaling itself).</summary>
        private static double ScreenPixels()
        {
            var dc = IntPtr.Zero;
            try
            {
                dc = GetDC(IntPtr.Zero);
                int seen = GetDeviceCaps(dc, HORZRES), real = GetDeviceCaps(dc, DESKTOPHORZRES);
                return seen > 0 && real > 0 ? (double)real / seen : 1.0;
            }
            catch { return 1.0; }
            finally { if (dc != IntPtr.Zero) try { ReleaseDC(IntPtr.Zero, dc); } catch { } }
        }

        /// <summary>
        /// Where it is now: along the bottom of its screen - the program laid out above it (the box brought into sight); gone,
        /// minimised or floating - the whole screen back. True when that changed.
        /// </summary>
        private static bool Follow()
        {
            var band = Rectangle.Empty;
            var h = Window();
            try
            {
                RECT r;
                if (h != IntPtr.Zero && IsWindowVisible(h) && !IsIconic(h) && GetWindowRect(h, out r))
                {
                    var rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                    var area = Screen.FromRectangle(rect).WorkingArea;
                    if (rect.Bottom >= area.Bottom - UiScale.Px(40) && rect.Width >= area.Width / 2) band = rect;
                }
            }
            catch { band = Rectangle.Empty; }
            if (band == _band) return false;
            // the HMI's own keyboard's place is its own to give back
            if (KeyboardRoom.Band.IsEmpty || KeyboardRoom.Band == _band) KeyboardRoom.SetBand(band);
            _band = band;
            if (band.IsEmpty) KeyboardRoom.GiveBack();
            else if (_target != null && !_target.IsDisposed) KeyboardRoom.Make(_target);
            return true;
        }

        private static void Tick()
        {
            var focused = TouchInput.FocusedTextControl();
            if (focused != null)
            {
                _away = 0;
                if (!ReferenceEquals(focused, _target))
                {
                    // another box now (the next field tapped, Tab): brought into sight in its turn
                    _target = focused;
                    _askedToClose = false;
                    if (!Follow() && !_band.IsEmpty) KeyboardRoom.Make(focused);
                    return;
                }
            }
            else if (_target != null && ++_away >= 2) { HideNow(); return; }      // seen so twice running (0.6 s)
            Follow();
            // nothing more to follow: it is gone, nothing is typed in
            if (_target == null && _band.IsEmpty && Window() == IntPtr.Zero && (DateTime.Now - _started).TotalSeconds > 4) _watch.Stop();
        }
    }
}
