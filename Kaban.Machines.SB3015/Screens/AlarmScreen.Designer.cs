using System.Windows.Forms;

namespace Kaban.Machines.SB3015.Screens
{
    partial class AlarmScreen
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AlarmScreen));
            this.dgvAlarms = new System.Windows.Forms.DataGridView();
            this.buttonHMI1 = new Kaban.UI.Controls.ButtonHMI();
            this.btnReset = new Kaban.UI.Controls.ButtonHMI();
            ((System.ComponentModel.ISupportInitialize)(this.dgvAlarms)).BeginInit();
            this.SuspendLayout();
            // 
            // dgvAlarms
            // 
            this.dgvAlarms.AllowUserToAddRows = false;
            this.dgvAlarms.AllowUserToDeleteRows = false;
            this.dgvAlarms.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvAlarms.BackgroundColor = System.Drawing.Color.White;
            this.dgvAlarms.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvAlarms.Dock = System.Windows.Forms.DockStyle.Top;
            this.dgvAlarms.Location = new System.Drawing.Point(0, 0);
            this.dgvAlarms.Name = "dgvAlarms";
            this.dgvAlarms.ReadOnly = true;
            this.dgvAlarms.RowHeadersVisible = false;
            this.dgvAlarms.RowHeadersWidth = 51;
            this.dgvAlarms.RowTemplate.Height = 24;
            this.dgvAlarms.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dgvAlarms.Size = new System.Drawing.Size(538, 253);
            this.dgvAlarms.TabIndex = 1;
            this.dgvAlarms.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvAlarms_CellContentClick);
            // 
            // buttonHMI1
            // 
            this.buttonHMI1.BackColor = System.Drawing.Color.LightGray;
            this.buttonHMI1.BlinkCondition = Kaban.Core.Enums.SystemCondition.None;
            this.buttonHMI1.ColorActive = System.Drawing.Color.Lime;
            this.buttonHMI1.ColorPassive = System.Drawing.Color.LightGray;
            this.buttonHMI1.ConfirmationNeeded = false;
            this.buttonHMI1.DisplayAddress = null;
            this.buttonHMI1.DisplayOffset.StaticValue = 0D;
            this.buttonHMI1.DisplayOffset.TagAddress = "";
            this.buttonHMI1.DisplayOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.buttonHMI1.EnableConditions = Kaban.Core.Enums.SystemCondition.None;
            this.buttonHMI1.ImageActive = null;
            this.buttonHMI1.ImagePassive = null;
            this.buttonHMI1.Location = new System.Drawing.Point(52, 260);
            this.buttonHMI1.LogAction = false;
            this.buttonHMI1.Name = "buttonHMI1";
            this.buttonHMI1.PlcNumber = 1;
            this.buttonHMI1.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.None;
            this.buttonHMI1.Size = new System.Drawing.Size(170, 36);
            this.buttonHMI1.TabIndex = 2;
            this.buttonHMI1.Text = "✔ Acknowledge";
            this.buttonHMI1.TranslationKey = "Btn_Ack";
            this.buttonHMI1.UseVisualStyleBackColor = false;
            this.buttonHMI1.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            // 
            // btnReset
            // 
            this.btnReset.BackColor = System.Drawing.Color.LightGray;
            this.btnReset.BlinkCondition = Kaban.Core.Enums.SystemCondition.None;
            this.btnReset.ColorActive = System.Drawing.Color.Lime;
            this.btnReset.ColorPassive = System.Drawing.Color.LightGray;
            this.btnReset.ConfirmationNeeded = false;
            this.btnReset.DisplayAddress = null;
            this.btnReset.DisplayOffset.StaticValue = 0D;
            this.btnReset.DisplayOffset.TagAddress = "";
            this.btnReset.DisplayOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.btnReset.EnableConditions = Kaban.Core.Enums.SystemCondition.None;
            this.btnReset.ImageActive = null;
            this.btnReset.ImagePassive = null;
            this.btnReset.Location = new System.Drawing.Point(264, 260);
            this.btnReset.LogAction = false;
            this.btnReset.Name = "btnReset";
            this.btnReset.PlcNumber = 1;
            this.btnReset.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.None;
            this.btnReset.Size = new System.Drawing.Size(170, 36);
            this.btnReset.TabIndex = 3;
            this.btnReset.Text = "🔄 Reset Alarms";
            this.btnReset.TranslationKey = "Btn_Reset";
            this.btnReset.UseVisualStyleBackColor = false;
            this.btnReset.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            // 
            // AlarmScreen
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.btnReset);
            this.Controls.Add(this.buttonHMI1);
            this.Controls.Add(this.dgvAlarms);
            this.Name = "AlarmScreen";
            this.Size = new System.Drawing.Size(538, 351);
            ((System.ComponentModel.ISupportInitialize)(this.dgvAlarms)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.DataGridView dgvAlarms;
        private Kaban.UI.Controls.ButtonHMI buttonHMI1;
        private Kaban.UI.Controls.ButtonHMI btnReset;
    }
}

