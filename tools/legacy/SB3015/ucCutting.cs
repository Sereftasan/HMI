using KabanDevBase.Controls;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Manuel
{
    public partial class ucCutting : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucCutting _instance;
        private static List<Control> KabanLambList;
        private static List<Control> KabanLedLabelList;
        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucCutting()
        {
            InitializeComponent();
            KabanLambList = new List<Control>();
            KabanLedLabelList = new List<Control>();
            //InitAuth();

            Translator.ChangeLangue_ByUI(this);

        }

        public static ucCutting Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucCutting();
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

        //private void InitAuth()
        //{
        //    lblVCutting.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        //    lblVCutting_Y.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        //    lblVCutting_Z.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        //    kabanButton12.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        //    kabanButton13.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        //    kabanLambButton7.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        //    kabanLambButton6.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        //    kabanLedLabel16.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        //    kabanLedLabel15.Visible = MachineInfosStatic.MachineInfo.IsVCutting;

        //    lblVCuttingSawMotor.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        //    label19.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        //    label8.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        //    lblVCuttingSawINverterError.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        //    label20.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        //    kabanLedLabel6.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        //    kabanButton16.Visible = MachineInfosStatic.MachineInfo.IsVCutting;
        //}

        #endregion

     
    }
}
