using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;

namespace _SB3015.Presentation.Home.Manuel
{
    [ToolboxItem(true)]
    public partial class ucHeadControlButtons : KabanUserControl
    {

        #region [ INSTANCES ]

        private static List<Control> KabanLambList;
        private static List<Control> KabanLedLabelList;
        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        public ucHeadControlButtons()
        {
            InitializeComponent();


            if (this.IsInDesignerMode())
                return;

            KabanLambList = new List<Control>();
            KabanLedLabelList = new List<Control>();
            InitAuth();

            Translator.ChangeLangue_ByUI(this);


        }

        private void InitAuth()
        {
            var roleName = KabanMachinesSystem.Common.Helpers.UserStatics.RoleName;
            if (roleName == "Developer" || roleName == "Service" || roleName == "Admin")
                btnSecurityByPass.Visible = true;
            else
                btnSecurityByPass.Visible = false;
        }

        public void AsyncReadPLC()
        {
            if (KabanLambList.Count == 0)
                KabanLambList = Get_KabanLambs();

            base.SyncLambs(KabanLambList);

            if (KabanLedLabelList.Count == 0)
                KabanLedLabelList = Get_KabanLedLabels();

            base.SyncLabels(KabanLedLabelList);

            kabanTrackBar1.TrackBarInit();
        }


        #endregion

        #region [ EVENTS ]




        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]



        #endregion

        private void btnSecurityByPass_Load(object sender, EventArgs e)
        {

        }
    }
}
