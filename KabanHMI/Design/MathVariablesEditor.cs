using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Linq;
using Kaban.Core.Models;

namespace Kaban.UI.Design
{
    /// <summary>
    /// THE FORMULA VARIABLES, AS THE FORMULAS SEE THEM: A first, then B, C ...
    ///
    /// A formula's A is the value itself - the raw PLC value in ReadFormula, the
    /// number typed in WriteFormula - and every other name is one of the field's
    /// MathVariables. The list used to show only the variables, so a programmer
    /// writing "(A * B) + 1" added a variable A as well (refused: A is the
    /// value), or added two items that were both named B (the default name).
    /// Either way the keypad then refused to open over the formula problem.
    ///
    /// Now the list starts with A - fixed, read only, not removable, never
    /// saved - and each item added is named with the next free letter: B, then
    /// C, then D ...
    /// </summary>
    public class MathVariablesEditor : CollectionEditor
    {
        public MathVariablesEditor(Type type) : base(type) { }

        protected override Type CreateCollectionItemType() { return typeof(MathVariable); }

        private readonly HashSet<string> _taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            _taken.Clear();
            var list = value as IEnumerable<MathVariable>;
            if (list != null)
                foreach (var v in list)
                    if (v != null && !string.IsNullOrWhiteSpace(v.Name)) _taken.Add(v.Name.Trim());
            return base.EditValue(context, provider, value);
        }

        /// <summary>The next letter after A that no variable has yet: B, C ... Z, then B2, C2 ...</summary>
        public static string NextName(ICollection<string> taken)
        {
            for (int round = 1; ; round++)
                for (char ch = 'B'; ch <= 'Z'; ch++)
                {
                    string n = round == 1 ? ch.ToString() : ch + round.ToString();
                    if (Kaban.Core.Helpers.NumericFormula.IsValueName(n)) continue;
                    if (!taken.Contains(n)) return n;
                }
        }

        protected override object CreateInstance(Type itemType)
        {
            var v = new MathVariable { Name = NextName(_taken) };
            _taken.Add(v.Name);
            return v;
        }

        protected override object[] GetItems(object editValue)
        {
            var items = base.GetItems(editValue) ?? new object[0];
            return new object[] { new ValueVariable() }.Concat(items.Where(i => !(i is ValueVariable))).ToArray();
        }

        protected override object SetItems(object editValue, object[] value)
        {
            return base.SetItems(editValue, (value ?? new object[0]).Where(v => !(v is ValueVariable)).ToArray());
        }

        protected override bool CanRemoveInstance(object value) { return !(value is ValueVariable); }

        protected override string GetDisplayText(object value)
        {
            var v = value as MathVariable;
            if (value is ValueVariable) return "A - the value itself";
            return v == null ? base.GetDisplayText(value) : v.ToString();
        }
    }

    /// <summary>
    /// "A" in the variables list: the value itself. Shown read only and never
    /// kept - the editor drops it again when the list is stored.
    /// </summary>
    public sealed class ValueVariable : MathVariable, ICustomTypeDescriptor
    {
        public const string Meaning =
            "The value itself: the raw PLC value in ReadFormula, the number typed in WriteFormula. " +
            "Also written val or x. It needs no variable - add B, C ... for the other names your formulas use.";

        public ValueVariable() { Name = "A"; }

        public override string ToString() { return "A = the value itself"; }

        // Read-only view: Name and what A means - nothing to set.
        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes) { return Props(); }
        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties() { return Props(); }

        private static PropertyDescriptorCollection Props()
        {
            return new PropertyDescriptorCollection(new PropertyDescriptor[]
            {
                new FixedText("Name", "A", "The name the formulas use."),
                new FixedText("Meaning", Meaning, "What A stands for.")
            });
        }

        AttributeCollection ICustomTypeDescriptor.GetAttributes() { return TypeDescriptor.GetAttributes(this, true); }
        string ICustomTypeDescriptor.GetClassName() { return TypeDescriptor.GetClassName(this, true); }
        string ICustomTypeDescriptor.GetComponentName() { return TypeDescriptor.GetComponentName(this, true); }
        TypeConverter ICustomTypeDescriptor.GetConverter() { return TypeDescriptor.GetConverter(this, true); }
        EventDescriptor ICustomTypeDescriptor.GetDefaultEvent() { return null; }
        PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty() { return null; }
        object ICustomTypeDescriptor.GetEditor(Type editorBaseType) { return null; }
        EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes) { return EventDescriptorCollection.Empty; }
        EventDescriptorCollection ICustomTypeDescriptor.GetEvents() { return EventDescriptorCollection.Empty; }
        object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd) { return this; }

        private sealed class FixedText : PropertyDescriptor
        {
            private readonly string _value;
            public FixedText(string name, string value, string description)
                : base(name, new Attribute[] { new DescriptionAttribute(description), ReadOnlyAttribute.Yes })
            { _value = value; }
            public override Type ComponentType { get { return typeof(ValueVariable); } }
            public override bool IsReadOnly { get { return true; } }
            public override Type PropertyType { get { return typeof(string); } }
            public override bool CanResetValue(object component) { return false; }
            public override object GetValue(object component) { return _value; }
            public override void ResetValue(object component) { }
            public override void SetValue(object component, object value) { }
            public override bool ShouldSerializeValue(object component) { return false; }
        }
    }
}
