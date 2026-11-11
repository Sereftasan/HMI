using KabanDevBase.Controls;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace _MD3030.Presentation.Parameters
{
    public partial class ucProcessParameters : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucProcessParameters _instance;
        #endregion

        #region [ MEMBERS ]
        

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucProcessParameters()
        {
            InitializeComponent();

            InitDefaults();

            FillSwitchCodes();

            Translator.ChangeLangue_ByUI(this);
        }

        public static ucProcessParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucProcessParameters();
                }

                return _instance;
            }
            catch (System.Exception exp)
            {

                throw;
            }
        }

        private void InitDefaults()
        {
        }

        #endregion

        #region [ EVENTS ]




        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]

        public void FillSwitchCodes()
        {
            ktsTestMode.Value = Plc.ReadDevice("L3", this.PlcType) == 1;
            ktsMeasurementWithResourceShare.Value = Plc.ReadDevice("L0", this.PlcType) == 1;
            ktsBarrierControlByPass.Value = Plc.ReadDevice("L5", this.PlcType) == 1;
            ktsLoadingWithX1Y.Value = Plc.ReadDevice("L6", this.PlcType) == 1;
            ktsUProfile.Value = Plc.ReadDevice("L13", this.PlcType) == 1;
        }

        public void SendSwitchCodes()
        {
            if (ktsTestMode.Value == true)
            {
                Plc.SetDevice("L3", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("L3", 0, this.PlcType);
            }

            if (ktsMeasurementWithResourceShare.Value == true)
            {
                Plc.SetDevice("L0", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("L0", 0, this.PlcType);
            }

            if (ktsBarrierControlByPass.Value == true)
            {
                Plc.SetDevice("L5", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("L5", 0, this.PlcType);
            }

            if (ktsLoadingWithX1Y.Value == true)
            {
                Plc.SetDevice("L6", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("L6", 0, this.PlcType);
            }

            if (ktsUProfile.Value == true)
            {
                Plc.SetDevice("L13", 1, this.PlcType);
            }
            else
            {
                Plc.SetDevice("L13", 0, this.PlcType);
            }

        }

        public void AsyncReadPLC()
        {
            SendSwitchCodes();
        }

        #endregion

        private void BoxWrapper_Paint(object sender, PaintEventArgs e)
        {

        }
    }
}
