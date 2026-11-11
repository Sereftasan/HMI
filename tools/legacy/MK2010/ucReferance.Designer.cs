namespace _MK2010.Presentation.Home.Manuel
{
    partial class ucReferance
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
            this.btnRef3 = new KabanDevBase.Plc_Componets.KabanLambButton();
            this.kabanLedLabel3 = new KabanDevBase.Plc_Componets.KabanLedLabel();
            this.lblAxisY2 = new DevExpress.XtraEditors.LabelControl();
            this.btnRef2 = new KabanDevBase.Plc_Componets.KabanLambButton();
            this.kabanLedLabel2 = new KabanDevBase.Plc_Componets.KabanLedLabel();
            this.lblAxisY1 = new DevExpress.XtraEditors.LabelControl();
            this.btnRef1 = new KabanDevBase.Plc_Componets.KabanLambButton();
            this.kabanLedLabel1 = new KabanDevBase.Plc_Componets.KabanLedLabel();
            this.lblAxisX1Mk = new DevExpress.XtraEditors.LabelControl();
            this.lblPositions = new DevExpress.XtraEditors.LabelControl();
            this.lblForward = new DevExpress.XtraEditors.LabelControl();
            this.lblName = new DevExpress.XtraEditors.LabelControl();
            this.lblReferenceOk = new DevExpress.XtraEditors.LabelControl();
            this.lblLimit = new DevExpress.XtraEditors.LabelControl();
            this.kabanLamb1 = new KabanDevBase.Plc_Componets.KabanLamb();
            this.kabanLamb2 = new KabanDevBase.Plc_Componets.KabanLamb();
            this.kabanLamb3 = new KabanDevBase.Plc_Componets.KabanLamb();
            this.lblXY1Y2 = new DevExpress.XtraEditors.LabelControl();
            this.btnRefAll = new KabanDevBase.Plc_Componets.KabanButton();
            this.kabanLambAlternative2 = new KabanDevBase.Plc_Componets.KabanLambAlternative();
            this.kabanLambAlternative3 = new KabanDevBase.Plc_Componets.KabanLambAlternative();
            this.kabanLambAlternative1 = new KabanDevBase.Plc_Componets.KabanLambAlternative();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.kabanLambAlternative1);
            this.BoxWrapper.Controls.Add(this.kabanLambAlternative3);
            this.BoxWrapper.Controls.Add(this.kabanLambAlternative2);
            this.BoxWrapper.Controls.Add(this.btnRefAll);
            this.BoxWrapper.Controls.Add(this.lblXY1Y2);
            this.BoxWrapper.Controls.Add(this.kabanLamb3);
            this.BoxWrapper.Controls.Add(this.kabanLamb2);
            this.BoxWrapper.Controls.Add(this.kabanLamb1);
            this.BoxWrapper.Controls.Add(this.lblLimit);
            this.BoxWrapper.Controls.Add(this.btnRef3);
            this.BoxWrapper.Controls.Add(this.kabanLedLabel3);
            this.BoxWrapper.Controls.Add(this.lblAxisY2);
            this.BoxWrapper.Controls.Add(this.btnRef2);
            this.BoxWrapper.Controls.Add(this.kabanLedLabel2);
            this.BoxWrapper.Controls.Add(this.lblAxisY1);
            this.BoxWrapper.Controls.Add(this.btnRef1);
            this.BoxWrapper.Controls.Add(this.kabanLedLabel1);
            this.BoxWrapper.Controls.Add(this.lblAxisX1Mk);
            this.BoxWrapper.Controls.Add(this.lblPositions);
            this.BoxWrapper.Controls.Add(this.lblForward);
            this.BoxWrapper.Controls.Add(this.lblName);
            this.BoxWrapper.Controls.Add(this.lblReferenceOk);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(614, 389);
            // 
            // btnRef3
            // 
            this.btnRef3.ButtonTag = null;
            this.btnRef3.ButtonText = "";
            this.btnRef3.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.btnRef3.DefaultValue = null;
            this.btnRef3.Device = "M32";
            this.btnRef3.IsHeadControlButton = false;
            this.btnRef3.IsReferanceButton = false;
            this.btnRef3.IsScrewingControl = false;
            this.btnRef3.IsSecurityButton = false;
            this.btnRef3.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.btnRef3.LambColor = KabanMachinesSystem.Common.Enums.LambColor.Green;
            this.btnRef3.LambDevice = "M32";
            this.btnRef3.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.btnRef3.Location = new System.Drawing.Point(151, 151);
            this.btnRef3.Name = "btnRef3";
            this.btnRef3.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnRef3.ResetDevice = null;
            this.btnRef3.Size = new System.Drawing.Size(93, 31);
            this.btnRef3.TabIndex = 1756;
            this.btnRef3.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanLedLabel3
            // 
            this.kabanLedLabel3.ConvertToHex = false;
            this.kabanLedLabel3.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
            this.kabanLedLabel3.Device = "D54";
            this.kabanLedLabel3.DigitCount = 6;
            this.kabanLedLabel3.DirectShow = false;
            this.kabanLedLabel3.Divisor = 1;
            this.kabanLedLabel3.FloatingPoint = 1;
            this.kabanLedLabel3.IsConvertible = false;
            this.kabanLedLabel3.LabelText = ",0";
            this.kabanLedLabel3.Location = new System.Drawing.Point(249, 144);
            this.kabanLedLabel3.Name = "kabanLedLabel3";
            this.kabanLedLabel3.ReadMode = KabanMachinesSystem.Common.Enums.ReadMode.CONTINUOUS;
            this.kabanLedLabel3.Size = new System.Drawing.Size(109, 43);
            this.kabanLedLabel3.TabIndex = 1755;
            // 
            // lblAxisY2
            // 
            this.lblAxisY2.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblAxisY2.Appearance.Options.UseFont = true;
            this.lblAxisY2.Appearance.Options.UseTextOptions = true;
            this.lblAxisY2.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblAxisY2.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblAxisY2.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblAxisY2.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblAxisY2.Location = new System.Drawing.Point(15, 151);
            this.lblAxisY2.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblAxisY2.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblAxisY2.Name = "lblAxisY2";
            this.lblAxisY2.Size = new System.Drawing.Size(130, 31);
            this.lblAxisY2.TabIndex = 1753;
            this.lblAxisY2.Text = "Axis Y2";
            // 
            // btnRef2
            // 
            this.btnRef2.ButtonTag = null;
            this.btnRef2.ButtonText = "";
            this.btnRef2.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.btnRef2.DefaultValue = null;
            this.btnRef2.Device = "M31";
            this.btnRef2.IsHeadControlButton = false;
            this.btnRef2.IsReferanceButton = false;
            this.btnRef2.IsScrewingControl = false;
            this.btnRef2.IsSecurityButton = false;
            this.btnRef2.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.btnRef2.LambColor = KabanMachinesSystem.Common.Enums.LambColor.Green;
            this.btnRef2.LambDevice = "M31";
            this.btnRef2.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.btnRef2.Location = new System.Drawing.Point(151, 114);
            this.btnRef2.Name = "btnRef2";
            this.btnRef2.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnRef2.ResetDevice = null;
            this.btnRef2.Size = new System.Drawing.Size(93, 31);
            this.btnRef2.TabIndex = 1752;
            this.btnRef2.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanLedLabel2
            // 
            this.kabanLedLabel2.ConvertToHex = false;
            this.kabanLedLabel2.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
            this.kabanLedLabel2.Device = "D52";
            this.kabanLedLabel2.DigitCount = 6;
            this.kabanLedLabel2.DirectShow = false;
            this.kabanLedLabel2.Divisor = 1;
            this.kabanLedLabel2.FloatingPoint = 1;
            this.kabanLedLabel2.IsConvertible = false;
            this.kabanLedLabel2.LabelText = ",0";
            this.kabanLedLabel2.Location = new System.Drawing.Point(249, 108);
            this.kabanLedLabel2.Name = "kabanLedLabel2";
            this.kabanLedLabel2.ReadMode = KabanMachinesSystem.Common.Enums.ReadMode.CONTINUOUS;
            this.kabanLedLabel2.Size = new System.Drawing.Size(109, 43);
            this.kabanLedLabel2.TabIndex = 1751;
            // 
            // lblAxisY1
            // 
            this.lblAxisY1.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblAxisY1.Appearance.Options.UseFont = true;
            this.lblAxisY1.Appearance.Options.UseTextOptions = true;
            this.lblAxisY1.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblAxisY1.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblAxisY1.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblAxisY1.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblAxisY1.Location = new System.Drawing.Point(15, 114);
            this.lblAxisY1.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblAxisY1.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblAxisY1.Name = "lblAxisY1";
            this.lblAxisY1.Size = new System.Drawing.Size(130, 31);
            this.lblAxisY1.TabIndex = 1749;
            this.lblAxisY1.Text = "Axis Y1";
            // 
            // btnRef1
            // 
            this.btnRef1.ButtonTag = null;
            this.btnRef1.ButtonText = "";
            this.btnRef1.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.btnRef1.DefaultValue = null;
            this.btnRef1.Device = "M30";
            this.btnRef1.IsHeadControlButton = false;
            this.btnRef1.IsReferanceButton = false;
            this.btnRef1.IsScrewingControl = false;
            this.btnRef1.IsSecurityButton = false;
            this.btnRef1.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.btnRef1.LambColor = KabanMachinesSystem.Common.Enums.LambColor.Green;
            this.btnRef1.LambDevice = "M30";
            this.btnRef1.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.btnRef1.Location = new System.Drawing.Point(151, 77);
            this.btnRef1.Name = "btnRef1";
            this.btnRef1.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnRef1.ResetDevice = null;
            this.btnRef1.Size = new System.Drawing.Size(93, 31);
            this.btnRef1.TabIndex = 1748;
            this.btnRef1.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanLedLabel1
            // 
            this.kabanLedLabel1.ConvertToHex = false;
            this.kabanLedLabel1.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
            this.kabanLedLabel1.Device = "D50";
            this.kabanLedLabel1.DigitCount = 6;
            this.kabanLedLabel1.DirectShow = false;
            this.kabanLedLabel1.Divisor = 1;
            this.kabanLedLabel1.FloatingPoint = 1;
            this.kabanLedLabel1.IsConvertible = false;
            this.kabanLedLabel1.LabelText = ",0";
            this.kabanLedLabel1.Location = new System.Drawing.Point(249, 71);
            this.kabanLedLabel1.Name = "kabanLedLabel1";
            this.kabanLedLabel1.ReadMode = KabanMachinesSystem.Common.Enums.ReadMode.CONTINUOUS;
            this.kabanLedLabel1.Size = new System.Drawing.Size(109, 43);
            this.kabanLedLabel1.TabIndex = 1747;
            // 
            // lblAxisX1Mk
            // 
            this.lblAxisX1Mk.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblAxisX1Mk.Appearance.Options.UseFont = true;
            this.lblAxisX1Mk.Appearance.Options.UseTextOptions = true;
            this.lblAxisX1Mk.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblAxisX1Mk.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblAxisX1Mk.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblAxisX1Mk.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblAxisX1Mk.Location = new System.Drawing.Point(15, 77);
            this.lblAxisX1Mk.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblAxisX1Mk.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblAxisX1Mk.Name = "lblAxisX1Mk";
            this.lblAxisX1Mk.Size = new System.Drawing.Size(130, 31);
            this.lblAxisX1Mk.TabIndex = 1745;
            this.lblAxisX1Mk.Text = "Axis X";
            // 
            // lblPositions
            // 
            this.lblPositions.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblPositions.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lblPositions.Appearance.Options.UseBackColor = true;
            this.lblPositions.Appearance.Options.UseFont = true;
            this.lblPositions.Appearance.Options.UseTextOptions = true;
            this.lblPositions.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblPositions.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblPositions.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblPositions.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblPositions.Location = new System.Drawing.Point(250, 14);
            this.lblPositions.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblPositions.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblPositions.Name = "lblPositions";
            this.lblPositions.Size = new System.Drawing.Size(102, 57);
            this.lblPositions.TabIndex = 1744;
            this.lblPositions.Text = "Positions";
            // 
            // lblForward
            // 
            this.lblForward.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblForward.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lblForward.Appearance.Options.UseBackColor = true;
            this.lblForward.Appearance.Options.UseFont = true;
            this.lblForward.Appearance.Options.UseTextOptions = true;
            this.lblForward.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblForward.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblForward.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblForward.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblForward.Location = new System.Drawing.Point(151, 14);
            this.lblForward.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblForward.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblForward.Name = "lblForward";
            this.lblForward.Size = new System.Drawing.Size(93, 57);
            this.lblForward.TabIndex = 1743;
            this.lblForward.Text = "Forward";
            // 
            // lblName
            // 
            this.lblName.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblName.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lblName.Appearance.Options.UseBackColor = true;
            this.lblName.Appearance.Options.UseFont = true;
            this.lblName.Appearance.Options.UseTextOptions = true;
            this.lblName.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblName.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblName.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblName.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblName.Location = new System.Drawing.Point(15, 14);
            this.lblName.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblName.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblName.Name = "lblName";
            this.lblName.Size = new System.Drawing.Size(130, 57);
            this.lblName.TabIndex = 1742;
            this.lblName.Text = "Name";
            // 
            // lblReferenceOk
            // 
            this.lblReferenceOk.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblReferenceOk.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lblReferenceOk.Appearance.Options.UseBackColor = true;
            this.lblReferenceOk.Appearance.Options.UseFont = true;
            this.lblReferenceOk.Appearance.Options.UseTextOptions = true;
            this.lblReferenceOk.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblReferenceOk.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblReferenceOk.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblReferenceOk.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblReferenceOk.Location = new System.Drawing.Point(429, 14);
            this.lblReferenceOk.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblReferenceOk.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblReferenceOk.Name = "lblReferenceOk";
            this.lblReferenceOk.Size = new System.Drawing.Size(100, 57);
            this.lblReferenceOk.TabIndex = 1741;
            this.lblReferenceOk.Text = "Reference OK";
            // 
            // lblLimit
            // 
            this.lblLimit.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblLimit.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lblLimit.Appearance.Options.UseBackColor = true;
            this.lblLimit.Appearance.Options.UseFont = true;
            this.lblLimit.Appearance.Options.UseTextOptions = true;
            this.lblLimit.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblLimit.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblLimit.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblLimit.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblLimit.Location = new System.Drawing.Point(358, 14);
            this.lblLimit.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblLimit.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblLimit.Name = "lblLimit";
            this.lblLimit.Size = new System.Drawing.Size(65, 57);
            this.lblLimit.TabIndex = 1760;
            this.lblLimit.Text = "Limit";
            // 
            // kabanLamb1
            // 
            this.kabanLamb1.DefaultValue = null;
            this.kabanLamb1.LambColor = KabanMachinesSystem.Common.Enums.LambColor.Green;
            this.kabanLamb1.LambDevice = "M22";
            this.kabanLamb1.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.kabanLamb1.Location = new System.Drawing.Point(429, 76);
            this.kabanLamb1.Name = "kabanLamb1";
            this.kabanLamb1.Size = new System.Drawing.Size(100, 31);
            this.kabanLamb1.TabIndex = 1764;
            this.kabanLamb1.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanLamb2
            // 
            this.kabanLamb2.DefaultValue = null;
            this.kabanLamb2.LambColor = KabanMachinesSystem.Common.Enums.LambColor.Green;
            this.kabanLamb2.LambDevice = "M20";
            this.kabanLamb2.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.kabanLamb2.Location = new System.Drawing.Point(429, 114);
            this.kabanLamb2.Name = "kabanLamb2";
            this.kabanLamb2.Size = new System.Drawing.Size(100, 31);
            this.kabanLamb2.TabIndex = 1765;
            this.kabanLamb2.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanLamb3
            // 
            this.kabanLamb3.DefaultValue = null;
            this.kabanLamb3.LambColor = KabanMachinesSystem.Common.Enums.LambColor.Green;
            this.kabanLamb3.LambDevice = "M21";
            this.kabanLamb3.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.kabanLamb3.Location = new System.Drawing.Point(429, 151);
            this.kabanLamb3.Name = "kabanLamb3";
            this.kabanLamb3.Size = new System.Drawing.Size(100, 31);
            this.kabanLamb3.TabIndex = 1766;
            this.kabanLamb3.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // lblXY1Y2
            // 
            this.lblXY1Y2.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
            this.lblXY1Y2.Appearance.Options.UseFont = true;
            this.lblXY1Y2.Appearance.Options.UseTextOptions = true;
            this.lblXY1Y2.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblXY1Y2.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblXY1Y2.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblXY1Y2.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblXY1Y2.Location = new System.Drawing.Point(15, 188);
            this.lblXY1Y2.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblXY1Y2.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblXY1Y2.Name = "lblXY1Y2";
            this.lblXY1Y2.Size = new System.Drawing.Size(130, 31);
            this.lblXY1Y2.TabIndex = 1771;
            this.lblXY1Y2.Text = "X, Y1, Y2";
            // 
            // btnRefAll
            // 
            this.btnRefAll.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnRefAll.ButtonTag = null;
            this.btnRefAll.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.btnRefAll.CriticalityStatus = false;
            this.btnRefAll.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnRefAll.DefaultValue = 0;
            this.btnRefAll.Device = "";
            this.btnRefAll.IsReferanceButton = false;
            this.btnRefAll.IsScrewingControl = false;
            this.btnRefAll.Location = new System.Drawing.Point(151, 188);
            this.btnRefAll.Name = "btnRefAll";
            this.btnRefAll.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnRefAll.ProcessDevice = false;
            this.btnRefAll.ResetDevice = null;
            this.btnRefAll.Size = new System.Drawing.Size(93, 31);
            this.btnRefAll.TabIndex = 1772;
            this.btnRefAll.Text = "Reference All";
            this.btnRefAll.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanLambAlternative2
            // 
            this.kabanLambAlternative2.DefaultValue = null;
            this.kabanLambAlternative2.LambColor = KabanMachinesSystem.Common.Enums.LambColor.Red;
            this.kabanLambAlternative2.LambDevice = "M102";
            this.kabanLambAlternative2.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.kabanLambAlternative2.Location = new System.Drawing.Point(358, 114);
            this.kabanLambAlternative2.Name = "kabanLambAlternative2";
            this.kabanLambAlternative2.Size = new System.Drawing.Size(65, 30);
            this.kabanLambAlternative2.TabIndex = 1774;
            this.kabanLambAlternative2.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanLambAlternative3
            // 
            this.kabanLambAlternative3.DefaultValue = null;
            this.kabanLambAlternative3.LambColor = KabanMachinesSystem.Common.Enums.LambColor.Green;
            this.kabanLambAlternative3.LambDevice = "M104";
            this.kabanLambAlternative3.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.kabanLambAlternative3.Location = new System.Drawing.Point(358, 150);
            this.kabanLambAlternative3.Name = "kabanLambAlternative3";
            this.kabanLambAlternative3.Size = new System.Drawing.Size(65, 32);
            this.kabanLambAlternative3.TabIndex = 1775;
            this.kabanLambAlternative3.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanLambAlternative1
            // 
            this.kabanLambAlternative1.DefaultValue = null;
            this.kabanLambAlternative1.LambColor = KabanMachinesSystem.Common.Enums.LambColor.Red;
            this.kabanLambAlternative1.LambDevice = "M100";
            this.kabanLambAlternative1.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.kabanLambAlternative1.Location = new System.Drawing.Point(358, 78);
            this.kabanLambAlternative1.Name = "kabanLambAlternative1";
            this.kabanLambAlternative1.Size = new System.Drawing.Size(65, 30);
            this.kabanLambAlternative1.TabIndex = 1776;
            this.kabanLambAlternative1.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // ucReferance
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucReferance";
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(614, 389);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private KabanDevBase.Plc_Componets.KabanLambButton btnRef3;
        private KabanDevBase.Plc_Componets.KabanLedLabel kabanLedLabel3;
        private DevExpress.XtraEditors.LabelControl lblAxisY2;
        private KabanDevBase.Plc_Componets.KabanLambButton btnRef2;
        private KabanDevBase.Plc_Componets.KabanLedLabel kabanLedLabel2;
        private DevExpress.XtraEditors.LabelControl lblAxisY1;
        private KabanDevBase.Plc_Componets.KabanLambButton btnRef1;
        private KabanDevBase.Plc_Componets.KabanLedLabel kabanLedLabel1;
        private DevExpress.XtraEditors.LabelControl lblAxisX1Mk;
        private DevExpress.XtraEditors.LabelControl lblPositions;
        private DevExpress.XtraEditors.LabelControl lblForward;
        private DevExpress.XtraEditors.LabelControl lblName;
        private DevExpress.XtraEditors.LabelControl lblReferenceOk;
        private DevExpress.XtraEditors.LabelControl lblLimit;
        private KabanDevBase.Plc_Componets.KabanLamb kabanLamb1;
        private KabanDevBase.Plc_Componets.KabanLamb kabanLamb3;
        private KabanDevBase.Plc_Componets.KabanLamb kabanLamb2;
        private DevExpress.XtraEditors.LabelControl lblXY1Y2;
        private KabanDevBase.Plc_Componets.KabanButton btnRefAll;
        private KabanDevBase.Plc_Componets.KabanLambAlternative kabanLambAlternative1;
        private KabanDevBase.Plc_Componets.KabanLambAlternative kabanLambAlternative3;
        private KabanDevBase.Plc_Componets.KabanLambAlternative kabanLambAlternative2;
    }
}
