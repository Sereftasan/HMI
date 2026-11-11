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

    public partial class ucJobList : KabanUserControl
    {

        private static ucJobList _instance;

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanLambList;


        public ucJobList()
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

        public static ucJobList Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucJobList();
                }

                return  _instance;
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

            //FillGrid();
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

            UpdateUISafe(() => { grdJobList.Refresh(); });
           
        }

        public void FillGridSafe()
        {
            if (this.IsDisposed || !this.IsHandleCreated || grdJobList.IsDisposed)
                return;

            UpdateUISafe(() => { FillGrid(); });
           
        }

        private void GridView1_DoubleClick(object sender, EventArgs e)
        {
            
        }

        private void Feed()
        {
            List<JobEntity> jobs = JobControllerBussiness.GetJobList();

            var JobPage = ucJobNew.Instance();

            if (jobs.Count <= 0)
            {
                JobPage.SetAutomaticMode(false);
                return;
            }
            JobEntity job = jobs[0];

            JobPage.SetAutomaticMode(true);
            JobPage.Feed(job);
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

       

        public void EnableDisableControl(bool isEnable){

            UpdateUISafe(() => { btnFeed.Enabled = isEnable; });
           
            UpdateUISafe(() => { btnClear.Enabled = isEnable; });
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
