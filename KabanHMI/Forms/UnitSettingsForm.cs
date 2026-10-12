using System;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Forms
{
    public partial class UnitSettingsForm : Form
    {
        public UnitSettingsForm()
        {
            InitializeComponent();
            Kaban.UI.Controls.UiScale.ApplyOnLoad(this);       // at the magnifying factor
            this.Text = "Unit Configuration";
            this.Size = new Size(300, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            GenerateControls();
        }

        private void GenerateControls()
        {
            int y = 20;

            foreach (UnitType type in Enum.GetValues(typeof(UnitType)))
            {
                if (type == UnitType.None) continue;

                // 1. Get Options
                var options = UnitManager.GetAvailable(type);

                // --- FIX: SKIP IF NO UNITS REGISTERED ---
                // This prevents creating empty ComboBoxes for Pressure/Time
                if (options == null || options.Count == 0) continue;

                // 2. Label
                Label lbl = new Label();
                lbl.Text = type.ToString() + ":";
                lbl.Location = new Point(20, y);
                lbl.AutoSize = true;
                this.Controls.Add(lbl);

                // 3. ComboBox
                ComboBox cmb = new ComboBox();
                cmb.Location = new Point(120, y - 3);
                cmb.Width = 120;
                cmb.DropDownStyle = ComboBoxStyle.DropDownList;

                // Populate
                cmb.DataSource = options;
                cmb.DisplayMember = "Name";

                // SET Current Selection
                var current = UnitManager.GetActive(type);

                // Safely find index
                int indexToSelect = -1;
                for (int i = 0; i < options.Count; i++)
                {
                    if (options[i].Name == current.Name)
                    {
                        indexToSelect = i;
                        break;
                    }
                }

                // Only set index if valid (Protects against 0-count errors)
                if (indexToSelect >= 0 && indexToSelect < cmb.Items.Count)
                {
                    cmb.SelectedIndex = indexToSelect;
                }
                else if (cmb.Items.Count > 0)
                {
                    cmb.SelectedIndex = 0; // Default to first if active not found
                }

                // Event: Save immediately on change
                cmb.SelectedIndexChanged += (s, e) =>
                {
                    if (cmb.SelectedItem is UnitDefinition selected)
                    {
                        UnitManager.SetActive(type, selected.Name);
                        SaveToConfig(type, selected.Name);
                    }
                };

                this.Controls.Add(cmb);
                y += 40;
            }
        }

        private void SaveToConfig(UnitType type, string unitName)
        {
            // Requires "Properties.Settings" to be set up!
            // DataFormat: Unit_Length, Unit_Weight, etc.
            string key = "Unit_" + type.ToString();

            try
            {
                // Check if setting exists, if not, we can't save (unless we handle it manually)
                // For simplicity, assume user created them in Project Properties.
                Properties.Settings.Default[key] = unitName;
                Properties.Settings.Default.Save();
            }
            catch
            {
                // Setting might not exist in Properties yet
            }
        }

        private void UnitSettingsForm_Load(object sender, EventArgs e)
        {

        }
    }
}
