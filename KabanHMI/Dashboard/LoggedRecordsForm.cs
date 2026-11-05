using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// THE RECORDS TO LOG (5 November: "We need to add the controls for the selection of which events to be logged to the
    /// database when 'Industry 4 logging in the HMI' activated ... 'ActiveDevice' shows which device to activate for which
    /// log"): a switch row for each record of the machine's state map that starts one and has its PLC bit - its words in
    /// the HMI's language, its code and bit before the switch; on: the PLC logs it.
    ///
    /// Opened from Machine Parameters > Alarms and Logs (Records to log: Choose...); OK gives the choices to the page, whose
    /// Save keeps them in the machine's settings and has the logger tell the PLC (MachineLogger.TellPlcAgain). "As the
    /// machine's file": the state map's own Active (Dashboard\StateMap.json, the Industry 4 program's file).
    /// </summary>
    public sealed class LoggedRecordsForm : Form
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        /// <summary>A row as the Machine Settings pages' rows: 450 wide, a settings row high.</summary>
        public const int RowW = 450, Gap = 24;

        private readonly StateMap _map;
        private readonly Dictionary<int, CheckBoxHMI> _rows = new Dictionary<int, CheckBoxHMI>();
        private readonly ToolTip _tips = UiStyle.Tips();

        /// <param name="chosen">Each switchable record's code -> logged now.</param>
        public LoggedRecordsForm(StateMap map, IDictionary<int, bool> chosen, string machine)
        {
            _map = map ?? new StateMap();
            Name = "loggedRecords";
            Text = T("Plg_Records", "Records to log") + (string.IsNullOrEmpty(machine) ? "" : " - " + machine);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = UiStyle.Body;

            int width = 2 * RowW + Gap;
            var note = new Label
            {
                Name = "recordsNote", AutoSize = false, Location = new Point(16, 12), Size = new Size(width, 58), ForeColor = Color.FromArgb(90, 96, 106),
                Text = T("Plg_RecordsNote", "The PLC logs a record only while its bit (L1000 ...) is on. The HMI sets each bit as chosen here when it connects " +
                                             "to the PLC, and again when the page is saved. A record continued by another (There is an error (2), an axis's " +
                                             "data (2)) is logged with it.")
            };
            Controls.Add(note);

            var list = new Panel { Name = "recordRows", Location = new Point(16, 76), BackColor = Color.White };
            string column = StateMap.ColumnOf(LanguageManager.CurrentLanguage);
            var entries = _map.Switchable.ToList();
            // the words of each: in the HMI's language - a text two records share ("Cutting done", of 8 and 9) with the
            // state map's own parenthesis after it ("(Processing Centre)")
            var words = entries.ToDictionary(e => e.Code, e => _map.Translate(e.Description, column));
            foreach (var e in entries)
            {
                string w = words[e.Code];
                int open = e.Description.LastIndexOf('(');
                if (open > 0 && words.Values.Count(x => x == w) > 1)
                {
                    string extra = e.Description.Substring(open).Trim();
                    if (!w.Contains(extra)) words[e.Code] = w + " " + extra;
                }
            }
            int perColumn = Math.Max(1, (entries.Count + 1) / 2);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                bool on;
                if (chosen == null || !chosen.TryGetValue(e.Code, out on)) on = e.Active;
                var row = new CheckBoxHMI
                {
                    Name = "record" + e.Code, Size = new Size(RowW, UiStyle.SettingRowHeight),
                    Location = new Point(i / perColumn * (RowW + Gap), i % perColumn * UiStyle.SettingRowHeight),
                    Checked = on, RowNote = e.Code + " · " + e.ActiveDevice.Trim()
                };
                row.DescriptionText = words[e.Code];
                var with = _map.ContinuationsOf(e.Code).Select(c => _map.Translate(c.Description, column)).ToList();
                UiStyle.SetTip(_tips, row, string.Format(T("Plg_RecordsTip", "Code {0}, PLC bit {1}."), e.Code, e.ActiveDevice.Trim()) +
                                           (with.Count > 0 ? "\n" + string.Format(T("Plg_RecordsWith", "Logged with it: {0}."), string.Join(", ", with)) : ""));
                _rows[e.Code] = row;
                list.Controls.Add(row);
            }
            list.Size = new Size(width, perColumn * UiStyle.SettingRowHeight);
            Controls.Add(list);

            int y = list.Bottom + 16;
            var all = UiStyle.TextButton(T("Plg_RecordsAll", "All"), 0, (s, e) => { foreach (var r in _rows.Values) r.Checked = true; });
            all.Name = "recordsAll";
            var file = UiStyle.TextButton(T("Plg_RecordsFile", "As the machine's file"), 0, (s, e) =>
            {
                foreach (var en in _map.Switchable) { CheckBoxHMI r; if (_rows.TryGetValue(en.Code, out r)) r.Checked = en.Active; }
            });
            file.Name = "recordsFile";
            var ok = UiStyle.TextButton(T("Btn_OK", "OK"), 0, (s, e) => { DialogResult = DialogResult.OK; Close(); });
            ok.Name = "recordsOk";
            var cancel = UiStyle.TextButton(T("Btn_Cancel", "Cancel"), 0, (s, e) => { DialogResult = DialogResult.Cancel; Close(); });
            cancel.Name = "recordsCancel";
            all.Location = new Point(16, y);
            file.Location = new Point(all.Right + 8, y);
            cancel.Location = new Point(16 + width - cancel.Width, y);
            ok.Location = new Point(cancel.Left - 8 - ok.Width, y);
            Controls.AddRange(new Control[] { all, file, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
            ClientSize = new Size(width + 32, y + Math.Max(ok.Height, all.Height) + 16);
            UiScale.ApplyOnLoad(this);
        }

        /// <summary>The choices now: each record's code -> logged.</summary>
        public Dictionary<int, bool> Chosen { get { return _rows.ToDictionary(kv => kv.Key, kv => kv.Value.Checked); } }

        /// <summary>The rows (tests).</summary>
        public IDictionary<int, CheckBoxHMI> Rows { get { return _rows; } }
    }
}
