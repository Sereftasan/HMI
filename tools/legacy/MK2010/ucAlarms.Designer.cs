namespace _MK2010.Presentation.Alarms
{
    partial class ucAlarms
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
            this.kscAlarmsMain = new KabanDevBase.Controls.KabanSplitContainer(this.components);
            this.KGroupBox_Button = new KabanDevBase.Controls.KabanGroupControl(this.components);
            this.btnReset = new KabanDevBase.Plc_Componets.KabanLambButton();
            this.kscAlarmsMain_Sub = new KabanDevBase.Controls.KabanSplitContainer(this.components);
            this.KGroupBox_Alarms = new KabanDevBase.Controls.KabanGroupControl(this.components);
            this.kabanGridControlAlarms = new KabanDevBase.Controls.KabanGridControl();
            this.kabanGridViewAlarms = new KabanDevBase.Controls.KabanGridView();
            this.colDevice = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colMessage = new DevExpress.XtraGrid.Columns.GridColumn();
            this.KGroupBox_Warnings = new KabanDevBase.Controls.KabanGroupControl(this.components);
            this.kabanGridControlWarnings = new KabanDevBase.Controls.KabanGridControl();
            this.kabanGridViewWarnings = new KabanDevBase.Controls.KabanGridView();
            this.colWarnDevice = new DevExpress.XtraGrid.Columns.GridColumn();
            this.colWarnMessage = new DevExpress.XtraGrid.Columns.GridColumn();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.kscAlarmsMain)).BeginInit();
            this.kscAlarmsMain.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.KGroupBox_Button)).BeginInit();
            this.KGroupBox_Button.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.kscAlarmsMain_Sub)).BeginInit();
            this.kscAlarmsMain_Sub.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.KGroupBox_Alarms)).BeginInit();
            this.KGroupBox_Alarms.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.kabanGridControlAlarms)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.kabanGridViewAlarms)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.KGroupBox_Warnings)).BeginInit();
            this.KGroupBox_Warnings.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.kabanGridControlWarnings)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.kabanGridViewWarnings)).BeginInit();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.kscAlarmsMain);
            this.BoxWrapper.LookAndFeel.SkinName = "Seven Classic";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Text = "Alarms and Warnings";
            // 
            // kscAlarmsMain
            // 
            this.kscAlarmsMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.kscAlarmsMain.Horizontal = false;
            this.kscAlarmsMain.IsSplitterFixed = true;
            this.kscAlarmsMain.Location = new System.Drawing.Point(2, 2);
            this.kscAlarmsMain.LookAndFeel.SkinName = "Office 2019 Colorful";
            this.kscAlarmsMain.LookAndFeel.TouchScaleFactor = 1.5F;
            this.kscAlarmsMain.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.False;
            this.kscAlarmsMain.LookAndFeel.UseDefaultLookAndFeel = false;
            this.kscAlarmsMain.Name = "kscAlarmsMain";
            this.kscAlarmsMain.Panel1.Controls.Add(this.KGroupBox_Button);
            this.kscAlarmsMain.Panel1.Text = "Panel1";
            this.kscAlarmsMain.Panel2.Controls.Add(this.kscAlarmsMain_Sub);
            this.kscAlarmsMain.Panel2.Text = "Panel2";
            this.kscAlarmsMain.Size = new System.Drawing.Size(796, 596);
            this.kscAlarmsMain.SplitterPosition = 68;
            this.kscAlarmsMain.TabIndex = 3;
            // 
            // KGroupBox_Button
            // 
            this.KGroupBox_Button.Controls.Add(this.btnReset);
            this.KGroupBox_Button.Dock = System.Windows.Forms.DockStyle.Fill;
            this.KGroupBox_Button.HierarchialPath = null;
            this.KGroupBox_Button.Location = new System.Drawing.Point(0, 0);
            this.KGroupBox_Button.LookAndFeel.SkinName = "Office 2013 Light Gray";
            this.KGroupBox_Button.LookAndFeel.TouchScaleFactor = 1.5F;
            this.KGroupBox_Button.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.KGroupBox_Button.LookAndFeel.UseDefaultLookAndFeel = false;
            this.KGroupBox_Button.Name = "KGroupBox_Button";
            this.KGroupBox_Button.Size = new System.Drawing.Size(796, 68);
            this.KGroupBox_Button.TabIndex = 10;
            // 
            // btnReset
            // 
            this.btnReset.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnReset.ButtonTag = null;
            this.btnReset.ButtonText = "Reset";
            this.btnReset.ButtonType = KabanMachinesSystem.Common.Enums.ButtonType.Momentary;
            this.btnReset.DefaultValue = "0";
            this.btnReset.Device = "M36";
            this.btnReset.IsHeadControlButton = true;
            this.btnReset.IsReferanceButton = false;
            this.btnReset.IsScrewingControl = false;
            this.btnReset.IsSecurityButton = false;
            this.btnReset.LambAlignment = System.Windows.Forms.HorizontalAlignment.Right;
            this.btnReset.LambDevice = "M36";
            this.btnReset.LambState = KabanMachinesSystem.Common.Enums.LambState.Off;
            this.btnReset.Location = new System.Drawing.Point(724, 15);
            this.btnReset.Name = "btnReset";
            this.btnReset.PlcType = KabanMachinesSystem.Common.Enums.PlcType.MainCPU;
            this.btnReset.ResetDevice = null;
            this.btnReset.Size = new System.Drawing.Size(67, 49);
            this.btnReset.TabIndex = 9;
            this.btnReset.Type = KabanMachinesSystem.Common.Enums.TypeEnum.Integer;
            // 
            // kscAlarmsMain_Sub
            // 
            this.kscAlarmsMain_Sub.Dock = System.Windows.Forms.DockStyle.Fill;
            this.kscAlarmsMain_Sub.FixedPanel = DevExpress.XtraEditors.SplitFixedPanel.None;
            this.kscAlarmsMain_Sub.Location = new System.Drawing.Point(0, 0);
            this.kscAlarmsMain_Sub.LookAndFeel.SkinName = "Office 2019 Colorful";
            this.kscAlarmsMain_Sub.LookAndFeel.TouchScaleFactor = 1.5F;
            this.kscAlarmsMain_Sub.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.False;
            this.kscAlarmsMain_Sub.LookAndFeel.UseDefaultLookAndFeel = false;
            this.kscAlarmsMain_Sub.Name = "kscAlarmsMain_Sub";
            this.kscAlarmsMain_Sub.Panel1.Controls.Add(this.KGroupBox_Alarms);
            this.kscAlarmsMain_Sub.Panel1.Text = "Panel1";
            this.kscAlarmsMain_Sub.Panel2.Controls.Add(this.KGroupBox_Warnings);
            this.kscAlarmsMain_Sub.Panel2.Text = "Panel2";
            this.kscAlarmsMain_Sub.Size = new System.Drawing.Size(796, 518);
            this.kscAlarmsMain_Sub.SplitterPosition = 382;
            this.kscAlarmsMain_Sub.TabIndex = 3;
            // 
            // KGroupBox_Alarms
            // 
            this.KGroupBox_Alarms.Controls.Add(this.kabanGridControlAlarms);
            this.KGroupBox_Alarms.Dock = System.Windows.Forms.DockStyle.Fill;
            this.KGroupBox_Alarms.HierarchialPath = null;
            this.KGroupBox_Alarms.Location = new System.Drawing.Point(0, 0);
            this.KGroupBox_Alarms.LookAndFeel.SkinName = "Office 2013 Light Gray";
            this.KGroupBox_Alarms.LookAndFeel.TouchScaleFactor = 1.5F;
            this.KGroupBox_Alarms.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.KGroupBox_Alarms.LookAndFeel.UseDefaultLookAndFeel = false;
            this.KGroupBox_Alarms.Name = "KGroupBox_Alarms";
            this.KGroupBox_Alarms.Size = new System.Drawing.Size(382, 518);
            this.KGroupBox_Alarms.TabIndex = 0;
            this.KGroupBox_Alarms.Text = "Alarm List";
            // 
            // kabanGridControlAlarms
            // 
            this.kabanGridControlAlarms.Dock = System.Windows.Forms.DockStyle.Fill;
            this.kabanGridControlAlarms.HierarchialPath = null;
            this.kabanGridControlAlarms.Location = new System.Drawing.Point(2, 21);
            this.kabanGridControlAlarms.MainView = this.kabanGridViewAlarms;
            this.kabanGridControlAlarms.Name = "kabanGridControlAlarms";
            this.kabanGridControlAlarms.PopupMenu = null;
            this.kabanGridControlAlarms.Size = new System.Drawing.Size(378, 495);
            this.kabanGridControlAlarms.TabIndex = 0;
            this.kabanGridControlAlarms.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.kabanGridViewAlarms});
            // 
            // kabanGridViewAlarms
            // 
            this.kabanGridViewAlarms.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.colDevice,
            this.colMessage});
            this.kabanGridViewAlarms.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
            this.kabanGridViewAlarms.GridControl = this.kabanGridControlAlarms;
            this.kabanGridViewAlarms.Name = "kabanGridViewAlarms";
            this.kabanGridViewAlarms.OptionsBehavior.Editable = false;
            this.kabanGridViewAlarms.OptionsView.ShowGroupPanel = false;
            // 
            // colDevice
            // 
            this.colDevice.Caption = "Device";
            this.colDevice.FieldName = "Device";
            this.colDevice.Name = "colDevice";
            this.colDevice.Visible = true;
            this.colDevice.VisibleIndex = 0;
            this.colDevice.Width = 91;
            // 
            // colMessage
            // 
            this.colMessage.Caption = "Message";
            this.colMessage.FieldName = "Message";
            this.colMessage.Name = "colMessage";
            this.colMessage.Visible = true;
            this.colMessage.VisibleIndex = 1;
            this.colMessage.Width = 268;
            // 
            // KGroupBox_Warnings
            // 
            this.KGroupBox_Warnings.Controls.Add(this.kabanGridControlWarnings);
            this.KGroupBox_Warnings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.KGroupBox_Warnings.HierarchialPath = null;
            this.KGroupBox_Warnings.Location = new System.Drawing.Point(0, 0);
            this.KGroupBox_Warnings.LookAndFeel.SkinName = "Office 2013 Light Gray";
            this.KGroupBox_Warnings.LookAndFeel.TouchScaleFactor = 1.5F;
            this.KGroupBox_Warnings.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.KGroupBox_Warnings.LookAndFeel.UseDefaultLookAndFeel = false;
            this.KGroupBox_Warnings.Name = "KGroupBox_Warnings";
            this.KGroupBox_Warnings.Size = new System.Drawing.Size(404, 518);
            this.KGroupBox_Warnings.TabIndex = 1;
            this.KGroupBox_Warnings.Text = "Warning List";
            // 
            // kabanGridControlWarnings
            // 
            this.kabanGridControlWarnings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.kabanGridControlWarnings.HierarchialPath = null;
            this.kabanGridControlWarnings.Location = new System.Drawing.Point(2, 21);
            this.kabanGridControlWarnings.MainView = this.kabanGridViewWarnings;
            this.kabanGridControlWarnings.Name = "kabanGridControlWarnings";
            this.kabanGridControlWarnings.PopupMenu = null;
            this.kabanGridControlWarnings.Size = new System.Drawing.Size(400, 495);
            this.kabanGridControlWarnings.TabIndex = 0;
            this.kabanGridControlWarnings.ViewCollection.AddRange(new DevExpress.XtraGrid.Views.Base.BaseView[] {
            this.kabanGridViewWarnings});
            // 
            // kabanGridViewWarnings
            // 
            this.kabanGridViewWarnings.Columns.AddRange(new DevExpress.XtraGrid.Columns.GridColumn[] {
            this.colWarnDevice,
            this.colWarnMessage});
            this.kabanGridViewWarnings.FocusRectStyle = DevExpress.XtraGrid.Views.Grid.DrawFocusRectStyle.RowFocus;
            this.kabanGridViewWarnings.GridControl = this.kabanGridControlWarnings;
            this.kabanGridViewWarnings.Name = "kabanGridViewWarnings";
            this.kabanGridViewWarnings.OptionsBehavior.Editable = false;
            this.kabanGridViewWarnings.OptionsView.ShowGroupPanel = false;
            // 
            // colWarnDevice
            // 
            this.colWarnDevice.Caption = "Device";
            this.colWarnDevice.FieldName = "Device";
            this.colWarnDevice.Name = "colWarnDevice";
            this.colWarnDevice.Visible = true;
            this.colWarnDevice.VisibleIndex = 0;
            this.colWarnDevice.Width = 98;
            // 
            // colWarnMessage
            // 
            this.colWarnMessage.Caption = "Message";
            this.colWarnMessage.FieldName = "Message";
            this.colWarnMessage.Name = "colWarnMessage";
            this.colWarnMessage.Visible = true;
            this.colWarnMessage.VisibleIndex = 1;
            this.colWarnMessage.Width = 283;
            // 
            // ucAlarms
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "ucAlarms";
            this.PageTitle = "Alarms and Warnings";
            this.ShowCaption = false;
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.kscAlarmsMain)).EndInit();
            this.kscAlarmsMain.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.KGroupBox_Button)).EndInit();
            this.KGroupBox_Button.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.kscAlarmsMain_Sub)).EndInit();
            this.kscAlarmsMain_Sub.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.KGroupBox_Alarms)).EndInit();
            this.KGroupBox_Alarms.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.kabanGridControlAlarms)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.kabanGridViewAlarms)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.KGroupBox_Warnings)).EndInit();
            this.KGroupBox_Warnings.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.kabanGridControlWarnings)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.kabanGridViewWarnings)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private KabanDevBase.Controls.KabanSplitContainer kscAlarmsMain;
        private KabanDevBase.Controls.KabanSplitContainer kscAlarmsMain_Sub;
        private KabanDevBase.Controls.KabanGroupControl KGroupBox_Alarms;
        private KabanDevBase.Controls.KabanGridControl kabanGridControlAlarms;
        private KabanDevBase.Controls.KabanGridView kabanGridViewAlarms;
        private KabanDevBase.Controls.KabanGroupControl KGroupBox_Warnings;
        private KabanDevBase.Controls.KabanGridControl kabanGridControlWarnings;
        private KabanDevBase.Controls.KabanGridView kabanGridViewWarnings;
        private DevExpress.XtraGrid.Columns.GridColumn colDevice;
        private DevExpress.XtraGrid.Columns.GridColumn colMessage;
        private DevExpress.XtraGrid.Columns.GridColumn colWarnDevice;
        private DevExpress.XtraGrid.Columns.GridColumn colWarnMessage;
        private KabanDevBase.Plc_Componets.KabanLambButton btnReset;
        private KabanDevBase.Controls.KabanGroupControl KGroupBox_Button;
    }
}
