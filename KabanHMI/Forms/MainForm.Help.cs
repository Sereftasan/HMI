using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// THE CAPTION BAR'S "?" (9 October: "a dropdown menu between the language and
    /// the screen scale for the HMI helper ... and move HMI Settings / About here,
    /// because it is not a setting thing at all and the user's eyes don't catch it
    /// up there"):
    ///   About Kaban HMI         - the program, its version, its machines (a message box)
    ///   Open the HMI manual     - in the browser, with this panel's machines
    ///   Create AI prompt        - the HMI explained and its manual, for any AI chat
    ///   Ask Claude              - the same, that AI chat opened
    ///   Ask ChatGPT ...         - every chat of the panel's list, in its order
    /// F1 opens it. The chats are an Admin's list: HMI Settings > AI. Its pictures
    /// are the user menu's kind (12 October): black line pictures in the picture
    /// column, not emojis in the words (MenuGlyphs).
    /// </summary>
    public partial class MainForm
    {
        private Button _btnHelp;

        private void SetupHelpButton()
        {
            _btnHelp = new Button
            {
                Width = 52,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.Black,
                Text = "",
                Cursor = Cursors.Hand,
                TabStop = false,
                Name = "btnHelp",
                AccessibleName = "Help"
            };
            _btnHelp.FlatAppearance.BorderSize = 0;
            _btnHelp.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 48, 48);
            _btnHelp.Paint += (s, e) =>
            {
                Func<int, int> px = UiScale.Px;
                var mark = new Rectangle(px(6), (_btnHelp.Height - px(20)) / 2, px(20), px(20));
                DrawHelpGlyph(e.Graphics, mark, Color.White);
                using (var caret = new SolidBrush(Color.White))
                    e.Graphics.FillPolygon(caret, new[]
                    {
                        new PointF(_btnHelp.Width - px(14), _btnHelp.Height / 2f - px(2)),
                        new PointF(_btnHelp.Width - px(6),  _btnHelp.Height / 2f - px(2)),
                        new PointF(_btnHelp.Width - px(10), _btnHelp.Height / 2f + px(3))
                    });
            };
            _btnHelp.Click += (s, e) => ShowHelpMenu();
            Action tip = () => Helpers.Shortcuts.Set(_btnHelp, Keys.F1, _topTips, LanguageManager.GetString("Tip_Help",
                "Help: about this HMI, its manual, and an AI to ask about it"));
            tip();
            EventHandler onLanguage = (s, e) => { if (!_btnHelp.IsDisposed) tip(); };
            LanguageManager.LanguageChanged += onLanguage;
            _btnHelp.Disposed += (s, e) => LanguageManager.LanguageChanged -= onLanguage;
            pnlTop.Controls.Add(_btnHelp);
        }

        /// <summary>A question mark in a ring.</summary>
        internal static void DrawHelpGlyph(Graphics g, Rectangle r, Color colour)
        {
            var state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                float s = Math.Min(r.Width, r.Height) / 20f;
                float x = r.X + (r.Width - 20 * s) / 2f, y = r.Y + (r.Height - 20 * s) / 2f;
                using (var ring = new Pen(colour, 1.8f * s)) g.DrawEllipse(ring, x + 1 * s, y + 1 * s, 18 * s, 18 * s);
                using (var pen = new Pen(colour, 2.2f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    // the hook of the "?"
                    g.DrawArc(pen, x + 6.6f * s, y + 4.2f * s, 6.8f * s, 6.4f * s, 180, 225);
                    g.DrawLine(pen, x + 10f * s, y + 10.6f * s, x + 10f * s, y + 12.2f * s);
                }
                using (var dot = new SolidBrush(colour)) g.FillEllipse(dot, x + 8.7f * s, y + 14.2f * s, 2.6f * s, 2.6f * s);
            }
            finally { g.Restore(state); }
        }

        /// <summary>Where the user is, for the HMI's AI prompt.</summary>
        public HmiAiPrompt.Context HelpContext()
        {
            var ctx = new HmiAiPrompt.Context();
            try { ctx.Place = CurrentPlace(); } catch { }
            // what the page on show shows now (14 October: a briefing the AI can talk from)
            try { ctx.OnScreen = HmiAiPrompt.DescribePage(CurrentView()); } catch { }
            if (_sidePanel != null && _sidePanel.Visible && _sideOpen != null) ctx.SidePage = _sideOpen.Title;
            ctx.Scale = DisplaySettings.Percent(UiScale.Factor);
            return ctx;
        }

        /// <summary>The menu under the "?" (tests open it themselves).</summary>
        public ContextMenuStrip BuildHelpMenu()
        {
            var menu = new ContextMenuStrip { ShowImageMargin = true, Font = UiStyle.MenuFont };
            menu.Renderer = new HmiMenuRenderer();
            menu.ImageScalingSize = UiScale.Px(new Size(22, 22));
            menu.ShowItemToolTips = true;
            menu.Items.Add(new ToolStripLabel(LanguageManager.GetString("Ttl_Help", "Help"))
            {
                Font = MenuHeaderFont(),
                ForeColor = Color.FromArgb(40, 46, 56),
                Margin = new Padding(-34, 2, 2, 2)
            });
            menu.Items.Add(new ToolStripSeparator());

            var about = new ToolStripMenuItem(LanguageManager.GetString("Hlp_About", "About Kaban HMI"), MenuGlyphs.Info()) { Name = "helpAbout" };
            about.ToolTipText = LanguageManager.GetString("Tip_HlpAbout", "The program, its version and the machines it runs");
            about.Click += (s, e) => ShowAbout();
            menu.Items.Add(about);

            var manual = new ToolStripMenuItem(LanguageManager.GetString("Btn_OpenManual", "Open the HMI manual"), MenuGlyphs.Book()) { Name = "helpManual" };
            manual.ToolTipText = LanguageManager.GetString("Tip_OpenManual", "Opens the operator and engineer manual in the browser, in the language on screen.");
            manual.Click += (s, e) => OpenManualFromHelp();
            menu.Items.Add(manual);

            menu.Items.Add(new ToolStripSeparator());
            foreach (var item in AiMenu.Items(() => HmiAiPrompt.Build(HelpContext()), AlarmAiPrompt.Subject.Hmi, this))
                menu.Items.Add(item);

            return menu;
        }

        private void ShowHelpMenu()
        {
            Kaban.UI.Controls.UiStyle.ShowPopup(BuildHelpMenu(), _btnHelp, new Point(0, _btnHelp.Height));
        }

        /// <summary>
        /// About: the program, its version, its machines - a plain message box (9
        /// October: "remove the title, Open the HMI manual and Close - just a clean
        /// message box is enough"). The text goes to AboutNotice instead when set (tests).
        /// </summary>
        public void ShowAbout()
        {
            string text = Kaban.UI.Settings.HmiManual.AboutText();
            string title = LanguageManager.GetString("Hlp_About", "About Kaban HMI");
            if (AboutNotice != null) { AboutNotice(title + "\n" + text); return; }
            MessageBox.Show(this, text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>Replaces About's message box (tests).</summary>
        public static Action<string> AboutNotice;

        private void OpenManualFromHelp()
        {
            var why = Kaban.UI.Settings.HmiManual.TryOpenManual();
            if (!string.IsNullOrEmpty(why))
                MessageBox.Show(this, why, LanguageManager.GetString("Ttl_Help", "Help"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
