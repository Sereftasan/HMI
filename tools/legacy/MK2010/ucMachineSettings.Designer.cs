namespace _MK2010.Presentation.Settings.MachineSettings
{
    partial class ucMachineSettings
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucMachineSettings));
            this.btnSave = new KabanDevBase.Controls.KabanSimpleButtonControl(this.components);
            this.ktsActiveUserBarcode = new KabanDevBase.Controls.KabanToggleSwitchControl();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.ktsActiveUserBarcode);
            this.BoxWrapper.Controls.Add(this.btnSave);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.Size = new System.Drawing.Size(800, 680);
            this.BoxWrapper.Text = "Machine Settings";
            // 
            // btnSave
            // 
            this.btnSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSave.Appearance.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnSave.Appearance.Options.UseForeColor = true;
            this.btnSave.HierarchialPath = "ucSawSettings";
            this.btnSave.ImageOptions.AllowGlyphSkinning = DevExpress.Utils.DefaultBoolean.True;
            this.btnSave.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnSave.ImageOptions.SvgImage")));
            this.btnSave.KeyboardDirectInput = false;
            this.btnSave.KeyboardDirectValue = null;
            this.btnSave.KeyboardShiftValue = null;
            this.btnSave.Keys = System.Windows.Forms.Keys.None;
            this.btnSave.Location = new System.Drawing.Point(730, 630);
            this.btnSave.LookAndFeel.SkinName = "The Bezier";
            this.btnSave.LookAndFeel.UseDefaultLookAndFeel = false;
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(65, 45);
            this.btnSave.TabIndex = 80;
            // 
            // ktsActiveUserBarcode
            // 
            this.ktsActiveUserBarcode.Appearance.BackColor = System.Drawing.Color.Transparent;
            this.ktsActiveUserBarcode.Appearance.Options.UseBackColor = true;
            this.ktsActiveUserBarcode.BarManager = null;
            this.ktsActiveUserBarcode.EntityPropertyName = null;
            this.ktsActiveUserBarcode.HierarchialPath = "";
            this.ktsActiveUserBarcode.IsRequired = false;
            this.ktsActiveUserBarcode.LabelText = "Active User Barcode";
            this.ktsActiveUserBarcode.Location = new System.Drawing.Point(12, 32);
            this.ktsActiveUserBarcode.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.ktsActiveUserBarcode.LookAndFeel.UseDefaultLookAndFeel = false;
            this.ktsActiveUserBarcode.Name = "ktsActiveUserBarcode";
            this.ktsActiveUserBarcode.OffText = "Off";
            this.ktsActiveUserBarcode.OnText = "On";
            this.ktsActiveUserBarcode.Size = new System.Drawing.Size(268, 59);
            this.ktsActiveUserBarcode.TabIndex = 96;
            this.ktsActiveUserBarcode.Value = false;
            // 
            // ucMachineSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucMachineSettings";
            this.PageTitle = "Machine Settings";
            this.Size = new System.Drawing.Size(800, 680);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private KabanDevBase.Controls.KabanSimpleButtonControl btnSave;
        private KabanDevBase.Controls.KabanToggleSwitchControl ktsActiveUserBarcode;
    }
}
