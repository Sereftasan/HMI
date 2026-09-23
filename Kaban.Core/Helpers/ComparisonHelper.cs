// Kaban.Core.Helpers/ComparisonHelper.cs
using Kaban.Core.Enums;

namespace Kaban.Core.Helpers
{
    public static class ComparisonHelper
    {
        public static bool Compare(double v1, ComparisonOperator op, double v2)
        {
            switch (op)
            {
                case ComparisonOperator.Greater: return v1 > v2;
                case ComparisonOperator.GreaterOrEqual: return v1 >= v2;
                case ComparisonOperator.Less: return v1 < v2;
                case ComparisonOperator.LessOrEqual: return v1 <= v2;
                case ComparisonOperator.Equal: return v1 == v2;
                case ComparisonOperator.NotEqual: return v1 != v2;
                default: return true;
            }
        }

        public static string GetOpSign(ComparisonOperator op)
        {
            switch (op)
            {
                case ComparisonOperator.Greater: return ">";
                case ComparisonOperator.GreaterOrEqual: return ">=";
                case ComparisonOperator.Less: return "<";
                case ComparisonOperator.LessOrEqual: return "<=";
                case ComparisonOperator.Equal: return "==";
                case ComparisonOperator.NotEqual: return "!=";
                default: return "";
            }
        }
    }
}
