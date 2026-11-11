using System;
using System.Collections.Generic;
using System.Linq;
using KabanDevBase.Controls;
using KabanDevBase.Extensions;
using KabanMachinesSystem.Business.BusinessLayer;
using KabanMachinesSystem.Business.Helpers;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Business.Statics;
using KabanMachinesSystem.Common.Enums;
using KabanMachinesSystem.Data.Entities;
using KabanMachinesSystem.Presentation.Settings.ExternalReceiptMapping;
using KabanMachinesSystem.Statics;

namespace _MD3030.Presentation.Settings.MachineSettings
{
    public partial class ucMachineSettings : KabanUserControl
    {

        #region [ INSTANCES ]
        private static ucMachineSettings _instance;
        private MachineInfoBusiness MachineInfoBusiness;
        private LabelHeaderBusiness LabelHeaderBusiness;
        private ExternalFileBusiness ExternalFileBusiness { get; set; }
        private MDBarcodeBusiness _MDBarcodeBusinesss;
        #endregion

        #region [ MEMBERS ]
        // Combobox event'larının sonsuz döngüye girmesini engellemek için flag
        private bool _isUpdatingCombos = false;
        #endregion

        #region [ CONSTRUCTOR AND DEFAULTS ]
        protected ucMachineSettings()
        {
            InitializeComponent();

            MachineInfoBusiness = new MachineInfoBusiness();
            LabelHeaderBusiness = new LabelHeaderBusiness();
            this.ExternalFileBusiness = new ExternalFileBusiness();
            this._MDBarcodeBusinesss = new MDBarcodeBusiness();

            InitDefaults();
            InitEvents();



            Translator.ChangeLangue_ByUI(this);
        }

        public static ucMachineSettings Instance()
        {
            if (_instance == null)
            {
                _instance = new ucMachineSettings();
            }
            else
                _instance.RefreshDataSources();

            return _instance;
        }

        private void InitDefaults()
        {
            txtBarcodeCount.TextBoxText = "0";

            FillFields();
            FillLabelHeaders();
            FillBarcodeSettings();
        }

        #endregion

        #region [ EVENTS ]

        private void InitEvents()
        {
            btnSave.Click += (s, e) =>
            {
                SaveAllSettings();
            };

            ktsPrinterActive.OnEditValueChanged += (s, e) =>
            {
                kleActiveLabel.Visible = ktsPrinterActive.Value;
                kleLeftOver.Visible = ktsPrinterActive.Value;
                ktsLeftOverActive.Visible = ktsPrinterActive.Value;
                txtBarcodeCount.Visible = ktsPrinterActive.Value;
            };

            ktsLeftOverActive.OnEditValueChanged += (s, e) =>
            {
                kleLeftOver.Visible = ktsPrinterActive.Value;
            };

            // Barcode Combobox seçim değişikliklerini yakala (Çakışma kontrolü)
            kleBarcodeField1.Properties.EditValueChanged += (s, e) => HandleBarcodeComboBoxChange(kleBarcodeField1);
            kleBarcodeField2.Properties.EditValueChanged += (s, e) => HandleBarcodeComboBoxChange(kleBarcodeField2);
            kleBarcodeField3.Properties.EditValueChanged += (s, e) => HandleBarcodeComboBoxChange(kleBarcodeField3);
        }

        #endregion

        #region [ METHODS ]

        public void RefreshDataSources()
        {

        }

        private void SaveAllSettings()
        {
            try
            {
                var machineInfos = MachineInfoBusiness.Get(MachineInfosStatic.MachineId);

                machineInfos.UsePrinter = ktsPrinterActive.Value;

                //KabanMachinesSystem.Common.Statics.MachineSettings.UsingInchSystem = ktsUsingInchSystem.Value;

                machineInfos.BarcodeCount = !string.IsNullOrEmpty(txtBarcodeCount.TextBoxText) ? Convert.ToInt32(txtBarcodeCount.TextBoxText) : 0;
                machineInfos.LeftOverActive = ktsLeftOverActive.Value;

                MachineInfoBusiness.Update(machineInfos);

                if (kleActiveLabel.Visible)
                {
                    var activeLabel = (LabelHeader)kleActiveLabel.SelectedDataRow;
                    if (activeLabel == null)
                        return;

                    activeLabel.IsActiveLabel = true;
                    activeLabel.IsLeftOverLabel = false;

                    LabelHeaderBusiness.SetActiveLabel(activeLabel.Id);
                }

                if (kleLeftOver.Visible)
                {
                    var activeLabel = (LabelHeader)kleLeftOver.SelectedDataRow;
                    if (activeLabel != null)
                    {
                        LabelHeaderBusiness.SetActiveLeftOverLabel(activeLabel.Id);
                    }
                }

                SetSettingsPLC();

                // BARCODE AYARLARINI KAYDET
                SaveBarcodeSettings();

                base.ShowToastNotify(MessageHelper.GetMessage(MessageType.TransactionSucceed));

                var restartQuestionResult = KabanMessageBox.ShowMessage(MessageHelper.GetMessage(MessageType.WaitingRestart), KabanDevBase.Enums.MessageBoxType.Question);
                if (restartQuestionResult == System.Windows.Forms.DialogResult.Yes)
                    ApplicationStatics.RestartSystem();
            }
            catch (System.Exception exp)
            {
                base.ShowToastNotify(MessageHelper.GetMessage(MessageType.TransactionError));
                throw exp;
            }
        }

        private void SetSettingsPLC()
        {
        }

        private void FillFields()
        {
            var machineInfo = MachineInfoBusiness.Get(MachineInfosStatic.MachineId);
            if (machineInfo == null)
                return;

            if (machineInfo.ScrewingMachineTypeId != null)
            {
            }

            ktsPrinterActive.Value = machineInfo.UsePrinter == null ? false : (bool)machineInfo.UsePrinter;
            ktsLeftOverActive.Value = machineInfo.LeftOverActive == null ? false : (bool)machineInfo.LeftOverActive;
        }

        private void FillBarcodeSettings()
        {
            // 1. Enum'dan Combobox'ları doldur
            var enumNames = Enum.GetNames(typeof(BarcodeFieldType));

            kleBarcodeField1.Properties.Items.Clear();
            kleBarcodeField2.Properties.Items.Clear();
            kleBarcodeField3.Properties.Items.Clear();

            kleBarcodeField1.Properties.Items.AddRange(enumNames);
            kleBarcodeField2.Properties.Items.AddRange(enumNames);
            kleBarcodeField3.Properties.Items.AddRange(enumNames);

            // 2. Default olarak hepsini null ve 0 yap
            _isUpdatingCombos = true; // DB yüklenirken çakışma event'ı tetiklenmesin

            kleBarcodeField1.SelectedItem = null;
            kneBarcodeField1Lenght.TextBoxText = "0";
            kneBarcodeField1StartIndex.TextBoxText = "0"; // YENİ

            kleBarcodeField2.SelectedItem = null;
            kneBarcodeField2Lenght.TextBoxText = "0";
            kneBarcodeField2StartIndex.TextBoxText = "0"; // YENİ

            kleBarcodeField3.SelectedItem = null;
            kneBarcodeField3Lenght.TextBoxText = "0";
            kneBarcodeField3StartIndex.TextBoxText = "0"; // YENİ

            // 3. Veritabanından mevcut kayıtları çek
            List<_MDBarcode> mDBarcodes = _MDBarcodeBusinesss.Get_All();

            // 4. Index numaralarına göre ilgili alanları set et
            foreach (var mdbarcode in mDBarcodes)
            {
                switch (mdbarcode.FieldIndex)
                {
                    case 0:
                        kleBarcodeField1.SelectedItem = mdbarcode.FieldName;
                        kneBarcodeField1Lenght.TextBoxText = mdbarcode.FieldLenght.ToString();
                        kneBarcodeField1StartIndex.TextBoxText = mdbarcode.FieldStartIndex.ToString(); // YENİ
                        break;
                    case 1:
                        kleBarcodeField2.SelectedItem = mdbarcode.FieldName;
                        kneBarcodeField2Lenght.TextBoxText = mdbarcode.FieldLenght.ToString();
                        kneBarcodeField2StartIndex.TextBoxText = mdbarcode.FieldStartIndex.ToString(); // YENİ
                        break;
                    case 2:
                        kleBarcodeField3.SelectedItem = mdbarcode.FieldName;
                        kneBarcodeField3Lenght.TextBoxText = mdbarcode.FieldLenght.ToString();
                        kneBarcodeField3StartIndex.TextBoxText = mdbarcode.FieldStartIndex.ToString(); // YENİ
                        break;
                }
            }

            _isUpdatingCombos = false;
        }

        // --- BARCODE ÖZEL METOTLARI ---

        private void HandleBarcodeComboBoxChange(KabanComboBoxControl changedCombo)
        {
            if (_isUpdatingCombos) return;
            if (changedCombo.SelectedItem == null) return;

            _isUpdatingCombos = true;

            var allCombos = new[] { kleBarcodeField1, kleBarcodeField2, kleBarcodeField3 };
            var allEnumNames = Enum.GetNames(typeof(BarcodeFieldType)).ToList();

            string selectedValue = changedCombo.SelectedItem.ToString();

            // Eğer bu seçilen değer, başka bir combobox'ta zaten varsa
            foreach (var otherCombo in allCombos)
            {
                if (otherCombo != changedCombo && otherCombo.SelectedItem != null && otherCombo.SelectedItem.ToString() == selectedValue)
                {
                    // Şu anda diğer combobox'ların kullandığı değerlerin listesini çıkar
                    var usedValues = allCombos
                        .Where(c => c.SelectedItem != null && c != otherCombo)
                        .Select(c => c.SelectedItem.ToString())
                        .ToList();

                    // Kullanımda OLMAYAN ilk enum değerini bul
                    var unusedValue = allEnumNames.FirstOrDefault(n => !usedValues.Contains(n));

                    // Çakışan diğer combobox'a boşta kalan değeri ata
                    if (unusedValue != null)
                    {
                        otherCombo.SelectedItem = unusedValue;
                    }
                    else
                    {
                        otherCombo.SelectedItem = null; // Beklenmedik bir durum oluşursa boşa çıkar
                    }
                }
            }

            _isUpdatingCombos = false;
        }

        private void SaveBarcodeSettings()
        {
            // Önce tablodaki tüm barcode kurallarını siliyoruz
            _MDBarcodeBusinesss.DeleteAll_BySql("DELETE FROM _mdbarcodes");

            var newBarcodes = new List<_MDBarcode>();

            // 0. İndex (Field 1)
            if (kleBarcodeField1.SelectedItem != null)
            {
                newBarcodes.Add(new _MDBarcode
                {
                    Id = Guid.NewGuid(),
                    FieldIndex = 0,
                    FieldName = kleBarcodeField1.SelectedItem.ToString(),
                    FieldCode = (int)Enum.Parse(typeof(BarcodeFieldType), kleBarcodeField1.SelectedItem.ToString()),
                    FieldLenght = string.IsNullOrEmpty(kneBarcodeField1Lenght.TextBoxText) ? 0 : Convert.ToInt32(kneBarcodeField1Lenght.TextBoxText),
                    FieldStartIndex = string.IsNullOrEmpty(kneBarcodeField1StartIndex.TextBoxText) ? 0 : Convert.ToInt32(kneBarcodeField1StartIndex.TextBoxText) // YENİ
                });
            }

            // 1. İndex (Field 2)
            if (kleBarcodeField2.SelectedItem != null)
            {
                newBarcodes.Add(new _MDBarcode
                {
                    Id = Guid.NewGuid(),
                    FieldIndex = 1,
                    FieldName = kleBarcodeField2.SelectedItem.ToString(),
                    FieldCode = (int)Enum.Parse(typeof(BarcodeFieldType), kleBarcodeField2.SelectedItem.ToString()),
                    FieldLenght = string.IsNullOrEmpty(kneBarcodeField2Lenght.TextBoxText) ? 0 : Convert.ToInt32(kneBarcodeField2Lenght.TextBoxText),
                    FieldStartIndex = string.IsNullOrEmpty(kneBarcodeField2StartIndex.TextBoxText) ? 0 : Convert.ToInt32(kneBarcodeField2StartIndex.TextBoxText) // YENİ
                });
            }

            // 2. İndex (Field 3)
            if (kleBarcodeField3.SelectedItem != null)
            {
                newBarcodes.Add(new _MDBarcode
                {
                    Id = Guid.NewGuid(),
                    FieldIndex = 2,
                    FieldName = kleBarcodeField3.SelectedItem.ToString(),
                    FieldCode = (int)Enum.Parse(typeof(BarcodeFieldType), kleBarcodeField3.SelectedItem.ToString()),
                    FieldLenght = string.IsNullOrEmpty(kneBarcodeField3Lenght.TextBoxText) ? 0 : Convert.ToInt32(kneBarcodeField3Lenght.TextBoxText),
                    FieldStartIndex = string.IsNullOrEmpty(kneBarcodeField3StartIndex.TextBoxText) ? 0 : Convert.ToInt32(kneBarcodeField3StartIndex.TextBoxText) // YENİ
                });
            }

            // Oluşturulan listeyi Bulk Insert ile tek seferde db'ye yaz
            if (newBarcodes.Any())
            {
                _MDBarcodeBusinesss.BulkInsert(newBarcodes);
            }
        }

        // --- DİĞER STANDART METOTLAR ---

        private void FillLabelHeaders()
        {
            FillWasteLabelHeaders(kleActiveLabel);
            FillWasteLabelHeaders(kleLeftOver, true);
        }

        private void FillWasteLabelHeaders(KabanLookUpEditControl kabanLookUpEditControl, bool isLeftOver = false)
        {
            var labelHeaders = LabelHeaderBusiness.Get_All();
            LabelHeader activeLabel = null;
            if (!isLeftOver)
                activeLabel = LabelHeaderBusiness.Get_ActiveLabel();
            else
                activeLabel = LabelHeaderBusiness.Get_LeftOverLabel();

            kabanLookUpEditControl.DataSource = labelHeaders;
            kabanLookUpEditControl.Properties.DisplayMember = "Name";
            kabanLookUpEditControl.Properties.ValueMember = "Id";

            if (kabanLookUpEditControl != null && activeLabel != null)
                kabanLookUpEditControl.EditValue = activeLabel.Id;

            kabanLookUpEditControl.CustomizeColumn();
        }

        private void FillMD3030Labels()
        {
            kleMachineTypeController.DataSource = new DevExpress.XtraEditors.Controls.ComboBoxItemCollection(kleMachineTypeController.Properties);
            kleMachineTypeController.DataSource.Add("MD3020 Q Motion");
            kleMachineTypeController.DataSource.Add("MD3020 FX");
            kleMachineTypeController.DataSource.Add("MD3030 FX");
        }

        #endregion

        private void lblName_Click(object sender, EventArgs e)
        {
        }

        private void lblSafetySensor_Click(object sender, EventArgs e)
        {
        }

        private void BoxWrapper_Paint(object sender, System.Windows.Forms.PaintEventArgs e)
        {
        }
    }
}