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
    public partial class ucSystemSettings : KabanUserControl
    {
        private static ucSystemSettings _instance;

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanLambList;
        private static List<Control> KabanInputList;

        public ucSystemSettings()
        {
            InitializeComponent();



            KabanLedLabelList = new List<Control>();
            KabanProgressList = new List<Control>();
            KabanLambList = new List<Control>();
            KabanInputList = new List<Control>();
            FillSwitchCodes();

            Translator.ChangeLangue_ByUI(this);

        }

        public static ucSystemSettings Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucSystemSettings();
                }

                return _instance;
            }
            catch (System.Exception exp)
            {

                throw exp;
            }
        }
        public void FillSwitchCodes()
        {
            ktsNoYes1.Value = Plc.ReadDevice("M1000", this.PlcType) == 1;
            ktsNoYes2.Value = Plc.ReadDevice("M1026", this.PlcType) == 1;
            ktsNoYes3.Value = Plc.ReadDevice("M1002", this.PlcType) == 1;
            ktsNoYes4.Value = Plc.ReadDevice("M1032", this.PlcType) == 1;
            ktsNoYes5.Value = Plc.ReadDevice("M1016", this.PlcType) == 1;
            ktsNoYes6.Value = Plc.ReadDevice("M1028", this.PlcType) == 1;
            ktsAfter.Value = Plc.ReadDevice("M1024", this.PlcType) == 1;
            ktsMKMV.Value= Plc.ReadDevice("M1022", this.PlcType) == 1;
        }
        public void SendSwitchCodes()
        {
            if (ktsNoYes1.Value == true)
            {
                Plc.SetDevice("M1000", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("M1000", 0, this.PlcType);
            }

            if (ktsNoYes2.Value == true)
            {
                Plc.SetDevice("M1026", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("M1026", 0, this.PlcType);
            }

            if (ktsNoYes3.Value == true)
            {
                Plc.SetDevice("M1002", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("M1002", 0, this.PlcType);
            }

            if (ktsNoYes4.Value == true)
            {
                Plc.SetDevice("M1032", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("M1032", 0, this.PlcType);
            }

            if (ktsNoYes5.Value == true)
            {
                Plc.SetDevice("M1016", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("M1016", 0, this.PlcType);
            }
            if (ktsNoYes6.Value == true)
            {
                Plc.SetDevice("M1028", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("M1028", 0, this.PlcType);
            }
            if (ktsAfter.Value == true)
            {
                Plc.SetDevice("M1024", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("M1024", 0, this.PlcType);
            }
            if (ktsMKMV.Value == true)
            {
                Plc.SetDevice("M1022", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("M1022", 0, this.PlcType);
            }

        }



        public void AsyncReadPLC()
        {


            if (KabanProgressList.Count == 0)
                KabanProgressList = Get_KabanProgressBars();

            base.SyncProgressBars(KabanProgressList);

            if (KabanLedLabelList.Count == 0)
                KabanLedLabelList = Get_KabanLedLabels();

            base.SyncLabels(KabanLedLabelList);

            if (KabanLambList.Count == 0)
                KabanLambList = Get_KabanLambs();

            base.SyncLambs(KabanLambList);
            SendSwitchCodes();
           // FillSwitchCodes();

        }

        private void labelControl1_Click(object sender, EventArgs e)
        {

        }
    }
}
