using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Helpers
{
    /// <summary>
    /// A CONTROL AS THE NOTES PAGE SEES IT: the control a tap lands on (a field,
    /// a button, a lamp, a caption's own control, a column heading - not the
    /// panel around them), its name as an operator would say it ("Robot X -
    /// Min"), its place in the program (a key that is the same at every start),
    /// and the translation keys of its texts.
    /// </summary>
    public static class NoteTarget
    {
        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        // the frame's own panels (AxisPageFrame) are not part of a control's place
        private static readonly HashSet<string> Invisible = new HashSet<string>
        {
            AxisPageFrame.FrameName, "axisPageBody", "axisPageHead", "axisPageHeadContent"
        };

        /// <summary>The control a tap on <paramref name="hit"/> is about, or null (a bare panel, the window).</summary>
        public static Control Notable(Control hit)
        {
            // An HMI control made of plain ones (a value field's box, a button's
            // picture) is the control: the tap is on it, not on its box. A unit (an
            // axis row, a cylinder unit) is not - its fields are what is tapped.
            Control first = null;
            for (var c = hit; c != null; c = c.Parent)
            {
                if (c is Form) break;
                var owner = AttachedDescription.OwnerOf(c);
                if (owner != null) return owner;
                if (c is IHmiControl && !(c is AxisSettingHMI) && !(c is CylinderUnitHMI)) return c;
                if (first == null && IsNotable(c)) first = c;
            }
            return first;
        }

        private static bool IsNotable(Control c)
        {
            if (c is AxisPageFrame) return false;
            if (c is IHmiControl || c is ButtonBase || c is Label || c is ListControl || c is TextBoxBase || c is DataGridView ||
                c is TrackBar || c is ProgressBar || c is PictureBox || c is Kaban.UI.Dashboard.PickList) return true;
            var ns = c.GetType().Namespace ?? "";
            return ns == "Kaban.UI.Controls" && !(c.GetType() == typeof(Panel));
        }

        /// <summary>The unit a control is part of (an axis row, a cylinder unit), or null.</summary>
        private static Control UnitOf(Control c)
        {
            for (var p = c.Parent; p != null; p = p.Parent)
                if (p is AxisSettingHMI || p is CylinderUnitHMI) return p;
            return null;
        }

        private static string Str(object o, string property)
        {
            var p = o.GetType().GetProperty(property, BindingFlags.Instance | BindingFlags.Public);
            if (p == null || p.PropertyType != typeof(string) || p.GetIndexParameters().Length > 0) return null;
            try { return p.GetValue(o, null) as string; } catch { return null; }
        }

        /// <summary>A control's own caption, translated.</summary>
        private static string Caption(Control c)
        {
            var axis = c as AxisSettingHMI;
            if (axis != null) return T(axis.AxisNameKey ?? "", axis.AxisNameKey ?? "");
            var button = c as ButtonHMI;
            if (button != null)
            {
                var s = T(button.TranslationKey ?? "", button.FallbackText ?? "");
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            foreach (var prop in new[] { "DescriptionText", "TranslationKey", "TitleTextKey", "AxisNameKey" })
            {
                var v = Str(c, prop);
                if (!string.IsNullOrWhiteSpace(v)) return T(v, v).TrimEnd(':', ' ');
            }
            var tag = c.Tag as string;
            if (c is Label && tag != null && tag.StartsWith("AxisHeading:")) return c.Text;
            if (!string.IsNullOrWhiteSpace(c.Text)) return c.Text.Trim();
            if (!string.IsNullOrWhiteSpace(c.AccessibleName)) return c.AccessibleName;
            var tip = UiStyle.TipOf(c);
            if (!string.IsNullOrWhiteSpace(tip))
            {
                var lines = tip.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
                // the shortcut line first (Shortcuts): the function is the line after it
                if (lines.Count > 1 && Shortcuts.Of(c) != System.Windows.Forms.Keys.None) lines.RemoveAt(0);
                if (lines.Count > 0) return lines[0];
            }
            return !string.IsNullOrEmpty(c.Name) ? c.Name : c.GetType().Name;
        }

        /// <summary>The control as an operator would name it: "Robot X - Min", "Front clamping - Close", "Save".</summary>
        public static string NameOf(Control c)
        {
            if (c == null) return "";
            var unit = UnitOf(c);
            var axis = unit as AxisSettingHMI;
            if (axis != null)
            {
                var col = axis.ColumnOf(c);
                return Caption(axis) + " - " + (col != null ? AxisSettingHMI.HeadingText(col) : Caption(c));
            }
            if (unit != null) return Caption(unit) + " - " + Caption(c);
            var tag = c.Tag as string;
            if (c is Label && tag != null && tag.StartsWith("AxisHeading:"))
                return T("Nts_Heading", "Column heading") + ": " + c.Text;
            return Caption(c);
        }

        /// <summary>
        /// The control's place in the program: its page (the outermost page it is
        /// on, by type) and the way down to it by names - a nameless one by its kind
        /// and place among its kind, a field of an axis row by its column. The same
        /// at every start, in the panel or in a window of its own.
        /// </summary>
        public static string KeyOf(Control c)
        {
            if (c == null) return "";
            var segments = new List<string>();
            Control page = null;
            for (var p = c; p != null; p = p.Parent)
            {
                if (p is Form && p.Parent == null) break;
                if (p is UserControl || (p is Form && p.Parent != null)) page = p;
            }
            for (var p = c; p != null; p = p.Parent)
            {
                if (p is Form && p.Parent == null) break;
                if (Invisible.Contains(p.Name)) continue;
                if (p == page) { segments.Add(p.GetType().FullName + (string.IsNullOrEmpty(p.Name) ? "" : "." + p.Name)); break; }
                segments.Add(Segment(p));
            }
            segments.Reverse();
            return string.Join("/", segments);
        }

        private static string Segment(Control c)
        {
            if (!string.IsNullOrEmpty(c.Name)) return c.Name;
            var axis = c.Parent as AxisSettingHMI;
            if (axis != null)
            {
                var col = axis.ColumnOf(c);
                if (col != null) return "col:" + col + ":" + c.GetType().Name;
            }
            var tag = c.Tag as string;
            if (tag != null && tag.StartsWith("AxisHeading:")) return tag;
            if (c.Parent == null) return c.GetType().Name;
            var same = c.Parent.Controls.Cast<Control>().Where(x => x.GetType() == c.GetType()).ToList();
            return c.GetType().Name + "#" + same.IndexOf(c);
        }

        /// <summary>A text of the control to translate: what it is (its caption, its tooltip ...) and its key.</summary>
        public sealed class TextKey
        {
            public string What;
            public string Key;
        }

        /// <summary>
        /// The translation keys of a control's texts: every "...Key" property set
        /// (TranslationKey, ToolTipTextKey, AxisNameKey ...), the caption
        /// (DescriptionText is its own key), a column heading's key; a label or a
        /// button with none: its text, which is then the key.
        /// </summary>
        public static IList<TextKey> KeysOf(Control c)
        {
            var list = new List<TextKey>();
            if (c == null) return list;
            Action<string, string> add = (what, key) =>
            {
                if (string.IsNullOrWhiteSpace(key) || list.Any(k => k.Key == key)) return;
                list.Add(new TextKey { What = what, Key = key.Trim() });
            };
            var tag = c.Tag as string;
            if (c is Label && tag != null && tag.StartsWith("AxisHeading:"))
                add(T("Nts_KeyHeading", "Column heading"), AxisSettingHMI.HeadingKey(tag.Substring("AxisHeading:".Length)));
            var axis = c.Parent as AxisSettingHMI;
            if (axis != null)
            {
                var col = axis.ColumnOf(c);
                if (col != null) add(T("Nts_KeyHeading", "Column heading"), AxisSettingHMI.HeadingKey(col));
            }
            add(T("Nts_KeyCaption", "Caption"), Str(c, "DescriptionText"));
            foreach (var p in c.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                .Where(p => p.PropertyType == typeof(string) && p.Name.EndsWith("Key") && p.Name != "Key" && p.GetIndexParameters().Length == 0)
                                .OrderBy(p => p.Name))
            {
                string v;
                try { v = p.GetValue(c, null) as string; } catch { v = null; }
                // a caption only shown with its value (the compared position): not without it
                if (p.Name == "ComparedCaptionKey" && string.IsNullOrWhiteSpace(Str(c, "ComparedAddress"))) continue;
                add(Describe(p.Name), v);
            }
            if (list.Count == 0 && (c is Label || c is ButtonBase) && !string.IsNullOrWhiteSpace(c.Text))
                add(T("Nts_KeyText", "Its text (no key of its own)"), c.Text);
            return list;
        }

        private static string Describe(string property)
        {
            switch (property)
            {
                case "TranslationKey": return T("Nts_KeyName", "Name");
                case "ToolTipTextKey": case "TipKey": return T("Nts_KeyTip", "Tooltip");
                case "AxisNameKey": return T("Nts_KeyAxis", "Axis name");
                case "TitleTextKey": return T("Nts_KeyTitle", "Title");
                case "ComparedCaptionKey": return T("Nts_KeyCompared", "Compared value");
                default: return property.EndsWith("Key") ? property.Substring(0, property.Length - 3) : property;
            }
        }
    }
}
