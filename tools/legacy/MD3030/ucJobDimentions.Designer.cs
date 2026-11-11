namespace _MD3030.Presentation.Home.Preparations
{
    partial class ucJobDimentions
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
            this.knmX = new KabanDevBase.Plc_Componets.KabanNumericButton();
            this.lblFrameWidthX = new DevExpress.XtraEditors.LabelControl();
            this.knmY = new KabanDevBase.Plc_Componets.KabanNumericButton();
            this.lblFrameDepthY = new DevExpress.XtraEditors.LabelControl();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.Appearance.BackColor = System.Drawing.Color.White;
            this.BoxWrapper.Appearance.Options.UseBackColor = true;
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.knmY);
            this.BoxWrapper.Controls.Add(this.lblFrameDepthY);
            this.BoxWrapper.Controls.Add(this.knmX);
            this.BoxWrapper.Controls.Add(this.lblFrameWidthX);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(306, 196);
            // 
            // knmX
            // 
            this.knmX.CalibrationDevice = null;
            this.knmX.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
            this.knmX.DefaultValue = 0;
            this.knmX.Device = "D1200";
            this.knmX.Divisor = 10;
            this.knmX.IsConvertible = false;
            this.knmX.IsScrewingControl = false;
            this.knmX.Location = new System.Drawing.Point(195, 16);
            this.knmX.Mask = "n1";
            this.knmX.MaxValue = 4100;
            this.knmX.MinValue = 0;
            this.knmX.Name = "knmX";
            this.knmX.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.knmX.ShowKeyboardOnClick = false;
            this.knmX.Size = new System.Drawing.Size(99, 31);
            this.knmX.TabIndex = 2301;
            this.knmX.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            this.knmX.Value = "0";
            // 
            // lblFrameWidthX
            // 
            this.lblFrameWidthX.Appearance.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFrameWidthX.Appearance.Options.UseFont = true;
            this.lblFrameWidthX.Appearance.Options.UseTextOptions = true;
            this.lblFrameWidthX.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblFrameWidthX.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblFrameWidthX.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblFrameWidthX.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblFrameWidthX.Location = new System.Drawing.Point(5, 16);
            this.lblFrameWidthX.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblFrameWidthX.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblFrameWidthX.Name = "lblFrameWidthX";
            this.lblFrameWidthX.Size = new System.Drawing.Size(185, 31);
            this.lblFrameWidthX.TabIndex = 2300;
            this.lblFrameWidthX.Text = "Frame Width (X)";
            // 
            // knmY
            // 
            this.knmY.CalibrationDevice = null;
            this.knmY.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
            this.knmY.DefaultValue = 0;
            this.knmY.Device = "D1202";
            this.knmY.Divisor = 10;
            this.knmY.IsConvertible = false;
            this.knmY.IsScrewingControl = false;
            this.knmY.Location = new System.Drawing.Point(195, 53);
            this.knmY.Mask = "n1";
            this.knmY.MaxValue = 2300;
            this.knmY.MinValue = 0;
            this.knmY.Name = "knmY";
            this.knmY.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.knmY.ShowKeyboardOnClick = false;
            this.knmY.Size = new System.Drawing.Size(99, 31);
            this.knmY.TabIndex = 2303;
            this.knmY.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            this.knmY.Value = "0";
            // 
            // lblFrameDepthY
            // 
            this.lblFrameDepthY.Appearance.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFrameDepthY.Appearance.Options.UseFont = true;
            this.lblFrameDepthY.Appearance.Options.UseTextOptions = true;
            this.lblFrameDepthY.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblFrameDepthY.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblFrameDepthY.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblFrameDepthY.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblFrameDepthY.Location = new System.Drawing.Point(5, 53);
            this.lblFrameDepthY.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblFrameDepthY.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblFrameDepthY.Name = "lblFrameDepthY";
            this.lblFrameDepthY.Size = new System.Drawing.Size(185, 31);
            this.lblFrameDepthY.TabIndex = 2302;
            this.lblFrameDepthY.Text = "Frame Depth (Y)";
            // 
            // ucJobDimentions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucJobDimentions";
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(306, 196);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        public KabanDevBase.Plc_Componets.KabanNumericButton knmY;
        private DevExpress.XtraEditors.LabelControl lblFrameDepthY;
        public KabanDevBase.Plc_Componets.KabanNumericButton knmX;
        private DevExpress.XtraEditors.LabelControl lblFrameWidthX;
    }
}
