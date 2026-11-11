using System;
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

namespace _MD3030.Presentation.Parameters
{
    public partial class ucMachineParameters : KabanUserControl
    {
        #region [ INSTANCES ]
        private static ucMachineParameters _instance;
        private static ucGeneralParameters _instanceAxis;

        SplashScreenManager SplashScreenManager { get; set; }

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucMachineParameters()
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



        public static ucMachineParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucMachineParameters();
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
            _instanceAxis = ucGeneralParameters.Instance();
            KabanUserControl_Temp = _instanceAxis;
            KabanUserControlStatics.SetActiveControls((KabanUserControl)_instanceAxis);

            InitiateInstance(_instanceAxis, xtpGeneralParameters);
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

        #endregion
    }
}



