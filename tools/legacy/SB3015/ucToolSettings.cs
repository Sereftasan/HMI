using DevExpress.XtraEditors;
using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanDevBase.Forms;
using KabanMachinesSystem.Business.BusinessLayer;
using KabanMachinesSystem.Business.Helpers;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Enums;
using KabanMachinesSystem.Common.Extensions;
using KabanMachinesSystem.Data.Entities;
using KabanMachinesSystem.Presentation.Settings.DrillBitSettings;
using KabanMachinesSystem.Statics;
using SB3015.Jsons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;

namespace SB3015.Presentation.Settings.ToolSettings
{
    public partial class ucToolSettings : KabanUserControl
    {

        #region [ INSTANCES ]
        private static ucToolSettings _instance;
        MachineToolsSettingBusiness MachineToolsSettingBusiness { get; set; }
        MachineToolInfoBusiness MachineToolInfoBusiness { get; set; }
        ToolInfoBusiness ToolInfoBusiness { get; set; }
        ToolsDependantPacketBusiness ToolsDependantPacketBusiness { get; set; }
        DrillBitBusiness DrillBitBusiness { get; set; }
        MachineToolsDrillBitBusiness MachineToolsDrillBitBusiness { get; set; }
        #endregion

        #region [ MEMBERS ]
        Dictionary<MachineToolInfo, MachineToolsSetting> machineToolsDic = new Dictionary<MachineToolInfo, MachineToolsSetting>();
        Dictionary<string, ToolsDependantPacket> machineToolGroupDic = new Dictionary<string, ToolsDependantPacket>();
        List<Control> KabanLookUpEditControlList { get; set; }
        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucToolSettings()
        {
            InitializeComponent();

            if (this.IsInDesignerMode())
                return;


            MachineToolsSettingBusiness = new MachineToolsSettingBusiness();
            MachineToolInfoBusiness = new MachineToolInfoBusiness();
            ToolInfoBusiness = new ToolInfoBusiness();
            ToolsDependantPacketBusiness = new ToolsDependantPacketBusiness();
            DrillBitBusiness = new DrillBitBusiness();
            MachineToolsDrillBitBusiness = new MachineToolsDrillBitBusiness();

            InitDefaults();
            InitEvents();

            Translator.ChangeLangue_ByUI(this);

        }



        public static ucToolSettings Instance()
        {
            try
            {
                if (_instance == null)
                {
                    _instance = new ucToolSettings();
                }
                else
                {
                    _instance.LoadToolSettings_PLC();
                    _instance.FillCombos();

                }
                return _instance;
            }
            catch (System.Exception)
            {

                throw;
            }
        }

        private void InitDefaults()
        {
            KabanLookUpEditControlList = GetAll(this, typeof(KabanLookUpEditControl)).ToList();
            LoadToolSettings_PLC();
            LoadToolSettings();
            FillCombos();
        }




        #endregion

        #region [ EVENTS ]

        private void InitEvents()
        {
            btnSave.Click += (s, e) =>
            {
                Save();
                MachineSettingsHelper.GetMachineToolInfo(MachineInfosStatic.MachineName, MachineInfosStatic.MachineId);
            };

            btnRefresh.Click += (s, e) =>
            {
                LoadToolSettings_PLC();
                FillCombos();

            };

            btnDrillBits.Click += (s, e) =>
            {
                ShowDrillBitSettings();
            };

            ksbCalibrate.Click += (s, e) =>
            {
                Calibrate_and_SaveMachineParameters();
            };
        }


        #endregion

        #region [ EVENT METHODS ]

        private void Save()
        {
            try
            {
                var KabanTextBoxList = GetAll(this, typeof(KabanTexboxControl)).ToList();
                //var KabanLookUpEditControlList = GetAll(this, typeof(KabanLookUpEditControl)).ToList();


                foreach (var item in machineToolsDic)
                {
                    var toolName = item.Key.ToolInfo.Name.ToLower();

                    var props = item.Value.GetType().GetProperties();
                    foreach (var prop in props)
                    {
                        var propName = $"{toolName}_{prop.Name}";

                        var controlResult = KabanTextBoxList.Where(i => i.Name == propName).FirstOrDefault();
                        if (controlResult == null)
                            continue;



                        if (controlResult is KabanTexboxControl)
                        {
                            var val = ((KabanTexboxControl)controlResult).TextBoxText;
                            if (val == "")
                                continue;

                            item.Value.SetPropValue(prop.Name, Convert.ToDouble(val));
                        }

                    }
                    MachineToolsSettingBusiness.Update(item.Value);


                    MachineToolsDrillBitBusiness.Delete_ByParent(item.Key.Id);

                    var lue = KabanLookUpEditControlList.FirstOrDefault(i => i.Name.Contains(item.Key.ToolInfo.Name));
                    var selectedRow = ((KabanLookUpEditControl)lue).SelectedDataRow;
                    if (selectedRow != null)
                    {
                        MachineToolsDrillBitBusiness.Add(
                            new MachineToolsDrillBit
                            {
                                Id = Guid.NewGuid(),
                                MachineToolInfoId = item.Key.Id,
                                DrillBitId = ((DrillBit)(selectedRow)).Id
                            });
                    }



                }




                base.ShowToastNotify(MessageHelper.GetMessage(MessageType.TransactionSucceed));


            }
            catch (Exception)
            {

                base.ShowToastNotify(MessageHelper.GetMessage(MessageType.TransactionError));

            }

        }


        private void ShowDrillBitSettings()
        {
            var uc = ucDrillBitSettings.Instance();
            var frmHost = new KabanHostForm(uc, KabanMachinesSystem.Common.Enums.PopupFormLevels.FirstLevel);
            frmHost.FormText = uc.PageTitle;
            frmHost.ShowDialog();
        }



        #endregion

        #region [ METHODS ]





        private void LoadToolSettings()
        {

            LoadMachineParameters();
            var TextEditList = GetAll(this, typeof(TextEdit)).ToList();
            var KabanTextBoxList = GetAll(this, typeof(KabanTexboxControl));
            //
            TextEditList.AddRange(KabanTextBoxList);


            var machineToolInfos = MachineToolInfoBusiness.GetAll_ByParentGuid(MachineInfosStatic.MachineId);
            if (machineToolInfos.Count == 0)
                return;



            machineToolInfos.ForEach(mti =>
            {
                var toolsDependantPacket = ToolsDependantPacketBusiness.Get((Guid)mti.PacketId);
                if (toolsDependantPacket != null)
                    mti.ToolsDependantPacket = toolsDependantPacket;

                mti.ToolInfo = ToolInfoBusiness.Get(mti.ToolId);
                machineToolsDic.Add(mti, MachineToolsSettingBusiness.Get(mti.Id));

                if (!machineToolGroupDic.ContainsKey(toolsDependantPacket.Name))
                    machineToolGroupDic.Add(toolsDependantPacket.Name, toolsDependantPacket);

            });


            foreach (var item in machineToolsDic)
            {
                var toolName = item.Key.ToolInfo.Name.ToLower();
                var toolControls = TextEditList.Where(i => i.Name.StartsWith(toolName)).ToList();

                var sql = $"select * from machinetoolsdrillbits m2 where \"MachineToolInfoId\" = '{item.Key.Id}'  ";
                var drillBitEntity = MachineToolsDrillBitBusiness.Get(sql);
                if (drillBitEntity != null)
                {
                    var lue = (KabanLookUpEditControl)KabanLookUpEditControlList.FirstOrDefault(i => i.Name.Contains(item.Key.ToolInfo.Name));
                    lue.EditValue = drillBitEntity.DrillBitId;
                }




                foreach (var cntrl in toolControls)
                {

                    var fieldName = cntrl.Name.Remove(0, 6);

                    if (item.Value.HasProperty(fieldName) == "")
                        continue;

                    var itemVal = item.Value.GetPropValue(fieldName).ToString();

                    if (cntrl is KabanTexboxControl)
                        ((KabanTexboxControl)cntrl).TextBoxText = itemVal;
                    else
                    {
                        cntrl.Text = itemVal;
                        ((TextEdit)cntrl).Enabled = false;
                    }

                }

            }


            foreach (var item in machineToolGroupDic)
            {

                var groupName = item.Key.ToLower();
                var toolControls = TextEditList.Where(i => i.Name.StartsWith(groupName)).ToList();
                foreach (var cntrl in toolControls)
                {
                    var fieldName = cntrl.Name.Remove(0, 8);

                    if (item.Value.HasProperty(fieldName) == "")
                        continue;

                    var itemVal = item.Value.GetPropValue(fieldName).ToString();

                    if (cntrl is KabanTexboxControl)
                        ((KabanTexboxControl)cntrl).TextBoxText = itemVal;
                    else
                    {
                        cntrl.Text = itemVal;
                        ((TextEdit)cntrl).Enabled = false;
                    }

                }

            }

        }

        public IEnumerable<Control> GetAll(Control control, Type type)
        {
            var controls = control.Controls.Cast<Control>();

            return controls.SelectMany(ctrl => GetAll(ctrl, type))
                                      .Concat(controls)
                                      .Where(c => c.GetType() == type && c.Tag == null);
        }


        private void LoadToolSettings_PLC()
        {
            try
            {
                base.ShowWaitForm();

                var controls = BoxWrapper.Controls.Cast<Control>();
                var textEdits = controls.Where(c => c.GetType().Name == "TextEdit" && c.Tag != null).ToList();

                textEdits.ForEach(txt =>
                {

                    var result = Plc.ReadDeviceBlock(txt.Tag.ToString(), 2);
                    var value = result / 10000;

                    txt.Text = value.ToString();
                });

                base.CloseWaitForm();

            }
            catch (Exception)
            {
                base.CloseWaitForm();

            }
        }



        private void RefreshDataSources()
        {

        }

        private void FillCombos()
        {
            var drillBits = DrillBitBusiness.Get_All().ToList();
            lueDrillBit_Tool2.DataSource = drillBits;
            lueDrillBit_Tool1.DataSource = drillBits;
            //
            EditLookUpEdit(lueDrillBit_Tool2);
            EditLookUpEdit(lueDrillBit_Tool1);
        }

        private void EditLookUpEdit(KabanLookUpEditControl lue)
        {
            lue.DisplayMember = "Diameter";
            lue.ValueMember = "Id";
            //
            lue.Properties.PopulateColumns();

            lue.Properties.Columns.ToList().ForEach(i =>
            {
                if (i.FieldName.Contains("Id") || i.FieldName == "MachineToolsDrillBits" || i.FieldName == "CuttingAmount" || i.FieldName == "GCodePrograms")
                    i.Visible = false;
            });


        }


        public void LoadMachineParameters()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "machine_parameters.json");

                if (!File.Exists(path))
                {
                    return;
                }

                // Read file and deserialize
                string json = File.ReadAllText(path);
                var parameters = JsonSerializer.Deserialize<SBMachineParameters>(json);

                if (parameters == null)
                {
                    MessageBox.Show("Failed to read parameters from file.",
                                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Fill input boxes
                kneWidthoftheFlatBarA.TextBoxText = parameters.FlatBarWidthA.ToString();
                kneHeightoftheFlatBarB.TextBoxText = parameters.FlatBarHeightB.ToString();
                kneZAxisValueat0Degrees.TextBoxText = parameters.ZAxisValueAt0Degrees.ToString();
                kneZAxisValueat90Degrees.TextBoxText = parameters.ZAxisValueAt90Degrees.ToString();
                kneZAxisValueat180Degrees.TextBoxText = parameters.ZAxisValueAt180Degrees.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error reading parameters:\n{ex.Message}",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void Calibrate_and_SaveMachineParameters()
        {
            // Try parsing inputs
            double.TryParse(kneWidthoftheFlatBarA.TextBoxText, out double a);
            double.TryParse(kneHeightoftheFlatBarB.TextBoxText, out double b);
            double.TryParse(kneZAxisValueat0Degrees.TextBoxText, out double z0);
            double.TryParse(kneZAxisValueat90Degrees.TextBoxText, out double z90);
            double.TryParse(kneZAxisValueat180Degrees.TextBoxText, out double z180);

            if (a == 0.0 || b == 0.0 || z0 == 0.0 || z90 == 0.0 || z180 == 0)
            {
                KabanMessageBox.ShowMessage(MessageHelper.GetMessage(MessageType.ValueMustGreaterThanZero), KabanDevBase.Enums.MessageBoxType.Information);
                return;
            }

            // Create object
            var parameters = new SBMachineParameters
            {
                FlatBarWidthA = a,
                FlatBarHeightB = b,
                ZAxisValueAt0Degrees = z0,
                ZAxisValueAt90Degrees = z90,
                ZAxisValueAt180Degrees = z180
            };

            JsonCreator.SaveParametersAsJson(parameters);

            double r = (b + z0 + z180) / 2.0;
            double y = (z90 - r + a);
            double z = -1 * (z180 - r + b);

            short short_r = (short)(Math.Round(r, 1) * 10.0);
            short short_y = (short)(Math.Round(y, 1) * 10.0);
            short short_z = (short)(Math.Round(z, 1) * 10.0);


            double simple_r = Convert.ToDouble(short_r) / 10.0;
            double simple_y = Convert.ToDouble(short_y) / 10.0;
            double simple_z = Convert.ToDouble(short_z) / 10.0;


            short[] numbers = new short[] { short_y, short_z, short_r };


            int iRet = Plc.MainCpuPrimary.WriteDeviceBlock2("D360", numbers.Length, ref numbers[0]);
            if (iRet == 0)
            {
                string str_R = string.Format("{0:0.0}", simple_r);
                string str_Y = string.Format("{0:0.0}", simple_y);
                string str_Z = string.Format("{0:0.0}", simple_z);

                kneMillingUnitCenterProfileYReferenceDistanceY.TextBoxText = str_Y;
                kneMillingUnitCenterProfileZReferenceDistanceZ.TextBoxText = str_Z;
                kneMillingZCenterDistanceR.TextBoxText = str_R;
            }
        }

        #endregion
    }
}
