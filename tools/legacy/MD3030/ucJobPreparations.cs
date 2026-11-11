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
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace _MD3030.Presentation.Home.Preparations
{
    public delegate void OnSendToScrewingMachineDelegate();
    public delegate void OnRefreshJobListDelegate();
    public delegate void OnSendJobDelegate();

    public partial class ucJobPreparations : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]

        public event Action OnSendJobEventCompleted;

        public event OnRefreshJobListDelegate OnRefreshJobListEvent = null;
        public event OnSendToScrewingMachineDelegate OnSendToScrewingMachine = null;
        public event OnSendJobDelegate OnSendJobEvent = null;

        private static ucJobPreparations _instance;
        public ucJobList _instanceJobList;
        public static ucJobDimentions _instanceJobDimentions;
        public ucMainControlPanel _instanceMainControl;
        public ucInfoPanel _instanceInfoPanel;
        public static ucActiveJobScreen _instanceActiveJobScreen;
        ResultEntity ResultEntity { get; set; }

        ProcessImporter ProccesingCenterImporter { get; set; }
        TransferProvider TransferProvider { get; set; }
        CuttingListhHeaderBusiness CuttingListhHeaderBusiness { get; set; }
        MachineCleaningInformationBusiness MachineCleaningInformationBusiness { get; set; }
        CuttingListItemBusiness CuttingListItemBusiness { get; set; }
        LabelHeaderBusiness LabelHeaderBusiness { get; set; }
        LabelDataBusiness LabelDataBusiness { get; set; }
        ProfileSeriesPProfileSubBusiness ProfileSeriesPProfileSubBusiness { get; set; }
        ProfileSeryAlternativeCodeBusiness ProfileSeryAlternativeCodeBusiness { get; set; }
        MDReceiptBusiness _MDReceiptBusiness { get; set; } 
        MDBarcodeBusiness _MDBarcodeBusiness { get; set; } // Barcode Business eklendi 

        MeltingProfileSeryProfileSettingBusiness meltingProfileSeryProfileSettingBusiness;
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
        protected ucJobPreparations()
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
            this.ProfileSeriesPProfileSubBusiness = new ProfileSeriesPProfileSubBusiness();
            this.ProfileSeryAlternativeCodeBusiness = new ProfileSeryAlternativeCodeBusiness();
            this._MDReceiptBusiness = new MDReceiptBusiness();
            this._MDBarcodeBusiness = new MDBarcodeBusiness(); // Barcode instance başlatıldı
            this.meltingProfileSeryProfileSettingBusiness = new MeltingProfileSeryProfileSettingBusiness();

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
            catch (System.Exception ex)
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

            _instanceJobList = ucJobList.Instance();

            InitiateInstance(_instanceJobList, splitContainerControl1.Panel1);

            _instanceJobDimentions = ucJobDimentions.Instance();

            InitiateInstance(_instanceJobDimentions, splitContainerControl1.Panel2);

            _instanceMainControl = ucMainControlPanel.Instance();
            InitiateInstance(_instanceMainControl, kscJobPreparation.Panel2);


            var waitingCuttingHeaders = CuttingListhHeaderBusiness.Get_AllWaiting();


            //var jobEntity = JobBusiness.Get(waitingCuttingHeaders.FirstOrDefault().JobFileId);

            _instanceActiveJobScreen = ucActiveJobScreen.Instance();


            _instanceActiveJobScreen.LoadActiveJobScreen(true);
            //CurrentJobEntity = jobEntity;


            InitiateInstance(_instanceActiveJobScreen, xtraTabPage_Main);// kscJobPreparation_Up_Right.Panel1);

            //SendJob(jobEntity, false);

        }




        #endregion

        #region [ EVENTS ]

        bool WasteMode_SendButtonClick = false;
        bool AutomateRaise { get; set; }

        private void InitEvents()
        {
            _instanceJobList.OnRefreshJobListEvent += () =>
            {
                OnRefreshJobListEvent?.Invoke();
            };

            _instanceJobList.OnSendJobEvent += (Job jobEntity, bool automateRaise) =>
            {
                this.AutomateRaise = automateRaise;
                OnSendJobEventCompleted?.Invoke();
                //if (automateRaise)
                //{
                //    Task.Run(() =>
                //    {
                //        this.OnRefreshJobListEvent?.Invoke();
                //    });
                //}
            };

            OnSendJobEventCompleted += () =>
            {
                this.OnRefreshJobListEvent?.Invoke();
            };

            _instanceMainControl.OnSendJobEvent += () =>
            {
                StartJob();
            };

            _instanceJobList.OnChangedJobSelectionEvent += () =>
            {
                var wasteMode = Plc.GetDevice(Constants.WasteMode);
                if (wasteMode == 1)
                {
                    Thread.Sleep(1000);
                    this.OnRefreshJobListEvent?.Invoke();
                }
            };
        }

        private void StartJob()
        {
            Plc.SetDevice(Constants.MachineIsWorking_Device_MD3030, 1);
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
                _instanceInfoPanel.Message = msg;
            }
        }


        public void RefreshDataSources()
        {
            // In frmFA2010 TabChanged and JobPreparation turned to active
            Reload_ActiveJobScreen();
        }

        public void FillWaitingJobs()
        {
            _instanceJobList?.FillWaitingJobs();
        }

        public void ShowMessage_InfoPanel(string message)
        {
            _instanceInfoPanel.Message = message;
        }



        private void LoadActiveJobScreen(Job jobEntity)//, bool currentBarIsFinished = false)
        {
            if (_instanceActiveJobScreen == null)
            {
                _instanceActiveJobScreen = ucActiveJobScreen.Instance();
                _instanceActiveJobScreen.LoadActiveJobScreen(true);
            }
            else
                _instanceActiveJobScreen.LoadActiveJobScreen(false);//, currentJobIsFinished:currentBarIsFinished);
        }


        public int WasteLength { get; set; }
        public bool RefreshControl { get; set; }
        public void AsyncReadPLC()
        {
            var _M101 = Plc.ReadDevice("M101");

            if (_M101 == 1)
            {
                bool isJobTranfered = _instanceJobList.SendJobEvent();
            }

            _instanceActiveJobScreen.AsyncReadPLC();
            _instanceJobDimentions.AsyncReadPLC();
        }



        public void RemoveWrapper_FromActiveList()
        {
            PlcComponentReflector.Clear_Common_Wrapper_UcName_Temp();//.Remove("ucManuelWrapper");
        }

        public void FillByBarcode(string barcodeValue)
        {
            if (string.IsNullOrEmpty(barcodeValue))
            {
                MessageBox.Show("Barkod değeri boş", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Temizlik işlemleri
            barcodeValue = barcodeValue.Replace("\r", "").Replace("\u0003", "").Replace("\u0002", "").Replace("\n", "").Replace("ABC", "");

            // Veritabanından barkod okuma ayarlarını sıralı şekilde çek
            var barcodeSettings = this._MDBarcodeBusiness.Get_All().OrderBy(x => x.FieldIndex).ToList();

            if (barcodeSettings == null || !barcodeSettings.Any())
            {
                MessageBox.Show("Sistemde barkod okuma ayarları (Settings) bulunamadı!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string code = string.Empty;
            int X = 0;
            int Y = 0;
            int codeLength = 6; // ProfileCode için varsayılan bir uzunluk, aşağıda dinamik dolacak

            // Ayarlara göre string'i start index mantığıyla parçalayarak ilgili değerlere ata
            foreach (var setting in barcodeSettings)
            {
                // Uzunluğu veya start index'i 0 olan bir ayar varsa atla
                if (setting.FieldLenght <= 0 || setting.FieldStartIndex <= 0) continue;

                // Kullanıcının UI tarafında index'i 1'den başlattığını varsayarak -1 yapıyoruz
                int startIndex = setting.FieldStartIndex - 1;

                // Okunan barkod bu alanı alacak kadar uzun mu?
                if (startIndex + setting.FieldLenght > barcodeValue.Length)
                {
                    MessageBox.Show($"Okunan barkod '{setting.FieldName}' alanını çözümlemek için çok kısa!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string extractedValue = barcodeValue.Substring(startIndex, setting.FieldLenght);

                // Enum karşılığına göre değerleri dağıt
                if (setting.FieldCode == (int)BarcodeFieldType.ProfileCode)
                {
                    code = extractedValue;
                    codeLength = setting.FieldLenght; // Padding için uzunluğu hafızaya alıyoruz
                }
                else if (setting.FieldCode == (int)BarcodeFieldType.ProfileX)
                {
                    int.TryParse(extractedValue, out X);
                }
                else if (setting.FieldCode == (int)BarcodeFieldType.ProfileY)
                {
                    int.TryParse(extractedValue, out Y);
                }
            }

            // Mevcut mantıktaki X ve Y yer değiştirme kuralı
            if (X < Y)
            {
                //int temp = X;
                //X = Y;
                //Y = temp;
            }

            // Parametre olarak artık hesaplanan code uzunluğunu da gönderiyoruz
            FeedBarcode(code, X, Y, codeLength);
        }

        // Metodun imzasını dinamik PadLeft işlemi için güncelledik (codeLength parametresi eklendi)
        public void FeedBarcode(string code, int X, int Y, int codeLength)
        {
            var companyProfilesVMs = this.ProfileSeriesPProfileSubBusiness.GetAllBySql();
            var AlternateCodes = this.ProfileSeryAlternativeCodeBusiness.GetAll();

            // Artık 10 karakter yerine dinamik gelen codeLength kullanılıyor
            var AlternateCode = AlternateCodes.FirstOrDefault(AlterCode => AlterCode.Code.ToString().PadLeft(codeLength, '0') == code);

            var matchedProfile = companyProfilesVMs.FirstOrDefault(profile =>
                (profile.Code != null && profile.Code.ToString().PadLeft(codeLength, '0') == code) ||
                (AlternateCode != null && profile.Id == AlternateCode.ProfileSeryProfileId));

            if (matchedProfile != null)
            {
                var allReceipts = this._MDReceiptBusiness.GetMDReceiptInfos();

                var targetReceipt = allReceipts
                    .Where(r => r.DetailName == matchedProfile.Id.ToString())
                    .OrderByDescending(r => r.UpdateDate > r.ReceiptDate ? r.UpdateDate : r.ReceiptDate)
                    .FirstOrDefault();

                if (targetReceipt != null)
                {
                    PlcUtility.Int2Short(X * 10, out short high, out short low);
                    short[] numbers = new short[] { low, high };
                    var result = Plc.SetDevice("D1200", low, this.PlcType);
                    Plc.SetDevice("D1201", high, this.PlcType);

                    PlcUtility.Int2Short(Y * 10, out short highY, out short lowY);
                    numbers = new short[] { lowY, highY };
                    result = Plc.SetDevice("D1202", lowY, this.PlcType);
                    Plc.SetDevice("D1203", highY, this.PlcType);

                    UpdateUISafe(() => { _instanceJobDimentions.knmX.Value = X; });
                    UpdateUISafe(() => { _instanceJobDimentions.knmY.Value = Y; });

                    Job CurrentJob = CreateJobs(targetReceipt);

                    var receiptId = CurrentJob.FullPath.Replace(".json", string.Empty).Split('/').Reverse().FirstOrDefault();
                    if (string.IsNullOrEmpty(receiptId))
                    {
                        KabanMessageBox.ShowMessage(MessageHelper.GetMessage(MessageType.JobSentFailed), KabanDevBase.Enums.MessageBoxType.Warning);
                        return;
                    }

                    var jobProvider = new _MD3030.Presentation.KabanSystem.JobProvider();
                    var entity = jobProvider.PrepareJob(Guid.Parse(receiptId));
                    var transfer = new _MD3030.Presentation.KabanSystem.TransferProvider_FXCpu();
                    bool iswrited = false;
                    try
                    {
                        iswrited = transfer.WriteProfileInfo(entity);

                        Job selectedJob = (Job)CurrentJob;
                        string[] elements = selectedJob.FileName.Split('>');
                        int ProfileCode = Convert.ToInt32(elements[0]);
                        CompanyProfilesVM companyProfile = companyProfilesVMs.FirstOrDefault(x => x.Code == ProfileCode);
                        MeltingProfileSeryProfileSetting extraSettings = meltingProfileSeryProfileSettingBusiness.Get(companyProfile.Id);
                        if (companyProfile == null)
                            return;
                        _instanceActiveJobScreen.FillFieldsByRow(selectedJob, companyProfile, extraSettings);
                    }
                    catch (Exception)
                    {
                    }

                    if (!iswrited)
                    {
                        KabanMessageBox.ShowMessage(MessageHelper.GetMessage(MessageType.JobSentFailed), KabanDevBase.Enums.MessageBoxType.Warning);
                        return;
                    }

                    OnSendJobEventCompleted?.Invoke();
                }
                else
                {
                    MessageBox.Show($"Profil bulundu fakat reçete tanımı bulunamadı!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

            }
            else
            {
                UpdateUISafe(() => { MessageBox.Show("Profil Bulunamadı!", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information); });
            }
        }


        // Eski Feed metodu geriye dönük uyumluluk veya başka yerden çağrılıyorsa diye tutuldu (Eğer eminsen silebilirsin)
        public void Feed(string code, string receiptCode, int X, int Y)
        {
            var companyProfilesVMs = this.ProfileSeriesPProfileSubBusiness.GetAllBySql();

            var AlternateCodes = this.ProfileSeryAlternativeCodeBusiness.GetAll();

            var AlternateCode = AlternateCodes.FirstOrDefault(AlterCode => AlterCode.Code.ToString().PadLeft(10, '0') == code);

            var matchedProfile = companyProfilesVMs.FirstOrDefault(profile => (profile.Code != null && profile.Code.ToString().PadLeft(10, '0') == code) || (AlternateCode != null && profile.Id == AlternateCode.ProfileSeryProfileId));

            if (matchedProfile != null)
            {
                var allReceipts = this._MDReceiptBusiness.GetMDReceiptInfos();

                var targetReceipt = allReceipts
    .Where(r => r.DetailName == matchedProfile.Id.ToString())
    .OrderByDescending(r => r.UpdateDate > r.ReceiptDate ? r.UpdateDate : r.ReceiptDate)
    .FirstOrDefault();

                if (targetReceipt != null)
                {
                    PlcUtility.Int2Short(X * 10, out short high, out short low);
                    short[] numbers = new short[] { low, high };
                    var result = Plc.SetDevice("D1200", low, this.PlcType);
                    Plc.SetDevice("D1201", high, this.PlcType);

                    PlcUtility.Int2Short(Y * 10, out short highY, out short lowY);
                    numbers = new short[] { lowY, highY };
                    result = Plc.SetDevice("D1202", lowY, this.PlcType);
                    Plc.SetDevice("D1203", highY, this.PlcType);

                    UpdateUISafe(() => { _instanceJobDimentions.knmX.Value = X; });
                    UpdateUISafe(() => { _instanceJobDimentions.knmY.Value = Y; });

                    Job CurrentJob = CreateJobs(targetReceipt);

                    //this.CurrentJob.FullPath = "/Saves/Recipts/0199769d-6b5e-7ae8-b170-1f83bc7ecfde.json";
                    var receiptId = CurrentJob.FullPath.Replace(".json", string.Empty).Split('/').Reverse().FirstOrDefault();
                    if (string.IsNullOrEmpty(receiptId))
                    {
                        //var message = $"{kabanNumericControl.LabelText}: '{kabanNumericControl.TextBoxText.Trim()}'.{Environment.NewLine}{MessageHelper.GetMessage(MessageType.ExceedLimits)}";
                        KabanMessageBox.ShowMessage(MessageHelper.GetMessage(MessageType.JobSentFailed), KabanDevBase.Enums.MessageBoxType.Warning);

                        return;
                    }

                    var jobProvider = new _MD3030.Presentation.KabanSystem.JobProvider();
                    var entity = jobProvider.PrepareJob(Guid.Parse(receiptId));
                    var transfer = new _MD3030.Presentation.KabanSystem.TransferProvider_FXCpu();
                    bool iswrited = false;
                    try
                    {
                        iswrited = transfer.WriteProfileInfo(entity);

                        Job selectedJob = (Job)CurrentJob;
                        string[] elements = selectedJob.FileName.Split('>');
                        int ProfileCode = Convert.ToInt32(elements[0]);
                        CompanyProfilesVM companyProfile = companyProfilesVMs.FirstOrDefault(x => x.Code == ProfileCode);
                        MeltingProfileSeryProfileSetting extraSettings = meltingProfileSeryProfileSettingBusiness.Get(companyProfile.Id);
                        if (companyProfile == null)
                            return;
                        _instanceActiveJobScreen.FillFieldsByRow(selectedJob, companyProfile, extraSettings);
                    }
                    catch (Exception)
                    {

                    }

                    if (!iswrited)
                    {
                        KabanMessageBox.ShowMessage(MessageHelper.GetMessage(MessageType.JobSentFailed), KabanDevBase.Enums.MessageBoxType.Warning);
                        return;
                    }

                    OnSendJobEventCompleted?.Invoke();


                }
                else
                {
                    MessageBox.Show($"Profil bulundu fakat '{receiptCode}' kodlu reçete tanımı bulunamadı!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

            }
            else
            {
                UpdateUISafe(() => { MessageBox.Show("Profil Bulunamadı!", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information); });

            }
        }

        private Job CreateJobs(MDReceiptInfoVM mdReceiptInfo)
        {
            try
            {
                var job = new Job();
                job.Id = Guid.NewGuid();
                job.FileAliasName = mdReceiptInfo.ReceiptName;
                job.FileName = mdReceiptInfo.ReceiptDescription;
                job.FullPath = mdReceiptInfo.JsonValue;
                job.ImportDate = DateTime.Now;
                job.IsJobDone = false;
                job.MainFileGuid = mdReceiptInfo.Id;
                job.ProfileCode = mdReceiptInfo.ProfilId.ToString();
                job.State = (int)JobStateEnum.Waiting;

                return job;
            }
            catch (Exception exp)
            {
                throw exp;
            }
        }

        public void UpdateUISafe(Action action)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;

            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke((MethodInvoker)delegate { try { action(); } catch { } });
                }
                else
                {
                    action();
                }
            }
            catch
            {
                // invoke sırasında da hata gelebilir (örn. disposed race) - yoksay veya logla
            }
        }
        #endregion
    }
}