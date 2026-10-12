using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// What a control's PLC write is, for the PLC write log (30 September):
    ///
    ///     using (WriteSource.For(this, "momentary press")) driver.Write(...);
    ///
    /// logs "ManualProfile_Recognition.unitFront_Clamping.btnForward [Front Clamping / Forward]:
    /// momentary press" - the page, the control's name as the designer knows it
    /// (so the programmer can find it), its caption as the operator saw it, and
    /// what was done with it.
    /// </summary>
    public static class WriteSource
    {
        public static IDisposable For(Control control, string action)
        {
            return PlcWriteLog.Source(Describe(control, action));
        }

        /// <summary>A write from a page or form that is not a control of its own: "PLC Monitor: value typed in".</summary>
        public static IDisposable For(string what)
        {
            return PlcWriteLog.Source(what ?? "");
        }

        public static string Describe(Control control, string action)
        {
            if (control == null) return action ?? "";
            var names = new List<string>();
            var captions = new List<string>();
            string page = "";
            for (Control c = control; c != null; c = c.Parent)
            {
                if (c is Form) { if (page.Length == 0) page = c.GetType().Name; break; }
                bool ours = (c.GetType().Namespace ?? "").StartsWith("Kaban.UI.Controls", StringComparison.Ordinal);
                if (c != control && !ours && c is UserControl) { page = c.GetType().Name; break; }
                if (c != control && (!ours || c is GroupBoxHMI || c is PaneHostControl || c is FloatingPaneHMI)) continue;
                if (!string.IsNullOrEmpty(c.Name)) names.Insert(0, c.Name);
                var text = TextOf(c);
                if (text.Length > 0 && !captions.Contains(text)) captions.Insert(0, text);
            }
            string s = page;
            if (names.Count > 0) s += (s.Length > 0 ? "." : "") + string.Join(".", names);
            if (captions.Count > 0) s += " [" + string.Join(" / ", captions) + "]";
            if (!string.IsNullOrEmpty(action)) s += (s.Length > 0 ? ": " : "") + action;
            return s;
        }

        private static string TextOf(Control c)
        {
            // The caption as painted: a ButtonHMI keeps its Text empty and paints
            // DisplayText; a meter has a Caption; a numeric field its description.
            string t = "";
            try
            {
                foreach (var name in new[] { "Caption", "DisplayText", "DescriptionText" })
                {
                    var p = c.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (p != null && p.PropertyType == typeof(string)) t = p.GetValue(c, null) as string ?? "";
                    if (t.Trim().Length > 0) break;
                }
            }
            catch { t = ""; }
            if (t.Trim().Length == 0) t = c.Text ?? "";
            return t.Replace("\r", " ").Replace("\n", " ").Replace("  ", " ").Trim();
        }
    }
}
