using DevExpress.XtraEditors;
using KabanDevBase.Controls;
using KabanDevBase.Plc_Componets;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Parameters
{
    public partial class ucRobotAxisCalibration : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucRobotAxisCalibration _instance;
        #endregion

        #region [ MEMBERS ]

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanInputList;

        bool isRendered = false;

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucRobotAxisCalibration()
        {

            if (InvokeRequired)
            {
                InitializeComponent();
                KabanLedLabelList = new List<Control>();
                KabanInputList = new List<Control>();
                InitEvents();

                if (!isRendered)
                {
                    base.ShowWaitForm();


                    Thread t = new Thread((ThreadStart)(() => {
                        RenderDynamicControls();
                    }));

                    t.SetApartmentState(ApartmentState.STA);
                    t.Start();
                    t.Join();


                    base.CloseWaitForm();

                    isRendered = true;
                }
                //
                Translator.ChangeLangue_ByUI(this);
                RefreshPLCSources();
            }
            else
            {
                InitializeComponent();
                KabanLedLabelList = new List<Control>();
                KabanInputList = new List<Control>();
                InitEvents();

                if (!isRendered)
                {
                    base.ShowWaitForm();

                    Thread t = new Thread((ThreadStart)(() => {
                        RenderDynamicControls();
                    }));

                    t.SetApartmentState(ApartmentState.STA);
                    t.Start();
                    t.Join();

                    base.CloseWaitForm();

                    isRendered = true;
                }
                //
                Translator.ChangeLangue_ByUI(this);
                RefreshPLCSources();
            }

        }

        public static ucRobotAxisCalibration Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucRobotAxisCalibration();
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

        private void InitEvents()
        {
            tsmRefreshPage.Click += (s, e) =>
            {
                base.ShowWaitForm();
                RefreshPLCSources();
                base.CloseWaitForm();

            };

            //xtraScrollableControl1.Scroll += (s, e) =>
            //{
            //    var vs = xtraScrollableControl1.VerticalScroll;
            //    if (e.NewValue != vs.Maximum - vs.LargeChange + 1)
            //        return;

            //    MessageBox.Show("Test");
            //};
        }


        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]

        public void AsyncReadPLC()
        {
            var Check_M23 = Plc.GetDevice("M23");
            if (Check_M23 != 0)
                return;

            if (KabanLedLabelList.Count == 0)
                KabanLedLabelList = Get_KabanLedLabels();

            base.SyncLabels(KabanLedLabelList);

        
        }




        private void RenderDynamicControls()
        {
            var RobotPos_DeviceStart = 31200;
            var ErrorQuantity_DeviceStart = 31600;
            var prefix = "R";
            //
            if (MachineInfosStatic.MachineInfo.Name == "ZC2010")
            {
                RobotPos_DeviceStart = 1000;
                ErrorQuantity_DeviceStart = 1400;
                prefix = "D";
            }

            var hLoc = 37;



            for (int i = 1; i < 200; i++)
            {
                //
                var lblControl = new LabelControl();
                lblControl.Appearance.Font = new System.Drawing.Font("Tahoma", 10F);
                lblControl.Appearance.Options.UseFont = true;
                lblControl.Appearance.Options.UseTextOptions = true;
                lblControl.Appearance.TextOptions.HAlignment = DevExpress.Utils.HorzAlignment.Center;
                lblControl.AutoSizeMode = DevExpress.XtraEditors.LabelAutoSizeMode.None;
                lblControl.BorderStyle = DevExpress.XtraEditors.Controls.BorderStyles.Simple;
                lblControl.LookAndFeel.SkinName = "Visual Studio 2013 Dark";
                lblControl.LookAndFeel.UseDefaultLookAndFeel = false;
                lblControl.Size = new System.Drawing.Size(186, 31);
                lblControl.TabIndex = 0;
                //
                lblControl.Name = $"knbErrorQuantity{i}";
                lblControl.Text = $"Fixed Position {i}";
                lblControl.Location = new System.Drawing.Point(0, 0 + (i * hLoc));

                //

                var knbRobotPos = new KabanNumericButton();
                knbRobotPos.Device = $"{prefix}{RobotPos_DeviceStart}";
                knbRobotPos.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
                knbRobotPos.Mask = "n4";
                knbRobotPos.Divisor = 100;
                knbRobotPos.Size = new System.Drawing.Size(102, 31);
                knbRobotPos.TabIndex = i;
                knbRobotPos.Name = $"knbRobotPos{i}";
                knbRobotPos.Location = new System.Drawing.Point(192, 0 + (i * hLoc));
                knbRobotPos.Tag = "IgnorePLC";

                //
                var knbErrorQuantity = new KabanNumericButton();
                knbErrorQuantity.Device = $"{prefix}{ErrorQuantity_DeviceStart}";
                knbErrorQuantity.DataType = KabanMachinesSystem.Common.Enums.DataType.Bit_32;
                knbErrorQuantity.Mask = "n4";
                knbErrorQuantity.Divisor = 1000;
                knbErrorQuantity.Size = new System.Drawing.Size(102, 31);
                knbErrorQuantity.TabIndex = i;
                knbErrorQuantity.Name = $"knbErrorQuantity{i}";
                knbErrorQuantity.Location = new System.Drawing.Point(300, 0 + (i * hLoc));
                knbErrorQuantity.Tag = "IgnorePLC";








                RobotPos_DeviceStart += 2;
                ErrorQuantity_DeviceStart += 2;


                xtraScrollableControl1.Controls.Add(knbRobotPos);
                xtraScrollableControl1.Controls.Add(knbErrorQuantity);
                xtraScrollableControl1.Controls.Add(lblControl);

            }
        }


        public void RefreshPLCSources()
        {
            if (KabanInputList.Count == 0)
                KabanInputList = Get_KabanNumericButtons();

            base.SyncInputs(KabanInputList);

            if (KabanLedLabelList.Count == 0)
                KabanLedLabelList = Get_KabanLedLabels();

            base.SyncLabels(KabanLedLabelList);

        }

        #endregion
    }
}
