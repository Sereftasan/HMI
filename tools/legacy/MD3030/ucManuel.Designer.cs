namespace MD3030.Presentation.Home.Manuel
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
            this.xtpGeneral = new DevExpress.XtraTab.XtraTabPage();
            this.xtpHead1 = new DevExpress.XtraTab.XtraTabPage();
            this.xtpHead2 = new DevExpress.XtraTab.XtraTabPage();
            this.xtpHead3 = new DevExpress.XtraTab.XtraTabPage();
            this.xtpHead4 = new DevExpress.XtraTab.XtraTabPage();
            this.xtpManualMovement = new DevExpress.XtraTab.XtraTabPage();
            this.xtpInputOutputWatch = new DevExpress.XtraTab.XtraTabPage();
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
            this.tpManuelMain.SelectedTabPage = this.xtpGeneral;
            this.tpManuelMain.Size = new System.Drawing.Size(796, 596);
            this.tpManuelMain.TabIndex = 2;
            this.tpManuelMain.TabPages.AddRange(new DevExpress.XtraTab.XtraTabPage[] {
            this.xtpGeneral,
            this.xtpHead1,
            this.xtpHead2,
            this.xtpHead3,
            this.xtpHead4,
            this.xtpManualMovement,
            this.xtpInputOutputWatch});
            // 
            // xtpGeneral
            // 
            this.xtpGeneral.AutoScroll = true;
            this.xtpGeneral.Name = "xtpGeneral";
            this.xtpGeneral.Size = new System.Drawing.Size(794, 561);
            this.xtpGeneral.Text = "General";
            // 
            // xtpHead1
            // 
            this.xtpHead1.AutoScroll = true;
            this.xtpHead1.Name = "xtpHead1";
            this.xtpHead1.PageVisible = false;
            this.xtpHead1.Size = new System.Drawing.Size(794, 561);
            this.xtpHead1.Text = "Head-1";
            // 
            // xtpHead2
            // 
            this.xtpHead2.Name = "xtpHead2";
            this.xtpHead2.PageVisible = false;
            this.xtpHead2.Size = new System.Drawing.Size(794, 561);
            this.xtpHead2.Text = "Head-2";
            // 
            // xtpHead3
            // 
            this.xtpHead3.Name = "xtpHead3";
            this.xtpHead3.PageVisible = false;
            this.xtpHead3.Size = new System.Drawing.Size(794, 561);
            this.xtpHead3.Text = "Head-3";
            // 
            // xtpHead4
            // 
            this.xtpHead4.Name = "xtpHead4";
            this.xtpHead4.PageVisible = false;
            this.xtpHead4.Size = new System.Drawing.Size(794, 561);
            this.xtpHead4.Text = "Head-4";
            // 
            // xtpManualMovement
            // 
            this.xtpManualMovement.Name = "xtpManualMovement";
            this.xtpManualMovement.Size = new System.Drawing.Size(794, 561);
            this.xtpManualMovement.Text = "Manual Movement";
            // 
            // xtpInputOutputWatch
            // 
            this.xtpInputOutputWatch.Name = "xtpInputOutputWatch";
            this.xtpInputOutputWatch.Size = new System.Drawing.Size(794, 561);
            this.xtpInputOutputWatch.Text = "Input Output Watch";
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
        private DevExpress.XtraTab.XtraTabPage xtpGeneral;
        private DevExpress.XtraTab.XtraTabPage xtpHead1;
        private DevExpress.XtraTab.XtraTabPage xtpHead2;
        private DevExpress.XtraTab.XtraTabPage xtpHead3;
        private DevExpress.XtraTab.XtraTabPage xtpHead4;
        private DevExpress.XtraTab.XtraTabPage xtpManualMovement;
        private DevExpress.XtraTab.XtraTabPage xtpInputOutputWatch;
    }
}
