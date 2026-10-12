namespace Kaban.Machines.FA2010.Screens
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
        // FA2010's HOME (12 October: "upgrade FA2010 home page from _FA2010 ... take
        // just Start, Pause, Cancel buttons and speed settings") - the old program's
        // main control panel (tools/legacy/FA2010/ucMainControlPanel.Designer.cs):
        //   Start   M21 SET - the machine is working (Constants.MachineIsWorking_Device),
        //           lit while it is on. The old Start sent the job list's bar first;
        //           the job list is not on this page.
        //   Pause   M111 alternate, lit while on.
        //   Cancel  M24 SET (Constants.MachineCancel_Device) - asked first while the
        //           machine works (M21), at once when it does not.
        //   Speed   D408, the working speed in percent, in the old track bar's steps.
        // ===================================================================
        private void InitializeComponent()
        {
            Kaban.Core.Models.BitAction bitAction1 = new Kaban.Core.Models.BitAction();
            Kaban.Core.Models.BitAction bitAction2 = new Kaban.Core.Models.BitAction();
            Kaban.Core.Models.BitAction bitAction3 = new Kaban.Core.Models.BitAction();
            this.btnStart = new Kaban.UI.Controls.ButtonHMI();
            this.btnPause = new Kaban.UI.Controls.ButtonHMI();
            this.btnCancel = new Kaban.UI.Controls.ButtonHMI();
            this.speedOverride = new Kaban.UI.Controls.SpeedOverrideHMI();
            this.SuspendLayout();
            //
            // btnStart
            //
            this.btnStart.Name = "btnStart";
            this.btnStart.PlcNumber = 1;
            this.btnStart.TranslationKey = "Btn_HomeStart";
            this.btnStart.FallbackText = "Start";
            bitAction1.Address = "M21";
            bitAction1.Action = Kaban.Core.Enums.BitActionType.SET;
            this.btnStart.BitActions.Add(bitAction1);
            this.btnStart.Lamp1Address = "M21";
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
            bitAction2.Address = "M111";
            bitAction2.Action = Kaban.Core.Enums.BitActionType.Alternate;
            this.btnPause.BitActions.Add(bitAction2);
            this.btnPause.Lamp1Address = "M111";
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
            bitAction3.Address = "M24";
            bitAction3.Action = Kaban.Core.Enums.BitActionType.SET;
            this.btnCancel.BitActions.Add(bitAction3);
            this.btnCancel.ConfirmationAddress = "M21";
            this.btnCancel.ConfirmationTextKey = "Msg_CancelOperation";
            this.btnCancel.ConfirmationText = "The machine is working. Cancel the operation?";
            this.btnCancel.Location = new System.Drawing.Point(296, 24);
            this.btnCancel.Size = new System.Drawing.Size(130, 56);
            this.btnCancel.LogAction = true;
            this.btnCancel.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            //
            // speedOverride
            //
            this.speedOverride.Name = "speedOverride";
            this.speedOverride.PlcNumber = 1;
            this.speedOverride.Address = "D408";
            this.speedOverride.Location = new System.Drawing.Point(456, 24);
            this.speedOverride.Size = new System.Drawing.Size(340, 56);
            this.speedOverride.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            //
            // HomeScreen
            //
            // AutoScaleMode.None, as the generated pages: the host scales the page (the screen scale).
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.White;
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.btnPause);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.speedOverride);
            this.Name = "HomeScreen";
            this.Size = new System.Drawing.Size(820, 120);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private Kaban.UI.Controls.ButtonHMI btnStart;
        private Kaban.UI.Controls.ButtonHMI btnPause;
        private Kaban.UI.Controls.ButtonHMI btnCancel;
        private Kaban.UI.Controls.SpeedOverrideHMI speedOverride;
    }
}
