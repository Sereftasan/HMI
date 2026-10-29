using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;

namespace Kaban.UI.Dashboard
{
    /// <summary>
    /// THE MACHINE'S LOG DATABASE, WRITTEN BY THE HMI (29 October: "create the same logger tool
    /// in our application so we won't need the windows service anymore"). The tables are the
    /// Industry 4 service's, made the way it made them:
    ///
    ///     main_log    (id, state_code, state_date)            one row a record, its date UTC as the PLC sent it
    ///     log_details (id, main_log_id, log_description, deger, str_deger)   its values, one row each
    ///
    /// The machine's codes are its state map's (0 - 5 its states, the rest its events and
    /// alarms). The HMI's own log goes into the same tables under codes of its own, 1000 +
    /// its kind (29 October: "we can consider HMI logs as the log in the industry 4 apart from
    /// the machine state code, just like the event codes inside it") - the dashboard, its
    /// tables and the reports leave them out (<see cref="NotHmi"/>); the All logs lists read them.
    /// </summary>
    public static class LogDatabase
    {
        /// <summary>The HMI's codes: <see cref="HmiFirst"/> + the kind of the entry (AuditKind: 0 other ... 6 a device written).</summary>
        public const int HmiFirst = 1000, HmiLast = 1099;

        public static int HmiCode(AuditKind kind) { return HmiFirst + (int)kind; }

        public static bool IsHmi(int code) { return code >= HmiFirst && code <= HmiLast; }

        /// <summary>The SQL condition leaving the HMI's rows out (" AND state_code NOT BETWEEN 1000 AND 1099").</summary>
        public static string NotHmi(string column = "state_code")
        {
            return " AND " + column + " NOT BETWEEN " + HmiFirst.ToString(CultureInfo.InvariantCulture) + " AND " +
                   HmiLast.ToString(CultureInfo.InvariantCulture);
        }

        public const string DateFormat = "yyyy-MM-dd HH:mm:ss";

        /// <summary>A date as the service wrote it ("2026-10-29 08:15:02").</summary>
        public static string ToDb(DateTime t) { return t.ToString(DateFormat, CultureInfo.InvariantCulture); }

        /// <summary>One value of a record.</summary>
        public sealed class Detail
        {
            public string Description = "";
            public double? Value;
            public string Text;
            public Detail() { }
            public Detail(string description, double? value, string text) { Description = description ?? ""; Value = value; Text = text; }
        }

        /// <summary>
        /// True when the HMI may make the file where it is set: a full path (a test's
        /// "C:/Industry4/..." on Linux is not one - nothing is made in the working folder).
        /// </summary>
        public static bool CanMake(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            try { return Path.IsPathRooted(path) && !string.IsNullOrEmpty(Path.GetFileName(path)); }
            catch { return false; }
        }

        /// <summary>
        /// The database opened for writing - made, with its folder and tables, when it is
        /// not there yet. Throws when it cannot be.
        /// </summary>
        public static DbConnection OpenForWriting(string path)
        {
            if (!File.Exists(path))
            {
                if (!CanMake(path)) throw new FileNotFoundException("the database is not found", path ?? "");
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            }
            var c = new NativeSqliteConnection { ConnectionString = "Data Source=" + path };
            c.Open();
            try { EnsureTables(c); }
            catch { c.Dispose(); throw; }
            return c;
        }

        /// <summary>The service's tables (and its later str_deger column) - as it made them.</summary>
        public static void EnsureTables(DbConnection c)
        {
            Exec(c, "CREATE TABLE IF NOT EXISTS \"main_log\" ( \"id\" INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE , \"state_code\" INTEGER, \"state_date\" DATETIME )");
            Exec(c, "CREATE TABLE IF NOT EXISTS \"log_details\" ( \"id\" INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE , \"main_log_id\" INTEGER, \"log_description\" TEXT, \"deger\" REAL, \"str_deger\" TEXT )");
            bool hasText = false;
            using (var cmd = Command(c, "PRAGMA table_info(log_details)"))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    if (string.Equals(Convert.ToString(r.GetValue(1), CultureInfo.InvariantCulture), "str_deger", StringComparison.OrdinalIgnoreCase))
                        hasText = true;
            if (!hasText) Exec(c, "ALTER TABLE log_details ADD str_deger TEXT");
            foreach (var sql in new[]
            {
                "CREATE INDEX IF NOT EXISTS idx_main_log_date ON main_log(state_date)",
                "CREATE INDEX IF NOT EXISTS idx_main_log_code_date ON main_log(state_code, state_date)",
                "CREATE INDEX IF NOT EXISTS idx_log_details_main_id ON log_details(main_log_id)"
            })
                try { Exec(c, sql); } catch { }
        }

        /// <summary>A record and its values; its main_log id.</summary>
        public static long Insert(DbConnection c, int code, string date, IEnumerable<Detail> details)
        {
            Exec(c, "INSERT INTO main_log (state_code, state_date) VALUES (@p0, @p1)", code, date);
            long id;
            using (var cmd = Command(c, "SELECT last_insert_rowid()"))
                id = Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (details != null)
                foreach (var d in details)
                    Exec(c, "INSERT INTO log_details (main_log_id, log_description, deger, str_deger) VALUES (@p0, @p1, @p2, @p3)",
                         id, d.Description ?? "", d.Value.HasValue ? (object)d.Value.Value : null, d.Text);
            return id;
        }

        public static void Exec(DbConnection c, string sql, params object[] args)
        {
            using (var cmd = Command(c, sql, args)) cmd.ExecuteNonQuery();
        }

        public static DbCommand Command(DbConnection c, string sql, params object[] args)
        {
            var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            for (int i = 0; i < args.Length; i++)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = "@p" + i;
                p.Value = args[i] ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
            return cmd;
        }
    }
}
