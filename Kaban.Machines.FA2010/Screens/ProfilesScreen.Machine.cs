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
    /// "they go to the PLC at R25600 - R25649", with R25615 Profile Correction Position, "an upgrade we recently done on the machine"). One
    /// field per member of the structured data type the label PC_ProfilParametreleri has (StructuredTypes.csv, "Profile Parameters"): the
    /// same structure the PLC Monitor's tab HMI -> PLC shows, and the one "Send to the machine" writes with the job. A value the profile has
    /// not is its group's template's (the group's reference profile, else its first - the old program's first profile of the group), shown
    /// grey with where it came from; typed here, it is the profile's own; emptied, the template's again.
    /// </summary>
    public partial class ProfilesScreen
    {
        /// <summary>The label of the profile's parameters in the PLC's label list (the structure and its address).</summary>
        internal const string ParametersLabel = "PC_ProfilParametreleri";
        internal const string ParametersType = "Profile Parameters";

        private TabPage _machineTab;
        private Panel _machineRows;
        private Label _machineHead;
        private readonly Dictionary<string, Control> _machineInputs = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Label> _machineFrom = new Dictionary<string, Label>(StringComparer.OrdinalIgnoreCase);
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
            _machineHead = new Label { Dock = DockStyle.Top, Height = 44, Font = UiStyle.Small, ForeColor = Color.FromArgb(70, 80, 95), Padding = new Padding(8, 4, 8, 0) };
            _machineRows = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            _machineTab.Controls.Add(_machineRows);
            _machineTab.Controls.Add(_machineHead);
            var st = ParametersStructure();
            var label = PlcLabelTable.ByLabel(1, ParametersLabel);
            if (st == null)
            {
                _machineHead.Text = T("Prf_MachineNone", "The machine's structured data types (StructuredTypes.csv) have no \"Profile Parameters\".");
                return;
            }
            _machineHead.Text = string.Format(T("Prf_MachineHead",
                "Sent to the PLC with the job ({0}, {1}). Grey: the group's template's value - type one to give this profile its own, empty it to take the template's again."),
                ParametersLabel, label != null ? label.Address : "R25600");
            int y = 4;
            foreach (var m in st.Members.OrderBy(x => x.Offset).ThenBy(x => x.Bit))
            {
                var row = new Panel { Left = 4, Top = y, Width = 520, Height = 30, Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right };
                var cap = new Label { Text = m.Name, Left = 4, Top = 0, Width = 300, Height = 30, TextAlign = ContentAlignment.MiddleLeft, Font = UiStyle.Small, AutoEllipsis = true };
                Control input;
                if (m.IsBit)
                {
                    var cb = new CheckBox { Left = 312, Top = 4, Width = 60, Height = 22 };
                    string code = m.Name;
                    cb.CheckedChanged += (s, e) => { if (!_fillingMachine) KeepMachineValue(code, cb.Checked ? "1" : "0"); };
                    input = cb;
                }
                else
                {
                    var tb = new TextBox { Left = 312, Top = 3, Width = 90, Font = UiStyle.Small, TextAlign = HorizontalAlignment.Right };
                    string code = m.Name;
                    tb.Leave += (s, e) => { if (!_fillingMachine) KeepMachineText(code, tb); };
                    tb.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; KeepMachineText(code, tb); } };
                    input = tb;
                }
                var from = new Label { Left = 408, Top = 0, Width = 110, Height = 30, TextAlign = ContentAlignment.MiddleLeft, Font = UiStyle.Small, ForeColor = Color.Gray, AutoEllipsis = true };
                string place = m.IsBit ? m.Offset + "." + m.Bit : m.Offset.ToString(CultureInfo.InvariantCulture);
                _tips.SetToolTip(cap, (m.Comment ?? "") + "  (+" + place + (m.IsScaled ? ", x" + m.Divisor.ToString(CultureInfo.InvariantCulture) : "") + ")");
                row.Controls.Add(cap); row.Controls.Add(input); row.Controls.Add(from);
                row.Paint += (s, e) => { using (var pen = new Pen(UiStyle.RowRule)) e.Graphics.DrawLine(pen, 0, row.Height - 1, row.Width, row.Height - 1); };
                _machineRows.Controls.Add(row);
                _machineInputs[m.Name] = input;
                _machineFrom[m.Name] = from;
                y += 30;
            }
        }

        /// <summary>The fields from the profile on display.</summary>
        private void FillMachine()
        {
            if (_machineTab == null) return;
            _fillingMachine = true;
            try
            {
                var values = _current != null && _libraryOk && _instance > 0 ? Library.MachineSettings(_current.Id, _instance) : new Dictionary<string, Library.MachineValue>();
                foreach (var kv in _machineInputs)
                {
                    Library.MachineValue v;
                    values.TryGetValue(kv.Key, out v);
                    var cb = kv.Value as CheckBox;
                    if (cb != null) cb.Checked = v != null && v.Value == "1";
                    else kv.Value.Text = v != null ? v.Value : "";
                    kv.Value.ForeColor = v != null && !v.Own ? Color.Gray : SystemColors.WindowText;
                    kv.Value.Enabled = _current != null && CanEdit;
                    _machineFrom[kv.Key].Text = v == null ? T("Prf_MachineUnset", "not set (0)") : v.Own ? "" : string.Format(T("Prf_MachineFrom", "from {0}"), v.From);
                }
            }
            finally { _fillingMachine = false; }
        }

        private void KeepMachineText(string code, TextBox tb)
        {
            string t = (tb.Text ?? "").Trim().Replace(',', '.');
            double d;
            if (t.Length > 0 && !double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            {
                Status(string.Format(T("Prf_MachineNotNumber", "{0}: \"{1}\" is not a number."), code, tb.Text));
                FillMachine();
                return;
            }
            KeepMachineValue(code, t);
        }

        /// <summary>A value of the profile's own kept ("": the template's again).</summary>
        internal void KeepMachineValue(string code, string value)
        {
            if (_current == null || !_libraryOk || _instance <= 0 || !CanEdit) return;
            var had = Library.MachineSettings(_current.Id, _instance);
            Library.MachineValue was;
            had.TryGetValue(code, out was);
            if (was != null && was.Own && was.Value == value) return;
            if (was != null && !was.Own && was.Value == value) return;        // the template's value, unchanged
            Library.SetMachineSetting(_current.Id, _instance, code, value);
            AuditLogger.Log("profile library: " + _current.Code + " - machine setting " + code + " = " + (value.Length == 0 ? "(the template's)" : value) + ".");
            FillMachine();
        }
    }
}
