
namespace Kaban.Core.Enums
{
    public enum TriggerType { None, OFF, ON, Range }
    public enum BitActionType { Momentary, Alternate, SET, RESET }
    public enum ComparisonOperator { None, Equal, NotEqual, Less, LessOrEqual, Greater, GreaterOrEqual }
    public enum DisplayFormatEnum { Signed_Decimal, Unsigned_Decimal, Hexadecimal, Octal, Binary, Real }
    public enum DataTypeEnum { Bin_16, Bin_32 }
    public enum LimitSourceType {None, Static, Tag, HmiVariable }
    public enum UnitType { None = 0, Length, Weight, Temperature, Pressure, Time }
}
