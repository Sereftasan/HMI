using System.ComponentModel;
using Kaban.Core.Enums;

namespace Kaban.Core.Models
{
    // 1. RESTORED: This fixes the "ArgumentException" in the Collection Editor
    [TypeConverter(typeof(ExpandableObjectConverter))]
    // 2. REMOVED [Serializable] so it writes to Designer.cs, not the .resx file!
    public class BitAction
    {
        public string Address { get; set; } = "";
        // MOMENTARY by default. A jog, a start, a test - almost every button on
        // these machines drops its bit when the finger lifts. SET latches a relay
        // ON and leaves it there, which is rare and is the one default that can do
        // real damage if it is chosen by accident in a collection editor.
        public BitActionType Action { get; set; } = BitActionType.Momentary;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public DynamicValue AddressOffset { get; set; } = new DynamicValue();

        public override string ToString() => $"{Action} {Address}";
    }

    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class WordAction
    {
        public string Address { get; set; } = "";

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public DynamicValue AddressOffset { get; set; } = new DynamicValue();

        [Description("Value to write. Can be Static (e.g. 50) or Tag (e.g. D100).")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public DynamicValue Value { get; set; } = new DynamicValue();

        public override string ToString() => $"{Address} = {Value}";
    }
}
