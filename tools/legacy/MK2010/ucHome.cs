using _MK2010.Presentation.Home.Preparations;
using DevExpress.XtraSplashScreen;
using DevExpress.XtraTab;
using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanDevBase.Forms;
using KabanDevBase.Statics;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Common.Enums;
using KabanMachinesSystem.Common.Statics;
using KabanMachinesSystem.Extensions;
using KabanMachinesSystem.Statics;
using System;
using System.Windows.Forms;

namespace _MK2010.Presentation.Home
{

    public partial class ucHome : KabanUserControl
    {

        #region [ INSTANCES ]

        public event OnBarcodeRefereshDelegate OnBarcodeReferesh = null;

        private static ucHome _instance;
        WaitFormMain WaitFormMain { get; set; }
        SplashScreenManager SplashScreenManager { get; set; }
        #endregion

        #region [ MEMBERS ]

        public ucJobPreparations _InstanceUcJobPreparations { get; set; }

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucHome()
        {
            InitializeComponent();


            if (this.IsInDesignerMode())
                return;


            InitEvents();
            InitDefaults();


            ShowParameters_Auth();
            
            Translator.ChangeLangue_ByUI(this);

        }


        public static ucHome Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucHome();
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
            WaitFormMain = new WaitFormMain();
            SplashScreenManager = new SplashScreenManager(this, typeof(global::KabanDevBase.Forms.WaitFormMain), true, true, ParentType.UserControl);
        }

        #endregion

        #region [ EVENTS ]



        public void InitEvents()
        {
            try
            {
                InitTabPageEvents();

                Load += (s, e) =>
                {
                    ChangeActiveTab(0);
                };
            }
            catch (System.Exception exp)
            {
                throw exp;
            }
        }




        #endregion

        #region [ EVENT METHODS ]
        public void InitTabPageEvents()
        {

            tpHomeMain.SelectedPageChanged += (s, e) =>
            {
                ActivePageHolder.ActivePageEnum = ActivePageEnum.None;
                var controlName = GetTabPageAssignedControlName(e.Page.Name);
                var result = ActivateorHandle(e.Page, controlName);
                if (result.Item2 != null && e.Page.Name == "xtpJobPreparations")
                {
                    ActivePageHolder.ActivePageEnum = ActivePageEnum.JobPreparation;
                    KabanUserControlStatics.SetActiveControls(((ucJobPreparations)result.Item2)._instanceucJob, false);

                }
            };

        }



        #endregion

        #region [ METHODS ]

        private string GetTabPageAssignedControlName(string tabPageName)
        {
            var controlName = "";
            switch (tabPageName)
            {

                case "xtpJobPreparations":
                    controlName = "ucJobPreparations";
                    break;
                case "xtpManuel":
                    ChangeMachineSpeed();
                    controlName = "ucManuel";
                    break;
                case "xtpParameters":

                    //var uc = ucParametersEntryPw.Instance();
                    //var frmHost = new KabanHostForm(uc, null, new System.Drawing.Size(220, 150));
                    //frmHost.FormText = uc.PageTitle;
                    //if (frmHost.ShowDialog() == DialogResult.OK)
                    //{
                    //    if (uc.txtPassword.TextBoxText == "1453")
                    //        controlName = "ucParameters";
                    //    else
                    //        tpHomeMain.SelectedTabPageIndex = 0;
                    //}



                    controlName = "ucParameters";
                    break;
                default:
                    break;
            }

            return controlName;
        }

        private void ChangeMachineSpeed()
        {
            var machineCurrentVal = Plc.GetDevice(Constants.MachineManuelSpeedDevice);
            var speed = machineCurrentVal;
            if (speed > 10)
                Plc.SetDevice(Constants.MachineManuelSpeedDevice, 10);

        }

        public void RefreshDataSources()
        {
            var tabPage = tpHomeMain.TabPages[tpHomeMain.SelectedTabPageIndex];
            if (tabPage.Name == "xtpJobPreparations")
                ActivePageHolder.ActivePageEnum = ActivePageEnum.JobPreparation;

            var controlName = GetTabPageAssignedControlName(tabPage.Name);
            ActivateorHandle(tabPage, controlName);

            
        }

        public void FillWaitingJobs()
        {
           // _InstanceUcJobPreparations?.FillWaitingJobs();

        }

        public void ShowMessage_InfoPanel(string message)
        {
            //_InstanceUcJobPreparations?.ShowMessage_InfoPanel(message);
        }

        public Tuple<Type, object> SetSelectedTabPate(int TabIndex)
        {
            ActivePageHolder.ActivePageEnum = ActivePageEnum.JobPreparation;
            var tabPage = tpHomeMain.TabPages[TabIndex];
            tpHomeMain.SelectedTabPage = tabPage;
            var controlName = GetTabPageAssignedControlName(tabPage.Name);
            var result = ActivateorHandle(tabPage, controlName);

            return result;
        }

        public void ChangeActiveTab(int TabIndex)
        {
            var result = SetSelectedTabPate(TabIndex);
            if (TabIndex == 0)
            {
                _InstanceUcJobPreparations = (ucJobPreparations)result.Item2;
                _InstanceUcJobPreparations.OnBarcodeReferesh += new OnBarcodeRefereshDelegate(delegate ()
                {
                    this.OnBarcodeReferesh?.Invoke();
                });
            }

            if (result.Item2 != null)
                KabanUserControlStatics.SetActiveControls(((ucJobPreparations)result.Item2)._instanceucJob, false);
        }

        public void Activate_ucMainControlPanel()
        {
            if (_InstanceUcJobPreparations != null && this.tpHomeMain.SelectedTabPage.Name == "xtpJobPreparations")
            {
                ActivePageHolder.ActivePageEnum = ActivePageEnum.JobPreparation;
                KabanUserControlStatics.SetActiveControls(_InstanceUcJobPreparations._instanceucJob, false);
            }
        }

        private Tuple<Type, object> ActivateorHandle(DevExpress.XtraTab.XtraTabPage xtraTab, string name)
        {
            SplashScreenManager.ShowWaitForm();
            var tuple = xtraTab.ActivatorHandle(name);
            SplashScreenManager.CloseWaitForm();


            return tuple;
        }



        private void ShowParameters_Auth()
        {
            var roleName = KabanMachinesSystem.Common.Helpers.UserStatics.RoleName;
            if (roleName == "Developer" || roleName == "Service" || roleName == "Admin")
                xtpParameters.PageVisible = true;
            else
                 xtpParameters.PageVisible = true;

        }



        #endregion
    }
}

