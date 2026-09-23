using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// The surface floating panes live on.
    ///
    /// It owns the three things a single pane cannot decide for itself:
    ///
    ///   THE TRAY   minimised panes line up along the BOTTOM-LEFT corner, in the
    ///              order they were minimised. A pane cannot place itself there
    ///              without knowing about its siblings, or two panes parked at the
    ///              same moment would sit on top of each other.
    ///
    ///   Z-ORDER    a maximised pane covers the page, so nothing else may be above
    ///              it; the tray is the exception, or the operator would have no
    ///              way back to a parked pane.
    ///
    ///   LAYOUTS    saved BY NAME, so one page serves several jobs - "profile
    ///              entry" wants the catalogue grids large and the DXF viewer
    ///              parked; "dxf work" wants the reverse.
    /// </summary>
    public class PaneHostControl : Panel
    {
        private const int TrayMargin = 6;
        private const int TrayGap = 4;
        private const int TrayPaneWidth = 190;

        private readonly List<FloatingPaneHMI> _panes = new List<FloatingPaneHMI>();

        /// <summary>Minimise order, so the tray does not reshuffle on every change.</summary>
        private readonly List<FloatingPaneHMI> _trayOrder = new List<FloatingPaneHMI>();

        /// <summary>Identifies this page's layouts on disk. Set before adding panes.</summary>
        public string LayoutScope { get; set; }

        public IEnumerable<FloatingPaneHMI> Panes { get { return _panes; } }

        public PaneHostControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(246, 247, 249);
            AutoScroll = false;      // panes are positioned absolutely; scrolling fights that
            LayoutScope = "panes";
        }

        // ==================================================================
        // PANES
        // ==================================================================

        public FloatingPaneHMI AddPane(string key, string caption, Control content,
                                       Rectangle bounds)
        {
            var pane = new FloatingPaneHMI(key, caption);
            pane.Bounds = bounds;
            pane.RestoreBounds = bounds;

            if (content != null)
            {
                content.Dock = DockStyle.Fill;
                pane.Content.Controls.Add(content);
            }

            pane.StateChanged += (s, e) => OnPaneStateChanged(pane);
            _panes.Add(pane);
            Controls.Add(pane);
            return pane;
        }

        private void OnPaneStateChanged(FloatingPaneHMI pane)
        {
            if (pane.State == FloatingPaneHMI.PaneState.Minimized)
            {
                if (!_trayOrder.Contains(pane)) _trayOrder.Add(pane);
            }
            else
            {
                _trayOrder.Remove(pane);
                // Restoring must lift the pane above whatever was covering it, or it
                // comes back invisibly underneath and looks like nothing happened.
                pane.BringToFront();
            }

            // Only ONE pane may be maximised: two both filling the page means the
            // one underneath is unreachable with no way to tell it is there.
            if (pane.State == FloatingPaneHMI.PaneState.Maximized)
                foreach (var other in _panes)
                    if (other != pane && other.State == FloatingPaneHMI.PaneState.Maximized)
                        other.State = FloatingPaneHMI.PaneState.Normal;

            LayoutTray();
        }

        /// <summary>
        /// Lines the minimised panes along the bottom-left, left to right, wrapping
        /// upward if the row fills.
        /// </summary>
        public void LayoutTray()
        {
            int x = TrayMargin;
            int rowTop = ClientSize.Height - TrayMargin;

            foreach (var pane in _trayOrder)
            {
                if (pane.State != FloatingPaneHMI.PaneState.Minimized) continue;

                int h = pane.Height;
                if (x + TrayPaneWidth > ClientSize.Width - TrayMargin && x > TrayMargin)
                {
                    x = TrayMargin;                 // wrap to a row above
                    rowTop -= h + TrayGap;
                }

                pane.SetBounds(x, rowTop - h, TrayPaneWidth, h);
                pane.BringToFront();                // the tray is always reachable
                x += TrayPaneWidth + TrayGap;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            foreach (var pane in _panes) pane.ReflowForParent();   // keep a maximised pane full
            LayoutTray();
        }

        // ==================================================================
        // NAMED LAYOUTS
        // ==================================================================

        private static string LayoutFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "KabanHMI", "Layouts");
            }
        }

        private string LayoutPath(string name)
        {
            var safe = new StringBuilder();
            foreach (char c in name ?? "")
                safe.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            return Path.Combine(LayoutFolder, LayoutScope + "." + safe.ToString().Trim() + ".layout");
        }

        /// <summary>Every saved layout name for this page.</summary>
        public List<string> LayoutNames()
        {
            var names = new List<string>();
            try
            {
                if (!Directory.Exists(LayoutFolder)) return names;
                string prefix = LayoutScope + ".";
                foreach (var f in Directory.GetFiles(LayoutFolder, prefix + "*.layout"))
                {
                    var n = Path.GetFileNameWithoutExtension(f);
                    if (n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        names.Add(n.Substring(prefix.Length));
                }
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not list pane layouts: " + ex.Message);
            }
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }

        public bool SaveLayout(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            try
            {
                Directory.CreateDirectory(LayoutFolder);
                var lines = new List<string>
                {
                    "# Kaban HMI pane layout - " + LayoutScope + " / " + name,
                    "# key=left,top,width,height,state   (state 0 normal, 1 maximised, 2 minimised)"
                };
                foreach (var pane in _panes)
                    lines.Add(pane.Key + "=" + pane.Serialize());

                // Tray ORDER is saved too: restoring a layout should reproduce the
                // row exactly, and re-deriving it from the pane list would reorder
                // the parked panes every time.
                lines.Add("#tray=" + string.Join("|", _trayOrder.Select(p => p.Key).ToArray()));

                File.WriteAllLines(LayoutPath(name), lines, new UTF8Encoding(true));
                AuditLogger.Log("saved pane layout '" + name + "' for " + LayoutScope + ".");
                return true;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not save pane layout: " + ex.Message);
                return false;
            }
        }

        public bool LoadLayout(string name)
        {
            try
            {
                string path = LayoutPath(name);
                if (!File.Exists(path)) return false;

                var byKey = _panes.ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
                var trayKeys = new List<string>();

                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.StartsWith("#tray=", StringComparison.OrdinalIgnoreCase))
                    {
                        trayKeys.AddRange(line.Substring(6).Split(
                            new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
                        continue;
                    }
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;

                    FloatingPaneHMI pane;
                    // A layout saved when the page had a pane that no longer exists
                    // is not an error - the page moves on, the layout should still
                    // restore everything it still recognises.
                    if (byKey.TryGetValue(line.Substring(0, eq).Trim(), out pane))
                        pane.Deserialize(line.Substring(eq + 1));
                }

                _trayOrder.Clear();
                foreach (var k in trayKeys)
                {
                    FloatingPaneHMI pane;
                    if (byKey.TryGetValue(k.Trim(), out pane) &&
                        pane.State == FloatingPaneHMI.PaneState.Minimized)
                        _trayOrder.Add(pane);
                }
                // Anything minimised but missing from the saved order still needs a
                // slot, or it would be parked nowhere and unreachable.
                foreach (var pane in _panes)
                    if (pane.State == FloatingPaneHMI.PaneState.Minimized && !_trayOrder.Contains(pane))
                        _trayOrder.Add(pane);

                LayoutTray();
                AuditLogger.Log("loaded pane layout '" + name + "' for " + LayoutScope + ".");
                return true;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not load pane layout: " + ex.Message);
                return false;
            }
        }

        public bool DeleteLayout(string name)
        {
            try
            {
                string path = LayoutPath(name);
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                AuditLogger.Log("could not delete pane layout: " + ex.Message);
                return false;
            }
        }

        /// <summary>Every pane back to a readable size, in a simple cascade.</summary>
        public void ResetLayout()
        {
            _trayOrder.Clear();
            int x = 12, y = 12, step = 26;
            foreach (var pane in _panes)
            {
                pane.State = FloatingPaneHMI.PaneState.Normal;
                pane.SetBounds(x, y, Math.Min(620, Math.Max(320, ClientSize.Width - x - 40)),
                                     Math.Min(360, Math.Max(200, ClientSize.Height - y - 60)));
                pane.RestoreBounds = pane.Bounds;
                x += step; y += step;
                if (y > ClientSize.Height - 220) { y = 12; x += 40; }
            }
        }
    }
}

