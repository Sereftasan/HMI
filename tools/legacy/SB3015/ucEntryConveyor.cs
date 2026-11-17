using KabanDevBase.Controls;
using KabanDevBase.Plc_Componets;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Enums;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Common.ViewModels.PlcEntities;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Manuel
{
    public partial class ucEntryConveyor : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucEntryConveyor _instance;
        private static List<Control> KabanControls;
        private static List<Control> KabanLedLabelList;
        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucEntryConveyor()
        {
            InitializeComponent();
            KabanControls = new List<Control>();
            KabanLedLabelList = new List<Control>();

            Translator.ChangeLangue_ByUI(this);

            ControlPagesByMachineInfosSetting();

        }

        public static ucEntryConveyor Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucEntryConveyor();
                }

                return _instance;
            }
            catch (System.Exception)
            {

                throw;
            }
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

        private void DoVisiblePreTighteningAdjustableMode(List<Control> preTighteningMode_Controls, bool visible)
        {
            foreach (var item in preTighteningMode_Controls)
            {
                item.Visible = visible;
            }
        }


        private void ChangeLocationByPretightening(bool changeLocation)
        {
            //try
            //{
            //    var widthDiff = 37;
            //    if (changeLocation)
            //    {

            //        lblProfileClamp6.Location = new System.Drawing.Point(15, 263);
            //        kabanLambButton16.Location = new Point(207, 263);
            //        kabanLambButton15.Location = new Point(302, 263);
            //        label10.Location = new Point(398, 263);

            //        var newY = 263 + widthDiff;
            //        lblProfileClamp7.Location = new System.Drawing.Point(15, newY);
            //        kabanLambButton18.Location = new Point(207, newY);
            //        kabanLambButton17.Location = new Point(302, newY);
            //        kabanLedLabel2.Location = new Point(398 - 4, newY - 4);

            //        lblRollerUpDown.Location = new System.Drawing.Point(15, newY + widthDiff);
            //        kabanButton2.Location = new Point(207, newY + widthDiff);
            //        kabanLambButton2.Location = new Point(302, newY + widthDiff);
            //        label7.Location = new Point(398, newY + widthDiff);
            //        label13.Visible = false;

            //    }
            //    else
            //    {
            //        lblProfileClamp6.Location = new System.Drawing.Point(15, 448);
            //        kabanLambButton16.Location = new Point(207, 448);
            //        kabanLambButton15.Location = new Point(302, 448);
            //        label10.Location = new Point(398, 448);

            //        var newY = 448 + widthDiff;
            //        lblProfileClamp7.Location = new System.Drawing.Point(15, newY);
            //        kabanLambButton18.Location = new Point(207, newY);
            //        kabanLambButton17.Location = new Point(302, newY);
            //        kabanLedLabel2.Location = new Point(394, 481);

            //        lblRollerUpDown.Location = new System.Drawing.Point(15, newY + widthDiff);
            //        kabanButton2.Location = new Point(207, newY + widthDiff);
            //        kabanLambButton2.Location = new Point(302, newY + widthDiff);
            //        label7.Location = new Point(398, newY + widthDiff);
            //        label13.Visible = true;
            //    }
            //}
            //catch (Exception exp)
            //{

            //}
        }


        #endregion

        #region [ EVENTS ]




        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]

        public void AsyncReadPLC()
        {

            if (KabanControls.Count == 0)
                KabanControls = Get_KabanLambs();


            if (KabanLedLabelList.Count == 0)
                KabanLedLabelList = Get_KabanLedLabels();


            base.SyncLambs(KabanControls);
            base.SyncLabels(KabanLedLabelList);
                

            // Burayi kapattik constructore tasindik bir sefer acilinca okusun. Performans icin. 20210915
            //Init_RollersUpDown();
        }



        public void RefreshDataSources()
        {

        }


        #endregion

    }
}
