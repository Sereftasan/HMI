namespace _MD3030.Presentation.Home.Preparations
{
    partial class ucJobList
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucJobList));
            this.ucJobListGroupBox = new DevExpress.XtraEditors.GroupControl();
            this.kabanGridControlMain = new KabanDevBase.Controls.KabanGridControl();
            this.kabanGridViewMain = new KabanDevBase.Controls.KabanGridView();
            this.colImportDate = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colFileAliasName = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colMainFileName = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colCompany = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colSerie = new DevExpress.XtraGrid.Columns.GridColumn();
            this.ucJobListSplitContainer = new DevExpress.XtraEditors.SplitContainerControl();
            this.ucJobListButtonPanel = new DevExpress.XtraEditors.PanelControl();
            this.btnSelectReceipt = new DevExpress.XtraEditors.SimpleButton();
            this.btnSendJob = new DevExpress.XtraEditors.SimpleButton();
            this.btnDelete = new DevExpress.XtraEditors.SimpleButton();
            this.btnRefresh = new DevExpress.XtraEditors.SimpleButton();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ucJobListGroupBox)).BeginInit();
            this.ucJobListGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.kabanGridControlMain)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.kabanGridViewMain)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.ucJobListSplitContainer)).BeginInit();
            this.ucJobListSplitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ucJobListButtonPanel)).BeginInit();
            this.ucJobListButtonPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Size = new System.Drawing.Size(323, 481);
            this.BoxWrapper.Text = "";
            // 
            // ucJobListGroupBox
            // 
            this.ucJobListGroupBox.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 9F);
            this.ucJobListGroupBox.AppearanceCaption.Options.UseFont = true;
            this.ucJobListGroupBox.AppearanceCaption.Options.UseTextOptions = true;
            this.ucJobListGroupBox.AppearanceCaption.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
            this.ucJobListGroupBox.Controls.Add(this.kabanGridControlMain);
            this.ucJobListGroupBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ucJobListGroupBox.Location = new System.Drawing.Point(0, 0);
            this.ucJobListGroupBox.LookAndFeel.SkinName = "Office 2013 Light Gray";
            this.ucJobListGroupBox.LookAndFeel.TouchScaleFactor = 1.5F;
            this.ucJobListGroupBox.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.ucJobListGroupBox.LookAndFeel.UseDefaultLookAndFeel = false;
            this.ucJobListGroupBox.Name = "ucJobListGroupBox";
            this.ucJobListGroupBox.Size = new System.Drawing.Size(323, 422);
            this.ucJobListGroupBox.TabIndex = 1;
            this.ucJobListGroupBox.Text = "Waiting Job List";
            // 
            // kabanGridControlMain
            // 
            this.kabanGridControlMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.kabanGridControlMain.HierarchialPath = null;
            this.kabanGridControlMain.Location = new System.Drawing.Point(2, 21);
            this.kabanGridControlMain.MainView = this.kabanGridViewMain;
            this.kabanGridControlMain.Name = "kabanGridControlMain";
            this.kabanGridControlMain.PopupMenu = null;
            this.kabanGridControlMain.Size = new System.Drawing.Size(319, 399);
            this.kabanGridControlMain.TabIndex = 0;
            this.kabanGridControlMain.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.kabanGridViewMain});
            // 
            // kabanGridViewMain
            // 
            this.kabanGridViewMain.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.colImportDate,
            this.colFileAliasName,
            this.colMainFileName,
            this.colCompany,
            this.colSerie});
            this.kabanGridViewMain.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
            this.kabanGridViewMain.GridControl = this.kabanGridControlMain;
            this.kabanGridViewMain.Name = "kabanGridViewMain";
            this.kabanGridViewMain.OptionsBehavior.Editable = false;
            this.kabanGridViewMain.OptionsView.ShowGroupPanel = false;
            // 
            // colImportDate
            // 
            this.colImportDate.Caption = "Date";
            this.colImportDate.FieldName = "ImportDate";
            this.colImportDate.MinWidth = 75;
            this.colImportDate.Name = "colImportDate";
            this.colImportDate.Visible = true;
            this.colImportDate.VisibleIndex = 0;
            // 
            // colFileAliasName
            // 
            this.colFileAliasName.Caption = "File Name";
            this.colFileAliasName.FieldName = "FileAliasName";
            this.colFileAliasName.Name = "colFileAliasName";
            this.colFileAliasName.Visible = true;
            this.colFileAliasName.VisibleIndex = 1;
            this.colFileAliasName.Width = 106;
            // 
            // colMainFileName
            // 
            this.colMainFileName.Caption = "Main File Name";
            this.colMainFileName.FieldName = "MainFileName";
            this.colMainFileName.Name = "colMainFileName";
            // 
            // colCompany
            // 
            this.colCompany.Caption = "Company";
            this.colCompany.FieldName = "ProfileBrandType";
            this.colCompany.Name = "colCompany";
            this.colCompany.Visible = true;
            this.colCompany.VisibleIndex = 2;
            this.colCompany.Width = 62;
            // 
            // colSerie
            // 
            this.colSerie.Caption = "Serie";
            this.colSerie.FieldName = "ProfileSerie";
            this.colSerie.Name = "colSerie";
            this.colSerie.Visible = true;
            this.colSerie.VisibleIndex = 3;
            this.colSerie.Width = 57;
            // 
            // ucJobListSplitContainer
            // 
            this.ucJobListSplitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ucJobListSplitContainer.FixedPanel = DevExpress.XtraEditors.SplitFixedPanel.Panel2;
            this.ucJobListSplitContainer.Horizontal = false;
            this.ucJobListSplitContainer.IsSplitterFixed = true;
            this.ucJobListSplitContainer.Location = new System.Drawing.Point(0, 0);
            this.ucJobListSplitContainer.Name = "ucJobListSplitContainer";
            this.ucJobListSplitContainer.Panel1.Controls.Add(this.ucJobListGroupBox);
            this.ucJobListSplitContainer.Panel1.Text = "Panel1";
            this.ucJobListSplitContainer.Panel2.Controls.Add(this.ucJobListButtonPanel);
            this.ucJobListSplitContainer.Panel2.Text = "Panel2";
            this.ucJobListSplitContainer.Size = new System.Drawing.Size(323, 481);
            this.ucJobListSplitContainer.SplitterPosition = 47;
            this.ucJobListSplitContainer.TabIndex = 0;
            // 
            // ucJobListButtonPanel
            // 
            this.ucJobListButtonPanel.Controls.Add(this.btnSelectReceipt);
            this.ucJobListButtonPanel.Controls.Add(this.btnSendJob);
            this.ucJobListButtonPanel.Controls.Add(this.btnDelete);
            this.ucJobListButtonPanel.Controls.Add(this.btnRefresh);
            this.ucJobListButtonPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ucJobListButtonPanel.Location = new System.Drawing.Point(0, 0);
            this.ucJobListButtonPanel.LookAndFeel.SkinName = "The Bezier";
            this.ucJobListButtonPanel.LookAndFeel.UseDefaultLookAndFeel = false;
            this.ucJobListButtonPanel.Name = "ucJobListButtonPanel";
            this.ucJobListButtonPanel.Size = new System.Drawing.Size(323, 47);
            this.ucJobListButtonPanel.TabIndex = 0;
            // 
            // btnSelectReceipt
            // 
            this.btnSelectReceipt.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnSelectReceipt.ImageOptions.Image = ((System.Drawing.Image)(resources.GetObject("btnSelectReceipt.ImageOptions.Image")));
            this.btnSelectReceipt.Location = new System.Drawing.Point(51, 7);
            this.btnSelectReceipt.Name = "btnSelectReceipt";
            this.btnSelectReceipt.Size = new System.Drawing.Size(40, 37);
            this.btnSelectReceipt.TabIndex = 4;
            // 
            // btnSendJob
            // 
            this.btnSendJob.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSendJob.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnSendJob.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnSendJob.ImageOptions.SvgImage")));
            this.btnSendJob.Location = new System.Drawing.Point(278, 7);
            this.btnSendJob.Name = "btnSendJob";
            this.btnSendJob.Size = new System.Drawing.Size(40, 37);
            this.btnSendJob.TabIndex = 3;
            this.btnSendJob.Text = "Send Job";
            // 
            // btnDelete
            // 
            this.btnDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDelete.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnDelete.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnDelete.ImageOptions.SvgImage")));
            this.btnDelete.Location = new System.Drawing.Point(232, 7);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(40, 37);
            this.btnDelete.TabIndex = 2;
            this.btnDelete.Text = "Delete Job";
            // 
            // btnRefresh
            // 
            this.btnRefresh.ButtonStyle = DevExpress.XtraEditors.Controls.BorderStyles.HotFlat;
            this.btnRefresh.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnRefresh.ImageOptions.SvgImage")));
            this.btnRefresh.Location = new System.Drawing.Point(5, 7);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(40, 37);
            this.btnRefresh.TabIndex = 1;
            this.btnRefresh.Text = "Refresh";
            // 
            // ucJobList
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.ucJobListSplitContainer);
            this.LookAndFeel.SkinName = "Office 2013 Light Gray";
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.False;
            this.LookAndFeel.UseDefaultLookAndFeel = false;
            this.Name = "ucJobList";
            this.PageTitle = "";
            this.ShowCaption = false;
            this.Size = new System.Drawing.Size(323, 481);
            this.Controls.SetChildIndex(this.BoxWrapper, 0);
            this.Controls.SetChildIndex(this.ucJobListSplitContainer, 0);
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ucJobListGroupBox)).EndInit();
            this.ucJobListGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.kabanGridControlMain)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.kabanGridViewMain)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.ucJobListSplitContainer)).EndInit();
            this.ucJobListSplitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.ucJobListButtonPanel)).EndInit();
            this.ucJobListButtonPanel.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private DevExpress.XtraEditors.GroupControl ucJobListGroupBox;
        private DevExpress.XtraEditors.SplitContainerControl ucJobListSplitContainer;
        private DevExpress.XtraEditors.PanelControl ucJobListButtonPanel;
        private DevExpress.XtraEditors.SimpleButton btnDelete;
        private DevExpress.XtraEditors.SimpleButton btnRefresh;
        private KabanDevBase.Controls.KabanGridControl kabanGridControlMain;
        private KabanDevBase.Controls.KabanGridView kabanGridViewMain;
        private DevExpress.XtraGrid.Columns.GridColumn colImportDate;
        private DevExpress.XtraGrid.Columns.GridColumn colFileAliasName;
        private DevExpress.XtraGrid.Columns.GridColumn colMainFileName;
        private DevExpress.XtraEditors.SimpleButton btnSendJob;
        private DevExpress.XtraEditors.SimpleButton btnSelectReceipt;
        private DevExpress.XtraGrid.Columns.GridColumn colCompany;
        private DevExpress.XtraGrid.Columns.GridColumn colSerie;
    }
}
