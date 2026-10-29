using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Forms;

namespace Kaban.UI.Helpers
{
    // ======================================================================
    // TYPING WITHOUT A KEYBOARD (29 October, the plan's 4: "an on-screen
    // keyboard matching the number pad, with Turkish letters; it opens when a
    // text box is tapped; number cells in tables use the number pad")
    // ----------------------------------------------------------------------
    // In touch mode a tap on
    //   - a text box that can be typed in (a search, a name, a note, the
    //     sign-in's password, a list one can type into): the keyboard on the
    //     screen (TouchKeyboard), the box keeps the focus;
    //   - a table's cell being edited (the table started it: a second tap on
    //     the cell, a double tap): a NUMBER cell the keypad, its value in it -
    //     OK writes it into the cell as Enter would; any other the keyboard;
    //   - a number box with arrows (NumericUpDown): the keypad, its limits.
    // The program's own number fields open the keypad themselves already.
    // ======================================================================
    public static partial class TouchInput
    {
        private static readonly ConditionalWeakTable<DataGridView, Func<DataGridViewCell, bool>> _numberCells =
            new ConditionalWeakTable<DataGridView, Func<DataGridViewCell, bool>>();

        /// <summary>
        /// Which cells of a table hold a number (a finger gets the keypad for them) - for a table whose
        /// numbers are kept as text. Cells whose type is a number are known without it.
        /// </summary>
        public static void NumberCells(DataGridView grid, Func<DataGridViewCell, bool> isNumber)
        {
            if (grid == null || isNumber == null) return;
            Func<DataGridViewCell, bool> old;
            if (_numberCells.TryGetValue(grid, out old)) _numberCells.Remove(grid);
            _numberCells.Add(grid, isNumber);
        }

        /// <summary>The columns of a table holding numbers, by name.</summary>
        public static void NumberColumns(DataGridView grid, params string[] columns)
        {
            var names = new HashSet<string>(columns ?? new string[0], StringComparer.OrdinalIgnoreCase);
            NumberCells(grid, c => c != null && c.OwningColumn != null && names.Contains(c.OwningColumn.Name));
        }

        /// <summary>Whether a cell holds a number: the table says so, or its type is one.</summary>
        public static bool IsNumberCell(DataGridView grid, DataGridViewCell cell)
        {
            if (grid == null || cell == null) return false;
            Func<DataGridViewCell, bool> says;
            if (_numberCells.TryGetValue(grid, out says)) { try { return says(cell); } catch { return false; } }
            return IsNumberType(cell.ValueType) || (cell.OwningColumn != null && IsNumberType(cell.OwningColumn.ValueType));
        }

        private static bool IsNumberType(Type t)
        {
            if (t == null) return false;
            t = Nullable.GetUnderlyingType(t) ?? t;
            switch (Type.GetTypeCode(t))
            {
                case TypeCode.Byte: case TypeCode.SByte: case TypeCode.Int16: case TypeCode.UInt16: case TypeCode.Int32: case TypeCode.UInt32:
                case TypeCode.Int64: case TypeCode.UInt64: case TypeCode.Single: case TypeCode.Double: case TypeCode.Decimal: return true;
                default: return false;
            }
        }

        private static bool IsWholeType(Type t)
        {
            if (t == null) return false;
            t = Nullable.GetUnderlyingType(t) ?? t;
            var code = Type.GetTypeCode(t);
            return code != TypeCode.Single && code != TypeCode.Double && code != TypeCode.Decimal && IsNumberType(t);
        }

        /// <summary>How the keypad is asked (tests answer it themselves): its answer, its value in pad.Value.</summary>
        public static Func<NumpadForm, IWin32Window, DialogResult> AskNumber = (pad, owner) => pad.ShowDialog(owner);

        // ---- a tap ----------------------------------------------------------------------------------

        static partial void TappedForTyping(Press p)
        {
            var c = p.Owner;
            if (c == null || c.IsDisposed) return;
            // a number box with arrows: the keypad
            var nud = Up<NumericUpDown>(c);
            if (nud != null) { if (nud.Enabled && !nud.ReadOnly) NumberBox(nud); return; }
            // a table: the cell it edits now (it may start a moment later - the second tap's own handling)
            var grid = c as DataGridView ?? GridOfEditor(c);
            if (grid != null)
            {
                Later(grid, () => Later(grid, () => GridTapped(grid)));
                return;
            }
            var text = TextTarget(c);
            if (text != null) TouchKeyboard.ShowFor(text);
        }

        private static T Up<T>(Control c) where T : Control
        {
            for (var x = c; x != null; x = x.Parent) { var t = x as T; if (t != null) return t; if (x is Form) break; }
            return null;
        }

        private static DataGridView GridOfEditor(Control c)
        {
            var editor = c as IDataGridViewEditingControl;
            return editor != null ? editor.EditingControlDataGridView : null;
        }

        /// <summary>A control a keyboard types into: a text box, a list one can type into - enabled, not read-only.</summary>
        internal static Control TextTarget(Control c)
        {
            if (c == null || !c.Enabled || !c.Visible) return null;
            if (Up<NumericUpDown>(c) != null || Up<BaseNumericHMI>(c) != null) return null;     // numbers: the keypad
            var tb = c as TextBoxBase;
            if (tb != null) return tb.ReadOnly ? null : tb;
            var combo = c as ComboBox;
            if (combo != null) return combo.DropDownStyle == ComboBoxStyle.DropDownList ? null : combo;
            return null;
        }

        [DllImport("user32.dll")] private static extern IntPtr GetFocus();

        /// <summary>The text box (or list one types into) that has the focus now; null when none.</summary>
        public static Control FocusedTextControl()
        {
            Control c = null;
            if (IsWindows)
            {
                try { var h = GetFocus(); if (h != IntPtr.Zero) c = Control.FromChildHandle(h); } catch { c = null; }
            }
            else
            {
                var f = Form.ActiveForm;
                c = f;
                for (int i = 0; i < 32 && c is ContainerControl && ((ContainerControl)c).ActiveControl != null; i++) c = ((ContainerControl)c).ActiveControl;
                if (c == f) c = null;
            }
            return c != null ? TextTarget(c) : null;
        }

        // ---- a table's cell ---------------------------------------------------------------------------

        private static void GridTapped(DataGridView grid)
        {
            if (grid.IsDisposed || !grid.IsCurrentCellInEditMode || grid.CurrentCell == null) return;
            var editor = grid.EditingControl as TextBoxBase;
            if (editor == null) return;                                // a tick, a list: their own
            if (IsNumberCell(grid, grid.CurrentCell)) NumberCell(grid);
            else TouchKeyboard.ShowFor(editor);
        }

        /// <summary>A number cell being edited: the keypad, its value in it; OK writes it in as Enter would.</summary>
        public static void NumberCell(DataGridView grid)
        {
            var cell = grid.CurrentCell;
            if (cell == null) return;
            TouchKeyboard.HideNow();
            if (!grid.IsCurrentCellInEditMode && !grid.BeginEdit(true)) return;
            var editor = grid.EditingControl as TextBoxBase;
            string initial = editor != null ? editor.Text : Convert.ToString(cell.FormattedValue, CultureInfo.CurrentCulture);
            var type = IsNumberType(cell.ValueType) ? cell.ValueType : cell.OwningColumn != null ? cell.OwningColumn.ValueType : null;
            string caption = cell.OwningColumn != null ? cell.OwningColumn.HeaderText : "";
            using (var pad = new NumpadForm(caption, initial, null, null, true, !IsWholeType(type)))
            {
                var r = grid.GetCellDisplayRectangle(cell.ColumnIndex, cell.RowIndex, false);
                pad.PlaceBeside(new Rectangle(grid.PointToScreen(r.Location), r.Size));
                var answer = AskNumber(pad, grid.FindForm());
                if (grid.IsDisposed) return;
                if (answer == DialogResult.OK && pad.Value.Length > 0)
                {
                    if (!grid.IsCurrentCellInEditMode) grid.BeginEdit(true);
                    var ed = grid.EditingControl as TextBoxBase;
                    // the separator the cell is written with (the screen's)
                    string mark = initial.Contains(",") && !initial.Contains(".") ? "," : ViewPreferences.DecimalMark;
                    string text = pad.Value.Replace(".", mark);
                    if (ed != null) ed.Text = text; else cell.Value = text;
                    grid.EndEdit();
                }
                else if (grid.IsCurrentCellInEditMode) { grid.CancelEdit(); grid.EndEdit(); }
            }
        }

        // ---- a number box with arrows -------------------------------------------------------------------

        /// <summary>A NumericUpDown tapped: the keypad, its limits and decimals; OK sets its value.</summary>
        public static void NumberBox(NumericUpDown nud)
        {
            TouchKeyboard.HideNow();
            string initial = nud.Value.ToString("F" + nud.DecimalPlaces, CultureInfo.InvariantCulture);
            using (var pad = new NumpadForm(nud.AccessibleName ?? "", initial, (double)nud.Minimum, (double)nud.Maximum,
                                            nud.Minimum < 0, nud.DecimalPlaces > 0))
            {
                pad.DecimalPlaces = nud.DecimalPlaces;
                pad.PlaceBeside(nud);
                if (AskNumber(pad, nud.FindForm()) != DialogResult.OK || nud.IsDisposed) return;
                decimal v;
                if (!decimal.TryParse(pad.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return;
                nud.Value = Math.Max(nud.Minimum, Math.Min(nud.Maximum, Math.Round(v, nud.DecimalPlaces)));
            }
        }
    }
}
