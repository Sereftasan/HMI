using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using KabanDevBase.Controls;
using KabanMachinesSystem.Statics;
using KabanMachinesSystem.Business.PlcObjects;

namespace _MK2010.Presentation.Home.Parameters
{
    public partial class ucProductionParameters : KabanUserControl
    {
        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanLambList;
        private static List<Control> KabanInputList;

        #region Instances
        private static ucProductionParameters _instance;
        #endregion
        protected ucProductionParameters()
        {
            InitializeComponent();

            ShowParameters_Auth();

            KabanLedLabelList = new List<Control>();
            KabanProgressList = new List<Control>();
            KabanLambList = new List<Control>();
            KabanInputList = new List<Control>();


            FillSwitchCodes();

            Translator.ChangeLangue_ByUI(this);
        }

        public void InitEvents()
        {
            
        }
        public static ucProductionParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucProductionParameters();
                }

                return _instance;
            }
            catch (System.Exception)
            {

                throw;
            }
        }

        public void FillSwitchCodes()
        {
            ktsActivePassive.Value = Plc.ReadDevice("M1008", this.PlcType) == 1;
            ktsNetWelding.Value = Plc.ReadDevice("M1001", this.PlcType) == 1;
            ktsOffOn.Value = Plc.ReadDevice("M1004", this.PlcType) == 1;
            ktsNormalU.Value = Plc.ReadDevice("M1034", this.PlcType) == 1;
            ktsSingleMultiple.Value = Plc.ReadDevice("M1030", this.PlcType) == 1;
        }

        public void SendSwitchCodes()
        {
            if(ktsActivePassive.Value == true)
            {
                Plc.SetDevice("M1008", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("M1008", 0, this.PlcType);
            }

            if (ktsNetWelding.Value == true)
            {
                Plc.SetDevice("M1001", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("M1001", 0, this.PlcType);
            }

            if (ktsOffOn.Value == true)
            {
                Plc.SetDevice("M1004", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("M1004", 0, this.PlcType);
            }

            if (ktsNormalU.Value == true)
            {
                Plc.SetDevice("M1034", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("M1034", 0, this.PlcType);
            }

            if (ktsSingleMultiple.Value == true)
            {
                Plc.SetDevice("M1030", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("M1030", 0, this.PlcType);
            }
            
        }

        public void AsyncReadPLC()
        {


            if (KabanProgressList.Count == 0)
                KabanProgressList = Get_KabanProgressBars();

            base.SyncProgressBars(KabanProgressList);
            
            if (KabanLambList.Count == 0)
                KabanLambList = Get_KabanLambs();

            base.SyncLambs(KabanLambList);

            SendSwitchCodes();

        }

        private void lblContaBaskıAktifPasif_Click(object sender, EventArgs e)
        {

        }

        private void kabanToggleSwitchControl1_Load(object sender, EventArgs e)
        {

        }

        private void kabanToggleSwitchControl2_Load(object sender, EventArgs e)
        {

        }

        private void kabanToggleSwitchControl4_Load(object sender, EventArgs e)
        {

        }

        private void ShowParameters_Auth()
        {
            var roleName = KabanMachinesSystem.Common.Helpers.UserStatics.RoleName;
            if (roleName == "Developer" || roleName == "Service" || roleName == "Admin")
            {
                knmTolerance.Enabled = true;
                lblSıcaklıToleransı.Enabled = true;

            }
            else
            {
                knmTolerance.Enabled = false;
                lblSıcaklıToleransı.Enabled = false;
            }

        }
    }
}
