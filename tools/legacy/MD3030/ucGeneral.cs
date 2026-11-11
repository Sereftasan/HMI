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
using KabanDevBase.Extensions;
using KabanMachinesSystem.Statics;

namespace MD3030.Presentation.Home.Manuel
{
    public partial class ucGeneral : KabanUserControl
    {
        private static ucGeneral _instance;
        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanLambList;

        protected ucGeneral()
        {
            InitializeComponent();


            if (this.IsInDesignerMode())
                return;

            KabanLedLabelList = new List<Control>();
            KabanProgressList = new List<Control>();
            KabanLambList = new List<Control>();

            Translator.ChangeLangue_ByUI(this);
        }

        public static ucGeneral Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucGeneral();
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

        private void kabanLambButton4_Load(object sender, EventArgs e)
        {

        }

        private void kabanLambButton11_Load(object sender, EventArgs e)
        {

        }
    }
}
