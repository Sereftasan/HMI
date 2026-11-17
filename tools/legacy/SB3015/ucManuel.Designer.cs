namespace _SB3015.Presentation.Home.Manuel
{
    partial class ucManuel
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
            DevExpress.XtraEditors.Controls.EditorButtonImageOptions editorButtonImageOptions1 = new DevExpress.XtraEditors.Controls.EditorButtonImageOptions();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ucManuel));
            DevExpress.Utils.SerializableAppearanceObject serializableAppearanceObject1 = new DevExpress.Utils.SerializableAppearanceObject();
            this.tpManuelMain = new KabanDevBase.Controls.KabanTabPageControl(this.components);
            this.xtpEntryConveyor = new DevExpress.XtraTab.XtraTabPage();
            this.xtpRobot = new DevExpress.XtraTab.XtraTabPage();
            this.xtpProfileRecognition = new DevExpress.XtraTab.XtraTabPage();
            this.xtpMilling = new DevExpress.XtraTab.XtraTabPage();
            this.xtpCutting = new DevExpress.XtraTab.XtraTabPage();
            this.xtpExit = new DevExpress.XtraTab.XtraTabPage();
            this.xtpOther = new DevExpress.XtraTab.XtraTabPage();
            this.xtpServoStatus = new DevExpress.XtraTab.XtraTabPage();
            this.xtpClampingMoulds = new DevExpress.XtraTab.XtraTabPage();
            this.xtpYZCrossRollers = new DevExpress.XtraTab.XtraTabPage();
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).BeginInit();
            this.BoxWrapper.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.tpManuelMain)).BeginInit();
            this.tpManuelMain.SuspendLayout();
            this.SuspendLayout();
            // 
            // BoxWrapper
            // 
            this.BoxWrapper.AppearanceCaption.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(162)));
            this.BoxWrapper.AppearanceCaption.Options.UseFont = true;
            this.BoxWrapper.CaptionImageOptions.ImageUri.Uri = "Copy;Size16x16";
            this.BoxWrapper.Controls.Add(this.tpManuelMain);
            this.BoxWrapper.LookAndFeel.SkinName = "The Bezier";
            this.BoxWrapper.LookAndFeel.TouchScaleFactor = 1.5F;
            this.BoxWrapper.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.BoxWrapper.LookAndFeel.UseDefaultLookAndFeel = false;
            this.BoxWrapper.ShowCaption = false;
            this.BoxWrapper.Text = "Manuel Control";
            // 
            // tpManuelMain
            // 
            this.tpManuelMain.Appearance.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(241)))), ((int)(((byte)(241)))), ((int)(((byte)(241)))));
            this.tpManuelMain.Appearance.Options.UseBackColor = true;
            editorButtonImageOptions1.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("editorButtonImageOptions1.SvgImage")));
            editorButtonImageOptions1.SvgImageSize = new System.Drawing.Size(24, 24);
            this.tpManuelMain.CustomHeaderButtons.AddRange(new DevExpress.XtraTab.Buttons.CustomHeaderButton[] {
            new DevExpress.XtraTab.Buttons.CustomHeaderButton(DevExpress.XtraEditors.Controls.ButtonPredefines.Glyph, "Receipts", -1, true, false, editorButtonImageOptions1, serializableAppearanceObject1, "Receipts", null, null)});
            this.tpManuelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tpManuelMain.HierarchialPath = "ucManuel";
            this.tpManuelMain.Location = new System.Drawing.Point(2, 2);
            this.tpManuelMain.Name = "tpManuelMain";
            this.tpManuelMain.SelectedTabPage = this.xtpEntryConveyor;
            this.tpManuelMain.Size = new System.Drawing.Size(796, 596);
            this.tpManuelMain.TabIndex = 2;
            this.tpManuelMain.TabPages.AddRange(new DevExpress.XtraTab.XtraTabPage[] {
            this.xtpEntryConveyor,
            this.xtpRobot,
            this.xtpProfileRecognition,
            this.xtpMilling,
            this.xtpCutting,
            this.xtpExit,
            this.xtpOther,
            this.xtpServoStatus,
            this.xtpClampingMoulds,
            this.xtpYZCrossRollers});
            // 
            // xtpEntryConveyor
            // 
            this.xtpEntryConveyor.Name = "xtpEntryConveyor";
            this.xtpEntryConveyor.Size = new System.Drawing.Size(794, 561);
            this.xtpEntryConveyor.Tag = "ucEntryConveyor";
            this.xtpEntryConveyor.Text = "Entry Conveyor";
            // 
            // xtpRobot
            // 
            this.xtpRobot.Name = "xtpRobot";
            this.xtpRobot.Size = new System.Drawing.Size(794, 561);
            this.xtpRobot.Tag = "ucRobot";
            this.xtpRobot.Text = "Robot";
            // 
            // xtpProfileRecognition
            // 
            this.xtpProfileRecognition.Name = "xtpProfileRecognition";
            this.xtpProfileRecognition.Size = new System.Drawing.Size(794, 561);
            this.xtpProfileRecognition.Tag = "ucProfileRecognition";
            this.xtpProfileRecognition.Text = "Profile Recognition";
            // 
            // xtpMilling
            // 
            this.xtpMilling.Name = "xtpMilling";
            this.xtpMilling.Size = new System.Drawing.Size(794, 561);
            this.xtpMilling.Tag = "ucMilling";
            this.xtpMilling.Text = "Milling";
            // 
            // xtpCutting
            // 
            this.xtpCutting.Name = "xtpCutting";
            this.xtpCutting.Size = new System.Drawing.Size(794, 561);
            this.xtpCutting.Tag = "ucCutting";
            this.xtpCutting.Text = "Cutting";
            // 
            // xtpExit
            // 
            this.xtpExit.Name = "xtpExit";
            this.xtpExit.Size = new System.Drawing.Size(794, 561);
            this.xtpExit.Tag = "ucExit";
            this.xtpExit.Text = "Exit";
            // 
            // xtpOther
            // 
            this.xtpOther.Name = "xtpOther";
            this.xtpOther.Size = new System.Drawing.Size(794, 561);
            this.xtpOther.Tag = "ucOther";
            this.xtpOther.Text = "Other";
            // 
            // xtpServoStatus
            // 
            this.xtpServoStatus.Name = "xtpServoStatus";
            this.xtpServoStatus.Size = new System.Drawing.Size(794, 561);
            this.xtpServoStatus.Tag = "ucServoStatus";
            this.xtpServoStatus.Text = "Servo Status";
            // 
            // xtpClampingMoulds
            // 
            this.xtpClampingMoulds.Name = "xtpClampingMoulds";
            this.xtpClampingMoulds.PageVisible = false;
            this.xtpClampingMoulds.Size = new System.Drawing.Size(794, 561);
            this.xtpClampingMoulds.Tag = "ucClampingMoulds";
            this.xtpClampingMoulds.Text = "Clamping Moulds";
            // 
            // xtpYZCrossRollers
            // 
            this.xtpYZCrossRollers.Name = "xtpYZCrossRollers";
            this.xtpYZCrossRollers.PageVisible = false;
            this.xtpYZCrossRollers.Size = new System.Drawing.Size(794, 561);
            this.xtpYZCrossRollers.Tag = "ucYZCrossRollers";
            this.xtpYZCrossRollers.Text = "YZ Cross Rollers";
            // 
            // ucManuel
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.LookAndFeel.SkinName = "The Bezier";
            this.LookAndFeel.TouchScaleFactor = 1.5F;
            this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
            this.LookAndFeel.UseDefaultLookAndFeel = false;
            this.Name = "ucManuel";
            this.PageTitle = "Manuel Control";
            this.ShowCaption = false;
            ((System.ComponentModel.ISupportInitialize)(this.BoxWrapper)).EndInit();
            this.BoxWrapper.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.tpManuelMain)).EndInit();
            this.tpManuelMain.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private KabanDevBase.Controls.KabanTabPageControl tpManuelMain;
        private DevExpress.XtraTab.XtraTabPage xtpEntryConveyor;
        private DevExpress.XtraTab.XtraTabPage xtpRobot;
        private DevExpress.XtraTab.XtraTabPage xtpProfileRecognition;
        private DevExpress.XtraTab.XtraTabPage xtpMilling;
        private DevExpress.XtraTab.XtraTabPage xtpCutting;
        private DevExpress.XtraTab.XtraTabPage xtpExit;
        private DevExpress.XtraTab.XtraTabPage xtpOther;
        private DevExpress.XtraTab.XtraTabPage xtpServoStatus;
        private DevExpress.XtraTab.XtraTabPage xtpClampingMoulds;
        private DevExpress.XtraTab.XtraTabPage xtpYZCrossRollers;
    }
}
