using KabanDevBase.Controls;
using KabanDevBase.Plc_Componets;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Manuel
{
    public partial class ucRobot : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucRobot _instance;
        private static List<Control> KabanLambList;
        private static List<Control> KabanLedLabelList;

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucRobot()
        {
            InitializeComponent();
            KabanLambList = new List<Control>();
            KabanLedLabelList = new List<Control>();

            Translator.ChangeLangue_ByUI(this);


        }

        public static ucRobot Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucRobot();
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

        private void BoxWrapper_Paint(object sender, PaintEventArgs e)
        {

        }

        private void kabanLedLabel2_Load(object sender, EventArgs e)
        {

        }
    }


    #endregion
}

