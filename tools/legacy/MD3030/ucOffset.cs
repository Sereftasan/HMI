using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using KabanDevBase.Controls;
using KabanMachinesSystem.Statics;

namespace _MD3030.Presentation.Home.Parameters
{
    public partial class ucOffset : KabanUserControl
    {
        #region [ INSTANCES ]
        private static ucOffset _instance;
        #endregion

        #region [ MEMBERS ]

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanInputList;
        private static List<Control> KabanNumericButtonList;
        private static List<Control> KabanProgressList;

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucOffset()
        {
            InitializeComponent();

            InitDefaults();
            InitEvents();

            Translator.ChangeLangue_ByUI(this);
        }

        private void InitEvents()
        {
            btnRefresh.Click += (s, e) => {
                if (KabanNumericButtonList.Count == 0)
                    KabanNumericButtonList = Get_KabanNumericButtons();

                base.SyncInputs(KabanNumericButtonList);
            };
        }

        public static ucOffset Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucOffset();
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
            KabanNumericButtonList = new List<Control>();
            KabanProgressList = new List<Control>();
        }

        #endregion

        #region [ EVENTS ]

        


        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]

        public void AsyncReadPLC()
        {
            if (KabanProgressList.Count == 0)
                KabanProgressList = Get_KabanProgressBars();

            base.SyncProgressBars(KabanProgressList);

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


        #endregion
    }
}
