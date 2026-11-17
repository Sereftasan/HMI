using DevExpress.XtraSplashScreen;
using DevExpress.XtraTab;
using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanDevBase.Statics;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Helpers;
using KabanMachinesSystem.Extensions;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Manuel
{
    public partial class ucManuel : KabanUserControl
    {

        #region [ INSTANCES ]
        private static ucManuel _instance;
        private static ucEntryConveyor _instanceEntryConveyor;
        private ucManuelWrapper _ucManuelWrapper { get; set; }

        SplashScreenManager SplashScreenManager { get; set; }

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucManuel()
        {
            InitializeComponent();

            InitDefaults();
            InitEvents();


            Translator.ChangeLangue_ByUI(this);

        }



        public static ucManuel Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucManuel();
                }

                PlcComponentReflector.Common_Wrapper_UcName_Temp.Add("ucManuelWrapper");

                return _instance;
            }
            catch (System.Exception)
            {

                throw;
            }
        }

        private void InitDefaults()
        {
            _ucManuelWrapper = ucManuelWrapper.Instance();
            SplashScreenManager = new SplashScreenManager(this, typeof(global::KabanDevBase.Forms.WaitFormMain), true, true, ParentType.UserControl);

            ChangeControlByTabPage("xtpEntryConveyor");

            //InitiateInstances();
        }


        public void InitEvents()
        {
            try
            {

                InitTabPageEvents();

            }
            catch (System.Exception exp)
            {
                throw exp;
            }
        }

        public void InitTabPageEvents()
        {

            tpManuelMain.SelectedPageChanged += (s, e) =>
            {
                SplashScreenManager.ShowWaitForm();

                ChangeControlByTabPage(e.Page.Name);

                SplashScreenManager.CloseWaitForm();

            };


        }

        public string TempTabPageName { get; set; }
        private void ChangeControlByTabPage(string pageName)
        {
            this.TempTabPageName = pageName;
            switch (pageName)
            {

                case "xtpEntryConveyor":
                    AddManuels(ucEntryConveyor.Instance(), xtpEntryConveyor);
                    break;
                case "xtpCutting":
                    AddManuels(ucCutting.Instance(), xtpCutting);
                    break;
                case "xtpExit":
                    AddManuels(ucExit.Instance(), xtpExit);
                    break;
                case "xtpMilling":
                    AddManuels(ucMilling.Instance(), xtpMilling);
                    break;
                case "xtpOther":
                    AddManuels(ucOther.Instance(), xtpOther);
                    break;
                case "xtpProfileRecognition":
                    AddManuels(ucProfileRecognition.Instance(), xtpProfileRecognition);
                    break;
                case "xtpRobot":
                    AddManuels(ucRobot.Instance(), xtpRobot);
                    break;
                case "xtpServoStatus":
                    AddManuels(ucServoStatus.Instance(), xtpServoStatus);
                    break;
                //case "xtpClampingMoulds":
                //    AddManuels(ucClampingMoulds.Instance(), xtpClampingMoulds);
                //    break;
                default:
                    break;
            }
        }

        public void AddManuels(UserControl control, XtraTabPage xtraTabPage)
        {
            InitiateInstance(_ucManuelWrapper, xtraTabPage);
            _ucManuelWrapper.AddUserControl(control);
            //
            KabanUserControlStatics.SetActiveControls((KabanUserControl)control);

        }


        private void InitiateInstances()
        {
            _instanceEntryConveyor = ucEntryConveyor.Instance();
            InitiateInstance(_instanceEntryConveyor, xtpEntryConveyor);


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

        #region [ EVENTS ]




        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]

        public void RefreshDataSources()
        {
            ChangeControlByTabPage(this.TempTabPageName);
        }





        private Tuple<Type, object> ActivateorHandle(DevExpress.XtraTab.XtraTabPage xtraTab, string name)
        {
            SplashScreenManager.ShowWaitForm();
            var tuple = xtraTab.ActivatorHandle(name);
            SplashScreenManager.CloseWaitForm();

            return tuple;
        }

        #endregion
    }
}
