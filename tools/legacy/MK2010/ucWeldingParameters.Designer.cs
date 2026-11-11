namespace _MK2010.Presentation.Home.Parameters
{
    partial class ucWeldingParameters
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
            this.lbl = new DevExpress.XtraEditors.LabelControl();
            this.btnManel = new KabanDevBase.Plc_Componets.KabanButton();
            this.lblSıfırNormalKaynakSecimi = new DevExpress.XtraEditors.LabelControl();
            this.btnNormalKaynak = new KabanDevBase.Plc_Componets.KabanButton();
            this.kabanLamb71 = new KabanDevBase.Plc_Componets.KabanLamb();
            this.kabanLamb1 = new KabanDevBase.Plc_Componets.KabanLamb();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.kabanLamb1);
            this.BoxWrapper.Controls.Add(this.kabanLamb71);
            this.BoxWrapper.Controls.Add(this.btnNormalKaynak);
            this.BoxWrapper.Controls.Add(this.lblSıfırNormalKaynakSecimi);
            this.BoxWrapper.Controls.Add(this.btnManel);
            this.BoxWrapper.Controls.Add(this.lbl);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(500, 157);
            this.BoxWrapper.Paint += new System.Windows.Forms.PaintEventHandler(this.BoxWrapper_Paint);
            // 
            // lbl
            // 
            this.lbl.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lbl.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lbl.Appearance.Options.UseBackColor = true;
            this.lbl.Appearance.Options.UseFont = true;
            this.lbl.Appearance.Options.UseTextOptions = true;
            this.lbl.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lbl.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lbl.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lbl.Location = new System.Drawing.Point(5, 3);
            this.lbl.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lbl.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lbl.Name = "lbl";
            this.lbl.Size = new System.Drawing.Size(492, 32);
            this.lbl.TabIndex = 444;
            this.lbl.Text = "Welding Parameters";
            // 
            // btnManel
            // 
            this.btnManel.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnManel.ButtonTag = null;
            this.btnManel.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Alternate;
            this.btnManel.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnManel.DefaultValue = 0;
            this.btnManel.Device = "M1006";
            this.btnManel.IsReferanceButton = false;
            this.btnManel.IsScrewingControl = false;
            this.btnManel.Location = new System.Drawing.Point(194, 41);
            this.btnManel.Name = "btnManel";
            this.btnManel.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnManel.ProcessDevice = false;
            this.btnManel.ResetDevice = null;
            this.btnManel.Size = new System.Drawing.Size(183, 50);
            this.btnManel.TabIndex = 493;
            this.btnManel.Text = "Manual";
            this.btnManel.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // lblSıfırNormalKaynakSecimi
            // 
            this.lblSıfırNormalKaynakSecimi.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblSıfırNormalKaynakSecimi.Appearance.Options.UseFont = true;
            this.lblSıfırNormalKaynakSecimi.Appearance.Options.UseTextOptions = true;
            this.lblSıfırNormalKaynakSecimi.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblSıfırNormalKaynakSecimi.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblSıfırNormalKaynakSecimi.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblSıfırNormalKaynakSecimi.Location = new System.Drawing.Point(5, 41);
            this.lblSıfırNormalKaynakSecimi.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblSıfırNormalKaynakSecimi.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblSıfırNormalKaynakSecimi.Name = "lblSıfırNormalKaynakSecimi";
            this.lblSıfırNormalKaynakSecimi.Size = new System.Drawing.Size(183, 106);
            this.lblSıfırNormalKaynakSecimi.TabIndex = 494;
            this.lblSıfırNormalKaynakSecimi.Text = "Zero/Normal Welding Selection";
            this.lblSıfırNormalKaynakSecimi.Click += new System.EventHandler(this.lblSıcaklıToleransı_Click);
            // 
            // btnNormalKaynak
            // 
            this.btnNormalKaynak.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnNormalKaynak.ButtonTag = null;
            this.btnNormalKaynak.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Alternate;
            this.btnNormalKaynak.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnNormalKaynak.DefaultValue = 0;
            this.btnNormalKaynak.Device = "M1003";
            this.btnNormalKaynak.IsReferanceButton = false;
            this.btnNormalKaynak.IsScrewingControl = false;
            this.btnNormalKaynak.Location = new System.Drawing.Point(194, 97);
            this.btnNormalKaynak.Name = "btnNormalKaynak";
            this.btnNormalKaynak.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnNormalKaynak.ProcessDevice = false;
            this.btnNormalKaynak.ResetDevice = null;
            this.btnNormalKaynak.Size = new System.Drawing.Size(183, 50);
            this.btnNormalKaynak.TabIndex = 495;
            this.btnNormalKaynak.Text = "Zero/Normal Welding";
            this.btnNormalKaynak.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            this.btnNormalKaynak.Click += new System.EventHandler(this.btnNormalKaynak_Click);
            // 
            // kabanLamb71
            // 
            this.kabanLamb71.DefaultValue = null;
            this.kabanLamb71.LambDevice = "M1006";
            this.kabanLamb71.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.kabanLamb71.Location = new System.Drawing.Point(383, 41);
            this.kabanLamb71.Name = "kabanLamb71";
            this.kabanLamb71.Size = new System.Drawing.Size(114, 50);
            this.kabanLamb71.TabIndex = 1737;
            this.kabanLamb71.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanLamb1
            // 
            this.kabanLamb1.DefaultValue = null;
            this.kabanLamb1.LambDevice = "M1003";
            this.kabanLamb1.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.kabanLamb1.Location = new System.Drawing.Point(383, 97);
            this.kabanLamb1.Name = "kabanLamb1";
            this.kabanLamb1.Size = new System.Drawing.Size(114, 50);
            this.kabanLamb1.TabIndex = 1738;
            this.kabanLamb1.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // ucWeldingParameters
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucWeldingParameters";
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(500, 157);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private DevExpress.XtraEditors.LabelControl lbl;
        private KabanDevBase.Plc_Componets.KabanButton btnManel;
        private DevExpress.XtraEditors.LabelControl lblSıfırNormalKaynakSecimi;
        private KabanDevBase.Plc_Componets.KabanButton btnNormalKaynak;
        private KabanDevBase.Plc_Componets.KabanLamb kabanLamb1;
        private KabanDevBase.Plc_Componets.KabanLamb kabanLamb71;
    }
}
