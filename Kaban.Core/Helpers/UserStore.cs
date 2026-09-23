using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Kaban.Core.Enums;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// In-memory user credential store.
    ///
    /// *** IMPORTANT / PRODUCTION NOTE ***
    /// This class ships with three DEFAULT accounts (operator/maintenance/admin,
    /// all with password "1234") purely so the HMI has something to log in with
    /// out of the box. These default passwords MUST be changed before the
    /// machine goes into service - see ChangePassword() below.
    ///
    /// For a real deployment, replace the in-memory dictionary with:
    ///   - an encrypted local file (DPAPI / AES with a machine-specific key), or
    ///   - a shared user database, or
    ///   - Windows/Active Directory group membership,
    /// so that credentials survive HMI restarts and can be centrally managed.
    /// Passwords are never stored or compared in plain text here - only a
    /// PBKDF2 salted hash - so even this in-memory version doesn't keep a
    /// recoverable copy of the password in code or in memory dumps.
    /// </summary>
    public static class UserStore
    {
        private static readonly Dictionary<string, UserAccount> _users =
            new Dictionary<string, UserAccount>(StringComparer.OrdinalIgnoreCase);

        private const int Iterations = 100000;
        private const int HashSize = 32;
        private const int SaltSize = 16;

        static UserStore()
        {
            // LOAD FIRST. SEED ONLY WHEN NOTHING WAS STORED.
            //
            // The seeds used to be added first - and every AddOrUpdateUser saves,
            // so each start-up wrote the three defaults over Users.dat and then
            // "loaded" that file. Every account created on the machine was erased
            // before it could be read, which is why users reset on every restart.
            LoadPersisted();
            if (_users.Count == 0)
            {
                bool firstRun = !System.IO.File.Exists(StorePath);
                _loading = true;              // seeding is not an edit
                try
                {
                    // CHANGE THESE PASSWORDS before commissioning the machine.
                    AddOrUpdateUser("operator", "1234", SecurityLevel.Operator);
                    AddOrUpdateUser("maintenance", "1234", SecurityLevel.Maintenance);
                    AddOrUpdateUser("admin", "1234", SecurityLevel.Admin);
                }
                finally { _loading = false; }

                // Written only on a genuine first run. A store that exists but
                // could not be read (locked, damaged) keeps the defaults in memory
                // so an admin can log in - it is never overwritten with them.
                if (firstRun) Persist();
            }
        }

        public static void AddOrUpdateUser(string username, string plainTextPassword, SecurityLevel level)
        {
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username required.");
            if (string.IsNullOrEmpty(plainTextPassword)) throw new ArgumentException("Password required.");

            byte[] salt = new byte[SaltSize];
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(salt);
            string hash = HashPassword(plainTextPassword, salt);

            _users[username] = new UserAccount
            {
                Username = username,
                Level = level,
                Salt = Convert.ToBase64String(salt),
                PasswordHash = hash
            };
            Persist();
        }

        public static bool RemoveUser(string username)
        {
            bool ok = _users.Remove(username);
            if (ok) Persist();
            return ok;
        }

        /// <summary>All accounts (for the User Settings page).</summary>
        public static IEnumerable<UserAccount> GetAllUsers() => _users.Values;

        /// <summary>Change only the level of an existing account (keeps the password).</summary>
        public static bool SetLevel(string username, SecurityLevel level)
        {
            UserAccount u;
            if (!_users.TryGetValue(username, out u)) return false;
            u.Level = level;
            Persist();
            return true;
        }

        // ------------------------------------------------------------------
        // PERSISTENCE: accounts survive a restart. Stored as
        // username;level;salt(base64);hash(base64) under CommonAppData\KabanHMI.
        // Passwords are never stored - only their PBKDF2 hash + salt.
        // ------------------------------------------------------------------
        private static readonly string StorePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Users.dat");
        private static bool _loading;

        internal static void Persist()
        {
            if (_loading) return;
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(StorePath));
                var sb = new System.Text.StringBuilder();
                foreach (var u in _users.Values)
                    sb.AppendLine(u.Username + ";" + u.Level + ";" + u.Salt + ";" + u.PasswordHash);
                System.IO.File.WriteAllText(StorePath, sb.ToString());
            }
            catch { /* keep running even if the store is unwritable */ }
        }

        private static void LoadPersisted()
        {
            try
            {
                if (!System.IO.File.Exists(StorePath)) return;
                _loading = true;
                _users.Clear();   // the stored set replaces the seeded defaults
                foreach (var line in System.IO.File.ReadAllLines(StorePath))
                {
                    var parts = line.Split(';');
                    if (parts.Length != 4) continue;
                    SecurityLevel level;
                    if (!Enum.TryParse(parts[1], out level)) continue;
                    _users[parts[0]] = new UserAccount
                    {
                        Username = parts[0],
                        Level = level,
                        Salt = parts[2],
                        PasswordHash = parts[3]
                    };
                }
            }
            catch { }
            finally { _loading = false; }
        }

        public static bool TryValidate(string username, string password, out UserAccount account)
        {
            account = null;
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return false;

            if (!_users.TryGetValue(username, out var user)) return false;

            byte[] salt = Convert.FromBase64String(user.Salt);
            string attemptHash = HashPassword(password, salt);

            // Constant-time compare to avoid leaking hash info via timing.
            if (!FixedTimeEquals(attemptHash, user.PasswordHash)) return false;

            account = user;
            return true;
        }

        public static bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            if (!TryValidate(username, oldPassword, out var account)) return false;
            AddOrUpdateUser(username, newPassword, account.Level);
            Persist();
            return true;
        }

        private static string HashPassword(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
            {
                return Convert.ToBase64String(pbkdf2.GetBytes(HashSize));
            }
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            byte[] ba = Encoding.UTF8.GetBytes(a);
            byte[] bb = Encoding.UTF8.GetBytes(b);
            if (ba.Length != bb.Length) return false;

            int diff = 0;
            for (int i = 0; i < ba.Length; i++) diff |= ba[i] ^ bb[i];
            return diff == 0;
        }
    }
}

