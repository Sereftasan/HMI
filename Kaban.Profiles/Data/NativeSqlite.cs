using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Kaban.Profiles.Data
{
    /// <summary>
    /// A small ADO.NET provider over the SQLite library the OPERATING SYSTEM
    /// already has - <c>winsqlite3.dll</c> in Windows 10 and 11 (System32), or
    /// <c>sqlite3.dll</c> beside the program, or <c>libsqlite3</c> on Linux.
    ///
    /// WHY: the profile library must work on the panel PC the day the program is
    /// copied there. System.Data.SQLite is a NuGet package with native files per
    /// bitness; forgetting it on one panel would leave the Profiles page empty.
    /// When System.Data.SQLite IS installed, Db uses it instead (see Db).
    ///
    /// Only what the library's repositories use: parameters (@name), non-query,
    /// scalar, a forward-only reader and transactions. Values come back as the
    /// types SQLite stores: long, double, string, byte[] or DBNull.
    /// </summary>
    public sealed class NativeSqliteConnection : DbConnection
    {
        internal IntPtr Handle = IntPtr.Zero;
        internal readonly NativeApi Api;
        private string _connectionString = "";
        private string _file = "";
        private ConnectionState _state = ConnectionState.Closed;

        public NativeSqliteConnection() { Api = NativeApi.Default(); }

        /// <summary>
        /// "Data Source=path" - made when it is missing. "Mode=ReadOnly" (or
        /// "Read Only=True") opens an existing file for reading only - another
        /// program's database, which must not be changed or created by mistake;
        /// "Mode=ReadWrite" opens an existing file without making one. Other keys
        /// are accepted and ignored.
        /// </summary>
        public override string ConnectionString
        {
            get { return _connectionString; }
            set
            {
                _connectionString = value ?? "";
                _file = "";
                _mode = ReadWrite | Create;
                foreach (var part in _connectionString.Split(';'))
                {
                    int eq = part.IndexOf('=');
                    if (eq <= 0) continue;
                    var key = part.Substring(0, eq).Trim();
                    var val = part.Substring(eq + 1).Trim();
                    if (key.Equals("Data Source", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("DataSource", StringComparison.OrdinalIgnoreCase) ||
                        key.Equals("Filename", StringComparison.OrdinalIgnoreCase))
                        _file = val;
                    else if (key.Equals("Mode", StringComparison.OrdinalIgnoreCase))
                    {
                        if (val.Equals("ReadOnly", StringComparison.OrdinalIgnoreCase)) _mode = ReadOnlyFlag;
                        else if (val.Equals("ReadWrite", StringComparison.OrdinalIgnoreCase)) _mode = ReadWrite;
                    }
                    else if (key.Equals("Read Only", StringComparison.OrdinalIgnoreCase) &&
                             val.Equals("True", StringComparison.OrdinalIgnoreCase))
                        _mode = ReadOnlyFlag;
                }
            }
        }

        private const int ReadOnlyFlag = 0x1, ReadWrite = 0x2, Create = 0x4, FullMutex = 0x10000;
        private int _mode = ReadWrite | Create;

        public override string Database { get { return "main"; } }
        public override string DataSource { get { return _file; } }
        public override string ServerVersion { get { return Api.Version(); } }
        public override ConnectionState State { get { return _state; } }
        public override void ChangeDatabase(string databaseName) { throw new NotSupportedException(); }

        public override void Open()
        {
            if (_state == ConnectionState.Open) return;
            if (string.IsNullOrEmpty(_file)) throw new InvalidOperationException("no Data Source");
            IntPtr db;
            int rc = Api.Open(NativeApi.Utf8(_file), out db, _mode | FullMutex, IntPtr.Zero);
            if (rc != 0)
            {
                string msg = db != IntPtr.Zero ? Api.ErrorMessage(db) : "code " + rc;
                if (db != IntPtr.Zero) Api.Close(db);
                throw new NativeSqliteException("could not open " + _file + ": " + msg, rc);
            }
            Handle = db;
            Api.BusyTimeout(db, 5000);        // another program (or page) writing: wait, do not fail
            _state = ConnectionState.Open;
        }

        public override void Close()
        {
            if (Handle != IntPtr.Zero) { Api.Close(Handle); Handle = IntPtr.Zero; }
            _state = ConnectionState.Closed;
        }

        protected override void Dispose(bool disposing) { Close(); base.Dispose(disposing); }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            var t = new NativeSqliteTransaction(this);
            Exec("BEGIN IMMEDIATE");
            return t;
        }

        protected override DbCommand CreateDbCommand() { return new NativeSqliteCommand { Connection = this }; }

        internal void Exec(string sql)
        {
            using (var c = new NativeSqliteCommand { Connection = this, CommandText = sql }) c.ExecuteNonQuery();
        }

        internal void Check(int rc, string what)
        {
            if (rc == 0 || rc == 100 || rc == 101) return;
            throw new NativeSqliteException(what + ": " + Api.ErrorMessage(Handle), rc);
        }
    }

    public sealed class NativeSqliteException : DbException
    {
        public int Code { get; private set; }
        public NativeSqliteException(string message, int code) : base(message) { Code = code; }
    }

    public sealed class NativeSqliteTransaction : DbTransaction
    {
        private NativeSqliteConnection _conn;
        internal NativeSqliteTransaction(NativeSqliteConnection conn) { _conn = conn; }
        protected override DbConnection DbConnection { get { return _conn; } }
        public override IsolationLevel IsolationLevel { get { return IsolationLevel.Serializable; } }
        public override void Commit() { if (_conn != null) { _conn.Exec("COMMIT"); _conn = null; } }
        public override void Rollback() { if (_conn != null) { try { _conn.Exec("ROLLBACK"); } catch { } _conn = null; } }
        protected override void Dispose(bool disposing) { Rollback(); base.Dispose(disposing); }
    }

    public sealed class NativeSqliteParameter : DbParameter
    {
        public override DbType DbType { get; set; } = DbType.String;
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; } = true;
        public override string ParameterName { get; set; } = "";
        public override string SourceColumn { get; set; } = "";
        public override object Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() { DbType = DbType.String; }
    }

    public sealed class NativeSqliteParameterCollection : DbParameterCollection
    {
        private readonly List<NativeSqliteParameter> _list = new List<NativeSqliteParameter>();
        public override int Count { get { return _list.Count; } }
        public override object SyncRoot { get { return _list; } }
        public override int Add(object value) { _list.Add((NativeSqliteParameter)value); return _list.Count - 1; }
        public override void AddRange(Array values) { foreach (var v in values) Add(v); }
        public override void Clear() { _list.Clear(); }
        public override bool Contains(object value) { return _list.Contains((NativeSqliteParameter)value); }
        public override bool Contains(string value) { return IndexOf(value) >= 0; }
        public override void CopyTo(Array array, int index) { ((ICollection)_list).CopyTo(array, index); }
        public override IEnumerator GetEnumerator() { return _list.GetEnumerator(); }
        public override int IndexOf(object value) { return _list.IndexOf((NativeSqliteParameter)value); }
        public override int IndexOf(string parameterName)
        {
            for (int i = 0; i < _list.Count; i++)
                if (string.Equals(_list[i].ParameterName, parameterName, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
        public override void Insert(int index, object value) { _list.Insert(index, (NativeSqliteParameter)value); }
        public override void Remove(object value) { _list.Remove((NativeSqliteParameter)value); }
        public override void RemoveAt(int index) { _list.RemoveAt(index); }
        public override void RemoveAt(string parameterName) { int i = IndexOf(parameterName); if (i >= 0) _list.RemoveAt(i); }
        protected override DbParameter GetParameter(int index) { return _list[index]; }
        protected override DbParameter GetParameter(string parameterName) { return _list[IndexOf(parameterName)]; }
        protected override void SetParameter(int index, DbParameter value) { _list[index] = (NativeSqliteParameter)value; }
        protected override void SetParameter(string parameterName, DbParameter value) { _list[IndexOf(parameterName)] = (NativeSqliteParameter)value; }
        internal IEnumerable<NativeSqliteParameter> Items { get { return _list; } }
    }

    public sealed class NativeSqliteCommand : DbCommand
    {
        private readonly NativeSqliteParameterCollection _params = new NativeSqliteParameterCollection();
        public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; } = 30;
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection { get { return _params; } }
        protected override DbTransaction DbTransaction { get; set; }
        public override void Cancel() { }
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() { return new NativeSqliteParameter(); }

        private NativeSqliteConnection Conn
        {
            get
            {
                var c = DbConnection as NativeSqliteConnection;
                if (c == null || c.Handle == IntPtr.Zero) throw new InvalidOperationException("the connection is not open");
                return c;
            }
        }

        /// <summary>
        /// Each statement of the text in turn: prepared, bound, and handed to
        /// <paramref name="each"/> (which steps it). The LAST statement is kept
        /// for a reader when <paramref name="keepLast"/>.
        /// </summary>
        private IntPtr Run(Action<IntPtr> each, bool keepLast)
        {
            var conn = Conn;
            var api = conn.Api;
            byte[] sql = NativeApi.Utf8(CommandText ?? "");
            var pin = GCHandle.Alloc(sql, GCHandleType.Pinned);
            try
            {
                IntPtr start = pin.AddrOfPinnedObject();
                IntPtr end = start + sql.Length - 1;             // before the terminating 0
                IntPtr cursor = start;
                IntPtr kept = IntPtr.Zero;
                while (cursor.ToInt64() < end.ToInt64())
                {
                    IntPtr stmt, tail;
                    int rc = api.Prepare(conn.Handle, cursor, (int)(end.ToInt64() - cursor.ToInt64()), out stmt, out tail);
                    conn.Check(rc, "SQL error");
                    cursor = tail;
                    if (stmt == IntPtr.Zero) continue;           // only whitespace or a comment
                    Bind(api, stmt);
                    bool last = cursor.ToInt64() >= end.ToInt64() || OnlyBlank(cursor, end);
                    if (keepLast && last) { kept = stmt; break; }
                    try { each(stmt); }
                    finally { api.Finalize(stmt); }
                }
                return kept;
            }
            finally { pin.Free(); }
        }

        private static bool OnlyBlank(IntPtr from, IntPtr end)
        {
            for (IntPtr p = from; p.ToInt64() < end.ToInt64(); p += 1)
            {
                byte b = Marshal.ReadByte(p);
                if (b != (byte)' ' && b != (byte)'\t' && b != (byte)'\r' && b != (byte)'\n' && b != (byte)';') return false;
            }
            return true;
        }

        private void Bind(NativeApi api, IntPtr stmt)
        {
            foreach (var p in _params.Items)
            {
                string name = p.ParameterName ?? "";
                if (!name.StartsWith("@", System.StringComparison.Ordinal) && !name.StartsWith(":", System.StringComparison.Ordinal) && !name.StartsWith("$", System.StringComparison.Ordinal)) name = "@" + name;
                int idx = api.ParameterIndex(stmt, NativeApi.Utf8(name));
                if (idx <= 0) continue;                           // not used by this statement
                object v = p.Value;
                int rc;
                if (v == null || v == DBNull.Value) rc = api.BindNull(stmt, idx);
                else if (v is double || v is float || v is decimal)
                    rc = api.BindDouble(stmt, idx, Convert.ToDouble(v, CultureInfo.InvariantCulture));
                else if (v is long || v is int || v is short || v is byte || v is uint || v is ushort || v is sbyte)
                    rc = api.BindInt64(stmt, idx, Convert.ToInt64(v, CultureInfo.InvariantCulture));
                else if (v is bool) rc = api.BindInt64(stmt, idx, (bool)v ? 1 : 0);
                else if (v is byte[]) rc = api.BindBlob(stmt, idx, (byte[])v);
                else if (v is DateTime)
                    rc = api.BindText(stmt, idx, ((DateTime)v).ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
                else rc = api.BindText(stmt, idx, Convert.ToString(v, CultureInfo.InvariantCulture));
                if (rc != 0) throw new NativeSqliteException("could not bind " + name, rc);
            }
        }

        public override int ExecuteNonQuery()
        {
            var conn = Conn;
            int changes = 0;
            Run(stmt =>
            {
                int rc;
                while ((rc = conn.Api.Step(stmt)) == 100) { }
                conn.Check(rc, "SQL error");
                changes += conn.Api.Changes(conn.Handle);
            }, false);
            return changes;
        }

        public override object ExecuteScalar()
        {
            using (var r = ExecuteDbDataReader(CommandBehavior.Default))
                return r.Read() && r.FieldCount > 0 ? r.GetValue(0) : null;
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            var conn = Conn;
            IntPtr stmt = Run(s =>
            {
                int rc;
                while ((rc = conn.Api.Step(s)) == 100) { }
                conn.Check(rc, "SQL error");
            }, true);
            return new NativeSqliteReader(conn, stmt);
        }
    }

    public sealed class NativeSqliteReader : DbDataReader
    {
        private readonly NativeSqliteConnection _conn;
        private IntPtr _stmt;
        private readonly string[] _names;
        private bool _closed, _done;

        internal NativeSqliteReader(NativeSqliteConnection conn, IntPtr stmt)
        {
            _conn = conn;
            _stmt = stmt;
            int n = stmt == IntPtr.Zero ? 0 : conn.Api.ColumnCount(stmt);
            _names = new string[n];
            for (int i = 0; i < n; i++) _names[i] = conn.Api.ColumnName(stmt, i);
            if (stmt == IntPtr.Zero) _done = true;
        }

        public override bool Read()
        {
            if (_done || _closed) return false;
            int rc = _conn.Api.Step(_stmt);
            if (rc == 100) return true;
            _done = true;
            _conn.Check(rc, "SQL error");
            return false;
        }

        public override object GetValue(int ordinal)
        {
            switch (_conn.Api.ColumnType(_stmt, ordinal))
            {
                case 1: return _conn.Api.ColumnInt64(_stmt, ordinal);
                case 2: return _conn.Api.ColumnDouble(_stmt, ordinal);
                case 3: return _conn.Api.ColumnText(_stmt, ordinal);
                case 4: return _conn.Api.ColumnBlob(_stmt, ordinal);
                default: return DBNull.Value;
            }
        }

        public override object this[int ordinal] { get { return GetValue(ordinal); } }
        public override object this[string name] { get { return GetValue(GetOrdinal(name)); } }
        public override int FieldCount { get { return _names.Length; } }
        public override string GetName(int ordinal) { return _names[ordinal]; }
        public override int GetOrdinal(string name)
        {
            for (int i = 0; i < _names.Length; i++)
                if (string.Equals(_names[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            throw new IndexOutOfRangeException(name);
        }
        public override bool IsDBNull(int ordinal) { return _conn.Api.ColumnType(_stmt, ordinal) == 5; }
        public override bool HasRows { get { return !_done; } }
        public override bool IsClosed { get { return _closed; } }
        public override int RecordsAffected { get { return -1; } }
        public override int Depth { get { return 0; } }
        public override bool NextResult() { return false; }
        public override void Close()
        {
            if (_closed) return;
            _closed = true;
            if (_stmt != IntPtr.Zero) { _conn.Api.Finalize(_stmt); _stmt = IntPtr.Zero; }
        }
        protected override void Dispose(bool disposing) { Close(); base.Dispose(disposing); }

        public override bool GetBoolean(int i) { return Convert.ToInt64(GetValue(i)) != 0; }
        public override byte GetByte(int i) { return Convert.ToByte(GetValue(i)); }
        public override long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
        {
            var b = GetValue(i) as byte[] ?? new byte[0];
            if (buffer == null) return b.Length;
            int n = (int)Math.Min(length, b.Length - fieldOffset);
            if (n > 0) Array.Copy(b, fieldOffset, buffer, bufferoffset, n);
            return Math.Max(0, n);
        }
        public override char GetChar(int i) { return Convert.ToString(GetValue(i))[0]; }
        public override long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
        {
            var s = Convert.ToString(GetValue(i));
            if (buffer == null) return s.Length;
            int n = (int)Math.Min(length, s.Length - fieldoffset);
            if (n > 0) s.CopyTo((int)fieldoffset, buffer, bufferoffset, n);
            return Math.Max(0, n);
        }
        public override string GetDataTypeName(int i) { return GetFieldType(i).Name; }
        public override DateTime GetDateTime(int i) { return DateTime.Parse(Convert.ToString(GetValue(i)), CultureInfo.InvariantCulture); }
        public override decimal GetDecimal(int i) { return Convert.ToDecimal(GetValue(i), CultureInfo.InvariantCulture); }
        public override double GetDouble(int i) { return Convert.ToDouble(GetValue(i), CultureInfo.InvariantCulture); }
        public override Type GetFieldType(int i)
        {
            switch (_conn.Api.ColumnType(_stmt, i))
            {
                case 1: return typeof(long);
                case 2: return typeof(double);
                case 4: return typeof(byte[]);
                default: return typeof(string);
            }
        }
        public override float GetFloat(int i) { return (float)GetDouble(i); }
        public override Guid GetGuid(int i) { return new Guid(Convert.ToString(GetValue(i))); }
        public override short GetInt16(int i) { return Convert.ToInt16(GetValue(i)); }
        public override int GetInt32(int i) { return Convert.ToInt32(GetValue(i)); }
        public override long GetInt64(int i) { return Convert.ToInt64(GetValue(i)); }
        public override string GetString(int i) { return Convert.ToString(GetValue(i), CultureInfo.InvariantCulture); }
        public override int GetValues(object[] values)
        {
            int n = Math.Min(values.Length, FieldCount);
            for (int i = 0; i < n; i++) values[i] = GetValue(i);
            return n;
        }
        public override IEnumerator GetEnumerator() { return new DbEnumerator(this); }
    }

    /// <summary>
    /// The sqlite3 C functions, from whichever library this computer has. Windows'
    /// own winsqlite3 uses __stdcall; the usual sqlite3.dll and libsqlite3 __cdecl
    /// (the same thing on a 64-bit process, not on a 32-bit one).
    /// </summary>
    public abstract class NativeApi
    {
        public abstract string Name { get; }
        public abstract int Open(byte[] file, out IntPtr db, int flags, IntPtr vfs);
        public abstract int Close(IntPtr db);
        public abstract int BusyTimeout(IntPtr db, int ms);
        protected abstract IntPtr ErrMsg(IntPtr db);
        protected abstract IntPtr LibVersion();
        public abstract int Prepare(IntPtr db, IntPtr sql, int bytes, out IntPtr stmt, out IntPtr tail);
        public abstract int Step(IntPtr stmt);
        public abstract int Finalize(IntPtr stmt);
        public abstract int Changes(IntPtr db);
        public abstract int ParameterIndex(IntPtr stmt, byte[] name);
        public abstract int BindNull(IntPtr stmt, int i);
        public abstract int BindInt64(IntPtr stmt, int i, long v);
        public abstract int BindDouble(IntPtr stmt, int i, double v);
        protected abstract int BindTextRaw(IntPtr stmt, int i, byte[] utf8, int n, IntPtr destructor);
        protected abstract int BindBlobRaw(IntPtr stmt, int i, byte[] data, int n, IntPtr destructor);
        public abstract int ColumnCount(IntPtr stmt);
        protected abstract IntPtr ColumnNameRaw(IntPtr stmt, int i);
        public abstract int ColumnType(IntPtr stmt, int i);
        public abstract long ColumnInt64(IntPtr stmt, int i);
        public abstract double ColumnDouble(IntPtr stmt, int i);
        protected abstract IntPtr ColumnTextRaw(IntPtr stmt, int i);
        protected abstract IntPtr ColumnBlobRaw(IntPtr stmt, int i);
        protected abstract int ColumnBytes(IntPtr stmt, int i);

        private static readonly IntPtr Transient = new IntPtr(-1);   // SQLITE_TRANSIENT: sqlite copies the value

        public string ErrorMessage(IntPtr db) { return FromUtf8(ErrMsg(db)); }
        public string Version() { return FromUtf8(LibVersion()); }
        public int BindText(IntPtr stmt, int i, string s) { var b = Encoding.UTF8.GetBytes(s ?? ""); return BindTextRaw(stmt, i, b, b.Length, Transient); }
        public int BindBlob(IntPtr stmt, int i, byte[] data) { return BindBlobRaw(stmt, i, data, data.Length, Transient); }
        public string ColumnName(IntPtr stmt, int i) { return FromUtf8(ColumnNameRaw(stmt, i)); }
        public string ColumnText(IntPtr stmt, int i)
        {
            IntPtr p = ColumnTextRaw(stmt, i);
            int n = ColumnBytes(stmt, i);
            if (p == IntPtr.Zero || n <= 0) return "";
            var b = new byte[n];
            Marshal.Copy(p, b, 0, n);
            return Encoding.UTF8.GetString(b);
        }
        public byte[] ColumnBlob(IntPtr stmt, int i)
        {
            IntPtr p = ColumnBlobRaw(stmt, i);
            int n = ColumnBytes(stmt, i);
            var b = new byte[Math.Max(0, n)];
            if (p != IntPtr.Zero && n > 0) Marshal.Copy(p, b, 0, n);
            return b;
        }

        public static byte[] Utf8(string s)
        {
            var b = Encoding.UTF8.GetBytes(s ?? "");
            var z = new byte[b.Length + 1];
            Array.Copy(b, z, b.Length);
            return z;
        }

        private static string FromUtf8(IntPtr p)
        {
            if (p == IntPtr.Zero) return "";
            int n = 0;
            while (Marshal.ReadByte(p, n) != 0) n++;
            var b = new byte[n];
            Marshal.Copy(p, b, 0, n);
            return Encoding.UTF8.GetString(b);
        }

        private static NativeApi _default;

        /// <summary>
        /// The library to use: forced by <see cref="Force"/>, else the first that
        /// answers - winsqlite3 (Windows), sqlite3.dll beside the program, libsqlite3.
        /// </summary>
        public static NativeApi Default()
        {
            if (_default != null) return _default;
            var candidates = new List<NativeApi>();
            if (Force != null) candidates.Add(Force);
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                candidates.Add(new WinSqlite3());
                candidates.Add(new Sqlite3Dll());
            }
            else candidates.Add(new LibSqlite3());
            var errors = new List<string>();
            foreach (var c in candidates)
            {
                try { c.Version(); _default = c; return c; }
                catch (Exception ex) { errors.Add(c.Name + ": " + ex.GetType().Name); }
            }
            throw new DllNotFoundException("no SQLite library found (" + string.Join("; ", errors) + ")");
        }

        /// <summary>Tried before the others (a test forcing one library).</summary>
        public static NativeApi Force;

        /// <summary>True when a library answers.</summary>
        public static bool Available
        {
            get { try { Default(); return true; } catch { return false; } }
        }
    }

    internal sealed class WinSqlite3 : NativeApi
    {
        private const string L = "winsqlite3.dll";
        private const CallingConvention C = CallingConvention.StdCall;
        public override string Name { get { return L; } }
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_open_v2(byte[] f, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_close_v2(IntPtr db);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_busy_timeout(IntPtr db, int ms);
        [DllImport(L, CallingConvention = C)] private static extern IntPtr sqlite3_errmsg(IntPtr db);
        [DllImport(L, CallingConvention = C)] private static extern IntPtr sqlite3_libversion();
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_prepare_v2(IntPtr db, IntPtr sql, int n, out IntPtr stmt, out IntPtr tail);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_step(IntPtr s);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_finalize(IntPtr s);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_changes(IntPtr db);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_parameter_index(IntPtr s, byte[] name);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_null(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_int64(IntPtr s, int i, long v);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_double(IntPtr s, int i, double v);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_text(IntPtr s, int i, byte[] v, int n, IntPtr d);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_blob(IntPtr s, int i, byte[] v, int n, IntPtr d);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_column_count(IntPtr s);
        [DllImport(L, CallingConvention = C)] private static extern IntPtr sqlite3_column_name(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_column_type(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern long sqlite3_column_int64(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern double sqlite3_column_double(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern IntPtr sqlite3_column_text(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern IntPtr sqlite3_column_blob(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_column_bytes(IntPtr s, int i);
        public override int Open(byte[] f, out IntPtr db, int flags, IntPtr vfs) { return sqlite3_open_v2(f, out db, flags, vfs); }
        public override int Close(IntPtr db) { return sqlite3_close_v2(db); }
        public override int BusyTimeout(IntPtr db, int ms) { return sqlite3_busy_timeout(db, ms); }
        protected override IntPtr ErrMsg(IntPtr db) { return sqlite3_errmsg(db); }
        protected override IntPtr LibVersion() { return sqlite3_libversion(); }
        public override int Prepare(IntPtr db, IntPtr sql, int n, out IntPtr stmt, out IntPtr tail) { return sqlite3_prepare_v2(db, sql, n, out stmt, out tail); }
        public override int Step(IntPtr s) { return sqlite3_step(s); }
        public override int Finalize(IntPtr s) { return sqlite3_finalize(s); }
        public override int Changes(IntPtr db) { return sqlite3_changes(db); }
        public override int ParameterIndex(IntPtr s, byte[] name) { return sqlite3_bind_parameter_index(s, name); }
        public override int BindNull(IntPtr s, int i) { return sqlite3_bind_null(s, i); }
        public override int BindInt64(IntPtr s, int i, long v) { return sqlite3_bind_int64(s, i, v); }
        public override int BindDouble(IntPtr s, int i, double v) { return sqlite3_bind_double(s, i, v); }
        protected override int BindTextRaw(IntPtr s, int i, byte[] v, int n, IntPtr d) { return sqlite3_bind_text(s, i, v, n, d); }
        protected override int BindBlobRaw(IntPtr s, int i, byte[] v, int n, IntPtr d) { return sqlite3_bind_blob(s, i, v, n, d); }
        public override int ColumnCount(IntPtr s) { return sqlite3_column_count(s); }
        protected override IntPtr ColumnNameRaw(IntPtr s, int i) { return sqlite3_column_name(s, i); }
        public override int ColumnType(IntPtr s, int i) { return sqlite3_column_type(s, i); }
        public override long ColumnInt64(IntPtr s, int i) { return sqlite3_column_int64(s, i); }
        public override double ColumnDouble(IntPtr s, int i) { return sqlite3_column_double(s, i); }
        protected override IntPtr ColumnTextRaw(IntPtr s, int i) { return sqlite3_column_text(s, i); }
        protected override IntPtr ColumnBlobRaw(IntPtr s, int i) { return sqlite3_column_blob(s, i); }
        protected override int ColumnBytes(IntPtr s, int i) { return sqlite3_column_bytes(s, i); }
    }

    internal sealed class Sqlite3Dll : NativeApi
    {
        private const string L = "sqlite3.dll";
        private const CallingConvention C = CallingConvention.Cdecl;
        public override string Name { get { return L; } }
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_open_v2(byte[] f, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_close_v2(IntPtr db);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_busy_timeout(IntPtr db, int ms);
        [DllImport(L, CallingConvention = C)] private static extern IntPtr sqlite3_errmsg(IntPtr db);
        [DllImport(L, CallingConvention = C)] private static extern IntPtr sqlite3_libversion();
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_prepare_v2(IntPtr db, IntPtr sql, int n, out IntPtr stmt, out IntPtr tail);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_step(IntPtr s);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_finalize(IntPtr s);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_changes(IntPtr db);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_parameter_index(IntPtr s, byte[] name);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_null(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_int64(IntPtr s, int i, long v);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_double(IntPtr s, int i, double v);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_text(IntPtr s, int i, byte[] v, int n, IntPtr d);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_blob(IntPtr s, int i, byte[] v, int n, IntPtr d);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_column_count(IntPtr s);
        [DllImport(L, CallingConvention = C)] private static extern IntPtr sqlite3_column_name(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_column_type(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern long sqlite3_column_int64(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern double sqlite3_column_double(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern IntPtr sqlite3_column_text(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern IntPtr sqlite3_column_blob(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_column_bytes(IntPtr s, int i);
        public override int Open(byte[] f, out IntPtr db, int flags, IntPtr vfs) { return sqlite3_open_v2(f, out db, flags, vfs); }
        public override int Close(IntPtr db) { return sqlite3_close_v2(db); }
        public override int BusyTimeout(IntPtr db, int ms) { return sqlite3_busy_timeout(db, ms); }
        protected override IntPtr ErrMsg(IntPtr db) { return sqlite3_errmsg(db); }
        protected override IntPtr LibVersion() { return sqlite3_libversion(); }
        public override int Prepare(IntPtr db, IntPtr sql, int n, out IntPtr stmt, out IntPtr tail) { return sqlite3_prepare_v2(db, sql, n, out stmt, out tail); }
        public override int Step(IntPtr s) { return sqlite3_step(s); }
        public override int Finalize(IntPtr s) { return sqlite3_finalize(s); }
        public override int Changes(IntPtr db) { return sqlite3_changes(db); }
        public override int ParameterIndex(IntPtr s, byte[] name) { return sqlite3_bind_parameter_index(s, name); }
        public override int BindNull(IntPtr s, int i) { return sqlite3_bind_null(s, i); }
        public override int BindInt64(IntPtr s, int i, long v) { return sqlite3_bind_int64(s, i, v); }
        public override int BindDouble(IntPtr s, int i, double v) { return sqlite3_bind_double(s, i, v); }
        protected override int BindTextRaw(IntPtr s, int i, byte[] v, int n, IntPtr d) { return sqlite3_bind_text(s, i, v, n, d); }
        protected override int BindBlobRaw(IntPtr s, int i, byte[] v, int n, IntPtr d) { return sqlite3_bind_blob(s, i, v, n, d); }
        public override int ColumnCount(IntPtr s) { return sqlite3_column_count(s); }
        protected override IntPtr ColumnNameRaw(IntPtr s, int i) { return sqlite3_column_name(s, i); }
        public override int ColumnType(IntPtr s, int i) { return sqlite3_column_type(s, i); }
        public override long ColumnInt64(IntPtr s, int i) { return sqlite3_column_int64(s, i); }
        public override double ColumnDouble(IntPtr s, int i) { return sqlite3_column_double(s, i); }
        protected override IntPtr ColumnTextRaw(IntPtr s, int i) { return sqlite3_column_text(s, i); }
        protected override IntPtr ColumnBlobRaw(IntPtr s, int i) { return sqlite3_column_blob(s, i); }
        protected override int ColumnBytes(IntPtr s, int i) { return sqlite3_column_bytes(s, i); }
    }

    internal sealed class LibSqlite3 : NativeApi
    {
        private const string L = "libsqlite3.so.0";
        private const CallingConvention C = CallingConvention.Cdecl;
        public override string Name { get { return L; } }
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_open_v2(byte[] f, out IntPtr db, int flags, IntPtr vfs);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_close_v2(IntPtr db);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_busy_timeout(IntPtr db, int ms);
        [DllImport(L, CallingConvention = C)] private static extern IntPtr sqlite3_errmsg(IntPtr db);
        [DllImport(L, CallingConvention = C)] private static extern IntPtr sqlite3_libversion();
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_prepare_v2(IntPtr db, IntPtr sql, int n, out IntPtr stmt, out IntPtr tail);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_step(IntPtr s);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_finalize(IntPtr s);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_changes(IntPtr db);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_parameter_index(IntPtr s, byte[] name);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_null(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_int64(IntPtr s, int i, long v);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_double(IntPtr s, int i, double v);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_text(IntPtr s, int i, byte[] v, int n, IntPtr d);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_bind_blob(IntPtr s, int i, byte[] v, int n, IntPtr d);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_column_count(IntPtr s);
        [DllImport(L, CallingConvention = C)] private static extern IntPtr sqlite3_column_name(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_column_type(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern long sqlite3_column_int64(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern double sqlite3_column_double(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern IntPtr sqlite3_column_text(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern IntPtr sqlite3_column_blob(IntPtr s, int i);
        [DllImport(L, CallingConvention = C)] private static extern int sqlite3_column_bytes(IntPtr s, int i);
        public override int Open(byte[] f, out IntPtr db, int flags, IntPtr vfs) { return sqlite3_open_v2(f, out db, flags, vfs); }
        public override int Close(IntPtr db) { return sqlite3_close_v2(db); }
        public override int BusyTimeout(IntPtr db, int ms) { return sqlite3_busy_timeout(db, ms); }
        protected override IntPtr ErrMsg(IntPtr db) { return sqlite3_errmsg(db); }
        protected override IntPtr LibVersion() { return sqlite3_libversion(); }
        public override int Prepare(IntPtr db, IntPtr sql, int n, out IntPtr stmt, out IntPtr tail) { return sqlite3_prepare_v2(db, sql, n, out stmt, out tail); }
        public override int Step(IntPtr s) { return sqlite3_step(s); }
        public override int Finalize(IntPtr s) { return sqlite3_finalize(s); }
        public override int Changes(IntPtr db) { return sqlite3_changes(db); }
        public override int ParameterIndex(IntPtr s, byte[] name) { return sqlite3_bind_parameter_index(s, name); }
        public override int BindNull(IntPtr s, int i) { return sqlite3_bind_null(s, i); }
        public override int BindInt64(IntPtr s, int i, long v) { return sqlite3_bind_int64(s, i, v); }
        public override int BindDouble(IntPtr s, int i, double v) { return sqlite3_bind_double(s, i, v); }
        protected override int BindTextRaw(IntPtr s, int i, byte[] v, int n, IntPtr d) { return sqlite3_bind_text(s, i, v, n, d); }
        protected override int BindBlobRaw(IntPtr s, int i, byte[] v, int n, IntPtr d) { return sqlite3_bind_blob(s, i, v, n, d); }
        public override int ColumnCount(IntPtr s) { return sqlite3_column_count(s); }
        protected override IntPtr ColumnNameRaw(IntPtr s, int i) { return sqlite3_column_name(s, i); }
        public override int ColumnType(IntPtr s, int i) { return sqlite3_column_type(s, i); }
        public override long ColumnInt64(IntPtr s, int i) { return sqlite3_column_int64(s, i); }
        public override double ColumnDouble(IntPtr s, int i) { return sqlite3_column_double(s, i); }
        protected override IntPtr ColumnTextRaw(IntPtr s, int i) { return sqlite3_column_text(s, i); }
        protected override IntPtr ColumnBlobRaw(IntPtr s, int i) { return sqlite3_column_blob(s, i); }
        protected override int ColumnBytes(IntPtr s, int i) { return sqlite3_column_bytes(s, i); }
    }
}
