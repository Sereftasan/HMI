using KabanDevBase.Controls;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Parameters
{
    public partial class ucEntryParameters : KabanUserControl
    {

        #region [ INSTANCES ]
        private static ucEntryParameters _instance;
        private static List<Control> KabanInputList;

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucEntryParameters()
        {
            InitializeComponent();
            KabanInputList = new List<Control>();

            InitEvents();

            Translator.ChangeLangue_ByUI(this);
            ControlPagesByMachineInfosSetting();
        }

        public static ucEntryParameters Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucEntryParameters();
                }

                return _instance;
            }
            catch (System.Exception exp)
            {

                throw;
            }
        }


        private void InitEvents()
        {
            tsmRefreshPage.Click += (s, e) =>
            {

                base.ShowWaitForm();
                RefreshPLCSources();
                base.CloseWaitForm();
            };
        }


        private void ControlPagesByMachineInfosSetting()
        {
            try
            {

                var preTighteningMode_Controls = this.BoxWrapper.Controls.Cast<Control>().Where(q => q.Tag != null && q.Tag.ToString() == "PreTighteningMode").ToList();


                var preTighteningAdjustableMode = MachineInfosStatic.MachineInfo.PreTighteningAdjustableMode ?? false;
                var backMold = MachineInfosStatic.MachineInfo.BackMold ?? false;
                if (preTighteningAdjustableMode || backMold)
                {
                    DoVisiblePreTighteningAdjustableMode(preTighteningMode_Controls, false);
                    ChangeLocationByPretightening(true);
                }
                else
                {
                    DoVisiblePreTighteningAdjustableMode(preTighteningMode_Controls, true);
                    ChangeLocationByPretightening(false);
                }

            }
            catch (Exception exp)
            {

            }
        }

        private void ChangeLocationByPretightening(bool changeLocation)
        {
            //try
            //{
            //    var widthDiff = 37;
            //    if (changeLocation)
            //    {
                    
            //        lblClamping6.Location = new System.Drawing.Point(110, 116);
            //        kabanNumericButton17.Location = new System.Drawing.Point(302, 116);

            //        lblClamping7.Location = new System.Drawing.Point(110, 116 + widthDiff);
            //        kabanNumericButton18.Location = new System.Drawing.Point(302, 116 + widthDiff);
            //        lblEntryConveyorPositions.Size = new System.Drawing.Size(89, 106);

            //    }
            //    else
            //    {
            //        lblClamping6.Location = new System.Drawing.Point(110, 301);
            //        kabanNumericButton17.Location = new System.Drawing.Point(302, 301);

            //        lblClamping7.Location = new System.Drawing.Point(110, 301 + widthDiff);
            //        kabanNumericButton18.Location = new System.Drawing.Point(302, 301 + widthDiff);
            //        lblEntryConveyorPositions.Size = new System.Drawing.Size(89, 291);
            //    }
            //}
            //catch (Exception exp)
            //{

            //}
        }

        private void DoVisiblePreTighteningAdjustableMode(List<Control> preTighteningMode_Controls, bool visible)
        {
            foreach (var item in preTighteningMode_Controls)
            {
                item.Visible = visible;
            }
        }

        #endregion

        #region [ EVENTS ]




        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]
        public void RefreshPLCSources()
        {
            if (KabanInputList.Count == 0)
                KabanInputList = Get_KabanNumericButtons();

            base.SyncInputs(KabanInputList);
        }



        #endregion

        private void BoxWrapper_Paint(object sender, PaintEventArgs e)
        {

        }

        private void lblRobotTotalLenght_Click(object sender, EventArgs e)
        {

        }
    }
}
