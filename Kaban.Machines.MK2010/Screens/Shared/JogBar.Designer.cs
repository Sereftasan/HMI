namespace Kaban.Machines.MK2010.Screens.Shared
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
            this.speedOverrideHMI1 = new Kaban.UI.Controls.SpeedOverrideHMI();
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
            // JogBar
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.Color.White;
            this.Controls.Add(this.speedOverrideHMI1);
            this.Name = "JogBar";
            this.Size = new System.Drawing.Size(280, 76);
            this.ResumeLayout(false);
        }

        #endregion

        private Kaban.UI.Controls.SpeedOverrideHMI speedOverrideHMI1;
    }
}
