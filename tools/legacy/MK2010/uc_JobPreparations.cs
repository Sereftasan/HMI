using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanLabelDesigner;
using KabanMachinesSystem.Business.BusinessLayer;
using KabanMachinesSystem.Business.Helpers;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Enums;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Data.Entities;
using KabanMachinesSystem.Data.ViewModels;
using KabanMachinesSystem.Helpers;
using KabanMachinesSystem.KabanSystem;
using KabanMachinesSystem.KabanSystem.ViewModels;
using KabanMachinesSystem.Presentation.Home;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KabanMachinesSystem.Common.Extensions;
using KabanMachinesSystem.Patterns.Strategies.TransferStrategy;
using KabanMachinesSystem.Common.Helpers;
using KabanDevBase.Statics;
using KabanMachinesSystem.Common.Statics;

namespace _MK2010.Presentation.Home.Preparations
{
    public delegate void OnSendToScrewingMachineDelegate();
    public delegate void OnRefreshJobListDelegate();
    public delegate void OnSendJobDelegate();

    public partial class uc_JobPreparations : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]



        public event OnRefreshJobListDelegate OnRefreshJobListEvent = null;
        public event OnSendToScrewingMachineDelegate OnSendToScrewingMachine = null;
        public event OnSendJobDelegate OnSendJobEvent = null;

        private static uc_JobPreparations _instance;
        public ucJobNew _instanceMainControl;
        

        ProcessImporter ProccesingCenterImporter { get; set; }
        TransferProvider TransferProvider { get; set; }
        CuttingListhHeaderBusiness CuttingListhHeaderBusiness { get; set; }
        MachineCleaningInformationBusiness MachineCleaningInformationBusiness { get; set; }
        CuttingListItemBusiness CuttingListItemBusiness { get; set; }
        LabelHeaderBusiness LabelHeaderBusiness { get; set; }
        LabelDataBusiness LabelDataBusiness { get; set; }

        JobProvider JobProvider { get; set; }
        JobBusiness JobBusiness { get; set; }

        TransferProvider_FXCpu TransferProvider_FXCpu { get; set; }

        TransferScrewing TransferScrewing { get; set; }
        #endregion

        #region [ MEMBERS ]

        BarWithStatusVM CurrentBar { get; set; }
        List<KabanMachinesSystem.KabanSystem.ViewModels.BarWithStatusVM> CurrentBarList { get; set; }

        Job CurrentJobEntity { get; set; }

        Task Task_Check_NextJob = null;
        Task Task_UnitsGCodeStatus = null;
        Task Task_SawGCodeStatus = null;
        Task Task_PrintBarcode = null;
        Task Task_ReadProfileInformation = null;



        //bool IsPageActive = true;

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected uc_JobPreparations()
        {
            InitializeComponent();


            ProccesingCenterImporter = new ProcessImporter();
            TransferProvider = new TransferProvider();
            JobProvider = new JobProvider();
            TransferProvider_FXCpu = new TransferProvider_FXCpu();
            CuttingListhHeaderBusiness = new CuttingListhHeaderBusiness();
            MachineCleaningInformationBusiness = new MachineCleaningInformationBusiness();
            TransferScrewing = new TransferScrewing();
            JobBusiness = new JobBusiness();
            CuttingListItemBusiness = new CuttingListItemBusiness();
            LabelHeaderBusiness = new LabelHeaderBusiness();
            LabelDataBusiness = new LabelDataBusiness();


            InitDefaults();
            InitEvents();

            Translator.ChangeLangue_ByUI(this);
        }



        public static uc_JobPreparations Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new uc_JobPreparations();
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
            InitiateInstances();
        }

        private void InitiateInstances()
        {
            

            _instanceMainControl = ucJobNew.Instance();
            InitiateInstance(_instanceMainControl, kscJobPreparation.Panel2);

            

            var waitingCuttingHeaders = CuttingListhHeaderBusiness.Get_AllWaiting();
            if (waitingCuttingHeaders == null || waitingCuttingHeaders.Count == 0)
                return;

            var jobEntity = JobBusiness.Get(waitingCuttingHeaders.FirstOrDefault().JobFileId);

            

            

            //SendJob(jobEntity, false);


        }




        #endregion

        #region [ EVENTS ]

        private void InitEvents()
        {
            

            
        }


        private void StartJob()
        {
            if (CurrentJobEntity == null)
            {
                
                var msg = MessageHelper.GetMessage(MessageType.BarEmpty) + " - " + DateTime.Now;
                base.ShowToastNotify(msg);
                return;
            }

           

            var barList = ProccesingCenterImporter.GetCurrentBarList(CurrentJobEntity.FullPath, CurrentJobEntity.Id);

            // Önceden kesilmiş barları çıkartıyoruz.
            barList = Calc_UnCuttingBarList(CurrentJobEntity, barList);


            // bu kısmı Calc_UnCuttingBarList metodundan elde edebiliriz boş yere iki kere dönmeyelim #TYFN
            CurrentBarList = new List<KabanMachinesSystem.KabanSystem.ViewModels.BarWithStatusVM>();
            foreach (var q in barList)
            {
                CurrentBarList.Add(
                    new KabanMachinesSystem.KabanSystem.ViewModels.BarWithStatusVM
                    {
                        Bar = q,
                        Finished = false,
                        JobEntity = CurrentJobEntity
                    });
            }


            

            var wasteMode = Plc.GetDevice(Constants.WasteMode);
            var wasteModeRunning = Plc.GetDevice(Constants.WasteModeRunning);
            if (wasteMode != 1) // || (wasteMode == 1 && wasteModeRunning == 1))
            {
                CurrentBar = GetNextBar();

                if (CurrentBarList?.Count > 1)
                    Plc.SetDevice("L10", 1);
                else
                    Plc.SetDevice("L10", 0);

                this.ResultEntity = SendBar(CurrentBar.Bar, CurrentJobEntity, true);
            }

            Plc.SetDevice(Constants.MachineIsWorking_Device, 1);
        }


        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]

        private List<KabanMachinesSystem.KabanSystem.Entities.Bar> Calc_UnCuttingBarList(Job JobEntity, List<KabanMachinesSystem.KabanSystem.Entities.Bar> bars)
        {

            try
            {
                List<KabanMachinesSystem.KabanSystem.Entities.Bar> unCuttingBar_List = new List<KabanMachinesSystem.KabanSystem.Entities.Bar>();


                var unCuttingBar_Items = CuttingListItemBusiness.Get_AllByJobFileId(JobEntity.Id);

                for (int i = 0; i < bars.Count; i++)
                {
                    var bar = bars[i];

                    var unCut = unCuttingBar_Items.FirstOrDefault(q => q.BarNo == bar.No);
                    if (unCut == null)
                        continue;

                    unCuttingBar_List.Add(bar);

                }

                return unCuttingBar_List;

            }
            catch (Exception exp)
            {
                throw exp;
            }

        }

        public void Reload_ActiveJobScreen()
        {
            if (CurrentBar == null)
                return;

            LoadActiveJobScreen(CurrentBar.JobEntity);//, CurrentBar.Finished);
            if (ResultEntity != null && ResultEntity.HasError)
            {
                var msg = MessageHelper.GetMessage(MessageType.JobSentFailed) + Environment.NewLine + CurrentBar.JobEntity.FileName + Environment.NewLine + ResultEntity.Message;
                base.ShowToastNotify(msg);
            }
        }


        public void RefreshDataSources()
        {
            // In frmFA2010 TabChanged and JobPreparation turned to active
            Reload_ActiveJobScreen();
        }

        public void FillWaitingJobs()
        {
        }

        public void ShowMessage_InfoPanel(string message)
        {
        }


        List<KabanMachinesSystem.KabanSystem.Entities.Bar> BarList { get; set; }
        public Dictionary<double, List<KabanMachinesSystem.KabanSystem.Entities.Profile>> OptimizationModel_Dict { get; set; }
        public string Content { get; set; }
        Job JobEntity { get; set; }
        public void SendJob(Job jobEntity, int wasteMode)
        {
            try
            {
                this.Content = string.Empty;
                this.BarList = new List<KabanMachinesSystem.KabanSystem.Entities.Bar>();
                this.OptimizationModel_Dict = new Dictionary<double, List<KabanMachinesSystem.KabanSystem.Entities.Profile>>();
                this.JobEntity = jobEntity;
                if (jobEntity == null)
                    return;

                base.ShowWaitForm();


                //_instanceJobList.OptimizeChangedFiles(this.BarList, jobEntity);
                

                var barList = ProccesingCenterImporter.GetCurrentBarList(jobEntity.FullPath, jobEntity.Id);
                if (barList == null || barList.Count == 0)
                {
                    var msg = MessageHelper.GetMessage(MessageType.BarEmpty) + Environment.NewLine + jobEntity.FileName;
                    base.ShowToastNotify(msg);
                    return;
                }


                this.BarList = barList;
                this.OptimizationModel_Dict = ProccesingCenterImporter.OptimizationModel_Dict;
                this.Content = ProccesingCenterImporter.Content;

                var result = ProccesingCenterImporter.Send(barList, jobEntity);// (barList, jobEntity.FullPath, jobEntity.Id);
                if (!result)
                {
                    base.CloseWaitForm();
                    var msg = MessageHelper.GetMessage(MessageType.JobSentFailed) + Environment.NewLine + jobEntity.FileName;
                    base.ShowToastNotify(msg);
                    return;
                }

                CurrentBarList = new List<KabanMachinesSystem.KabanSystem.ViewModels.BarWithStatusVM>();
                foreach (var q in barList)
                {
                    CurrentBarList.Add(
                        new KabanMachinesSystem.KabanSystem.ViewModels.BarWithStatusVM
                        {
                            Bar = q,
                            Finished = false,
                            JobEntity = jobEntity
                        });
                }

                CurrentJobEntity = jobEntity;

                //CuttingListhHeaderBusiness.Update_AllFinishedByDate(jobEntity.SendDate, jobEntity.Id);
                //JobProvider.UpdateJobStatus(jobEntity.Id, KabanMachinesSystem.Common.Enums.JobStateEnum.Finished);

                //var transferOK = SendBar(CurrentBarList[0], jobEntity);

                //Plc.SetDevice("L0", 0);


                LoadActiveJobScreen(jobEntity);

                FillWaitingJobs();


                base.CloseWaitForm();

                //Check_MachineCleaning();
            }
            catch (Exception exp)
            {
                base.CloseWaitForm();

                JobProvider.UpdateJobStatus(jobEntity.Id, KabanMachinesSystem.Common.Enums.JobStateEnum.Failed, exp);

                var msg = MessageHelper.GetMessage(MessageType.JobSentFailed) + Environment.NewLine + jobEntity.FileName;
                base.ShowToastNotify(msg);

            }
        }


        
        ResultEntity ResultEntity { get; set; }
        private ResultEntity SendBar(KabanMachinesSystem.KabanSystem.Entities.Bar bar, Job jobEntity, bool showMessage)
        {


            ResultEntity transferOK;

            if (MachineInfosStatic.MachineName == "ZC2010")
                transferOK = TransferProvider.Transfer(bar, jobEntity.Id);
            else
                transferOK = TransferProvider_FXCpu.Transfer(bar, jobEntity.Id);

            if (!transferOK.HasError)
            {
                Plc.SetDevice("L0", 0);
                Plc.SetDevice("L1", 1);

                // Önceki kesimleri iptal ediyoruz.
                //CuttingListhHeaderBusiness.Update_AllFinished();
                //CuttingListhHeaderBusiness.Update_AllFinishedByDate(jobEntity.SendDate, jobEntity.Id);
                //JobProvider.UpdateJobStatus(jobEntity.Id, KabanMachinesSystem.Common.Enums.JobStateEnum.Finished);

                if (ActivePageHolder.ActivePageEnum == ActivePageEnum.JobPreparation)
                    LoadActiveJobScreen(jobEntity);


                if (showMessage)
                {
                    var msg = MessageHelper.GetMessage(MessageType.JobSentSuccessfully) + Environment.NewLine + jobEntity.FileName;
                    base.ShowToastNotify(msg);

                    if (ActivePageHolder.ActivePageEnum == ActivePageEnum.JobPreparation)
                    {

                    }
                        
                }



            }
            else
            {
                Plc.SetDevice("L0", 0);

                var msg = MessageHelper.GetMessage(MessageType.JobSentFailed) + Environment.NewLine + jobEntity.FileName + Environment.NewLine + transferOK.Message;
                base.ShowToastNotify(msg);

                if (ActivePageHolder.ActivePageEnum == ActivePageEnum.JobPreparation)
                {

                }
            }



            return transferOK;

        }


        private void LoadActiveJobScreen(Job jobEntity)//, bool currentBarIsFinished = false)
        {
            
        }



        public int WasteLength { get; set; }
        public void AsyncReadPLC()
        {

            try
            {
                var machineIsWorking = Plc.GetDevice(Constants.MachineIsWorking_Device);
                if (machineIsWorking == 0)
                    return;

                //var wasteMode = Plc.GetDevice(Constants.WasteMode);

                //if (wasteMode == 1)
                //{
                //    var l0 = Plc.GetDevice("L0");
                //    var l1 = Plc.GetDevice("L1");
                //    if (l0 == 1)
                //    {
                //        if (l1 == 0)
                //        {
                //            _instanceJobList?.CheckIf_ProcessedWasteFileRaised();
                //        }

                //        // D89
                //        var measured_ProfileLength = Plc.GetDevice(Constants.ProfileInfo_Length);
                //        if (measured_ProfileLength <= 0)
                //            return;

                //        CreateWasteFile_ByMeasuredProfileLength(measured_ProfileLength);

                //        Plc.SetDevice("L0", 0);
                //    }

                //    //var wasteModeRunning = Plc.GetDevice(Constants.WasteModeRunning);
                //    //if (wasteModeRunning != 0)
                //    //    return;

                //}
                //else
                //    WasteFileInfo.Clear();


                Check_PrintBarcode();
                Check_NextJob();
                Check_UnitsGCodeStatus();
                Check_SawGCodeStatus();

                //Task_Check_NextJob.Start2Complation(Check_NextJob);
                //Task_UnitsGCodeStatus.Start2Complation(Check_UnitsGCodeStatus);
                //Task_SawGCodeStatus.Start2Complation(Check_SawGCodeStatus);
                //Task_PrintBarcode.Start2Complation(Check_PrintBarcode);

                if (ActivePageHolder.ActivePageEnum == ActivePageEnum.JobPreparation)
                    Task_ReadProfileInformation.Start2Complation(ReadProfileInformation);
            }
            catch (Exception exp)
            {
                CommonLog.WriteToFile("Thread.txt", $"Error FA 2010 ucJobPreparation AsyncReadPLC(): {exp.Message}", true, true);
            }


            //var task1 = new Task(() => { Check_NextJob(); });
            //task1.Start();

            //var task2 = new Task(() => { Check_UnitsGCodeStatus(); });
            //task2.Start();

            //var task3 = new Task(() => { Check_SawGCodeStatus(); });
            //task3.Start();

            //var task4 = new Task(() => { Check_PrintBarcode(); });
            //task4.Start();

            //var task5 = new Task(() => { ReadProfileInformation(); });
            //task5.Start();




            //ReadProfileInformation();

            //Check_SawGCodeStatus();

            //Check_PrintBarcode();

            //Check_NextJob();

            //Check_UnitsGCodeStatus();


            #region RobotGripperLog
            //var L3Value = Plc.ReadDevice(Constants.RobotGripperReport);
            //if (L3Value == 1)
            //{
            //    Plc.SetDevice(Constants.RobotGripperReport, 0);
            //    // Robot Gripper Log Oluşturuyoruz
            //    RobotGripperLogProvider.CreateLog("");
            //}
            #endregion


        }

        private void CreateWasteFile_ByMeasuredProfileLength(int measured_ProfileLength)
        {
            if (this.BarList == null)
                return;

            Plc.SetDevice(Constants.WasteModeRunning, 1);
            

           
        }

        object lockObj = new object();
        List<CuttingListHeader> CuttingListHeaders_Temp { get; set; }
        private void Check_NextJob()
        {
            try
            {
                var L0Value = Plc.ReadDevice("L0");
                if (L0Value == 0)
                {
                    return;
                }


                


                lock (lockObj)
                {
                    CommonLog.WriteToFile("SendJob.txt", $"lockObj: Lock Started ", false);

                    
                    var CurrentProcessTimeVal = Plc.ReadDevice(Constants.CurrentProcessTimeDevice);
                    
                    //ucActiveJobScreen.SetPreviousJobId();


                    
                    

                }
            }
            catch (Exception exp)
            {
                CommonLog.WriteToFile("SendJob.txt", $"exp: {exp.Message}", true);
                throw exp;
            }


        }


        private BarWithStatusVM GetNextBar()
        {
            if (CurrentBarList == null)
                return null;

            var currentBars = CurrentBarList.Where(i => i.Finished == false).ToList();
            if (currentBars.Count == 0)
                return null;

            var currentBar = currentBars.OrderBy(i => i.BarNo).FirstOrDefault();
            if (currentBar == null)
                return null;

            return currentBar;

        }


        private void Check_PrintBarcode()
        {
            try
            {
                CommonLog.WriteToFile(CommonLog.PrinterLog_FileName, "Check_PrintBarcode Started", false);
                var printBarcodeDevice = Plc.ReadDevice(Constants.PrintBarcode);
                if (printBarcodeDevice == 0)
                {
                    CheckLeftOverBarcode(CurrentBar);
                    return;
                }

                CommonLog.WriteToFile(CommonLog.PrinterLog_FileName, $"printBarcodeDevice: {Constants.PrintBarcode}", true);

                var ProcessNo = Plc.ReadDevice32Bit(Constants.ProcessNo);

                CommonLog.WriteToFile(CommonLog.PrinterLog_FileName, $"ProcessNo: {Constants.ProcessNo}", true);

                Plc.SetDevice(Constants.PrintBarcode, 0);

                CommonLog.WriteToFile(CommonLog.PrinterLog_FileName, $"PrintBarcode Wrote to PLC", true);

                //var currentCuttingHeaderId = ucActiveJobScreen?.FinishCurrentBar();
                if (this.CuttingListHeaders_Temp?.Count == 1)
                {
                    
                }


                var labelData = OnPrintBarcode(ProcessNo);
                if (CurrentBar == null)
                    return;

                CurrentBar.Finished = true;


                // If profile's NotScrewable property set to true we don't have to send it to Screwing Machine.
                var notScrewable = CurrentBar.Bar.ProfileInfoVM.NotScrewable != null ?
                                    (bool)CurrentBar.Bar.ProfileInfoVM.NotScrewable : false;
                if (notScrewable)
                    return;

                var result = TransferScrewing.SendToScewingDB(CurrentBar.Bar.ProfileInfoVM.Id, ProcessNo, labelData);
                if (result)
                    OnSendToScrewingMachine?.Invoke();
            }
            catch (Exception exp)
            {
                CommonLog.WriteToFile(CommonLog.PrinterLog_FileName, $"Check_PrintBarcode Exception: {exp.Message}", true);
            }

        }

        private void CheckLeftOverBarcode(BarWithStatusVM currentBar)
        {
            try
            {
                var leftOverBarcode = Plc.ReadDevice(Constants.LeftOverBacode);
                if (leftOverBarcode == 0)
                    return;

                var line = "";
                if (currentBar != null && currentBar.Bar != null)
                    line = currentBar.Bar.Line;

                var leftOverCuttingLength = Plc.ReadDevice32Bit(Constants.LeftOverCuttingLength);
                var leftoverDouble = leftOverCuttingLength >= 0 ? (double)leftOverCuttingLength/ 10d : 0d;

                CommonLog.WriteToFile("LeftOverLengthList.txt", $"\"{line}\"  |  {leftoverDouble.ToString()}", true, false);

                Plc.SetDevice(Constants.LeftOverBacode, 0);
                OnPrintLeftOverBarcode((double)leftOverCuttingLength/10d);
            }
            catch (Exception exp)
            {
                throw exp;
            }
        }

        private LabelData OnPrintBarcode(int ProcessNo)
        {
            var cuttingListItemGuid = Guid.Empty;
            LabelData labelDataEntity = null;
            try
            {
                if (!MachineInfosStatic.MachineInfo.UsePrinter.HasValue || MachineInfosStatic.MachineInfo.UsePrinter == false)
                    return labelDataEntity;

                CommonLog.WriteToFile(CommonLog.PrinterLog_FileName, "OnPrintBarcode Started", true);

                var activeLabel = LoadActiveLabels();
                if (activeLabel == null)
                {
                    var msg = MessageHelper.GetMessage(MessageType.ActiveLabelNotExist);
                    base.ShowToastNotify(msg);
                    KabanLog.Write(msg);
                    return labelDataEntity;
                }

                labelDataEntity = LabelDataBusiness.GetByParentNo(ProcessNo);
                if (labelDataEntity == null)
                    return labelDataEntity;


                cuttingListItemGuid = labelDataEntity.CuttingListItemId != null ? (Guid)labelDataEntity.CuttingListItemId : Guid.Empty;

                KabanLabelDesigner.Statics.LabelFieldProviderHelper.Transfer(activeLabel, labelDataEntity, MachineInfosStatic.KabanFileSystemHelper.TransferDirectoryImages);

                CommonLog.WriteToFile(CommonLog.PrinterLog_FileName, $"ZebraPrinterSender Started. Port: {activeLabel.ComPort}", true);
                ZebraPrinterSender printer = new ZebraPrinterSender(activeLabel.ComPort);
                CommonLog.WriteToFile(CommonLog.PrinterLog_FileName, $"ZebraPrinterSender Is Ready. Port: {activeLabel.ComPort}", true);

                printer.PrintLabel(activeLabel);

                CommonLog.WriteToFile(CommonLog.PrinterLog_FileName, $"ZebraPrinterSender Ended.", true);
            }
            catch (Exception exp)
            {
                CommonLog.WriteToFile(CommonLog.PrinterLog_FileName, $"OnPrintBarcode Exception: {exp.Message}", true);
            }
            finally
            {
                if (cuttingListItemGuid != Guid.Empty)
                {
                    CuttingListItemBusiness.Update_As_IsPrinted(cuttingListItemGuid);
                    OnUpdateOriginalFromArchiveProfile(cuttingListItemGuid);
                }
            }

            return labelDataEntity;
        }

        private void OnUpdateOriginalFromArchiveProfile(Guid cuttingListItemGuid)
        {
            if (CurrentBar != null && CurrentBar.IsArchiveFile)
            {
                this.CuttingListItemBusiness.UpdateOriginalByArchive_As_IsPrinted(cuttingListItemGuid);
            }
        }

        private void OnPrintLeftOverBarcode(double leftOverCuttingLength)
        {
            var cuttingListItemGuid = Guid.Empty;
            try
            {
                if (!MachineInfosStatic.MachineInfo.UsePrinter.HasValue || MachineInfosStatic.MachineInfo.UsePrinter == false)
                    return;


                var activeLeftOver = LoadLeftOverLabels();
                if (activeLeftOver == null)
                {
                    var msg = MessageHelper.GetMessage(MessageType.ActiveLabelNotExist);
                    base.ShowToastNotify(msg);
                    KabanLog.Write(msg);
                    return;
                }


                KabanLabelDesigner.Statics.LabelFieldProviderHelper.TransferLeftOver(activeLeftOver, leftOverCuttingLength, MachineInfosStatic.KabanFileSystemHelper.TransferDirectoryImages);

                CommonLog.WriteToFile(CommonLog.PrinterLog_FileName, $"ZebraPrinterSender Started. Port: {activeLeftOver.ComPort}", true);
                ZebraPrinterSender printer = new ZebraPrinterSender(activeLeftOver.ComPort);
                CommonLog.WriteToFile(CommonLog.PrinterLog_FileName, $"ZebraPrinterSender Is Ready. Port: {activeLeftOver.ComPort}", true);

                printer.PrintLabel(activeLeftOver);

                CommonLog.WriteToFile(CommonLog.PrinterLog_FileName, $"ZebraPrinterSender Ended.", true);



            }
            catch (Exception exp)
            {
                CommonLog.WriteToFile(CommonLog.PrinterLog_FileName, $"OnPrintBarcode Exception: {exp.Message}", true);
            }
            finally
            {
                if (cuttingListItemGuid != Guid.Empty)
                    CuttingListItemBusiness.Update_As_IsPrinted(cuttingListItemGuid);
            }


        }

        private KabanLabelDesigner.Models.LabelHeaderProvider LoadActiveLabels()
        {
            var activeLabel = LabelHeaderBusiness.Get_ActiveLabel();
            if (activeLabel == null)
                return null;


            var LabelHeaderProvider = new KabanLabelDesigner.Models.LabelHeaderProvider(activeLabel.Name, activeLabel.Size);


            LabelHeaderProvider.Fields.Clear();
            LabelHeaderProvider.Load(activeLabel.Id);

            return LabelHeaderProvider;
        }

        private KabanLabelDesigner.Models.LabelHeaderProvider LoadLeftOverLabels()
        {
            var leftOverLabel = LabelHeaderBusiness.Get_LeftOverLabel();
            if (leftOverLabel == null)
                return null;


            var LabelHeaderProvider = new KabanLabelDesigner.Models.LabelHeaderProvider(leftOverLabel.Name, leftOverLabel.Size);


            LabelHeaderProvider.Fields.Clear();
            LabelHeaderProvider.Load(leftOverLabel.Id);

            return LabelHeaderProvider;
        }


        private void ReadProfileInformation()
        {
        }

        private void Check_SawGCodeStatus()
        {
            try
            {
                var status = Plc.ReadDevice(Constants.Saw_SendGCode);
                if (status == 1)
                {

                    var ProcessNo = Plc.ReadDevice(Constants.Saw_ProcessNo);

                    TransferProvider_FXCpu.WriteSawGCodes_ByNo(ProcessNo);

                    Plc.SetDevice(Constants.Saw_SendGCode, 0);
                    Plc.SetDevice(Constants.Saw_SentGCode, 1);
                }
            }
            catch (Exception exp)
            {
                CommonLog.WriteToFile("Thread.txt", $"Error FA 2010 ucJobPreparation Check_SawGCodeStatus(): {exp.Message}", true, true);
            }

        }

        private void Check_UnitsGCodeStatus()
        {

            try
            {
                var Unit1Status = Plc.ReadDevice(Constants.Unit1_SendGCode);
                if (Unit1Status == 1)
                {
                    Plc.SetDevice(Constants.Unit1_SendGCode, 0);
                    var ProcessNo = Plc.ReadDevice(Constants.Unit1_ProcessNo);

                    TransferProvider_FXCpu.WriteGCodes_ByUnitNo(ProcessNo, UnitEnum.Unit1);



                    Plc.SetDevice(Constants.Unit1_SentGCode, 1);
                }

                var Unit2Status = Plc.ReadDevice(Constants.Unit2_SendGCode);
                if (Unit2Status == 1)
                {
                    Plc.SetDevice(Constants.Unit2_SendGCode, 0);
                    var ProcessNo = Plc.ReadDevice(Constants.Unit2_ProcessNo);

                    TransferProvider_FXCpu.WriteGCodes_ByUnitNo(ProcessNo, UnitEnum.Unit2);




                    Plc.SetDevice(Constants.Unit2_SentGCode, 1);

                }

            }
            catch (Exception exp)
            {
                CommonLog.WriteToFile("Thread.txt", $"Error FA 2010 ucJobPreparation Check_UnitsGCodeStatus(): {exp.Message}", true, true);
            }

        }

        private void Check_MachineCleaning()
        {

            var machineCleaninInfo = MachineCleaningInformationBusiness.GetAll().FirstOrDefault();
            if (machineCleaninInfo == null)
                return;

            if (!machineCleaninInfo.ShowCleanAlert)
                return;

            if (machineCleaninInfo.CleanLimitCount > machineCleaninInfo.ActuelCount)
                return;

            var result = KabanMessageBox.ShowMessage(MessageHelper.GetMessage(MessageType.NeedsCleaned), KabanDevBase.Enums.MessageBoxType.Question);
            if (result != System.Windows.Forms.DialogResult.Yes)
                return;


            machineCleaninInfo.ActuelCount = 0;
            MachineCleaningInformationBusiness.Update(machineCleaninInfo);
        }

        private void Increase_MachineCleaning()
        {
            MachineCleaningInformationBusiness.Increase_ActuelCount();
        }


        public void RemoveWrapper_FromActiveList()
        {
            PlcComponentReflector.Clear_Common_Wrapper_UcName_Temp();//.Remove("ucManuelWrapper");
        }

        #endregion
    }
}
