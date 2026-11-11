using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using KabanDevBase.Controls;
using KabanMachinesSystem.Statics;
using KabanMachinesSystem.Business.BusinessLayer;
using KabanMachinesSystem.Business.Statics;

namespace _MK2010.Presentation.Home.Preparations
{
    public partial class ucJobPreparations : KabanUserControl
    {
        #region [ INSTANCES ]

        private static ucJobPreparations _instance;
        public ucJobNew _instanceucJob;
        public ucJobList _instanceJoblist;
        public ucJobListWithUserBarcode _instanceJoblistWithUserBarcode;

        #endregion
        #region [ MEMBERS ]
        private MachineInfoBusiness MachineInfoBusiness;

        public event OnBarcodeRefereshDelegate OnBarcodeReferesh = null;

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucJobPreparations()
        {
            InitializeComponent();


            MachineInfoBusiness = new MachineInfoBusiness();

            InitDefaults();
            InitEvents();

            Translator.ChangeLangue_ByUI(this);
        }



        public static ucJobPreparations Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucJobPreparations();
                }


                return _instance;
            }
            catch (System.Exception exp)
            {

                throw exp;
            }
        }

        private void InitDefaults()
        {
            InitiateInstances();
        }

        private void InitiateInstances()
        {
            _instanceucJob = ucJobNew.Instance();
            InitiateInstance(_instanceucJob, this.kabanSplitContainer_Main.Panel1);
            _instanceucJob.OnBarcodeReferesh += new OnBarcodeRefereshDelegate(delegate ()
            {
                this.OnBarcodeReferesh?.Invoke();
            });

            _instanceucJob.SetUserBarcode(IsUserBarcodeActive());

            if (!IsUserBarcodeActive())
            {
                _instanceJoblist = ucJobList.Instance();
                InitiateInstance(_instanceJoblist, this.kabanSplitContainer_Main.Panel2);
            }
            else
            {
                _instanceJoblistWithUserBarcode = ucJobListWithUserBarcode.Instance();
                InitiateInstance(_instanceJoblistWithUserBarcode, this.kabanSplitContainer_Main.Panel2);

                _instanceucJob.OnUserBarcodeRead += new OnUserBarcodeReadDelegate(delegate (string Barcode)
                {
                    _instanceJoblistWithUserBarcode.CallProfileWithCustomBarcode(Barcode);
                });
            }

        }

        #endregion

        #region [ EVENTS ]

        private void InitEvents()
        {



        }


        private void StartJob()
        {

        }


        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]
        public void AsyncReadPLC()
        {



        }

        private bool IsUserBarcodeActive()
        {
            var machineInfo = MachineInfoBusiness.Get(MachineInfosStatic.MachineId);
            if (machineInfo == null)
                return false;

            bool isUserBarcodeActive = machineInfo.ActiveScrwingWithBarcode != null ? (bool)machineInfo.ActiveScrwingWithBarcode : false;

            return isUserBarcodeActive;
        }

        #endregion
    }
}

