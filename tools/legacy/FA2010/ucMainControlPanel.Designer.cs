namespace KabanMachinesSystem.Presentation.Home
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucMainControlPanel));
            this.kabanTrackBar1 = new KabanDevBase.Plc_Componets.KabanTrackBar();
            this.btnCancel2 = new KabanDevBase.Controls.KabanSimpleButtonControl(this.components);
            this.btnStart = new KabanDevBase.Controls.KabanSimpleButtonControl(this.components);
            this.btnPause = new KabanDevBase.Plc_Componets.KabanButton();
            this.btnStartOld = new KabanDevBase.Plc_Componets.KabanButton();
            this.btnLeftFeed = new KabanDevBase.Plc_Componets.KabanButton();
            this.kbnWaste = new KabanDevBase.Plc_Componets.KabanButton();
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
            this.BoxWrapper.Controls.Add(this.kbnWaste);
            this.BoxWrapper.Controls.Add(this.btnLeftFeed);
            this.BoxWrapper.Controls.Add(this.btnStart);
            this.BoxWrapper.Controls.Add(this.btnCancel2);
            this.BoxWrapper.Controls.Add(this.alarmPanel1);
            this.BoxWrapper.Controls.Add(this.kabanTrackBar1);
            this.BoxWrapper.Controls.Add(this.btnPause);
            this.BoxWrapper.Controls.Add(this.btnStartOld);
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
            this.btnCancel2.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnCancel2.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCancel2.HierarchialPath = null;
            this.btnCancel2.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnCancel2.ImageOptions.SvgImage")));
            this.btnCancel2.KeyboardDirectInput = false;
            this.btnCancel2.KeyboardDirectValue = null;
            this.btnCancel2.KeyboardShiftValue = null;
            this.btnCancel2.Keys = System.Windows.Forms.Keys.None;
            this.btnCancel2.Location = new System.Drawing.Point(647, 15);
            this.btnCancel2.Name = "btnCancel2";
            this.btnCancel2.Size = new System.Drawing.Size(99, 54);
            this.btnCancel2.TabIndex = 3;
            this.btnCancel2.Text = "Cancel";
            // 
            // btnStart
            // 
            this.btnStart.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnStart.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.btnStart.Appearance.Options.UseFont = true;
            this.btnStart.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnStart.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStart.HierarchialPath = null;
            this.btnStart.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnStart.ImageOptions.SvgImage")));
            this.btnStart.KeyboardDirectInput = false;
            this.btnStart.KeyboardDirectValue = null;
            this.btnStart.KeyboardShiftValue = null;
            this.btnStart.Keys = System.Windows.Forms.Keys.None;
            this.btnStart.Location = new System.Drawing.Point(412, 15);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(99, 54);
            this.btnStart.TabIndex = 1;
            this.btnStart.Text = "Start";
            // 
            // btnPause
            // 
            this.btnPause.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnPause.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.btnPause.Appearance.Options.UseFont = true;
            this.btnPause.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnPause.ButtonTag = null;
            this.btnPause.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Alternate;
            this.btnPause.CriticalityStatus = false;
            this.btnPause.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnPause.DefaultValue = 0;
            this.btnPause.Device = "M111";
            this.btnPause.IsReferanceButton = false;
            this.btnPause.IsScrewingControl = false;
            this.btnPause.Location = new System.Drawing.Point(532, 15);
            this.btnPause.Name = "btnPause";
            this.btnPause.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnPause.ProcessDevice = false;
            this.btnPause.ResetDevice = null;
            this.btnPause.Size = new System.Drawing.Size(99, 54);
            this.btnPause.TabIndex = 2;
            this.btnPause.Text = "Pause";
            this.btnPause.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // btnStartOld
            // 
            this.btnStartOld.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.btnStartOld.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.btnStartOld.Appearance.Options.UseFont = true;
            this.btnStartOld.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnStartOld.ButtonTag = null;
            this.btnStartOld.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Set;
            this.btnStartOld.CriticalityStatus = false;
            this.btnStartOld.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnStartOld.DefaultValue = 0;
            this.btnStartOld.Device = "M21";
            this.btnStartOld.IsReferanceButton = false;
            this.btnStartOld.IsScrewingControl = false;
            this.btnStartOld.Location = new System.Drawing.Point(412, 15);
            this.btnStartOld.Name = "btnStartOld";
            this.btnStartOld.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnStartOld.ProcessDevice = false;
            this.btnStartOld.ResetDevice = null;
            this.btnStartOld.Size = new System.Drawing.Size(99, 54);
            this.btnStartOld.TabIndex = 19;
            this.btnStartOld.Text = "Start";
            this.btnStartOld.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            this.btnStartOld.Visible = false;
            // 
            // btnLeftFeed
            // 
            this.btnLeftFeed.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnLeftFeed.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.btnLeftFeed.Appearance.Options.UseFont = true;
            this.btnLeftFeed.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnLeftFeed.ButtonTag = null;
            this.btnLeftFeed.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Alternate;
            this.btnLeftFeed.CriticalityStatus = false;
            this.btnLeftFeed.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnLeftFeed.DefaultValue = 0;
            this.btnLeftFeed.Device = "L17";
            this.btnLeftFeed.IsReferanceButton = false;
            this.btnLeftFeed.IsScrewingControl = false;
            this.btnLeftFeed.Location = new System.Drawing.Point(909, 12);
            this.btnLeftFeed.Name = "btnLeftFeed";
            this.btnLeftFeed.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnLeftFeed.ProcessDevice = false;
            this.btnLeftFeed.ResetDevice = null;
            this.btnLeftFeed.Size = new System.Drawing.Size(106, 54);
            this.btnLeftFeed.TabIndex = 24;
            this.btnLeftFeed.Text = "Feeding";
            this.btnLeftFeed.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            this.btnLeftFeed.Visible = false;
            // 
            // kbnWaste
            // 
            this.kbnWaste.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.kbnWaste.Appearance.Options.UseFont = true;
            this.kbnWaste.Appearance.Options.UseTextOptions = true;
            this.kbnWaste.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.kbnWaste.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.kbnWaste.ButtonTag = null;
            this.kbnWaste.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Alternate;
            this.kbnWaste.CriticalityStatus = false;
            this.kbnWaste.Cursor = System.Windows.Forms.Cursors.Hand;
            this.kbnWaste.DefaultValue = 0;
            this.kbnWaste.Device = "L26";
            this.kbnWaste.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("kbnWaste.ImageOptions.SvgImage")));
            this.kbnWaste.IsReferanceButton = false;
            this.kbnWaste.IsScrewingControl = false;
            this.kbnWaste.Location = new System.Drawing.Point(258, 12);
            this.kbnWaste.Name = "kbnWaste";
            this.kbnWaste.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.kbnWaste.ProcessDevice = false;
            this.kbnWaste.ResetDevice = null;
            this.kbnWaste.Size = new System.Drawing.Size(106, 54);
            this.kbnWaste.TabIndex = 25;
            this.kbnWaste.Text = "Waste Mode";
            this.kbnWaste.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // alarmPanel1
            // 
            this.alarmPanel1.AlarmDevice = "M800";
            this.alarmPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.alarmPanel1.Location = new System.Drawing.Point(1021, 11);
            this.alarmPanel1.Name = "alarmPanel1";
            this.alarmPanel1.ResetDevice = "M102";
            this.alarmPanel1.Size = new System.Drawing.Size(162, 55);
            this.alarmPanel1.TabIndex = 23;
            this.alarmPanel1.WarningDevice = "M1000";
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
        private KabanDevBase.Plc_Componets.KabanButton btnStartOld;
        private KabanDevBase.Plc_Componets.KabanTrackBar kabanTrackBar1;
        private KabanDevBase.Controls.KabanSimpleButtonControl btnCancel2;
        private KabanDevBase.Controls.KabanSimpleButtonControl btnStart;
        private KabanDevBase.Plc_Componets.KabanButton btnLeftFeed;
        public Alarms.AlarmPanel alarmPanel1;
        private KabanDevBase.Plc_Componets.KabanButton kbnWaste;
    }
}
