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

        // ==================================================================
        // THE MAGNIFIER ON THE CAPTION BAR
        // ------------------------------------------------------------------
        // A glass and the factor on screen, between the user and the language:
        // the size of the screen is a thing an operator changes for their own
        // eyes, so it sits with the other two things about "me" - who I am and
        // my language - not three levels down in HMI Settings.
        //
        // Signed in, a factor chosen is the user's OWN (DisplaySettings.User):
        // they get it at every sign-in. "The panel's" drops their own. An Admin
        // also sets the factor for everyone (the panel's), in its own submenu.
        // Nobody signed in, it is the factor this panel starts with until
        // someone with their own signs in. The factor is applied at start-up,
        // so a change offers the restart (the machine keeps running).
        // ==================================================================

        private Button _btnZoom;

        /// <summary>The factors offered - 0.8 to 1.5.</summary>
        internal static readonly double[] FactorChoices = { 0.80, 0.90, 1.00, 1.10, 1.20, 1.25, 1.30, 1.40, 1.50 };

        private void SetupZoomButton()
        {
            _btnZoom = new Button
            {
                Width = 84,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.Black,
                Text = "",
                Cursor = Cursors.Hand,
                TabStop = false,
                AccessibleName = "Magnifying factor"
            };
            _btnZoom.FlatAppearance.BorderSize = 0;
            _btnZoom.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(48, 48, 48);
            _btnZoom.Paint += (s, e) =>
            {
                Func<int, int> px = UiScale.Px;
                var glass = new System.Drawing.Rectangle(px(6), (_btnZoom.Height - px(20)) / 2, px(20), px(20));
                DrawMagnifierGlyph(e.Graphics, glass, System.Drawing.Color.White);
                TextRenderer.DrawText(e.Graphics, DisplaySettings.Format(UiScale.Factor), _btnZoom.Font,
                    new System.Drawing.Rectangle(glass.Right + px(4), 0, _btnZoom.Width - glass.Right - px(18), _btnZoom.Height),
                    System.Drawing.Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);
                using (var caret = new System.Drawing.SolidBrush(System.Drawing.Color.White))
                    e.Graphics.FillPolygon(caret, new[]
                    {
                        new System.Drawing.PointF(_btnZoom.Width - px(14), _btnZoom.Height / 2f - px(2)),
                        new System.Drawing.PointF(_btnZoom.Width - px(6),  _btnZoom.Height / 2f - px(2)),
                        new System.Drawing.PointF(_btnZoom.Width - px(10), _btnZoom.Height / 2f + px(3))
                    });
            };
            _btnZoom.Click += (s, e) => ShowZoomMenu();
            _topTips.SetToolTip(_btnZoom, LanguageManager.GetString("Tip_Zoom",
                "Magnifying factor: how large the screen is drawn (0.8 - 1.5)"));
            pnlTop.Controls.Add(_btnZoom);
        }

        /// <summary>A magnifying glass: the lens, and its handle down to the right.</summary>
        internal static void DrawMagnifierGlyph(System.Drawing.Graphics g, System.Drawing.Rectangle r, System.Drawing.Color colour)
        {
            var state = g.Save();
            try
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                float s = Math.Min(r.Width, r.Height) / 20f;
                float x = r.X + (r.Width - 20 * s) / 2f, y = r.Y + (r.Height - 20 * s) / 2f;
                using (var lens = new System.Drawing.Pen(colour, 2.2f * s))
                    g.DrawEllipse(lens, x + 2 * s, y + 2 * s, 11 * s, 11 * s);
                using (var handle = new System.Drawing.Pen(colour, 3.2f * s) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                    g.DrawLine(handle, x + 12 * s, y + 12 * s, x + 17.5f * s, y + 17.5f * s);
            }
            finally { g.Restore(state); }
        }

        /// <summary>The menu under the glass - built for the user signed in now.</summary>
        internal ContextMenuStrip BuildZoomMenu()
        {
            var menu = new ContextMenuStrip { ShowImageMargin = true, Font = UiStyle.MenuFont };
            menu.Renderer = new HmiMenuRenderer();
            menu.ImageScalingSize = new System.Drawing.Size(22, 22);
            bool signedIn = SecurityManager.CurrentLevel != SecurityLevel.None;
            string user = SecurityManager.CurrentUser;

            menu.Items.Add(new ToolStripLabel(LanguageManager.GetString("Ttl_Magnifier", "Magnifying factor"))
            {
                Font = MenuHeaderFont(),
                ForeColor = System.Drawing.Color.FromArgb(40, 46, 56),
                Margin = new Padding(-34, 2, 2, 2)
            });
            menu.Items.Add(new ToolStripLabel(signedIn
                ? string.Format(LanguageManager.GetString("Zoom_ForUser", "Your own, whenever you sign in ({0})"), user)
                : LanguageManager.GetString("Zoom_ForStart", "This panel, until someone with their own signs in"))
            {
                ForeColor = System.Drawing.Color.FromArgb(96, 102, 112),
                Margin = new Padding(-34, 0, 2, 2)
            });
            menu.Items.Add(new ToolStripSeparator());

            double? own = signedIn ? DisplaySettings.UserFactor(user) : null;
            foreach (var f in FactorChoices)
            {
                double factor = f;
                var item = new ToolStripMenuItem(DisplaySettings.Format(factor))
                {
                    Checked = Math.Abs(factor - (own ?? UiScale.Factor)) < 0.001,
                    Tag = factor
                };
                item.Click += (s, e) => ChooseFactor(factor, forEveryone: false);
                menu.Items.Add(item);
            }
            if (signedIn && own.HasValue)
            {
                var panel = new ToolStripMenuItem(string.Format(LanguageManager.GetString("Zoom_PanelOne", "The panel's ({0})"),
                                                                DisplaySettings.Format(DisplaySettings.PanelFactor)));
                panel.Click += (s, e) => ChooseFactor(null, forEveryone: false);
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(panel);
            }
            if (SecurityManager.HasAccess(SecurityLevel.Admin))
            {
                var everyone = new ToolStripMenuItem(LanguageManager.GetString("Zoom_ForEveryone", "For everyone on this panel"));
                foreach (var f in FactorChoices)
                {
                    double factor = f;
                    var sub = new ToolStripMenuItem(DisplaySettings.Format(factor))
                    {
                        Checked = Math.Abs(factor - DisplaySettings.PanelFactor) < 0.001,
                        Tag = factor
                    };
                    sub.Click += (s, e) => ChooseFactor(factor, forEveryone: true);
                    everyone.DropDownItems.Add(sub);
                }
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(everyone);
            }
            return menu;
        }

        private void ShowZoomMenu()
        {
            UiStyle.ShowPopup(BuildZoomMenu(), _btnZoom, new System.Drawing.Point(0, _btnZoom.Height));
        }

        /// <summary>
        /// Keeps a factor chosen under the glass and offers the restart that
        /// applies it. <paramref name="factor"/> null: the user's own is dropped
        /// (they get the panel's).
        /// </summary>
        public void ChooseFactor(double? factor, bool forEveryone)
        {
            bool signedIn = SecurityManager.CurrentLevel != SecurityLevel.None;
            string user = SecurityManager.CurrentUser;
            double wanted;
            if (forEveryone && factor.HasValue)
            {
                DisplaySettings.PanelFactor = factor.Value;
                AuditLogger.Log("magnifying factor of the panel: " + DisplaySettings.Format(factor.Value) + ".");
                // The screen follows only when the user has none of their own.
                var mine = signedIn ? DisplaySettings.UserFactor(user) : null;
                wanted = mine ?? factor.Value;
            }
            else if (signedIn)
            {
                DisplaySettings.SetUserFactor(user, factor);
                AuditLogger.Log("magnifying factor of user '" + user + "': " +
                                (factor.HasValue ? DisplaySettings.Format(factor.Value) : "the panel's") + ".");
                wanted = factor ?? DisplaySettings.PanelFactor;
            }
            else
            {
                wanted = factor ?? DisplaySettings.PanelFactor;
                AuditLogger.Log("magnifying factor of this panel's start: " + DisplaySettings.Format(wanted) + ".");
            }
            // The next start is at this factor either way: a power cut brings it back.
            DisplaySettings.StartFactor = wanted;
            if (_btnZoom != null) _btnZoom.Invalidate();
            if (Math.Abs(wanted - UiScale.Factor) < 0.001) return;

            string question = string.Format(LanguageManager.GetString("Msg_FactorRestartNow",
                "Restart the HMI now with the magnifying factor {0}? The machine keeps running, the screen is back in a few seconds, and you stay signed in."),
                DisplaySettings.Format(wanted));
            Func<string, bool> ask = ConfirmZoomRestart ?? (q =>
                MessageBox.Show(this, q, LanguageManager.GetString("Ttl_Magnifier", "Magnifying factor"),
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
            if (ask(question)) RestartWithFactor(wanted, keepSignedIn: true);
        }

        /// <summary>Asks before the restart; a test answers instead of the operator.</summary>
        internal Func<string, bool> ConfirmZoomRestart;

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
