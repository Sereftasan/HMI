using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Enums;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// WHO MAY CHANGE WHICH ACCOUNT (HMI Settings > Users and Preferences).
    ///
    /// - Their own: anyone signed in - password, screen scale, view preferences;
    ///   never their own level, never deleting themselves.
    /// - An account BELOW their level: edit, delete, any level up to their own.
    /// - An account OF THEIR LEVEL: only the one who created it (UserStore keeps
    ///   who created each account), so the setter-up can fix a password or a
    ///   preference later; other users of that level cannot. An Admin therefore
    ///   changes every account but another Admin's - unless they created it.
    /// - A new account: at their level or lower.
    /// - Everyone's preferences (the panel's): Maintenance and Admin.
    /// The last Admin is never deleted or lowered (UserStore).
    /// </summary>
    public static class UserRights
    {
        /// <summary>The one signed in, or null.</summary>
        public static string Actor
        {
            get { return SecurityManager.CurrentLevel == SecurityLevel.None ? null : SecurityManager.CurrentUser; }
        }

        public static SecurityLevel ActorLevel { get { return SecurityManager.CurrentLevel; } }

        private static UserAccount Account(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return UserStore.GetAllUsers().FirstOrDefault(u => string.Equals(u.Username, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsSelf(string actor, string target)
        {
            return actor != null && target != null && string.Equals(actor.Trim(), target.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>May <paramref name="actor"/> (at <paramref name="actorLevel"/>) change the account <paramref name="target"/>?</summary>
        public static bool CanManage(string actor, SecurityLevel actorLevel, string target)
        {
            if (actor == null || actorLevel == SecurityLevel.None) return false;
            var t = Account(target);
            if (t == null) return false;
            if (IsSelf(actor, t.Username)) return true;
            if (actorLevel > t.Level) return true;
            return actorLevel == t.Level && IsSelf(actor, UserStore.CreatedBy(t.Username));
        }

        public static bool CanManage(string target) { return CanManage(Actor, ActorLevel, target); }

        /// <summary>Delete: one they manage, never themselves, never the last Admin.</summary>
        public static bool CanDelete(string target)
        {
            return CanManage(target) && !IsSelf(Actor, target) && !UserStore.WouldRemoveLastAdmin(target, null);
        }

        /// <summary>Give an account a level: one they manage (not themselves), up to their own level.</summary>
        public static bool CanSetLevel(string target, SecurityLevel level)
        {
            return CanManage(target) && !IsSelf(Actor, target) && level >= SecurityLevel.Operator && level <= ActorLevel &&
                   !UserStore.WouldRemoveLastAdmin(target, level);
        }

        /// <summary>A new account at this level.</summary>
        public static bool CanCreate(SecurityLevel level)
        {
            return Actor != null && level >= SecurityLevel.Operator && level <= ActorLevel;
        }

        /// <summary>The levels they may give: Operator up to their own.</summary>
        public static IList<SecurityLevel> LevelsToGive()
        {
            return new[] { SecurityLevel.Operator, SecurityLevel.Maintenance, SecurityLevel.Admin }.Where(l => l <= ActorLevel).ToList();
        }

        /// <summary>Everyone's preferences and the panel's screen scale.</summary>
        public static bool CanEditEveryone { get { return Actor != null && ActorLevel >= SecurityLevel.Maintenance; } }
    }
}
