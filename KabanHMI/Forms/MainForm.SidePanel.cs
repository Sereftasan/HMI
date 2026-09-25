using System;
using System.Collections.Generic;
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
    // ======================================================================
    // THE SIDE PANEL
    // ----------------------------------------------------------------------
    // A bar of icons at the right edge of the window, from under the caption to
    // the bottom. Each opens a SIDE PAGE beside the page on display - the PLC
    // Monitor, Inputs / Outputs, a machine's own monitoring pages - and the page
    // on display NARROWS to make room: nothing is covered, so a unit framed by
    // Find, or a sensor watched while a cylinder is moved, stays in sight.
    //
    //  * the border between the two can be dragged; the width is kept;
    //  * the page's header closes it, or takes it out into a window of its own
    //    (a second screen) and back;
    //  * the bar's top button folds the bar to a thin edge, and back;
    //  * a page stays built while the program runs - closing only hides it, so it
    //    opens again as it was left;
    //  * the bar shows only the pages the user's level may open, and is not
    //    there at all when there are none.
    // ======================================================================
    public partial class MainForm
    {
        private Panel _sideBar, _sideBarList, _sidePanel, _sideHeader, _sideHost;
        private Button _sideBarToggle, _sidePopOut, _sideClose;
        private HeaderTitle _sideTitle;
        private Splitter _sideSplitter;
        private readonly Dictionary<string, Control> _sideViews = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SidePageWindow> _sideWindows = new Dictionary<string, SidePageWindow>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Button> _sideButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _sideWantsWindow = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private SidePage _sideOpen;
        private int _sideWidthLogical = 520;
        private bool _sideBarFolded;
        private readonly ToolTip _sideTips = UiStyle.CreateTips();

        private const int SideBarWidth = 48, SideBarFoldedWidth = 14, SideButtonSize = 40;
        private const string PlcMonitorKey = "PlcMonitor", IoMonitorKey = "IoMonitor";

        /// <summary>The side page open in the panel (null when the panel is closed).</summary>
        public SidePage OpenSidePage { get { return _sidePanel != null && _sidePanel.Visible ? _sideOpen : null; } }

        /// <summary>A side page's view, once built (tests; Find into a side page).</summary>
        public Control SidePageView(string key)
        {
            Control v;
            return _sideViews.TryGetValue(key ?? "", out v) && !v.IsDisposed ? v : null;
        }

        /// <summary>The panel's width, the bar folded or not, the pages kept in a window. Replaceable for tests.</summary>
        internal static string SidePanelPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "SidePanel.cfg");

        /// <summary>The program's own side pages. Machines add theirs through ScreenRoles.SidePage.</summary>
        private void RegisterBuiltInSidePages()
        {
            SidePages.Register(new SidePage
            {
                Key = IoMonitorKey,
                TitleKey = "Ttl_IoMonitor", TitleFallback = "Inputs / Outputs",
                Glyph = DrawIoGlyph,
                Create = () => new Screens.IoMonitorPage(),
                RequiredLevel = SecurityLevel.Maintenance,
                SortOrder = 10
            });
            SidePages.Register(new SidePage
            {
                Key = PlcMonitorKey,
                TitleKey = "Ttl_SideMonitor", TitleFallback = "PLC Monitor",
                Glyph = (g, r, c) => DrawMonitorGlyph(g, r, c),
                Create = () => new SimulationMonitorForm(embedded: true),
                RequiredLevel = SecurityLevel.Admin,
                SortOrder = 1000
            });
        }

        /// <summary>Builds the bar, the panel and the border between it and the page. Called from Load.</summary>
        private void SetupSidePanel()
        {
            RegisterBuiltInSidePages();
            LoadSidePanelState();

            var back = Color.FromArgb(249, 249, 250);
            _sideBar = new Panel { Dock = DockStyle.Right, Width = _sideBarFolded ? SideBarFoldedWidth : SideBarWidth, BackColor = back };
            _sideBar.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(214, 218, 224)))
                    e.Graphics.DrawLine(pen, 0, 0, 0, _sideBar.Height);
            };
            _sideBarToggle = new Button
            {
                Dock = DockStyle.Top, Height = 30, FlatStyle = FlatStyle.Flat, BackColor = back, Text = "",
                Cursor = Cursors.Hand, TabStop = false, AccessibleName = "Side bar"
            };
            _sideBarToggle.FlatAppearance.BorderSize = 0;
            var toggleHover = UiStyle.HoverBorder(_sideBarToggle);
            _sideBarToggle.Paint += (s, e) =>
            {
                DrawChevron(e.Graphics, _sideBarToggle.ClientRectangle, !_sideBarFolded, Color.FromArgb(60, 66, 76));
                if (toggleHover())
                    using (var pen = new Pen(UiStyle.HoverBorderColor, 1.6f))
                        e.Graphics.DrawRectangle(pen, 1, 1, _sideBarToggle.Width - 3, _sideBarToggle.Height - 3);
            };
            _sideBarToggle.Click += (s, e) => SetSideBarFolded(!_sideBarFolded);
            _sideBarList = new Panel { Dock = DockStyle.Fill, BackColor = back };
            _sideBar.Controls.Add(_sideBarList);
            _sideBar.Controls.Add(_sideBarToggle);

            _sidePanel = new Panel { Dock = DockStyle.Right, Width = _sideWidthLogical, BackColor = Color.White, Visible = false };
            _sideHeader = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(238, 240, 243) };
            _sideTitle = new HeaderTitle
            {
                Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0), Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 46, 56)
            };
            _sidePopOut = HeaderButton(DrawPopOutGlyph, "Pop out", T("Tip_SidePopOut", "Open in a window of its own (for a second screen)"),
                                       (s, e) => PopOutSidePage());
            _sideClose = HeaderButton(DrawCloseGlyph, "Close", T("Tip_SideClose", "Close (the page stays as you left it)"),
                                      (s, e) => CloseSidePanel());
            _sideHeader.Controls.Add(_sideTitle);
            _sideHeader.Controls.Add(_sidePopOut);
            _sideHeader.Controls.Add(_sideClose);
            _sideHost = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            _sidePanel.Controls.Add(_sideHost);
            _sidePanel.Controls.Add(_sideHeader);

            _sideSplitter = new Splitter
            {
                Dock = DockStyle.Right, Width = 6, BackColor = Color.FromArgb(214, 218, 224),
                MinSize = 240, MinExtra = 320, Visible = false
            };
            _sideSplitter.SplitterMoved += (s, e) =>
            {
                _sideWidthLogical = UiScale.Logical(_sidePanel.Width);
                SaveSidePanelState();
            };

            // Dock order: the caption first (full width), then the bar, the panel
            // and its border - each placed just under the caption in the order
            // WinForms docks (the higher the index, the earlier).
            foreach (var c in new Control[] { _sideBar, _sidePanel, _sideSplitter })
            {
                int under = Controls.GetChildIndex(c == _sideBar ? pnlTop : c == _sidePanel ? (Control)_sideBar : _sidePanel);
                Controls.Add(c);
                Controls.SetChildIndex(c, under);
            }

            // Values read from a page's controls in the panel, like the page's own.
            _scanner.AddTarget(_sideHost);

            SecurityManager.UserChanged += (s, e) => { if (!IsDisposed) RefreshSideBar(); };
            RefreshSideBar();
        }

        private Button HeaderButton(Action<Graphics, Rectangle, Color> glyph, string name, string tip, EventHandler click)
        {
            var b = new Button
            {
                Dock = DockStyle.Right, Width = 34, FlatStyle = FlatStyle.Flat, Text = "", TabStop = false,
                BackColor = Color.FromArgb(238, 240, 243), Cursor = Cursors.Hand, AccessibleName = name
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.White;
            b.Paint += (s, e) => glyph(e.Graphics, b.ClientRectangle, Color.FromArgb(50, 56, 66));
            b.Click += click;
            _sideTips.SetToolTip(b, tip);
            return b;
        }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        // ---- the bar ---------------------------------------------------------

        /// <summary>The buttons of the pages this user may open; the bar goes when there are none.</summary>
        private void RefreshSideBar()
        {
            if (_sideBar == null) return;
            var pages = SidePages.All().Where(p => SecurityManager.HasAccess(p.RequiredLevel)).ToList();

            // A page the user may no longer open is closed - in the panel and in a window.
            if (_sideOpen != null && !SecurityManager.HasAccess(_sideOpen.RequiredLevel)) CloseSidePanel();
            foreach (var key in _sideWindows.Keys.ToList())
            {
                var page = SidePages.Find(key);
                if (page != null && !SecurityManager.HasAccess(page.RequiredLevel)) _sideWindows[key].Close();
            }

            _sideBarList.SuspendLayout();
            foreach (var b in _sideButtons.Values) { _sideBarList.Controls.Remove(b); b.Dispose(); }
            _sideButtons.Clear();
            int y = 6;
            foreach (var p in pages)
            {
                var page = p;
                var b = new Button
                {
                    Size = new Size(SideButtonSize, SideButtonSize), FlatStyle = FlatStyle.Flat, Text = "",
                    BackColor = _sideBar.BackColor, Cursor = Cursors.Hand, TabStop = false, AccessibleName = page.Title
                };
                b.FlatAppearance.BorderSize = 0;
                var hover = UiStyle.HoverBorder(b);
                b.Paint += (s, e) => PaintSideButton(e.Graphics, b, page, hover());
                b.Click += (s, e) => ToggleSidePage(page.Key);
                _sideTips.SetToolTip(b, page.Title);
                // Placed at factor 1 and scaled as it is added (see UiScale).
                b.Location = new Point((SideBarWidth - SideButtonSize) / 2, y);
                y += SideButtonSize + 6;
                _sideBarList.Controls.Add(b);
                _sideButtons[page.Key] = b;
            }
            _sideBarList.ResumeLayout(true);
            _sideBar.Visible = pages.Count > 0;
            ApplySideBarFolded();
        }

        private void PaintSideButton(Graphics g, Button b, SidePage page, bool hover)
        {
            bool active = (_sideOpen != null && _sidePanel.Visible && string.Equals(page.Key, _sideOpen.Key, StringComparison.OrdinalIgnoreCase)) ||
                          _sideWindows.ContainsKey(page.Key);
            var r = b.ClientRectangle;
            if (active)
            {
                using (var fill = new SolidBrush(UiStyle.AccentBack)) g.FillRectangle(fill, r);
                using (var bar = new SolidBrush(Color.FromArgb(40, 90, 170))) g.FillRectangle(bar, r.Right - UiScale.Px(3), r.Top + 4, UiScale.Px(3), r.Height - 8);
            }
            var img = page.Icon != null ? page.Icon() as Image : null;
            if (img != null)
            {
                int s = Math.Min(r.Width, r.Height) * 3 / 5;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(img, r.X + (r.Width - s) / 2, r.Y + (r.Height - s) / 2, s, s);
            }
            else if (page.Glyph != null) DrawScaled(g, r, rr => page.Glyph(g, rr, Color.FromArgb(40, 90, 170)));
            if (hover)
                using (var pen = new Pen(UiStyle.HoverBorderColor, 1.6f))
                    g.DrawRectangle(pen, 1, 1, r.Width - 3, r.Height - 3);
        }

        /// <summary>A glyph drawn for factor 1, at the magnifying factor, centred in r.</summary>
        internal static void DrawScaled(Graphics g, Rectangle r, Action<Rectangle> draw)
        {
            var state = g.Save();
            try
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                if (UiScale.Active)
                {
                    float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
                    g.TranslateTransform(cx, cy);
                    g.ScaleTransform(UiScale.Factor, UiScale.Factor);
                    g.TranslateTransform(-cx, -cy);
                    // The same centre, the rectangle at factor 1.
                    int w = UiScale.Logical(r.Width), h = UiScale.Logical(r.Height);
                    r = new Rectangle((int)(cx - w / 2f), (int)(cy - h / 2f), w, h);
                }
                draw(r);
            }
            finally { g.Restore(state); }
        }

        private void SetSideBarFolded(bool folded)
        {
            _sideBarFolded = folded;
            ApplySideBarFolded();
            SaveSidePanelState();
        }

        private void ApplySideBarFolded()
        {
            if (_sideBar == null) return;
            _sideBar.Width = UiScale.For(_sideBar, _sideBarFolded ? SideBarFoldedWidth : SideBarWidth);
            _sideBarList.Visible = !_sideBarFolded;
            _sideTips.SetToolTip(_sideBarToggle, _sideBarFolded ? T("Tip_SideBarShow", "Show the side bar")
                                                               : T("Tip_SideBarHide", "Fold the side bar to the edge"));
            _sideBarToggle.Invalidate();
        }

        // ---- opening and closing -------------------------------------------

        /// <summary>Opens the page in the panel - or, when it is open there, closes it. A page in its own window comes to the front.</summary>
        public void ToggleSidePage(string key)
        {
            if (OpenSidePage != null && string.Equals(OpenSidePage.Key, key, StringComparison.OrdinalIgnoreCase)) { CloseSidePanel(); return; }
            ShowSidePage(key);
        }

        /// <summary>Opens a side page where it was last: in the panel, or in its own window. Returns its view.</summary>
        public Control ShowSidePage(string key)
        {
            var page = SidePages.Find(key);
            if (page == null || _sidePanel == null) return null;
            if (!SecurityManager.HasAccess(page.RequiredLevel))
            {
                MessageBox.Show(this, LanguageManager.GetString("Sys_ReqSec", "Requires Security Level:") + " " + page.RequiredLevel,
                                LanguageManager.GetString("Sys_Disabled", "Action Disabled."), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            SidePageWindow window;
            if (_sideWindows.TryGetValue(page.Key, out window) && !window.IsDisposed)
            {
                if (window.WindowState == FormWindowState.Minimized) window.WindowState = FormWindowState.Normal;
                window.Activate();
                return window.View;
            }

            var view = ViewOf(page);
            if (view == null) return null;
            if (_sideWantsWindow.Contains(page.Key) && page.CanPopOut) { PopOut(page, view); return view; }

            foreach (Control other in _sideHost.Controls) if (!ReferenceEquals(other, view)) other.Visible = false;
            if (view.Parent != _sideHost) { view.Dock = DockStyle.Fill; _sideHost.Controls.Add(view); }
            view.Visible = true;

            _sideOpen = page;
            _sideTitle.Text = page.Title;
            _sidePopOut.Visible = page.CanPopOut;
            // Never wider than leaves the page a working width.
            _sideSplitter.MinSize = UiScale.For(_sideSplitter, 240);
            _sideSplitter.MinExtra = UiScale.For(_sideSplitter, 320);
            int room = ClientSize.Width - pnlMenu.Width - (_sideBar.Visible ? _sideBar.Width : 0) - _sideSplitter.MinExtra;
            _sidePanel.Width = Math.Max(_sideSplitter.MinSize, Math.Min(UiScale.For(_sidePanel, _sideWidthLogical), room));
            _sideSplitter.Visible = true;
            _sidePanel.Visible = true;
            RestoreDockOrder();
            view.Focus();
            SideStateChanged();
            return view;
        }

        /// <summary>Closes the panel; the page stays built, as it was left.</summary>
        public void CloseSidePanel()
        {
            if (_sidePanel == null || !_sidePanel.Visible) return;
            // A page with lists keeps them now: it is hidden, never closed.
            var monitor = _sideOpen != null ? SidePageView(_sideOpen.Key) as SimulationMonitorForm : null;
            if (monitor != null) monitor.SaveLists();
            _sidePanel.Visible = false;
            _sideSplitter.Visible = false;
            _sideOpen = null;
            SideStateChanged();
        }

        private Control ViewOf(SidePage page)
        {
            Control view;
            if (_sideViews.TryGetValue(page.Key, out view) && !view.IsDisposed) return view;
            try { view = page.Create != null ? page.Create() : null; }
            catch (Exception ex)
            {
                AuditLogger.Log("side page '" + page.Key + "' could not be built: " + ex.Message);
                MessageBox.Show(this, ex.Message, page.Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            if (view == null) return null;
            var form = view as Form;
            if (form != null)
            {
                form.TopLevel = false;
                form.FormBorderStyle = FormBorderStyle.None;
                form.ShowInTaskbar = false;
                form.Visible = true;
            }
            _sideViews[page.Key] = view;
            UiStyle.ApplyInteractionCues(view);
            return view;
        }

        /// <summary>The panel docks between the caption and the menu - kept so after any Controls change.</summary>
        private void RestoreDockOrder()
        {
            int top = Controls.GetChildIndex(pnlTop);
            Controls.SetChildIndex(_sideBar, Math.Max(0, top - 1));
            Controls.SetChildIndex(_sidePanel, Math.Max(0, Controls.GetChildIndex(_sideBar) - 1));
            Controls.SetChildIndex(_sideSplitter, Math.Max(0, Controls.GetChildIndex(_sidePanel) - 1));
        }

        private void SideStateChanged()
        {
            foreach (var b in _sideButtons.Values) b.Invalidate();
            PerformLayout();
            if (_currentScreen != null) RelayoutFunctionUnits(_currentScreen);
            if (_scanner != null) _scanner.RefreshTags();
        }

        /// <summary>Keeps what the side pages hold (the monitor's lists) - on closing the program.</summary>
        private void SaveAllSideViews()
        {
            foreach (var v in _sideViews.Values)
            {
                var monitor = v as SimulationMonitorForm;
                if (monitor != null && !monitor.IsDisposed) monitor.SaveLists();
            }
            SaveSidePanelState();
        }

        // ---- a window of its own -----------------------------------------------

        private void PopOutSidePage()
        {
            if (_sideOpen == null) return;
            var page = _sideOpen;
            var view = ViewOf(page);
            CloseSidePanel();
            _sideWantsWindow.Add(page.Key);
            SaveSidePanelState();
            PopOut(page, view);
        }

        private void PopOut(SidePage page, Control view)
        {
            if (view.Parent != null) view.Parent.Controls.Remove(view);
            var window = new SidePageWindow(page, view, this);
            _sideWindows[page.Key] = window;
            window.DockBack += (s, e) =>
            {
                _sideWantsWindow.Remove(page.Key);
                SaveSidePanelState();
                window.Close();
                ShowSidePage(page.Key);
            };
            window.FormClosed += (s, e) =>
            {
                _sideWindows.Remove(page.Key);
                if (_scanner != null) _scanner.RemoveTarget(window);
                SideStateChanged();
            };
            // Its lamps and fields read like the panel's.
            if (_scanner != null) _scanner.AddTarget(window);
            window.Show(this);
            SideStateChanged();
        }

        // ---- kept between runs -------------------------------------------------

        private void LoadSidePanelState()
        {
            var v = WindowLayout.Read(SidePanelPath);
            string text;
            int w;
            if (v.TryGetValue("Width", out text) && int.TryParse(text, out w) && w >= 200 && w <= 3000) _sideWidthLogical = w;
            _sideBarFolded = v.TryGetValue("BarFolded", out text) && text == "1";
            _sideWantsWindow.Clear();
            if (v.TryGetValue("InWindow", out text))
                foreach (var key in text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) _sideWantsWindow.Add(key.Trim());
        }

        private void SaveSidePanelState()
        {
            WindowLayout.Write(SidePanelPath, new Dictionary<string, string>
            {
                { "Width", _sideWidthLogical.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                { "BarFolded", _sideBarFolded ? "1" : "0" },
                { "InWindow", string.Join(",", _sideWantsWindow) }
            });
        }

        // ---- pictures -------------------------------------------------------------

        /// <summary>The bar's fold button: a chevron pointing where the bar goes.</summary>
        private static void DrawChevron(Graphics g, Rectangle r, bool pointRight, Color colour)
        {
            DrawScaled(g, r, rr =>
            {
                float cx = rr.X + rr.Width / 2f, cy = rr.Y + rr.Height / 2f, d = pointRight ? 1 : -1;
                using (var pen = new Pen(colour, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    g.DrawLines(pen, new[] { new PointF(cx - 4 * d, cy - 6), new PointF(cx + 1 * d, cy), new PointF(cx - 4 * d, cy + 6) });
                    if (rr.Width > 20)
                        g.DrawLines(pen, new[] { new PointF(cx + 1 * d, cy - 6), new PointF(cx + 6 * d, cy), new PointF(cx + 1 * d, cy + 6) });
                }
            });
        }

        /// <summary>Inputs / Outputs: two lamps, one lit, each with its line.</summary>
        internal static void DrawIoGlyph(Graphics g, Rectangle r, Color colour)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            var top = new RectangleF(cx - 10, cy - 8.5f, 7, 7);
            var bottom = new RectangleF(cx - 10, cy + 1.5f, 7, 7);
            using (var lit = new SolidBrush(Color.FromArgb(76, 175, 80))) g.FillEllipse(lit, top);
            using (var pen = new Pen(Color.FromArgb(40, 46, 56), 1.4f))
            {
                g.DrawEllipse(pen, top);
                g.DrawEllipse(pen, bottom);
            }
            using (var line = new Pen(colour, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(line, cx, cy - 5, cx + 10, cy - 5);
                g.DrawLine(line, cx, cy + 5, cx + 10, cy + 5);
            }
        }

        /// <summary>A window with an arrow out of it.</summary>
        internal static void DrawPopOutGlyph(Graphics g, Rectangle r, Color colour)
        {
            DrawScaled(g, r, rr =>
            {
                float cx = rr.X + rr.Width / 2f, cy = rr.Y + rr.Height / 2f;
                using (var pen = new Pen(colour, 1.7f) { LineJoin = LineJoin.Round })
                {
                    g.DrawLines(pen, new[] { new PointF(cx - 1, cy - 6), new PointF(cx - 7, cy - 6), new PointF(cx - 7, cy + 6),
                                             new PointF(cx + 5, cy + 6), new PointF(cx + 5, cy + 1) });
                    g.DrawLine(pen, cx - 2, cy + 1, cx + 6, cy - 7);
                    g.DrawLines(pen, new[] { new PointF(cx + 1, cy - 7), new PointF(cx + 6, cy - 7), new PointF(cx + 6, cy - 2) });
                }
            });
        }

        /// <summary>A window with an arrow into it (back into the main window).</summary>
        internal static void DrawDockGlyph(Graphics g, Rectangle r, Color colour)
        {
            DrawScaled(g, r, rr =>
            {
                float cx = rr.X + rr.Width / 2f, cy = rr.Y + rr.Height / 2f;
                using (var pen = new Pen(colour, 1.7f) { LineJoin = LineJoin.Round })
                {
                    g.DrawRectangle(pen, cx - 7, cy - 6, 14, 12);
                    g.DrawLine(pen, cx + 1, cy - 6, cx + 1, cy + 6);
                    g.DrawLine(pen, cx - 5, cy, cx - 1, cy);
                    g.DrawLines(pen, new[] { new PointF(cx - 3, cy - 2), new PointF(cx - 1, cy), new PointF(cx - 3, cy + 2) });
                }
                using (var fill = new SolidBrush(Color.FromArgb(90, colour))) g.FillRectangle(fill, cx + 1, cy - 6, 6, 12);
            });
        }

        internal static void DrawCloseGlyph(Graphics g, Rectangle r, Color colour)
        {
            DrawScaled(g, r, rr =>
            {
                float cx = rr.X + rr.Width / 2f, cy = rr.Y + rr.Height / 2f;
                using (var pen = new Pen(colour, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                    g.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
                }
            });
        }
    }

    /// <summary>
    /// A side page taken out of the panel into a window of its own - for a second
    /// screen, or to see it beside a page it would otherwise narrow. It opens
    /// where it was last closed; its header's button puts it back in the panel.
    /// Closing the window hides the page, as closing the panel does.
    /// </summary>
    public sealed class SidePageWindow : Form
    {
        private readonly SidePage _page;
        public Control View { get; private set; }
        public event EventHandler DockBack;

        public SidePageWindow(SidePage page, Control view, Form owner)
        {
            _page = page;
            View = view;
            Text = page.Title;
            ShowInTaskbar = true;
            MinimizeBox = true;
            BackColor = Color.White;
            Font = UiStyle.Body;
            Size = new Size(760, 680);
            MinimumSize = new Size(300, 240);
            StartPosition = FormStartPosition.Manual;
            if (owner != null) Location = new Point(Math.Max(0, owner.Right - Width - 40), owner.Top + 60);

            var header = new Panel { Dock = DockStyle.Top, Height = 32, BackColor = Color.FromArgb(238, 240, 243) };
            var title = new HeaderTitle
            {
                Dock = DockStyle.Fill, Padding = new Padding(8, 0, 0, 0),
                Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold), ForeColor = Color.FromArgb(40, 46, 56), Text = page.Title
            };
            var back = new Button
            {
                Dock = DockStyle.Right, Width = 34, FlatStyle = FlatStyle.Flat, Text = "", TabStop = false,
                BackColor = header.BackColor, Cursor = Cursors.Hand, AccessibleName = "Dock back"
            };
            back.FlatAppearance.BorderSize = 0;
            back.FlatAppearance.MouseOverBackColor = Color.White;
            back.Paint += (s, e) => MainForm.DrawDockGlyph(e.Graphics, back.ClientRectangle, Color.FromArgb(50, 56, 66));
            back.Click += (s, e) => { if (DockBack != null) DockBack(this, EventArgs.Empty); };
            UiStyle.CreateTips().SetToolTip(back, LanguageManager.GetString("Tip_SideDockBack", "Back into the main window's side panel"));
            header.Controls.Add(title);
            header.Controls.Add(back);

            view.Dock = DockStyle.Fill;
            view.Visible = true;
            Controls.Add(view);
            Controls.Add(header);

            // Scaled first, then placed where it was: a kept size is not scaled again.
            UiScale.ApplyOnLoad(this);
            WindowLayout.Apply(this, LayoutName, WindowLayout.Read(Kaban.UI.Forms.SimulationMonitorForm.WindowsPath));
        }

        private string LayoutName { get { return "Side." + _page.Key; } }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;
            var values = new Dictionary<string, string>();
            WindowLayout.Store(this, LayoutName, values);
            WindowLayout.Write(Kaban.UI.Forms.SimulationMonitorForm.WindowsPath, values);
            // The page is not closed with the window: it is kept, as it was left.
            var monitor = View as SimulationMonitorForm;
            if (monitor != null) monitor.SaveLists();
            Controls.Remove(View);
            View.Visible = false;
        }
    }

    /// <summary>A header's title: one line, cut with "..." when narrow - never wrapped onto a second line.</summary>
    internal sealed class HeaderTitle : Control
    {
        public HeaderTitle()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var r = new Rectangle(Padding.Left, 0, Math.Max(0, Width - Padding.Horizontal), Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, r, ForeColor,
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }
    }
}
