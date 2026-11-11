namespace _MD3030.Presentation.Settings.MachineSettings
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
            this.ktsPrinterActive = new KabanDevBase.Controls.KabanToggleSwitchControl();
            this.kleActiveLabel = new KabanDevBase.Controls.KabanLookUpEditControl();
            this.txtBarcodeCount = new KabanDevBase.Controls.KabanNumericEditControl();
            this.kleLeftOver = new KabanDevBase.Controls.KabanLookUpEditControl();
            this.ktsLeftOverActive = new KabanDevBase.Controls.KabanToggleSwitchControl();
            this.kleMachineTypeController = new KabanDevBase.Controls.KabanComboBoxControl();
            this.lblModelType = new DevExpress.XtraEditors.LabelControl();
            this.lblModel = new DevExpress.XtraEditors.LabelControl();
            this.labelControl1 = new DevExpress.XtraEditors.LabelControl();
            this.lblSerie = new DevExpress.XtraEditors.LabelControl();
            this.kleBarcodeField3 = new KabanDevBase.Controls.KabanComboBoxControl();
            this.kleBarcodeField2 = new KabanDevBase.Controls.KabanComboBoxControl();
            this.kleBarcodeField1 = new KabanDevBase.Controls.KabanComboBoxControl();
            this.kneBarcodeField2Lenght = new KabanDevBase.Controls.KabanNumericEditControl();
            this.kneBarcodeField3Lenght = new KabanDevBase.Controls.KabanNumericEditControl();
            this.kneBarcodeField1Lenght = new KabanDevBase.Controls.KabanNumericEditControl();
            this.lblBarcodeFields = new DevExpress.XtraEditors.LabelControl();
            this.kneBarcodeField1StartIndex = new KabanDevBase.Controls.KabanNumericEditControl();
            this.kneBarcodeField2StartIndex = new KabanDevBase.Controls.KabanNumericEditControl();
            this.kneBarcodeField3StartIndex = new KabanDevBase.Controls.KabanNumericEditControl();
            this.lblReadEncoderAdress = new DevExpress.XtraEditors.LabelControl();
            this.lblBodyX = new DevExpress.XtraEditors.LabelControl();
            this.lblBodyY = new DevExpress.XtraEditors.LabelControl();
            this.kabanButton30 = new KabanDevBase.Plc_Componets.KabanButton();
            this.kabanButton1 = new KabanDevBase.Plc_Componets.KabanButton();
            this.kabanLedLabel9 = new KabanDevBase.Plc_Componets.KabanLedLabel();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.kabanLedLabel9);
            this.BoxWrapper.Controls.Add(this.kabanButton1);
            this.BoxWrapper.Controls.Add(this.kabanButton30);
            this.BoxWrapper.Controls.Add(this.lblBodyY);
            this.BoxWrapper.Controls.Add(this.lblBodyX);
            this.BoxWrapper.Controls.Add(this.lblReadEncoderAdress);
            this.BoxWrapper.Controls.Add(this.kneBarcodeField3StartIndex);
            this.BoxWrapper.Controls.Add(this.kneBarcodeField2StartIndex);
            this.BoxWrapper.Controls.Add(this.kneBarcodeField1StartIndex);
            this.BoxWrapper.Controls.Add(this.lblBarcodeFields);
            this.BoxWrapper.Controls.Add(this.kleBarcodeField3);
            this.BoxWrapper.Controls.Add(this.kleBarcodeField2);
            this.BoxWrapper.Controls.Add(this.kneBarcodeField1Lenght);
            this.BoxWrapper.Controls.Add(this.kneBarcodeField3Lenght);
            this.BoxWrapper.Controls.Add(this.kneBarcodeField2Lenght);
            this.BoxWrapper.Controls.Add(this.kleBarcodeField1);
            this.BoxWrapper.Controls.Add(this.labelControl1);
            this.BoxWrapper.Controls.Add(this.lblSerie);
            this.BoxWrapper.Controls.Add(this.lblModelType);
            this.BoxWrapper.Controls.Add(this.lblModel);
            this.BoxWrapper.Controls.Add(this.kleMachineTypeController);
            this.BoxWrapper.Controls.Add(this.kleLeftOver);
            this.BoxWrapper.Controls.Add(this.txtBarcodeCount);
            this.BoxWrapper.Controls.Add(this.kleActiveLabel);
            this.BoxWrapper.Controls.Add(this.ktsPrinterActive);
            this.BoxWrapper.Controls.Add(this.btnSave);
            this.BoxWrapper.Controls.Add(this.ktsLeftOverActive);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.Size = new System.Drawing.Size(975, 680);
            this.BoxWrapper.Text = "Machine Settings";
            this.BoxWrapper.Paint += new System.Windows.Forms.PaintEventHandler(this.BoxWrapper_Paint);
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
            this.btnSave.Location = new System.Drawing.Point(905, 630);
            this.btnSave.LookAndFeel.SkinName = "The Bezier";
            this.btnSave.LookAndFeel.UseDefaultLookAndFeel = false;
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(65, 45);
            this.btnSave.TabIndex = 80;
            // 
            // ktsPrinterActive
            // 
            this.ktsPrinterActive.Appearance.BackColor = System.Drawing.Color.Transparent;
            this.ktsPrinterActive.Appearance.Options.UseBackColor = true;
            this.ktsPrinterActive.BarManager = null;
            this.ktsPrinterActive.EntityPropertyName = null;
            this.ktsPrinterActive.HierarchialPath = "";
            this.ktsPrinterActive.IsRequired = false;
            this.ktsPrinterActive.LabelText = "Printer Active";
            this.ktsPrinterActive.Location = new System.Drawing.Point(4, 371);
            this.ktsPrinterActive.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.ktsPrinterActive.LookAndFeel.UseDefaultLookAndFeel = false;
            this.ktsPrinterActive.Name = "ktsPrinterActive";
            this.ktsPrinterActive.OffText = "Off";
            this.ktsPrinterActive.OnText = "On";
            this.ktsPrinterActive.Size = new System.Drawing.Size(108, 59);
            this.ktsPrinterActive.TabIndex = 85;
            this.ktsPrinterActive.Value = false;
            this.ktsPrinterActive.Visible = false;
            // 
            // kleActiveLabel
            // 
            this.kleActiveLabel.BarManager = null;
            this.kleActiveLabel.DataSource = null;
            this.kleActiveLabel.DisplayMember = "";
            this.kleActiveLabel.HierarchialPath = null;
            this.kleActiveLabel.IsRequired = false;
            this.kleActiveLabel.LabelText = "Active Label";
            this.kleActiveLabel.Location = new System.Drawing.Point(92, 371);
            this.kleActiveLabel.Name = "kleActiveLabel";
            this.kleActiveLabel.SearchMode = DevExpress.XtraEditors.Controls.SearchMode.AutoFilter;
            this.kleActiveLabel.Size = new System.Drawing.Size(285, 59);
            this.kleActiveLabel.TabIndex = 86;
            this.kleActiveLabel.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            this.kleActiveLabel.ValueMember = "";
            this.kleActiveLabel.Visible = false;
            // 
            // txtBarcodeCount
            // 
            this.txtBarcodeCount.BarManager = null;
            this.txtBarcodeCount.EntityPropertyName = "ProfileVCutting_Y";
            this.txtBarcodeCount.HierarchialPath = "ucCompanyDetails";
            this.txtBarcodeCount.IsConvertible = false;
            this.txtBarcodeCount.IsRequired = false;
            this.txtBarcodeCount.LabelText = "Barcode Count";
            this.txtBarcodeCount.Location = new System.Drawing.Point(383, 372);
            this.txtBarcodeCount.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.txtBarcodeCount.Mask = "n0";
            this.txtBarcodeCount.MaxValue = 0D;
            this.txtBarcodeCount.Min = 0D;
            this.txtBarcodeCount.Name = "txtBarcodeCount";
            this.txtBarcodeCount.Size = new System.Drawing.Size(169, 59);
            this.txtBarcodeCount.TabIndex = 92;
            this.txtBarcodeCount.TextBoxText = "0";
            this.txtBarcodeCount.TextEditTooltip = "";
            this.txtBarcodeCount.Visible = false;
            // 
            // kleLeftOver
            // 
            this.kleLeftOver.BarManager = null;
            this.kleLeftOver.DataSource = null;
            this.kleLeftOver.DisplayMember = "";
            this.kleLeftOver.HierarchialPath = null;
            this.kleLeftOver.IsRequired = false;
            this.kleLeftOver.LabelText = "Active Left Over";
            this.kleLeftOver.Location = new System.Drawing.Point(92, 436);
            this.kleLeftOver.Name = "kleLeftOver";
            this.kleLeftOver.SearchMode = DevExpress.XtraEditors.Controls.SearchMode.AutoFilter;
            this.kleLeftOver.Size = new System.Drawing.Size(285, 59);
            this.kleLeftOver.TabIndex = 94;
            this.kleLeftOver.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            this.kleLeftOver.ValueMember = "";
            this.kleLeftOver.Visible = false;
            // 
            // ktsLeftOverActive
            // 
            this.ktsLeftOverActive.Appearance.BackColor = System.Drawing.Color.Transparent;
            this.ktsLeftOverActive.Appearance.Options.UseBackColor = true;
            this.ktsLeftOverActive.BarManager = null;
            this.ktsLeftOverActive.EntityPropertyName = null;
            this.ktsLeftOverActive.HierarchialPath = "";
            this.ktsLeftOverActive.IsRequired = false;
            this.ktsLeftOverActive.LabelText = "Left Over";
            this.ktsLeftOverActive.Location = new System.Drawing.Point(4, 436);
            this.ktsLeftOverActive.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.ktsLeftOverActive.LookAndFeel.UseDefaultLookAndFeel = false;
            this.ktsLeftOverActive.Name = "ktsLeftOverActive";
            this.ktsLeftOverActive.OffText = "Off";
            this.ktsLeftOverActive.OnText = "On";
            this.ktsLeftOverActive.Size = new System.Drawing.Size(108, 59);
            this.ktsLeftOverActive.TabIndex = 95;
            this.ktsLeftOverActive.Value = false;
            this.ktsLeftOverActive.Visible = false;
            // 
            // kleMachineTypeController
            // 
            this.kleMachineTypeController.BarManager = null;
            this.kleMachineTypeController.HierarchialPath = null;
            this.kleMachineTypeController.IsRequired = false;
            this.kleMachineTypeController.LabelText = "Machine Type";
            this.kleMachineTypeController.Location = new System.Drawing.Point(383, 436);
            this.kleMachineTypeController.Name = "kleMachineTypeController";
            this.kleMachineTypeController.SelectedItem = null;
            this.kleMachineTypeController.Size = new System.Drawing.Size(285, 82);
            this.kleMachineTypeController.TabIndex = 102;
            this.kleMachineTypeController.Visible = false;
            // 
            // lblModelType
            // 
            this.lblModelType.Appearance.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblModelType.Appearance.Options.UseFont = true;
            this.lblModelType.Appearance.Options.UseTextOptions = true;
            this.lblModelType.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblModelType.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblModelType.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblModelType.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblModelType.Location = new System.Drawing.Point(97, 501);
            this.lblModelType.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblModelType.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblModelType.Name = "lblModelType";
            this.lblModelType.Size = new System.Drawing.Size(280, 31);
            this.lblModelType.TabIndex = 1028;
            this.lblModelType.Text = "CNC F-1";
            this.lblModelType.Visible = false;
            this.lblModelType.Click += new System.EventHandler(this.lblSafetySensor_Click);
            // 
            // lblModel
            // 
            this.lblModel.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblModel.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lblModel.Appearance.Options.UseBackColor = true;
            this.lblModel.Appearance.Options.UseFont = true;
            this.lblModel.Appearance.Options.UseTextOptions = true;
            this.lblModel.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblModel.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblModel.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblModel.Location = new System.Drawing.Point(4, 501);
            this.lblModel.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblModel.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblModel.Name = "lblModel";
            this.lblModel.Size = new System.Drawing.Size(87, 31);
            this.lblModel.TabIndex = 1027;
            this.lblModel.Text = "Model";
            this.lblModel.Visible = false;
            this.lblModel.Click += new System.EventHandler(this.lblName_Click);
            // 
            // labelControl1
            // 
            this.labelControl1.Appearance.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelControl1.Appearance.Options.UseFont = true;
            this.labelControl1.Appearance.Options.UseTextOptions = true;
            this.labelControl1.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.labelControl1.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.labelControl1.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.labelControl1.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.labelControl1.Location = new System.Drawing.Point(97, 538);
            this.labelControl1.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.labelControl1.LookAndFeel.UseDefaultLookAndFeel = false;
            this.labelControl1.Name = "labelControl1";
            this.labelControl1.Size = new System.Drawing.Size(280, 31);
            this.labelControl1.TabIndex = 1030;
            this.labelControl1.Text = "0312TYRES23";
            this.labelControl1.Visible = false;
            // 
            // lblSerie
            // 
            this.lblSerie.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblSerie.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lblSerie.Appearance.Options.UseBackColor = true;
            this.lblSerie.Appearance.Options.UseFont = true;
            this.lblSerie.Appearance.Options.UseTextOptions = true;
            this.lblSerie.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblSerie.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblSerie.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblSerie.Location = new System.Drawing.Point(4, 538);
            this.lblSerie.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblSerie.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblSerie.Name = "lblSerie";
            this.lblSerie.Size = new System.Drawing.Size(87, 31);
            this.lblSerie.TabIndex = 1029;
            this.lblSerie.Text = "Serie";
            this.lblSerie.Visible = false;
            // 
            // kleBarcodeField3
            // 
            this.kleBarcodeField3.BarManager = null;
            this.kleBarcodeField3.HierarchialPath = null;
            this.kleBarcodeField3.IsRequired = false;
            this.kleBarcodeField3.LabelText = "Barcode Field 3";
            this.kleBarcodeField3.Location = new System.Drawing.Point(5, 221);
            this.kleBarcodeField3.Name = "kleBarcodeField3";
            this.kleBarcodeField3.SelectedItem = null;
            this.kleBarcodeField3.Size = new System.Drawing.Size(285, 82);
            this.kleBarcodeField3.TabIndex = 1031;
            // 
            // kleBarcodeField2
            // 
            this.kleBarcodeField2.BarManager = null;
            this.kleBarcodeField2.HierarchialPath = null;
            this.kleBarcodeField2.IsRequired = false;
            this.kleBarcodeField2.LabelText = "Barcode Field  2";
            this.kleBarcodeField2.Location = new System.Drawing.Point(5, 154);
            this.kleBarcodeField2.Name = "kleBarcodeField2";
            this.kleBarcodeField2.SelectedItem = null;
            this.kleBarcodeField2.Size = new System.Drawing.Size(285, 82);
            this.kleBarcodeField2.TabIndex = 1032;
            // 
            // kleBarcodeField1
            // 
            this.kleBarcodeField1.BarManager = null;
            this.kleBarcodeField1.HierarchialPath = null;
            this.kleBarcodeField1.IsRequired = false;
            this.kleBarcodeField1.LabelText = "Barcode Field 1";
            this.kleBarcodeField1.Location = new System.Drawing.Point(5, 86);
            this.kleBarcodeField1.Name = "kleBarcodeField1";
            this.kleBarcodeField1.SelectedItem = null;
            this.kleBarcodeField1.Size = new System.Drawing.Size(285, 82);
            this.kleBarcodeField1.TabIndex = 1033;
            // 
            // kneBarcodeField2Lenght
            // 
            this.kneBarcodeField2Lenght.BarManager = null;
            this.kneBarcodeField2Lenght.EntityPropertyName = "";
            this.kneBarcodeField2Lenght.HierarchialPath = "ucCompanyDetails";
            this.kneBarcodeField2Lenght.IsConvertible = false;
            this.kneBarcodeField2Lenght.IsRequired = false;
            this.kneBarcodeField2Lenght.LabelText = "Barcode Field 2 Lenght";
            this.kneBarcodeField2Lenght.Location = new System.Drawing.Point(296, 154);
            this.kneBarcodeField2Lenght.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.kneBarcodeField2Lenght.Mask = "n0";
            this.kneBarcodeField2Lenght.MaxValue = 0D;
            this.kneBarcodeField2Lenght.Min = 0D;
            this.kneBarcodeField2Lenght.Name = "kneBarcodeField2Lenght";
            this.kneBarcodeField2Lenght.Size = new System.Drawing.Size(136, 59);
            this.kneBarcodeField2Lenght.TabIndex = 1034;
            this.kneBarcodeField2Lenght.TextBoxText = "0";
            this.kneBarcodeField2Lenght.TextEditTooltip = "";
            // 
            // kneBarcodeField3Lenght
            // 
            this.kneBarcodeField3Lenght.BarManager = null;
            this.kneBarcodeField3Lenght.EntityPropertyName = "";
            this.kneBarcodeField3Lenght.HierarchialPath = "ucCompanyDetails";
            this.kneBarcodeField3Lenght.IsConvertible = false;
            this.kneBarcodeField3Lenght.IsRequired = false;
            this.kneBarcodeField3Lenght.LabelText = "Barcode Field 3 Lenght";
            this.kneBarcodeField3Lenght.Location = new System.Drawing.Point(296, 221);
            this.kneBarcodeField3Lenght.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.kneBarcodeField3Lenght.Mask = "n0";
            this.kneBarcodeField3Lenght.MaxValue = 0D;
            this.kneBarcodeField3Lenght.Min = 0D;
            this.kneBarcodeField3Lenght.Name = "kneBarcodeField3Lenght";
            this.kneBarcodeField3Lenght.Size = new System.Drawing.Size(136, 59);
            this.kneBarcodeField3Lenght.TabIndex = 1035;
            this.kneBarcodeField3Lenght.TextBoxText = "0";
            this.kneBarcodeField3Lenght.TextEditTooltip = "";
            // 
            // kneBarcodeField1Lenght
            // 
            this.kneBarcodeField1Lenght.BarManager = null;
            this.kneBarcodeField1Lenght.EntityPropertyName = "";
            this.kneBarcodeField1Lenght.HierarchialPath = "ucCompanyDetails";
            this.kneBarcodeField1Lenght.IsConvertible = false;
            this.kneBarcodeField1Lenght.IsRequired = false;
            this.kneBarcodeField1Lenght.LabelText = "Barcode Field 1 Lenght";
            this.kneBarcodeField1Lenght.Location = new System.Drawing.Point(296, 85);
            this.kneBarcodeField1Lenght.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.kneBarcodeField1Lenght.Mask = "n0";
            this.kneBarcodeField1Lenght.MaxValue = 0D;
            this.kneBarcodeField1Lenght.Min = 0D;
            this.kneBarcodeField1Lenght.Name = "kneBarcodeField1Lenght";
            this.kneBarcodeField1Lenght.Size = new System.Drawing.Size(136, 59);
            this.kneBarcodeField1Lenght.TabIndex = 1036;
            this.kneBarcodeField1Lenght.TextBoxText = "0";
            this.kneBarcodeField1Lenght.TextEditTooltip = "";
            // 
            // lblBarcodeFields
            // 
            this.lblBarcodeFields.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblBarcodeFields.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lblBarcodeFields.Appearance.Options.UseBackColor = true;
            this.lblBarcodeFields.Appearance.Options.UseFont = true;
            this.lblBarcodeFields.Appearance.Options.UseTextOptions = true;
            this.lblBarcodeFields.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblBarcodeFields.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblBarcodeFields.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblBarcodeFields.Location = new System.Drawing.Point(5, 44);
            this.lblBarcodeFields.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblBarcodeFields.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblBarcodeFields.Name = "lblBarcodeFields";
            this.lblBarcodeFields.Size = new System.Drawing.Size(569, 36);
            this.lblBarcodeFields.TabIndex = 1037;
            this.lblBarcodeFields.Text = "Barcode Fields";
            // 
            // kneBarcodeField1StartIndex
            // 
            this.kneBarcodeField1StartIndex.BarManager = null;
            this.kneBarcodeField1StartIndex.EntityPropertyName = "";
            this.kneBarcodeField1StartIndex.HierarchialPath = "ucCompanyDetails";
            this.kneBarcodeField1StartIndex.IsConvertible = false;
            this.kneBarcodeField1StartIndex.IsRequired = false;
            this.kneBarcodeField1StartIndex.LabelText = "Barcode Field 1 Start Index";
            this.kneBarcodeField1StartIndex.Location = new System.Drawing.Point(438, 86);
            this.kneBarcodeField1StartIndex.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.kneBarcodeField1StartIndex.Mask = "n0";
            this.kneBarcodeField1StartIndex.MaxValue = 150D;
            this.kneBarcodeField1StartIndex.Min = 1D;
            this.kneBarcodeField1StartIndex.Name = "kneBarcodeField1StartIndex";
            this.kneBarcodeField1StartIndex.Size = new System.Drawing.Size(136, 59);
            this.kneBarcodeField1StartIndex.TabIndex = 1038;
            this.kneBarcodeField1StartIndex.TextBoxText = "0";
            this.kneBarcodeField1StartIndex.TextEditTooltip = "";
            // 
            // kneBarcodeField2StartIndex
            // 
            this.kneBarcodeField2StartIndex.BarManager = null;
            this.kneBarcodeField2StartIndex.EntityPropertyName = "";
            this.kneBarcodeField2StartIndex.HierarchialPath = "ucCompanyDetails";
            this.kneBarcodeField2StartIndex.IsConvertible = false;
            this.kneBarcodeField2StartIndex.IsRequired = false;
            this.kneBarcodeField2StartIndex.LabelText = "Barcode Field 2 Start Index";
            this.kneBarcodeField2StartIndex.Location = new System.Drawing.Point(438, 154);
            this.kneBarcodeField2StartIndex.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.kneBarcodeField2StartIndex.Mask = "n0";
            this.kneBarcodeField2StartIndex.MaxValue = 150D;
            this.kneBarcodeField2StartIndex.Min = 1D;
            this.kneBarcodeField2StartIndex.Name = "kneBarcodeField2StartIndex";
            this.kneBarcodeField2StartIndex.Size = new System.Drawing.Size(136, 59);
            this.kneBarcodeField2StartIndex.TabIndex = 1039;
            this.kneBarcodeField2StartIndex.TextBoxText = "0";
            this.kneBarcodeField2StartIndex.TextEditTooltip = "";
            // 
            // kneBarcodeField3StartIndex
            // 
            this.kneBarcodeField3StartIndex.BarManager = null;
            this.kneBarcodeField3StartIndex.EntityPropertyName = "";
            this.kneBarcodeField3StartIndex.HierarchialPath = "ucCompanyDetails";
            this.kneBarcodeField3StartIndex.IsConvertible = false;
            this.kneBarcodeField3StartIndex.IsRequired = false;
            this.kneBarcodeField3StartIndex.LabelText = "Barcode Field 3 Start Index";
            this.kneBarcodeField3StartIndex.Location = new System.Drawing.Point(438, 221);
            this.kneBarcodeField3StartIndex.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.kneBarcodeField3StartIndex.Mask = "n0";
            this.kneBarcodeField3StartIndex.MaxValue = 150D;
            this.kneBarcodeField3StartIndex.Min = 1D;
            this.kneBarcodeField3StartIndex.Name = "kneBarcodeField3StartIndex";
            this.kneBarcodeField3StartIndex.Size = new System.Drawing.Size(136, 59);
            this.kneBarcodeField3StartIndex.TabIndex = 1040;
            this.kneBarcodeField3StartIndex.TextBoxText = "0";
            this.kneBarcodeField3StartIndex.TextEditTooltip = "";
            // 
            // lblReadEncoderAdress
            // 
            this.lblReadEncoderAdress.Appearance.BackColor = System.Drawing.Color.LightGray;
            this.lblReadEncoderAdress.Appearance.Font = new System.Drawing.Font("Tahoma", 10F, System.Drawing.FontStyle.Bold);
            this.lblReadEncoderAdress.Appearance.Options.UseBackColor = true;
            this.lblReadEncoderAdress.Appearance.Options.UseFont = true;
            this.lblReadEncoderAdress.Appearance.Options.UseTextOptions = true;
            this.lblReadEncoderAdress.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblReadEncoderAdress.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblReadEncoderAdress.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblReadEncoderAdress.Location = new System.Drawing.Point(597, 44);
            this.lblReadEncoderAdress.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblReadEncoderAdress.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblReadEncoderAdress.Name = "lblReadEncoderAdress";
            this.lblReadEncoderAdress.Size = new System.Drawing.Size(366, 36);
            this.lblReadEncoderAdress.TabIndex = 1041;
            this.lblReadEncoderAdress.Text = "Read Encoder Adress";
            // 
            // lblBodyX
            // 
            this.lblBodyX.Appearance.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblBodyX.Appearance.Options.UseFont = true;
            this.lblBodyX.Appearance.Options.UseTextOptions = true;
            this.lblBodyX.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblBodyX.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblBodyX.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblBodyX.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblBodyX.Location = new System.Drawing.Point(597, 86);
            this.lblBodyX.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblBodyX.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblBodyX.Name = "lblBodyX";
            this.lblBodyX.Size = new System.Drawing.Size(85, 31);
            this.lblBodyX.TabIndex = 1042;
            this.lblBodyX.Text = "Body X";
            // 
            // lblBodyY
            // 
            this.lblBodyY.Appearance.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblBodyY.Appearance.Options.UseFont = true;
            this.lblBodyY.Appearance.Options.UseTextOptions = true;
            this.lblBodyY.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.lblBodyY.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.lblBodyY.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
            this.lblBodyY.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
            this.lblBodyY.Location = new System.Drawing.Point(597, 123);
            this.lblBodyY.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
            this.lblBodyY.LookAndFeel.UseDefaultLookAndFeel = false;
            this.lblBodyY.Name = "lblBodyY";
            this.lblBodyY.Size = new System.Drawing.Size(85, 31);
            this.lblBodyY.TabIndex = 1043;
            this.lblBodyY.Text = "Body Y";
            // 
            // kabanButton30
            // 
            this.kabanButton30.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.kabanButton30.ButtonTag = null;
            this.kabanButton30.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.kabanButton30.CriticalityStatus = false;
            this.kabanButton30.Cursor = System.Windows.Forms.Cursors.Hand;
            this.kabanButton30.DefaultValue = 0;
            this.kabanButton30.Device = "M51";
            this.kabanButton30.ImageOptions.Location = DevExpress.XtraEditors.ImageLocation.MiddleCenter;
            this.kabanButton30.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("kabanButton30.ImageOptions.SvgImage")));
            this.kabanButton30.ImageOptions.SvgImageSize = new System.Drawing.Size(20, 20);
            this.kabanButton30.IsReferanceButton = false;
            this.kabanButton30.IsScrewingControl = false;
            this.kabanButton30.Location = new System.Drawing.Point(688, 86);
            this.kabanButton30.Name = "kabanButton30";
            this.kabanButton30.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.kabanButton30.ProcessDevice = false;
            this.kabanButton30.ResetDevice = null;
            this.kabanButton30.Size = new System.Drawing.Size(89, 31);
            this.kabanButton30.TabIndex = 2544;
            this.kabanButton30.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanButton1
            // 
            this.kabanButton1.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.kabanButton1.ButtonTag = null;
            this.kabanButton1.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.kabanButton1.CriticalityStatus = false;
            this.kabanButton1.Cursor = System.Windows.Forms.Cursors.Hand;
            this.kabanButton1.DefaultValue = 0;
            this.kabanButton1.Device = "M51";
            this.kabanButton1.ImageOptions.Location = DevExpress.XtraEditors.ImageLocation.MiddleCenter;
            this.kabanButton1.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("kabanButton1.ImageOptions.SvgImage")));
            this.kabanButton1.ImageOptions.SvgImageSize = new System.Drawing.Size(20, 20);
            this.kabanButton1.IsReferanceButton = false;
            this.kabanButton1.IsScrewingControl = false;
            this.kabanButton1.Location = new System.Drawing.Point(688, 123);
            this.kabanButton1.Name = "kabanButton1";
            this.kabanButton1.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.kabanButton1.ProcessDevice = false;
            this.kabanButton1.ResetDevice = null;
            this.kabanButton1.Size = new System.Drawing.Size(89, 31);
            this.kabanButton1.TabIndex = 2545;
            this.kabanButton1.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kabanLedLabel9
            // 
            this.kabanLedLabel9.ConvertToHex = false;
            this.kabanLedLabel9.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_16;
            this.kabanLedLabel9.Device = "D2822";
            this.kabanLedLabel9.DigitCount = 6;
            this.kabanLedLabel9.DirectShow = false;
            this.kabanLedLabel9.Divisor = 1;
            this.kabanLedLabel9.FloatingPoint = 2;
            this.kabanLedLabel9.IsConvertible = false;
            this.kabanLedLabel9.LabelText = ",00";
            this.kabanLedLabel9.Location = new System.Drawing.Point(862, 86);
            this.kabanLedLabel9.Name = "kabanLedLabel9";
            this.kabanLedLabel9.ReadMode = KabanMachinesSystem.Common.Enums.ReadMode.CONTINUOUS;
            this.kabanLedLabel9.Size = new System.Drawing.Size(101, 38);
            this.kabanLedLabel9.TabIndex = 2546;
            // 
            // ucMachineSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucMachineSettings";
            this.PageTitle = "Machine Settings";
            this.Size = new System.Drawing.Size(975, 680);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private KabanDevBase.Controls.KabanSimpleButtonControl btnSave;
        private KabanDevBase.Controls.KabanToggleSwitchControl ktsPrinterActive;
        private KabanDevBase.Controls.KabanNumericEditControl txtBarcodeCount;
        private KabanDevBase.Controls.KabanLookUpEditControl kleLeftOver;
        private KabanDevBase.Controls.KabanToggleSwitchControl ktsLeftOverActive;
        private KabanDevBase.Controls.KabanLookUpEditControl kleActiveLabel;
        private KabanDevBase.Controls.KabanComboBoxControl kleMachineTypeController;
        private DevExpress.XtraEditors.LabelControl lblModelType;
        private DevExpress.XtraEditors.LabelControl lblModel;
        private DevExpress.XtraEditors.LabelControl labelControl1;
        private DevExpress.XtraEditors.LabelControl lblSerie;
        private KabanDevBase.Controls.KabanNumericEditControl kneBarcodeField1Lenght;
        private KabanDevBase.Controls.KabanNumericEditControl kneBarcodeField3Lenght;
        private KabanDevBase.Controls.KabanNumericEditControl kneBarcodeField2Lenght;
        private KabanDevBase.Controls.KabanComboBoxControl kleBarcodeField1;
        private KabanDevBase.Controls.KabanComboBoxControl kleBarcodeField2;
        private KabanDevBase.Controls.KabanComboBoxControl kleBarcodeField3;
        private DevExpress.XtraEditors.LabelControl lblBarcodeFields;
        private KabanDevBase.Controls.KabanNumericEditControl kneBarcodeField3StartIndex;
        private KabanDevBase.Controls.KabanNumericEditControl kneBarcodeField2StartIndex;
        private KabanDevBase.Controls.KabanNumericEditControl kneBarcodeField1StartIndex;
        private DevExpress.XtraEditors.LabelControl lblBodyY;
        private DevExpress.XtraEditors.LabelControl lblBodyX;
        private DevExpress.XtraEditors.LabelControl lblReadEncoderAdress;
        private KabanDevBase.Plc_Componets.KabanButton kabanButton1;
        private KabanDevBase.Plc_Componets.KabanButton kabanButton30;
        private KabanDevBase.Plc_Componets.KabanLedLabel kabanLedLabel9;
    }
}
