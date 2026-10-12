using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Kaban.Core.Helpers;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// Where a tool window was, how big, and how its list's columns were - so it
    /// opens the way it was closed.
    ///
    /// Kept in a small text file of name=value lines ("Find.Bounds=120,80,820,440"),
    /// one file for the tool windows of a program part, each window under its own
    /// name. A place that is no longer on any screen (the second monitor is gone,
    /// the resolution went down) is not used: the window opens where it would
    /// have opened the first time, instead of somewhere nobody can reach it.
    /// </summary>
    public static class WindowLayout
    {
        /// <summary>The values in a layout file; empty when there is none or it cannot be read.</summary>
        public static Dictionary<string, string> Read(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return values;
                foreach (var line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0 || line.TrimStart().StartsWith("#")) continue;
                    values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            catch (Exception ex) { AuditLogger.Log("could not read the window layout " + path + ": " + ex.Message); }
            return values;
        }

        /// <summary>Writes these values into the file, keeping the other windows' lines (<paramref name="replace"/>: these values only).</summary>
        public static void Write(string path, IDictionary<string, string> changes, bool replace = false)
        {
            try
            {
                var values = replace ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) : Read(path);
                foreach (var kv in changes) values[kv.Key] = kv.Value;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var lines = new List<string> { "# Kaban HMI tool windows: <window>.<what>=<value>" };
                lines.AddRange(values.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase).Select(kv => kv.Key + "=" + kv.Value));
                SafeFile.WriteAllText(path, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(false));
            }
            catch (Exception ex) { AuditLogger.Log("could not save the window layout " + path + ": " + ex.Message); }
        }

        /// <summary>The window's place and size as it would open again: its normal bounds, and whether it was maximised.</summary>
        public static void Store(Form form, string name, IDictionary<string, string> into)
        {
            var b = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
            if (b.Width <= 0 || b.Height <= 0) return;
            into[name + ".Bounds"] = string.Join(",", new[] { b.X, b.Y, b.Width, b.Height }.Select(v => v.ToString(CultureInfo.InvariantCulture)));
            into[name + ".Maximized"] = form.WindowState == FormWindowState.Maximized ? "1" : "0";
        }

        /// <summary>Puts the window where it was kept; false when nothing usable was kept.</summary>
        public static bool Apply(Form form, string name, IDictionary<string, string> from)
        {
            Rectangle r;
            string text;
            if (from == null || !from.TryGetValue(name + ".Bounds", out text) || !TryParse(text, out r)) return false;
            r.Width = Math.Max(r.Width, form.MinimumSize.Width);
            r.Height = Math.Max(r.Height, form.MinimumSize.Height);
            if (r.Width < 100 || r.Height < 80 || !OnScreen(r)) return false;
            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = r;
            bool maximized = from.TryGetValue(name + ".Maximized", out text) && text == "1";
            if (maximized) form.WindowState = FormWindowState.Maximized;
            // The magnifier scales a window as it loads; a kept size is the size it
            // had on screen, so it is put back after that (call UiScale.ApplyOnLoad
            // first, so this runs after it).
            if (Kaban.UI.Controls.UiScale.Active)
                form.Load += (s, e) =>
                {
                    if (form.WindowState == FormWindowState.Normal) form.Bounds = r;
                };
            return true;
        }

        /// <summary>True when the window's title strip is on a screen: it can be taken and moved.</summary>
        public static bool OnScreen(Rectangle r)
        {
            var title = new Rectangle(r.X, r.Y, r.Width, 32);
            foreach (var screen in Screen.AllScreens)
            {
                var seen = Rectangle.Intersect(screen.WorkingArea, title);
                if (seen.Width >= 80 && seen.Height >= 16) return true;
            }
            return false;
        }

        internal static bool TryParse(string text, out Rectangle r)
        {
            r = Rectangle.Empty;
            var f = (text ?? "").Split(',');
            int[] v = new int[4];
            if (f.Length != 4) return false;
            for (int i = 0; i < 4; i++)
                if (!int.TryParse(f[i].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v[i])) return false;
            r = new Rectangle(v[0], v[1], v[2], v[3]);
            return true;
        }

        /// <summary>A list's columns: "width:position" each, in the columns' own order.</summary>
        public static string ColumnsOf(ListView list)
        {
            // At factor 1 (see UiScale): the list is scaled again when it opens.
            bool scaled = Kaban.UI.Controls.UiScale.IsScaled(list);
            return string.Join(",", list.Columns.Cast<ColumnHeader>().Select(c =>
                (scaled ? Kaban.UI.Controls.UiScale.Logical(c.Width) : c.Width).ToString(CultureInfo.InvariantCulture) + ":" +
                c.DisplayIndex.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>A table's columns, the same way as a list's (Find's list became a table) - ":0" after a hidden one.</summary>
        public static string ColumnsOf(DataGridView grid)
        {
            bool scaled = Kaban.UI.Controls.UiScale.IsScaled(grid);
            return string.Join(",", grid.Columns.Cast<DataGridViewColumn>().Select(c =>
                (scaled ? Kaban.UI.Controls.UiScale.Logical(c.Width) : c.Width).ToString(CultureInfo.InvariantCulture) + ":" +
                c.DisplayIndex.ToString(CultureInfo.InvariantCulture) + (c.Visible ? "" : ":0")));
        }

        /// <summary>
        /// Gives a table the widths, order and hidden columns kept by ColumnsOf.
        /// Kept for fewer columns (the table has had columns added at its end
        /// since): those are given theirs, the new ones follow them.
        /// </summary>
        public static void ApplyColumns(DataGridView grid, string text)
        {
            int[] width, position;
            bool[] shown;
            if (!ParseColumns(text, grid.Columns.Count, true, out width, out position, out shown)) return;
            for (int i = 0; i < width.Length; i++) { grid.Columns[i].Width = Math.Max(30, width[i]); grid.Columns[i].Visible = shown[i]; }
            var order = Enumerable.Range(0, width.Length).OrderBy(i => position[i]).ThenBy(i => i)
                                  .Concat(Enumerable.Range(width.Length, grid.Columns.Count - width.Length)).ToList();
            for (int k = 0; k < order.Count; k++) grid.Columns[order[k]].DisplayIndex = k;
            if (!grid.Columns.Cast<DataGridViewColumn>().Any(c => c.Visible) && grid.Columns.Count > 0) grid.Columns[0].Visible = true;
        }

        private static bool ParseColumns(string text, int count, out int[] width, out int[] position)
        {
            bool[] shown;
            return ParseColumns(text, count, false, out width, out position, out shown);
        }

        private static bool ParseColumns(string text, int count, bool fewerAllowed, out int[] width, out int[] position, out bool[] shown)
        {
            width = position = null;
            shown = null;
            if (string.IsNullOrEmpty(text)) return false;
            var f = text.Split(',');
            if (f.Length > count || (f.Length < count && !fewerAllowed)) return false;
            width = new int[f.Length];
            position = new int[f.Length];
            shown = new bool[f.Length];
            for (int i = 0; i < f.Length; i++)
            {
                var p = f[i].Split(':');
                if (p.Length < 2 || p.Length > 3 ||
                    !int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width[i]) ||
                    !int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out position[i])) return false;
                shown[i] = p.Length < 3 || p[2].Trim() != "0";
            }
            return true;
        }

        /// <summary>Gives a list the widths and order kept by ColumnsOf; a different number of columns is ignored.</summary>
        public static void ApplyColumns(ListView list, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var f = text.Split(',');
            if (f.Length != list.Columns.Count) return;
            var width = new int[f.Length];
            var position = new int[f.Length];
            for (int i = 0; i < f.Length; i++)
            {
                var p = f[i].Split(':');
                if (p.Length != 2 ||
                    !int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width[i]) ||
                    !int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out position[i])) return;
            }
            for (int i = 0; i < f.Length; i++) list.Columns[i].Width = Math.Max(30, width[i]);
            // Placed from the left, so each column lands where it was.
            var order = Enumerable.Range(0, f.Length).OrderBy(i => position[i]).ThenBy(i => i).ToList();
            for (int k = 0; k < order.Count; k++) list.Columns[order[k]].DisplayIndex = k;
        }
    }
}
