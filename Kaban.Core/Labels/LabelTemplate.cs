using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Kaban.Core.Helpers;

namespace Kaban.Core.Labels
{
    /// <summary>What a field of a label shows (the old KabanLabelDesigner's LabelFieldType, its numbers kept).</summary>
    public enum LabelFieldType { Text = 1, Barcode = 2, Picture = 3, YesNo = 4, DateTime = 5, QRCode = 6 }

    /// <summary>The printer's own language: EPL (the old program's - its Zebra printers) or ZPL (the newer Zebra printers).</summary>
    public enum LabelPrinterLanguage { EPL, ZPL }

    /// <summary>When a label is printed: a piece cut (the old main label) or a leftover (the old IsLeftOver label).</summary>
    public enum LabelUse { Piece, Leftover }

    /// <summary>
    /// A field of a label (the old LabelFieldProvider): a value of the piece (Key - "CutLength", "KabanBarcode" ...) or a fixed
    /// text (no Key: Text), where it is (X, Y mm from the label's top left corner), its font (1 - 5, the printer's fonts; 5 upper
    /// case only), a picture's size (Width, Height mm); a Yes / No value's texts (ValueOnTrue / ValueOnFalse); a date's format
    /// (ValueOnTrue: "dd.MM.yyyy HH:mm").
    /// </summary>
    public sealed class LabelField
    {
        public string Key = "", Text = "";
        public LabelFieldType Type = LabelFieldType.Text;
        public int X, Y, Width = 20, Height = 10, FontSize = 2;
        public string ValueOnTrue = "", ValueOnFalse = "";

        public bool IsDataField { get { return !string.IsNullOrEmpty(Key); } }
        public LabelField Clone() { return (LabelField)MemberwiseClone(); }
    }

    /// <summary>
    /// A LABEL (the old LabelHeader): its name, size (mm - the old "100x50"), printer - "COM3" (a serial printer, its baud
    /// rate), a Windows printer's name (sent raw, as the old RawPrinterHelper), "192.168.1.50:9100" (a network printer) or
    /// "file:C:\Labels\last.prn" (written to a file) - the text's language (the old Language: its code page and the
    /// printer's character set), EPL or ZPL, a ribbon (off: direct thermal - the old "OD"), when it is printed, on or off.
    /// </summary>
    public sealed class LabelTemplate
    {
        public string Name = "Label";
        public int WidthMm = 100, HeightMm = 50;
        public string Printer = "COM1";
        public int BaudRate = 9600;
        public string Language = "turkish";
        public LabelPrinterLanguage PrinterLanguage = LabelPrinterLanguage.EPL;
        public bool HasRibbon;
        public LabelUse Use = LabelUse.Piece;
        public bool Enabled = true;
        /// <summary>The machine (module id) it is for, "" every machine of the panel.</summary>
        public string Machine = "";
        /// <summary>How many of the label at each print (the old Machine Settings' Barcode Count).</summary>
        public int Copies = 1;
        public readonly List<LabelField> Fields = new List<LabelField>();

        public LabelTemplate Clone()
        {
            var c = new LabelTemplate
            {
                Name = Name, WidthMm = WidthMm, HeightMm = HeightMm, Printer = Printer, BaudRate = BaudRate, Language = Language,
                PrinterLanguage = PrinterLanguage, HasRibbon = HasRibbon, Use = Use, Enabled = Enabled, Machine = Machine, Copies = Copies
            };
            c.Fields.AddRange(Fields.Select(f => f.Clone()));
            return c;
        }

        /// <summary>The old program's default label: the profile, its lengths and angles, the order, the trolley, its barcode.</summary>
        public static LabelTemplate Sample(LabelUse use)
        {
            var t = new LabelTemplate { Name = use == LabelUse.Leftover ? "Leftover" : "Piece", Use = use, WidthMm = 100, HeightMm = 50 };
            if (use == LabelUse.Leftover)
            {
                t.Fields.Add(new LabelField { Text = "LEFTOVER", X = 4, Y = 3, FontSize = 4 });
                t.Fields.Add(new LabelField { Key = LabelKeys.ProfileCode, X = 4, Y = 12, FontSize = 3 });
                t.Fields.Add(new LabelField { Key = LabelKeys.Color, X = 50, Y = 12, FontSize = 3 });
                t.Fields.Add(new LabelField { Key = LabelKeys.LeftOverLength, X = 4, Y = 20, FontSize = 4 });
                t.Fields.Add(new LabelField { Key = LabelKeys.LeftOverBarcode, Type = LabelFieldType.Barcode, X = 4, Y = 30, FontSize = 1 });
                return t;
            }
            t.Fields.Add(new LabelField { Key = LabelKeys.ProfileCode, X = 3, Y = 2, FontSize = 3 });
            t.Fields.Add(new LabelField { Key = LabelKeys.Color, X = 45, Y = 2, FontSize = 2 });
            t.Fields.Add(new LabelField { Key = LabelKeys.CutLength, X = 3, Y = 9, FontSize = 4 });
            t.Fields.Add(new LabelField { Key = LabelKeys.LeftAngle, X = 45, Y = 9, FontSize = 2 });
            t.Fields.Add(new LabelField { Key = LabelKeys.RightAngle, X = 65, Y = 9, FontSize = 2 });
            t.Fields.Add(new LabelField { Key = LabelKeys.OrderNo, X = 3, Y = 16, FontSize = 2 });
            t.Fields.Add(new LabelField { Key = LabelKeys.Customer, X = 35, Y = 16, FontSize = 2 });
            t.Fields.Add(new LabelField { Key = LabelKeys.TrolleyName, X = 3, Y = 21, FontSize = 2 });
            t.Fields.Add(new LabelField { Key = LabelKeys.TrolleySection, X = 35, Y = 21, FontSize = 2 });
            t.Fields.Add(new LabelField { Key = LabelKeys.ProfilePosition, X = 65, Y = 21, FontSize = 2 });
            t.Fields.Add(new LabelField { Key = LabelKeys.KabanBarcode, Type = LabelFieldType.Barcode, X = 3, Y = 27, FontSize = 1 });
            return t;
        }
    }

    /// <summary>
    /// The values a label can show (the old LabelFieldProviderHelper's keys, their names kept so the old labels read the same):
    /// the piece's, its bar's, its job's, and the program's own (the date, the leftover's length).
    /// </summary>
    public static class LabelKeys
    {
        public const string BarNo = "BarNo", LabelNo = "LabelNo", Customer = "Customer", DealerName = "DealerName", OrderNo = "OrderNo",
            OrderLineNo = "OrderLineNo", ProfileCode = "ProfileCode", ProfileDescription = "ProfileDescription", CutLength = "CutLength",
            OtherLength = "OtherLength", LeftAngle = "LeftAngle", RightAngle = "RightAngle", ProfilePosition = "ProfilePosition",
            Accessories = "Accessories", TrolleyName = "TrolleyName", TrolleySection = "TrolleySection", Color = "Color",
            ReinforcementCode = "ReinforcementCode", ReinforcementLength = "ReinforcementLength", FrameNo = "FrameNo",
            UserBarcode = "UserBarcode", KabanBarcode = "KabanBarcode", UserComment1 = "UserComment1", UserComment2 = "UserComment2",
            UserComment3 = "UserComment3", Picture = "Picture", ManufactureDate = "ManufactureDate", LeftOverLength = "LeftOverLength",
            LeftOverBarcode = "LeftOverBarcode", PrintTime = "PrintTime", JobName = "JobName", MachineName = "MachineName";

        /// <summary>Every key, the field type it is usually shown as, its English name (translated: LblKey_&lt;key&gt;).</summary>
        public static readonly KeyValuePair<string, LabelFieldType>[] All =
        {
            K(BarNo), K(LabelNo), K(Customer), K(DealerName), K(OrderNo), K(OrderLineNo), K(ProfileCode), K(ProfileDescription),
            K(CutLength), K(OtherLength), K(LeftAngle), K(RightAngle), K(ProfilePosition), K(Accessories), K(TrolleyName),
            K(TrolleySection), K(Color), K(ReinforcementCode), K(ReinforcementLength), K(FrameNo), K(UserBarcode, LabelFieldType.Barcode),
            K(KabanBarcode, LabelFieldType.Barcode), K(UserComment1), K(UserComment2), K(UserComment3), K(Picture, LabelFieldType.Picture),
            K(ManufactureDate, LabelFieldType.DateTime), K(LeftOverLength), K(LeftOverBarcode, LabelFieldType.Barcode),
            K(PrintTime, LabelFieldType.DateTime), K(JobName), K(MachineName)
        };

        private static KeyValuePair<string, LabelFieldType> K(string k, LabelFieldType t = LabelFieldType.Text) { return new KeyValuePair<string, LabelFieldType>(k, t); }

        /// <summary>A key's name on the screen.</summary>
        public static string Caption(string key)
        {
            if (string.IsNullOrEmpty(key)) return LanguageManager.GetString("Lbl_FixedText", "Fixed text");
            return LanguageManager.GetString("LblKey_" + key, Words(key));
        }

        /// <summary>"CutLength" -> "Cut Length".</summary>
        internal static string Words(string key)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < key.Length; i++)
            {
                if (i > 0 && char.IsUpper(key[i]) && !char.IsUpper(key[i - 1])) sb.Append(' ');
                else if (i > 0 && char.IsDigit(key[i]) && !char.IsDigit(key[i - 1])) sb.Append(' ');
                sb.Append(key[i]);
            }
            return sb.ToString();
        }

        /// <summary>The values of a test label: every key a sample value.</summary>
        public static LabelValues Sample()
        {
            var v = new LabelValues();
            v[BarNo] = "12"; v[LabelNo] = "1203"; v[Customer] = "KABAN"; v[DealerName] = "Dealer"; v[OrderNo] = "ORD-2026-118";
            v[OrderLineNo] = "3"; v[ProfileCode] = "P1234"; v[ProfileDescription] = "Sash 70 mm"; v[CutLength] = "1250.5";
            v[OtherLength] = "1160.5"; v[LeftAngle] = "45"; v[RightAngle] = "45"; v[ProfilePosition] = "L"; v[Accessories] = "";
            v[TrolleyName] = "T1"; v[TrolleySection] = "4"; v[Color] = "White"; v[ReinforcementCode] = "R70";
            v[ReinforcementLength] = "1150"; v[FrameNo] = "7"; v[UserBarcode] = "123456789012"; v[UserComment1] = "Comment";
            v[UserComment2] = ""; v[UserComment3] = ""; v[LeftOverLength] = "655.0"; v[JobName] = "Sample job"; v[MachineName] = "Machine";
            v[KabanBarcode] = KabanCode("P1234", 12, 3);
            v[LeftOverBarcode] = LeftoverCode("P1234", 655);
            return v;
        }

        /// <summary>
        /// The piece's own code (the old KabanBarcode: the profile's code and the label's unique code): the profile code, the bar,
        /// the piece - "P1234-0012-03", the same each time the piece is printed (the old one added three random letters).
        /// </summary>
        public static string KabanCode(string profileCode, int bar, int piece)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1:0000}-{2:00}", (profileCode ?? "").Trim(), bar, piece);
        }

        /// <summary>A leftover's code: the profile code and the length in mm - "P1234-L0655" - read back as a bar of that length.</summary>
        public static string LeftoverCode(string profileCode, double lengthMm)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}-L{1:0000}", (profileCode ?? "").Trim(), Math.Round(lengthMm));
        }
    }

    /// <summary>A label's values by key (the piece's), any case.</summary>
    public sealed class LabelValues
    {
        private readonly Dictionary<string, string> _v = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        /// <summary>The folder a Picture value's file is in (the job file's).</summary>
        public string PictureFolder = "";

        public string this[string key]
        {
            get { string s; return key != null && _v.TryGetValue(key, out s) ? s ?? "" : ""; }
            set { if (key != null) _v[key] = value ?? ""; }
        }

        public bool Has(string key) { return key != null && _v.ContainsKey(key); }
        public IEnumerable<string> Keys { get { return _v.Keys; } }

        public LabelValues Clone()
        {
            var c = new LabelValues { PictureFolder = PictureFolder };
            foreach (var kv in _v) c._v[kv.Key] = kv.Value;
            return c;
        }

        /// <summary>"Key=Value" pairs, one per line (kept between restarts).</summary>
        public string Pack()
        {
            var sb = new StringBuilder();
            foreach (var kv in _v) sb.Append(kv.Key).Append('\u001f').Append((kv.Value ?? "").Replace('\u001e', ' ').Replace('\u001f', ' ').Replace('\r', ' ').Replace('\n', ' ')).Append('\u001e');
            if (PictureFolder.Length > 0) sb.Append("\u0001folder").Append('\u001f').Append(PictureFolder).Append('\u001e');
            return sb.ToString();
        }

        public static LabelValues Unpack(string s)
        {
            var v = new LabelValues();
            foreach (var p in (s ?? "").Split(new[] { '\u001e' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int i = p.IndexOf('\u001f');
                if (i <= 0) continue;
                string k = p.Substring(0, i), val = p.Substring(i + 1);
                if (k == "\u0001folder") v.PictureFolder = val; else v[k] = val;
            }
            return v;
        }
    }

    /// <summary>
    /// The labels kept (the old program's LabelHeader / LabelField tables): a file a label in C:\ProgramData\KabanHMI\Labels
    /// (Name.label - text, a line a field), for every machine of the panel.
    /// </summary>
    public static class LabelStore
    {
        /// <summary>Replaceable (tests).</summary>
        public static string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Labels");
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        public static event EventHandler Changed;

        public static List<LabelTemplate> All()
        {
            var list = new List<LabelTemplate>();
            try
            {
                if (!Directory.Exists(Folder)) return list;
                foreach (var f in Directory.GetFiles(Folder, "*.label").OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    var t = Read(f);
                    if (t != null) list.Add(t);
                }
            }
            catch (Exception ex) { AuditLogger.Log("[LABEL] " + Folder + ": " + ex.Message); }
            return list;
        }

        public static LabelTemplate Find(string name)
        {
            return All().FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static string FileOf(string name)
        {
            var bad = Path.GetInvalidFileNameChars();
            var n = new string((name ?? "").Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim();
            if (n.Length == 0) n = "Label";
            return Path.Combine(Folder, n + ".label");
        }

        /// <summary>The label written (its old name's file removed when it was renamed).</summary>
        public static void Save(LabelTemplate t, string oldName = null)
        {
            if (t == null) return;
            var sb = new StringBuilder();
            sb.AppendLine("# Kaban HMI label (HMI Settings > Labels)");
            sb.AppendLine("name\t" + Esc(t.Name));
            sb.AppendLine("size\t" + t.WidthMm.ToString(CI) + "\t" + t.HeightMm.ToString(CI));
            sb.AppendLine("printer\t" + Esc(t.Printer) + "\t" + t.BaudRate.ToString(CI));
            sb.AppendLine("language\t" + Esc(t.Language));
            sb.AppendLine("printerlanguage\t" + t.PrinterLanguage);
            sb.AppendLine("ribbon\t" + (t.HasRibbon ? "on" : "off"));
            sb.AppendLine("use\t" + t.Use);
            sb.AppendLine("enabled\t" + (t.Enabled ? "on" : "off"));
            sb.AppendLine("machine\t" + Esc(t.Machine));
            sb.AppendLine("copies\t" + Math.Max(1, t.Copies).ToString(CI));
            foreach (var f in t.Fields)
                sb.AppendLine(string.Join("\t", "field", Esc(f.Key), Esc(f.Text), f.Type.ToString(), f.X.ToString(CI), f.Y.ToString(CI),
                                          f.Width.ToString(CI), f.Height.ToString(CI), f.FontSize.ToString(CI), Esc(f.ValueOnTrue), Esc(f.ValueOnFalse)));
            string path = FileOf(t.Name);
            try
            {
                Directory.CreateDirectory(Folder);
                SafeFile.WriteAllText(path, sb.ToString());
                if (!string.IsNullOrEmpty(oldName) && !string.Equals(oldName, t.Name, StringComparison.OrdinalIgnoreCase))
                {
                    var old = FileOf(oldName);
                    if (File.Exists(old) && !string.Equals(old, path, StringComparison.OrdinalIgnoreCase)) File.Delete(old);
                }
                AuditLogger.Log("[LABEL] label " + t.Name + " saved (" + t.Fields.Count + " fields, " + t.Use + ", " + t.Printer + ").");
            }
            catch (Exception ex) { AuditLogger.Log("[LABEL] " + path + " could not be written: " + ex.Message); throw; }
            var h = Changed;
            if (h != null) h(null, EventArgs.Empty);
        }

        public static void Delete(string name)
        {
            try
            {
                var f = FileOf(name);
                if (File.Exists(f)) File.Delete(f);
                AuditLogger.Log("[LABEL] label " + name + " deleted.");
            }
            catch (Exception ex) { AuditLogger.Log("[LABEL] label " + name + " could not be deleted: " + ex.Message); }
            var h = Changed;
            if (h != null) h(null, EventArgs.Empty);
        }

        private static string Esc(string s) { return (s ?? "").Replace("\t", " ").Replace("\r", "").Replace("\n", " "); }

        internal static LabelTemplate Read(string path)
        {
            try
            {
                var t = new LabelTemplate { Name = Path.GetFileNameWithoutExtension(path) };
                foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (line.StartsWith("#", StringComparison.Ordinal)) continue;
                    var p = line.Split('\t');
                    int n;
                    switch (p[0])
                    {
                        case "name": if (p.Length > 1 && p[1].Trim().Length > 0) t.Name = p[1].Trim(); break;
                        case "size":
                            if (p.Length > 2 && int.TryParse(p[1], NumberStyles.Integer, CI, out n) && n > 0) t.WidthMm = n;
                            if (p.Length > 2 && int.TryParse(p[2], NumberStyles.Integer, CI, out n) && n > 0) t.HeightMm = n;
                            break;
                        case "printer":
                            if (p.Length > 1) t.Printer = p[1];
                            if (p.Length > 2 && int.TryParse(p[2], NumberStyles.Integer, CI, out n) && n > 0) t.BaudRate = n;
                            break;
                        case "language": if (p.Length > 1) t.Language = p[1]; break;
                        case "printerlanguage": { LabelPrinterLanguage l; if (p.Length > 1 && Enum.TryParse(p[1], out l)) t.PrinterLanguage = l; } break;
                        case "ribbon": t.HasRibbon = p.Length > 1 && p[1] == "on"; break;
                        case "use": { LabelUse u; if (p.Length > 1 && Enum.TryParse(p[1], out u)) t.Use = u; } break;
                        case "enabled": t.Enabled = p.Length > 1 && p[1] == "on"; break;
                        case "machine": if (p.Length > 1) t.Machine = p[1].Trim(); break;
                        case "copies": if (p.Length > 1 && int.TryParse(p[1], NumberStyles.Integer, CI, out n) && n > 0) t.Copies = Math.Min(99, n); break;
                        case "field":
                            if (p.Length < 9) break;
                            var f = new LabelField { Key = p[1], Text = p[2] };
                            LabelFieldType ft;
                            if (Enum.TryParse(p[3], out ft)) f.Type = ft;
                            if (int.TryParse(p[4], NumberStyles.Integer, CI, out n)) f.X = n;
                            if (int.TryParse(p[5], NumberStyles.Integer, CI, out n)) f.Y = n;
                            if (int.TryParse(p[6], NumberStyles.Integer, CI, out n)) f.Width = n;
                            if (int.TryParse(p[7], NumberStyles.Integer, CI, out n)) f.Height = n;
                            if (int.TryParse(p[8], NumberStyles.Integer, CI, out n)) f.FontSize = Math.Max(1, Math.Min(5, n));
                            if (p.Length > 9) f.ValueOnTrue = p[9];
                            if (p.Length > 10) f.ValueOnFalse = p[10];
                            t.Fields.Add(f);
                            break;
                    }
                }
                return t;
            }
            catch (Exception ex) { AuditLogger.Log("[LABEL] " + path + " could not be read: " + ex.Message); return null; }
        }
    }
}
