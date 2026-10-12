using Kaban.Core.Enums;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// A single HMI user account. Passwords are never stored in plain text -
    /// only a salted hash is kept (see UserStore).
    /// </summary>
    public class UserAccount
    {
        public string Username { get; set; }
        public SecurityLevel Level { get; set; }
        public string PasswordHash { get; set; }
        public string Salt { get; set; }
    }
}

