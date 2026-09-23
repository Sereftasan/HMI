"""Reference resolver for schema_v2 - machine-independent.

NOTE ON TIMESTAMPS: updated_at has no database default, on purpose - each
provider spells "now" differently (GETUTCDATE / now() / datetime('now')), so
setting it in code is what keeps the schema portable. Every write here supplies
it explicitly, and the C# port must do the same.

resolve_profile(db, profile_id) materialises every accessory operation for a
profile from: the group's accessory assignments, each accessory's operations,
the profile's own references, and the correction chain.

    final = reference + operation offset + series corr + group corr + profile corr

Not production code. It exists so the design can be executed rather than argued
about, and so the C# port has something to match.
"""

def _utcnow():
    import datetime
    return datetime.datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ")


def _profile_context(db, profile_id):
    row = db.execute("""
        SELECT p.id, p.group_id, p.width_mm, p.height_mm, p.base_profile_id,
               pg.default_width_mm, pg.default_height_mm, pg.profile_type,
               pg.reference_profile_id, pv.id AS variant_id, ps.id AS series_id
        FROM profile p
        JOIN profile_group pg   ON pg.id = p.group_id
        JOIN profile_variant pv ON pv.id = pg.variant_id
        JOIN profile_series ps  ON ps.id = pv.series_id
        WHERE p.id = ?""", (profile_id,)).fetchone()
    if not row: return None
    ctx = dict(zip([d[0] for d in db.execute("""
        SELECT p.id, p.group_id, p.width_mm, p.height_mm, p.base_profile_id,
               pg.default_width_mm, pg.default_height_mm, pg.profile_type,
               pg.reference_profile_id, pv.id AS variant_id, ps.id AS series_id
        FROM profile p
        JOIN profile_group pg   ON pg.id = p.group_id
        JOIN profile_variant pv ON pv.id = pg.variant_id
        JOIN profile_series ps  ON ps.id = pv.series_id
        WHERE p.id = ?""", (profile_id,)).description], row))
    # dimensions fall back: own -> group default -> base profile
    if ctx["width_mm"] is None:  ctx["width_mm"]  = ctx["default_width_mm"]
    if ctx["height_mm"] is None: ctx["height_mm"] = ctx["default_height_mm"]
    if (ctx["width_mm"] is None or ctx["height_mm"] is None) and ctx["base_profile_id"]:
        b = _profile_context(db, ctx["base_profile_id"])
        if b:
            ctx["width_mm"]  = ctx["width_mm"]  if ctx["width_mm"]  is not None else b["width_mm"]
            ctx["height_mm"] = ctx["height_mm"] if ctx["height_mm"] is not None else b["height_mm"]
    return ctx


def _references(db, profile_id, _seen=None):
    """This profile's datums, inheriting any it lacks from its base profile."""
    _seen = _seen or set()
    if profile_id in _seen: return {}          # guards a circular base chain
    _seen.add(profile_id)
    refs = {r[0]: {"y": r[1], "z": r[2], "source": r[3], "confidence": r[4]}
            for r in db.execute("""SELECT reference_code, y_mm, z_mm, source, confidence
                                   FROM profile_reference WHERE profile_id=?""", (profile_id,))}
    base = db.execute("SELECT base_profile_id FROM profile WHERE id=?", (profile_id,)).fetchone()
    if base and base[0]:
        for code, v in _references(db, base[0], _seen).items():
            if code not in refs:
                v = dict(v); v["source"] = "INHERITED"
                refs[code] = v
    return refs


def _corrections(db, ctx, accessory_id, operation_id):
    """Sum the correction chain: series -> group -> profile, widest first."""
    cy = cz = 0.0
    scopes = (("SERIES", ctx["series_id"]), ("GROUP", ctx["group_id"]), ("PROFILE", ctx["id"]))
    for scope_type, scope_id in scopes:
        for y, z in db.execute("""
                SELECT correction_y_mm, correction_z_mm FROM placement_correction
                WHERE scope_type=? AND scope_id=?
                  AND (accessory_id IS NULL OR accessory_id=?)
                  AND (operation_id IS NULL OR operation_id=?)""",
                (scope_type, scope_id, accessory_id, operation_id)):
            cy += y; cz += z
    return cy, cz


def _base_position(op, refs, ctx):
    """Apply the operation's position mode. Returns (y, z, source, confidence)
    or (None, None, reason, None) when a needed datum is missing."""
    mode = op["position_mode"]
    code = op["reference_code"]

    def ref(c):
        r = refs.get(c)
        return r if r else None

    if mode == "FIXED":
        return op["offset_y_mm"], op["offset_z_mm"], "MANUAL", None

    if mode == "PROFILE_CENTER":
        o, i = ref("OUTSIDE_SURFACE"), ref("INSIDE_SURFACE")
        if not o or not i: return None, None, "needs OUTSIDE_SURFACE and INSIDE_SURFACE", None
        y = (o["y"] + i["y"]) / 2.0 + op["offset_y_mm"]
        z = (o["z"] + i["z"]) / 2.0 + op["offset_z_mm"]
        worst = min(o.get("confidence") or 1.0, i.get("confidence") or 1.0)
        src = "AI_PROPOSED" if "AI_PROPOSED" in (o["source"], i["source"]) else o["source"]
        return y, z, src, worst

    if mode == "FROM_INSIDE":
        i = ref("INSIDE_SURFACE")
        if not i: return None, None, "needs INSIDE_SURFACE", None
        return i["y"] - op["offset_y_mm"], i["z"] + op["offset_z_mm"], i["source"], i.get("confidence")

    r = ref(code)
    if not r: return None, None, "needs " + str(code), None

    if mode == "RATIO_OF_WIDTH":
        y = r["y"] + op["offset_y_mm"] + (op["ratio"] or 0) * (ctx["width_mm"] or 0)
        z = r["z"] + op["offset_z_mm"]
    elif mode == "RATIO_OF_HEIGHT":
        y = r["y"] + op["offset_y_mm"]
        z = r["z"] + op["offset_z_mm"] + (op["ratio"] or 0) * (ctx["height_mm"] or 0)
    else:                                        # REFERENCE_OFFSET
        y = r["y"] + op["offset_y_mm"]
        z = r["z"] + op["offset_z_mm"]
    return y, z, r["source"], r.get("confidence")


def resolve_profile(db, profile_id):
    """Returns (created, skipped[]). Never writes CONFIRMED - only a person does."""
    ctx = _profile_context(db, profile_id)
    if not ctx: return 0, ["no such profile"]

    refs = _references(db, profile_id)
    ref_refs = {}
    if ctx["reference_profile_id"] and ctx["reference_profile_id"] != profile_id:
        ref_refs = _references(db, ctx["reference_profile_id"])

    ops = db.execute("""
        SELECT op.*, a.id AS acc_id, a.code AS acc_code
        FROM group_accessory_assignment ga
        JOIN accessory a            ON a.id = ga.accessory_id
        JOIN accessory_operation op ON op.accessory_id = a.id
        WHERE ga.profile_group_id = ? AND ga.is_enabled = 1
        ORDER BY a.id, op.seq""", (ctx["group_id"],)).fetchall()
    cols = [d[0] for d in db.execute("""
        SELECT op.*, a.id AS acc_id, a.code AS acc_code
        FROM group_accessory_assignment ga
        JOIN accessory a            ON a.id = ga.accessory_id
        JOIN accessory_operation op ON op.accessory_id = a.id
        LIMIT 0""").description]

    created, skipped = 0, []
    for row in ops:
        op = dict(zip(cols, row))
        by, bz, src, conf = _base_position(op, refs, ctx)
        if by is None:
            skipped.append("%s op%d: %s" % (op["acc_code"], op["seq"], src))
            continue

        cy, cz = _corrections(db, ctx, op["acc_id"], op["id"])
        y, z = by + cy, bz + cz

        dev = None
        code = op["reference_code"]
        if code and code in refs and code in ref_refs:
            dy = refs[code]["y"] - ref_refs[code]["y"]
            dz = refs[code]["z"] - ref_refs[code]["z"]
            dev = round((dy*dy + dz*dz) ** 0.5, 3)

        status = "PROPOSED" if src == "AI_PROPOSED" else "AUTO"

        db.execute("""
            INSERT INTO resolved_placement
              (profile_id, operation_id, y_mm, z_mm, base_y_mm, base_z_mm,
               corr_y_mm, corr_z_mm, x_strategy, x_base_mm, x_pitch_mm, x_count,
               status, confidence, deviation_mm, updated_at)
            VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)
            ON CONFLICT(profile_id, operation_id) DO UPDATE SET
               y_mm=excluded.y_mm, z_mm=excluded.z_mm,
               base_y_mm=excluded.base_y_mm, base_z_mm=excluded.base_z_mm,
               corr_y_mm=excluded.corr_y_mm, corr_z_mm=excluded.corr_z_mm,
               deviation_mm=excluded.deviation_mm,
               updated_at=excluded.updated_at
            WHERE resolved_placement.status NOT IN ('CONFIRMED','REJECTED')""",
            (profile_id, op["id"], round(y,3), round(z,3), round(by,3), round(bz,3),
             round(cy,3), round(cz,3), op["x_strategy"], op["x_base_mm"],
             op["x_pitch_mm"], op["x_count"], status, conf, dev, _utcnow()))
        created += 1
    db.commit()
    return created, skipped

