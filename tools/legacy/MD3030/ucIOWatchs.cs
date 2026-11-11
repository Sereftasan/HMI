using System;
using System.Collections.Generic;
using System.Windows.Forms;
using _MD3030.Presentation.Home.Parameters;
using _MD3030.Presentation.Manuel;
using DevExpress.XtraSplashScreen;
using DevExpress.XtraTab;
using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanDevBase.Statics;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Extensions;
using KabanMachinesSystem.Statics;
using MD3030.Presentation.Home.IOWatch.Manuel;

namespace _MD3030.Presentation.Parameters
{
    public partial class ucIOWatchs : KabanUserControl, IPlcComponent
    {
        #region [ INSTANCES ]
        private static ucIOWatchs _instance;
        //private static ucGeneralParameters _instanceAxis;
        private static ucHead1 _instanceHead1;
        private static ucHead2 _instanceHead2;
        private static ucHead3 _instanceHead3;
        private static ucHead4 _instanceHead4;

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanLambList;
        SplashScreenManager SplashScreenManager { get; set; }

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucIOWatchs()
        {
            InitializeComponent();

            InitDefaults();
            InitEvents();


            Translator.ChangeLangue_ByUI(this);
        }

        private void InitDefaults()
        {
            SplashScreenManager = new SplashScreenManager(this, typeof(global::KabanDevBase.Forms.WaitFormMain), true, true, ParentType.UserControl);

            InitiateInstances();

            KabanLedLabelList = new List<Control>();
            KabanProgressList = new List<Control>();
            KabanLambList = new List<Control>();
        }


        public string ControlName { get; set; }
        public XtraTabPage TabPage { get; set; }
        private void InitEvents()
        {
            tpParametersMain.SelectedPageChanged += (s, e) =>
            {
                string controlName = e.Page.Tag?.ToString();
                if (controlName == "")
                    return;
                ActivateorHandle(e.Page, controlName);

                this.ControlName = controlName;
                this.TabPage = e.Page;
            };

        }



        public static ucIOWatchs Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucIOWatchs();
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

        public void RefreshDataSources()
        {
            if (string.IsNullOrEmpty(this.ControlName))
                return;

            var tuple = ActivateorHandle(this.TabPage, this.ControlName);
            if (tuple == null)
                return;

            KabanUserControlStatics.SetActiveControls((KabanUserControl)tuple.Item2);
        }

        private void InitiateInstances()
        {

            //_instanceAxis = ucGeneralParameters.Instance();
            _instanceHead1 = ucHead1.Instance();
            _instanceHead2 = ucHead2.Instance();
            _instanceHead3 = ucHead3.Instance();
            _instanceHead4 = ucHead4.Instance();
            KabanUserControl_Temp = _instanceHead1;
            KabanUserControlStatics.SetActiveControls((KabanUserControl)_instanceHead1);

            InitiateInstance(_instanceHead1, xtpHead1);
            InitiateInstance(_instanceHead2, xtpHead2);
            InitiateInstance(_instanceHead3, xtpHead3);
            InitiateInstance(_instanceHead4, xtpHead4);
            RefreshDataSources();
        }



        object KabanUserControl_Temp { get; set; }
        private Tuple<Type, object> ActivateorHandle(DevExpress.XtraTab.XtraTabPage xtraTab, string name)
        {
            KabanUserControl_Temp = null;

            SplashScreenManager.ShowWaitForm();
            var tuple = xtraTab.ActivatorHandle(name);

            SplashScreenManager.CloseWaitForm();

            return tuple;
        }

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

        #endregion
    }
}



