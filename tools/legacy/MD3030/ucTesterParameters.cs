using KabanDevBase.Controls;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _MD3030.Presentation.Parameters
{
    public partial class ucTesterParameters : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucTesterParameters _instance;
        #endregion

        #region [ MEMBERS ]

        private static List<Control> KabanNumericButtonList;

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucTesterParameters()
        {
            InitializeComponent();

            InitDefaults();
            InitEvents();

            Translator.ChangeLangue_ByUI(this);
        }

        public static ucTesterParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucTesterParameters();
                }

                return _instance;
            }
            catch (System.Exception exp)
            {

                throw;
            }
        }

        private void InitDefaults()
        {
            KabanNumericButtonList = new List<Control>();
        }

        #endregion

        #region [ EVENTS ]

        private void InitEvents()
        {
            btnRefresh.Click += (s, e) => {
                if (KabanNumericButtonList.Count == 0)
                    KabanNumericButtonList = Get_KabanNumericButtons();

                base.SyncInputs(KabanNumericButtonList);
            };
        }


        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]

        public void AsyncReadPLC()
        {
        }

        #endregion
    }
}
