using KabanDevBase.Controls;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _MD3030.Presentation.Manuel
{
    public partial class ucPositionDefinition : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucPositionDefinition _instance;
        #endregion

        #region [ MEMBERS ]

        private static List<Control> KabanLambList;
        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanInputList;
        private static List<Control> KabanNumericButtonList;

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucPositionDefinition()
        {
            InitializeComponent();

            InitDefaults();
            InitEvents();

            Translator.ChangeLangue_ByUI(this);
        }

        public static ucPositionDefinition Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucPositionDefinition();
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
                KabanNumericButtonList = new List<Control>();
                KabanInputList = new List<Control>();
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
    }
}
