using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Common.Helpers;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System;
using System.ComponentModel;
using System.Windows.Forms;

namespace _MD3030.Presentation.Home.Preparations
{
    [ToolboxItem(true)]
    public partial class ucProfileInfoPanel : KabanUserControl
    {


        #region [ INSTANCES ]
        //private static ucProfileInfoPanel _instance;

        #endregion

        #region [ MEMBERS ]




        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        public ucProfileInfoPanel()
        {

            InitializeComponent();


            if (this.IsInDesignerMode())
                return;

            InitDefaults();
                Translator.ChangeLangue_ByUI(this);



        }

        //public static ucProfileInfoPanel Instance()
        //{
        //    try
        //    {
        //        if (_instance == null)
        //        {
        //            _instance = new ucProfileInfoPanel();
        //        }

        //        return _instance;
        //    }
        //    catch (System.Exception)
        //    {

        //        throw;
        //    }
        //}


        private void InitDefaults()
        {
            //Canvas = new ucDXFReaderCanvas();
            //Canvas.Dock = DockStyle.Fill;
            //profileDXFPanel.Controls.Add(Canvas);
        }



        #endregion

        #region [ EVENTS ]




        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]


        public void AsyncReadPLC()
        {
            try
            {
                var profileWidth = 0d;
                var profileHeight = 0d;

                string[] deviceArray = new string[] { Constants.ProfileInfo_Width, Constants.ProfileInfo_Height };
                var profileInfo = Plc.ReadDeviceList(deviceArray);
                profileWidth = profileInfo[0];
                profileHeight = profileInfo[1];

                //txtProfileWidth.TextBoxText = (profileWidth / 10).ToString();
                //txtProfileHeight.TextBoxText = (profileHeight / 10).ToString();

                ShowWidthHeight_Safe(profileWidth, profileHeight);


                short retVal = 0;
                //Plc.GetDevice2(Constants.ProfileInfo_Length, out retVal, false);
                Plc.GetDevice2(Constants.ProfileInfo_Length, out retVal);
                var readVal = Convert.ToDouble(retVal);
                //var outputs_Bit32 = Plc.ReadDevice(Constants.ProfileInfo_Length2, this.IsScrewingControl);
                var outputs_Bit32 = Plc.ReadDevice(Constants.ProfileInfo_Length2);//, KabanMachinesSystem.Common.Enums.PlcType.MainCPU);
                if (readVal < 0)
                    readVal = (Math.Pow(2, 16) + readVal);

                readVal += Convert.ToDouble(outputs_Bit32) * (Math.Pow(2, 16));
                var dividVal = (Convert.ToDouble(readVal) / 10000);

                ShowLenght_Safe(dividVal);
                //txtProfileLength.TextBoxText = dividVal.ToString("####.#");
            }
            catch (Exception exp)
            {
                CommonLog.WriteToFile("Thread.txt", $"Error ucProfileInfoPanel AsyncReadPLC(): {exp.Message}", true, true);
            }

        }

        private void ShowWidthHeight_Safe(double profileWidth, double profileHeight)
        {
            if (this.InvokeRequired)
            {
                Invoke(new System.Windows.Forms.MethodInvoker(delegate
                {
                    txtProfileWidth.TextBoxText = (profileWidth / 10).ToString();
                    txtProfileHeight.TextBoxText = (profileHeight / 10).ToString();
                }));
            }
            else
            {
                txtProfileWidth.TextBoxText = (profileWidth / 10).ToString();
                txtProfileHeight.TextBoxText = (profileHeight / 10).ToString();
            }
        }

        private void ShowLenght_Safe(double dividVal)
        {
            if (this.InvokeRequired)
            {
                Invoke(new System.Windows.Forms.MethodInvoker(delegate
                {
                    txtProfileLength.TextBoxText = dividVal.ToString("####.#");
                }));
            }
            else
            {
                txtProfileLength.TextBoxText = dividVal.ToString("####.#");
            }
        }

        #endregion
    }
}
