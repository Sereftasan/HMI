namespace _SB3015.Presentation.Home.Manuel
{
    partial class ucOther
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucOther));
            this.kabanButton2 = new KabanDevBase.Plc_Componets.KabanButton();
            this.lblLubrication = new DevExpress.XtraEditors.LabelControl();
            this.lblForward = new DevExpress.XtraEditors.LabelControl();
            this.lblName = new DevExpress.XtraEditors.LabelControl();
            this.lblBack = new DevExpress.XtraEditors.LabelControl();
            this.lblHorn = new DevExpress.XtraEditors.LabelControl();
            this.lblEmergencyStop = new DevExpress.XtraEditors.LabelControl();
            this.lblAirPressure = new DevExpress.XtraEditors.LabelControl();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.kabanButton3 = new KabanDevBase.Plc_Componets.KabanButton();
            this.kabanLamb2 = new KabanDevBase.Plc_Componets.KabanLamb();
            this.kabanLamb1 = new KabanDevBase.Plc_Componets.KabanLamb();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.kabanLamb1);
            this.BoxWrapper.Controls.Add(this.kabanLamb2);
            this.BoxWrapper.Controls.Add(this.kabanButton3);
            this.BoxWrapper.Controls.Add(this.label3);
            this.BoxWrapper.Controls.Add(this.label2);
            this.BoxWrapper.Controls.Add(this.lblAirPressure);
            this.BoxWrapper.Controls.Add(this.lblEmergencyStop);
            this.BoxWrapper.Controls.Add(this.lblHorn);
            this.BoxWrapper.Controls.Add(this.kabanButton2);
            this.BoxWrapper.Controls.Add(this.lblLubrication);
            this.BoxWrapper.Controls.Add(this.lblForward);
            this.BoxWrapper.Controls.Add(this.lblName);
            this.BoxWrapper.Controls.Add(this.lblBack);
            this.BoxWrapper.LookAndFeel.SkinName = "The Bezier";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            // 
            // kabanButton2
            // 
            this.kabanButton2.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.kabanButton2.ButtonTag = null;
            this.kabanButton2.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.kabanButton2.CriticalityStatus = false;
            this.kabanButton2.Cursor = System.Windows.Forms.Cursors.Hand;
            this.kabanButton2.DefaultValue = 0;
            this.kabanButton2.Device = "M668";
            this.kabanButton2.ImageOptions.Location = DevExpress.XtraEditors.ImageLocation.MiddleCenter;
            this.kabanButton2.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("kabanButton2.ImageOptions.SvgImage")));
            this.kabanButton2.ImageOptions.SvgImageSize = new System.Drawing.Size(20, 20);
            this.kabanButton2.IsReferanceButton = false;
            this.kabanButton2.IsScrewingControl = false;
            this.kabanButton2.Location = new System.Drawing.Point(302, 336);
            this.kabanButton2.Name = "kabanButton2";
            this.kabanButton2.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.kabanButton2.ProcessDevice = false;
            this.kabanButton2.ResetDevice = null;
            this.kabanButton2.Size = new System.Drawing.Size(89, 31);
            this.kabanButton2.TabIndex = 451;
            this.kabanButton2.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            this.kabanButton2.Visible = false;
            // 
            // lblLubrication
            // 
            this.lblLubrication.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblLubrication.Appearance.Options.UseFont = true;
            this.lblLubrication.Appearance.Options.UseTextOptions = true;
            this.lblLubrication.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblLubrication.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblLubrication.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblLubrication.Location = new System.Drawing.Point(15, 335);
            this.lblLubrication.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblLubrication.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblLubrication.Name = "lblLubrication";
            this.lblLubrication.Size = new System.Drawing.Size(186, 31);
            this.lblLubrication.TabIndex = 447;
            this.lblLubrication.Text = "Lubrication";
            this.lblLubrication.Visible = false;
            // 
            // lblForward
            // 
            this.lblForward.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblForward.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lblForward.Appearance.Options.UseBackColor = true;
            this.lblForward.Appearance.Options.UseFont = true;
            this.lblForward.Appearance.Options.UseTextOptions = true;
            this.lblForward.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblForward.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblForward.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblForward.Location = new System.Drawing.Point(302, 15);
            this.lblForward.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblForward.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblForward.Name = "lblForward";
            this.lblForward.Size = new System.Drawing.Size(89, 57);
            this.lblForward.TabIndex = 446;
            this.lblForward.Text = "Forward";
            // 
            // lblName
            // 
            this.lblName.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblName.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lblName.Appearance.Options.UseBackColor = true;
            this.lblName.Appearance.Options.UseFont = true;
            this.lblName.Appearance.Options.UseTextOptions = true;
            this.lblName.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblName.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblName.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblName.Location = new System.Drawing.Point(15, 15);
            this.lblName.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblName.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(186, 57);
            this.lblName.TabIndex = 445;
            this.lblName.Text = "Name";
            // 
            // lblBack
            // 
            this.lblBack.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblBack.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lblBack.Appearance.Options.UseBackColor = true;
            this.lblBack.Appearance.Options.UseFont = true;
            this.lblBack.Appearance.Options.UseTextOptions = true;
            this.lblBack.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblBack.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblBack.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblBack.Location = new System.Drawing.Point(207, 15);
            this.lblBack.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblBack.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblBack.Name = "lblBack";
            this.lblBack.Size = new System.Drawing.Size(89, 57);
            this.lblBack.TabIndex = 444;
            this.lblBack.Text = "Back";
            // 
            // lblHorn
            // 
            this.lblHorn.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblHorn.Appearance.Options.UseFont = true;
            this.lblHorn.Appearance.Options.UseTextOptions = true;
            this.lblHorn.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblHorn.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblHorn.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblHorn.Location = new System.Drawing.Point(15, 78);
            this.lblHorn.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblHorn.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblHorn.Name = "lblHorn";
            this.lblHorn.Size = new System.Drawing.Size(186, 31);
            this.lblHorn.TabIndex = 453;
            this.lblHorn.Text = "Horn";
            // 
            // lblEmergencyStop
            // 
            this.lblEmergencyStop.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblEmergencyStop.Appearance.Options.UseFont = true;
            this.lblEmergencyStop.Appearance.Options.UseTextOptions = true;
            this.lblEmergencyStop.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblEmergencyStop.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblEmergencyStop.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblEmergencyStop.Location = new System.Drawing.Point(15, 115);
            this.lblEmergencyStop.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblEmergencyStop.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblEmergencyStop.Name = "lblEmergencyStop";
            this.lblEmergencyStop.Size = new System.Drawing.Size(186, 31);
            this.lblEmergencyStop.TabIndex = 454;
            this.lblEmergencyStop.Text = "Emergency Stop";
            // 
            // lblAirPressure
            // 
            this.lblAirPressure.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblAirPressure.Appearance.Options.UseFont = true;
            this.lblAirPressure.Appearance.Options.UseTextOptions = true;
            this.lblAirPressure.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblAirPressure.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblAirPressure.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblAirPressure.Location = new System.Drawing.Point(15, 152);
            this.lblAirPressure.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblAirPressure.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblAirPressure.Name = "lblAirPressure";
            this.lblAirPressure.Size = new System.Drawing.Size(186, 31);
            this.lblAirPressure.TabIndex = 455;
            this.lblAirPressure.Text = "Air Pressure";
            // 
            // label2
            // 
            this.label2.BackColor = System.Drawing.Color.LightGray;
            this.label2.Font = new System.Drawing.Font("Tahoma", 11F, System.Drawing.FontStyle.Bold);
            this.label2.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label2.Location = new System.Drawing.Point(207, 78);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(89, 31);
            this.label2.TabIndex = 616;
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label3
            // 
            this.label3.BackColor = System.Drawing.Color.LightGray;
            this.label3.Font = new System.Drawing.Font("Tahoma", 11F, System.Drawing.FontStyle.Bold);
            this.label3.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label3.Location = new System.Drawing.Point(207, 335);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(89, 31);
            this.label3.TabIndex = 617;
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.label3.Visible = false;
            // 
            // kabanButton3
            // 
            this.kabanButton3.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.kabanButton3.ButtonTag = null;
            this.kabanButton3.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.kabanButton3.CriticalityStatus = false;
            this.kabanButton3.Cursor = System.Windows.Forms.Cursors.Hand;
            this.kabanButton3.DefaultValue = 0;
            this.kabanButton3.Device = "M638";
            this.kabanButton3.ImageOptions.Location = DevExpress.XtraEditors.ImageLocation.MiddleCenter;
            this.kabanButton3.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("kabanButton3.ImageOptions.SvgImage")));
            this.kabanButton3.ImageOptions.SvgImageSize = new System.Drawing.Size(20, 20);
            this.kabanButton3.IsReferanceButton = false;
            this.kabanButton3.IsScrewingControl = false;
            this.kabanButton3.Location = new System.Drawing.Point(302, 79);
            this.kabanButton3.Name = "kabanButton3";
            this.kabanButton3.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.kabanButton3.ProcessDevice = false;
            this.kabanButton3.ResetDevice = null;
            this.kabanButton3.Size = new System.Drawing.Size(89, 31);
            this.kabanButton3.TabIndex = 619;
            this.kabanButton3.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanLamb2
            // 
            this.kabanLamb2.DefaultValue = null;
            this.kabanLamb2.LambDevice = "M200";
            this.kabanLamb2.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.kabanLamb2.Location = new System.Drawing.Point(207, 115);
            this.kabanLamb2.Name = "kabanLamb2";
            this.kabanLamb2.Size = new System.Drawing.Size(184, 31);
            this.kabanLamb2.TabIndex = 620;
            this.kabanLamb2.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanLamb1
            // 
            this.kabanLamb1.DefaultValue = null;
            this.kabanLamb1.LambDevice = "M201";
            this.kabanLamb1.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.kabanLamb1.Location = new System.Drawing.Point(207, 152);
            this.kabanLamb1.Name = "kabanLamb1";
            this.kabanLamb1.Size = new System.Drawing.Size(184, 31);
            this.kabanLamb1.TabIndex = 621;
            this.kabanLamb1.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // ucOther
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucOther";
            this.ShowCaption = false;
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private DevExpress.XtraEditors.LabelControl lblAirPressure;
        private DevExpress.XtraEditors.LabelControl lblEmergencyStop;
        private DevExpress.XtraEditors.LabelControl lblHorn;
        private KabanDevBase.Plc_Componets.KabanButton kabanButton2;
        private DevExpress.XtraEditors.LabelControl lblLubrication;
        private DevExpress.XtraEditors.LabelControl lblForward;
        private DevExpress.XtraEditors.LabelControl lblName;
        private DevExpress.XtraEditors.LabelControl lblBack;
        private KabanDevBase.Plc_Componets.KabanButton kabanButton3;
        public System.Windows.Forms.Label label3;
        public System.Windows.Forms.Label label2;
        private KabanDevBase.Plc_Componets.KabanLamb kabanLamb1;
        private KabanDevBase.Plc_Componets.KabanLamb kabanLamb2;
    }
}
