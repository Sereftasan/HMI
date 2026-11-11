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
    public partial class ucCalibration : KabanUserControl
    {
       
        private static ucCalibration _instance;

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanInputList;



        public ucCalibration()
        {
            InitializeComponent();
            InitEvents();


            KabanLedLabelList = new List<Control>();
            KabanProgressList = new List<Control>();
            KabanInputList = new List<Control>();

            Translator.ChangeLangue_ByUI(this);

        }

        public static ucCalibration Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucCalibration();
                }

                return _instance;
            }
            catch (System.Exception exp)
            {

                throw exp;
            }
        }

        public void InitEvents()
        {
            btnCalibrateAll.Click += new EventHandler(delegate (object sender, EventArgs e)
            {
                var iRet = Plc.SetDevice(btnKalibreEt1.Device, 1, this.PlcType);
                 iRet = Plc.SetDevice(btnKalibreEt2.Device, 1, this.PlcType);
                 iRet = Plc.SetDevice(btnKalibreEt3.Device, 1, this.PlcType);

            });
        }

        public void AsyncReadPLC()
        {

            if (KabanProgressList.Count == 0)
                KabanProgressList = Get_KabanProgressBars();

            base.SyncProgressBars(KabanProgressList);

            if (KabanLedLabelList.Count == 0)
                KabanLedLabelList = Get_KabanLedLabels();

            base.SyncLabels(KabanLedLabelList);

        }
    }
}
