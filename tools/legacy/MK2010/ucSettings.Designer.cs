namespace _MK2010.Presentation.Settings
{
    partial class ucSettings
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
            this.kscUcSettings = new KabanDevBase.Controls.KabanSplitContainer(this.components);
            this.ktlcLeftMenu = new KabanDevBase.Controls.KabanTreeListControl(this.components);
            this.Menu = new DevExpress.XtraTreeList.Columns.TreeListColumn();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.kscUcSettings)).BeginInit();
            this.kscUcSettings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.ktlcLeftMenu)).BeginInit();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.kscUcSettings);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.Text = "Settings";
            // 
            // kscUcSettings
            // 
            this.kscUcSettings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.kscUcSettings.IsSplitterFixed = true;
            this.kscUcSettings.Location = new System.Drawing.Point(2, 29);
            this.kscUcSettings.LookAndFeel.SkinName = "Office 2019 Colorful";
            this.kscUcSettings.LookAndFeel.TouchScaleFactor = 1.5F;
            this.kscUcSettings.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.kscUcSettings.LookAndFeel.UseDefaultLookAndFeel = false;
            this.kscUcSettings.Name = "kscUcSettings";
            this.kscUcSettings.Panel1.Controls.Add(this.ktlcLeftMenu);
            this.kscUcSettings.Panel1.Text = "Panel1";
            this.kscUcSettings.Panel2.Text = "Panel2";
            this.kscUcSettings.Size = new System.Drawing.Size(796, 569);
            this.kscUcSettings.SplitterPosition = 184;
            this.kscUcSettings.TabIndex = 0;
            // 
            // ktlcLeftMenu
            // 
            this.ktlcLeftMenu.Appearance.FocusedCell.Font = new System.Drawing.Font("Tahoma", 8.25F, System.Drawing.FontStyle.Bold);
            this.ktlcLeftMenu.Appearance.FocusedCell.Options.UseFont = true;
            this.ktlcLeftMenu.Columns.AddRange(new DevExpress.XtraTreeList.Columns.TreeListColumn[] {
            this.Menu});
            this.ktlcLeftMenu.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ktlcLeftMenu.HierarchialPath = "ucSettings";
            this.ktlcLeftMenu.Location = new System.Drawing.Point(0, 0);
            this.ktlcLeftMenu.LookAndFeel.SkinName = "Seven Classic";
            this.ktlcLeftMenu.LookAndFeel.UseDefaultLookAndFeel = false;
            this.ktlcLeftMenu.Name = "ktlcLeftMenu";
            this.ktlcLeftMenu.BeginUnboundLoad();
            this.ktlcLeftMenu.AppendNode(new object[] {
            "Language Settings"}, -1, "ucLanguage");
            this.ktlcLeftMenu.AppendNode(new object[] {
            "User Settings"}, -1, "ucUserSettings");
            this.ktlcLeftMenu.AppendNode(new object[] {
            "Barcode Reader Settings"}, -1, "ucBarcodeReaderSettings");
            this.ktlcLeftMenu.AppendNode(new object[] {
            "Machine Settings"}, -1, "ucMachineSettings");
            this.ktlcLeftMenu.EndUnboundLoad();
            this.ktlcLeftMenu.OptionsBehavior.Editable = false;
            this.ktlcLeftMenu.OptionsPrint.PrintHorzLines = false;
            this.ktlcLeftMenu.OptionsPrint.PrintImages = false;
            this.ktlcLeftMenu.OptionsPrint.PrintPageHeader = false;
            this.ktlcLeftMenu.OptionsPrint.PrintReportFooter = false;
            this.ktlcLeftMenu.OptionsPrint.PrintVertLines = false;
            this.ktlcLeftMenu.OptionsView.ShowColumns = false;
            this.ktlcLeftMenu.OptionsView.ShowFirstLines = false;
            this.ktlcLeftMenu.OptionsView.ShowHorzLines = false;
            this.ktlcLeftMenu.OptionsView.ShowIndicator = false;
            this.ktlcLeftMenu.OptionsView.ShowTreeLines = DevExpress.Utils.DefaultBoolean.False;
            this.ktlcLeftMenu.OptionsView.ShowVertLines = false;
            this.ktlcLeftMenu.PopupMenu = null;
            this.ktlcLeftMenu.Size = new System.Drawing.Size(184, 569);
            this.ktlcLeftMenu.TabIndex = 0;
            // 
            // Menu
            // 
            this.Menu.Caption = "Menu";
            this.Menu.FieldName = "Menu";
            this.Menu.Name = "Menu";
            this.Menu.Visible = true;
            this.Menu.VisibleIndex = 0;
            // 
            // ucSettings
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Name = "ucSettings";
            this.PageTitle = "Settings";
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.kscUcSettings)).EndInit();
            this.kscUcSettings.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.ktlcLeftMenu)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private DevExpress.XtraTreeList.Columns.TreeListColumn Menu;
        public KabanDevBase.Controls.KabanTreeListControl ktlcLeftMenu;
        public KabanDevBase.Controls.KabanSplitContainer kscUcSettings;
    }
}
