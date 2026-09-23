using System;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// Unit Settings as a settings SUB-PAGE (previously a popup dialog). Lets the
    /// operator choose the display unit for each unit family; the choice is applied
    /// through UnitManager immediately, so every numeric control re-renders at once.
    /// </summary>
    public class UnitSettingsPage : ISettingsPage
    {
        public string TitleKey => "Set_Units";
        public string TitleFallback => "Unit Settings";
        public SecurityLevel RequiredLevel => SecurityLevel.Operator;

        public UserControl GetControl() => new UnitSettingsControl();
    }

    internal class UnitSettingsControl : UserControl
    {
        public UnitSettingsControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Build();
        }

        private void Build()
        {
            // No heading: the tab above names this page.

            int y = 52;
            foreach (UnitType type in Enum.GetValues(typeof(UnitType)))
            {
                if (type == UnitType.None) continue;
                var options = UnitManager.GetAvailable(type);
                if (options == null || options.Count == 0) continue;

                // RIGHT-aligned against its combo, like every other parameter page:
                // a left-aligned caption column leaves a ragged gap between each word
                // and the control it belongs to. The caption and its colon come from
                // UiStyle so all the pages punctuate identically.
                var lbl = Kaban.UI.Controls.UiStyle.CaptionLabel(
                    LanguageManager.GetString("Unit_Family_" + type, type.ToString()),
                    8, y + 2, 144);
                lbl.Font = new Font("Segoe UI", 10F);
                Controls.Add(lbl);

                var cmb = new ComboBox
                {
                    Location = new Point(160, y),
                    Width = 160,
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Font = new Font("Segoe UI", 10F),
                    DisplayMember = "Name"
                };
                // Items, NOT DataSource: binding a list installs a CurrencyManager
                // when the combo is added to the page, and THAT resets the position
                // to 0 - which is why the page always showed the first unit (mm)
                // however the machine was actually set.
                foreach (var opt in options) cmb.Items.Add(opt);

                var capturedType = type;
                _pending[capturedType] = cmb;

                Controls.Add(cmb);

                // select AFTER adding, so nothing can reset it afterwards
                var current = UnitManager.GetActive(type);
                for (int i = 0; i < cmb.Items.Count; i++)
                {
                    var opt = cmb.Items[i] as UnitDefinition;
                    if (opt != null && current != null && opt.Name == current.Name) { cmb.SelectedIndex = i; break; }
                }
                if (cmb.SelectedIndex < 0 && cmb.Items.Count > 0) cmb.SelectedIndex = 0;
                y += 44;
            }

            // Emoji-only save (label lives in the tooltip), same style as the DXF tools.
            var btnSave = new Button
            {
                Text = "\uD83D\uDCBE",   // floppy disk
                Font = new Font("Segoe UI", 12F),
                Width = 44,
                Height = 32,
                Left = 160,
                Top = y + 6,
                FlatStyle = FlatStyle.Flat
            };
            btnSave.FlatAppearance.BorderColor = Color.FromArgb(180, 186, 194);
            Kaban.UI.Controls.UiStyle.Tips().SetToolTip(btnSave, LanguageManager.GetString("Btn_Save", "Save"));
            btnSave.Click += (s, e) => ApplyPending();
            Controls.Add(btnSave);
        }

        private readonly System.Collections.Generic.Dictionary<UnitType, ComboBox> _pending
            = new System.Collections.Generic.Dictionary<UnitType, ComboBox>();

        /// <summary>Applies every staged unit choice at once (the Save button).</summary>
        private void ApplyPending()
        {
            foreach (var kv in _pending)
            {
                var chosen = kv.Value.SelectedItem as UnitDefinition;
                if (chosen != null) UnitManager.SetActive(kv.Key, chosen.Name);
            }
        }
    }
}

