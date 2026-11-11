using KabanDevBase.Controls;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _MD3030.Presentation.Parameters
{
    public partial class ucHeadParameters : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucHeadParameters _instance;
        #endregion

        #region [ MEMBERS ]

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanInputList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanNumericButtonList;

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucHeadParameters()
        {
            InitializeComponent();

            InitDefaults();
            InitEvents();

            Translator.ChangeLangue_ByUI(this);
        }

        public static ucHeadParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucHeadParameters();
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

            KabanLedLabelList = new List<Control>();
            KabanInputList = new List<Control>();
            KabanProgressList = new List<Control>();
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
        private void RefreshDataSources()
        {
        }
        public void AsyncReadPLC()
        {
        }

        #endregion

        private void BoxWrapper_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
