using _MD3030.Bussiness;
using _MD3030.Presentation.Home.Parameters;
using _MD3030.Presentation.Settings;
using KabanDevBase.Extensions;
using KabanDevBase.Statics;
using KabanMachinesSystem;
using KabanMachinesSystem.Business.BusinessLayer;
using KabanMachinesSystem.Business.Helpers;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.CustomExceptions;
using KabanMachinesSystem.Common.Enums;
using KabanMachinesSystem.Common.Helpers;
using KabanMachinesSystem.Common.Statics;
using KabanMachinesSystem.Data.Entities;
using KabanMachinesSystem.Facade;
using KabanMachinesSystem.Helpers;
using KabanMachinesSystem.KabanSystem.Entities;
using MD3030.Presentation.Home;
using MD3030.Presentation.Home.Manuel;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace MD3030
{
    public partial class frmMD3030 : frmMain
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
        public static IMachineInfoSetter mis;
        public ChartAxisSettingBusiness ChartAxisSettingBusiness { get; set; }

        ucHome _InstanceUcHome { get; set; }

        #endregion

        #region [ MEMBERS ]

        SerialPort barcodeReaderPortMain { get; set; }

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]

        public frmMD3030() : base()
        {
            InitLeftMenu();
            

            InitCustomizeSettings();

            base.InitKabanFileSystem();

            InitDefualts();
            InitEvents();
            //InitFileWatcher();
            InitBarcodeReader();
        }

        private void InitDefualts()
        {
            this.MachineInfoBusiness = new MachineInfoBusiness();
            this.ChartAxisSettingBusiness = new ChartAxisSettingBusiness();

            var roleName = KabanMachinesSystem.Common.Helpers.UserStatics.RoleName;
            if (roleName == "LimitedUser" || roleName == "User")
            {
                RestrictAccess();
            }
        }

        private void InitFileWatcher()
        {
            FileWatcherBussiness fileWatcher = new FileWatcherBussiness();

            fileWatcher.WatchFolder();
        }
        private void RestrictAccess()
        {
            foreach (var element in mainLeftAccordionControl.Elements)
            {
                if (element.Name == "btnHome" || element.Name == "btnCompanies" ||element.Name == "btnManuel")
                    element.Enabled = true;
                else
                    element.Enabled = false;
            }
        }

        private void InitCustomizeSettings()
        {
            var machineTitleName = "MD3030";
            MachineInfosStatic.Version = "V.1.0.0";
            //
            Text = $"{base.Text} - { machineTitleName } - { MachineInfosStatic.Version}";

            MachineInfosStatic.MachineName = "MD3030";

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
        private void InitBarcodeReader()
        {
            try
            {
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
                //BarcodeReaderConfiguration activeSettings = BarcodeReaderBusiness.GetActiveSettings();
                //if (activeSettings == null)
                //    return;

                //if (activeSettings.IsActive)
                //{
                //    SplashScreenManager.SendCommand(WaitFormCommand.Hide, null);
                //    var message = MessageHelper.GetMessage(MessageType.COMPortException);
                //    KabanMessageBox.ShowMessage($"{message} '{barcodeReaderPortMain.PortName}. {Environment.NewLine}Specific Message: {exp.Message}'", KabanDevBase.Enums.MessageBoxType.Question);
                //    SplashScreenManager.SendCommand(WaitFormCommand.Show, null);
                //}
                // The port does not exist.: 
            }
        }

        #endregion

        #region [ EVENTS ]

        private void InitEvents()
        {
            base.OnHomeClick += new KabanMachinesSystem.OnHomeClickDelegate(delegate (object sender, EventArgs e, bool jobDetected, bool isFirstLoad)
            {
                ActivePageHolder.ActivePageEnum = ActivePageEnum.None;
                if (isFirstLoad)
                {
                    _InstanceUcHome = ucHome.Instance();
                }

                if (!isFirstLoad)
                {
                    //_InstanceUcHome.RefreshDataSources();
                }


                _InstanceUcHome.Activate_ucMainControlPanel();



                return _InstanceUcHome;
            });


            base.OnCompanyClick += new OnCompanyClickDelegate(delegate ()
            {
                ActivePageHolder.ActivePageEnum = ActivePageEnum.None;
                Stop_AlarmPanelTimer();
                PlcComponentReflector.Clear_Common_Wrapper_UcName_Temp();//.Remove("ucManuelWrapper");
            });


            btnManuel.Click += (s, e) =>
            {

                SplashScreenManager.ShowWaitForm();


                PortChecker.CloseCOMPortIfOpen(this.barcodeReaderPortMain);
                this.barcodeReaderPortMain = null;

                //this.barcodeReaderPortMain.UnsubscribeDataReceivedEvent();
                this.barcodeReaderPortMain = null;
                KabanUserControlStatics.ClearActiveControls();
                var instance = ucManuel.Instance();
                instance.InitDefaults();
                InitiateInstance(instance, s);

                SplashScreenManager.CloseWaitForm();
            };


            btnParametersMain.Click += (s, e) =>
            {

                SplashScreenManager.ShowWaitForm();

                PortChecker.CloseCOMPortIfOpen(this.barcodeReaderPortMain);
                this.barcodeReaderPortMain = null;

                KabanUserControlStatics.ClearActiveControls();
                //PlcComponentReflector.Common_Wrapper_UcName_Temp.Remove("ucManuelWrapper");
                var instance = ucParameters.Instance();
                instance.InitDefaults();
                InitiateInstance(instance, s);

                SplashScreenManager.CloseWaitForm();
            };

            btnSettings.Click += (s, e) => {
                //ClearControls();

                SplashScreenManager.ShowWaitForm();

                var instance = ucSettings.Instance();
                InitiateInstance(instance, s);

                SplashScreenManager.CloseWaitForm();
            };

        }
        #endregion

        #region [ EVENT METHODS ]

        private void Stop_AlarmPanelTimer(bool mainAlarms = true, bool screwingAlarms = true)
        {

        }

        //private void InitBarcodeReader()
        //{
        //    try
        //    {
        //        //barcodeReaderPortMain = new SerialPort();
        //        //BarcodeReaderBusiness BarcodeReaderBusiness = new BarcodeReaderBusiness();
        //        //var activeSettings = BarcodeReaderBusiness.GetActiveSettings();
        //        //if (activeSettings == null)
        //        //    return;

        //        //if (!activeSettings.IsActive)
        //        //    return;

        //        //if (barcodeReaderPortMain == null)
        //        //    barcodeReaderPortMain = new SerialPort();

        //        //barcodeReaderPortMain.BaudRate = activeSettings.BaudRate;
        //        //barcodeReaderPortMain.DataBits = 8;
        //        //barcodeReaderPortMain.Parity = (Parity)activeSettings.Parity;
        //        //barcodeReaderPortMain.StopBits = (StopBits)activeSettings.StopBits;
        //        //barcodeReaderPortMain.PortName = $"COM{activeSettings.Port}";

        //        //if (!barcodeReaderPortMain.IsOpen)
        //        //    barcodeReaderPortMain.Open();

        //        //barcodeReaderPortMain.DataReceived += BarcodeReaderPortMain_DataReceived;
        //    }
        //    catch (Exception)
        //    {

        //    }
        //}
        
        public void BarcodeReaderPortMain_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var kabanBarcode = barcodeReaderPortMain.ReadExisting();
                if (string.IsNullOrEmpty(kabanBarcode))
                    return;

                kabanBarcode = kabanBarcode.Replace("\r", "");
                kabanBarcode = kabanBarcode.Replace("\u0003", "");
                kabanBarcode = kabanBarcode.Replace("\u0002", "");
                kabanBarcode = kabanBarcode.Replace("\n", "");

                this.Invoke((MethodInvoker)delegate
                {
                    // Bu blok artık güvenli bir şekilde UI thread'inde çalışır
                    //_InstanceUcHome?._InstanceUcJobPreparations?._instanceucJob?.FillByBarcode(readData);
                    _InstanceUcHome?._InstanceUcJobPreparations?.FillByBarcode(kabanBarcode);
                });
                //_Instance_H500_Screwing.Check_AddNewScrewingProcess(kabanBarcode);
            }
            catch (Exception exp)
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

        #region [ METHODS ]

        public override void OnParseFileError(ParseFileException e)
        {

            //_InstanceUcHome?.ShowMessage_InfoPanel(e.DisplayUserMsg());

            //base.OnParseFileError(e);

        }

        public override void OnNewFile(List<Bar> BarList, string FileName)
        {
            //base.OnNewFile(BarList, FileName);

            //Thread.Sleep(100);
            //_InstanceUcHome?.FillWaitingJobs();
        }


        private void InitLeftMenu()
        {
            btnCam.Visible = false;
            accordionControlSeparator2.Visible = false;
            //
            btnPreparation.Visible = false;
            accordionControlSeparator3.Visible = false;

            accordionControlSeparator4.Visible = true;
            //
            btnScrewing.Visible = false;
            accordionControlSeparator5.Visible = false;
            //
            btnManuel.Visible = true;
            accordionControlSeparator6.Visible = true;
            //
            btnParametersMain.Visible = true;
            accordionControlSeparator7.Visible = true;
            //
            btnCamQuix.Visible = false;
            accordionControlSeparator8.Visible = false;
        }


        #endregion

    }
}
