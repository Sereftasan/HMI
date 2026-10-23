namespace Kaban.Machines.FA2010.Screens.Shared
{
    partial class AxisJogBar
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
        // FA2010's AXES' MANUAL MOVEMENT BAR (14 October): the speed of the axes'
        // manual movement (D409) and the jog step - JOG (continuous) or 10 / 1 /
        // 0.1 / 0.01 mm - Safety Bypass and Cylinder FW/Back (17 October: a cylinder started
        // moves back and forth (each way just under its alarm delay) - UnitCycling; both for Maintenance and Admin,
        // both off when the user changes), as the user set them on the manual
        // pages. Designed here ONCE: Manual Movement (above its tabs), Machine
        // Parameters > Axis and the Axis Monitor show this one. Its parts 14 from
        // the left and 13 from the top and the bottom, as the Home page's speed
        // (16 October: "it looks too crowded around there"). 17 October: the step buttons,
        // Safety Bypass and Cylinder FW/Back with their captions on two lines (TwoLineCaption),
        // narrower; every part 50 high for the two lines.
        // ===================================================================
        private void InitializeComponent()
        {
            Kaban.Core.Models.BitAction bitAction2 = new Kaban.Core.Models.BitAction();
            Kaban.Core.Models.BitAction bitAction3 = new Kaban.Core.Models.BitAction();
            Kaban.Core.Models.BitAction bitAction4 = new Kaban.Core.Models.BitAction();
            Kaban.Core.Models.BitAction bitAction5 = new Kaban.Core.Models.BitAction();
            Kaban.Core.Models.BitAction bitAction6 = new Kaban.Core.Models.BitAction();
            Kaban.Core.Models.BitAction bitAction7 = new Kaban.Core.Models.BitAction();
            Kaban.Core.Models.BitAction bitAction8 = new Kaban.Core.Models.BitAction();
            Kaban.Core.Models.BitAction bitAction9 = new Kaban.Core.Models.BitAction();
            Kaban.Core.Models.BitAction bitAction10 = new Kaban.Core.Models.BitAction();
            this.speedOverrideHMI1 = new Kaban.UI.Controls.SpeedOverrideHMI();
            this.BtnJog = new Kaban.UI.Controls.ButtonHMI();
            this.Btn10mm = new Kaban.UI.Controls.ButtonHMI();
            this.Btn1mm = new Kaban.UI.Controls.ButtonHMI();
            this.Btn01mm = new Kaban.UI.Controls.ButtonHMI();
            this.Btn001mm = new Kaban.UI.Controls.ButtonHMI();
            this.BtnSafetyBypass = new Kaban.UI.Controls.ButtonHMI();
            this.BtnCylinderFwBack = new Kaban.UI.Controls.ButtonHMI();
            this.SuspendLayout();
            bitAction2.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction2.Address = "M173";
            bitAction2.AddressOffset.StaticValue = 0D;
            bitAction2.AddressOffset.TagAddress = "";
            bitAction2.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            bitAction3.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction3.Address = "M172";
            bitAction3.AddressOffset.StaticValue = 0D;
            bitAction3.AddressOffset.TagAddress = "";
            bitAction3.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            bitAction4.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction4.Address = "M171";
            bitAction4.AddressOffset.StaticValue = 0D;
            bitAction4.AddressOffset.TagAddress = "";
            bitAction4.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            bitAction5.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction5.Address = "M170";
            bitAction5.AddressOffset.StaticValue = 0D;
            bitAction5.AddressOffset.TagAddress = "";
            bitAction5.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            bitAction6.Action = Kaban.Core.Enums.BitActionType.RESET;
            bitAction6.Address = "M170";
            bitAction6.AddressOffset.StaticValue = 0D;
            bitAction6.AddressOffset.TagAddress = "";
            bitAction6.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            bitAction7.Action = Kaban.Core.Enums.BitActionType.RESET;
            bitAction7.Address = "M171";
            bitAction7.AddressOffset.StaticValue = 0D;
            bitAction7.AddressOffset.TagAddress = "";
            bitAction7.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            bitAction8.Action = Kaban.Core.Enums.BitActionType.RESET;
            bitAction8.Address = "M172";
            bitAction8.AddressOffset.StaticValue = 0D;
            bitAction8.AddressOffset.TagAddress = "";
            bitAction8.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            bitAction9.Action = Kaban.Core.Enums.BitActionType.RESET;
            bitAction9.Address = "M173";
            bitAction9.AddressOffset.StaticValue = 0D;
            bitAction9.AddressOffset.TagAddress = "";
            bitAction9.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            bitAction10.Action = Kaban.Core.Enums.BitActionType.Alternate;
            bitAction10.Address = "M175";
            bitAction10.AddressOffset.StaticValue = 0D;
            bitAction10.AddressOffset.TagAddress = "";
            bitAction10.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            // 
            // speedOverrideHMI1
            // 
            this.speedOverrideHMI1.Address = "D409";
            this.speedOverrideHMI1.BackColor = System.Drawing.Color.White;
            this.speedOverrideHMI1.FallbackText = "Speed";
            this.speedOverrideHMI1.Location = new System.Drawing.Point(14, 13);
            this.speedOverrideHMI1.LogAction = true;
            this.speedOverrideHMI1.Name = "speedOverrideHMI1";
            this.speedOverrideHMI1.PlcNumber = 1;
            this.speedOverrideHMI1.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.speedOverrideHMI1.Size = new System.Drawing.Size(248, 50);
            this.speedOverrideHMI1.Steps = new int[] {
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
            this.speedOverrideHMI1.TabIndex = 39;
            this.speedOverrideHMI1.TranslationKey = "Lbl_Speed";
            // 
            // BtnJog
            // 
            this.BtnJog.ActiveBackColor = System.Drawing.Color.White;
            this.BtnJog.AlertBlink = false;
            this.BtnJog.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            bitAction6.Action = Kaban.Core.Enums.BitActionType.RESET;
            bitAction6.Address = "M170";
            bitAction6.AddressOffset.StaticValue = 0D;
            bitAction6.AddressOffset.TagAddress = "";
            bitAction6.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            bitAction7.Action = Kaban.Core.Enums.BitActionType.RESET;
            bitAction7.Address = "M171";
            bitAction7.AddressOffset.StaticValue = 0D;
            bitAction7.AddressOffset.TagAddress = "";
            bitAction7.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            bitAction8.Action = Kaban.Core.Enums.BitActionType.RESET;
            bitAction8.Address = "M172";
            bitAction8.AddressOffset.StaticValue = 0D;
            bitAction8.AddressOffset.TagAddress = "";
            bitAction8.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            bitAction9.Action = Kaban.Core.Enums.BitActionType.RESET;
            bitAction9.Address = "M173";
            bitAction9.AddressOffset.StaticValue = 0D;
            bitAction9.AddressOffset.TagAddress = "";
            bitAction9.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.BtnJog.BitActions.Add(bitAction6);
            this.BtnJog.BitActions.Add(bitAction7);
            this.BtnJog.BitActions.Add(bitAction8);
            this.BtnJog.BitActions.Add(bitAction9);
            this.BtnJog.BlinkCondition = Kaban.Core.Enums.SystemCondition.None;
            this.BtnJog.ButtonText = "JOG";
            this.BtnJog.ColorActive = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.BtnJog.ColorDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(250)))));
            this.BtnJog.ColorPassive = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.BtnJog.ConfirmationAddress = "";
            this.BtnJog.ConfirmationNeeded = false;
            this.BtnJog.ConfirmationText = "";
            this.BtnJog.ConfirmationTextKey = "";
            this.BtnJog.CornerRadius = 4;
            this.BtnJog.DescriptionText = "";
            this.BtnJog.Direction = Kaban.UI.Controls.ButtonGlyphs.Direction.None;
            this.BtnJog.DisplayAddress = null;
            this.BtnJog.DisplayOffset.StaticValue = 0D;
            this.BtnJog.DisplayOffset.TagAddress = "";
            this.BtnJog.DisplayOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.BtnJog.EnableConditions = Kaban.Core.Enums.SystemCondition.ManualMode;
            this.BtnJog.EnableTrigger.DescriptionKey = "";
            this.BtnJog.EnableTrigger.MonitorAddress = "";
            this.BtnJog.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.BtnJog.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.BtnJog.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.BtnJog.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.BtnJog.EnableTrigger.ValA = 0D;
            this.BtnJog.EnableTrigger.ValB = 0D;
            this.BtnJog.ErrorAddress = "";
            this.BtnJog.FallbackText = "JOG";
            this.BtnJog.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.BtnJog.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.BtnJog.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.BtnJog.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.BtnJog.ForeColor = System.Drawing.Color.Black;
            this.BtnJog.ForeDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(182)))), ((int)(((byte)(190)))));
            this.BtnJog.HoverBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(120)))), ((int)(((byte)(220)))));
            this.BtnJog.ImageActive = null;
            this.BtnJog.ImagePassive = null;
            this.BtnJog.IsActivePage = false;
            this.BtnJog.Lamp1Address = "M174";
            this.BtnJog.Lamp1Tip = "";
            this.BtnJog.Lamp2Address = "";
            this.BtnJog.Lamp2Tip = "";
            this.BtnJog.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.BtnJog.Location = new System.Drawing.Point(274, 13);
            this.BtnJog.LogAction = true;
            this.BtnJog.Name = "BtnJog";
            this.BtnJog.OutputAddress = "";
            this.BtnJog.OutputBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(170)))), ((int)(((byte)(70)))));
            this.BtnJog.PlcNumber = 1;
            this.BtnJog.RelayAddress = "";
            this.BtnJog.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.BtnJog.RestingBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(176)))), ((int)(((byte)(186)))));
            this.BtnJog.RoundLeftOnly = false;
            this.BtnJog.RoundRightOnly = false;
            this.BtnJog.ShowCaption = true;
            this.BtnJog.Size = new System.Drawing.Size(50, 50);
            this.BtnJog.TabIndex = 38;
            this.BtnJog.ToolTipText = "";
            this.BtnJog.TranslationKey = "Btn_JOG";
            this.BtnJog.UseVisualStyleBackColor = false;
            this.BtnJog.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            // 
            // Btn10mm
            // 
            this.Btn10mm.ActiveBackColor = System.Drawing.Color.White;
            this.Btn10mm.AlertBlink = false;
            this.Btn10mm.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            bitAction5.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction5.Address = "M170";
            bitAction5.AddressOffset.StaticValue = 0D;
            bitAction5.AddressOffset.TagAddress = "";
            bitAction5.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.Btn10mm.BitActions.Add(bitAction5);
            this.Btn10mm.BlinkCondition = Kaban.Core.Enums.SystemCondition.None;
            this.Btn10mm.ButtonText = "10 mm";
            this.Btn10mm.ColorActive = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.Btn10mm.ColorDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(250)))));
            this.Btn10mm.ColorPassive = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.Btn10mm.ConfirmationAddress = "";
            this.Btn10mm.ConfirmationNeeded = false;
            this.Btn10mm.ConfirmationText = "";
            this.Btn10mm.ConfirmationTextKey = "";
            this.Btn10mm.CornerRadius = 4;
            this.Btn10mm.DescriptionText = "";
            this.Btn10mm.Direction = Kaban.UI.Controls.ButtonGlyphs.Direction.None;
            this.Btn10mm.DisplayAddress = null;
            this.Btn10mm.DisplayOffset.StaticValue = 0D;
            this.Btn10mm.DisplayOffset.TagAddress = "";
            this.Btn10mm.DisplayOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.Btn10mm.EnableConditions = Kaban.Core.Enums.SystemCondition.ManualMode;
            this.Btn10mm.EnableTrigger.DescriptionKey = "";
            this.Btn10mm.EnableTrigger.MonitorAddress = "";
            this.Btn10mm.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.Btn10mm.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.Btn10mm.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.Btn10mm.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.Btn10mm.EnableTrigger.ValA = 0D;
            this.Btn10mm.EnableTrigger.ValB = 0D;
            this.Btn10mm.ErrorAddress = "";
            this.Btn10mm.FallbackText = "10 mm";
            this.Btn10mm.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.Btn10mm.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.Btn10mm.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.Btn10mm.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.Btn10mm.ForeColor = System.Drawing.Color.Black;
            this.Btn10mm.ForeDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(182)))), ((int)(((byte)(190)))));
            this.Btn10mm.HoverBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(120)))), ((int)(((byte)(220)))));
            this.Btn10mm.ImageActive = null;
            this.Btn10mm.ImagePassive = null;
            this.Btn10mm.IsActivePage = false;
            this.Btn10mm.Lamp1Address = "M170";
            this.Btn10mm.Lamp1Tip = "";
            this.Btn10mm.Lamp2Address = "";
            this.Btn10mm.Lamp2Tip = "";
            this.Btn10mm.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.Btn10mm.Location = new System.Drawing.Point(330, 13);
            this.Btn10mm.LogAction = true;
            this.Btn10mm.Name = "Btn10mm";
            this.Btn10mm.OutputAddress = "";
            this.Btn10mm.OutputBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(170)))), ((int)(((byte)(70)))));
            this.Btn10mm.PlcNumber = 1;
            this.Btn10mm.RelayAddress = "";
            this.Btn10mm.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.Btn10mm.RestingBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(176)))), ((int)(((byte)(186)))));
            this.Btn10mm.RoundLeftOnly = false;
            this.Btn10mm.RoundRightOnly = false;
            this.Btn10mm.ShowCaption = true;
            this.Btn10mm.Size = new System.Drawing.Size(50, 50);
            this.Btn10mm.TabIndex = 40;
            this.Btn10mm.ToolTipText = "";
            this.Btn10mm.TwoLineCaption = true;
            this.Btn10mm.TranslationKey = "Btn_10mm";
            this.Btn10mm.UseVisualStyleBackColor = false;
            this.Btn10mm.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            // 
            // Btn1mm
            // 
            this.Btn1mm.ActiveBackColor = System.Drawing.Color.White;
            this.Btn1mm.AlertBlink = false;
            this.Btn1mm.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            bitAction4.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction4.Address = "M171";
            bitAction4.AddressOffset.StaticValue = 0D;
            bitAction4.AddressOffset.TagAddress = "";
            bitAction4.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.Btn1mm.BitActions.Add(bitAction4);
            this.Btn1mm.BlinkCondition = Kaban.Core.Enums.SystemCondition.None;
            this.Btn1mm.ButtonText = "1 mm";
            this.Btn1mm.ColorActive = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.Btn1mm.ColorDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(250)))));
            this.Btn1mm.ColorPassive = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.Btn1mm.ConfirmationAddress = "";
            this.Btn1mm.ConfirmationNeeded = false;
            this.Btn1mm.ConfirmationText = "";
            this.Btn1mm.ConfirmationTextKey = "";
            this.Btn1mm.CornerRadius = 4;
            this.Btn1mm.DescriptionText = "";
            this.Btn1mm.Direction = Kaban.UI.Controls.ButtonGlyphs.Direction.None;
            this.Btn1mm.DisplayAddress = null;
            this.Btn1mm.DisplayOffset.StaticValue = 0D;
            this.Btn1mm.DisplayOffset.TagAddress = "";
            this.Btn1mm.DisplayOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.Btn1mm.EnableConditions = Kaban.Core.Enums.SystemCondition.ManualMode;
            this.Btn1mm.EnableTrigger.DescriptionKey = "";
            this.Btn1mm.EnableTrigger.MonitorAddress = "";
            this.Btn1mm.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.Btn1mm.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.Btn1mm.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.Btn1mm.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.Btn1mm.EnableTrigger.ValA = 0D;
            this.Btn1mm.EnableTrigger.ValB = 0D;
            this.Btn1mm.ErrorAddress = "";
            this.Btn1mm.FallbackText = "1 mm";
            this.Btn1mm.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.Btn1mm.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.Btn1mm.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.Btn1mm.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.Btn1mm.ForeColor = System.Drawing.Color.Black;
            this.Btn1mm.ForeDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(182)))), ((int)(((byte)(190)))));
            this.Btn1mm.HoverBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(120)))), ((int)(((byte)(220)))));
            this.Btn1mm.ImageActive = null;
            this.Btn1mm.ImagePassive = null;
            this.Btn1mm.IsActivePage = false;
            this.Btn1mm.Lamp1Address = "M171";
            this.Btn1mm.Lamp1Tip = "";
            this.Btn1mm.Lamp2Address = "";
            this.Btn1mm.Lamp2Tip = "";
            this.Btn1mm.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.Btn1mm.Location = new System.Drawing.Point(386, 13);
            this.Btn1mm.LogAction = true;
            this.Btn1mm.Name = "Btn1mm";
            this.Btn1mm.OutputAddress = "";
            this.Btn1mm.OutputBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(170)))), ((int)(((byte)(70)))));
            this.Btn1mm.PlcNumber = 1;
            this.Btn1mm.RelayAddress = "";
            this.Btn1mm.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.Btn1mm.RestingBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(176)))), ((int)(((byte)(186)))));
            this.Btn1mm.RoundLeftOnly = false;
            this.Btn1mm.RoundRightOnly = false;
            this.Btn1mm.ShowCaption = true;
            this.Btn1mm.Size = new System.Drawing.Size(50, 50);
            this.Btn1mm.TabIndex = 41;
            this.Btn1mm.ToolTipText = "";
            this.Btn1mm.TwoLineCaption = true;
            this.Btn1mm.TranslationKey = "Btn_1mm";
            this.Btn1mm.UseVisualStyleBackColor = false;
            this.Btn1mm.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            // 
            // Btn01mm
            // 
            this.Btn01mm.ActiveBackColor = System.Drawing.Color.White;
            this.Btn01mm.AlertBlink = false;
            this.Btn01mm.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            bitAction3.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction3.Address = "M172";
            bitAction3.AddressOffset.StaticValue = 0D;
            bitAction3.AddressOffset.TagAddress = "";
            bitAction3.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.Btn01mm.BitActions.Add(bitAction3);
            this.Btn01mm.BlinkCondition = Kaban.Core.Enums.SystemCondition.None;
            this.Btn01mm.ButtonText = "0.1 mm";
            this.Btn01mm.ColorActive = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.Btn01mm.ColorDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(250)))));
            this.Btn01mm.ColorPassive = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.Btn01mm.ConfirmationAddress = "";
            this.Btn01mm.ConfirmationNeeded = false;
            this.Btn01mm.ConfirmationText = "";
            this.Btn01mm.ConfirmationTextKey = "";
            this.Btn01mm.CornerRadius = 4;
            this.Btn01mm.DescriptionText = "";
            this.Btn01mm.Direction = Kaban.UI.Controls.ButtonGlyphs.Direction.None;
            this.Btn01mm.DisplayAddress = null;
            this.Btn01mm.DisplayOffset.StaticValue = 0D;
            this.Btn01mm.DisplayOffset.TagAddress = "";
            this.Btn01mm.DisplayOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.Btn01mm.EnableConditions = Kaban.Core.Enums.SystemCondition.ManualMode;
            this.Btn01mm.EnableTrigger.DescriptionKey = "";
            this.Btn01mm.EnableTrigger.MonitorAddress = "";
            this.Btn01mm.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.Btn01mm.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.Btn01mm.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.Btn01mm.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.Btn01mm.EnableTrigger.ValA = 0D;
            this.Btn01mm.EnableTrigger.ValB = 0D;
            this.Btn01mm.ErrorAddress = "";
            this.Btn01mm.FallbackText = "0.1 mm";
            this.Btn01mm.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.Btn01mm.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.Btn01mm.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.Btn01mm.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.Btn01mm.ForeColor = System.Drawing.Color.Black;
            this.Btn01mm.ForeDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(182)))), ((int)(((byte)(190)))));
            this.Btn01mm.HoverBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(120)))), ((int)(((byte)(220)))));
            this.Btn01mm.ImageActive = null;
            this.Btn01mm.ImagePassive = null;
            this.Btn01mm.IsActivePage = false;
            this.Btn01mm.Lamp1Address = "M172";
            this.Btn01mm.Lamp1Tip = "";
            this.Btn01mm.Lamp2Address = "";
            this.Btn01mm.Lamp2Tip = "";
            this.Btn01mm.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.Btn01mm.Location = new System.Drawing.Point(442, 13);
            this.Btn01mm.LogAction = true;
            this.Btn01mm.Name = "Btn01mm";
            this.Btn01mm.OutputAddress = "";
            this.Btn01mm.OutputBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(170)))), ((int)(((byte)(70)))));
            this.Btn01mm.PlcNumber = 1;
            this.Btn01mm.RelayAddress = "";
            this.Btn01mm.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.Btn01mm.RestingBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(176)))), ((int)(((byte)(186)))));
            this.Btn01mm.RoundLeftOnly = false;
            this.Btn01mm.RoundRightOnly = false;
            this.Btn01mm.ShowCaption = true;
            this.Btn01mm.Size = new System.Drawing.Size(50, 50);
            this.Btn01mm.TabIndex = 42;
            this.Btn01mm.ToolTipText = "";
            this.Btn01mm.TwoLineCaption = true;
            this.Btn01mm.TranslationKey = "Btn_01mm";
            this.Btn01mm.UseVisualStyleBackColor = false;
            this.Btn01mm.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            // 
            // Btn001mm
            // 
            this.Btn001mm.ActiveBackColor = System.Drawing.Color.White;
            this.Btn001mm.AlertBlink = false;
            this.Btn001mm.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            bitAction2.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction2.Address = "M173";
            bitAction2.AddressOffset.StaticValue = 0D;
            bitAction2.AddressOffset.TagAddress = "";
            bitAction2.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.Btn001mm.BitActions.Add(bitAction2);
            this.Btn001mm.BlinkCondition = Kaban.Core.Enums.SystemCondition.None;
            this.Btn001mm.ButtonText = "0.01 mm";
            this.Btn001mm.ColorActive = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.Btn001mm.ColorDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(250)))));
            this.Btn001mm.ColorPassive = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.Btn001mm.ConfirmationAddress = "";
            this.Btn001mm.ConfirmationNeeded = false;
            this.Btn001mm.ConfirmationText = "";
            this.Btn001mm.ConfirmationTextKey = "";
            this.Btn001mm.CornerRadius = 4;
            this.Btn001mm.DescriptionText = "";
            this.Btn001mm.Direction = Kaban.UI.Controls.ButtonGlyphs.Direction.None;
            this.Btn001mm.DisplayAddress = null;
            this.Btn001mm.DisplayOffset.StaticValue = 0D;
            this.Btn001mm.DisplayOffset.TagAddress = "";
            this.Btn001mm.DisplayOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.Btn001mm.EnableConditions = Kaban.Core.Enums.SystemCondition.ManualMode;
            this.Btn001mm.EnableTrigger.DescriptionKey = "";
            this.Btn001mm.EnableTrigger.MonitorAddress = "";
            this.Btn001mm.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.Btn001mm.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.Btn001mm.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.Btn001mm.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.Btn001mm.EnableTrigger.ValA = 0D;
            this.Btn001mm.EnableTrigger.ValB = 0D;
            this.Btn001mm.ErrorAddress = "";
            this.Btn001mm.FallbackText = "0.01 mm";
            this.Btn001mm.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.Btn001mm.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.Btn001mm.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.Btn001mm.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.Btn001mm.ForeColor = System.Drawing.Color.Black;
            this.Btn001mm.ForeDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(182)))), ((int)(((byte)(190)))));
            this.Btn001mm.HoverBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(120)))), ((int)(((byte)(220)))));
            this.Btn001mm.ImageActive = null;
            this.Btn001mm.ImagePassive = null;
            this.Btn001mm.IsActivePage = false;
            this.Btn001mm.Lamp1Address = "M173";
            this.Btn001mm.Lamp1Tip = "";
            this.Btn001mm.Lamp2Address = "";
            this.Btn001mm.Lamp2Tip = "";
            this.Btn001mm.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.Btn001mm.Location = new System.Drawing.Point(498, 13);
            this.Btn001mm.LogAction = true;
            this.Btn001mm.Name = "Btn001mm";
            this.Btn001mm.OutputAddress = "";
            this.Btn001mm.OutputBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(170)))), ((int)(((byte)(70)))));
            this.Btn001mm.PlcNumber = 1;
            this.Btn001mm.RelayAddress = "";
            this.Btn001mm.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.Btn001mm.RestingBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(176)))), ((int)(((byte)(186)))));
            this.Btn001mm.RoundLeftOnly = false;
            this.Btn001mm.RoundRightOnly = false;
            this.Btn001mm.ShowCaption = true;
            this.Btn001mm.Size = new System.Drawing.Size(50, 50);
            this.Btn001mm.TabIndex = 43;
            this.Btn001mm.ToolTipText = "";
            this.Btn001mm.TwoLineCaption = true;
            this.Btn001mm.TranslationKey = "Btn_001mm";
            this.Btn001mm.UseVisualStyleBackColor = false;
            this.Btn001mm.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            // 
            // BtnSafetyBypass
            // 
            this.BtnSafetyBypass.ActiveBackColor = System.Drawing.Color.White;
            this.BtnSafetyBypass.AlertBlink = false;
            this.BtnSafetyBypass.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            bitAction10.Action = Kaban.Core.Enums.BitActionType.Alternate;
            bitAction10.Address = "M175";
            bitAction10.AddressOffset.StaticValue = 0D;
            bitAction10.AddressOffset.TagAddress = "";
            bitAction10.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.BtnSafetyBypass.BitActions.Add(bitAction10);
            this.BtnSafetyBypass.BlinkCondition = Kaban.Core.Enums.SystemCondition.None;
            this.BtnSafetyBypass.ButtonText = "Safety Bypass";
            this.BtnSafetyBypass.ColorActive = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.BtnSafetyBypass.ColorDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(250)))));
            this.BtnSafetyBypass.ColorPassive = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.BtnSafetyBypass.ConfirmationAddress = "";
            this.BtnSafetyBypass.ConfirmationNeeded = false;
            this.BtnSafetyBypass.ConfirmationText = "";
            this.BtnSafetyBypass.ConfirmationTextKey = "";
            this.BtnSafetyBypass.CornerRadius = 4;
            this.BtnSafetyBypass.DescriptionText = "";
            this.BtnSafetyBypass.Direction = Kaban.UI.Controls.ButtonGlyphs.Direction.None;
            this.BtnSafetyBypass.DisplayAddress = null;
            this.BtnSafetyBypass.DisplayOffset.StaticValue = 0D;
            this.BtnSafetyBypass.DisplayOffset.TagAddress = "";
            this.BtnSafetyBypass.DisplayOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.BtnSafetyBypass.EnableConditions = Kaban.Core.Enums.SystemCondition.ManualMode;
            this.BtnSafetyBypass.EnableTrigger.DescriptionKey = "";
            this.BtnSafetyBypass.EnableTrigger.MonitorAddress = "";
            this.BtnSafetyBypass.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.BtnSafetyBypass.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.BtnSafetyBypass.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.BtnSafetyBypass.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.BtnSafetyBypass.EnableTrigger.ValA = 0D;
            this.BtnSafetyBypass.EnableTrigger.ValB = 0D;
            this.BtnSafetyBypass.ErrorAddress = "";
            this.BtnSafetyBypass.FallbackText = "Safety Bypass";
            this.BtnSafetyBypass.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.BtnSafetyBypass.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.BtnSafetyBypass.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.BtnSafetyBypass.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.BtnSafetyBypass.ForeColor = System.Drawing.Color.Black;
            this.BtnSafetyBypass.ForeDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(182)))), ((int)(((byte)(190)))));
            this.BtnSafetyBypass.HoverBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(120)))), ((int)(((byte)(220)))));
            this.BtnSafetyBypass.ImageActive = null;
            this.BtnSafetyBypass.ImagePassive = null;
            this.BtnSafetyBypass.IsActivePage = false;
            this.BtnSafetyBypass.Lamp1Address = "M175";
            this.BtnSafetyBypass.Lamp1Tip = "";
            this.BtnSafetyBypass.Lamp2Address = "";
            this.BtnSafetyBypass.Lamp2Tip = "";
            this.BtnSafetyBypass.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.BtnSafetyBypass.Location = new System.Drawing.Point(560, 13);
            this.BtnSafetyBypass.LogAction = true;
            this.BtnSafetyBypass.Name = "BtnSafetyBypass";
            this.BtnSafetyBypass.OutputAddress = "";
            this.BtnSafetyBypass.OutputBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(170)))), ((int)(((byte)(70)))));
            this.BtnSafetyBypass.PlcNumber = 1;
            this.BtnSafetyBypass.RelayAddress = "";
            this.BtnSafetyBypass.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.BtnSafetyBypass.ResetOnUserChange = true;
            this.BtnSafetyBypass.RestingBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(176)))), ((int)(((byte)(186)))));
            this.BtnSafetyBypass.RoundLeftOnly = false;
            this.BtnSafetyBypass.RoundRightOnly = false;
            this.BtnSafetyBypass.ShowCaption = true;
            this.BtnSafetyBypass.Size = new System.Drawing.Size(64, 50);
            this.BtnSafetyBypass.TabIndex = 47;
            this.BtnSafetyBypass.ToolTipText = "";
            this.BtnSafetyBypass.TwoLineCaption = true;
            this.BtnSafetyBypass.TranslationKey = "Btn_SafetyBypass";
            this.BtnSafetyBypass.UseVisualStyleBackColor = false;
            this.BtnSafetyBypass.VisibilityConditions = Kaban.Core.Enums.SystemCondition.MaintenanceUser;
            // 
            // BtnCylinderFwBack
            // 
            this.BtnCylinderFwBack.ActiveBackColor = System.Drawing.Color.White;
            this.BtnCylinderFwBack.AlertBlink = false;
            this.BtnCylinderFwBack.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.BtnCylinderFwBack.BlinkCondition = Kaban.Core.Enums.SystemCondition.None;
            this.BtnCylinderFwBack.ButtonText = "Cylinder FW/Back";
            this.BtnCylinderFwBack.ColorActive = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.BtnCylinderFwBack.ColorDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(250)))));
            this.BtnCylinderFwBack.ColorPassive = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.BtnCylinderFwBack.ConfirmationAddress = "";
            this.BtnCylinderFwBack.ConfirmationNeeded = false;
            this.BtnCylinderFwBack.ConfirmationText = "";
            this.BtnCylinderFwBack.ConfirmationTextKey = "";
            this.BtnCylinderFwBack.CornerRadius = 4;
            this.BtnCylinderFwBack.DescriptionText = "";
            this.BtnCylinderFwBack.Direction = Kaban.UI.Controls.ButtonGlyphs.Direction.None;
            this.BtnCylinderFwBack.DisplayAddress = null;
            this.BtnCylinderFwBack.DisplayOffset.StaticValue = 0D;
            this.BtnCylinderFwBack.DisplayOffset.TagAddress = "";
            this.BtnCylinderFwBack.DisplayOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.BtnCylinderFwBack.EnableConditions = Kaban.Core.Enums.SystemCondition.ManualMode;
            this.BtnCylinderFwBack.EnableTrigger.DescriptionKey = "";
            this.BtnCylinderFwBack.EnableTrigger.MonitorAddress = "";
            this.BtnCylinderFwBack.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.BtnCylinderFwBack.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.BtnCylinderFwBack.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.BtnCylinderFwBack.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.BtnCylinderFwBack.EnableTrigger.ValA = 0D;
            this.BtnCylinderFwBack.EnableTrigger.ValB = 0D;
            this.BtnCylinderFwBack.ErrorAddress = "";
            this.BtnCylinderFwBack.FallbackText = "Cylinder FW/Back";
            this.BtnCylinderFwBack.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.BtnCylinderFwBack.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.BtnCylinderFwBack.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.BtnCylinderFwBack.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.BtnCylinderFwBack.ForeColor = System.Drawing.Color.Black;
            this.BtnCylinderFwBack.HmiFunction = Kaban.UI.Controls.HmiButtonFunction.CylinderForwardBack;
            this.BtnCylinderFwBack.ForeDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(182)))), ((int)(((byte)(190)))));
            this.BtnCylinderFwBack.HoverBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(120)))), ((int)(((byte)(220)))));
            this.BtnCylinderFwBack.ImageActive = null;
            this.BtnCylinderFwBack.ImagePassive = null;
            this.BtnCylinderFwBack.IsActivePage = false;
            this.BtnCylinderFwBack.Lamp1Address = "";
            this.BtnCylinderFwBack.Lamp1Tip = "";
            this.BtnCylinderFwBack.Lamp2Address = "";
            this.BtnCylinderFwBack.Lamp2Tip = "";
            this.BtnCylinderFwBack.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.BtnCylinderFwBack.Location = new System.Drawing.Point(630, 13);
            this.BtnCylinderFwBack.LogAction = true;
            this.BtnCylinderFwBack.Name = "BtnCylinderFwBack";
            this.BtnCylinderFwBack.OutputAddress = "";
            this.BtnCylinderFwBack.OutputBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(170)))), ((int)(((byte)(70)))));
            this.BtnCylinderFwBack.PlcNumber = 1;
            this.BtnCylinderFwBack.RelayAddress = "";
            this.BtnCylinderFwBack.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.BtnCylinderFwBack.RestingBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(176)))), ((int)(((byte)(186)))));
            this.BtnCylinderFwBack.RoundLeftOnly = false;
            this.BtnCylinderFwBack.RoundRightOnly = false;
            this.BtnCylinderFwBack.ShowCaption = true;
            this.BtnCylinderFwBack.Size = new System.Drawing.Size(68, 50);
            this.BtnCylinderFwBack.TabIndex = 48;
            this.BtnCylinderFwBack.ToolTipText = "";
            this.BtnCylinderFwBack.TwoLineCaption = true;
            this.BtnCylinderFwBack.TranslationKey = "Btn_CylinderFwBack";
            this.BtnCylinderFwBack.UseVisualStyleBackColor = false;
            this.BtnCylinderFwBack.VisibilityConditions = Kaban.Core.Enums.SystemCondition.MaintenanceUser;
            // 
            // AxisJogBar
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.White;
            this.Controls.Add(this.speedOverrideHMI1);
            this.Controls.Add(this.BtnJog);
            this.Controls.Add(this.Btn10mm);
            this.Controls.Add(this.Btn1mm);
            this.Controls.Add(this.Btn01mm);
            this.Controls.Add(this.Btn001mm);
            this.Controls.Add(this.BtnSafetyBypass);
            this.Controls.Add(this.BtnCylinderFwBack);
            this.Name = "AxisJogBar";
            this.Size = new System.Drawing.Size(712, 76);
            this.ResumeLayout(false);
        }

        #endregion

        private Kaban.UI.Controls.SpeedOverrideHMI speedOverrideHMI1;
        private Kaban.UI.Controls.ButtonHMI BtnJog;
        private Kaban.UI.Controls.ButtonHMI Btn10mm;
        private Kaban.UI.Controls.ButtonHMI Btn1mm;
        private Kaban.UI.Controls.ButtonHMI Btn01mm;
        private Kaban.UI.Controls.ButtonHMI Btn001mm;
        private Kaban.UI.Controls.ButtonHMI BtnSafetyBypass;
        private Kaban.UI.Controls.ButtonHMI BtnCylinderFwBack;
    }
}
