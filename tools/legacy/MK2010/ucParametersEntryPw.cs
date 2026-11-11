using KabanDevBase.Controls;

namespace _MK2010.Presentation.Home.Parameters
{
    public partial class ucParametersEntryPw : KabanUserControl
    {

        #region [ INSTANCES ]
        private static ucParametersEntryPw _instance;
        public delegate void OnEnterPasswordDelegate(string mergedContent);
        public event OnEnterPasswordDelegate OnEnterPassword = null;

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucParametersEntryPw()
        {
            InitializeComponent();
            InitEvents();
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

        private void InitEvents()
        {
            btnConfirm.Click += (s, e) =>
            {
                OnEnterPassword?.Invoke(txtPassword.TextBoxText.ToString());
                
            };

            
        }

        private void OnClose()
        {

            
        }



        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]



        #endregion

        private void BoxWrapper_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {

        }

        private void btnConfirm_Click(object sender, System.EventArgs e)
        {
            
                
        }

        private void txtPassword_Load(object sender, System.EventArgs e)
        {

        }
    }
}
