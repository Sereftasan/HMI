using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Kaban.Core.Machines;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// STRUCTURED DATA TYPES (19 October: "another label data type entry - a Structured Data
    /// Type in Mitsubishi literature: a complex array for the recipe steps. Each member of the
    /// array is a combination of data, each with its own data type and its index inside it,
    /// and the structure has its own length, so the next array member follows the previous
    /// one that many devices on - the milling steps' recipe is 16 devices long: R0, R16 ...
    /// It should be saved, and selected just like a simple data type later").
    ///
    ///     [FrezeAdim];16;Milling unit 1 - one recipe step
    ///     HareketKodu;Word [Signed];0;;Movement code
    ///     X;Double Word [Signed];1;;xxx.xx mm
    ///     ...
    ///     Hiz;Word [Signed];13;;xxx.xx mm/min
    ///
    /// A member is a simple data type (Bit, Word, Double Word, Float, String) at its OFFSET -
    /// the words from the structure's start; a Bit member is a bit of the word at its offset
    /// (R16.3). A structure inside a structure is not offered (the machines have none).
    /// Each machine keeps its own (StructuredTypes.csv, shipped with its project like PlcLabels.csv).
    /// </summary>
    public static class StructuredTypes
    {
        public sealed class Member
        {
            public string Name = "";
            /// <summary>A simple data type, as the PLC Monitor names them ("Double Word [Signed]", "String(32)").</summary>
            public string DataType = "Word [Signed]";
            /// <summary>Words from the structure's start.</summary>
            public int Offset;
            /// <summary>A Bit member: its bit of the word at Offset (0 - 15).</summary>
            public int Bit;
            public string Comment = "";

            public Member Clone() { return (Member)MemberwiseClone(); }

            public bool IsBit { get { return IsBitType(DataType); } }

            /// <summary>Words it takes: 1 for a bit (of its word) or a word, 2 for a double word or a float, a string's.</summary>
            public int Words { get { return WordsOf(DataType); } }

            /// <summary>Its place in the structure: "+1" - "+0.3" for a bit.</summary>
            public string Place
            {
                get { return "+" + Offset.ToString(CultureInfo.InvariantCulture) + (IsBit ? "." + Bit.ToString("X", CultureInfo.InvariantCulture) : ""); }
            }
        }

        public sealed class Type
        {
            public string Name = "";
            /// <summary>Devices (words) one element takes: the next element of an array starts that far on.</summary>
            public int Size;
            public string Comment = "";
            public readonly List<Member> Members = new List<Member>();

            /// <summary>The words its members reach: the smallest size it can have.</summary>
            public int MinimumSize { get { return Members.Count == 0 ? 1 : Members.Max(m => m.Offset + m.Words); } }

            /// <summary>Where a new member goes: right after the last one (a bit after the last bit of its word).</summary>
            public void NextPlace(string dataType, out int offset, out int bit)
            {
                bit = 0;
                offset = 0;
                if (Members.Count == 0) return;
                var last = Members.OrderBy(m => m.Offset).ThenBy(m => m.Bit).Last();
                if (IsBitType(dataType) && last.IsBit && last.Bit < 15) { offset = last.Offset; bit = last.Bit + 1; return; }
                offset = Members.Max(m => m.Offset + m.Words);
            }

            public Type Clone()
            {
                var t = new Type { Name = Name, Size = Size, Comment = Comment };
                t.Members.AddRange(Members.Select(m => m.Clone()));
                return t;
            }
        }

        // ---- the simple types a member can have ------------------------------------------

        /// <summary>The simple data types (the PLC Monitor's), a member's choices.</summary>
        public static readonly string[] SimpleTypes =
        {
            "Bit", "Word [Signed]", "Word [Unsigned]", "Double Word [Signed]", "Double Word [Unsigned]",
            "Float [Single Precision]", "String"
        };

        public static bool IsBitType(string dataType)
        {
            var d = (dataType ?? "").Trim();
            return d.Equals("Bit", StringComparison.OrdinalIgnoreCase) || d.Equals("Bool", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Words a simple type takes (a string's from its "(n)" characters, 16 when it does not say).</summary>
        public static int WordsOf(string dataType)
        {
            var d = (dataType ?? "").Trim().ToLowerInvariant();
            if (d.StartsWith("string"))
            {
                var m = Regex.Match(d, @"\((\d+)\)");
                int chars;
                if (!m.Success || !int.TryParse(m.Groups[1].Value, out chars) || chars <= 0) chars = 16;
                return (chars + 1) / 2;
            }
            if ((d.Contains("float") || d.Contains("real")) && !d.Contains("double precision")) return 2;
            if (d.Contains("double") || d.Contains("dword") || d.Contains("32")) return 2;
            return 1;
        }

        // ---- the library: one per machine ------------------------------------------------

        /// <summary>
        /// A machine's types (19 October: "they should belong to each machine - shipped with its
        /// project like the PLC labels"): beside its PlcLabels.csv - the main machine's beside the
        /// program, a mounted machine's in its own folder (H500\StructuredTypes.csv) - and, when the
        /// program runs from Visual Studio's build, also in the machine's project, so the next build
        /// and every install ship them.
        /// </summary>
        public const string FileName = "StructuredTypes.csv";

        /// <summary>
        /// Where the types are kept when no machine is mounted (the tests), and where the first
        /// version kept every machine's (the main machine takes those over). Replaceable for the tests.
        /// </summary>
        public static string FilePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Monitor", "StructuredTypes.cfg");

        private sealed class Library
        {
            /// <summary>The machine's id ("" with no machine mounted).</summary>
            public string Machine = "";
            /// <summary>The program's copy.</summary>
            public string Path = "";
            /// <summary>The machine project's own copy (null: not run from a build of it).</summary>
            public string ProjectPath;
            public bool IsMain;
            public List<Type> Types;
        }

        private static readonly object _lock = new object();
        private static readonly Dictionary<string, Library> _libraries = new Dictionary<string, Library>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raised after the types were saved (a list showing them names them anew).</summary>
        public static event EventHandler Changed;

        /// <summary>Every type of the station's machine (copies are not made: change them through Save).</summary>
        public static IList<Type> All(int station = 0)
        {
            lock (_lock) return LibraryFor(station).Types.AsReadOnly();
        }

        public static IEnumerable<string> Names(int station = 0) { return All(station).Select(t => t.Name); }

        /// <summary>The machine whose types the station uses ("" with no machine mounted).</summary>
        public static string MachineOf(int station = 0)
        {
            lock (_lock) return LibraryFor(station).Machine;
        }

        /// <summary>The file the station's machine keeps its types in (the program's copy).</summary>
        public static string PathOf(int station = 0)
        {
            lock (_lock) return LibraryFor(station).Path;
        }

        /// <summary>The machine project's copy (null when the program does not run from its build).</summary>
        public static string ProjectPathOf(int station = 0)
        {
            lock (_lock) return LibraryFor(station).ProjectPath;
        }

        /// <summary>
        /// The structure a data type names on the station's machine: "FrezeAdim", or the label
        /// list's array of it "FrezeAdim(0..19)"; null for a simple type or one not defined.
        /// Station 0: the main machine's.
        /// </summary>
        public static Type Find(string dataType, int station = 0)
        {
            var name = BaseName(dataType);
            if (name.Length == 0) return null;
            lock (_lock)
                return LibraryFor(station).Types.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The type without the label list's array range: "FrezeAdim(0..19)" - "FrezeAdim".</summary>
        public static string BaseName(string dataType)
        {
            var d = (dataType ?? "").Trim();
            var m = Regex.Match(d, @"^(.*?)\s*\(\s*\d+\s*\.\.\s*\d+\s*\)$");
            return m.Success ? m.Groups[1].Value.Trim() : d;
        }

        /// <summary>The elements the label list's array range gives: "(0..19)" - 20; 0 without one.</summary>
        public static int CountIn(string dataType)
        {
            var m = Regex.Match((dataType ?? "").Trim(), @"\(\s*(\d+)\s*\.\.\s*(\d+)\s*\)$");
            int a, b;
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out a) || !int.TryParse(m.Groups[2].Value, out b) || b < a) return 0;
            return b - a + 1;
        }

        /// <summary>Reads the files again (the tests, a file changed by hand, machines mounted anew).</summary>
        public static void Reload()
        {
            lock (_lock) _libraries.Clear();
        }

        /// <summary>Keeps the station's machine's types (replacing all of them) and tells whoever shows them.</summary>
        public static void Save(IEnumerable<Type> types, int station = 0)
        {
            var list = (types ?? Enumerable.Empty<Type>()).Select(t => t.Clone()).ToList();
            lock (_lock)
            {
                var lib = LibraryFor(station);
                Write(lib, list);
                lib.Types = list;
            }
            var h = Changed;
            if (h != null) h(null, EventArgs.Empty);
        }

        /// <summary>The station's machine: the mounted one using it, the main machine for the others (and station 0).</summary>
        private static MachineRegistry.MountedModule ModuleOf(int station)
        {
            var modules = MachineRegistry.Modules;
            if (modules.Count == 0) return null;
            if (station > 0)
                foreach (var m in modules.OrderBy(m => m.IsMain))
                    if (m.Context != null && m.Context.MappedStations.Any(kv => kv.Value == station)) return m;
            return MachineRegistry.Main ?? modules[0];
        }

        private static Library LibraryFor(int station)
        {
            var module = ModuleOf(station);
            string path;
            if (module == null || module.Context == null) path = FilePath;
            else
            {
                var id = module.Context.ModuleId ?? "";
                path = module.IsMain ? Path.Combine(module.Context.BaseFolder, FileName) : Path.Combine(module.Context.BaseFolder, id, FileName);
            }
            Library lib;
            if (!_libraries.TryGetValue(path, out lib))
            {
                lib = new Library { Path = path };
                if (module != null && module.Context != null)
                {
                    lib.Machine = module.Context.ModuleId ?? "";
                    lib.IsMain = module.IsMain;
                    var project = ProjectFolderOf(module.Module, module.Context.BaseFolder);
                    if (project != null) lib.ProjectPath = Path.Combine(project, FileName);
                    if (lib.ProjectPath != null && SamePath(lib.ProjectPath, lib.Path)) lib.ProjectPath = null;
                }
                _libraries[path] = lib;
            }
            if (lib.Types == null) Load(lib);
            return lib;
        }

        private static void Load(Library lib)
        {
            lib.Types = new List<Type>();
            try
            {
                var from = Newer(lib.Path, lib.ProjectPath);
                if (from != null) { lib.Types = Parse(File.ReadAllLines(from)); return; }
                // the first version's file, every machine's types in one: the main machine's now
                if (lib.IsMain && File.Exists(FilePath) && !SamePath(FilePath, lib.Path))
                {
                    var taken = Parse(File.ReadAllLines(FilePath));
                    if (taken.Count == 0) return;
                    lib.Types = taken;
                    Write(lib, taken);
                    AuditLogger.Log("the structured data types of " + FilePath + " are the main machine's now: " + lib.Path);
                }
            }
            catch (Exception ex) { AuditLogger.Log("could not read the structured data types: " + ex.Message); }
        }

        /// <summary>The program's copy (it has to be written), then the project's (a failure there is only logged).</summary>
        private static void Write(Library lib, List<Type> types)
        {
            var text = string.Join("\r\n", Serialize(types)) + "\r\n";
            Directory.CreateDirectory(Path.GetDirectoryName(lib.Path));
            SafeFile.WriteAllText(lib.Path, text, new UTF8Encoding(true));
            if (string.IsNullOrEmpty(lib.ProjectPath)) return;
            try { SafeFile.WriteAllText(lib.ProjectPath, text, new UTF8Encoding(true)); }
            catch (Exception ex) { AuditLogger.Log("could not write the structured data types into the project (" + lib.ProjectPath + "): " + ex.Message); }
        }

        /// <summary>The newer of two files (either may be missing; null: neither is there).</summary>
        private static string Newer(string a, string b)
        {
            bool hasA = !string.IsNullOrEmpty(a) && File.Exists(a), hasB = !string.IsNullOrEmpty(b) && File.Exists(b);
            if (hasA && hasB) return File.GetLastWriteTimeUtc(b) > File.GetLastWriteTimeUtc(a) ? b : a;
            return hasA ? a : hasB ? b : null;
        }

        private static bool SamePath(string a, string b)
        {
            try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        /// <summary>
        /// The machine's project folder when the program runs from Visual Studio's build of it: the
        /// folder with the machine assembly's .csproj, the build's folder or one above it - the
        /// main machine's bin\Debug is two below its project, a mounted machine's project its
        /// neighbour (Kaban.Machines.FA2010\bin\Debug - ..\..\..\Kaban.Machines.H500).
        /// </summary>
        public static string ProjectFolderOf(object module, string buildFolder)
        {
            if (module == null || string.IsNullOrEmpty(buildFolder)) return null;
            try
            {
                var asm = module.GetType().Assembly.GetName().Name;
                var dir = new DirectoryInfo(buildFolder);
                for (int i = 0; dir != null && i < 6; i++, dir = dir.Parent)
                {
                    if (File.Exists(Path.Combine(dir.FullName, asm + ".csproj"))) return dir.FullName;
                    var inner = Path.Combine(dir.FullName, asm);
                    if (File.Exists(Path.Combine(inner, asm + ".csproj"))) return inner;
                }
            }
            catch { }
            return null;
        }

        // ---- the file --------------------------------------------------------------------

        public static List<string> Serialize(IEnumerable<Type> types)
        {
            var lines = new List<string>
            {
                "# Kaban HMI structured data types - the PLC Monitor's, chosen like a simple data type.",
                "# [Name];Size in devices;Comment - then one member a line: Name;DataType;Offset (words from the start);Bit (a Bit member's, 0-15);Comment"
            };
            foreach (var t in types)
            {
                lines.Add("[" + Clean(t.Name) + "];" + t.Size.ToString(CultureInfo.InvariantCulture) + ";" + Clean(t.Comment));
                foreach (var m in t.Members)
                    lines.Add(string.Join(";", Clean(m.Name), Clean(m.DataType), m.Offset.ToString(CultureInfo.InvariantCulture),
                                          m.IsBit ? m.Bit.ToString(CultureInfo.InvariantCulture) : "", Clean(m.Comment)));
            }
            return lines;
        }

        public static List<Type> Parse(IEnumerable<string> lines)
        {
            var list = new List<Type>();
            Type current = null;
            foreach (var raw in lines)
            {
                var line = (raw ?? "").Trim().TrimStart('﻿');
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var f = line.Split(';');
                int n;
                if (f[0].StartsWith("[") && f[0].EndsWith("]"))
                {
                    current = new Type { Name = f[0].Substring(1, f[0].Length - 2).Trim() };
                    if (f.Length > 1 && int.TryParse(f[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) current.Size = n;
                    if (f.Length > 2) current.Comment = f[2].Trim();
                    if (current.Name.Length > 0) list.Add(current);
                    continue;
                }
                if (current == null || f.Length < 3) continue;
                var m = new Member { Name = f[0].Trim(), DataType = f[1].Trim() };
                if (int.TryParse(f[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) m.Offset = Math.Max(0, n);
                if (f.Length > 3 && int.TryParse(f[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) m.Bit = Math.Max(0, Math.Min(15, n));
                if (f.Length > 4) m.Comment = string.Join(";", f.Skip(4)).Trim();
                current.Members.Add(m);
            }
            foreach (var t in list) if (t.Size < t.MinimumSize) t.Size = t.MinimumSize;
            return list;
        }

        private static string Clean(string s) { return (s ?? "").Replace(";", ",").Replace("\r", " ").Replace("\n", " ").Trim(); }

        // ---- checks ----------------------------------------------------------------------

        /// <summary>What is wrong with a type among the others (null: nothing).</summary>
        public static string Problem(Type t, IEnumerable<Type> all)
        {
            if (t == null) return null;
            var name = (t.Name ?? "").Trim();
            if (name.Length == 0) return "The structure needs a name.";
            if (Regex.IsMatch(name, @"[;\[\]()|.@]")) return "A structure's name cannot have ; [ ] ( ) | . or @ in it.";
            if (SimpleTypes.Any(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase)) || name.StartsWith("String", StringComparison.OrdinalIgnoreCase))
                return "'" + name + "' is a simple data type's name.";
            if (all != null && all.Count(o => !ReferenceEquals(o, t) && string.Equals((o.Name ?? "").Trim(), name, StringComparison.OrdinalIgnoreCase)) > 0)
                return "There is another structure named '" + name + "'.";
            if (t.Members.Count == 0) return "'" + name + "' has no labels.";
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in t.Members)
            {
                if ((m.Name ?? "").Trim().Length == 0) return "Every label of '" + name + "' needs a name.";
                if (Regex.IsMatch(m.Name, @"[;\[\]()|.@]")) return "A label's name cannot have ; [ ] ( ) | . or @ in it: '" + m.Name + "'.";
                if (!seen.Add(m.Name.Trim())) return "Two labels of '" + name + "' are named '" + m.Name.Trim() + "'.";
                if (!IsSimple(m.DataType)) return "Label '" + m.Name + "': a simple data type only (" + m.DataType + ").";
                if (m.Offset < 0) return "Label '" + m.Name + "': its offset cannot be below 0.";
                if (m.IsBit && (m.Bit < 0 || m.Bit > 15)) return "Label '" + m.Name + "': a bit of its word is 0 - 15.";
            }
            for (int i = 0; i < t.Members.Count; i++)
                for (int j = i + 1; j < t.Members.Count; j++)
                    if (Overlap(t.Members[i], t.Members[j]))
                        return "Labels '" + t.Members[i].Name + "' (" + t.Members[i].Place + ") and '" + t.Members[j].Name + "' (" + t.Members[j].Place + ") use the same device.";
            if (t.Size < t.MinimumSize)
            {
                // the label that reaches furthest, named: the one just added, or the one just made longer
                var far = t.Members.OrderByDescending(m => m.Offset + m.Words).First();
                return "'" + name + "' is " + t.Size + " devices long, but its labels reach " + t.MinimumSize +
                       " ('" + far.Name + "' at " + far.Place + (far.Words > 1 ? ", " + far.Words + " devices" : "") + "): make it longer, or move the label.";
            }
            return null;
        }

        /// <summary>A simple type the members can have ("String(32)" with its length too).</summary>
        public static bool IsSimple(string dataType)
        {
            var d = (dataType ?? "").Trim();
            if (d.StartsWith("String", StringComparison.OrdinalIgnoreCase)) return Regex.IsMatch(d, @"^String(\(\d+\))?$", RegexOptions.IgnoreCase);
            return SimpleTypes.Any(s => string.Equals(s, d, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Two members on the same device (two bits of one word only when it is the same bit).</summary>
        public static bool Overlap(Member a, Member b)
        {
            if (a.IsBit && b.IsBit) return a.Offset == b.Offset && a.Bit == b.Bit;
            return a.Offset < b.Offset + b.Words && b.Offset < a.Offset + a.Words;
        }
    }
}
