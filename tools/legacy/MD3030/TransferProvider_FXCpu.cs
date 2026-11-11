using KabanDevBase.Extensions;
using KabanMachinesSystem.Business.Helpers;
using KabanMachinesSystem.Business.PlcObjects;
using KabanMachinesSystem.Common.Enums;
using KabanMachinesSystem.Common.Helpers;
using System;
using System.Collections.Generic;

namespace _MD3030.Presentation.KabanSystem
{
    public class TransferProvider_FXCpu
    {

        public bool WriteProfileInfo(ProfileModelViewModel profileModelViewModel)
        {
            //bool //isWriteOperationAccurate = true;
            try
            {
                if (profileModelViewModel == null)
                    return false;

                short high = 0;
                short low = 0;
                //short[] numbers;
                bool result;

                if (profileModelViewModel.receiptDepths != null)
                {
                    //Boy Kesim: D2094 32 * 10000
                    //Yardımcı Kesim: D2092 32 *10000
                    //Ana Kesim : D7042 * 10000

                    double MouldCuttingDepth;
                    if (double.TryParse(profileModelViewModel.receiptDepths.MouldCuttingDepth, out MouldCuttingDepth))
                    {
                        PlcUtility.Int2Short((int)(MouldCuttingDepth * 10000), out high, out low);
                        //numbers = new short[] { low, high };

                        result = Plc.SetDevice("D2094", low);
                        Plc.SetDevice("D2095", high);
                    }

                    double AuxCuttingDepth;
                    if (double.TryParse(profileModelViewModel.receiptDepths.AuxCuttingDepth, out AuxCuttingDepth))
                    {
                        PlcUtility.Int2Short((int)(AuxCuttingDepth * 10000), out high, out low);
                        //numbers = new short[] { low, high };

                        result = Plc.SetDevice("D2092", low);
                        Plc.SetDevice("D2093", high);
                    }

                    double MainCuttingDepth;
                    if (double.TryParse(profileModelViewModel.receiptDepths.FinalCuttingDepth, out MainCuttingDepth))
                    {
                        PlcUtility.Int2Short((int)(MainCuttingDepth * 10000), out high, out low);
                        //numbers = new short[] { low, high };
                        result = Plc.SetDevice("D7042", low);
                        Plc.SetDevice("D7043", high);
                    }


                }


                // bit 

                int selectedModByte = 0;
                if (profileModelViewModel.MouldCuttingFirst != null && (bool)profileModelViewModel.MouldCuttingFirst)
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L12", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L12", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);

                selectedModByte = 0;
                if (profileModelViewModel.SashProfile != null && (bool)profileModelViewModel.SashProfile)
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L14", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L14", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);

                selectedModByte = 0;
                if (profileModelViewModel.PressBeforeMelting != null && (bool)profileModelViewModel.PressBeforeMelting)
                {
                    selectedModByte = 1;
                }
                //Plc.SetDevice("L19", selectedModByte);
                ////isWriteOperationAccurate = ControlWriteOperation("L13", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);
                //
                selectedModByte = 0;
                if (profileModelViewModel.MouldPressInWelding != null && (bool)profileModelViewModel.MouldPressInWelding) //Yaylı kalıp olmalı adı
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L20", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L20", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);

                selectedModByte = 0;
                if (profileModelViewModel.TopPressWithResistance != null && (bool)profileModelViewModel.TopPressWithResistance) //Üst Baskı
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L22", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L22", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);

                selectedModByte = 0;
                if (profileModelViewModel.BotPressWithResistance != null && (bool)profileModelViewModel.BotPressWithResistance) //Alt Baskı
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L24", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L24", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);

                selectedModByte = 0;
                if (profileModelViewModel.ResistancePressFixing != null && (bool)profileModelViewModel.ResistancePressFixing) //Düzeltme
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L28", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L28", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);

                selectedModByte = 0;
                if (profileModelViewModel.MainCutRepeat != null && (bool)profileModelViewModel.MainCutRepeat) //Ana kesim tekrarla
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L29", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L29", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);

                selectedModByte = 0;
                if (profileModelViewModel.TopSurfaceCleaning != null && (bool)profileModelViewModel.TopSurfaceCleaning)
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L50", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L50", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);

                selectedModByte = 0;
                if (profileModelViewModel.BotSurfaceCleaning != null && (bool)profileModelViewModel.BotSurfaceCleaning)
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L51", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L51", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);

                selectedModByte = 0;
                if (profileModelViewModel.EncoderFixActive != null && (bool)profileModelViewModel.EncoderFixActive)
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L61", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L61", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);


                selectedModByte = 0;
                if (profileModelViewModel.GasketCutActive != null && (bool)profileModelViewModel.GasketCutActive)
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L60", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L60", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);


                selectedModByte = 0;
                if (profileModelViewModel.FrontClampActive != null && (bool)profileModelViewModel.FrontClampActive) //Ana kesim tekrarla
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L40", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L40", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);

                selectedModByte = 0;
                if (profileModelViewModel.PrintVertivalFirst != null && (bool)profileModelViewModel.PrintVertivalFirst) //Ana kesim tekrarla
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L37", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L37", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);

                selectedModByte = 0;

                if (profileModelViewModel.JawLowPressure != null && (bool)profileModelViewModel.JawLowPressure) //Ana kesim tekrarla
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L38", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L38", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);

                selectedModByte = 0;

                if (profileModelViewModel.FrontPressLowPressure != null && (bool)profileModelViewModel.FrontPressLowPressure) //Ana kesim tekrarla
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L39", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L39", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);

                selectedModByte = 0;

                if (profileModelViewModel.SetPrintEmptying != null && (bool)profileModelViewModel.SetPrintEmptying) //Ana kesim tekrarla
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L35", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L35", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);

                selectedModByte = 0;

                if (profileModelViewModel.OneByOneBackPrint != null && (bool)profileModelViewModel.OneByOneBackPrint) //Ana kesim tekrarla
                {
                    selectedModByte = 1;
                }
                Plc.SetDevice("L36", selectedModByte);
                //isWriteOperationAccurate = ControlWriteOperation("L36", //isWriteOperationAccurate, Convert.ToBoolean(selectedModByte), null);

                selectedModByte = 0;
                if (profileModelViewModel.GasketCutBeforeSurfaceCleaning != null && (bool)profileModelViewModel.GasketCutBeforeSurfaceCleaning)
                {
                    selectedModByte = 1;
                    Plc.SetDevice("L54", 1);
                    //isWriteOperationAccurate = ControlWriteOperation("L54", //isWriteOperationAccurate, true, null);

                    Plc.SetDevice("L58", 0);
                    //isWriteOperationAccurate = ControlWriteOperation("L58", //isWriteOperationAccurate, false, null);

                }
                else
                {
                    Plc.SetDevice("L54", 0);
                    //isWriteOperationAccurate = ControlWriteOperation("L54", //isWriteOperationAccurate, false, null);

                    Plc.SetDevice("L58", 1);
                    //isWriteOperationAccurate = ControlWriteOperation("L58", //isWriteOperationAccurate, true, null);

                }


                //Double
                if (profileModelViewModel.TopResistancePressTime != null) //ÜstBaskıSüresi
                {
                    Plc.SetDevice("D7036", (int)(profileModelViewModel.TopResistancePressTime * 10));
                    //int extremeint = (int)(profileModelViewModel.TopResistancePressTime * 10);
                    //isWriteOperationAccurate = ControlWriteOperation("D7036", //isWriteOperationAccurate, null, extremeint );
                }


                if (profileModelViewModel.BotResistancePressTime != null) //Alt Baskı Süresi
                {
                    Plc.SetDevice("D7038", (int)(profileModelViewModel.BotResistancePressTime * 10));
                    //int extremeint = (int)(profileModelViewModel.BotResistancePressTime);
                    //isWriteOperationAccurate = ControlWriteOperation("D7038", //isWriteOperationAccurate, null, extremeint, 10);

                }




                if (profileModelViewModel.JawDistanceOffset != null) //Çene Açıklık Offseti
                {
                    PlcUtility.Int2Short((int)(profileModelViewModel.JawDistanceOffset * 10000), out high, out low);
                    //numbers = new short[] { low, high };
                    result = Plc.SetDevice("D7040", low);
                    Plc.SetDevice("D7041", high);

                    //int extremeint = (int)(profileModelViewModel.JawDistanceOffset * 10000);
                    //isWriteOperationAccurate = ControlWriteOperation("D7040", //isWriteOperationAccurate, null, extremeint, 1);
                }


                if (profileModelViewModel.MainCutOffset != null) //Ana Kesim Offseti
                {
                    PlcUtility.Int2Short((int)(profileModelViewModel.MainCutOffset * 10000), out high, out low);
                    //numbers = new short[] { low, high };
                    result = Plc.SetDevice("D7042", low);
                    //Plc.SetDevice("D7043", high);

                    ////int extremeint = (int)(profileModelViewModel.MainCutOffset * 10000);
                    ////isWriteOperationAccurate = ControlWriteOperation("D7042", //isWriteOperationAccurate, null, extremeint);
                }


                if (profileModelViewModel.MinimumJawDistance != null) //Minimum Çene Açıklığı
                {
                    PlcUtility.Int2Short((int)(profileModelViewModel.MinimumJawDistance * 10000), out high, out low);
                    //numbers = new short[] { low, high };
                    result = Plc.SetDevice("D7056", low);
                    //Plc.SetDevice("D7057", high);


                    ////int extremeint = (int)(profileModelViewModel.MinimumJawDistance * 10000);
                    ////isWriteOperationAccurate = ControlWriteOperation("D7056", //isWriteOperationAccurate, null, extremeint, 1);
                }


                if (profileModelViewModel.ResistanceMeltingAmount != null) //Eritme Miktarı
                {
                    PlcUtility.Int2Short((int)(profileModelViewModel.ResistanceMeltingAmount * 10000), out high, out low);
                    //numbers = new short[] { low, high };
                    result = Plc.SetDevice("D7058", low);
                    //Plc.SetDevice("D7059", high);

                    ////int extremeint = (int)(profileModelViewModel.ResistanceMeltingAmount * 10000);
                    ////isWriteOperationAccurate = ControlWriteOperation("D7040", //isWriteOperationAccurate, null, extremeint, 1);
                }

                if (profileModelViewModel.ToolCuttingAmount != null) //Freze Kesme Miktarı
                {
                    PlcUtility.Int2Short((int)(profileModelViewModel.ToolCuttingAmount * 10000), out high, out low);
                    //numbers = new short[] { low, high };
                    result = Plc.SetDevice("D7060", low);
                    //Plc.SetDevice("D7061", high);

                    // //int extremeint = (int)(profileModelViewModel.ToolCuttingAmount * 10000);
                    // //isWriteOperationAccurate = ControlWriteOperation("D7060", //isWriteOperationAccurate, null, extremeint, 1);
                }

                if (profileModelViewModel.MeltingDuration != null)
                {
                    Plc.SetDevice("D2408", (int)(profileModelViewModel.MeltingDuration * 10));

                    //int extremeint = (int)(profileModelViewModel.MeltingDuration * 10);
                    //isWriteOperationAccurate = ControlWriteOperation("D2408", //isWriteOperationAccurate, null, extremeint, 1, 16);

                }

                if (profileModelViewModel.WeldingDuration != null)
                {
                    Plc.SetDevice("D2414", (int)(profileModelViewModel.WeldingDuration * 10));

                    //int extremeint = (int)(profileModelViewModel.WeldingDuration * 10);
                    //isWriteOperationAccurate = ControlWriteOperation("D2414", //isWriteOperationAccurate, null, extremeint, 1, 16);

                }


                //TopResistanceTime

                if (profileModelViewModel.TopResistancePressTorque != null) //Üst Baskı Torku
                {
                    Plc.SetDevice("D2588", (int)(profileModelViewModel.TopResistancePressTorque));

                    //int extremeint = (int)(profileModelViewModel.TopResistancePressTorque);
                    //isWriteOperationAccurate = ControlWriteOperation("D2588", //isWriteOperationAccurate, null, extremeint, 1, 16);

                }

                if (profileModelViewModel.MeltingTorque != null)
                {
                    //Plc.SetDevice("D2586", (int)(profileModelViewModel.MeltingTorque * 1));

                    // //int extremeint = (int)(profileModelViewModel.MeltingTorque);
                    ////isWriteOperationAccurate = ControlWriteOperation("D2586", //isWriteOperationAccurate, null, extremeint, 1, 16);

                }

                if (profileModelViewModel.WeldingTorque != null) //Eklenmeli
                {
                    //Plc.SetDevice("D2587", (int)(profileModelViewModel.WeldingTorque));
                }

                if (profileModelViewModel.Height != null)
                {
                    PlcUtility.Int2Short((int)(profileModelViewModel.Height * 10000), out high, out low); // Boy
                                                                                                          //numbers = new short[] { low, high };
                                                                                                          //result = Plc.SetDevice("D2740", low);
                                                                                                          //Plc.SetDevice("D2741", high);

                    int[] liste = { (int)(profileModelViewModel.Height * 10000) };

                    Plc.WriteDeviceList_32Bit("D2740", liste);



                    //int extremeint = (int)(profileModelViewModel.Height * 10000);
                    //isWriteOperationAccurate = ControlWriteOperation("D2740", //isWriteOperationAccurate, null, extremeint, 1);

                }



                if (profileModelViewModel.TempretureSet != null) //Sıcaklık
                {
                    Plc.SetDevice("D349", (int)(profileModelViewModel.TempretureSet * 10));
                    //int extremeint = (int)(profileModelViewModel.TempretureSet * 10);
                    //isWriteOperationAccurate = ControlWriteOperation("D349", //isWriteOperationAccurate, null, extremeint, 1,16);

                }

                if (profileModelViewModel.Width != null) //Genişlik
                {
                    PlcUtility.Int2Short((int)(profileModelViewModel.Width * 10), out high, out low);
                    //numbers = new short[] { low, high };

                    result = Plc.SetDevice("D346", low);
                    Plc.SetDevice("D347", high);

                    //int extremeint = (int)(profileModelViewModel.Width * 10);
                    //isWriteOperationAccurate = ControlWriteOperation("D346", //isWriteOperationAccurate, null, extremeint, 1, 32);

                }

                if (profileModelViewModel.HeadMillingCleaningSpeed != null)
                {
                    PlcUtility.Int2Short((int)(profileModelViewModel.HeadMillingCleaningSpeed * 100), out high, out low);
                    //numbers = new short[] { low, high };

                    result = Plc.SetDevice("D2422", low);
                    Plc.SetDevice("D2423", high);

                    //int extremeint = (int)(profileModelViewModel.HeadMillingCleaningSpeed * 100);
                    //isWriteOperationAccurate = ControlWriteOperation("D2422", //isWriteOperationAccurate, null, extremeint, 1, 32);

                }

                if (profileModelViewModel.BodyLoadingPositionY != null)
                {
                    PlcUtility.Int2Short((int)(profileModelViewModel.BodyLoadingPositionY * 10000), out high, out low);
                    //numbers = new short[] { low, high };

                    result = Plc.SetDevice("D2540", low);
                    Plc.SetDevice("D2542", high);

                    //int extremeint = (int)(profileModelViewModel.BodyLoadingPositionY * 10000);
                    //isWriteOperationAccurate = ControlWriteOperation("D2540", //isWriteOperationAccurate, null, extremeint, 1, 32);

                }

                if (profileModelViewModel.SpringMoldTorque != null)
                {
                    Plc.SetDevice("D2589", (int)(profileModelViewModel.SpringMoldTorque * 1));

                    //int extremeint = (int)(profileModelViewModel.SpringMoldTorque);
                    //isWriteOperationAccurate = ControlWriteOperation("D2589", //isWriteOperationAccurate, null, extremeint, 1, 16);

                }

                if (profileModelViewModel.SpringMoldXOffset != null)
                {
                    PlcUtility.Int2Short((int)(profileModelViewModel.SpringMoldXOffset * 10000), out high, out low);
                    //numbers = new short[] { low, high };

                    result = Plc.SetDevice("D2766", low);
                    Plc.SetDevice("D2767", high);

                    //int extremeint = (int)(profileModelViewModel.SpringMoldXOffset * 10000);
                    //isWriteOperationAccurate = ControlWriteOperation("D2766", //isWriteOperationAccurate, null, extremeint, 1, 32);

                }

                if (profileModelViewModel.SpringMoldZOffset != null)
                {
                    PlcUtility.Int2Short((int)(profileModelViewModel.SpringMoldZOffset * 10000), out high, out low);
                    //numbers = new short[] { low, high };

                    result = Plc.SetDevice("D2768", low);
                    Plc.SetDevice("D2769", high);

                    //int extremeint = (int)(profileModelViewModel.SpringMoldZOffset * 10000);
                    //isWriteOperationAccurate = ControlWriteOperation("D2768", //isWriteOperationAccurate, null, extremeint, 1, 32);

                }

                if (profileModelViewModel.MoldDifference != null)
                {
                    Plc.SetDevice("D347", (int)(profileModelViewModel.MoldDifference * 10));

                    //int extremeint = (int)(profileModelViewModel.MoldDifference * 10);
                    //isWriteOperationAccurate = ControlWriteOperation("D347", //isWriteOperationAccurate, null, extremeint, 1, 16);
                }


                if (profileModelViewModel.ProfilePlacementOffset != null)
                {
                    PlcUtility.Int2Short((int)(profileModelViewModel.ProfilePlacementOffset * 10000), out high, out low);
                    //numbers = new short[] { low, high };

                    result = Plc.SetDevice("D1992", low);
                    Plc.SetDevice("D1993", high);


                    //int extremeint = (int)(profileModelViewModel.ProfilePlacementOffset * 10000);
                    //isWriteOperationAccurate = ControlWriteOperation("D2768", //isWriteOperationAccurate, null, extremeint, 1, 32);

                }

                if (profileModelViewModel.BottomPrintingTemperature != null)
                {
                    Plc.SetDevice("D2824", (int)(profileModelViewModel.BottomPrintingTemperature * 10));
                }

                if (profileModelViewModel.CuttingSpeed != null)
                {
                    Plc.SetDevice("D2792", (int)(profileModelViewModel.CuttingSpeed));
                }

                //bool lastcheck = WriteReceiptInfo(profileModelViewModel.ReceiptViewModels);
                WriteReceiptInfo(profileModelViewModel.ReceiptViewModels);

                //SetPLCDataRequstReset();
                SetPLCDataRequstReset();


                return true;
                //return lastcheck;
            }
            catch (Exception exp)
            {

                return false;
            }
        }


        public bool WriteReceiptInfo(Dictionary<ReceiptType, List<ReceiptViewModel>> receiptViewModels)
        {
            if (receiptViewModels.Count == 0)
                return false;

            var index = 0;
            var device = $"R0";
            var buffer = new short[3613];
            bool isContinue = false;

            foreach (var keyValuePair in receiptViewModels)
            {

                if (isContinue)
                {
                    isContinue = false;
                    //continue;
                }
                else
                {
                    buffer[index++] = 0;
                }
                
                switch (keyValuePair.Key)
                {
                    case ReceiptType.MainReceipt:
                        buffer[index++] = 9999;
                        break;
                    case ReceiptType.AuxReceipt:
                        buffer[index++] = 9998;
                        break;
                    case ReceiptType.RearCutting:
                        //buffer[index++] = 9997;
                        WriteRearCuttingInfo(keyValuePair.Value);
                        isContinue = true;
                        break;
                    case ReceiptType.SurfaceCleaning:
                        buffer[index++] = 9996;
                        break;
                    case ReceiptType.GasketCutting:
                        //buffer[index++] = 9995;
                        WriteGasketInfo(keyValuePair.Value);
                        isContinue = true;
                        break;
                    default:
                        break;
                }

                if (isContinue)
                {
                    //isContinue = false;
                    continue;
                }

                index += 10;

                foreach (var item in keyValuePair.Value)
                {

                    buffer[index++] = item.EndCheck ? (short)1 : (short)0;

                    buffer[index++] = (short)item.ProcessCode;

                    if (item.ProcessCode == 0)
                    {

                    }

                    var high = 0;
                    var low = 0;
                    high = (short)((int)(item.X * 10000) >> 16);//ikinci turda r26 ya gelmesi geeken r 28 e gelmiş
                    low = (short)((int)(item.X * 10000) & 0xffff);

                    buffer[index++] = (short)low;
                    buffer[index++] = (short)high;

                    high = (short)((int)(item.Z * 10000) >> 16);//burası deneniyo şuan iks ti bu
                    low = (short)((int)(item.Z * 10000) & 0xffff);//burası z olarak giççek

                    buffer[index++] = (short)low;
                    buffer[index++] = (short)high;



                    if (item.ProcessCode == 28 || item.ProcessCode == 29)
                    {
                        high = (short)((int)(item.R * 10000) >> 16);
                        low = (short)((int)(item.R * 10000) & 0xffff);

                        buffer[index++] = (short)low;
                        buffer[index++] = (short)high;

                        high = (short)((int)(item.Y2 * 10000) >> 16);
                        low = (short)((int)(item.Y2 * 10000) & 0xffff);

                        buffer[index++] = (short)low;
                        buffer[index++] = (short)high;
                        /*
                        buffer[index++] = 0;
                        buffer[index++] = 0;*/
                }
                else if (item.ProcessCode == 33)
                    {
                        high = (short)((int)(item.Y1 * 10000) >> 16);
                        low = (short)((int)(item.Y1 * 10000) & 0xffff);

                        buffer[index++] = (short)low;
                        buffer[index++] = (short)high;

                        high = (short)((int)(item.Y2 * 10000) >> 16);
                        low = (short)((int)(item.Y2 * 10000) & 0xffff);

                        buffer[index++] = (short)low;
                        buffer[index++] = (short)high;
                    }
                    else
                    {
                        high = (short)((int)(item.Y1 * 10000) >> 16);
                        low = (short)((int)(item.Y1 * 10000) & 0xffff);

                        buffer[index++] = (short)low;
                        buffer[index++] = (short)high;

                        high = (short)((int)(item.Y2 * 10000) >> 16);
                        low = (short)((int)(item.Y2 * 10000) & 0xffff);

                        buffer[index++] = (short)low;
                        buffer[index++] = (short)high;
                    }

                    high = (short)((int)(item.Speed) >> 16);
                    low = (short)((int)(item.Speed) & 0xffff);

                    buffer[index++] = (short)low;
                    buffer[index++] = (short)high;
                }
            }

            while (index < 3613)
            {
                //burda mesela bafır patlamaz mı???
                buffer[index++] = 0;
            }

            Plc.WriteBuffer(new PlcData(device, buffer));

            Plc.SetDevice("M102", 1);
            //Plc.WriteDeviceList_32Bit(device, buffer);
            return true;
        }

        public bool ControlWriteOperation(string device, bool finalBool, bool? bval, int? dval, int divisor = 1,int byteCount = 32) {
            if (finalBool == false)
                return false;
            if (bval == null)
            {
                int cotrolAblePLCDeviceValue = 0;
                if (byteCount==32)
                    cotrolAblePLCDeviceValue = Plc.ReadDevice32Bit(device);
                else if(byteCount == 16)
                    cotrolAblePLCDeviceValue = Plc.ReadDevice(device);
                var finval = cotrolAblePLCDeviceValue / divisor ;
                if (finval != dval)
                    return false;
            }
            else
            {
                var cotrolAblePLCDeviceValue = Plc.GetDevice(device);
                if (Convert.ToInt32(bval) != cotrolAblePLCDeviceValue)
                    return false;
            }

            return true;
        }

        public void WriteGasketInfo(List<ReceiptViewModel> receiptViewModels)
        {
            int bufferLenght = 10;

            var index = 0;
            var device = $"D2442";
            var buffer = new short[bufferLenght];

            foreach (var item in receiptViewModels)
            {
                if (index == buffer.Length)
                {
                    continue;
                }

                var high = 0;
                var low = 0;
                high = (short)((int)(item.X * 10000) >> 16); //X1 //Yanaşma D2442 -44-46-48-50
                low = (short)((int)(item.X * 10000) & 0xffff);

                buffer[index++] = (short)low;
                buffer[index++] = (short)high;
            }
            while (index < bufferLenght)
            {
                buffer[index++] = 0;
            }
            Plc.WriteBuffer(new PlcData(device, buffer));


            index = 0;
            device = $"D2472";
            buffer = new short[bufferLenght];

            foreach (var item in receiptViewModels)
            {
                if (index == buffer.Length)
                {
                    continue;
                }

                var high = 0;
                var low = 0;

                high = (short)((int)(item.Z * 10000) >> 16);//Z1 //D2472 -80
                low = (short)((int)(item.Z * 10000) & 0xffff);

                buffer[index++] = (short)low;
                buffer[index++] = (short)high;
            }
            while (index < bufferLenght)
            {
                buffer[index++] = 0;
            }
            Plc.WriteBuffer(new PlcData(device, buffer));

            index = 0;
            device = $"D2452";
            buffer = new short[bufferLenght];

            foreach (var item in receiptViewModels)
            {
                if (index == buffer.Length)
                {
                    continue;
                }
                var high = 0;
                var low = 0;

                high = (short)((int)(item.R * 10000) >> 16);//X2 KEsme  //D2452 -60
                low = (short)((int)(item.R * 10000) & 0xffff);

                buffer[index++] = (short)low;
                buffer[index++] = (short)high;
            }
            while (index < bufferLenght)
            {
                buffer[index++] = 0;
            }
            Plc.WriteBuffer(new PlcData(device, buffer));

            index = 0;
            device = $"D2482";
            buffer = new short[bufferLenght];

            foreach (var item in receiptViewModels)
            {
                if (index == buffer.Length)
                {
                    continue;
                }

                var high = 0;
                var low = 0;

                high = (short)((int)(item.Y1 * 10000) >> 16);//Z2  //D2482 -90 
                low = (short)((int)(item.Y1 * 10000) & 0xffff);

                buffer[index++] = (short)low;
                buffer[index++] = (short)high;
            }
            while (index < bufferLenght)
            {
                buffer[index++] = 0;
            }
            Plc.WriteBuffer(new PlcData(device, buffer));

            index = 0;
            device = $"D2462";
            buffer = new short[bufferLenght];

            foreach (var item in receiptViewModels)
            {
                if (index == buffer.Length)
                {
                    continue;
                }

                var high = 0;
                var low = 0;

                high = (short)((int)(item.Y2 * 10000) >> 16);//X3 UZaklaşma D2462
                low = (short)((int)(item.Y2 * 10000) & 0xffff);

                buffer[index++] = (short)low;
                buffer[index++] = (short)high;
            }
            while (index < bufferLenght)
            {
                buffer[index++] = 0;
            }
            Plc.WriteBuffer(new PlcData(device, buffer));


            index = 0;
            device = $"D2492";
            buffer = new short[bufferLenght];

            foreach (var item in receiptViewModels)
            {
                if (index == buffer.Length)
                {
                    continue;
                }

                var high = 0;
                var low = 0;

                high = (short)((int)(item.Speed * 10000) >> 16);//Z3 D2492
                low = (short)((int)(item.Speed * 10000) & 0xffff);

                buffer[index++] = (short)low;
                buffer[index++] = (short)high;

            }
            while (index < bufferLenght)
            {
                buffer[index++] = 0;
            }
            Plc.WriteBuffer(new PlcData(device, buffer));

            //
            index = 0;
            device = $"D2126";
            buffer = new short[bufferLenght];

            foreach (var item in receiptViewModels)
            {
                if (index == buffer.Length)
                {
                    continue;
                }

                var high = 0;
                var low = 0;

                high = (short)((int)(item.Speed1 * 1) >> 16);//Z3 D2492
                low = (short)((int)(item.Speed1* 1) & 0xffff);

                buffer[index++] = (short)low;
                buffer[index++] = (short)high;

            }
            while (index < bufferLenght)
            {
                buffer[index++] = 0;
            }
            Plc.WriteBuffer(new PlcData(device, buffer));

            //
            index = 0;
            device = $"D2136";
            buffer = new short[bufferLenght];

            foreach (var item in receiptViewModels)
            {
                if (index == buffer.Length)
                {
                    continue;
                }

                var high = 0;
                var low = 0;

                high = (short)((int)(item.Speed2 * 1) >> 16);//Z3 D2492
                low = (short)((int)(item.Speed2 * 1) & 0xffff);

                buffer[index++] = (short)low;
                buffer[index++] = (short)high;

            }
            while (index < bufferLenght)
            {
                buffer[index++] = 0;
            }
            Plc.WriteBuffer(new PlcData(device, buffer));
            //
            index = 0;
            device = $"D2512";
            buffer = new short[bufferLenght];

            foreach (var item in receiptViewModels)
            {
                if (index == buffer.Length)
                {
                    continue;
                }

                var high = 0;
                var low = 0;

                high = (short)((int)(item.Y * 10000) >> 16);//Z3 D2492
                low = (short)((int)(item.Y * 10000) & 0xffff);

                buffer[index++] = (short)low;
                buffer[index++] = (short)high;

            }
            while (index < bufferLenght)
            {
                buffer[index++] = 0;
            }
            Plc.WriteBuffer(new PlcData(device, buffer));

            //Plc.WriteBuffer(new PlcData(device, buffer));
        }

        public void WriteRearCuttingInfo(List<ReceiptViewModel> receiptViewModels)
        {
            // 10'luk dizi limiti
            int itemCount = 10;

            // 32 bit veriler ardışık 2 device kullanacağı için 10 * 2 = 20 uzunluğunda bir buffer gerektirir.
            int bufferLenght32 = itemCount * 2;

            // 16 bit veriler tek device kullanacağı için 10 uzunluğunda bir buffer yeterlidir.
            int bufferLenght16 = itemCount;

            var index = 0;

            // --- X : D7110 (32 Bit x 10 luk dizi) -> 10000 ile çarpılıyor ---
            var device = $"D7110";
            var buffer = new short[bufferLenght32];

            foreach (var item in receiptViewModels)
            {
                // Limit dolduysa fazlasını işleme, döngüden çık
                if (index >= buffer.Length)
                {
                    break;
                }

                var high = 0;
                var low = 0;
                high = (short)((int)(item.X * 10000) >> 16);
                low = (short)((int)(item.X * 10000) & 0xffff);

                buffer[index++] = (short)low;
                buffer[index++] = (short)high;
            }
            // Listede 10'dan az eleman varsa, kalan buffer alanlarını 0 ile doldurur
            while (index < bufferLenght32)
            {
                buffer[index++] = 0;
            }
            Plc.WriteBuffer(new PlcData(device, buffer));


            // --- ProcessCode : D7130 (32 Bit x 10 luk dizi) -> 10000 ile çarpılıyor ---
            index = 0;
            device = $"D7130";
            buffer = new short[bufferLenght32];

            foreach (var item in receiptViewModels)
            {
                if (index >= buffer.Length)
                {
                    break;
                }

                var high = 0;
                var low = 0;

                high = (short)((int)(item.R * 10000) >> 16);
                low = (short)((int)(item.R * 10000) & 0xffff);

                buffer[index++] = (short)low;
                buffer[index++] = (short)high;
            }
            while (index < bufferLenght32)
            {
                buffer[index++] = 0;
            }
            Plc.WriteBuffer(new PlcData(device, buffer));


            // --- Z : D7150 (32 Bit x 10 luk dizi) -> 10000 ile çarpılıyor ---
            index = 0;
            device = $"D7150";
            buffer = new short[bufferLenght32];

            foreach (var item in receiptViewModels)
            {
                if (index >= buffer.Length)
                {
                    break;
                }

                var high = 0;
                var low = 0;
                high = (short)((int)(item.Z * 10000) >> 16);
                low = (short)((int)(item.Z * 10000) & 0xffff);

                buffer[index++] = (short)low;
                buffer[index++] = (short)high;
            }
            while (index < bufferLenght32)
            {
                buffer[index++] = 0;
            }
            Plc.WriteBuffer(new PlcData(device, buffer));


            // --- TORK (Önceki Y1) : D7170 (16 Bit x 10 luk dizi) -> 10000 ile ÇARPILMIYOR ---
            index = 0;
            device = $"D7170";
            buffer = new short[bufferLenght16];

            foreach (var item in receiptViewModels)
            {
                if (index >= buffer.Length)
                {
                    break;
                }

                buffer[index++] = (short)((int)(item.Y1) & 0xffff);
            }
            while (index < bufferLenght16)
            {
                buffer[index++] = 0;
            }
            Plc.WriteBuffer(new PlcData(device, buffer));


            // --- SÜRE (Önceki Y2) : D7180 (16 Bit x 10 luk dizi) -> 10000 ile ÇARPILMIYOR ---
            index = 0;
            device = $"D7180";
            buffer = new short[bufferLenght16];

            foreach (var item in receiptViewModels)
            {
                if (index >= buffer.Length)
                {
                    break;
                }

                buffer[index++] = (short)((int)(item.Y2) & 0xffff);
            }
            while (index < bufferLenght16)
            {
                buffer[index++] = 0;
            }
            Plc.WriteBuffer(new PlcData(device, buffer));
        }
        public void SetPLCDataRequstReset()
        {
            Plc.SetDevice("M101", 0);
            Plc.SetDevice("M102", 1);
        }

        /*
        public bool WriteProfileInfoOLD(ProfileModelViewModel profileModelViewModel)
        {
            try
            {
                if (profileModelViewModel == null)
                    return false;

                var index = 0;
                var device = $"D1000";
                var buffer = new int[50];

                buffer[index++] = (int)(profileModelViewModel.Width * 100);
                buffer[index++] = (int)(profileModelViewModel.Height * 100);


                double selectedModByte = 0;
                if (profileModelViewModel.Overprint != null && (bool)profileModelViewModel.Overprint)
                    selectedModByte += Math.Pow(2, 0);
                if (profileModelViewModel.BackMold != null && (bool)profileModelViewModel.BackMold)
                    selectedModByte += Math.Pow(2, 1);

                buffer[index++] = (int)selectedModByte;

                buffer[index++] = profileModelViewModel.MeltingTemperature != null ? (int)(profileModelViewModel.MeltingTemperature * 10) : 0;
                buffer[index++] = profileModelViewModel.MeltingDistance != null ? (int)(profileModelViewModel.MeltingDistance * 10) : 0;
                buffer[index++] = profileModelViewModel.GluingTime != null ? (int)(profileModelViewModel.GluingTime * 10) : 0;

                //buffer[index++] = profileModelViewModel.BottomPrintingTime != null ? (int)(profileModelViewModel.BottomPrintingTime * 100) : 0;
                buffer[index++] = profileModelViewModel.BottomPrintingTemperature != null ? (int)(profileModelViewModel.BottomPrintingTemperature * 100) : 0;

                buffer[index++] = profileModelViewModel.OverprintTime != null ? (int)(profileModelViewModel.OverprintTime * 100) : 0;
                buffer[index++] = profileModelViewModel.OverprintZPosition != null ? (int)(profileModelViewModel.OverprintZPosition * 100) : 0;

                buffer[index++] = profileModelViewModel.RearDieTorque != null ? (int)(profileModelViewModel.RearDieTorque * 100) : 0;

                var high = 0;
                var low = 0;
                if (profileModelViewModel.BackMoldPrintXPosition != null)
                {
                    high = (short)((int)(profileModelViewModel.BackMoldPrintXPosition * 10000) >> 16);
                    low = (short)((int)(profileModelViewModel.BackMoldPrintXPosition * 10000) & 0xffff);
                }

                buffer[index++] = low;
                buffer[index++] = high;

                if (profileModelViewModel.BackMoldPrintXPosition != null)
                {
                    high = (short)(((int)profileModelViewModel.BackMoldPrintXPosition * 10000) >> 16);
                    low = (short)(((int)profileModelViewModel.BackMoldPrintXPosition * 10000) & 0xffff);

                    buffer[index++] = low;
                    buffer[index++] = high;
                }
                else
                {
                    buffer[index++] = 0;
                    buffer[index++] = 0;
                }

                buffer[index++] = profileModelViewModel.MeltingDistance != null ? (int)(profileModelViewModel.MeltingDistance * 100) : 0;


                if (profileModelViewModel.JawOpeningDistance != null)
                {
                    high = (short)((int)(profileModelViewModel.JawOpeningDistance * 10000) >> 16);
                    low = (short)(((int)profileModelViewModel.JawOpeningDistance * 10000) & 0xffff);

                    buffer[index++] = low;
                    buffer[index++] = high;
                }
                else
                {
                    buffer[index++] = 0;
                    buffer[index++] = 0;
                }

                if (profileModelViewModel.UpperStrippingZPosition != null)
                {
                    high = (short)((int)(profileModelViewModel.UpperStrippingZPosition * 10000) >> 16);
                    low = (short)((int)(profileModelViewModel.UpperStrippingZPosition * 10000) & 0xffff);

                    buffer[index++] = low;
                    buffer[index++] = high;
                }
                else
                {
                    buffer[index++] = 0;
                    buffer[index++] = 0;
                }

                if (profileModelViewModel.LowerStrippingZPosition != null)
                {
                    high = (short)(((int)profileModelViewModel.LowerStrippingZPosition * 10000) >> 16);
                    low = (short)(((int)profileModelViewModel.LowerStrippingZPosition * 10000) & 0xffff);

                    buffer[index++] = low;
                    buffer[index++] = high;
                }
                else
                {
                    buffer[index++] = 0;
                    buffer[index++] = 0;
                }

                buffer[index++] = profileModelViewModel.MoldDifference != null ? (short)(profileModelViewModel.MoldDifference * 100) : 0;

                //Plc.WriteBuffer(new PlcData(device, buffer));

                Plc.WriteDeviceList_32Bit(device, buffer);

                WriteReceiptInfo(profileModelViewModel.ReceiptViewModels);


                SetPLCDataRequstReset();

                return true;
            }
            catch (Exception exp)
            {
                return false;
            }
        }*/
    }
}
