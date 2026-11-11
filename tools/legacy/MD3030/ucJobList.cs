using _MD3030.Entities;
using DevExpress.XtraBars;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanDevBase.Forms;
using KabanMachinesSystem.Business.BusinessLayer;
using KabanMachinesSystem.Business.Helpers;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Enums;
using KabanMachinesSystem.Common.Extensions;
using KabanMachinesSystem.Common.Helpers;
using KabanMachinesSystem.Common.Statics;
using KabanMachinesSystem.Data.Entities;
using KabanMachinesSystem.Data.ViewModels;
using KabanMachinesSystem.KabanSystem;
using KabanMachinesSystem.KabanSystem.Entities;
using KabanMachinesSystem.Presentation.Common;
using KabanMachinesSystem.Presentation.Settings.ExternalReceiptMapping;
using KabanMachinesSystem.Presentation.Settings.ExternalReceiptMapping.ExternalTypeStrategy;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace _MD3030.Presentation.Home.Preparations
{
    public partial class ucJobList : KabanUserControl
    {
        #region [ INSTANCES ]

        public delegate void OnRefreshJobListDelegate();
        public event OnRefreshJobListDelegate OnRefreshJobListEvent = null;
        //
        public delegate void OnSendJobDelegate(Job jobEntity, bool automateRaise);
        public event OnSendJobDelegate OnSendJobEvent = null;

        public delegate void OnChangedJobSelectionDelegate();
        public event OnChangedJobSelectionDelegate OnChangedJobSelectionEvent = null;

        JobProvider JobProvider { get; set; }
        JobBusiness JobBusiness { get; set; }
        ProfileSeriesPProfileSubBusiness ProfileSeriesPProfileSubBusiness { get; set; }
        private List<CompanyProfilesVM> CompanyProfilesVMs { get; set; }



        private static ucJobList _instance;

        private Job CurrentJob { get; set; }
        private ucActiveJobScreen ucActiveJobScreenInstance;
        #endregion

        #region [ MEMBERS ]

        private ExternalFileBusiness ExternalFileBusiness { get; set; }
        private ExternalFieldBusiness ExternalFieldBusiness { get; set; }
        private ExternalMappingBusiness ExternalMappingBusiness { get; set; }
        List<string> ExtensionList { get; set; }

        MeltingProfileSeryProfileSettingBusiness meltingProfileSeryProfileSettingBusiness;

        private bool isAutomaticMode;
        private bool initialized = false;


        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucJobList()
        {

            InitializeComponent();
            JobProvider = new JobProvider();
            JobBusiness = new JobBusiness();


            InitEvents();
            InitDefaults();

            Translator.ChangeLangue_ByUI(this);

            initialized = true;
        }

        private void InitDefaults()
        {
            ProfileSeriesPProfileSubBusiness = new ProfileSeriesPProfileSubBusiness();
            this.ExternalFileBusiness = new ExternalFileBusiness();
            this.ExternalFieldBusiness = new ExternalFieldBusiness();
            this.ExternalMappingBusiness = new ExternalMappingBusiness();
            this.ExtensionList = new List<string>();

            meltingProfileSeryProfileSettingBusiness = new MeltingProfileSeryProfileSettingBusiness();
            FillWaitingJobs();
            FillFileTypeExtensions();
        }


        private void FillFileTypeExtensions()
        {
            try
            {
                HashSet<string> extensions = JsonReader.LoadFileExtensions("Jsons/FileTypes.json");
                this.ExtensionList = extensions.ToList();
            }
            catch (Exception exp)
            {
                throw exp;
            } 
        }


        bool SelectWasteFile { get; set; }
        public void FillWaitingJobs()
        {
            if (InvokeRequired)
            {
                Invoke(new System.Windows.Forms.MethodInvoker(delegate
                {
                    FillGrid();
                }));
            }
            else
            {
                FillGrid();
            }

        }
        public void SetAutomaticMode(bool isAuto)
        {
            isAutomaticMode = isAuto;
        }
         
        

        private bool rowSelected = false;
        private void FillGrid()
        {
            List<ActiveJobGridViewModel> JobListForGrid = new List<ActiveJobGridViewModel>();

            var companyProfilesVMs = ProfileSeriesPProfileSubBusiness.GetAllBySql();
            var dict = new Dictionary<bool, string[]>();
            dict.Add(false, new string[] { "MainTableName", "ProfileVCutting_Y", "ProviderCode" });
            dict.Add(true, new string[] { "Code", "Comment" });
            kabanGridControlMain.SetDataSource_IEnumerable(companyProfilesVMs, dict);



            rowSelected = false;
            List<Job> waitingJobList = JobProvider.GetAll_WaitingJobs(); 
            for (int i = 0; i < waitingJobList.Count; i++)
            {
                ActiveJobGridViewModel temp = new ActiveJobGridViewModel();

                Job selectedJob = waitingJobList[i];
                string[] elements = selectedJob.FileName.Split('>');
                int code = Convert.ToInt32(elements[0]);
                //List<CompanyProfilesVM> companyProfilesVMs = ProfileSeriesPProfileSubBusiness.GetAllBySql();
                CompanyProfilesVM companyProfile = companyProfilesVMs.FirstOrDefault(x => x.Code == code);

                temp.ImportDate = waitingJobList[i].ImportDate;
                temp.FileAliasName = waitingJobList[i].FileAliasName;
                temp.ProfileBrandType = companyProfile.ProfileBrandType;
                temp.ProfileSerie = companyProfile.ProfileSerie;
                temp.ProfileType = companyProfile.ProfileType;
                temp.ProfileSubType = companyProfile.ProfileSubType;
                temp.job = selectedJob;

                JobListForGrid.Add(temp);
            }
            //Job selectedJob = (Job)asd;

            kabanGridControlMain.DataSource = JobListForGrid;
            kabanGridControlMain.RefreshDataSource();

            ShownEditor();
        }
        private void ShownEditor()
        {
            if (kabanGridViewMain == null || kabanGridViewMain.Columns[1] == null)
            {
                return;
            }
            //object cellValue = kabanGridViewMain.GetRowCellValue(2, "MainFileName");

            int rowHandle = kabanGridViewMain.LocateByDisplayText(0, kabanGridViewMain.Columns["MainFileName"], WasteFileInfo.SelectWasteFile);
            if (rowHandle >= 0)
            {
                kabanGridViewMain.FocusedRowHandle = rowHandle;

                kabanGridViewMain.FocusedColumn = kabanGridViewMain.Columns[1];

                // Begin the cell edit
                kabanGridViewMain.ShowEditor();

                btnSendJob.PerformClick();
                Plc.SetDevice(Constants.WasteModeRunning, 0);
            }
        }

        //private void KabanGridViewMain_OnLoad(object sender, EventArgs e)
        //{
        //    GridView view = sender as GridView;
        //    if (view == null || rowSelected)
        //    {
        //        return;
        //    }

        //    //if (SelectWasteFile)
        //    {
        //        int columnIndex = view.Columns["colFileAliasName"].VisibleIndex; // Adjust column name
        //        string valueToFind = "Waste_FA 2010_MakineTest - England (Proplast Kanat Surme.Gri Cift Lm.)_25_20231212 115025 (Proplast Kanat Surme.Gri Cift Lm.).kbn"; // Replace with the value to search for
        //        int rowHandle = view.LocateByDisplayText(0, view.Columns[columnIndex], valueToFind);

        //        this.SelectWasteFile = false;
        //        //int rowIndex = view.LocateByValue("FileAliasName", "Waste_FA 2010_MakineTest - England (Proplast Kanat Surme.Gri Cift Lm.)_25_20231212 115025 (Proplast Kanat Surme.Gri Cift Lm.).kbn");
        //        if (rowHandle != DevExpress.XtraGrid.GridControl.InvalidRowHandle)
        //        {
        //            view.FocusedRowHandle = rowHandle;
        //            view.MakeRowVisible(rowHandle); // Optional: Scrolls to the selected row
        //            view.ShowEditor(); // Optional: Shows the editor for the selected cell
        //            view.FocusedColumn = view.Columns["colFileAliasName"]; // Optional: Focuses on the column
        //        }


        //        //Plc.SetDevice(Constants.WasteModeRunning, 0);
        //        //btnSendJob.PerformClick();
        //    }

        //    kabanGridViewMain.ShownEditor -= KabanGridViewMain_OnLoad;
        //    rowSelected = true;
        //}


        public void UpdateUISafe(Action action)
        {
            if (this.IsDisposed ) return;

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
        private void InitEvents()
        {
            btnRefresh.Click += (s, e) =>
            {
                FillWaitingJobs();
                //
                OnRefreshJobListEvent?.Invoke();
                //
                //FillWaitingJobs();
            };


            btnSelectReceipt.Click += (s, e) =>
            {
                //var uc = new ucCheckedList(pageTitle, childs);
                var uc = new _MD3030.Presentation.Home.Common.ucCheckedList();
                uc.IsFirstLoad = true;

                var frmHost = new KabanHostForm(uc, PopupFormLevels.FirstLevel);
                frmHost.FormText = uc.PageTitle;
                if (frmHost.ShowDialog() == DialogResult.OK)
                {
                    this.FillGrid();
                }
            };

            btnDelete.Click += (s, e) =>
            {
                OnDelete();
            };

            btnSendJob.Click += (s, e) =>
            {
                var sw = Stopwatch.StartNew();

                TriggerSendJobEvent();
                sw.Stop();
            };

            kabanGridControlMain.MouseClick += (sender, e) =>
            {
                ucActiveJobScreenInstance = ucActiveJobScreen.Instance();

                var asd = kabanGridControlMain.GetCurrentRow();
                if(asd is _MD3030.Entities.ActiveJobGridViewModel)
                {
                    ActiveJobGridViewModel model = (ActiveJobGridViewModel)asd;
                    Job selectedJob = model.job;
                    string[] elements = selectedJob.FileName.Split('>');
                    int code = Convert.ToInt32(elements[0]);
                    List<CompanyProfilesVM> companyProfilesVMs = ProfileSeriesPProfileSubBusiness.GetAllBySql();
                    CompanyProfilesVM companyProfile = companyProfilesVMs.FirstOrDefault(x => x.Code == code);
                    MeltingProfileSeryProfileSetting extraSettings = meltingProfileSeryProfileSettingBusiness.Get(companyProfile.Id);
                    if (companyProfile == null)
                        return;
                    
                    ucActiveJobScreenInstance.FillFieldsByRow(selectedJob, companyProfile, extraSettings);
                }



            };


            //JobList İçerisinde doubleClick eventine bağlı herhangi bir aksiyon yoktur.
            // kabanGridControlMain.MouseDoubleClick += (s, e) =>
            //{

            //    var currentRow = kabanGridControlMain.GetCurrentRow<Job>();
            //    if (currentRow == null)
            //        return;

            //    if (!File.Exists(currentRow.FullPath))
            //        return;


            //    OpenNotePad(File.ReadAllText(currentRow.FullPath, Encoding.UTF8));

            //};
        }

        public void TriggerSendJobEvent(bool automateRaise = false)
        {
            this.CurrentJob = CurrentJobNullValidation();
            if (this.CurrentJob == null)
            {
                //var message = $"{kabanNumericControl.LabelText}: '{kabanNumericControl.TextBoxText.Trim()}'.{Environment.NewLine}{MessageHelper.GetMessage(MessageType.ExceedLimits)}";
                KabanMessageBox.ShowMessage(MessageHelper.GetMessage(MessageType.JobSentFailed), KabanDevBase.Enums.MessageBoxType.Warning);

                return;
            }

            //this.CurrentJob.FullPath = "/Saves/Recipts/0199769d-6b5e-7ae8-b170-1f83bc7ecfde.json";
            var receiptId = this.CurrentJob.FullPath.Replace(".json", string.Empty).Split('/').Reverse().FirstOrDefault();
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
            }
            catch (Exception)
            {

            }

            if (!iswrited)
            {
                //var message = $"{kabanNumericControl.LabelText}: '{kabanNumericControl.TextBoxText.Trim()}'.{Environment.NewLine}{MessageHelper.GetMessage(MessageType.ExceedLimits)}";
                //KabanMessageBox.ShowMessage(MessageHelper.GetMessage(MessageType.JobSentFailed), KabanDevBase.Enums.MessageBoxType.Warning);
                //return;
            }
            OnSendJobEvent?.Invoke(this.CurrentJob, automateRaise);
            //sw.Stop();

        }

        public void NoAbleThis(bool ableity)
        {
            UpdateUISafe(() => {
                if (this != null)
                    if (initialized == true)
                        if (this.Enabled != ableity)
                            this.Enabled = ableity;
                    
            });
        }
        public bool SendJobEvent(bool automateRaise = false)
        {

          

            this.CurrentJob = CurrentJobNullValidation();
            if (this.CurrentJob == null)
                return false;

            //this.CurrentJob.FullPath = "/Saves/Recipts/0199769d-6b5e-7ae8-b170-1f83bc7ecfde.json";
            var receiptId = this.CurrentJob.FullPath.Replace(".json", string.Empty).Split('/').Reverse().FirstOrDefault();
            if (string.IsNullOrEmpty(receiptId))
                return false;

            var jobProvider = new _MD3030.Presentation.KabanSystem.JobProvider();
            var entity = jobProvider.PrepareJob(Guid.Parse(receiptId));
            var transfer = new _MD3030.Presentation.KabanSystem.TransferProvider_FXCpu();
            transfer.WriteProfileInfo(entity);
            OnSendJobEvent?.Invoke(this.CurrentJob, automateRaise);

            return true;
        }


        public Job CurrentJobNullValidation()
        {
            var job = kabanGridControlMain.GetCurrentRow<ActiveJobGridViewModel>().job;
            if (job == null)
                return null;

            return job;
        }

        public void CheckIf_ProcessedWasteFileRaised()
        {
            if (this.CurrentJob.DependantJobIdForWastes == null || this.CurrentJob.DependantJobIdForWastes == Guid.Empty)
                return;

            var newJob = kabanGridControlMain.GetCurrentRow<Job>();
            if (this.CurrentJob.DependantJobIdForWastes != newJob?.DependantJobIdForWastes)
                return;

            Plc.SetDevice(Constants.MachineIsWorking_Device, 0);
            TriggerSendJobEvent(true);
        }

        public static ucJobList Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucJobList();
                }

                return _instance;
            }
            catch (System.Exception)
            {

                throw;
            }
        }


        #endregion

        #region [ EVENTS ]


        private void OnDelete()
        {

            var currentRow = kabanGridControlMain.GetCurrentRow<ActiveJobGridViewModel>().job;
            if (currentRow == null)
                return;

            currentRow.ErrorDetail = $"Deleted by the user -> Date {DateTime.Now}";
            currentRow.State = (int)JobStateEnum.Cancelled;

            var result = JobBusiness.Update(currentRow);
            if (!result)
            {
                ShowToastNotify(MessageHelper.GetMessage(MessageType.TransactionError));
                return;
            }

            ShowToastNotify(MessageHelper.GetMessage(MessageType.TransactionSucceed));
            FillWaitingJobs();


        }



        #endregion

        #region [ EVENT METHODS ]


        private void SelectExternalJob()
        {
            try
            {
                var defaultExternalJob = this.ExternalFileBusiness.GetDefaultExternalJob();
                if (defaultExternalJob == null)
                    return;

                var file = new OpenFileDialog();
                file.Filter = $"{defaultExternalJob.Extension} Files (*.{defaultExternalJob.Extension.ToLower()})|*.{defaultExternalJob.Extension.ToLower()}";

                if (file.ShowDialog() != DialogResult.OK)
                    return;



                var subQuery = $"\"ExternalFileId\" = '{defaultExternalJob.Id}' ";
                var mappedFields = this.ExternalMappingBusiness.GetAllBySql_BySubQuery(subQuery);


                var external_Kaban_Field_MappedDictionary = new Dictionary<string, string>();
                foreach (var item in mappedFields)
                {
                    if (!external_Kaban_Field_MappedDictionary.ContainsKey(item.KabanField))
                        external_Kaban_Field_MappedDictionary.Add(item.KabanField, item.ExternalField);
                }


                ExternalTypeStrategyContext.InitializeStrategies(this.ExtensionList);
                ExternalTypeStrategyContext.CreateKabanFile(defaultExternalJob.Extension, file.FileName, external_Kaban_Field_MappedDictionary);
            }
            catch (Exception exp)
            {

                throw exp;
            }
        }


        #endregion

        #region [ METHODS ]

        public void RefreshDataSources()
        {

        }

        private void OpenNotePad(string text)
        {
            var uc = new ucNotePad(text);
            var frmHost = new KabanHostForm(uc, KabanMachinesSystem.Common.Enums.PopupFormLevels.FirstLevel);
            frmHost.FormText = uc.PageTitle;
            frmHost.ShowDialog();
        }

        #endregion





        #region [ COMMENTS ]
        //internal void CreateWasteFile_BothOptimization(List<KabanMachinesSystem.KabanSystem.Entities.Bar> barList,
        //                              Dictionary<double, List<KabanMachinesSystem.KabanSystem.Entities.Profile>> optimizationModel_Dict,
        //                              string filePath,
        //                              int measured_ProfileLength)
        //{
        //    WasteFileInfo.SelectWasteFile = "";
        //    this.SelectWasteFile = true;
        //    var dictionary = new Dictionary<int, int>();
        //    var arrayCount = 0;

        //    var controlMeasurment = measured_ProfileLength / 10;
        //    foreach (var item in optimizationModel_Dict)
        //    {
        //        if (item.Key > controlMeasurment)
        //            continue;

        //        arrayCount += item.Value.Count;
        //        dictionary.Add((int)item.Key, item.Value.Count);
        //    }

        //    var optimalList = DynamicOptimization.FlattenArray_OptimalPlacement(dictionary, arrayCount, controlMeasurment);


        //    var clonedDict = optimizationModel_Dict.ToDictionary(entry => entry.Key, entry => entry.Value);
        //    string[] selectedProfiles = new string[optimalList.Count];
        //    var index = 0;
        //    foreach (var item in optimalList)
        //    {
        //        if (clonedDict.ContainsKey(item))
        //        {
        //            selectedProfiles[index] = clonedDict[item][0].Line;
        //            clonedDict[item].Remove(clonedDict[item][0]);
        //            index++;
        //        }
        //    }





        //    List<string> linesToRemove = new List<string>();

        //    var previousBar = "";
        //    var barPrinted = false;
        //    var findLine = false;

        //    var originalFileName = Path.GetFileNameWithoutExtension(filePath);
        //    var dateStr = DateTime.Now.ToString("yyyyMMdd HHmmss");
        //    WasteFileInfo.SelectWasteFile = $"Waste_{originalFileName}_{dateStr}.kbn";
        //    string provider = "";

        //    string tempFilePath_Waste = $"{Path.GetTempPath()}\\{WasteFileInfo.SelectWasteFile}";

        //    if (optimalList.Count > 0)
        //    {
        //        using (StreamReader file = new StreamReader(filePath))
        //        using (StreamWriter outputFile = new StreamWriter(tempFilePath_Waste))
        //        {
        //            string line;
        //            while ((line = file.ReadLine()) != null)
        //            {
        //                if (selectedProfiles.Contains(line))
        //                {
        //                    if (!barPrinted)
        //                    {
        //                        var splitBar = previousBar.Split(';');
        //                        provider = splitBar[3];
        //                        splitBar[6] = measured_ProfileLength.ToString();
        //                        var newBar = string.Join(";", splitBar);

        //                        outputFile.WriteLine(newBar);
        //                        barPrinted = true;
        //                    }

        //                    findLine = true;
        //                    outputFile.WriteLine(line);

        //                    if (!string.IsNullOrEmpty(line.Trim()))
        //                        linesToRemove.Add(line);
        //                }
        //                else
        //                {
        //                    if (findLine)
        //                    {
        //                        if (line.StartsWith("Bar") || line.StartsWith("Profile"))
        //                        {
        //                            findLine = false;
        //                        }
        //                        else
        //                        {
        //                            outputFile.WriteLine(line);

        //                            if (!string.IsNullOrEmpty(line.Trim()))
        //                                linesToRemove.Add(line);
        //                        }
        //                    }
        //                }

        //                if (line.StartsWith("Bar"))
        //                    previousBar = line;
        //            }
        //        }
        //    }




        //    string waste_OutputFilePath = $"C:\\kabantransfer\\{WasteFileInfo.SelectWasteFile}";
        //    string changed_OriginalFilePath = $"C:\\kabantransfer\\Changed_{originalFileName}_{dateStr}.kbn";

        //    string tempFilePath = $"{Path.GetTempPath()}\\Changed_{originalFileName}_{dateStr}.kbn";
        //    //using (StreamReader file = new StreamReader(filePath))
        //    //using (StreamWriter tempFile = new StreamWriter(tempFilePath))
        //    //{
        //    //    string line;
        //    //    while ((line = file.ReadLine()) != null)
        //    //    {
        //    //        if (!linesToRemove.Contains(line))
        //    //        {
        //    //            tempFile.WriteLine(line);
        //    //        }
        //    //    }
        //    //}


        //    var dictionaryOriginal = new Dictionary<int, List<string>>();
        //    var cutLenght_PlusCutting = 0;
        //    var profileSpec = "";
        //    var permutedResultsDict = new Dictionary<int, OptimizeInputEntity>();
        //    var profileSpec_Printed = false;
        //    var firstBar = "";
        //    var barLength = 0d;
        //    using (StreamReader file = new StreamReader(filePath))
        //    {
        //        string line;
        //        while ((line = file.ReadLine()) != null)
        //        {
        //            if (!linesToRemove.Contains(line))
        //            {
        //                if (line.StartsWith("Bar") && string.IsNullOrEmpty(firstBar))
        //                {
        //                    object obj = KabanFileReader.GetObject(line.Trim());
        //                    var currentBar = (KabanMachinesSystem.KabanSystem.Entities.Bar)obj;
        //                    barLength = currentBar.Lenght;
        //                    firstBar = line;
        //                }

        //                if (line.StartsWith("Profile"))
        //                {
        //                    profileSpec_Printed = false;
        //                    if (!string.IsNullOrEmpty(profileSpec))
        //                    {
        //                        //profileSpec = $"{profileSpec}{Environment.NewLine}";
        //                        dictionaryOriginal[cutLenght_PlusCutting].Add(profileSpec);
        //                        //profileSpec_Printed = true;
        //                    }


        //                    profileSpec = line;
        //                    object obj = KabanFileReader.GetObject(line.Trim());

        //                    var currentProfile = (KabanMachinesSystem.KabanSystem.Entities.Profile)obj;

        //                    cutLenght_PlusCutting = currentProfile.LeftAngle == 90 && currentProfile.RightAngle == 90 ?
        //                        (int)currentProfile.CutLength + 4 : (int)currentProfile.CutLength + 7;

        //                    if (!dictionaryOriginal.ContainsKey(cutLenght_PlusCutting))
        //                    {
        //                        dictionaryOriginal[cutLenght_PlusCutting] = new List<string>();
        //                    }


        //                    if (!permutedResultsDict.ContainsKey(cutLenght_PlusCutting))
        //                    {
        //                        var permutedResult = new OptimizeInputEntity();

        //                        permutedResult.LeftAngle = currentProfile.LeftAngle;
        //                        permutedResult.RightAngle = currentProfile.RightAngle;
        //                        permutedResult.Amount = 1;
        //                        permutedResult.Length = cutLenght_PlusCutting;

        //                        permutedResultsDict[cutLenght_PlusCutting] = permutedResult;
        //                    }
        //                    else
        //                    {
        //                        permutedResultsDict[cutLenght_PlusCutting].Amount++;
        //                    }

        //                }

        //                if (!line.StartsWith("Bar") && !line.StartsWith("Profile") && !string.IsNullOrEmpty(line.Trim()))
        //                {
        //                    profileSpec = $"{profileSpec}{Environment.NewLine}{line}";
        //                }



        //                //tempFile.WriteLine(line);
        //            }
        //            else
        //            {
        //                if (!string.IsNullOrEmpty(profileSpec))
        //                {
        //                    //profileSpec = $"{previousBar}{Environment.NewLine}{profileSpec}{Environment.NewLine}";
        //                    dictionaryOriginal[cutLenght_PlusCutting].Add(profileSpec);
        //                    profileSpec = "";
        //                    profileSpec_Printed = true;
        //                }
        //            }
        //        }
        //    }

        //    if (!profileSpec_Printed)
        //    {
        //        profileSpec = $"{previousBar}{Environment.NewLine}{profileSpec}{Environment.NewLine}";
        //        if (dictionaryOriginal.Count > 0)
        //            dictionaryOriginal[cutLenght_PlusCutting].Add(profileSpec);
        //        else
        //        {
        //            var list = new List<string>();
        //            list.Add(profileSpec);
        //            dictionaryOriginal.Add(cutLenght_PlusCutting, list);
        //        }
        //        profileSpec = "";
        //        profileSpec_Printed = true;
        //    }

        //    var permutedResults = permutedResultsDict.Values.ToArray();
        //    var angleDetecter = permutedResults.FirstOrDefault(q => q.LeftAngle != 90 || q.RightAngle != 90);
        //    foreach (var item in permutedResults)
        //    {
        //        item.Lenght_Plus_Tickness = item.Length;
        //    }

        //    permutedResults = permutedResults.Where(q => q != null).OrderByDescending(q => q.Lenght_Plus_Tickness).ToArray();
        //    var optimizeResult = GreedyOptimization.Optimize(permutedResults, barLength);

        //    if (optimizeResult.OptimizedResults.Count > 0)
        //    {
        //        var splitedBar = firstBar.Split(';');
        //        using (StreamWriter tempFile = new StreamWriter(tempFilePath))
        //        {
        //            for (int i = 0; i < optimizeResult.OptimizedResults.Count; i++)
        //            {
        //                splitedBar[1] = $"{i + 1}";
        //                tempFile.WriteLine(string.Join(";", splitedBar));
        //                for (int j = 0; j < optimizeResult.OptimizedResults[i].Lengths.Count; j++)
        //                {
        //                    if (dictionaryOriginal.ContainsKey((int)optimizeResult.OptimizedResults[i].Lengths[j]))
        //                    {
        //                        tempFile.WriteLine($"{dictionaryOriginal[(int)optimizeResult.OptimizedResults[i].Lengths[j]][0]}");
        //                        dictionaryOriginal[(int)optimizeResult.OptimizedResults[i].Lengths[i]].Remove(dictionaryOriginal[(int)optimizeResult.OptimizedResults[i].Lengths[j]][0]);
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    if (File.Exists(tempFilePath_Waste))
        //        File.Move(tempFilePath_Waste, waste_OutputFilePath);

        //    if (File.Exists(tempFilePath))
        //        File.Move(tempFilePath, changed_OriginalFilePath);

        //    WasteFileInfo.SelectWasteFile = $"Waste_{originalFileName}_{dateStr} ({provider}).kbn";

        //}

        #endregion
    }


    public class ProfileContent
    {
        public int ProfileLength { get; set; }
        public int LeftAngle { get; set; }
        public int RightAngle { get; set; }
        public StringBuilder Content { get; set; }
    }
}
