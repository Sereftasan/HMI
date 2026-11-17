using _ZC2010.Temps;
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
using KabanMachinesSystem.Presentation.Home;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace SB3015
{
    public partial class frmSB3015 : frmMain
    {
        private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator1;
        private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator8;
        private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator7;
        private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator2;
        private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator3;
        private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator4;
        private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator5;
        private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator6;
        private DevExpress.XtraBars.Navigation.AccordionControlSeparator accordionControlSeparator9;

        #region [ INSTANCES ]

        MachineInfoBusiness MachineInfoBusiness { get; set; }
        public static IMachineInfoSetter mis;
        public ChartAxisSettingBusiness ChartAxisSettingBusiness { get; set; }
        _SB3015.Presentation.Home.ucHome _InstanceUcHome { get; set; }
        _H500.Presentation.Home.ucScrewingHome _Instance_H500_Screwing { get; set; }

        #endregion

        #region [ MEMBERS ]

        SerialPort barcodeReaderPortMain { get; set; }

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]

        public frmSB3015() : base()
        {
            ChartAxisSettingBusiness = new ChartAxisSettingBusiness();

            InitCustomizeSettings();

            base.InitKabanFileSystem();

            InitDefualts();
            InitEvents();
        }

        private void InitDefualts()
        {
            // TestBits();
            this.MachineInfoBusiness = new MachineInfoBusiness();

            var roleName = KabanMachinesSystem.Common.Helpers.UserStatics.RoleName;
            if (roleName == "LimitedUser")
            {
                RestrictAccess();
            }
        }

        private void RestrictAccess()
        {
            foreach (var element in mainLeftAccordionControl.Elements)
            {
                if (element.Name == "btnHome" || element.Name == "btnScrewing")
                    element.Enabled = true;
                else
                    element.Enabled = false;
            }
        }

        private void InitCustomizeSettings()
        {
            var machineTitleName = "SB3015";// !string.IsNullOrEmpty(_SB3015.Properties.Settings.Default.MachineHeaderName) ? FA2010.Properties.Settings.Default.MachineHeaderName : "FA2010";
            MachineInfosStatic.Version = "V.1.0.6";
            //
            Text = $"{base.Text} - { machineTitleName } - { MachineInfosStatic.Version}";

            MachineInfosStatic.MachineName = "SB3015";

            notifyIconMain.Visible = true;                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                 
            notifyIconMain.Text = Text;
            tsmVersion.Text = $"Version: {MachineInfosStatic.Version}";

            //Makine ilk defa instance yapılırsa açıyoruz bilgileri dbye gitsin diye
            MachineInformationTemp.CreateMachineInfo("SB3015", group: "PVC");

            MachineSettingsHelper.SetCurrentMachine(MachineInfosStatic.MachineName);

            mis = MachineInfoSetter.Instance;
            mis.SetMachineInfos();


            MachineSettingsHelper.SetChartSettings();
            
            //GetChartSettings();

        }


        private void TestBits()
        {
            var arr = new int[] { 2, 4, 6, 8, 4, 6, 8, 10, 2, 1, 10 };
            var ans = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                ans ^= arr[i];
            }


            var nums = new int[] { 2, 4, 3, 2, 4, 2, 4 };
            ans = 0;
            for (int i = 0; i < 32; i++)
            {
                int mask = 1 << i, c = 0;
                foreach (var v in nums)
                    if ((v & mask) != 0)
                        c++;

                if (c % 3 != 0)
                    ans |= mask; // OR 
            }


            int ones = 0, twos = 0;
            foreach (var numx in nums)
            {
                twos |= ones & numx;   // AND then OR
                ones ^= numx;          // XOR toggles
                int three = ones & twos;
                ones &= ~three;     // NOT + AND clears bits seen 3 times
                twos &= ~three;
            }


            var numsTwoSingle = new int[] { 1, 1, 2, 4, 3, 2, 4, 7};

            int x = 0;
            foreach (var num in numsTwoSingle)
            {
                x ^= num;
            }

            int low = x & -x;

            int first = 0;
            int second = 0;

            foreach (var num in numsTwoSingle)
            {
                if ((num & low) == 0)
                    first ^= num;
                else
                    second ^= num;
            }
        }

        //private void GetChartSettings()
        //{
        //    var chartSettings = ChartAxisSettingBusiness.Get_ByParentGuid(MachineInfosStatic.MachineId);
        //    //
        //    MachineInfosStatic.Axis_Y = chartSettings.AxisX_Max;
        //    MachineInfosStatic.Axis_Z = chartSettings.AxisY_Max;
        //    MachineInfosStatic.Min_Axis_Y = chartSettings.AxisX_Min;
        //    MachineInfosStatic.Min_Axis_Z = chartSettings.AxisY_Min;

        //    MachineInfosStatic.CompanyProfile_MaxX = chartSettings.CompanyProfile_MaxX;
        //    MachineInfosStatic.CompanyProfile_MaxY = chartSettings.CompanyProfile_MaxY;
        //    MachineInfosStatic.CompanyProfile_MinX = chartSettings.CompanyProfile_MinX;
        //    MachineInfosStatic.CompanyProfile_MinY = chartSettings.CompanyProfile_MinY;

        //}
        #endregion

        #region [ EVENTS ]

        private void InitEvents()
        {
            base.OnHomeClick += new KabanMachinesSystem.OnHomeClickDelegate(delegate (object sender, EventArgs e, bool jobDetected, bool isFirstLoad)
            {
                ActivePageHolder.ActivePageEnum = ActivePageEnum.None;
                _InstanceUcHome = _SB3015.Presentation.Home.ucHome.Instance();

                if (isFirstLoad)
                {
                    _InstanceUcHome.OnRefreshJobListEvent += () =>
                    {
                        base.ScanDirectory();
                    };

                    _InstanceUcHome.OnSendToScrewingMachine += new _ZC2010.Presentation.Home.Preparations.OnSendToScrewingMachineDelegate(delegate ()
                    {
                        _Instance_H500_Screwing = _H500.Presentation.Home.ucScrewingHome.Instance();
                        _Instance_H500_Screwing.Check_PrintBarcode();
                    });
                }

                if (jobDetected)
                    _InstanceUcHome.SetSelectedTabPate(0);


                
                if (!isFirstLoad)
                {
                    _InstanceUcHome.RefreshDataSources();
                }

                _InstanceUcHome.Activate_ucMainControlPanel();

                Stop_AlarmPanelTimer(mainAlarms:false);

                return _InstanceUcHome;


            });

            base.OnSettingsClick += new KabanMachinesSystem.OnSettingsClickDelegate(delegate (object sender, EventArgs e)
            {
                if (this.barcodeReaderPortMain != null)
                {
                    PortChecker.CloseCOMPortIfOpen(this.barcodeReaderPortMain);
                    this.barcodeReaderPortMain = null;
                }

                ActivePageHolder.ActivePageEnum = ActivePageEnum.None;
                Stop_AlarmPanelTimer();
                PlcComponentReflector.Clear_Common_Wrapper_UcName_Temp();//.Remove("ucManuelWrapper");
                return SB3015.Presentation.Settings.ucSettings.Instance();
            });

            base.OnScrewingClick += new OnScrewingClickDelegate(delegate (object sender, EventArgs e)
            {
                InitBarcodeReader();
                ActivePageHolder.ActivePageEnum = ActivePageEnum.None;
                PlcComponentReflector.Clear_Common_Wrapper_UcName_Temp();//.Remove("ucManuelWrapper");

                _Instance_H500_Screwing = _H500.Presentation.Home.ucScrewingHome.Instance();
                Stop_AlarmPanelTimer(screwingAlarms: false);
                _Instance_H500_Screwing.CheckTabIs_ScrewingJobPreparation();

                return _Instance_H500_Screwing;
            });

            //base.OnH800Click += new OnH800ClickDelegate(delegate (object sender, EventArgs e)
            //{
            //    ActivePageHolder.ActivePageEnum = ActivePageEnum.None;
            //    PlcComponentReflector.Clear_Common_Wrapper_UcName_Temp();//.Remove("ucManuelWrapper");

            //    _Instance_ucH800Home = _H800.Presentation.Home.ucH800Home.Instance();
            //    Stop_AlarmPanelTimer(screwingAlarms: false);
            //    _Instance_ucH800Home.CheckTabIs_H800JobPreparation();

            //    return _Instance_ucH800Home;
            //});

            //base.OnCheckPrintBarcodeEvent += new OnCheckPrintBarcodeDelegate(delegate ()
            //{
            //    _Instance_H500_Screwing = _H500.Presentation.Home.ucScrewingHome.Instance();
            //    _Instance_H500_Screwing.Check_PrintBarcode();

            //    //var machineInfo = MachineInfoBusiness.Get((Guid)MachineInfosStatic.MachineInfo.ScrewingMachineTypeId);
            //    //switch (machineInfo.Name)
            //    //{
            //    //    case "H100":
            //    //        _Instance_H100_Screwing = _H100.Presentation.Home.ucScrewingHome.Instance();
            //    //        _Instance_H100_Screwing.Check_PrintBarcode();

            //    //        break;

            //    //    case "H500":
            //    //        _Instance_H500_Screwing = _H500.Presentation.Home.ucScrewingHome.Instance();
            //    //        _Instance_H500_Screwing.Check_PrintBarcode();

            //    //        break;

            //    //    default:
            //    //        _Instance_H100_Screwing = _H100.Presentation.Home.ucScrewingHome.Instance();
            //    //        _Instance_H100_Screwing.Check_PrintBarcode();

            //    //        break;
            //    //};
            //});


            base.OnCompanyClick += new OnCompanyClickDelegate(delegate ()
            {
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
            if(mainAlarms)
                _InstanceUcHome._InstanceUcJobPreparations._instanceMainControl.alarmPanel1.StopTimer_Thread();

            if (screwingAlarms)
            {
            }
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
            catch (Exception)
            {

            }

            //IBarcodeReaderHelper barcodeReaderHelper = new BarcodeReaderHelper();
            //barcodeReaderHelper.InitBarcodeReader();
            //barcodeReaderHelper.OnBarcodeReaderPortMain_DataReceived += new OnBarcodeReaderPortMain_DataReceivedDelegate(delegate (string receivedData)
            //{
            //    var labelData = receivedData;
            //});
        }
        
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

                //_Instance_H500_Screwing.Check_AddNewScrewingProcess(kabanBarcode);
            }
            catch (Exception exp)
            {
                
            }
        }

        #endregion

        #region [ METHODS ]

        public override void OnParseFileError(ParseFileException e)
        {

            _InstanceUcHome?.ShowMessage_InfoPanel(e.DisplayUserMsg());

            base.OnParseFileError(e);

        }

        public override void OnNewFile(List<Bar> BarList, string FileName)
        {


            base.OnNewFile(BarList, FileName);

            Thread.Sleep(100);
            _InstanceUcHome?.FillWaitingJobs();



        }




        #endregion

        #region [Commented]

        //private void InitializeComponent()
        //{
        //    System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmFA2010));
        //    this.accordionControlSeparator1 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.accordionControlSeparator8 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.accordionControlSeparator7 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.accordionControlSeparator2 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.accordionControlSeparator3 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.accordionControlSeparator4 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.accordionControlSeparator5 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.accordionControlSeparator6 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    ((System.ComponentModel.ISupportInitialize)(this.fluentDesignFormMain)).BeginInit();
        //    ((System.ComponentModel.ISupportInitialize)(this.mainLeftAccordionControl)).BeginInit();
        //    this.SuspendLayout();
        //    // 
        //    // btnCompanies
        //    // 
        //    this.btnCompanies.Appearance.Pressed.Options.UseBorderColor = true;
        //    this.btnCompanies.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnCompanies.ImageOptions.SvgImage")));
        //    // 
        //    // btnCam
        //    // 
        //    this.btnCam.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnCam.ImageOptions.SvgImage")));
        //    // 
        //    // btnPreparation
        //    // 
        //    this.btnPreparation.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnPreparation.ImageOptions.SvgImage")));
        //    // 
        //    // btnHome
        //    // 
        //    this.btnHome.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnHome.ImageOptions.SvgImage")));
        //    // 
        //    // btnSettings
        //    // 
        //    this.btnSettings.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnSettings.ImageOptions.SvgImage")));
        //    // 
        //    // MainContainer
        //    // 
        //    this.MainContainer.Size = new System.Drawing.Size(963, 717);
        //    // 
        //    // fluentDesignFormMain
        //    // 
        //    this.fluentDesignFormMain.Size = new System.Drawing.Size(1136, 40);
        //    // 
        //    // mainLeftAccordionControl
        //    // 
        //    this.mainLeftAccordionControl.Appearance.Item.Hovered.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(215)))), ((int)(((byte)(16)))), ((int)(((byte)(21)))));
        //    this.mainLeftAccordionControl.Appearance.Item.Hovered.Options.UseBackColor = true;
        //    this.mainLeftAccordionControl.Appearance.Item.Normal.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
        //    this.mainLeftAccordionControl.Appearance.Item.Normal.Options.UseBackColor = true;
        //    this.mainLeftAccordionControl.Appearance.Item.Pressed.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
        //    this.mainLeftAccordionControl.Appearance.Item.Pressed.Options.UseBackColor = true;
        //    this.mainLeftAccordionControl.Size = new System.Drawing.Size(173, 717);
        //    // 
        //    // btnScrewing
        //    // 
        //    this.btnScrewing.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnScrewing.ImageOptions.SvgImage")));
        //    // 
        //    // btnManuel
        //    // 
        //    this.btnManuel.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnManuel.ImageOptions.SvgImage")));
        //    // 
        //    // btnParametersMain
        //    // 
        //    this.btnParametersMain.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnParametersMain.ImageOptions.SvgImage")));
        //    // 
        //    // btnCamQuix
        //    // 
        //    this.btnCamQuix.ImageOptions.SvgImage = ((DevExpress.Utils.Svg.SvgImage)(resources.GetObject("btnCamQuix.ImageOptions.SvgImage")));
        //    // 
        //    // accordionControlSeparator1
        //    // 
        //    this.accordionControlSeparator1.Name = "accordionControlSeparator1";
        //    // 
        //    // accordionControlSeparator8
        //    // 
        //    this.accordionControlSeparator8.Name = "accordionControlSeparator8";
        //    this.accordionControlSeparator8.Visible = false;
        //    // 
        //    // accordionControlSeparator7
        //    // 
        //    this.accordionControlSeparator7.Name = "accordionControlSeparator7";
        //    this.accordionControlSeparator7.Visible = false;
        //    // 
        //    // accordionControlSeparator2
        //    // 
        //    this.accordionControlSeparator2.Name = "accordionControlSeparator2";
        //    // 
        //    // accordionControlSeparator3
        //    // 
        //    this.accordionControlSeparator3.Name = "accordionControlSeparator3";
        //    // 
        //    // accordionControlSeparator4
        //    // 
        //    this.accordionControlSeparator4.Name = "accordionControlSeparator4";
        //    // 
        //    // accordionControlSeparator5
        //    // 
        //    this.accordionControlSeparator5.Name = "accordionControlSeparator5";
        //    // 
        //    // accordionControlSeparator6
        //    // 
        //    this.accordionControlSeparator6.Name = "accordionControlSeparator6";
        //    this.accordionControlSeparator6.Visible = false;
        //    // 
        //    // frmFA2010
        //    // 
        //    this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        //    this.ClientSize = new System.Drawing.Size(1136, 757);
        //    this.Location = new System.Drawing.Point(0, 0);
        //    this.LookAndFeel.TouchScaleFactor = 1.5F;
        //    this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
        //    this.Name = "frmFA2010";
        //    this.Controls.SetChildIndex(this.fluentDesignFormMain, 0);
        //    this.Controls.SetChildIndex(this.mainLeftAccordionControl, 0);
        //    this.Controls.SetChildIndex(this.MainContainer, 0);
        //    ((System.ComponentModel.ISupportInitialize)(this.fluentDesignFormMain)).EndInit();
        //    ((System.ComponentModel.ISupportInitialize)(this.mainLeftAccordionControl)).EndInit();
        //    this.ResumeLayout(false);

        //}

        #endregion

        //private void InitializeComponent()
        //{
        //    this.accordionControlSeparator9 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.accordionControlSeparator1 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.accordionControlSeparator8 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.accordionControlSeparator7 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.accordionControlSeparator2 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.accordionControlSeparator3 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.accordionControlSeparator4 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.accordionControlSeparator5 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.accordionControlSeparator6 = new DevExpress.XtraBars.Navigation.AccordionControlSeparator();
        //    this.SuspendLayout();
        //    // 
        //    // MainContainer
        //    // 
        //    this.MainContainer.Size = new System.Drawing.Size(963, 717);
        //    // 
        //    // accordionControlSeparator9
        //    // 
        //    this.accordionControlSeparator9.Name = "accordionControlSeparator9";
        //    // 
        //    // accordionControlSeparator1
        //    // 
        //    this.accordionControlSeparator1.Name = "accordionControlSeparator1";
        //    // 
        //    // accordionControlSeparator8
        //    // 
        //    this.accordionControlSeparator8.Name = "accordionControlSeparator8";
        //    this.accordionControlSeparator8.Visible = false;
        //    // 
        //    // accordionControlSeparator7
        //    // 
        //    this.accordionControlSeparator7.Name = "accordionControlSeparator7";
        //    this.accordionControlSeparator7.Visible = false;
        //    // 
        //    // accordionControlSeparator2
        //    // 
        //    this.accordionControlSeparator2.Name = "accordionControlSeparator2";
        //    // 
        //    // accordionControlSeparator3
        //    // 
        //    this.accordionControlSeparator3.Name = "accordionControlSeparator3";
        //    // 
        //    // accordionControlSeparator4
        //    // 
        //    this.accordionControlSeparator4.Name = "accordionControlSeparator4";
        //    // 
        //    // accordionControlSeparator5
        //    // 
        //    this.accordionControlSeparator5.Name = "accordionControlSeparator5";
        //    // 
        //    // accordionControlSeparator6
        //    // 
        //    this.accordionControlSeparator6.Name = "accordionControlSeparator6";
        //    this.accordionControlSeparator6.Visible = false;
        //    // 
        //    // frmFA2010
        //    // 
        //    this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
        //    this.ClientSize = new System.Drawing.Size(1136, 757);
        //    this.Location = new System.Drawing.Point(0, 0);
        //    this.LookAndFeel.TouchScaleFactor = 1.5F;
        //    this.LookAndFeel.TouchUIMode = DevExpress.Utils.DefaultBoolean.True;
        //    this.Name = "frmFA2010";
        //    this.Controls.SetChildIndex(this.MainContainer, 0);
        //    this.ResumeLayout(false);

        //}
    }
}
