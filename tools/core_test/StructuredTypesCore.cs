using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;

// 19 October: "a Structured Data Type in Mitsubishi literature - each member of the array is a
// combination of data, each with its own data type and index; the structure has its own length,
// so the next member follows that many devices on (the milling steps' recipe: 16 devices, R0, R16)".
class STC {
    static int fail = 0;
    class Mod : IMachineModule {
        public string Id;
        public string ModuleId { get { return Id; } }
        public string DisplayNameKey { get { return Id; } }
        public void RegisterPlc(IMachineMountContext ctx) { ctx.Station(1); }
        public void RegisterConditions(IMachineMountContext ctx) { }
        public void RegisterData(IMachineMountContext ctx) { }
        public IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx) { yield break; }
    }
    static void Check(string w, bool ok, string d = "") { d = d ?? ""; Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }

    /// <summary>The picture's milling step: a movement code, X Y Z I J K (32 bit), a speed (16 bit) - 16 devices.</summary>
    static StructuredTypes.Type Milling()
    {
        var t = new StructuredTypes.Type { Name = "FrezeAdim", Size = 16, Comment = "Unit 1 milling step" };
        t.Members.Add(new StructuredTypes.Member { Name = "HareketKodu", DataType = "Word [Signed]", Offset = 0, Comment = "Integer" });
        int o = 1;
        foreach (var n in new[] { "X", "Y", "Z", "I", "J", "K" })
        {
            t.Members.Add(new StructuredTypes.Member { Name = n, DataType = "Double Word [Signed]", Offset = o, Comment = "xxx.xx mm" });
            o += 2;
        }
        t.Members.Add(new StructuredTypes.Member { Name = "Hiz", DataType = "Word [Signed]", Offset = 13, Comment = "xxx.xx mm (16 bit)" });
        return t;
    }

    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "struct_" + Guid.NewGuid().ToString("N"));
        StructuredTypes.FilePath = Path.Combine(dir, "StructuredTypes.cfg");

        Console.WriteLine("--- a structure: its members at their offsets, its own length ---");
        var t = Milling();
        Check("the milling step: 8 members, its members reach 14 devices, 16 long", t.Members.Count == 8 && t.MinimumSize == 14 && t.Size == 16);
        Check("  complete as it is", StructuredTypes.Problem(t, new[] { t }) == null, StructuredTypes.Problem(t, new[] { t }));
        Check("a member's width: a word 1, a double word and a float 2, a 32 character string 16",
              StructuredTypes.WordsOf("Word [Signed]") == 1 && StructuredTypes.WordsOf("Double Word [Unsigned]") == 2 &&
              StructuredTypes.WordsOf("Float [Single Precision]") == 2 && StructuredTypes.WordsOf("String(32)") == 16 && StructuredTypes.WordsOf("String") == 8);
        int off, bit;
        t.NextPlace("Word [Signed]", out off, out bit);
        Check("a new member goes after the last one: +14", off == 14 && bit == 0, off + "." + bit);
        t.Members.Add(new StructuredTypes.Member { Name = "Bitti", DataType = "Bit", Offset = 15, Bit = 0 });
        t.NextPlace("Bit", out off, out bit);
        Check("  a bit after a bit: the next bit of that word (+15.1)", off == 15 && bit == 1, off + "." + bit);
        t.Members.Add(new StructuredTypes.Member { Name = "Hazir", DataType = "Bit", Offset = 15, Bit = 1 });
        Check("  two bits of one word do not clash", StructuredTypes.Problem(t, new[] { t }) == null, StructuredTypes.Problem(t, new[] { t }));

        Console.WriteLine("--- what is not a structure ---");
        var bad = Milling(); bad.Members[2].Offset = 2;
        Check("two members on one device: said, with their places", (StructuredTypes.Problem(bad, new[] { bad }) ?? "").Contains("'X' (+1) and 'Y' (+2)"),
              StructuredTypes.Problem(bad, new[] { bad }));
        bad = Milling(); bad.Size = 12;
        Check("shorter than its labels reach: said, with the label reaching furthest", (StructuredTypes.Problem(bad, new[] { bad }) ?? "").Contains("reach 14 ('Hiz' at +13)"), StructuredTypes.Problem(bad, new[] { bad }));
        bad = Milling(); bad.Name = "Bit";
        Check("named as a simple type", (StructuredTypes.Problem(bad, new[] { bad }) ?? "").Contains("simple data type"));
        bad = Milling(); bad.Members[1].Name = "HareketKodu";
        Check("two members of one name", (StructuredTypes.Problem(bad, new[] { bad }) ?? "").Contains("Two labels"));
        var twin = Milling();
        Check("two structures of one name", (StructuredTypes.Problem(twin, new[] { Milling(), twin }) ?? "").Contains("another structure"));
        bad = Milling(); bad.Members[0].DataType = "FrezeAdim";
        Check("a structure in a structure: not offered (a simple type only)", (StructuredTypes.Problem(bad, new[] { bad }) ?? "").Contains("simple data type only"));

        Console.WriteLine("--- kept, and found by its name ---");
        int raised = 0;
        StructuredTypes.Changed += (s, e) => raised++;
        StructuredTypes.Save(new[] { t });
        Check("saved to its file, the lists told", File.Exists(StructuredTypes.FilePath) && raised == 1);
        StructuredTypes.Reload();
        var back = StructuredTypes.Find("FrezeAdim");
        Check("read back: 10 members, 16 devices, the bits with their bits",
              back != null && back.Members.Count == 10 && back.Size == 16 && back.Members[9].IsBit && back.Members[9].Bit == 1 && back.Members[9].Place == "+15.1" &&
              back.Members[1].DataType == "Double Word [Signed]" && back.Members[1].Offset == 1 && back.Members[1].Comment == "xxx.xx mm",
              back == null ? "none" : back.Members.Count + " " + back.Size);
        Check("the label list's array of it: FrezeAdim(0..19) - the structure, 20 elements",
              StructuredTypes.Find("FrezeAdim(0..19)") == back && StructuredTypes.CountIn("FrezeAdim(0..19)") == 20 && StructuredTypes.BaseName("FrezeAdim(0..19)") == "FrezeAdim" &&
              StructuredTypes.CountIn("FrezeAdim") == 0 && StructuredTypes.Find("Word [Signed]") == null);
        Check("found whatever its letters' case", StructuredTypes.Find("frezeadim") == back);
        var text = File.ReadAllText(StructuredTypes.FilePath);
        Check("the file reads as written: [FrezeAdim];16;... then Name;DataType;Offset;Bit;Comment",
              text.Contains("[FrezeAdim];16;Unit 1 milling step") && text.Contains("X;Double Word [Signed];1;;xxx.xx mm") && text.Contains("Hazir;Bit;15;1;"), text.Substring(0, Math.Min(400, text.Length)));
        var parsed = StructuredTypes.Parse(new[] { "[Short];2", "A;Double Word [Signed];1" });
        Check("a length below its members' reach is made what they reach", parsed.Count == 1 && parsed[0].Size == 3);

        // 19 October: "should they belong to each machine instead - shipped with its project like
        // the PLC labels? It would be good to work with, yeah"
        Console.WriteLine("--- each machine its own, shipped with its project ---");
        string asm = typeof(STC).Assembly.GetName().Name;
        var proj = Path.Combine(dir, "Main"); var build = Path.Combine(proj, "bin", "Debug");
        Directory.CreateDirectory(build);
        File.WriteAllText(Path.Combine(proj, asm + ".csproj"), "<Project />");
        var subBase = Path.Combine(dir, "Elsewhere");
        MachineRegistry.Reset();
        MachineRegistry.MountMain(new Mod { Id = "MAIN" }, null, build);
        MachineRegistry.MountSub(new Mod { Id = "SUB" }, new[] { 3 }, subBase);
        StructuredTypes.Reload();
        Check("the main machine's: beside the program, and its project found two above the build",
              StructuredTypes.PathOf(1) == Path.Combine(build, "StructuredTypes.csv") && StructuredTypes.ProjectPathOf(1) == Path.Combine(proj, "StructuredTypes.csv") &&
              StructuredTypes.MachineOf(1) == "MAIN", StructuredTypes.PathOf(1) + " | " + StructuredTypes.ProjectPathOf(1));
        Check("  the first version's one file is the main machine's now: written beside the program and into its project",
              StructuredTypes.Find("FrezeAdim", 1) != null && File.Exists(Path.Combine(build, "StructuredTypes.csv")) && File.Exists(Path.Combine(proj, "StructuredTypes.csv")));
        Check("  station 0 and a station of no machine: the main machine's", StructuredTypes.Find("FrezeAdim") != null && StructuredTypes.Find("FrezeAdim", 7) != null);
        Check("the mounted machine's: in its own folder (like its PlcLabels.csv), none of the main machine's",
              StructuredTypes.PathOf(3) == Path.Combine(subBase, "SUB", "StructuredTypes.csv") && StructuredTypes.MachineOf(3) == "SUB" &&
              StructuredTypes.Find("FrezeAdim", 3) == null && StructuredTypes.All(3).Count == 0, StructuredTypes.PathOf(3));
        var kesim = new StructuredTypes.Type { Name = "Kesim", Size = 4 };
        kesim.Members.Add(new StructuredTypes.Member { Name = "Boy", DataType = "Float [Single Precision]", Offset = 0 });
        StructuredTypes.Save(new[] { kesim }, 3);
        StructuredTypes.Reload();
        Check("  saved for its station: its file only, the main machine's untouched",
              StructuredTypes.Find("Kesim", 3) != null && StructuredTypes.Find("Kesim", 1) == null && File.Exists(StructuredTypes.PathOf(3)) &&
              StructuredTypes.Find("FrezeAdim", 1) != null && !File.ReadAllText(Path.Combine(proj, "StructuredTypes.csv")).Contains("Kesim"));
        // the project's copy newer (a new version from the repository, not built yet): it is read
        File.WriteAllLines(Path.Combine(proj, "StructuredTypes.csv"), new[] { "[Neu];2;", "A;Word [Signed];0;;" });
        File.SetLastWriteTimeUtc(Path.Combine(proj, "StructuredTypes.csv"), DateTime.UtcNow.AddMinutes(5));
        StructuredTypes.Reload();
        Check("the newer of the program's and the project's copy is read", StructuredTypes.Find("Neu", 1) != null && StructuredTypes.Find("FrezeAdim", 1) == null);
        var sln = Path.Combine(dir, "Sln"); Directory.CreateDirectory(Path.Combine(sln, "Host", "bin", "Debug")); Directory.CreateDirectory(Path.Combine(sln, asm));
        File.WriteAllText(Path.Combine(sln, asm, asm + ".csproj"), "<Project />");
        Check("a mounted machine's project: the host's neighbour (FA2010\\bin\\Debug - ..\\H500)",
              StructuredTypes.ProjectFolderOf(new Mod(), Path.Combine(sln, "Host", "bin", "Debug")) == Path.Combine(sln, asm));
        Check("  no project above an installed program: none", StructuredTypes.ProjectFolderOf(new Mod(), subBase) == null);
        MachineRegistry.Reset();
        StructuredTypes.Reload();

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "STRUCTURED TYPES ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
