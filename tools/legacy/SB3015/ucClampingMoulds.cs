using KabanDevBase.Controls;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Manuel
{
    public partial class ucClampingMoulds : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucClampingMoulds _instance;
        private static List<Control> KabanLambList;
        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanInputList;
        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucClampingMoulds()
        {
            InitializeComponent();
            KabanLambList = new List<Control>();
            KabanLedLabelList = new List<Control>();
            KabanInputList = new List<Control>();

            InitEvents();
            InvisibleControlsByUserRole();

            Translator.ChangeLangue_ByUI(this);

        }

        public static ucClampingMoulds Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucClampingMoulds();
                }

                return _instance;
            }
            catch (System.Exception)
            {

                throw;
            }
        }



        #endregion

        #region [ EVENTS ]


        private void InitEvents()
        {
            tsmRefreshPage.Click += (s, e) =>
            {
                base.ShowWaitForm();
                RefreshPLCSources();
                base.CloseWaitForm();
            };
        }

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


        public void InvisibleControlsByUserRole()
        {
            foreach (Control control in BoxWrapper.Controls)
            {
                if (KabanMachinesSystem.Common.Helpers.UserStatics.RoleName == "Admin")
                    control.Enabled = true;
                else
                {
                    if (control.Tag != null)
                    {
                        if(control.Tag.ToString() == "Admin")
                            control.Enabled = false;
                        else
                            control.Enabled = true;
                    }
                    else
                        control.Enabled = true;
                }
            }
        }


        #endregion
    }
}
