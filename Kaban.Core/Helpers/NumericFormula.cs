using System;
using System.Collections.Generic;
using System.Linq;
using Kaban.Core.Models;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// THE NAMES A NUMERIC FIELD'S FORMULA USES, AND WHAT IS WRONG WITH THEM.
    ///
    /// A numeric input's ReadFormula turns the PLC's raw value into the number
    /// shown, its WriteFormula turns the number typed into the raw value written
    /// - "(A - 1) / B" and "(A * B) + 1". In both, A (or val, x, value) is the
    /// value itself, and every other name is one of the field's MathVariables.
    ///
    /// The formula is evaluated by NCalc, which only reports a missing name when
    /// it runs - "Parameter was not defined" at the moment the operator presses
    /// ENT, with nothing in the designer to say B still had to be added. This
    /// reads the names out of the formula beforehand, so the designer can say it
    /// (NumericInputHMI.FormulaCheck) and the error at run time can name the name.
    ///
    /// The names are matched in ANY CASE: "b" finds the variable B. NCalc itself
    /// matches parameters exactly, so each name is bound as it is written.
    /// </summary>
    public static class NumericFormula
    {
        /// <summary>The names that stand for the value itself.</summary>
        public static readonly string[] ValueNames = { "A", "val", "value", "x" };

        // NCalc's word operators and literals - not names.
        private static readonly HashSet<string> Words = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "and", "or", "not", "true", "false" };

        /// <summary>True when the formula leaves the value alone: blank, "val", "A", "x" or "value".</summary>
        public static bool IsIdentity(string formula)
        {
            if (string.IsNullOrWhiteSpace(formula)) return true;
            string f = formula.Trim();
            if (f.StartsWith("[") && f.EndsWith("]")) f = f.Substring(1, f.Length - 2).Trim();
            return IsValueName(f);
        }

        /// <summary>A (val, x, value) - the value itself, in any case.</summary>
        public static bool IsValueName(string name)
        {
            return ValueNames.Any(v => string.Equals(v, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The names the formula uses as values, as written, each once - not the
        /// functions it calls ("Round(A / B, 1)" uses A and B), not the text in
        /// quotes, not the "e" of a number such as 1e3.
        /// </summary>
        public static List<string> Names(string formula)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(formula)) return names;
            string s = formula;
            int i = 0;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\'' || c == '#')
                {
                    // A text or a date literal: skip to its end.
                    int end = s.IndexOf(c, i + 1);
                    i = end < 0 ? s.Length : end + 1;
                    continue;
                }
                if (c == '[')
                {
                    int end = s.IndexOf(']', i + 1);
                    if (end < 0) { i = s.Length; continue; }
                    Add(names, s.Substring(i + 1, end - i - 1).Trim());
                    i = end + 1;
                    continue;
                }
                if (char.IsDigit(c) || c == '.')
                {
                    // A number, with its exponent: 12.5, 1e3, 2.5E-4.
                    i++;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '.' ||
                                            ((s[i] == '-' || s[i] == '+') && (s[i - 1] == 'e' || s[i - 1] == 'E'))))
                        i++;
                    continue;
                }
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                    string word = s.Substring(start, i - start);
                    int j = i;
                    while (j < s.Length && char.IsWhiteSpace(s[j])) j++;
                    bool isFunction = j < s.Length && s[j] == '(';
                    if (!isFunction && !Words.Contains(word)) Add(names, word);
                    continue;
                }
                i++;
            }
            return names;
        }

        private static void Add(List<string> names, string n)
        {
            if (n.Length > 0 && !names.Contains(n)) names.Add(n);
        }

        /// <summary>The variable a name stands for, in any case - null when there is none.</summary>
        public static MathVariable Find(IEnumerable<MathVariable> vars, string name)
        {
            if (vars == null) return null;
            return vars.FirstOrDefault(v => v != null && !string.IsNullOrWhiteSpace(v.Name) &&
                                            string.Equals(v.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The names the formula uses that are neither the value nor a variable.</summary>
        public static List<string> Undefined(string formula, IEnumerable<MathVariable> vars)
        {
            return Names(formula).Where(n => !IsValueName(n) && Find(vars, n) == null)
                                 .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>"B is not defined: add a MathVariable named B ..." - null when every name is known.</summary>
        public static string UndefinedMessage(string formula, IEnumerable<MathVariable> vars)
        {
            var missing = Undefined(formula, vars);
            if (missing.Count == 0) return null;
            string list = string.Join(", ", missing);
            return missing.Count == 1
                ? list + " is not defined: add a MathVariable named " + list +
                  " (a constant, or a PLC device to read). A, val and x are the value itself."
                : list + " are not defined: add MathVariables with these names (constants, or PLC devices to read). " +
                  "A, val and x are the value itself.";
        }

        /// <summary>
        /// What is wrong with the variables themselves: a name missing, used twice,
        /// or one of the value's own names; a device to read with no address.
        /// </summary>
        public static List<string> VariableProblems(IEnumerable<MathVariable> vars)
        {
            var problems = new List<string>();
            if (vars == null) return problems;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in vars)
            {
                if (v == null) continue;
                string n = (v.Name ?? "").Trim();
                if (n.Length == 0) { problems.Add("A MathVariable has no Name."); continue; }
                if (IsValueName(n)) problems.Add("A MathVariable is named " + n + ", which is the value itself - name it B, C ...");
                else if (!seen.Add(n)) problems.Add("Two MathVariables are named " + n + ".");
                if (!v.IsConstant && string.IsNullOrWhiteSpace(v.AddressOrValue))
                    problems.Add(n + " reads a PLC device (IsConstant false) but has no AddressOrValue.");
            }
            return problems;
        }
    }

    /// <summary>A formula that cannot be worked out - its message says why, in plain words.</summary>
    public class FormulaException : Exception
    {
        public FormulaException(string message, Exception inner = null) : base(message, inner) { }
    }
}
