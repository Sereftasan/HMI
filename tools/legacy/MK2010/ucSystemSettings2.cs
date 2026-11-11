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
    public partial class ucSystemSettings2 : KabanUserControl
    {
        private static ucSystemSettings2 _instance;

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanLambList;
        private static List<Control> KabanInputList;

        public ucSystemSettings2()  
        {
            InitializeComponent();

            

            KabanLedLabelList = new List<Control>();
            KabanProgressList = new List<Control>();
            KabanLambList = new List<Control>();
            KabanInputList = new List<Control>();

            Translator.ChangeLangue_ByUI(this);
            setVisibility(false);
        }
        

        public static ucSystemSettings2 Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucSystemSettings2();
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

        private void labelControl1_Click(object sender, EventArgs e)
        {

        }
        public void setVisibility(bool flag) {
        
        lblName1.Visible = flag;
        }
    }
}

