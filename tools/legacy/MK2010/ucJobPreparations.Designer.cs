namespace _MK2010.Presentation.Home.Preparations
{
    partial class ucJobPreparations
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
            this.kabanSplitContainer_Main = new KabanDevBase.Controls.KabanSplitContainer(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.kabanSplitContainer_Main)).BeginInit();
            this.kabanSplitContainer_Main.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.kabanSplitContainer_Main);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(1113, 830);
            // 
            // kabanSplitContainer_Main
            // 
            this.kabanSplitContainer_Main.Dock = System.Windows.Forms.DockStyle.Fill;
            this.kabanSplitContainer_Main.Horizontal = false;
            this.kabanSplitContainer_Main.IsSplitterFixed = true;
            this.kabanSplitContainer_Main.Location = new System.Drawing.Point(2, 2);
            this.kabanSplitContainer_Main.Name = "kabanSplitContainer_Main";
            this.kabanSplitContainer_Main.Panel1.Text = "Panel1";
            this.kabanSplitContainer_Main.Panel2.Text = "Panel2";
            this.kabanSplitContainer_Main.Size = new System.Drawing.Size(1109, 826);
            this.kabanSplitContainer_Main.SplitterPosition = 569;
            this.kabanSplitContainer_Main.TabIndex = 0;
            // 
            // ucJobPreparations
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.SkinName = "Office 2019 Colorful";
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.False;
            this.Name = "ucJobPreparations";
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(1113, 830);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.kabanSplitContainer_Main)).EndInit();
            this.kabanSplitContainer_Main.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Panel panel3;
        private System.Windows.Forms.Panel panel2;
        private KabanDevBase.Controls.KabanSplitContainer kabanSplitContainer_Main;
    }
}
