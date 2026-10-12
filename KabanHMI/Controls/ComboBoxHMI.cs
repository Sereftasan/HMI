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
    /// PLC-bound COMBOBOX: each option maps a translated caption to a word VALUE.
    /// Selecting writes the value to Address; the scanner reflects the current
    /// PLC value back into the selection. Options, one per line:
    ///     TranslationKey|Fallback|Value
    /// e.g.
    ///     Md_Auto|Automatic|0
    ///     Md_Manual|Manual|1
    /// </summary>

    // The Properties window in the order of the work (see PropertyOrder).
    [System.ComponentModel.TypeDescriptionProvider(typeof(Kaban.UI.Design.PropertyOrder.Provider))]
    public class ComboBoxHMI : ComboBox
    {
        private class Opt { public string Key, Fallback; public int Value;
            public override string ToString() => LanguageManager.GetString(Key, Fallback); }

        private readonly List<Opt> _opts = new List<Opt>();
        private string _options = "";
        private bool _suppress;

        // ---- optional attached description ----
        private AttachedDescription _descr;
        private AttachedDescription Descr => _descr ?? (_descr = new AttachedDescription(this));

        [Category("   Appearance")]
        [Description("Optional description attached to the control's left edge. Enter a TRANSLATION KEY or plain text. Empty = no label.")]
        public string DescriptionText { get { return _descr?.Text ?? ""; } set { Descr.Text = value; } }

        [Category("   Data")]
        [Description("One option per line: TranslationKey|Fallback|Value")]
        [Editor("System.ComponentModel.Design.MultilineStringEditor, System.Design", typeof(System.Drawing.Design.UITypeEditor))]
        public string Options { get { return _options; } set { _options = value ?? ""; RebuildItems(); } }

        [Category("   Data")] public int PlcNumber { get; set; } = 1;
        [Category("   Data")] public string Address { get; set; } = "";
        [Category("   Behavior")] public SecurityLevel RequiredSecurity { get; set; } = SecurityLevel.Operator;
        [Category("   Behavior")] public bool LogAction { get; set; } = true;

        public ComboBoxHMI()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            Font = UiStyle.Body;
            ItemHeight = 30;   // combo height tracks ItemHeight; ~36 total like the family
            LanguageManager.LanguageChanged += OnLanguage;
        }

        private void OnLanguage(object s, EventArgs e)
        {
            int sel = SelectedIndex;
            RebuildItems();
            _suppress = true;
            if (sel >= 0 && sel < Items.Count) SelectedIndex = sel;
            _suppress = false;
        }

        private void RebuildItems()
        {
            _opts.Clear();
            Items.Clear();
            foreach (var line in _options.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                int v;
                if (parts.Length < 3 || !int.TryParse(parts[2].Trim(), out v)) continue;
                var o = new Opt { Key = parts[0].Trim(), Fallback = parts[1].Trim(), Value = v };
                _opts.Add(o);
                Items.Add(o);
            }
        }

        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            base.OnSelectedIndexChanged(e);
            if (_suppress || SelectedIndex < 0 || string.IsNullOrWhiteSpace(Address)) return;

            if (!SecurityManager.HasAccess(RequiredSecurity) || !PlcManager.Instance.CheckPermission(PlcNumber))
            {
                _suppress = true; SelectedIndex = -1; _suppress = false;   // revert; scan restores
                return;
            }
            var drv = PlcManager.Instance.GetDriverForWrite(PlcNumber);
            if (drv == null) return;
            int val = _opts[SelectedIndex].Value;
            using (Kaban.UI.Helpers.WriteSource.For(this, "chose '" + Text + "'")) drv.Write(Address, val);
            if (LogAction)
                AuditLogger.Log($"wrote {val} to {Address} (PLC {PlcNumber}) via combobox.");
        }

        /// <summary>Scanner hook - selects the option matching the PLC value.</summary>
        public void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            if (string.IsNullOrWhiteSpace(Address) || DroppedDown) return;   // don't fight the user mid-pick
            int v = readPlc(Address);
            int idx = _opts.FindIndex(o => o.Value == v);
            if (idx != SelectedIndex) { _suppress = true; SelectedIndex = idx; _suppress = false; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                LanguageManager.LanguageChanged -= OnLanguage;
                _descr?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

