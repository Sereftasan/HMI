using System;
using System.Data;
using System.IO;
using System.Reflection;

namespace Kaban.Profiles.Data
{
    /// <summary>
    /// The one place that knows WHICH database engine is in use.
    ///
    /// WHY NOT ENTITY FRAMEWORK
    /// ------------------------
    /// EF Core cannot be compiled or run in this project's build environment, so
    /// every line of it would have been shipped unverified - into the part of the
    /// program that decides where a cutter goes. Plain ADO.NET is testable today,
    /// against a real SQLite file, which is worth more than the mapping
    /// convenience EF would have added.
    ///
    /// The cost is hand-written SQL. That cost is contained deliberately: all of
    /// it lives in the repository classes beside this one, none of it leaks into
    /// screens, and the SQL is written to the portable subset described in
    /// schema_v2.sql - so the move to PostgreSQL is a provider swap plus a review
    /// of that one folder, not a rewrite.
    ///
    /// SWITCHING PROVIDER
    /// ------------------
    /// Everything here is IDbConnection / IDbCommand. Nothing above this file
    /// names SQLite. To move to PostgreSQL: reference Npgsql, add a case to
    /// Open(), and change the configured provider - the repositories are
    /// untouched.
    /// </summary>
    public static class Db
    {
        public enum Provider { Sqlite, PostgreSql }

        /// <summary>Which engine to open. Set once at startup.</summary>
        public static Provider Engine = Provider.Sqlite;

        /// <summary>
        /// For SQLite: the file path. For PostgreSQL: the connection string.
        /// Defaults to the shared per-machine location, NOT a machine's output
        /// folder - two machine executables on one panel PC must see one library,
        /// or each gets a private copy of the same catalogue and they drift.
        /// </summary>
        public static string ConnectionTarget = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "KabanHMI", "Kaban.db");

        /// <summary>
        /// The ADO.NET provider type, resolved by NAME rather than referenced.
        ///
        /// Kaban.Profiles must build on a machine that has only one of the two
        /// providers installed, and the production build uses System.Data.SQLite
        /// while the test host has Mono.Data.Sqlite. A hard reference to either
        /// would break the other; late binding costs one reflection call per
        /// connection and keeps both working.
        /// </summary>
        private static Type _connectionType;

        private static Type ResolveConnectionType()
        {
            if (_connectionType != null) return _connectionType;

            // Two shapes are needed. A provider copied next to the exe (the
            // Windows/System.Data.SQLite case) loads by simple name; one that
            // exists only in the GAC (Mono.Data.Sqlite on the test host) will not
            // load without its full strong name.
            string[] candidates = Engine == Provider.PostgreSql
                ? new[] { "Npgsql.NpgsqlConnection, Npgsql" }
                : SqliteChoice == SqliteLibrary.MonoDataSqlite
                ? new[]
                  {
                      "Mono.Data.Sqlite.SqliteConnection, Mono.Data.Sqlite",
                      "Mono.Data.Sqlite.SqliteConnection, Mono.Data.Sqlite, " +
                          "Version=4.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756"
                  }
                : new[]
                  {
                      "System.Data.SQLite.SQLiteConnection, System.Data.SQLite",
                      "Mono.Data.Sqlite.SqliteConnection, Mono.Data.Sqlite",
                      "Mono.Data.Sqlite.SqliteConnection, Mono.Data.Sqlite, " +
                          "Version=4.0.0.0, Culture=neutral, PublicKeyToken=0738eb9f132ed756"
                  };

            foreach (var name in candidates)
            {
                // Type.GetType only finds a type in an assembly already loaded (or
                // fully version-qualified). A provider sitting in the GAC is
                // neither, so the assembly is loaded by simple name first - which
                // is the normal case for both System.Data.SQLite and
                // Mono.Data.Sqlite.
                int comma = name.IndexOf(',');
                var typeName = comma < 0 ? name : name.Substring(0, comma).Trim();
                var asmName = comma < 0 ? null : name.Substring(comma + 1).Trim();

                var t = Type.GetType(name, false);
                if (t == null && asmName != null)
                {
                    try
                    {
                        var asm = Assembly.Load(asmName);
                        if (asm != null) t = asm.GetType(typeName, false);
                    }
                    catch (Exception) { /* not this one - try the next candidate */ }
                }
                // Last resort for a GAC assembly whose exact version is unknown.
                // Obsolete, but the alternative is failing to find a provider that
                // is demonstrably installed.
                if (t == null && asmName != null)
                {
                    try
                    {
#pragma warning disable 618
                        var asm = Assembly.LoadWithPartialName(asmName.Split(',')[0].Trim());
#pragma warning restore 618
                        if (asm != null) t = asm.GetType(typeName, false);
                    }
                    catch (Exception) { }
                }
                if (t != null) { _connectionType = t; return t; }
            }

            throw new InvalidOperationException(
                "No ADO.NET provider found for " + Engine + ". Tried: " + string.Join(" | ", candidates));
        }

        private static string BuildConnectionString()
        {
            if (Engine == Provider.PostgreSql) return ConnectionTarget;

            var dir = Path.GetDirectoryName(ConnectionTarget);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // Foreign keys are OFF by default in SQLite and must be asked for on
            // every connection. Without this the schema's cascades and references
            // are silently decorative - an orphaned accessory_operation would be
            // accepted and would then resolve onto nothing.
            return "Data Source=" + ConnectionTarget + ";Version=3;Foreign Keys=True;";
        }

        /// <summary>Which SQLite library: the first that is there (Auto), or one forced (a test).</summary>
        public enum SqliteLibrary { Auto, SystemDataSqlite, Native, MonoDataSqlite }
        public static SqliteLibrary SqliteChoice = SqliteLibrary.Auto;

        /// <summary>What Open() uses - for the page's status line and the log.</summary>
        public static string ProviderName
        {
            get
            {
                try
                {
                    if (Engine == Provider.PostgreSql) return "PostgreSQL (Npgsql)";
                    if (UseNative()) return "SQLite (" + NativeApi.Default().Name + " " + NativeApi.Default().Version() + ")";
                    return ResolveConnectionType().FullName;
                }
                catch (Exception ex) { return "none (" + ex.Message + ")"; }
            }
        }

        /// <summary>
        /// SQLite through the operating system's own library (NativeSqlite)? In
        /// Auto: when System.Data.SQLite is not installed beside the program - the
        /// panel PC then needs nothing extra. Mono.Data.Sqlite comes last (tests).
        /// </summary>
        private static bool UseNative()
        {
            if (Engine != Provider.Sqlite) return false;
            switch (SqliteChoice)
            {
                case SqliteLibrary.Native: return true;
                case SqliteLibrary.SystemDataSqlite:
                case SqliteLibrary.MonoDataSqlite: return false;
            }
            if (_nativeDecided.HasValue) return _nativeDecided.Value;
            bool sds = false;
            try { sds = TryType("System.Data.SQLite.SQLiteConnection, System.Data.SQLite") != null; } catch { }
            _nativeDecided = !sds && NativeApi.Available;
            return _nativeDecided.Value;
        }
        private static bool? _nativeDecided;

        private static Type TryType(string name)
        {
            int comma = name.IndexOf(',');
            var t = Type.GetType(name, false);
            if (t == null && comma > 0)
            {
                try
                {
                    var asm = Assembly.Load(name.Substring(comma + 1).Trim());
                    if (asm != null) t = asm.GetType(name.Substring(0, comma).Trim(), false);
                }
                catch (Exception) { }
            }
            return t;
        }

        public static IDbConnection Open()
        {
            if (UseNative())
            {
                var native = new NativeSqliteConnection { ConnectionString = BuildConnectionString() };
                native.Open();
                Execute(native, "PRAGMA foreign_keys = ON");
                return native;
            }
            var conn = (IDbConnection)Activator.CreateInstance(ResolveConnectionType());
            conn.ConnectionString = BuildConnectionString();
            conn.Open();

            // Mono's provider ignores "Foreign Keys=True" in the connection string,
            // so it is asserted here as well. Harmless on providers that honoured it.
            if (Engine == Provider.Sqlite) Execute(conn, "PRAGMA foreign_keys = ON");
            return conn;
        }

        // ==================================================================
        // SMALL HELPERS
        // ==================================================================
        // Deliberately minimal - just enough to keep the repositories readable.
        // This is not the start of an ORM; if it grows into one, that is the
        // signal to reconsider EF rather than to keep extending this file.

        public static IDbCommand Command(IDbConnection conn, string sql, params object[] args)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            for (int i = 0; i < args.Length; i++)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = "@p" + i;

                var value = args[i];
                if (value == null) { p.Value = DBNull.Value; }
                else
                {
                    // DbType is set EXPLICITLY for numbers. Left to infer, Mono's
                    // SQLite provider bound a double as a 32-bit float and 0.87 came
                    // back as 0.870000004768372. Harmless for that confidence value,
                    // but the same path carries Y and Z COORDINATES, and a datum
                    // silently losing precision on the way into the database is not
                    // something to leave to provider guesswork.
                    var t = value.GetType();
                    if (t == typeof(double) || t == typeof(float) || t == typeof(decimal))
                    {
                        p.DbType = DbType.Double;
                        p.Value = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                    }
                    else if (t == typeof(int) || t == typeof(long) || t == typeof(short))
                    {
                        p.DbType = DbType.Int64;
                        p.Value = Convert.ToInt64(value);
                    }
                    else if (t == typeof(bool))
                    {
                        p.DbType = DbType.Int64;          // portable: no BOOL on SQLite
                        p.Value = ((bool)value) ? 1L : 0L;
                    }
                    else
                    {
                        p.Value = value;
                    }
                }
                cmd.Parameters.Add(p);
            }
            return cmd;
        }

        public static int Execute(IDbConnection conn, string sql, params object[] args)
        {
            using (var cmd = Command(conn, sql, args)) return cmd.ExecuteNonQuery();
        }

        public static object Scalar(IDbConnection conn, string sql, params object[] args)
        {
            using (var cmd = Command(conn, sql, args))
            {
                var v = cmd.ExecuteScalar();
                return v == DBNull.Value ? null : v;
            }
        }

        public static long Count(IDbConnection conn, string sql, params object[] args)
        {
            var v = Scalar(conn, sql, args);
            return v == null ? 0 : Convert.ToInt64(v);
        }

        /// <summary>
        /// The id the last insert produced. Spelled differently by every engine,
        /// which is exactly the sort of thing that has to be behind this wall.
        /// </summary>
        public static long LastInsertId(IDbConnection conn)
        {
            string sql = Engine == Provider.PostgreSql
                ? "SELECT lastval()"
                : "SELECT last_insert_rowid()";
            return Convert.ToInt64(Scalar(conn, sql));
        }

        /// <summary>UTC timestamp in the one format every provider stores unchanged.</summary>
        public static string Now()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'",
                System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads a REAL column as a full double.
        ///
        /// Mono's SQLite reader hands back a System.Single for a REAL column, and
        /// Convert.ToDouble on a float widens the 32-bit ERROR as well as the
        /// value: 0.87 becomes 0.870000004768372. Round-tripping through the
        /// float's shortest round-trippable text ("R") recovers the number that
        /// was actually stored.
        ///
        /// It matters because this is the path every Y and Z COORDINATE takes on
        /// the way out of the database. 4e-7 mm is far below anything a cutter can
        /// do, but a datum that changes merely by being read is the kind of thing
        /// that makes two screens disagree and costs a day to find.
        /// </summary>
        public static double Real(object v)
        {
            if (v == null || v == DBNull.Value) return 0;
            if (v is float)
                return double.Parse(((float)v).ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                                    System.Globalization.CultureInfo.InvariantCulture);
            return Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        public static double? ToDouble(object v)
        {
            if (v == null || v == DBNull.Value) return null;
            return Real(v);
        }

        public static int? ToInt(object v)
        {
            if (v == null || v == DBNull.Value) return null;
            return Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture);
        }

        public static string ToText(object v)
        {
            return (v == null || v == DBNull.Value) ? null : Convert.ToString(v);
        }
    }
}

