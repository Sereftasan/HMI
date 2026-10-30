using System;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Forms
{
    // ======================================================================
    // MOUSE OR TOUCH, ON THE CAPTION BAR
    // ----------------------------------------------------------------------
    // 29 October: "add an emoji dropdown button on top of the screen to switch
    // in between mouse and touch screen mode and add it to the view preferences,
    // just like the screen scale". The way the screen is used now - 🖱 or 👆 -
    // beside the screen scale; its menu switches it. The choice is the signed-in
    // user's own when they keep one (Users and Preferences > Screen), else the
    // panel's: choosing here always changes the screen in front of the operator.
    // Taken at once - nothing to restart.
    // ======================================================================
    public partial class MainForm
    {
        private Button _btnInput;

        private void SetupInputButton()
        {
            _btnInput = new Button
            {
                Name = "btnInputMode",
                Width = 52,
                Height = 26,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.Black,
                Text = "",
                Cursor = Cursors.Hand,
                TabStop = false,
                AccessibleName = "Screen used with"
            };
            _btnInput.FlatAppearance.BorderSize = 0;
            _btnInput.FlatAppearance.MouseOverBackColor = Color.FromArgb(48, 48, 48);
            _btnInput.Paint += (s, e) =>
            {
                Func<int, int> px = UiScale.Px;
                // a white line drawing, as the Help and screen scale buttons' (30 October: not a filled picture)
                int side = Math.Min(px(22), _btnInput.Height - px(2));
                var pic = new Rectangle(px(6), (_btnInput.Height - side) / 2, side, side);
                MenuGlyphs.DrawInput(e.Graphics, pic, Color.White, TouchInput.On);
                using (var caret = new SolidBrush(Color.White))
                    e.Graphics.FillPolygon(caret, new[]
                    {
                        new PointF(_btnInput.Width - px(14), _btnInput.Height / 2f - px(2)),
                        new PointF(_btnInput.Width - px(6),  _btnInput.Height / 2f - px(2)),
                        new PointF(_btnInput.Width - px(10), _btnInput.Height / 2f + px(3))
                    });
            };
            _btnInput.Click += (s, e) => ShowInputMenu();
            UiStyle.HoverTip(_topTips, _btnInput, InputTip);
            pnlTop.Controls.Add(_btnInput);
            TouchInput.ModeChanged += InputModeChanged;
            Disposed += (s, e) => TouchInput.ModeChanged -= InputModeChanged;
            // only while the touch screen is switched on (HMI Settings > Features, 30 October)
            _btnInput.Visible = HmiFeatures.TouchOn;
            EventHandler features = (s, e) =>
            {
                try { if (IsHandleCreated && InvokeRequired) BeginInvoke((Action)ShowInputButton); else ShowInputButton(); } catch { }
            };
            HmiFeatures.Changed += features;
            Disposed += (s, e) => HmiFeatures.Changed -= features;
        }

        private void InputModeChanged(object sender, EventArgs e)
        {
            if (_btnInput != null && !_btnInput.IsDisposed) _btnInput.Invalidate();
        }

        private static string InputTip()
        {
            return (TouchInput.On ? LanguageManager.GetString("Tip_InputTouch", "Used by touch: larger menus, rows and buttons, a long press for a menu, a finger drag scrolls, a keyboard on the screen")
                                  : LanguageManager.GetString("Tip_InputMouse", "Used with a mouse")) +
                   "\n" + LanguageManager.GetString("Tip_InputSwitch", "Click to switch between a mouse and a touch screen");
        }

        /// <summary>The menu under the button: the two ways, the one in force ticked.</summary>
        public ContextMenuStrip BuildInputMenu()
        {
            var menu = UiStyle.Menu();
            menu.Name = "inputModeMenu";
            menu.Items.Add(new ToolStripLabel(LanguageManager.GetString("VP_InputMode", "Screen used with"))
            {
                Font = MenuHeaderFont(),
                ForeColor = Color.FromArgb(40, 46, 56),
                Margin = new Padding(-34, 2, 2, 2)
            });
            menu.Items.Add(new ToolStripSeparator());
            var mouse = new ToolStripMenuItem(LanguageManager.GetString("VP_InputMouse", "A mouse"), MenuGlyphs.Mouse())
            {
                Name = "inputMouse", Checked = !TouchInput.On
            };
            mouse.Click += (s, e) => { if (TouchInput.On) TouchInput.Choose(false); };
            var touch = new ToolStripMenuItem(LanguageManager.GetString("VP_InputTouch", "Touch (a finger)"), MenuGlyphs.Touch())
            {
                Name = "inputTouch", Checked = TouchInput.On
            };
            touch.Click += (s, e) => { if (!TouchInput.On) TouchInput.Choose(true); };
            menu.Items.Add(mouse);
            menu.Items.Add(touch);
            // whose choice it is: the user's own, or everyone's on this panel
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripLabel(TouchInput.ChoiceIsOwn
                ? string.Format(LanguageManager.GetString("Inp_OwnNote", "Kept as {0}'s own (Users and Preferences)"), ViewPreferences.ActiveUser)
                : LanguageManager.GetString("Inp_PanelNote", "Kept for everyone on this panel"))
            {
                Name = "inputWhose", ForeColor = Color.FromArgb(110, 116, 124), Font = UiStyle.Small
            });
            return menu;
        }

        private void ShowInputMenu()
        {
            UiStyle.ShowPopup(BuildInputMenu(), _btnInput, new Point(0, _btnInput.Height));
        }
    }
}
