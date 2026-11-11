using KabanDevBase.Controls;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _MD3030.Presentation.Parameters
{
    public partial class ucOtherParameters : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucOtherParameters _instance;

        private static List<Control> KabanNumericButtonList;
        private static List<Control> KabanLedLabelList;

        #endregion

        #region [ MEMBERS ]


        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucOtherParameters()
        {
            InitializeComponent();

            InitDefaults();
            InitEvents();
            Translator.ChangeLangue_ByUI(this);
        }

        public static ucOtherParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucOtherParameters();
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
            KabanLedLabelList = new List<Control>();
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
            if(KabanLedLabelList!=null)
                if (KabanLedLabelList.Count == 0)
                    KabanLedLabelList = Get_KabanLedLabels();

            base.SyncLabels(KabanLedLabelList);
        }

        #endregion

        private void labelControl10_Click(object sender, EventArgs e)
        {

        }
    }
}
