namespace _MD3030.Presentation.Parameters
{
    partial class ucIOWatchs
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucIOWatchs));
            DevExpress.Utils.SerializableAppearanceObject serializableAppearanceObject1 = new DevExpress.Utils.SerializableAppearanceObject();
            this.tpParametersMain = new KabanDevBase.Controls.KabanTabPageControl(this.components);
            this.xtpHead1 = new DevExpress.XtraTab.XtraTabPage();
            this.xtpHead2 = new DevExpress.XtraTab.XtraTabPage();
            this.xtpHead3 = new DevExpress.XtraTab.XtraTabPage();
            this.xtpHead4 = new DevExpress.XtraTab.XtraTabPage();
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
            this.tpParametersMain.SelectedTabPage = this.xtpHead1;
            this.tpParametersMain.Size = new System.Drawing.Size(1078, 676);
            this.tpParametersMain.TabIndex = 1;
            this.tpParametersMain.TabPages.AddRange(new DevExpress.XtraTab.XtraTabPage[] {
            this.xtpHead1,
            this.xtpHead2,
            this.xtpHead3,
            this.xtpHead4});
            // 
            // xtpHead1
            // 
            this.xtpHead1.Name = "xtpHead1";
            this.xtpHead1.Size = new System.Drawing.Size(1076, 641);
            this.xtpHead1.Text = "Head1";
            // 
            // xtpHead2
            // 
            this.xtpHead2.Name = "xtpHead2";
            this.xtpHead2.Size = new System.Drawing.Size(0, 0);
            this.xtpHead2.Text = "Head2";
            // 
            // xtpHead3
            // 
            this.xtpHead3.Name = "xtpHead3";
            this.xtpHead3.Size = new System.Drawing.Size(0, 0);
            this.xtpHead3.Text = "Head3";
            // 
            // xtpHead4
            // 
            this.xtpHead4.Name = "xtpHead4";
            this.xtpHead4.Size = new System.Drawing.Size(0, 0);
            this.xtpHead4.Text = "Head4";
            // 
            // ucIOWatchs
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucIOWatchs";
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
        private DevExpress.XtraTab.XtraTabPage xtpHead1;
        private DevExpress.XtraTab.XtraTabPage xtpHead2;
        private DevExpress.XtraTab.XtraTabPage xtpHead3;
        private DevExpress.XtraTab.XtraTabPage xtpHead4;
    }
}
