namespace _MD3030.Presentation.Home.Common
{
    partial class ucHeadControlButtons
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucHeadControlButtons));
            this.btnSpeed_01mm = new KabanDevBase.Plc_Componets.KabanLambButton();
            this.btnSpeed_1mm = new KabanDevBase.Plc_Componets.KabanLambButton();
            this.btnSpeed_001mm = new KabanDevBase.Plc_Componets.KabanLambButton();
            this.btnSpeed_Jog = new KabanDevBase.Plc_Componets.KabanLambButton();
            this.kabanTrackBar1 = new KabanDevBase.Plc_Componets.KabanTrackBar();
            this.alarmPanel1 = new KabanMachinesSystem.Presentation.Home.Alarms.AlarmPanel();
            this.btnServoSituations = new KabanDevBase.Plc_Componets.KabanButton();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.btnServoSituations);
            this.BoxWrapper.Controls.Add(this.alarmPanel1);
            this.BoxWrapper.Controls.Add(this.kabanTrackBar1);
            this.BoxWrapper.Controls.Add(this.btnSpeed_01mm);
            this.BoxWrapper.Controls.Add(this.btnSpeed_1mm);
            this.BoxWrapper.Controls.Add(this.btnSpeed_001mm);
            this.BoxWrapper.Controls.Add(this.btnSpeed_Jog);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(1311, 61);
            this.BoxWrapper.Text = "";
            // 
            // btnSpeed_01mm
            // 
            this.btnSpeed_01mm.ButtonTag = "StepButton";
            this.btnSpeed_01mm.ButtonText = "0.1 mm";
            this.btnSpeed_01mm.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Set;
            this.btnSpeed_01mm.DefaultValue = "0";
            this.btnSpeed_01mm.Device = "M1";
            this.btnSpeed_01mm.IsHeadControlButton = true;
            this.btnSpeed_01mm.IsReferanceButton = false;
            this.btnSpeed_01mm.IsScrewingControl = false;
            this.btnSpeed_01mm.IsSecurityButton = false;
            this.btnSpeed_01mm.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.btnSpeed_01mm.LambColor = KabanMachinesSystem.Common.Enums.LambColor.Green;
            this.btnSpeed_01mm.LambDevice = "M1";
            this.btnSpeed_01mm.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.btnSpeed_01mm.Location = new System.Drawing.Point(163, 7);
            this.btnSpeed_01mm.Name = "btnSpeed_01mm";
            this.btnSpeed_01mm.ResetDevice = null;
            this.btnSpeed_01mm.Size = new System.Drawing.Size(67, 49);
            this.btnSpeed_01mm.TabIndex = 4;
            this.btnSpeed_01mm.Tag = "";
            this.btnSpeed_01mm.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // btnSpeed_1mm
            // 
            this.btnSpeed_1mm.ButtonTag = "StepButton";
            this.btnSpeed_1mm.ButtonText = "1 mm";
            this.btnSpeed_1mm.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Set;
            this.btnSpeed_1mm.DefaultValue = "0";
            this.btnSpeed_1mm.Device = "M2";
            this.btnSpeed_1mm.IsHeadControlButton = true;
            this.btnSpeed_1mm.IsReferanceButton = false;
            this.btnSpeed_1mm.IsScrewingControl = false;
            this.btnSpeed_1mm.IsSecurityButton = false;
            this.btnSpeed_1mm.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.btnSpeed_1mm.LambColor = KabanMachinesSystem.Common.Enums.LambColor.Green;
            this.btnSpeed_1mm.LambDevice = "M2";
            this.btnSpeed_1mm.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.btnSpeed_1mm.Location = new System.Drawing.Point(90, 7);
            this.btnSpeed_1mm.Name = "btnSpeed_1mm";
            this.btnSpeed_1mm.ResetDevice = null;
            this.btnSpeed_1mm.Size = new System.Drawing.Size(67, 49);
            this.btnSpeed_1mm.TabIndex = 3;
            this.btnSpeed_1mm.Tag = "";
            this.btnSpeed_1mm.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // btnSpeed_001mm
            // 
            this.btnSpeed_001mm.ButtonTag = "StepButton";
            this.btnSpeed_001mm.ButtonText = "0.01 mm";
            this.btnSpeed_001mm.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Set;
            this.btnSpeed_001mm.DefaultValue = "0";
            this.btnSpeed_001mm.Device = "M0";
            this.btnSpeed_001mm.IsHeadControlButton = true;
            this.btnSpeed_001mm.IsReferanceButton = false;
            this.btnSpeed_001mm.IsScrewingControl = false;
            this.btnSpeed_001mm.IsSecurityButton = false;
            this.btnSpeed_001mm.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.btnSpeed_001mm.LambColor = KabanMachinesSystem.Common.Enums.LambColor.Green;
            this.btnSpeed_001mm.LambDevice = "M0";
            this.btnSpeed_001mm.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.btnSpeed_001mm.Location = new System.Drawing.Point(236, 7);
            this.btnSpeed_001mm.Name = "btnSpeed_001mm";
            this.btnSpeed_001mm.ResetDevice = null;
            this.btnSpeed_001mm.Size = new System.Drawing.Size(67, 49);
            this.btnSpeed_001mm.TabIndex = 3;
            this.btnSpeed_001mm.Tag = "";
            this.btnSpeed_001mm.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // btnSpeed_Jog
            // 
            this.btnSpeed_Jog.ButtonTag = "StepButton";
            this.btnSpeed_Jog.ButtonText = "JOG";
            this.btnSpeed_Jog.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Set;
            this.btnSpeed_Jog.DefaultValue = "0";
            this.btnSpeed_Jog.Device = "M90";
            this.btnSpeed_Jog.IsHeadControlButton = true;
            this.btnSpeed_Jog.IsReferanceButton = false;
            this.btnSpeed_Jog.IsScrewingControl = false;
            this.btnSpeed_Jog.IsSecurityButton = false;
            this.btnSpeed_Jog.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.btnSpeed_Jog.LambColor = KabanMachinesSystem.Common.Enums.LambColor.Green;
            this.btnSpeed_Jog.LambDevice = "M90";
            this.btnSpeed_Jog.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.btnSpeed_Jog.Location = new System.Drawing.Point(17, 7);
            this.btnSpeed_Jog.Name = "btnSpeed_Jog";
            this.btnSpeed_Jog.ResetDevice = null;
            this.btnSpeed_Jog.Size = new System.Drawing.Size(67, 49);
            this.btnSpeed_Jog.TabIndex = 0;
            this.btnSpeed_Jog.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanTrackBar1
            // 
            this.kabanTrackBar1.Device = "D409";
            this.kabanTrackBar1.FSpeed = ((System.Collections.Generic.Dictionary<int, int>)(resources.GetObject("kabanTrackBar1.FSpeed")));
            this.kabanTrackBar1.Location = new System.Drawing.Point(599, 7);
            this.kabanTrackBar1.MaxValue = 100;
            this.kabanTrackBar1.MinValue = 0;
            this.kabanTrackBar1.Name = "kabanTrackBar1";
            this.kabanTrackBar1.Size = new System.Drawing.Size(247, 49);
            this.kabanTrackBar1.TabIndex = 7;
            // 
            // alarmPanel1
            // 
            this.alarmPanel1.AlarmDevice = "M103";
            this.alarmPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.alarmPanel1.Location = new System.Drawing.Point(1135, 5);
            this.alarmPanel1.Name = "alarmPanel1";
            this.alarmPanel1.ResetDevice = "M314";
            this.alarmPanel1.Size = new System.Drawing.Size(162, 55);
            this.alarmPanel1.TabIndex = 24;
            this.alarmPanel1.WarningDevice = "M104";
            // 
            // btnServoSituations
            // 
            this.btnServoSituations.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnServoSituations.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.btnServoSituations.Appearance.Options.UseFont = true;
            this.btnServoSituations.Appearance.Options.UseTextOptions = true;
            this.btnServoSituations.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.btnServoSituations.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnServoSituations.ButtonTag = null;
            this.btnServoSituations.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Alternate;
            this.btnServoSituations.CriticalityStatus = false;
            this.btnServoSituations.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnServoSituations.DefaultValue = 0;
            this.btnServoSituations.Device = "";
            this.btnServoSituations.IsReferanceButton = false;
            this.btnServoSituations.IsScrewingControl = false;
            this.btnServoSituations.Location = new System.Drawing.Point(1002, 5);
            this.btnServoSituations.Name = "btnServoSituations";
            this.btnServoSituations.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnServoSituations.ProcessDevice = false;
            this.btnServoSituations.ResetDevice = null;
            this.btnServoSituations.Size = new System.Drawing.Size(127, 54);
            this.btnServoSituations.TabIndex = 25;
            this.btnServoSituations.Text = "Servo Situations";
            this.btnServoSituations.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // ucHeadControlButtons
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.IsTranslate = true;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucHeadControlButtons";
            this.PageTitle = "";
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(1311, 61);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private KabanDevBase.Plc_Componets.KabanLambButton btnSpeed_01mm;
        private KabanDevBase.Plc_Componets.KabanLambButton btnSpeed_1mm;
        private KabanDevBase.Plc_Componets.KabanLambButton btnSpeed_001mm;
        private KabanDevBase.Plc_Componets.KabanLambButton btnSpeed_Jog;
        private KabanDevBase.Plc_Componets.KabanTrackBar kabanTrackBar1;
        public KabanMachinesSystem.Presentation.Home.Alarms.AlarmPanel alarmPanel1;
        private KabanDevBase.Plc_Componets.KabanButton btnServoSituations;
    }
}
