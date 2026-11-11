using DevExpress.XtraEditors;
using KabanDevBase.Controls;
using KabanMachinesSystem.Business.BusinessLayer;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Helpers;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Data.Entities;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace _MD3030.Presentation.Servos
{
    public partial class ucServos : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucServos _instance;

        #endregion

        #region [ MEMBERS ]

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanNumericButtonList;
        private static List<Control> KabanInputList;
        private static List<Control> KabanProgressList;


        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        private ucServos()
        {
            InitializeComponent();

            InitDefaults();

            XtraScrollableControl scroll = new XtraScrollableControl();
            scroll.Dock = DockStyle.Fill;

            this.Controls.Add(scroll);
            scroll.Controls.Add(BoxWrapper);

            this.PageTitle = "Servo";

            Translator.ChangeLangue_ByUI(this);

        }
        public static ucServos Instance()
        {
            try
            {
                if (_instance == null || _instance.IsDisposed)
                {
                    _instance = new ucServos();
                }

                return _instance;
            }
            catch (System.Exception)
            {

                throw;
            }
        }

        private void InitDefaults()
        {

            KabanLedLabelList = new List<Control>();
            KabanInputList = new List<Control>();
            KabanProgressList = new List<Control>();
            KabanNumericButtonList = new List<Control>();
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


        #endregion
    }
}
