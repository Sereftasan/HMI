using System;
using System.Windows.Forms;
using _MD3030.Presentation.Home.Common;
using _MD3030.Presentation.Home.Parameters;
using _MD3030.Presentation.Manuel;
using _MD3030.Presentation.Parameters;
using DevExpress.XtraSplashScreen;
using DevExpress.XtraTab;
using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanDevBase.Statics;
using KabanMachinesSystem.Common.Helpers;
using KabanMachinesSystem.Extensions;
using KabanMachinesSystem.Statics;
using MD3030.Presentation.Home.Manuel;

namespace _MD3030.Presentation.Home.Parameters
{
    public partial class ucParameters : KabanUserControl
    {

        #region [ INSTANCES ]
        private static ucParameters _instance;
        private static ucAxisParameters _instanceAxis;
        private static ucOffset _instanceOffset;
        private static ucMachineParameters _instanceMachineParameters;
        private static ucProcessParameters _instanceProcessParameters;
        private static ucSpeedParameters _instanceSpeedParameters;
        private static ucTesterParameters _instanceTesterParameters;
        private ucManuelWrapper _ucManuelWrapper { get; set; }
        SplashScreenManager SplashScreenManager { get; set; }

        private bool TabPagesInitialized = false;

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucParameters()
        {
            InitializeComponent();

            InitDefaults();
            InitEvents();


            Translator.ChangeLangue_ByUI(this);
        }

        public void InitDefaults()
        {
            _instanceOffset = ucOffset.Instance();
            _instanceAxis = ucAxisParameters.Instance();
            _instanceMachineParameters = ucMachineParameters.Instance();
            _instanceProcessParameters = ucProcessParameters.Instance();
            _instanceSpeedParameters = ucSpeedParameters.Instance();
            _instanceTesterParameters = ucTesterParameters.Instance();

            _ucManuelWrapper = ucManuelWrapper.Instance();
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


                if(e.Page.Name == "xtpAxisCalibration")
                {
                    _instanceAxis = ucAxisParameters.Instance();
                    KabanUserControl_Temp = _instanceAxis;
                    AddManuels(_instanceAxis, e.Page);
                }else if (e.Page.Name == "xtpOffset")
                {
                    _instanceOffset= ucOffset.Instance();
                    KabanUserControl_Temp = _instanceOffset;
                    AddManuels(_instanceOffset, e.Page);
                }
                else
                    ActivateorHandle(e.Page, controlName);

                this.ControlName = controlName;
                this.TabPage = e.Page;
            };

        }



        public static ucParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucParameters();
                }
                PlcComponentReflector.Common_Wrapper_UcName_Temp.Add("ucManuelWrapper");
                _instance.InitiateInstances();

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
            KabanUserControl dynamicKabanUserControl;

            string pagename = tpParametersMain.SelectedTabPage.Name;
            switch (pagename)
            {
                case "xtpAxisCalibration":
                    dynamicKabanUserControl = _instanceAxis;
                    KabanUserControl_Temp = dynamicKabanUserControl;
                    AddManuels(dynamicKabanUserControl, tpParametersMain.TabPages[0]);
                    break;
                case "xtpMachineParameters":
                    dynamicKabanUserControl = _instanceMachineParameters;
                    KabanUserControl_Temp = dynamicKabanUserControl;
                    AddManuels(dynamicKabanUserControl, tpParametersMain.TabPages[1]);
                    break;
                case "xtpSpeedParameters":
                    dynamicKabanUserControl = _instanceSpeedParameters;
                    KabanUserControl_Temp = dynamicKabanUserControl;
                    AddManuels(dynamicKabanUserControl, tpParametersMain.TabPages[2]);
                    break;
                case "xtpProcessParameters":
                    dynamicKabanUserControl = _instanceProcessParameters;
                    KabanUserControl_Temp = dynamicKabanUserControl;
                    AddManuels(dynamicKabanUserControl, tpParametersMain.TabPages[3]);
                    break;
                case "xtpTestParameters":
                    dynamicKabanUserControl = _instanceTesterParameters;
                    KabanUserControl_Temp = dynamicKabanUserControl;
                    AddManuels(dynamicKabanUserControl, tpParametersMain.TabPages[4]);
                    break;
                case "xtpOffset":
                    dynamicKabanUserControl = _instanceOffset;
                    KabanUserControl_Temp = dynamicKabanUserControl;
                    AddManuels(dynamicKabanUserControl, tpParametersMain.TabPages[5]);
                    break;
                default:
                    dynamicKabanUserControl = _instanceAxis;
                    KabanUserControl_Temp = dynamicKabanUserControl;
                    AddManuels(dynamicKabanUserControl, tpParametersMain.TabPages[0]);
                    break;


            }
            ////_instanceAxis = ucAxisParameters.Instance();
            //KabanUserControl_Temp = dynamicKabanUserControl;
            //AddManuels(dynamicKabanUserControl, tpParametersMain.TabPages[0]);


            _ucManuelWrapper.AddUserControl(dynamicKabanUserControl);
            KabanUserControlStatics.SetActiveControls((KabanUserControl)dynamicKabanUserControl);
            if(!TabPagesInitialized)
                InitiateInstance(_ucManuelWrapper, xtpAxisCalibration);
            TabPagesInitialized = true;
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

        public void AddManuels(UserControl control, XtraTabPage xtraTabPage)
        {
            InitiateInstance(_ucManuelWrapper, xtraTabPage);
            _ucManuelWrapper.AddUserControl(control);


            KabanUserControlStatics.SetActiveControls((KabanUserControl)control);
        }

        public static void InitiateInstance<T>(T instance, XtraTabPage xtraTabPage) where T : Control
        {
            if (!xtraTabPage.Controls.Contains(instance))
            {
                xtraTabPage.Controls.Add(instance);
                instance.Dock = DockStyle.Fill;
            }
            instance.BringToFront();
        }

        #endregion
    }
}
