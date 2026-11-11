using KabanDevBase.Controls;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _MD3030.Presentation.Manuel
{
    public partial class ucGeneralParameters : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucGeneralParameters _instance;
        #endregion

        #region [ MEMBERS ]

        private static List<Control> KabanLambList;
        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanInputList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanNumericButtonList;

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucGeneralParameters()
        {
            InitializeComponent();

            InitDefaults();
            InitEvents();

            Translator.ChangeLangue_ByUI(this);
        }

        public static ucGeneralParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucGeneralParameters();
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
            try
            {
                KabanLambList = new List<Control>();
                KabanLedLabelList = new List<Control>();
                KabanInputList = new List<Control>();
                KabanProgressList = new List<Control>();
                KabanNumericButtonList = new List<Control>();
            }
            catch (Exception exp)
            {
                throw exp;
            }
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

            if (KabanLambList.Count == 0)
                KabanLambList = Get_KabanLambs();

            base.SyncLambs(KabanLambList);

            if (KabanLedLabelList.Count == 0)
                KabanLedLabelList = Get_KabanLedLabels();

            base.SyncLabels(KabanLedLabelList);

            if (KabanNumericButtonList.Count == 0)
                KabanNumericButtonList = Get_KabanNumericButtons();

            base.SyncLabels(KabanNumericButtonList);
        }

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

        private void BoxWrapper_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
