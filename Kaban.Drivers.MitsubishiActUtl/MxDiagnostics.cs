using System;
using System.Text;
using Microsoft.Win32;
using Kaban.Core.Helpers;

namespace Kaban.Drivers.MitsubishiActUtl
{
    /// <summary>
    /// Environment diagnostics for MX Component. Instead of guessing why a connect
    /// fails, this reports the FACTS from the machine: in which COM view the
    /// ActUtlType class is registered, and which logical stations the Communication
    /// Setting Utility has actually stored - the two things that explain
    /// 0x80040154 (class not registered) and 0xF0000003 (station not found).
    /// </summary>
    public static class MxDiagnostics
    {
        // CLSID of the ActUtlType coclass (identical in the customer's working build)
        private const string ActUtlTypeClsid = "{63885648-1785-41A4-82D5-C578D29E4DA8}";

        private static bool _reported;

        /// <summary>Logs the report at most once per run (connect retries every 2s).</summary>
        public static void ReportOnce()
        {
            if (_reported) return;
            _reported = true;
            Report();
        }

        public static void Report()
        {
            AuditLogger.Log(BuildReport());
        }

        /// <summary>The same report as text, for PLC Connection > MX Component on screen.</summary>
        public static string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[PLC DIAG] ---- MX Component environment ----");
            sb.AppendLine("  process: " + (Environment.Is64BitProcess ? "64-bit" : "32-bit") +
                          " on " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit") + " Windows");

            sb.AppendLine("  ActUtlType class registration:");
            sb.AppendLine("    32-bit view: " + DescribeClass(RegistryView.Registry32));
            sb.AppendLine("    64-bit view: " + DescribeClass(RegistryView.Registry64));

            sb.AppendLine("  MX Component registry (searching for logical-station settings):");
            _lines = 0;
            bool any = false;
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
                    any |= DumpStations(sb, hive, view);
            if (!any)
                sb.AppendLine("    (no Mitsubishi/MELSOFT vendor keys visible to this process at all)");
            sb.AppendLine("  NOTE: if no station entries appear above, use the station SCAN on");
            sb.AppendLine("        PLC Connection > MX Component - it opens each logical station number directly and");
            sb.AppendLine("        reports which one answers, regardless of where settings are stored.");

            return sb.ToString();
        }

        private static string DescribeClass(RegistryView view)
        {
            try
            {
                using (var root = RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, view))
                using (var k = root.OpenSubKey(@"CLSID\" + ActUtlTypeClsid + @"\InprocServer32"))
                {
                    if (k == null) return "NOT registered";
                    var path = k.GetValue("") as string;
                    return "registered -> " + (string.IsNullOrEmpty(path) ? "(no path)" : path);
                }
            }
            catch (Exception ex) { return "probe failed: " + ex.Message; }
        }

        /// <summary>
        /// SEARCHES for MX Component's station settings instead of assuming a path.
        /// The first version guessed key names and found nothing on the customer's
        /// PC, which proved only that the guesses were wrong - so this walks the
        /// Mitsubishi/MELSOFT vendor roots (both hives, both registry views) and
        /// reports every key that actually looks like a logical-station entry.
        /// </summary>
        private static bool DumpStations(StringBuilder sb, RegistryHive hive, RegistryView view)
        {
            string[] roots =
            {
                @"SOFTWARE\Mitsubishi", @"SOFTWARE\MITSUBISHI", @"SOFTWARE\Mitsubishi Electric",
                @"SOFTWARE\MELSOFT", @"SOFTWARE\MELSEC"
            };
            bool found = false;
            foreach (var root in roots)
            {
                try
                {
                    using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
                    using (var k = baseKey.OpenSubKey(root))
                    {
                        if (k == null) continue;
                        found = true;
                        sb.AppendLine("    [" + hive + " / " + view + "] " + root + " exists:");
                        Walk(sb, k, root, 0);
                    }
                }
                catch { /* probing only - a diagnostic must never break the HMI */ }
            }
            return found;
        }

        private const int MaxDepth = 4;
        private static int _lines;

        /// <summary>Bounded walk that prints keys looking like station configuration.</summary>
        private static void Walk(StringBuilder sb, RegistryKey key, string path, int depth)
        {
            if (depth > MaxDepth || _lines > 60) return;
            string[] subs;
            try { subs = key.GetSubKeyNames(); } catch { return; }

            foreach (var name in subs)
            {
                if (_lines > 60) { sb.AppendLine("      ... (truncated)"); return; }
                string childPath = path + "\\" + name;
                try
                {
                    using (var child = key.OpenSubKey(name))
                    {
                        if (child == null) continue;
                        var values = child.GetValueNames();
                        bool looksLikeStation =
                            name.IndexOf("station", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            name.IndexOf("logical", StringComparison.OrdinalIgnoreCase) >= 0;
                        foreach (var v in values)
                            if (v.IndexOf("station", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                v.IndexOf("logical", StringComparison.OrdinalIgnoreCase) >= 0)
                                looksLikeStation = true;

                        if (looksLikeStation)
                        {
                            _lines++;
                            sb.Append("      " + childPath);
                            if (values.Length > 0)
                            {
                                sb.Append("   values: ");
                                int shown = 0;
                                foreach (var v in values)
                                {
                                    if (shown++ == 6) { sb.Append(", ..."); break; }
                                    object val = null;
                                    try { val = child.GetValue(v); } catch { }
                                    sb.Append((shown > 1 ? ", " : "") + (string.IsNullOrEmpty(v) ? "(default)" : v) +
                                              "=" + Convert.ToString(val));
                                }
                            }
                            sb.AppendLine();
                        }
                        Walk(sb, child, childPath, depth + 1);
                    }
                }
                catch { }
            }
        }

        private static bool _scanned;

        /// <summary>
        /// After the configured station fails, try every logical station number once
        /// and report which (if any) actually opens. The customer's utility shows a
        /// station configured while the control reports it missing, so the empirical
        /// answer is worth more than any further theory - and it needs no operator
        /// action to produce.
        /// </summary>
        public static void AutoScanOnce(int configuredStation, int upTo = 20)
        {
            if (_scanned) return;
            _scanned = true;
            var sb = new StringBuilder();
            sb.AppendLine("[PLC DIAG] scanning logical stations 0.." + upTo +
                          " (configured station " + configuredStation + " failed):");
            var answered = new StringBuilder();
            for (int n = 0; n <= upTo; n++)
            {
                int code = TryOpenStation(n);
                if (code == 0)
                {
                    sb.AppendLine("    station " + n + ": OPEN OK");
                    answered.Append(answered.Length > 0 ? ", " : "").Append(n);
                }
                else if (unchecked((uint)code) != 0xF0000003)
                    sb.AppendLine("    station " + n + ": 0x" + code.ToString("X8") + " (" + DescribeOpenError(code) + ")");
            }
            sb.AppendLine(answered.Length > 0
                ? "  --> USE LOGICAL STATION: " + answered
                : "  --> no logical station answered; MX Component cannot see ANY station from this process.");
            AuditLogger.Log(sb.ToString());
        }

        /// <summary>
        /// THE PLC DIAGNOSTICS PAGE'S TEST: opens one logical station on a
        /// throwaway control, on a thread of its own, and returns the code.
        ///
        /// The page used to build a whole driver per station for this - each with
        /// a message-loop thread that was never ended, and a connect it waited
        /// only 150 ms for, so a station that needed longer to open was reported
        /// as failing while it was still opening. Here the control lives exactly
        /// as long as the test, and the wait is the caller's to choose. Called
        /// from a background thread; blocks for at most timeoutMs.
        /// </summary>
        /// <param name="timedOut">True when Open() had not returned in time (it
        /// carries on in the background and its control is released when it does).</param>
        public static int TestStation(int station, int timeoutMs, out bool timedOut)
        {
            int code = unchecked((int)0x80040154);
            var t = new System.Threading.Thread(() => { code = TryOpenStation(station); })
            {
                IsBackground = true,
                Name = "MX Component test of logical station " + station
            };
            t.SetApartmentState(System.Threading.ApartmentState.STA);
            t.Start();
            timedOut = !t.Join(Math.Max(100, timeoutMs));
            return code;
        }

        /// <summary>Opens one station on a throwaway control and returns its code.</summary>
        private static int TryOpenStation(int station)
        {
            object plc = null;
            try
            {
                plc = new ActUtlTypeLib.ActUtlType();
                var typed = (ActUtlTypeLib.ActUtlType)plc;
                typed.ActLogicalStationNumber = station;
                int rc = typed.Open();
                if (rc == 0) { try { typed.Close(); } catch { } }
                return rc;
            }
            catch (Exception) { return unchecked((int)0x80040154); }
            finally
            {
                try { if (plc != null) System.Runtime.InteropServices.Marshal.ReleaseComObject(plc); } catch { }
            }
        }

        /// <summary>Plain-language meaning of the common ActUtlType Open() codes.</summary>
        public static string DescribeOpenError(int code)
        {
            switch (unchecked((uint)code))
            {
                // Meanings corrected from the on-site scan: unconfigured stations
                // answered 0xF0000002, while the CONFIGURED station 1 opened fine on
                // a fresh control and only returned 0xF0000003 on a control that had
                // already failed once. 0xF0000003 is MX Component's "already open":
                // the logs of 28-29 September show it one second after a SUCCESSFUL
                // Open, when the same control was opened a second time.
                case 0xF0000003: return "already open - Open() on a control that is open, or left half-open by a failed Open; a fresh control is needed";
                case 0x01808301: return "no answer from the controller while opening - not reachable, or still holding an earlier connection";
                case 0xF0000002: return "no such logical station in the Communication Setting Utility";
                case 0xF0000001: return "logical station number invalid / not set";
                case 0x80040154: return "ActUtlType COM class not registered for this process bitness";
                case 0x01010002: return "communication error - target unreachable";
                default: return "see the MX Component error code list";
            }
        }
    }
}

