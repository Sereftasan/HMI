using DevExpress.XtraSplashScreen;
using DevExpress.XtraTab;
using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanDevBase.Statics;
using KabanMachinesSystem.Extensions;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _MK2010.Presentation.Home.Manuel
{
    public partial class ucManuel : KabanUserControl
    {

        #region [ INSTANCES ]
        private static ucManuel _instance;
        private static ucManuelControl _instanceManuelControl;
        private ucManuelWrapper _ucManuelWrapper { get; set; }
        private ucReferance _instanceReferance;

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

                return _instance;
            }
            catch (System.Exception)
            {

                throw;
            }
        }

        private void InitDefaults()
        {
            _instanceManuelControl = ucManuelControl.Instance();
            SplashScreenManager = new SplashScreenManager(this, typeof(global::KabanDevBase.Forms.WaitFormMain), true, true, ParentType.UserControl);

            ChangeControlByTabPage("xtpManuelKontrol");
            
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

                case "xtpManuelKontrol":
                    AddManuels(ucManuelControl.Instance(), xtpManuelKontrol);
                    break;
                case "xtpReferences":
                    AddManuels(ucReferance.Instance(), xtpReferences);
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
            _instanceManuelControl = ucManuelControl.Instance();
            InitiateInstance(_instanceManuelControl, xtpManuelKontrol);

            _instanceReferance = ucReferance.Instance();
            InitiateInstance(_instanceReferance, xtpReferences);


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
