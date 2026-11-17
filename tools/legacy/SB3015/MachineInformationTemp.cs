using KabanMachinesSystem.Business.BusinessLayer;
using KabanMachinesSystem.Data;
using KabanMachinesSystem.Data.Entities;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;

namespace _SB3015.Temps
{
    public static class MachineInformationTemp
    {

        public static MachineInfoBusiness MachineInfoBusiness = new MachineInfoBusiness();
        public static MachineCategoryBusiness MachineCategoryBusiness = new MachineCategoryBusiness();
        public static MachineGroupBusiness MachineGroupBusiness = new MachineGroupBusiness();
        public static MachineGroupCategoryBusiness MachineGroupCategoryBusiness = new MachineGroupCategoryBusiness();

        public static ToolInfoBusiness ToolInfoBusiness = new ToolInfoBusiness();
        public static MachineToolInfoBusiness MachineToolInfoBusiness = new MachineToolInfoBusiness();
        public static MachineToolsSettingBusiness MachineToolsSettingBusiness = new MachineToolsSettingBusiness();
        public static MachineToolAbsoluteAngleBusiness MachineToolAbsoluteAngleBusiness = new MachineToolAbsoluteAngleBusiness();
        public static ToolsDependantPacketBusiness ToolsDependantPacketBusiness = new ToolsDependantPacketBusiness();
        public static ChartAxisSettingBusiness MachineChartAxisSettingBusiness = new ChartAxisSettingBusiness();

        public static MachineToolsSetting MachineToolsSettingTemp = new MachineToolsSetting
        {
            AngleFrom = 0,
            AngleTo = 180,
            DefaultAngle = 0,
            OffsetValue = 0
        };


        public static MachineInfo MachineInfoTemp = new MachineInfo
        {


        };


        public static void CreateMachineInfo()
        {
            var machineCategory = MachineCategoryBusiness.Get_All().FirstOrDefault(q => q.Name == "Process Centers");
            var machineGroup = MachineGroupBusiness.Get_All().FirstOrDefault(q => q.Name == "PVC");
            var machineGroupCategories = MachineGroupCategoryBusiness.Get_All();

            var toolInfos = ToolInfoBusiness.Get_All();

            using (var _dbContext = new NpgsqlConnection(ConnectionHelper.Connection))
            {
                if (_dbContext.State == ConnectionState.Closed)
                    _dbContext.Open();

                var transaction = _dbContext.BeginTransaction();

                var old_MachineInfo = MachineInfoBusiness.GetByName("SB3015", _dbContext, transaction);
                if (old_MachineInfo != null)
                    return;


                var machineGroupCategory = machineGroupCategories.FirstOrDefault(q => q.CategoryId == machineCategory.Id && q.GroupId == machineGroup.Id);
                if (machineGroupCategory == null)
                {
                    machineGroupCategory = new MachineGroupsCategory();
                    machineGroupCategory.GroupId = machineGroup.Id;
                    machineGroupCategory.CategoryId = machineCategory.Id;

                    MachineGroupCategoryBusiness.Add(machineGroupCategory, _dbContext, transaction);
                }

                var machineInfo = new MachineInfo();
                machineInfo.CategoryGroupId = machineGroupCategory.Id;
                machineInfo.Name = "SB3015";
                machineInfo.Version = 1;
                machineInfo.Model = "SB3015";
                machineInfo.IsCurrentMachine = true;
                machineInfo.IsVCutting = false;
                machineInfo.UseManuelReceipts = true;

                MachineInfoBusiness.Add(machineInfo, _dbContext, transaction);



                var pointRotate1 = new PointF(26, -238);
                var pointRotate2 = new PointF(483, -238);
                var pointRotate3 = new PointF(-170, -238);
                var pointRotate4 = new PointF(-627, -238);

                var defaultAngle1 = 270;
                var defaultAngle2 = 270;
                var defaultAngle3 = 90;
                var defaultAngle4 = 90;

                var centerRotate1 = new PointF(255, -238);
                var centerRotate2 = new PointF(-398, -238);
                var package_DXFPath = @"SB 3015 Freze.dxf";


                var package = new ToolsDependantPacket();
                package.Name = $"Packet1";
                package.DependencyTypeId = new Guid("8942a686-594a-4924-8c28-ee393d7103d5");
                package.YLimit_From = 200;
                package.YLimit_To = 400;
                package.ZLimit_To = 300;
                package.ZLimit_From = 600;
                //
                package.DefaultAngle = defaultAngle1;
                package.RotatedPackage_Center_X = centerRotate1.X;
                package.RotatedPackage_Center_Y = centerRotate1.Y;
                package.StartPoint_Y = pointRotate1.X;
                package.StartPoint_Z = pointRotate1.Y;
                package.ToolCounts = 2;
                package.ReferenceTool = "Tool1";
                package.ReferenceIndex = 0;
                package.Package_DXFPath = package_DXFPath;
                //
                ToolsDependantPacketBusiness.Add(package, _dbContext, transaction);
                //

                var package2 = new ToolsDependantPacket();
                package2.Name = $"Packet2";
                package2.YLimit_From = 150;
                package2.YLimit_To = 350;
                package2.ZLimit_To = 250;
                package2.ZLimit_From = 550;
                package2.DependencyTypeId = new Guid("8942a686-594a-4924-8c28-ee393d7103d5");
                //
                //
                package2.DefaultAngle = defaultAngle3;
                package2.RotatedPackage_Center_X = centerRotate2.X;
                package2.RotatedPackage_Center_Y = centerRotate2.Y;
                package2.StartPoint_Y = pointRotate3.X;
                package2.StartPoint_Z = pointRotate3.Y;
                package2.ToolCounts = 2;
                package2.ReferenceTool = "Tool3";
                package2.ReferenceIndex = 1;
                package2.Package_DXFPath = package_DXFPath;
                //
                ToolsDependantPacketBusiness.Add(package2, _dbContext, transaction);










                //var absoluteAngles = new List<int>() { 0, 45 };
                var angleRotateState = new Guid("9d77c74c-b7fd-4b79-8ed6-2b05dfee9d1a");
                package_DXFPath = null;
                CreateMachineToolSetting(_dbContext, transaction, machineInfo.Id, toolInfos[0].Id, package.Id,
                    0, 270, 90, 10, defaultAngle1, pointRotate1.X, pointRotate1.Y,
                    centerRotate1.X, centerRotate1.Y, false, false, package_DXFPath, angleRotateState, null, true, false);


                CreateMachineToolSetting(_dbContext, transaction, machineInfo.Id, toolInfos[1].Id, package.Id,
                    0, 270, 90, 10, defaultAngle2, pointRotate2.X, pointRotate2.Y,
                    centerRotate1.X, centerRotate1.Y, false, false, package_DXFPath, angleRotateState, null, true, false);


                var angelStateRangeId = new Guid("9d77c74c-b7fd-4b79-8ed6-2b05dfee9d1a");
                CreateMachineToolSetting(_dbContext, transaction, machineInfo.Id, toolInfos[2].Id, package2.Id,
                    0, 270, 180, 5, defaultAngle3, pointRotate3.X, pointRotate3.Y,
                    centerRotate2.X, centerRotate2.Y, false, false, package_DXFPath, angleRotateState, null, true, true);


                CreateMachineToolSetting(_dbContext, transaction, machineInfo.Id, toolInfos[3].Id, package2.Id,
                    0, 270, 0, 10, defaultAngle4, pointRotate4.X, pointRotate4.Y,
                    centerRotate2.X, centerRotate2.Y, false, false, package_DXFPath, angleRotateState, null, true, false);



                ChartAxisSettings(_dbContext, transaction, machineInfo.Id);

                transaction.Commit();
            }
        }

        private static void ChartAxisSettings(NpgsqlConnection _dbContext, NpgsqlTransaction transaction, Guid machineInfoId)
        {
            var chartAxisSetings = new ChartAxisSetting();
            chartAxisSetings.MachineId = machineInfoId;
            chartAxisSetings.AxisX_Min = 0;
            chartAxisSetings.AxisX_Max = 475;
            chartAxisSetings.AxisY_Min = 0;
            chartAxisSetings.AxisY_Max = 475;

            MachineChartAxisSettingBusiness.Add(chartAxisSetings, _dbContext, transaction);

        }

        private static void CreateMachineToolSetting(NpgsqlConnection _dbContext, NpgsqlTransaction transaction, Guid machineInfoId, Guid toolInfoId, Guid packetId,
                    double angleFrom, double angleTo, double chartRotateAngle, double cuttingSpeed, double defaultAngle, double startPointY, double startPointZ,
                    double? centerRotateY, double? centerRotateZ, bool isFix, bool hasAbsoluteRotate, string dxfDath, Guid? angleStateId = null, List<int> absoluteAngles = null,
                    bool isLandscape = false, bool calcByMaxValue = false)
        {
            var machineToolInfo = new MachineToolInfo();
            machineToolInfo.MachineId = machineInfoId;
            machineToolInfo.ToolId = toolInfoId;
            machineToolInfo.PacketId = packetId;

            MachineToolInfoBusiness.Add(machineToolInfo, _dbContext, transaction);

            var machineToolsSetting = new MachineToolsSetting();
            machineToolsSetting.Id = machineToolInfo.Id;

            machineToolsSetting.AngleFrom = angleFrom;
            machineToolsSetting.AngleTo = angleTo;
            machineToolsSetting.ChartRotateAngle = chartRotateAngle;

            machineToolsSetting.DefaultAngle = defaultAngle;
            machineToolsSetting.DefaultSpeed = 8;


            machineToolsSetting.OffsetValue = 0;
            machineToolsSetting.StartPoint_Y = startPointY;
            machineToolsSetting.StartPoint_Z = startPointZ;

            machineToolsSetting.YLimit_From = -200;
            machineToolsSetting.YLimit_To = 200;
            machineToolsSetting.ZLimit_From = -200;
            machineToolsSetting.ZLimit_To = 200;
            machineToolsSetting.DXFPath = dxfDath;
            machineToolsSetting.AngleStateId = angleStateId;
            machineToolsSetting.IsLandscape = isLandscape;
            machineToolsSetting.CalcByMaxValue = calcByMaxValue;

            if (centerRotateY != null)
                machineToolsSetting.RotatedTool_Center_X = (double)centerRotateY;

            if (centerRotateZ != null)
                machineToolsSetting.RotatedTool_Center_Y = (double)centerRotateZ;

            machineToolsSetting.IsFix = isFix;
            machineToolsSetting.HasAbsoluteRotate = hasAbsoluteRotate;

            MachineToolsSettingBusiness.Add(machineToolsSetting, _dbContext, transaction);


            if (absoluteAngles == null)
                return;

            foreach (var item in absoluteAngles)
            {
                var machineToolAbsoluteAngle = new MachineToolAbsoluteAngle();
                machineToolAbsoluteAngle.Angle = item;
                machineToolAbsoluteAngle.MachineToolId = machineToolsSetting.Id;

                MachineToolAbsoluteAngleBusiness.Add(machineToolAbsoluteAngle, _dbContext, transaction);
            }

        }
    }
}
