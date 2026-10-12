using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanDevBase.Forms;
using KabanDevBase.Statics;
using KabanMachinesSystem.Business.Helpers;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Enums;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Presentation.Home.Alarms;
using KabanMachinesSystem.Statics;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace KabanMachinesSystem.Presentation.Home
{

    public partial class ucMainControlPanel : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucMainControlPanel _instance;

        public delegate void OnSendJobDelegate();
        public event OnSendJobDelegate OnSendJobEvent = null;
        public Action MachineWorkChangeDevice = null;

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucMainControlPanel()
        {
            //if (InvokeRequired)
            //{
            //    Invoke(new System.Windows.Forms.MethodInvoker(delegate
            //    {
            //        InitializeComponent();
            //        InitEvents();

            //        Translator.ChangeLangue_ByUI(this);
            //    }));
            //}
            //else
            //{
            //    InitializeComponent();
            //    InitEvents();

            //    Translator.ChangeLangue_ByUI(this);
            //}


            InitializeComponent();

            InitEvents();

            Init_LeftProfileRecognition();

            Translator.ChangeLangue_ByUI(this);

        }


        //private void ChangeKabanTrackBarDevice()
        //{
        //    try
        //    {
        //        if (MachineInfosStatic.MachineName == "ZU2010")
        //        {
        //            if (kabanTrackBar1 != null)
        //                kabanTrackBar1.Device = "D408";
        //        }
        //    }
        //    catch (Exception ex)
        //    {

        //    }
        //}


        //public void HideWasteButton() { kbnWaste.Visible = false; }
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
                MachineStart();
            };

            kbnWaste.Click += (s, e) =>
            {
                var wasteMode = Plc.GetDevice("L26");
                if(wasteMode == 0)
                {
                    Plc.SetDevice("L100", 0);
                }
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

        private void MachineCancel()
        {
            var machineIsWorking = Plc.GetDevice(Constants.MachineIsWorking_Device);
            if (machineIsWorking == 0)
            {
                Plc.SetDevice(Constants.MachineCancel_Device, 1);
                return;
            }


            var dialogResult = KabanMessageBox.ShowMessage(MessageHelper.GetMessage(MessageType.CancelOperation), KabanDevBase.Enums.MessageBoxType.Question);
            if (dialogResult != System.Windows.Forms.DialogResult.Yes)
                return;



            Plc.SetDevice(Constants.MachineCancel_Device, 1);
        }


        public void changeTrackbarDevice(string device)
        {
            if (kabanTrackBar1 != null)
                kabanTrackBar1.Device = device;
        }

        public void AsyncReadPLC()
        {

            //Ana main sayfası olduğundan dolayı tüm makinalar bu methodu kullanıyor makina bazlı özel kodların burada olmaması gerekiyor. 
         
            MachineWorkChangeDevice?.Invoke();

            //if (MachineInfosStatic.MachineName == "ZU2010")
            //{
            //    try
            //    {
            //        var setKabantrackbarDeviceViaRespectingTheZU2010ISMachineWorkingPrinciplesBit = Plc.GetDevice(Constants.MachineIsWorking_Device);
            //        if (setKabantrackbarDeviceViaRespectingTheZU2010ISMachineWorkingPrinciplesBit == 0 && MachineInfosStatic.MachineName == "ZU2010")
            //        {
            //            if (kabanTrackBar1 != null)
            //                kabanTrackBar1.Device = "D408";
            //        }
            //        else
            //        {
            //            if (kabanTrackBar1 != null)
            //                kabanTrackBar1.Device = "D409";
            //        }
            //    }
            //    catch (Exception ex)
            //    {

            //    }
            //}
            

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
            var startMode = Plc.ReadDevice("M21");
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


            var btnLeftFeedColor = btnLeftFeed.Appearance.BackColor.Name;
            //var leftFeedMode = Plc.ReadDevice(btnLeftFeed.Device, false);
            var leftFeedMode = Plc.ReadDevice(btnLeftFeed.Device);
            if (leftFeedMode == 0)
            {
                if (btnLeftFeedColor != SystemColors.Control.Name)
                    btnLeftFeed.SetBackColorSafe(SystemColors.Control);
            }
            else
            {
                if (btnLeftFeedColor != Color.GreenYellow.Name)
                    btnLeftFeed.SetBackColorSafe(Color.GreenYellow);
            }

            var kbnWasteColor = kbnWaste.Appearance.BackColor.Name;
            //var wasteMode = Plc.ReadDevice(kbnWaste.Device, false);
            var wasteMode = Plc.ReadDevice(kbnWaste.Device);
            if (wasteMode == 0)
            {
                if (kbnWasteColor != SystemColors.Control.Name)
                    kbnWaste.SetBackColorSafe(SystemColors.Control);
            }
            else
            {
                if (kbnWasteColor != Color.GreenYellow.Name)
                    kbnWaste.SetBackColorSafe(Color.Coral);
            }
            
            

        }

        private void Init_LeftProfileRecognition()
        {
            try
            {
                var result = Plc.GetDevice(Constants.LeftProfileRecognition_Enable);
                if (result < 0)
                    return;


                var leftProfileRecognition_IsEnable = Convert.ToBoolean(result);

                if (leftProfileRecognition_IsEnable)
                    this.btnLeftFeed.Visible = true;
                else
                    this.btnLeftFeed.Visible = false;
            }
            catch (Exception exp)
            {
                throw exp;
            }
        }


        #endregion
    }
}
