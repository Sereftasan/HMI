using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Facade;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace _MK2010.Presentation.Settings
{
    public partial class ucSettings : KabanUserControl
    {

        #region [ INSTANCES ]
        private static ucSettings _instance;
        List<Type> AssemblyList = new List<Type>();

        #endregion

        #region [ MEMBERS ]

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucSettings()
        {
            InitializeComponent();

            if (this.IsInDesignerMode())
                return;

            InitEvents();
            InitDefaults();

            Translator.ChangeLangue_ByUI(this);
        }
        public static ucSettings Instance()
        {
            if (_instance == null)
                _instance = new ucSettings();

            return _instance;
        }



        private void InitDefaults()
        {
            AssemblyList = AppDomainHelper.AssemblyList;
  
        }

        #endregion

        #region [ EVENTS ]

        private void InitEvents()
        {
            ktlcLeftMenu.RowCellClick += (s, e) =>
            {
                if (e.Node == null || e.Node.Tag == null)
                    return;

                if (AssemblyList == null || AssemblyList.Count == 0)
                    return;




                if (e.Node.Tag.ToString() == "ucLabelDesigner")
                {
                    base.ShowWaitForm();

                    this.kscUcSettings.SetLabelDesignerInstance();

                    base.CloseWaitForm();

                    return;
                }


                var dynamicType = AssemblyList.FirstOrDefault(i => i.Name == e.Node.Tag.ToString());
                if (dynamicType == null)
                    return;


                base.ShowWaitForm();

                var methodInfo = dynamicType.GetMethod("Instance");
                var dynamicUC = (UserControl)methodInfo.Invoke(null, null);

                if (!kscUcSettings.Panel2.Controls.Contains(dynamicUC))
                {
                    kscUcSettings.Panel2.Controls.Add(dynamicUC);
                    dynamicUC.Dock = DockStyle.Fill;
                    dynamicUC.BringToFront();
                }
                dynamicUC.BringToFront();

                base.CloseWaitForm();

            };
        }


        #endregion

        #region [ EVENT METHODS ]




        #endregion

        #region [ METHODS ]


        #endregion


    }
}
