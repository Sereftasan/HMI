using System;
using System.Data;
using System.Linq;
using Kaban.Profiles.Data;

class T {
    static int fail = 0;
    static void Check(string w, bool ok, string d="") {
        Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":"")); if(!ok) fail++;
    }

    static void Main() {
        var file = "/tmp/pdb/live.db";
        if (System.IO.File.Exists(file)) System.IO.File.Delete(file);
        Db.Engine = Db.Provider.Sqlite;
        Db.ConnectionTarget = file;

        using (var conn = Db.Open()) {
            Console.WriteLine("=== 1. THE SCHEMA BUILDS AND SEEDS, FROM C#, ON A REAL FILE ===");
            SchemaInstaller.EnsureCreated(conn, withSeed: true);
            Check("schema installed", SchemaInstaller.IsInstalled(conn));
            Check("version recorded", SchemaInstaller.InstalledVersion(conn) == 1);
            Check("idempotent - second call is a no-op",
                  (Action)(() => SchemaInstaller.EnsureCreated(conn, true)) != null
                  && SafeReinstall(conn));

            var profiles = new ProfileRepository(conn);
            Check("20 profiles seeded", profiles.All().Count == 20, profiles.All().Count.ToString());
            Check("joins resolve vendor/series/group",
                  profiles.ByCode("ML8-S-4510").VendorName == "Reynaers Aluminium" &&
                  profiles.ByCode("ML8-S-4510").ProfileType == "SASH",
                  profiles.ByCode("ML8-S-4510").VendorName + " / " + profiles.ByCode("ML8-S-4510").ProfileType);

            Console.WriteLine("\n=== 2. FOREIGN KEYS ARE ACTUALLY ON (they are OFF by default in SQLite) ===");
            bool rejected = false;
            try { Db.Execute(conn, "INSERT INTO profile (group_id, code, dxf_status, is_active, created_at) VALUES (9999,'X','NONE',1,'2026-01-01T00:00:00Z')"); }
            catch (Exception) { rejected = true; }
            Check("a profile in a non-existent group is refused", rejected);

            Console.WriteLine("\n=== 3. ADD A PROFILE AND RESOLVE ITS ACCESSORIES ===");
            long groupId = Convert.ToInt64(Db.Scalar(conn, "SELECT id FROM profile_group WHERE name='ML8 Functional - Sash'"));
            long pid = profiles.Insert(groupId, "ML8-S-9001", "Test sash from C#", 87, 86, "tester");
            Check("profile inserted", pid > 0, "id=" + pid);

            var resolver = new ProfileResolver(conn);
            var r0 = resolver.Resolve(pid);
            Check("nothing resolves without datums", r0.Created == 0, r0.Created + " created");
            Check("and it NAMES the missing datum",
                  r0.Problems.Any(p => p.Contains("HARDWARE_AXIS")),
                  r0.Problems.FirstOrDefault() ?? "(none)");

            profiles.SetReference(pid, "OUTSIDE_SURFACE", 0, 0, "DXF_LAYER", null);
            profiles.SetReference(pid, "INSIDE_SURFACE", 87, 0, "DXF_LAYER", null);
            profiles.SetReference(pid, "HARDWARE_AXIS", 22, 41, "DXF_LAYER", null);
            var r1 = resolver.Resolve(pid);
            Check("with datums, every operation resolves", r1.Created == 10 && r1.Ok,
                  r1.Created + " created, " + r1.Problems.Count + " problems");

            var placements = resolver.AllPlacements(pid);
            foreach (var p in placements.Take(4))
                Console.WriteLine("     {0,-12} op{1}  {2,-12} Y={3,-7} Z={4,-7} {5}",
                    p.AccessoryCode, p.Seq, p.MacroCode, p.YMm, p.ZMm, p.Status);

            var hinge1 = placements.First(p => p.AccessoryCode=="NX-HNG-STD" && p.Seq==1);
            Check("hinge follows THIS profile's hardware axis (Z=41)",
                  hinge1.YMm == 22 && hinge1.ZMm == 41, "Y=" + hinge1.YMm + " Z=" + hinge1.ZMm);

            Console.WriteLine("\n=== 4. PRODUCTION SEES CONFIRMED ROWS ONLY ===");
            Check("nothing cuttable while AUTO", resolver.CuttablePlacements(pid).Count == 0);
            Check("all queued for review", resolver.ReviewQueue().Count >= 10,
                  resolver.ReviewQueue().Count.ToString());
            resolver.Confirm(hinge1.Id, "engineer", "checked against drawing");
            Check("confirmed row becomes cuttable", resolver.CuttablePlacements(pid).Count == 1);
            Check("previous value written to history",
                  Db.Count(conn, "SELECT COUNT(*) FROM placement_history WHERE placement_id=@p0", hinge1.Id) == 1);

            Console.WriteLine("\n=== 5. CORRECTIONS STACK; CONFIRMED ROWS SURVIVE RE-RESOLVE ===");
            long seriesId = Convert.ToInt64(Db.Scalar(conn,
                "SELECT ps.id FROM profile p JOIN profile_group pg ON pg.id=p.group_id " +
                "JOIN profile_variant pv ON pv.id=pg.variant_id JOIN profile_series ps ON ps.id=pv.series_id WHERE p.id=@p0", pid));
            resolver.AddCorrection("SERIES", seriesId, null, null, 0.5, 0, "all ML8 run wide", "engineer");
            resolver.AddCorrection("PROFILE", pid, null, null, 0, -1.5, "trial cut", "engineer");
            resolver.Resolve(pid);

            var after = resolver.AllPlacements(pid);
            var hingeNow = after.First(p => p.Id == hinge1.Id);
            Check("the CONFIRMED hinge kept its value", hingeNow.YMm == 22 && hingeNow.ZMm == 41,
                  "Y=" + hingeNow.YMm + " Z=" + hingeNow.ZMm);
            var hinge2 = after.First(p => p.AccessoryCode=="NX-HNG-STD" && p.Seq==2);
            Check("an AUTO row picked the corrections up (0.5 / -1.5)",
                  Math.Abs(hinge2.CorrYMm - 0.5) < 1e-9 && Math.Abs(hinge2.CorrZMm + 1.5) < 1e-9,
                  "corr Y=" + hinge2.CorrYMm + " Z=" + hinge2.CorrZMm);
            Check("base is kept separately for the UI",
                  hinge2.BaseYMm == 13 && hinge2.BaseZMm == 41,
                  "base Y=" + hinge2.BaseYMm + " Z=" + hinge2.BaseZMm);

            Console.WriteLine("\n=== 6. AI DATUMS ARE PROPOSED, NEVER AUTO ===");
            long pid2 = profiles.Insert(groupId, "ML8-S-9002", "AI extracted", 87, 80, "tester");
            profiles.SetReference(pid2, "HARDWARE_AXIS", 21.7, 34.9, "AI_PROPOSED", 0.87);
            profiles.SetReference(pid2, "OUTSIDE_SURFACE", 0, 0, "DXF_GEOMETRY", null);
            profiles.SetReference(pid2, "INSIDE_SURFACE", 87, 0, "DXF_GEOMETRY", null);
            resolver.Resolve(pid2);
            var ai = resolver.AllPlacements(pid2).First(p => p.AccessoryCode=="NX-HNG-STD");
            Check("status PROPOSED", ai.Status == "PROPOSED", ai.Status);
            Check("confidence carried", ai.Confidence == 0.87, ai.Confidence.ToString());
            Check("not cuttable", resolver.CuttablePlacements(pid2).Count == 0);

            Console.WriteLine("\n=== 7. COMPATIBILITY BY HARDWARE INTERFACE ===");
            var acc = new AccessoryRepository(conn);
            long aws = profiles.ByCode("AWS75-S-210").Id;
            var compat = acc.CompatibleAccessoryCodes(aws);
            Check("a Schueco sash is offered Roto hardware",
                  compat.Contains("NX-HNG-STD") && compat.Contains("NX-HDL-92"),
                  string.Join(",", compat.Take(4)));

            Console.WriteLine("\n=== 8. MACHINE LAYER: capabilities drive the parameter screen ===");
            long n = Db.Count(conn, @"
SELECT COUNT(*) FROM parameter_definition pd
JOIN series_capability sc ON sc.capability_code = pd.capability_code
JOIN machine_series ms ON ms.id = sc.series_id WHERE ms.code='FA2010'");
            Check("FA2010 has its 14 parameters", n == 14, n.ToString());
            Db.Execute(conn, "INSERT INTO machine_series (category_id,code,name,branch) VALUES (4,'KT4000','KT4000 Corner Cleaner','PVC')");
            long kt = Db.LastInsertId(conn);
            Db.Execute(conn, "INSERT INTO series_capability (series_id,capability_code) VALUES (@p0,'CORNER_CLEAN')", kt);
            long ktn = Db.Count(conn, @"
SELECT COUNT(*) FROM parameter_definition pd
JOIN series_capability sc ON sc.capability_code = pd.capability_code
WHERE sc.series_id=@p0", kt);
            Check("a new machine gets its screen with no schema change", ktn == 6, ktn.ToString());
        }

        Console.WriteLine(fail==0 ? "\nDATA LAYER ALL PASS" : "\nDATA LAYER " + fail + " FAILED");
        Environment.Exit(fail==0?0:1);
    }

    static bool SafeReinstall(IDbConnection conn) {
        try { SchemaInstaller.EnsureCreated(conn, true); return true; } catch { return false; }
    }
}

