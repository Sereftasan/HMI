namespace H500.Presentation.Home.Preparations
{
    partial class ucMainControlPanelH500FX
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
            this.alarmPanelScrewing1 = new _H500.Presentation.Home.Alarms.AlarmPanelScrewing();
            this.btnCancel = new KabanDevBase.Plc_Componets.KabanButton();
            this.btnPause = new KabanDevBase.Plc_Componets.KabanButton();
            this.btnStart = new KabanDevBase.Plc_Componets.KabanButton();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.btnStart);
            this.BoxWrapper.Controls.Add(this.alarmPanelScrewing1);
            this.BoxWrapper.Controls.Add(this.btnCancel);
            this.BoxWrapper.Controls.Add(this.btnPause);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(1031, 78);
            // 
            // alarmPanelScrewing1
            // 
            this.alarmPanelScrewing1.AlarmDevice = "M800";
            this.alarmPanelScrewing1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.alarmPanelScrewing1.Location = new System.Drawing.Point(856, 12);
            this.alarmPanelScrewing1.Name = "alarmPanelScrewing1";
            this.alarmPanelScrewing1.Size = new System.Drawing.Size(162, 55);
            this.alarmPanelScrewing1.TabIndex = 32;
            this.alarmPanelScrewing1.WarningDevice = "M1000";
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnCancel.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.btnCancel.Appearance.Options.UseFont = true;
            this.btnCancel.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnCancel.ButtonTag = null;
            this.btnCancel.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.btnCancel.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCancel.DefaultValue = 0;
            this.btnCancel.Device = "M3";
            this.btnCancel.IsReferanceButton = false;
            this.btnCancel.IsScrewingControl = false;
            this.btnCancel.Location = new System.Drawing.Point(559, 13);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.PlcType = KabanMachinesSystem.Common.Enums.PlcType.ScrewingFX5CPU;
            this.btnCancel.ProcessDevice = false;
            this.btnCancel.ResetDevice = null;
            this.btnCancel.Size = new System.Drawing.Size(99, 54);
            this.btnCancel.TabIndex = 31;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // btnPause
            // 
            this.btnPause.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnPause.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.btnPause.Appearance.Options.UseFont = true;
            this.btnPause.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnPause.ButtonTag = null;
            this.btnPause.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Alternate;
            this.btnPause.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnPause.DefaultValue = 0;
            this.btnPause.Device = "M4";
            this.btnPause.IsReferanceButton = false;
            this.btnPause.IsScrewingControl = false;
            this.btnPause.Location = new System.Drawing.Point(444, 13);
            this.btnPause.Name = "btnPause";
            this.btnPause.PlcType = KabanMachinesSystem.Common.Enums.PlcType.ScrewingFX5CPU;
            this.btnPause.ProcessDevice = false;
            this.btnPause.ResetDevice = null;
            this.btnPause.Size = new System.Drawing.Size(99, 54);
            this.btnPause.TabIndex = 30;
            this.btnPause.Text = "Pause";
            this.btnPause.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // btnStart
            // 
            this.btnStart.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnStart.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.btnStart.Appearance.Options.UseFont = true;
            this.btnStart.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnStart.ButtonTag = null;
            this.btnStart.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.btnStart.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStart.DefaultValue = 0;
            this.btnStart.Device = "M2";
            this.btnStart.IsReferanceButton = false;
            this.btnStart.IsScrewingControl = false;
            this.btnStart.Location = new System.Drawing.Point(325, 12);
            this.btnStart.Name = "btnStart";
            this.btnStart.PlcType = KabanMachinesSystem.Common.Enums.PlcType.ScrewingFX5CPU;
            this.btnStart.ProcessDevice = false;
            this.btnStart.ResetDevice = null;
            this.btnStart.Size = new System.Drawing.Size(99, 54);
            this.btnStart.TabIndex = 34;
            this.btnStart.Text = "Start";
            this.btnStart.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // ucMainControlPanelH500FX
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucMainControlPanelH500FX";
            this.PlcType = KabanMachinesSystem.Common.Enums.PlcType.ScrewingFX5CPU;
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(1031, 78);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        public _H500.Presentation.Home.Alarms.AlarmPanelScrewing alarmPanelScrewing1;
        private KabanDevBase.Plc_Componets.KabanButton btnCancel;
        private KabanDevBase.Plc_Componets.KabanButton btnPause;
        private KabanDevBase.Plc_Componets.KabanButton btnStart;
    }
}
