namespace _MK2010.Presentation.Home.Parameters
{
    partial class ucDemo
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
            this.lblHeadDemo = new DevExpress.XtraEditors.LabelControl();
            this.kabanLambButton44 = new KabanDevBase.Plc_Componets.KabanLambButton();
            this.lblDemo = new DevExpress.XtraEditors.LabelControl();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.kabanLambButton44);
            this.BoxWrapper.Controls.Add(this.lblDemo);
            this.BoxWrapper.Controls.Add(this.lblHeadDemo);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(312, 110);
            // 
            // lblHeadDemo
            // 
            this.lblHeadDemo.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblHeadDemo.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lblHeadDemo.Appearance.Options.UseBackColor = true;
            this.lblHeadDemo.Appearance.Options.UseFont = true;
            this.lblHeadDemo.Appearance.Options.UseTextOptions = true;
            this.lblHeadDemo.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblHeadDemo.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblHeadDemo.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblHeadDemo.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblHeadDemo.Location = new System.Drawing.Point(18, 15);
            this.lblHeadDemo.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblHeadDemo.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblHeadDemo.Name = "lblHeadDemo";
            this.lblHeadDemo.Size = new System.Drawing.Size(258, 38);
            this.lblHeadDemo.TabIndex = 1320;
            this.lblHeadDemo.Text = "Demo";
            // 
            // kabanLambButton44
            // 
            this.kabanLambButton44.ButtonTag = null;
            this.kabanLambButton44.ButtonText = "";
            this.kabanLambButton44.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Alternate;
            this.kabanLambButton44.DefaultValue = null;
            this.kabanLambButton44.Device = "M99";
            this.kabanLambButton44.IsHeadControlButton = false;
            this.kabanLambButton44.IsReferanceButton = false;
            this.kabanLambButton44.IsScrewingControl = false;
            this.kabanLambButton44.IsSecurityButton = false;
            this.kabanLambButton44.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.kabanLambButton44.LambDevice = "M99";
            this.kabanLambButton44.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.kabanLambButton44.Location = new System.Drawing.Point(163, 59);
            this.kabanLambButton44.Name = "kabanLambButton44";
            this.kabanLambButton44.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.kabanLambButton44.ResetDevice = null;
            this.kabanLambButton44.Size = new System.Drawing.Size(113, 38);
            this.kabanLambButton44.TabIndex = 1322;
            this.kabanLambButton44.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // lblDemo
            // 
            this.lblDemo.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblDemo.Appearance.Options.UseFont = true;
            this.lblDemo.Appearance.Options.UseTextOptions = true;
            this.lblDemo.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblDemo.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblDemo.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblDemo.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblDemo.Location = new System.Drawing.Point(18, 59);
            this.lblDemo.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblDemo.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblDemo.Name = "lblDemo";
            this.lblDemo.Size = new System.Drawing.Size(139, 38);
            this.lblDemo.TabIndex = 1321;
            this.lblDemo.Text = "Demo";
            // 
            // ucDemo
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucDemo";
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(312, 110);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private DevExpress.XtraEditors.LabelControl lblHeadDemo;
        private KabanDevBase.Plc_Componets.KabanLambButton kabanLambButton44;
        private DevExpress.XtraEditors.LabelControl lblDemo;
    }
}
