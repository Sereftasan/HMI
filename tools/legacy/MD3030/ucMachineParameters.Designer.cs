namespace _MD3030.Presentation.Parameters
{
    partial class ucMachineParameters
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucMachineParameters));
            DevExpress.Utils.SerializableAppearanceObject serializableAppearanceObject1 = new DevExpress.Utils.SerializableAppearanceObject();
            this.tpParametersMain = new KabanDevBase.Controls.KabanTabPageControl(this.components);
            this.xtpHead1Parameters = new DevExpress.XtraTab.XtraTabPage();
            this.xtpHead2Parameters = new DevExpress.XtraTab.XtraTabPage();
            this.xtpHead3Parameters = new DevExpress.XtraTab.XtraTabPage();
            this.xtpHead4Parameters = new DevExpress.XtraTab.XtraTabPage();
            this.xtpGeneralParameters = new DevExpress.XtraTab.XtraTabPage();
            this.xtpPositionDefinition = new DevExpress.XtraTab.XtraTabPage();
            this.xtpHeadParameters = new DevExpress.XtraTab.XtraTabPage();
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
            this.BoxWrapper.Size = new System.Drawing.Size(1082, 680);
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
            this.tpParametersMain.SelectedTabPage = this.xtpHead1Parameters;
            this.tpParametersMain.Size = new System.Drawing.Size(1078, 676);
            this.tpParametersMain.TabIndex = 1;
            this.tpParametersMain.TabPages.AddRange(new DevExpress.XtraTab.XtraTabPage[] {
            this.xtpHead1Parameters,
            this.xtpHead2Parameters,
            this.xtpHead3Parameters,
            this.xtpHead4Parameters,
            this.xtpGeneralParameters,
            this.xtpPositionDefinition,
            this.xtpHeadParameters});
            // 
            // xtpHead1Parameters
            // 
            this.xtpHead1Parameters.Name = "xtpHead1Parameters";
            this.xtpHead1Parameters.PageVisible = false;
            this.xtpHead1Parameters.Size = new System.Drawing.Size(1076, 641);
            this.xtpHead1Parameters.Tag = "ucHead1Parameters";
            this.xtpHead1Parameters.Text = "Head-1";
            // 
            // xtpHead2Parameters
            // 
            this.xtpHead2Parameters.Name = "xtpHead2Parameters";
            this.xtpHead2Parameters.PageVisible = false;
            this.xtpHead2Parameters.Size = new System.Drawing.Size(794, 561);
            this.xtpHead2Parameters.Tag = "ucHead2Parameters";
            this.xtpHead2Parameters.Text = "Head-2";
            // 
            // xtpHead3Parameters
            // 
            this.xtpHead3Parameters.Name = "xtpHead3Parameters";
            this.xtpHead3Parameters.PageVisible = false;
            this.xtpHead3Parameters.Size = new System.Drawing.Size(794, 561);
            this.xtpHead3Parameters.Tag = "ucHead3Parameters";
            this.xtpHead3Parameters.Text = "Head-3";
            // 
            // xtpHead4Parameters
            // 
            this.xtpHead4Parameters.Name = "xtpHead4Parameters";
            this.xtpHead4Parameters.PageVisible = false;
            this.xtpHead4Parameters.Size = new System.Drawing.Size(794, 561);
            this.xtpHead4Parameters.Tag = "ucHead4Parameters";
            this.xtpHead4Parameters.Text = "Head-4";
            // 
            // xtpGeneralParameters
            // 
            this.xtpGeneralParameters.Name = "xtpGeneralParameters";
            this.xtpGeneralParameters.Size = new System.Drawing.Size(794, 561);
            this.xtpGeneralParameters.Tag = "ucGeneralParameters";
            this.xtpGeneralParameters.Text = "Genel";
            // 
            // xtpPositionDefinition
            // 
            this.xtpPositionDefinition.Name = "xtpPositionDefinition";
            this.xtpPositionDefinition.Size = new System.Drawing.Size(794, 561);
            this.xtpPositionDefinition.Tag = "ucPositionDefinition";
            this.xtpPositionDefinition.Text = "Position Defination";
            // 
            // xtpHeadParameters
            // 
            this.xtpHeadParameters.Name = "xtpHeadParameters";
            this.xtpHeadParameters.PageVisible = false;
            this.xtpHeadParameters.Size = new System.Drawing.Size(1076, 641);
            this.xtpHeadParameters.Tag = "ucHeadParameters";
            this.xtpHeadParameters.Text = "Head Parameters";
            // 
            // ucMachineParameters
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucMachineParameters";
            this.PageTitle = "";
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(1082, 680);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.tpParametersMain)).EndInit();
            this.tpParametersMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private KabanDevBase.Controls.KabanTabPageControl tpParametersMain;
        private DevExpress.XtraTab.XtraTabPage xtpHead1Parameters;
        private DevExpress.XtraTab.XtraTabPage xtpHead2Parameters;
        private DevExpress.XtraTab.XtraTabPage xtpHead3Parameters;
        private DevExpress.XtraTab.XtraTabPage xtpHead4Parameters;
        private DevExpress.XtraTab.XtraTabPage xtpGeneralParameters;
        private DevExpress.XtraTab.XtraTabPage xtpPositionDefinition;
        private DevExpress.XtraTab.XtraTabPage xtpHeadParameters;
    }
}
