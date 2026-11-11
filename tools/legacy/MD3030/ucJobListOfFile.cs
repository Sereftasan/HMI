using _MD3030.Business;
using _MD3030.Entities;
using DevExpress.XtraGrid.Views.Grid;
using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Common.Helpers;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace _MD3030.Presentation.Home.Preparations
{

    public partial class ucJobListOfFile : KabanUserControl, IPlcComponent
    {

        private static ucJobListOfFile _instance;

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanLambList;

        private bool initialized = false;

        private ucJobListOfFile()
        {
            InitializeComponent();

            if (this.IsInDesignerMode())
                return;

            KabanLedLabelList = new List<Control>();
            KabanProgressList = new List<Control>();
            KabanLambList = new List<Control>();


            InitEvents();
            FillGridSafe();

            Translator.ChangeLangue_ByUI(this);
            initialized = true;
        }

        public static ucJobListOfFile Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucJobListOfFile();
                }

                return _instance;
            }
            catch (System.Exception exp)
            {

                throw exp;
            }
        }
        public void InitEvents()
        {

            btnFeed.Click += new EventHandler(delegate (object sender, EventArgs e)
            {
                Feed();
            });

            btnClear.Click += new EventHandler(delegate (object sender, EventArgs e)
            {

                var YesNo = MessageBox.Show(btnMessagebox334.Text, btnMessagebox333.Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (YesNo == DialogResult.Yes)
                {
                    JobControllerBussiness.CleanJobList();
                    FillGridSafe();
                }
            });
            grdJobList.DoubleClick += GridView1_DoubleClick;

        }




        public void AsyncReadPLC()
        {

            if (KabanProgressList.Count == 0)
                KabanProgressList = Get_KabanProgressBars();


            if (KabanLedLabelList.Count == 0)
                KabanLedLabelList = Get_KabanLedLabels();


            if (KabanLambList.Count == 0)
                KabanLambList = Get_KabanLambs();


            UpdateUISafe(() =>
            {
                base.SyncProgressBars(KabanProgressList);
                base.SyncLabels(KabanLedLabelList);
                base.SyncLambs(KabanLambList);
            });
            var isMachineWork = Plc.GetDevice(Constants.MachineIsWorking_Device_MD3030);
            UpdateUISafe(() =>
            {
                SetVisibles(isMachineWork);
            });
            //FillGrid();
        }

        public void SetVisibles(short isMachineWork) {
            if (isMachineWork != 0)
            {
                btnFeed.Enabled = false;
                btnClear.Enabled = false;
            }
            else
            {
                btnFeed.Enabled = true;
                btnClear.Enabled = true;
            }
        }

        public void FillGrid()
        {
            // Orijinal listeyi al
            var joblist = JobControllerBussiness.GetJobList();

            if (joblist == null) return;

            UpdateUISafe(() => { grdJobList.DataSource = joblist.ToList(); });
            UpdateUISafe(() => { grdJobList.MainView.PopulateColumns(); });
            

            var view = grdJobList.MainView as GridView;
            // null kontrolü eklemek her zaman iyidir
            if (view.Columns["CustomBarcode"] != null)
            {
                UpdateUISafe(() => { view.Columns["CustomBarcode"].Visible = false; });
                UpdateUISafe(() => { view.Columns["CustomBarcode"].OptionsColumn.ShowInCustomizationForm = false; });
            }
            foreach (var c in view.Columns.ToArray()) {
                if (c.Visible == false)
                    c.Visible = true;

            }
            UpdateUISafe(() => { grdJobList.Refresh(); });
           
        }

        public void FillGridSafe()
        {
            if (this.IsDisposed || grdJobList.IsDisposed)
                return;

            UpdateUISafe(() => { FillGrid(); });
           
        }

        private void GridView1_DoubleClick(object sender, EventArgs e)
        {
            
        }

        private void Feed()
        {
            List<JobEntity> jobs = JobControllerBussiness.GetJobList();

            var JobPage = ucActiveJobScreen.Instance();

            if (jobs.Count <= 0)
            {
                return;
            }
            JobEntity job = jobs[0];
            var xdasda = grdJobList.GetSelectedRows().FirstOrDefault();

            JobEntity rowHandle = (JobEntity)grdJobList.GetSelectedRows().FirstOrDefault();

            var value = grdJobList.GetType();
            int x = Convert.ToInt32( rowHandle.Height);
            int y = Convert.ToInt32(rowHandle.Width);
            ucJobPreparations.Instance().Feed(rowHandle.ProfileCode,rowHandle.CustomBarcode,x, y);
            
            JobControllerBussiness.RemoveJob(job);
            FillGridSafe();
            //JobPage.Feed(job);
        }
        private void Feed(JobEntity job)
        {

        }
        public void ReadPLCUpdate()
        {

            List<JobEntity> jobs = JobControllerBussiness.GetJobList();
            if (jobs.Count <= 0)
            {
                return;
            }
            JobEntity job = jobs[0];
            JobControllerBussiness.RemoveJob(job);
            FillGridSafe();
            Feed();

        }


        public void NoAbleThis(bool ableity)
        {
            UpdateUISafe(() => {
                if(this != null)
                    if(initialized==true)
                        if(grdJobList != null)
                        {

                            var view = grdJobList.MainView as DevExpress.XtraGrid.Views.Grid.GridView;
                            if (view == null) return;

                            view.OptionsBehavior.Editable = ableity;
                            view.OptionsBehavior.ReadOnly = ableity;
                            grdJobList.Enabled = ableity;
                        }
            });
        }

        public void EnableDisableControl(bool isEnable){

            UpdateUISafe(() => { btnFeed.Enabled = isEnable; });
           
            UpdateUISafe(() => { btnClear.Enabled = isEnable; });
        }


        public void UpdateUISafe(Action action)
        {
            if (this.IsDisposed) return;

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

        private void BoxWrapper_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
