using _H500.Presentation.Home.Alarms;
using H500.Statics;
using KabanDevBase.Controls;
using KabanDevBase.Forms;
using KabanDevBase.Statics;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Statics;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace _H500.Presentation.Home
{
    public delegate void OnScrewingProgressDelegate();
    public partial class ucScrewingMainControlPanel : KabanUserControl
    {
        public delegate void OnStartMachineDelegate();
        

        public event OnStartMachineDelegate OnStartMachineEvent = null;
        public event OnScrewingProgressDelegate OnScrewingProgressEvent = null;


        #region [ INSTANCES ]
        private static ucScrewingMainControlPanel _instance;
        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucScrewingMainControlPanel()
        {
            InitializeComponent();
            InitEvents();
            //this.PlcType = KabanMachinesSystem.Common.Enums.PlcType.ScrewingCPU;
            InitDefaults();

            Translator.ChangeLangue_ByUI(this);

        }


        private void InitDefaults()
        {
            this.SetPLCByType();
            if(this.PlcType == KabanMachinesSystem.Common.Enums.PlcType.ScrewingFX5CPU)
            {
                this.btnPause.Device = "M4";
                this.btnCancel.Device = "M3";

                this.btnPause.PlcType = KabanMachinesSystem.Common.Enums.PlcType.ScrewingFX5CPU;
                this.btnCancel.PlcType = KabanMachinesSystem.Common.Enums.PlcType.ScrewingFX5CPU;
            }
        }

        private void InitEvents()
        {
            alarmPanelScrewing1.OpenAlarmListEvent += () =>
            {
                OpenAlarmList();
            };

            btnStart.Click += (s, e) =>
            {

                if(this.PlcType == KabanMachinesSystem.Common.Enums.PlcType.ScrewingFX5CPU)
                {
                    var machineReadytoWork = Plc.ReadDevice("M1", this.PlcType);
                    if (machineReadytoWork == 0)
                        return;
                }


                //Plc.SetDevice(Constants.ScrewingJobFinish, 0, true);
                Plc.SetDevice(Constants.ScrewingJobFinish, 0, this.PlcType); // KabanMachinesSystem.Common.Enums.PlcType.ScrewingCPU);

                OnStartMachineEvent?.Invoke();

                //Plc.SetDevice("M1", 1, true);
                if (this.PlcType == KabanMachinesSystem.Common.Enums.PlcType.ScrewingFX5CPU)
                {
                    Plc.SetDevice("M2", 1, this.PlcType); // KabanMachinesSystem.Common.Enums.PlcType.ScrewingCPU);
                    Thread.Sleep(150);
                    Plc.SetDevice("M2", 0, this.PlcType);
                }
                else
                {
                    Plc.SetDevice("M1", 1, this.PlcType);
                }

                OnScrewingProgressEvent?.Invoke();
            };
        }



        public static ucScrewingMainControlPanel Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucScrewingMainControlPanel();
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


        private void OpenAlarmList()
        {
            try
            {
                var uc = ucAlarmsScrewing.Instance();
                uc.PlcType = this.PlcType;
                var frmHost = new KabanHostForm(uc, KabanMachinesSystem.Common.Enums.PopupFormLevels.FirstLevel, threadStart:true);
                frmHost.FormText = uc.PageTitle;

                KabanUserControlStatics.SetActiveControlScrewing(uc, false);
                if (frmHost.ShowDialog() == DialogResult.OK)
                {
                    KabanUserControlStatics.RemoveFromActiveScrewingControls(uc);
                }
            }
            catch (System.Exception)
            {

                
            }

           


        }

        private void ButtonStyleChange()
        {
            try
            {

                var btnStartColor = btnStart.Appearance.BackColor.Name;
                //var startMode = Plc.ReadDevice("M1", true);
                var startMode = 0;
                if (this.PlcType == KabanMachinesSystem.Common.Enums.PlcType.ScrewingFX5CPU)
                {
                    // M0 = 1 means machine is working
                    startMode = Plc.ReadDevice("M0", this.PlcType); // KabanMachinesSystem.Common.Enums.PlcType.ScrewingCPU);
                }
                else
                {
                    startMode = Plc.ReadDevice("M2", this.PlcType);
                }

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
                //var pauseMode = Plc.ReadDevice(btnPause.Device, true);
                var pauseMode = Plc.ReadDevice(btnPause.Device, this.PlcType); // KabanMachinesSystem.Common.Enums.PlcType.ScrewingCPU);
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
            catch (System.Exception)
            {
            }
        }

        public void AsyncReadPLC()
        {

            alarmPanelScrewing1.AsyncReadPLC(this.PlcType);

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


        #endregion

    }
}
