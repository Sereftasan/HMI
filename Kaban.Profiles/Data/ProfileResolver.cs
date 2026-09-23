using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;

namespace Kaban.Profiles.Data
{
    /// <summary>
    /// Works out where every accessory operation sits on a profile, and writes
    /// the answers into resolved_placement.
    ///
    ///     final = reference + operation offset
    ///           + series correction + group correction + profile correction
    ///
    /// TWO RULES THIS CLASS MUST NEVER BREAK
    /// -------------------------------------
    ///  1. It writes AUTO or PROPOSED. It NEVER writes CONFIRMED - only a person
    ///     does, after checking the drawing or a trial cut. A machine reads
    ///     CONFIRMED rows only, so a value nobody has looked at cannot be cut.
    ///  2. A missing datum is an ERROR that names the datum. It is never a
    ///     default, a zero, or a nearby profile's number: silently inventing a
    ///     coordinate is how the wrong hole gets drilled.
    /// </summary>
    public class ProfileResolver
    {
        private readonly IDbConnection _conn;

        public ProfileResolver(IDbConnection conn) { _conn = conn; }

        public class Result
        {
            public int Created;
            public int Skipped;
            /// <summary>One line per operation that could not be resolved, and why.</summary>
            public readonly List<string> Problems = new List<string>();
            public bool Ok { get { return Problems.Count == 0; } }
        }

        private class Ref
        {
            public double Y, Z;
            public string Source;
            public double? Confidence;
        }

        private class Context
        {
            public long ProfileId, GroupId, SeriesId;
            public long? ReferenceProfileId, BaseProfileId;
            public double? WidthMm, HeightMm;
        }

        // ==================================================================
        // CONTEXT AND REFERENCES
        // ==================================================================

        private Context LoadContext(long profileId)
        {
            using (var cmd = Db.Command(_conn, @"
SELECT p.id, p.group_id, ps.id, pg.reference_profile_id, p.base_profile_id,
       COALESCE(p.width_mm,  pg.default_width_mm),
       COALESCE(p.height_mm, pg.default_height_mm)
FROM   profile p
JOIN   profile_group pg    ON pg.id = p.group_id
JOIN   profile_variant pvr ON pvr.id = pg.variant_id
JOIN   profile_series ps   ON ps.id = pvr.series_id
WHERE  p.id = @p0", profileId))
            using (var r = cmd.ExecuteReader())
            {
                if (!r.Read()) return null;
                return new Context
                {
                    ProfileId = Convert.ToInt64(r[0]),
                    GroupId = Convert.ToInt64(r[1]),
                    SeriesId = Convert.ToInt64(r[2]),
                    ReferenceProfileId = r[3] == DBNull.Value ? (long?)null : Convert.ToInt64(r[3]),
                    BaseProfileId = r[4] == DBNull.Value ? (long?)null : Convert.ToInt64(r[4]),
                    WidthMm = Db.ToDouble(r[5]),
                    HeightMm = Db.ToDouble(r[6])
                };
            }
        }

        /// <summary>
        /// A profile's datums, inheriting any it lacks from its base profile.
        /// Guards against a circular base chain, which is a data error a person
        /// can make in the editor and which would otherwise hang the resolver.
        /// </summary>
        private Dictionary<string, Ref> LoadReferences(long profileId, HashSet<long> seen = null)
        {
            var result = new Dictionary<string, Ref>(StringComparer.OrdinalIgnoreCase);
            seen = seen ?? new HashSet<long>();
            if (!seen.Add(profileId)) return result;

            using (var cmd = Db.Command(_conn,
                "SELECT reference_code, y_mm, z_mm, source, confidence " +
                "FROM profile_reference WHERE profile_id=@p0", profileId))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    result[Db.ToText(r[0])] = new Ref
                    {
                        Y = Db.Real(r[1]),
                        Z = Db.Real(r[2]),
                        Source = Db.ToText(r[3]),
                        Confidence = Db.ToDouble(r[4])
                    };

            var baseId = Db.Scalar(_conn, "SELECT base_profile_id FROM profile WHERE id=@p0", profileId);
            if (baseId != null)
            {
                foreach (var kv in LoadReferences(Convert.ToInt64(baseId), seen))
                    if (!result.ContainsKey(kv.Key))
                        result[kv.Key] = new Ref
                        {
                            Y = kv.Value.Y,
                            Z = kv.Value.Z,
                            Source = "INHERITED",
                            Confidence = kv.Value.Confidence
                        };
            }
            return result;
        }

        /// <summary>Series, then group, then profile - widest scope first, all summed.</summary>
        private void LoadCorrections(Context ctx, long accessoryId, long operationId,
                                     out double cy, out double cz)
        {
            cy = 0; cz = 0;
            var scopes = new[]
            {
                new { Type = "SERIES",  Id = ctx.SeriesId },
                new { Type = "GROUP",   Id = ctx.GroupId },
                new { Type = "PROFILE", Id = ctx.ProfileId }
            };

            foreach (var scope in scopes)
            {
                using (var cmd = Db.Command(_conn, @"
SELECT correction_y_mm, correction_z_mm FROM placement_correction
WHERE  scope_type=@p0 AND scope_id=@p1
  AND (accessory_id IS NULL OR accessory_id=@p2)
  AND (operation_id IS NULL OR operation_id=@p3)",
                    scope.Type, scope.Id, accessoryId, operationId))
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        cy += Db.Real(r[0]);
                        cz += Db.Real(r[1]);
                    }
            }
        }

        // ==================================================================
        // POSITION MODES
        // ==================================================================

        private static bool TryBasePosition(OperationRow op, Dictionary<string, Ref> refs, Context ctx,
                                            out double y, out double z,
                                            out string source, out double? confidence,
                                            out string problem)
        {
            y = z = 0; source = null; confidence = null; problem = null;
            string mode = (op.PositionMode ?? "REFERENCE_OFFSET").ToUpperInvariant();

            Func<string, Ref> get = code =>
            {
                Ref v;
                return (code != null && refs.TryGetValue(code, out v)) ? v : null;
            };

            switch (mode)
            {
                case "FIXED":
                    y = op.OffsetYMm; z = op.OffsetZMm;
                    source = "MANUAL";
                    return true;

                case "PROFILE_CENTER":
                {
                    var o = get("OUTSIDE_SURFACE");
                    var i = get("INSIDE_SURFACE");
                    if (o == null || i == null)
                    {
                        problem = "needs OUTSIDE_SURFACE and INSIDE_SURFACE";
                        return false;
                    }
                    y = (o.Y + i.Y) / 2.0 + op.OffsetYMm;
                    z = (o.Z + i.Z) / 2.0 + op.OffsetZMm;
                    // the weaker of the two datums decides how much to trust the result
                    source = (o.Source == "AI_PROPOSED" || i.Source == "AI_PROPOSED")
                             ? "AI_PROPOSED" : o.Source;
                    confidence = Math.Min(o.Confidence ?? 1.0, i.Confidence ?? 1.0);
                    return true;
                }

                case "FROM_INSIDE":
                {
                    var i = get("INSIDE_SURFACE");
                    if (i == null) { problem = "needs INSIDE_SURFACE"; return false; }
                    y = i.Y - op.OffsetYMm;
                    z = i.Z + op.OffsetZMm;
                    source = i.Source; confidence = i.Confidence;
                    return true;
                }

                default:
                {
                    var reference = get(op.ReferenceCode);
                    if (reference == null)
                    {
                        problem = "needs " + (op.ReferenceCode ?? "(no reference set)");
                        return false;
                    }
                    y = reference.Y + op.OffsetYMm;
                    z = reference.Z + op.OffsetZMm;

                    if (mode == "RATIO_OF_WIDTH")  y += (op.Ratio ?? 0) * (ctx.WidthMm ?? 0);
                    if (mode == "RATIO_OF_HEIGHT") z += (op.Ratio ?? 0) * (ctx.HeightMm ?? 0);

                    source = reference.Source; confidence = reference.Confidence;
                    return true;
                }
            }
        }

        // ==================================================================
        // RESOLVE
        // ==================================================================

        public Result Resolve(long profileId)
        {
            var result = new Result();
            var ctx = LoadContext(profileId);
            if (ctx == null) { result.Problems.Add("no such profile: " + profileId); return result; }

            var refs = LoadReferences(profileId);
            var referenceRefs = ctx.ReferenceProfileId.HasValue && ctx.ReferenceProfileId.Value != profileId
                ? LoadReferences(ctx.ReferenceProfileId.Value)
                : new Dictionary<string, Ref>(StringComparer.OrdinalIgnoreCase);

            var ops = new AccessoryRepository(_conn).OperationsForProfile(profileId);

            foreach (var op in ops)
            {
                double baseY, baseZ, confidenceValue;
                string source, problem;
                double? confidence;

                if (!TryBasePosition(op, refs, ctx, out baseY, out baseZ,
                                     out source, out confidence, out problem))
                {
                    result.Skipped++;
                    result.Problems.Add(op.AccessoryCode + " op" + op.Seq + ": " + problem);
                    continue;
                }

                double cy, cz;
                LoadCorrections(ctx, op.AccessoryId, op.Id, out cy, out cz);
                double y = baseY + cy, z = baseZ + cz;

                double? deviation = null;
                Ref mine, theirs;
                if (op.ReferenceCode != null &&
                    refs.TryGetValue(op.ReferenceCode, out mine) &&
                    referenceRefs.TryGetValue(op.ReferenceCode, out theirs))
                {
                    double dy = mine.Y - theirs.Y, dz = mine.Z - theirs.Z;
                    deviation = Math.Round(Math.Sqrt(dy * dy + dz * dz), 3);
                }

                // AI-derived datums produce PROPOSED, which stays out of production
                // until a person confirms it.
                string status = source == "AI_PROPOSED" ? "PROPOSED" : "AUTO";
                confidenceValue = confidence ?? 0;

                UpsertPlacement(profileId, op, y, z, baseY, baseZ, cy, cz,
                                status, confidence, deviation);
                result.Created++;
            }
            return result;
        }

        /// <summary>
        /// Writes a placement, LEAVING CONFIRMED AND REJECTED ROWS ALONE.
        ///
        /// Re-resolving happens whenever a correction or a datum changes, and it
        /// must never quietly move a position a person has already checked and
        /// signed off. Expressed as a WHERE clause rather than a convention so it
        /// holds however this is called.
        /// </summary>
        private void UpsertPlacement(long profileId, OperationRow op,
                                     double y, double z, double baseY, double baseZ,
                                     double cy, double cz,
                                     string status, double? confidence, double? deviation)
        {
            int updated = Db.Execute(_conn, @"
UPDATE resolved_placement
SET    y_mm=@p2, z_mm=@p3, base_y_mm=@p4, base_z_mm=@p5,
       corr_y_mm=@p6, corr_z_mm=@p7, status=@p8, confidence=@p9,
       deviation_mm=@p10, x_strategy=@p11, x_base_mm=@p12, x_pitch_mm=@p13,
       x_count=@p14, updated_at=@p15
WHERE  profile_id=@p0 AND operation_id=@p1
  AND  status NOT IN ('CONFIRMED','REJECTED')",
                profileId, op.Id, y, z, baseY, baseZ, cy, cz, status, confidence,
                deviation, op.XStrategy, op.XBaseMm, op.XPitchMm, op.XCount, Db.Now());

            if (updated > 0) return;

            // Nothing updated: either the row does not exist, or it is confirmed
            // and must be left as it is.
            long existing = Db.Count(_conn,
                "SELECT COUNT(*) FROM resolved_placement WHERE profile_id=@p0 AND operation_id=@p1",
                profileId, op.Id);
            if (existing > 0) return;

            Db.Execute(_conn, @"
INSERT INTO resolved_placement
 (profile_id, operation_id, y_mm, z_mm, base_y_mm, base_z_mm, corr_y_mm, corr_z_mm,
  x_strategy, x_base_mm, x_pitch_mm, x_count, status, confidence, deviation_mm, updated_at)
VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13,@p14,@p15)",
                profileId, op.Id, y, z, baseY, baseZ, cy, cz,
                op.XStrategy, op.XBaseMm, op.XPitchMm, op.XCount,
                status, confidence, deviation, Db.Now());
        }

        // ==================================================================
        // READING BACK
        // ==================================================================

        /// <summary>
        /// WHAT A MACHINE MAY CUT. CONFIRMED and OVERRIDDEN only.
        ///
        /// This replaces the database view the schema used to carry - views are
        /// not portable across providers - but the rule it enforces is unchanged
        /// and is the reason the method exists at all.
        /// </summary>
        public List<PlacementRow> CuttablePlacements(long profileId)
        {
            return ReadPlacements(profileId, "AND rp.status IN ('CONFIRMED','OVERRIDDEN')");
        }

        /// <summary>Everything, whatever its status - for the editing screen.</summary>
        public List<PlacementRow> AllPlacements(long profileId)
        {
            return ReadPlacements(profileId, "");
        }

        /// <summary>Waiting for a person, least trustworthy first.</summary>
        public List<PlacementRow> ReviewQueue()
        {
            var list = new List<PlacementRow>();
            using (var cmd = Db.Command(_conn, @"
SELECT rp.id, rp.profile_id, rp.operation_id, a.code, op.seq, m.code,
       rp.y_mm, rp.z_mm, rp.base_y_mm, rp.base_z_mm, rp.corr_y_mm, rp.corr_z_mm,
       rp.status, rp.confidence, rp.deviation_mm
FROM   resolved_placement rp
JOIN   accessory_operation op ON op.id = rp.operation_id
JOIN   accessory a            ON a.id = op.accessory_id
JOIN   macro m                ON m.id = op.macro_id
WHERE  rp.status IN ('AUTO','PROPOSED')
ORDER BY COALESCE(rp.confidence, 0) ASC, rp.deviation_mm DESC"))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) list.Add(ReadPlacement(r));
            return list;
        }

        private List<PlacementRow> ReadPlacements(long profileId, string extra)
        {
            var list = new List<PlacementRow>();
            using (var cmd = Db.Command(_conn, @"
SELECT rp.id, rp.profile_id, rp.operation_id, a.code, op.seq, m.code,
       rp.y_mm, rp.z_mm, rp.base_y_mm, rp.base_z_mm, rp.corr_y_mm, rp.corr_z_mm,
       rp.status, rp.confidence, rp.deviation_mm
FROM   resolved_placement rp
JOIN   profile p              ON p.id = rp.profile_id
JOIN   accessory_operation op ON op.id = rp.operation_id
JOIN   accessory a            ON a.id = op.accessory_id
JOIN   macro m                ON m.id = op.macro_id
WHERE  rp.profile_id = @p0 AND p.is_active = 1 " + extra + @"
ORDER BY a.code, op.seq", profileId))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) list.Add(ReadPlacement(r));
            return list;
        }

        private static PlacementRow ReadPlacement(IDataReader r)
        {
            return new PlacementRow
            {
                Id = Convert.ToInt64(r[0]),
                ProfileId = Convert.ToInt64(r[1]),
                OperationId = Convert.ToInt64(r[2]),
                AccessoryCode = Db.ToText(r[3]),
                Seq = Convert.ToInt32(r[4]),
                MacroCode = Db.ToText(r[5]),
                YMm = Db.Real(r[6]),
                ZMm = Db.Real(r[7]),
                BaseYMm = Db.ToDouble(r[8]) ?? 0,
                BaseZMm = Db.ToDouble(r[9]) ?? 0,
                CorrYMm = Db.ToDouble(r[10]) ?? 0,
                CorrZMm = Db.ToDouble(r[11]) ?? 0,
                Status = Db.ToText(r[12]),
                Confidence = Db.ToDouble(r[13]),
                DeviationMm = Db.ToDouble(r[14])
            };
        }

        /// <summary>
        /// A person signing off a placement. The ONLY route to CONFIRMED, and the
        /// previous value is kept - a position that was right last month and wrong
        /// today is a question somebody will ask.
        /// </summary>
        public void Confirm(long placementId, string user, string reason = null)
        {
            using (var cmd = Db.Command(_conn,
                "SELECT y_mm, z_mm, status FROM resolved_placement WHERE id=@p0", placementId))
            using (var r = cmd.ExecuteReader())
                if (r.Read())
                {
                    Db.Execute(_conn, @"
INSERT INTO placement_history (placement_id, y_mm, z_mm, status, changed_by, changed_at, reason)
VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6)",
                        placementId, Db.Real(r[0]), Db.Real(r[1]),
                        Db.ToText(r[2]), user, Db.Now(), reason ?? "confirmed");
                }

            Db.Execute(_conn,
                "UPDATE resolved_placement SET status='CONFIRMED', confirmed_by=@p1, " +
                "confirmed_at=@p2, updated_at=@p2 WHERE id=@p0", placementId, user, Db.Now());
        }

        /// <summary>Records a correction and re-resolves so the effect is visible at once.</summary>
        public void AddCorrection(string scopeType, long scopeId, long? accessoryId, long? operationId,
                                  double correctionY, double correctionZ, string reason, string user)
        {
            Db.Execute(_conn, @"
INSERT INTO placement_correction
 (scope_type, scope_id, accessory_id, operation_id, correction_y_mm, correction_z_mm,
  reason, created_by, created_at)
VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8)",
                scopeType, scopeId, accessoryId, operationId,
                correctionY, correctionZ, reason, user, Db.Now());
        }
    }
}

