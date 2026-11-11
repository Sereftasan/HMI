namespace _MK2010.Presentation.Home.Parameters
{
    partial class ucParametersEntryPw
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucParametersEntryPw));
            this.txtPassword = new KabanDevBase.Controls.KabanTexboxControl();
            this.btnConfirm = new KabanDevBase.Plc_Componets.KabanButton();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.btnConfirm);
            this.BoxWrapper.Controls.Add(this.txtPassword);
            this.BoxWrapper.LookAndFeel.SkinName = "The Bezier";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(204, 113);
            this.BoxWrapper.Text = "Please Enter Password";
            this.BoxWrapper.Paint += new System.Windows.Forms.PaintEventHandler(this.BoxWrapper_Paint);
            // 
            // txtPassword
            // 
            this.txtPassword.BarManager = null;
            this.txtPassword.Device = null;
            this.txtPassword.EntityPropertyName = null;
            this.txtPassword.HierarchialPath = null;
            this.txtPassword.IsPasswordField = true;
            this.txtPassword.IsRequired = false;
            this.txtPassword.LabelText = "Enter password to access";
            this.txtPassword.Location = new System.Drawing.Point(5, 5);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.Size = new System.Drawing.Size(193, 59);
            this.txtPassword.TabIndex = 0;
            this.txtPassword.TextBoxText = "";
            this.txtPassword.Load += new System.EventHandler(this.txtPassword_Load);
            // 
            // btnConfirm
            // 
            this.btnConfirm.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnConfirm.ButtonTag = null;
            this.btnConfirm.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Set;
            this.btnConfirm.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnConfirm.DefaultValue = 1;
            this.btnConfirm.Device = "D72";
            this.btnConfirm.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnConfirm.ImageOptions.SvgImage")));
            this.btnConfirm.IsReferanceButton = false;
            this.btnConfirm.IsScrewingControl = false;
            this.btnConfirm.Location = new System.Drawing.Point(86, 66);
            this.btnConfirm.Name = "btnConfirm";
            this.btnConfirm.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnConfirm.ProcessDevice = false;
            this.btnConfirm.ResetDevice = null;
            this.btnConfirm.Size = new System.Drawing.Size(40, 40);
            this.btnConfirm.TabIndex = 4;
            this.btnConfirm.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            this.btnConfirm.Click += new System.EventHandler(this.btnConfirm_Click);
            // 
            // ucParametersEntryPw
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.SkinName = "The Bezier";
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.LookAndFeel.UseDefaultLookAndFeel = false;
            this.Name = "ucParametersEntryPw";
            this.PageTitle = "Please Enter Password";
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(204, 113);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        public KabanDevBase.Controls.KabanTexboxControl txtPassword;
        public KabanDevBase.Plc_Componets.KabanButton btnConfirm;
    }
}
