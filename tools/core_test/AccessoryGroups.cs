using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kaban.Profiles.Data;

// 6 November: "Why can't we attach accessory groups to the profile groups directly so any accessory would be attached to all the
// profiles as soon as they are defined using their accessory and profile groups"
class AG {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }

    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "accg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Db.ConnectionTarget = Path.Combine(dir, "Kaban.db");
        Db.SqliteChoice = Db.SqliteLibrary.Native;
        string why;
        Check("the example library opened (schema 3)", Library.Open(() => true, out why) && Library.Use(c => SchemaInstaller.InstalledVersion(c)) == 3, why);
        var sash = Library.Profiles().First(p => p.Code == "0001000104");
        long g = sash.GroupId;
        // a new accessory group with one accessory of one operation (the macro and the reference of an existing operation)
        var op0 = Library.Rows("SELECT macro_id, reference_code FROM accessory_operation WHERE reference_code IS NOT NULL LIMIT 1")[0];
        Library.Exec("INSERT INTO accessory_group (name, accessory_type) VALUES ('Test locks','LOCK')");
        long ag = Convert.ToInt64(Library.Scalar("SELECT id FROM accessory_group WHERE name='Test locks'"));
        Library.Exec("INSERT INTO accessory (group_id, code, description) VALUES (@p0,'LOCK-A','Lock A')", ag);
        long la = Convert.ToInt64(Library.Scalar("SELECT id FROM accessory WHERE code='LOCK-A'"));
        Library.Exec("INSERT INTO accessory_operation (accessory_id, macro_id, seq, reference_code, offset_y_mm, offset_z_mm) VALUES (@p0,@p1,1,@p2,0,0)",
                     la, Library.Id(op0, "macro_id"), Library.Text(op0, "reference_code"));
        // the profile measured at that reference (its own point: the placements follow it)
        Library.SetReference(sash.Id, Library.Text(op0, "reference_code"), 22, 78, "MANUAL", 1, null);
        Func<List<string>> codes = () => Library.GroupAccessories(g).Select(r => Library.Text(r, "code")).ToList();
        int before = codes().Count;
        Check("not on the profile group yet", !codes().Contains("LOCK-A"));
        var groups = Library.AccessoryGroupsOf(g);
        Check("the accessory groups offered, not attached", groups.Any(r => Library.Text(r, "name") == "Test locks" && Library.Id(r, "attached") == 0));

        Library.AttachAccessoryGroup(g, ag, true);
        var row = Library.GroupAccessories(g).FirstOrDefault(r => Library.Text(r, "code") == "LOCK-A");
        Check("attached: its accessory on the profile group, saying it came with its group", row != null && Library.Text(row, "via_group") == "Test locks" && codes().Count == before + 1);
        Check("  not offered one by one any more", !Library.AccessoriesFor(g).Any(r => Library.Text(r, "code") == "LOCK-A"));
        Library.Resolve(sash.Id);
        Func<string, int> placed = code => Library.Placements(sash.Id).Count(p => p.Accessory == code);
        Check("  its operation placed on the group's profile", placed("LOCK-A") == 1, placed("LOCK-A").ToString());

        Library.Exec("INSERT INTO accessory (group_id, code, description) VALUES (@p0,'LOCK-B','Lock B, added later')", ag);
        long lb = Convert.ToInt64(Library.Scalar("SELECT id FROM accessory WHERE code='LOCK-B'"));
        Library.Exec("INSERT INTO accessory_operation (accessory_id, macro_id, seq, reference_code, offset_y_mm, offset_z_mm) VALUES (@p0,@p1,1,@p2,0,5)",
                     lb, Library.Id(op0, "macro_id"), Library.Text(op0, "reference_code"));
        Library.Resolve(sash.Id);
        Check("an accessory added to the accessory group later: on the profile at once (nothing given one by one)", codes().Contains("LOCK-B") && placed("LOCK-B") == 1);

        Library.AssignAccessory(g, lb, false);
        Library.Resolve(sash.Id);
        Check("one of them switched off for this profile group: off, the other stays", !Library.Rows("SELECT * FROM v_group_accessory WHERE profile_group_id=@p0 AND accessory_id=@p1", g, lb).Any()
              && placed("LOCK-B") == 0 && placed("LOCK-A") == 1);

        Library.AttachAccessoryGroup(g, ag, false);
        Library.Resolve(sash.Id);
        Check("the accessory group taken off: its accessories off the profile (the placements gone)", !codes().Contains("LOCK-A") && placed("LOCK-A") == 0);
        Check("  the profile group's own accessories as before", codes().Count == before + 1 /* LOCK-B: kept as switched off for the group */,
              string.Join(",", codes()));
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nACCESSORY GROUPS ALL PASS" : "\nACCESSORY GROUPS " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
