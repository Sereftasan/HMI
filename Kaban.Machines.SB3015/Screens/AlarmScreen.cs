using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.Core.Models;

namespace Kaban.Machines.SB3015.Screens // Or wherever your UserControls are located
{
    public partial class AlarmScreen : UserControl, ILocalizable
    {
        public string TranslationKey { get; set; } = "";

        public AlarmScreen()
        {
            InitializeComponent();

            if (this.DesignMode) return;

            SetupGrid();

            // "Acknowledge" clears the visible alarm list (does not touch history).
            // "RESET Alarms" (btnReset) still fires whatever BitActions/WordActions
            // you configure on it in the designer, e.g. writing a PLC alarm-reset bit.
            buttonHMI1.Click += (s, e) =>
            {
                if (!AlarmManager.AcknowledgeAll())
                {
                    MessageBox.Show("You do not have permission to acknowledge alarms.",
                        "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };

            // Subscribe to Language changes
            LanguageManager.LanguageChanged += OnLanguageChanged;

            // CLEANUP: Automatically unsubscribe when this screen is destroyed
            this.Disposed += (s, e) =>
            {
                LanguageManager.LanguageChanged -= OnLanguageChanged;
            };
        }

        private void SetupGrid()
        {
            dgvAlarms.AutoGenerateColumns = false;
            dgvAlarms.Columns.Clear();

            // 1. Time Column
            dgvAlarms.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColTime",
                DataPropertyName = "TriggerTime",
                Width = 150,
                DefaultCellStyle = new DataGridViewCellStyle { Format = Kaban.Core.Helpers.ViewPreferences.DateTimePattern() }
            });

            // 2. Code Column
            dgvAlarms.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColCode",
                DataPropertyName = "ShownCode",   // "M840.2" for an alarm the HMI raised itself
                Width = 100
            });

            // 3. Description Column (Takes up remaining space)
            // NOTE: this used to bind to "DescriptionKey", a property that doesn't
            // exist on AlarmItem, so the column always rendered blank. Fixed to
            // bind to the real "Description" property (translated text).
            dgvAlarms.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ColDesc",
                DataPropertyName = "Description",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            // Bind directly to the shared, live alarm engine instead of a local
            // throwaway list - this is what actually connects the screen to
            // real PLC alarm conditions (see AlarmManager / PlcScanner).
            dgvAlarms.DataSource = AlarmManager.ActiveAlarms;
            // The program's table look and column menu (the PLC Monitor's).
            Kaban.UI.Controls.UiStyle.EnableTableTools(dgvAlarms, "MachineAlarmList");

            ApplyTranslation();
        }

        // --- TRANSLATION LOGIC ---
        private void OnLanguageChanged(object sender, EventArgs e) => ApplyTranslation();

        public void ApplyTranslation()
        {
            if (dgvAlarms.Columns.Contains("ColTime"))
                dgvAlarms.Columns["ColTime"].HeaderText = LanguageManager.GetString("Sys_Time", "Time");

            if (dgvAlarms.Columns.Contains("ColCode"))
                dgvAlarms.Columns["ColCode"].HeaderText = LanguageManager.GetString("Sys_Code", "Code");

            if (dgvAlarms.Columns.Contains("ColDesc"))
                dgvAlarms.Columns["ColDesc"].HeaderText = LanguageManager.GetString("Sys_Description", "Description");

            // Force the grid to repaint so the row descriptions update
            dgvAlarms.Refresh();
        }

        private void dgvAlarms_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }
    }
}

