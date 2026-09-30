# Kaban.Profiles — the data layer

Plain **ADO.NET**, not Entity Framework.

## Why

EF Core cannot be compiled or run in this build environment, so every line of it
would have shipped **unverified** — into the part of the program that decides
where a cutter goes. This layer is tested today against a real SQLite file:
`DataLayerTests.cs`, 24 checks, all passing.

The cost is hand-written SQL. It is contained on purpose: all of it lives in
`Data/`, none leaks into screens, and it stays inside the portable subset
described at the top of `sql/schema_v2.sql`. Moving to PostgreSQL is a provider
swap plus a review of one folder.

## Structure

| File | Role |
|---|---|
| `Data/Db.cs` | The only file that knows which engine is in use. Connection, parameters, type conversion. |
| `Data/NativeSqlite.cs` | An ADO.NET provider over the operating system's own SQLite (`winsqlite3.dll` on Windows 10 and later, `sqlite3.dll`, `libsqlite3.so.0`): no package to install. |
| `Data/Repositories.cs` | `SchemaInstaller` (and its upgrades, `sql/upgrade_N.sql`), `ProfileRepository`, `AccessoryRepository`, row types. |
| `Data/ProfileResolver.cs` | Placement resolution, corrections, confirm/review. |
| `Data/Library.cs` | What the Profiles page asks of the library: profiles, references, placements, corrections, the machine's tools, faces, feeds and settings. |
| `Geometry/DxfReader.cs`, `Section.cs` | The DXF read into a profile section (turned, mirrored, layers left out), and the RAYS through it: material and air from a face. |
| `Geometry/ReferenceFinder.cs`, `Fingerprint.cs` | Reference points found on a drawing (layers, texts, outline, groove, a similar profile), and how alike two sections are. |
| `Machining/SideProgram.cs` | The side machining program: positions along the bar, the tool chosen, depth passes, feeds per wall. |
| `sql/*.sql` | Schema and seed, embedded at build. |

**Which SQLite.** `Db.SqliteChoice` (`Auto` by default) takes System.Data.SQLite when it
is installed, else the operating system's own library through `NativeSqlite.cs`, else
Mono.Data.Sqlite. The library file is `ProgramData\KabanHMI\Kaban.db`.

**Schema 2** (`sql/upgrade_2.sql`, applied in place to a library made by the first
version): the face an operation is machined from (`entry_side`) and how deep it goes
(`depth_mode`), how a profile's drawing is read (`dxf_rotation`, `dxf_mirror`,
`ignored_layers`), the milling unit of a mounted tool, and three machine tables -
`machine_face`, `end_mill_program`, `machine_setting`.

Switching provider:

```csharp
Db.Engine = Db.Provider.PostgreSql;
Db.ConnectionTarget = "Host=...;Database=kaban;...";
```

Nothing above `Db.cs` names SQLite.

## Two bugs the live test caught

Both were invisible to inspection and would have reached a machine.

**Foreign keys were off.** SQLite disables them per connection unless asked.
Without the `PRAGMA`, every cascade and reference in the schema is decorative —
an orphaned `accessory_operation` would be accepted and then resolve onto
nothing. The test now inserts a profile into a non-existent group and requires
the database to refuse it.

**REAL columns were being read at 32-bit precision.** Mono's SQLite reader hands
back a `System.Single`, and `Convert.ToDouble` on a float widens the *error*
along with the value — `0.87` came back as `0.870000004768372`. That is the path
every **Y and Z coordinate** takes out of the database. 4e-7 mm is far below what
a cutter can do, but a datum that changes merely by being read is what makes two
screens disagree and costs a day to find. `Db.Real()` round-trips through the
float's shortest round-trippable text; all 12 coordinate reads go through it.

Numeric parameters are also bound with an explicit `DbType` on the way in, so
the provider never has to guess.

## The safety rules, restated in code

1. `ProfileResolver` writes `AUTO` or `PROPOSED`. **Only a person writes
   `CONFIRMED`** (`Confirm()`), and the previous value goes to
   `placement_history`.
2. Machines read `CuttablePlacements()` — `CONFIRMED`/`OVERRIDDEN` only.
   AI-derived datums produce `PROPOSED`, which never reaches it.
3. A missing datum is an **error that names the datum**
   (`"NX-CD-STD op1: needs HARDWARE_AXIS"`), never a default or a zero.
4. Re-resolving **never overwrites a confirmed row** — a `WHERE status NOT IN
   ('CONFIRMED','REJECTED')` clause, not a convention.

## Running the tests

The full check of the library - the native provider, the DXF reader, rays, references,
placements, corrections, tool choice and programs - is `tools/core_test/ProfileLibrary.cs`;
the Profiles page on it is `tools/ui_test/ProfilesPage.cs`. The older data-layer test:

```
mcs -target:library -out:Kaban.Profiles.dll -r:System.Data.dll $(find Data -name '*.cs')
mcs -out:T.exe -r:System.Data.dll -r:Kaban.Profiles.dll -r:Mono.Data.Sqlite.dll DataLayerTests.cs
cp sql/*.sql .   # SchemaInstaller falls back to disk when not embedded
mono T.exe
```

