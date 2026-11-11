using KabanDevBase.Statics;
using KabanMachinesSystem;
using KabanMachinesSystem.Business.BusinessLayer;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.CustomExceptions;
using KabanMachinesSystem.Common.Enums;
using KabanMachinesSystem.Common.Helpers;
using KabanMachinesSystem.Common.Statics;
using KabanMachinesSystem.Facade;
using KabanMachinesSystem.Helpers;
using KabanMachinesSystem.KabanSystem.Entities;
using _MK2010.Presentation.Home;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using _MK2010.Presentation.Companies;
using _MK2010.KabanSystem;
using _MK2010.Bussiness;
using System.IO;
using System.Threading.Tasks;
using KabanDevBase.Extensions;
using KabanMachinesSystem.Business.Helpers;
using KabanMachinesSystem.Data.Entities;
using System.Windows.Forms;

namespace _MK2010
{
    public partial class frmMK2010 : frmMain
    {
        //private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator1;
        //private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator8;
        //private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator7;
        //private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator2;
        //private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator3;
        //private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator4;
        //private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator5;
        //private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator6;

        #region [ INSTANCES ]

        MachineInfoBusiness MachineInfoBusiness { get; set; }
        BarcodeReaderBusiness BarcodeReaderBusiness { get; set; }

        public static IMachineInfoSetter mis;
        public ChartAxisSettingBusiness ChartAxisSettingBusiness { get; set; }

        ucHome _InstanceUcHome { get; set; }

        #endregion

        #region [ MEMBERS ]

        SerialPort barcodeReaderPortMain { get; set; }

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]

        public frmMK2010() : base()
        {
            //ChartAxisSettingBusiness = new ChartAxisSettingBusiness();


            InitCustomizeSettings();

            base.InitKabanFileSystem();

            InitDefualts();
            InitEvents();
            InitFileWatcher();

            this.FormBorderEffect = DevExpress.XtraEditors.FormBorderEffect.None;
            //StartConnectionMonitor();
        }

        private void InitDefualts()
        {
            string FileWatcherPath = @"C:\KabanTransferMK2010";
            if (!Directory.Exists(FileWatcherPath))
            {
                Directory.CreateDirectory(FileWatcherPath);
            }


            this.MachineInfoBusiness = new MachineInfoBusiness();

            BarcodeReaderBusiness = new BarcodeReaderBusiness();

            var roleName = KabanMachinesSystem.Common.Helpers.UserStatics.RoleName;

            RemoveElements();

            if (roleName == "LimitedUser")
            {
                RestrictAccess(1);
            }
            if (roleName == "User")
            {
                RestrictAccess(2);
            }

            //InitBarcodeReader();
        }

        private void RestrictAccess(int type)
        {
            if (type == 1)
            {
                foreach (var element in mainLeftAccordionControl.Elements)
                {
                    if (element.Name == "btnHome" || element.Name == "btnScrewing")
                        element.Enabled = true;
                    else
                        element.Enabled = false;
                }
            }
            else if (type == 2)
            {
                foreach (var element in mainLeftAccordionControl.Elements)
                {
                    if (element.Name == "btnSettings")
                        element.Enabled = false;
                }
            }

        }

        private void RemoveElements()
        {
            foreach (var element in mainLeftAccordionControl.Elements)
            {
                if (element.Name == "btnCam" || element.Name == "btnPreparation")
                    element.Visible = false;

            }
        }

        private void InitCustomizeSettings()
        {
            var machineTitleName = !string.IsNullOrEmpty(_MK2010.Properties.Settings.Default.MachineHeaderName) ? _MK2010.Properties.Settings.Default.MachineHeaderName : "MK2010";
            MachineInfosStatic.Version = "V.1.0.23";
            //
            Text = $"{base.Text} - { machineTitleName } - { MachineInfosStatic.Version}";

            MainPlcDevice = "D260";
            MachineInfosStatic.MachineName = "MK2010";
            MachineInfosStatic.AlarmDevice = Constant.AlarmDevice;
            MachineInfosStatic.AlaramYesNo_NoActAsCloseForm = false;
            MachineInfosStatic.WarningDevice = "-1";
            MachineInfosStatic.AlarmSize = 3;
            MachineInfosStatic.PopupDevice = Constant.PopupDevice;

            notifyIconMain.Visible = true;
            notifyIconMain.Text = Text;
            tsmVersion.Text = $"Version: {MachineInfosStatic.Version}";

            //Makine ilk defa instance yapılırsa açıyoruz bilgileri dbye gitsin diye
            //MachineInformationTemp.CreateMachineInfo();

            MachineSettingsHelper.SetCurrentMachine(MachineInfosStatic.MachineName);

            mis = MachineInfoSetter.Instance;
            mis.SetMachineInfos();


            MachineSettingsHelper.SetChartSettings();

            //GetChartSettings();

        }

        private void InitFileWatcher()
        {
            FileWatcherBussiness fileWatcher = new FileWatcherBussiness();

            fileWatcher.WatchFolder();
        }

        private void InitBarcodeReader()
        {
            try
            {


                //if (barcodeReaderPortMain != null)
                //{
                //    if (barcodeReaderPortMain.IsOpen)
                //    {
                //        barcodeReaderPortMain.Close(); // Port açıksa önce kapat
                //    }

                //    barcodeReaderPortMain.DataReceived -= BarcodeReaderPortMain_DataReceived; // Event'ten çık

                //    barcodeReaderPortMain.Dispose(); // Hafızayı temizle
                //    barcodeReaderPortMain = null;   // Obje referansını kopar
                //}

                barcodeReaderPortMain = new SerialPort();
                BarcodeReaderBusiness BarcodeReaderBusiness = new BarcodeReaderBusiness();
                var activeSettings = BarcodeReaderBusiness.GetActiveSettings();
                if (activeSettings == null)
                    return;

                if (!activeSettings.IsActive)
                    return;

                if (barcodeReaderPortMain == null)
                    barcodeReaderPortMain = new SerialPort();

                barcodeReaderPortMain.BaudRate = activeSettings.BaudRate;
                barcodeReaderPortMain.DataBits = 8;
                barcodeReaderPortMain.Parity = (Parity)activeSettings.Parity;
                barcodeReaderPortMain.StopBits = (StopBits)activeSettings.StopBits;
                barcodeReaderPortMain.PortName = $"COM{activeSettings.Port}";


                if (!barcodeReaderPortMain.IsOpen)
                    barcodeReaderPortMain.Open();

                barcodeReaderPortMain.DiscardInBuffer();
                barcodeReaderPortMain.DiscardOutBuffer();

                barcodeReaderPortMain.DataReceived -= BarcodeReaderPortMain_DataReceived;
                barcodeReaderPortMain.DataReceived += BarcodeReaderPortMain_DataReceived;
            }
            catch (Exception exp)
            {
                BarcodeReaderConfiguration activeSettings = BarcodeReaderBusiness.GetActiveSettings();
                if (activeSettings == null)
                    return;

                if (activeSettings.IsActive)
                {
                    SplashScreenManager.SendCommand(WaitFormCommand.Hide, null);
                    var message = MessageHelper.GetMessage(MessageType.COMPortException);
                    KabanMessageBox.ShowMessage($"{message} '{barcodeReaderPortMain.PortName}. {Environment.NewLine}Specific Message: {exp.Message}'", KabanDevBase.Enums.MessageBoxType.Question);
                     SplashScreenManager.SendCommand(WaitFormCommand.Show, null);
                }
                // The port does not exist.: 
            }
        }

        //private void StartConnectionMonitor()
        //{
        //    Task.Run(() =>
        //    {
        //        while (true)
        //        {
        //            try
        //            {
        //                if (barcodeReaderPortMain == null)
        //                {
        //                    InitBarcodeReader();
        //                }
        //                else if (barcodeReaderPortMain != null && !barcodeReaderPortMain.IsOpen)
        //                {
        //                    InitBarcodeReader();
        //                }
        //            }
        //            catch (Exception ex)
        //            {

        //                break;
        //            }

        //            Thread.Sleep(5000); // Her 2 saniyede bir dene
        //        }
        //    });
        //}



        private void BarcodeReaderPortMain_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {

                var readData = barcodeReaderPortMain.ReadExisting();

                if (string.IsNullOrEmpty(readData))
                    return;

                this.Invoke((MethodInvoker)delegate
                {
                    // Bu blok artık güvenli bir şekilde UI thread'inde çalışır
                    _InstanceUcHome?._InstanceUcJobPreparations?._instanceucJob?.FillByBarcode(readData);
                });
            }
            catch (Exception ex)
            {

                this.Invoke((MethodInvoker)delegate
                {
                    SplashScreenManager.SendCommand(WaitFormCommand.Hide, null);
                    var message = MessageHelper.GetMessage(MessageType.COMPortException);
                    KabanMessageBox.ShowMessage($"{message} '{barcodeReaderPortMain.PortName}.", KabanDevBase.Enums.MessageBoxType.Question);
                    SplashScreenManager.SendCommand(WaitFormCommand.Show, null);
                });
              

            }


        }






        #endregion

        #region [ EVENTS ]

        private void InitEvents()
        {
            base.OnHomeClick += new KabanMachinesSystem.OnHomeClickDelegate(delegate (object sender, EventArgs e, bool jobDetected, bool isFirstLoad)
            {
                InitBarcodeReader();
                ActivePageHolder.ActivePageEnum = ActivePageEnum.None;
                if (isFirstLoad)
                {
                    _InstanceUcHome = ucHome.Instance();
                }

                if (!isFirstLoad)
                {
                    _InstanceUcHome.RefreshDataSources();
                }


                _InstanceUcHome.Activate_ucMainControlPanel();
                _InstanceUcHome.OnBarcodeReferesh += new Presentation.Home.Preparations.OnBarcodeRefereshDelegate(delegate ()
                {
                    PortChecker.CloseCOMPortIfOpen(this.barcodeReaderPortMain);
                    InitBarcodeReader();
                });


                return _InstanceUcHome;
            });

            base.OnCompanyDetailClick += new KabanMachinesSystem.Presentation.Companies.OnCompanyDetailClickDelegate(delegate ()
            {
                try
                {
                    //PlcComponentReflector.Clear_Common_Wrapper_UcName_Temp();
                    if ((MachineStateChecker.CheckIsMachineWorks(Constants.MachineIs_Active_Device)))
                        return null;

                    var uc = new ucCompanyDetails();
                    Guid CompanyProfileId = new Guid();
                    uc.InitDefaults(CompanyProfileId);
                    return uc;
                }
                catch (Exception exp)
                {
                    return null;
                }
            });

            base.OnSettingsClick += new KabanMachinesSystem.OnSettingsClickDelegate(delegate (object sender, EventArgs e)
            {
                KabanUserControlStatics.ClearActiveControls();

                PortChecker.CloseCOMPortIfOpen(this.barcodeReaderPortMain);
                this.barcodeReaderPortMain = null;

                PlcComponentReflector.Common_Wrapper_UcName_Temp.Remove("ucManuelWrapper");
                return _MK2010.Presentation.Settings.ucSettings.Instance();
            });

            base.OnScrewingClick += new OnScrewingClickDelegate(delegate (object sender, EventArgs e)
            {
                //InitBarcodeReader();
                //ActivePageHolder.ActivePageEnum = ActivePageEnum.None;
                //PlcComponentReflector.Clear_Common_Wrapper_UcName_Temp();//.Remove("ucManuelWrapper");

                //_Instance_H500_Screwing = _H500.Presentation.Home.ucScrewingHome.Instance();
                //Stop_AlarmPanelTimer(screwingAlarms: false);
                //_Instance_H500_Screwing.CheckTabIs_ScrewingJobPreparation();

                //return _Instance_H500_Screwing;

                return null;
            });

            base.OnH800Click += new OnH800ClickDelegate(delegate (object sender, EventArgs e)
            {
                //ActivePageHolder.ActivePageEnum = ActivePageEnum.None;
                //PlcComponentReflector.Clear_Common_Wrapper_UcName_Temp();//.Remove("ucManuelWrapper");

                //_Instance_ucH800Home = _H800.Presentation.Home.ucH800Home.Instance();
                //Stop_AlarmPanelTimer(screwingAlarms: false);
                //_Instance_ucH800Home.CheckTabIs_H800JobPreparation();

                //return _Instance_ucH800Home;

                return null;
            });

            base.OnCheckPrintBarcodeEvent += new OnCheckPrintBarcodeDelegate(delegate ()
            {
                //_Instance_H500_Screwing = _H500.Presentation.Home.ucScrewingHome.Instance();
                //_Instance_H500_Screwing.Check_PrintBarcode();
            });


            base.OnCompanyClick += new OnCompanyClickDelegate(delegate ()
            {
                PortChecker.CloseCOMPortIfOpen(this.barcodeReaderPortMain);
                ActivePageHolder.ActivePageEnum = ActivePageEnum.None;
                Stop_AlarmPanelTimer();
                PlcComponentReflector.Clear_Common_Wrapper_UcName_Temp();//.Remove("ucManuelWrapper");
            });

            base.OnCAMClick += new OnCAMClickDelegate(delegate ()
            {
                ActivePageHolder.ActivePageEnum = ActivePageEnum.None;
                Stop_AlarmPanelTimer();
                PlcComponentReflector.Clear_Common_Wrapper_UcName_Temp();//.Remove("ucManuelWrapper");
            });

            base.OnPreparationClick += new OnPreparationClickDelegate(delegate ()
            {
                ActivePageHolder.ActivePageEnum = ActivePageEnum.None;
                Stop_AlarmPanelTimer();
                PlcComponentReflector.Clear_Common_Wrapper_UcName_Temp();//.Remove("ucManuelWrapper");
            });

        }
        #endregion

        #region [ EVENT METHODS ]

        private void Stop_AlarmPanelTimer(bool mainAlarms = true, bool screwingAlarms = true)
        {
            //if(mainAlarms)
            //    _InstanceUcHome._InstanceUcJobPreparations._instanceMainControl.alarmPanel1.StopTimer_Thread();
        }



        #endregion

        #region [ METHODS ]

        public override void OnParseFileError(ParseFileException e)
        {

            //_InstanceUcHome?.ShowMessage_InfoPanel(e.DisplayUserMsg());

            base.OnParseFileError(e);

        }

        public override void OnNewFile(List<Bar> BarList, string FileName)
        {


            base.OnNewFile(BarList, FileName);

            Thread.Sleep(100);
            //_InstanceUcHome?.FillWaitingJobs();

        }


        #endregion

    }
}
