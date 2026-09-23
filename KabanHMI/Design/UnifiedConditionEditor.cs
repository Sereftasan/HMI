using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System.ComponentModel.Design;
using System.IO;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Design
{
    public class UnifiedConditionEditor : UITypeEditor
    {
        private void Log(string message)
        {
            try 
            { 
                string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Kaban_Debug.txt");
                System.IO.File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n"); 
            } 
            catch { }
        }

        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.DropDown;

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            var edSvc = (IWindowsFormsEditorService)provider?.GetService(typeof(IWindowsFormsEditorService));
            var host = (IDesignerHost)provider?.GetService(typeof(IDesignerHost));
            // ADD THIS NEW SERVICE:
            var typeResSvc = (ITypeResolutionService)provider?.GetService(typeof(ITypeResolutionService));

            if (edSvc == null || context?.Instance == null) return value;

            Log("\n--- DROPDOWN CLICKED ---");

            // 1. Determine Target Machine
            string machineNamespace = "";
            if (host != null && !string.IsNullOrEmpty(host.RootComponentClassName))
            {
                var parts = host.RootComponentClassName.Split('.');
                if (parts.Length >= 3) machineNamespace = $"{parts[0]}.{parts[1]}.{parts[2]}";
            }
            Log($"Target Namespace: {machineNamespace}");

            // 2. Clear previous data
            foreach (var field in typeof(MachineConditionMap).GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public))
            {
                if (typeof(System.Collections.IDictionary).IsAssignableFrom(field.FieldType))
                {
                    var dict = (System.Collections.IDictionary)field.GetValue(null);
                    dict?.Clear();
                }
            }

            // 3. Native Project Resolution Logic
            if (!string.IsNullOrEmpty(machineNamespace) && typeResSvc != null)
            {
                string exactClassName = $"{machineNamespace}.MachineConfig";
                Log($"Attempting to resolve exactly: {exactClassName}");

                try
                {
                    // Use the native bridge to reach into the user's project
                    Type configType = typeResSvc.GetType(exactClassName);

                    if (configType != null)
                    {
                        var initMethod = configType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                        initMethod?.Invoke(null, null);
                        Log($"SUCCESS: Initialize() executed via TypeResolutionService. Loaded {MachineConditionMap.GetAllMachineBits().Count()} bits.");
                    }
                    else
                    {
                        Log($"ERROR: ITypeResolutionService could not find {exactClassName}. Is it compiled?");
                    }
                }
                catch (Exception ex)
                {
                    Log($"CRITICAL ERROR during Type Resolution: {ex.Message}");
                }
            }
            else
            {
                Log("ERROR: Machine Namespace is empty, or ITypeResolutionService is unavailable.");
            }

            // 4. Build UI
            var plcProp = context.Instance.GetType().GetProperty("PlcNumber");
            int targetPlc = (int)(plcProp?.GetValue(context.Instance) ?? 1);
            long currentVal = (long)(SystemCondition)value;

            var primary = new List<DisplayItem>();
            var secondary = new List<DisplayItem>();

            foreach (SystemCondition item in Enum.GetValues(typeof(SystemCondition)))
            {
                if (item == SystemCondition.None) continue;
                var rule = MachineConditionMap.GetRule(item);
                if (rule != null)
                {
                    var di = new DisplayItem { Bit = (long)item, Text = EnumHelper.GetDescription(item), Plc = rule.PlcNumber };
                    if (rule.PlcNumber == targetPlc) primary.Add(di); else secondary.Add(di);
                }
            }

            foreach (var bit in MachineConditionMap.GetAllMachineBits())
            {
                var di = new DisplayItem { Bit = bit.Key, Text = bit.Value.Name, Plc = bit.Value.PlcNumber };
                if (bit.Value.PlcNumber == targetPlc) primary.Add(di); else secondary.Add(di);
            }

            var panel = new Panel { Width = 400, Height = 500, AutoScroll = true, BackColor = System.Drawing.SystemColors.Window };
            int yPos = 2;
            var checkBoxes = new List<(CheckBox cb, DisplayItem di)>();

            if (!primary.Any() && !secondary.Any())
            {
                string missingInfo = string.IsNullOrEmpty(machineNamespace) ? "Unknown Machine" : machineNamespace;
                var lbl = new Label { Text = $"No logic found for {missingInfo}", Left = 4, Top = yPos, Width = 390, Height = 20, ForeColor = System.Drawing.SystemColors.GrayText };
                panel.Controls.Add(lbl);
                yPos += 25;
            }
            else
            {
                foreach (var di in primary.OrderBy(x => x.Text))
                {
                    var cb = new CheckBox { Text = di.Text, Checked = (currentVal & di.Bit) != 0, Left = 2, Top = yPos, Width = 378, Height = 20, AutoSize = false };
                    panel.Controls.Add(cb);
                    checkBoxes.Add((cb, di));
                    yPos += 20;
                }

                if (secondary.Any())
                {
                    yPos += 8;
                    panel.Controls.Add(new Label { Left = 6, Top = yPos, Width = 372, Height = 1, BorderStyle = BorderStyle.Fixed3D, AutoSize = false });
                    yPos += 3;
                    panel.Controls.Add(new Label { Text = "Conditions from Other PLCs", Left = 6, Top = yPos, Width = 372, Height = 18, ForeColor = System.Drawing.SystemColors.GrayText, AutoSize = false, Font = new System.Drawing.Font(panel.Font, System.Drawing.FontStyle.Italic) });
                    yPos += 20;

                    foreach (var di in secondary.OrderBy(x => x.Text))
                    {
                        var dItem = new DisplayItem { Bit = di.Bit, Text = di.Text + $" (PLC {di.Plc})", Plc = di.Plc };
                        var cb = new CheckBox { Text = dItem.Text, Checked = (currentVal & dItem.Bit) != 0, Left = 2, Top = yPos, Width = 378, Height = 20, AutoSize = false };
                        panel.Controls.Add(cb);
                        checkBoxes.Add((cb, dItem));
                        yPos += 20;
                    }
                }
            }

            panel.Height = Math.Min(500, yPos + 4);
            edSvc.DropDownControl(panel);

            long result = 0;
            foreach (var (cb, di) in checkBoxes) if (cb.Checked) result |= di.Bit;

            Type enumType = context?.PropertyDescriptor?.PropertyType ?? typeof(SystemCondition);
            return Enum.ToObject(enumType, result);
        }

        private class DisplayItem { public long Bit; public string Text; public int Plc; }
    }
}
