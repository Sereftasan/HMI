namespace _MK2010.Presentation.Home.Manuel
{
    partial class ucManuelWrapper
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
            this.splitContainerMain = new System.Windows.Forms.SplitContainer();
            this.ucHeadControlButtons1 = new _MK2010.Presentation.Home.Manuel.ucHeadControlButtons();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).BeginInit();
            this.splitContainerMain.Panel1.SuspendLayout();
            this.splitContainerMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.splitContainerMain);
            this.BoxWrapper.LookAndFeel.SkinName = "The Bezier";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            // 
            // splitContainerMain
            // 
            this.splitContainerMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainerMain.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.splitContainerMain.IsSplitterFixed = true;
            this.splitContainerMain.Location = new System.Drawing.Point(2, 2);
            this.splitContainerMain.Name = "splitContainerMain";
            this.splitContainerMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainerMain.Panel1
            // 
            this.splitContainerMain.Panel1.Controls.Add(this.ucHeadControlButtons1);
            this.splitContainerMain.Panel1Collapsed = true;
            this.splitContainerMain.Panel1MinSize = 0;
            this.splitContainerMain.Size = new System.Drawing.Size(796, 596);
            this.splitContainerMain.SplitterDistance = 25;
            this.splitContainerMain.TabIndex = 1;
            // 
            // ucHeadControlButtons1
            // 
            this.ucHeadControlButtons1.BarManager = null;
            this.ucHeadControlButtons1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ucHeadControlButtons1.IsRequired = false;
            this.ucHeadControlButtons1.IsTranslate = true;
            this.ucHeadControlButtons1.Location = new System.Drawing.Point(0, 0);
            this.ucHeadControlButtons1.LookAndFeel.TouchScaleFactor = 1.5F;
            this.ucHeadControlButtons1.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.ucHeadControlButtons1.Name = "ucHeadControlButtons1";
            this.ucHeadControlButtons1.PageTitle = "";
            this.ucHeadControlButtons1.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.ucHeadControlButtons1.ShowCaption = false;
            this.ucHeadControlButtons1.Size = new System.Drawing.Size(796, 25);
            this.ucHeadControlButtons1.TabIndex = 1;
            // 
            // ucManuelWrapper
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucManuelWrapper";
            this.ShowCaption = false;
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            this.splitContainerMain.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainerMain)).EndInit();
            this.splitContainerMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainerMain;
        public ucHeadControlButtons ucHeadControlButtons1;
    }
}
