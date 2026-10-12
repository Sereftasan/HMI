using System.Collections.Generic;

namespace Kaban.Core.Helpers
{
    public static class HmiInternalVariables
    {
        // Dictionary to store Name -> Value
        private static Dictionary<string, int> _variables = new Dictionary<string, int>();

        // SET a variable (e.g. SET("PageOffset", 20))
        public static void Set(string name, int value)
        {
            if (string.IsNullOrEmpty(name)) return;
            string key = name.ToUpper().Trim();
            if (_variables.ContainsKey(key)) _variables[key] = value;
            else _variables.Add(key, value);
        }

        // Get a variable (Returns 0 if not found)
        public static int Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            string key = name.ToUpper().Trim();

            if (_variables.ContainsKey(key)) return _variables[key];
            return 0;
        }
    }
}
