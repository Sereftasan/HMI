using KabanDevBase.Controls;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Parameters
{
    public partial class ucAxisParameters : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucAxisParameters _instance;
        private static List<Control> KabanLambList;
        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanInputList;

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucAxisParameters()
        {
            InitializeComponent();
            KabanLambList = new List<Control>();
            KabanLedLabelList = new List<Control>();
            KabanInputList = new List<Control>();
            InitEvents();

            InitAuth();

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

        public static ucAxisParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucAxisParameters();
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


        private void InitAuth()
        {
            lblVCutting.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            lblVCuttingAxisY.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            lblVCuttingAxisZ.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            kabanNumericButton76.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            kabanNumericButton74.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            kabanButton12.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            kabanButton13.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            kabanLedLabel16.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            kabanLedLabel15.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            kabanLambButton3.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            kabanLambButton4.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            kabanNumericButton75.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            kabanNumericButton73.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            kabanLambButton2.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            kabanLambButton1.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            kabanButton10.Location = kabanLambButton2.Location;



        }

        #endregion
    }
}
