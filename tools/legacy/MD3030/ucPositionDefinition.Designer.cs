namespace _MD3030.Presentation.Manuel
{
    partial class ucPositionDefinition
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucPositionDefinition));
            this.kabanNumericButton5 = new KabanDevBase.Plc_Componets.KabanNumericButton();
            this.lblStartingPosition = new DevExpress.XtraEditors.LabelControl();
            this.lblPositionHeadX = new DevExpress.XtraEditors.LabelControl();
            this.lblPositionName = new DevExpress.XtraEditors.LabelControl();
            this.lblHeadY = new KabanDevBase.Plc_Componets.KabanNumericButton();
            this.lblPositionHeadY = new DevExpress.XtraEditors.LabelControl();
            this.kabanNumericButton2 = new KabanDevBase.Plc_Componets.KabanNumericButton();
            this.lblPositionHeadZ1 = new DevExpress.XtraEditors.LabelControl();
            this.btnRefresh = new DevExpress.XtraEditors.SimpleButton();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.btnRefresh);
            this.BoxWrapper.Controls.Add(this.kabanNumericButton2);
            this.BoxWrapper.Controls.Add(this.lblPositionHeadZ1);
            this.BoxWrapper.Controls.Add(this.lblHeadY);
            this.BoxWrapper.Controls.Add(this.lblPositionHeadY);
            this.BoxWrapper.Controls.Add(this.kabanNumericButton5);
            this.BoxWrapper.Controls.Add(this.lblStartingPosition);
            this.BoxWrapper.Controls.Add(this.lblPositionHeadX);
            this.BoxWrapper.Controls.Add(this.lblPositionName);
            this.BoxWrapper.CustomHeaderButtonsLocation = DevExpress.Utils.GroupElementLocation.BeforeText;
            this.BoxWrapper.LookAndFeel.SkinName = "The Bezier";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(1074, 741);
            this.BoxWrapper.Text = "";
            // 
            // kabanNumericButton5
            // 
            this.kabanNumericButton5.CalibrationDevice = null;
            this.kabanNumericButton5.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
            this.kabanNumericButton5.DefaultValue = 0;
            this.kabanNumericButton5.Device = "D1940";
            this.kabanNumericButton5.Divisor = 10;
            this.kabanNumericButton5.IsConvertible = false;
            this.kabanNumericButton5.IsScrewingControl = false;
            this.kabanNumericButton5.Location = new System.Drawing.Point(206, 57);
            this.kabanNumericButton5.Mask = "n1";
            this.kabanNumericButton5.MaxValue = -95;
            this.kabanNumericButton5.MinValue = -132;
            this.kabanNumericButton5.Name = "kabanNumericButton5";
            this.kabanNumericButton5.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.kabanNumericButton5.ShowKeyboardOnClick = false;
            this.kabanNumericButton5.Size = new System.Drawing.Size(112, 31);
            this.kabanNumericButton5.TabIndex = 2378;
            this.kabanNumericButton5.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            this.kabanNumericButton5.Value = "0";
            // 
            // lblStartingPosition
            // 
            this.lblStartingPosition.Appearance.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblStartingPosition.Appearance.Options.UseFont = true;
            this.lblStartingPosition.Appearance.Options.UseTextOptions = true;
            this.lblStartingPosition.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblStartingPosition.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblStartingPosition.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblStartingPosition.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblStartingPosition.Location = new System.Drawing.Point(16, 57);
            this.lblStartingPosition.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblStartingPosition.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblStartingPosition.Name = "lblStartingPosition";
            this.lblStartingPosition.Size = new System.Drawing.Size(185, 31);
            this.lblStartingPosition.TabIndex = 2377;
            this.lblStartingPosition.Text = "Starting Position";
            // 
            // lblPositionHeadX
            // 
            this.lblPositionHeadX.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblPositionHeadX.Appearance.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold);
            this.lblPositionHeadX.Appearance.Options.UseBackColor = true;
            this.lblPositionHeadX.Appearance.Options.UseFont = true;
            this.lblPositionHeadX.Appearance.Options.UseTextOptions = true;
            this.lblPositionHeadX.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblPositionHeadX.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblPositionHeadX.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblPositionHeadX.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblPositionHeadX.Location = new System.Drawing.Point(205, 15);
            this.lblPositionHeadX.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblPositionHeadX.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblPositionHeadX.Name = "lblPositionHeadX";
            this.lblPositionHeadX.Size = new System.Drawing.Size(113, 36);
            this.lblPositionHeadX.TabIndex = 2376;
            this.lblPositionHeadX.Text = "Head X";
            // 
            // lblPositionName
            // 
            this.lblPositionName.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblPositionName.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lblPositionName.Appearance.Options.UseBackColor = true;
            this.lblPositionName.Appearance.Options.UseFont = true;
            this.lblPositionName.Appearance.Options.UseTextOptions = true;
            this.lblPositionName.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblPositionName.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblPositionName.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblPositionName.Location = new System.Drawing.Point(15, 15);
            this.lblPositionName.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblPositionName.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblPositionName.Name = "lblPositionName";
            this.lblPositionName.Size = new System.Drawing.Size(186, 36);
            this.lblPositionName.TabIndex = 2375;
            this.lblPositionName.Text = "Position Name";
            // 
            // lblHeadY
            // 
            this.lblHeadY.CalibrationDevice = null;
            this.lblHeadY.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
            this.lblHeadY.DefaultValue = 0;
            this.lblHeadY.Device = "D1944";
            this.lblHeadY.Divisor = 10;
            this.lblHeadY.IsConvertible = false;
            this.lblHeadY.IsScrewingControl = false;
            this.lblHeadY.Location = new System.Drawing.Point(325, 57);
            this.lblHeadY.Mask = "n1";
            this.lblHeadY.MaxValue = 63;
            this.lblHeadY.MinValue = 55;
            this.lblHeadY.Name = "lblHeadY";
            this.lblHeadY.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.lblHeadY.ShowKeyboardOnClick = false;
            this.lblHeadY.Size = new System.Drawing.Size(112, 31);
            this.lblHeadY.TabIndex = 2380;
            this.lblHeadY.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            this.lblHeadY.Value = "0";
            // 
            // lblPositionHeadY
            // 
            this.lblPositionHeadY.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblPositionHeadY.Appearance.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold);
            this.lblPositionHeadY.Appearance.Options.UseBackColor = true;
            this.lblPositionHeadY.Appearance.Options.UseFont = true;
            this.lblPositionHeadY.Appearance.Options.UseTextOptions = true;
            this.lblPositionHeadY.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblPositionHeadY.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblPositionHeadY.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblPositionHeadY.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblPositionHeadY.Location = new System.Drawing.Point(324, 15);
            this.lblPositionHeadY.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblPositionHeadY.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblPositionHeadY.Name = "lblPositionHeadY";
            this.lblPositionHeadY.Size = new System.Drawing.Size(113, 36);
            this.lblPositionHeadY.TabIndex = 2379;
            this.lblPositionHeadY.Text = "Head Y";
            // 
            // kabanNumericButton2
            // 
            this.kabanNumericButton2.CalibrationDevice = null;
            this.kabanNumericButton2.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
            this.kabanNumericButton2.DefaultValue = 0;
            this.kabanNumericButton2.Device = "D1942";
            this.kabanNumericButton2.Divisor = 10;
            this.kabanNumericButton2.IsConvertible = false;
            this.kabanNumericButton2.IsScrewingControl = false;
            this.kabanNumericButton2.Location = new System.Drawing.Point(444, 57);
            this.kabanNumericButton2.Mask = "n1";
            this.kabanNumericButton2.MaxValue = 35;
            this.kabanNumericButton2.MinValue = -5;
            this.kabanNumericButton2.Name = "kabanNumericButton2";
            this.kabanNumericButton2.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.kabanNumericButton2.ShowKeyboardOnClick = false;
            this.kabanNumericButton2.Size = new System.Drawing.Size(112, 31);
            this.kabanNumericButton2.TabIndex = 2382;
            this.kabanNumericButton2.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            this.kabanNumericButton2.Value = "0";
            // 
            // lblPositionHeadZ1
            // 
            this.lblPositionHeadZ1.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblPositionHeadZ1.Appearance.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold);
            this.lblPositionHeadZ1.Appearance.Options.UseBackColor = true;
            this.lblPositionHeadZ1.Appearance.Options.UseFont = true;
            this.lblPositionHeadZ1.Appearance.Options.UseTextOptions = true;
            this.lblPositionHeadZ1.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblPositionHeadZ1.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblPositionHeadZ1.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblPositionHeadZ1.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblPositionHeadZ1.Location = new System.Drawing.Point(443, 15);
            this.lblPositionHeadZ1.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblPositionHeadZ1.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblPositionHeadZ1.Name = "lblPositionHeadZ1";
            this.lblPositionHeadZ1.Size = new System.Drawing.Size(113, 36);
            this.lblPositionHeadZ1.TabIndex = 2381;
            this.lblPositionHeadZ1.Text = "Head Z1";
            // 
            // btnRefresh
            // 
            this.btnRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnRefresh.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnRefresh.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnRefresh.ImageOptions.SvgImage")));
            this.btnRefresh.Location = new System.Drawing.Point(1029, 699);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(40, 37);
            this.btnRefresh.TabIndex = 2417;
            this.btnRefresh.Text = "Refresh";
            // 
            // ucPositionDefinition
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucPositionDefinition";
            this.PageTitle = "";
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(1074, 741);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private KabanDevBase.Plc_Componets.KabanNumericButton kabanNumericButton2;
        private DevExpress.XtraEditors.LabelControl lblPositionHeadZ1;
        private KabanDevBase.Plc_Componets.KabanNumericButton lblHeadY;
        private DevExpress.XtraEditors.LabelControl lblPositionHeadY;
        private KabanDevBase.Plc_Componets.KabanNumericButton kabanNumericButton5;
        private DevExpress.XtraEditors.LabelControl lblStartingPosition;
        private DevExpress.XtraEditors.LabelControl lblPositionHeadX;
        private DevExpress.XtraEditors.LabelControl lblPositionName;
        private DevExpress.XtraEditors.SimpleButton btnRefresh;
    }
}
