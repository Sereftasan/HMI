using KabanDevBase.Controls;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Manuel
{
    public partial class ucExit : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucExit _instance;
        private static List<Control> KabanLambList;
        private static List<Control> KabanLedLabelList;

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucExit()
        {
            InitializeComponent();
            KabanLambList = new List<Control>();
            KabanLedLabelList = new List<Control>();

            Translator.ChangeLangue_ByUI(this);

        }

        public static ucExit Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucExit();
                }

                return _instance;
            }
            catch (System.Exception exp)
            {

                throw;
            }
        }



        #endregion

        #region [ EVENTS ]




        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]

        public void AsyncReadPLC()
        {
            if (KabanLambList.Count == 0)
                KabanLambList = Get_KabanLambs();

            base.SyncLambs(KabanLambList);

            if (KabanLedLabelList.Count == 0)
                KabanLedLabelList = Get_KabanLedLabels();

            base.SyncLabels(KabanLedLabelList);
        }

        #endregion

        private void labelControl1_Click(object sender, System.EventArgs e)
        {

        }
    }
}
