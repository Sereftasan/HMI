using _MD3030.Presentation.Models;
using KabanMachinesSystem.Common.Enums;
using System;
using System.Collections.Generic;

namespace _MD3030.Presentation.KabanSystem
{
    public class ProfileModelViewModel
    {
        public Guid? Id { get; set; }

        public string ProfileBrandId { get; set; }
        public Guid? ProfileSerieId { get; set; }
        public Guid? ProfileGroupId { get; set; }
        public Guid? ProfileTypeId { get; set; }
        public Guid? ProfileSubTypeId { get; set; }
        public long Code { get; set; }
        public string Name { get; set; }
        public string Comment { get; set; }
        public string File { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }


        //bits

        public bool? MeasureWithWeldingTolerance { get; set; }
        public bool? RearCutting { get; set; }
        public bool? MouldCuttingFirst { get; set; }
        public bool? UProfile { get; set; }
        public bool? SashProfile { get; set; }
        public bool? PressBeforeMelting { get; set; }
        public bool? MouldPressInWelding { get; set; }
        public bool? TopPressWithResistance { get; set; }
        public bool? BotPressWithResistance { get; set; }
        public bool? ResistancePressFixing { get; set; }
        public bool? MainCutRepeat { get; set; }
        public bool? TopSurfaceCleaning { get; set; }
        public bool? BotSurfaceCleaning { get; set; }
        public bool? GasketCutActive { get; set; }
        public bool? GasketCutBeforeSurfaceCleaning { get; set; }
        public bool? EncoderFixActive { get; set; }
        public bool? FrontClampActive { get; set; }
        public bool? PrintVertivalFirst { get; set; }
        public bool? JawLowPressure { get; set; }
        public bool? FrontPressLowPressure { get; set; }
        public bool? SetPrintEmptying { get; set; }
        public bool? OneByOneBackPrint { get; set; }
        public bool? VerticalPressFirst { get; set; }
        public bool?  ReleaseJawCylinderFirst { get; set; }
        public bool? BackPressWithoutMould { get; set; }

        //double

        public double? TopResistancePressTime { get; set; }
        public double? BotResistancePressTime { get; set; }
        public double? JawDistanceOffset { get; set; }
        public double? MainCutOffset { get; set; }
        public double? MinimumJawDistance { get; set; }
        public double? ResistanceMeltingAmount { get; set; }
        public double? ToolCuttingAmount { get; set; }
        public double? MeltingDuration { get; set; }
        public double? WeldingDuration { get; set; }
        public double? TopResistancePressTorque { get; set; }
        public double? MeltingTorque { get; set; }
        public double? WeldingTorque { get; set; }
        public double? MainMouldPressTorque { get; set; }
        public double? MainAxisTorqueLimit { get; set; }
        public double? ProfilePlacementOffset { get; set; }
        public double? TempretureSet { get; set; }
        public double? HeadMillingCleaningSpeed { get; set; }
        public double? BodyLoadingPositionY { get; set; }
        public double? BottomPrintingTemperature { get; set; }
        public double? CuttingSpeed { get; set; }


        public double? SpringMoldTorque { get; set; }
        public double? SpringMoldXOffset { get; set; }
        public double? SpringMoldZOffset { get; set; }
        public double? MoldDifference { get; set; }


        public double? EarWidth { get; set; }
        public string FileId { get; set; }
        public string FilePath { get; set; }
        public  ReceiptDepths receiptDepths { get; set; }
        public Dictionary<ReceiptType, List<ReceiptViewModel>> ReceiptViewModels { get; set; } = new Dictionary<ReceiptType, List<ReceiptViewModel>>();
    }
}
