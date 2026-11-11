using _MD3030.Presentation.Alarms;
using _MD3030.Presentation.Servos;
using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanDevBase.Forms;
using KabanDevBase.Statics;
using KabanMachinesSystem.Business.BusinessLayer;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Data.Entities;
using KabanMachinesSystem.Presentation.Home.Alarms;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using ucAlarms = _MD3030.Presentation.Alarms.ucAlarms;

namespace _MD3030.Presentation.Home.Common
{
    [ToolboxItem(true)]
    public partial class ucHeadControlButtons : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]

        private static List<Control> KabanLambList;
        private static List<Control> KabanLedLabelList;

        #endregion

        #region [ MEMBERS ]

        private static KabanHostForm _servoForm;
        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        public ucHeadControlButtons()
        {
            InitializeComponent();
            InitEvents();


            if (this.IsInDesignerMode())
                return;

            KabanLambList = new List<Control>();
            KabanLedLabelList = new List<Control>();

            Translator.ChangeLangue_ByUI(this);
        }
        private void InitEvents()
        {


            alarmPanel1.OpenAlarmListEvent += () =>
            {
                OpenAlarmList();
            };
            btnServoSituations.Click += (s, e) =>
            {
                OpenServo();
            };
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

        private void OpenServo()
        {
            foreach (Form frm in Application.OpenForms)
            {
                if (frm is KabanHostForm && frm.Text == "Servo")
                {
                    frm.Activate();
                    frm.BringToFront();
                    return;
                }
            }

            var uc = ucServos.Instance();

            var frmHost = new KabanHostForm(
                uc,
                KabanMachinesSystem.Common.Enums.PopupFormLevels.FirstLevel,
                threadStart: true);

            frmHost.FormText = uc.PageTitle;

            KabanUserControlStatics.SetActiveControls(uc, false);

            frmHost.Show();
        }

        public void AsyncReadPLC()
        {
            alarmPanel1.AsyncReadPLC();

            if (KabanLambList.Count == 0)
                KabanLambList = Get_KabanLambs();

            base.SyncLambs(KabanLambList);

            if (KabanLedLabelList.Count == 0)
                KabanLedLabelList = Get_KabanLedLabels();

            base.SyncLabels(KabanLedLabelList);

            kabanTrackBar1.TrackBarInit();
        }


        //private void GetJobState()
        //{
        //    var m0 = Plc.GetDevice("M0");
        //    var m1 = Plc.GetDevice("M1");
        //    var m2 = Plc.GetDevice("M2");
        //    if(m0 == 1 || m1 == 1|| m2 == 1)
        //    {

        //    }
        //    else
        //    {

        //    }
        //}


        #endregion

        #region [ EVENTS ]




        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]



        #endregion
    }
}
