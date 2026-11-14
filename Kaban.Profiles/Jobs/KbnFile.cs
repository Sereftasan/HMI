using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Kaban.Profiles.Jobs
{
    /// <summary>An accessory on a piece: "Rec;code;position" - the code of an accessory of the profile's group, where it goes along the piece.</summary>
    public sealed class KbnRec
    {
        public string Code = "";
        /// <summary>Millimetres from the piece's start (the file has tenths).</summary>
        public double Position;
        public int LineNo;
        public string Line = "";
    }

    /// <summary>
    /// A V cut on a piece: "VCut;position x10;depth" - the V saw's cut at that place from the piece's start (the old program's VCutting: the
    /// depth is kept, the PLC gets the code 3000 at the position).
    /// </summary>
    public sealed class KbnVCut
    {
        public double Position, Depth;
        public int LineNo;
        public string Line = "";
    }

    /// <summary>
    /// A piece's special ends ("&lt;&gt;" cuts): "SpecialCut;left code;right code;left value x10;right value x10" - the code of a special cut
    /// at either end (0: that end is an ordinary cut), and the value its recipe's formulas take as L and R (the old program's SpecialCut).
    /// </summary>
    public sealed class KbnSpecialCut
    {
        public string LeftCode = "0", RightCode = "0";
        public double LeftParam, RightParam;
        public int LineNo;
        public string Line = "";
        public bool LeftSpecial { get { return LeftCode.Length > 0 && LeftCode != "0"; } }
        public bool RightSpecial { get { return RightCode.Length > 0 && RightCode != "0"; } }
    }

    /// <summary>A piece to cut from the bar: "Profile;..." and the lines under it.</summary>
    public sealed class KbnPiece
    {
        public double CutLength, OtherLength, LeftAngle, RightAngle;
        public string TrolleyName = "", TrolleySection = "", OrderNo = "", OrderLineNo = "", Customer = "", Position = "", Accessories = "";
        public string FrameNo = "", ReinforcementCode = "", Barcode = "", Comment1 = "", Comment2 = "", Comment3 = "", Picture = "", Dealer = "";
        public double ReinforcementLength, LeftPivot, RightPivot, MidPoint;
        public bool ReverseCut;
        public int Quantity = 1;
        public readonly List<KbnRec> Recs = new List<KbnRec>();
        /// <summary>The piece's V cuts, its special ends (null: none).</summary>
        public readonly List<KbnVCut> VCuts = new List<KbnVCut>();
        public KbnSpecialCut Special;
        /// <summary>The lines under the piece the HMI does not take yet (Screw, Hole ...) - kept, and shown.</summary>
        public readonly List<string> Other = new List<string>();
        public int LineNo;
        public string Line = "";
    }

    /// <summary>A bar: "Bar;no;profile code;name;quantity;colour;length;leftover;" and its pieces.</summary>
    public sealed class KbnBar
    {
        public int No;
        public string Code = "", Name = "", Color = "";
        /// <summary>How many such bars the file asks for; each is a bar of its own here (<see cref="Copy"/> 1 ... Quantity).</summary>
        public int Quantity = 1, Copy = 1;
        /// <summary>The bar's length in mm (the file has tenths).</summary>
        public double Length;
        public string Leftover = "";
        public readonly List<KbnPiece> Pieces = new List<KbnPiece>();
        public int LineNo;
        public string Line = "";
        public string Title { get { return No.ToString(CultureInfo.InvariantCulture) + (Quantity > 1 ? " (" + Copy + "/" + Quantity + ")" : ""); } }
    }

    /// <summary>A job file: its bars, in order, and what could not be read.</summary>
    public sealed class KbnJob
    {
        public string Path = "", Name = "";
        public readonly List<KbnBar> Bars = new List<KbnBar>();
        /// <summary>Lines that could not be read: "line 12: ...".</summary>
        public readonly List<string> Problems = new List<string>();
        /// <summary>The file's "#" lines.</summary>
        public readonly List<string> Comments = new List<string>();
    }

    /// <summary>
    /// THE JOB FILE (.kbn) the window software writes for the machine - the old program's KabanFileReader and KBNFileCreator
    /// (6 November: "They are send to the machine when the 'Send to the machine' button clicked for the job on the job list ... you can
    /// create it now using our old program, the attached file is the sample job list"). Lines of fields split by ';', lengths and angles
    /// in tenths:
    ///
    ///   Bar;no;profile code;name;quantity;colour;length x10;leftover;
    ///   Profile;length x10;other length x10;left angle x10;right angle x10;trolley;section;order no;order line;customer;position;
    ///           accessories;frame no;reinforcement;reinforcement length x10;barcode;comment 1;comment 2;comment 3;picture;dealer;
    ///           reverse cut;left pivot x10;right pivot x10;quantity;;mid point x10
    ///   Rec;accessory code;position x10            (from the start of the piece above it)
    ///   VCut;position x10;depth                     (a V cut of the piece)
    ///   SpecialCut;left code;right code;left value x10;right value x10   (its special ends, 0 an ordinary one)
    ///
    /// A bar of quantity N is N bars (the old program wrote the file so). Lines starting "//" and empty ones are left out, "#" ones kept.
    /// </summary>
    public static class KbnFile
    {
        public const double Scale = 10;
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        /// <summary>The job in the file (its encoding: UTF-8 if it is that, else the PC's code page - Turkish names).</summary>
        public static KbnJob Read(string path)
        {
            byte[] b = File.ReadAllBytes(path);
            string text;
            try { text = new UTF8Encoding(false, true).GetString(b); }
            catch (DecoderFallbackException) { text = Encoding.Default.GetString(b); }
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
            var job = Parse(text.Split('\n').Select(l => l.TrimEnd('\r')), System.IO.Path.GetFileNameWithoutExtension(path));
            job.Path = path;
            return job;
        }

        public static KbnJob Parse(IEnumerable<string> lines, string name)
        {
            var job = new KbnJob { Name = name ?? "" };
            var read = new List<KbnBar>();
            KbnBar bar = null;
            KbnPiece piece = null;
            bool skip = false, skipPiece = false;
            int no = 0;
            foreach (var raw in lines)
            {
                no++;
                string line = (raw ?? "").Trim();
                if (line.Length == 0 || line.StartsWith("//")) continue;
                if (line.StartsWith("#", System.StringComparison.Ordinal)) { job.Comments.Add(line); continue; }
                var t = line.Split(';');
                string kind = t[0].Trim().ToLowerInvariant();
                int eq = kind.IndexOf('=');
                if (eq >= 0) kind = kind.Substring(0, eq);
                try
                {
                    switch (kind)
                    {
                        case "bar":
                            // a bar that cannot be read: its pieces are left out with it (one problem, not one a line)
                            bar = null;
                            piece = null;
                            skip = true;
                            if (t.Length < 7) throw new FormatException("a bar needs 7 fields - no, profile code, name, quantity, colour, length, leftover");
                            var nb = new KbnBar
                            {
                                No = Int(t, 1), Code = Str(t, 2), Name = Str(t, 3), Quantity = Math.Max(1, Int(t, 4, 1)), Color = Str(t, 5),
                                Length = Num(t, 6) / Scale, Leftover = Str(t, 7), LineNo = no, Line = line
                            };
                            if (nb.Code.Length == 0) throw new FormatException("the bar has no profile code");
                            if (nb.Length <= 0) throw new FormatException("the bar has no length");
                            bar = nb;
                            skip = false;
                            read.Add(bar);
                            break;
                        case "profile":
                            piece = null;
                            if (bar == null && skip) { skipPiece = true; break; }
                            skipPiece = true;
                            if (bar == null) throw new FormatException("a piece before any bar");
                            if (t.Length < 5) throw new FormatException("a piece needs its length and both angles");
                            var np = new KbnPiece
                            {
                                CutLength = Num(t, 1) / Scale, OtherLength = Num(t, 2) / Scale, LeftAngle = Num(t, 3) / Scale, RightAngle = Num(t, 4) / Scale,
                                TrolleyName = Str(t, 5), TrolleySection = Str(t, 6), OrderNo = Str(t, 7), OrderLineNo = Str(t, 8), Customer = Str(t, 9),
                                Position = Str(t, 10), Accessories = Str(t, 11), FrameNo = Str(t, 12), ReinforcementCode = Str(t, 13),
                                ReinforcementLength = Num(t, 14, 0) / Scale, Barcode = Str(t, 15), Comment1 = Str(t, 16), Comment2 = Str(t, 17),
                                Comment3 = Str(t, 18), Picture = Str(t, 19), Dealer = Str(t, 20), ReverseCut = Bool(t, 21),
                                LeftPivot = Num(t, 22, 0) / Scale, RightPivot = Num(t, 23, 0) / Scale, Quantity = Int(t, 24, 1), MidPoint = Num(t, 26, 0) / Scale,
                                LineNo = no, Line = line
                            };
                            if (np.CutLength <= 0) throw new FormatException("the piece has no length (its accessories are left out with it)");
                            if (np.LeftAngle <= 0 || np.LeftAngle >= 180 || np.RightAngle <= 0 || np.RightAngle >= 180)
                                throw new FormatException("an angle out of 0 - 180 degrees (the piece's accessories are left out with it)");
                            piece = np;
                            skipPiece = false;
                            bar.Pieces.Add(piece);
                            break;
                        case "rec":
                            if (piece == null && skipPiece) break;
                            if (piece == null) throw new FormatException("an accessory before any piece");
                            if (t.Length < 3) throw new FormatException("an accessory needs its code and position");
                            var rec = new KbnRec { Code = Str(t, 1), Position = Num(t, 2) / Scale, LineNo = no, Line = line };
                            if (rec.Code.Length == 0) throw new FormatException("the accessory has no code");
                            piece.Recs.Add(rec);
                            break;
                        case "vcut":
                            // the old program's VCutting: the position in tenths, its depth as written
                            if (piece == null && skipPiece) break;
                            if (piece == null) throw new FormatException("a V cut before any piece");
                            if (t.Length < 2 || Str(t, 1).Length == 0) throw new FormatException("a V cut needs its position");
                            piece.VCuts.Add(new KbnVCut { Position = Num(t, 1) / Scale, Depth = Num(t, 2, 0), LineNo = no, Line = line });
                            break;
                        case "specialcut":
                            if (piece == null && skipPiece) break;
                            if (piece == null) throw new FormatException("a special cut before any piece");
                            if (t.Length < 3) throw new FormatException("a special cut needs its left and right codes");
                            if (piece.Special != null) throw new FormatException("the piece has a special cut already");
                            var sc = new KbnSpecialCut
                            {
                                LeftCode = Code(Str(t, 1)), RightCode = Code(Str(t, 2)), LeftParam = Num(t, 3, 0) / Scale, RightParam = Num(t, 4, 0) / Scale,
                                LineNo = no, Line = line
                            };
                            double code;
                            foreach (var c in new[] { sc.LeftCode, sc.RightCode })
                                if (!double.TryParse(c, NumberStyles.Float, CI, out code)) throw new FormatException("the special cut's code \"" + c + "\" is not a number");
                            piece.Special = sc;
                            break;
                        default:
                            // Screw, Hole ...: kept with the piece
                            if (piece == null && skipPiece) break;
                            if (piece == null) throw new FormatException("\"" + t[0] + "\" is not a line of a job file");
                            piece.Other.Add(line);
                            break;
                    }
                }
                catch (FormatException ex)
                {
                    job.Problems.Add(string.Format(CI, "line {0}: {1} ({2})", no, ex.Message, line.Length > 60 ? line.Substring(0, 60) + "..." : line));
                }
            }
            foreach (var b in read)
                for (int i = 1; i <= b.Quantity; i++)
                    job.Bars.Add(WithCopy(b, i));
            return job;
        }

        /// <summary>The bar as copy <paramref name="copy"/> of its quantity (its pieces are the same objects: they are only read).</summary>
        private static KbnBar WithCopy(KbnBar b, int copy)
        {
            if (copy == 1) return b;
            var c = new KbnBar { No = b.No, Code = b.Code, Name = b.Name, Quantity = b.Quantity, Copy = copy, Color = b.Color, Length = b.Length, Leftover = b.Leftover, LineNo = b.LineNo, Line = b.Line };
            c.Pieces.AddRange(b.Pieces);
            return c;
        }

        // ---- writing (the sample job of the library) ----------------------------------------------------

        /// <summary>The file's text of <paramref name="bars"/> (a bar of quantity N written once; CRLF, as the window software writes).</summary>
        public static string Format(IEnumerable<KbnBar> bars, IEnumerable<string> comments = null)
        {
            var sb = new StringBuilder();
            foreach (var c in comments ?? new string[0]) sb.Append(c.StartsWith("#", System.StringComparison.Ordinal) ? c : "# " + c).Append("\r\n");
            bool first = true;
            foreach (var b in bars.Where(x => x.Copy == 1))
            {
                if (!first) sb.Append(" \r\n");
                first = false;
                sb.Append(string.Join(";", new[] { "Bar", b.No.ToString(CI), b.Code, b.Name, b.Quantity.ToString(CI), b.Color, Tenths(b.Length), b.Leftover.Length > 0 ? b.Leftover : "0", "" })).Append("\r\n");
                foreach (var p in b.Pieces)
                {
                    sb.Append(string.Join(";", new[]
                    {
                        "Profile", Tenths(p.CutLength), Tenths(p.OtherLength), Angle(p.LeftAngle), Angle(p.RightAngle), p.TrolleyName, p.TrolleySection,
                        p.OrderNo, p.OrderLineNo, p.Customer, p.Position, p.Accessories, p.FrameNo, p.ReinforcementCode, Tenths(p.ReinforcementLength),
                        p.Barcode, p.Comment1, p.Comment2, p.Comment3, p.Picture, p.Dealer, ""
                    })).Append("\r\n");
                    foreach (var r in p.Recs) sb.Append("Rec;").Append(r.Code).Append(';').Append(Tenths(r.Position)).Append(";\r\n");
                    foreach (var v in p.VCuts) sb.Append("VCut;").Append(Tenths(v.Position)).Append(';').Append(v.Depth.ToString("0.###", CI)).Append(";\r\n");
                    if (p.Special != null)
                        sb.Append(string.Join(";", new[] { "SpecialCut", p.Special.LeftCode, p.Special.RightCode, Tenths(p.Special.LeftParam), Tenths(p.Special.RightParam) })).Append("\r\n");
                    foreach (var o in p.Other) sb.Append(o).Append("\r\n");
                }
            }
            return sb.ToString();
        }

        private static string Tenths(double mm) { return ((long)Math.Round(mm * Scale, MidpointRounding.AwayFromZero)).ToString(CI); }
        /// <summary>An angle as the window software writes it: four digits ("0450").</summary>
        private static string Angle(double deg) { return ((long)Math.Round(deg * Scale, MidpointRounding.AwayFromZero)).ToString("0000", CI); }

        // ---- fields -------------------------------------------------------------------------------------

        private static string Str(string[] t, int i) { return i < t.Length ? t[i].Trim() : ""; }

        /// <summary>A special cut's code as written ("" and "0" alike: none).</summary>
        private static string Code(string s) { return s.Length == 0 ? "0" : s.Replace(',', '.'); }

        private static double Num(string[] t, int i, double? missing = null)
        {
            string s = Str(t, i);
            if (s.Length == 0)
            {
                if (missing.HasValue) return missing.Value;
                throw new FormatException("field " + i + " is empty");
            }
            double v;
            if (double.TryParse(s.Replace(',', '.'), NumberStyles.Float, CI, out v)) return v;
            if (missing.HasValue) return missing.Value;
            throw new FormatException("field " + i + " (\"" + s + "\") is not a number");
        }

        private static int Int(string[] t, int i, int? missing = null)
        {
            string s = Str(t, i);
            int v;
            if (int.TryParse(s, NumberStyles.Integer, CI, out v)) return v;
            if (missing.HasValue) return missing.Value;
            throw new FormatException("field " + i + " (\"" + s + "\") is not a whole number");
        }

        private static bool Bool(string[] t, int i)
        {
            string s = Str(t, i).ToLowerInvariant();
            return s == "1" || s == "true" || s == "yes" || s == "evet";
        }
    }
}
