using System;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// THE LANGUAGE OF THE ONE SIGNED IN, as the screen scale is theirs (12 October:
    /// "add language selection to the preferences as well, under Screen, so the selection
    /// turns to the user's preference if it is selected - just like Screen scale").
    ///
    /// Everyone's language - this panel's, in force while nobody is signed in and for
    /// every user "As for everyone" - is the flag's on the top bar, kept now (a restart
    /// came back in English). A user's own (HMI Settings > Users and Preferences >
    /// Screen) is put on the screen when they sign in, at once - no restart, unlike the
    /// scale - and everyone's again when they sign out (the one the screen had before
    /// theirs, while everyone's was never chosen).
    /// </summary>
    public static class UserLanguage
    {
        private static bool _attached;
        // the signed-in user's own language on the screen (null: none), and the one before it
        private static string _ownShown, _beforeOwn;

        /// <summary>Once the translations are read: everyone's language now, the user's at each sign-in.</summary>
        public static void Attach()
        {
            Apply();
            if (_attached) return;
            _attached = true;
            SecurityManager.UserChanged += (s, e) => Apply();
        }

        /// <summary>The language in force put on the screen - when one was chosen and its texts are there.</summary>
        public static void Apply()
        {
            var shell = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (shell != null && shell.InvokeRequired)
            {
                try { shell.BeginInvoke(new Action(Apply)); } catch { }
                return;
            }
            var user = ViewPreferences.ActiveUser;
            string own = user != null ? ViewPreferences.UserValue(user, ViewPreferences.Language) : null;
            string lang;
            if (own != null)
            {
                if (_ownShown == null) _beforeOwn = LanguageManager.CurrentLanguage;
                _ownShown = own;
                lang = own;
            }
            else
            {
                lang = ViewPreferences.PanelChosen(ViewPreferences.Language) ?? (_ownShown != null ? _beforeOwn : null);
                _ownShown = null;
            }
            if (string.IsNullOrWhiteSpace(lang)) return;
            lang = lang.Trim().ToUpperInvariant();
            if (!LanguageManager.AvailableLanguages.Any(l => string.Equals(l, lang, StringComparison.OrdinalIgnoreCase))) return;
            if (string.Equals(LanguageManager.CurrentLanguage, lang, StringComparison.OrdinalIgnoreCase)) return;
            LanguageManager.ChangeLanguage(lang);
        }

        /// <summary>The flag on the top bar: everyone's language, on the screen at once (a user's own stays theirs).</summary>
        public static void ChooseForPanel(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return;
            code = code.Trim().ToUpperInvariant();
            LanguageManager.ChangeLanguage(code);
            ViewPreferences.SetPanel(ViewPreferences.Language, code);
        }

        /// <summary>A language's own name ("Türkçe"), the code when it is not known here.</summary>
        public static string NameOf(string code)
        {
            switch ((code ?? "").Trim().ToUpperInvariant())
            {
                case "EN": return "English";
                case "TR": return "Türkçe";
                case "RU": return "Русский";
                case "DE": return "Deutsch";
                case "IT": return "Italiano";
                case "ES": return "Español";
                case "FR": return "Français";
                case "PL": return "Polski";
                case "PT": return "Português";
                case "NL": return "Nederlands";
                case "CS": return "Čeština";
                case "RO": return "Română";
                case "BG": return "Български";
                case "AR": return "العربية";
                case "ZH": return "中文";
                default: return (code ?? "").Trim().ToUpperInvariant();
            }
        }
    }
}
