using _MD3030.Presentation.Alarms;
using _MD3030.Presentation.Servos;
using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanDevBase.Forms;
using KabanDevBase.Statics;
using KabanMachinesSystem.Business.Helpers;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Common.Enums;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Presentation.CAM.Templates;
using KabanMachinesSystem.Statics;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace _MD3030.Presentation.Home.Preparations
{

    public partial class ucMainControlPanel : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucMainControlPanel _instance;

        public delegate void OnSendJobDelegate();
        public event OnSendJobDelegate OnSendJobEvent = null;

        #endregion

        #region [ MEMBERS ]
        private static KabanHostForm _servoForm;

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucMainControlPanel()
        {
            InitializeComponent();
            InitEvents();

            Translator.ChangeLangue_ByUI(this);
        }



        private void InitEvents()
        {
            btnCancel2.Click += (s, e) =>
            {
                MachineCancel();
            };

            alarmPanel1.OpenAlarmListEvent += () =>
            {
                OpenAlarmList();
            };

            btnStart.Click += (s, e) =>
            {
                bool jobDimensionValuesAccepted = ucJobDimentions.Instance().WriteDimentionsAlways();
                if(jobDimensionValuesAccepted)
                    MachineStart();
            };
            btnPause.Click += (s, e) =>
            {
                MachinePause();
            };
            btnServoSituations.Click += (s, e) => 
            {
                OpenServo();
            };
        }



        public static ucMainControlPanel Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucMainControlPanel();
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

        


        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]


        private void MachineStart()
        {
            OnSendJobEvent?.Invoke();
        }
        private void MachinePause()
        {
            Plc.SetDevice(Constants.MachineIsPause, 1);
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
        private void MachineCancel()
        {

            var dialogResult = KabanMessageBox.ShowMessage(MessageHelper.GetMessage(MessageType.CancelOperation), KabanDevBase.Enums.MessageBoxType.Question);
            if (dialogResult != System.Windows.Forms.DialogResult.Yes)
                return;

            var machineIsWorking = Plc.GetDevice(Constants.MachineIsWorking_Device_MD3030);
            if (machineIsWorking == 0)
            {
                Plc.SetDevice(Constants.MachineCancel_Device_MD3030, 1);
                return;
            }


            Plc.SetDevice(Constants.MachineCancel_Device_MD3030, 1);
        }




        public void AsyncReadPLC()
        {
            alarmPanel1.AsyncReadPLC();

            //if (InvokeRequired)
            //{
            //    Invoke(new System.Windows.Forms.MethodInvoker(delegate
            //    {
            //        ButtonStyleChange();
            //    }));
            //}
            //else
            //{
            //    ButtonStyleChange();
            //}

            ButtonStyleChange();
        }


        private void OpenAlarmList()
        {
            var uc = ucAlarms.Instance();


            var frmHost = new KabanHostForm(uc, KabanMachinesSystem.Common.Enums.PopupFormLevels.FirstLevel, threadStart:true);
            frmHost.FormText = uc.PageTitle;
            KabanUserControlStatics.SetActiveControls(uc, false);
            if (frmHost.ShowDialog() == DialogResult.OK)
            {
                //KabanUserControlStatics.RemoveFromActiveControls(uc);
            }
        }

        private void ButtonStyleChange()
        {
            var btnStartColor = btnStart.Appearance.BackColor.Name;
            //var startMode = Plc.ReadDevice("M21", false);
            var startMode = Plc.ReadDevice("M316");
            if (startMode == 0)
            {
                if (btnStartColor != SystemColors.Control.Name)
                    btnStart.SetBackColorSafe(SystemColors.Control);
            }
            else
            {
                if (btnStartColor != Color.GreenYellow.Name)
                    btnStart.SetBackColorSafe(Color.GreenYellow);
            }


            var btnPauseColor = btnPause.Appearance.BackColor.Name;
            //var pauseMode = Plc.ReadDevice(btnPause.Device, false);
            var pauseMode = Plc.ReadDevice(btnPause.Device);
            if (pauseMode == 0)
            {
                if (btnPauseColor != SystemColors.Control.Name)
                    btnPause.SetBackColorSafe(SystemColors.Control);
            }
            else
            {
                if (btnPauseColor != Color.GreenYellow.Name)
                    btnPause.SetBackColorSafe(Color.GreenYellow);
            }
        }


        #endregion
    }
}
