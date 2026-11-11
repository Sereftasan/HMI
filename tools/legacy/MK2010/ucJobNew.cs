using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Drawing;
using KabanDevBase.Forms;
using KabanDevBase.Statics;
using System.Windows.Forms;
using KabanDevBase.Controls;
using System.ComponentModel;
using System.Threading.Tasks;
using KabanDevBase.Extensions;
using System.Collections.Generic;
using _MK2010.Presentation.Alarms;
using KabanMachinesSystem.Statics;
using KabanMachinesSystem.Data.ViewModels;
using KabanMachinesSystem.Common.Helpers;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Business.BusinessLayer;
using _MK2010.Entities;
using DevExpress.XtraEditors.Controls;
using KabanMachinesSystem.Data.Entities;
using System.IO;

namespace _MK2010.Presentation.Home.Preparations
{

    public delegate void OnBarcodeRefereshDelegate();
    public delegate void OnUserBarcodeReadDelegate(string Barcode);
    public partial class ucJobNew : KabanUserControl
    {
        #region Instances

        public event OnBarcodeRefereshDelegate OnBarcodeReferesh = null;
        public event OnUserBarcodeReadDelegate OnUserBarcodeRead = null;


        private static ucJobNew _instance;
        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanInputList;
        private static bool isAutomaticMode { get; set; }
        private ProfileSeriesPProfileSubBusiness ProfileSeriesPProfileSubBusiness { get; set; }
        ProfileSeryProfileSettingBusiness ProfileSeryProfileSettingBusiness { get; set; }
        ProfileSeryAlternativeCodeBusiness ProfileSeryAlternativeCodeBusiness { get; set; }

        private bool isUserBarcodeActive = false;
        #endregion
        private List<CompanyProfilesVM> CompanyProfilesVMs { get; set; }

        protected ucJobNew()
        {
            InitializeComponent();

            if (this.IsInDesignerMode())
                return;

            InitDefaults();
            InitEvents();

            KabanLedLabelList = new List<Control>();
            KabanProgressList = new List<Control>();
            KabanInputList = new List<Control>();

            Translator.ChangeLangue_ByUI(this);

        }


        private void InitDefaults()
        {
            
            SetAutomaticMode(false);
            this.ProfileSeriesPProfileSubBusiness = new ProfileSeriesPProfileSubBusiness();
            this.ProfileSeryProfileSettingBusiness = new ProfileSeryProfileSettingBusiness();
            this.ProfileSeryAlternativeCodeBusiness = new ProfileSeryAlternativeCodeBusiness();
            FillProfileLookUpEdit();
            //FillByBarcode("003102210708001256x4");
            Plc.SetDevice("M1039", 1, this.PlcType);
            LimitValidator();
        }

        private void InitEvents()
        {
            btnEmptyRefresh.Click += new EventHandler(delegate (object sender, EventArgs e)
            {
                Plc.SetDevice("D62", 21);
                FillProfileLookUpEdit();
                kleProfiles.EditValue = null;
                lblProfileCodeValue.Text = "";
                lblBrandType.Text = "";
                lblSerius.Text = "";
                lblProfileType.Text = "";
                lblAlterType.Text = "";
                lblAlterCode.Text = "";
            });
            kleProfiles.Properties.EditValueChanged += new EventHandler(delegate (object sender, EventArgs e)
            {
                ChangeProfile(kleProfiles.GetSelectedDataRow());

            });

            alarmPanel1.OpenAlarmListEvent += () =>
            {
                OpenAlarmList();
            };


            this.btnRefreshBarcode.Click += new EventHandler(delegate (object sender, EventArgs e)
            {
                this.OnBarcodeReferesh?.Invoke();
            });
        }

        public void LimitValidator()
        {

            knmWidthX.MinValue = Plc.ReadDevice32Bit("D144") / knmWidthX.Divisor;
            knmWidthX.MaxValue = Plc.ReadDevice32Bit("D146") / knmWidthX.Divisor;

            knmWidthY.MinValue = Plc.ReadDevice32Bit("D148") / knmWidthY.Divisor;
            knmWidthY.MaxValue = Plc.ReadDevice32Bit("D150") / knmWidthY.Divisor;

        }

        private void OpenAlarmList()
        {
            var uc = ucAlarms.Instance();


            var frmHost = new KabanHostForm(uc, KabanMachinesSystem.Common.Enums.PopupFormLevels.FirstLevel, threadStart: true);
            frmHost.FormText = uc.PageTitle;
            KabanUserControlStatics.SetActiveControls(uc, false);
            if (frmHost.ShowDialog() == DialogResult.OK)
            {
                //KabanUserControlStatics.RemoveFromActiveControls(uc);
            }
        }

        public static ucJobNew Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucJobNew();
                }

                return _instance;
            }
            catch (System.Exception exp)
            {

                throw exp;
            }
        }


        public void AsyncReadPLC()
        {
            alarmPanel1.AsyncReadPLC();

            if (KabanProgressList.Count == 0)
                KabanProgressList = Get_KabanProgressBars();

            

            if (KabanLedLabelList.Count == 0)
                KabanLedLabelList = Get_KabanLedLabels();


            UpdateUISafe(() =>
            {
                base.SyncProgressBars(KabanProgressList);
                base.SyncLabels(KabanLedLabelList);
            });

            ReadPLC();
            EnableDisableControls();

        }

        private void FillProfileLookUpEdit()
        {
            try
            {
                CompanyProfilesVMs = this.ProfileSeriesPProfileSubBusiness.GetAllBySql();

                kleProfiles.Properties.DisplayMember = "ComputedName";
                kleProfiles.Properties.ValueMember = "Id";
                kleProfiles.Properties.DataSource = CompanyProfilesVMs;
                kleProfiles.CustomizeColumn();
            }
            catch (Exception exp)
            {
                throw exp;
            }
        }

        private void kabanLedLabel18_Load(object sender, EventArgs e)
        {

        }

        public void FillByBarcode(string barcodeValue)
        {
            string filePath = Path.Combine(Application.StartupPath, "Log.txt");

            if (!File.Exists(filePath))
            {
                File.Create(filePath);
                //burada dosyaya yazsın
            }



            if (string.IsNullOrEmpty(barcodeValue))
            {
                UpdateUISafe(() => { MessageBox.Show(lblPleaseCheckTheProfileCode.Text, lblProfileNotFound.Text, MessageBoxButtons.OK, MessageBoxIcon.Information); });
                return;
            }

            //burada dosyaya barcodeValue yazsın
            barcodeValue = barcodeValue.Replace("\r", "");
            barcodeValue = barcodeValue.Replace("\u0003", "");
            barcodeValue = barcodeValue.Replace("\u0002", "");
            barcodeValue = barcodeValue.Replace("\n", "");


            if ((barcodeValue.Length > 21 || barcodeValue.Length < 18) && barcodeValue.Length != 12)
            {
                UpdateUISafe(() => { MessageBox.Show(lblPleaseCheckTheProfileCode.Text, lblProfileNotFound.Text, MessageBoxButtons.OK, MessageBoxIcon.Information); });
                return;
            }

            if (barcodeValue.Length == 12)
            {
                if (Plc.ReadDevice("M26") == 1)
                {
                    return;
                }
                OnUserBarcodeRead?.Invoke(barcodeValue);
                return;
            }

            string code = barcodeValue.Substring(0, 10);
            int X = int.Parse(barcodeValue.Substring(10, 4));
            int Y = int.Parse(barcodeValue.Substring(14, 4));

            if (X < Y)
            {
                int temp = X;
                X = Y;
                Y = temp;
            }

            var companyProfilesVMs = this.ProfileSeriesPProfileSubBusiness.GetAllBySql();

            var AlternateCodes = this.ProfileSeryAlternativeCodeBusiness.GetAll();

            var AlternateCode = AlternateCodes.FirstOrDefault(AlterCode => AlterCode.Code.ToString().PadLeft(10, '0') == code);

            var matchedProfile = companyProfilesVMs.FirstOrDefault(profile => (profile.Code != null && profile.Code.ToString().PadLeft(10, '0') == code) || (AlternateCode != null && profile.Id == AlternateCode.ProfileSeryProfileId));


            if (matchedProfile != null)
            {
                UpdateUISafe(() => { kleProfiles.EditValue = matchedProfile.Id; });
               
                ChangeProfile(kleProfiles.GetSelectedDataRow());

                PlcUtility.Int2Short(X * knmWidthX.Divisor, out short high, out short low);
                short[] numbers = new short[] { low, high };
                var result = Plc.SetDevice(knmWidthX.Device, low, this.PlcType);
                Plc.SetDevice(knmWidthX.DeviceDynamic, high, this.PlcType);

                PlcUtility.Int2Short(Y * knmWidthY.Divisor, out short highY, out short lowY);
                numbers = new short[] { lowY, highY };
                result = Plc.SetDevice(knmWidthY.Device, lowY, this.PlcType);
                Plc.SetDevice(knmWidthY.DeviceDynamic, highY, this.PlcType);

                UpdateUISafe(() => { knmWidthX.Value = X; });
                UpdateUISafe(() => { knmWidthY.Value = Y; });
            }
            else
            {
                UpdateUISafe(() => { MessageBox.Show(lblPleaseCheckTheProfileCode.Text, lblProfileNotFound.Text, MessageBoxButtons.OK, MessageBoxIcon.Information); });

            }
        }
        
        private void ChangeProfile(object SelectedDataRow)
        {
            var selectedDataRow = SelectedDataRow;
            if (selectedDataRow == null)
                return;

            var profileSeriesPProfileSubId = ((CompanyProfilesVM)this.kleProfiles.GetSelectedDataRow()).Id;
            var profileSettings = ProfileSeryProfileSettingBusiness.Get(profileSeriesPProfileSubId);
            if (profileSettings != null)
            {
                // this.txtProfileHeight.Text = profileSettings.ProfileRecognition_Height.ToString();
            }

            Guid dataId = ((CompanyProfilesVM)this.kleProfiles.GetSelectedDataRow()).Id;
            var dataSet = ProfileSeryProfileSettingBusiness.Get(dataId);

            var CompanyDetails = ((CompanyProfilesVM)this.kleProfiles.GetSelectedDataRow());

            UpdateUISafe(() => { lblProfileCodeValue.Text = CompanyDetails.Code.ToString(); });
            UpdateUISafe(() => { lblBrandType.Text = CompanyDetails.ProfileBrandType.ToString(); });
            UpdateUISafe(() => { lblSerius.Text = CompanyDetails.ProfileSerie.ToString(); });
            UpdateUISafe(() => { lblProfileType.Text = CompanyDetails.ProfileType.ToString(); });
            UpdateUISafe(() => { lblAlterType.Text = CompanyDetails.ProfileSubType.ToString(); });

            
            if (this.ProfileSeryAlternativeCodeBusiness.GetAll_ByParentGuid(dataId).Count > 0)
            {
                var AlternateCode = this.ProfileSeryAlternativeCodeBusiness.GetAll_ByParentGuid(dataId);

                UpdateUISafe(() => { lblAlterCode.Text = AlternateCode.FirstOrDefault().Code.ToString(); });
              
            }
            else
            {
                lblAlterCode.Text = "";
                UpdateUISafe(() => { lblAlterCode.Text = ""; });

            }


            PlcUtility.Int2Short(((CompanyProfilesVM)this.kleProfiles.GetSelectedDataRow()).Code, out short high, out short low);
            short[] numbers = new short[] { low, high };
            var result = Plc.SetDevice("R1500", low, this.PlcType);
            Plc.SetDevice("R1501", high, this.PlcType);

            dataSet.Profile_Width = dataSet.Profile_Width != null ? dataSet.Profile_Width : 0;
            dataSet.MoldDifference = dataSet.MoldDifference != null ? dataSet.MoldDifference : 0;
            dataSet.HeatingTime = dataSet.HeatingTime != null ? dataSet.HeatingTime : 0;
            dataSet.WeldingTime = dataSet.WeldingTime != null ? dataSet.WeldingTime : 0;
            dataSet.WeldingTemperature = dataSet.WeldingTemperature != null ? dataSet.WeldingTemperature : 0;


            string[] devices = { "R1502", "R1503", "R1504", "R1505", "R1506", "R1507", "D120", "D124", "M86" };
            int[] value = { (int)(dataSet.Profile_Width * 10), (int)(dataSet.MoldDifference * 10), (int)(dataSet.ProfileModeBytes), (int)(dataSet.HeatingTime * 10), (int)(dataSet.WeldingTime * 10), (int)(dataSet.WeldingTemperature * 10), 0, 0, 1 };

            for (int i = 0; i < devices.Length; i++)
            {
                Plc.SetDevice(devices[i], value[i], this.PlcType);
            }


        }

        public void Feed(JobEntity job)
        {

            if (CompanyProfilesVMs == null || job == null) return;

            try
            {
                if (!int.TryParse(job.ProfileCode, out int profileCodeInt))
                {
                    return;
                }

                var matchingProfile = CompanyProfilesVMs.FirstOrDefault(profile => profile.Code == profileCodeInt);

                if (matchingProfile == null)
                {
                    UpdateUISafe(() => { MessageBox.Show(lblPleaseCheckTheProfileCode.Text, lblProfileNotFound.Text, MessageBoxButtons.OK, MessageBoxIcon.Information); });
                    return;
                }

                UpdateUISafe(() => { kleProfiles.EditValue = matchingProfile.Id;});

                UpdateUISafe(() => { knmWidthX.Value = job.Height; });

                UpdateUISafe(() => { knmWidthY.Value = job.Width; });
                
                int h = (int)job.Height * 10;
                int w = (int)job.Width * 10;

                PlcUtility.Int2Short(h, out short high, out short low);
                Plc.SetDevice("D1200", low, this.PlcType);
                Plc.SetDevice("D1201", high, this.PlcType);

                PlcUtility.Int2Short(w, out short high2, out short low2);
                Plc.SetDevice("D1202", low2, this.PlcType);
                Plc.SetDevice("D1203", high2, this.PlcType);
            }
            catch (Exception ex)
            {
                // Olası bir hatada programın tamamen kapanmasını engeller
                System.Diagnostics.Debug.WriteLine("Feed Hatası: " + ex.Message);
            }

        }

        public void ReadPLC()
        {
            if (Plc.ReadDevice("D335") == 1 && (isAutomaticMode || isUserBarcodeActive))
            {
                Plc.SetDevice("D358", 0);

                if (!this.isUserBarcodeActive)
                {
                    var jobList = ucJobList.Instance();
                    jobList.ReadPLCUpdate();
                }
                else
                {
                    var jobListUserBarcode = ucJobListWithUserBarcode.Instance();
                    jobListUserBarcode.ReadPLCUpdate();
                }
            }
        }

        public void EnableDisableControls()
        {
            bool isEnable = true;
            if (Plc.ReadDevice("M26") == 1)
            {
                isEnable = false;

            }

            UpdateUISafe(() => { kleProfiles.Enabled = isEnable; });

            UpdateUISafe(() => { knmWidthX.Enabled = isEnable; });

            UpdateUISafe(() => { knmWidthY.Enabled = isEnable; });
      
            UpdateUISafe(() => { knmAdet.Enabled = isEnable; });

            UpdateUISafe(() =>
            {
                var jobList = ucJobList.Instance();
                jobList.EnableDisableControl(isEnable);
            });
        }

        public void SetUserBarcode(bool isActive)
        {
            this.isUserBarcodeActive = isActive;
        }

        public bool IsUserBarcodeActive()
        {
            return this.isUserBarcodeActive;
        }

        public void SetAutomaticMode(bool isAuto)
        {
            isAutomaticMode = isAuto;
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
    }
}
