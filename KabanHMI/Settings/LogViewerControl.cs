using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// THE AUDIT LOG, READ ON THE PANEL: a table of the entries of a time range,
    /// narrowed by words, user and kind - on the Log Settings page (everything)
    /// and on PLC Diagnostics (connection events).
    ///
    /// It replaces a text box holding the last 200 raw lines of today's file,
    /// which could not look at yesterday, could not find anything, and scrolled
    /// itself to the bottom every two seconds while it was being read.
    ///
    ///   * Period: the last hour, today, yesterday, 7 or 30 days, everything
    ///     kept - or any From/To (changing either picks "From - to").
    ///   * Search: every word must appear, in any case - "D210 admin".
    ///   * User, controller (PLC) and Kind (sign-in, connection, PLC writes,
    ///     problems, settings).
    ///   * Live: a period that runs up to now follows new entries; the row being
    ///     read stays where it is.
    ///   * Newest first; click the Time heading to turn it round.
    ///   * The selected entry in full below the table (a station scan is a
    ///     report of several lines), and Export writes what is shown.
    /// </summary>
    public class LogViewerControl : UserControl
    {
        public enum Period { LastHour = 0, Today, Yesterday, Last7Days, Last30Days, AllKept, Custom }

        /// <summary>At most this many entries are listed - the newest; the status line says so.</summary>
        public const int MaxRows = 20000;

        private readonly ComboBox _period = new ComboBox();
        private readonly DateTimePicker _from = new DateTimePicker();
        private readonly DateTimePicker _to = new DateTimePicker();
        private readonly TextBox _search = new TextBox();
        private readonly ComboBox _user = new ComboBox();
        private readonly ComboBox _plc = new ComboBox();
        private readonly ComboBox _kind = new ComboBox();
        private readonly CheckBox _live = new CheckBox();
        private readonly DataGridView _grid = new DataGridView();
        private readonly TextBox _detail = new TextBox();
        private readonly Label _status = new Label();
        private readonly Timer _refresh = new Timer { Interval = 2000 };
        private readonly Timer _typing = new Timer { Interval = 350 };
        private readonly ToolTip _tips = UiStyle.Tips();

        private List<AuditEntry> _inPeriod = new List<AuditEntry>();
        private List<AuditEntry> _shown = new List<AuditEntry>();
        private bool _newestFirst = true;
        private bool _setting;               // the pickers are being set by the period, not the user
        private string _allUsers;

        private sealed class Choice
        {
            public readonly object Value; private readonly string _text;
            public Choice(object value, string text) { Value = value; _text = text; }
            public override string ToString() { return _text; }
        }

        public LogViewerControl() : this(null, Period.Today) { }

        /// <param name="kind">A kind to start with (PLC Diagnostics: Connection); null for all.</param>
        public LogViewerControl(AuditKind? kind, Period period)
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9.5F);

            // ---- the filters ------------------------------------------------
            var bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = true,
                Padding = new Padding(0, 2, 0, 4),
                BackColor = Color.White
            };

            _period.DropDownStyle = ComboBoxStyle.DropDownList;
            _period.Width = 150;
            foreach (var p in new[]
            {
                new Choice(Period.LastHour,   LanguageManager.GetString("Log_PeriodHour", "Last hour")),
                new Choice(Period.Today,      LanguageManager.GetString("Log_PeriodToday", "Today")),
                new Choice(Period.Yesterday,  LanguageManager.GetString("Log_PeriodYesterday", "Yesterday")),
                new Choice(Period.Last7Days,  LanguageManager.GetString("Log_Period7", "Last 7 days")),
                new Choice(Period.Last30Days, LanguageManager.GetString("Log_Period30", "Last 30 days")),
                new Choice(Period.AllKept,    LanguageManager.GetString("Log_PeriodAll", "Everything kept")),
                new Choice(Period.Custom,     LanguageManager.GetString("Log_PeriodCustom", "From - to"))
            }) _period.Items.Add(p);

            foreach (var dp in new[] { _from, _to })
            {
                dp.Format = DateTimePickerFormat.Custom;
                dp.CustomFormat = "dd.MM.yyyy  HH:mm";
                dp.Width = 150;
                dp.ValueChanged += (s, e) => { if (_setting) return; SelectPeriod(Period.Custom, false); Reload(); };
            }

            _search.Width = 190;
            _tips.SetToolTip(_search, LanguageManager.GetString("Log_SearchTip",
                "Every word must appear, in any case: 'D210 admin' finds admin's entries about D210."));
            _search.TextChanged += (s, e) => { _typing.Stop(); _typing.Start(); };
            _typing.Tick += (s, e) => { _typing.Stop(); ApplyFilter(true); };

            _allUsers = LanguageManager.GetString("Log_AllUsers", "All users");
            _user.DropDownStyle = ComboBoxStyle.DropDownList;
            _user.Width = 130;
            _user.Items.Add(_allUsers);
            _user.SelectedIndex = 0;
            _user.SelectedIndexChanged += (s, e) => { if (!_setting) ApplyFilter(true); };

            // THE CONTROLLER an entry is about: a write names its PLC, a connection
            // entry its station (AuditLogReader.StationOf). Entries about no
            // controller (a sign-in, a setting) are left out once one is chosen.
            _plc.DropDownStyle = ComboBoxStyle.DropDownList;
            _plc.Width = 190;
            _plc.Items.Add(new Choice(null, LanguageManager.GetString("Log_AllPlcs", "All controllers")));
            _plc.SelectedIndex = 0;
            _tips.SetToolTip(_plc, LanguageManager.GetString("Log_PlcTip",
                "Only the entries about this controller: its writes, its connection losses and returns."));
            _plc.SelectedIndexChanged += (s, e) => { if (!_setting) ApplyFilter(true); };

            _kind.DropDownStyle = ComboBoxStyle.DropDownList;
            _kind.Width = 150;
            _kind.Items.Add(new Choice(null, LanguageManager.GetString("Log_KindAll", "All kinds")));
            foreach (AuditKind k in new[] { AuditKind.SignIn, AuditKind.Connection, AuditKind.PlcWrite,
                                            AuditKind.Problem, AuditKind.Settings, AuditKind.Other })
                _kind.Items.Add(new Choice(k, KindName(k)));
            _kind.SelectedIndex = 0;
            if (kind.HasValue)
                for (int i = 1; i < _kind.Items.Count; i++)
                    if ((AuditKind)((Choice)_kind.Items[i]).Value == kind.Value) _kind.SelectedIndex = i;
            _kind.SelectedIndexChanged += (s, e) => { if (!_setting) ApplyFilter(true); };

            _live.Text = LanguageManager.GetString("Log_Live", "Live");
            _live.AutoSize = true;
            _live.Checked = true;
            _live.Margin = new Padding(10, 8, 3, 3);
            _tips.SetToolTip(_live, LanguageManager.GetString("Log_LiveTip",
                "Adds new entries as they are written, while the period runs up to now."));
            _live.CheckedChanged += (s, e) => { if (_live.Checked) Reload(); };

            Func<string, Label> caption = t => new Label
            {
                Text = t, AutoSize = true, Margin = new Padding(8, 9, 2, 3), ForeColor = Color.FromArgb(90, 96, 106)
            };
            // Each caption travels WITH its field: when the bar wraps (a narrow
            // screen, the magnifier) a label is never left at the end of one line
            // with its box at the start of the next.
            bar.Controls.Add(Pair(caption(LanguageManager.GetString("Log_Period", "Period")), _period));
            bar.Controls.Add(Pair(caption(LanguageManager.GetString("Log_From", "From")), _from));
            bar.Controls.Add(Pair(caption(LanguageManager.GetString("Log_To", "To")), _to));
            bar.Controls.Add(Pair(caption("\uD83D\uDD0D"), _search));
            bar.Controls.Add(Pair(caption(LanguageManager.GetString("Log_User", "User")), _user));
            bar.Controls.Add(Pair(caption(LanguageManager.GetString("Log_Plc", "PLC")), _plc));
            bar.Controls.Add(Pair(caption(LanguageManager.GetString("Log_Kind", "Kind")), _kind));
            bar.Controls.Add(_live);
            var clear = UiStyle.ToolButton("✖", LanguageManager.GetString("Log_ClearTip", "Clear the search, user and kind"),
                                           _tips, (s, e) => ClearFilters());
            clear.Margin = new Padding(8, 1, 3, 1);
            bar.Controls.Add(clear);
            var export = UiStyle.ToolButton("💾", LanguageManager.GetString("Log_ExportTip", "Export the entries shown"),
                                            _tips, (s, e) => Export());
            export.Margin = new Padding(3, 1, 3, 1);
            bar.Controls.Add(export);

            // ---- the table --------------------------------------------------
            _grid.Dock = DockStyle.Fill;
            _grid.VirtualMode = true;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.BackgroundColor = Color.White;
            _grid.BorderStyle = BorderStyle.FixedSingle;
            _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _grid.GridColor = Color.FromArgb(228, 231, 236);
            _grid.RowTemplate.Height = 26;
            _grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 2, 4, 5);
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            _grid.EnableHeadersVisualStyles = false;
            _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(242, 244, 247);
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(214, 228, 248);
            _grid.DefaultCellStyle.SelectionForeColor = Color.Black;
            _grid.Columns.Add(Column("Time", LanguageManager.GetString("Log_ColTime", "Time") + "  ▼", 160));
            _grid.Columns.Add(Column("User", LanguageManager.GetString("Log_ColUser", "User"), 110));
            _grid.Columns.Add(Column("Plc", LanguageManager.GetString("Log_ColPlc", "PLC"), 50));
            _grid.Columns.Add(Column("Kind", LanguageManager.GetString("Log_ColKind", "Kind"), 120));
            var entry = Column("Entry", LanguageManager.GetString("Log_ColEntry", "Entry"), 400);
            entry.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns.Add(entry);
            _grid.CellValueNeeded += OnCellValueNeeded;
            _grid.CellFormatting += OnCellFormatting;
            _grid.SelectionChanged += (s, e) => ShowDetail();
            _grid.ColumnHeaderMouseClick += (s, e) =>
            {
                if (e.ColumnIndex != 0) return;
                var keep = Selected();
                _newestFirst = !_newestFirst;
                _grid.Columns[0].HeaderText = LanguageManager.GetString("Log_ColTime", "Time") +
                                              (_newestFirst ? "  ▼" : "  ▲");
                Fill(keep, null);
            };

            _detail.Dock = DockStyle.Bottom;
            _detail.Height = 70;
            _detail.Multiline = true;
            _detail.ReadOnly = true;
            _detail.ScrollBars = ScrollBars.Vertical;
            _detail.BackColor = Color.FromArgb(248, 249, 251);
            _detail.Font = new Font("Consolas", 9F);

            _status.Dock = DockStyle.Bottom;
            _status.Height = 24;
            _status.Padding = new Padding(2, 5, 0, 0);
            _status.ForeColor = Color.FromArgb(90, 96, 106);

            var gap = new Panel { Dock = DockStyle.Bottom, Height = 4, BackColor = Color.White };
            Controls.Add(_grid);
            Controls.Add(gap);
            Controls.Add(_detail);
            Controls.Add(_status);
            Controls.Add(bar);

            SelectPeriod(period, true);
            _period.SelectedIndexChanged += (s, e) =>
            {
                if (_setting) return;
                SelectPeriod((Period)((Choice)_period.SelectedItem).Value, true);
                Reload();
            };

            _refresh.Tick += (s, e) => { if (_live.Checked && FollowsNow) Reload(); };
            VisibleChanged += (s, e) => { _refresh.Enabled = Visible; if (Visible) Reload(); };
            Reload();
        }

        // ------------------------------------------------------------------

        /// <summary>The translated name of a kind - the Kind column and the filter.</summary>
        public static string KindName(AuditKind k)
        {
            switch (k)
            {
                case AuditKind.SignIn: return LanguageManager.GetString("Log_KindSignIn", "Sign-in, users");
                case AuditKind.Connection: return LanguageManager.GetString("Log_KindConnection", "Connection");
                case AuditKind.PlcWrite: return LanguageManager.GetString("Log_KindPlcWrite", "PLC writes");
                case AuditKind.Problem: return LanguageManager.GetString("Log_KindProblem", "Problems");
                case AuditKind.Settings: return LanguageManager.GetString("Log_KindSettings", "Settings");
                default: return LanguageManager.GetString("Log_KindOther", "Other");
            }
        }

        private static Control Pair(Label caption, Control field)
        {
            field.Margin = new Padding(2, 5, 3, 3);
            var p = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0),
                Padding = new Padding(0),
                BackColor = Color.White
            };
            p.Controls.Add(caption);
            p.Controls.Add(field);
            return p;
        }

        private static DataGridViewTextBoxColumn Column(string name, string header, int width)
        {
            return new DataGridViewTextBoxColumn
            {
                Name = name,
                HeaderText = header,
                Width = width,
                SortMode = DataGridViewColumnSortMode.NotSortable,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None
            };
        }

        /// <summary>True when the chosen period runs up to now - only then is there anything to follow.</summary>
        private bool FollowsNow
        {
            get
            {
                var p = CurrentPeriod;
                return p != Period.Yesterday && p != Period.Custom;
            }
        }

        public Period CurrentPeriod
        {
            get { return _period.SelectedItem == null ? Period.Today : (Period)((Choice)_period.SelectedItem).Value; }
        }

        /// <summary>From and To of a period, as of now.</summary>
        public static void Range(Period p, DateTime now, out DateTime from, out DateTime to)
        {
            to = now;
            switch (p)
            {
                case Period.LastHour: from = now.AddHours(-1); break;
                case Period.Yesterday: from = now.Date.AddDays(-1); to = now.Date.AddSeconds(-1); break;
                case Period.Last7Days: from = now.Date.AddDays(-6); break;
                case Period.Last30Days: from = now.Date.AddDays(-29); break;
                case Period.AllKept: from = AuditLogReader.OldestDay() ?? now.Date; break;
                default: from = now.Date; break;       // Today
            }
        }

        private void SelectPeriod(Period p, bool setPickers)
        {
            _setting = true;
            try
            {
                for (int i = 0; i < _period.Items.Count; i++)
                    if ((Period)((Choice)_period.Items[i]).Value == p) _period.SelectedIndex = i;
                if (setPickers && p != Period.Custom)
                {
                    DateTime from, to;
                    Range(p, DateTime.Now, out from, out to);
                    _from.Value = Clamp(from);
                    _to.Value = Clamp(to);
                }
            }
            finally { _setting = false; }
        }

        private DateTime Clamp(DateTime d)
        {
            if (d < _from.MinDate) return _from.MinDate;
            if (d > _from.MaxDate) return _from.MaxDate;
            return d;
        }

        /// <summary>Reads the period again and filters it.</summary>
        public void Reload()
        {
            DateTime from, to;
            var p = CurrentPeriod;
            if (p == Period.Custom)
            {
                from = _from.Value;
                // To is a minute: 14:05 means up to 14:05:59.
                to = _to.Value.AddSeconds(59 - _to.Value.Second);
            }
            else
            {
                Range(p, DateTime.Now, out from, out to);
                _setting = true;
                try { _from.Value = Clamp(from); _to.Value = Clamp(to); }
                finally { _setting = false; }
            }

            var read = AuditLogReader.Read(from, to);
            bool same = read.Count == _inPeriod.Count &&
                        (read.Count == 0 || ReferenceEquals(read[read.Count - 1], _inPeriod[_inPeriod.Count - 1]));
            _inPeriod = read;
            RefreshUsers();
            RefreshPlcs();
            if (!same) ApplyFilter(false);
            else UpdateStatus();
        }

        private void RefreshUsers()
        {
            var users = _inPeriod.Select(e => e.User).Where(u => !string.IsNullOrEmpty(u))
                                 .Distinct(StringComparer.OrdinalIgnoreCase)
                                 .OrderBy(u => u, StringComparer.OrdinalIgnoreCase).ToList();
            string chosen = _user.SelectedIndex > 0 ? Convert.ToString(_user.SelectedItem) : null;
            // The chosen user stays in the list even when the period no longer has them.
            if (chosen != null && !users.Contains(chosen, StringComparer.OrdinalIgnoreCase)) users.Add(chosen);
            var now = _user.Items.Cast<object>().Skip(1).Select(Convert.ToString).ToList();
            if (now.SequenceEqual(users)) return;
            _setting = true;
            try
            {
                _user.BeginUpdate();
                _user.Items.Clear();
                _user.Items.Add(_allUsers);
                foreach (var u in users) _user.Items.Add(u);
                int at = chosen == null ? 0 : _user.Items.IndexOf(chosen);
                _user.SelectedIndex = at < 0 ? 0 : at;
                _user.EndUpdate();
            }
            finally { _setting = false; }
        }

        /// <summary>The controllers of this program - and any other the period's entries name.</summary>
        private void RefreshPlcs()
        {
            var names = new SortedDictionary<int, string>();
            try
            {
                foreach (var ep in PlcConnectionSettings.Endpoints())
                    if (!names.ContainsKey(ep.StationId)) names[ep.StationId] = Kaban.UI.Helpers.PlcEndpointNaming.Caption(ep);
            }
            catch { }
            foreach (var st in _inPeriod.Where(e => e.Station.HasValue).Select(e => e.Station.Value).Distinct())
                if (!names.ContainsKey(st)) names[st] = "";
            var chosen = CurrentStation;
            if (chosen.HasValue && !names.ContainsKey(chosen.Value)) names[chosen.Value] = "";
            var want = names.Select(kv => "PLC " + kv.Key + (kv.Value.Length > 0 ? " - " + kv.Value : "")).ToList();
            var now = _plc.Items.Cast<object>().Skip(1).Select(Convert.ToString).ToList();
            if (now.SequenceEqual(want)) return;
            _setting = true;
            try
            {
                _plc.BeginUpdate();
                while (_plc.Items.Count > 1) _plc.Items.RemoveAt(1);
                int i = 0;
                foreach (var kv in names) _plc.Items.Add(new Choice(kv.Key, want[i++]));
                _plc.SelectedIndex = 0;
                if (chosen.HasValue)
                    for (int k = 1; k < _plc.Items.Count; k++)
                        if ((int)((Choice)_plc.Items[k]).Value == chosen.Value) _plc.SelectedIndex = k;
                _plc.EndUpdate();
            }
            finally { _setting = false; }
        }

        private int? CurrentStation
        {
            get
            {
                var c = _plc.SelectedItem as Choice;
                return c == null || c.Value == null ? (int?)null : (int)c.Value;
            }
        }

        /// <summary>The entries of the period that pass the search, user, controller and kind.</summary>
        public List<AuditEntry> Filtered()
        {
            string search = _search.Text;
            string user = _user.SelectedIndex > 0 ? Convert.ToString(_user.SelectedItem) : null;
            var kindChoice = _kind.SelectedItem as Choice;
            AuditKind? kind = kindChoice == null || kindChoice.Value == null ? (AuditKind?)null : (AuditKind)kindChoice.Value;
            int? station = CurrentStation;
            return _inPeriod.Where(e =>
                    (user == null || string.Equals(e.User, user, StringComparison.OrdinalIgnoreCase)) &&
                    (!station.HasValue || e.Station == station) &&
                    (!kind.HasValue || e.Kind == kind.Value) &&
                    AuditLogReader.Matches(e, search)).ToList();
        }

        private void ApplyFilter(bool userAction)
        {
            var keepSelected = Selected();
            var keepTop = TopEntry();
            var list = Filtered();
            _total = list.Count;
            if (list.Count > MaxRows) list = list.GetRange(list.Count - MaxRows, MaxRows);
            _shown = list;
            Fill(keepSelected, userAction ? null : keepTop);
        }

        private int _total;

        private AuditEntry EntryAt(int row)
        {
            if (row < 0 || row >= _shown.Count) return null;
            return _newestFirst ? _shown[_shown.Count - 1 - row] : _shown[row];
        }

        private int RowOf(AuditEntry e)
        {
            if (e == null) return -1;
            int i = _shown.IndexOf(e);
            if (i < 0) return -1;
            return _newestFirst ? _shown.Count - 1 - i : i;
        }

        private AuditEntry Selected()
        {
            return _grid.SelectedRows.Count > 0 ? EntryAt(_grid.SelectedRows[0].Index) : null;
        }

        private AuditEntry TopEntry()
        {
            try { return _grid.RowCount > 0 ? EntryAt(_grid.FirstDisplayedScrollingRowIndex) : null; }
            catch { return null; }
        }

        /// <summary>
        /// Puts the list in the table, keeping the selected entry selected and -
        /// on a live refresh - the entry at the top of the view at the top, so the
        /// row being read does not slide away as new entries arrive.
        /// </summary>
        private void Fill(AuditEntry select, AuditEntry top)
        {
            _grid.RowCount = 0;
            _grid.RowCount = _shown.Count;
            int sel = RowOf(select);
            _grid.ClearSelection();
            if (sel >= 0)
            {
                _grid.Rows[sel].Selected = true;
                try { _grid.CurrentCell = _grid.Rows[sel].Cells[0]; } catch { }
            }
            int topRow = RowOf(top);
            if (topRow >= 0)
            {
                try { _grid.FirstDisplayedScrollingRowIndex = topRow; } catch { }
            }
            else if (sel < 0 && _shown.Count > 0)
            {
                // Nothing to keep: start where the newest entry is.
                try { _grid.FirstDisplayedScrollingRowIndex = _newestFirst ? 0 : _shown.Count - 1; } catch { }
            }
            _grid.Invalidate();
            ShowDetail();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            string text = string.Format(LanguageManager.GetString("Log_Status", "{0} entries shown, {1} in the period"),
                                        _total, _inPeriod.Count);
            if (_total > _shown.Count)
                text += "  -  " + string.Format(LanguageManager.GetString("Log_StatusCapped",
                    "the newest {0} are listed: narrow the period or the search"), _shown.Count);
            _status.Text = text;
        }

        private void ShowDetail()
        {
            var e = Selected();
            _detail.Text = e == null ? "" :
                (e.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "   " + e.User + "   " +
                 (e.Station.HasValue ? "PLC " + e.Station.Value + "   " : "") + KindName(e.Kind) + Environment.NewLine + e.Text.Replace("\n", Environment.NewLine));
        }

        private void OnCellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            var entry = EntryAt(e.RowIndex);
            if (entry == null) return;
            switch (e.ColumnIndex)
            {
                case 0: e.Value = entry.Time.ToString("yyyy-MM-dd  HH:mm:ss", CultureInfo.InvariantCulture); break;
                case 1: e.Value = entry.User; break;
                case 2: e.Value = entry.Station.HasValue ? entry.Station.Value.ToString(CultureInfo.InvariantCulture) : ""; break;
                case 3: e.Value = KindName(entry.Kind); break;
                default:
                    int nl = entry.Text.IndexOf('\n');
                    e.Value = nl < 0 ? entry.Text : entry.Text.Substring(0, nl) + "  …";
                    break;
            }
        }

        private void OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            var entry = EntryAt(e.RowIndex);
            if (entry == null) return;
            if (entry.Kind == AuditKind.Problem) e.CellStyle.ForeColor = Color.FromArgb(170, 30, 30);
            else if (entry.Kind == AuditKind.Connection) e.CellStyle.ForeColor = Color.FromArgb(20, 80, 150);
            else if (e.ColumnIndex == 3) e.CellStyle.ForeColor = Color.FromArgb(90, 96, 106);
        }

        private void ClearFilters()
        {
            _setting = true;
            try
            {
                _search.Text = "";
                _user.SelectedIndex = 0;
                _plc.SelectedIndex = 0;
                _kind.SelectedIndex = 0;
            }
            finally { _setting = false; }
            _typing.Stop();
            ApplyFilter(true);
        }

        /// <summary>
        /// Writes the entries passing the filters - oldest first, as a log reads -
        /// as a log file (.txt) or a table for a spreadsheet (.csv).
        /// </summary>
        private void Export()
        {
            var list = Filtered();
            if (list.Count == 0)
            {
                _status.Text = LanguageManager.GetString("Log_NothingToExport", "Nothing to export: no entry passes the filters.");
                return;
            }
            using (var dlg = new SaveFileDialog
            {
                Filter = LanguageManager.GetString("Log_FilterTxt", "Log file") + " (*.txt)|*.txt|CSV (*.csv)|*.csv",
                FileName = "KabanHMI-log-" + DateTime.Now.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture) + ".txt"
            })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(dlg.FileName, ExportText(list, dlg.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)),
                                      new UTF8Encoding(true));
                    _status.Text = string.Format(LanguageManager.GetString("Log_Exported", "Exported {0} entries: {1}"),
                                                 list.Count, dlg.FileName);
                    AuditLogger.Log("exported " + list.Count + " log entries to '" + dlg.FileName + "'.");
                }
                catch (Exception ex)
                {
                    _status.Text = string.Format(LanguageManager.GetString("Log_ExportFailed", "Export failed: {0}"), ex.Message);
                }
            }
        }

        /// <summary>The export's content - public for the tests.</summary>
        public static string ExportText(IEnumerable<AuditEntry> entries, bool csv)
        {
            var sb = new StringBuilder();
            if (csv) sb.Append("Time;User;PLC;Kind;Entry\r\n");
            foreach (var e in entries)
            {
                if (csv)
                    sb.Append(e.Time.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(';')
                      .Append(Csv(e.User)).Append(';')
                      .Append(e.Station.HasValue ? e.Station.Value.ToString(CultureInfo.InvariantCulture) : "").Append(';')
                      .Append(Csv(KindName(e.Kind))).Append(';')
                      .Append(Csv(e.Text)).Append("\r\n");
                else
                    sb.Append(e.ToLogLine().Replace("\n", "\r\n")).Append("\r\n");
            }
            return sb.ToString();
        }

        private static string Csv(string s)
        {
            s = s ?? "";
            return s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) < 0 ? s : "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        // ---- for the tests ------------------------------------------------
        public int RowCount { get { return _grid.RowCount; } }
        public string CellText(int row, int col)
        {
            var e = new DataGridViewCellValueEventArgs(col, row);
            OnCellValueNeeded(this, e);
            return Convert.ToString(e.Value);
        }
        public string SearchText { get { return _search.Text; } set { _search.Text = value; _typing.Stop(); ApplyFilter(true); } }
        public void SetKind(AuditKind? k)
        {
            for (int i = 0; i < _kind.Items.Count; i++)
            {
                var v = ((Choice)_kind.Items[i]).Value;
                if ((v == null && !k.HasValue) || (v != null && k.HasValue && (AuditKind)v == k.Value)) _kind.SelectedIndex = i;
            }
        }
        public void SetStation(int? station)
        {
            _plc.SelectedIndex = 0;
            if (station.HasValue)
                for (int k = 1; k < _plc.Items.Count; k++)
                    if ((int)((Choice)_plc.Items[k]).Value == station.Value) _plc.SelectedIndex = k;
        }
        public List<string> PlcChoices { get { return _plc.Items.Cast<object>().Select(Convert.ToString).ToList(); } }

        public void SetUser(string user)
        {
            int i = user == null ? 0 : _user.Items.IndexOf(user);
            _user.SelectedIndex = i < 0 ? 0 : i;
        }
        public void SetPeriod(Period p) { SelectPeriod(p, true); Reload(); }
        public void SetCustom(DateTime from, DateTime to)
        {
            _from.Value = from;      // each switches the period to "From - to" and reloads
            _to.Value = to;
        }
        public string DetailText { get { return _detail.Text; } }
        public string StatusText { get { return _status.Text; } }
        public void SelectRow(int row) { _grid.ClearSelection(); if (row >= 0 && row < _grid.RowCount) _grid.Rows[row].Selected = true; ShowDetail(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refresh.Stop(); _refresh.Dispose();
                _typing.Stop(); _typing.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
