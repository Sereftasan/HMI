# Kaban database — one database, two halves

| File | What it is |
|---|---|
| `schema_v2.sql` | **Profile & accessory library.** Machine-independent. |
| `seed_data.sql` | Real vendor structure; **example coordinates** (read §Safety). |
| `schema_machines.sql` | **Machine layer.** Categories, series, capabilities, parameters, tools, feeds. |
| `resolver_v2.py` | Reference resolver for accessory placement. |
| `test_resolver_v2.py` | 26 checks — placement, corrections, safety. |
| `test_machines.py` | 12 checks — capability wiring, new-machine extensibility, boundary. |

Run: `python3 test_resolver_v2.py && python3 test_machines.py`

## Why the split

The library says where a hinge sits on a Reynaers frame — true whoever is
cutting it. The machine layer says what *this* machine does about it.

The boundary is **tested, not just documented**: `test_machines.py` walks every
library table and fails if a column name matches `feed`, `spindle`, `rpm`,
`tool` or `plunge`. An earlier draft seeded `milling_class` into the library and
the load failed with *"no such table"* — the boundary working as intended.

## Adding a machine series: data, not a migration

You build nine PVC categories plus aluminium. A table per category makes every
new machine a schema migration — and a migration means a release on every panel
PC already in the field.

Instead a series declares **capabilities**, and a capability owns **parameter
definitions** that are rows. The test adds a KT4000 corner-cleaning centre:

```
INSERT machine_series      KT4000
INSERT series_capability   KT4000 does CORNER_CLEAN, MULLION_CLEAN
```

Two inserts. **No table was added** (39 before, 39 after) and the machine
immediately has a complete 8-field parameter screen — knife depth, feed, groove
depth, bead width, face flags — with units, ranges and defaults, because
`CORNER_CLEAN` already declared them.

Each HMI shows only its own duties: FA2010 gets 14 parameters, SB3012 gets 5,
H500 gets 8. A cutting machine never carries milling fields.

### The honest trade-off

`parameter_value` is a key/value table, and key/value loses column typing and is
clumsy to query in bulk. That is accepted **here and nowhere else**, because
these values are read a screenful at a time for one profile on one machine —
never aggregated over thousands of rows. The profile library, which *is* queried
in bulk and whose shape is stable and shared, stays strongly typed. Key/value
everywhere would be far worse than either.

When something is a **sequence or a shape** rather than a value, it gets a real
table — `corner_clean_program` / `corner_clean_step` is the worked example.

## SQLite now, PostgreSQL later — and the bit that matters

**Phase 1 (now): SQLite, one file per machine.** No server, no DBA, copies with
the machine, and EF Core makes the provider a one-line change. For a handful of
machines this is genuinely sufficient — a server would be infrastructure you
have to keep alive for no benefit yet.

**Phase 2: PostgreSQL for the shared library**, once several machines in one
factory must see the same profiles.

**The constraint I would not design around:** a machining centre must keep
running when the network is down. So even in Phase 2, each machine keeps a local
SQLite cache of the library and syncs when it can — PostgreSQL becomes the
master copy, never a runtime dependency of the cutting head. Designing the
machine to stop because a database server is unreachable would be a worse
failure than any it currently has.

That is why the schema is provider-portable from day one: no `AUTOINCREMENT`, no
`datetime('now')`, no triggers, no views, no `CHECK` constraints EF cannot
round-trip. See the note at the top of `schema_v2.sql`.

## Safety — please read before using a coordinate

Two kinds of data are seeded, and they are not equally trustworthy.

**Structure** — vendor names, series, variants, depths — comes from the
manufacturers' public pages (Reynaers MasterLine 8 variants; fixed frame 77 mm
Functional, 87 mm Renaissance/Deco; casement 87 mm). A wrong one is a naming
annoyance.

**Every coordinate is my own plausible example.** None comes from a fabrication
manual. I deliberately did not invent authoritative-looking machining figures: a
fabricated hinge coordinate that *looks* researched is worse than an obviously
missing one, because someone may confirm it without checking and it gets cut
into a customer's profile.

So every seeded profile has `dxf_status = 'NONE'` and no measured datums — which
means nothing resolves to a cuttable placement at all. Real numbers come from
the vendor's fabrication manual plus a trial cut, entered by someone who can
check them.

