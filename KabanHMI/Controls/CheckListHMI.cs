using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Controls
{
    /// <summary>
    /// Machine option list (image-10 style): rows of "Description : [x]" bound to
    /// PLC bits, in one or more columns. One control replaces a whole screenful of
    /// label+checkbox pairs.
    ///
    /// Options are declared one per line:
    ///     TranslationKey | Fallback text | Address
    /// e.g.
    ///     Opt_TestMode|Test Mode|M300
    ///     Opt_UProfile|U Profile|M311
    /// Reading follows the scanner like every other control (UpdateState);
    /// clicking writes the bit through PlcManager with the usual permission gate.
    /// </summary>

    // The Properties window in the order of the work (see PropertyOrder).
    [System.ComponentModel.TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class CheckListHMI : Panel
    {
        private class Row { public string Key, Fallback, Address; public CheckBox Box; public Label Lbl; }
        private readonly List<Row> _rows = new List<Row>();
        private string _options = "";
        private int _columns = 1;
        private bool _suppress;   // guards against write-back while the scanner updates

        public CheckListHMI()
        {
            BackColor = Color.White;
            Size = new Size(340, 160);
            LanguageManager.LanguageChanged += OnLanguage;
        }

        [Category("   Data")]
        [Description("One option per line: TranslationKey|Fallback|Address")]
        [Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", typeof(System.Drawing.Design.UITypeEditor))]
        public string Options { get { return _options; } set { _options = value ?? ""; Rebuild(); } }

        [Category("   Data")] public int PlcNumber { get; set; } = 1;
        [Category("   Data")] public int Columns { get { return _columns; } set { _columns = Math.Max(1, value); Rebuild(); } }
        [Category("   Behavior")] public SecurityLevel RequiredSecurity { get; set; } = SecurityLevel.Operator;
        [Category("   Behavior")] public bool LogAction { get; set; } = true;

        private void OnLanguage(object s, EventArgs e)
        {
            foreach (var r in _rows) r.Lbl.Text = LanguageManager.GetString(r.Key, r.Fallback) + ":";
        }

        private void Rebuild()
        {
            foreach (var r in _rows) { r.Box.Dispose(); r.Lbl.Dispose(); }
            _rows.Clear();
            Controls.Clear();

            var lines = _options.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            const int rowH = 26, colGap = 16;
            int perCol = Math.Max(1, (int)Math.Ceiling(lines.Length / (double)_columns));
            int colW = (Width - colGap * (_columns - 1)) / Math.Max(1, _columns);

            for (int i = 0; i < lines.Length; i++)
            {
                var parts = lines[i].Split('|');
                if (parts.Length < 3) continue;
                var row = new Row { Key = parts[0].Trim(), Fallback = parts[1].Trim(), Address = parts[2].Trim() };

                int col = i / perCol, idx = i % perCol;
                int x = col * (colW + colGap), y = idx * rowH;

                row.Lbl = new Label
                {
                    AutoSize = false,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    Text = LanguageManager.GetString(row.Key, row.Fallback) + ":",
                    Location = new Point(x, y),
                    Size = new Size(colW - 28, rowH)
                };
                row.Box = new CheckBox
                {
                    AutoSize = false,
                    Size = new Size(20, rowH),
                    Location = new Point(x + colW - 24, y)   // box right-aligned in its column
                };
                var captured = row;
                row.Box.CheckedChanged += (s, e) => OnToggled(captured);

                Controls.Add(row.Lbl);
                Controls.Add(row.Box);
                _rows.Add(row);
            }
        }

        private void OnToggled(Row row)
        {
            if (_suppress) return;
            if (!SecurityManager.HasAccess(RequiredSecurity) || !PlcManager.Instance.CheckPermission(PlcNumber))
            {
                _suppress = true; row.Box.Checked = !row.Box.Checked; _suppress = false;   // revert
                return;
            }
            var drv = PlcManager.Instance.GetDriver(PlcNumber);
            if (drv == null) return;
            using (Kaban.UI.Helpers.WriteSource.For(row.Box, row.Box.Checked ? "ticked" : "unticked")) drv.Write(row.Address, row.Box.Checked ? 1 : 0);
            if (LogAction)
                AuditLogger.Log($"set option {row.Address} (PLC {PlcNumber}) = {(row.Box.Checked ? 1 : 0)}");
        }

        /// <summary>Scanner hook - reflects the PLC bits into the checkboxes.</summary>
        public void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            _suppress = true;
            try
            {
                foreach (var r in _rows)
                {
                    bool on = readPlc(r.Address) != 0;
                    if (r.Box.Checked != on) r.Box.Checked = on;
                }
            }
            finally { _suppress = false; }
        }

        /// <summary>Addresses the scanner must poll for this control.</summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IEnumerable<string> Addresses { get { foreach (var r in _rows) yield return r.Address; } }

        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); Rebuild(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing) LanguageManager.LanguageChanged -= OnLanguage;
            base.Dispose(disposing);
        }
    }
}

