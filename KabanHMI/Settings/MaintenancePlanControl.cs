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
    /// HMI SETTINGS > MAINTENANCE (17 November - the old program's Cleaning Settings, for any task): a row a task - its name, its
    /// machine, every so many days and / or every so many counts of a PLC counter, when it was done last, what is left. A task due
    /// is on the Alarms page as a warning; Done marks the selected one done now (an Operator may; the plan itself is a Maintenance
    /// user's to change).
    /// </summary>
    public sealed class MaintenancePlanControl : UserControl, IRebuildForLanguage
    {
        object IRebuildForLanguage.Place { get { return null; } }
        void IRebuildForLanguage.ReturnTo(object place) { }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        private static bool CanEdit { get { return SecurityManager.HasAccess(SecurityLevel.Maintenance); } }

        private readonly DataGridView _grid = new DataGridView { Name = "maintGrid" };
        private readonly Label _status = new Label { Name = "maintStatus", AutoSize = true };
        private readonly DirtyTracker _dirty = new DirtyTracker();
        private readonly Button _add, _remove, _done, _save, _revert;
        private readonly Timer _clock = new Timer { Interval = 5000 };
        private readonly EventHandler _onChanged;
        private bool _filling;

        public MaintenancePlanControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Font = UiStyle.Body;
            var tips = UiStyle.Tips();

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false, Padding = new Padding(4, 4, 0, 0), BackColor = Color.White };
            _save = UiStyle.ToolButton("💾", T("Mnt_Save", "Keep the maintenance plan"), tips, (s, e) => Keep());
            _revert = UiStyle.ToolButton("↺", T("Mnt_Revert", "Throw the changes away and show the kept plan"), tips, (s, e) => Fill());
            _add = UiStyle.ToolButton("➕", T("Mnt_Add", "Add a task"), tips, (s, e) => AddTask());
            _remove = UiStyle.ToolButton("🗑", T("Mnt_Remove", "Remove the selected task"), tips, (s, e) => RemoveTask());
            _done = UiStyle.TextButton("✔ " + T("Mnt_Done", "Done"), 0, (s, e) => MarkDone());
            _done.Name = "maintDone"; _save.Name = "maintSave"; _add.Name = "maintAdd"; _remove.Name = "maintRemove";
            UiStyle.SetTip(tips, _done, T("Mnt_TipDone", "The selected task done now: its date and counter kept, its reminder off"));
            foreach (var b in new Control[] { _save, _revert, _add, _remove, _done }) { b.Margin = new Padding(2, 0, 2, 0); bar.Controls.Add(b); }
            _status.Margin = new Padding(14, 9, 0, 0);
            bar.Controls.Add(_status);
            _dirty.Guard(_save, _revert);

            var note = new Label
            {
                Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 4, 8, 0), ForeColor = Color.FromArgb(110, 116, 124), Font = UiStyle.Small,
                Text = T("Mnt_Note", "A task is due every so many days and / or every so many counts of a PLC counter (D500 - its two words, or one). Due: a warning on the Alarms page until it is done - acknowledged, it comes back after 4 hours.")
            };

            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.MultiSelect = false;
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.BackgroundColor = Color.White;
            UiStyle.GridLook(_grid);
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "on", HeaderText = T("Mnt_On", "On"), FillWeight = 5 });
            _grid.Columns.Add(Col("name", T("Mnt_Task", "Task"), 22));
            var machine = new DataGridViewComboBoxColumn { Name = "machine", HeaderText = T("Mnt_Machine", "Machine"), FillWeight = 11, FlatStyle = FlatStyle.Flat };
            machine.Items.Add("");
            foreach (var m in Kaban.UI.Helpers.MachineLinks.All()) if (!machine.Items.Contains(m.ModuleId)) machine.Items.Add(m.ModuleId);
            _grid.Columns.Add(machine);
            _grid.Columns.Add(Col("days", T("Mnt_EveryDays", "Every days"), 8));
            _grid.Columns.Add(Col("counter", T("Mnt_Counter", "Counter"), 8));
            _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "dw", HeaderText = T("Mnt_TwoWords", "32-bit"), FillWeight = 6 });
            _grid.Columns.Add(Col("count", T("Mnt_EveryCount", "Every counts"), 9));
            var last = Col("last", T("Mnt_LastDone", "Done last"), 14); last.ReadOnly = true; _grid.Columns.Add(last);
            var left = Col("left", T("Mnt_Left", "Left"), 18); left.ReadOnly = true; _grid.Columns.Add(left);
            _grid.Columns.Add(Col("notes", T("Mnt_Notes", "Notes"), 18));
            _grid.CurrentCellDirtyStateChanged += (s, e) => { if (_grid.IsCurrentCellDirty && (_grid.CurrentCell is DataGridViewCheckBoxCell || _grid.CurrentCell is DataGridViewComboBoxCell)) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
            _grid.DataError += (s, e) => { e.ThrowException = false; };
            _grid.CellValueChanged += (s, e) => { if (!_filling) _dirty.MarkDirty(); };

            Controls.Add(_grid);
            Controls.Add(note);
            Controls.Add(bar);

            _onChanged = (s, e) => { try { if (IsHandleCreated) BeginInvoke((Action)(() => { if (!_dirty.IsDirty) Fill(); })); } catch { } };
            MaintenancePlan.Changed += _onChanged;
            _clock.Tick += (s, e) => ShowLeft();
            VisibleChanged += (s, e) => { if (Visible) _clock.Start(); else _clock.Stop(); };
            Disposed += (s, e) => { MaintenancePlan.Changed -= _onChanged; _clock.Dispose(); };
            Fill();
        }

        private static DataGridViewTextBoxColumn Col(string name, string head, float weight)
        {
            return new DataGridViewTextBoxColumn { Name = name, HeaderText = head, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable };
        }

        private void Fill()
        {
            _filling = true;
            try
            {
                _grid.Rows.Clear();
                foreach (var t in MaintenancePlan.All) AddRow(t);
            }
            finally { _filling = false; }
            _dirty.Reset();
            ApplyAccess();
            ShowLeft();
        }

        private void AddRow(MaintenanceTask t)
        {
            var items = ((DataGridViewComboBoxColumn)_grid.Columns["machine"]).Items;
            if (!items.Contains(t.Machine)) items.Add(t.Machine);
            int i = _grid.Rows.Add(t.Enabled, t.Name, t.Machine, t.EveryDays > 0 ? t.EveryDays.ToString(CI) : "", t.Counter, t.CounterDouble,
                                   t.EveryCount > 0 ? t.EveryCount.ToString(CI) : "", Last(t), "", t.Notes);
            _grid.Rows[i].Tag = t;
        }

        private static string Last(MaintenanceTask t)
        {
            return t.LastDone.ToString("dd.MM.yyyy HH:mm", CI) + (t.DoneBy.Length > 0 ? " (" + t.DoneBy + ")" : "");
        }

        private void ApplyAccess()
        {
            bool edit = CanEdit;
            foreach (DataGridViewColumn c in _grid.Columns) if (c.Name != "last" && c.Name != "left") c.ReadOnly = !edit;
            _add.Enabled = _remove.Enabled = edit;
        }

        /// <summary>What is left of each task, as the plan has it now (its counters read last).</summary>
        private void ShowLeft()
        {
            if (IsDisposed) return;
            var now = DateTime.Now;
            var kept = MaintenancePlan.All.ToDictionary(t => t.Id);
            bool was = _filling;
            _filling = true;      // what is left is shown, not a change of the plan
            try { ShowLeft(now, kept); }
            finally { _filling = was; }
        }

        private void ShowLeft(DateTime now, Dictionary<string, MaintenanceTask> kept)
        {
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var t = r.Tag as MaintenanceTask;
                MaintenanceTask k;
                if (t == null || !kept.TryGetValue(t.Id, out k)) continue;
                var text = MaintenancePlan.Describe(k, now);
                if (!Equals(r.Cells["left"].Value, text)) r.Cells["left"].Value = text;
                bool due = MaintenancePlan.IsDue(k, now);
                r.Cells["left"].Style.ForeColor = due ? Color.FromArgb(170, 40, 30) : Color.Empty;
                r.Cells["left"].Style.Font = due ? new Font(UiStyle.Body, FontStyle.Bold) : null;
            }
        }

        private static string Cell(DataGridViewRow r, string col) { var v = r.Cells[col].Value; return v == null ? "" : v.ToString().Trim(); }

        private bool Read(out List<MaintenanceTask> tasks, out string problem)
        {
            tasks = new List<MaintenanceTask>();
            problem = "";
            foreach (DataGridViewRow r in _grid.Rows)
            {
                var old = r.Tag as MaintenanceTask;
                var t = old != null ? old.Clone() : new MaintenanceTask();
                t.Enabled = Equals(r.Cells["on"].Value, true);
                t.Name = Cell(r, "name");
                t.Machine = Cell(r, "machine");
                t.Counter = Cell(r, "counter").ToUpperInvariant();
                t.CounterDouble = Equals(r.Cells["dw"].Value, true);
                t.Notes = Cell(r, "notes");
                int d; long c;
                t.EveryDays = int.TryParse(Cell(r, "days"), NumberStyles.Integer, CI, out d) && d > 0 ? d : 0;
                t.EveryCount = long.TryParse(Cell(r, "count"), NumberStyles.Integer, CI, out c) && c > 0 ? c : 0;
                if (t.Name.Length == 0) { problem = T("Mnt_NoName", "A task has no name."); return false; }
                if (t.EveryDays == 0 && t.EveryCount == 0) { problem = string.Format(T("Mnt_NoInterval", "{0}: every how many days or counts?"), t.Name); return false; }
                if (t.EveryCount > 0 && t.Counter.Length == 0) { problem = string.Format(T("Mnt_NoCounter2", "{0}: its counter (a PLC device: D500)?"), t.Name); return false; }
                tasks.Add(t);
            }
            return true;
        }

        private void Keep()
        {
            List<MaintenanceTask> tasks; string problem;
            if (!Read(out tasks, out problem)) { Say(problem, false); return; }
            MaintenancePlan.Save(tasks);
            Fill();
            Say(T("Mnt_Kept", "Kept."), true);
        }

        private void AddTask()
        {
            AddRow(new MaintenanceTask { Name = T("Mnt_NewTask", "Cleaning"), EveryDays = 7 });
            _grid.CurrentCell = _grid.Rows[_grid.Rows.Count - 1].Cells["name"];
            _dirty.MarkDirty();
        }

        private void RemoveTask()
        {
            if (_grid.CurrentCell == null) return;
            _grid.Rows.RemoveAt(_grid.CurrentCell.RowIndex);
            _dirty.MarkDirty();
        }

        private void MarkDone()
        {
            if (_grid.CurrentCell == null) { Say(T("Mnt_PickTask", "Pick the task done first."), false); return; }
            var t = _grid.Rows[_grid.CurrentCell.RowIndex].Tag as MaintenanceTask;
            if (t == null || _dirty.IsDirty) { Say(T("Mnt_KeepFirst", "Keep the plan first."), false); return; }
            MaintenancePlan.MarkDone(t.Id, SecurityManager.CurrentUser);
            Fill();
            Say(string.Format(T("Mnt_MarkedDone", "{0}: done."), t.Name), true);
        }

        private void Say(string text, bool ok)
        {
            _status.ForeColor = ok ? Color.FromArgb(30, 110, 50) : Color.FromArgb(160, 40, 40);
            _status.Text = text;
        }

        /// <summary>The page's parts (tests).</summary>
        public DataGridView Grid { get { return _grid; } }
        public string Status { get { return _status.Text; } }
        public void Save() { Keep(); }
        public void Done() { MarkDone(); }
        public void Add() { AddTask(); }
    }

    /// <summary>HMI Settings > Maintenance: an Operator's (Done); the plan a Maintenance user's to change.</summary>
    public class MaintenancePlanPage : ISettingsPage
    {
        public string TitleKey { get { return "Tab_Maintenance"; } }
        public string TitleFallback { get { return "Maintenance"; } }
        public SecurityLevel RequiredLevel { get { return SecurityLevel.Operator; } }
        public UserControl GetControl() { return new MaintenancePlanControl(); }
    }
}
