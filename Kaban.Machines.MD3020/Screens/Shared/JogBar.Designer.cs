namespace Kaban.Machines.MD3020.Screens.Shared
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
            this.speedOverrideHMI1 = new Kaban.UI.Controls.SpeedOverrideHMI();
            this.BtnJog = new Kaban.UI.Controls.ButtonHMI();
            this.Btn1mm = new Kaban.UI.Controls.ButtonHMI();
            this.Btn01mm = new Kaban.UI.Controls.ButtonHMI();
            this.Btn001mm = new Kaban.UI.Controls.ButtonHMI();
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
            // BtnJog - SET M90, its lamp M90
            // 
            bitAction1.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction1.Address = "M90";
            this.BtnJog.BitActions.Add(bitAction1);
            this.BtnJog.ButtonText = "JOG";
            this.BtnJog.FallbackText = "JOG";
            this.BtnJog.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.BtnJog.Lamp1Address = "M90";
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
            // Btn1mm - SET M2, its lamp M2
            // 
            bitAction2.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction2.Address = "M2";
            this.Btn1mm.BitActions.Add(bitAction2);
            this.Btn1mm.ButtonText = "1 mm";
            this.Btn1mm.FallbackText = "1 mm";
            this.Btn1mm.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.Btn1mm.Lamp1Address = "M2";
            this.Btn1mm.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.Btn1mm.Location = new System.Drawing.Point(330, 13);
            this.Btn1mm.LogAction = true;
            this.Btn1mm.Name = "Btn1mm";
            this.Btn1mm.PlcNumber = 1;
            this.Btn1mm.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.Btn1mm.Size = new System.Drawing.Size(50, 50);
            this.Btn1mm.TabIndex = 2;
            this.Btn1mm.TranslationKey = "1 mm";
            // 
            // Btn01mm - SET M1, its lamp M1
            // 
            bitAction3.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction3.Address = "M1";
            this.Btn01mm.BitActions.Add(bitAction3);
            this.Btn01mm.ButtonText = "0.1 mm";
            this.Btn01mm.FallbackText = "0.1 mm";
            this.Btn01mm.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.Btn01mm.Lamp1Address = "M1";
            this.Btn01mm.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.Btn01mm.Location = new System.Drawing.Point(386, 13);
            this.Btn01mm.LogAction = true;
            this.Btn01mm.Name = "Btn01mm";
            this.Btn01mm.PlcNumber = 1;
            this.Btn01mm.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.Btn01mm.Size = new System.Drawing.Size(50, 50);
            this.Btn01mm.TabIndex = 3;
            this.Btn01mm.TranslationKey = "0.1 mm";
            // 
            // Btn001mm - SET M0, its lamp M0
            // 
            bitAction4.Action = Kaban.Core.Enums.BitActionType.SET;
            bitAction4.Address = "M0";
            this.Btn001mm.BitActions.Add(bitAction4);
            this.Btn001mm.ButtonText = "0.01 mm";
            this.Btn001mm.FallbackText = "0.01 mm";
            this.Btn001mm.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.Btn001mm.Lamp1Address = "M0";
            this.Btn001mm.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Bottom;
            this.Btn001mm.Location = new System.Drawing.Point(442, 13);
            this.Btn001mm.LogAction = true;
            this.Btn001mm.Name = "Btn001mm";
            this.Btn001mm.PlcNumber = 1;
            this.Btn001mm.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.Btn001mm.Size = new System.Drawing.Size(50, 50);
            this.Btn001mm.TabIndex = 4;
            this.Btn001mm.TranslationKey = "0.01 mm";
            // 
            // JogBar
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.White;
            this.Controls.Add(this.speedOverrideHMI1);
            this.Controls.Add(this.BtnJog);
            this.Controls.Add(this.Btn1mm);
            this.Controls.Add(this.Btn01mm);
            this.Controls.Add(this.Btn001mm);
            this.Name = "JogBar";
            this.Size = new System.Drawing.Size(510, 76);
            this.ResumeLayout(false);
        }

        #endregion

        private Kaban.UI.Controls.SpeedOverrideHMI speedOverrideHMI1;
        private Kaban.UI.Controls.ButtonHMI BtnJog;
        private Kaban.UI.Controls.ButtonHMI Btn1mm;
        private Kaban.UI.Controls.ButtonHMI Btn01mm;
        private Kaban.UI.Controls.ButtonHMI Btn001mm;
    }
}
