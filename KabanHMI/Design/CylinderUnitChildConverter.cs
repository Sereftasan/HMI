using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Kaban.UI.Design
{
    /// <summary>
    /// Property-grid converter for the child controls exposed by CylinderUnitHMI
    /// (ButtonA / ButtonB / PositionDisplay).
    ///
    /// Two jobs:
    ///  1. HIDE everything the CylinderUnitHMI owns - PLC station, addresses,
    ///     actions, conditions, security and geometry. A function unit is one
    ///     family of signals on ONE PLC, so a per-child PlcNumber is meaningless
    ///     and editing a child's address/condition here would silently fight the
    ///     unit's own settings (and be overwritten on the next scan).
    ///  2. ORDER what remains: HMI-specific appearance properties are shown with
    ///     a leading space so the grid sorts them to the TOP, above the ~40
    ///     inherited WinForms properties.
    ///
    /// The result: the child grids only offer per-button LOOK, while wiring and
    /// conditions live in exactly one place - the unit itself.
    /// </summary>
    public class CylinderUnitChildConverter : ExpandableObjectConverter
    {
        // Managed by CylinderUnitHMI - must not be edited on the child.
        private static readonly HashSet<string> Hidden = new HashSet<string>(StringComparer.Ordinal)
        {
            // wiring / behaviour owned by the unit
            "PlcNumber", "Address", "AddressOffset",
            "BitActions", "WordActions",
            "RelayAddress", "OutputAddress",
            "Lamp1Address", "Lamp2Address", "Lamp1Tip", "Lamp2Tip",
            "ToolTipText", "ToolTipTextKey", "TranslationKey",
            "DisplayAddress", "DisplayOffset",
            "VisibilityConditions", "EnableConditions", "EnableTrigger",
            "RequiredSecurity", "ConfirmationNeeded", "LogAction",
            "SlotDisabled", "IsActivePage",
            "Digits", "DecimalPlaces", "Divisor", "Suffix", "DataType", "DataFormat",
            // geometry - LayoutChildren owns this; serializing it caused stale
            // bounds and the floating position box
            "Location", "Size", "Bounds", "Dock", "Anchor", "Visible",
            "Left", "Top", "Width", "Height", "Margin", "Padding",
            "TabIndex", "TabStop", "Name",
        };

        // Shown first (leading space) - the properties an integrator actually tunes.
        private static readonly string[] SpecialOrder =
        {
            "ColorActive", "ColorPassive", "ActiveBackColor",
            "HoverBorderColor", "RestingBorderColor", "OutputBorderColor",
            "CornerRadius", "RoundLeftOnly", "LampDirection",
            "BlinkCondition", "BlinkColor",
            "ImageActive", "ImagePassive",
            "OnColor", "OffColor",
        };

        public override bool GetPropertiesSupported(ITypeDescriptorContext context) => true;

        public override PropertyDescriptorCollection GetProperties(
            ITypeDescriptorContext context, object value, Attribute[] attributes)
        {
            var baseProps = TypeDescriptor.GetProperties(value, attributes);
            var kept = new List<PropertyDescriptor>();

            foreach (PropertyDescriptor pd in baseProps)
            {
                if (Hidden.Contains(pd.Name)) continue;

                int idx = Array.IndexOf(SpecialOrder, pd.Name);
                if (idx >= 0)
                {
                    // leading space -> sorts above the inherited WinForms properties
                    kept.Add(new RenamedPropertyDescriptor(pd, " " + pd.Name));
                }
                else
                {
                    kept.Add(pd);
                }
            }

            // Special ones first (in the declared order), then everything else A-Z.
            kept.Sort((a, b) =>
            {
                int ia = Array.IndexOf(SpecialOrder, a.Name);
                int ib = Array.IndexOf(SpecialOrder, b.Name);
                if (ia >= 0 && ib >= 0) return ia.CompareTo(ib);
                if (ia >= 0) return -1;
                if (ib >= 0) return 1;
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal);
            });

            return new PropertyDescriptorCollection(kept.ToArray(), true);
        }

        /// <summary>Wraps a PropertyDescriptor to show a different display name.</summary>
        private class RenamedPropertyDescriptor : PropertyDescriptor
        {
            private readonly PropertyDescriptor _inner;
            private readonly string _display;

            public RenamedPropertyDescriptor(PropertyDescriptor inner, string display)
                : base(inner.Name, null)
            {
                _inner = inner;
                _display = display;
            }

            public override string DisplayName => _display;
            public override string Category => _inner.Category;
            public override string Description => _inner.Description;
            public override AttributeCollection Attributes => _inner.Attributes;
            public override Type ComponentType => _inner.ComponentType;
            public override Type PropertyType => _inner.PropertyType;
            public override bool IsReadOnly => _inner.IsReadOnly;
            public override object GetValue(object component) => _inner.GetValue(component);
            public override void SetValue(object component, object value) => _inner.SetValue(component, value);
            public override void ResetValue(object component) => _inner.ResetValue(component);
            public override bool CanResetValue(object component) => _inner.CanResetValue(component);
            public override bool ShouldSerializeValue(object component) => _inner.ShouldSerializeValue(component);
            public override TypeConverter Converter => _inner.Converter;
        }
    }
}

