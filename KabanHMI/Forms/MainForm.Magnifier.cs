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
            AuditLogger.Log("signed in again after the restart (level: " + account.Level + ").");
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
                "Your screen scale is {0}; the screen is at {1}.\r\n\r\n" +
                "Restart the HMI with {0} now? The machine keeps running, the screen is back in a few seconds, and you stay signed in."),
                DisplaySettings.Percent(mine.Value), DisplaySettings.Percent(UiScale.Factor));
            if (MessageBox.Show(this, question, LanguageManager.GetString("Ttl_Magnifier", "Screen scale"),
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                AuditLogger.Log("kept the magnifying factor " + DisplaySettings.Percent(UiScale.Factor) + " (own: " + DisplaySettings.Percent(mine.Value) + ").");
                return;
            }
            RestartWithFactor(mine.Value, keepSignedIn: true);
        }

        // ==================================================================
        // THE MAGNIFIER ON THE CAPTION BAR
        // ------------------------------------------------------------------
        // A glass and the size on screen ("150%"), between the user and the
        // language: the size of the screen is a thing an operator changes for
        // their own eyes, so it sits with the other two things about "me" - who
        // I am and my language - not three levels down in HMI Settings.
        //
        // As simple as the language menu: "Screen scale (100%)" in the heading,
        // the usual sizes, and a row to set it to the percent (- 137% + OK) - 10%
        // at a time moved a page by more than an operator wanted.
        //
        // IT IS THE PANEL'S SCALE, whoever is signed in. A user's own request is
        // a PARAMETER of that user, set on User Settings (the same menu, with "No
        // specific request"), and only there: choosing here used to overwrite the
        // signed-in user's parameter, so User Settings showed whatever was last
        // chosen on the caption bar instead of what the user had asked for. The
        // size is applied at start-up, so a change offers the restart (the
        // machine keeps running, the user stays signed in and is not asked about
        // their own scale on the way back).
        // ==================================================================

        private Button _btnZoom;

        /// <summary>The sizes offered in the list - 80% to 150%; any percent between is set on the row under it.</summary>
        internal static readonly double[] FactorChoices = { 0.80, 0.90, 1.00, 1.10, 1.20, 1.25, 1.30, 1.40, 1.50 };

        private void SetupZoomButton()
        {
            _btnZoom = new Button
            {
                Width = 88,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.Black,
                Text = "",
                Cursor = Cursors.Hand,
                TabStop = false,
                AccessibleName = "Screen scale"
            };
            _btnZoom.FlatAppearance.BorderSize = 0;
            _btnZoom.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(48, 48, 48);
            _btnZoom.Paint += (s, e) =>
            {
                Func<int, int> px = UiScale.Px;
                var glass = new System.Drawing.Rectangle(px(6), (_btnZoom.Height - px(20)) / 2, px(20), px(20));
                DrawMagnifierGlyph(e.Graphics, glass, System.Drawing.Color.White);
                TextRenderer.DrawText(e.Graphics, DisplaySettings.Percent(UiScale.Factor), _btnZoom.Font,
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
            Kaban.UI.Controls.UiStyle.SetTip(_topTips, _btnZoom, LanguageManager.GetString("Tip_Zoom",
                "Screen scale: how large the screen is drawn (80% - 150%)"));
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
                // A "+" in the lens (2 October): the screen scale, not the Find glass.
                float cx = x + 7.5f * s, cy = y + 7.5f * s, arm = 2.8f * s;
                using (var plus = new System.Drawing.Pen(colour, 1.8f * s) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round })
                {
                    g.DrawLine(plus, cx - arm, cy, cx + arm, cy);
                    g.DrawLine(plus, cx, cy - arm, cx, cy + arm);
                }
            }
            finally { g.Restore(state); }
        }

        /// <summary>The size the menu marks: the one the program starts with (normally the one on screen).</summary>
        private static double ChosenFactor()
        {
            return DisplaySettings.StartFactor;
        }

        /// <summary>The menu under the glass.</summary>
        internal ContextMenuStrip BuildZoomMenu()
        {
            return BuildScaleMenu(LanguageManager.GetString("Ttl_Magnifier", "Screen scale") + " (" + DisplaySettings.Percent(UiScale.Factor) + ")",
                                  ChosenFactor(), f => { if (f.HasValue) ChooseFactor(f.Value); });
        }

        /// <summary>
        /// THE SCALE MENU under the caption bar's glass: its heading, and the screen scale
        /// set as the speed is (16 October: "change the Screen Scale setting dropdown menu
        /// to our new Speed setting control, it looks much neater") - the slider through the
        /// usual sizes, the percentage for any size between them. Let go (or the keypad's
        /// OK): the menu closes and the size is chosen.
        /// </summary>
        internal static ContextMenuStrip BuildScaleMenu(string heading, double? chosen, Action<double?> choose)
        {
            var menu = new ContextMenuStrip { ShowImageMargin = false, Font = UiStyle.MenuFont };
            menu.Renderer = new HmiMenuRenderer();

            if (!string.IsNullOrEmpty(heading))
            {
                menu.Items.Add(new ToolStripLabel(heading)
                {
                    Font = MenuHeaderFont(),
                    ForeColor = System.Drawing.Color.FromArgb(40, 46, 56),
                    Margin = new Padding(UiScale.Px(4), 2, 2, 2)
                });
                menu.Items.Add(new ToolStripSeparator());
            }
            var slider = new Kaban.UI.Controls.ScaleSlider { Value = chosen ?? UiScale.Factor, Name = "scaleSlider" };
            slider.Chosen += (s, f) => { menu.Close(); choose(f); };
            menu.Items.Add(new ToolStripControlHost(slider)
            {
                AutoSize = false,
                Size = new System.Drawing.Size(UiScale.Px(300), UiScale.Px(34)),
                Margin = new Padding(UiScale.Px(6), UiScale.Px(4), UiScale.Px(6), UiScale.Px(6))
            });
            return menu;
        }

        /// <summary>"No specific request": a user who asks for no scale of their own gets the panel's.</summary>
        internal static string NoScaleRequest
        {
            get { return LanguageManager.GetString("Usr_NoScaleRequest", "No specific request"); }
        }

        private void ShowZoomMenu()
        {
            UiStyle.ShowPopup(BuildZoomMenu(), _btnZoom, new System.Drawing.Point(0, _btnZoom.Height));
        }

        /// <summary>
        /// Keeps a size chosen under the glass as the PANEL'S screen scale - the
        /// one the program starts with, and everyone's who asks for none of their
        /// own - and offers the restart that applies it. A user's own request is
        /// not touched (see above).
        /// </summary>
        public void ChooseFactor(double factor)
        {
            factor = DisplaySettings.Clamp(factor);
            DisplaySettings.PanelFactor = factor;
            AuditLogger.Log("screen scale of the panel: " + DisplaySettings.Percent(factor) + ".");
            // The next start is at this size: a power cut brings it back.
            DisplaySettings.StartFactor = factor;
            if (_btnZoom != null) _btnZoom.Invalidate();
            if (Math.Abs(factor - UiScale.Factor) < 0.001) return;

            string question = string.Format(LanguageManager.GetString("Msg_FactorRestartNow",
                "Restart the HMI now with the screen scale {0}? The machine keeps running, the screen is back in a few seconds, and you stay signed in."),
                DisplaySettings.Percent(factor));
            Func<string, bool> ask = ConfirmZoomRestart ?? (q =>
                MessageBox.Show(this, q, LanguageManager.GetString("Ttl_Magnifier", "Screen scale"),
                                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
            if (ask(question)) RestartWithFactor(factor, keepSignedIn: true);
        }

        /// <summary>Asks before the restart; a test answers instead of the operator.</summary>
        internal Func<string, bool> ConfirmZoomRestart;

        /// <summary>Restarts with this factor - with the user signed in again when asked.</summary>
        public void RestartWithFactor(double factor, bool keepSignedIn)
        {
            DisplaySettings.StartFactor = factor;
            AuditLogger.Log("restart for the screen scale " + DisplaySettings.Percent(factor) + ".");
            // The sign-in is handed over by every restart (RestartApplication).
            RestartApplication(null);
        }
    }
}
