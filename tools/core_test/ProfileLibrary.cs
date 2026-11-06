using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Kaban.Profiles.Data;
using Kaban.Profiles.Geometry;
using Kaban.Profiles.Machining;

// THE PROFILE LIBRARY (30 September): the database on the operating system's own
// SQLite, the section read from a DXF, the references found on it, the walls a
// tool goes through, the placements with their corrections and confirmations, and
// the side machining program with the tool chosen and the feeds of each wall.
class PL {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }
    static readonly CultureInfo C = CultureInfo.InvariantCulture;

    // ---- a DXF, written by hand ----
    static StringBuilder Dxf() { var sb = new StringBuilder(); sb.Append("0\nSECTION\n2\nENTITIES\n"); return sb; }
    static void Poly(StringBuilder sb, string layer, bool closed, params double[] xy) {
        sb.Append("0\nLWPOLYLINE\n8\n" + layer + "\n90\n" + (xy.Length / 2) + "\n70\n" + (closed ? 1 : 0) + "\n");
        for (int i = 0; i < xy.Length; i += 2) sb.Append("10\n" + xy[i].ToString(C) + "\n20\n" + xy[i + 1].ToString(C) + "\n");
    }
    static void Line(StringBuilder sb, string layer, double x1, double y1, double x2, double y2) {
        sb.Append("0\nLINE\n8\n" + layer + "\n10\n" + x1.ToString(C) + "\n20\n" + y1.ToString(C) + "\n11\n" + x2.ToString(C) + "\n21\n" + y2.ToString(C) + "\n");
    }
    static void Text(StringBuilder sb, string layer, double x, double y, string t) {
        sb.Append("0\nTEXT\n8\n" + layer + "\n10\n" + x.ToString(C) + "\n20\n" + y.ToString(C) + "\n40\n2.5\n1\n" + t + "\n");
    }
    static void Point(StringBuilder sb, string layer, double x, double y) {
        sb.Append("0\nPOINT\n8\n" + layer + "\n10\n" + x.ToString(C) + "\n20\n" + y.ToString(C) + "\n");
    }
    static string End(StringBuilder sb) { sb.Append("0\nENDSEC\n0\nEOF\n"); return sb.ToString(); }

    /// <summary>A PVC sash-like section, W x 78: a 16 mm groove on top at 22, a chamber, a steel inside it.</summary>
    static string Sash(double w, double offsetX = 1000, double offsetY = 500) {
        var sb = Dxf();
        Func<double, double> X = x => x + offsetX; Func<double, double> Y = y => y + offsetY;
        Poly(sb, "PROFILE", true, X(0), Y(0), X(w), Y(0), X(w), Y(78), X(30), Y(78), X(30), Y(73), X(14), Y(73), X(14), Y(78), X(0), Y(78));
        Poly(sb, "PROFILE", true, X(5), Y(5), X(w - 5), Y(5), X(w - 5), Y(60), X(5), Y(60));
        Poly(sb, "STEEL", true, X(20), Y(20), X(50), Y(20), X(50), Y(40), X(20), Y(40));
        Line(sb, "DIM", X(0), Y(-20), X(w), Y(-20));                  // a dimension line below - not material
        Text(sb, "TEXT", X(w + 5), Y(10), "GLAZING_REFERENCE 48 15");
        Point(sb, "REF_CHAMBER_CENTRE", X(35), Y(30));
        return End(sb);
    }

    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "plib_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Db.ConnectionTarget = Path.Combine(dir, "Kaban.db");
        Db.SqliteChoice = Db.SqliteLibrary.Native;

        Console.WriteLine("--- the database, on the operating system's own SQLite ---");
        Check("the provider is the native one", Db.ProviderName.StartsWith("SQLite ("), Db.ProviderName);
        string why;
        bool asked = false;
        Check("opened, built and seeded when asked", Library.Open(() => { asked = true; return true; }, out why), why);
        Check("  it asked, being new", asked);
        Check("  schema 3", Library.Use(c => SchemaInstaller.InstalledVersion(c)) == SchemaInstaller.SchemaVersion && SchemaInstaller.SchemaVersion == 3);
        Check("  foreign keys are on", Throws(() => Library.Exec("INSERT INTO profile (group_id, code, dxf_status, is_active, created_at) VALUES (9999,'X','NONE',1,'x')")));
        asked = false;
        Library.Open(() => { asked = true; return true; }, out why);
        Check("opening again asks nothing", !asked);
        var all = Library.Profiles();
        Check("the example catalogue: 23 profiles", all.Count == 23, all.Count.ToString());
        var sash = all.First(p => p.Code == "0001000104");
        Check("the machine's own PVC samples are there", sash.Vendor == "DECEUNINCK" && sash.Series == "Elegant" && sash.Material == "PVC" && sash.Type == "SASH",
              sash.Vendor + " / " + sash.Series + " / " + sash.Type);
        Check("  the door sash keeps its drawing", all.First(p => p.Code == "0001020307").DxfPath.EndsWith("82_Disa_Kapi.dxf"));
        Check("drainage is slotted from the outside face", Convert.ToString(Library.Scalar("SELECT entry_side FROM accessory_operation WHERE id=22")) == "OUTSIDE");

        Console.WriteLine("--- an older library is upgraded in place ---");
        var old = Path.Combine(dir, "old.db");
        Db.ConnectionTarget = old;
        Library.Use(c =>
        {
            foreach (var f in new[] { "schema_v2.sql", "schema_machines.sql" })
                SchemaInstaller.RunScript(c, File.ReadAllText(Path.Combine("/home/claude/build_work/Kaban.Profiles/sql", f)));
            Db.Execute(c, "CREATE TABLE schema_info (version INTEGER NOT NULL, applied_at TEXT NOT NULL)");
            Db.Execute(c, "INSERT INTO schema_info VALUES (1, 'x')");
        });
        Check("a schema 1 library opens", Library.Open(null, out why), why);
        Check("  and is schema 3, with the new columns", Library.Use(c => SchemaInstaller.InstalledVersion(c)) == 3 &&
              Library.Rows("SELECT entry_side, depth_mode FROM accessory_operation").Count == 0 && Library.Rows("SELECT * FROM machine_face").Count == 0);
        Db.ConnectionTarget = Path.Combine(dir, "Kaban.db");

        Console.WriteLine("--- the DXF reader ---");
        var sb2 = new StringBuilder("0\nSECTION\n2\nBLOCKS\n0\nBLOCK\n8\n0\n2\nB\n10\n0\n20\n0\n");
        Line(sb2, "0", 0, 0, 1, 0);
        sb2.Append("0\nENDBLK\n0\nBLOCK\n8\n0\n2\nUNUSED\n10\n0\n20\n0\n");
        Line(sb2, "0", 500, 500, 600, 600);
        sb2.Append("0\nENDBLK\n0\nENDSEC\n0\nSECTION\n2\nENTITIES\n");
        sb2.Append("0\nINSERT\n8\nFRAME\n2\nB\n10\n100\n20\n50\n41\n2\n42\n2\n50\n90\n");
        sb2.Append("0\nLWPOLYLINE\n8\nBULGE\n90\n2\n70\n0\n10\n0\n20\n0\n42\n1\n10\n10\n20\n0\n");
        sb2.Append("0\nARC\n8\nMIRROR\n10\n5\n20\n0\n40\n5\n50\n0\n51\n90\n210\n0\n220\n0\n230\n-1\n");
        var d = DxfReader.Read(new StringReader(End(sb2)));
        var ins = d.Paths.FirstOrDefault(p => p.Layer == "FRAME");
        Check("an INSERT places its block (moved, scaled, turned)", ins != null && Near(ins.Points[0].X, 100) && Near(ins.Points[0].Y, 50) && Near(ins.Points[1].X, 100) && Near(ins.Points[1].Y, 52),
              ins == null ? "none" : ins.Points[1].X + "," + ins.Points[1].Y);
        Check("  its layer-0 entities take the insert's layer", ins != null);
        Check("a block nobody inserts is not drawn", !d.Paths.Any(p => p.Points.Any(q => q.X >= 500)));
        var bulge = d.Paths.First(p => p.Layer == "BULGE");
        Check("a bulge is the arc it says (counter-clockwise, below the chord)", bulge.Points.All(q => Math.Abs(Math.Sqrt((q.X - 5) * (q.X - 5) + q.Y * q.Y) - 5) < 0.01) &&
              bulge.Points.Min(q => q.Y) < -4.9 && Near(bulge.Points.Last().X, 10), "min y " + bulge.Points.Min(q => q.Y).ToString("0.##"));
        var arc = d.Paths.First(p => p.Layer == "MIRROR");
        Check("an arc with its extrusion down is mirrored into place", Near(arc.Points[0].X, -10) && Near(arc.Points.Last().X, -5) && Near(arc.Points.Last().Y, 5),
              arc.Points[0].X + " .. " + arc.Points.Last().X + "," + arc.Points.Last().Y);

        Console.WriteLine("--- the section ---");
        var file = Path.Combine(dir, "sash70.dxf");
        File.WriteAllText(file, Sash(70));
        var s = Section.Load(file, 0, false, null);
        Check("placed at 0,0, its size from the material only (not the dimension line)", Near(s.Width, 70) && Near(s.Height, 78), s.Width + " x " + s.Height);
        var top = s.Ray(Section.Side.Top, 22);
        Check("from the top at the groove: air, wall, chamber, steel, air, wall",
              string.Join(" ", top.Select(i => i.ToString())) == "air 0-5 wall 5-18 air 18-38 wall 38-58 air 58-73 wall 73-78", string.Join(" ", top.Select(i => i.ToString())));
        Check("  the steel wall is on its layer", top[3].Layer == "STEEL", top[3].Layer);
        var side = s.Ray(Section.Side.Outside, 30);
        Check("from the outside at 30: wall, air, steel, air, wall", string.Join(" ", side.Select(i => i.ToString())) == "wall 0-5 air 5-20 wall 20-50 air 50-65 wall 65-70",
              string.Join(" ", side.Select(i => i.ToString())));
        var s2 = Section.Load(file, 1, false, null);
        Check("turned a quarter clockwise, it is 78 x 70", Near(s2.Width, 78) && Near(s2.Height, 70));
        var s3 = Section.Load(file, 0, true, null);
        Check("mirrored, the groove is on the other side", Near(s3.FirstMaterial(Section.Side.Top, 70 - 22).Value, 5) && Near(s3.FirstMaterial(Section.Side.Top, 22).Value, 0));
        var s4 = Section.Load(file, 0, false, "STEEL");
        Check("an ignored layer is not material", string.Join(" ", s4.Ray(Section.Side.Outside, 30).Select(i => i.ToString())) == "wall 0-5 air 5-65 wall 65-70");

        Console.WriteLine("--- finding the references ---");
        var found = ReferenceFinder.Find(s, 16, null);
        Func<string, ReferenceFinder.Found> F = code => found.FirstOrDefault(f => f.Code == code);
        Check("outside and inside faces from the outline", F("OUTSIDE_SURFACE") != null && Near(F("INSIDE_SURFACE").Y, 70) && F("OUTSIDE_SURFACE").Source == "DXF_GEOMETRY");
        Check("the hardware axis from the 16 mm groove", F("HARDWARE_AXIS") != null && Near(F("HARDWARE_AXIS").Y, 22, 0.3) && Near(F("HARDWARE_AXIS").Z, 78),
              F("HARDWARE_AXIS") == null ? "none" : F("HARDWARE_AXIS").ToString());
        Check("the glazing reference from the text on the drawing", F("GLAZING_REFERENCE") != null && Near(F("GLAZING_REFERENCE").Y, 48) && Near(F("GLAZING_REFERENCE").Z, 15) && F("GLAZING_REFERENCE").Confidence == 1);
        Check("the chamber centre from its layer", F("CHAMBER_CENTRE") != null && Near(F("CHAMBER_CENTRE").Y, 35) && Near(F("CHAMBER_CENTRE").Z, 30) && F("CHAMBER_CENTRE").Source == "DXF_LAYER");
        var none = ReferenceFinder.Find(s, 13, null);
        Check("no groove of the wrong width is taken for the axis", !none.Any(f => f.Code == "HARDWARE_AXIS"));
        Check("several references in one text", ReferenceFinder.Find(Section.FromDrawing(DxfReader.Read(new StringReader(TextOnly("OUTSIDE_SURFACE 0 0 INSIDE_SURFACE 70 0 HARDWARE_AXIS 22 35"))), 0, false, null), null, null)
              .Count(f => f.Source == "DXF_MARKER") == 3);

        Console.WriteLine("--- a profile, its references, its placements ---");
        sash.DxfPath = file;
        Library.SaveProfile(sash, "tester");
        foreach (var f in found) Library.SetReference(sash.Id, f.Code, f.Y, f.Z, f.Source, f.Confidence, null);
        var fp = Fingerprint.Of(s);
        Library.SaveFingerprint(sash.Id, fp);
        var res = Library.Resolve(sash.Id);
        var pl = Library.Placements(sash.Id);
        Check("the group's handle resolves on the hardware axis", res.Ok && pl.Count == 1 && pl[0].Macro == "POCKET_32X32" && Near(pl[0].Y, 22, 0.3) && Near(pl[0].Z, 78),
              res.Problems.FirstOrDefault() ?? (pl.Count + " " + (pl.Count > 0 ? pl[0].Y + "/" + pl[0].Z : "")));
        Check("  AUTO: nobody has confirmed it", pl[0].Status == "AUTO");
        Check("  from the top face", pl[0].Side == "TOP");

        Console.WriteLine("--- the machine: tools, faces, feeds ---");
        long inst = Library.EnsureMachine("FA2010", "FA2010", new[] { P("U1-T1", 1), P("U1-T2", 1), P("U2-T1", 2), P("U2-T2", 2) },
                                          new[] { new Face { Unit = 1, Side = "TOP", Angle = 0 }, new Face { Unit = 1, Side = "OUTSIDE", Angle = 90 },
                                                  new Face { Unit = 2, Side = "TOP", Angle = 0 }, new Face { Unit = 2, Side = "OUTSIDE", Angle = 90 } });
        Check("the machine and its four tool places", Library.Tools(inst).Count == 4 && Library.Faces(inst).Count == 4);
        Check("  created once", Library.EnsureMachine("FA2010", "FA2010", new[] { P("X", 1) }, null) == inst && Library.Tools(inst).Count == 4);
        Check("  its series' feeds", Library.Feeds(inst).Any(x => x.MillingClass == "PVC_OUTER_WALL" && x.ToolType == "END_MILL" && x.Entry == 1500));
        var ops = pl.Select(x => x.ToOperation()).ToList();
        var ps = Library.ProgramSettings(inst, sash);
        var prog = SideProgram.Generate(sash.Code, 1500, ops, s, s.Width, s.Height, Library.Tools(inst), Library.Faces(inst), Library.Feeds(inst), ps, true);
        Check("no tool sizes yet: the program says so, and names the tools", !prog.Ok && prog.Problems[0].Contains("diameters are not set") && prog.Problems[0].Contains("U1-T1"), prog.Problems.FirstOrDefault());
        Library.Exec("UPDATE mounted_tool SET diameter_mm = 10 WHERE position='U1-T1'");
        Library.Exec("UPDATE mounted_tool SET diameter_mm = 5, tool_type='DRILL' WHERE position='U1-T2'");
        Library.Exec("UPDATE mounted_tool SET diameter_mm = 6 WHERE position='U2-T1'");
        Library.Exec("UPDATE mounted_tool SET diameter_mm = 8, tool_type='DRILL' WHERE position='U2-T2'");
        var tools = Library.Tools(inst);
        var pocket = pl[0].Shape();
        var ch = SideProgram.Choose(pocket, "TOP", tools, Library.Faces(inst), 10);
        Check("the pocket (R3 corners) takes the 6 mm end mill", ch.Ok && ch.Tool.Position == "U2-T1", ch.Ok ? ch.Tool + " " + ch.Why : ch.Problem);
        var hole5 = new MacroShape { Code = "DRILL_5", Type = "ROUND_HOLE", Params = FlatJson.ParseDictionary("{\"diameter_mm\":5,\"depth_mm\":12}") };
        ch = SideProgram.Choose(hole5, "TOP", tools, Library.Faces(inst), 12);
        Check("a 5 mm hole takes the 5 mm drill", ch.Ok && ch.Tool.Position == "U1-T2", ch.Ok ? ch.Tool.ToString() : ch.Problem);
        var hole12 = new MacroShape { Code = "D12", Type = "ROUND_HOLE", Params = FlatJson.ParseDictionary("{\"diameter_mm\":12,\"depth_mm\":12}") };
        ch = SideProgram.Choose(hole12, "TOP", tools, Library.Faces(inst), 12);
        Check("a 12 mm hole is milled round with the 10 mm end mill", ch.Ok && ch.Tool.Position == "U1-T1" && ch.Why.Contains("circular"), ch.Ok ? ch.Why : ch.Problem);
        ch = SideProgram.Choose(hole5, "BOTTOM", tools, Library.Faces(inst), 12);
        Check("no unit machines the bottom face: said so", !ch.Ok && ch.Problem.Contains("bottom"), ch.Problem);
        var slot = new MacroShape { Code = "S4", Type = "SLOT", Params = FlatJson.ParseDictionary("{\"length_mm\":20,\"width_mm\":4,\"depth_mm\":3}") };
        ch = SideProgram.Choose(slot, "TOP", tools, Library.Faces(inst), 3);
        Check("a slot narrower than every end mill: no tool", !ch.Ok && ch.Problem.Contains("no mounted tool"), ch.Problem);

        prog = SideProgram.Generate(sash.Code, 1500, ops, s, s.Width, s.Height, tools, Library.Faces(inst), Library.Feeds(inst), ps, false);
        Check("not confirmed: not in the program", prog.Ok && prog.Operations == 0 && prog.Notes.Any(n => n.Contains("not confirmed")), string.Join(" | ", prog.Notes));
        prog = SideProgram.Generate(sash.Code, 1500, ops, s, s.Width, s.Height, tools, Library.Faces(inst), Library.Feeds(inst), ps, true);
        Check("a TRIAL program has it, and says it is a trial", prog.Ok && prog.Operations == 1 && prog.Code.Contains("TRIAL PROGRAM"), string.Join(" | ", prog.Problems));
        Check("  centred on the bar", prog.Code.Contains("AT X750"));
        Check("  the surface is the groove's bottom, 5 below the top", prog.Code.Contains("SURFACE Z-5,") && prog.Code.Contains("BOTTOM Z-15"), Grab(prog.Code, "SURFACE"));
        Check("  plunged at the wall's entry speed, cut at its feed", prog.Code.Contains("F1500") && prog.Code.Contains("F4000"));
        Check("  incremental", prog.Code.Contains("\nG91") && prog.Code.Contains("M30"));
        Library.Exec("UPDATE feed_strategy SET thin_wall_mm = 20 WHERE milling_class_id = 1 AND tool_type = 'END_MILL'");
        prog = SideProgram.Generate(sash.Code, 1500, ops, s, s.Width, s.Height, tools, Library.Faces(inst), Library.Feeds(inst), ps, true);
        Check("a THIN wall is entered slower (x0.6)", prog.Code.Contains("F900") && prog.Code.Contains("F2400"), Grab(prog.Code, "F9"));
        Library.Exec("UPDATE feed_strategy SET thin_wall_mm = 1.5 WHERE milling_class_id = 1 AND tool_type = 'END_MILL'");

        Console.WriteLine("--- trial and tune: corrections, confirmation ---");
        Library.ConfirmPlacement(pl[0].Id, "tester", "trial cut OK");
        Check("confirmed", Library.Placements(sash.Id)[0].Status == "CONFIRMED");
        prog = SideProgram.Generate(sash.Code, 1500, Library.Placements(sash.Id).Select(x => x.ToOperation()).ToList(), s, s.Width, s.Height, tools, Library.Faces(inst), Library.Feeds(inst), ps, false);
        Check("  now in the production program", prog.Operations == 1 && !prog.Code.Contains("TRIAL"));
        Library.Resolve(sash.Id);
        Check("resolving again leaves a confirmed position alone", Library.Placements(sash.Id)[0].Status == "CONFIRMED");
        Library.AddCorrection(sash.Id, "PROFILE", pl[0].AccessoryId, pl[0].OperationId, 1.5, 0, "1.5 too far out on the test piece", "tester");
        var moved = Library.Placements(sash.Id)[0];
        Check("a correction moves it: base + correction", Near(moved.Y, moved.BaseY + 1.5) && Near(moved.CorrY, 1.5), moved.BaseY + " + " + moved.CorrY + " = " + moved.Y);
        Check("  and it must be confirmed again", moved.Status == "AUTO");
        Check("  the history keeps the confirmed position", Library.PlacementHistory(moved.Id).Any(h => Library.Text(h, "reason").Contains("moved by a correction")));
        Check("  the accessory itself is untouched", Near(Convert.ToDouble(Library.Scalar("SELECT offset_y_mm FROM accessory_operation WHERE id=@p0", moved.OperationId)), 0));
        Library.AddCorrection(sash.Id, "GROUP", pl[0].AccessoryId, null, 0, -0.5, "group", "tester");
        moved = Library.Placements(sash.Id)[0];
        Check("group and profile corrections add up", Near(moved.CorrY, 1.5) && Near(moved.CorrZ, -0.5), moved.CorrY + "/" + moved.CorrZ);
        Check("  both listed", Library.Corrections(sash.Id).Count == 2);
        Library.RejectPlacement(moved.Id, "tester", "no handle on this sash");
        prog = SideProgram.Generate(sash.Code, 1500, Library.Placements(sash.Id).Select(x => x.ToOperation()).ToList(), s, s.Width, s.Height, tools, Library.Faces(inst), Library.Feeds(inst), ps, true);
        Check("rejected: not even in a trial program", prog.Operations == 0);
        Library.ReopenPlacement(moved.Id, "tester", null);
        Check("reopened: AUTO again", Library.Placements(sash.Id)[0].Status == "AUTO");

        Console.WriteLine("--- through the wall, from the outside ---");
        var frame = all.First(p => p.Code == "0001000007");
        frame.DxfPath = file;
        Library.SaveProfile(frame, "tester");
        foreach (var f in found) Library.SetReference(frame.Id, f.Code, f.Y, f.Z, f.Source, f.Confidence, null);
        Library.Resolve(frame.Id);
        var drain = Library.Placements(frame.Id);
        Check("the frame's drainage slot, from the outside face at 5 up", drain.Count == 1 && drain[0].Side == "OUTSIDE" && Near(drain[0].Z, 5), drain.Count + "");
        Library.Exec("UPDATE accessory_operation SET depth_mode = 1 WHERE id = @p0", drain[0].OperationId);
        Library.Exec("UPDATE accessory_operation SET offset_z_mm = 30 WHERE id = @p0", drain[0].OperationId);
        Library.Resolve(frame.Id);
        drain = Library.Placements(frame.Id);
        prog = SideProgram.Generate(frame.Code, 2000, drain.Select(x => x.ToOperation()).ToList(), s, s.Width, s.Height, tools, Library.Faces(inst), Library.Feeds(inst), ps, true);
        Check("  through the first wall only (5 mm + 0.5)", prog.Ok && prog.Code.Contains("SURFACE Z-0,") && prog.Code.Contains("BOTTOM Z-5.5"), string.Join(" | ", prog.Problems) + Grab(prog.Code, "SURFACE"));
        Check("  spread along the bar: 150 from each end, 600 at most apart", Regex(prog.Code, "(   AT X") == 4, Regex(prog.Code, "(   AT X").ToString());
        Library.Exec("UPDATE accessory_operation SET depth_mode = 2 WHERE id = @p0", drain[0].OperationId);
        Library.Resolve(frame.Id);
        prog = SideProgram.Generate(frame.Code, 2000, Library.Placements(frame.Id).Select(x => x.ToOperation()).ToList(), s, s.Width, s.Height, tools, Library.Faces(inst), Library.Feeds(inst), ps, true);
        Check("  through the whole profile: to 70.5, fast through the chambers", prog.Code.Contains("BOTTOM Z-70.5") && prog.Code.Contains("F3000"), Grab(prog.Code, "BOTTOM"));
        Library.Exec("INSERT INTO layer_milling_map (profile_series_id, dxf_layer_name, milling_class_id) VALUES (@p0, 'STEEL', 6)", frame.SeriesId);
        Library.Exec("INSERT INTO feed_strategy (series_id, milling_class_id, tool_type, entry_speed, feed_speed, spindle_rpm, plunge_rate, thin_wall_factor, thin_wall_mm) VALUES (1, 6, 'END_MILL', 222, 666, 9000, 100, 1, 1)");
        ps = Library.ProgramSettings(inst, Library.Profile(frame.Id));
        prog = SideProgram.Generate(frame.Code, 2000, Library.Placements(frame.Id).Select(x => x.ToOperation()).ToList(), s, s.Width, s.Height, tools, Library.Faces(inst), Library.Feeds(inst), ps, true);
        Check("  the steel, its layer mapped to steel, at steel's entry speed", prog.Code.Contains("F222"), Grab(prog.Code, "F2"));

        Console.WriteLine("--- a new profile from a similar one ---");
        var file72 = Path.Combine(dir, "sash72.dxf");
        File.WriteAllText(file72, Sash(72).Replace("GLAZING_REFERENCE 48 15", "NOTE"));
        var s72 = Section.Load(file72, 0, false, null);
        var fp72 = Fingerprint.Of(s72);
        var bead = Section.FromDrawing(DxfReader.Read(new StringReader(Rect(20, 20))), 0, false, null);
        Check("similar profiles are close, a bead is not", fp72.Similarity(fp) > 0.8 && Fingerprint.Of(bead).Similarity(fp) < 0.3,
              fp72.Similarity(fp).ToString("0.00") + " / " + Fingerprint.Of(bead).Similarity(fp).ToString("0.00"));
        Check("  the fingerprint survives the database", Fingerprint.Unpack(fp.Pack()).Similarity(fp) > 0.999);
        long newId = Library.CopyProfile(sash.Id, "0001000105", "tester");
        var np = Library.Profile(newId);
        np.DxfPath = file72;
        Library.SaveProfile(np, "tester");
        Check("based on: the new one inherits every reference", Library.References(newId).Count == found.Count && Library.References(newId).All(r => r.Inherited));
        var closest = Library.Closest(fp72, newId, 3);
        Check("the closest profile with references is found", closest.Count > 0 && closest[0].Code == sash.Code, closest.Count > 0 ? closest[0].Code + " " + closest[0].Similarity.ToString("0.00") : "none");
        var tpl = Library.TemplateFor(np, fp72);
        var found72 = ReferenceFinder.Find(s72, 16, tpl);
        var g = found72.First(f => f.Code == "GLAZING_REFERENCE");
        Check("a reference not on the drawing is PROPOSED from the similar one, kept from the nearer face", g.Source == "AI_PROPOSED" && Near(g.Y, 72 - (70 - 48)) && g.Confidence <= 0.6,
              g.ToString());
        Check("  the groove is found on the new drawing itself", found72.First(f => f.Code == "HARDWARE_AXIS").Source == "DXF_GEOMETRY");
        Library.SetReference(newId, "HARDWARE_AXIS", 23, 78, "AI_PROPOSED", 0.5, null);
        Library.Resolve(newId);
        Check("a placement on a proposed reference is PROPOSED", Library.Placements(newId)[0].Status == "PROPOSED");
        Library.ConfirmReference(newId, "HARDWARE_AXIS", "tester");
        Library.Resolve(newId);
        Check("  confirming the reference makes it AUTO (and it can then be cut on trial and confirmed)", Library.Placements(newId)[0].Status == "AUTO");

        Console.WriteLine("--- along the bar ---");
        var op = new SideOperation { XStrategy = "FROM_BOTH_ENDS", XBase = 120, XCount = 2, Mirror = true };
        var xs = SideProgram.Positions(op, 1000);
        Check("hinges 120 from each end, the far one mirrored", xs.Count == 2 && Near(xs[0].Key, 120) && Near(xs[1].Key, 880) && !xs[0].Value && xs[1].Value);
        op = new SideOperation { XStrategy = "DISTRIBUTED", XBase = 150, XPitch = 600, XCount = 3 };
        xs = SideProgram.Positions(op, 2000);
        Check("drainage every 600 at most: 4 on 2000", xs.Count == 4 && Near(xs[0].Key, 150) && Near(xs[3].Key, 1850), string.Join(",", xs.Select(x => x.Key.ToString("0.#"))));

        Console.WriteLine("--- the end milling program ---");
        Library.SaveEndMillProgram(sash.Id, inst, "G0 X0\nG1 X10", 3, 5, "tester");
        var em = Library.EndMillProgram(sash.Id, inst);
        Check("kept per profile and machine", em != null && Library.Text(em, "gcode").Contains("G1 X10") && Library.Real(em, "tool_radius_mm") == 3);

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nPROFILE LIBRARY ALL PASS" : "\n" + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }

    static KeyValuePair<string, int> P(string k, int v) { return new KeyValuePair<string, int>(k, v); }
    static bool Near(double a, double b, double tol = 0.01) { return Math.Abs(a - b) <= tol; }
    static bool Throws(Action a) { try { a(); return false; } catch { return true; } }
    static int Regex(string text, string what) { int n = 0, i = 0; while ((i = text.IndexOf(what, i, StringComparison.Ordinal)) >= 0) { n++; i++; } return n; }
    static string Grab(string text, string what) { int i = text.IndexOf(what, StringComparison.Ordinal); return i < 0 ? "" : text.Substring(i, Math.Min(80, text.Length - i)).Split('\n')[0]; }
    static string TextOnly(string t) { var sb = Dxf(); Poly(sb, "P", true, 0, 0, 70, 0, 70, 78, 0, 78); Text(sb, "NOTES", 80, 0, t); return End(sb); }
    static string Rect(double w, double h) { var sb = Dxf(); Poly(sb, "P", true, 0, 0, w, 0, w, h, 0, h); return End(sb); }
}
