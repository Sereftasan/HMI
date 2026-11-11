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

namespace _MK2010.Presentation.Home.Parameters
{
    public partial class ucWeldingParameters : KabanUserControl
    {
        #region Instances
        private static ucWeldingParameters _instance;

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanProgressList;
        #endregion
        protected ucWeldingParameters()
        {
            InitializeComponent();

            KabanLedLabelList = new List<Control>();
            KabanProgressList = new List<Control>();
        }

        public static ucWeldingParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucWeldingParameters();
                }

                return _instance;
            }
            catch (System.Exception)
            {

                throw;
            }
        }

        private void lblSıcaklıToleransı_Click(object sender, EventArgs e)
        {

        }

        private void BoxWrapper_Paint(object sender, PaintEventArgs e)
        {

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

        private void btnNormalKaynak_Click(object sender, EventArgs e)
        {

        }
    }
}
