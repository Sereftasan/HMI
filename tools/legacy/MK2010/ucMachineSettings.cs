using System;
using System.Linq;
using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanMachinesSystem.Business.BusinessLayer;
using KabanMachinesSystem.Business.Helpers;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Enums;
using KabanMachinesSystem.Data.Entities;
using KabanMachinesSystem.Presentation.Settings.ExternalReceiptMapping;
using KabanMachinesSystem.Statics;

namespace _MK2010.Presentation.Settings.MachineSettings
{
    public partial class ucMachineSettings : KabanUserControl
    {

        #region [ INSTANCES ]
        private static ucMachineSettings _instance;
        private MachineInfoBusiness MachineInfoBusiness;
        private LabelHeaderBusiness LabelHeaderBusiness;
        private ExternalFileBusiness ExternalFileBusiness { get; set; }
        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucMachineSettings()
        {
            InitializeComponent();

            MachineInfoBusiness = new MachineInfoBusiness();
            LabelHeaderBusiness = new LabelHeaderBusiness();
            this.ExternalFileBusiness = new ExternalFileBusiness();

            InitDefaults();
            InitEvents();


            Translator.ChangeLangue_ByUI(this);

        }



        public static ucMachineSettings Instance()
        {
            if (_instance == null)
            {
                _instance = new ucMachineSettings();
            }
            else
                _instance.RefreshDataSources();

            return _instance;
        }

        private void InitDefaults()
        {
           

            FillFields();
        }

      

        


      


        #endregion

        #region [ EVENTS ]


        private void InitEvents()
        {
            btnSave.Click += (s, e) =>
            {
                SaveAllSettings();
            };

          
        }

        #endregion

        #region [ EVENT METHODS ]

      

        #endregion

        #region [ METHODS ]


        public void RefreshDataSources()
        {

        }
        private void SaveAllSettings()
        {

            try
            {
                var machineInfos = MachineInfoBusiness.Get(MachineInfosStatic.MachineId);
               

                //KabanMachinesSystem.Common.Statics.MachineSettings.UsingInchSystem = ktsUsingInchSystem.Value;

              
                machineInfos.ActiveScrwingWithBarcode = ktsActiveUserBarcode.Value;

               
                MachineInfoBusiness.Update(machineInfos);

               

               

                


                SetSettingsPLC();


                base.ShowToastNotify(MessageHelper.GetMessage(MessageType.TransactionSucceed));

                var restartQuestionResult = KabanMessageBox.ShowMessage(MessageHelper.GetMessage(MessageType.WaitingRestart), KabanDevBase.Enums.MessageBoxType.Question);
                if (restartQuestionResult == System.Windows.Forms.DialogResult.Yes)
                    ApplicationStatics.RestartSystem();


            }
            catch (System.Exception exp)
            {
                base.ShowToastNotify(MessageHelper.GetMessage(MessageType.TransactionError));

                throw exp;
            }

        }


        private void SetSettingsPLC()
        {
           
        }

        private void FillFields()
        {
            ktsActiveUserBarcode.LabelText = "Activate User Barcode";
            var machineInfo = MachineInfoBusiness.Get(MachineInfosStatic.MachineId);
            if (machineInfo == null)
                return;

            ktsActiveUserBarcode.Value = machineInfo.ActiveScrwingWithBarcode != null ? (bool)machineInfo.ActiveScrwingWithBarcode : false;
           
        }
        

     

        private void FillWasteLabelHeaders(KabanLookUpEditControl kabanLookUpEditControl, bool isLeftOver = false)
        {
            var labelHeaders = LabelHeaderBusiness.Get_All();
            LabelHeader activeLabel = null;
            if (!isLeftOver)
                activeLabel = LabelHeaderBusiness.Get_ActiveLabel();
            else
                activeLabel = LabelHeaderBusiness.Get_LeftOverLabel();

            kabanLookUpEditControl.DataSource = labelHeaders;
            kabanLookUpEditControl.Properties.DisplayMember = "Name";
            kabanLookUpEditControl.Properties.ValueMember = "Id";

            if (kabanLookUpEditControl != null && activeLabel != null)
                kabanLookUpEditControl.EditValue = activeLabel.Id;

            kabanLookUpEditControl.CustomizeColumn();
        }



        #endregion
    }
}
