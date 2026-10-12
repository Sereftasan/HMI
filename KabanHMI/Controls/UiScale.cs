using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// The MAGNIFIER: draws the whole HMI larger or smaller - pages, text,
    /// buttons, fields, menus - by the magnifying factor this run was started
    /// with (DisplaySettings: 0.8 .. 1.5).
    ///
    /// HOW. Every window and every page is scaled ONCE, when it first appears,
    /// the way Windows itself scales a program for a large-text display: the
    /// positions and sizes of all its parts (Control.Scale), then the fonts. A
    /// page added later to a window already scaled is scaled as it is added.
    /// Nothing is ever scaled twice - scaled controls and fonts are remembered -
    /// so a page shown, hidden and shown again stays the size it was.
    ///
    /// THE RULE FOR CODE THAT BUILDS CONTROLS. Sizes given to a control BEFORE it
    /// is added are written as they are at factor 1 - the control is scaled with
    /// the rest when it is added. Sizes set on a control that is ALREADY on
    /// screen (a Resize or Layout handler, a width restored from a file) go
    /// through Px(). Fonts made for drawing (not given to a control) go through
    /// NewFont() or Pt().
    ///
    /// At factor 1 nothing here does anything: the program is exactly what it
    /// was without the magnifier.
    /// </summary>
    public static class UiScale
    {
        private static float? _factor;

        /// <summary>The factor this run started with (read once; changing it needs a restart).</summary>
        public static float Factor
        {
            get
            {
                if (!_factor.HasValue)
                {
                    try { _factor = (float)DisplaySettings.StartFactor; }
                    catch { _factor = 1f; }
                }
                return _factor.Value;
            }
        }

        /// <summary>For tests and tools: the factor to use, set before any window is built.</summary>
        public static void Override(double factor) { _factor = (float)DisplaySettings.Clamp(factor); }

        /// <summary>False at factor 1: nothing is scaled.</summary>
        public static bool Active { get { return Math.Abs(Factor - 1f) > 0.001f; } }

        /// <summary>A length at factor 1, in pixels at this factor.</summary>
        public static int Px(int logical) { return (int)Math.Round(logical * Factor); }
        public static Size Px(Size logical) { return new Size(Px(logical.Width), Px(logical.Height)); }
        public static Padding Px(Padding p) { return new Padding(Px(p.Left), Px(p.Top), Px(p.Right), Px(p.Bottom)); }

        /// <summary>
        /// A length for a control that may or may not have been scaled yet: as it
        /// is before (the control is scaled with its window), in pixels after.
        /// </summary>
        public static int For(Control c, int logical) { return IsScaled(c) ? Px(logical) : logical; }

        /// <summary>Pixels at this factor back to factor 1 - for a size kept in a file.</summary>
        public static int Logical(int pixels) { return (int)Math.Round(pixels / Factor); }

        /// <summary>A font size in points at this factor.</summary>
        public static float Pt(float points) { return points * Factor; }

        // ------------------------------------------------------------------
        // FONTS: a font made here, or already scaled, is never scaled again.
        // ------------------------------------------------------------------

        private static readonly ConditionalWeakTable<Font, Font> _scaledOf = new ConditionalWeakTable<Font, Font>();
        private static readonly ConditionalWeakTable<Font, object> _isScaled = new ConditionalWeakTable<Font, object>();
        private static readonly object Mark = new object();

        /// <summary>A font at this factor, for drawing or for a control: "Segoe UI", 9.5 -> 9.5 x factor.</summary>
        public static Font NewFont(string family, float points, FontStyle style = FontStyle.Regular)
        {
            return Marked(new Font(family, Pt(points), style));
        }

        /// <summary>Records a font as being at this factor already.</summary>
        public static Font Marked(Font f)
        {
            if (f != null && Active)
            {
                object unused;
                if (!_isScaled.TryGetValue(f, out unused)) _isScaled.Add(f, Mark);
            }
            return f;
        }

        public static bool IsScaled(Font f)
        {
            object unused;
            return f != null && _isScaled.TryGetValue(f, out unused);
        }

        /// <summary>The font at this factor: the same one if it already is; one copy per original font.</summary>
        public static Font Scaled(Font f)
        {
            if (f == null || !Active || IsScaled(f)) return f;
            Font s;
            if (_scaledOf.TryGetValue(f, out s)) return s;
            s = Marked(new Font(f.FontFamily, f.Size * Factor, f.Style, f.Unit, f.GdiCharSet, f.GdiVerticalFont));
            _scaledOf.Add(f, s);
            return s;
        }

        // ------------------------------------------------------------------
        // CONTROLS
        // ------------------------------------------------------------------

        private static readonly ConditionalWeakTable<Control, object> _done = new ConditionalWeakTable<Control, object>();

        public static bool IsScaled(Control c)
        {
            object unused;
            return c != null && _done.TryGetValue(c, out unused);
        }

        // ---- pictures ------------------------------------------------------
        //
        // A button's picture is NOT scaled with the button (Control.Scale leaves
        // images alone): at 150% the menu's pictures stayed their 24 px in
        // buttons half as large again, and at 80% they crowded smaller buttons.

        private static readonly ConditionalWeakTable<Image, object> _pictureAtFactor = new ConditionalWeakTable<Image, object>();
        private static readonly ConditionalWeakTable<Image, Image> _pictureScaled = new ConditionalWeakTable<Image, Image>();

        /// <summary>Records a picture as made at this factor already (drawn at its size - see NavGlyphs).</summary>
        public static Image MarkedPicture(Image img)
        {
            object unused;
            if (img != null && !_pictureAtFactor.TryGetValue(img, out unused)) _pictureAtFactor.Add(img, Mark);
            return img;
        }

        /// <summary>
        /// A picture at this factor: smoothly enlarged or reduced, once per
        /// picture; one made at the factor already is returned as it is.
        /// </summary>
        public static Image Picture(Image img)
        {
            if (img == null || !Active) return img;
            object unused;
            if (_pictureAtFactor.TryGetValue(img, out unused)) return img;
            Image done;
            if (_pictureScaled.TryGetValue(img, out done)) return done;
            var bmp = new Bitmap(Math.Max(1, Px(img.Width)), Math.Max(1, Px(img.Height)));
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.DrawImage(img, new Rectangle(0, 0, bmp.Width, bmp.Height));
            }
            MarkedPicture(bmp);
            _pictureScaled.Add(img, bmp);
            return bmp;
        }

        /// <summary>
        /// A form scaled as it loads - before it is seen. Called from the forms'
        /// constructors; a form that does not is scaled on the next idle moment
        /// instead (see UiStyle.SweepOpenForms), a moment after it appears.
        /// </summary>
        public static void ApplyOnLoad(Form form)
        {
            if (!Active || form == null) return;
            form.Load += (s, e) =>
            {
                var before = form.Bounds;
                Apply(form);
                if (form.WindowState != FormWindowState.Normal || form.Bounds.Size == before.Size) return;
                // Centred again at its new size, where it was meant to open.
                if (form.StartPosition == FormStartPosition.CenterParent || form.StartPosition == FormStartPosition.CenterScreen)
                {
                    var around = form.StartPosition == FormStartPosition.CenterParent && form.Owner != null
                        ? form.Owner.Bounds : Screen.FromRectangle(before).WorkingArea;
                    var area = Screen.FromRectangle(around).WorkingArea;
                    int x = around.X + (around.Width - form.Width) / 2, y = around.Y + (around.Height - form.Height) / 2;
                    form.Location = new Point(Math.Max(area.Left, Math.Min(x, area.Right - form.Width)),
                                              Math.Max(area.Top, Math.Min(y, area.Bottom - form.Height)));
                }
            };
        }

        /// <summary>
        /// Scales a window or a page and everything in it, once; and whatever is
        /// added to it later, as it is added.
        /// </summary>
        public static void Apply(Control root)
        {
            if (!Active || root == null || root.IsDisposed) return;
            if (IsScaled(root)) { Watch(root); return; }

            // Parts that were scaled on their own already (a page moved between
            // windows, the monitor docked in a panel) are taken out while the rest
            // is scaled, and put back where they were.
            var parked = new List<Tuple<Control, Control, int>>();
            CollectScaled(root, parked);
            foreach (var p in parked) p.Item2.Controls.Remove(p.Item1);

            try
            {
                var all = new List<Control>();
                Collect(root, all);

                // AutoScale would scale a SECOND time when the fonts change below.
                foreach (var c in all)
                {
                    var cc = c as ContainerControl;
                    if (cc != null && cc.AutoScaleMode != AutoScaleMode.None) cc.AutoScaleMode = AutoScaleMode.None;
                }

                var form = root as Form;
                bool topLevel = form != null && form.TopLevel;
                var before = root.Bounds;
                bool wasMaximized = topLevel && form.WindowState == FormWindowState.Maximized;

                root.SuspendLayout();
                try
                {
                    // Positions and sizes of everything under root (and root's own size).
                    root.Scale(new SizeF(Factor, Factor));

                    foreach (var c in all)
                    {
                        ScaleExtras(c);
                        ScaleFont(c);
                        _done.Add(c, Mark);
                    }
                }
                finally { root.ResumeLayout(true); }

                // Code that lays controls out as they are resized ran DURING the
                // scaling, before anything counted as scaled (so with factor-1
                // sizes, then scaled again). It runs once more now, parents first,
                // with the scaled sizes.
                foreach (var c in all)
                {
                    if (c.IsDisposed) continue;
                    try { OnResizeMethod.Invoke(c, new object[] { EventArgs.Empty }); } catch { }
                    c.PerformLayout();
                }

                // A window stays on its screen: a dialog grows, but never past the
                // working area; the main window keeps the screen it had.
                if (wasMaximized)
                {
                    // The screen is the screen: a maximised window keeps its bounds.
                    if (form.Bounds != before) form.Bounds = before;
                }
                else if (topLevel && form.WindowState == FormWindowState.Normal)
                {
                    var area = Screen.FromRectangle(before).WorkingArea;
                    var r = form.Bounds;
                    r.Width = Math.Min(r.Width, area.Width);
                    r.Height = Math.Min(r.Height, area.Height);
                    r.X = Math.Max(area.Left, Math.Min(r.X, area.Right - r.Width));
                    r.Y = Math.Max(area.Top, Math.Min(r.Y, area.Bottom - r.Height));
                    if (r != form.Bounds) form.Bounds = r;
                }
            }
            finally
            {
                foreach (var p in parked)
                {
                    p.Item2.Controls.Add(p.Item1);
                    p.Item2.Controls.SetChildIndex(p.Item1, Math.Min(p.Item3, p.Item2.Controls.Count - 1));
                }
            }
            Watch(root);
        }

        private static readonly System.Reflection.MethodInfo OnResizeMethod =
            typeof(Control).GetMethod("OnResize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        private static void Collect(Control c, List<Control> into)
        {
            into.Add(c);
            foreach (Control child in c.Controls) Collect(child, into);
        }

        private static void CollectScaled(Control root, List<Tuple<Control, Control, int>> into)
        {
            foreach (Control child in root.Controls.Cast<Control>().ToList())
            {
                if (IsScaled(child)) into.Add(Tuple.Create(child, root, root.Controls.GetChildIndex(child)));
                else CollectScaled(child, into);
            }
        }

        /// <summary>A control's OWN font (not the one it takes from its parent), at this factor.</summary>
        private static void ScaleFont(Control c)
        {
            var f = c.Font;
            if (IsScaled(f)) return;
            // Inherited: follows the parent, scaled before it (the list is parents first).
            if (c.Parent != null && ReferenceEquals(f, c.Parent.Font)) return;
            c.Font = Scaled(f);
        }

        /// <summary>What Control.Scale leaves as it was: grid columns and rows, list columns, toolbar items.</summary>
        private static void ScaleExtras(Control c)
        {
            var grid = c as DataGridView;
            if (grid != null)
            {
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    col.Width = Px(col.Width);
                    col.MinimumWidth = Math.Max(2, Px(col.MinimumWidth));
                }
                grid.RowTemplate.Height = Px(grid.RowTemplate.Height);
                foreach (DataGridViewRow row in grid.Rows) row.Height = Px(row.Height);
                if (grid.ColumnHeadersHeightSizeMode != DataGridViewColumnHeadersHeightSizeMode.AutoSize)
                    grid.ColumnHeadersHeight = Math.Max(4, Px(grid.ColumnHeadersHeight));
                if (grid.RowHeadersWidthSizeMode == DataGridViewRowHeadersWidthSizeMode.EnableResizing ||
                    grid.RowHeadersWidthSizeMode == DataGridViewRowHeadersWidthSizeMode.DisableResizing)
                    grid.RowHeadersWidth = Math.Max(4, Px(grid.RowHeadersWidth));
                foreach (var style in new[] { grid.DefaultCellStyle, grid.ColumnHeadersDefaultCellStyle, grid.RowsDefaultCellStyle, grid.AlternatingRowsDefaultCellStyle })
                    if (style != null && style.Font != null) style.Font = Scaled(style.Font);
                foreach (DataGridViewColumn col in grid.Columns)
                    if (col.HasDefaultCellStyle && col.DefaultCellStyle.Font != null) col.DefaultCellStyle.Font = Scaled(col.DefaultCellStyle.Font);
                return;
            }
            var list = c as ListView;
            if (list != null)
            {
                foreach (ColumnHeader h in list.Columns) h.Width = Px(h.Width);
                return;
            }
            var strip = c as ToolStrip;
            if (strip != null)
            {
                foreach (ToolStripItem item in strip.Items)
                {
                    if (!item.AutoSize) item.Size = Px(item.Size);
                    item.Margin = Px(item.Margin);
                    if (item.Font != null && !(item is ToolStripControlHost)) item.Font = Scaled(item.Font);
                }
                strip.Padding = Px(strip.Padding);
            }
        }

        /// <summary>Scales what is added under root later, as it is added.</summary>
        private static readonly ConditionalWeakTable<Control, object> _watched = new ConditionalWeakTable<Control, object>();

        private static void Watch(Control root)
        {
            var all = new List<Control>();
            Collect(root, all);
            foreach (var c in all)
            {
                object unused;
                if (_watched.TryGetValue(c, out unused)) continue;
                _watched.Add(c, Mark);
                c.ControlAdded += (s, e) =>
                {
                    if (e.Control == null || IsScaled(e.Control)) { if (e.Control != null) Watch(e.Control); return; }
                    Apply(e.Control);
                };
            }
        }
    }
}
