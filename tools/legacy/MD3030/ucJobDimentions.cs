using KabanDevBase.Controls;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _MD3030.Presentation.Home.Preparations
{
    public partial class ucJobDimentions : KabanUserControl
    {
        private static ucJobDimentions _instance;

        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanLambList;
        private static List<Control> KabanInputList;

        private ucJobDimentions()
        {
            InitializeComponent();

            KabanLedLabelList = new List<Control>();
            KabanProgressList = new List<Control>();
            KabanLambList = new List<Control>();
            KabanInputList = new List<Control>();

            Translator.ChangeLangue_ByUI(this);

        }

        public static ucJobDimentions Instance()
        {
            if (_instance == null)
            {
                _instance = new ucJobDimentions();
            }

            return _instance;
        }

        public bool WriteDimentionsAlways()
        {
            try
            {
                string knmXDevice = knmX.Device;
                string knmYDevice = knmY.Device;

                float knmXValue = float.Parse(knmX.Value.ToString());
                float knmYValue = float.Parse(knmY.Value.ToString());

                int knmXDivisor = knmX.Divisor;//4100
                int knmYDivisor = knmY.Divisor;//2300

                if (knmXValue > 4100)
                {
                    MessageBox.Show($"Value must be in the limit between {0} and {4100}", "Limit Exceeded!", MessageBoxButtons.OK);

                    return false;
                }
                if (knmYValue > 2300)
                {
                    MessageBox.Show($"Value must be in the limit between {0} and {2300}", "Limit Exceeded!", MessageBoxButtons.OK);
                    return false;
                }

                Plc.SetDevice(knmXDevice, (int)(knmXValue * knmXDivisor));
                Plc.SetDevice(knmYDevice, (int)(knmYValue * knmYDivisor));
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Please check the Frame Width and Frame Depth values.");
                return false;
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
        }
    }
}
