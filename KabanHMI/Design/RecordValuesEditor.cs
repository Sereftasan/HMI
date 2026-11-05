using System;
using System.ComponentModel.Design;
using Kaban.UI.Controls;

namespace Kaban.UI.Design
{
    /// <summary>
    /// A log record's values (LogRecordHMI.Values): the list's Add button offers both kinds - RecordNumber (a numeric display's
    /// word or two) and RecordLamp (a lamp's bit) - each shown as "its name = its device".
    /// </summary>
    public class RecordValuesEditor : CollectionEditor
    {
        public RecordValuesEditor(Type type) : base(type) { }

        protected override Type CreateCollectionItemType() { return typeof(RecordNumber); }

        protected override Type[] CreateNewItemTypes() { return new[] { typeof(RecordNumber), typeof(RecordLamp) }; }

        protected override string GetDisplayText(object value)
        {
            var v = value as RecordValue;
            if (v == null) return base.GetDisplayText(value);
            return (v is RecordLamp ? "Lamp: " : "Number: ") + v;
        }
    }
}
