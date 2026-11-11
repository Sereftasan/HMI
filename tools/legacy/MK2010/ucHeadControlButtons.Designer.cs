namespace _MK2010.Presentation.Home.Manuel
{
    partial class ucHeadControlButtons
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucHeadControlButtons));
            this.kabanTrackBar1 = new KabanDevBase.Plc_Componets.KabanTrackBar();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.kabanTrackBar1);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(861, 61);
            this.BoxWrapper.Text = "";
            // 
            // kabanTrackBar1
            // 
            this.kabanTrackBar1.Device = "D409";
            this.kabanTrackBar1.FSpeed = ((System.Collections.Generic.Dictionary<int, int>)(resources.GetObject("kabanTrackBar1.FSpeed")));
            this.kabanTrackBar1.Location = new System.Drawing.Point(5, 5);
            this.kabanTrackBar1.MaxValue = 100;
            this.kabanTrackBar1.MinValue = 0;
            this.kabanTrackBar1.Name = "kabanTrackBar1";
            this.kabanTrackBar1.Size = new System.Drawing.Size(247, 49);
            this.kabanTrackBar1.TabIndex = 7;
            // 
            // ucHeadControlButtons
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.IsTranslate = true;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucHeadControlButtons";
            this.PageTitle = "";
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(861, 61);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private KabanDevBase.Plc_Componets.KabanTrackBar kabanTrackBar1;
    }
}
