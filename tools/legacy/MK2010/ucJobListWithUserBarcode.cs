using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using KabanDevBase.Controls;
using KabanMachinesSystem.Statics;
using _MK2010.Business;
using _MK2010.Entities;
using KabanMachinesSystem.Business.PlcObjects;
using KabanDevBase.Extensions;
using DevExpress.XtraGrid.Views.Grid;

namespace _MK2010.Presentation.Home.Preparations
{

    public partial class ucJobListWithUserBarcode : KabanUserControl
    {

        private static ucJobListWithUserBarcode _instance;
        JobEntity selectedJob = null;

        public event OnUserBarcodeReadDelegate OnUserBarcodeRead = null;

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanLambList;


        public ucJobListWithUserBarcode()
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
        }

        public static ucJobListWithUserBarcode Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucJobListWithUserBarcode();
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
            grdJobList1.DoubleClick += GridView1_DoubleClick;

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


            //FillGrid();
        }



        public void FillGrid()
        {
            var joblist = JobControllerBussiness.GetJobList();
            if (joblist == null) return;

            // --- DÜZELTME ---
            // Listeyi doğrudan referans olarak atamak yerine .ToList() ile kopyalıyoruz.
            // Bu sayede 'MoveJobToTop' veya 'RemoveJob' çalışırken Grid etkilenmez.
            grdJobList1.DataSource = joblist.ToList();

            grdJobList1.MainView.PopulateColumns();

            grdJobList1.Refresh();
        }

        public void FillGridSafe()
        {
            if (this.IsDisposed || !this.IsHandleCreated || grdJobList1.IsDisposed)
                return;

            if (grdJobList1.InvokeRequired)
            {
                grdJobList1.Invoke((MethodInvoker)(() => FillGrid()));
            }
            else
            {
                FillGrid();
            }
        }

        private void GridView1_DoubleClick(object sender, EventArgs e)
        {
            /*
            Feed();*/
        }


        public void CallProfileWithCustomBarcode(string CustomBarcode)
        {
            List<JobEntity> jobs = JobControllerBussiness.GetJobList();

            var job = jobs.FirstOrDefault(j => j.CustomBarcode == CustomBarcode);

            if (job == null)
            {
                return;
            }

            selectedJob = job;

            Feed(selectedJob);
            JobControllerBussiness.MoveJobToTop(selectedJob);
            FillGridSafe();

        }


        private void Feed(JobEntity job)
        {
            List<JobEntity> jobs = JobControllerBussiness.GetJobList();

            var JobPage = ucJobNew.Instance();

            if (jobs.Count <= 0)
            {
                JobPage.SetAutomaticMode(false);
                return;
            }

            JobPage.SetAutomaticMode(false);
            Plc.SetDevice("D358", 0);
            JobPage.Feed(job);
        }

        public void ReadPLCUpdate()
        {

            List<JobEntity> jobs = JobControllerBussiness.GetJobList();
            if (jobs.Count <= 0)
            {
                return;
            }

            if(selectedJob!= null)
            {
                JobEntity job = selectedJob;
                JobControllerBussiness.RemoveJob(job);
                FillGridSafe();
            }
            
        }

       

        public void EnableDisableControl(bool isEnable){

           // btnFeed.Enabled = isEnable;
            btnClear.Enabled = isEnable;
        }
    }
}
