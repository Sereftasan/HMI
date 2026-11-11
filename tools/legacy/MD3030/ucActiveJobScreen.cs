using KabanDevBase.Controls;
using KabanDevBase.Plc_Componets;
using KabanMachinesSystem.Business.BusinessLayer;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Common.Enums;
using KabanMachinesSystem.Common.Statics;
using KabanMachinesSystem.Data.Entities;
using KabanMachinesSystem.Data.ViewModels;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _MD3030.Presentation.Home.Preparations
{
    public partial class ucActiveJobScreen : KabanUserControl
    {

        #region [ INSTANCES ]

        private static ucActiveJobScreen _instance;
        CuttingListhHeaderBusiness CuttingListHeaderBusiness { get; set; }
        //PictureEdit PictureEdit { get; set; }

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanLambList;
        private static List<Control> KabanInputList;
        private List<CompanyProfilesVM> CompanyProfilesVMs { get; set; }
        private ProfileSeriesPProfileSubBusiness profileSeriesPProfileSubBusiness;
        private MeltingProfileSeryProfileSettingBusiness meltingProfileSeryProfileSettingBusiness;

        #endregion

        #region [ MEMBERS ]


        public int TotalProcessTime = 0;
        public int CurrentProcessTime = 0;

        Guid CurretJobFileId { get; set; }
        internal Guid CurrentBarId { get; set; }
        


        public List<CuttingListHeader> cuttingListHeaders { get; set; }

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]

        private ucActiveJobScreen()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
            CuttingListHeaderBusiness = new CuttingListhHeaderBusiness();
            cuttingListHeaders = new List<CuttingListHeader>();

            KabanLedLabelList = new List<Control>();
            KabanProgressList = new List<Control>();
            KabanLambList = new List<Control>();
            KabanInputList = new List<Control>();
            profileSeriesPProfileSubBusiness = new ProfileSeriesPProfileSubBusiness();
            meltingProfileSeryProfileSettingBusiness = new MeltingProfileSeryProfileSettingBusiness();
            CompanyProfilesVMs = profileSeriesPProfileSubBusiness.GetAllBySql();
            InitDefaults();
            InitEvents();
            //ucJobListButtonPanel.Controls.Add(ucJobListOfFile.Instance());
            //ucjoblistoffile = ucJobListOfFile.Instance();
            //this.panelSubRight.Controls.Remove(this.KGroupbox_Summary);

            //this.kscActiveJobScreen_Sub.Controls.Add(ucJobListOfFile.Instance());


            Translator.ChangeLangue_ByUI(this);
        }
      
        public static ucActiveJobScreen Instance()
        {
            if(_instance == null)
            {
                _instance = new ucActiveJobScreen();
            }

            return _instance;
        }

        public void LoadActiveJobScreen(bool summaryReset = false)//, bool currentJobIsFinished = false)
        {
            if (summaryReset)
                SummaryReset();

            //CurretJobFileId = jobEntity.Id;
        }


        public void LoadActiveJobScreen_Clear()
        {
            CurretJobFileId = Guid.Empty;
            //
            cuttingListHeaders = new List<CuttingListHeader>();

            SummaryReset();

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
        private void InitDefaults()
        {
            SummaryReset();

        }

        private void InitEvents()
        {
            InitButtonEvents();
        }

        private void InitButtonEvents()
        {
            //try
            //{
            //    this.btnDxfScreenshotZoom.Click += new EventHandler(delegate (object sender, EventArgs e)
            //    {
            //        if (dxfScreenshot.Tag is null)
            //            return;
            //        string tagString = dxfScreenshot.Tag.ToString();
            //        if (tagString != "")
            //        {
            //            string pathControl = Environment.CurrentDirectory.ToString() + "\\ProfileImages";
            //            if (Directory.Exists(pathControl))
            //            {
            //                string pathString = Path.Combine(Environment.CurrentDirectory, "ProfileImages\\" + tagString + ".jpeg");
            //                if (File.Exists(pathString))
            //                {                                
            //                    Form frm = new Form();
            //                    var picture = new PictureEdit
            //                    {
            //                        Name = "Profile",
            //                        Image = Image.FromFile(pathString),
            //                        Width = 600,
            //                        Height = 600
            //                    };
            //                    frm.Width = 620;
            //                    frm.Height = 650;
            //                    frm.StartPosition = FormStartPosition.CenterScreen;

            //                    picture.Properties.SizeMode = DevExpress.XtraEditors.Controls.PictureSizeMode.Stretch;
            //                    frm.Controls.Add(picture);
            //                    frm.Show();
            //                }
            //            }
            //        }
            //        else
            //            KabanMessageBox.ShowMessage("Ekran görüntüsü alınmamış!", KabanDevBase.Enums.MessageBoxType.Information);

            //    });
            //}
            //catch (Exception)
            //{

            //    throw;
            //}
            
        }


        //public void LoadDXFScreenshot(string profileId = "")
        //{
        //    if (profileId != "")
        //    {
        //        string pathControl = Environment.CurrentDirectory.ToString() + "\\ProfileImages";
        //        if (Directory.Exists(pathControl))
        //        {
        //            string pathString = Path.Combine(Environment.CurrentDirectory, "ProfileImages\\" + profileId + ".jpeg");
        //            if (File.Exists(pathString))
        //            {
        //                Image image = Image.FromFile(pathString);
        //                dxfScreenshot.Image = image;
        //                dxfScreenshot.Properties.SizeMode = DevExpress.XtraEditors.Controls.PictureSizeMode.Stretch;
        //                dxfScreenshot.Tag = profileId;                                                                                                
        //            }
        //            else
        //            {
        //                dxfScreenshot.Tag = "";
        //                dxfScreenshot.Image = null;
        //            }
        //        }
        //        else
        //        {
        //            dxfScreenshot.Tag = "";
        //            dxfScreenshot.Image = null;
        //        }
        //    }
        //    else
        //    {
        //        dxfScreenshot.Tag = "";
        //        dxfScreenshot.Image = null;
        //    }               
        //}


        //private List<CuttingListHeader> FillCuttinhHeaderList_Invoke(bool currentJobIsFinished = false)
        //{
        //    var jobList = new List<CuttingListHeader>();

        //    if (CurretJobFileId == null)
        //        return jobList;

        //    List<CuttingListHeader> cuttingHeaderList = null;
        //    cuttingHeaderList = CuttingListHeaderBusiness.Get_AllByJobFileId(CurretJobFileId);

        //    jobList.AddRange(cuttingHeaderList);






        //    if (currentJobIsFinished)
        //    {
        //        if (ActivePageHolder.ActivePageEnum != ActivePageEnum.JobPreparation)
        //            return jobList;

        //        var finishedJobsCount = jobList.Where(q => q.Finished).ToList()?.Count();
        //        if (finishedJobsCount > 1)
        //        {
        //            finishedJobsCount -= 1;
        //            var anotherSublist = jobList.GetRange((int)finishedJobsCount, jobList.Count - 1);
        //            jobList = anotherSublist;
        //        }
        //        else
        //            return jobList;
        //    }

        //    var first = jobList.FirstOrDefault();
        //    if (first != null)
        //        CurrentBarId = first.Id;

        //    if (ActivePageHolder.ActivePageEnum != ActivePageEnum.JobPreparation)
        //        return jobList;


        //    kabanGridControl_ActiveJobs.DataSource = jobList;
        //    kabanGridControl_ActiveJobs.RefreshDataSource();
        //    //

        //    if (cuttingHeaderList.Count == 0)
        //    {
        //        picEditAccesories.EditValue = null;
        //        Update_TotalProcessTime();
        //        CurrentProcessTime = 0;
        //        txtCurrentProcessTime.Text = "0";
        //        return jobList;
        //    }

        //    if (cuttingHeaderList.FirstOrDefault().ImageThumb != null)
        //    {
        //        picEditAccesories.Properties.PictureStoreMode = DevExpress.XtraEditors.Controls.PictureStoreMode.ByteArray;
        //        picEditAccesories.EditValue = cuttingHeaderList.FirstOrDefault().ImageThumb;
        //    }



        //    Update_TotalProcessTime();

        //    return jobList;
        //}

        private void Update_TotalProcessTime()
        {
            TotalProcessTime += CurrentProcessTime;

            TimeSpan ts = TimeSpan.FromSeconds(TotalProcessTime);
        }

        public Guid FinishCurrentBar()
        {
            if (CurrentBarId == null || CurrentBarId == Guid.Empty)
                return Guid.Empty;


            CuttingListHeaderBusiness.Update_FinishedById(CurrentBarId);

            return CurrentBarId;

            //if (this.CurrentBarId_Previous == null || CurrentBarId_Previous == Guid.Empty)
            //    return Guid.Empty;


            //CuttingListHeaderBusiness.Update_FinishedById(CurrentBarId_Previous);

            //return CurrentBarId_Previous;
        }

        public void SyncOnlineTime()
        {
            // PLC'den veriyi oku
            double onlineTime = Plc.ReadDevice32Bit("D1854") / 10.0;

            // Saniye ve Dakika hesaplamaları
            double onlineSec = onlineTime % 60;
            // (int) cast işlemi virgülden sonrasını atar (örn: 5.24 -> 5, 5.99 -> 5)
            int onlineMin = (int)(onlineTime / 60);

            // UI elementlerine güvenli erişim (Thread-Safe)

            try
            {
                if (lblOnlineSec.InvokeRequired || lblOnlineMin.InvokeRequired)
                {
                    lblOnlineSec.Invoke(new Action(() =>
                    {
                        lblOnlineSec.Text = onlineSec.ToString("0.##"); // İsteğe bağlı: Saniye küsuratını formatlayabilirsin
                        lblOnlineMin.Text = onlineMin.ToString();
                    }));
                }
                else
                {
                    lblOnlineSec.Text = onlineSec.ToString("0.##");
                    lblOnlineMin.Text = onlineMin.ToString();
                }
            }
            catch
            {
                return;
            }
            
        }

        public void AsyncReadPLC()
        {
            if (KabanProgressList.Count == 0)
                KabanProgressList = Get_KabanProgressBars();

            base.SyncProgressBars(KabanProgressList);

            if (KabanLedLabelList.Count == 0)
                KabanLedLabelList = Get_KabanLedLabels();

            base.SyncLabels(KabanLedLabelList);

            if (KabanLambList.Count == 0)
                KabanLambList = Get_KabanLambs();

            base.SyncLambs(KabanLambList);

            SyncOnlineTime();
            var isMachineWorking = Plc.GetDevice("M316");
            InfopanelAndTableDisabler(isMachineWorking);
        }
        #endregion

        #region [ EVENTS ]

        private void InfopanelAndTableDisabler(int flag) {
            //;
            if (!Plc.OpenConnection())
                return;
            if(ucjoblistoffile != null)
                ucjoblistoffile.NoAbleThis(flag == 0);
            ucJobList.Instance().NoAbleThis(flag == 0);

        }


        #endregion

        #region [ EVENT METHODS ]

        public void FillFieldsByRow(Job job, CompanyProfilesVM companyProfilesVM, MeltingProfileSeryProfileSetting extraSettings) {
            //if (phProfileCode == null || extraSettings == null)
            //    return;
            ////if (phProfileCode.Text == "" || phCompany.Text == "" || phSerie.Text == "" || phType.Text == "")
            ////    return;
            //if (extraSettings.MeasureWithWeldingTolerance == null ||
            //    extraSettings.BotPressWithResistance == null ||
            //    extraSettings.GasketCutActive == null ||
            //    extraSettings.MainCutRepeat == null ||
            //    extraSettings.UProfile == null ||
            //    extraSettings.MouldCuttingFirst == null ||
            //    extraSettings.MouldPressInWelding == null ||
            //    extraSettings.PressBeforeMelting == null ||
            //    extraSettings.RearCutting == null ||
            //    extraSettings.ResistancePressFixing == null ||
            //    extraSettings.SashProfile == null ||
            //    extraSettings.TopPressWithResistance == null)
            //{
            //    return;
            //}

            phProfileCode.Text = companyProfilesVM.Code.ToString();
            phCompany.Text = companyProfilesVM.ProfileBrandType;
            phSerie.Text = companyProfilesVM.ProfileSerie;
            phType.Text = companyProfilesVM.ProfileType;
            phSubType.Text = companyProfilesVM.ProfileSubType;
            phAltercode.Text = companyProfilesVM.ProviderCode;

            lmbWeldMargin.SetLambStateSafe(extraSettings.MeasureWithWeldingTolerance == true ? LambState.On : LambState.Off);
            lmbBotPressWithResistance.SetLambStateSafe(extraSettings.BotPressWithResistance == true ? LambState.On : LambState.Off);
            lmbGasketCutActive.SetLambStateSafe(extraSettings.GasketCutActive == true ? LambState.On : LambState.Off);
            lmbMainCutRepeat.SetLambStateSafe(extraSettings.MainCutRepeat == true ? LambState.On : LambState.Off);

            lmbUProfile.SetLambStateSafe(extraSettings.UProfile == true ? LambState.On : LambState.Off);
            lmbMouldCuttingFirst.SetLambStateSafe(extraSettings.MouldCuttingFirst == true ? LambState.On : LambState.Off);
            lmbMouldPressInWelding.SetLambStateSafe(extraSettings.MouldPressInWelding == true ? LambState.On : LambState.Off);
            lmbPressBeforeMelting.SetLambStateSafe(extraSettings.PressBeforeMelting == true ? LambState.On : LambState.Off);

            lmbRearCutting.SetLambStateSafe(extraSettings.RearCutting == true ? LambState.On : LambState.Off);
            lmbSashProfile.SetLambStateSafe(extraSettings.SashProfile == true ? LambState.On : LambState.Off);
            lmbTopPressWithResistance.SetLambStateSafe(extraSettings.TopPressWithResistance == true ? LambState.On : LambState.Off);
            lmbTopSurfaceCleaning.SetLambStateSafe(extraSettings.TopSurfaceCleaning == true ? LambState.On : LambState.Off);
            lmbGasketCutBeforeSurfaceCleaning.SetLambStateSafe(extraSettings.GasketCutBeforeSurfaceCleaning == true ? LambState.On : LambState.Off);
            lmbEncoderFixActive.SetLambStateSafe(extraSettings.EncoderFixActive == true ? LambState.On : LambState.Off);
            lmbFrontClampActive.SetLambStateSafe(extraSettings.FrontClampActive == true ? LambState.On : LambState.Off);
            lmbJawLowPressure.SetLambStateSafe(extraSettings.JawLowPressure == true ? LambState.On : LambState.Off);
            lmbFrontClampLowPressure.SetLambStateSafe(extraSettings.FrontPressLowPressure == true ? LambState.On : LambState.Off);

            InitAllLambs(this);

            //ktsMeasureWithWeldingTolerance.Value = extraSettings.MeasureWithWeldingTolerance ?? false;
            //ktsBotPressWithResistance.Value = extraSettings.BotPressWithResistance ?? false;
            //ktsGasketCutActive.Value = extraSettings.GasketCutActive ?? false;
            //ktsMainCutRepeat.Value = extraSettings.MainCutRepeat ?? false;

            //ktsUProfile.Value = extraSettings.UProfile ?? false;
            //ktsMouldCuttingFirst.Value = extraSettings.MouldCuttingFirst ?? false;
            //ktsMouldPressInWelding.Value = extraSettings.MouldPressInWelding ?? false;
            //ktsPressBeforeMelting.Value = extraSettings.PressBeforeMelting ?? false;

            //ktsRearCutting.Value = extraSettings.RearCutting ?? false;
            //ktsResistancePressFixing.Value = extraSettings.ResistancePressFixing ?? false;
            //ktsSashProfile.Value = extraSettings.SashProfile ?? false;
            //ktsTopPressWithResistance.Value = extraSettings.TopPressWithResistance ?? false;
            //ktsTopSurfaceCleaning.Value = extraSettings.TopSurfaceCleaning ?? false;
            //ktsGasketCutBeforeSurfaceCleaning.Value = extraSettings.GasketCutBeforeSurfaceCleaning ?? false;
            //ktsEncoderFixActive.Value = extraSettings.EncoderFixActive ?? false;
            //ktsFrontClampActive.Value = extraSettings.FrontClampActive ?? false;
            ////ktsVerticalPressFirst.Value = extraSettings.VerticalPressFirst ?? false;
            //ktsJawLowPressure.Value = extraSettings.JawLowPressure ?? false;
            //ktsFrontClampLowPressure.Value = extraSettings.FrontPressLowPressure ?? false;
            ////ktsReleaseJawCylinderFirst.Value = extraSettings.ReleaseJawCylinderFirst ?? false;
            ////ktsBackPressWithoutMould.Value = extraSettings.BackPressWithoutMould ?? false;
            //ktsBotSurfaceCleaning.Value = extraSettings.BotSurfaceCleaning ?? false;
            //ktsPressBeforeMelting.Value = extraSettings.PressBeforeMelting ?? false;


        }


        #endregion

        #region [ METHODS ]

        private void InitAllLambs(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control.Name.StartsWith("lmb", StringComparison.OrdinalIgnoreCase)
                    && control is KabanLamb lamb)
                {
                    lamb.InitLambState();
                }

                if (control.HasChildren)
                {
                    InitAllLambs(control);
                }
            }
        }
        private void SummaryReset()
        {
            TotalProcessTime = 0;
            CurrentProcessTime = 0;

            if (ActivePageHolder.ActivePageEnum != ActivePageEnum.JobPreparation)
                return;
        }




        Guid CurrentBarId_Previous { get; set; }
        internal void SetPreviousJobId()
        {
            if (this.CurrentBarId == null)
                return;

            this.CurrentBarId_Previous = this.CurrentBarId;
        }
        #endregion

        private void pictureEdit1_EditValueChanged(object sender, EventArgs e)
        {

        }
    }
}
