using _MD3030.Presentation.Home.Common;
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
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MD3030.Presentation.Home.Manuel
{
    public partial class ucManuel : KabanUserControl
    {

        #region [ INSTANCES ]
        private static ucManuel _instance;
        private static ucGeneral _instanceGeneral;
        private ucManuelWrapper _ucManuelWrapper { get; set; }
        private ucHead1 _instanceHead1;
        private ucHead2 _instanceHead2;
        private ucHead3 _instanceHead3;
        private ucHead4 _instanceHead4;
        private ucManualMovement _instanceManualMovement;
        private ucManuelWrapper _instanceManuelWrapper;
        private ucIOWatchs _instanceIOWatchs;

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
                _instance.InitiateInstances();

                return _instance;
            }
            catch (System.Exception)
            {

                throw;
            }
        }

        public void InitDefaults()
        {
            _instanceIOWatchs = ucIOWatchs.Instance();
            _instanceGeneral = ucGeneral.Instance();
            SplashScreenManager = new SplashScreenManager(this, typeof(global::KabanDevBase.Forms.WaitFormMain), true, true, ParentType.UserControl);
            InitiateInstances();
            var thisTabPage = this.tpManuelMain.SelectedTabPage.Name;
            ChangeControlByTabPage(thisTabPage);
            
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

                case "xtpGeneral":
                    AddManuels(ucGeneral.Instance(), xtpGeneral);
                    break;
                //case "xtpHead1":
                //    AddManuels(ucHead1.Instance(), xtpHead1);
                //    break;
                //case "xtpHead2":
                //    AddManuels(ucHead2.Instance(), xtpHead2);
                //    break;
                //case "xtpHead3":
                //    AddManuels(ucHead3.Instance(), xtpHead3);
                //    break;
                //case "xtpHead4":
                //    AddManuels(ucHead4.Instance(), xtpHead4);
                //    break;
                case "xtpManualMovement":
                    AddManuels(ucManualMovement.Instance(), xtpManualMovement);
                    break;
                case "xtpInputOutputWatch":
                    AddManuels(_instanceIOWatchs, xtpInputOutputWatch);
                    break;
                default:
                    break;
            }
        }

        public void AddManuels(UserControl control, XtraTabPage xtraTabPage)
        {
            _ucManuelWrapper = ucManuelWrapper.Instance();
            InitiateInstance(_ucManuelWrapper, xtraTabPage);
            _ucManuelWrapper.AddUserControl(control);
            //
            
            KabanUserControlStatics.SetActiveControls((KabanUserControl)control);

        }


        private void InitiateInstances()
        {
            _instanceGeneral = ucGeneral.Instance();
            InitiateInstance(_instanceGeneral, xtpGeneral);

            _instanceHead1 = ucHead1.Instance();
            InitiateInstance(_instanceHead1, xtpHead1);

            _instanceHead2 = ucHead2.Instance();
            InitiateInstance(_instanceHead2, xtpHead2);

            _instanceHead3 = ucHead3.Instance();
            InitiateInstance(_instanceHead3, xtpHead3);

            _instanceHead4 = ucHead4.Instance();
            InitiateInstance(_instanceHead4, xtpHead4);

            _instanceManualMovement = ucManualMovement.Instance();
            InitiateInstance(_instanceManualMovement, xtpManualMovement);




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
