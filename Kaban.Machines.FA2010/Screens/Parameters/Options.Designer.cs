namespace Kaban.Machines.FA2010.Screens.Parameters
{
    partial class Options
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
            this.optCooling = new Kaban.UI.Controls.CheckBoxHMI();
            this.optProfile_Carrying_Roller_Sensor = new Kaban.UI.Controls.CheckBoxHMI();
            this.optProfile_Recog_Carrying_Roller = new Kaban.UI.Controls.CheckBoxHMI();
            this.optMilling_Carrying_Roller = new Kaban.UI.Controls.CheckBoxHMI();
            this.optExit_Carrying_Roller = new Kaban.UI.Controls.CheckBoxHMI();
            this.optLeft_Profile_Recognition = new Kaban.UI.Controls.CheckBoxHMI();
            this.optSliding_Conveyor_Rollers = new Kaban.UI.Controls.CheckBoxHMI();
            this.optReverse_Machine = new Kaban.UI.Controls.CheckBoxHMI();
            this.optSpecial_Reverse_Machine = new Kaban.UI.Controls.CheckBoxHMI();
            this.optCutting_Right_Top_Clamping_Long = new Kaban.UI.Controls.CheckBoxHMI();
            this.optMajor_Profile_Ejection_Tray_Operates_Individually = new Kaban.UI.Controls.CheckBoxHMI();
            this.optTop_Clamping_Reclamp = new Kaban.UI.Controls.CheckBoxHMI();
            this.optGripper_0_90_Degree_Turn = new Kaban.UI.Controls.CheckBoxHMI();
            this.optClamping_Mould = new Kaban.UI.Controls.CheckBoxHMI();
            this.optCross_Roller = new Kaban.UI.Controls.CheckBoxHMI();
            this.optCutting_Left_Front_Encoder = new Kaban.UI.Controls.CheckBoxHMI();
            this.optCutting_Right_Front_Vertical_Without_Encoder = new Kaban.UI.Controls.CheckBoxHMI();
            this.optCutting_Left_Front_Air_Exhaust = new Kaban.UI.Controls.CheckBoxHMI();
            this.groupBoxMullionNotching = new Kaban.UI.Controls.GroupBoxHMI();
            this.checkBoxWithBladeBackFinishCut = new Kaban.UI.Controls.CheckBoxHMI();
            this.checkBoxWithBladeLeftBackFinishCut = new Kaban.UI.Controls.CheckBoxHMI();
            this.checkBoxWithBladeSingleSideCut = new Kaban.UI.Controls.CheckBoxHMI();
            this.optVithBlade = new Kaban.UI.Controls.CheckBoxHMI();
            this.optMullionNotching = new Kaban.UI.Controls.CheckBoxHMI();
            this.logMachine_Closed = new Kaban.UI.Controls.CheckBoxHMI();
            this.logOperation_Not_Active = new Kaban.UI.Controls.CheckBoxHMI();
            this.logOperation_Active = new Kaban.UI.Controls.CheckBoxHMI();
            this.logPause_Mode = new Kaban.UI.Controls.CheckBoxHMI();
            this.logError = new Kaban.UI.Controls.CheckBoxHMI();
            this.logEmergency_Stop = new Kaban.UI.Controls.CheckBoxHMI();
            this.logProfile_Changed = new Kaban.UI.Controls.CheckBoxHMI();
            this.logCutting_Done = new Kaban.UI.Controls.CheckBoxHMI();
            this.logWaste_Profile_Emerged = new Kaban.UI.Controls.CheckBoxHMI();
            this.logBar_Length_Measured = new Kaban.UI.Controls.CheckBoxHMI();
            this.logProfile_Not_Recognised = new Kaban.UI.Controls.CheckBoxHMI();
            this.logProfile_Measured = new Kaban.UI.Controls.CheckBoxHMI();
            this.recProfile_Changed = new Kaban.UI.Controls.LogRecordHMI();
            this.recWaste_Profile_Emerged = new Kaban.UI.Controls.LogRecordHMI();
            this.recBar_Length_Measured = new Kaban.UI.Controls.LogRecordHMI();
            this.recProfile_Not_Recognised = new Kaban.UI.Controls.LogRecordHMI();
            this.recProfile_Measured = new Kaban.UI.Controls.LogRecordHMI();
            this.grpIndustry4Logging = new Kaban.UI.Controls.GroupBoxHMI();
            this.industry4SettingsPlace = new System.Windows.Forms.Panel();
            this.groupBoxMullionNotching.SuspendLayout();
            this.grpIndustry4Logging.SuspendLayout();
            this.SuspendLayout();
            // 
            // optCooling
            // 
            this.optCooling.Address = "L6";
            this.optCooling.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optCooling.ConfirmWrite = true;
            this.optCooling.CornerRadius = 4;
            this.optCooling.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optCooling.DescriptionText = "Cooling";
            this.optCooling.EnableTrigger.DescriptionKey = "";
            this.optCooling.EnableTrigger.MonitorAddress = "";
            this.optCooling.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optCooling.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optCooling.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optCooling.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optCooling.EnableTrigger.ValA = 0D;
            this.optCooling.EnableTrigger.ValB = 0D;
            this.optCooling.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optCooling.Location = new System.Drawing.Point(24, 24);
            this.optCooling.LogAction = true;
            this.optCooling.Name = "optCooling";
            this.optCooling.PlcNumber = 1;
            this.optCooling.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optCooling.Size = new System.Drawing.Size(450, 38);
            this.optCooling.TabIndex = 61;
            this.optCooling.ToolTipCaption = null;
            // 
            // optProfile_Carrying_Roller_Sensor
            // 
            this.optProfile_Carrying_Roller_Sensor.Address = "L3";
            this.optProfile_Carrying_Roller_Sensor.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optProfile_Carrying_Roller_Sensor.ConfirmWrite = true;
            this.optProfile_Carrying_Roller_Sensor.CornerRadius = 4;
            this.optProfile_Carrying_Roller_Sensor.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optProfile_Carrying_Roller_Sensor.DescriptionText = "Profile Carrying Roller Sensor";
            this.optProfile_Carrying_Roller_Sensor.EnableTrigger.DescriptionKey = "";
            this.optProfile_Carrying_Roller_Sensor.EnableTrigger.MonitorAddress = "";
            this.optProfile_Carrying_Roller_Sensor.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optProfile_Carrying_Roller_Sensor.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optProfile_Carrying_Roller_Sensor.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optProfile_Carrying_Roller_Sensor.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optProfile_Carrying_Roller_Sensor.EnableTrigger.ValA = 0D;
            this.optProfile_Carrying_Roller_Sensor.EnableTrigger.ValB = 0D;
            this.optProfile_Carrying_Roller_Sensor.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optProfile_Carrying_Roller_Sensor.Location = new System.Drawing.Point(24, 62);
            this.optProfile_Carrying_Roller_Sensor.LogAction = true;
            this.optProfile_Carrying_Roller_Sensor.Name = "optProfile_Carrying_Roller_Sensor";
            this.optProfile_Carrying_Roller_Sensor.PlcNumber = 1;
            this.optProfile_Carrying_Roller_Sensor.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optProfile_Carrying_Roller_Sensor.Size = new System.Drawing.Size(450, 38);
            this.optProfile_Carrying_Roller_Sensor.TabIndex = 62;
            this.optProfile_Carrying_Roller_Sensor.ToolTipCaption = null;
            // 
            // optProfile_Recog_Carrying_Roller
            // 
            this.optProfile_Recog_Carrying_Roller.Address = "L13";
            this.optProfile_Recog_Carrying_Roller.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optProfile_Recog_Carrying_Roller.ConfirmWrite = true;
            this.optProfile_Recog_Carrying_Roller.CornerRadius = 4;
            this.optProfile_Recog_Carrying_Roller.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optProfile_Recog_Carrying_Roller.DescriptionText = "Profile Recog. Carrying Roller";
            this.optProfile_Recog_Carrying_Roller.EnableTrigger.DescriptionKey = "";
            this.optProfile_Recog_Carrying_Roller.EnableTrigger.MonitorAddress = "";
            this.optProfile_Recog_Carrying_Roller.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optProfile_Recog_Carrying_Roller.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optProfile_Recog_Carrying_Roller.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optProfile_Recog_Carrying_Roller.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optProfile_Recog_Carrying_Roller.EnableTrigger.ValA = 0D;
            this.optProfile_Recog_Carrying_Roller.EnableTrigger.ValB = 0D;
            this.optProfile_Recog_Carrying_Roller.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optProfile_Recog_Carrying_Roller.Location = new System.Drawing.Point(24, 100);
            this.optProfile_Recog_Carrying_Roller.LogAction = true;
            this.optProfile_Recog_Carrying_Roller.Name = "optProfile_Recog_Carrying_Roller";
            this.optProfile_Recog_Carrying_Roller.PlcNumber = 1;
            this.optProfile_Recog_Carrying_Roller.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optProfile_Recog_Carrying_Roller.Size = new System.Drawing.Size(450, 38);
            this.optProfile_Recog_Carrying_Roller.TabIndex = 63;
            this.optProfile_Recog_Carrying_Roller.ToolTipCaption = null;
            // 
            // optMilling_Carrying_Roller
            // 
            this.optMilling_Carrying_Roller.Address = "L14";
            this.optMilling_Carrying_Roller.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optMilling_Carrying_Roller.ConfirmWrite = true;
            this.optMilling_Carrying_Roller.CornerRadius = 4;
            this.optMilling_Carrying_Roller.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optMilling_Carrying_Roller.DescriptionText = "Milling Carrying Roller";
            this.optMilling_Carrying_Roller.EnableTrigger.DescriptionKey = "";
            this.optMilling_Carrying_Roller.EnableTrigger.MonitorAddress = "";
            this.optMilling_Carrying_Roller.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optMilling_Carrying_Roller.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optMilling_Carrying_Roller.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optMilling_Carrying_Roller.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optMilling_Carrying_Roller.EnableTrigger.ValA = 0D;
            this.optMilling_Carrying_Roller.EnableTrigger.ValB = 0D;
            this.optMilling_Carrying_Roller.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optMilling_Carrying_Roller.Location = new System.Drawing.Point(24, 138);
            this.optMilling_Carrying_Roller.LogAction = true;
            this.optMilling_Carrying_Roller.Name = "optMilling_Carrying_Roller";
            this.optMilling_Carrying_Roller.PlcNumber = 1;
            this.optMilling_Carrying_Roller.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optMilling_Carrying_Roller.Size = new System.Drawing.Size(450, 38);
            this.optMilling_Carrying_Roller.TabIndex = 64;
            this.optMilling_Carrying_Roller.ToolTipCaption = null;
            // 
            // optExit_Carrying_Roller
            // 
            this.optExit_Carrying_Roller.Address = "L15";
            this.optExit_Carrying_Roller.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optExit_Carrying_Roller.ConfirmWrite = true;
            this.optExit_Carrying_Roller.CornerRadius = 4;
            this.optExit_Carrying_Roller.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optExit_Carrying_Roller.DescriptionText = "Exit Carrying Roller";
            this.optExit_Carrying_Roller.EnableTrigger.DescriptionKey = "";
            this.optExit_Carrying_Roller.EnableTrigger.MonitorAddress = "";
            this.optExit_Carrying_Roller.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optExit_Carrying_Roller.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optExit_Carrying_Roller.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optExit_Carrying_Roller.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optExit_Carrying_Roller.EnableTrigger.ValA = 0D;
            this.optExit_Carrying_Roller.EnableTrigger.ValB = 0D;
            this.optExit_Carrying_Roller.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optExit_Carrying_Roller.Location = new System.Drawing.Point(24, 176);
            this.optExit_Carrying_Roller.LogAction = true;
            this.optExit_Carrying_Roller.Name = "optExit_Carrying_Roller";
            this.optExit_Carrying_Roller.PlcNumber = 1;
            this.optExit_Carrying_Roller.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optExit_Carrying_Roller.Size = new System.Drawing.Size(450, 38);
            this.optExit_Carrying_Roller.TabIndex = 65;
            this.optExit_Carrying_Roller.ToolTipCaption = null;
            // 
            // optLeft_Profile_Recognition
            // 
            this.optLeft_Profile_Recognition.Address = "L19";
            this.optLeft_Profile_Recognition.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optLeft_Profile_Recognition.ConfirmWrite = true;
            this.optLeft_Profile_Recognition.CornerRadius = 4;
            this.optLeft_Profile_Recognition.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optLeft_Profile_Recognition.DescriptionText = "Left Profile Recognition";
            this.optLeft_Profile_Recognition.EnableTrigger.DescriptionKey = "";
            this.optLeft_Profile_Recognition.EnableTrigger.MonitorAddress = "";
            this.optLeft_Profile_Recognition.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optLeft_Profile_Recognition.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optLeft_Profile_Recognition.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optLeft_Profile_Recognition.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optLeft_Profile_Recognition.EnableTrigger.ValA = 0D;
            this.optLeft_Profile_Recognition.EnableTrigger.ValB = 0D;
            this.optLeft_Profile_Recognition.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optLeft_Profile_Recognition.Location = new System.Drawing.Point(24, 214);
            this.optLeft_Profile_Recognition.LogAction = true;
            this.optLeft_Profile_Recognition.Name = "optLeft_Profile_Recognition";
            this.optLeft_Profile_Recognition.PlcNumber = 1;
            this.optLeft_Profile_Recognition.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optLeft_Profile_Recognition.Size = new System.Drawing.Size(450, 38);
            this.optLeft_Profile_Recognition.TabIndex = 66;
            this.optLeft_Profile_Recognition.ToolTipCaption = null;
            // 
            // optSliding_Conveyor_Rollers
            // 
            this.optSliding_Conveyor_Rollers.Address = "L18";
            this.optSliding_Conveyor_Rollers.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optSliding_Conveyor_Rollers.ConfirmWrite = true;
            this.optSliding_Conveyor_Rollers.CornerRadius = 4;
            this.optSliding_Conveyor_Rollers.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optSliding_Conveyor_Rollers.DescriptionText = "Sliding Conveyor Rollers";
            this.optSliding_Conveyor_Rollers.EnableTrigger.DescriptionKey = "";
            this.optSliding_Conveyor_Rollers.EnableTrigger.MonitorAddress = "";
            this.optSliding_Conveyor_Rollers.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optSliding_Conveyor_Rollers.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optSliding_Conveyor_Rollers.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optSliding_Conveyor_Rollers.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optSliding_Conveyor_Rollers.EnableTrigger.ValA = 0D;
            this.optSliding_Conveyor_Rollers.EnableTrigger.ValB = 0D;
            this.optSliding_Conveyor_Rollers.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optSliding_Conveyor_Rollers.Location = new System.Drawing.Point(24, 252);
            this.optSliding_Conveyor_Rollers.LogAction = true;
            this.optSliding_Conveyor_Rollers.Name = "optSliding_Conveyor_Rollers";
            this.optSliding_Conveyor_Rollers.PlcNumber = 1;
            this.optSliding_Conveyor_Rollers.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optSliding_Conveyor_Rollers.Size = new System.Drawing.Size(450, 38);
            this.optSliding_Conveyor_Rollers.TabIndex = 67;
            this.optSliding_Conveyor_Rollers.ToolTipCaption = null;
            // 
            // optReverse_Machine
            // 
            this.optReverse_Machine.Address = "L37";
            this.optReverse_Machine.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optReverse_Machine.ConfirmWrite = true;
            this.optReverse_Machine.CornerRadius = 4;
            this.optReverse_Machine.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optReverse_Machine.DescriptionText = "Reverse Machine";
            this.optReverse_Machine.EnableTrigger.DescriptionKey = "";
            this.optReverse_Machine.EnableTrigger.MonitorAddress = "";
            this.optReverse_Machine.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optReverse_Machine.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optReverse_Machine.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optReverse_Machine.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optReverse_Machine.EnableTrigger.ValA = 0D;
            this.optReverse_Machine.EnableTrigger.ValB = 0D;
            this.optReverse_Machine.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optReverse_Machine.Location = new System.Drawing.Point(24, 290);
            this.optReverse_Machine.LogAction = true;
            this.optReverse_Machine.Name = "optReverse_Machine";
            this.optReverse_Machine.PlcNumber = 1;
            this.optReverse_Machine.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optReverse_Machine.Size = new System.Drawing.Size(450, 38);
            this.optReverse_Machine.TabIndex = 68;
            this.optReverse_Machine.ToolTipCaption = null;
            // 
            // optSpecial_Reverse_Machine
            // 
            this.optSpecial_Reverse_Machine.Address = "L25";
            this.optSpecial_Reverse_Machine.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optSpecial_Reverse_Machine.ConfirmWrite = true;
            this.optSpecial_Reverse_Machine.CornerRadius = 4;
            this.optSpecial_Reverse_Machine.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optSpecial_Reverse_Machine.DescriptionText = "Special Reverse Machine";
            this.optSpecial_Reverse_Machine.EnableTrigger.DescriptionKey = "";
            this.optSpecial_Reverse_Machine.EnableTrigger.MonitorAddress = "";
            this.optSpecial_Reverse_Machine.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optSpecial_Reverse_Machine.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optSpecial_Reverse_Machine.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optSpecial_Reverse_Machine.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optSpecial_Reverse_Machine.EnableTrigger.ValA = 0D;
            this.optSpecial_Reverse_Machine.EnableTrigger.ValB = 0D;
            this.optSpecial_Reverse_Machine.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optSpecial_Reverse_Machine.Location = new System.Drawing.Point(24, 328);
            this.optSpecial_Reverse_Machine.LogAction = true;
            this.optSpecial_Reverse_Machine.Name = "optSpecial_Reverse_Machine";
            this.optSpecial_Reverse_Machine.PlcNumber = 1;
            this.optSpecial_Reverse_Machine.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optSpecial_Reverse_Machine.Size = new System.Drawing.Size(450, 38);
            this.optSpecial_Reverse_Machine.TabIndex = 69;
            this.optSpecial_Reverse_Machine.ToolTipCaption = null;
            // 
            // optCutting_Right_Top_Clamping_Long
            // 
            this.optCutting_Right_Top_Clamping_Long.Address = "L28";
            this.optCutting_Right_Top_Clamping_Long.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optCutting_Right_Top_Clamping_Long.ConfirmWrite = true;
            this.optCutting_Right_Top_Clamping_Long.CornerRadius = 4;
            this.optCutting_Right_Top_Clamping_Long.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optCutting_Right_Top_Clamping_Long.DescriptionText = "Cutting Right Top Clamping Long";
            this.optCutting_Right_Top_Clamping_Long.EnableTrigger.DescriptionKey = "";
            this.optCutting_Right_Top_Clamping_Long.EnableTrigger.MonitorAddress = "";
            this.optCutting_Right_Top_Clamping_Long.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optCutting_Right_Top_Clamping_Long.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optCutting_Right_Top_Clamping_Long.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optCutting_Right_Top_Clamping_Long.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optCutting_Right_Top_Clamping_Long.EnableTrigger.ValA = 0D;
            this.optCutting_Right_Top_Clamping_Long.EnableTrigger.ValB = 0D;
            this.optCutting_Right_Top_Clamping_Long.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optCutting_Right_Top_Clamping_Long.Location = new System.Drawing.Point(24, 366);
            this.optCutting_Right_Top_Clamping_Long.LogAction = true;
            this.optCutting_Right_Top_Clamping_Long.Name = "optCutting_Right_Top_Clamping_Long";
            this.optCutting_Right_Top_Clamping_Long.PlcNumber = 1;
            this.optCutting_Right_Top_Clamping_Long.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optCutting_Right_Top_Clamping_Long.Size = new System.Drawing.Size(450, 38);
            this.optCutting_Right_Top_Clamping_Long.TabIndex = 70;
            this.optCutting_Right_Top_Clamping_Long.ToolTipCaption = null;
            // 
            // optMajor_Profile_Ejection_Tray_Operates_Individually
            // 
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.Address = "L29";
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.ConfirmWrite = true;
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.CornerRadius = 4;
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.DescriptionText = "Major Profile Ejection Tray Operates Individually";
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.EnableTrigger.DescriptionKey = "";
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.EnableTrigger.MonitorAddress = "";
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.EnableTrigger.ValA = 0D;
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.EnableTrigger.ValB = 0D;
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.Location = new System.Drawing.Point(24, 404);
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.LogAction = true;
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.Name = "optMajor_Profile_Ejection_Tray_Operates_Individually";
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.PlcNumber = 1;
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.Size = new System.Drawing.Size(450, 38);
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.TabIndex = 71;
            this.optMajor_Profile_Ejection_Tray_Operates_Individually.ToolTipCaption = null;
            // 
            // optTop_Clamping_Reclamp
            // 
            this.optTop_Clamping_Reclamp.Address = "L30";
            this.optTop_Clamping_Reclamp.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optTop_Clamping_Reclamp.ConfirmWrite = true;
            this.optTop_Clamping_Reclamp.CornerRadius = 4;
            this.optTop_Clamping_Reclamp.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optTop_Clamping_Reclamp.DescriptionText = "Top Clamping Reclamp";
            this.optTop_Clamping_Reclamp.EnableTrigger.DescriptionKey = "";
            this.optTop_Clamping_Reclamp.EnableTrigger.MonitorAddress = "";
            this.optTop_Clamping_Reclamp.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optTop_Clamping_Reclamp.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optTop_Clamping_Reclamp.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optTop_Clamping_Reclamp.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optTop_Clamping_Reclamp.EnableTrigger.ValA = 0D;
            this.optTop_Clamping_Reclamp.EnableTrigger.ValB = 0D;
            this.optTop_Clamping_Reclamp.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optTop_Clamping_Reclamp.Location = new System.Drawing.Point(24, 442);
            this.optTop_Clamping_Reclamp.LogAction = true;
            this.optTop_Clamping_Reclamp.Name = "optTop_Clamping_Reclamp";
            this.optTop_Clamping_Reclamp.PlcNumber = 1;
            this.optTop_Clamping_Reclamp.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optTop_Clamping_Reclamp.Size = new System.Drawing.Size(450, 38);
            this.optTop_Clamping_Reclamp.TabIndex = 72;
            this.optTop_Clamping_Reclamp.ToolTipCaption = null;
            // 
            // optGripper_0_90_Degree_Turn
            // 
            this.optGripper_0_90_Degree_Turn.Address = "L31";
            this.optGripper_0_90_Degree_Turn.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optGripper_0_90_Degree_Turn.ConfirmWrite = true;
            this.optGripper_0_90_Degree_Turn.CornerRadius = 4;
            this.optGripper_0_90_Degree_Turn.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optGripper_0_90_Degree_Turn.DescriptionText = "Gripper 0/90 Degree Turn";
            this.optGripper_0_90_Degree_Turn.EnableTrigger.DescriptionKey = "";
            this.optGripper_0_90_Degree_Turn.EnableTrigger.MonitorAddress = "";
            this.optGripper_0_90_Degree_Turn.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optGripper_0_90_Degree_Turn.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optGripper_0_90_Degree_Turn.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optGripper_0_90_Degree_Turn.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optGripper_0_90_Degree_Turn.EnableTrigger.ValA = 0D;
            this.optGripper_0_90_Degree_Turn.EnableTrigger.ValB = 0D;
            this.optGripper_0_90_Degree_Turn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optGripper_0_90_Degree_Turn.Location = new System.Drawing.Point(510, 260);
            this.optGripper_0_90_Degree_Turn.LogAction = true;
            this.optGripper_0_90_Degree_Turn.Name = "optGripper_0_90_Degree_Turn";
            this.optGripper_0_90_Degree_Turn.PlcNumber = 1;
            this.optGripper_0_90_Degree_Turn.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optGripper_0_90_Degree_Turn.Size = new System.Drawing.Size(450, 38);
            this.optGripper_0_90_Degree_Turn.TabIndex = 73;
            this.optGripper_0_90_Degree_Turn.ToolTipCaption = null;
            // 
            // optClamping_Mould
            // 
            this.optClamping_Mould.Address = "L27";
            this.optClamping_Mould.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optClamping_Mould.ConfirmWrite = true;
            this.optClamping_Mould.CornerRadius = 4;
            this.optClamping_Mould.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optClamping_Mould.DescriptionText = "Clamping Mould";
            this.optClamping_Mould.EnableTrigger.DescriptionKey = "";
            this.optClamping_Mould.EnableTrigger.MonitorAddress = "";
            this.optClamping_Mould.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optClamping_Mould.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optClamping_Mould.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optClamping_Mould.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optClamping_Mould.EnableTrigger.ValA = 0D;
            this.optClamping_Mould.EnableTrigger.ValB = 0D;
            this.optClamping_Mould.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optClamping_Mould.Location = new System.Drawing.Point(510, 298);
            this.optClamping_Mould.LogAction = true;
            this.optClamping_Mould.Name = "optClamping_Mould";
            this.optClamping_Mould.PlcNumber = 1;
            this.optClamping_Mould.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optClamping_Mould.Size = new System.Drawing.Size(450, 38);
            this.optClamping_Mould.TabIndex = 74;
            this.optClamping_Mould.ToolTipCaption = null;
            // 
            // optCross_Roller
            // 
            this.optCross_Roller.Address = "L32";
            this.optCross_Roller.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optCross_Roller.ConfirmWrite = true;
            this.optCross_Roller.CornerRadius = 4;
            this.optCross_Roller.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optCross_Roller.DescriptionText = "Cross Roller";
            this.optCross_Roller.EnableTrigger.DescriptionKey = "";
            this.optCross_Roller.EnableTrigger.MonitorAddress = "";
            this.optCross_Roller.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optCross_Roller.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optCross_Roller.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optCross_Roller.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optCross_Roller.EnableTrigger.ValA = 0D;
            this.optCross_Roller.EnableTrigger.ValB = 0D;
            this.optCross_Roller.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optCross_Roller.Location = new System.Drawing.Point(510, 336);
            this.optCross_Roller.LogAction = true;
            this.optCross_Roller.Name = "optCross_Roller";
            this.optCross_Roller.PlcNumber = 1;
            this.optCross_Roller.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optCross_Roller.Size = new System.Drawing.Size(450, 38);
            this.optCross_Roller.TabIndex = 75;
            this.optCross_Roller.ToolTipCaption = null;
            // 
            // optCutting_Left_Front_Encoder
            // 
            this.optCutting_Left_Front_Encoder.Address = "L33";
            this.optCutting_Left_Front_Encoder.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optCutting_Left_Front_Encoder.ConfirmWrite = true;
            this.optCutting_Left_Front_Encoder.CornerRadius = 4;
            this.optCutting_Left_Front_Encoder.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optCutting_Left_Front_Encoder.DescriptionText = "Cutting Left Front with Encoder";
            this.optCutting_Left_Front_Encoder.EnableTrigger.DescriptionKey = "";
            this.optCutting_Left_Front_Encoder.EnableTrigger.MonitorAddress = "";
            this.optCutting_Left_Front_Encoder.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optCutting_Left_Front_Encoder.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optCutting_Left_Front_Encoder.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optCutting_Left_Front_Encoder.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optCutting_Left_Front_Encoder.EnableTrigger.ValA = 0D;
            this.optCutting_Left_Front_Encoder.EnableTrigger.ValB = 0D;
            this.optCutting_Left_Front_Encoder.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optCutting_Left_Front_Encoder.Location = new System.Drawing.Point(510, 374);
            this.optCutting_Left_Front_Encoder.LogAction = true;
            this.optCutting_Left_Front_Encoder.Name = "optCutting_Left_Front_Encoder";
            this.optCutting_Left_Front_Encoder.PlcNumber = 1;
            this.optCutting_Left_Front_Encoder.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optCutting_Left_Front_Encoder.Size = new System.Drawing.Size(450, 38);
            this.optCutting_Left_Front_Encoder.TabIndex = 76;
            this.optCutting_Left_Front_Encoder.ToolTipCaption = null;
            // 
            // optCutting_Right_Front_Vertical_Without_Encoder
            // 
            this.optCutting_Right_Front_Vertical_Without_Encoder.Address = "L34";
            this.optCutting_Right_Front_Vertical_Without_Encoder.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optCutting_Right_Front_Vertical_Without_Encoder.ConfirmWrite = true;
            this.optCutting_Right_Front_Vertical_Without_Encoder.CornerRadius = 4;
            this.optCutting_Right_Front_Vertical_Without_Encoder.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optCutting_Right_Front_Vertical_Without_Encoder.DescriptionText = "Cutting Right Front Vertical without Encoder";
            this.optCutting_Right_Front_Vertical_Without_Encoder.EnableTrigger.DescriptionKey = "";
            this.optCutting_Right_Front_Vertical_Without_Encoder.EnableTrigger.MonitorAddress = "";
            this.optCutting_Right_Front_Vertical_Without_Encoder.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optCutting_Right_Front_Vertical_Without_Encoder.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optCutting_Right_Front_Vertical_Without_Encoder.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optCutting_Right_Front_Vertical_Without_Encoder.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optCutting_Right_Front_Vertical_Without_Encoder.EnableTrigger.ValA = 0D;
            this.optCutting_Right_Front_Vertical_Without_Encoder.EnableTrigger.ValB = 0D;
            this.optCutting_Right_Front_Vertical_Without_Encoder.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optCutting_Right_Front_Vertical_Without_Encoder.Location = new System.Drawing.Point(510, 412);
            this.optCutting_Right_Front_Vertical_Without_Encoder.LogAction = true;
            this.optCutting_Right_Front_Vertical_Without_Encoder.Name = "optCutting_Right_Front_Vertical_Without_Encoder";
            this.optCutting_Right_Front_Vertical_Without_Encoder.PlcNumber = 1;
            this.optCutting_Right_Front_Vertical_Without_Encoder.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optCutting_Right_Front_Vertical_Without_Encoder.Size = new System.Drawing.Size(450, 38);
            this.optCutting_Right_Front_Vertical_Without_Encoder.TabIndex = 77;
            this.optCutting_Right_Front_Vertical_Without_Encoder.ToolTipCaption = null;
            // 
            // optCutting_Left_Front_Air_Exhaust
            // 
            this.optCutting_Left_Front_Air_Exhaust.Address = "L35";
            this.optCutting_Left_Front_Air_Exhaust.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optCutting_Left_Front_Air_Exhaust.ConfirmWrite = true;
            this.optCutting_Left_Front_Air_Exhaust.CornerRadius = 4;
            this.optCutting_Left_Front_Air_Exhaust.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optCutting_Left_Front_Air_Exhaust.DescriptionText = "Cutting Left Front with Air Exhaust";
            this.optCutting_Left_Front_Air_Exhaust.EnableTrigger.DescriptionKey = "";
            this.optCutting_Left_Front_Air_Exhaust.EnableTrigger.MonitorAddress = "";
            this.optCutting_Left_Front_Air_Exhaust.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optCutting_Left_Front_Air_Exhaust.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optCutting_Left_Front_Air_Exhaust.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optCutting_Left_Front_Air_Exhaust.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optCutting_Left_Front_Air_Exhaust.EnableTrigger.ValA = 0D;
            this.optCutting_Left_Front_Air_Exhaust.EnableTrigger.ValB = 0D;
            this.optCutting_Left_Front_Air_Exhaust.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optCutting_Left_Front_Air_Exhaust.Location = new System.Drawing.Point(510, 450);
            this.optCutting_Left_Front_Air_Exhaust.LogAction = true;
            this.optCutting_Left_Front_Air_Exhaust.Name = "optCutting_Left_Front_Air_Exhaust";
            this.optCutting_Left_Front_Air_Exhaust.PlcNumber = 1;
            this.optCutting_Left_Front_Air_Exhaust.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optCutting_Left_Front_Air_Exhaust.Size = new System.Drawing.Size(450, 38);
            this.optCutting_Left_Front_Air_Exhaust.TabIndex = 78;
            this.optCutting_Left_Front_Air_Exhaust.ToolTipCaption = null;
            // 
            // groupBoxMullionNotching
            // 
            this.groupBoxMullionNotching.Address = null;
            this.groupBoxMullionNotching.BackColor = System.Drawing.Color.White;
            this.groupBoxMullionNotching.Controls.Add(this.checkBoxWithBladeBackFinishCut);
            this.groupBoxMullionNotching.Controls.Add(this.checkBoxWithBladeLeftBackFinishCut);
            this.groupBoxMullionNotching.Controls.Add(this.checkBoxWithBladeSingleSideCut);
            this.groupBoxMullionNotching.Controls.Add(this.optVithBlade);
            this.groupBoxMullionNotching.Controls.Add(this.optMullionNotching);
            this.groupBoxMullionNotching.Emoji = "";
            this.groupBoxMullionNotching.EnableAddress = "";
            this.groupBoxMullionNotching.EnableConditions = Kaban.Core.Enums.SystemCondition.None;
            this.groupBoxMullionNotching.Location = new System.Drawing.Point(498, 16);
            this.groupBoxMullionNotching.Name = "groupBoxMullionNotching";
            this.groupBoxMullionNotching.Padding = new System.Windows.Forms.Padding(10, 22, 10, 8);
            this.groupBoxMullionNotching.PlcNumber = 1;
            this.groupBoxMullionNotching.Size = new System.Drawing.Size(474, 228);
            this.groupBoxMullionNotching.TabIndex = 77;
            this.groupBoxMullionNotching.TitleText = "Mullion Notching";
            this.groupBoxMullionNotching.TitleTextKey = "Mullion Notching";
            this.groupBoxMullionNotching.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            this.groupBoxMullionNotching.VisibleAddress = "";
            // 
            // checkBoxWithBladeBackFinishCut
            // 
            this.checkBoxWithBladeBackFinishCut.Address = "L47";
            this.checkBoxWithBladeBackFinishCut.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.checkBoxWithBladeBackFinishCut.ConfirmWrite = true;
            this.checkBoxWithBladeBackFinishCut.CornerRadius = 4;
            this.checkBoxWithBladeBackFinishCut.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.checkBoxWithBladeBackFinishCut.DescriptionText = "with Blade Back Finish Cut";
            this.checkBoxWithBladeBackFinishCut.EnableTrigger.DescriptionKey = "";
            this.checkBoxWithBladeBackFinishCut.EnableTrigger.MonitorAddress = "L46";
            this.checkBoxWithBladeBackFinishCut.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.checkBoxWithBladeBackFinishCut.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.checkBoxWithBladeBackFinishCut.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.checkBoxWithBladeBackFinishCut.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.ON;
            this.checkBoxWithBladeBackFinishCut.EnableTrigger.ValA = 0D;
            this.checkBoxWithBladeBackFinishCut.EnableTrigger.ValB = 0D;
            this.checkBoxWithBladeBackFinishCut.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.checkBoxWithBladeBackFinishCut.Location = new System.Drawing.Point(12, 106);
            this.checkBoxWithBladeBackFinishCut.LogAction = true;
            this.checkBoxWithBladeBackFinishCut.Name = "checkBoxWithBladeBackFinishCut";
            this.checkBoxWithBladeBackFinishCut.PlcNumber = 1;
            this.checkBoxWithBladeBackFinishCut.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.checkBoxWithBladeBackFinishCut.Size = new System.Drawing.Size(450, 38);
            this.checkBoxWithBladeBackFinishCut.TabIndex = 70;
            this.checkBoxWithBladeBackFinishCut.ToolTipCaption = null;
            // 
            // checkBoxWithBladeLeftBackFinishCut
            // 
            this.checkBoxWithBladeLeftBackFinishCut.Address = "L48";
            this.checkBoxWithBladeLeftBackFinishCut.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.checkBoxWithBladeLeftBackFinishCut.ConfirmWrite = true;
            this.checkBoxWithBladeLeftBackFinishCut.CornerRadius = 4;
            this.checkBoxWithBladeLeftBackFinishCut.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.checkBoxWithBladeLeftBackFinishCut.DescriptionText = "with Blade Left Back Finish Cut";
            this.checkBoxWithBladeLeftBackFinishCut.EnableTrigger.DescriptionKey = "";
            this.checkBoxWithBladeLeftBackFinishCut.EnableTrigger.MonitorAddress = "L46";
            this.checkBoxWithBladeLeftBackFinishCut.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.checkBoxWithBladeLeftBackFinishCut.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.checkBoxWithBladeLeftBackFinishCut.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.checkBoxWithBladeLeftBackFinishCut.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.ON;
            this.checkBoxWithBladeLeftBackFinishCut.EnableTrigger.ValA = 0D;
            this.checkBoxWithBladeLeftBackFinishCut.EnableTrigger.ValB = 0D;
            this.checkBoxWithBladeLeftBackFinishCut.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.checkBoxWithBladeLeftBackFinishCut.Location = new System.Drawing.Point(12, 144);
            this.checkBoxWithBladeLeftBackFinishCut.LogAction = true;
            this.checkBoxWithBladeLeftBackFinishCut.Name = "checkBoxWithBladeLeftBackFinishCut";
            this.checkBoxWithBladeLeftBackFinishCut.PlcNumber = 1;
            this.checkBoxWithBladeLeftBackFinishCut.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.checkBoxWithBladeLeftBackFinishCut.Size = new System.Drawing.Size(450, 38);
            this.checkBoxWithBladeLeftBackFinishCut.TabIndex = 68;
            this.checkBoxWithBladeLeftBackFinishCut.ToolTipCaption = null;
            // 
            // checkBoxWithBladeSingleSideCut
            // 
            this.checkBoxWithBladeSingleSideCut.Address = "L49";
            this.checkBoxWithBladeSingleSideCut.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.checkBoxWithBladeSingleSideCut.ConfirmWrite = true;
            this.checkBoxWithBladeSingleSideCut.CornerRadius = 4;
            this.checkBoxWithBladeSingleSideCut.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.checkBoxWithBladeSingleSideCut.DescriptionText = "with Blade Single Side Cut";
            this.checkBoxWithBladeSingleSideCut.EnableTrigger.DescriptionKey = "";
            this.checkBoxWithBladeSingleSideCut.EnableTrigger.MonitorAddress = "L46";
            this.checkBoxWithBladeSingleSideCut.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.checkBoxWithBladeSingleSideCut.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.checkBoxWithBladeSingleSideCut.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.checkBoxWithBladeSingleSideCut.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.ON;
            this.checkBoxWithBladeSingleSideCut.EnableTrigger.ValA = 0D;
            this.checkBoxWithBladeSingleSideCut.EnableTrigger.ValB = 0D;
            this.checkBoxWithBladeSingleSideCut.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.checkBoxWithBladeSingleSideCut.Location = new System.Drawing.Point(12, 182);
            this.checkBoxWithBladeSingleSideCut.LogAction = true;
            this.checkBoxWithBladeSingleSideCut.Name = "checkBoxWithBladeSingleSideCut";
            this.checkBoxWithBladeSingleSideCut.PlcNumber = 1;
            this.checkBoxWithBladeSingleSideCut.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.checkBoxWithBladeSingleSideCut.Size = new System.Drawing.Size(450, 38);
            this.checkBoxWithBladeSingleSideCut.TabIndex = 66;
            this.checkBoxWithBladeSingleSideCut.ToolTipCaption = null;
            // 
            // optVithBlade
            // 
            this.optVithBlade.Address = "L46";
            this.optVithBlade.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optVithBlade.ConfirmWrite = true;
            this.optVithBlade.CornerRadius = 4;
            this.optVithBlade.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optVithBlade.DescriptionText = "with Blade";
            this.optVithBlade.EnableConditions = Kaban.Core.Enums.SystemCondition.MachineBit20;
            this.optVithBlade.EnableTrigger.DescriptionKey = "";
            this.optVithBlade.EnableTrigger.MonitorAddress = "";
            this.optVithBlade.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optVithBlade.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optVithBlade.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optVithBlade.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optVithBlade.EnableTrigger.ValA = 0D;
            this.optVithBlade.EnableTrigger.ValB = 0D;
            this.optVithBlade.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optVithBlade.Location = new System.Drawing.Point(12, 68);
            this.optVithBlade.LogAction = true;
            this.optVithBlade.Name = "optVithBlade";
            this.optVithBlade.PlcNumber = 1;
            this.optVithBlade.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optVithBlade.Size = new System.Drawing.Size(450, 38);
            this.optVithBlade.TabIndex = 64;
            this.optVithBlade.ToolTipCaption = null;
            // 
            // optMullionNotching
            // 
            this.optMullionNotching.Address = "L5";
            this.optMullionNotching.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.optMullionNotching.ConfirmWrite = true;
            this.optMullionNotching.CornerRadius = 4;
            this.optMullionNotching.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.optMullionNotching.DescriptionText = "Mullion Notching";
            this.optMullionNotching.EnableTrigger.DescriptionKey = "";
            this.optMullionNotching.EnableTrigger.MonitorAddress = "";
            this.optMullionNotching.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.optMullionNotching.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.optMullionNotching.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.optMullionNotching.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.optMullionNotching.EnableTrigger.ValA = 0D;
            this.optMullionNotching.EnableTrigger.ValB = 0D;
            this.optMullionNotching.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.optMullionNotching.Location = new System.Drawing.Point(12, 30);
            this.optMullionNotching.LogAction = true;
            this.optMullionNotching.Name = "optMullionNotching";
            this.optMullionNotching.PlcNumber = 1;
            this.optMullionNotching.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.optMullionNotching.Size = new System.Drawing.Size(450, 38);
            this.optMullionNotching.TabIndex = 62;
            this.optMullionNotching.ToolTipCaption = null;
            // 
            // grpIndustry4Logging  (5 November: the records the PLC logs and the machine's log settings - shown while the
            // HMI does the Industry 4 logging; the first three always logged: the HMI switches them on)
            // 
            this.grpIndustry4Logging.Address = null;
            this.grpIndustry4Logging.BackColor = System.Drawing.Color.White;
            this.grpIndustry4Logging.Controls.Add(this.logMachine_Closed);
            this.grpIndustry4Logging.Controls.Add(this.logOperation_Not_Active);
            this.grpIndustry4Logging.Controls.Add(this.logOperation_Active);
            this.grpIndustry4Logging.Controls.Add(this.logPause_Mode);
            this.grpIndustry4Logging.Controls.Add(this.logError);
            this.grpIndustry4Logging.Controls.Add(this.logEmergency_Stop);
            this.grpIndustry4Logging.Controls.Add(this.logProfile_Changed);
            this.grpIndustry4Logging.Controls.Add(this.logCutting_Done);
            this.grpIndustry4Logging.Controls.Add(this.logWaste_Profile_Emerged);
            this.grpIndustry4Logging.Controls.Add(this.logBar_Length_Measured);
            this.grpIndustry4Logging.Controls.Add(this.logProfile_Not_Recognised);
            this.grpIndustry4Logging.Controls.Add(this.logProfile_Measured);
            this.grpIndustry4Logging.Controls.Add(this.recProfile_Changed);
            this.grpIndustry4Logging.Controls.Add(this.recWaste_Profile_Emerged);
            this.grpIndustry4Logging.Controls.Add(this.recBar_Length_Measured);
            this.grpIndustry4Logging.Controls.Add(this.recProfile_Not_Recognised);
            this.grpIndustry4Logging.Controls.Add(this.recProfile_Measured);
            this.grpIndustry4Logging.Controls.Add(this.industry4SettingsPlace);
            this.grpIndustry4Logging.Emoji = "";
            this.grpIndustry4Logging.EnableAddress = "";
            this.grpIndustry4Logging.EnableConditions = Kaban.Core.Enums.SystemCondition.None;
            this.grpIndustry4Logging.Location = new System.Drawing.Point(996, 16);
            this.grpIndustry4Logging.Name = "grpIndustry4Logging";
            this.grpIndustry4Logging.Padding = new System.Windows.Forms.Padding(10, 22, 10, 8);
            this.grpIndustry4Logging.PlcNumber = 1;
            this.grpIndustry4Logging.Size = new System.Drawing.Size(474, 912);
            this.grpIndustry4Logging.TabIndex = 79;
            this.grpIndustry4Logging.TitleText = "Industry 4 Logging";
            this.grpIndustry4Logging.TitleTextKey = "Industry 4 Logging";
            this.grpIndustry4Logging.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            this.grpIndustry4Logging.VisibleAddress = "";
            // 
            // industry4SettingsPlace  (where the machine's log settings go - put there by the page when it is made: its
            // PLC log area, the logger's state, the KMSystem connection)
            // 
            this.industry4SettingsPlace.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(246)))), ((int)(((byte)(252)))));
            this.industry4SettingsPlace.Location = new System.Drawing.Point(12, 684);
            this.industry4SettingsPlace.Name = "industry4SettingsPlace";
            this.industry4SettingsPlace.Size = new System.Drawing.Size(450, 220);
            this.industry4SettingsPlace.TabIndex = 92;
            // 
            // logMachine_Closed  (L1000: the PLC logs code 0 while it is on)
            // 
            this.logMachine_Closed.Address = "L1000";
            this.logMachine_Closed.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.logMachine_Closed.CornerRadius = 4;
            this.logMachine_Closed.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.logMachine_Closed.DescriptionText = "The machine is closed";
            this.logMachine_Closed.DisplayOnly = true;
            this.logMachine_Closed.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.logMachine_Closed.Location = new System.Drawing.Point(12, 30);
            this.logMachine_Closed.LogAction = true;
            this.logMachine_Closed.Name = "logMachine_Closed";
            this.logMachine_Closed.PlcNumber = 1;
            this.logMachine_Closed.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.logMachine_Closed.Size = new System.Drawing.Size(450, 38);
            this.logMachine_Closed.TabIndex = 80;
            this.logMachine_Closed.ToolTipCaption = null;
            // 
            // logOperation_Not_Active  (L1001: the PLC logs code 1 while it is on)
            // 
            this.logOperation_Not_Active.Address = "L1001";
            this.logOperation_Not_Active.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.logOperation_Not_Active.CornerRadius = 4;
            this.logOperation_Not_Active.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.logOperation_Not_Active.DescriptionText = "Operation is not active";
            this.logOperation_Not_Active.DisplayOnly = true;
            this.logOperation_Not_Active.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.logOperation_Not_Active.Location = new System.Drawing.Point(12, 68);
            this.logOperation_Not_Active.LogAction = true;
            this.logOperation_Not_Active.Name = "logOperation_Not_Active";
            this.logOperation_Not_Active.PlcNumber = 1;
            this.logOperation_Not_Active.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.logOperation_Not_Active.Size = new System.Drawing.Size(450, 38);
            this.logOperation_Not_Active.TabIndex = 81;
            this.logOperation_Not_Active.ToolTipCaption = null;
            // 
            // logOperation_Active  (L1002: the PLC logs code 2 while it is on)
            // 
            this.logOperation_Active.Address = "L1002";
            this.logOperation_Active.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.logOperation_Active.CornerRadius = 4;
            this.logOperation_Active.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.logOperation_Active.DescriptionText = "Operation is active";
            this.logOperation_Active.DisplayOnly = true;
            this.logOperation_Active.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.logOperation_Active.Location = new System.Drawing.Point(12, 106);
            this.logOperation_Active.LogAction = true;
            this.logOperation_Active.Name = "logOperation_Active";
            this.logOperation_Active.PlcNumber = 1;
            this.logOperation_Active.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.logOperation_Active.Size = new System.Drawing.Size(450, 38);
            this.logOperation_Active.TabIndex = 82;
            this.logOperation_Active.ToolTipCaption = null;
            // 
            // logPause_Mode  (L1003: the PLC logs code 3 while it is on)
            // 
            this.logPause_Mode.Address = "L1003";
            this.logPause_Mode.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.logPause_Mode.CornerRadius = 4;
            this.logPause_Mode.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.logPause_Mode.DescriptionText = "On pause mode";
            this.logPause_Mode.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.logPause_Mode.Location = new System.Drawing.Point(12, 144);
            this.logPause_Mode.LogAction = true;
            this.logPause_Mode.Name = "logPause_Mode";
            this.logPause_Mode.PlcNumber = 1;
            this.logPause_Mode.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.logPause_Mode.Size = new System.Drawing.Size(450, 38);
            this.logPause_Mode.TabIndex = 83;
            this.logPause_Mode.ToolTipCaption = null;
            // 
            // logError  (L1004: the PLC logs code 4 while it is on)
            // 
            this.logError.Address = "L1004";
            this.logError.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.logError.CornerRadius = 4;
            this.logError.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.logError.DescriptionText = "There is an error";
            this.logError.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.logError.Location = new System.Drawing.Point(12, 182);
            this.logError.LogAction = true;
            this.logError.Name = "logError";
            this.logError.PlcNumber = 1;
            this.logError.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.logError.Size = new System.Drawing.Size(450, 38);
            this.logError.TabIndex = 84;
            this.logError.ToolTipCaption = null;
            // 
            // logEmergency_Stop  (L1005: the PLC logs code 5 while it is on)
            // 
            this.logEmergency_Stop.Address = "L1005";
            this.logEmergency_Stop.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.logEmergency_Stop.CornerRadius = 4;
            this.logEmergency_Stop.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.logEmergency_Stop.DescriptionText = "Emergency stop";
            this.logEmergency_Stop.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.logEmergency_Stop.Location = new System.Drawing.Point(12, 220);
            this.logEmergency_Stop.LogAction = true;
            this.logEmergency_Stop.Name = "logEmergency_Stop";
            this.logEmergency_Stop.PlcNumber = 1;
            this.logEmergency_Stop.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.logEmergency_Stop.Size = new System.Drawing.Size(450, 38);
            this.logEmergency_Stop.TabIndex = 85;
            this.logEmergency_Stop.ToolTipCaption = null;
            // 
            // logProfile_Changed  (L1006: the PLC logs code 6 while it is on)
            // 
            this.logProfile_Changed.Address = "L1006";
            this.logProfile_Changed.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.logProfile_Changed.CornerRadius = 4;
            this.logProfile_Changed.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.logProfile_Changed.DescriptionText = "Profile changed";
            this.logProfile_Changed.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.logProfile_Changed.Location = new System.Drawing.Point(12, 258);
            this.logProfile_Changed.LogAction = true;
            this.logProfile_Changed.Name = "logProfile_Changed";
            this.logProfile_Changed.PlcNumber = 1;
            this.logProfile_Changed.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.logProfile_Changed.Size = new System.Drawing.Size(450, 38);
            this.logProfile_Changed.TabIndex = 86;
            this.logProfile_Changed.ToolTipCaption = null;
            // 
            // logCutting_Done  (L1008: the PLC logs code 8 while it is on)
            // 
            this.logCutting_Done.Address = "L1008";
            this.logCutting_Done.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.logCutting_Done.CornerRadius = 4;
            this.logCutting_Done.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.logCutting_Done.DescriptionText = "Cutting done";
            this.logCutting_Done.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.logCutting_Done.Location = new System.Drawing.Point(12, 296);
            this.logCutting_Done.LogAction = true;
            this.logCutting_Done.Name = "logCutting_Done";
            this.logCutting_Done.PlcNumber = 1;
            this.logCutting_Done.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.logCutting_Done.Size = new System.Drawing.Size(450, 38);
            this.logCutting_Done.TabIndex = 87;
            this.logCutting_Done.ToolTipCaption = null;
            // 
            // logWaste_Profile_Emerged  (L1015: the PLC logs code 15 while it is on)
            // 
            this.logWaste_Profile_Emerged.Address = "L1015";
            this.logWaste_Profile_Emerged.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.logWaste_Profile_Emerged.CornerRadius = 4;
            this.logWaste_Profile_Emerged.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.logWaste_Profile_Emerged.DescriptionText = "Waste profile emerged";
            this.logWaste_Profile_Emerged.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.logWaste_Profile_Emerged.Location = new System.Drawing.Point(12, 334);
            this.logWaste_Profile_Emerged.LogAction = true;
            this.logWaste_Profile_Emerged.Name = "logWaste_Profile_Emerged";
            this.logWaste_Profile_Emerged.PlcNumber = 1;
            this.logWaste_Profile_Emerged.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.logWaste_Profile_Emerged.Size = new System.Drawing.Size(450, 38);
            this.logWaste_Profile_Emerged.TabIndex = 88;
            this.logWaste_Profile_Emerged.ToolTipCaption = null;
            // 
            // logBar_Length_Measured  (L1016: the PLC logs code 16 while it is on)
            // 
            this.logBar_Length_Measured.Address = "L1016";
            this.logBar_Length_Measured.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.logBar_Length_Measured.CornerRadius = 4;
            this.logBar_Length_Measured.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.logBar_Length_Measured.DescriptionText = "Bar length measured";
            this.logBar_Length_Measured.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.logBar_Length_Measured.Location = new System.Drawing.Point(12, 372);
            this.logBar_Length_Measured.LogAction = true;
            this.logBar_Length_Measured.Name = "logBar_Length_Measured";
            this.logBar_Length_Measured.PlcNumber = 1;
            this.logBar_Length_Measured.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.logBar_Length_Measured.Size = new System.Drawing.Size(450, 38);
            this.logBar_Length_Measured.TabIndex = 89;
            this.logBar_Length_Measured.ToolTipCaption = null;
            // 
            // logProfile_Not_Recognised  (L1020: the PLC logs code 20 while it is on)
            // 
            this.logProfile_Not_Recognised.Address = "L1020";
            this.logProfile_Not_Recognised.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.logProfile_Not_Recognised.CornerRadius = 4;
            this.logProfile_Not_Recognised.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.logProfile_Not_Recognised.DescriptionText = "Profile not recognised";
            this.logProfile_Not_Recognised.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.logProfile_Not_Recognised.Location = new System.Drawing.Point(12, 410);
            this.logProfile_Not_Recognised.LogAction = true;
            this.logProfile_Not_Recognised.Name = "logProfile_Not_Recognised";
            this.logProfile_Not_Recognised.PlcNumber = 1;
            this.logProfile_Not_Recognised.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.logProfile_Not_Recognised.Size = new System.Drawing.Size(450, 38);
            this.logProfile_Not_Recognised.TabIndex = 90;
            this.logProfile_Not_Recognised.ToolTipCaption = null;
            // 
            // logProfile_Measured  (L1022: the PLC logs code 22 while it is on)
            // 
            this.logProfile_Measured.Address = "L1022";
            this.logProfile_Measured.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.logProfile_Measured.CornerRadius = 4;
            this.logProfile_Measured.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
            this.logProfile_Measured.DescriptionText = "Profile measured";
            this.logProfile_Measured.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.logProfile_Measured.Location = new System.Drawing.Point(12, 448);
            this.logProfile_Measured.LogAction = true;
            this.logProfile_Measured.Name = "logProfile_Measured";
            this.logProfile_Measured.PlcNumber = 1;
            this.logProfile_Measured.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
            this.logProfile_Measured.Size = new System.Drawing.Size(450, 38);
            this.logProfile_Measured.TabIndex = 91;
            this.logProfile_Measured.ToolTipCaption = null;
            // 
            // recProfile_Changed  (a new machine: code 6 when L1006 comes on - the record's device in MachineLog_DeviceList.xlsx; the profile's code: its device to be set)
            // 
            this.recProfile_Changed.BackColor = System.Drawing.Color.White;
            this.recProfile_Changed.Location = new System.Drawing.Point(12, 486);
            this.recProfile_Changed.Name = "recProfile_Changed";
            this.recProfile_Changed.PlcNumber = 1;
            this.recProfile_Changed.RecordCode = 6;
            this.recProfile_Changed.Size = new System.Drawing.Size(450, 38);
            this.recProfile_Changed.TabIndex = 96;
            this.recProfile_Changed.TextKey = "";
            this.recProfile_Changed.Trigger.DescriptionKey = "";
            this.recProfile_Changed.Trigger.MonitorAddress = "L1006";
            this.recProfile_Changed.Trigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.recProfile_Changed.Trigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.recProfile_Changed.Trigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.recProfile_Changed.Trigger.TriggerType = Kaban.Core.Enums.TriggerType.ON;
            this.recProfile_Changed.Trigger.ValA = 0D;
            this.recProfile_Changed.Trigger.ValB = 0D;
            this.recProfile_Changed.Values.Add(new Kaban.UI.Controls.RecordNumber("", "", Kaban.Core.Enums.DataTypeEnum.Bin_32, 1D));
            // 
            // recWaste_Profile_Emerged  (a new machine: code 15 when PC_FireBarkod_Bas (L24) comes, its waste length FireBoyu - its device to be set)
            // 
            this.recWaste_Profile_Emerged.BackColor = System.Drawing.Color.White;
            this.recWaste_Profile_Emerged.Location = new System.Drawing.Point(12, 524);
            this.recWaste_Profile_Emerged.Name = "recWaste_Profile_Emerged";
            this.recWaste_Profile_Emerged.PlcNumber = 1;
            this.recWaste_Profile_Emerged.RecordCode = 15;
            this.recWaste_Profile_Emerged.Size = new System.Drawing.Size(450, 38);
            this.recWaste_Profile_Emerged.TabIndex = 93;
            this.recWaste_Profile_Emerged.TextKey = "";
            this.recWaste_Profile_Emerged.Trigger.DescriptionKey = "";
            this.recWaste_Profile_Emerged.Trigger.MonitorAddress = "L24";
            this.recWaste_Profile_Emerged.Trigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.recWaste_Profile_Emerged.Trigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.recWaste_Profile_Emerged.Trigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.recWaste_Profile_Emerged.Trigger.TriggerType = Kaban.Core.Enums.TriggerType.ON;
            this.recWaste_Profile_Emerged.Trigger.ValA = 0D;
            this.recWaste_Profile_Emerged.Trigger.ValB = 0D;
            this.recWaste_Profile_Emerged.Values.Add(new Kaban.UI.Controls.RecordNumber("", "", Kaban.Core.Enums.DataTypeEnum.Bin_32, 1D));
            // 
            // recBar_Length_Measured  (a new machine: code 16 when BoyOkundu comes - its device to be set - the bar's length PC_ProfilBarBoyu (D89))
            // 
            this.recBar_Length_Measured.BackColor = System.Drawing.Color.White;
            this.recBar_Length_Measured.Location = new System.Drawing.Point(12, 562);
            this.recBar_Length_Measured.Name = "recBar_Length_Measured";
            this.recBar_Length_Measured.PlcNumber = 1;
            this.recBar_Length_Measured.RecordCode = 16;
            this.recBar_Length_Measured.Size = new System.Drawing.Size(450, 38);
            this.recBar_Length_Measured.TabIndex = 94;
            this.recBar_Length_Measured.TextKey = "";
            this.recBar_Length_Measured.Trigger.DescriptionKey = "";
            this.recBar_Length_Measured.Trigger.MonitorAddress = "";
            this.recBar_Length_Measured.Trigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.recBar_Length_Measured.Trigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.recBar_Length_Measured.Trigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.recBar_Length_Measured.Trigger.TriggerType = Kaban.Core.Enums.TriggerType.ON;
            this.recBar_Length_Measured.Trigger.ValA = 0D;
            this.recBar_Length_Measured.Trigger.ValB = 0D;
            this.recBar_Length_Measured.Values.Add(new Kaban.UI.Controls.RecordNumber("", "D89", Kaban.Core.Enums.DataTypeEnum.Bin_32, 1D));
            // 
            // recProfile_Not_Recognised  (a new machine: code 20 when L1020 comes on - the record's device in MachineLog_DeviceList.xlsx; the measured width and height D481 / D482; the required switch state: its device to be set)
            // 
            this.recProfile_Not_Recognised.BackColor = System.Drawing.Color.White;
            this.recProfile_Not_Recognised.Location = new System.Drawing.Point(12, 600);
            this.recProfile_Not_Recognised.Name = "recProfile_Not_Recognised";
            this.recProfile_Not_Recognised.PlcNumber = 1;
            this.recProfile_Not_Recognised.RecordCode = 20;
            this.recProfile_Not_Recognised.Size = new System.Drawing.Size(450, 38);
            this.recProfile_Not_Recognised.TabIndex = 97;
            this.recProfile_Not_Recognised.TextKey = "";
            this.recProfile_Not_Recognised.Trigger.DescriptionKey = "";
            this.recProfile_Not_Recognised.Trigger.MonitorAddress = "L1020";
            this.recProfile_Not_Recognised.Trigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.recProfile_Not_Recognised.Trigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.recProfile_Not_Recognised.Trigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.recProfile_Not_Recognised.Trigger.TriggerType = Kaban.Core.Enums.TriggerType.ON;
            this.recProfile_Not_Recognised.Trigger.ValA = 0D;
            this.recProfile_Not_Recognised.Trigger.ValB = 0D;
            this.recProfile_Not_Recognised.Values.Add(new Kaban.UI.Controls.RecordNumber("", "D481", Kaban.Core.Enums.DataTypeEnum.Bin_16, 1D));
            this.recProfile_Not_Recognised.Values.Add(new Kaban.UI.Controls.RecordNumber("", "D482", Kaban.Core.Enums.DataTypeEnum.Bin_16, 1D));
            this.recProfile_Not_Recognised.Values.Add(new Kaban.UI.Controls.RecordNumber("", "", Kaban.Core.Enums.DataTypeEnum.Bin_16, 1D));
            // 
            // recProfile_Measured  (a new machine: code 22 when ProfilOlcum_OK comes - its device to be set; the measured width and height
            // Profil_OkunanGenislik / Profil_OkunanYukseklik: D481 / D482, "Okunan Profil Genişliği / Yüksekliği" in FA2010_DeviceList.xls; Profil_Dogru: its device to be set)
            // 
            this.recProfile_Measured.BackColor = System.Drawing.Color.White;
            this.recProfile_Measured.Location = new System.Drawing.Point(12, 638);
            this.recProfile_Measured.Name = "recProfile_Measured";
            this.recProfile_Measured.PlcNumber = 1;
            this.recProfile_Measured.RecordCode = 22;
            this.recProfile_Measured.Size = new System.Drawing.Size(450, 38);
            this.recProfile_Measured.TabIndex = 95;
            this.recProfile_Measured.TextKey = "";
            this.recProfile_Measured.Trigger.DescriptionKey = "";
            this.recProfile_Measured.Trigger.MonitorAddress = "";
            this.recProfile_Measured.Trigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.recProfile_Measured.Trigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.recProfile_Measured.Trigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.recProfile_Measured.Trigger.TriggerType = Kaban.Core.Enums.TriggerType.ON;
            this.recProfile_Measured.Trigger.ValA = 0D;
            this.recProfile_Measured.Trigger.ValB = 0D;
            this.recProfile_Measured.Values.Add(new Kaban.UI.Controls.RecordNumber("", "D481", Kaban.Core.Enums.DataTypeEnum.Bin_16, 1D));
            this.recProfile_Measured.Values.Add(new Kaban.UI.Controls.RecordNumber("", "D482", Kaban.Core.Enums.DataTypeEnum.Bin_16, 1D));
            this.recProfile_Measured.Values.Add(new Kaban.UI.Controls.RecordLamp("", ""));
            // 
            // Options
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.Controls.Add(this.grpIndustry4Logging);
            this.Controls.Add(this.groupBoxMullionNotching);
            this.Controls.Add(this.optCooling);
            this.Controls.Add(this.optProfile_Carrying_Roller_Sensor);
            this.Controls.Add(this.optProfile_Recog_Carrying_Roller);
            this.Controls.Add(this.optMilling_Carrying_Roller);
            this.Controls.Add(this.optExit_Carrying_Roller);
            this.Controls.Add(this.optLeft_Profile_Recognition);
            this.Controls.Add(this.optSliding_Conveyor_Rollers);
            this.Controls.Add(this.optReverse_Machine);
            this.Controls.Add(this.optSpecial_Reverse_Machine);
            this.Controls.Add(this.optCutting_Right_Top_Clamping_Long);
            this.Controls.Add(this.optMajor_Profile_Ejection_Tray_Operates_Individually);
            this.Controls.Add(this.optTop_Clamping_Reclamp);
            this.Controls.Add(this.optGripper_0_90_Degree_Turn);
            this.Controls.Add(this.optClamping_Mould);
            this.Controls.Add(this.optCross_Roller);
            this.Controls.Add(this.optCutting_Left_Front_Encoder);
            this.Controls.Add(this.optCutting_Right_Front_Vertical_Without_Encoder);
            this.Controls.Add(this.optCutting_Left_Front_Air_Exhaust);
            this.Name = "Options";
            this.Size = new System.Drawing.Size(1494, 754);
            this.groupBoxMullionNotching.ResumeLayout(false);
            this.grpIndustry4Logging.ResumeLayout(false);
            this.groupBoxMullionNotching.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private UI.Controls.CheckBoxHMI optCooling;
        private UI.Controls.CheckBoxHMI optProfile_Carrying_Roller_Sensor;
        private UI.Controls.CheckBoxHMI optProfile_Recog_Carrying_Roller;
        private UI.Controls.CheckBoxHMI optMilling_Carrying_Roller;
        private UI.Controls.CheckBoxHMI optExit_Carrying_Roller;
        private UI.Controls.CheckBoxHMI optLeft_Profile_Recognition;
        private UI.Controls.CheckBoxHMI optSliding_Conveyor_Rollers;
        private UI.Controls.CheckBoxHMI optReverse_Machine;
        private UI.Controls.CheckBoxHMI optSpecial_Reverse_Machine;
        private UI.Controls.CheckBoxHMI optCutting_Right_Top_Clamping_Long;
        private UI.Controls.CheckBoxHMI optMajor_Profile_Ejection_Tray_Operates_Individually;
        private UI.Controls.CheckBoxHMI optTop_Clamping_Reclamp;
        private UI.Controls.CheckBoxHMI optGripper_0_90_Degree_Turn;
        private UI.Controls.CheckBoxHMI optClamping_Mould;
        private UI.Controls.CheckBoxHMI optCross_Roller;
        private UI.Controls.CheckBoxHMI optCutting_Left_Front_Encoder;
        private UI.Controls.CheckBoxHMI optCutting_Right_Front_Vertical_Without_Encoder;
        private UI.Controls.CheckBoxHMI optCutting_Left_Front_Air_Exhaust;
        private UI.Controls.GroupBoxHMI groupBoxMullionNotching;
        private UI.Controls.CheckBoxHMI optVithBlade;
        private UI.Controls.CheckBoxHMI optMullionNotching;
        private UI.Controls.CheckBoxHMI checkBoxWithBladeBackFinishCut;
        private UI.Controls.CheckBoxHMI checkBoxWithBladeLeftBackFinishCut;
        private UI.Controls.CheckBoxHMI checkBoxWithBladeSingleSideCut;
        private UI.Controls.CheckBoxHMI logMachine_Closed;
        private UI.Controls.CheckBoxHMI logOperation_Not_Active;
        private UI.Controls.CheckBoxHMI logOperation_Active;
        private UI.Controls.CheckBoxHMI logPause_Mode;
        private UI.Controls.CheckBoxHMI logError;
        private UI.Controls.CheckBoxHMI logEmergency_Stop;
        private UI.Controls.CheckBoxHMI logProfile_Changed;
        private UI.Controls.CheckBoxHMI logCutting_Done;
        private UI.Controls.CheckBoxHMI logWaste_Profile_Emerged;
        private UI.Controls.CheckBoxHMI logBar_Length_Measured;
        private UI.Controls.CheckBoxHMI logProfile_Not_Recognised;
        private UI.Controls.CheckBoxHMI logProfile_Measured;
        private UI.Controls.LogRecordHMI recProfile_Changed;
        private UI.Controls.LogRecordHMI recWaste_Profile_Emerged;
        private UI.Controls.LogRecordHMI recBar_Length_Measured;
        private UI.Controls.LogRecordHMI recProfile_Not_Recognised;
        private UI.Controls.LogRecordHMI recProfile_Measured;
        private UI.Controls.GroupBoxHMI grpIndustry4Logging;
        private System.Windows.Forms.Panel industry4SettingsPlace;
    }
}
