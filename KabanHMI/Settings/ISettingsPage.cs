using System.Windows.Forms;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// One sub-page of the Program Settings screen (Unit Settings, User Settings,
    /// Log Settings, Machine Parameter Records, Barcode Settings, About, ...).
    ///
    /// A settings page is just a UserControl plus a translated title and an optional
    /// minimum security level. SettingsScreen discovers the pages it is given and
    /// shows one tab per page, so adding a new settings area is: implement this,
    /// and add it to the list in SettingsScreen.
    /// </summary>
    public interface ISettingsPage
    {
        /// <summary>Translation key for the tab caption (falls back to TitleFallback).</summary>
        string TitleKey { get; }

        /// <summary>Literal caption if the key is not in the translation files.</summary>
        string TitleFallback { get; }

        /// <summary>Minimum level required to open this sub-page. None = anyone.</summary>
        Kaban.Core.Enums.SecurityLevel RequiredLevel { get; }

        /// <summary>The actual page content.</summary>
        UserControl GetControl();
    }

    /// <summary>
    /// A page that puts its texts on when it is BUILT - made in code, not of the HMI
    /// controls (they follow a language change themselves). The tab strip holding it
    /// builds it again after a language change: the one on show at once, the others
    /// when they are next shown (12 October: Users and Preferences stayed English with
    /// the flag on TR). <see cref="Place"/> is what the operator was looking at, handed
    /// to the new one by <see cref="ReturnTo"/> (null: nothing to keep).
    /// </summary>
    public interface IRebuildForLanguage
    {
        object Place { get; }
        void ReturnTo(object place);
    }
}

