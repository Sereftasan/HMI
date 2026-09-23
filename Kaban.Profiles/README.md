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
| `Data/Repositories.cs` | `SchemaInstaller`, `ProfileRepository`, `AccessoryRepository`, row types. |
| `Data/ProfileResolver.cs` | Placement resolution, corrections, confirm/review. |
| `sql/*.sql` | Schema and seed, embedded at build. |

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

```
mcs -target:library -out:Kaban.Profiles.dll -r:System.Data.dll $(find Data -name '*.cs')
mcs -out:T.exe -r:System.Data.dll -r:Kaban.Profiles.dll -r:Mono.Data.Sqlite.dll DataLayerTests.cs
cp sql/*.sql .   # SchemaInstaller falls back to disk when not embedded
mono T.exe
```

