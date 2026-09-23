import sqlite3, sys, os
HERE = os.path.dirname(os.path.abspath(__file__))
db = sqlite3.connect(":memory:")
for f in ("schema_v2.sql", "seed_data.sql", "schema_machines.sql"):
    db.executescript(open(os.path.join(HERE, f), encoding="utf-8").read())
db.commit()

fail = 0
def check(w, ok, d=""):
    global fail
    print(("  PASS  " if ok else "  FAIL  ") + w + (("   [" + str(d) + "]") if d else ""))
    if not ok: fail += 1

print("=== ONE DATABASE, TWO HALVES ===")
tabs = {r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
check("library and machine tables coexist",
      {'profile','accessory','macro'} <= tabs and {'machine_series','capability','parameter_definition'} <= tabs,
      "%d tables total" % len(tabs))

print("\n=== WHAT EACH MACHINE SEES - the join that drives its screens ===")
for series in ('FA2010','SB3012','H500'):
    caps = [r[0] for r in db.execute("""SELECT sc.capability_code FROM machine_series ms
        JOIN series_capability sc ON sc.series_id=ms.id WHERE ms.code=? ORDER BY 1""",(series,))]
    n = db.execute("""SELECT COUNT(*) FROM parameter_definition pd
        JOIN series_capability sc ON sc.capability_code=pd.capability_code
        JOIN machine_series ms ON ms.id=sc.series_id WHERE ms.code=?""",(series,)).fetchone()[0]
    print("  %-8s duties=%-45s %2d parameters" % (series, ",".join(caps), n))

check("a cutting machine does NOT carry milling parameters",
      db.execute("""SELECT COUNT(*) FROM parameter_definition pd
        JOIN series_capability sc ON sc.capability_code=pd.capability_code
        JOIN machine_series ms ON ms.id=sc.series_id
        WHERE ms.code='SB3012' AND pd.capability_code LIKE 'MILL%'""").fetchone()[0] == 0)
check("the machining centre does",
      db.execute("""SELECT COUNT(*) FROM parameter_definition pd
        JOIN series_capability sc ON sc.capability_code=pd.capability_code
        JOIN machine_series ms ON ms.id=sc.series_id
        WHERE ms.code='FA2010' AND pd.capability_code LIKE 'MILL%'""").fetchone()[0] > 0)

print("\n=== THE CLAIM: A NEW MACHINE SERIES IS DATA, NOT A MIGRATION ===")
before_tables = {r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
# A corner cleaning machine - a category nothing in this database has yet.
db.executescript("""
INSERT INTO machine_series (id,category_id,code,name,branch,axis_count)
  VALUES (6,4,'KT4000','KT4000 Corner Cleaning Centre','PVC',3);
INSERT INTO series_capability (series_id,capability_code) VALUES (6,'CORNER_CLEAN'),(6,'MULLION_CLEAN');
INSERT INTO machine_instance (id,series_id,name,module_id,is_main) VALUES (1,6,'KT4000 #1','KT4000',1);
""")
db.commit()
after_tables = {r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table'")}
check("no table was added", before_tables == after_tables, "%d tables before and after" % len(after_tables))

params = list(db.execute("""SELECT pd.ui_group, pd.code, pd.name, pd.unit, pd.default_value
    FROM parameter_definition pd
    JOIN series_capability sc ON sc.capability_code=pd.capability_code
    JOIN machine_series ms ON ms.id=sc.series_id WHERE ms.code='KT4000'
    ORDER BY pd.ui_group, pd.ui_order"""))
check("the new machine immediately has a full parameter screen", len(params) == 8, "%d fields" % len(params))
for p in params: print("     %-10s %-18s %-22s %-6s default=%s" % p)

print("\n=== IT SHARES THE PROFILE LIBRARY, IT DOES NOT COPY IT ===")
n = db.execute("SELECT COUNT(*) FROM profile").fetchone()[0]
check("all machines see the same %d profiles" % n, n == 20)
# a per-profile corner cleaning value for one profile on this machine
d = db.execute("SELECT id FROM parameter_definition WHERE code='knife_depth_mm'").fetchone()[0]
pid = db.execute("SELECT id FROM profile WHERE code='PVC70-F-01'").fetchone()[0]
db.execute("""INSERT INTO parameter_value (definition_id,series_id,profile_id,value,status,updated_at)
              VALUES (?,?,?,?,'CONFIRMED','2026-01-01T00:00:00Z')""",(d,6,pid,'2.8'))
db.commit()
v = db.execute("""SELECT pv.value FROM parameter_value pv
   JOIN parameter_definition pd ON pd.id=pv.definition_id
   JOIN profile p ON p.id=pv.profile_id
   WHERE pd.code='knife_depth_mm' AND p.code='PVC70-F-01' AND pv.series_id=6""").fetchone()[0]
check("a per-profile machine value stores and reads back", v == '2.8', v)

print("\n=== FALLBACK CHAIN: specific value, else the declared default ===")
def resolve(param, series_code, profile_code):
    row = db.execute("""
      SELECT pv.value FROM parameter_value pv
      JOIN parameter_definition pd ON pd.id=pv.definition_id
      JOIN machine_series ms ON ms.id=pv.series_id
      LEFT JOIN profile p ON p.id=pv.profile_id
      WHERE pd.code=? AND ms.code=? AND (p.code=? OR pv.profile_id IS NULL)
      ORDER BY pv.profile_id IS NULL""", (param, series_code, profile_code)).fetchone()
    if row: return row[0], 'value'
    d = db.execute("SELECT default_value FROM parameter_definition WHERE code=?", (param,)).fetchone()
    return (d[0], 'default') if d else (None, 'missing')

check("tuned profile returns its own value", resolve('knife_depth_mm','KT4000','PVC70-F-01') == ('2.8','value'))
check("an untuned profile falls back to the declared default",
      resolve('knife_depth_mm','KT4000','ML8-F-4501') == ('2.5','default'),
      resolve('knife_depth_mm','KT4000','ML8-F-4501'))

print("\n=== BOUNDARY: feeds are machine-side only ===")
libtabs = {'profile','profile_group','accessory','accessory_operation','macro','profile_reference'}
bad = []
for t in libtabs:
    for c in db.execute("PRAGMA table_info(%s)" % t):
        if any(k in c[1].lower() for k in ('feed','spindle','rpm','tool','plunge')): bad.append(t+"."+c[1])
check("no library table carries a feed, tool or spindle column", not bad, bad)
check("feed_strategy exists on the machine side and is per series+material+tool",
      db.execute("SELECT COUNT(*) FROM feed_strategy WHERE series_id=1").fetchone()[0] == 7)

print("\nALL PASS" if fail==0 else "\n%d FAILED" % fail)
sys.exit(1 if fail else 0)

