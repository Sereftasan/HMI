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
            Kaban.Core.Models.BitAction bitAction22 = new Kaban.Core.Models.BitAction();
            Kaban.Core.Models.BitAction bitAction23 = new Kaban.Core.Models.BitAction();
            Kaban.Core.Models.BitAction bitAction24 = new Kaban.Core.Models.BitAction();
            this.btnStart = new Kaban.UI.Controls.ButtonHMI();
            this.btnPause = new Kaban.UI.Controls.ButtonHMI();
            this.btnCancel = new Kaban.UI.Controls.ButtonHMI();
            this.speedOverride = new Kaban.UI.Controls.SpeedOverrideHMI();
            this.SuspendLayout();
            // 
            // btnStart
            // 
            this.btnStart.ActiveBackColor = System.Drawing.Color.White;
            this.btnStart.AlertBlink = false;
            this.btnStart.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            bitAction22.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction22.Address = "M21";
            bitAction22.AddressOffset.StaticValue = 0D;
            bitAction22.AddressOffset.TagAddress = "";
            bitAction22.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.btnStart.BitActions.Add(bitAction22);
            this.btnStart.BlinkCondition = Kaban.Core.Enums.SystemCondition.None;
            this.btnStart.ButtonText = "Start";
            this.btnStart.ColorActive = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.btnStart.ColorDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(250)))));
            this.btnStart.ColorPassive = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.btnStart.ConfirmationAddress = "";
            this.btnStart.ConfirmationNeeded = false;
            this.btnStart.ConfirmationText = "";
            this.btnStart.ConfirmationTextKey = "";
            this.btnStart.CornerRadius = 4;
            this.btnStart.DescriptionText = "";
            this.btnStart.Direction = Kaban.UI.Controls.ButtonGlyphs.Direction.None;
            this.btnStart.DisplayAddress = null;
            this.btnStart.DisplayOffset.StaticValue = 0D;
            this.btnStart.DisplayOffset.TagAddress = "";
            this.btnStart.DisplayOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.btnStart.EnableConditions = Kaban.Core.Enums.SystemCondition.None;
            this.btnStart.EnableTrigger.DescriptionKey = "";
            this.btnStart.EnableTrigger.MonitorAddress = "";
            this.btnStart.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.btnStart.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.btnStart.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.btnStart.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.btnStart.EnableTrigger.ValA = 0D;
            this.btnStart.EnableTrigger.ValB = 0D;
            this.btnStart.ErrorAddress = "";
            this.btnStart.FallbackText = "Start";
            this.btnStart.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.btnStart.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.btnStart.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnStart.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.btnStart.ForeColor = System.Drawing.Color.Black;
            this.btnStart.ForeDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(182)))), ((int)(((byte)(190)))));
            this.btnStart.HoverBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(120)))), ((int)(((byte)(220)))));
            this.btnStart.ImageActive = null;
            this.btnStart.ImagePassive = null;
            this.btnStart.IsActivePage = false;
            this.btnStart.Lamp1Address = "M21";
            this.btnStart.Lamp1Tip = "";
            this.btnStart.Lamp2Address = "";
            this.btnStart.Lamp2Tip = "";
            this.btnStart.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.btnStart.Location = new System.Drawing.Point(403, 654);
            this.btnStart.LogAction = true;
            this.btnStart.Name = "btnStart";
            this.btnStart.OutputAddress = "";
            this.btnStart.OutputBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(170)))), ((int)(((byte)(70)))));
            this.btnStart.PlcNumber = 1;
            this.btnStart.RelayAddress = "";
            this.btnStart.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.btnStart.RestingBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(176)))), ((int)(((byte)(186)))));
            this.btnStart.RoundLeftOnly = false;
            this.btnStart.RoundRightOnly = false;
            this.btnStart.ShowCaption = true;
            this.btnStart.Size = new System.Drawing.Size(130, 56);
            this.btnStart.TabIndex = 0;
            this.btnStart.ToolTipText = "";
            this.btnStart.TranslationKey = "Btn_HomeStart";
            this.btnStart.UseVisualStyleBackColor = false;
            this.btnStart.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            // 
            // btnPause
            // 
            this.btnPause.ActiveBackColor = System.Drawing.Color.White;
            this.btnPause.AlertBlink = false;
            this.btnPause.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            bitAction23.Action = Kaban.Core.Enums.BitActionType.Alternate;
            bitAction23.Address = "M111";
            bitAction23.AddressOffset.StaticValue = 0D;
            bitAction23.AddressOffset.TagAddress = "";
            bitAction23.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.btnPause.BitActions.Add(bitAction23);
            this.btnPause.BlinkCondition = Kaban.Core.Enums.SystemCondition.None;
            this.btnPause.ButtonText = "Pause";
            this.btnPause.ColorActive = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.btnPause.ColorDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(250)))));
            this.btnPause.ColorPassive = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.btnPause.ConfirmationAddress = "";
            this.btnPause.ConfirmationNeeded = false;
            this.btnPause.ConfirmationText = "";
            this.btnPause.ConfirmationTextKey = "";
            this.btnPause.CornerRadius = 4;
            this.btnPause.DescriptionText = "";
            this.btnPause.Direction = Kaban.UI.Controls.ButtonGlyphs.Direction.None;
            this.btnPause.DisplayAddress = null;
            this.btnPause.DisplayOffset.StaticValue = 0D;
            this.btnPause.DisplayOffset.TagAddress = "";
            this.btnPause.DisplayOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.btnPause.EnableConditions = Kaban.Core.Enums.SystemCondition.None;
            this.btnPause.EnableTrigger.DescriptionKey = "";
            this.btnPause.EnableTrigger.MonitorAddress = "";
            this.btnPause.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.btnPause.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.btnPause.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.btnPause.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.btnPause.EnableTrigger.ValA = 0D;
            this.btnPause.EnableTrigger.ValB = 0D;
            this.btnPause.ErrorAddress = "";
            this.btnPause.FallbackText = "Pause";
            this.btnPause.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.btnPause.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.btnPause.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnPause.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.btnPause.ForeColor = System.Drawing.Color.Black;
            this.btnPause.ForeDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(182)))), ((int)(((byte)(190)))));
            this.btnPause.HoverBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(120)))), ((int)(((byte)(220)))));
            this.btnPause.ImageActive = null;
            this.btnPause.ImagePassive = null;
            this.btnPause.IsActivePage = false;
            this.btnPause.Lamp1Address = "M111";
            this.btnPause.Lamp1Tip = "";
            this.btnPause.Lamp2Address = "";
            this.btnPause.Lamp2Tip = "";
            this.btnPause.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.btnPause.Location = new System.Drawing.Point(543, 654);
            this.btnPause.LogAction = true;
            this.btnPause.Name = "btnPause";
            this.btnPause.OutputAddress = "";
            this.btnPause.OutputBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(170)))), ((int)(((byte)(70)))));
            this.btnPause.PlcNumber = 1;
            this.btnPause.RelayAddress = "";
            this.btnPause.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.btnPause.RestingBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(176)))), ((int)(((byte)(186)))));
            this.btnPause.RoundLeftOnly = false;
            this.btnPause.RoundRightOnly = false;
            this.btnPause.ShowCaption = true;
            this.btnPause.Size = new System.Drawing.Size(130, 56);
            this.btnPause.TabIndex = 1;
            this.btnPause.ToolTipText = "";
            this.btnPause.TranslationKey = "Btn_HomePause";
            this.btnPause.UseVisualStyleBackColor = false;
            this.btnPause.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            // 
            // btnCancel
            // 
            this.btnCancel.ActiveBackColor = System.Drawing.Color.White;
            this.btnCancel.AlertBlink = false;
            this.btnCancel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            bitAction24.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction24.Address = "M24";
            bitAction24.AddressOffset.StaticValue = 0D;
            bitAction24.AddressOffset.TagAddress = "";
            bitAction24.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.btnCancel.BitActions.Add(bitAction24);
            this.btnCancel.BlinkCondition = Kaban.Core.Enums.SystemCondition.None;
            this.btnCancel.ButtonText = "Cancel";
            this.btnCancel.ColorActive = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.btnCancel.ColorDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(250)))));
            this.btnCancel.ColorPassive = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.btnCancel.ConfirmationAddress = "M21";
            this.btnCancel.ConfirmationNeeded = false;
            this.btnCancel.ConfirmationText = "The machine is working. Cancel the operation?";
            this.btnCancel.ConfirmationTextKey = "Msg_CancelOperation";
            this.btnCancel.CornerRadius = 4;
            this.btnCancel.DescriptionText = "";
            this.btnCancel.Direction = Kaban.UI.Controls.ButtonGlyphs.Direction.None;
            this.btnCancel.DisplayAddress = null;
            this.btnCancel.DisplayOffset.StaticValue = 0D;
            this.btnCancel.DisplayOffset.TagAddress = "";
            this.btnCancel.DisplayOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.btnCancel.EnableConditions = Kaban.Core.Enums.SystemCondition.None;
            this.btnCancel.EnableTrigger.DescriptionKey = "";
            this.btnCancel.EnableTrigger.MonitorAddress = "";
            this.btnCancel.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.btnCancel.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.btnCancel.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.btnCancel.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.btnCancel.EnableTrigger.ValA = 0D;
            this.btnCancel.EnableTrigger.ValB = 0D;
            this.btnCancel.ErrorAddress = "";
            this.btnCancel.FallbackText = "Cancel";
            this.btnCancel.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.btnCancel.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCancel.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.btnCancel.ForeColor = System.Drawing.Color.Black;
            this.btnCancel.ForeDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(182)))), ((int)(((byte)(190)))));
            this.btnCancel.HoverBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(120)))), ((int)(((byte)(220)))));
            this.btnCancel.ImageActive = null;
            this.btnCancel.ImagePassive = null;
            this.btnCancel.IsActivePage = false;
            this.btnCancel.Lamp1Address = "";
            this.btnCancel.Lamp1Tip = "";
            this.btnCancel.Lamp2Address = "";
            this.btnCancel.Lamp2Tip = "";
            this.btnCancel.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.btnCancel.Location = new System.Drawing.Point(683, 654);
            this.btnCancel.LogAction = true;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.OutputAddress = "";
            this.btnCancel.OutputBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(170)))), ((int)(((byte)(70)))));
            this.btnCancel.PlcNumber = 1;
            this.btnCancel.RelayAddress = "";
            this.btnCancel.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.btnCancel.RestingBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(176)))), ((int)(((byte)(186)))));
            this.btnCancel.RoundLeftOnly = false;
            this.btnCancel.RoundRightOnly = false;
            this.btnCancel.ShowCaption = true;
            this.btnCancel.Size = new System.Drawing.Size(130, 56);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.ToolTipText = "";
            this.btnCancel.TranslationKey = "Btn_Cancel";
            this.btnCancel.UseVisualStyleBackColor = false;
            this.btnCancel.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            // 
            // speedOverride
            // 
            this.speedOverride.Address = "D408";
            this.speedOverride.BackColor = System.Drawing.Color.White;
            this.speedOverride.FallbackText = "Speed";
            this.speedOverride.Location = new System.Drawing.Point(14, 13);
            this.speedOverride.LogAction = true;
            this.speedOverride.Name = "speedOverride";
            this.speedOverride.PlcNumber = 1;
            this.speedOverride.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.speedOverride.Size = new System.Drawing.Size(248, 42);
            this.speedOverride.Steps = new int[] {
        0,
        1,
        2,
        4,
        6,
        8,
        10,
        15,
        20,
        30,
        40,
        50,
        60,
        70,
        80,
        90,
        95,
        100};
            this.speedOverride.TabIndex = 3;
            this.speedOverride.TranslationKey = "Lbl_Speed";
            // 
            // HomeScreen
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.White;
            this.Controls.Add(this.btnStart);
            this.Controls.Add(this.btnPause);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.speedOverride);
            this.Name = "HomeScreen";
            this.Size = new System.Drawing.Size(1266, 818);
            this.ResumeLayout(false);

        }

        #endregion

        private Kaban.UI.Controls.ButtonHMI btnStart;
        private Kaban.UI.Controls.ButtonHMI btnPause;
        private Kaban.UI.Controls.ButtonHMI btnCancel;
        private Kaban.UI.Controls.SpeedOverrideHMI speedOverride;
    }
}
