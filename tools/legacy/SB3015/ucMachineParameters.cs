using KabanDevBase.Controls;
using KabanMachinesSystem.Statics;

namespace _SB3015.Presentation.Home
{
    public partial class ucMachineParameters : KabanUserControl
    {

        #region [ INSTANCES ]
        private static ucMachineParameters _instance;
        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucMachineParameters()
        {
            InitializeComponent();
            Translator.ChangeLangue_ByUI(this);

        }

        public static ucMachineParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucMachineParameters();
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



        #endregion
    }
}
