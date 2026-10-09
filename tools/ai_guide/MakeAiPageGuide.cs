using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

// THE PAGE DESIGN GUIDE FOR AN AI (9 October: "I just need an AI prompt that explains our controls -
// which control we use for which function -, page design etc., so I can feed the AI with it and it creates
// the designer pages for me"). Made from the program itself, so it never drifts from it:
//   - each control's properties in the order and categories of the Properties window (PropertyOrder),
//     their types, defaults and descriptions (the [Description] attributes, else the source's summaries);
//   - each control's size on our pages and a real example of it, from the machines' designer files;
//   - the values of every enum those properties take;
//   - one of our pages whole;
// put into tools/ai_guide/guide_template.md (the hand-written sections).
//
// Run (mono, the built program beside it):  MakeAiPageGuide.exe <repository folder> <output .md>
class MakeAiPageGuide
{
    static readonly string[] Controls =
    {
        "CylinderUnitHMI", "AxisUnitHMI", "AxisSettingHMI", "PneumaticPositioningHMI", "CylinderEncoderUnitHMI", "CylinderEncoderSettingHMI", "ButtonHMI", "LampHMI", "NumericInputHMI", "NumericDisplayHMI",
        "CheckBoxHMI", "CheckListHMI", "ComboBoxHMI", "LabelHMI", "GroupBoxHMI", "MeterHMI", "MeterInputHMI"
    };

    static readonly Dictionary<string, string> Purpose = new Dictionary<string, string>
    {
        { "CylinderUnitHMI", "A cylinder (any two-way movement) on a Manual Movement page: a frame with its title, two command buttons (A left, B right), the end sensors as lamps on the buttons, the \"?\" with its helpers and its alarms - and no position (a cylinder whose position is read is a PneumaticPositioningHMI or a CylinderEncoderUnitHMI). Its ValveType and FeedbackType decide which sensors it needs and how the HMI watches its movements." },
        { "AxisUnitHMI", "A servo axis on a Manual Movement page, looking like a CylinderUnitHMI: set only its AxisNumber - its jog bits, position, name, error bit and reference sensor are taken from the AxisSettingHMI row with the same HMIAxisIndex." },
        { "AxisSettingHMI", "One servo axis as one row of the Axis Parameters page: jog buttons, position, current, limits, reference, calibration, its codes (servo alarm, axis alarm, axis warning), and the simple motion module's values. One row per axis; the rows of a page line up as a table." },
        { "PneumaticPositioningHMI", "A pneumatic positioning - a cylinder with a position sensor (analogue or encoder) and a 5/3 valve that the PLC drives to a target, used on our machines as an axis - as ONE ROW, laid out like an AxisSettingHMI row: its name (TitleTextKey) at the left end, then Manual movement (CommandBackAddress / CommandForwardAddress and the position: a CylinderUnitHMI inside, so its ValveType, FeedbackType, error bits, help, alarms and coupling are a cylinder unit's), Target position (TargetPosition + PositioningStartAddress, a SET button), Calibration (CalibrationAddress + CalibrateAddress, a SET button), Positioning tolerance (PositioningToleranceAddress) and Direction ref (PositioningDirectionReferenceAddress - the old pages' 'Stopping Offset': the PLC compares the target with it to choose the direction, a target above it approached the way the position increases, one below it the other way). The rows of a page share one names' column (the widest name) and the headings over their columns: plain Labels tagged \"PositioningHeading:Manual\" (Target, Calibration, Tolerance, DirectionRef) above the first row - made at run time when the page has none. A row whose VisibilityConditions are not met is taken out and the rows under it move up. Rows 40 high, 44 apart." },
        { "CylinderEncoderUnitHMI", "A cylinder with an encoder (a clamp or a stop whose position an encoder counts, its reference sensor setting the count) on a Manual Movement page, looking like a CylinderUnitHMI: set only its EncoderNumber (and PlcNumber) - its buttons, sensors, position and scaling, valve, feedback, error bits and helpers are taken from the CylinderEncoderSettingHMI row with the same EncoderNumber. Its own: TitleTextKey (the title on this page; empty: the row's name) and the conditions it is shown and used under on this page." },
        { "CylinderEncoderSettingHMI", "A cylinder with an encoder as ONE ROW of Machine Parameters > Encoders, laid out like an AxisSettingHMI row (and like PneumaticPositioningHMI, the same base): its name, Manual movement (CommandBackAddress / CommandForwardAddress, the sensors as BackSensorAddress / ForwardSensorAddress, the position; ValveType, FeedbackType, the error bits and help as a cylinder unit's), Reference (ReferenceAddress), Measuring tolerance (MeasuringToleranceAddress), Minimum limit (MinimumLimitAddress - the old pages' 'Start Limit'), Unclamping interval (UnclampingIntervalAddress). EncoderNumber is the number a CylinderEncoderUnitHMI names: the cylinder is described once, here. Headings: plain Labels tagged \"EncoderHeading:Manual\" (Reference, Tolerance, MinimumLimit, Unclamping). Rows 40 high, 44 apart." },
        { "ButtonHMI", "A push button writing bits (BitActions) and / or words (WordActions): momentary, alternate, SET or RESET; a lamp of its state or sensor on it (Lamp1Address). Its caption: TranslationKey + FallbackText (or ButtonText)." },
        { "LampHMI", "A bit shown as a lamp (a sensor, a state, an output), its caption (DescriptionText) beside it." },
        { "NumericInputHMI", "A value typed in (a word or a double word): the keypad on a tap, limits, scaling, units, confirmation, conditions. Its caption (DescriptionText) is drawn to its left." },
        { "NumericDisplayHMI", "A value only shown (a word or a double word): scaled, in decimal or hexadecimal (an error code)." },
        { "CheckBoxHMI", "One bit as a tick box (an option on / off), written on a click." },
        { "CheckListHMI", "Many option bits as rows of 'caption : [x]' in columns - one control for a whole list of options. Options: one per line, TranslationKey|Fallback|Address." },
        { "ComboBoxHMI", "A choice written to one word: Options one per line, TranslationKey|Fallback|Value; the PLC's value shown as the chosen one." },
        { "LabelHMI", "A heading or a fixed text on a page, translated (TranslationKey)." },
        { "GroupBoxHMI", "A frame like a CylinderUnitHMI's around controls that belong together (a row of small buttons); its title TitleTextKey. The controls inside are added to it." },
        { "MeterHMI", "A gauge showing a value (a load, a pressure) with its bands - read only." },
        { "MeterInputHMI", "A gauge whose value is set (with its arrows and limits) - it writes to the PLC." },
    };

    static string Root;

    /// <summary>The program's own chrome (the menu's buttons, colours, pictures): left at their defaults on a page - not listed.</summary>
    static readonly HashSet<string> Skip = new HashSet<string>
    {
        "ActiveBackColor", "IsActivePage", "RoundLeftOnly", "RoundRightOnly", "RestingBorderColor", "OutputBorderColor", "HoverBorderColor",
        "AlertBlink", "ImageActive", "ImagePassive", "ColorActive", "ColorPassive", "ColorDisabled", "ForeDisabled", "DisplayOffset", "ShowCaption",
        "BlinkCondition", "CornerRadius", "ValueBorderColor", "ShowValueBorder", "TypeCheck", "ButtonA", "ButtonB", "PositionDisplay"
    };

    /// <summary>What a property is, said for a page's designer - before the code's own words (which tell its history too).</summary>
    static readonly Dictionary<string, string> Known = new Dictionary<string, string>
    {
        { "PlcNumber", "The machine's own PLC number: 1 = its main PLC (2 = a second one)." },
        { "Address", "The PLC device it reads / writes (\"D16\", \"M232\")." },
        { "AddressOffset", "Leave empty: an offset added to the address at run time." },
        { "DataType", "Bin_16 = one word, Bin_32 = a double word (two words)." },
        { "Divisor", "The raw PLC value is divided by it (100: shown in 0.01 steps)." },
        { "DecimalPlaces", "Decimals shown." },
        { "Digits", "Digits shown in all (the box's width follows)." },
        { "Suffix", "The unit written after the value (\"mm\", \"s\")." },
        { "UnitType", "The value's family (Length, Time ...): a user may see it in another unit; the PLC keeps its own." },
        { "DescriptionText", "Its caption, drawn beside it: a translation key - usually the English text, shown as written when it has no translation." },
        { "DescriptionSide", "Which side of it the caption is drawn." },
        { "TranslationKey", "The key of its text in the translation files - usually the English text itself." },
        { "FallbackText", "Its caption when the key has no translation (the English text)." },
        { "ButtonText", "The same as FallbackText." },
        { "RequiredSecurity", "The lowest level allowed to use it: Operator (Manual Movement), Maintenance (Machine Parameters), Admin." },
        { "LogAction", "true: every write it makes goes to the log with the user's name - set it on everything that writes." },
        { "VisibilityConditions", "Shown only while these SystemCondition flags are met (None: always)." },
        { "EnableConditions", "Usable only while these are met, greyed otherwise (None: always)." },
        { "EnableTrigger", "A PLC bit that must be ON before it can be used (leave empty)." },
        { "ToolTipTextKey", "Its tooltip: a translation key, or the text." },
        { "ToolTipText", "Its tooltip: a translation key, or the text." },
        { "TitleTextKey", "The frame's title: a translation key - usually the English text." },
        { "TitleTipKey", "The title's tooltip (a translation key, or the text)." },
        { "PositionTipKey", "The position's tooltip (a translation key, or the text)." },
        { "PositionDataType", "Bin_16 = one word, Bin_32 = a double word." },
        { "PositionDecimalPlaces", "Decimals of the position." },
        { "PositionSuffix", "The position's unit (\"mm\")." },
        { "PositionDigits", "Digits of the position in all." },
        { "ConfirmationNeeded", "Asks \"Are you sure?\" before a SET / RESET / ALTERNATE or word write (never before a momentary jog)." },
        { "ConfirmWrite", "Asks before the value is written." },
        { "MinLimit", "The lowest value accepted (a number, a PLC word, or none)." },
        { "MaxLimit", "The highest value accepted (a number, a PLC word, or none)." },
        { "BitActions", "The bits the button writes: one BitAction (Address, Action) each." },
        { "WordActions", "The words the button writes (a value each): leave empty for a plain button." },
        { "Options", "One per line (see above)." },
        { "AxisNumber", "The axis: the HMIAxisIndex of its AxisSettingHMI row on the Axis Parameters page." },
    };

    static void Main(string[] args)
    {
        Root = args.Length > 0 ? args[0] : ".";
        string output = args.Length > 1 ? args[1] : Path.Combine(Root, "docs", "AI_Page_Design_Guide.md");
        var ui = typeof(Kaban.UI.Controls.ButtonHMI).Assembly;
        var layouts = Kaban.UI.Design.PropertyOrder.Layouts;
        var summaries = ReadSummaries(Path.Combine(Root, "KabanHMI"), Path.Combine(Root, "Kaban.Core"));
        var designers = Directory.GetFiles(Path.Combine(Root, "Kaban.Machines.FA2010", "Screens"), "*.Designer.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetDirectories(Root, "Kaban.Machines.*").Where(d => !d.EndsWith("FA2010"))
                .Where(d => Directory.Exists(Path.Combine(d, "Screens")))
                .SelectMany(d => Directory.GetFiles(Path.Combine(d, "Screens"), "*.Designer.cs", SearchOption.AllDirectories)))
            .ToList();
        var instances = ReadInstances(designers);
        var enums = new List<Type>();

        var controls = new StringBuilder();
        foreach (var name in Controls)
        {
            var t = ui.GetType("Kaban.UI.Controls." + name);
            if (t == null) continue;
            controls.AppendLine("### " + name);
            controls.AppendLine();
            string purpose;
            if (Purpose.TryGetValue(name, out purpose)) controls.AppendLine(purpose);
            var mine = instances.Where(i => i.Type == name).ToList();
            var sizes = mine.Select(i => i.Size).Where(s => s != null).GroupBy(s => s).OrderByDescending(g => g.Count()).ToList();
            if (mine.Count > 0)
                controls.AppendLine("On our pages: " + mine.Count + (mine.Count == 1 ? " of it" : " of them") +
                                    (sizes.Count > 0 ? "; size " + string.Join(", ", sizes.Take(3).Select(g => g.Key + " (" + g.Count() + ")")) : "") + ".");
            controls.AppendLine();
            object fresh = null;
            try { fresh = Activator.CreateInstance(t); } catch { }
            List<KeyValuePair<string, string[]>> layout;
            if (layouts.TryGetValue(name, out layout))
            {
                controls.AppendLine("| Property | Type | Default | What it is |");
                controls.AppendLine("|---|---|---|---|");
                foreach (var cat in layout.OrderBy(c => Array.IndexOf(Kaban.UI.Design.PropertyOrder.CategoryOrder, c.Key)))
                {
                    if (cat.Value.All(Skip.Contains)) continue;
                    controls.AppendLine("| **" + cat.Key + "** | | | |");
                    foreach (var p in cat.Value)
                    {
                        if (Skip.Contains(p)) continue;
                        var pi = t.GetProperty(p, BindingFlags.Public | BindingFlags.Instance);
                        if (pi == null) continue;
                        var pt = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
                        if (pt.IsEnum && !enums.Contains(pt)) enums.Add(pt);
                        controls.AppendLine("| `" + p + "` | " + TypeName(pi.PropertyType) + " | " + Cell(Default(pi, fresh)) + " | " + Cell(Describe(pi, summaries)) + " |");
                    }
                }
                controls.AppendLine();
            }
            if (name == "ButtonHMI")
            {
                controls.AppendLine("A `BitAction` (`Kaban.Core.Models.BitAction`): `Address` (a bit), `Action` (BitActionType). A `WordAction` writes a value to a word.");
                controls.AppendLine();
            }
            // an example from a real page (Manual Movement, Machine Parameters) before the demo screens
            var real = mine.Where(i => i.Page.StartsWith("Manual\\") || i.Page.StartsWith("Parameters\\")).ToList();
            var pool = real.Count > 0 ? real : mine;
            var example = pool.Where(i => i.Lines.Count <= 30).OrderByDescending(i => i.Lines.Count).FirstOrDefault() ?? pool.FirstOrDefault();
            if (example != null)
            {
                controls.AppendLine("An example from " + example.Page + ":");
                controls.AppendLine();
                controls.AppendLine("```csharp");
                foreach (var l in example.Lines.Take(40)) controls.AppendLine(l.Trim());
                controls.AppendLine("```");
                controls.AppendLine();
            }
        }

        foreach (var extra in new[] { typeof(Kaban.Core.Enums.BitActionType), typeof(Kaban.Core.Enums.DataTypeEnum), typeof(Kaban.Core.Enums.DisplayFormatEnum),
                                      typeof(Kaban.Core.Enums.UnitType), typeof(Kaban.Core.Enums.SecurityLevel), typeof(Kaban.Core.Enums.SystemCondition) })
            if (!enums.Contains(extra)) enums.Add(extra);
        var en = new StringBuilder();
        foreach (var e in enums.OrderBy(x => x.Name))
        {
            en.AppendLine("- **" + e.FullName.Replace('+', '.') + "**" + (e.GetCustomAttributes(typeof(FlagsAttribute), false).Any() ? " (flags: combine with `|`)" : "") + ": " +
                          string.Join(", ", Enum.GetNames(e).Select(n =>
                          {
                              var f = e.GetField(n);
                              var d = f.GetCustomAttributes(typeof(DescriptionAttribute), false).OfType<DescriptionAttribute>().FirstOrDefault();
                              string shown = n;
                              var conv = TypeDescriptor.GetConverter(e);
                              try { var s = conv.ConvertToInvariantString(Enum.Parse(e, n)); if (!string.IsNullOrEmpty(s) && s != n) shown = n + " (shown as " + s + ")"; } catch { }
                              return "`" + shown + "`" + (d != null && d.Description != n ? " - " + d.Description : "");
                          })));
        }

        var pages = new StringBuilder();
        foreach (var rel in new[] { @"Kaban.Machines.FA2010/Screens/Manual/ManualRobot.cs", @"Kaban.Machines.FA2010/Screens/Manual/ManualRobot.Designer.cs" })
        {
            var path = Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) continue;
            pages.AppendLine("`" + rel.Replace('/', '\\') + "`:");
            pages.AppendLine();
            pages.AppendLine("```csharp");
            pages.AppendLine(File.ReadAllText(path).TrimStart('﻿').TrimEnd());
            pages.AppendLine("```");
            pages.AppendLine();
        }

        // THE MACHINES' OWN CONDITIONS: registered by each machine module (RegisterConditions)
        // and stored in a designer file as their number.
        var conds = new StringBuilder();
        try
        {
            var prog = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly.GetType("Kaban.Machines.FA2010.Program");
            prog.GetMethod("RegisterDriverKinds", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            prog.GetMethod("MountMachines", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            // indented: a table inside the list item it belongs to
            conds.AppendLine("  | Number | Name (as the conditions list shows it) | Machine | Read from |");
            conds.AppendLine("  |---|---|---|---|");
            foreach (var kv in Kaban.Core.Helpers.MachineConditionMap.GetAllMachineBits().OrderBy(k => k.Key))
            {
                var d = kv.Value;
                string module = (d.TagName ?? "").Contains(".") ? d.TagName.Substring(0, d.TagName.IndexOf('.')) : "";
                conds.AppendLine("  | `" + kv.Key + "L` (1 << " + (int)Math.Round(Math.Log(kv.Key, 2)) + ") | " + d.Name + " | " + module + " | PLC " + d.PlcNumber + " `" + d.Address + "` " +
                                 OpText(d.Operator) + " " + d.RequiredValue + " |");
            }
        }
        catch (Exception ex) { conds.AppendLine("(the machines could not be mounted to list them: " + ex.Message + ")"); }

        var template = File.ReadAllText(Path.Combine(Root, "tools", "ai_guide", "guide_template.md"));
        var text = template.Replace("{{DATE}}", DateTime.Now.ToString("d MMMM yyyy", CultureInfo.InvariantCulture))
                           .Replace("{{MACHINE_CONDITIONS}}", conds.ToString().TrimEnd())
                           .Replace("{{CONTROLS}}", controls.ToString().TrimEnd())
                           .Replace("{{ENUMS}}", en.ToString().TrimEnd())
                           .Replace("{{PAGES}}", pages.ToString().TrimEnd());
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        File.WriteAllText(output, text.Replace("\r\n", "\n"), new UTF8Encoding(false));
        Console.WriteLine("written " + output + " (" + text.Length + " characters, " + Controls.Length + " controls, " + enums.Count + " enums, " + instances.Count + " examples read)");
    }

    static string OpText(Kaban.Core.Enums.ComparisonOperator op)
    {
        switch (op.ToString())
        {
            case "Equal": return "=";
            case "NotEqual": return "<>";
            case "Greater": return ">";
            case "GreaterOrEqual": return ">=";
            case "Less": return "<";
            case "LessOrEqual": return "<=";
            default: return op.ToString();
        }
    }

    // ---- the properties ------------------------------------------------------------

    static string TypeName(Type t)
    {
        var u = Nullable.GetUnderlyingType(t);
        if (u != null) return TypeName(u) + "?";
        if (t == typeof(string)) return "string";
        if (t == typeof(int)) return "int";
        if (t == typeof(bool)) return "bool";
        if (t == typeof(double)) return "double";
        if (t == typeof(decimal)) return "decimal";
        if (t == typeof(float)) return "float";
        if (t == typeof(long)) return "long";
        if (t.IsGenericType) return t.Name.Split('`')[0] + "<" + string.Join(", ", t.GetGenericArguments().Select(TypeName)) + ">";
        return t.Name;
    }

    static string Default(PropertyInfo pi, object fresh)
    {
        var dv = pi.GetCustomAttributes(typeof(DefaultValueAttribute), true).OfType<DefaultValueAttribute>().FirstOrDefault();
        object v = null;
        if (dv != null) v = dv.Value;
        else if (fresh != null && pi.CanRead && pi.GetIndexParameters().Length == 0)
            try { v = pi.GetValue(fresh, null); } catch { v = null; }
        if (v == null) return "";
        var t = v.GetType();
        if (t.IsPrimitive || t.IsEnum || v is string || v is decimal)
        {
            var s = Convert.ToString(v, CultureInfo.InvariantCulture);
            if (t.IsEnum) try { s = TypeDescriptor.GetConverter(t).ConvertToInvariantString(v) ?? s; } catch { }
            return v is string ? (s.Length == 0 ? "\"\"" : "\"" + s + "\"") : s;
        }
        if (v is System.Drawing.Size) { var z = (System.Drawing.Size)v; return z.Width + " x " + z.Height; }
        if (v is System.Drawing.Color) return ((System.Drawing.Color)v).Name;
        return "";
    }

    static string Describe(PropertyInfo pi, Dictionary<string, string> summaries)
    {
        string k;
        // a row's own words first: its name is not a frame's title, its divisor is its fields'
        if (pi.DeclaringType != null && new[] { "CylinderRowHMI", "PneumaticPositioningHMI", "CylinderEncoderSettingHMI", "CylinderEncoderUnitHMI" }.Contains(pi.DeclaringType.Name))
        {
            var own = Own(pi, summaries);
            if (own.Length > 0) return own;
        }
        if (Known.TryGetValue(pi.Name, out k)) return k;
        // a unit's B side as its A side
        var side = Regex.Match(pi.Name, @"^(Command|Button|Lamp1|Lamp2|Relay|Output|Error|Help)([AB])(.*)$");
        if (side.Success)
        {
            string a = side.Groups[1].Value + "A" + side.Groups[3].Value;
            var pa = pi.DeclaringType.GetProperty(a);
            string da = pa != null ? Own(pa, summaries) : "";
            if (side.Groups[2].Value == "B" && da.Length > 0)
                return da.Replace("button A", "button B").Replace("command A", "command B").Replace("A (left button)", "B (right button)").Replace(" A movement", " B movement").Replace("ErrorA", "ErrorB");
            if (da.Length > 0 || side.Groups[2].Value == "A")
            {
                if (da.Length > 0) return da;
            }
            if (side.Groups[1].Value.StartsWith("Lamp") && side.Groups[3].Value == "Address")
                return "A sensor of the " + side.Groups[2].Value + " movement shown as a lamp on button " + side.Groups[2].Value + " (an end sensor) - the relay the input is read at (see section 4).";
            if (side.Groups[3].Value == "TipKey") return "Its tooltip (a translation key, or the text).";
            if (side.Groups[1].Value == "Command" && side.Groups[3].Value == "Action") return "How button " + side.Groups[2].Value + " writes its command bit: Momentary (while pressed), Alternate, SET, RESET.";
        }
        return Own(pi, summaries);
    }

    /// <summary>The code's own words for a property: its [Description], else its summary - the first sentences only.</summary>
    static string Own(PropertyInfo pi, Dictionary<string, string> summaries)
    {
        string text = "";
        var d = pi.GetCustomAttributes(typeof(DescriptionAttribute), true).OfType<DescriptionAttribute>().FirstOrDefault();
        if (d != null && !string.IsNullOrWhiteSpace(d.Description)) text = d.Description.Trim();
        else
            for (var t = pi.DeclaringType; t != null && t != typeof(object); t = t.BaseType)
            {
                string s;
                if (summaries.TryGetValue(t.Name + "." + pi.Name, out s)) { text = s; break; }
            }
        return Short(text);
    }

    /// <summary>The first sentences, up to about 240 characters (the rest is the code's history).</summary>
    static string Short(string text)
    {
        text = Regex.Replace(text ?? "", @"\s+", " ").Trim();
        if (text.Length <= 240) return text;
        int cut = -1;
        foreach (Match m in Regex.Matches(text, @"[.!?](\s|$)"))
        {
            if (m.Index + 1 > 240) break;
            cut = m.Index + 1;
        }
        return cut > 40 ? text.Substring(0, cut) : text.Substring(0, 237).TrimEnd() + "...";
    }

    static string Cell(string s)
    {
        s = Regex.Replace(s ?? "", @"\s+", " ").Trim().Replace("|", "\\|");
        return s.Length > 400 ? s.Substring(0, 397) + "..." : s;
    }

    /// <summary>The source's summaries of public properties: "Class.Property" (and "*.Property" for the first seen).</summary>
    static Dictionary<string, string> ReadSummaries(params string[] folders)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var classRx = new Regex(@"^\s*(?:public|internal)\s+(?:sealed\s+|abstract\s+|static\s+|partial\s+)*class\s+(\w+)");
        var propRx = new Regex(@"^\s*public\s+(?:override\s+|virtual\s+|new\s+)*[\w.<>\[\],?]+(?:\s*<[^>]+>)?\s+(\w+)\s*(?:\{|=>|$)");
        foreach (var folder in folders.Where(Directory.Exists))
            foreach (var file in Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                string cls = Path.GetFileNameWithoutExtension(file).Split('.')[0];
                var doc = new List<string>();
                foreach (var raw in File.ReadAllLines(file))
                {
                    var line = raw.Trim();
                    var cm = classRx.Match(raw);
                    if (cm.Success) { cls = cm.Groups[1].Value; doc.Clear(); continue; }
                    if (line.StartsWith("///")) { doc.Add(line.Substring(3).Trim()); continue; }
                    if (line.StartsWith("[") || line.Length == 0) continue;
                    var pm = propRx.Match(raw);
                    if (pm.Success && doc.Count > 0 && !raw.Contains("("))
                    {
                        var text = string.Join(" ", doc);
                        text = Regex.Replace(text, @"</?summary>|<see cref=""([^""]+)""\s*/>|</?(c|para|b|i)>", m => m.Groups[1].Success ? m.Groups[1].Value : "");
                        text = System.Net.WebUtility.HtmlDecode(Regex.Replace(text, @"<[^>]+>", "")).Trim();
                        if (text.Length > 0)
                        {
                            map[cls + "." + pm.Groups[1].Value] = text;
                            if (!map.ContainsKey("*." + pm.Groups[1].Value)) map["*." + pm.Groups[1].Value] = text;
                        }
                    }
                    doc.Clear();
                }
            }
        return map;
    }

    // ---- the real pages -----------------------------------------------------------------

    sealed class Instance { public string Type, Name, Page, Size; public List<string> Lines = new List<string>(); }

    static List<Instance> ReadInstances(IEnumerable<string> files)
    {
        var list = new List<Instance>();
        var decl = new Regex(@"^\s*this\.(\w+) = new Kaban\.UI\.Controls\.(\w+)\(\);");
        var bitDecl = new Regex(@"^\s*(bitAction\d+)\.");
        var addBit = new Regex(@"^\s*this\.(\w+)\.BitActions\.Add\((bitAction\d+)\);");
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            var byName = new Dictionary<string, Instance>();
            var bits = new Dictionary<string, List<string>>();
            string page = Path.GetFileName(Path.GetDirectoryName(file)) + "\\" + Path.GetFileName(file);
            foreach (var l in lines)
            {
                var m = decl.Match(l);
                if (m.Success) { byName[m.Groups[1].Value] = new Instance { Name = m.Groups[1].Value, Type = m.Groups[2].Value, Page = page }; continue; }
                var b = bitDecl.Match(l);
                if (b.Success && !l.Contains(" = new "))
                {
                    List<string> bl;
                    if (!bits.TryGetValue(b.Groups[1].Value, out bl)) bits[b.Groups[1].Value] = bl = new List<string>();
                    bl.Add(l);
                    continue;
                }
                var a = addBit.Match(l);
                if (a.Success && byName.ContainsKey(a.Groups[1].Value))
                {
                    List<string> bl;
                    if (bits.TryGetValue(a.Groups[2].Value, out bl))
                    {
                        byName[a.Groups[1].Value].Lines.Add("Kaban.Core.Models.BitAction " + a.Groups[2].Value + " = new Kaban.Core.Models.BitAction();   // declared at the top of InitializeComponent");
                        byName[a.Groups[1].Value].Lines.AddRange(bl);
                    }
                    byName[a.Groups[1].Value].Lines.Add(l);
                    continue;
                }
                foreach (var kv in byName)
                {
                    string head = "this." + kv.Key + ".";
                    if (!l.TrimStart().StartsWith(head)) continue;
                    kv.Value.Lines.Add(l);
                    var sz = Regex.Match(l, @"\.Size = new System\.Drawing\.Size\((\d+), (\d+)\)");
                    if (sz.Success) kv.Value.Size = sz.Groups[1].Value + " x " + sz.Groups[2].Value;
                    break;
                }
            }
            foreach (var i in byName.Values)
            {
                i.Lines.Insert(0, "this." + i.Name + " = new Kaban.UI.Controls." + i.Type + "();");
                list.Add(i);
            }
        }
        return list;
    }
}
