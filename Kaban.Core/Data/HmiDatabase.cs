using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

namespace Kaban.Core.Data
{
    /// <summary>
    /// PostgreSQL access for HMI data (users, profiles, parameter records, ...).
    ///
    /// The Npgsql driver is loaded BY REFLECTION so this solution compiles and runs
    /// without the package; install it per machine project with
    ///     Install-Package Npgsql
    /// (any recent 4.x/8.x works - only NpgsqlConnection is used). When Npgsql or
    /// the server is absent, IsAvailable is false and screens can show "database
    /// not configured" instead of crashing.
    ///
    /// Schema: see Schema.sql next to this file - run it once with psql/pgAdmin.
    /// </summary>
    public class HmiDatabase
    {
        private readonly string _connectionString;
        private Type _connType;

        public HmiDatabase(string host, string database, string user, string password, int port = 5432)
        {
            // Timeout=3: Npgsql's default connect timeout is 15 s, and IsAvailable is
            // asked from the UI thread - with the server switched off, a page that
            // checks the database froze the whole panel for 15 seconds.
            _connectionString =
                $"Host={host};Port={port};Database={database};Username={user};Password={password};Timeout=3";
            _connType = Type.GetType("Npgsql.NpgsqlConnection, Npgsql");
        }

        /// <summary>False when Npgsql is not installed or the server is unreachable.</summary>
        public bool IsAvailable
        {
            get
            {
                if (_connType == null) return false;
                try { using (var c = Open()) return c.State == ConnectionState.Open; }
                catch { return false; }
            }
        }

        private IDbConnection Open()
        {
            var conn = (IDbConnection)Activator.CreateInstance(_connType, _connectionString);
            conn.Open();
            return conn;
        }

        /// <summary>Runs a SELECT and returns rows as dictionaries (column -> value).</summary>
        public List<Dictionary<string, object>> Query(string sql, params (string name, object value)[] args)
        {
            var rows = new List<Dictionary<string, object>>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                foreach (var a in args)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = a.name;
                    p.Value = a.value ?? DBNull.Value;
                    cmd.Parameters.Add(p);
                }
                using (var r = cmd.ExecuteReader())
                    while (r.Read())
                    {
                        var row = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                        for (int i = 0; i < r.FieldCount; i++)
                            row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
                        rows.Add(row);
                    }
            }
            return rows;
        }

        /// <summary>INSERT/UPDATE/DELETE; returns affected row count.</summary>
        public int Execute(string sql, params (string name, object value)[] args)
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                foreach (var a in args)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = a.name;
                    p.Value = a.value ?? DBNull.Value;
                    cmd.Parameters.Add(p);
                }
                return cmd.ExecuteNonQuery();
            }
        }
    }
}

