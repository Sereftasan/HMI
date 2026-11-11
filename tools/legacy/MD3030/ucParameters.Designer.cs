namespace _MD3030.Presentation.Home.Parameters
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
            DevExpress.XtraEditors.Controls.EditorButtonImageOptions editorButtonImageOptions1 = new DevExpress.XtraEditors.Controls.EditorButtonImageOptions();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucParameters));
            DevExpress.Utils.SerializableAppearanceObject serializableAppearanceObject1 = new DevExpress.Utils.SerializableAppearanceObject();
            this.tpParametersMain = new KabanDevBase.Controls.KabanTabPageControl();
            this.xtpAxisCalibration = new DevExpress.XtraTab.XtraTabPage();
            this.xtpMachineParameters = new DevExpress.XtraTab.XtraTabPage();
            this.xtpSpeedParameters = new DevExpress.XtraTab.XtraTabPage();
            this.xtpProcessParameters = new DevExpress.XtraTab.XtraTabPage();
            this.xtpTestParameters = new DevExpress.XtraTab.XtraTabPage();
            this.xtpOffset = new DevExpress.XtraTab.XtraTabPage();
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
            this.BoxWrapper.Size = new System.Drawing.Size(1113, 600);
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
            this.tpParametersMain.SelectedTabPage = this.xtpAxisCalibration;
            this.tpParametersMain.Size = new System.Drawing.Size(1109, 596);
            this.tpParametersMain.TabIndex = 0;
            this.tpParametersMain.TabPages.AddRange(new DevExpress.XtraTab.XtraTabPage[] {
            this.xtpAxisCalibration,
            this.xtpMachineParameters,
            this.xtpSpeedParameters,
            this.xtpProcessParameters,
            this.xtpTestParameters,
            this.xtpOffset});
            // 
            // xtpAxisCalibration
            // 
            this.xtpAxisCalibration.Name = "xtpAxisCalibration";
            this.xtpAxisCalibration.Size = new System.Drawing.Size(1107, 561);
            this.xtpAxisCalibration.Tag = "ucAxisParameters";
            this.xtpAxisCalibration.Text = "Axis Calibration";
            // 
            // xtpMachineParameters
            // 
            this.xtpMachineParameters.Name = "xtpMachineParameters";
            this.xtpMachineParameters.Size = new System.Drawing.Size(1107, 561);
            this.xtpMachineParameters.Tag = "ucMachineParameters";
            this.xtpMachineParameters.Text = "Machine Parameters";
            // 
            // xtpSpeedParameters
            // 
            this.xtpSpeedParameters.Name = "xtpSpeedParameters";
            this.xtpSpeedParameters.Size = new System.Drawing.Size(794, 561);
            this.xtpSpeedParameters.Tag = "ucSpeedParameters";
            this.xtpSpeedParameters.Text = "Speed Parameters";
            // 
            // xtpProcessParameters
            // 
            this.xtpProcessParameters.Name = "xtpProcessParameters";
            this.xtpProcessParameters.Size = new System.Drawing.Size(1107, 561);
            this.xtpProcessParameters.Tag = "ucProcessParameters";
            this.xtpProcessParameters.Text = "Process Parameters";
            // 
            // xtpTestParameters
            // 
            this.xtpTestParameters.Name = "xtpTestParameters";
            this.xtpTestParameters.PageVisible = false;
            this.xtpTestParameters.Size = new System.Drawing.Size(1107, 561);
            this.xtpTestParameters.Tag = "ucTesterParameters";
            this.xtpTestParameters.Text = "Test Parameters";
            // 
            // xtpOffset
            // 
            this.xtpOffset.Name = "xtpOffset";
            this.xtpOffset.Size = new System.Drawing.Size(1107, 561);
            this.xtpOffset.Tag = "ucOffset";
            this.xtpOffset.Text = "Offset";
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
            this.Size = new System.Drawing.Size(1113, 600);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.tpParametersMain)).EndInit();
            this.tpParametersMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private KabanDevBase.Controls.KabanTabPageControl tpParametersMain;
        private DevExpress.XtraTab.XtraTabPage xtpAxisCalibration;
        private DevExpress.XtraTab.XtraTabPage xtpSpeedParameters;
        private DevExpress.XtraTab.XtraTabPage xtpProcessParameters;
        private DevExpress.XtraTab.XtraTabPage xtpTestParameters;
        private DevExpress.XtraTab.XtraTabPage xtpMachineParameters;
        private DevExpress.XtraTab.XtraTabPage xtpOffset;
    }
}
