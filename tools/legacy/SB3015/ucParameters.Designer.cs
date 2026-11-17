namespace _SB3015.Presentation.Home.Settings
{
    partial class ucParameters
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
            DevExpress.XtraEditors.Controls.EditorButtonImageOptions editorButtonImageOptions1 = new DevExpress.XtraEditors.Controls.EditorButtonImageOptions();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucParameters));
            DevExpress.Utils.SerializableAppearanceObject serializableAppearanceObject1 = new DevExpress.Utils.SerializableAppearanceObject();
            this.tpParametersMain = new KabanDevBase.Controls.KabanTabPageControl(this.components);
            this.xtpAxisParameters = new DevExpress.XtraTab.XtraTabPage();
            this.xtpAxisSpeeds = new DevExpress.XtraTab.XtraTabPage();
            this.xtpToolSettings = new DevExpress.XtraTab.XtraTabPage();
            this.xtpEntryParameters = new DevExpress.XtraTab.XtraTabPage();
            this.xtpProcessParameters = new DevExpress.XtraTab.XtraTabPage();
            this.xtpEncodersParameters = new DevExpress.XtraTab.XtraTabPage();
            this.xtpOtherParameters = new DevExpress.XtraTab.XtraTabPage();
            this.xtpRobotAxisCalibration = new DevExpress.XtraTab.XtraTabPage();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tpParametersMain)).BeginInit();
            this.tpParametersMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.tpParametersMain);
            this.BoxWrapper.LookAndFeel.SkinName = "The Bezier";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Text = "";
            // 
            // tpParametersMain
            // 
            this.tpParametersMain.Appearance.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(241)))), ((int)(((byte)(241)))));
            this.tpParametersMain.Appearance.Options.UseBackColor = true;
            editorButtonImageOptions1.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("editorButtonImageOptions1.SvgImage")));
            editorButtonImageOptions1.SvgImageSize = new System.Drawing.Size(24, 24);
            this.tpParametersMain.CustomHeaderButtons.AddRange(new DevExpress.XtraTab.Buttons.CustomHeaderButton[] {
            new DevExpress.XtraTab.Buttons.CustomHeaderButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Glyph, "Receipts", -1, true, false, editorButtonImageOptions1, serializableAppearanceObject1, "Receipts", null, null)});
            this.tpParametersMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tpParametersMain.HierarchialPath = "ucManuel";
            this.tpParametersMain.Location = new System.Drawing.Point(2, 2);
            this.tpParametersMain.Name = "tpParametersMain";
            this.tpParametersMain.SelectedTabPage = this.xtpAxisParameters;
            this.tpParametersMain.Size = new System.Drawing.Size(796, 596);
            this.tpParametersMain.TabIndex = 0;
            this.tpParametersMain.TabPages.AddRange(new DevExpress.XtraTab.XtraTabPage[] {
            this.xtpAxisParameters,
            this.xtpAxisSpeeds,
            this.xtpToolSettings,
            this.xtpEntryParameters,
            this.xtpProcessParameters,
            this.xtpEncodersParameters,
            this.xtpOtherParameters,
            this.xtpRobotAxisCalibration});
            // 
            // xtpAxisParameters
            // 
            this.xtpAxisParameters.Name = "xtpAxisParameters";
            this.xtpAxisParameters.Size = new System.Drawing.Size(794, 561);
            this.xtpAxisParameters.Tag = "ucAxisParameters";
            this.xtpAxisParameters.Text = "Axis Parameters";
            // 
            // xtpAxisSpeeds
            // 
            this.xtpAxisSpeeds.Name = "xtpAxisSpeeds";
            this.xtpAxisSpeeds.Size = new System.Drawing.Size(794, 561);
            this.xtpAxisSpeeds.Tag = "ucAxisSpeeds";
            this.xtpAxisSpeeds.Text = "Axis Speeds";
            // 
            // xtpToolSettings
            // 
            this.xtpToolSettings.Name = "xtpToolSettings";
            this.xtpToolSettings.Size = new System.Drawing.Size(794, 561);
            this.xtpToolSettings.Tag = "ucToolSettingParameters";
            this.xtpToolSettings.Text = "Tool Settings";
            // 
            // xtpEntryParameters
            // 
            this.xtpEntryParameters.Name = "xtpEntryParameters";
            this.xtpEntryParameters.Size = new System.Drawing.Size(794, 561);
            this.xtpEntryParameters.Tag = "ucEntryParameters";
            this.xtpEntryParameters.Text = "Entry Parameters";
            // 
            // xtpProcessParameters
            // 
            this.xtpProcessParameters.Name = "xtpProcessParameters";
            this.xtpProcessParameters.Size = new System.Drawing.Size(794, 561);
            this.xtpProcessParameters.Tag = "ucProcessParameters";
            this.xtpProcessParameters.Text = "Process Parameters";
            // 
            // xtpEncodersParameters
            // 
            this.xtpEncodersParameters.Name = "xtpEncodersParameters";
            this.xtpEncodersParameters.Size = new System.Drawing.Size(794, 561);
            this.xtpEncodersParameters.Tag = "ucEncodersParameters";
            this.xtpEncodersParameters.Text = "Encoders Parameters";
            // 
            // xtpOtherParameters
            // 
            this.xtpOtherParameters.Name = "xtpOtherParameters";
            this.xtpOtherParameters.Size = new System.Drawing.Size(794, 561);
            this.xtpOtherParameters.Tag = "ucOtherParameters";
            this.xtpOtherParameters.Text = "Other Parameters";
            // 
            // xtpRobotAxisCalibration
            // 
            this.xtpRobotAxisCalibration.Name = "xtpRobotAxisCalibration";
            this.xtpRobotAxisCalibration.PageVisible = false;
            this.xtpRobotAxisCalibration.Size = new System.Drawing.Size(794, 561);
            this.xtpRobotAxisCalibration.Tag = "ucRobotAxisCalibration";
            this.xtpRobotAxisCalibration.Text = "Robot Axis Calibration";
            // 
            // ucParameters
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucParameters";
            this.PageTitle = "";
            this.ShowCaption = false;
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.tpParametersMain)).EndInit();
            this.tpParametersMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private KabanDevBase.Controls.KabanTabPageControl tpParametersMain;
        private DevExpress.XtraTab.XtraTabPage xtpAxisParameters;
        private DevExpress.XtraTab.XtraTabPage xtpEntryParameters;
        private DevExpress.XtraTab.XtraTabPage xtpAxisSpeeds;
        private DevExpress.XtraTab.XtraTabPage xtpOtherParameters;
        private DevExpress.XtraTab.XtraTabPage xtpProcessParameters;
        private DevExpress.XtraTab.XtraTabPage xtpEncodersParameters;
        private DevExpress.XtraTab.XtraTabPage xtpRobotAxisCalibration;
        private DevExpress.XtraTab.XtraTabPage xtpToolSettings;
    }
}
