namespace _MD3030.Presentation.Home.Preparations
{
    partial class ucMainControlPanel
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucMainControlPanel));
            this.kabanTrackBar1 = new KabanDevBase.Plc_Componets.KabanTrackBar();
            this.btnCancel2 = new KabanDevBase.Controls.KabanSimpleButtonControl();
            this.btnStart = new KabanDevBase.Controls.KabanSimpleButtonControl();
            this.btnPause = new KabanDevBase.Plc_Componets.KabanButton();
            this.btnServoSituations = new KabanDevBase.Plc_Componets.KabanButton();
            this.alarmPanel1 = new KabanMachinesSystem.Presentation.Home.Alarms.AlarmPanel();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.btnServoSituations);
            this.BoxWrapper.Controls.Add(this.btnStart);
            this.BoxWrapper.Controls.Add(this.btnCancel2);
            this.BoxWrapper.Controls.Add(this.alarmPanel1);
            this.BoxWrapper.Controls.Add(this.kabanTrackBar1);
            this.BoxWrapper.Controls.Add(this.btnPause);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(1188, 78);
            this.BoxWrapper.Text = "";
            // 
            // kabanTrackBar1
            // 
            this.kabanTrackBar1.Device = "D408";
            this.kabanTrackBar1.FSpeed = ((System.Collections.Generic.Dictionary<int, int>)(resources.GetObject("kabanTrackBar1.FSpeed")));
            this.kabanTrackBar1.Location = new System.Drawing.Point(5, 12);
            this.kabanTrackBar1.MaxValue = 100;
            this.kabanTrackBar1.MinValue = 0;
            this.kabanTrackBar1.Name = "kabanTrackBar1";
            this.kabanTrackBar1.Size = new System.Drawing.Size(247, 54);
            this.kabanTrackBar1.TabIndex = 22;
            // 
            // btnCancel2
            // 
            this.btnCancel2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnCancel2.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.btnCancel2.Appearance.Options.UseFont = true;
            this.btnCancel2.Appearance.Options.UseTextOptions = true;
            this.btnCancel2.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.btnCancel2.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnCancel2.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCancel2.HierarchialPath = null;
            this.btnCancel2.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnCancel2.ImageOptions.SvgImage")));
            this.btnCancel2.KeyboardDirectInput = false;
            this.btnCancel2.KeyboardDirectValue = null;
            this.btnCancel2.KeyboardShiftValue = null;
            this.btnCancel2.Keys = System.Windows.Forms.Keys.None;
            this.btnCancel2.Location = new System.Drawing.Point(674, 15);
            this.btnCancel2.Name = "btnCancel2";
            this.btnCancel2.Size = new System.Drawing.Size(127, 54);
            this.btnCancel2.TabIndex = 3;
            this.btnCancel2.Text = "Cancel";
            // 
            // btnStart
            // 
            this.btnStart.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnStart.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.btnStart.Appearance.Options.UseFont = true;
            this.btnStart.Appearance.Options.UseTextOptions = true;
            this.btnStart.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.btnStart.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnStart.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStart.HierarchialPath = null;
            this.btnStart.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnStart.ImageOptions.SvgImage")));
            this.btnStart.KeyboardDirectInput = false;
            this.btnStart.KeyboardDirectValue = null;
            this.btnStart.KeyboardShiftValue = null;
            this.btnStart.Keys = System.Windows.Forms.Keys.None;
            this.btnStart.Location = new System.Drawing.Point(408, 15);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(127, 54);
            this.btnStart.TabIndex = 1;
            this.btnStart.Text = "Start";
            // 
            // btnPause
            // 
            this.btnPause.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnPause.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.btnPause.Appearance.Options.UseFont = true;
            this.btnPause.Appearance.Options.UseTextOptions = true;
            this.btnPause.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.btnPause.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnPause.ButtonTag = null;
            this.btnPause.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Alternate;
            this.btnPause.CriticalityStatus = false;
            this.btnPause.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnPause.DefaultValue = 0;
            this.btnPause.Device = "M111";
            this.btnPause.IsReferanceButton = false;
            this.btnPause.IsScrewingControl = false;
            this.btnPause.Location = new System.Drawing.Point(541, 15);
            this.btnPause.Name = "btnPause";
            this.btnPause.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnPause.ProcessDevice = false;
            this.btnPause.ResetDevice = null;
            this.btnPause.Size = new System.Drawing.Size(127, 54);
            this.btnPause.TabIndex = 2;
            this.btnPause.Text = "Pause";
            this.btnPause.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
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
            this.btnServoSituations.Location = new System.Drawing.Point(888, 11);
            this.btnServoSituations.Name = "btnServoSituations";
            this.btnServoSituations.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnServoSituations.ProcessDevice = false;
            this.btnServoSituations.ResetDevice = null;
            this.btnServoSituations.Size = new System.Drawing.Size(127, 54);
            this.btnServoSituations.TabIndex = 24;
            this.btnServoSituations.Text = "Servo Situations";
            this.btnServoSituations.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // alarmPanel1
            // 
            this.alarmPanel1.AlarmDevice = "M103";
            this.alarmPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.alarmPanel1.Location = new System.Drawing.Point(1021, 11);
            this.alarmPanel1.Name = "alarmPanel1";
            this.alarmPanel1.ResetDevice = "M314";
            this.alarmPanel1.Size = new System.Drawing.Size(162, 55);
            this.alarmPanel1.TabIndex = 23;
            this.alarmPanel1.WarningDevice = "M104";
            // 
            // ucMainControlPanel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucMainControlPanel";
            this.PageTitle = "";
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(1188, 78);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private KabanDevBase.Plc_Componets.KabanButton btnPause;
        private KabanDevBase.Plc_Componets.KabanTrackBar kabanTrackBar1;
        private KabanDevBase.Controls.KabanSimpleButtonControl btnCancel2;
        private KabanDevBase.Controls.KabanSimpleButtonControl btnStart;
        public KabanMachinesSystem.Presentation.Home.Alarms.AlarmPanel alarmPanel1;
        private KabanDevBase.Plc_Componets.KabanButton btnServoSituations;
    }
}
