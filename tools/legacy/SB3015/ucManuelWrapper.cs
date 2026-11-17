using KabanDevBase.Controls;
using KabanMachinesSystem.Common.Interfaces;
using System.Linq;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Manuel
{
    public partial class ucManuelWrapper : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucManuelWrapper _instance;
        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucManuelWrapper()
        {
            InitializeComponent();
        }

        public static ucManuelWrapper Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucManuelWrapper();
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

        public void AddUserControl(UserControl control)
        {
            control.Dock = DockStyle.Fill;
            var hasAdded = this.splitContainerMain?.Panel2.Controls.Cast<Control>().FirstOrDefault(q => q.Name == control.Name);
            if (hasAdded == null)
            {
                this.splitContainerMain.Panel2.Controls.Add(control);
                control.BringToFront();
                return;
            }

            hasAdded.BringToFront();
        }

        public void AsyncReadPLC()
        {
            this.ucHeadControlButtons1.AsyncReadPLC();
        }

        #endregion
    }
}
