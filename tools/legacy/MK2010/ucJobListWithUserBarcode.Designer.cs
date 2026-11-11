namespace _MK2010.Presentation.Home.Preparations
{
    partial class ucJobListWithUserBarcode
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucJobListWithUserBarcode));
            this.grdJobList1 = new KabanDevBase.Controls.KabanGridControl();
            this.kabanGridView1 = new KabanDevBase.Controls.KabanGridView();
            this.grdOrderNo = new DevExpress.XtraGrid.Columns.GridColumn();
            this.grdTrolleyNo = new DevExpress.XtraGrid.Columns.GridColumn();
            this.grdTrolleyShelfNo = new DevExpress.XtraGrid.Columns.GridColumn();
            this.grdWidth = new DevExpress.XtraGrid.Columns.GridColumn();
            this.grdHeight = new DevExpress.XtraGrid.Columns.GridColumn();
            this.grdProfileCode = new DevExpress.XtraGrid.Columns.GridColumn();
            this.grdCustomBarcode = new DevExpress.XtraGrid.Columns.GridColumn();
            this.btnFeed = new KabanDevBase.Plc_Componets.KabanButton();
            this.btnClear = new KabanDevBase.Plc_Componets.KabanButton();
            this.btnMessagebox333 = new KabanDevBase.Plc_Componets.KabanButton();
            this.btnMessagebox334 = new KabanDevBase.Plc_Componets.KabanButton();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.grdJobList1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.kabanGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.btnMessagebox334);
            this.BoxWrapper.Controls.Add(this.btnMessagebox333);
            this.BoxWrapper.Controls.Add(this.btnClear);
            this.BoxWrapper.Controls.Add(this.btnFeed);
            this.BoxWrapper.Controls.Add(this.grdJobList1);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(1006, 276);
            // 
            // grdJobList1
            // 
            this.grdJobList1.HierarchialPath = null;
            this.grdJobList1.Location = new System.Drawing.Point(26, 3);
            this.grdJobList1.MainView = this.kabanGridView1;
            this.grdJobList1.Name = "grdJobList1";
            this.grdJobList1.PopupMenu = null;
            this.grdJobList1.Size = new System.Drawing.Size(898, 270);
            this.grdJobList1.TabIndex = 0;
            this.grdJobList1.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.kabanGridView1});
            // 
            // kabanGridView1
            // 
            this.kabanGridView1.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.grdOrderNo,
            this.grdTrolleyNo,
            this.grdTrolleyShelfNo,
            this.grdWidth,
            this.grdHeight,
            this.grdProfileCode,
            this.grdCustomBarcode});
            this.kabanGridView1.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
            this.kabanGridView1.GridControl = this.grdJobList1;
            this.kabanGridView1.Name = "kabanGridView1";
            this.kabanGridView1.OptionsBehavior.Editable = false;
            this.kabanGridView1.OptionsView.ShowFooter = true;
            this.kabanGridView1.OptionsView.ShowGroupPanel = false;
            // 
            // grdOrderNo
            // 
            this.grdOrderNo.Caption = "Order No";
            this.grdOrderNo.FieldName = "OrderNo";
            this.grdOrderNo.Name = "grdOrderNo";
            this.grdOrderNo.Visible = true;
            this.grdOrderNo.VisibleIndex = 0;
            // 
            // grdTrolleyNo
            // 
            this.grdTrolleyNo.Caption = "Trolley No";
            this.grdTrolleyNo.Name = "grdTrolleyNo";
            this.grdTrolleyNo.Visible = true;
            this.grdTrolleyNo.VisibleIndex = 1;
            // 
            // grdTrolleyShelfNo
            // 
            this.grdTrolleyShelfNo.Caption = "Trolley Shelf No";
            this.grdTrolleyShelfNo.Name = "grdTrolleyShelfNo";
            this.grdTrolleyShelfNo.Visible = true;
            this.grdTrolleyShelfNo.VisibleIndex = 2;
            // 
            // grdWidth
            // 
            this.grdWidth.Caption = "Width";
            this.grdWidth.Name = "grdWidth";
            this.grdWidth.Visible = true;
            this.grdWidth.VisibleIndex = 3;
            // 
            // grdHeight
            // 
            this.grdHeight.Caption = "Heigth";
            this.grdHeight.Name = "grdHeight";
            this.grdHeight.Visible = true;
            this.grdHeight.VisibleIndex = 4;
            // 
            // grdProfileCode
            // 
            this.grdProfileCode.Caption = "Profile Code";
            this.grdProfileCode.Name = "grdProfileCode";
            this.grdProfileCode.Visible = true;
            this.grdProfileCode.VisibleIndex = 6;
            // 
            // grdCustomBarcode
            // 
            this.grdCustomBarcode.Caption = "Custom Barcode";
            this.grdCustomBarcode.Name = "grdCustomBarcode";
            this.grdCustomBarcode.Visible = true;
            this.grdCustomBarcode.VisibleIndex = 5;
            // 
            // btnFeed
            // 
            this.btnFeed.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnFeed.ButtonTag = null;
            this.btnFeed.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.btnFeed.CriticalityStatus = false;
            this.btnFeed.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnFeed.DefaultValue = 0;
            this.btnFeed.Device = null;
            this.btnFeed.Enabled = false;
            this.btnFeed.ImageOptions.ImageToTextAlignment = DevExpress.XtraEditors.ImageAlignToText.BottomCenter;
            this.btnFeed.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnFeed.ImageOptions.SvgImage")));
            this.btnFeed.IsReferanceButton = false;
            this.btnFeed.IsScrewingControl = false;
            this.btnFeed.Location = new System.Drawing.Point(930, 3);
            this.btnFeed.Name = "btnFeed";
            this.btnFeed.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnFeed.ProcessDevice = false;
            this.btnFeed.ResetDevice = null;
            this.btnFeed.Size = new System.Drawing.Size(61, 210);
            this.btnFeed.TabIndex = 1666;
            this.btnFeed.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // btnClear
            // 
            this.btnClear.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnClear.ButtonTag = null;
            this.btnClear.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.btnClear.CriticalityStatus = false;
            this.btnClear.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnClear.DefaultValue = 0;
            this.btnClear.Device = null;
            this.btnClear.ImageOptions.Image = global::_MK2010.Properties.Resources.delete1;
            this.btnClear.ImageOptions.ImageToTextAlignment = DevExpress.XtraEditors.ImageAlignToText.BottomCenter;
            this.btnClear.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnClear.ImageOptions.SvgImage")));
            this.btnClear.IsReferanceButton = false;
            this.btnClear.IsScrewingControl = false;
            this.btnClear.Location = new System.Drawing.Point(930, 219);
            this.btnClear.Name = "btnClear";
            this.btnClear.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnClear.ProcessDevice = false;
            this.btnClear.ResetDevice = null;
            this.btnClear.Size = new System.Drawing.Size(61, 54);
            this.btnClear.TabIndex = 1667;
            this.btnClear.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // btnMessagebox333
            // 
            this.btnMessagebox333.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnMessagebox333.ButtonTag = null;
            this.btnMessagebox333.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.btnMessagebox333.CriticalityStatus = false;
            this.btnMessagebox333.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnMessagebox333.DefaultValue = 0;
            this.btnMessagebox333.Device = null;
            this.btnMessagebox333.Enabled = false;
            this.btnMessagebox333.ImageOptions.ImageToTextAlignment = DevExpress.XtraEditors.ImageAlignToText.BottomCenter;
            this.btnMessagebox333.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnMessagebox333.ImageOptions.SvgImage")));
            this.btnMessagebox333.IsReferanceButton = false;
            this.btnMessagebox333.IsScrewingControl = false;
            this.btnMessagebox333.Location = new System.Drawing.Point(0, 5);
            this.btnMessagebox333.Name = "btnMessagebox333";
            this.btnMessagebox333.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnMessagebox333.ProcessDevice = false;
            this.btnMessagebox333.ResetDevice = null;
            this.btnMessagebox333.Size = new System.Drawing.Size(20, 34);
            this.btnMessagebox333.TabIndex = 1668;
            this.btnMessagebox333.Text = "Remove All";
            this.btnMessagebox333.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            this.btnMessagebox333.Visible = false;
            // 
            // btnMessagebox334
            // 
            this.btnMessagebox334.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnMessagebox334.ButtonTag = null;
            this.btnMessagebox334.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.btnMessagebox334.CriticalityStatus = false;
            this.btnMessagebox334.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnMessagebox334.DefaultValue = 0;
            this.btnMessagebox334.Device = null;
            this.btnMessagebox334.Enabled = false;
            this.btnMessagebox334.ImageOptions.ImageToTextAlignment = DevExpress.XtraEditors.ImageAlignToText.BottomCenter;
            this.btnMessagebox334.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnMessagebox334.ImageOptions.SvgImage")));
            this.btnMessagebox334.IsReferanceButton = false;
            this.btnMessagebox334.IsScrewingControl = false;
            this.btnMessagebox334.Location = new System.Drawing.Point(0, 45);
            this.btnMessagebox334.Name = "btnMessagebox334";
            this.btnMessagebox334.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnMessagebox334.ProcessDevice = false;
            this.btnMessagebox334.ResetDevice = null;
            this.btnMessagebox334.Size = new System.Drawing.Size(20, 34);
            this.btnMessagebox334.TabIndex = 1669;
            this.btnMessagebox334.Text = "Are you sure that you want to remove all rows?";
            this.btnMessagebox334.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            this.btnMessagebox334.Visible = false;
            // 
            // ucJobListWithUserBarcode
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucJobListWithUserBarcode";
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(1006, 276);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.grdJobList1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.kabanGridView1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private KabanDevBase.Controls.KabanGridControl grdJobList1;
        private KabanDevBase.Controls.KabanGridView kabanGridView1;
        private DevExpress.XtraGrid.Columns.GridColumn grdOrderNo;
        private DevExpress.XtraGrid.Columns.GridColumn grdTrolleyNo;
        private DevExpress.XtraGrid.Columns.GridColumn grdTrolleyShelfNo;
        private DevExpress.XtraGrid.Columns.GridColumn grdWidth;
        private DevExpress.XtraGrid.Columns.GridColumn grdHeight;
        private DevExpress.XtraGrid.Columns.GridColumn grdProfileCode;
        private DevExpress.XtraGrid.Columns.GridColumn grdCustomBarcode;
        private KabanDevBase.Plc_Componets.KabanButton btnFeed;
        private KabanDevBase.Plc_Componets.KabanButton btnClear;
        private KabanDevBase.Plc_Componets.KabanButton btnMessagebox334;
        private KabanDevBase.Plc_Componets.KabanButton btnMessagebox333;
    }
}
