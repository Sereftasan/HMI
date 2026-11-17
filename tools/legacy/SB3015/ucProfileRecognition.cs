using KabanDevBase.Controls;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Manuel
{
    public partial class ucProfileRecognition : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucProfileRecognition _instance;
        private static List<Control> KabanLambList;
        private static List<Control> KabanLedLabelList;
        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucProfileRecognition()
        {
            InitializeComponent();
            KabanLambList = new List<Control>();
            KabanLedLabelList = new List<Control>();

            Translator.ChangeLangue_ByUI(this);

            // Burayi tasindik bir sefer acilinca okusun. Performans icin. 20210915
            Init_LeftProfileRecognition();

        }

        

        public static ucProfileRecognition Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucProfileRecognition();
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

            // Burayi kapattik constructore tasindik bir sefer acilinca okusun. Performans icin. 20210915
            // Init_LeftProfileRecognition();
        }


        private void Init_LeftProfileRecognition()
        {
            try
            {
                var result = Plc.GetDevice(Constants.LeftProfileRecognition_Enable);
                var leftProfileRecognition_IsEnable = Convert.ToBoolean(result);
                if (leftProfileRecognition_IsEnable)
                    DoVisible_LeftProfileRecognition(true);
                else
                    DoVisible_LeftProfileRecognition(false);
            }
            catch (Exception exp)
            {
                throw exp;
            }
        }

        private void DoVisible_LeftProfileRecognition(bool visible)
        {
            //// 1.
            //this.lblLeftEndEncodersActive.Visible = visible;
            //this.label6.Visible = visible;
            //this.kabanLambButton6.Visible = visible;
            //this.label5.Visible = visible;

            //// 2.
            //this.lblLeftTopFWBack.Visible = visible;
            //this.kabanLambButton5.Visible = visible;
            //this.kabanLambButton7.Visible = visible;
            //this.label9.Visible = visible;

            //// 3.
            //this.lblLeftTopClamping.Visible = visible;
            //this.kabanLambButton8.Visible = visible;
            //this.kabanButton7.Visible = visible;
            //this.kabanLedLabel3.Visible = visible;

        }

        #endregion

        private void kabanLambButton3_Load(object sender, EventArgs e)
        {

        }
    }
}
