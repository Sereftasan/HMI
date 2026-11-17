using KabanDevBase.Controls;

namespace _SB3015.Presentation.Home.Parameters
{
    public partial class ucParametersEntryPw : KabanUserControl
    {

        #region [ INSTANCES ]
        private static ucParametersEntryPw _instance;
        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucParametersEntryPw()
        {
            InitializeComponent();
        }

        public static ucParametersEntryPw Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucParametersEntryPw();
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
