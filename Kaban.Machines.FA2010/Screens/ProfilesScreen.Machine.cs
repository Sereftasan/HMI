using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;
using Kaban.UI.Controls;

namespace Kaban.Machines.FA2010.Screens
{
    /// <summary>
    /// THE PROFILE'S MACHINE SETTINGS (6 November: the old program's profile parameters - its ucCompanyDetails, ProfileSeryProfileSetting -
    /// "they go to the PLC at R25600 - R25649", with R25615 Profile Correction Position). One row per member of the structured data type the
    /// label PC_ProfilParametreleri has (StructuredTypes.csv, "Profile Parameters"): the structure the PLC Monitor's tab HMI -> PLC shows and
    /// "Send to the machine" writes with the job.
    ///
    /// ONE TABLE (6 November, second answer: "The profile details window is not very responsive, it is very hard to set it's borders even" -
    /// fifty rows of three controls each were laid out again at every move of the pane's border), its columns: the parameter, the profile's
    /// value, the GROUP's (its template's - the group's reference profile, else its first: the old program's first profile of the group). A
    /// new profile takes the template's values as its own ("We should load the group parameter values to the profile when we create a new
    /// one"); a value that differs from the group's is in bold blue ("so the user could see the edited ones"); nothing set is 0.
    /// </summary>
    public partial class ProfilesScreen
    {
        /// <summary>The label of the profile's parameters in the PLC's label list (the structure and its address).</summary>
        internal const string ParametersLabel = "PC_ProfilParametreleri";
        internal const string ParametersType = "Profile Parameters";

        private TabPage _machineTab;
        private DataGridView _machineGrid;
        private Label _machineHead;
        private bool _fillingMachine;

        /// <summary>The structure of the profile's parameters (null: the machine's StructuredTypes.csv has none).</summary>
        internal static StructuredTypes.Type ParametersStructure()
        {
            var label = PlcLabelTable.ByLabel(1, ParametersLabel);
            return StructuredTypes.Find(label != null && !string.IsNullOrWhiteSpace(label.DataType) ? label.DataType : ParametersType, 1);
        }

        private void BuildMachineTab()
        {
            _machineTab = new TabPage(T("Prf_TabMachine", "Machine settings")) { BackColor = Color.White };
            _detailTabs.TabPages.Add(_machineTab);
            _machineHead = new Label { Dock = DockStyle.Top, Height = 40, Font = UiStyle.Small, ForeColor = Color.FromArgb(70, 80, 95), Padding = new Padding(8, 4, 8, 0) };
            _machineGrid = new DataGridView
            {
                Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.CellSelect, MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, EditMode = DataGridViewEditMode.EditOnEnter
            };
            UiStyle.StyleGrid(_machineGrid);
            _machineGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = T("Prf_ColParameter", "Parameter"), ReadOnly = true, Width = 250, SortMode = DataGridViewColumnSortMode.NotSortable });
            _machineGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "value", HeaderText = T("Prf_ColProfileValue", "This profile"), Width = 90, SortMode = DataGridViewColumnSortMode.NotSortable });
            _machineGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "group", HeaderText = T("Prf_ColGroupValue", "Group"), ReadOnly = true, Width = 90, SortMode = DataGridViewColumnSortMode.NotSortable });
            _machineGrid.Columns["value"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _machineGrid.Columns["group"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _machineGrid.Columns["group"].DefaultCellStyle.ForeColor = Color.Gray;
            _machineGrid.CellValueChanged += (s, e) => { if (!_fillingMachine && e.RowIndex >= 0 && _machineGrid.Columns[e.ColumnIndex].Name == "value") MachineCellChanged(e.RowIndex); };
            _machineGrid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                // a tick is kept as it is clicked (not when the cell is left)
                if (_machineGrid.IsCurrentCellDirty && _machineGrid.CurrentCell is DataGridViewCheckBoxCell) _machineGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _machineGrid.DataError += (s, e) => { e.ThrowException = false; };
            _machineTab.Controls.Add(_machineGrid);
            _machineTab.Controls.Add(_machineHead);
            var st = ParametersStructure();
            var label = PlcLabelTable.ByLabel(1, ParametersLabel);
            if (st == null)
            {
                _machineHead.Text = T("Prf_MachineNone", "The machine's structured data types (StructuredTypes.csv) have no \"Profile Parameters\".");
                return;
            }
            _machineHead.Text = string.Format(T("Prf_MachineHead2",
                "Sent to the PLC with the job ({0}, {1}). Group: its template profile's value; bold blue: this profile's differs from it."),
                ParametersLabel, label != null ? label.Address : "R25600");
            _fillingMachine = true;
            foreach (var m in st.Members.OrderBy(x => x.Offset).ThenBy(x => x.Bit))
            {
                int i = _machineGrid.Rows.Add(m.Name, "", "");
                var row = _machineGrid.Rows[i];
                row.Tag = m;
                if (m.IsBit)
                {
                    row.Cells["value"] = new DataGridViewCheckBoxCell { TrueValue = "1", FalseValue = "0", Value = "0" };
                    row.Cells["group"] = new DataGridViewCheckBoxCell { TrueValue = "1", FalseValue = "0", Value = "0" };
                    row.Cells["group"].ReadOnly = true;
                }
                string place = m.IsBit ? m.Offset + "." + m.Bit : m.Offset.ToString(CultureInfo.InvariantCulture);
                row.Cells["name"].ToolTipText = (m.Comment ?? "") + "  (+" + place + (m.IsScaled ? ", x" + m.Divisor.ToString(CultureInfo.InvariantCulture) : "") + ")";
            }
            _fillingMachine = false;
        }

        /// <summary>The table from the profile on display.</summary>
        private void FillMachine()
        {
            if (_machineGrid == null) return;
            _fillingMachine = true;
            try
            {
                bool ok = _current != null && _libraryOk && _instance > 0;
                var values = ok ? Library.MachineSettings(_current.Id, _instance) : new Dictionary<string, Library.MachineValue>();
                long template = ok ? Library.GroupTemplateOf(_current.Id) : 0;
                var group = template > 0 ? Library.MachineSettings(template, _instance) : new Dictionary<string, Library.MachineValue>();
                _machineGrid.Columns["group"].HeaderText = template > 0 && template != (_current != null ? _current.Id : 0)
                    ? string.Format(T("Prf_ColGroupOf", "Group ({0})"), Convert.ToString(Library.Scalar("SELECT code FROM profile WHERE id=@p0", template)))
                    : T("Prf_ColGroupValue", "Group");
                foreach (DataGridViewRow row in _machineGrid.Rows)
                {
                    var m = (StructuredTypes.Member)row.Tag;
                    Library.MachineValue v, g;
                    values.TryGetValue(m.Name, out v);
                    group.TryGetValue(m.Name, out g);
                    string mine = Normal(v != null ? v.Value : null), theirs = Normal(g != null ? g.Value : null);
                    row.Cells["value"].Value = mine;
                    row.Cells["group"].Value = theirs;
                    bool edited = mine != theirs;
                    row.Cells["value"].Style.ForeColor = edited ? Color.FromArgb(20, 90, 200) : SystemColors.WindowText;
                    row.Cells["value"].Style.Font = edited ? new Font(_machineGrid.Font, FontStyle.Bold) : null;
                    row.Cells["value"].ReadOnly = !ok || !CanEdit;
                }
            }
            finally { _fillingMachine = false; }
        }

        /// <summary>A value as kept and shown: a number in the invariant culture, nothing set 0.</summary>
        private static string Normal(string value)
        {
            double d;
            if (string.IsNullOrWhiteSpace(value)) return "0";
            return double.TryParse(value.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out d)
                ? d.ToString("0.###", CultureInfo.InvariantCulture) : value.Trim();
        }

        private void MachineCellChanged(int rowIndex)
        {
            var row = _machineGrid.Rows[rowIndex];
            var m = row.Tag as StructuredTypes.Member;
            if (m == null) return;
            string t = Convert.ToString(row.Cells["value"].Value ?? "").Trim().Replace(',', '.');
            double d;
            if (t.Length == 0) t = "0";
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            {
                Status(string.Format(T("Prf_MachineNotNumber", "{0}: \"{1}\" is not a number."), m.Name, t));
                BeginInvoke((Action)FillMachine);
                return;
            }
            KeepMachineValue(m.Name, Normal(t));
        }

        /// <summary>A value of the profile kept as its own.</summary>
        internal void KeepMachineValue(string code, string value)
        {
            if (_current == null || !_libraryOk || _instance <= 0 || !CanEdit) return;
            var had = Library.MachineSettings(_current.Id, _instance);
            Library.MachineValue was;
            had.TryGetValue(code, out was);
            if (was != null && was.Own && Normal(was.Value) == Normal(value)) return;
            Library.SetMachineSetting(_current.Id, _instance, code, Normal(value));
            AuditLogger.Log("profile library: " + _current.Code + " - machine setting " + code + " = " + Normal(value) + ".");
            if (IsHandleCreated) BeginInvoke((Action)FillMachine); else FillMachine();
            ShowSpecialCutValues();
        }
    }
}
