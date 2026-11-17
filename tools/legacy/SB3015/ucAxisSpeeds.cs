using KabanDevBase.Controls;
using KabanMachinesSystem.Statics;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Parameters
{
    public partial class ucAxisSpeeds : KabanUserControl
    {

        #region [ INSTANCES ]
        private static ucAxisSpeeds _instance;
        private static List<Control> KabanInputList;


        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucAxisSpeeds()
        {
            InitializeComponent();
            KabanInputList = new List<Control>();
            InitEvents();
            Translator.ChangeLangue_ByUI(this);

        }

        

        public static ucAxisSpeeds Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucAxisSpeeds();
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
    }
}
