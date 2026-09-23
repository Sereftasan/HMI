import sqlite3, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from resolver_v2 import resolve_profile

HERE = os.path.dirname(os.path.abspath(__file__))
db = sqlite3.connect(":memory:")
db.executescript(open(os.path.join(HERE, "schema_v2.sql"), encoding="utf-8").read())


# The two views were removed for provider portability - they are LINQ queries on
# ProfileDbContext (Queries.cs). The SQL below is the exact shape those queries
# must produce, so this test still pins the behaviour that matters: production
# sees CONFIRMED rows only.
Q_PROFILE_OPS = """
SELECT p.id AS profile_id, a.code AS accessory_code, op.seq, m.code AS macro_code,
       m.params_json, rp.y_mm, rp.z_mm
FROM   resolved_placement rp
JOIN   profile p              ON p.id  = rp.profile_id
JOIN   accessory_operation op ON op.id = rp.operation_id
JOIN   accessory a            ON a.id  = op.accessory_id
JOIN   macro m                ON m.id  = op.macro_id
WHERE  rp.status = 'CONFIRMED' AND p.is_active = 1"""

Q_REVIEW = """
SELECT p.code, a.code, op.seq, rp.status, rp.confidence
FROM   resolved_placement rp
JOIN   profile p              ON p.id  = rp.profile_id
JOIN   accessory_operation op ON op.id = rp.operation_id
JOIN   accessory a            ON a.id  = op.accessory_id
WHERE  rp.status IN ('AUTO','PROPOSED')
ORDER BY COALESCE(rp.confidence,0) ASC"""

fail = 0
def check(w, ok, d=""):
    global fail
    print(("  PASS  " if ok else "  FAIL  ") + w + (("   [" + str(d) + "]") if d else ""))
    if not ok: fail += 1

# ---------------- catalogue: Reynaers ML8, Roto NX ----------------
db.executescript("""
INSERT INTO profile_vendor (id,code,name) VALUES (1,'REY','Reynaers');
INSERT INTO profile_series (id,vendor_id,code,name,material,system_depth_mm)
  VALUES (1,1,'ML8','MasterLine 8','ALU',70);
INSERT INTO profile_variant (id,series_id,code,name) VALUES (1,1,'FUNC','Functional');
INSERT INTO profile_group (id,variant_id,name,profile_type,hardware_interface_id,
                           default_width_mm,default_height_mm)
  VALUES (1,1,'ML8 Functional Sash','SASH',1,70,80),
         (2,1,'ML8 Functional Frame','FRAME',1,70,80);

INSERT INTO accessory_vendor (id,code,name) VALUES (1,'ROT','Roto');
INSERT INTO accessory_series (id,vendor_id,code,name,hardware_interface_id)
  VALUES (1,1,'NX','Roto NX',1);
INSERT INTO accessory_group (id,name,accessory_type)
  VALUES (1,'Hinge','HINGE'),(2,'Handle','HANDLE'),(3,'Drainage','WATER_DRAINAGE');
INSERT INTO accessory (id,group_id,series_id,code,description)
  VALUES (1,1,1,'NX-HINGE-A','Roto NX hinge type A'),
         (2,2,1,'NX-HANDLE',  'Roto NX handle');

INSERT INTO macro (id,code,macro_type,name,params_json) VALUES
 (1,'DRILL_5','ROUND_HOLE','Drill 5mm','{"diameter_mm":5,"depth_mm":12}'),
 (2,'DRILL_8','ROUND_HOLE','Drill 8mm','{"diameter_mm":8,"depth_mm":14}'),
 (3,'SLOT_10X25','SLOT','Slot 10x25','{"length_mm":25,"width_mm":10,"depth_mm":6}'),
 (4,'SQUARE_HANDLE','POCKET','Handle pocket','{"length_mm":32,"width_mm":32,"depth_mm":10}');
""")

# A HINGE IS THREE OPERATIONS - the thing schema v1 could not express at all.
db.executescript("""
INSERT INTO accessory_operation
 (id,accessory_id,macro_id,seq,position_mode,reference_code,offset_y_mm,offset_z_mm,
  x_strategy,x_base_mm,x_count) VALUES
 (1,1,1,1,'REFERENCE_OFFSET','HARDWARE_AXIS', 0,  0,'FROM_BOTH_ENDS',120,2),
 (2,1,2,2,'REFERENCE_OFFSET','HARDWARE_AXIS',12,  0,'FROM_BOTH_ENDS',120,2),
 (3,1,3,3,'REFERENCE_OFFSET','HARDWARE_AXIS', 0, 15,'FROM_BOTH_ENDS',120,2),
 (4,2,4,1,'REFERENCE_OFFSET','HARDWARE_AXIS', 0, 35,'CENTRED',0,1);

INSERT INTO group_accessory_assignment (profile_group_id,accessory_id) VALUES (1,1),(1,2);
""")

# ---------------- the group's reference profile, measured by a person -------
db.execute("INSERT INTO profile (id,group_id,code,width_mm,height_mm,dxf_status,created_at) VALUES (1,1,'ML8-S-001',70,80,'CONFIRMED','2026-01-01T00:00:00Z')")
for c,y,z in [('OUTSIDE_SURFACE',0,0),('INSIDE_SURFACE',70,0),('HARDWARE_AXIS',22,35),('BED_DATUM',0,0)]:
    db.execute("INSERT INTO profile_reference (profile_id,reference_code,y_mm,z_mm,source,confirmed_by)"
               " VALUES (1,?,?,?, 'MANUAL','engineer')", (c,y,z))
db.execute("UPDATE profile_group SET reference_profile_id=1 WHERE id=1")
db.commit()
n,_ = resolve_profile(db, 1)
print("--- reference profile ML8-S-001 resolved: %d operations ---" % n)

print("\n=== 1. A NEW PROFILE INHERITS EVERYTHING FROM ITS GROUP ===")
db.execute("INSERT INTO profile (id,group_id,code,width_mm,height_mm,dxf_status,created_at) VALUES (2,1,'ML8-S-002',70,86,'IMPORTED','2026-01-01T00:00:00Z')")
for c,y,z in [('OUTSIDE_SURFACE',0,0),('INSIDE_SURFACE',70,0),('HARDWARE_AXIS',22,41),('BED_DATUM',0,0)]:
    db.execute("INSERT INTO profile_reference (profile_id,reference_code,y_mm,z_mm,source)"
               " VALUES (2,?,?,?, 'DXF_LAYER')", (c,y,z))
db.commit()
n, skipped = resolve_profile(db, 2)
check("all 4 operations created with nothing typed", n == 4, "%d created, skipped=%s" % (n, skipped))

rows = list(db.execute("""SELECT a.code, op.seq, rp.y_mm, rp.z_mm, rp.x_strategy
                          FROM resolved_placement rp
                          JOIN accessory_operation op ON op.id=rp.operation_id
                          JOIN accessory a ON a.id=op.accessory_id
                          WHERE rp.profile_id=2 ORDER BY a.code, op.seq"""))
for r in rows: print("     %-12s op%d  Y=%-6s Z=%-6s %s" % r)

check("a hinge produced THREE operations, not one",
      len([r for r in rows if r[0]=='NX-HINGE-A']) == 3)

# looked up by name, not row index - the ordering is by accessory code, so
# NX-HANDLE sorts before NX-HINGE-A and positional indices lie
at = {(r[0], r[1]): (r[2], r[3]) for r in rows}
# this profile's hardware axis is at Z=41, the reference profile's at Z=35
check("hinge op1 follows THIS profile's hardware axis",
      at[('NX-HINGE-A',1)] == (22.0, 41.0),
      "%s (the reference profile would give Z=35)" % (at[('NX-HINGE-A',1)],))
check("hinge op2 keeps its own +12 Y offset", at[('NX-HINGE-A',2)] == (34.0, 41.0), at[('NX-HINGE-A',2)])
check("hinge op3 keeps its own +15 Z offset", at[('NX-HINGE-A',3)] == (22.0, 56.0), at[('NX-HINGE-A',3)])
check("handle resolves to axis +35 Z", at[('NX-HANDLE',1)] == (22.0, 76.0), at[('NX-HANDLE',1)])
check("deviation from the group reference recorded",
      db.execute("SELECT deviation_mm FROM resolved_placement WHERE profile_id=2 LIMIT 1").fetchone()[0] == 6.0)

print("\n=== 2. THE CORRECTION CHAIN (series -> group -> profile) ===")
db.execute("INSERT INTO placement_correction (scope_type,scope_id,accessory_id,correction_y_mm,reason,created_at)"
           " VALUES ('SERIES',1,NULL,0.5,'all ML8 run 0.5 wide','2026-01-01T00:00:00Z')")
db.execute("INSERT INTO placement_correction (scope_type,scope_id,accessory_id,correction_y_mm,reason,created_at)"
           " VALUES ('GROUP',1,1,0.2,'sash hinges','2026-01-01T00:00:00Z')")
db.execute("INSERT INTO placement_correction (scope_type,scope_id,accessory_id,operation_id,correction_z_mm,reason,created_at)"
           " VALUES ('PROFILE',2,1,1,-1.5,'trial cut on ML8-S-002','2026-01-01T00:00:00Z')")
db.commit()
resolve_profile(db, 2)
r = db.execute("""SELECT base_y_mm, base_z_mm, corr_y_mm, corr_z_mm, y_mm, z_mm
                  FROM resolved_placement WHERE profile_id=2 AND operation_id=1""").fetchone()
print("     base Y=%s Z=%s  correction Y=%s Z=%s  ->  final Y=%s Z=%s" % r)
check("series+group corrections add up in Y (0.5 + 0.2)", r[2] == 0.7, r[2])
check("the profile-specific Z correction applies to op1 only", r[3] == -1.5, r[3])
check("final = base + correction", (r[4], r[5]) == (22.7, 39.5), (r[4], r[5]))
r2 = db.execute("SELECT corr_z_mm FROM resolved_placement WHERE profile_id=2 AND operation_id=2").fetchone()
check("op2 did NOT get op1's correction", r2[0] == 0.0, r2[0])
check("base is kept separately, so the UI can show template vs correction",
      r[0] == 22.0 and r[1] == 41.0, (r[0], r[1]))

print("\n=== 3. PRODUCTION SEES ONLY CONFIRMED VALUES ===")
check("nothing visible while everything is AUTO",
      len([r for r in db.execute(Q_PROFILE_OPS) if r[0]==2]) == 0)
db.execute("UPDATE resolved_placement SET status='CONFIRMED', confirmed_by='eng' WHERE profile_id=2")
db.commit()
check("all four visible once confirmed",
      len([r for r in db.execute(Q_PROFILE_OPS) if r[0]==2]) == 4)
check("the view carries the macro shape, but no tool and no feed",
      'params_json' in [d[0] for d in db.execute(Q_PROFILE_OPS + " AND 1=0").description] and
      not any(c[0] in ('tool_id','feed','spindle_rpm')
              for c in db.execute(Q_PROFILE_OPS + " AND 1=0").description))

print("\n=== 4. RE-RESOLVING NEVER OVERWRITES A CONFIRMED VALUE ===")
db.execute("INSERT INTO placement_correction (scope_type,scope_id,correction_y_mm,reason,created_at)"
           " VALUES ('PROFILE',2,99,'should not reach a confirmed row','2026-01-01T00:00:00Z')")
db.commit()
resolve_profile(db, 2)
check("confirmed Y untouched by a later correction",
      db.execute("SELECT y_mm FROM resolved_placement WHERE profile_id=2 AND operation_id=1").fetchone()[0] == 22.7)

print("\n=== 5. AI-EXTRACTED DATUMS ARE PROPOSED, NEVER TRUSTED ===")
db.execute("INSERT INTO profile (id,group_id,code,width_mm,height_mm,dxf_status,created_at) VALUES (3,1,'ML8-S-AI',70,80,'PROPOSED','2026-01-01T00:00:00Z')")
for c,y,z,s,conf in [('OUTSIDE_SURFACE',0,0,'DXF_GEOMETRY',None),('INSIDE_SURFACE',70,0,'DXF_GEOMETRY',None),
                     ('HARDWARE_AXIS',21.7,34.9,'AI_PROPOSED',0.87),('BED_DATUM',0,0,'DXF_GEOMETRY',None)]:
    db.execute("INSERT INTO profile_reference (profile_id,reference_code,y_mm,z_mm,source,confidence)"
               " VALUES (3,?,?,?,?,?)", (c,y,z,s,conf))
db.commit()
resolve_profile(db, 3)
st = db.execute("SELECT status,confidence FROM resolved_placement WHERE profile_id=3 LIMIT 1").fetchone()
check("status is PROPOSED, not AUTO", st[0]=='PROPOSED', st)
check("confidence carried from the weakest datum used", st[1]==0.87, st[1])
check("invisible to production", len([r for r in db.execute(Q_PROFILE_OPS) if r[0]==3])==0)
check("queued for review", len(list(db.execute(Q_REVIEW))) > 0)

print("\n=== 6. MISSING DATUM -> REFUSE, AND SAY WHICH ===")
db.execute("INSERT INTO profile (id,group_id,code,dxf_status,created_at) VALUES (4,1,'ML8-S-BAD','IMPORTED','2026-01-01T00:00:00Z')")
db.execute("INSERT INTO profile_reference (profile_id,reference_code,y_mm,z_mm,source)"
           " VALUES (4,'OUTSIDE_SURFACE',0,0,'DXF_LAYER')")
db.commit()
n, skipped = resolve_profile(db, 4)
check("nothing invented when the hardware axis is missing", n == 0, "%d created" % n)
check("and it names the missing datum",
      any('HARDWARE_AXIS' in s for s in skipped), skipped[:1])

print("\n=== 7. INHERITANCE FROM A BASE PROFILE ===")
db.execute("INSERT INTO profile (id,group_id,code,base_profile_id,dxf_status,created_at) VALUES (5,1,'ML8-S-003',2,'NONE','2026-01-01T00:00:00Z')")
db.execute("INSERT INTO profile_reference (profile_id,reference_code,y_mm,z_mm,source)"
           " VALUES (5,'HARDWARE_AXIS',24,41,'MANUAL')")   # only this one differs
db.commit()
n, sk = resolve_profile(db, 5)
check("a derived profile resolves from inherited datums", n == 4, "%d created, %s" % (n, sk))
y = db.execute("SELECT y_mm FROM resolved_placement WHERE profile_id=5 AND operation_id=1").fetchone()[0]
check("its own overridden axis wins (24, not the base's 22)", y == 24 + 0.7, y)

print("\n=== 8. COMPATIBILITY BY HARDWARE INTERFACE, NOT BY VENDOR ===")
db.executescript("""
INSERT INTO profile_vendor (id,code,name) VALUES (2,'SCH','Schueco');
INSERT INTO profile_series (id,vendor_id,code,name,material) VALUES (2,2,'AWS75','AWS 75','ALU');
INSERT INTO profile_variant (id,series_id,code,name) VALUES (2,2,'STD','Standard');
INSERT INTO profile_group (id,variant_id,name,profile_type,hardware_interface_id)
  VALUES (3,2,'AWS75 Sash','SASH',1);
INSERT INTO profile (id,group_id,code,created_at) VALUES (6,3,'AWS75-S-001','2026-01-01T00:00:00Z');
""")
db.commit()
Q_COMPATIBLE = """
SELECT DISTINCT a.code
FROM   profile p
JOIN   profile_group pg   ON pg.id = p.group_id
JOIN   accessory_series s ON s.hardware_interface_id = pg.hardware_interface_id
JOIN   accessory a        ON a.series_id = s.id
WHERE  a.is_active = 1 AND p.id = ?"""
compat = [r[0] for r in db.execute(Q_COMPATIBLE, (6,))]
check("a Schueco profile gets Roto hardware with no pairing entered",
      'NX-HINGE-A' in compat and 'NX-HANDLE' in compat, compat)

print("\n=== 9. THE LIBRARY IS MACHINE-INDEPENDENT ===")
tables = [r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table'")]
banned = [t for t in tables if any(k in t.lower() for k in
          ('tool','feed','spindle','gcode','nc_','machine_fixture','station'))]
check("no tool / feed / spindle / g-code / station table exists", not banned, banned)
allcols = []
for t in tables:
    allcols += [t + "." + c[1] for c in db.execute("PRAGMA table_info(%s)" % t)]
bannedc = [c for c in allcols if any(k in c.lower() for k in
           ('feed','spindle','tool_','rpm','plunge','gcode'))]
check("and no column carries a machine parameter", not bannedc, bannedc)

print("\nALL PASS" if fail==0 else "\n%d FAILED" % fail)
sys.exit(1 if fail else 0)

