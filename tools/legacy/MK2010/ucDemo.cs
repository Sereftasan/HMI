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

namespace _MK2010.Presentation.Home.Parameters
{
    public partial class ucDemo : KabanUserControl
    {
        private static ucDemo _instance;

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanLambList;

        public ucDemo()
        {
            InitializeComponent();



            KabanLedLabelList = new List<Control>();
            KabanProgressList = new List<Control>();
            KabanLambList = new List<Control>();

            Translator.ChangeLangue_ByUI(this);

        }

        public static ucDemo Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucDemo();
                }

                return _instance;
            }
            catch (System.Exception exp)
            {

                throw exp;
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


        }

    }
}
