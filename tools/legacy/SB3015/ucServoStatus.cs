using KabanDevBase.Controls;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Manuel
{
    public partial class ucServoStatus : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucServoStatus _instance;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanLambList;



        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucServoStatus()
        {
            InitializeComponent();
            KabanProgressList = new List<Control>();
            KabanLedLabelList = new List<Control>();
            KabanLambList = new List<Control>();

            InitAuth();

            Translator.ChangeLangue_ByUI(this);


        }

        public static ucServoStatus Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucServoStatus();
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
            if (KabanProgressList.Count == 0)
                KabanProgressList = Get_KabanProgressBars();

            base.SyncProgressBars(KabanProgressList);

            if (KabanLedLabelList.Count == 0)
                KabanLedLabelList = Get_KabanLedLabels();

            base.SyncLabels(KabanLedLabelList);


            if (KabanLambList.Count == 0)
                KabanLambList = Get_KabanLambs();

            base.SyncLambs(KabanLambList);



        }

        private void InitAuth()
        {
            //lblVCutting.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //lblVCuttingAxisY.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //lblVCuttingAxisZ.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //kllVCuttinAxisPosition_Y.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //kllVCuttinAxisPosition_Z.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //kpbVCuttinAxisLoad_Y.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //kpbVCuttinAxisLoad_Z.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //klVCuttinRererenceRequest_Y.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //klVCuttinRererenceRequest_Z.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //kpbVCuttinZPahse_Y.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //kpbVCuttinZPahse_Z.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //kllVCuttinError_Y.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //kllVCuttinError_Z.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //kllVCuttinServoError_Y.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //kllVCuttinServoError_Z.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //kllVCuttinWarning_Y.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //kllVCuttinWarning_Z.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //klbVCuttinSendReference_Y.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
            //klbVCuttinSendReference_Z.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        }


        #endregion
    }
}
