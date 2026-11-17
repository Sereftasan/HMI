namespace SB3015.Presentation.Settings.ToolSettings
{
    partial class ucToolSettings
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucToolSettings));
            this.btnSave = new KabanDevBase.Controls.KabanSimpleButtonControl(this.components);
            this.btnRefresh = new KabanDevBase.Controls.KabanSimpleButtonControl(this.components);
            this.btnDrillBits = new KabanDevBase.Controls.KabanSimpleButtonControl(this.components);
            this.lueDrillBit_Tool1 = new KabanDevBase.Controls.KabanLookUpEditControl();
            this.lueDrillBit_Tool2 = new KabanDevBase.Controls.KabanLookUpEditControl();
            this.pictureEdit1 = new DevExpress.XtraEditors.PictureEdit();
            this.kneMillingZCenterDistanceR = new KabanDevBase.Controls.KabanNumericEditControl();
            this.kneMillingUnitCenterProfileZReferenceDistanceZ = new KabanDevBase.Controls.KabanNumericEditControl();
            this.kneMillingUnitCenterProfileYReferenceDistanceY = new KabanDevBase.Controls.KabanNumericEditControl();
            this.kneZAxisValueat180Degrees = new KabanDevBase.Controls.KabanNumericEditControl();
            this.kneZAxisValueat90Degrees = new KabanDevBase.Controls.KabanNumericEditControl();
            this.kneZAxisValueat0Degrees = new KabanDevBase.Controls.KabanNumericEditControl();
            this.kneWidthoftheFlatBarA = new KabanDevBase.Controls.KabanNumericEditControl();
            this.kneHeightoftheFlatBarB = new KabanDevBase.Controls.KabanNumericEditControl();
            this.kgcCalibration = new KabanDevBase.Controls.KabanGroupControl(this.components);
            this.ksbCalibrate = new KabanDevBase.Controls.KabanSimpleButtonControl(this.components);
            this.kgcCalibrationResult = new KabanDevBase.Controls.KabanGroupControl(this.components);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureEdit1.Properties)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.kgcCalibration)).BeginInit();
            this.kgcCalibration.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.kgcCalibrationResult)).BeginInit();
            this.kgcCalibrationResult.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.Appearance.BackColor = System.Drawing.Color.White;
            this.BoxWrapper.Appearance.Options.UseBackColor = true;
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.kgcCalibrationResult);
            this.BoxWrapper.Controls.Add(this.kgcCalibration);
            this.BoxWrapper.Controls.Add(this.lueDrillBit_Tool2);
            this.BoxWrapper.Controls.Add(this.lueDrillBit_Tool1);
            this.BoxWrapper.Controls.Add(this.btnDrillBits);
            this.BoxWrapper.Controls.Add(this.btnRefresh);
            this.BoxWrapper.Controls.Add(this.btnSave);
            this.BoxWrapper.Controls.Add(this.pictureEdit1);
            this.BoxWrapper.LookAndFeel.SkinName = "Office 2013";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(1387, 890);
            this.BoxWrapper.Text = "";
            // 
            // btnSave
            // 
            this.btnSave.Appearance.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnSave.Appearance.Options.UseForeColor = true;
            this.btnSave.HierarchialPath = "ToolsPage";
            this.btnSave.ImageOptions.AllowGlyphSkinning = DevExpress.Utils.DefaultBoolean.True;
            this.btnSave.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnSave.ImageOptions.SvgImage")));
            this.btnSave.KeyboardDirectInput = false;
            this.btnSave.KeyboardDirectValue = null;
            this.btnSave.KeyboardShiftValue = null;
            this.btnSave.Keys = System.Windows.Forms.Keys.None;
            this.btnSave.Location = new System.Drawing.Point(17, 14);
            this.btnSave.LookAndFeel.SkinName = "The Bezier";
            this.btnSave.LookAndFeel.UseDefaultLookAndFeel = false;
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(65, 45);
            this.btnSave.TabIndex = 61;
            // 
            // btnRefresh
            // 
            this.btnRefresh.Appearance.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnRefresh.Appearance.Options.UseForeColor = true;
            this.btnRefresh.HierarchialPath = "ToolsPage";
            this.btnRefresh.ImageOptions.AllowGlyphSkinning = DevExpress.Utils.DefaultBoolean.True;
            this.btnRefresh.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnRefresh.ImageOptions.SvgImage")));
            this.btnRefresh.KeyboardDirectInput = false;
            this.btnRefresh.KeyboardDirectValue = null;
            this.btnRefresh.KeyboardShiftValue = null;
            this.btnRefresh.Keys = System.Windows.Forms.Keys.None;
            this.btnRefresh.Location = new System.Drawing.Point(88, 14);
            this.btnRefresh.LookAndFeel.SkinName = "The Bezier";
            this.btnRefresh.LookAndFeel.UseDefaultLookAndFeel = false;
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(65, 45);
            this.btnRefresh.TabIndex = 78;
            // 
            // btnDrillBits
            // 
            this.btnDrillBits.Appearance.ForeColor = System.Drawing.SystemColors.ControlText;
            this.btnDrillBits.Appearance.Options.UseForeColor = true;
            this.btnDrillBits.HierarchialPath = "ToolsPage";
            this.btnDrillBits.ImageOptions.AllowGlyphSkinning = DevExpress.Utils.DefaultBoolean.True;
            this.btnDrillBits.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnDrillBits.ImageOptions.SvgImage")));
            this.btnDrillBits.ImageOptions.SvgImageSize = new System.Drawing.Size(32, 32);
            this.btnDrillBits.KeyboardDirectInput = false;
            this.btnDrillBits.KeyboardDirectValue = null;
            this.btnDrillBits.KeyboardShiftValue = null;
            this.btnDrillBits.Keys = System.Windows.Forms.Keys.None;
            this.btnDrillBits.Location = new System.Drawing.Point(159, 14);
            this.btnDrillBits.LookAndFeel.SkinName = "The Bezier";
            this.btnDrillBits.LookAndFeel.UseDefaultLookAndFeel = false;
            this.btnDrillBits.Name = "btnDrillBits";
            this.btnDrillBits.Size = new System.Drawing.Size(65, 45);
            this.btnDrillBits.TabIndex = 79;
            // 
            // lueDrillBit_Tool1
            // 
            this.lueDrillBit_Tool1.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lueDrillBit_Tool1.BarManager = null;
            this.lueDrillBit_Tool1.DataSource = null;
            this.lueDrillBit_Tool1.DisplayMember = "";
            this.lueDrillBit_Tool1.HierarchialPath = null;
            this.lueDrillBit_Tool1.IsRequired = false;
            this.lueDrillBit_Tool1.LabelText = "Drill Bits 1";
            this.lueDrillBit_Tool1.Location = new System.Drawing.Point(1009, 447);
            this.lueDrillBit_Tool1.Name = "lueDrillBit_Tool1";
            this.lueDrillBit_Tool1.SearchMode = DevExpress.XtraEditors.Controls.SearchMode.AutoFilter;
            this.lueDrillBit_Tool1.Size = new System.Drawing.Size(165, 69);
            this.lueDrillBit_Tool1.TabIndex = 80;
            this.lueDrillBit_Tool1.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            this.lueDrillBit_Tool1.ValueMember = "";
            // 
            // lueDrillBit_Tool2
            // 
            this.lueDrillBit_Tool2.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.lueDrillBit_Tool2.BarManager = null;
            this.lueDrillBit_Tool2.DataSource = null;
            this.lueDrillBit_Tool2.DisplayMember = "";
            this.lueDrillBit_Tool2.HierarchialPath = null;
            this.lueDrillBit_Tool2.IsRequired = false;
            this.lueDrillBit_Tool2.LabelText = "Drill Bits 2";
            this.lueDrillBit_Tool2.Location = new System.Drawing.Point(1009, 320);
            this.lueDrillBit_Tool2.Name = "lueDrillBit_Tool2";
            this.lueDrillBit_Tool2.SearchMode = DevExpress.XtraEditors.Controls.SearchMode.AutoFilter;
            this.lueDrillBit_Tool2.Size = new System.Drawing.Size(165, 72);
            this.lueDrillBit_Tool2.TabIndex = 82;
            this.lueDrillBit_Tool2.TextEditStyle = DevExpress.XtraEditors.Controls.TextEditStyles.DisableTextEditor;
            this.lueDrillBit_Tool2.ValueMember = "";
            // 
            // pictureEdit1
            // 
            this.pictureEdit1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureEdit1.EditValue = global::SB3015.Properties.Resources.Tool_Image;
            this.pictureEdit1.Location = new System.Drawing.Point(2, 2);
            this.pictureEdit1.Name = "pictureEdit1";
            this.pictureEdit1.Properties.ShowCameraMenuItem = DevExpress.XtraEditors.Controls.CameraMenuItemVisibility.Auto;
            this.pictureEdit1.Properties.SizeMode = DevExpress.XtraEditors.Controls.PictureSizeMode.Zoom;
            this.pictureEdit1.Size = new System.Drawing.Size(1383, 886);
            this.pictureEdit1.TabIndex = 83;
            // 
            // kneMillingZCenterDistanceR
            // 
            this.kneMillingZCenterDistanceR.BarManager = null;
            this.kneMillingZCenterDistanceR.EntityPropertyName = null;
            this.kneMillingZCenterDistanceR.HierarchialPath = null;
            this.kneMillingZCenterDistanceR.IsConvertible = false;
            this.kneMillingZCenterDistanceR.IsRequired = false;
            this.kneMillingZCenterDistanceR.LabelText = "Milling Z Center Distance (r)";
            this.kneMillingZCenterDistanceR.Location = new System.Drawing.Point(6, 162);
            this.kneMillingZCenterDistanceR.Mask = "n2";
            this.kneMillingZCenterDistanceR.MaxValue = 0D;
            this.kneMillingZCenterDistanceR.Min = 0D;
            this.kneMillingZCenterDistanceR.Name = "kneMillingZCenterDistanceR";
            this.kneMillingZCenterDistanceR.Size = new System.Drawing.Size(378, 59);
            this.kneMillingZCenterDistanceR.TabIndex = 107;
            this.kneMillingZCenterDistanceR.TextBoxText = "";
            this.kneMillingZCenterDistanceR.TextEditTooltip = "";
            // 
            // kneMillingUnitCenterProfileZReferenceDistanceZ
            // 
            this.kneMillingUnitCenterProfileZReferenceDistanceZ.BarManager = null;
            this.kneMillingUnitCenterProfileZReferenceDistanceZ.EntityPropertyName = null;
            this.kneMillingUnitCenterProfileZReferenceDistanceZ.HierarchialPath = null;
            this.kneMillingUnitCenterProfileZReferenceDistanceZ.IsConvertible = false;
            this.kneMillingUnitCenterProfileZReferenceDistanceZ.IsRequired = false;
            this.kneMillingUnitCenterProfileZReferenceDistanceZ.LabelText = "Milling Unit Center – Profile Z Reference Distance (Z)";
            this.kneMillingUnitCenterProfileZReferenceDistanceZ.Location = new System.Drawing.Point(6, 97);
            this.kneMillingUnitCenterProfileZReferenceDistanceZ.Mask = "n3";
            this.kneMillingUnitCenterProfileZReferenceDistanceZ.MaxValue = 0D;
            this.kneMillingUnitCenterProfileZReferenceDistanceZ.Min = 0D;
            this.kneMillingUnitCenterProfileZReferenceDistanceZ.Name = "kneMillingUnitCenterProfileZReferenceDistanceZ";
            this.kneMillingUnitCenterProfileZReferenceDistanceZ.Size = new System.Drawing.Size(378, 59);
            this.kneMillingUnitCenterProfileZReferenceDistanceZ.TabIndex = 106;
            this.kneMillingUnitCenterProfileZReferenceDistanceZ.TextBoxText = "";
            this.kneMillingUnitCenterProfileZReferenceDistanceZ.TextEditTooltip = "";
            // 
            // kneMillingUnitCenterProfileYReferenceDistanceY
            // 
            this.kneMillingUnitCenterProfileYReferenceDistanceY.BarManager = null;
            this.kneMillingUnitCenterProfileYReferenceDistanceY.EntityPropertyName = null;
            this.kneMillingUnitCenterProfileYReferenceDistanceY.HierarchialPath = null;
            this.kneMillingUnitCenterProfileYReferenceDistanceY.IsConvertible = false;
            this.kneMillingUnitCenterProfileYReferenceDistanceY.IsRequired = false;
            this.kneMillingUnitCenterProfileYReferenceDistanceY.LabelText = "Milling Unit Center – Profile Y Reference Distance (Y)";
            this.kneMillingUnitCenterProfileYReferenceDistanceY.Location = new System.Drawing.Point(6, 35);
            this.kneMillingUnitCenterProfileYReferenceDistanceY.Mask = "n3";
            this.kneMillingUnitCenterProfileYReferenceDistanceY.MaxValue = 0D;
            this.kneMillingUnitCenterProfileYReferenceDistanceY.Min = 0D;
            this.kneMillingUnitCenterProfileYReferenceDistanceY.Name = "kneMillingUnitCenterProfileYReferenceDistanceY";
            this.kneMillingUnitCenterProfileYReferenceDistanceY.Size = new System.Drawing.Size(378, 59);
            this.kneMillingUnitCenterProfileYReferenceDistanceY.TabIndex = 105;
            this.kneMillingUnitCenterProfileYReferenceDistanceY.TextBoxText = "";
            this.kneMillingUnitCenterProfileYReferenceDistanceY.TextEditTooltip = "";
            // 
            // kneZAxisValueat180Degrees
            // 
            this.kneZAxisValueat180Degrees.BarManager = null;
            this.kneZAxisValueat180Degrees.EntityPropertyName = null;
            this.kneZAxisValueat180Degrees.HierarchialPath = null;
            this.kneZAxisValueat180Degrees.IsConvertible = false;
            this.kneZAxisValueat180Degrees.IsRequired = false;
            this.kneZAxisValueat180Degrees.LabelText = "Z-Axis Value (Z₁₈₀) at 180 Degrees";
            this.kneZAxisValueat180Degrees.Location = new System.Drawing.Point(5, 261);
            this.kneZAxisValueat180Degrees.Mask = "n2";
            this.kneZAxisValueat180Degrees.MaxValue = 0D;
            this.kneZAxisValueat180Degrees.Min = 0D;
            this.kneZAxisValueat180Degrees.Name = "kneZAxisValueat180Degrees";
            this.kneZAxisValueat180Degrees.Size = new System.Drawing.Size(378, 59);
            this.kneZAxisValueat180Degrees.TabIndex = 104;
            this.kneZAxisValueat180Degrees.TextBoxText = "";
            this.kneZAxisValueat180Degrees.TextEditTooltip = "";
            // 
            // kneZAxisValueat90Degrees
            // 
            this.kneZAxisValueat90Degrees.BarManager = null;
            this.kneZAxisValueat90Degrees.EntityPropertyName = null;
            this.kneZAxisValueat90Degrees.HierarchialPath = null;
            this.kneZAxisValueat90Degrees.IsConvertible = false;
            this.kneZAxisValueat90Degrees.IsRequired = false;
            this.kneZAxisValueat90Degrees.LabelText = "Z-Axis Value (Z₉₀) at 90 Degrees";
            this.kneZAxisValueat90Degrees.Location = new System.Drawing.Point(5, 204);
            this.kneZAxisValueat90Degrees.Mask = "n2";
            this.kneZAxisValueat90Degrees.MaxValue = 0D;
            this.kneZAxisValueat90Degrees.Min = 0D;
            this.kneZAxisValueat90Degrees.Name = "kneZAxisValueat90Degrees";
            this.kneZAxisValueat90Degrees.Size = new System.Drawing.Size(378, 59);
            this.kneZAxisValueat90Degrees.TabIndex = 103;
            this.kneZAxisValueat90Degrees.TextBoxText = "";
            this.kneZAxisValueat90Degrees.TextEditTooltip = "";
            // 
            // kneZAxisValueat0Degrees
            // 
            this.kneZAxisValueat0Degrees.Appearance.Options.UseTextOptions = true;
            this.kneZAxisValueat0Degrees.Appearance.TextOptions.WordWrap = DevExpress.Utils.WordWrap.Wrap;
            this.kneZAxisValueat0Degrees.BarManager = null;
            this.kneZAxisValueat0Degrees.EntityPropertyName = null;
            this.kneZAxisValueat0Degrees.HierarchialPath = null;
            this.kneZAxisValueat0Degrees.IsConvertible = false;
            this.kneZAxisValueat0Degrees.IsRequired = false;
            this.kneZAxisValueat0Degrees.LabelText = "Z-Axis Value (Z₀) at 0 Degrees";
            this.kneZAxisValueat0Degrees.Location = new System.Drawing.Point(5, 147);
            this.kneZAxisValueat0Degrees.Mask = "n2";
            this.kneZAxisValueat0Degrees.MaxValue = 0D;
            this.kneZAxisValueat0Degrees.Min = 0D;
            this.kneZAxisValueat0Degrees.Name = "kneZAxisValueat0Degrees";
            this.kneZAxisValueat0Degrees.Size = new System.Drawing.Size(378, 59);
            this.kneZAxisValueat0Degrees.TabIndex = 102;
            this.kneZAxisValueat0Degrees.TextBoxText = "";
            this.kneZAxisValueat0Degrees.TextEditTooltip = "";
            // 
            // kneWidthoftheFlatBarA
            // 
            this.kneWidthoftheFlatBarA.BarManager = null;
            this.kneWidthoftheFlatBarA.EntityPropertyName = null;
            this.kneWidthoftheFlatBarA.HierarchialPath = null;
            this.kneWidthoftheFlatBarA.IsConvertible = false;
            this.kneWidthoftheFlatBarA.IsRequired = false;
            this.kneWidthoftheFlatBarA.LabelText = "Width of the Flat Bar (a)";
            this.kneWidthoftheFlatBarA.Location = new System.Drawing.Point(5, 35);
            this.kneWidthoftheFlatBarA.Mask = "n2";
            this.kneWidthoftheFlatBarA.MaxValue = 0D;
            this.kneWidthoftheFlatBarA.Min = 0D;
            this.kneWidthoftheFlatBarA.Name = "kneWidthoftheFlatBarA";
            this.kneWidthoftheFlatBarA.Size = new System.Drawing.Size(378, 59);
            this.kneWidthoftheFlatBarA.TabIndex = 100;
            this.kneWidthoftheFlatBarA.TextBoxText = "";
            this.kneWidthoftheFlatBarA.TextEditTooltip = "";
            // 
            // kneHeightoftheFlatBarB
            // 
            this.kneHeightoftheFlatBarB.BarManager = null;
            this.kneHeightoftheFlatBarB.EntityPropertyName = null;
            this.kneHeightoftheFlatBarB.HierarchialPath = null;
            this.kneHeightoftheFlatBarB.IsConvertible = false;
            this.kneHeightoftheFlatBarB.IsRequired = false;
            this.kneHeightoftheFlatBarB.LabelText = "Height of the Flat Bar (b)";
            this.kneHeightoftheFlatBarB.Location = new System.Drawing.Point(5, 91);
            this.kneHeightoftheFlatBarB.Mask = "n2";
            this.kneHeightoftheFlatBarB.MaxValue = 0D;
            this.kneHeightoftheFlatBarB.Min = 0D;
            this.kneHeightoftheFlatBarB.Name = "kneHeightoftheFlatBarB";
            this.kneHeightoftheFlatBarB.Size = new System.Drawing.Size(378, 59);
            this.kneHeightoftheFlatBarB.TabIndex = 101;
            this.kneHeightoftheFlatBarB.TextBoxText = "";
            this.kneHeightoftheFlatBarB.TextEditTooltip = "";
            // 
            // kgcCalibration
            // 
            this.kgcCalibration.Controls.Add(this.ksbCalibrate);
            this.kgcCalibration.Controls.Add(this.kneWidthoftheFlatBarA);
            this.kgcCalibration.Controls.Add(this.kneHeightoftheFlatBarB);
            this.kgcCalibration.Controls.Add(this.kneZAxisValueat0Degrees);
            this.kgcCalibration.Controls.Add(this.kneZAxisValueat90Degrees);
            this.kgcCalibration.Controls.Add(this.kneZAxisValueat180Degrees);
            this.kgcCalibration.HierarchialPath = null;
            this.kgcCalibration.Location = new System.Drawing.Point(17, 99);
            this.kgcCalibration.LookAndFeel.SkinName = "Office 2019 Colorful";
            this.kgcCalibration.LookAndFeel.TouchScaleFactor = 1.5F;
            this.kgcCalibration.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.kgcCalibration.LookAndFeel.UseDefaultLookAndFeel = false;
            this.kgcCalibration.Name = "kgcCalibration";
            this.kgcCalibration.Size = new System.Drawing.Size(398, 364);
            this.kgcCalibration.TabIndex = 108;
            this.kgcCalibration.Text = "Calibration Group";
            // 
            // ksbCalibrate
            // 
            this.ksbCalibrate.HierarchialPath = null;
            this.ksbCalibrate.KeyboardDirectInput = false;
            this.ksbCalibrate.KeyboardDirectValue = null;
            this.ksbCalibrate.KeyboardShiftValue = null;
            this.ksbCalibrate.Keys = System.Windows.Forms.Keys.None;
            this.ksbCalibrate.Location = new System.Drawing.Point(248, 326);
            this.ksbCalibrate.Name = "ksbCalibrate";
            this.ksbCalibrate.Size = new System.Drawing.Size(135, 28);
            this.ksbCalibrate.TabIndex = 105;
            this.ksbCalibrate.Text = "Calibrate";
            // 
            // kgcCalibrationResult
            // 
            this.kgcCalibrationResult.Controls.Add(this.kneMillingUnitCenterProfileZReferenceDistanceZ);
            this.kgcCalibrationResult.Controls.Add(this.kneMillingUnitCenterProfileYReferenceDistanceY);
            this.kgcCalibrationResult.Controls.Add(this.kneMillingZCenterDistanceR);
            this.kgcCalibrationResult.HierarchialPath = null;
            this.kgcCalibrationResult.Location = new System.Drawing.Point(17, 479);
            this.kgcCalibrationResult.LookAndFeel.SkinName = "Office 2019 Colorful";
            this.kgcCalibrationResult.LookAndFeel.TouchScaleFactor = 1.5F;
            this.kgcCalibrationResult.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.kgcCalibrationResult.LookAndFeel.UseDefaultLookAndFeel = false;
            this.kgcCalibrationResult.Name = "kgcCalibrationResult";
            this.kgcCalibrationResult.Size = new System.Drawing.Size(398, 230);
            this.kgcCalibrationResult.TabIndex = 109;
            this.kgcCalibrationResult.Text = "Result";
            // 
            // ucToolSettings
            // 
            this.Appearance.BackColor = System.Drawing.Color.White;
            this.Appearance.Options.UseBackColor = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucToolSettings";
            this.PageTitle = "";
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(1387, 890);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureEdit1.Properties)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.kgcCalibration)).EndInit();
            this.kgcCalibration.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.kgcCalibrationResult)).EndInit();
            this.kgcCalibrationResult.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private KabanDevBase.Controls.KabanSimpleButtonControl btnSave;
        private KabanDevBase.Controls.KabanSimpleButtonControl btnDrillBits;
        private KabanDevBase.Controls.KabanSimpleButtonControl btnRefresh;
        private KabanDevBase.Controls.KabanLookUpEditControl lueDrillBit_Tool2;
        private KabanDevBase.Controls.KabanLookUpEditControl lueDrillBit_Tool1;
        private DevExpress.XtraEditors.PictureEdit pictureEdit1;
        private KabanDevBase.Controls.KabanNumericEditControl kneMillingZCenterDistanceR;
        private KabanDevBase.Controls.KabanNumericEditControl kneMillingUnitCenterProfileZReferenceDistanceZ;
        private KabanDevBase.Controls.KabanNumericEditControl kneMillingUnitCenterProfileYReferenceDistanceY;
        private KabanDevBase.Controls.KabanNumericEditControl kneZAxisValueat180Degrees;
        private KabanDevBase.Controls.KabanNumericEditControl kneZAxisValueat90Degrees;
        private KabanDevBase.Controls.KabanNumericEditControl kneZAxisValueat0Degrees;
        private KabanDevBase.Controls.KabanNumericEditControl kneWidthoftheFlatBarA;
        private KabanDevBase.Controls.KabanNumericEditControl kneHeightoftheFlatBarB;
        private KabanDevBase.Controls.KabanGroupControl kgcCalibrationResult;
        private KabanDevBase.Controls.KabanGroupControl kgcCalibration;
        private KabanDevBase.Controls.KabanSimpleButtonControl ksbCalibrate;
    }
}
