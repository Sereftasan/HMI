using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using Kaban.Profiles.Geometry;
using Kaban.Profiles.Machining;

namespace Kaban.Profiles.Data
{
    /// <summary>
    /// What the Profiles page asks of the library, each call on its own connection
    /// (a panel PC has one user and SQLite opens in microseconds; a connection held
    /// open by a page is a file held locked).
    ///
    /// Generic reads and writes (Rows, Exec) serve the page's table editors; the
    /// typed calls are the ones with rules: a profile and its references, the
    /// resolved placements and their corrections, the machine's tools.
    /// </summary>
    public static class Library
    {
        // ---- opening ------------------------------------------------------------

        private static bool _ready;

        /// <summary>
        /// Opens (and builds, or upgrades) the library. <paramref name="seedIfNew"/>
        /// asks, when the library is new, whether to load the example catalogue.
        /// </summary>
        public static bool Open(Func<bool> seedIfNew, out string why)
        {
            why = "";
            try
            {
                using (var c = Db.Open())
                {
                    bool fresh = !SchemaInstaller.IsInstalled(c);
                    bool seed = fresh && seedIfNew != null && seedIfNew();
                    SchemaInstaller.EnsureCreated(c, seed);
                }
                _ready = true;
                return true;
            }
            catch (Exception ex)
            {
                why = ex.Message;
                _ready = false;
                return false;
            }
        }

        public static bool IsReady { get { return _ready; } }

        public static T Use<T>(Func<IDbConnection, T> work)
        {
            using (var c = Db.Open()) return work(c);
        }

        public static void Use(Action<IDbConnection> work)
        {
            using (var c = Db.Open()) work(c);
        }

        /// <summary>Several writes, all or none.</summary>
        public static void Transaction(Action<IDbConnection> work)
        {
            using (var c = Db.Open())
            using (var t = c.BeginTransaction())
            {
                work(c);
                t.Commit();
            }
        }

        public static List<Dictionary<string, object>> Rows(string sql, params object[] args)
        {
            return Use(c => Rows(c, sql, args));
        }

        public static List<Dictionary<string, object>> Rows(IDbConnection c, string sql, params object[] args)
        {
            var list = new List<Dictionary<string, object>>();
            using (var cmd = Db.Command(c, sql, args))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                {
                    var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < r.FieldCount; i++)
                    {
                        var v = r.GetValue(i);
                        if (v is float) v = Db.Real(v);
                        row[r.GetName(i)] = v == DBNull.Value ? null : v;
                    }
                    list.Add(row);
                }
            return list;
        }

        public static int Exec(string sql, params object[] args) { return Use(c => Db.Execute(c, sql, args)); }

        public static object Scalar(string sql, params object[] args) { return Use(c => Db.Scalar(c, sql, args)); }

        public static long Insert(IDbConnection c, string sql, params object[] args)
        {
            Db.Execute(c, sql, args);
            return Db.LastInsertId(c);
        }

        public static string Text(Dictionary<string, object> row, string col)
        {
            object v;
            return row != null && row.TryGetValue(col, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : "";
        }

        public static double? Real(Dictionary<string, object> row, string col)
        {
            object v;
            return row != null && row.TryGetValue(col, out v) && v != null ? Db.Real(v) : (double?)null;
        }

        public static long? Id(Dictionary<string, object> row, string col)
        {
            object v;
            return row != null && row.TryGetValue(col, out v) && v != null ? Convert.ToInt64(v, CultureInfo.InvariantCulture) : (long?)null;
        }

        // ---- profiles -------------------------------------------------------------

        public sealed class ProfileInfo
        {
            public long Id, GroupId, VariantId, SeriesId, VendorId;
            public long? FamilyId, BaseProfileId, InterfaceId;
            public string Code = "", Description = "", Vendor = "", Series = "", Variant = "", Group = "", Type = "", Material = "", Family = "", Interface = "";
            public double? Width, Height, GroupWidth, GroupHeight;
            public string DxfPath = "", DxfStatus = "NONE", IgnoredLayers = "", Note = "";
            public int Rotation;
            public bool Mirror, Active = true;
            public int Placements, Confirmed, Proposed, Rejected;
            public double EffectiveWidth { get { return Width ?? GroupWidth ?? 0; } }
            public double EffectiveHeight { get { return Height ?? GroupHeight ?? 0; } }
            /// <summary>"3 of 10 confirmed", "no accessories", ...</summary>
            public string PlacementText
            {
                get
                {
                    if (Placements == 0) return "";
                    return Confirmed + " / " + (Placements - Rejected) + (Proposed > 0 ? " (" + Proposed + " proposed)" : "");
                }
            }
        }

        private const string ProfileSelect = @"
SELECT p.id, p.group_id, pg.variant_id, pvr.series_id, ps.vendor_id, p.machining_family_id, p.base_profile_id, pg.hardware_interface_id,
       p.code, p.description, pv.name, ps.name, pvr.name, pg.name, pg.profile_type, ps.material, mf.name, hi.code,
       p.width_mm, p.height_mm, pg.default_width_mm, pg.default_height_mm,
       p.dxf_path, p.dxf_status, p.ignored_layers, p.note, p.dxf_rotation, p.dxf_mirror, p.is_active,
       (SELECT COUNT(*) FROM resolved_placement rp WHERE rp.profile_id = p.id),
       (SELECT COUNT(*) FROM resolved_placement rp WHERE rp.profile_id = p.id AND rp.status IN ('CONFIRMED','OVERRIDDEN')),
       (SELECT COUNT(*) FROM resolved_placement rp WHERE rp.profile_id = p.id AND rp.status = 'PROPOSED'),
       (SELECT COUNT(*) FROM resolved_placement rp WHERE rp.profile_id = p.id AND rp.status = 'REJECTED')
FROM   profile p
JOIN   profile_group pg    ON pg.id = p.group_id
JOIN   profile_variant pvr ON pvr.id = pg.variant_id
JOIN   profile_series ps   ON ps.id = pvr.series_id
JOIN   profile_vendor pv   ON pv.id = ps.vendor_id
LEFT JOIN machining_family mf  ON mf.id = p.machining_family_id
LEFT JOIN hardware_interface hi ON hi.id = pg.hardware_interface_id";

        private static ProfileInfo ReadProfile(IDataReader r)
        {
            Func<int, long?> L = i => r.IsDBNull(i) ? (long?)null : Convert.ToInt64(r.GetValue(i));
            Func<int, string> S = i => r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture);
            Func<int, double?> D = i => r.IsDBNull(i) ? (double?)null : Db.Real(r.GetValue(i));
            return new ProfileInfo
            {
                Id = L(0).Value, GroupId = L(1).Value, VariantId = L(2).Value, SeriesId = L(3).Value, VendorId = L(4).Value,
                FamilyId = L(5), BaseProfileId = L(6), InterfaceId = L(7),
                Code = S(8), Description = S(9), Vendor = S(10), Series = S(11), Variant = S(12), Group = S(13), Type = S(14),
                Material = S(15), Family = S(16), Interface = S(17),
                Width = D(18), Height = D(19), GroupWidth = D(20), GroupHeight = D(21),
                DxfPath = S(22), DxfStatus = S(23), IgnoredLayers = S(24), Note = S(25),
                Rotation = (int)(L(26) ?? 0), Mirror = (L(27) ?? 0) != 0, Active = (L(28) ?? 1) != 0,
                Placements = (int)(L(29) ?? 0), Confirmed = (int)(L(30) ?? 0), Proposed = (int)(L(31) ?? 0), Rejected = (int)(L(32) ?? 0)
            };
        }

        public static List<ProfileInfo> Profiles(bool includeInactive = false)
        {
            return Use(c =>
            {
                var list = new List<ProfileInfo>();
                using (var cmd = Db.Command(c, ProfileSelect + (includeInactive ? "" : " WHERE p.is_active = 1") + " ORDER BY pv.name, ps.name, pg.name, p.code"))
                using (var r = cmd.ExecuteReader())
                    while (r.Read()) list.Add(ReadProfile(r));
                return list;
            });
        }

        public static ProfileInfo Profile(long id)
        {
            return Use(c =>
            {
                using (var cmd = Db.Command(c, ProfileSelect + " WHERE p.id = @p0", id))
                using (var r = cmd.ExecuteReader())
                    return r.Read() ? ReadProfile(r) : null;
            });
        }

        /// <summary>Inserts (Id 0) or updates a profile; returns its id.</summary>
        public static long SaveProfile(ProfileInfo p, string user)
        {
            return Use(c =>
            {
                if (p.Id <= 0)
                {
                    return Insert(c, @"
INSERT INTO profile (group_id, machining_family_id, code, description, base_profile_id, width_mm, height_mm,
                     dxf_path, dxf_status, dxf_rotation, dxf_mirror, ignored_layers, note, is_active, created_at, created_by)
VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,1,@p13,@p14)",
                        p.GroupId, p.FamilyId, p.Code, p.Description, p.BaseProfileId, p.Width, p.Height,
                        Null(p.DxfPath), p.DxfStatus ?? "NONE", p.Rotation, p.Mirror ? 1 : 0, Null(p.IgnoredLayers), Null(p.Note), Db.Now(), user);
                }
                Db.Execute(c, @"
UPDATE profile SET group_id=@p1, machining_family_id=@p2, code=@p3, description=@p4, base_profile_id=@p5,
       width_mm=@p6, height_mm=@p7, dxf_path=@p8, dxf_status=@p9, dxf_rotation=@p10, dxf_mirror=@p11,
       ignored_layers=@p12, note=@p13, is_active=@p14
WHERE id=@p0",
                    p.Id, p.GroupId, p.FamilyId, p.Code, p.Description, p.BaseProfileId == p.Id ? null : p.BaseProfileId,
                    p.Width, p.Height, Null(p.DxfPath), p.DxfStatus ?? "NONE", p.Rotation, p.Mirror ? 1 : 0,
                    Null(p.IgnoredLayers), Null(p.Note), p.Active ? 1 : 0);
                return p.Id;
            });
        }

        private static object Null(string s) { return string.IsNullOrWhiteSpace(s) ? null : s.Trim(); }

        /// <summary>
        /// A new profile BASED ON another: the same group, family, sizes and drawing
        /// settings, and <c>base_profile_id</c> pointing at it - so every reference it
        /// does not set itself is the other's (shown as inherited, resolved as
        /// inherited). Only what differs then needs entering.
        /// </summary>
        public static long CopyProfile(long fromId, string newCode, string user)
        {
            var copy = Profile(fromId);
            if (copy == null) throw new ArgumentException("no profile " + fromId);
            copy.Id = 0;
            copy.Code = newCode;
            copy.BaseProfileId = fromId;
            copy.DxfStatus = string.IsNullOrEmpty(copy.DxfPath) ? "NONE" : "IMPORTED";
            return SaveProfile(copy, user);
        }

        public static void DeactivateProfile(long id)
        {
            Exec("UPDATE profile SET is_active = 0 WHERE id = @p0", id);
        }

        // ---- references --------------------------------------------------------

        public sealed class ReferenceInfo
        {
            public string Code = "";
            public double Y, Z;
            public string Source = "";
            public double? Confidence;
            public string ConfirmedBy = "", ConfirmedAt = "";
            /// <summary>Not the profile's own: from its base profile.</summary>
            public bool Inherited;
            public string InheritedFrom = "";
            public bool Confirmed { get { return !string.IsNullOrEmpty(ConfirmedBy); } }
        }

        /// <summary>The profile's references, and those it inherits from its base profiles (marked).</summary>
        public static List<ReferenceInfo> References(long profileId)
        {
            return Use(c =>
            {
                var result = new Dictionary<string, ReferenceInfo>(StringComparer.OrdinalIgnoreCase);
                var seen = new HashSet<long>();
                long? id = profileId;
                bool own = true;
                while (id.HasValue && seen.Add(id.Value))
                {
                    string code = own ? "" : Convert.ToString(Db.Scalar(c, "SELECT code FROM profile WHERE id=@p0", id.Value));
                    foreach (var row in Rows(c, "SELECT reference_code, y_mm, z_mm, source, confidence, confirmed_by, confirmed_at FROM profile_reference WHERE profile_id=@p0", id.Value))
                    {
                        var rc = Text(row, "reference_code");
                        if (result.ContainsKey(rc)) continue;
                        result[rc] = new ReferenceInfo
                        {
                            Code = rc, Y = Real(row, "y_mm") ?? 0, Z = Real(row, "z_mm") ?? 0, Source = Text(row, "source"),
                            Confidence = Real(row, "confidence"), ConfirmedBy = Text(row, "confirmed_by"), ConfirmedAt = Text(row, "confirmed_at"),
                            Inherited = !own, InheritedFrom = code
                        };
                    }
                    var b = Db.Scalar(c, "SELECT base_profile_id FROM profile WHERE id=@p0", id.Value);
                    id = b == null ? (long?)null : Convert.ToInt64(b);
                    own = false;
                }
                return ReferenceFinder.Codes.Where(result.ContainsKey).Select(k => result[k])
                    .Concat(result.Values.Where(v => !ReferenceFinder.Codes.Contains(v.Code))).ToList();
            });
        }

        /// <summary>Sets one reference of a profile (its confirmation with it, or none).</summary>
        public static void SetReference(long profileId, string code, double y, double z, string source, double? confidence, string confirmedBy)
        {
            Transaction(c =>
            {
                Db.Execute(c, "DELETE FROM profile_reference WHERE profile_id=@p0 AND reference_code=@p1", profileId, code);
                Db.Execute(c, @"INSERT INTO profile_reference (profile_id, reference_code, y_mm, z_mm, source, confidence, confirmed_by, confirmed_at)
VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7)", profileId, code, y, z, source, confidence,
                    string.IsNullOrEmpty(confirmedBy) ? null : confirmedBy, string.IsNullOrEmpty(confirmedBy) ? null : Db.Now());
            });
        }

        public static void RemoveReference(long profileId, string code)
        {
            Exec("DELETE FROM profile_reference WHERE profile_id=@p0 AND reference_code=@p1", profileId, code);
        }

        /// <summary>A person checked the reference against the drawing: it is no longer a proposal.</summary>
        public static void ConfirmReference(long profileId, string code, string user)
        {
            Exec(@"UPDATE profile_reference SET confirmed_by=@p2, confirmed_at=@p3,
                          source = CASE WHEN source='AI_PROPOSED' THEN 'MANUAL' ELSE source END, confidence = 1
                   WHERE profile_id=@p0 AND reference_code=@p1", profileId, code, user, Db.Now());
        }

        // ---- the drawing -------------------------------------------------------------

        /// <summary>The profile's drawing, relative paths from the program's folder.</summary>
        public static string DxfFile(ProfileInfo p)
        {
            if (p == null || string.IsNullOrWhiteSpace(p.DxfPath)) return null;
            // stored as typed on Windows ("Profiles\\x.dxf"): the separator of the system reading it
            var rel = p.DxfPath.Trim().Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            var path = Path.IsPathRooted(rel) ? rel : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rel);
            return File.Exists(path) ? path : null;
        }

        /// <summary>The profile's section, turned as its record says (null: no drawing).</summary>
        public static Section SectionOf(ProfileInfo p)
        {
            var f = DxfFile(p);
            if (f == null) return null;
            return Section.Load(f, p.Rotation, p.Mirror, p.IgnoredLayers);
        }

        public static void SaveFingerprint(long profileId, Fingerprint f)
        {
            Transaction(c =>
            {
                Db.Execute(c, "DELETE FROM profile_fingerprint WHERE profile_id=@p0", profileId);
                Db.Execute(c, @"INSERT INTO profile_fingerprint (profile_id, bbox_width_mm, bbox_height_mm, closed_regions, line_count, arc_count, area_mm2, perimeter_mm, signature)
VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8)", profileId, f.Width, f.Height, f.ClosedLoops, f.Lines, f.Arcs, f.Area, f.Perimeter, f.Pack());
            });
        }

        public sealed class Similar
        {
            public long ProfileId;
            public string Code = "", Group = "";
            public double Similarity;
        }

        /// <summary>The profiles most like <paramref name="f"/>, best first (only those with references).</summary>
        public static List<Similar> Closest(Fingerprint f, long exceptId, int take = 5)
        {
            var list = new List<Similar>();
            foreach (var row in Rows(@"SELECT fp.profile_id, fp.signature, p.code, pg.name AS grp FROM profile_fingerprint fp
                                       JOIN profile p ON p.id = fp.profile_id JOIN profile_group pg ON pg.id = p.group_id
                                       WHERE p.is_active = 1 AND fp.profile_id <> @p0
                                         AND EXISTS (SELECT 1 FROM profile_reference r WHERE r.profile_id = p.id)", exceptId))
            {
                var other = Fingerprint.Unpack(Text(row, "signature"));
                if (other == null) continue;
                list.Add(new Similar { ProfileId = Id(row, "profile_id").Value, Code = Text(row, "code"), Group = Text(row, "grp"), Similarity = f.Similarity(other) });
            }
            return list.OrderByDescending(s => s.Similarity).Take(take).ToList();
        }

        /// <summary>
        /// The profile to take proposals from: the group's reference profile, the
        /// base profile, or the closest one - the first that has references.
        /// </summary>
        public static ReferenceFinder.Template TemplateFor(ProfileInfo p, Fingerprint mine)
        {
            var candidates = new List<KeyValuePair<long, double>>();
            var refProfile = Scalar("SELECT reference_profile_id FROM profile_group WHERE id=@p0", p.GroupId);
            if (refProfile != null) candidates.Add(new KeyValuePair<long, double>(Convert.ToInt64(refProfile), 1));
            if (p.BaseProfileId.HasValue) candidates.Add(new KeyValuePair<long, double>(p.BaseProfileId.Value, 1));
            if (mine != null)
                foreach (var s in Closest(mine, p.Id, 1)) candidates.Add(new KeyValuePair<long, double>(s.ProfileId, s.Similarity));
            foreach (var cand in candidates)
            {
                if (cand.Key == p.Id) continue;
                var other = Profile(cand.Key);
                if (other == null) continue;
                var refs = References(other.Id);
                if (refs.Count == 0) continue;
                var t = new ReferenceFinder.Template { ProfileCode = other.Code, Width = other.EffectiveWidth, Height = other.EffectiveHeight, Similarity = cand.Value };
                if (mine != null && cand.Value >= 1)
                {
                    var fp = Scalar("SELECT signature FROM profile_fingerprint WHERE profile_id=@p0", other.Id);
                    var of = Fingerprint.Unpack(Convert.ToString(fp));
                    if (of != null) t.Similarity = mine.Similarity(of);
                }
                foreach (var r in refs) t.References[r.Code] = new Section.Pt(r.Y, r.Z);
                return t;
            }
            return null;
        }

        // ---- accessories and placements ------------------------------------------

        public sealed class PlacementInfo
        {
            public long Id, OperationId, AccessoryId, MacroId;
            public string Accessory = "", AccessoryDescription = "", AccessoryType = "", Macro = "", MacroType = "", MacroParams = "";
            public int Seq, ThroughProfile, DepthMode;
            public string Side = "TOP", PositionMode = "", Reference = "";
            public double OffsetY, OffsetZ, Rotation;
            public bool Mirror;
            public double Y, Z, BaseY, BaseZ, CorrY, CorrZ;
            public string XStrategy = "", Status = "";
            public double? XBase, XPitch, Confidence, Deviation;
            public int? XCount;
            public string ConfirmedBy = "", ConfirmedAt = "", UpdatedAt = "";

            public MacroShape Shape()
            {
                return new MacroShape { Code = Macro, Type = MacroType, Params = FlatJson.ParseDictionary(MacroParams), ThroughProfile = ThroughProfile };
            }

            public SideOperation ToOperation()
            {
                return new SideOperation
                {
                    Accessory = Accessory, Seq = Seq, Macro = Shape(), Side = Side, Y = Y, Z = Z, Status = Status,
                    XStrategy = XStrategy, XBase = XBase, XPitch = XPitch, XCount = XCount, Rotation = Rotation, Mirror = Mirror, DepthMode = DepthMode
                };
            }

            public string XText
            {
                get
                {
                    var ci = CultureInfo.InvariantCulture;
                    string b = XBase.HasValue ? XBase.Value.ToString("0.#", ci) : "0";
                    switch ((XStrategy ?? "").ToUpperInvariant())
                    {
                        case "FROM_BOTH_ENDS": return b + " from each end" + (XCount > 2 ? ", " + XCount + " in all" : "");
                        case "FROM_END": return b + " from the end";
                        case "CENTRED": return "centre" + (XBase.HasValue && XBase.Value != 0 ? " + " + b : "");
                        case "DISTRIBUTED": return "from " + b + ", every " + (XPitch.HasValue ? XPitch.Value.ToString("0.#", ci) : "?") + " at most" + (XCount.HasValue ? ", " + XCount + " at least" : "");
                        default: return b + " from the start";
                    }
                }
            }
        }

        private const string PlacementSelect = @"
SELECT rp.id, rp.operation_id, a.id, m.id, a.code, a.description, ag.accessory_type, m.code, m.macro_type, m.params_json,
       op.seq, m.through_profile, op.depth_mode, op.entry_side, op.position_mode, op.reference_code,
       op.offset_y_mm, op.offset_z_mm, op.rotation_deg, op.mirror,
       rp.y_mm, rp.z_mm, rp.base_y_mm, rp.base_z_mm, rp.corr_y_mm, rp.corr_z_mm,
       op.x_strategy, rp.status, op.x_base_mm, op.x_pitch_mm, rp.confidence, rp.deviation_mm, op.x_count,
       rp.confirmed_by, rp.confirmed_at, rp.updated_at
FROM   resolved_placement rp
JOIN   accessory_operation op ON op.id = rp.operation_id
JOIN   accessory a            ON a.id = op.accessory_id
JOIN   accessory_group ag     ON ag.id = a.group_id
JOIN   macro m                ON m.id = op.macro_id";

        public static List<PlacementInfo> Placements(long profileId)
        {
            return Use(c =>
            {
                var list = new List<PlacementInfo>();
                using (var cmd = Db.Command(c, PlacementSelect + " WHERE rp.profile_id = @p0 ORDER BY ag.accessory_type, a.code, op.seq", profileId))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        Func<int, string> S = i => r.IsDBNull(i) ? "" : Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture);
                        Func<int, double?> D = i => r.IsDBNull(i) ? (double?)null : Db.Real(r.GetValue(i));
                        Func<int, long> L = i => r.IsDBNull(i) ? 0 : Convert.ToInt64(r.GetValue(i));
                        list.Add(new PlacementInfo
                        {
                            Id = L(0), OperationId = L(1), AccessoryId = L(2), MacroId = L(3), Accessory = S(4), AccessoryDescription = S(5),
                            AccessoryType = S(6), Macro = S(7), MacroType = S(8), MacroParams = S(9), Seq = (int)L(10), ThroughProfile = (int)L(11),
                            DepthMode = (int)L(12), Side = S(13).Length > 0 ? S(13) : "TOP", PositionMode = S(14), Reference = S(15),
                            OffsetY = D(16) ?? 0, OffsetZ = D(17) ?? 0, Rotation = D(18) ?? 0, Mirror = L(19) != 0,
                            Y = D(20) ?? 0, Z = D(21) ?? 0, BaseY = D(22) ?? 0, BaseZ = D(23) ?? 0, CorrY = D(24) ?? 0, CorrZ = D(25) ?? 0,
                            XStrategy = S(26), Status = S(27), XBase = D(28), XPitch = D(29), Confidence = D(30), Deviation = D(31),
                            XCount = r.IsDBNull(32) ? (int?)null : (int)L(32), ConfirmedBy = S(33), ConfirmedAt = S(34), UpdatedAt = S(35)
                        });
                    }
                return list;
            });
        }

        /// <summary>Works out every placement of the profile again (confirmed ones stay as they are).</summary>
        public static ProfileResolver.Result Resolve(long profileId)
        {
            ProfileResolver.Result result = null;
            Transaction(c => result = new ProfileResolver(c).Resolve(profileId));
            // An operation that no longer applies (its accessory taken off the group)
            // leaves a placement nobody should see as current.
            Exec(@"DELETE FROM resolved_placement WHERE profile_id=@p0 AND status IN ('AUTO','PROPOSED')
                   AND operation_id NOT IN (SELECT op.id FROM profile p
                        JOIN v_group_accessory ga ON ga.profile_group_id = p.group_id
                        JOIN accessory_operation op ON op.accessory_id = ga.accessory_id WHERE p.id = @p0)", profileId);
            return result;
        }

        public static void ConfirmPlacement(long placementId, string user, string reason)
        {
            Transaction(c => new ProfileResolver(c).Confirm(placementId, user, reason));
        }

        /// <summary>Not on this profile: the machine never cuts it; the history keeps why.</summary>
        public static void RejectPlacement(long placementId, string user, string reason)
        {
            Transaction(c =>
            {
                History(c, placementId, user, reason ?? "rejected");
                Db.Execute(c, "UPDATE resolved_placement SET status='REJECTED', confirmed_by=@p1, confirmed_at=@p2, updated_at=@p2 WHERE id=@p0", placementId, user, Db.Now());
            });
        }

        /// <summary>A confirmed or rejected placement back to AUTO (to be tuned and confirmed again), then resolved.</summary>
        public static void ReopenPlacement(long placementId, string user, string reason)
        {
            long profileId = 0;
            Transaction(c =>
            {
                History(c, placementId, user, reason ?? "reopened");
                profileId = Convert.ToInt64(Db.Scalar(c, "SELECT profile_id FROM resolved_placement WHERE id=@p0", placementId));
                Db.Execute(c, "UPDATE resolved_placement SET status='AUTO', confirmed_by=NULL, confirmed_at=NULL, updated_at=@p1 WHERE id=@p0", placementId, Db.Now());
            });
            Resolve(profileId);
        }

        private static void History(IDbConnection c, long placementId, string user, string reason)
        {
            using (var cmd = Db.Command(c, "SELECT y_mm, z_mm, status FROM resolved_placement WHERE id=@p0", placementId))
            using (var r = cmd.ExecuteReader())
                if (r.Read())
                    Db.Execute(c, @"INSERT INTO placement_history (placement_id, y_mm, z_mm, status, changed_by, changed_at, reason)
VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6)", placementId, Db.Real(r[0]), Db.Real(r[1]), Db.ToText(r[2]), user, Db.Now(), reason);
        }

        public static List<Dictionary<string, object>> PlacementHistory(long placementId)
        {
            return Rows("SELECT changed_at, changed_by, status, y_mm, z_mm, reason FROM placement_history WHERE placement_id=@p0 ORDER BY id DESC", placementId);
        }

        /// <summary>
        /// A TRIAL correction: <paramref name="dy"/>/<paramref name="dz"/> added at
        /// SERIES, GROUP or PROFILE scope, for one operation (or the whole accessory
        /// when <paramref name="operationId"/> is null). The placements it moves are
        /// reopened (a moved position must be checked again) and resolved at once.
        /// The accessory's own definition is not touched.
        /// </summary>
        public static void AddCorrection(long profileId, string scope, long accessoryId, long? operationId, double dy, double dz, string reason, string user)
        {
            var p = Profile(profileId);
            long scopeId = scope == "SERIES" ? p.SeriesId : scope == "GROUP" ? p.GroupId : p.Id;
            Transaction(c =>
            {
                new ProfileResolver(c).AddCorrection(scope, scopeId, accessoryId, operationId, dy, dz, reason, user);
                ReopenInScope(c, scope, scopeId, accessoryId, operationId, user, "moved by a correction: " + (reason ?? ""));
            });
            ResolveScope(scope, scopeId);
        }

        /// <summary>
        /// The confirmed placements a correction reaches, on every profile of its
        /// scope, back to AUTO: a moved position is checked again.
        /// </summary>
        private static void ReopenInScope(IDbConnection c, string scope, long scopeId, long? accessoryId, long? operationId, string user, string reason)
        {
            string where = scope == "SERIES"
                ? "rp.profile_id IN (SELECT p.id FROM profile p JOIN profile_group g ON g.id = p.group_id JOIN profile_variant v ON v.id = g.variant_id WHERE v.series_id = @p0)"
                : scope == "GROUP" ? "rp.profile_id IN (SELECT id FROM profile WHERE group_id = @p0)" : "rp.profile_id = @p0";
            var ids = Rows(c, "SELECT rp.id FROM resolved_placement rp JOIN accessory_operation op ON op.id = rp.operation_id WHERE " + where +
                              (accessoryId.HasValue ? " AND op.accessory_id = @p1" : "") + (operationId.HasValue ? " AND rp.operation_id = @p2" : "") +
                              " AND rp.status IN ('CONFIRMED','OVERRIDDEN')",
                           scopeId, accessoryId ?? 0, operationId ?? 0).Select(r => Id(r, "id").Value).ToList();
            foreach (var id in ids)
            {
                History(c, id, user, reason);
                Db.Execute(c, "UPDATE resolved_placement SET status='AUTO', confirmed_by=NULL, confirmed_at=NULL WHERE id=@p0", id);
            }
        }

        /// <summary>Every profile a scope reaches, resolved again.</summary>
        private static void ResolveScope(string scope, long scopeId)
        {
            string profiles = scope == "SERIES"
                ? "SELECT p.id FROM profile p JOIN profile_group g ON g.id = p.group_id JOIN profile_variant v ON v.id = g.variant_id WHERE v.series_id = @p0 AND p.is_active = 1"
                : scope == "GROUP" ? "SELECT id FROM profile WHERE group_id = @p0 AND is_active = 1" : "SELECT id FROM profile WHERE id = @p0";
            foreach (var row in Rows(profiles, scopeId)) Resolve(Id(row, "id").Value);
        }

        public static List<Dictionary<string, object>> Corrections(long profileId)
        {
            var p = Profile(profileId);
            return Rows(@"SELECT pc.id, pc.scope_type, a.code AS accessory, op.seq, pc.correction_y_mm, pc.correction_z_mm, pc.reason, pc.created_by, pc.created_at
FROM placement_correction pc LEFT JOIN accessory a ON a.id = pc.accessory_id LEFT JOIN accessory_operation op ON op.id = pc.operation_id
WHERE (pc.scope_type='SERIES' AND pc.scope_id=@p0) OR (pc.scope_type='GROUP' AND pc.scope_id=@p1) OR (pc.scope_type='PROFILE' AND pc.scope_id=@p2)
ORDER BY pc.id DESC", p.SeriesId, p.GroupId, p.Id);
        }

        /// <summary>
        /// A correction taken away: the placements it moved go back - and those a
        /// person had confirmed at the moved position are checked again.
        /// </summary>
        public static void RemoveCorrection(long correctionId, long profileId, string user = null)
        {
            var row = Rows("SELECT scope_type, scope_id, accessory_id, operation_id FROM placement_correction WHERE id=@p0", correctionId).FirstOrDefault();
            if (row == null) { Resolve(profileId); return; }
            string scope = Text(row, "scope_type");
            long scopeId = Id(row, "scope_id") ?? profileId;
            Transaction(c =>
            {
                ReopenInScope(c, scope, scopeId, Id(row, "accessory_id"), Id(row, "operation_id"), user ?? "", "a correction was removed");
                Db.Execute(c, "DELETE FROM placement_correction WHERE id=@p0", correctionId);
            });
            ResolveScope(scope, scopeId);
            Resolve(profileId);
        }

        /// <summary>
        /// The accessories of the profile's group, with whether each is on: given one by one, or with an accessory group attached to it
        /// (<c>via_group</c>: that group's name) - an accessory added to that accessory group later is there at once.
        /// </summary>
        public static List<Dictionary<string, object>> GroupAccessories(long groupId)
        {
            return Rows(@"SELECT ga.id, a.id AS accessory_id, a.code, a.description, ag.accessory_type, ga.is_enabled, NULL AS via_group,
                                 (SELECT COUNT(*) FROM accessory_operation op WHERE op.accessory_id = a.id) AS operations
                          FROM group_accessory_assignment ga JOIN accessory a ON a.id = ga.accessory_id JOIN accessory_group ag ON ag.id = a.group_id
                          WHERE ga.profile_group_id = @p0
                          UNION ALL
                          SELECT NULL, a.id, a.code, a.description, ag.accessory_type, 1, ag.name,
                                 (SELECT COUNT(*) FROM accessory_operation op WHERE op.accessory_id = a.id)
                          FROM group_accessory_group gg JOIN accessory_group ag ON ag.id = gg.accessory_group_id
                          JOIN accessory a ON a.group_id = ag.id AND a.is_active = 1
                          WHERE gg.profile_group_id = @p0 AND gg.is_enabled = 1
                            AND NOT EXISTS (SELECT 1 FROM group_accessory_assignment x WHERE x.profile_group_id = @p0 AND x.accessory_id = a.id)
                          ORDER BY 5, 3", groupId);
        }

        /// <summary>Accessories the group can take: those of its hardware interface first, then the rest (not those it has already).</summary>
        public static List<Dictionary<string, object>> AccessoriesFor(long groupId)
        {
            return Rows(@"SELECT a.id, a.code, a.description, ag.accessory_type,
                                 CASE WHEN s.hardware_interface_id IS NOT NULL AND s.hardware_interface_id = g.hardware_interface_id THEN 1 ELSE 0 END AS compatible
                          FROM accessory a JOIN accessory_group ag ON ag.id = a.group_id LEFT JOIN accessory_series s ON s.id = a.series_id
                          JOIN profile_group g ON g.id = @p0
                          WHERE a.is_active = 1 AND a.id NOT IN (SELECT accessory_id FROM group_accessory_assignment WHERE profile_group_id = @p0)
                            AND a.id NOT IN (SELECT accessory_id FROM v_group_accessory WHERE profile_group_id = @p0)
                          ORDER BY compatible DESC, ag.accessory_type, a.code", groupId);
        }

        /// <summary>Every accessory group, with whether it is attached to the profile group (<c>attached</c> 1) and how many accessories it has.</summary>
        public static List<Dictionary<string, object>> AccessoryGroupsOf(long groupId)
        {
            return Rows(@"SELECT ag.id, ag.name, ag.accessory_type,
                                 CASE WHEN EXISTS (SELECT 1 FROM group_accessory_group gg WHERE gg.profile_group_id = @p0 AND gg.accessory_group_id = ag.id
                                                   AND gg.is_enabled = 1) THEN 1 ELSE 0 END AS attached,
                                 (SELECT COUNT(*) FROM accessory a WHERE a.group_id = ag.id AND a.is_active = 1) AS accessories
                          FROM accessory_group ag ORDER BY ag.accessory_type, ag.name", groupId);
        }

        /// <summary>
        /// Attaches an accessory group to a profile group (or takes it off): its accessories - now and those added to it later - on
        /// every profile of the group. The profiles' placements are worked out again as each is shown.
        /// </summary>
        public static void AttachAccessoryGroup(long groupId, long accessoryGroupId, bool attached)
        {
            Transaction(c =>
            {
                if (!attached) { Db.Execute(c, "DELETE FROM group_accessory_group WHERE profile_group_id=@p0 AND accessory_group_id=@p1", groupId, accessoryGroupId); return; }
                if (Db.Count(c, "SELECT COUNT(*) FROM group_accessory_group WHERE profile_group_id=@p0 AND accessory_group_id=@p1", groupId, accessoryGroupId) > 0)
                    Db.Execute(c, "UPDATE group_accessory_group SET is_enabled=1 WHERE profile_group_id=@p0 AND accessory_group_id=@p1", groupId, accessoryGroupId);
                else
                    Db.Execute(c, "INSERT INTO group_accessory_group (profile_group_id, accessory_group_id, is_enabled) VALUES (@p0,@p1,1)", groupId, accessoryGroupId);
            });
        }

        public static void AssignAccessory(long groupId, long accessoryId, bool enabled)
        {
            Transaction(c =>
            {
                if (Db.Count(c, "SELECT COUNT(*) FROM group_accessory_assignment WHERE profile_group_id=@p0 AND accessory_id=@p1", groupId, accessoryId) > 0)
                    Db.Execute(c, "UPDATE group_accessory_assignment SET is_enabled=@p2 WHERE profile_group_id=@p0 AND accessory_id=@p1", groupId, accessoryId, enabled ? 1 : 0);
                else
                    Db.Execute(c, "INSERT INTO group_accessory_assignment (profile_group_id, accessory_id, is_enabled) VALUES (@p0,@p1,@p2)", groupId, accessoryId, enabled ? 1 : 0);
            });
        }

        public static void UnassignAccessory(long groupId, long accessoryId)
        {
            Exec("DELETE FROM group_accessory_assignment WHERE profile_group_id=@p0 AND accessory_id=@p1", groupId, accessoryId);
        }

        // ---- the machine layer ----------------------------------------------------

        /// <summary>
        /// The machine this program runs, in the machine layer: its instance, and
        /// the tool positions and faces it has (created once, with no diameters -
        /// a tool's size is entered by someone who has measured it).
        /// </summary>
        public static long EnsureMachine(string seriesCode, string name, IEnumerable<KeyValuePair<string, int>> toolPositions, IEnumerable<Face> faces)
        {
            long instance = 0;
            Transaction(c =>
            {
                var series = Db.Scalar(c, "SELECT id FROM machine_series WHERE code=@p0", seriesCode);
                if (series == null)
                {
                    series = Insert(c, "INSERT INTO machine_series (category_id, code, name, branch) VALUES (1, @p0, @p1, 'BOTH')", seriesCode, name);
                    Db.Execute(c, "INSERT INTO series_capability (series_id, capability_code) VALUES (@p0,'MILL_ACCESSORY')", series);
                }
                long sid = Convert.ToInt64(series);
                var inst = Db.Scalar(c, "SELECT id FROM machine_instance WHERE series_id=@p0 ORDER BY is_main DESC, id LIMIT 1", sid);
                instance = inst != null ? Convert.ToInt64(inst)
                    : Insert(c, "INSERT INTO machine_instance (series_id, name, module_id, is_main) VALUES (@p0,@p1,@p2,1)", sid, name, seriesCode);
                if (Db.Count(c, "SELECT COUNT(*) FROM mounted_tool WHERE instance_id=@p0", instance) == 0)
                    foreach (var t in toolPositions ?? new KeyValuePair<string, int>[0])
                        Db.Execute(c, "INSERT INTO mounted_tool (instance_id, position, tool_code, tool_type, unit_no, is_active) VALUES (@p0,@p1,@p1,'END_MILL',@p2,1)",
                                   instance, t.Key, t.Value);
                if (Db.Count(c, "SELECT COUNT(*) FROM machine_face WHERE instance_id=@p0", instance) == 0)
                    foreach (var f in faces ?? new Face[0])
                        Db.Execute(c, "INSERT INTO machine_face (instance_id, unit_no, entry_side, angle_deg, is_enabled, note) VALUES (@p0,@p1,@p2,@p3,1,@p4)",
                                   instance, f.Unit, f.Side, f.Angle, "assumed - check on the machine");
            });
            return instance;
        }

        public static long SeriesOfInstance(long instance)
        {
            return Convert.ToInt64(Scalar("SELECT series_id FROM machine_instance WHERE id=@p0", instance));
        }

        public static List<Tool> Tools(long instance)
        {
            return Rows("SELECT id, position, tool_code, tool_type, diameter_mm, usable_length_mm, max_rpm, unit_no, is_active FROM mounted_tool WHERE instance_id=@p0 ORDER BY unit_no, position", instance)
                .Select(r => new Tool
                {
                    Id = Id(r, "id").Value, Position = Text(r, "position"), Code = Text(r, "tool_code"), Type = Text(r, "tool_type"),
                    Diameter = Real(r, "diameter_mm"), UsableLength = Real(r, "usable_length_mm"), MaxRpm = Real(r, "max_rpm"),
                    Unit = (int)(Id(r, "unit_no") ?? 1), Active = (Id(r, "is_active") ?? 1) != 0
                }).ToList();
        }

        public static List<Face> Faces(long instance)
        {
            return Rows("SELECT unit_no, entry_side, angle_deg, is_enabled FROM machine_face WHERE instance_id=@p0 ORDER BY unit_no, angle_deg", instance)
                .Select(r => new Face { Unit = (int)(Id(r, "unit_no") ?? 1), Side = Text(r, "entry_side"), Angle = Real(r, "angle_deg") ?? 0, Enabled = (Id(r, "is_enabled") ?? 1) != 0 }).ToList();
        }

        /// <summary>The feeds of the machine: its own instance's, else its series'.</summary>
        public static List<Feed> Feeds(long instance)
        {
            long series = SeriesOfInstance(instance);
            var rows = Rows(@"SELECT mc.code, fs.tool_type, fs.entry_speed, fs.feed_speed, fs.spindle_rpm, fs.plunge_rate, fs.thin_wall_factor, fs.thin_wall_mm, fs.instance_id
FROM feed_strategy fs JOIN milling_class mc ON mc.id = fs.milling_class_id
WHERE fs.instance_id = @p0 OR (fs.instance_id IS NULL AND fs.series_id = @p1) ORDER BY fs.instance_id DESC", instance, series);
            var list = new List<Feed>();
            foreach (var r in rows)
            {
                var f = new Feed
                {
                    MillingClass = Text(r, "code"), ToolType = Text(r, "tool_type"), Entry = Real(r, "entry_speed") ?? 0, Cut = Real(r, "feed_speed") ?? 0,
                    Rpm = Real(r, "spindle_rpm"), Plunge = Real(r, "plunge_rate"), ThinFactor = Real(r, "thin_wall_factor") ?? 1, ThinMm = Real(r, "thin_wall_mm") ?? 1.5
                };
                if (!list.Any(x => x.MillingClass == f.MillingClass && x.ToolType == f.ToolType)) list.Add(f);
            }
            return list;
        }

        public static string Setting(long instance, string code, string fallback)
        {
            var v = Scalar("SELECT value FROM machine_setting WHERE instance_id=@p0 AND code=@p1", instance, code);
            return v == null ? fallback : Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static void SetSetting(long instance, string code, string value)
        {
            Transaction(c =>
            {
                Db.Execute(c, "DELETE FROM machine_setting WHERE instance_id=@p0 AND code=@p1", instance, code);
                Db.Execute(c, "INSERT INTO machine_setting (instance_id, code, value) VALUES (@p0,@p1,@p2)", instance, code, value);
            });
        }

        public static ProgramSettings ProgramSettings(long instance, ProfileInfo p)
        {
            var ci = CultureInfo.InvariantCulture;
            Func<string, double, double> num = (code, def) =>
            {
                double v;
                return double.TryParse(Setting(instance, code, ""), NumberStyles.Float, ci, out v) ? v : def;
            };
            var s = new ProgramSettings
            {
                SafeZ = num("safe_z_mm", 30), ApproachFeed = num("approach_feed", 3000), PeckDepth = num("peck_depth_mm", 4),
                Breakthrough = num("breakthrough_mm", 0.5), Incremental = Setting(instance, "incremental", "1") != "0",
                Material = p != null && !string.IsNullOrEmpty(p.Material) ? p.Material : "PVC"
            };
            if (p != null)
                foreach (var r in Rows(@"SELECT lm.dxf_layer_name, mc.code FROM layer_milling_map lm JOIN milling_class mc ON mc.id = lm.milling_class_id WHERE lm.profile_series_id=@p0", p.SeriesId))
                    s.LayerClass[Text(r, "dxf_layer_name")] = Text(r, "code");
            return s;
        }

        // ---- the end milling program -----------------------------------------------------

        public static Dictionary<string, object> EndMillProgram(long profileId, long instance)
        {
            return Rows("SELECT gcode, tool_radius_mm, cut_depth_mm, updated_at, updated_by FROM end_mill_program WHERE profile_id=@p0 AND instance_id=@p1", profileId, instance).FirstOrDefault();
        }

        public static void SaveEndMillProgram(long profileId, long instance, string gcode, double toolRadius, double depth, string user)
        {
            Transaction(c =>
            {
                Db.Execute(c, "DELETE FROM end_mill_program WHERE profile_id=@p0 AND instance_id=@p1", profileId, instance);
                Db.Execute(c, "INSERT INTO end_mill_program (profile_id, instance_id, tool_radius_mm, cut_depth_mm, gcode, updated_at, updated_by) VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6)",
                           profileId, instance, toolRadius, depth, gcode, Db.Now(), user);
            });
        }
    }
}
