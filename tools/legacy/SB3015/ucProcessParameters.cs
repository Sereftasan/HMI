using KabanDevBase.Controls;
using KabanMachinesSystem.Statics;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Parameters
{
    public partial class ucProcessParameters : KabanUserControl
    {

        #region [ INSTANCES ]
        private static ucProcessParameters _instance;
        private static List<Control> KabanInputList;
   

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucProcessParameters()
        {
            InitializeComponent();
            KabanInputList = new List<Control>();
            InitEvents();
            Translator.ChangeLangue_ByUI(this);

        }

        public static ucProcessParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucProcessParameters();
                }

                return _instance;
            }
            catch (System.Exception exp)
            {

                throw;
            }
        }

        private void InitEvents()
        {
            tsmRefreshPage.Click += (s, e) =>
            {

                base.ShowWaitForm();
                RefreshPLCSources();
                base.CloseWaitForm();
            };

        }

        #endregion

        #region [ EVENTS ]




        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]
        public void RefreshPLCSources()
        {
            if (KabanInputList.Count == 0)
                KabanInputList = Get_KabanNumericButtons();

            base.SyncInputs(KabanInputList);

 
        }


        #endregion

        private void kabanNumericButton15_Load(object sender, System.EventArgs e)
        {

        }
    }
}
