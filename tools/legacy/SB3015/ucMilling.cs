using KabanDevBase.Controls;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Manuel
{
    public partial class ucMilling : KabanUserControl,IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucMilling _instance;
        private static List<Control> KabanLambList;
        private static List<Control> KabanLedLabelList;
        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucMilling()
        {
            InitializeComponent();
            KabanLambList = new List<Control>();
            KabanLedLabelList = new List<Control>();

            Translator.ChangeLangue_ByUI(this);

        }

        public static ucMilling Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucMilling();
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
    }
}
