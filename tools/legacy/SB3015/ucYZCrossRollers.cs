using KabanDevBase.Controls;
using KabanDevBase.Plc_Componets;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Common.Enums;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Common.ViewModels.PlcEntities;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Manuel
{
    public partial class ucYZCrossRollers : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucYZCrossRollers _instance;
        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanLambList;

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucYZCrossRollers()
        {
            InitializeComponent();
            KabanLedLabelList = new List<Control>();
            KabanLambList = new List<Control>();

            Translator.ChangeLangue_ByUI(this);

        }

        public static ucYZCrossRollers Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucYZCrossRollers();
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

            base.SyncLambs(KabanLambList);;
        }

        public void RefreshDataSources()
        {

        }

        #endregion

    }
}
