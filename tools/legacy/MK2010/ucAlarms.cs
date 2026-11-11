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

namespace _MK2010.Presentation.Alarms
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
                {

                    //TranslationAlarmListTemp = AlarmBusiness.GetAll_ByMachineId(Guid.Parse("c0aeff4b-efcd-4748-96fd-203b59864e8d"));
                }

                TranslationAlarmList = TranslationAlarmListTemp;
            }



            if (AlarmList.Count == 0)
            {
                var AlarmListTemp = AlarmAndWarningBusiness.GetAll_AlarmsByMachineId(MachineInfosStatic.MachineId);
                //if (AlarmListTemp.Count == 0)
                    //AlarmListTemp = AlarmAndWarningBusiness.GetAll_AlarmsByMachineId(Guid.Parse("c0aeff4b-efcd-4748-96fd-203b59864e8d"));

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


            var hasAlarm = Plc.ReadDevice("M33");
            //var hasWarning = Plc.ReadDevice("M1100");
            if (hasAlarm <= 0)
            {
                kabanGridControlWarnings.DataSource = null;
                kabanGridControlAlarms.DataSource = null;
                return;
            }


            int[] alarms = new int[3];
            //int[] warnings = new int[7];


            // Onemli not 2024-07-30
            // Bitlerin baslangici 16 ya bolunmeli. M600 den basliyorsan hata aliyoruz conku 16 bolunmuyor.
            // Bloklar 16 bit (2 Byte) 592, 16 ye bolunuyor
            var aRet = Plc.ReadDeviceBlock("M592", alarms.Length, out alarms[0]);
            //ool[] alarmList = OptimizationUtility.IntArrayToBoolean(alarms);
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
