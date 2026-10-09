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
                // A UNIT'S FRAME (its title and border) is the unit: its title is what
                // a tap there is about (8 October: "it doesn't find TitleTextKey for the
                // cylinder unit, e.g. 'Profile Clamping 6'")
                if (first == null && IsUnitFrame(c)) return c.Parent;
                var owner = AttachedDescription.OwnerOf(c);
                if (owner != null) return owner;
                if (c is IHmiControl && !(c is AxisSettingHMI) && !(c is CylinderUnitHMI) && !(c is CylinderRowHMI)) return c;
                if (first == null && IsNotable(c)) first = c;
            }
            return first;
        }

        /// <summary>The frame a cylinder unit draws its title and border with.</summary>
        internal static bool IsUnitFrame(Control c)
        {
            return c != null && c.Parent is CylinderUnitHMI && c is RoundedGroupBox;
        }

        private static bool IsNotable(Control c)
        {
            if (c is AxisPageFrame) return false;
            // the room kept clear over a table's names (CylinderRowHMI.StickHeading): nothing to note
            if (c.Name == CylinderRowHMI.NameCoverName) return false;
            if (c is IHmiControl || c is ButtonBase || c is Label || c is ListControl || c is TextBoxBase || c is DataGridView ||
                c is TrackBar || c is ProgressBar || c is PictureBox || c is Kaban.UI.Dashboard.PickList) return true;
            var ns = c.GetType().Namespace ?? "";
            return ns == "Kaban.UI.Controls" && !(c.GetType() == typeof(Panel));
        }

        /// <summary>The unit a control is part of (an axis row, a cylinder unit), or null.</summary>
        private static Control UnitOf(Control c)
        {
            for (var p = c.Parent; p != null; p = p.Parent)
                if (p is AxisSettingHMI || p is CylinderUnitHMI || p is CylinderRowHMI) return p;
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
            // an encoder's unit: the title it shows (its own, or its encoder row's name)
            var encoderUnit = c as CylinderEncoderUnitHMI;
            if (encoderUnit != null) return T(encoderUnit.Unit.TitleTextKey ?? "", encoderUnit.Unit.TitleTextKey ?? "");
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
            // A PART OF A CYLINDER ROW (a pneumatic positioning's, an encoder's: its
            // fields, its manual movement): the row's name and the part's column - its
            // name alone for the name itself.
            var row = unit as CylinderRowHMI ?? (unit != null ? unit.Parent as CylinderRowHMI : null);
            if (row != null && (unit == row || c == unit))
            {
                var col = row.ColumnOf(c);
                return col == null || col == "Name" ? Caption(row) : Caption(row) + " - " + row.SectionTitle(col);
            }
            if (unit != null) return Caption(unit) + " - " + Caption(c);
            var tag = c.Tag as string;
            string headingKey;
            if (c is Label && CylinderRowHMI.RowOfHeading((Label)c, out headingKey) != null)
                return T("Nts_Heading", "Column heading") + ": " + c.Text;
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
                // a glyph (◀ ▶ ▲) is no text to translate
                if (string.IsNullOrWhiteSpace(key) || !key.Any(char.IsLetter) || list.Any(k => k.Key == key)) return;
                list.Add(new TextKey { What = what, Key = key.Trim() });
            };
            var tag = c.Tag as string;
            if (c is Label && tag != null && tag.StartsWith("AxisHeading:"))
                add(T("Nts_KeyHeading", "Column heading"), AxisSettingHMI.HeadingKey(tag.Substring("AxisHeading:".Length)));
            string rowHeading = null;
            var headed = c is Label ? CylinderRowHMI.RowOfHeading((Label)c, out rowHeading) : null;
            if (headed != null) add(T("Nts_KeyHeading", "Column heading"), headed.SectionTitleKey(rowHeading));
            // the name at a cylinder row's left end: its TitleTextKey
            var named = c.Parent as CylinderRowHMI;
            if (named != null && c == named.NameLabel) { add(T("Nts_KeyTitle", "Title"), named.TitleTextKey); return list; }
            // (a field of an axis row: no texts of its own - its column's heading is
            // translated on the heading; 8 October: "the current one opens translation
            // lists for numerical values")
            add(T("Nts_KeyCaption", "Caption"), Str(c, "DescriptionText"));
            foreach (var p in c.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                .Where(p => p.PropertyType == typeof(string) && p.Name.EndsWith("Key") && p.Name != "Key" && p.GetIndexParameters().Length == 0)
                                // its name first (a unit's title, a button's text), then the rest
                                .OrderBy(p => p.Name == "TitleTextKey" || p.Name == "TranslationKey" || p.Name == "AxisNameKey" ? 0 : 1).ThenBy(p => p.Name))
            {
                string v;
                try { v = p.GetValue(c, null) as string; } catch { v = null; }
                // a caption only shown with its value (the compared position): not without it
                if (p.Name == "ComparedCaptionKey" && string.IsNullOrWhiteSpace(Str(c, "ComparedAddress"))) continue;
                add(Describe(p.Name), v);
            }
            if (list.Count == 0 && (c is Label || c is ButtonBase) && !string.IsNullOrWhiteSpace(c.Text) && !IsNumber(c.Text))
                add(T("Nts_KeyText", "Its text (no key of its own)"), c.Text);
            // a button or a lamp of a cylinder unit: the unit's title too - it names them
            var unit = UnitOf(c) as CylinderUnitHMI;
            if (unit != null) add(T("Nts_KeyUnitTitle", "Unit title"), unit.TitleTextKey);
            // ...and a part of a cylinder row: the row's name
            var row = UnitOf(c) as CylinderRowHMI;
            if (row != null) add(T("Nts_KeyUnitTitle", "Unit title"), row.TitleTextKey);
            return list;
        }

        /// <summary>A text that is a value, not words: "12.5", "-3", "0 mm", "%40".</summary>
        private static bool IsNumber(string text)
        {
            var t = (text ?? "").Trim();
            int digits = t.Count(char.IsDigit), letters = t.Count(char.IsLetter);
            return digits > 0 && letters <= 3 && digits >= letters;
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
