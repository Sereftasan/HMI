using System;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Forms
{
    // ======================================================================
    // THE MAGNIFYING FACTOR AT SIGN-IN
    // ----------------------------------------------------------------------
    // A user may have a factor of their own (HMI Settings > User Settings).
    // When they sign in and the screen is at another one, the HMI OFFERS to
    // restart with theirs - never restarts by itself: the operator may be in
    // the middle of something. After that restart they are signed in again
    // (LoginHandoff) and find the screen at their size. The program starts
    // with the factor it last restarted for, so the same operator after a
    // power cut needs no second restart.
    // ======================================================================
    public partial class MainForm
    {
        /// <summary>Signs in the user this program was restarted for, when it was; false otherwise.</summary>
        private bool SignInFromHandoff()
        {
            string user;
            if (!LoginHandoff.TryConsume(Environment.GetCommandLineArgs(), out user)) return false;
            var account = System.Linq.Enumerable.FirstOrDefault(UserStore.GetAllUsers(),
                              u => string.Equals(u.Username, user, StringComparison.OrdinalIgnoreCase));
            if (account == null) { AuditLogger.Log("[RESTART] sign-in hand-over for '" + user + "': no such account now."); return false; }
            SecurityManager.Login(account.Username, account.Level);
            UserStore.RememberLastUser(account.Username);
            AuditLogger.Log("signed in again after the restart for the magnifying factor (level: " + account.Level + ").");
            return true;
        }

        /// <summary>
        /// The factor to offer the user signed in: theirs, when they have one and
        /// the screen is not at it; null otherwise.
        /// </summary>
        public static double? MagnifierToOffer()
        {
            if (SecurityManager.CurrentLevel == SecurityLevel.None) return null;
            var mine = DisplaySettings.UserFactor(SecurityManager.CurrentUser);
            return mine.HasValue && Math.Abs(mine.Value - UiScale.Factor) >= 0.001 ? mine : null;
        }

        /// <summary>
        /// After a sign-in: when the user has a factor of their own and the screen
        /// is not at it, asks whether to restart with it.
        /// </summary>
        private void OfferUserMagnifier()
        {
            var mine = MagnifierToOffer();
            if (!mine.HasValue) return;

            string question = string.Format(LanguageManager.GetString("Msg_MagnifierRestart",
                "Your magnifying factor is {0}; the screen is at {1}.\r\n\r\n" +
                "Restart the HMI with {0} now? The machine keeps running, the screen is back in a few seconds, and you stay signed in."),
                DisplaySettings.Format(mine.Value), DisplaySettings.Format(UiScale.Factor));
            if (MessageBox.Show(this, question, LanguageManager.GetString("Ttl_Magnifier", "Magnifying factor"),
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                AuditLogger.Log("kept the magnifying factor " + DisplaySettings.Format(UiScale.Factor) + " (own: " + DisplaySettings.Format(mine.Value) + ").");
                return;
            }
            RestartWithFactor(mine.Value, keepSignedIn: true);
        }

        /// <summary>Restarts with this factor - with the user signed in again when asked.</summary>
        public void RestartWithFactor(double factor, bool keepSignedIn)
        {
            DisplaySettings.StartFactor = factor;
            string args = null;
            if (keepSignedIn && SecurityManager.CurrentLevel != SecurityLevel.None)
            {
                try { args = LoginHandoff.Switch + " " + LoginHandoff.Write(SecurityManager.CurrentUser); }
                catch (Exception ex) { AuditLogger.Log("[RESTART] sign-in hand-over not written (" + ex.Message + "): sign in again after the restart."); }
            }
            AuditLogger.Log("restart for the magnifying factor " + DisplaySettings.Format(factor) + ".");
            RestartApplication(args);
        }
    }
}
