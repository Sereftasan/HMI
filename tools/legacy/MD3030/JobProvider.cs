using KabanMachinesSystem.Common.Enums;
using KabanMachinesSystem.Business.BusinessLayer;
using Newtonsoft.Json;
using _MD3030.Presentation.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Configuration;

namespace _MD3030.Presentation.KabanSystem
{
    public class JobProvider
    {
        MDReceiptBusiness MDReceiptBusiness { get; set; }
        MDReceiptDetailBusiness MDReceiptDetailBusiness { get; set; }
        ProfileSeriesPProfileSubBusiness ProfileSeriesPProfileSubBusiness { get; set; }
        MeltingProfileSeryProfileSettingBusiness MeltingProfileSeryProfileSettingBusiness { get; set; }

        public JobProvider()
        {
            this.MDReceiptBusiness = new MDReceiptBusiness();
            this.MDReceiptDetailBusiness = new MDReceiptDetailBusiness();
            this.ProfileSeriesPProfileSubBusiness = new ProfileSeriesPProfileSubBusiness();
            this.MeltingProfileSeryProfileSettingBusiness = new MeltingProfileSeryProfileSettingBusiness();
        }

        public ProfileModelViewModel PrepareJob(Guid Id)
        {
            var receipt = MDReceiptBusiness.Get(Id);
            var receiptDetail = MDReceiptDetailBusiness.GetAll_ByParentGuid(receipt.Id).FirstOrDefault();

            var processInfo = MeltingProfileSeryProfileSettingBusiness.Get(receipt.ProfileId);
            //var processInfo = MeltingProfileSeryProfileSettingBusiness.Get(new Guid("56c346ac-3a59-4535-9035-6e32fc964db9"));
            if (processInfo == null)
                return new ProfileModelViewModel();

            var profilData = this.ProfileSeriesPProfileSubBusiness.GetProfileInfo_ById(receipt.ProfileId);

            var profileModel = new ProfileModelViewModel()
            {
                Id = profilData.Id,
                Code = profilData.Code,
                Height = processInfo.Height,
                MeasureWithWeldingTolerance = processInfo.MeasureWithWeldingTolerance,
                RearCutting = processInfo.RearCutting,
                MouldCuttingFirst = processInfo.MouldCuttingFirst,
                UProfile = processInfo.UProfile,
                SashProfile = processInfo.SashProfile,
                PressBeforeMelting = processInfo.PressBeforeMelting,
                MouldPressInWelding = processInfo.MouldPressInWelding,
                TopPressWithResistance = processInfo.TopPressWithResistance,
                BotPressWithResistance = processInfo.BotPressWithResistance,
                ResistancePressFixing = processInfo.ResistancePressFixing,
                MainCutRepeat = processInfo.MainCutRepeat,
                TopSurfaceCleaning = processInfo.TopSurfaceCleaning,
                BotSurfaceCleaning = processInfo.BotSurfaceCleaning,
                GasketCutActive = processInfo.GasketCutActive,
                GasketCutBeforeSurfaceCleaning = processInfo.GasketCutBeforeSurfaceCleaning,
                TopResistancePressTime = processInfo.TopResistancePressTime,
                BotResistancePressTime = processInfo.BotResistancePressTime,
                JawDistanceOffset = processInfo.JawDistanceOffset,
                MainCutOffset = processInfo.MainCutOffset,
                MinimumJawDistance = processInfo.MinimumJawDistance,
                ResistanceMeltingAmount = processInfo.ResistanceMeltingAmount,
                ToolCuttingAmount = processInfo.ToolCuttingAmount,
                MeltingDuration = processInfo.MeltingDuration,
                WeldingDuration = processInfo.WeldingDuration,
                TopResistancePressTorque = processInfo.TopResistancePressTorque,
                MeltingTorque = processInfo.MeltingTorque,
                WeldingTorque = processInfo.WeldingTorque,
                MainMouldPressTorque = processInfo.MainMouldPressTorque,
                ProfilePlacementOffset = processInfo.ProfilePlacementOffset,
                MainAxisTorqueLimit = processInfo.MainAxisTorqueLimit,
                Width = processInfo.Width,
                TempretureSet = processInfo.TempretureSet,
                HeadMillingCleaningSpeed = processInfo.HeadMillingCleaningSpeed,
                BodyLoadingPositionY = processInfo.BodyLoadingPositionY,
                SpringMoldTorque = processInfo.SpringMoldTorque,
                SpringMoldXOffset = processInfo.SpringMoldXOffset,
                SpringMoldZOffset = processInfo.SpringMoldZOffset,
                EncoderFixActive = processInfo.EncoderFixActive,
                MoldDifference = processInfo.MoldDifference,
                FrontClampActive = processInfo.FrontClampActive,
                PrintVertivalFirst = processInfo.PrintVertivalFirst,
                JawLowPressure = processInfo.JawLowPressure,
                FrontPressLowPressure = processInfo.FrontPressLowPressure,
                SetPrintEmptying = processInfo.SetPrintEmptying,
                OneByOneBackPrint = processInfo.OneByOneBackPrint,
                BottomPrintingTemperature = processInfo.BottomPrintingTemperature,
                CuttingSpeed = processInfo.CuttingSpeed
                //VerticalPressFirst=processInfo.VerticalPressFirst,
                //ReleaseJawCylinderFirst=processInfo.ReleaseJawCylinderFirst,
                //BackPressWithoutMould = processInfo.BackPressWithoutMould,
                /*MeltingTemperature = processInfo.MeltingTemperature,
                MeltingTime = processInfo.MeltingTime,
                GluingTime = processInfo.BondingTime,
                BottomPrintingTemperature = processInfo.BottomPrintTemperature,
                Overprint = processInfo.Overprint,
                OverprintTime = processInfo.PrintingTime,
                BackMold = processInfo.BackMold,
                RearDieTorque = processInfo.MoldTorque,
                BackMoldPrintXPosition = processInfo.BackPatternPrintingXPosition,
                BackBlade = processInfo.BackBlade,
                MeltingDistance = processInfo.MeltingDistance,
                JawOpeningDistance = processInfo.JawOpeningDistance,
                UpperStrippingZPosition = processInfo.TopStrippingZPosition,
                LowerStrippingZPosition = processInfo.BottomStrippingZPosition,
                MoldDifference = processInfo.MoldDifference,*/
            };

            if (receiptDetail != null && !string.IsNullOrWhiteSpace(receiptDetail.JsonValue))
            {
                // Normalize path
                var relative = receiptDetail.JsonValue
                    .Replace('/', Path.DirectorySeparatorChar)
                    .Replace('\\', Path.DirectorySeparatorChar);

                var path = ConfigurationManager.AppSettings["FileWritePath"] + relative;

                string directory = Path.GetDirectoryName(path);
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(path);
                string extension = Path.GetExtension(path);

                string path2 = Path.Combine(directory, $"{fileNameWithoutExt}-Fields{extension}");
                
                if (File.Exists(path2))
                {
                    string jsonContent = File.ReadAllText(path2);
                    
                    var fieldsObj = JsonConvert.DeserializeObject<ReceiptDepths>(jsonContent);

                    profileModel.receiptDepths = fieldsObj;
                }

                if (File.Exists(path))
                {
                    var jsonContent = File.ReadAllText(path);
                    var deserializedObject = JsonConvert.DeserializeObject<ReceiptRoot>(jsonContent);

                    if (deserializedObject != null && deserializedObject.Receipts != null)
                    {
                        // Build a concrete, non-null list of enum values
                        var receiptTypes = deserializedObject.Receipts
                            .Where(r => r != null)
                            .Select(r => r.ReceiptType) // enum
                            .Distinct()
                            //.OrderByDescending(t => t)  // order by enum value
                            .ToList();

                        // Iterate over the non-null list
                        foreach (var type in receiptTypes)
                        {
                            var receipts = new List<ReceiptViewModel>();

                            var itemsOfType = deserializedObject.Receipts
                                .Where(x => x != null && x.ReceiptType == type);

                            foreach (var item in itemsOfType)
                            {
                                if (type == ReceiptType.GasketCutting)
                                {
                                    // Varsayılan değerleri tanımlıyoruz
                                    double yValue = 0;
                                    double speed1Value = 1;
                                    double speed2Value = 1;

                                    // SelectedReceipt boş değilse ve içinde tire (-) varsa
                                    if (!string.IsNullOrEmpty(item.SelectedReceipt) && item.SelectedReceipt.Contains("-"))
                                    {
                                        string[] parts = item.SelectedReceipt.Split('-');

                                        // 0. parça Y'ye
                                        yValue = ToDouble(parts[0]);

                                        // 1. ve 2. parçalar varsa Speed1 ve Speed2'ye (Hata almamak için Length kontrolü ekledik)
                                        if (parts.Length > 1) speed1Value = ToDouble(parts[1]);
                                        if (parts.Length > 2) speed2Value = ToDouble(parts[2]);
                                    }
                                    else
                                    {
                                        // Tire yoksa tek parçadır, Y bu değeri alır, hızlar 1 kalır
                                        yValue = ToDouble(item.SelectedReceipt);
                                    }

                                    var viewModel = new ReceiptViewModel
                                    {
                                        EndCheck = false,
                                        ProcessCode = ToInt(item.ProcessCode),
                                        X = ToDouble(item.XPosition),
                                        Z = ToDouble(item.ZPosition),
                                        R = ToDouble(item.ProcessCode),
                                        Y1 = ToDouble(item.RY1),
                                        Y2 = ToDouble(item.Y2),
                                        Speed = ToDouble(item.Speed),

                                        // Yeni atadığımız ayrıştırılmış değişkenleri kullanıyoruz
                                        Y = yValue,
                                        Speed1 = speed1Value,
                                        Speed2 = speed2Value
                                    };

                                    receipts.Add(viewModel);
                                }
                                else if(type == ReceiptType.RearCutting)
                                {
                                    // Varsayılan değerleri tanımlıyoruz
                                    double yValue = 0;
                                    double speed1Value = 1;
                                    double speed2Value = 1;

                                    // SelectedReceipt boş değilse ve içinde tire (-) varsa
                                    if (!string.IsNullOrEmpty(item.SelectedReceipt) && item.SelectedReceipt.Contains("-"))
                                    {
                                        string[] parts = item.SelectedReceipt.Split('-');

                                        // 0. parça Y'ye
                                        yValue = ToDouble(parts[0]);

                                        // 1. ve 2. parçalar varsa Speed1 ve Speed2'ye (Hata almamak için Length kontrolü ekledik)
                                        if (parts.Length > 1) speed1Value = ToDouble(parts[1]);
                                        if (parts.Length > 2) speed2Value = ToDouble(parts[2]);
                                    }
                                    else
                                    {
                                        // Tire yoksa tek parçadır, Y bu değeri alır, hızlar 1 kalır
                                        yValue = ToDouble(item.SelectedReceipt);
                                    }

                                    var viewModel = new ReceiptViewModel
                                    {
                                        EndCheck = false,
                                        ProcessCode = ToInt(item.ProcessCode),
                                        X = ToDouble(item.XPosition),
                                        Z = ToDouble(item.ZPosition),
                                        R = ToDouble(item.ProcessCode),
                                        Y1 = ToDouble(item.RY1),
                                        Y2 = ToDouble(item.Y2),
                                        Speed = ToDouble(item.Speed),

                                        // Yeni atadığımız ayrıştırılmış değişkenleri kullanıyoruz
                                        Y = ToDouble(item.ProcessCode),
                                        Speed1 = speed1Value,
                                        Speed2 = speed2Value
                                    };

                                    receipts.Add(viewModel);
                                }
                                else
                                {
                                    var viewModel = new ReceiptViewModel
                                    {
                                        EndCheck = item.SelectedReceipt == "1" ? true : false,
                                        ProcessCode = ToInt(item.ProcessCode),
                                        X = ToDouble(item.XPosition),
                                        Y1 = ToDouble(item.RY1),
                                        R = ToDouble(item.RY1),
                                        Y2 = ToDouble(item.Y2),
                                        Speed = ToDouble(item.Speed),
                                        Z = ToDouble(item.ZPosition)
                                    };

                                    receipts.Add(viewModel);
                                }
                            }

                            // FIX: use enum directly (not string) if dictionary key is ReceiptType
                            profileModel.ReceiptViewModels[type] = receipts;
                        }

                    }
                }
                // else: file missing — handle/log as needed
            }

            return profileModel;
        }

        private static int ToInt(string s)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0;
        }

        private static double ToDouble(string s)
        {
            double v;
            return double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture, out v) ? v : 0d;
        }
    }
}
