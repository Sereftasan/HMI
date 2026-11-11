namespace _MK2010.Presentation.Home.Settings
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
            this.xtpReports = new DevExpress.XtraTab.XtraTabPage();
            this.xtpProductSettings = new DevExpress.XtraTab.XtraTabPage();
            this.xtpDemo = new DevExpress.XtraTab.XtraTabPage();
            this.xtpSystemSettings2 = new DevExpress.XtraTab.XtraTabPage();
            this.xtpSystemSettings = new DevExpress.XtraTab.XtraTabPage();
            this.xtpCalibration = new DevExpress.XtraTab.XtraTabPage();
            this.tpParametersMain = new KabanDevBase.Controls.KabanTabPageControl(this.components);
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
            // xtpReports
            // 
            this.xtpReports.Name = "xtpReports";
            this.xtpReports.Size = new System.Drawing.Size(794, 561);
            this.xtpReports.Tag = "ucMachineRuntime";
            this.xtpReports.Text = "Reports";
            // 
            // xtpProductSettings
            // 
            this.xtpProductSettings.Name = "xtpProductSettings";
            this.xtpProductSettings.Size = new System.Drawing.Size(794, 561);
            this.xtpProductSettings.Tag = "ucProductionParameters";
            this.xtpProductSettings.Text = "Production Parameters";
            // 
            // xtpDemo
            // 
            this.xtpDemo.Name = "xtpDemo";
            this.xtpDemo.Size = new System.Drawing.Size(794, 561);
            this.xtpDemo.Tag = "ucDemo";
            this.xtpDemo.Text = "Demo";
            // 
            // xtpSystemSettings2
            // 
            this.xtpSystemSettings2.Name = "xtpSystemSettings2";
            this.xtpSystemSettings2.Size = new System.Drawing.Size(794, 561);
            this.xtpSystemSettings2.Tag = "ucSystemSettings2";
            this.xtpSystemSettings2.Text = "System Parameters 2";
            // 
            // xtpSystemSettings
            // 
            this.xtpSystemSettings.Name = "xtpSystemSettings";
            this.xtpSystemSettings.Size = new System.Drawing.Size(794, 561);
            this.xtpSystemSettings.Tag = "ucSystemSettings";
            this.xtpSystemSettings.Text = "System Parameters";
            // 
            // xtpCalibration
            // 
            this.xtpCalibration.Name = "xtpCalibration";
            this.xtpCalibration.Size = new System.Drawing.Size(794, 561);
            this.xtpCalibration.Tag = "ucCalibration";
            this.xtpCalibration.Text = "Calibration";
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
            this.tpParametersMain.SelectedTabPage = this.xtpCalibration;
            this.tpParametersMain.Size = new System.Drawing.Size(796, 596);
            this.tpParametersMain.TabIndex = 0;
            this.tpParametersMain.TabPages.AddRange(new DevExpress.XtraTab.XtraTabPage[] {
            this.xtpProductSettings,
            this.xtpCalibration,
            this.xtpSystemSettings,
            this.xtpSystemSettings2,
            this.xtpDemo,
            this.xtpReports});
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
        private DevExpress.XtraTab.XtraTabPage xtpCalibration;
        private DevExpress.XtraTab.XtraTabPage xtpSystemSettings;
        private DevExpress.XtraTab.XtraTabPage xtpSystemSettings2;
        private DevExpress.XtraTab.XtraTabPage xtpDemo;
        private DevExpress.XtraTab.XtraTabPage xtpProductSettings;
        private DevExpress.XtraTab.XtraTabPage xtpReports;
    }
}
