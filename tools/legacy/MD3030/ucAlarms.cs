using KabanDevBase.Controls;
using KabanMachinesSystem.Business.BusinessLayer;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Helpers;
using KabanMachinesSystem.Common.Interfaces;
using KabanMachinesSystem.Data.Entities;
using KabanMachinesSystem.Statics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace _MD3030.Presentation.Alarms
{
    public partial class ucAlarms : KabanUserControl, IPlcComponent
    {

        #region [ INSTANCES ]
        private static ucAlarms _instance;

        #endregion

        #region [ MEMBERS ]

        AlarmAndWarningBusiness AlarmAndWarningBusiness { get; set; }
        AlarmBusiness AlarmBusiness { get; set; }

        List<Alarm> AlarmList = new List<Alarm>();
        List<Warning> WarningList = new List<Warning>();
        List<Control> KabanLambList = new List<Control>();
        List<TranslationAlarm> TranslationAlarmList = new List<TranslationAlarm>();

        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucAlarms()
        {
            InitializeComponent();

            AlarmAndWarningBusiness = new AlarmAndWarningBusiness();
            AlarmBusiness = new AlarmBusiness();
            //

            if (TranslationStatics.AlarmList.Count == 0)
            {
                var TranslationAlarmListTemp = AlarmBusiness.GetAll_ByMachineId(MachineInfosStatic.MachineInfo.Id);
                if (TranslationAlarmListTemp.Count == 0)
                    //TranslationAlarmListTemp = AlarmBusiness.GetAll_ByMachineId(Guid.Parse("c0aeff4b-efcd-4748-96fd-203b59864e8d"));

                TranslationAlarmList = TranslationAlarmListTemp;
            }



            if (AlarmList.Count == 0)
            {
                var AlarmListTemp = AlarmAndWarningBusiness.GetAll_AlarmsByMachineId(MachineInfosStatic.MachineId);
                if (AlarmListTemp.Count == 0)
                    AlarmListTemp = AlarmAndWarningBusiness.GetAll_AlarmsByMachineId(Guid.Parse("c0aeff4b-efcd-4748-96fd-203b59864e8d"));

                AlarmList = AlarmListTemp;
            }




            if (WarningList.Count == 0)
            {
                var WarningListTemp = AlarmAndWarningBusiness.GetAll_WarningsByMachineId(MachineInfosStatic.MachineId);
                if (WarningListTemp.Count == 0)
                    WarningListTemp = AlarmAndWarningBusiness.GetAll_WarningsByMachineId(Guid.Parse("c0aeff4b-efcd-4748-96fd-203b59864e8d"));

                WarningList = WarningListTemp;
            }

            Translator.ChangeLangue_ByUI(this);

        }
        public static ucAlarms Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucAlarms();
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

        public void AsyncReadPLC()
        {

            //if (InvokeRequired)
            //{
            //    Invoke(new System.Windows.Forms.MethodInvoker(delegate
            //    {
            //        ReadAll_AlarmsAndWarnings();

            //        if (KabanLambList.Count == 0)
            //            KabanLambList = Get_KabanLambs();

            //        base.SyncLambs(KabanLambList);

            //    }));
            //}
            //else
            //{
            //    ReadAll_AlarmsAndWarnings();

            //    if (KabanLambList.Count == 0)
            //        KabanLambList = Get_KabanLambs();

            //    base.SyncLambs(KabanLambList);
            //}


            ReadAll_AlarmsAndWarnings();

            if (KabanLambList.Count == 0)
                KabanLambList = Get_KabanLambs();

            base.SyncLambs(KabanLambList);


        }


        private void ReadAll_AlarmsAndWarnings()
        {
            if (AlarmList.Count == 0 && WarningList.Count == 0)
                return;


            //var hasAlarm = Plc.ReadDevice("600");
            //var hasWarning = Plc.ReadDevice("M800");


            int[] alarms = new int[13];
            int[] warnings = new int[13];

            var aRet = Plc.ReadDeviceBlock("M592", alarms.Length, out alarms[0]);
            bool[] alarmList = OptimizationUtility.IntArrayToBoolean(alarms).Skip(8).ToArray();

            var activeAlarmList = new List<Alarm>();
            activeAlarmList.Clear();
            for (int i = 0; i < AlarmList.Count; i++)
            {
                if (!alarmList[i])
                    continue;


                var alarmEntity = AlarmList[i];

                alarmEntity.Message = Get_AlarmAndWarningValue(alarmEntity);
                //
                activeAlarmList.Add(alarmEntity);
            }

            kabanGridControlAlarms.DataSource = activeAlarmList;


            var wRet = Plc.ReadDeviceBlock("M800", warnings.Length, out warnings[0]);//M103
            bool[] warningList = OptimizationUtility.IntArrayToBoolean(warnings);

            var activeWarningList = new List<Warning>();
            activeWarningList.Clear();
            for (int i = 1; i < WarningList.Count + 1; i++)
            {

                if (!warningList[i])
                    continue;

                var warningEntity = WarningList[i - 1];
                warningEntity.Message = Get_AlarmAndWarningValue(warningEntity);
                //
                activeWarningList.Add(warningEntity);
            }

            kabanGridControlWarnings.DataSource = activeWarningList;
        }

       

        private string Get_AlarmAndWarningValue(Alarm alarm)
        {
            

            var alarmEntity = TranslationAlarmList.Where(i => i.MachineId == MachineInfosStatic.MachineInfo.Id && i.TransKey == alarm.Key && i.LanguageId == TranslatorHelper.DefaultLanguageGuid).ToList();
            if (alarmEntity == null)
                return "Translate Error!";
            if (alarmEntity.Count == 0)
                return alarm.Message;
            return alarmEntity.First().TransValue;


        }

        private string Get_AlarmAndWarningValue(Warning warning)
        {
            var alarmEntity = TranslationAlarmList.Where(i => i.MachineId == MachineInfosStatic.MachineInfo.Id && i.TransKey == warning.Key && i.LanguageId == TranslatorHelper.DefaultLanguageGuid).ToList();
            if (alarmEntity == null)
                return "Translate Error!";
            if (alarmEntity.Count == 0)
                return warning.Message;
            return alarmEntity.First().TransValue;


        }

        #endregion
    }
}
