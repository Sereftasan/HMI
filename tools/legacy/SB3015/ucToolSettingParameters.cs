using KabanDevBase.Controls;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Parameters
{
    public partial class ucToolSettingParameters : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucToolSettingParameters _instance;
        private static List<Control> KabanLambList;
        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanInputList;

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucToolSettingParameters()
        {
            InitializeComponent();
            KabanLambList = new List<Control>();
            KabanLedLabelList = new List<Control>();
            KabanInputList = new List<Control>();
            InitEvents();

            Translator.ChangeLangue_ByUI(this);

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

        public static ucToolSettingParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucToolSettingParameters();
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

        public void RefreshPLCSources()
        {
            if (KabanInputList.Count == 0)
                KabanInputList = Get_KabanNumericButtons();

            base.SyncInputs(KabanInputList);
        }


        public void RefreshDataSources()
        {

        }


        #endregion
    }
}
