using Kaban.Core.Enums;
using System;
using System.Collections.Generic;

namespace Kaban.Core.Helpers
{
    public static class SecurityManager
    {
        public static event EventHandler UserChanged;

        public static SecurityLevel CurrentLevel { get; private set; } = SecurityLevel.None;
        public static string CurrentUser { get; private set; } = "Guest";

        // --- Basic brute-force protection ---
        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromSeconds(60);
        private static readonly Dictionary<string, int> _failedAttempts =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, DateTime> _lockedUntil =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Attempts to log a user in against the credential store (UserStore).
        /// This is the ONLY entry point that should be used from the UI -
        /// it is the real authentication check that was previously missing.
        /// </summary>
        public static bool TryLogin(string username, string password, out string errorMessage)
        {
            errorMessage = "";
            string key = username ?? "";

            DateTime until;
            if (_lockedUntil.TryGetValue(key, out until) && DateTime.Now < until)
            {
                int secondsLeft = (int)Math.Ceiling((until - DateTime.Now).TotalSeconds);
                errorMessage = $"Account locked. Try again in {secondsLeft}s.";
                return false;
            }

            UserAccount account;
            if (UserStore.TryValidate(username, password, out account))
            {
                _failedAttempts[key] = 0;
                Login(account.Username, account.Level);
                UserStore.RememberLastUser(account.Username);   // offered first by the next login dialog
                AuditLogger.Log($"logged in (level: {account.Level}).");
                return true;
            }

            int attempts;
            attempts = _failedAttempts.TryGetValue(key, out attempts) ? attempts + 1 : 1;
            _failedAttempts[key] = attempts;
            AuditLogger.Log($"FAILED login attempt for user '{username}'.");

            if (attempts >= MaxFailedAttempts)
            {
                _lockedUntil[key] = DateTime.Now.Add(LockoutDuration);
                errorMessage = $"Too many failed attempts. Account locked for {LockoutDuration.TotalSeconds:0}s.";
            }
            else
            {
                errorMessage = $"Invalid username or password ({attempts}/{MaxFailedAttempts} attempts).";
            }

            return false;
        }

        /// <summary>
        /// Directly sets the current session's user/level WITHOUT checking a password.
        /// Only call this after TryLogin() has already succeeded, or from trusted
        /// internal bootstrapping code (e.g. the Simulation driver's test harness).
        /// Do NOT call this directly from any UI in response to user input.
        /// </summary>
        public static void Login(string username, SecurityLevel level)
        {
            CurrentUser = username;
            CurrentLevel = level;
            UserChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void Logout()
        {
            if (CurrentLevel != SecurityLevel.None)
                AuditLogger.Log("logged out.");

            CurrentUser = "Guest";
            CurrentLevel = SecurityLevel.None;
            UserChanged?.Invoke(null, EventArgs.Empty);
        }

        // The magic check: Does the current user have high enough clearance?
        public static bool HasAccess(SecurityLevel requiredLevel)
        {
            return (int)CurrentLevel >= (int)requiredLevel;
        }
    }
}

