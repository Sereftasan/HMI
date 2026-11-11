using System;
using System.Windows.Forms;
using _MK2010.Presentation.Home.Parameters;
using DevExpress.XtraSplashScreen;
using DevExpress.XtraTab;
using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanDevBase.Forms;
using KabanDevBase.Statics;
using KabanMachinesSystem.Extensions;
using KabanMachinesSystem.Statics;

namespace _MK2010.Presentation.Home.Settings
{
    public partial class ucParameters : KabanUserControl
    {

        #region [ INSTANCES ]
        private static ucParameters _instance;
        private static ucCalibration _instanceCalibration; //_instanceAxis;
        private static ucSystemSettings _instanceSystemSettings;
        private static ucSystemSettings2 _instanceSystemSettings2;
        private static ucDemo _instanceDemo;
        private static ucProductionParameters _instanceProduction;
        private static ucWeldingParameters _instanceWelding;
        private static ucMachineRuntime _instanceReports;
        private static ucParametersEntryPw _instanceUcParmetersEntryPW;
        private bool isPasswordCorrect = false;
        private KabanHostForm HostForm = null;

        SplashScreenManager SplashScreenManager { get; set; }

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucParameters()
        {
            InitializeComponent();

            InitDefaults();
            InitEvents();
            ShowParameters_Auth();

            Translator.ChangeLangue_ByUI(this);
        }

        private void InitDefaults()
        {
            SplashScreenManager = new SplashScreenManager(this, typeof(global::KabanDevBase.Forms.WaitFormMain), true, true, ParentType.UserControl);

            InitiateInstances();


        }


        public string ControlName { get; set; }
        public XtraTabPage TabPage { get; set; }

        public string TempTabPageName { get; set; }
        private void InitEvents()
        {
            tpParametersMain.SelectedPageChanged += (s, e) =>
            {
                string controlName = e.Page.Tag?.ToString();
                if (controlName == "")
                    return;
                if (controlName == "ucSystemSettings2" || controlName == "ucMachineRuntime")
                {
                    var uc = ucParametersEntryPw.Instance();
                    HostForm = new KabanHostForm(uc, KabanMachinesSystem.Common.Enums.PopupFormLevels.SecondLevel, new System.Drawing.Size(220, 150));
                    HostForm.FormText = uc.PageTitle;

                    if (HostForm.ShowDialog() == DialogResult.OK)
                    {
                        if (uc.txtPassword.TextBoxText != "1453")
                        {
                            controlName = "ucProductionParameters";
                            tpParametersMain.SelectedTabPageIndex = 0;
                        }
                        else
                        {
                            uc.txtPassword.TextBoxText = "";
                        }
                    }

                    ActivateorHandle(tpParametersMain.SelectedTabPage, controlName);

                    this.ControlName = controlName;
                    this.TabPage = tpParametersMain.SelectedTabPage;
                    return;
                }

                ActivateorHandle(e.Page, controlName);

                this.ControlName = controlName;
                this.TabPage = e.Page;
            };


            _instanceUcParmetersEntryPW.OnEnterPassword += new ucParametersEntryPw.OnEnterPasswordDelegate(delegate (string password)
            {
                //isPasswordCorrect = true;

                if (HostForm != null)
                {
                    HostForm.DialogResult = DialogResult.OK;
                }

            });
        }



        public static ucParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucParameters();
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

            _instanceUcParmetersEntryPW = ucParametersEntryPw.Instance();

            _instanceDemo = ucDemo.Instance();

            KabanUserControl_Temp = _instanceDemo;
            KabanUserControlStatics.SetActiveControls((KabanUserControl)_instanceDemo);

            InitiateInstance(_instanceDemo, xtpDemo);

            _instanceSystemSettings = ucSystemSettings.Instance();
            KabanUserControl_Temp = _instanceSystemSettings;
            KabanUserControlStatics.SetActiveControls((KabanUserControl)_instanceSystemSettings);


            InitiateInstance(_instanceSystemSettings, xtpSystemSettings);

            _instanceSystemSettings2 = ucSystemSettings2.Instance();
            KabanUserControl_Temp = _instanceSystemSettings2;
            KabanUserControlStatics.SetActiveControls((KabanUserControl)_instanceSystemSettings2);


            InitiateInstance(_instanceSystemSettings, xtpSystemSettings);


            _instanceCalibration = ucCalibration.Instance();
            KabanUserControl_Temp = _instanceCalibration;
            KabanUserControlStatics.SetActiveControls((KabanUserControl)_instanceCalibration);

            InitiateInstance(_instanceCalibration, xtpCalibration);

            _instanceProduction = ucProductionParameters.Instance();
            KabanUserControl_Temp = _instanceProduction;
            KabanUserControlStatics.SetActiveControls((KabanUserControl)_instanceProduction);

            InitiateInstance(_instanceProduction, xtpProductSettings);

            _instanceReports = ucMachineRuntime.Instance();
            KabanUserControl_Temp = _instanceReports;
            KabanUserControlStatics.SetActiveControls((KabanUserControl)_instanceReports);

            InitiateInstance(_instanceReports, xtpReports);



            /*
            _instanceWelding = ucWeldingParameters.Instance();
            KabanUserControl_Temp = _instanceWelding;
            KabanUserControlStatics.SetActiveControls((KabanUserControl)_instanceWelding);

            InitiateInstance(_instanceWelding, xtpWeldingParameters);*/
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

        private void ShowParameters_Auth()
        {
            var roleName = KabanMachinesSystem.Common.Helpers.UserStatics.RoleName;
            if (roleName == "Developer" || roleName == "Service" || roleName == "Admin")
            {
                xtpCalibration.PageVisible = true;
                xtpSystemSettings.PageVisible = true;
                xtpSystemSettings2.PageVisible = true;
                xtpDemo.PageVisible = true;
                xtpReports.PageVisible = true;
            }
            else
            {
                xtpCalibration.PageVisible = false;
                xtpSystemSettings.PageVisible = false;
                xtpSystemSettings2.PageVisible = false;
                xtpDemo.PageVisible = false;
                xtpReports.PageVisible = false;
            }

        }

        #endregion
    }
}
