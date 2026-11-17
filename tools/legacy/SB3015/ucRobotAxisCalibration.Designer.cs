namespace _SB3015.Presentation.Home.Parameters
{
    partial class ucRobotAxisCalibration
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucRobotAxisCalibration));
            this.lblRobotPos = new DevExpress.XtraEditors.LabelControl();
            this.lblRobotErrorQuantity = new DevExpress.XtraEditors.LabelControl();
            this.lblName = new DevExpress.XtraEditors.LabelControl();
            this.lblDownReference = new DevExpress.XtraEditors.LabelControl();
            this.lblUpReference = new DevExpress.XtraEditors.LabelControl();
            this.lblTargetFixedVQuantity = new DevExpress.XtraEditors.LabelControl();
            this.label8 = new System.Windows.Forms.Label();
            this.cmsMain = new System.Windows.Forms.ContextMenuStrip();
            this.tsmRefreshPage = new System.Windows.Forms.ToolStripMenuItem();
            this.lblRobotRealTimePos = new DevExpress.XtraEditors.LabelControl();
            this.lblGoToPos = new DevExpress.XtraEditors.LabelControl();
            this.xtraScrollableControl1 = new DevExpress.XtraEditors.XtraScrollableControl();
            this.lblTargetPos = new DevExpress.XtraEditors.LabelControl();
            this.kabanButton2 = new KabanDevBase.Plc_Componets.KabanButton();
            this.kabanButton1 = new KabanDevBase.Plc_Componets.KabanButton();
            this.kabanLedLabel6 = new KabanDevBase.Plc_Componets.KabanLedLabel();
            this.btnCalc = new KabanDevBase.Plc_Componets.KabanButton();
            this.kabanLedLabel2 = new KabanDevBase.Plc_Componets.KabanLedLabel();
            this.kabanLedLabel3 = new KabanDevBase.Plc_Componets.KabanLedLabel();
            this.kabanLedLabel1 = new KabanDevBase.Plc_Componets.KabanLedLabel();
            this.kabanNumericButton46 = new KabanDevBase.Plc_Componets.KabanNumericButton();
            this.kabanLedLabel4 = new KabanDevBase.Plc_Componets.KabanLedLabel();
            this.kabanLedLabel5 = new KabanDevBase.Plc_Componets.KabanLedLabel();
            this.lblPositionRobotAutomaticalyDuringMeassuring = new DevExpress.XtraEditors.LabelControl();
            this.lblAppointTarhetPosAutoDuringMeassuring = new DevExpress.XtraEditors.LabelControl();
            this.ksbPosAutoDuringMessuring = new KabanDevBase.Plc_Componets.KabanSwitchButton();
            this.ksbAppointTargetPosDuring = new KabanDevBase.Plc_Componets.KabanSwitchButton();
            this.lblRobotMessasuringActive = new DevExpress.XtraEditors.LabelControl();
            this.ksbRobotMeassuringActive = new KabanDevBase.Plc_Componets.KabanSwitchButton();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            this.cmsMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.ContextMenuStrip = this.cmsMain;
            this.BoxWrapper.Controls.Add(this.ksbRobotMeassuringActive);
            this.BoxWrapper.Controls.Add(this.lblRobotMessasuringActive);
            this.BoxWrapper.Controls.Add(this.ksbAppointTargetPosDuring);
            this.BoxWrapper.Controls.Add(this.ksbPosAutoDuringMessuring);
            this.BoxWrapper.Controls.Add(this.lblAppointTarhetPosAutoDuringMeassuring);
            this.BoxWrapper.Controls.Add(this.lblPositionRobotAutomaticalyDuringMeassuring);
            this.BoxWrapper.Controls.Add(this.lblTargetPos);
            this.BoxWrapper.Controls.Add(this.lblGoToPos);
            this.BoxWrapper.Controls.Add(this.kabanButton2);
            this.BoxWrapper.Controls.Add(this.kabanButton1);
            this.BoxWrapper.Controls.Add(this.kabanLedLabel6);
            this.BoxWrapper.Controls.Add(this.lblRobotRealTimePos);
            this.BoxWrapper.Controls.Add(this.btnCalc);
            this.BoxWrapper.Controls.Add(this.kabanLedLabel2);
            this.BoxWrapper.Controls.Add(this.kabanLedLabel3);
            this.BoxWrapper.Controls.Add(this.kabanLedLabel1);
            this.BoxWrapper.Controls.Add(this.label8);
            this.BoxWrapper.Controls.Add(this.lblDownReference);
            this.BoxWrapper.Controls.Add(this.lblUpReference);
            this.BoxWrapper.Controls.Add(this.lblTargetFixedVQuantity);
            this.BoxWrapper.Controls.Add(this.kabanNumericButton46);
            this.BoxWrapper.Controls.Add(this.lblName);
            this.BoxWrapper.Controls.Add(this.lblRobotPos);
            this.BoxWrapper.Controls.Add(this.lblRobotErrorQuantity);
            this.BoxWrapper.Controls.Add(this.kabanLedLabel4);
            this.BoxWrapper.Controls.Add(this.kabanLedLabel5);
            this.BoxWrapper.Controls.Add(this.xtraScrollableControl1);
            this.BoxWrapper.LookAndFeel.SkinName = "The Bezier";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(928, 844);
            this.BoxWrapper.Text = "Robot Axis Calibration";
            // 
            // lblRobotPos
            // 
            this.lblRobotPos.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblRobotPos.Appearance.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold);
            this.lblRobotPos.Appearance.Options.UseBackColor = true;
            this.lblRobotPos.Appearance.Options.UseFont = true;
            this.lblRobotPos.Appearance.Options.UseTextOptions = true;
            this.lblRobotPos.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblRobotPos.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblRobotPos.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblRobotPos.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblRobotPos.Location = new System.Drawing.Point(207, 15);
            this.lblRobotPos.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblRobotPos.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblRobotPos.Name = "lblRobotPos";
            this.lblRobotPos.Size = new System.Drawing.Size(102, 57);
            this.lblRobotPos.TabIndex = 609;
            this.lblRobotPos.Text = "Position";
            // 
            // lblRobotErrorQuantity
            // 
            this.lblRobotErrorQuantity.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblRobotErrorQuantity.Appearance.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold);
            this.lblRobotErrorQuantity.Appearance.Options.UseBackColor = true;
            this.lblRobotErrorQuantity.Appearance.Options.UseFont = true;
            this.lblRobotErrorQuantity.Appearance.Options.UseTextOptions = true;
            this.lblRobotErrorQuantity.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblRobotErrorQuantity.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblRobotErrorQuantity.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblRobotErrorQuantity.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblRobotErrorQuantity.Location = new System.Drawing.Point(315, 15);
            this.lblRobotErrorQuantity.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblRobotErrorQuantity.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblRobotErrorQuantity.Name = "lblRobotErrorQuantity";
            this.lblRobotErrorQuantity.Size = new System.Drawing.Size(102, 57);
            this.lblRobotErrorQuantity.TabIndex = 608;
            this.lblRobotErrorQuantity.Text = "Error Quantity";
            // 
            // lblName
            // 
            this.lblName.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblName.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lblName.Appearance.Options.UseBackColor = true;
            this.lblName.Appearance.Options.UseFont = true;
            this.lblName.Appearance.Options.UseTextOptions = true;
            this.lblName.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblName.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblName.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblName.Location = new System.Drawing.Point(15, 15);
            this.lblName.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblName.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(186, 57);
            this.lblName.TabIndex = 611;
            this.lblName.Text = "Name";
            // 
            // lblDownReference
            // 
            this.lblDownReference.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblDownReference.Appearance.Options.UseFont = true;
            this.lblDownReference.Appearance.Options.UseTextOptions = true;
            this.lblDownReference.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblDownReference.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblDownReference.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblDownReference.Location = new System.Drawing.Point(443, 152);
            this.lblDownReference.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblDownReference.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblDownReference.Name = "lblDownReference";
            this.lblDownReference.Size = new System.Drawing.Size(186, 31);
            this.lblDownReference.TabIndex = 613;
            this.lblDownReference.Text = "Down Reference";
            // 
            // lblUpReference
            // 
            this.lblUpReference.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblUpReference.Appearance.Options.UseFont = true;
            this.lblUpReference.Appearance.Options.UseTextOptions = true;
            this.lblUpReference.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblUpReference.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblUpReference.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblUpReference.Location = new System.Drawing.Point(443, 189);
            this.lblUpReference.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblUpReference.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblUpReference.Name = "lblUpReference";
            this.lblUpReference.Size = new System.Drawing.Size(186, 31);
            this.lblUpReference.TabIndex = 614;
            this.lblUpReference.Text = "Up Reference";
            // 
            // lblTargetFixedVQuantity
            // 
            this.lblTargetFixedVQuantity.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblTargetFixedVQuantity.Appearance.Options.UseFont = true;
            this.lblTargetFixedVQuantity.Appearance.Options.UseTextOptions = true;
            this.lblTargetFixedVQuantity.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblTargetFixedVQuantity.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblTargetFixedVQuantity.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblTargetFixedVQuantity.Location = new System.Drawing.Point(444, 226);
            this.lblTargetFixedVQuantity.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblTargetFixedVQuantity.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblTargetFixedVQuantity.Name = "lblTargetFixedVQuantity";
            this.lblTargetFixedVQuantity.Size = new System.Drawing.Size(186, 31);
            this.lblTargetFixedVQuantity.TabIndex = 615;
            this.lblTargetFixedVQuantity.Text = "Target Fixed Quantity";
            // 
            // label8
            // 
            this.label8.BackColor = System.Drawing.Color.LightGray;
            this.label8.Font = new System.Drawing.Font("Tahoma", 11F, System.Drawing.FontStyle.Bold);
            this.label8.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.label8.Location = new System.Drawing.Point(638, 226);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(96, 31);
            this.label8.TabIndex = 725;
            this.label8.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // cmsMain
            // 
            this.cmsMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.tsmRefreshPage});
            this.cmsMain.Name = "cmsMain";
            this.cmsMain.ShowImageMargin = false;
            this.cmsMain.Size = new System.Drawing.Size(118, 26);
            // 
            // tsmRefreshPage
            // 
            this.tsmRefreshPage.Name = "tsmRefreshPage";
            this.tsmRefreshPage.Size = new System.Drawing.Size(117, 22);
            this.tsmRefreshPage.Text = "Refresh Page";
            // 
            // lblRobotRealTimePos
            // 
            this.lblRobotRealTimePos.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblRobotRealTimePos.Appearance.Options.UseFont = true;
            this.lblRobotRealTimePos.Appearance.Options.UseTextOptions = true;
            this.lblRobotRealTimePos.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblRobotRealTimePos.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblRobotRealTimePos.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblRobotRealTimePos.Location = new System.Drawing.Point(444, 263);
            this.lblRobotRealTimePos.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblRobotRealTimePos.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblRobotRealTimePos.Name = "lblRobotRealTimePos";
            this.lblRobotRealTimePos.Size = new System.Drawing.Size(186, 31);
            this.lblRobotRealTimePos.TabIndex = 733;
            this.lblRobotRealTimePos.Text = "Robot Realtime Position";
            // 
            // lblGoToPos
            // 
            this.lblGoToPos.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblGoToPos.Appearance.Options.UseFont = true;
            this.lblGoToPos.Appearance.Options.UseTextOptions = true;
            this.lblGoToPos.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblGoToPos.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblGoToPos.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblGoToPos.Location = new System.Drawing.Point(443, 115);
            this.lblGoToPos.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblGoToPos.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblGoToPos.Name = "lblGoToPos";
            this.lblGoToPos.Size = new System.Drawing.Size(186, 31);
            this.lblGoToPos.TabIndex = 737;
            this.lblGoToPos.Text = "Go to Position";
            // 
            // xtraScrollableControl1
            // 
            this.xtraScrollableControl1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.xtraScrollableControl1.Location = new System.Drawing.Point(15, 41);
            this.xtraScrollableControl1.Name = "xtraScrollableControl1";
            this.xtraScrollableControl1.Size = new System.Drawing.Size(423, 800);
            this.xtraScrollableControl1.TabIndex = 738;
            // 
            // lblTargetPos
            // 
            this.lblTargetPos.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblTargetPos.Appearance.Options.UseFont = true;
            this.lblTargetPos.Appearance.Options.UseTextOptions = true;
            this.lblTargetPos.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblTargetPos.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblTargetPos.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblTargetPos.Location = new System.Drawing.Point(443, 78);
            this.lblTargetPos.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblTargetPos.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblTargetPos.Name = "lblTargetPos";
            this.lblTargetPos.Size = new System.Drawing.Size(186, 31);
            this.lblTargetPos.TabIndex = 739;
            this.lblTargetPos.Text = "Target Position";
            // 
            // kabanButton2
            // 
            this.kabanButton2.Appearance.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold);
            this.kabanButton2.Appearance.Options.UseFont = true;
            this.kabanButton2.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.kabanButton2.ButtonTag = null;
            this.kabanButton2.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Set;
            this.kabanButton2.Cursor = System.Windows.Forms.Cursors.Hand;
            this.kabanButton2.DefaultValue = 0;
            this.kabanButton2.Device = "M25";
            this.kabanButton2.ImageOptions.ImageToTextAlignment = DevExpress.XtraEditors.ImageAlignToText.LeftCenter;
            this.kabanButton2.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("kabanButton2.ImageOptions.SvgImage")));
            this.kabanButton2.ImageOptions.SvgImageSize = new System.Drawing.Size(24, 24);
            this.kabanButton2.IsReferanceButton = false;
            this.kabanButton2.IsScrewingControl = false;
            this.kabanButton2.Location = new System.Drawing.Point(635, 114);
            this.kabanButton2.Name = "kabanButton2";
            this.kabanButton2.ProcessDevice = false;
            this.kabanButton2.ResetDevice = null;
            this.kabanButton2.Size = new System.Drawing.Size(103, 31);
            this.kabanButton2.TabIndex = 736;
            this.kabanButton2.Text = "GO!";
            this.kabanButton2.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanButton1
            // 
            this.kabanButton1.Appearance.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold);
            this.kabanButton1.Appearance.Options.UseFont = true;
            this.kabanButton1.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.kabanButton1.ButtonTag = null;
            this.kabanButton1.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Set;
            this.kabanButton1.Cursor = System.Windows.Forms.Cursors.Hand;
            this.kabanButton1.DefaultValue = 0;
            this.kabanButton1.Device = "M26";
            this.kabanButton1.ImageOptions.ImageToTextAlignment = DevExpress.XtraEditors.ImageAlignToText.LeftCenter;
            this.kabanButton1.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("kabanButton1.ImageOptions.SvgImage")));
            this.kabanButton1.ImageOptions.SvgImageSize = new System.Drawing.Size(24, 24);
            this.kabanButton1.IsReferanceButton = false;
            this.kabanButton1.IsScrewingControl = false;
            this.kabanButton1.Location = new System.Drawing.Point(743, 115);
            this.kabanButton1.Name = "kabanButton1";
            this.kabanButton1.ProcessDevice = false;
            this.kabanButton1.ResetDevice = null;
            this.kabanButton1.Size = new System.Drawing.Size(103, 31);
            this.kabanButton1.TabIndex = 735;
            this.kabanButton1.Text = "GO!";
            this.kabanButton1.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanLedLabel6
            // 
            this.kabanLedLabel6.ConvertToHex = false;
            this.kabanLedLabel6.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
            this.kabanLedLabel6.Device = "D58";
            this.kabanLedLabel6.DigitCount = 8;
            this.kabanLedLabel6.DirectShow = false;
            this.kabanLedLabel6.Divisor = 1;
            this.kabanLedLabel6.FloatingPoint = 4;
            this.kabanLedLabel6.LabelText = ".0";
            this.kabanLedLabel6.Location = new System.Drawing.Point(638, 256);
            this.kabanLedLabel6.Name = "kabanLedLabel6";
            this.kabanLedLabel6.ReadMode = KabanMachinesSystem.Common.Enums.ReadMode.CONTINUOUS;
            this.kabanLedLabel6.Size = new System.Drawing.Size(100, 43);
            this.kabanLedLabel6.TabIndex = 734;
            // 
            // btnCalc
            // 
            this.btnCalc.Appearance.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Bold);
            this.btnCalc.Appearance.Options.UseFont = true;
            this.btnCalc.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnCalc.ButtonTag = null;
            this.btnCalc.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Set;
            this.btnCalc.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnCalc.DefaultValue = 0;
            this.btnCalc.Device = "M23";
            this.btnCalc.ImageOptions.ImageToTextAlignment = DevExpress.XtraEditors.ImageAlignToText.LeftCenter;
            this.btnCalc.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnCalc.ImageOptions.SvgImage")));
            this.btnCalc.ImageOptions.SvgImageSize = new System.Drawing.Size(24, 24);
            this.btnCalc.IsReferanceButton = false;
            this.btnCalc.IsScrewingControl = false;
            this.btnCalc.Location = new System.Drawing.Point(743, 78);
            this.btnCalc.Name = "btnCalc";
            this.btnCalc.ProcessDevice = false;
            this.btnCalc.ResetDevice = null;
            this.btnCalc.Size = new System.Drawing.Size(103, 31);
            this.btnCalc.TabIndex = 732;
            this.btnCalc.Text = "Calc!";
            this.btnCalc.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanLedLabel2
            // 
            this.kabanLedLabel2.ConvertToHex = false;
            this.kabanLedLabel2.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
            this.kabanLedLabel2.Device = "D42";
            this.kabanLedLabel2.DigitCount = 8;
            this.kabanLedLabel2.DirectShow = false;
            this.kabanLedLabel2.Divisor = 1;
            this.kabanLedLabel2.FloatingPoint = 4;
            this.kabanLedLabel2.LabelText = ".0";
            this.kabanLedLabel2.Location = new System.Drawing.Point(742, 146);
            this.kabanLedLabel2.Name = "kabanLedLabel2";
            this.kabanLedLabel2.ReadMode = KabanMachinesSystem.Common.Enums.ReadMode.CONTINUOUS;
            this.kabanLedLabel2.Size = new System.Drawing.Size(100, 43);
            this.kabanLedLabel2.TabIndex = 728;
            // 
            // kabanLedLabel3
            // 
            this.kabanLedLabel3.ConvertToHex = false;
            this.kabanLedLabel3.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
            this.kabanLedLabel3.Device = "D46";
            this.kabanLedLabel3.DigitCount = 8;
            this.kabanLedLabel3.DirectShow = false;
            this.kabanLedLabel3.Divisor = 1;
            this.kabanLedLabel3.FloatingPoint = 4;
            this.kabanLedLabel3.LabelText = ".0";
            this.kabanLedLabel3.Location = new System.Drawing.Point(742, 183);
            this.kabanLedLabel3.Name = "kabanLedLabel3";
            this.kabanLedLabel3.ReadMode = KabanMachinesSystem.Common.Enums.ReadMode.CONTINUOUS;
            this.kabanLedLabel3.Size = new System.Drawing.Size(100, 43);
            this.kabanLedLabel3.TabIndex = 730;
            // 
            // kabanLedLabel1
            // 
            this.kabanLedLabel1.ConvertToHex = false;
            this.kabanLedLabel1.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
            this.kabanLedLabel1.Device = "D40";
            this.kabanLedLabel1.DigitCount = 8;
            this.kabanLedLabel1.DirectShow = false;
            this.kabanLedLabel1.Divisor = 1;
            this.kabanLedLabel1.FloatingPoint = 4;
            this.kabanLedLabel1.LabelText = ".0";
            this.kabanLedLabel1.Location = new System.Drawing.Point(637, 145);
            this.kabanLedLabel1.Name = "kabanLedLabel1";
            this.kabanLedLabel1.ReadMode = KabanMachinesSystem.Common.Enums.ReadMode.CONTINUOUS;
            this.kabanLedLabel1.Size = new System.Drawing.Size(105, 43);
            this.kabanLedLabel1.TabIndex = 727;
            // 
            // kabanNumericButton46
            // 
            this.kabanNumericButton46.CalibrationDevice = null;
            this.kabanNumericButton46.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
            this.kabanNumericButton46.DefaultValue = 0;
            this.kabanNumericButton46.Device = "D38";
            this.kabanNumericButton46.Divisor = 100;
            this.kabanNumericButton46.IsScrewingControl = false;
            this.kabanNumericButton46.Location = new System.Drawing.Point(635, 78);
            this.kabanNumericButton46.Mask = "n2";
            this.kabanNumericButton46.Name = "kabanNumericButton46";
            this.kabanNumericButton46.Size = new System.Drawing.Size(102, 31);
            this.kabanNumericButton46.TabIndex = 712;
            this.kabanNumericButton46.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            this.kabanNumericButton46.Value = "0";
            // 
            // kabanLedLabel4
            // 
            this.kabanLedLabel4.ConvertToHex = false;
            this.kabanLedLabel4.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
            this.kabanLedLabel4.Device = "D44";
            this.kabanLedLabel4.DigitCount = 8;
            this.kabanLedLabel4.DirectShow = false;
            this.kabanLedLabel4.Divisor = 1;
            this.kabanLedLabel4.FloatingPoint = 4;
            this.kabanLedLabel4.LabelText = ".0";
            this.kabanLedLabel4.Location = new System.Drawing.Point(637, 184);
            this.kabanLedLabel4.Name = "kabanLedLabel4";
            this.kabanLedLabel4.ReadMode = KabanMachinesSystem.Common.Enums.ReadMode.CONTINUOUS;
            this.kabanLedLabel4.Size = new System.Drawing.Size(100, 43);
            this.kabanLedLabel4.TabIndex = 729;
            // 
            // kabanLedLabel5
            // 
            this.kabanLedLabel5.ConvertToHex = false;
            this.kabanLedLabel5.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
            this.kabanLedLabel5.Device = "D48";
            this.kabanLedLabel5.DigitCount = 8;
            this.kabanLedLabel5.DirectShow = false;
            this.kabanLedLabel5.Divisor = 1;
            this.kabanLedLabel5.FloatingPoint = 4;
            this.kabanLedLabel5.LabelText = ".0";
            this.kabanLedLabel5.Location = new System.Drawing.Point(742, 221);
            this.kabanLedLabel5.Name = "kabanLedLabel5";
            this.kabanLedLabel5.ReadMode = KabanMachinesSystem.Common.Enums.ReadMode.CONTINUOUS;
            this.kabanLedLabel5.Size = new System.Drawing.Size(100, 43);
            this.kabanLedLabel5.TabIndex = 731;
            // 
            // lblPositionRobotAutomaticalyDuringMeassuring
            // 
            this.lblPositionRobotAutomaticalyDuringMeassuring.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblPositionRobotAutomaticalyDuringMeassuring.Appearance.Options.UseFont = true;
            this.lblPositionRobotAutomaticalyDuringMeassuring.Appearance.Options.UseTextOptions = true;
            this.lblPositionRobotAutomaticalyDuringMeassuring.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblPositionRobotAutomaticalyDuringMeassuring.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblPositionRobotAutomaticalyDuringMeassuring.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblPositionRobotAutomaticalyDuringMeassuring.Location = new System.Drawing.Point(443, 300);
            this.lblPositionRobotAutomaticalyDuringMeassuring.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblPositionRobotAutomaticalyDuringMeassuring.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblPositionRobotAutomaticalyDuringMeassuring.Name = "lblPositionRobotAutomaticalyDuringMeassuring";
            this.lblPositionRobotAutomaticalyDuringMeassuring.Size = new System.Drawing.Size(186, 31);
            this.lblPositionRobotAutomaticalyDuringMeassuring.TabIndex = 740;
            this.lblPositionRobotAutomaticalyDuringMeassuring.Text = "Pos. Auto. During Meassuring";
            // 
            // lblAppointTarhetPosAutoDuringMeassuring
            // 
            this.lblAppointTarhetPosAutoDuringMeassuring.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblAppointTarhetPosAutoDuringMeassuring.Appearance.Options.UseFont = true;
            this.lblAppointTarhetPosAutoDuringMeassuring.Appearance.Options.UseTextOptions = true;
            this.lblAppointTarhetPosAutoDuringMeassuring.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblAppointTarhetPosAutoDuringMeassuring.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblAppointTarhetPosAutoDuringMeassuring.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblAppointTarhetPosAutoDuringMeassuring.Location = new System.Drawing.Point(444, 337);
            this.lblAppointTarhetPosAutoDuringMeassuring.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblAppointTarhetPosAutoDuringMeassuring.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblAppointTarhetPosAutoDuringMeassuring.Name = "lblAppointTarhetPosAutoDuringMeassuring";
            this.lblAppointTarhetPosAutoDuringMeassuring.Size = new System.Drawing.Size(186, 31);
            this.lblAppointTarhetPosAutoDuringMeassuring.TabIndex = 741;
            this.lblAppointTarhetPosAutoDuringMeassuring.Text = "Appoint Target  Pos. During ";
            // 
            // ksbPosAutoDuringMessuring
            // 
            this.ksbPosAutoDuringMessuring.Device = "L7";
            this.ksbPosAutoDuringMessuring.IsScrewingControl = false;
            this.ksbPosAutoDuringMessuring.Location = new System.Drawing.Point(641, 300);
            this.ksbPosAutoDuringMessuring.Name = "ksbPosAutoDuringMessuring";
            this.ksbPosAutoDuringMessuring.Size = new System.Drawing.Size(68, 31);
            this.ksbPosAutoDuringMessuring.TabIndex = 859;
            // 
            // ksbAppointTargetPosDuring
            // 
            this.ksbAppointTargetPosDuring.Device = "L8";
            this.ksbAppointTargetPosDuring.IsScrewingControl = false;
            this.ksbAppointTargetPosDuring.Location = new System.Drawing.Point(641, 337);
            this.ksbAppointTargetPosDuring.Name = "ksbAppointTargetPosDuring";
            this.ksbAppointTargetPosDuring.Size = new System.Drawing.Size(68, 31);
            this.ksbAppointTargetPosDuring.TabIndex = 860;
            // 
            // lblRobotMessasuringActive
            // 
            this.lblRobotMessasuringActive.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblRobotMessasuringActive.Appearance.Options.UseFont = true;
            this.lblRobotMessasuringActive.Appearance.Options.UseTextOptions = true;
            this.lblRobotMessasuringActive.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblRobotMessasuringActive.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblRobotMessasuringActive.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblRobotMessasuringActive.Location = new System.Drawing.Point(443, 374);
            this.lblRobotMessasuringActive.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblRobotMessasuringActive.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblRobotMessasuringActive.Name = "lblRobotMessasuringActive";
            this.lblRobotMessasuringActive.Size = new System.Drawing.Size(186, 31);
            this.lblRobotMessasuringActive.TabIndex = 861;
            this.lblRobotMessasuringActive.Text = "Robot Meassuring Active";
            // 
            // ksbRobotMeassuringActive
            // 
            this.ksbRobotMeassuringActive.Device = "L9";
            this.ksbRobotMeassuringActive.IsScrewingControl = false;
            this.ksbRobotMeassuringActive.Location = new System.Drawing.Point(641, 374);
            this.ksbRobotMeassuringActive.Name = "ksbRobotMeassuringActive";
            this.ksbRobotMeassuringActive.Size = new System.Drawing.Size(68, 31);
            this.ksbRobotMeassuringActive.TabIndex = 862;
            // 
            // ucRobotAxisCalibration
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucRobotAxisCalibration";
            this.PageTitle = "Robot Axis Calibration";
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(928, 844);
            this.Controls.SetChildIndex(this.BoxWrapper, 0);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            this.cmsMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private DevExpress.XtraEditors.LabelControl lblRobotPos;
        private DevExpress.XtraEditors.LabelControl lblRobotErrorQuantity;
        private DevExpress.XtraEditors.LabelControl lblName;
        private DevExpress.XtraEditors.LabelControl lblUpReference;
        private DevExpress.XtraEditors.LabelControl lblDownReference;
        private DevExpress.XtraEditors.LabelControl lblTargetFixedVQuantity;
        private KabanDevBase.Plc_Componets.KabanNumericButton kabanNumericButton46;
        public System.Windows.Forms.Label label8;
        private KabanDevBase.Plc_Componets.KabanLedLabel kabanLedLabel3;
        private KabanDevBase.Plc_Componets.KabanLedLabel kabanLedLabel1;
        private KabanDevBase.Plc_Componets.KabanLedLabel kabanLedLabel4;
        private KabanDevBase.Plc_Componets.KabanLedLabel kabanLedLabel5;
        private KabanDevBase.Plc_Componets.KabanLedLabel kabanLedLabel2;
        private KabanDevBase.Plc_Componets.KabanButton btnCalc;
        private System.Windows.Forms.ContextMenuStrip cmsMain;
        private System.Windows.Forms.ToolStripMenuItem tsmRefreshPage;
        private KabanDevBase.Plc_Componets.KabanLedLabel kabanLedLabel6;
        private DevExpress.XtraEditors.LabelControl lblRobotRealTimePos;
        private DevExpress.XtraEditors.LabelControl lblGoToPos;
        private KabanDevBase.Plc_Componets.KabanButton kabanButton2;
        private KabanDevBase.Plc_Componets.KabanButton kabanButton1;
        private DevExpress.XtraEditors.XtraScrollableControl xtraScrollableControl1;
        private DevExpress.XtraEditors.LabelControl lblTargetPos;
        private DevExpress.XtraEditors.LabelControl lblAppointTarhetPosAutoDuringMeassuring;
        private DevExpress.XtraEditors.LabelControl lblPositionRobotAutomaticalyDuringMeassuring;
        private KabanDevBase.Plc_Componets.KabanSwitchButton ksbRobotMeassuringActive;
        private DevExpress.XtraEditors.LabelControl lblRobotMessasuringActive;
        private KabanDevBase.Plc_Componets.KabanSwitchButton ksbAppointTargetPosDuring;
        private KabanDevBase.Plc_Componets.KabanSwitchButton ksbPosAutoDuringMessuring;
    }
}
