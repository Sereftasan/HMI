using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Reflection;

namespace Kaban.Profiles.Data
{
    /// <summary>
    /// Creates and upgrades the database.
    ///
    /// The .sql files are EMBEDDED RESOURCES, not files beside the exe: a
    /// schema that can be edited on a panel PC will eventually be edited on a
    /// panel PC, and then that machine's database no longer matches the code
    /// that reads it.
    /// </summary>
    public static class SchemaInstaller
    {
        /// <summary>
        /// Bumped whenever an embedded .sql changes. Stored in the database so an
        /// older program meeting a newer file says so instead of failing on a
        /// column it does not know about.
        /// </summary>
        public const int SchemaVersion = 3;

        private static string ReadResource(string name)
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var res in asm.GetManifestResourceNames())
            {
                // ".upgrade_2.sql", not just "upgrade_2.sql": upgrade_12.sql ends with that too
                if (!res.EndsWith("." + name, StringComparison.OrdinalIgnoreCase)) continue;
                using (var s = asm.GetManifestResourceStream(res))
                using (var r = new StreamReader(s))
                    return r.ReadToEnd();
            }
            // Falling back to disk keeps the test harness and the schema tooling
            // working without an embedded build step.
            foreach (var dir in new[] { AppDomain.CurrentDomain.BaseDirectory,
                                        Directory.GetCurrentDirectory() })
            {
                var p = Path.Combine(dir, name);
                if (File.Exists(p)) return File.ReadAllText(p);
                p = Path.Combine(dir, "sql", name);
                if (File.Exists(p)) return File.ReadAllText(p);
            }
            throw new FileNotFoundException("schema resource not found: " + name);
        }

        /// <summary>True when the database has already been built.</summary>
        public static bool IsInstalled(IDbConnection conn)
        {
            try
            {
                return Db.Count(conn,
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='profile'") > 0
                    || Db.Count(conn,
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_name='profile'") > 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// Builds the schema if it is not there. Idempotent, so start-up can just
        /// call it.
        /// </summary>
        public static void EnsureCreated(IDbConnection conn, bool withSeed = false)
        {
            if (IsInstalled(conn)) { Upgrade(conn); return; }

            RunScript(conn, ReadResource("schema_v2.sql"));
            RunScript(conn, ReadResource("schema_machines.sql"));

            Db.Execute(conn, "CREATE TABLE schema_info (version INTEGER NOT NULL, applied_at TEXT NOT NULL)");
            Db.Execute(conn, "INSERT INTO schema_info (version, applied_at) VALUES (@p0, @p1)", 1, Db.Now());
            Upgrade(conn);

            if (withSeed) RunScript(conn, ReadResource("seed_data.sql"));
        }

        /// <summary>
        /// Brings a library made by an older program up to SchemaVersion, one step
        /// at a time (upgrade_2.sql, ...). A library NEWER than this program is
        /// refused - reading it could silently drop what the newer one stored.
        /// </summary>
        public static void Upgrade(IDbConnection conn)
        {
            int v = InstalledVersion(conn);
            if (v > SchemaVersion)
                throw new InvalidOperationException("The profile library was made by a newer program (schema " + v +
                                                    ", this program knows " + SchemaVersion + "). Update the program.");
            for (int next = v + 1; next <= SchemaVersion; next++)
            {
                RunScript(conn, ReadResource("upgrade_" + next + ".sql"));
                Db.Execute(conn, "INSERT INTO schema_info (version, applied_at) VALUES (@p0, @p1)", next, Db.Now());
            }
        }

        public static int InstalledVersion(IDbConnection conn)
        {
            try { return Convert.ToInt32(Db.Scalar(conn, "SELECT MAX(version) FROM schema_info") ?? 0); }
            catch { return 0; }
        }

        /// <summary>
        /// Runs a multi-statement script.
        ///
        /// Split on semicolons at END OF LINE only. A naive split on every ';'
        /// breaks any statement containing one inside a string literal - and the
        /// seed data is full of JSON macro parameters that do.
        /// </summary>
        public static void RunScript(IDbConnection conn, string script)
        {
            var statement = new System.Text.StringBuilder();
            foreach (var raw in script.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                var trimmed = line.Trim();
                if (trimmed.StartsWith("--", System.StringComparison.Ordinal) || trimmed.Length == 0) continue;

                statement.Append(line).Append('\n');
                if (trimmed.EndsWith(";", System.StringComparison.Ordinal))
                {
                    var sql = statement.ToString().Trim();
                    statement.Length = 0;
                    if (sql.Length <= 1) continue;
                    if (sql.StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase) &&
                        Db.Engine != Db.Provider.Sqlite) continue;   // not portable, not needed
                    Db.Execute(conn, sql.TrimEnd(';'));
                }
            }
        }
    }

    // ======================================================================
    // ROW TYPES - plain data, no behaviour
    // ======================================================================

    public class ProfileRow
    {
        public long Id;
        public long GroupId;
        public string Code;
        public string Description;
        public double? WidthMm, HeightMm;
        public string DxfStatus;
        public string GroupName;
        public string ProfileType;
        public string SeriesName;
        public string VendorName;
    }

    public class ReferenceRow
    {
        public long Id;
        public long ProfileId;
        public string ReferenceCode;
        public double YMm, ZMm;
        public string Source;
        public double? Confidence;
    }

    public class OperationRow
    {
        public long Id;
        public long AccessoryId;
        public string AccessoryCode;
        public int Seq;
        public string MacroCode;
        public string PositionMode;
        public string ReferenceCode;
        public double OffsetYMm, OffsetZMm;
        public double? Ratio;
        public string XStrategy;
        public double? XBaseMm, XPitchMm;
        public int? XCount;
    }

    public class PlacementRow
    {
        public long Id;
        public long ProfileId, OperationId;
        public string AccessoryCode;
        public int Seq;
        public string MacroCode;
        public double YMm, ZMm;
        public double BaseYMm, BaseZMm, CorrYMm, CorrZMm;
        public string Status;
        public double? Confidence, DeviationMm;
    }

    // ======================================================================
    // REPOSITORIES
    // ======================================================================

    /// <summary>Reads and writes the profile catalogue.</summary>
    public class ProfileRepository
    {
        private readonly IDbConnection _conn;
        public ProfileRepository(IDbConnection conn) { _conn = conn; }

        private const string SelectProfile = @"
SELECT p.id, p.group_id, p.code, p.description, p.width_mm, p.height_mm, p.dxf_status,
       pg.name, pg.profile_type, ps.name, pv.name
FROM   profile p
JOIN   profile_group pg   ON pg.id = p.group_id
JOIN   profile_variant pvr ON pvr.id = pg.variant_id
JOIN   profile_series ps  ON ps.id = pvr.series_id
JOIN   profile_vendor pv  ON pv.id = ps.vendor_id";

        private static ProfileRow ReadProfile(IDataReader r)
        {
            return new ProfileRow
            {
                Id = Convert.ToInt64(r[0]),
                GroupId = Convert.ToInt64(r[1]),
                Code = Db.ToText(r[2]),
                Description = Db.ToText(r[3]),
                WidthMm = Db.ToDouble(r[4]),
                HeightMm = Db.ToDouble(r[5]),
                DxfStatus = Db.ToText(r[6]),
                GroupName = Db.ToText(r[7]),
                ProfileType = Db.ToText(r[8]),
                SeriesName = Db.ToText(r[9]),
                VendorName = Db.ToText(r[10])
            };
        }

        public List<ProfileRow> All()
        {
            var list = new List<ProfileRow>();
            using (var cmd = Db.Command(_conn, SelectProfile + " WHERE p.is_active = 1 ORDER BY p.code"))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) list.Add(ReadProfile(r));
            return list;
        }

        public ProfileRow ByCode(string code)
        {
            using (var cmd = Db.Command(_conn, SelectProfile + " WHERE p.code = @p0", code))
            using (var r = cmd.ExecuteReader())
                return r.Read() ? ReadProfile(r) : null;
        }

        public long Insert(long groupId, string code, string description,
                           double? widthMm, double? heightMm, string createdBy)
        {
            Db.Execute(_conn, @"
INSERT INTO profile (group_id, code, description, width_mm, height_mm,
                     dxf_status, is_active, created_at, created_by)
VALUES (@p0,@p1,@p2,@p3,@p4,'NONE',1,@p5,@p6)",
                groupId, code, description, widthMm, heightMm, Db.Now(), createdBy);
            return Db.LastInsertId(_conn);
        }

        public void Update(long id, string code, string description, double? widthMm, double? heightMm)
        {
            Db.Execute(_conn,
                "UPDATE profile SET code=@p1, description=@p2, width_mm=@p3, height_mm=@p4 WHERE id=@p0",
                id, code, description, widthMm, heightMm);
        }

        /// <summary>
        /// Deactivates rather than deletes. A profile that has been cut is
        /// referenced by placements and by whatever job history exists; removing
        /// the row would take that with it.
        /// </summary>
        public void Deactivate(long id)
        {
            Db.Execute(_conn, "UPDATE profile SET is_active=0 WHERE id=@p0", id);
        }

        public List<ReferenceRow> References(long profileId)
        {
            var list = new List<ReferenceRow>();
            using (var cmd = Db.Command(_conn,
                "SELECT id, profile_id, reference_code, y_mm, z_mm, source, confidence " +
                "FROM profile_reference WHERE profile_id=@p0 ORDER BY reference_code", profileId))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    list.Add(new ReferenceRow
                    {
                        Id = Convert.ToInt64(r[0]),
                        ProfileId = Convert.ToInt64(r[1]),
                        ReferenceCode = Db.ToText(r[2]),
                        YMm = Db.Real(r[3]),
                        ZMm = Db.Real(r[4]),
                        Source = Db.ToText(r[5]),
                        Confidence = Db.ToDouble(r[6])
                    });
            return list;
        }

        public void SetReference(long profileId, string code, double y, double z,
                                 string source, double? confidence)
        {
            // No MERGE / UPSERT: the syntax differs on every engine, and a
            // delete-then-insert inside the caller's transaction is portable and
            // does the same thing at this row count.
            Db.Execute(_conn, "DELETE FROM profile_reference WHERE profile_id=@p0 AND reference_code=@p1",
                       profileId, code);
            Db.Execute(_conn, @"
INSERT INTO profile_reference (profile_id, reference_code, y_mm, z_mm, source, confidence)
VALUES (@p0,@p1,@p2,@p3,@p4,@p5)", profileId, code, y, z, source, confidence);
        }
    }

    /// <summary>Accessory catalogue and the operations that make one up.</summary>
    public class AccessoryRepository
    {
        private readonly IDbConnection _conn;
        public AccessoryRepository(IDbConnection conn) { _conn = conn; }

        /// <summary>Operations for every accessory assigned to a profile's group.</summary>
        public List<OperationRow> OperationsForProfile(long profileId)
        {
            var list = new List<OperationRow>();
            using (var cmd = Db.Command(_conn, @"
SELECT op.id, a.id, a.code, op.seq, m.code, op.position_mode, op.reference_code,
       op.offset_y_mm, op.offset_z_mm, op.ratio,
       op.x_strategy, op.x_base_mm, op.x_pitch_mm, op.x_count
FROM   profile p
JOIN   v_group_accessory ga ON ga.profile_group_id = p.group_id
JOIN   accessory a            ON a.id = ga.accessory_id AND a.is_active = 1
JOIN   accessory_operation op ON op.accessory_id = a.id
JOIN   macro m                ON m.id = op.macro_id
WHERE  p.id = @p0
ORDER BY a.code, op.seq", profileId))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    list.Add(new OperationRow
                    {
                        Id = Convert.ToInt64(r[0]),
                        AccessoryId = Convert.ToInt64(r[1]),
                        AccessoryCode = Db.ToText(r[2]),
                        Seq = Convert.ToInt32(r[3]),
                        MacroCode = Db.ToText(r[4]),
                        PositionMode = Db.ToText(r[5]),
                        ReferenceCode = Db.ToText(r[6]),
                        OffsetYMm = Db.Real(r[7]),
                        OffsetZMm = Db.Real(r[8]),
                        Ratio = Db.ToDouble(r[9]),
                        XStrategy = Db.ToText(r[10]),
                        XBaseMm = Db.ToDouble(r[11]),
                        XPitchMm = Db.ToDouble(r[12]),
                        XCount = Db.ToInt(r[13])
                    });
            return list;
        }

        /// <summary>Accessories a profile CAN take, by hardware interface.</summary>
        public List<string> CompatibleAccessoryCodes(long profileId)
        {
            var list = new List<string>();
            using (var cmd = Db.Command(_conn, @"
SELECT DISTINCT a.code
FROM   profile p
JOIN   profile_group pg   ON pg.id = p.group_id
JOIN   accessory_series s ON s.hardware_interface_id = pg.hardware_interface_id
JOIN   accessory a        ON a.series_id = s.id
WHERE  a.is_active = 1 AND p.id = @p0
ORDER BY a.code", profileId))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) list.Add(Db.ToText(r[0]));
            return list;
        }
    }
}

