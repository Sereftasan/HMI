namespace Kaban.Machines.SB3015.Screens.Shared
{
    partial class JogBar
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            Kaban.Core.Models.BitAction bitAction1 = new Kaban.Core.Models.BitAction();
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
            this.BtnReset = new Kaban.UI.Controls.ButtonHMI();
            this.BtnSecurityByPass = new Kaban.UI.Controls.ButtonHMI();
            this.SuspendLayout();
            // 
            // speedOverrideHMI1 - the axes' manual speed (the old bar's track bar, D409)
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
            this.speedOverrideHMI1.TabIndex = 0;
            this.speedOverrideHMI1.TranslationKey = "Lbl_Speed";
            // 
            // BtnJog - RESET M170 M171 M172 M173 (the step buttons' bits - the old JOG had no device of its own), its lamp M174
            // 
            bitAction1.Action = Kaban.Core.Enums.BitActionType.RESET;
            bitAction1.Address = "M170";
            this.BtnJog.BitActions.Add(bitAction1);
            bitAction2.Action = Kaban.Core.Enums.BitActionType.RESET;
            bitAction2.Address = "M171";
            this.BtnJog.BitActions.Add(bitAction2);
            bitAction3.Action = Kaban.Core.Enums.BitActionType.RESET;
            bitAction3.Address = "M172";
            this.BtnJog.BitActions.Add(bitAction3);
            bitAction4.Action = Kaban.Core.Enums.BitActionType.RESET;
            bitAction4.Address = "M173";
            this.BtnJog.BitActions.Add(bitAction4);
            this.BtnJog.ButtonText = "JOG";
            this.BtnJog.FallbackText = "JOG";
            this.BtnJog.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.BtnJog.Lamp1Address = "M174";
            this.BtnJog.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.BtnJog.Location = new System.Drawing.Point(274, 13);
            this.BtnJog.LogAction = true;
            this.BtnJog.Name = "BtnJog";
            this.BtnJog.PlcNumber = 1;
            this.BtnJog.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.BtnJog.Size = new System.Drawing.Size(50, 50);
            this.BtnJog.TabIndex = 1;
            this.BtnJog.TranslationKey = "JOG";
            // 
            // Btn10mm - SET M170, its lamp M170
            // 
            bitAction5.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction5.Address = "M170";
            this.Btn10mm.BitActions.Add(bitAction5);
            this.Btn10mm.ButtonText = "10 mm";
            this.Btn10mm.FallbackText = "10 mm";
            this.Btn10mm.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.Btn10mm.Lamp1Address = "M170";
            this.Btn10mm.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.Btn10mm.Location = new System.Drawing.Point(330, 13);
            this.Btn10mm.LogAction = true;
            this.Btn10mm.Name = "Btn10mm";
            this.Btn10mm.PlcNumber = 1;
            this.Btn10mm.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.Btn10mm.Size = new System.Drawing.Size(50, 50);
            this.Btn10mm.TabIndex = 2;
            this.Btn10mm.TranslationKey = "10 mm";
            // 
            // Btn1mm - SET M173, its lamp M173
            // 
            bitAction6.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction6.Address = "M173";
            this.Btn1mm.BitActions.Add(bitAction6);
            this.Btn1mm.ButtonText = "1 mm";
            this.Btn1mm.FallbackText = "1 mm";
            this.Btn1mm.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.Btn1mm.Lamp1Address = "M173";
            this.Btn1mm.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.Btn1mm.Location = new System.Drawing.Point(386, 13);
            this.Btn1mm.LogAction = true;
            this.Btn1mm.Name = "Btn1mm";
            this.Btn1mm.PlcNumber = 1;
            this.Btn1mm.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.Btn1mm.Size = new System.Drawing.Size(50, 50);
            this.Btn1mm.TabIndex = 3;
            this.Btn1mm.TranslationKey = "1 mm";
            // 
            // Btn01mm - SET M172, its lamp M172
            // 
            bitAction7.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction7.Address = "M172";
            this.Btn01mm.BitActions.Add(bitAction7);
            this.Btn01mm.ButtonText = "0.1 mm";
            this.Btn01mm.FallbackText = "0.1 mm";
            this.Btn01mm.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.Btn01mm.Lamp1Address = "M172";
            this.Btn01mm.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.Btn01mm.Location = new System.Drawing.Point(442, 13);
            this.Btn01mm.LogAction = true;
            this.Btn01mm.Name = "Btn01mm";
            this.Btn01mm.PlcNumber = 1;
            this.Btn01mm.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.Btn01mm.Size = new System.Drawing.Size(50, 50);
            this.Btn01mm.TabIndex = 4;
            this.Btn01mm.TranslationKey = "0.1 mm";
            // 
            // Btn001mm - SET M171, its lamp M171
            // 
            bitAction8.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction8.Address = "M171";
            this.Btn001mm.BitActions.Add(bitAction8);
            this.Btn001mm.ButtonText = "0.01 mm";
            this.Btn001mm.FallbackText = "0.01 mm";
            this.Btn001mm.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.Btn001mm.Lamp1Address = "M171";
            this.Btn001mm.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.Btn001mm.Location = new System.Drawing.Point(498, 13);
            this.Btn001mm.LogAction = true;
            this.Btn001mm.Name = "Btn001mm";
            this.Btn001mm.PlcNumber = 1;
            this.Btn001mm.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.Btn001mm.Size = new System.Drawing.Size(50, 50);
            this.Btn001mm.TabIndex = 5;
            this.Btn001mm.TranslationKey = "0.01 mm";
            // 
            // BtnReset - M102 while pressed (the alarms' reset), its lamp M800
            // 
            bitAction9.Action = Kaban.Core.Enums.BitActionType.Momentary;
            bitAction9.Address = "M102";
            this.BtnReset.BitActions.Add(bitAction9);
            this.BtnReset.ButtonText = "Reset";
            this.BtnReset.FallbackText = "Reset";
            this.BtnReset.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.BtnReset.Lamp1Address = "M800";
            this.BtnReset.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.BtnReset.Location = new System.Drawing.Point(566, 13);
            this.BtnReset.LogAction = true;
            this.BtnReset.Name = "BtnReset";
            this.BtnReset.PlcNumber = 1;
            this.BtnReset.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.BtnReset.Size = new System.Drawing.Size(64, 50);
            this.BtnReset.TabIndex = 6;
            this.BtnReset.TranslationKey = "Reset";
            // 
            // BtnSecurityByPass - M175 on / off, its lamp M175 - an Admin's
            // 
            bitAction10.Action = Kaban.Core.Enums.BitActionType.Alternate;
            bitAction10.Address = "M175";
            this.BtnSecurityByPass.BitActions.Add(bitAction10);
            this.BtnSecurityByPass.ButtonText = "Security ByPass";
            this.BtnSecurityByPass.FallbackText = "Security ByPass";
            this.BtnSecurityByPass.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.BtnSecurityByPass.Lamp1Address = "M175";
            this.BtnSecurityByPass.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.BtnSecurityByPass.Location = new System.Drawing.Point(642, 13);
            this.BtnSecurityByPass.LogAction = true;
            this.BtnSecurityByPass.Name = "BtnSecurityByPass";
            this.BtnSecurityByPass.PlcNumber = 1;
            this.BtnSecurityByPass.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Admin;
            this.BtnSecurityByPass.Size = new System.Drawing.Size(90, 50);
            this.BtnSecurityByPass.TabIndex = 7;
            this.BtnSecurityByPass.TranslationKey = "Security ByPass";
            // 
            // JogBar
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.White;
            this.Controls.Add(this.speedOverrideHMI1);
            this.Controls.Add(this.BtnJog);
            this.Controls.Add(this.Btn10mm);
            this.Controls.Add(this.Btn1mm);
            this.Controls.Add(this.Btn01mm);
            this.Controls.Add(this.Btn001mm);
            this.Controls.Add(this.BtnReset);
            this.Controls.Add(this.BtnSecurityByPass);
            this.Name = "JogBar";
            this.Size = new System.Drawing.Size(750, 76);
            this.ResumeLayout(false);
        }

        #endregion

        private Kaban.UI.Controls.SpeedOverrideHMI speedOverrideHMI1;
        private Kaban.UI.Controls.ButtonHMI BtnJog;
        private Kaban.UI.Controls.ButtonHMI Btn10mm;
        private Kaban.UI.Controls.ButtonHMI Btn1mm;
        private Kaban.UI.Controls.ButtonHMI Btn01mm;
        private Kaban.UI.Controls.ButtonHMI Btn001mm;
        private Kaban.UI.Controls.ButtonHMI BtnReset;
        private Kaban.UI.Controls.ButtonHMI BtnSecurityByPass;
    }
}
