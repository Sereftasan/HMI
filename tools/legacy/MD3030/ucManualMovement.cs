using KabanDevBase.Controls;
using KabanMachinesSystem.Statics;
using KabanMachinesSystem.Common.Interfaces;
using System.Collections.Generic;
using System.Windows.Forms;
using System;

namespace _MD3030.Presentation.Manuel
{
    public partial class ucManualMovement : KabanUserControl, IPlcComponent
    {
        
        #region [ INSTANCES ]
        private static ucManualMovement _instance;
        #endregion

        #region [ MEMBERS ]
        
        private static List<Control> KabanLedLabelList;
        private static List<Control> KabanInputList;
        private static List<Control> KabanProgressList;
        private static List<Control> KabanLambList;

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucManualMovement()
        {
            InitializeComponent();
            
            InitEvents();
            InitDefaults();

            Translator.ChangeLangue_ByUI(this);
        }

        private void InitEvents()
        {
            kabanButton1.Click += (s, e) => {
                if (KabanLambList.Count == 0)
                    KabanLambList = Get_KabanLambs();
                base.SyncLambs(KabanLambList);

            };
            tsmRefreshPage.Click += (s, e) =>
            {
                base.ShowWaitForm();
                RefreshPLCSources();
                base.CloseWaitForm();
            };
        }

        public static ucManualMovement Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucManualMovement();
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
            KabanLambList = new List<Control>();
            KabanLedLabelList = new List<Control>();
            KabanInputList = new List<Control>();
            KabanProgressList = new List<Control>();
        }

        #endregion

        #region [ EVENTS ]




        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]

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

        public void RefreshPLCSources()
        {
            if (KabanInputList.Count == 0)
                KabanInputList = Get_KabanNumericButtons();

            base.SyncInputs(KabanInputList);
        }


        #endregion
        #region [ CLİCKS ]
        private void kabanButton24_Click(object sender, System.EventArgs e)
        {

        }

        private void BoxWrapper_Paint(object sender, PaintEventArgs e)
        {

        }

        private void kabanButton33_Click(object sender, System.EventArgs e)
        {

        }

        private void lblSectionsAxisParameters_Click(object sender, EventArgs e)
        {

        }

        private void lblAxisParameters_Click(object sender, EventArgs e)
        {

        }

        private void lblRerefenceValueAxisParameters_Click(object sender, EventArgs e)
        {

        }

        private void lblStartingLimitAxisParameters_Click(object sender, EventArgs e)
        {

        }

        private void lblBackAxisParameters_Click(object sender, EventArgs e)
        {

        }

        private void lblAxisPositionAxisParameters_Click(object sender, EventArgs e)
        {

        }

        private void lblForwardAxisParameters_Click(object sender, EventArgs e)
        {

        }

        private void lblEndLimitAxisParameters_Click(object sender, EventArgs e)
        {

        }

        private void lblSendReferenceAxisParameters_Click(object sender, EventArgs e)
        {

        }

        private void lblGeneral_Click(object sender, EventArgs e)
        {

        }

        private void lblHead1_Click(object sender, EventArgs e)
        {

        }

        private void lblHead2_Click(object sender, EventArgs e)
        {

        }

        private void lblHead3_Click(object sender, EventArgs e)
        {

        }

        private void lblHead4_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton2_Click(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton3_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel1_Load(object sender, EventArgs e)
        {

        }

        private void kabanButton3_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton5_Click(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton4_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton5_Load(object sender, EventArgs e)
        {

        }

        private void lblGeneralX_Click(object sender, EventArgs e)
        {

        }

        private void lblGeneralY1_Click(object sender, EventArgs e)
        {

        }

        private void lblGeneralY2_Click(object sender, EventArgs e)
        {

        }

        private void lblHeadX_Click(object sender, EventArgs e)
        {

        }

        private void lblHeadZ1_Click(object sender, EventArgs e)
        {

        }

        private void lblHeadY2_Click(object sender, EventArgs e)
        {

        }

        private void lblHeadY1_Click(object sender, EventArgs e)
        {

        }

        private void lblHead2Z1_Click(object sender, EventArgs e)
        {

        }

        private void lblHead2Y2_Click(object sender, EventArgs e)
        {

        }

        private void lblHead2Y1_Click(object sender, EventArgs e)
        {

        }

        private void lblHead2X_Click(object sender, EventArgs e)
        {

        }

        private void lblHead3Y2_Click(object sender, EventArgs e)
        {

        }

        private void lblHead3Y1_Click(object sender, EventArgs e)
        {

        }

        private void lblHead3X_Click(object sender, EventArgs e)
        {

        }

        private void lblHead3Z1_Click(object sender, EventArgs e)
        {

        }

        private void lblHead4Z1_Click(object sender, EventArgs e)
        {

        }

        private void lblHead4Y2_Click(object sender, EventArgs e)
        {

        }

        private void lblHead4Y1_Click(object sender, EventArgs e)
        {

        }

        private void lblHead4X_Click(object sender, EventArgs e)
        {

        }

        private void lblMeasuredValue_Click(object sender, EventArgs e)
        {

        }

        private void lblCalibrate_Click(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton86_Load(object sender, EventArgs e)
        {

        }

        private void kabanButton93_Click(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton6_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton9_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton12_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton13_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton14_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton15_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton18_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton21_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton24_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton27_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton30_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton33_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton36_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton39_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton42_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton45_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton46_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton47_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton48_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton49_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton50_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton51_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton54_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton55_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton56_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton57_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton60_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton63_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton66_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton69_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton70_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton71_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton72_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton73_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton74_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton75_Load(object sender, EventArgs e)
        {

        }

        private void kabanButton13_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton14_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton15_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton46_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton47_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton48_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton49_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton50_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton51_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton55_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton56_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton57_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton70_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton71_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton72_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton73_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton74_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton75_Click(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton2_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton8_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton11_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton17_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton20_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton23_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton26_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton29_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton32_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton38_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton41_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton44_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton53_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton59_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton62_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton65_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton68_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton76_Load(object sender, EventArgs e)
        {

        }

        private void kabanButton4_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton8_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton11_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton17_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton20_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton23_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton26_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton29_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton32_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton35_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton38_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton41_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton44_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton53_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton59_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton62_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton65_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton68_Click(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel2_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel3_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel4_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel5_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel6_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel7_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel8_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel9_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel10_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel11_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel12_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel13_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel14_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel15_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel16_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel17_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel18_Load(object sender, EventArgs e)
        {

        }

        private void kabanLedLabel19_Load(object sender, EventArgs e)
        {

        }

        private void kabanButton6_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton9_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton12_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton18_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton21_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton24_Click_1(object sender, EventArgs e)
        {

        }

        private void kabanButton27_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton30_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton36_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton39_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton42_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton45_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton54_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton60_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton63_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton66_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton69_Click(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton1_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton7_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton10_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton16_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton19_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton22_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton25_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton28_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton31_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton34_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton35_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton37_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton40_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton43_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton52_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton58_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton61_Load(object sender, EventArgs e)
        {

        }

        private void kabanNumericButton64_Load(object sender, EventArgs e)
        {

        }

        private void kabanButton1_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton7_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton10_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton16_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton19_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton22_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton25_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton28_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton31_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton34_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton37_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton40_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton43_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton52_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton58_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton61_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton64_Click(object sender, EventArgs e)
        {

        }

        private void kabanButton67_Click(object sender, EventArgs e)
        {

        }

        private void cmsMain_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }

        private void tsmRefreshPage_Click(object sender, EventArgs e)
        {

        }
        #endregion

        private void kabanLambButton15_Load(object sender, EventArgs e)
        {

        }
    }
}
