using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Forms
{
    /// <summary>
    /// THE CAPTION BAR'S CHAIN (17 October: "add a clutching button on top of the page,
    /// blinking just like the alarm button but in blue, when any clutching button is
    /// marked - a dropdown of the clutched controls, each to cancel it, and the last
    /// entry to cancel all of them"). Shown only while units are coupled, beside the
    /// alarm mark; its chain blinks blue (steady with View Preferences' blinking off).
    /// </summary>
    public partial class MainForm
    {
        private Button _btnCoupled;
        private bool _coupledPhase = true;
        private Timer _coupledTimer;

        /// <summary>The caption bar's chain (tests).</summary>
        internal Button CouplingButton { get { return _btnCoupled; } }

        private void SetupCouplingIndicator()
        {
            _btnCoupled = new Button
            {
                Text = "",
                Width = 40,
                Height = 27,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Visible = false,
                Name = "btnCoupled"
            };
            _btnCoupled.FlatAppearance.BorderSize = 0;
            _btnCoupled.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 70, 85);
            UiStyle.Round(_btnCoupled);
            _btnCoupled.Paint += (s, e) =>
            {
                var r = _btnCoupled.ClientRectangle;
                // the bar's own colour, the chain blinking - as the alarm's mark does
                using (var back = new SolidBrush(pnlTop.BackColor)) e.Graphics.FillRectangle(back, r);
                float side = Math.Min(r.Width, r.Height) * 0.9f;
                var area = new RectangleF(r.Left + (r.Width - side) / 2f, r.Top + (r.Height - side) / 2f, side, side);
                CoupleBadge.DrawChain(e.Graphics, area, _coupledPhase ? Color.FromArgb(64, 156, 255) : Color.White, true);
            };
            _btnCoupled.Click += (s, e) => UiStyle.ShowPopup(BuildCouplingMenu(), _btnCoupled, new Point(0, _btnCoupled.Height));
            UiStyle.SetTip(_topTips, _btnCoupled, LanguageManager.GetString("Ttl_Coupled", "Coupled units"));
            pnlTop.Controls.Add(_btnCoupled);

            _coupledTimer = new Timer { Interval = 300 };
            _coupledTimer.Tick += (s, e) =>
            {
                _coupledPhase = !ViewPreferences.BlinkingOn || !_coupledPhase;
                if (_btnCoupled.Visible) _btnCoupled.Invalidate();
            };
            UnitCoupling.Changed += OnCouplingChanged;
            Disposed += (s, e) => UnitCoupling.Changed -= OnCouplingChanged;
            // the pages they work on (17 October)
            UnitCoupling.PagesShown = CouplingPagesShown;
            UnitCycling.ManualPageShown = () => _currentScreen != null && UnitCycling.ShowsManualPage(_currentScreen);
            UpdateCouplingIndicator();
        }

        private void OnCouplingChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { if (IsHandleCreated) BeginInvoke(new Action(UpdateCouplingIndicator)); }
            else UpdateCouplingIndicator();
        }

        /// <summary>Shown while units are coupled, its chain blinking; hidden otherwise.</summary>
        private void UpdateCouplingIndicator()
        {
            if (_btnCoupled == null) return;
            bool any = UnitCoupling.All.Count > 0;
            if (any) { if (!_coupledTimer.Enabled) _coupledTimer.Start(); }
            else { _coupledTimer.Stop(); _coupledPhase = true; }
            if (_btnCoupled.Visible == any) return;
            _btnCoupled.Visible = any;
            LayoutTopBar();
        }

        /// <summary>
        /// A page coupling works on is shown: Manual Movement; the Axis page (Machine
        /// Parameters' page of axis rows); the Axis Monitor beside the page or in its window.
        /// </summary>
        internal bool CouplingPagesShown()
        {
            if (IsDisposed) return false;
            if (_currentScreen != null && UnitCycling.ShowsManualPage(_currentScreen)) return true;
            if (_currentScreen != null && HasAxisRows(_currentScreen)) return true;
            var side = OpenSidePage;
            if (side != null && SidePageView(side.Key) is Kaban.UI.Screens.AxisMonitorPage) return true;
            foreach (var w in _sideWindows.Values)
                if (w != null && !w.IsDisposed && w.Visible && w.View is Kaban.UI.Screens.AxisMonitorPage) return true;
            return false;
        }

        /// <summary>An axis row (not a monitor's) in sight on the page: the Axis page is shown.</summary>
        private static bool HasAxisRows(Control c)
        {
            foreach (Control k in c.Controls)
            {
                if (!k.Visible) continue;                      // another tab's page: not shown
                if (k is AxisSettingHMI) { if (!((AxisSettingHMI)k).MonitorMode) return true; continue; }
                if (HasAxisRows(k)) return true;
            }
            return false;
        }

        /// <summary>The coupled units, each to uncouple, and - last - all of them.</summary>
        internal ContextMenuStrip BuildCouplingMenu()
        {
            var menu = UiStyle.Menu();
            menu.Items.Add(UiStyle.MenuHeader(LanguageManager.GetString("Ttl_Coupled", "Coupled units")));
            menu.Items.Add(new ToolStripSeparator());
            var units = UnitCoupling.All;
            foreach (var kind in units.Select(u => u.Kind).Distinct())
                foreach (var u in units.Where(x => x.Kind == kind))
                {
                    var unit = u;
                    menu.Items.Add(new ToolStripMenuItem(string.Format(LanguageManager.GetString("Cpl_UncoupleThis", "Uncouple '{0}'"), unit.CouplingName), null,
                                                         (s, e) => UnitCoupling.Remove(unit, null)) { Name = "uncouple:" + unit.CouplingName });
                }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem(string.Format(LanguageManager.GetString("Cpl_UncoupleAll", "Uncouple all {0}"), units.Count), null,
                                                 (s, e) => UnitCoupling.LetGoAll()) { Name = "uncoupleAll" });
            return menu;
        }
    }
}
