namespace _SB3015.Presentation.Home.Manuel
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
            this.btnSpeed_10mm = new KabanDevBase.Plc_Componets.KabanLambButton();
            this.btnSpeed_01mm = new KabanDevBase.Plc_Componets.KabanLambButton();
            this.btnSpeed_1mm = new KabanDevBase.Plc_Componets.KabanLambButton();
            this.btnSpeed_001mm = new KabanDevBase.Plc_Componets.KabanLambButton();
            this.btnSpeed_Jog = new KabanDevBase.Plc_Componets.KabanLambButton();
            this.btnReset = new KabanDevBase.Plc_Componets.KabanLambButton();
            this.kabanTrackBar1 = new KabanDevBase.Plc_Componets.KabanTrackBar();
            this.btnSecurityByPass = new KabanDevBase.Plc_Componets.KabanLambButton();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.btnSecurityByPass);
            this.BoxWrapper.Controls.Add(this.kabanTrackBar1);
            this.BoxWrapper.Controls.Add(this.btnReset);
            this.BoxWrapper.Controls.Add(this.btnSpeed_10mm);
            this.BoxWrapper.Controls.Add(this.btnSpeed_01mm);
            this.BoxWrapper.Controls.Add(this.btnSpeed_1mm);
            this.BoxWrapper.Controls.Add(this.btnSpeed_001mm);
            this.BoxWrapper.Controls.Add(this.btnSpeed_Jog);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(861, 61);
            this.BoxWrapper.Text = "";
            // 
            // btnSpeed_10mm
            // 
            this.btnSpeed_10mm.ButtonTag = "StepButton";
            this.btnSpeed_10mm.ButtonText = "10 mm";
            this.btnSpeed_10mm.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Set;
            this.btnSpeed_10mm.DefaultValue = "0";
            this.btnSpeed_10mm.Device = "M170";
            this.btnSpeed_10mm.IsHeadControlButton = true;
            this.btnSpeed_10mm.IsReferanceButton = false;
            this.btnSpeed_10mm.IsScrewingControl = false;
            this.btnSpeed_10mm.IsSecurityButton = false;
            this.btnSpeed_10mm.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.btnSpeed_10mm.LambDevice = "M170";
            this.btnSpeed_10mm.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.btnSpeed_10mm.Location = new System.Drawing.Point(90, 7);
            this.btnSpeed_10mm.Name = "btnSpeed_10mm";
            this.btnSpeed_10mm.ResetDevice = null;
            this.btnSpeed_10mm.Size = new System.Drawing.Size(67, 49);
            this.btnSpeed_10mm.TabIndex = 5;
            this.btnSpeed_10mm.Tag = "";
            this.btnSpeed_10mm.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // btnSpeed_01mm
            // 
            this.btnSpeed_01mm.ButtonTag = "StepButton";
            this.btnSpeed_01mm.ButtonText = "0.1 mm";
            this.btnSpeed_01mm.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Set;
            this.btnSpeed_01mm.DefaultValue = "0";
            this.btnSpeed_01mm.Device = "M172";
            this.btnSpeed_01mm.IsHeadControlButton = true;
            this.btnSpeed_01mm.IsReferanceButton = false;
            this.btnSpeed_01mm.IsScrewingControl = false;
            this.btnSpeed_01mm.IsSecurityButton = false;
            this.btnSpeed_01mm.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.btnSpeed_01mm.LambDevice = "M172";
            this.btnSpeed_01mm.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.btnSpeed_01mm.Location = new System.Drawing.Point(236, 7);
            this.btnSpeed_01mm.Name = "btnSpeed_01mm";
            this.btnSpeed_01mm.ResetDevice = null;
            this.btnSpeed_01mm.Size = new System.Drawing.Size(67, 49);
            this.btnSpeed_01mm.TabIndex = 4;
            this.btnSpeed_01mm.Tag = "";
            this.btnSpeed_01mm.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // btnSpeed_1mm
            // 
            this.btnSpeed_1mm.ButtonTag = "StepButton";
            this.btnSpeed_1mm.ButtonText = "1 mm";
            this.btnSpeed_1mm.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Set;
            this.btnSpeed_1mm.DefaultValue = "0";
            this.btnSpeed_1mm.Device = "M173";
            this.btnSpeed_1mm.IsHeadControlButton = true;
            this.btnSpeed_1mm.IsReferanceButton = false;
            this.btnSpeed_1mm.IsScrewingControl = false;
            this.btnSpeed_1mm.IsSecurityButton = false;
            this.btnSpeed_1mm.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.btnSpeed_1mm.LambDevice = "M173";
            this.btnSpeed_1mm.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.btnSpeed_1mm.Location = new System.Drawing.Point(163, 7);
            this.btnSpeed_1mm.Name = "btnSpeed_1mm";
            this.btnSpeed_1mm.ResetDevice = null;
            this.btnSpeed_1mm.Size = new System.Drawing.Size(67, 49);
            this.btnSpeed_1mm.TabIndex = 3;
            this.btnSpeed_1mm.Tag = "";
            this.btnSpeed_1mm.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // btnSpeed_001mm
            // 
            this.btnSpeed_001mm.ButtonTag = "StepButton";
            this.btnSpeed_001mm.ButtonText = "0.01 mm";
            this.btnSpeed_001mm.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Set;
            this.btnSpeed_001mm.DefaultValue = "0";
            this.btnSpeed_001mm.Device = "M171";
            this.btnSpeed_001mm.IsHeadControlButton = true;
            this.btnSpeed_001mm.IsReferanceButton = false;
            this.btnSpeed_001mm.IsScrewingControl = false;
            this.btnSpeed_001mm.IsSecurityButton = false;
            this.btnSpeed_001mm.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.btnSpeed_001mm.LambDevice = "M171";
            this.btnSpeed_001mm.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.btnSpeed_001mm.Location = new System.Drawing.Point(309, 7);
            this.btnSpeed_001mm.Name = "btnSpeed_001mm";
            this.btnSpeed_001mm.ResetDevice = null;
            this.btnSpeed_001mm.Size = new System.Drawing.Size(67, 49);
            this.btnSpeed_001mm.TabIndex = 3;
            this.btnSpeed_001mm.Tag = "";
            this.btnSpeed_001mm.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // btnSpeed_Jog
            // 
            this.btnSpeed_Jog.ButtonTag = "StepButton";
            this.btnSpeed_Jog.ButtonText = "JOG";
            this.btnSpeed_Jog.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Set;
            this.btnSpeed_Jog.DefaultValue = "0";
            this.btnSpeed_Jog.Device = "";
            this.btnSpeed_Jog.IsHeadControlButton = true;
            this.btnSpeed_Jog.IsReferanceButton = false;
            this.btnSpeed_Jog.IsScrewingControl = false;
            this.btnSpeed_Jog.IsSecurityButton = false;
            this.btnSpeed_Jog.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.btnSpeed_Jog.LambDevice = "M174";
            this.btnSpeed_Jog.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.btnSpeed_Jog.Location = new System.Drawing.Point(17, 7);
            this.btnSpeed_Jog.Name = "btnSpeed_Jog";
            this.btnSpeed_Jog.ResetDevice = null;
            this.btnSpeed_Jog.Size = new System.Drawing.Size(67, 49);
            this.btnSpeed_Jog.TabIndex = 0;
            this.btnSpeed_Jog.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // btnReset
            // 
            this.btnReset.ButtonTag = null;
            this.btnReset.ButtonText = "Reset";
            this.btnReset.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.btnReset.DefaultValue = "0";
            this.btnReset.Device = "M102";
            this.btnReset.IsHeadControlButton = true;
            this.btnReset.IsReferanceButton = false;
            this.btnReset.IsScrewingControl = false;
            this.btnReset.IsSecurityButton = false;
            this.btnReset.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.btnReset.LambDevice = "M800";
            this.btnReset.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.btnReset.Location = new System.Drawing.Point(382, 7);
            this.btnReset.Name = "btnReset";
            this.btnReset.ResetDevice = null;
            this.btnReset.Size = new System.Drawing.Size(67, 49);
            this.btnReset.TabIndex = 6;
            this.btnReset.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanTrackBar1
            // 
            this.kabanTrackBar1.Device = "D409";
            this.kabanTrackBar1.FSpeed = ((System.Collections.Generic.Dictionary<int, int>)(resources.GetObject("kabanTrackBar1.FSpeed")));
            this.kabanTrackBar1.Location = new System.Drawing.Point(513, 7);
            this.kabanTrackBar1.MaxValue = 100;
            this.kabanTrackBar1.MinValue = 0;
            this.kabanTrackBar1.Name = "kabanTrackBar1";
            this.kabanTrackBar1.Size = new System.Drawing.Size(247, 49);
            this.kabanTrackBar1.TabIndex = 7;
            // 
            // btnSecurityByPass
            // 
            this.btnSecurityByPass.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSecurityByPass.ButtonTag = null;
            this.btnSecurityByPass.ButtonText = "Security ByPass";
            this.btnSecurityByPass.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Alternate;
            this.btnSecurityByPass.DefaultValue = "0";
            this.btnSecurityByPass.Device = "M175";
            this.btnSecurityByPass.IsHeadControlButton = true;
            this.btnSecurityByPass.IsReferanceButton = false;
            this.btnSecurityByPass.IsScrewingControl = false;
            this.btnSecurityByPass.IsSecurityButton = false;
            this.btnSecurityByPass.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.btnSecurityByPass.LambDevice = "M175";
            this.btnSecurityByPass.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.btnSecurityByPass.Location = new System.Drawing.Point(766, 7);
            this.btnSecurityByPass.Name = "btnSecurityByPass";
            this.btnSecurityByPass.ResetDevice = null;
            this.btnSecurityByPass.Size = new System.Drawing.Size(90, 49);
            this.btnSecurityByPass.TabIndex = 8;
            this.btnSecurityByPass.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            this.btnSecurityByPass.Load += new System.EventHandler(this.btnSecurityByPass_Load);
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
        private KabanDevBase.Plc_Componets.KabanLambButton btnSpeed_10mm;
        private KabanDevBase.Plc_Componets.KabanLambButton btnSpeed_01mm;
        private KabanDevBase.Plc_Componets.KabanLambButton btnSpeed_1mm;
        private KabanDevBase.Plc_Componets.KabanLambButton btnSpeed_001mm;
        private KabanDevBase.Plc_Componets.KabanLambButton btnSpeed_Jog;
        private KabanDevBase.Plc_Componets.KabanLambButton btnReset;
        private KabanDevBase.Plc_Componets.KabanTrackBar kabanTrackBar1;
        private KabanDevBase.Plc_Componets.KabanLambButton btnSecurityByPass;
    }
}
