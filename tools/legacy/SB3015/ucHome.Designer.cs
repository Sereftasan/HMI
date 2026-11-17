namespace _SB3015.Presentation.Home
{
    partial class ucHome
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucHome));
            DevExpress.Utils.SerializableAppearanceObject serializableAppearanceObject1 = new DevExpress.Utils.SerializableAppearanceObject();
            this.tpHomeMain = new KabanDevBase.Controls.KabanTabPageControl(this.components);
            this.xtpJobPreparations = new DevExpress.XtraTab.XtraTabPage();
            this.xtpManuel = new DevExpress.XtraTab.XtraTabPage();
            this.xtpParameters = new DevExpress.XtraTab.XtraTabPage();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tpHomeMain)).BeginInit();
            this.tpHomeMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.tpHomeMain);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Text = "";
            // 
            // tpHomeMain
            // 
            this.tpHomeMain.Appearance.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(241)))), ((int)(((byte)(241)))));
            this.tpHomeMain.Appearance.Options.UseBackColor = true;
            editorButtonImageOptions1.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("editorButtonImageOptions1.SvgImage")));
            editorButtonImageOptions1.SvgImageSize = new System.Drawing.Size(24, 24);
            this.tpHomeMain.CustomHeaderButtons.AddRange(new DevExpress.XtraTab.Buttons.CustomHeaderButton[] {
            new DevExpress.XtraTab.Buttons.CustomHeaderButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Glyph, "Receipts", -1, true, false, editorButtonImageOptions1, serializableAppearanceObject1, "Receipts", null, null)});
            this.tpHomeMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tpHomeMain.HierarchialPath = "ucHome";
            this.tpHomeMain.Location = new System.Drawing.Point(2, 2);
            this.tpHomeMain.LookAndFeel.SkinName = "The Bezier";
            this.tpHomeMain.LookAndFeel.UseDefaultLookAndFeel = false;
            this.tpHomeMain.Name = "tpHomeMain";
            this.tpHomeMain.SelectedTabPage = this.xtpJobPreparations;
            this.tpHomeMain.Size = new System.Drawing.Size(796, 596);
            this.tpHomeMain.TabIndex = 1;
            this.tpHomeMain.TabPages.AddRange(new DevExpress.XtraTab.XtraTabPage[] {
            this.xtpJobPreparations,
            this.xtpManuel,
            this.xtpParameters});
            // 
            // xtpJobPreparations
            // 
            this.xtpJobPreparations.Name = "xtpJobPreparations";
            this.xtpJobPreparations.Size = new System.Drawing.Size(794, 561);
            this.xtpJobPreparations.Text = "Job Preparations";
            // 
            // xtpManuel
            // 
            this.xtpManuel.Name = "xtpManuel";
            this.xtpManuel.Size = new System.Drawing.Size(794, 561);
            this.xtpManuel.Text = "Manuel Control";
            // 
            // xtpParameters
            // 
            this.xtpParameters.Name = "xtpParameters";
            this.xtpParameters.Size = new System.Drawing.Size(794, 561);
            this.xtpParameters.Text = "Machine Parameters";
            // 
            // ucHome
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucHome";
            this.PageTitle = "";
            this.ShowCaption = false;
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.tpHomeMain)).EndInit();
            this.tpHomeMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private KabanDevBase.Controls.KabanTabPageControl tpHomeMain;
        private DevExpress.XtraTab.XtraTabPage xtpJobPreparations;
        private DevExpress.XtraTab.XtraTabPage xtpManuel;
        private DevExpress.XtraTab.XtraTabPage xtpParameters;
    }
}
