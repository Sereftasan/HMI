using KabanDevBase.Controls;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Parameters
{
    public partial class ucEncodersParameters : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucEncodersParameters _instance;
        private static List<Control> KabanInputList;
        private static List<Control> KabanLedLabelList;

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucEncodersParameters()
        {
            InitializeComponent();
            KabanInputList = new List<Control>();
            KabanLedLabelList = new List<Control>();

            InitEvents();

            Translator.ChangeLangue_ByUI(this);

            // Burayi tasindik bir sefer acilinca okusun. Performans icin
            Init_LeftProfileRecognition();
        }

        public static ucEncodersParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucEncodersParameters();
                }

                return _instance;
            }
            catch (System.Exception exp)
            {

                throw;
            }
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

        #endregion

        #region [ EVENTS ]




        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]

        public void RefreshPLCSources()
        {
            if (KabanInputList.Count == 0)
                KabanInputList = Get_KabanNumericButtons();

            base.SyncInputs(KabanInputList);
        }

        public void AsyncReadPLC()
        {
            if (KabanLedLabelList.Count == 0)
                KabanLedLabelList = Get_KabanLedLabels();

            base.SyncLabels(KabanLedLabelList);

            // Burayi kapattik constructore tasindik bir sefer acilinca okusun. Performans icin. 20210915
            //Init_LeftProfileRecognition();
        }


        private void Init_LeftProfileRecognition()
        {
            try
            {
                var result = Plc.GetDevice(Constants.LeftProfileRecognition_Enable);
                var leftProfileRecognition_IsEnable = Convert.ToBoolean(result);
                if (leftProfileRecognition_IsEnable)
                    DoVisible_LeftEncoder(true);
                else
                    DoVisible_LeftEncoder(false);
            }
            catch (Exception exp)
            {
                throw exp;
            }
        }


        private void DoVisible_LeftEncoder(bool visible)
        {
            //lblLeftEncoders.Visible = visible;

            //// 1.
            //this.lblLeftProfileRecognitionFront.Visible = visible;
            //this.kabanNumericButton25.Visible = visible;
            //this.label21.Visible = visible;
            //this.label20.Visible = visible;
            //this.kabanButton23.Visible = visible;
            //this.kabanLedLabel11.Visible = visible;
            //this.kabanButton22.Visible = visible;
            //this.label24.Visible = visible;

            //// 2.
            //this.lblLeftProfileRecognitionTop.Visible = visible;
            //this.kabanNumericButton24.Visible = visible;
            //this.label23.Visible = visible;
            //this.label22.Visible = visible;
            //this.kabanButton21.Visible = visible;
            //this.kabanLedLabel12.Visible = visible;
            //this.kabanButton20.Visible = visible;
            //this.label25.Visible = visible;
        }


        #endregion
    }
}
