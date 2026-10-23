using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

namespace Kaban.UI.Forms
{
    // ======================================================================
    // THE PLC MONITOR'S LABELS WRITTEN FROM A BACKUP (23 October: "We should add PLC
    // device write function to the PLC monitor labels (selected label or all the labels
    // in selected tabs). The backup selection page should open (just for the backup
    // selection btw, not for the writing device value interval selection) when labels or
    // tabs selected and 'Write to PLC from backup' button clicked, then only the selected
    // label devices are written to PLC from the selected backup (it should be controlled
    // if there is a backup read at the directory even then and the backup should be taken
    // first before actually writing the selected labels here as well to prevent accidental
    // device value loss (this preventive data backup function should only be considered
    // for the real PLCs btw, not for the simulation driver))").
    //
    // The labels chosen - the selected ones, every one of the tab, every one of every
    // tab of the controller - give their devices: an array's every element, a structure's
    // whole length, a 32-bit device (LCN, LZ) once, a bit of a word only that bit (the
    // rest of the word as it is now). The backup chosen (PlcBackupChooseForm) gives their
    // values; what it does not have is left as it is - said in the question. A real PLC:
    // the inputs, outputs, contacts and system devices are not written (as the PLC
    // Connection page's Write), and without a backup of today one is read first
    // (PlcBackupUi.SafetyBackup).
    // ======================================================================
    public partial class SimulationMonitorForm
    {
        /// <summary>The toolbar button's menu: the selected labels, this tab's, every tab's.</summary>
        private ContextMenuStrip BackupMenu()
        {
            var m = new ContextMenuStrip();
            var mon = CurrentMonitor;
            var sh = mon != null ? mon.Current : null;
            bool sel = sh != null && SelectedIndices(sh).Any(i => i < sh.Rows.Count && sh.Rows[i].IsDevice);
            bool tab = sh != null && sh.AllRows.Any(r => r.IsDevice);
            bool every = mon != null && mon.Sheets.Any(s => s.AllRows.Any(r => r.IsDevice));
            m.Items.Add(Item(T("Mnu_MonBakSelected", "The selected labels..."), (s, e) => WriteFromBackup(mon, SelectedLabels(sh), SelectedText(sh)), sel));
            m.Items.Add(Item(sh != null ? string.Format(T("Mnu_MonBakTab", "Every label of the tab {0}..."), sh.Name) : T("Mnu_MonBakTabNone", "Every label of this tab..."),
                             (s, e) => WriteFromBackup(mon, TabLabels(sh), string.Format(T("Msg_MonBakTabWhat", "the labels of the tab {0}"), sh.Name)), tab));
            m.Items.Add(Item(mon != null ? string.Format(T("Mnu_MonBakEvery", "Every label of every tab of {0}..."), PlcEndpointNaming.Caption(mon.Endpoint))
                                         : T("Mnu_MonBakEveryNone", "Every label of every tab..."),
                             (s, e) => WriteFromBackup(mon, mon.Sheets.SelectMany(TabLabels).ToList(),
                                                       string.Format(T("Msg_MonBakEveryWhat", "the labels of every tab of {0}"), PlcEndpointNaming.Caption(mon.Endpoint))), every));
            return m;
        }

        /// <summary>The selected rows that are devices - a row under another one chosen goes with it.</summary>
        internal static List<WatchRow> SelectedLabels(Sheet sh)
        {
            if (sh == null) return new List<WatchRow>();
            var chosen = SelectedIndices(sh).Where(i => i >= 0 && i < sh.Rows.Count).Select(i => sh.Rows[i]).Where(r => r.IsDevice).ToList();
            return chosen.Where(r => !chosen.Any(o => !ReferenceEquals(o, r) && IsUnder(r, o))).ToList();
        }

        /// <summary>"D100 [Speed], M10" - the labels chosen, for the question.</summary>
        private static string SelectedText(Sheet sh)
        {
            var rows = SelectedLabels(sh);
            var names = rows.Take(6).Select(r => string.IsNullOrWhiteSpace(r.EffectiveLabel) ? r.ShownAddress : r.EffectiveLabel.Trim()).ToList();
            return string.Join(", ", names) + (rows.Count > 6 ? ", ..." : "");
        }

        /// <summary>Every label of a tab - the list's and the tables' (each item once: an array with its elements).</summary>
        internal static List<WatchRow> TabLabels(Sheet sh)
        {
            if (sh == null) return new List<WatchRow>();
            EnsureElements(sh);
            EnsureElements(sh.Tables);
            return sh.AllRows.Where(r => r.IsDevice && r.Head == null).ToList();
        }

        private static readonly Regex BitOfWord = new Regex(@"^(.+)\.([0-9A-Fa-f])$");

        /// <summary>
        /// The devices of <paramref name="rows"/> as a backup holds them: <paramref name="devices"/> written whole;
        /// <paramref name="bits"/> - a bit of a word (a structure's Bit member, "D100.5") - only that bit.
        /// </summary>
        internal static void BackupDevices(IEnumerable<WatchRow> rows, List<string> devices, List<KeyValuePair<string, int>> bits)
        {
            foreach (var leaf in rows.SelectMany(ValueRows))
            {
                if (leaf.BitIndex >= 0) { bits.Add(new KeyValuePair<string, int>(leaf.Address, leaf.BitIndex)); continue; }
                var m = BitOfWord.Match((leaf.Address ?? "").Trim());
                if (m.Success)
                {
                    bits.Add(new KeyValuePair<string, int>(m.Groups[1].Value, int.Parse(m.Groups[2].Value, NumberStyles.HexNumber)));
                    continue;
                }
                var list = (leaf.Structure != null ? StructureWords(leaf) : WordsOf(leaf)).ToList();
                // a 32-bit device (LCN, LZ): its two words are one device
                string p; long n;
                if (list.Count > 1 && PlcAddressHelper.TrySplit(list[0], out p, out n))
                {
                    var k = PlcBackup.KindOf(p);
                    if (k != null && k.WordsPerPoint == 2) list = list.Take((list.Count + 1) / 2).ToList();
                }
                foreach (var d in list)
                    if (!devices.Contains(d, StringComparer.OrdinalIgnoreCase)) devices.Add(d);
            }
        }

        /// <summary>What a write from a backup does (tests read it).</summary>
        internal sealed class BackupWrite
        {
            public List<PlcBackup.WriteRange> Parts = new List<PlcBackup.WriteRange>();
            /// <summary>Bits of words: the bits' values the backup has (the word's other bits as the PLC has them when written).</summary>
            public List<WordBits> Bits = new List<WordBits>();
            /// <summary>Not in the backup; not written to a real PLC (why).</summary>
            public List<string> Missing = new List<string>(), NotWritten = new List<string>();
            public long Count { get { return Parts.Sum(p => (long)p.Count) + Bits.Sum(b => (long)b.Shown.Count); } }
        }

        /// <summary>A word's bits written: those of <see cref="Mask"/> set as in <see cref="Value"/>.</summary>
        internal sealed class WordBits
        {
            public string Word = "";
            public int Mask, Value;
            /// <summary>"D300.3: 1" a bit.</summary>
            public List<string> Shown = new List<string>();
            /// <summary>The word with these bits as the backup has them, the others as <paramref name="now"/>.</summary>
            public int Into(int now) { return (short)((now & ~Mask) | (Value & Mask)); }
        }

        /// <summary>What writing <paramref name="rows"/> from <paramref name="backup"/> writes - nothing written yet.</summary>
        internal static BackupWrite PlanBackupWrite(PlcBackup.Backup backup, IEnumerable<WatchRow> rows, bool realPlc)
        {
            var plan = new BackupWrite();
            var devices = new List<string>();
            var bits = new List<KeyValuePair<string, int>>();
            BackupDevices(rows, devices, bits);
            List<string> missing;
            var parts = PlcBackup.PartsFor(backup, devices, out missing);
            plan.Missing.AddRange(missing);
            foreach (var part in parts)
            {
                string why = realPlc ? part.Source.WhyNotWritten : null;
                if (why == null) plan.Parts.Add(part);
                else plan.NotWritten.Add(part + " (" + why + ")");
            }
            // a bit of a word: the backup's bit into the word as it is now - its word written whole when chosen too
            foreach (var g in bits.GroupBy(b => b.Key, StringComparer.OrdinalIgnoreCase))
            {
                string word = g.Key;
                var shown = g.Select(b => word + "." + b.Value.ToString("X", CultureInfo.InvariantCulture)).ToList();
                if (devices.Contains(word, StringComparer.OrdinalIgnoreCase)) continue;
                int index;
                var area = PlcBackup.AreaOf(backup, word, out index);
                if (area == null || area.Bits) { plan.Missing.AddRange(shown); continue; }
                string why = realPlc ? area.WhyNotWritten : null;
                if (why != null) { plan.NotWritten.AddRange(shown.Select(s => s + " (" + why + ")")); continue; }
                int from = area.Value(index);
                var wb = new WordBits { Word = word };
                foreach (var b in g.Select(x => x.Value).Distinct().OrderBy(x => x))
                {
                    wb.Mask |= 1 << b;
                    wb.Shown.Add(word + "." + b.ToString("X", CultureInfo.InvariantCulture) + ": " + ((from >> b) & 1));
                }
                wb.Value = from & wb.Mask;
                plan.Bits.Add(wb);
            }
            return plan;
        }

        /// <summary>"D100 - D103: 1, 2, 3, 4" - a part's values as the backup has them.</summary>
        private static string PartLine(PlcBackup.WriteRange p)
        {
            var values = Enumerable.Range(0, Math.Min(p.Count, 8)).Select(i => p.Source.Value(p.From + i).ToString(CultureInfo.InvariantCulture));
            return p + ": " + string.Join(", ", values) + (p.Count > 8 ? ", ..." : "");
        }

        /// <summary>
        /// <paramref name="rows"/> (<paramref name="what"/> in the question) written from a backup: the backup
        /// chosen (<paramref name="backup"/>: chosen already - tests), a question, the safety backup of a real
        /// PLC, the write. The result: null when nothing was written.
        /// </summary>
        internal PlcBackup.WriteResult WriteFromBackup(MonitorTab mon, List<WatchRow> rows, string what, PlcBackup.Backup backup = null)
        {
            if (mon == null || rows == null || rows.Count == 0) return null;
            var owner = DialogOwner;
            if (!SecurityManager.HasAccess(Kaban.Core.Enums.SecurityLevel.Admin))
            {
                PlcBackupUi.Say(owner, T("Msg_MonBakAdmin", "Writing from a backup is for the Admin level."), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            var driver = PlcManager.Instance.GetDriverForWrite(mon.Station);
            var block = driver as IPlcBlockAccess;
            if (driver == null || block == null || !driver.IsConnected)
            {
                PlcBackupUi.Say(owner, T("Msg_BakNotConnectedWrite", "The PLC is not connected (or your user level may not write to it)."), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            if (backup == null) backup = PlcBackupChooseForm.Choose(owner, mon.Endpoint, what);
            if (backup == null) return null;

            bool real = !PlcBackupUi.IsSimulator(mon.Station);
            var plan = PlanBackupWrite(backup, rows, real);
            string from = backup.Date.ToString("yyyy-MM-dd HH:mm") + " - " + (backup.Machine + " " + backup.Plc).Trim();
            Func<string, List<string>, string> listed = (title, l) => l.Count == 0 ? "" :
                "\r\n\r\n" + title + "\r\n" + string.Join("\r\n", l.Take(10).Select(x => "  " + x)) + (l.Count > 10 ? "\r\n  ..." : "");
            string others = listed(T("Msg_MonBakMissing", "Not in the backup - left as they are:"), plan.Missing) +
                            listed(T("Msg_MonBakNotWritten", "Not written:"), plan.NotWritten);
            if (plan.Count == 0)
            {
                PlcBackupUi.Say(owner, string.Format(T("Msg_MonBakNothing", "Nothing of {0} can be written from the backup of {1}."), what, from) + others,
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
            }
            var lines = plan.Parts.Select(PartLine).Concat(plan.Bits.SelectMany(b => b.Shown)).ToList();
            string question = string.Format(T("Msg_MonBakAsk", "Write {0} device(s) of {1} from the backup of {2} into {3}?"),
                                            plan.Count.ToString("N0"), what, from, PlcEndpointNaming.Caption(mon.Endpoint)) +
                              "\r\n\r\n" + string.Join("\r\n", lines.Take(16).Select(l => "  " + l)) + (lines.Count > 16 ? "\r\n  ..." : "") + others;
            if (real) question += "\r\n\r\n" + T("Msg_MonResetLive", "This is the machine's PLC: it uses the new values at once.");
            string machine, plc;
            PlcBackupUi.Names(mon.Endpoint, out machine, out plc);
            if (backup.Machine != machine || backup.Plc != plc)
                question += "\r\n\r\n" + string.Format(T("Msg_BakOtherPlc", "This backup is of another PLC ({0}) - not of {1}."),
                                                       (backup.Machine + " " + backup.Plc).Trim(), (machine + " " + plc).Trim());
            if (PlcBackupUi.Say(owner, question, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return null;
            // no backup of this (real) PLC today: one read first - what is overwritten can be written back
            if (!PlcBackupUi.SafetyBackup(owner, mon.Endpoint, string.Format(T("Bak_SafetyBeforeLabels", "writing {0} from the backup of {1}"), what, from)))
                return null;

            var link = new PlcBackup.Link(driver, block);
            var written = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string source = string.Format("PLC Monitor: {0} from the backup of {1}", what, from);
            var result = WorkForm.Run(owner, Text, T("Msg_BakWritingStart", "Writing..."), p =>
            {
                using (WriteSource.For(source))
                {
                    var r = PlcBackup.Write(link, plan.Parts, (d, t, w) => { p.Report(d, t, w); return !p.Cancelled; });
                    // a bit of a word: the word read now, those bits changed in it
                    foreach (var b in plan.Bits)
                    {
                        if (r.Cancelled || r.LinkLost) break;
                        int now;
                        if (!link.ReadOne(b.Word, out now))
                        {
                            if (link.Lost) { r.LinkLost = true; break; }
                            r.Failed.Add(string.Join(", ", b.Shown) + " (" + T("Msg_MonBakWordNotRead", "its word could not be read") + ")");
                            continue;
                        }
                        int rc = link.WriteOne(b.Word, b.Into(now));
                        if (link.Lost) { r.LinkLost = true; break; }
                        if (rc != 0) r.Failed.Add(b.Word + " (" + (rc == -1 ? "refused" : "0x" + rc.ToString("X8")) + ")");
                        else { r.Points += b.Shown.Count; written[b.Word] = b.Into(now); }
                    }
                    return r;
                }
            }, cancellable: true);
            foreach (var w in written) mon.Values[w.Key] = w.Value;
            AuditLogger.Log(string.Format("PLC Monitor: {0} written (station {1}) from {2}: {3} devices{4}{5}{6}", what, mon.Station, backup.Path, result.Points,
                                          result.Cancelled ? " - cancelled" : "", result.LinkLost ? " - the connection was lost" : "",
                                          result.Failed.Count > 0 ? " - failed: " + string.Join(", ", result.Failed) : ""));
            // a module's data written: it counts as changed - its flash ROM (the Axis Settings page's button) tells
            foreach (var module in plan.Parts.Where(x => x.Source.IsModule).Select(x => PlcBackup.PlaceOf(x.Target)).Where(x => x != null && x.Module > 0).Select(x => x.Module).Distinct())
                MotionFlash.MarkChanged(mon.Station, module, string.Format(T("Msg_MonBakModuleChanged", "{0} written into it from the backup of {1}"), what, from));
            string text = string.Format(T("Msg_BakWritten", "{0} devices written."), result.Points.ToString("N0"));
            if (result.Cancelled) text += "\r\n" + T("Msg_BakWriteCancelled", "Cancelled - the parts after it were not written.");
            if (result.LinkLost) text += "\r\n" + T("Msg_BakWriteLinkLost", "The connection to the PLC was lost and did not come back - the parts after it were not written.");
            if (result.Failed.Count > 0) text += "\r\n\r\n" + T("Msg_BakWriteFailed", "The PLC refused:") + "\r\n" + string.Join("\r\n", result.Failed);
            PlcBackupUi.Say(owner, text, MessageBoxButtons.OK, result.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            SetStatus(text.Split('\r')[0]);
            return result;
        }
    }
}
