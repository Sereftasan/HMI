namespace _MK2010.Presentation.Home.Manuel
{
    partial class ucManuel
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucManuel));
            DevExpress.Utils.SerializableAppearanceObject serializableAppearanceObject1 = new DevExpress.Utils.SerializableAppearanceObject();
            this.tpManuelMain = new KabanDevBase.Controls.KabanTabPageControl(this.components);
            this.xtpManuelKontrol = new DevExpress.XtraTab.XtraTabPage();
            this.xtpReferences = new DevExpress.XtraTab.XtraTabPage();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tpManuelMain)).BeginInit();
            this.tpManuelMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.tpManuelMain);
            this.BoxWrapper.LookAndFeel.SkinName = "The Bezier";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Text = "Manuel Control";
            // 
            // tpManuelMain
            // 
            this.tpManuelMain.Appearance.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(241)))), ((int)(((byte)(241)))));
            this.tpManuelMain.Appearance.Options.UseBackColor = true;
            editorButtonImageOptions1.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("editorButtonImageOptions1.SvgImage")));
            editorButtonImageOptions1.SvgImageSize = new System.Drawing.Size(24, 24);
            this.tpManuelMain.CustomHeaderButtons.AddRange(new DevExpress.XtraTab.Buttons.CustomHeaderButton[] {
            new DevExpress.XtraTab.Buttons.CustomHeaderButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Glyph, "Receipts", -1, true, false, editorButtonImageOptions1, serializableAppearanceObject1, "Receipts", null, null)});
            this.tpManuelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tpManuelMain.HierarchialPath = "ucManuel";
            this.tpManuelMain.Location = new System.Drawing.Point(2, 2);
            this.tpManuelMain.Name = "tpManuelMain";
            this.tpManuelMain.SelectedTabPage = this.xtpManuelKontrol;
            this.tpManuelMain.Size = new System.Drawing.Size(796, 596);
            this.tpManuelMain.TabIndex = 2;
            this.tpManuelMain.TabPages.AddRange(new DevExpress.XtraTab.XtraTabPage[] {
            this.xtpManuelKontrol,
            this.xtpReferences});
            // 
            // xtpManuelKontrol
            // 
            this.xtpManuelKontrol.Name = "xtpManuelKontrol";
            this.xtpManuelKontrol.Size = new System.Drawing.Size(794, 561);
            this.xtpManuelKontrol.Text = "Manuel Control";
            // 
            // xtpReferences
            // 
            this.xtpReferences.Name = "xtpReferences";
            this.xtpReferences.Size = new System.Drawing.Size(0, 0);
            this.xtpReferences.Text = "References";
            // 
            // ucManuel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.SkinName = "The Bezier";
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.LookAndFeel.UseDefaultLookAndFeel = false;
            this.Name = "ucManuel";
            this.PageTitle = "Manuel Control";
            this.ShowCaption = false;
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.tpManuelMain)).EndInit();
            this.tpManuelMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private KabanDevBase.Controls.KabanTabPageControl tpManuelMain;
        private DevExpress.XtraTab.XtraTabPage xtpManuelKontrol;
        private DevExpress.XtraTab.XtraTabPage xtpReferences;
    }
}
