namespace Kaban.Machines.H500.Screens
{
    partial class HomeScreen
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        // ===================================================================
        // H500's HOME (12 October: "take just Start, Pause, Cancel buttons and speed
        // settings from home pages") - the old H500 FX5 main control panel:
        //   Start   M2 while held, only while the machine is ready (M1); the job-
        //           finished flag M60 cleared first (HomeScreen.cs); lit while the
        //           machine works (M0). The old Start sent the job list first; the
        //           job list is not on this page.
        //   Pause   M4 alternate, lit while on.
        //   Cancel  M3 momentary.
        // The old H500 home has no speed setting.
        // ===================================================================
        private void InitializeComponent()
        {
            Kaban.Core.Models.BitAction bitAction1 = new Kaban.Core.Models.BitAction();
            Kaban.Core.Models.BitAction bitAction2 = new Kaban.Core.Models.BitAction();
            Kaban.Core.Models.BitAction bitAction3 = new Kaban.Core.Models.BitAction();
            Kaban.Core.Models.PlcTrigger plcTrigger1 = new Kaban.Core.Models.PlcTrigger();
            this.btnStart = new Kaban.UI.Controls.ButtonHMI();
            this.btnPause = new Kaban.UI.Controls.ButtonHMI();
            this.btnCancel = new Kaban.UI.Controls.ButtonHMI();
            this.SuspendLayout();
            // 
            // btnStart
            // 
            this.btnStart.Name = "btnStart";
            this.btnStart.PlcNumber = 1;
            this.btnStart.TranslationKey = "Btn_HomeStart";
            this.btnStart.FallbackText = "Start";
            bitAction1.Address = "M2";
            bitAction1.Action = Kaban.Core.Enums.BitActionType.Momentary;
            this.btnStart.BitActions.Add(bitAction1);
            plcTrigger1.TriggerType = Kaban.Core.Enums.TriggerType.ON;
            plcTrigger1.MonitorAddress = "M1";
            plcTrigger1.DescriptionKey = "Machine ready to work (M1)";
            this.btnStart.EnableTrigger = plcTrigger1;
            this.btnStart.Lamp1Address = "M0";
            this.btnStart.Location = new System.Drawing.Point(16, 24);
            this.btnStart.Size = new System.Drawing.Size(130, 56);
            this.btnStart.LogAction = true;
            this.btnStart.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            // 
            // btnPause
            // 
            this.btnPause.Name = "btnPause";
            this.btnPause.PlcNumber = 1;
            this.btnPause.TranslationKey = "Btn_HomePause";
            this.btnPause.FallbackText = "Pause";
            bitAction2.Address = "M4";
            bitAction2.Action = Kaban.Core.Enums.BitActionType.Alternate;
            this.btnPause.BitActions.Add(bitAction2);
            this.btnPause.Lamp1Address = "M4";
            this.btnPause.Location = new System.Drawing.Point(156, 24);
            this.btnPause.Size = new System.Drawing.Size(130, 56);
            this.btnPause.LogAction = true;
            this.btnPause.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            // 
            // btnCancel
            // 
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.PlcNumber = 1;
            this.btnCancel.TranslationKey = "Btn_Cancel";
            this.btnCancel.FallbackText = "Cancel";
            bitAction3.Address = "M3";
            bitAction3.Action = Kaban.Core.Enums.BitActionType.Momentary;
            this.btnCancel.BitActions.Add(bitAction3);
            this.btnCancel.Location = new System.Drawing.Point(296, 24);
            this.btnCancel.Size = new System.Drawing.Size(130, 56);
            this.btnCancel.LogAction = true;
            this.btnCancel.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            // 
            // HomeScreen
            // 
            // AutoScaleMode.None, as the generated pages: the host scales the page (the screen scale).
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.White;
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.btnPause);
            this.Controls.Add(this.btnCancel);
            this.Name = "HomeScreen";
            this.Size = new System.Drawing.Size(460, 120);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private Kaban.UI.Controls.ButtonHMI btnStart;
        private Kaban.UI.Controls.ButtonHMI btnPause;
        private Kaban.UI.Controls.ButtonHMI btnCancel;
    }
}
