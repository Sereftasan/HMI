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
using KabanDevBase.Extensions;
using KabanMachinesSystem.Business.PlcObjects;
using System.Threading;

namespace _MK2010.Presentation.Home.Manuel
{
    public partial class ucReferance : KabanUserControl
    {

        private static ucReferance _instance;

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanLambList;

        public ucReferance()
        {
            InitializeComponent();

            if (this.IsInDesignerMode())
                return;

            KabanLedLabelList = new List<Control>();
            KabanProgressList = new List<Control>();
            KabanLambList = new List<Control>();

            InitEvents();

            Translator.ChangeLangue_ByUI(this);

        }

        public static ucReferance Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucReferance();
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
            btnRefAll.Click += new EventHandler(delegate (object sender, EventArgs e)
            {
                var iRet = Plc.SetDevice(btnRef1.Device, 1, this.PlcType);
                iRet = Plc.SetDevice(btnRef2.Device, 1, this.PlcType);
                iRet = Plc.SetDevice(btnRef3.Device, 1, this.PlcType);

                

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

            if (KabanLambList.Count == 0)
                KabanLambList = Get_KabanLambs();

            base.SyncLambs(KabanLambList);

        }
    }
}
