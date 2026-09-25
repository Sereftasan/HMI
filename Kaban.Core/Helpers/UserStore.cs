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
            // The store is one "name;level;salt;hash" line per user: a ";" or a line
            // break in a name split that line on the next start, and the account
            // (with every account after it on a broken line) was silently lost.
            if (!IsValidUsername(username))
                throw new ArgumentException("A user name may not contain ';' or line breaks.");
            if (WouldRemoveLastAdmin(username, level))
                throw new InvalidOperationException("The last Admin account cannot be given a lower level.");

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
            // Never the last Admin: with none left, nobody can manage users, PLC
            // connections or imports on this machine again.
            if (WouldRemoveLastAdmin(username, null)) return false;
            bool ok = _users.Remove(username);
            if (ok)
            {
                Persist();
                DisplaySettings.SetUserFactor(username, null);    // their magnifying factor goes with them
            }
            return ok;
        }

        /// <summary>All accounts (for the User Settings page).</summary>
        public static IEnumerable<UserAccount> GetAllUsers() => _users.Values;

        /// <summary>Change only the level of an existing account (keeps the password).</summary>
        public static bool SetLevel(string username, SecurityLevel level)
        {
            UserAccount u;
            if (!_users.TryGetValue(username, out u)) return false;
            if (WouldRemoveLastAdmin(username, level)) return false;     // see RemoveUser
            u.Level = level;
            Persist();
            return true;
        }

        /// <summary>True for a name the store can keep (no ';', no line breaks).</summary>
        public static bool IsValidUsername(string username)
        {
            return !string.IsNullOrWhiteSpace(username) &&
                   username.IndexOfAny(new[] { ';', '\r', '\n' }) < 0;
        }

        /// <summary>
        /// True when removing the user (newLevel null) or giving it newLevel would
        /// leave no Admin account at all.
        /// </summary>
        public static bool WouldRemoveLastAdmin(string username, SecurityLevel? newLevel)
        {
            UserAccount u;
            if (username == null || !_users.TryGetValue(username, out u) || u.Level != SecurityLevel.Admin) return false;
            if (newLevel.HasValue && newLevel.Value >= SecurityLevel.Admin) return false;
            int admins = 0;
            foreach (var x in _users.Values) if (x.Level == SecurityLevel.Admin) admins++;
            return admins <= 1;
        }

        // ------------------------------------------------------------------
        // PERSISTENCE: accounts survive a restart. Stored as
        // username;level;salt(base64);hash(base64) under CommonAppData\KabanHMI.
        // Passwords are never stored - only their PBKDF2 hash + salt.
        // ------------------------------------------------------------------
        private static readonly string StorePath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Users.dat");
        private static bool _loading;

        // ------------------------------------------------------------------
        // THE LAST USER: who signed in last, so the login dialog can start with
        // that name selected and the cursor in the password box. Only a name -
        // never a password or anything that would sign anybody in by itself.
        // ------------------------------------------------------------------
        internal static string LastUserPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "LastUser.txt");

        /// <summary>The name that signed in last ("" when none, or when it is no longer an account).</summary>
        public static string LastUser
        {
            get
            {
                try
                {
                    if (!System.IO.File.Exists(LastUserPath)) return "";
                    string name = System.IO.File.ReadAllText(LastUserPath).Trim();
                    return _users.ContainsKey(name) ? _users[name].Username : "";
                }
                catch { return ""; }
            }
        }

        /// <summary>Kept after a successful login (see SecurityManager.TryLogin).</summary>
        public static void RememberLastUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return;
            try { SafeFile.WriteAllText(LastUserPath, username.Trim(), new UTF8Encoding(false)); }
            catch { /* a name not remembered is only a convenience lost */ }
        }

        internal static void Persist()
        {
            if (_loading) return;
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (var u in _users.Values)
                    sb.AppendLine(u.Username + ";" + u.Level + ";" + u.Salt + ";" + u.PasswordHash);
                // All-or-nothing: a power cut mid-write must not leave a machine
                // with no accounts (see SafeFile).
                SafeFile.WriteAllText(StorePath, sb.ToString(), new UTF8Encoding(false));
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

            // A damaged line in Users.dat (bad base64, a truncated salt) used to
            // throw here - out of the login dialog. It is simply a failed login.
            string attemptHash;
            try
            {
                byte[] salt = Convert.FromBase64String(user.Salt ?? "");
                attemptHash = HashPassword(password, salt);
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException || ex is CryptographicException)
            {
                AuditLogger.Log("login refused: the stored account '" + username + "' is damaged.");
                return false;
            }

            // Constant-time compare to avoid leaking hash info via timing.
            if (!FixedTimeEquals(attemptHash, user.PasswordHash)) return false;

            account = user;
            return true;
        }

        public static bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            if (!TryValidate(username, oldPassword, out var account)) return false;
            AddOrUpdateUser(username, newPassword, account.Level);     // saves
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

