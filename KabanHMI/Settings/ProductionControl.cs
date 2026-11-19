using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// HMI SETTINGS > PRODUCTION (19 November - the old program's Machine Logs: Customer Report and General Information): the
    /// production log between two days - the bars sent and finished, the pieces cut, the leftovers - filtered by machine, event and a
    /// text (a customer, an order, a profile, a job), its sums (bars, pieces, metres, working time) and the machines' counters
    /// (power-on and working time). Export: the lines shown as a CSV file.
    /// </summary>
    public sealed class ProductionControl : UserControl, IRebuildForLanguage
    {
        object IRebuildForLanguage.Place { get { return null; } }
        void IRebuildForLanguage.ReturnTo(object place) { }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        private readonly DateTimePicker _from = new DateTimePicker { Name = "prdFrom", Format = DateTimePickerFormat.Short, Width = 120 };
        private readonly DateTimePicker _to = new DateTimePicker { Name = "prdTo", Format = DateTimePickerFormat.Short, Width = 120 };
        private readonly ComboBox _machine = new ComboBox { Name = "prdMachine", DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
        private readonly ComboBox _kind = new ComboBox { Name = "prdKind", DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        private readonly TextBox _search = new TextBox { Name = "prdSearch", Width = 180 };
        private readonly Label _sums = new Label { Name = "prdSums", AutoSize = false, Height = 22, Dock = DockStyle.Top, Padding = new Padding(8, 3, 8, 0) };
        private readonly Label _counters = new Label { Name = "prdCounters", AutoSize = false, Height = 22, Dock = DockStyle.Top, Padding = new Padding(8, 3, 8, 0), ForeColor = Color.FromArgb(70, 80, 95) };
        private readonly DataGridView _grid = new DataGridView { Name = "prdGrid" };
        private readonly Timer _clock = new Timer { Interval = 5000 };
        private readonly Action<ProductionEntry> _onAdded;
        private List<ProductionEntry> _shown = new List<ProductionEntry>();

        public ProductionControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = UiStyle.Body;
            var tips = UiStyle.Tips();

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false, Padding = new Padding(4, 6, 0, 0), BackColor = Color.White };
            Func<string, Label> cap = t => new Label { Text = t, AutoSize = true, Margin = new Padding(8, 5, 4, 0) };
            _from.Value = DateTime.Today.AddDays(-7);
            _to.Value = DateTime.Today;
            _machine.Items.Add(T("Prd_AllMachines", "every machine"));
            foreach (var m in Kaban.UI.Helpers.MachineLinks.All()) if (!_machine.Items.Contains(m.ModuleId)) _machine.Items.Add(m.ModuleId);
            if (!_machine.Items.Contains("FA2010")) _machine.Items.Add("FA2010");
            _machine.SelectedIndex = 0;
            _kind.Items.Add(T("Prd_AllEvents", "every event"));
            foreach (ProductionKind k in Enum.GetValues(typeof(ProductionKind))) _kind.Items.Add(KindText(k));
            _kind.SelectedIndex = 0;
            var refresh = UiStyle.ToolButton("🔄", T("Prd_Refresh", "Read the log again"), tips, (s, e) => Fill());
            var export = UiStyle.ToolButton("📄", T("Prd_Export", "The lines shown as a CSV file (Excel)"), tips, (s, e) => Export());
            refresh.Name = "prdRefresh"; export.Name = "prdExport";
            bar.Controls.Add(cap(T("Prd_From", "From")));
            bar.Controls.Add(_from);
            bar.Controls.Add(cap(T("Prd_To", "to")));
            bar.Controls.Add(_to);
            bar.Controls.Add(_machine);
            bar.Controls.Add(_kind);
            bar.Controls.Add(cap(T("Prd_Search", "Find")));
            bar.Controls.Add(_search);
            foreach (var b in new Control[] { refresh, export }) { b.Margin = new Padding(4, 0, 2, 0); bar.Controls.Add(b); }
            foreach (Control c in new Control[] { _from, _to, _machine, _kind }) c.Margin = new Padding(2, 3, 2, 0);
            _search.Margin = new Padding(2, 3, 2, 0);
            UiStyle.SetTip(tips, _search, T("Prd_TipSearch", "A customer, an order, a profile, a job or a user"));

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.BackgroundColor = Color.White;
            UiStyle.GridLook(_grid);
            foreach (var c in new[]
            {
                new[] { "at", T("Prd_At", "Time"), "13" }, new[] { "machine", T("Prd_Machine", "Machine"), "7" }, new[] { "event", T("Prd_Event", "Event"), "10" },
                new[] { "job", T("Prd_Job", "Job"), "12" }, new[] { "bar", T("Prd_Bar", "Bar"), "5" }, new[] { "profile", T("Prd_Profile", "Profile"), "10" },
                new[] { "length", T("Prd_Length", "Length mm"), "7" }, new[] { "pieces", T("Prd_Pieces", "Pieces"), "5" }, new[] { "time", T("Prd_Time", "Time taken"), "7" },
                new[] { "customer", T("Prd_Customer", "Customer"), "11" }, new[] { "order", T("Prd_Order", "Order"), "9" }, new[] { "user", T("Prd_User", "User"), "7" },
                new[] { "note", T("Prd_Note", "Note"), "14" }
            })
                _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = c[0], HeaderText = c[1], FillWeight = float.Parse(c[2], CI), SortMode = DataGridViewColumnSortMode.Automatic });

            _sums.Font = new Font(UiStyle.Body, FontStyle.Bold);
            Controls.Add(_grid);
            Controls.Add(_counters);
            Controls.Add(_sums);
            Controls.Add(bar);

            EventHandler again = (s, e) => Fill();
            _from.ValueChanged += again; _to.ValueChanged += again;
            _machine.SelectedIndexChanged += again; _kind.SelectedIndexChanged += again;
            _search.TextChanged += again;
            _onAdded = e => { try { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)Fill); } catch { } };
            ProductionLog.Added += _onAdded;
            _clock.Tick += (s, e) => ShowCounters();
            VisibleChanged += (s, e) => { if (Visible) { _clock.Start(); ShowCounters(); } else _clock.Stop(); };
            Disposed += (s, e) => { ProductionLog.Added -= _onAdded; _clock.Dispose(); };
            Fill();
            ShowCounters();
        }

        internal static string KindText(ProductionKind k)
        {
            switch (k)
            {
                case ProductionKind.BarSent: return T("Prd_KindSent", "bar sent");
                case ProductionKind.BarFinished: return T("Prd_KindFinished", "bar finished");
                case ProductionKind.Piece: return T("Prd_KindPiece", "piece cut");
                case ProductionKind.Leftover: return T("Prd_KindLeftover", "leftover");
                case ProductionKind.RunStarted: return T("Prd_KindRunStarted", "automatic run started");
                default: return T("Prd_KindRunEnded", "automatic run ended");
            }
        }

        /// <summary>The lines of the days chosen that pass the filters.</summary>
        private List<ProductionEntry> Filtered()
        {
            var from = _from.Value.Date;
            var to = _to.Value.Date < from ? from : _to.Value.Date;
            var all = ProductionLog.Read(from, to);
            string machine = _machine.SelectedIndex > 0 ? _machine.SelectedItem.ToString() : "";
            var kinds = Enum.GetValues(typeof(ProductionKind)).Cast<ProductionKind>().ToList();
            ProductionKind? kind = _kind.SelectedIndex > 0 ? kinds[_kind.SelectedIndex - 1] : (ProductionKind?)null;
            string q = _search.Text.Trim();
            return all.Where(e => (machine.Length == 0 || string.Equals(e.Machine, machine, StringComparison.OrdinalIgnoreCase)) &&
                                  (!kind.HasValue || e.Kind == kind.Value) &&
                                  (q.Length == 0 || new[] { e.Job, e.Bar, e.Profile, e.Customer, e.Order, e.User, e.Note }
                                       .Any(x => (x ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)))
                      .OrderByDescending(e => e.At).ToList();
        }

        private void Fill()
        {
            if (IsDisposed) return;
            _shown = Filtered();
            _grid.SuspendLayout();
            try
            {
                _grid.Rows.Clear();
                foreach (var e in _shown.Take(5000))
                    _grid.Rows.Add(e.At.ToString("yyyy-MM-dd HH:mm:ss", CI), e.Machine, KindText(e.Kind), e.Job, e.Bar, e.Profile,
                                   e.LengthMm > 0 ? e.LengthMm.ToString("0.#", CI) : "", e.Pieces > 0 ? e.Pieces.ToString(CI) : "",
                                   e.Seconds > 0 ? TimeSpan.FromSeconds(e.Seconds).ToString(@"hh\:mm\:ss", CI) : "", e.Customer, e.Order, e.User,
                                   (e.Trial ? T("Job_Trial", "Trial") + " " : "") + (e.Automatic ? T("Job_Automatic", "Automatic") + " " : "") + e.Note);
            }
            finally { _grid.ResumeLayout(); }
            var finished = _shown.Where(e => e.Kind == ProductionKind.BarFinished).ToList();
            int sent = _shown.Count(e => e.Kind == ProductionKind.BarSent);
            int pieces = _shown.Where(e => e.Kind == ProductionKind.Piece).Sum(e => Math.Max(1, e.Pieces));
            double metres = finished.Sum(e => e.LengthMm) / 1000.0;
            double seconds = finished.Sum(e => e.Seconds);
            _sums.Text = string.Format(T("Prd_Sums", "Bars sent {0}, finished {1} ({2:0.##} m, {3}) - pieces cut {4} - leftovers {5}"),
                                       sent, finished.Count, metres, ProductionLog.Hours(seconds), pieces, _shown.Count(e => e.Kind == ProductionKind.Leftover));
        }

        private void ShowCounters()
        {
            if (IsDisposed) return;
            var parts = new List<string>();
            foreach (var g in ProductionLog.Counters.GroupBy(c => c.Machine))
            {
                var vals = new List<string>();
                foreach (var c in g)
                {
                    var v = ProductionLog.ValueOf(c);
                    vals.Add(T(c.Key, c.Text) + " " + (v.HasValue ? (c.Seconds ? ProductionLog.Hours(v.Value) : v.Value.ToString(CI)) : "-"));
                }
                parts.Add(g.Key + ": " + string.Join(", ", vals));
            }
            _counters.Text = parts.Count > 0 ? string.Join("   |   ", parts) : T("Prd_NoCounters", "No machine of this panel gives its counters.");
        }

        private void Export()
        {
            using (var d = new SaveFileDialog { FileName = "Production_" + _from.Value.ToString("yyyy-MM-dd", CI) + "_" + _to.Value.ToString("yyyy-MM-dd", CI) + ".csv",
                                                Filter = "CSV (*.csv)|*.csv" })
            {
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                try { ProductionLog.Export(_shown.OrderBy(e => e.At), d.FileName); }
                catch (Exception ex) { MessageBox.Show(FindForm(), ex.Message, T("Tab_Production", "Production"), MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
        }

        /// <summary>The page's parts (tests).</summary>
        public DataGridView Grid { get { return _grid; } }
        public string Sums { get { return _sums.Text; } }
        public string CountersText { get { return _counters.Text; } }
        public TextBox SearchBox { get { return _search; } }
        public DateTimePicker From { get { return _from; } }
        public void Refill() { Fill(); ShowCounters(); }
    }

    /// <summary>HMI Settings > Production: everyone's to read (an Operator's level).</summary>
    public class ProductionPage : ISettingsPage
    {
        public string TitleKey { get { return "Tab_Production"; } }
        public string TitleFallback { get { return "Production"; } }
        public SecurityLevel RequiredLevel { get { return SecurityLevel.Operator; } }
        public UserControl GetControl() { return new ProductionControl(); }
    }
}
