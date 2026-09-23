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
        // COMPONENT SHOWCASE - one of every HMI control, with its properties
        // filled in, as a live test sample.
        //
        // PLC ROUTING (this is the multi-PLC test):
        //   left column   -> PlcNumber = 1
        //   right column  -> PlcNumber = 2
        // Both paths are visible side by side on one screen. WHICH driver each one
        // talks to is a setting (HMI Settings -> Station Connection), so do not
        // name a vendor here - watch either in the PLC Monitor (F12).
        // ===================================================================
        private void InitializeComponent()
        {
            this.lblPlc1 = new Kaban.UI.Controls.LabelHMI();
            this.lblPlc2 = new Kaban.UI.Controls.LabelHMI();
            this.btnDemo = new Kaban.UI.Controls.ButtonHMI();
            this.lampDemo = new Kaban.UI.Controls.LampHMI();
            this.numDisplayDemo = new Kaban.UI.Controls.NumericDisplayHMI();
            this.numInputDemo = new Kaban.UI.Controls.NumericInputHMI();
            this.funcUnitDemo = new Kaban.UI.Controls.FunctionUnitHMI();
            this.btnDemo2 = new Kaban.UI.Controls.ButtonHMI();
            this.lampDemo2 = new Kaban.UI.Controls.LampHMI();
            this.numDisplayDemo2 = new Kaban.UI.Controls.NumericDisplayHMI();
            this.numInputDemo2 = new Kaban.UI.Controls.NumericInputHMI();
            this.funcUnitDemo2 = new Kaban.UI.Controls.FunctionUnitHMI();
            this.displayPlcDemo = new Kaban.UI.Controls.DisplayPLC();
            this.hexBoxDemo = new Kaban.UI.Controls.HexTextBox();
            this.txtDemo = new Kaban.UI.Controls.CustomTextBox();
            this.SuspendLayout();

            // ---------------- column headers ----------------
            this.lblPlc1.AutoSize = true;
            this.lblPlc1.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblPlc1.Location = new System.Drawing.Point(20, 12);
            this.lblPlc1.Name = "lblPlc1";
            this.lblPlc1.Text = "PLC 1";
            this.lblPlc1.TranslationKey = "Lbl_Plc1Header";

            this.lblPlc2.AutoSize = true;
            this.lblPlc2.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
            this.lblPlc2.Location = new System.Drawing.Point(430, 12);
            this.lblPlc2.Name = "lblPlc2";
            this.lblPlc2.Text = "PLC 2";
            this.lblPlc2.TranslationKey = "Lbl_Plc2Header";

            // ================= PLC 1 COLUMN (Mitsubishi) =================

            // ButtonHMI - momentary command with relay, output and two sensor lamps
            this.btnDemo.Location = new System.Drawing.Point(20, 44);
            this.btnDemo.Size = new System.Drawing.Size(170, 36);
            this.btnDemo.Name = "btnDemo";
            this.btnDemo.Text = "Demo Button";
            this.btnDemo.TranslationKey = "Btn_Demo";
            this.btnDemo.ToolTipText = "Tip_Demo";
            this.btnDemo.PlcNumber = 1;
            this.btnDemo.RelayAddress = "M10";
            this.btnDemo.OutputAddress = "Y0";
            this.btnDemo.Lamp1Address = "X10";
            this.btnDemo.Lamp1Tip = "Tip_Demo_Lamp1";
            this.btnDemo.Lamp2Address = "X11";
            this.btnDemo.Lamp2Tip = "Tip_Demo_Lamp2";
            this.btnDemo.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Left;
            this.btnDemo.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.btnDemo.EnableConditions = Kaban.Core.Enums.SystemCondition.ManualMode;
            this.btnDemo.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            this.btnDemo.ConfirmationNeeded = false;
            this.btnDemo.LogAction = true;
            this.btnDemo.CornerRadius = 6;

            // LampHMI - plain indicator bound to a sensor bit
            this.lampDemo.Location = new System.Drawing.Point(210, 44);
            this.lampDemo.Size = new System.Drawing.Size(36, 36);
            this.lampDemo.Name = "lampDemo";
            this.lampDemo.PlcNumber = 1;
            this.lampDemo.Address = "X20";
            this.lampDemo.ToolTipTextKey = "Tip_Demo_Lamp";
            this.lampDemo.OnColor = System.Drawing.Color.LimeGreen;
            this.lampDemo.OffColor = System.Drawing.Color.Gainsboro;
            this.lampDemo.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;

            // NumericDisplayHMI - read-only word with unit suffix
            this.numDisplayDemo.Location = new System.Drawing.Point(20, 100);
            this.numDisplayDemo.Size = new System.Drawing.Size(170, 36);
            this.numDisplayDemo.Name = "numDisplayDemo";
            this.numDisplayDemo.PlcNumber = 1;
            this.numDisplayDemo.Address = "D200";
            this.numDisplayDemo.DataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.numDisplayDemo.DataFormat = Kaban.Core.Enums.DisplayFormatEnum.Signed_Decimal;
            this.numDisplayDemo.Digits = 5;
            this.numDisplayDemo.DecimalPlaces = 1;
            this.numDisplayDemo.Divisor = 10D;
            this.numDisplayDemo.Suffix = "mm";
            this.numDisplayDemo.UnitType = Kaban.Core.Enums.UnitType.Length;
            this.numDisplayDemo.FillZero = false;
            this.numDisplayDemo.ToolTipTextKey = "Tip_Demo_Display";

            // NumericInputHMI - writable setpoint with limits
            this.numInputDemo.Location = new System.Drawing.Point(210, 100);
            this.numInputDemo.Size = new System.Drawing.Size(170, 36);
            this.numInputDemo.Name = "numInputDemo";
            this.numInputDemo.PlcNumber = 1;
            this.numInputDemo.Address = "D210";
            this.numInputDemo.DataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.numInputDemo.DataFormat = Kaban.Core.Enums.DisplayFormatEnum.Signed_Decimal;
            this.numInputDemo.Digits = 4;
            this.numInputDemo.DecimalPlaces = 0;
            this.numInputDemo.Suffix = "mm";
            this.numInputDemo.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.numInputDemo.EnableConditions = Kaban.Core.Enums.SystemCondition.ManualMode;
            this.numInputDemo.ConfirmWrite = true;
            this.numInputDemo.OutOfLimitsWarning = true;
            this.numInputDemo.MinLimit = new Kaban.Core.Models.DynamicValue();
            this.numInputDemo.MinLimit.Type = Kaban.Core.Enums.LimitSourceType.Static;
            this.numInputDemo.MinLimit.StaticValue = 0D;
            this.numInputDemo.MaxLimit = new Kaban.Core.Models.DynamicValue();
            this.numInputDemo.MaxLimit.Type = Kaban.Core.Enums.LimitSourceType.Static;
            this.numInputDemo.MaxLimit.StaticValue = 1000D;
            this.numInputDemo.LogAction = true;

            // FunctionUnitHMI - the composite unit (2 commands + position readout)
            this.funcUnitDemo.Location = new System.Drawing.Point(20, 144);
            this.funcUnitDemo.Size = new System.Drawing.Size(360, 64);
            this.funcUnitDemo.Name = "funcUnitDemo";
            this.funcUnitDemo.PlcNumber = 1;
            this.funcUnitDemo.TitleTextKey = "Fnc_Demo_Unit";
            this.funcUnitDemo.CommandAAddress = "M10";
            this.funcUnitDemo.CommandAAction = Kaban.Core.Enums.BitActionType.Momentary;
            this.funcUnitDemo.OutputAAddress = "Y0";
            this.funcUnitDemo.Lamp1AAddress = "X10";
            this.funcUnitDemo.Lamp1ATipKey = "Tip_Demo_Fwd";
            this.funcUnitDemo.ButtonATextKey = "Btn_Forward";
            this.funcUnitDemo.CommandBAddress = "M11";
            this.funcUnitDemo.CommandBAction = Kaban.Core.Enums.BitActionType.Momentary;
            this.funcUnitDemo.OutputBAddress = "Y1";
            this.funcUnitDemo.Lamp1BAddress = "X11";
            this.funcUnitDemo.Lamp1BTipKey = "Tip_Demo_Rev";
            this.funcUnitDemo.ButtonBTextKey = "Btn_Reverse";
            this.funcUnitDemo.PositionAddress = "D10";
            this.funcUnitDemo.PositionDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.funcUnitDemo.PositionDigits = 5;
            this.funcUnitDemo.PositionDecimalPlaces = 1;
            this.funcUnitDemo.PositionDivisor = 10D;
            this.funcUnitDemo.PositionSuffix = "mm";
            this.funcUnitDemo.PositionUnitType = Kaban.Core.Enums.UnitType.Length;
            this.funcUnitDemo.PositionTipKey = "Tip_Demo_Position";
            this.funcUnitDemo.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.funcUnitDemo.EnableConditions = Kaban.Core.Enums.SystemCondition.ManualMode;
            this.funcUnitDemo.ConfirmationNeeded = false;
            this.funcUnitDemo.LogAction = true;

            // ===================== PLC 2 COLUMN =========================
            // Same controls, PlcNumber = 2. Press these and the values change on the
            // PLC2 tab of the monitor - proving the second CPU is really addressed.

            this.btnDemo2.Location = new System.Drawing.Point(430, 44);
            this.btnDemo2.Size = new System.Drawing.Size(170, 36);
            this.btnDemo2.Name = "btnDemo2";
            this.btnDemo2.Text = "Demo Button (PLC2)";
            this.btnDemo2.TranslationKey = "Btn_Demo2";
            this.btnDemo2.PlcNumber = 2;
            this.btnDemo2.RelayAddress = "M10";
            this.btnDemo2.OutputAddress = "Y0";
            this.btnDemo2.Lamp1Address = "X10";
            this.btnDemo2.Lamp1Tip = "Tip_Demo_Lamp1";
            this.btnDemo2.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.btnDemo2.EnableConditions = Kaban.Core.Enums.SystemCondition.ManualMode;
            this.btnDemo2.CornerRadius = 6;
            this.btnDemo2.LogAction = true;

            this.lampDemo2.Location = new System.Drawing.Point(620, 44);
            this.lampDemo2.Size = new System.Drawing.Size(36, 36);
            this.lampDemo2.Name = "lampDemo2";
            this.lampDemo2.PlcNumber = 2;
            this.lampDemo2.Address = "X25";
            this.lampDemo2.OnColor = System.Drawing.Color.LimeGreen;
            this.lampDemo2.OffColor = System.Drawing.Color.Gainsboro;

            this.numDisplayDemo2.Location = new System.Drawing.Point(430, 100);
            this.numDisplayDemo2.Size = new System.Drawing.Size(170, 36);
            this.numDisplayDemo2.Name = "numDisplayDemo2";
            this.numDisplayDemo2.PlcNumber = 2;
            this.numDisplayDemo2.Address = "D100";
            this.numDisplayDemo2.DataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.numDisplayDemo2.Digits = 5;
            this.numDisplayDemo2.Suffix = "mm";

            this.numInputDemo2.Location = new System.Drawing.Point(620, 100);
            this.numInputDemo2.Size = new System.Drawing.Size(170, 36);
            this.numInputDemo2.Name = "numInputDemo2";
            this.numInputDemo2.PlcNumber = 2;
            this.numInputDemo2.Address = "D110";
            this.numInputDemo2.Digits = 4;
            this.numInputDemo2.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.numInputDemo2.EnableConditions = Kaban.Core.Enums.SystemCondition.ManualMode;
            this.numInputDemo2.ConfirmWrite = false;

            this.funcUnitDemo2.Location = new System.Drawing.Point(430, 144);
            this.funcUnitDemo2.Size = new System.Drawing.Size(360, 64);
            this.funcUnitDemo2.Name = "funcUnitDemo2";
            this.funcUnitDemo2.PlcNumber = 2;
            this.funcUnitDemo2.TitleTextKey = "Fnc_Demo_Unit2";
            this.funcUnitDemo2.CommandAAddress = "M10";
            this.funcUnitDemo2.OutputAAddress = "Y0";
            this.funcUnitDemo2.Lamp1AAddress = "X10";
            this.funcUnitDemo2.ButtonATextKey = "Btn_Forward";
            this.funcUnitDemo2.CommandBAddress = "M11";
            this.funcUnitDemo2.OutputBAddress = "Y1";
            this.funcUnitDemo2.Lamp1BAddress = "X11";
            this.funcUnitDemo2.ButtonBTextKey = "Btn_Reverse";
            this.funcUnitDemo2.PositionAddress = "D10";
            this.funcUnitDemo2.PositionDigits = 5;
            this.funcUnitDemo2.PositionDecimalPlaces = 1;
            this.funcUnitDemo2.PositionDivisor = 10D;
            this.funcUnitDemo2.PositionSuffix = "mm";
            this.funcUnitDemo2.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.funcUnitDemo2.EnableConditions = Kaban.Core.Enums.SystemCondition.ManualMode;

            // ================= misc controls =================

            // DisplayPLC - simple raw value viewer
            this.displayPlcDemo.Location = new System.Drawing.Point(20, 236);
            this.displayPlcDemo.Size = new System.Drawing.Size(170, 36);
            this.displayPlcDemo.Name = "displayPlcDemo";
            this.displayPlcDemo.Address = "D200";
            this.displayPlcDemo.Presentation = Kaban.UI.Controls.DisplayType.Numeric;

            // HexTextBox - hex entry / display
            this.hexBoxDemo.Location = new System.Drawing.Point(210, 236);
            this.hexBoxDemo.Size = new System.Drawing.Size(170, 26);
            this.hexBoxDemo.Name = "hexBoxDemo";
            this.hexBoxDemo.InputOutput = Kaban.UI.Controls.IOTypeDef.INPUT;
            this.hexBoxDemo.Value = 255;

            // CustomTextBox - styled free-text entry
            this.txtDemo.Location = new System.Drawing.Point(430, 236);
            this.txtDemo.Size = new System.Drawing.Size(170, 26);
            this.txtDemo.Name = "txtDemo";
            this.txtDemo.Text = "Sample text";

            // ---------------- screen ----------------
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.Controls.Add(this.lblPlc1);
            this.Controls.Add(this.lblPlc2);
            this.Controls.Add(this.btnDemo);
            this.Controls.Add(this.lampDemo);
            this.Controls.Add(this.numDisplayDemo);
            this.Controls.Add(this.numInputDemo);
            this.Controls.Add(this.funcUnitDemo);
            this.Controls.Add(this.btnDemo2);
            this.Controls.Add(this.lampDemo2);
            this.Controls.Add(this.numDisplayDemo2);
            this.Controls.Add(this.numInputDemo2);
            this.Controls.Add(this.funcUnitDemo2);
            this.Controls.Add(this.displayPlcDemo);
            this.Controls.Add(this.hexBoxDemo);
            this.Controls.Add(this.txtDemo);
            this.Name = "HomeScreen";
            this.Size = new System.Drawing.Size(800, 600);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private Kaban.UI.Controls.LabelHMI lblPlc1;
        private Kaban.UI.Controls.LabelHMI lblPlc2;
        private Kaban.UI.Controls.ButtonHMI btnDemo;
        private Kaban.UI.Controls.LampHMI lampDemo;
        private Kaban.UI.Controls.NumericDisplayHMI numDisplayDemo;
        private Kaban.UI.Controls.NumericInputHMI numInputDemo;
        private Kaban.UI.Controls.FunctionUnitHMI funcUnitDemo;
        private Kaban.UI.Controls.ButtonHMI btnDemo2;
        private Kaban.UI.Controls.LampHMI lampDemo2;
        private Kaban.UI.Controls.NumericDisplayHMI numDisplayDemo2;
        private Kaban.UI.Controls.NumericInputHMI numInputDemo2;
        private Kaban.UI.Controls.FunctionUnitHMI funcUnitDemo2;
        private Kaban.UI.Controls.DisplayPLC displayPlcDemo;
        private Kaban.UI.Controls.HexTextBox hexBoxDemo;
        private Kaban.UI.Controls.CustomTextBox txtDemo;
    }
}

