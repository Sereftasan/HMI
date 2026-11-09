using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Kaban.Machines.FA2010.Jobs
{
    /// <summary>
    /// THE PROFILE'S MULLION NOTCHING STEPS (7 November - the answer: "Yes it should and yes values per profile"; the old program's
    /// ProfileSeryNotchSetting and TransferProvider_FXCpu.WriteNotchInfo): the saw's notching of a mullion's end, a step each line - X1 / X2
    /// (the plunge depth), Z1 / Z2 and Y1 / Y2 (the cutting range), the step and the speed - written with each bar of the profile into
    /// PC_OrtakayitKertmeAdimlari (R25700, the structure "Mullion Notching Step": 10 words a step, x100). Kept as the profile's machine
    /// setting "Mullion Notching Steps" (a profile without its own takes its group's template's, as its other machine settings).
    /// </summary>
    internal static class MullionNotch
    {
        internal const string Code = "Mullion Notching Steps";
        internal const string Label = "PC_OrtakayitKertmeAdimlari", Type = "Mullion Notching Step";
        /// <summary>The fields of a step, in the structure's order.</summary>
        internal static readonly string[] Fields = { "X1", "X2", "Z1", "Z2", "Y1", "Y2", "Step", "Speed" };
        /// <summary>The PLC's area takes 21 steps (the old program's 210 words).</summary>
        internal const int MaxSteps = 21;
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        /// <summary>The setting of a profile that has none of its own steps (its group's are not taken then).</summary>
        internal const string None = "-";

        /// <summary>"X1,X2,Z1,Z2,Y1,Y2,Step,Speed|..." - a step each, in their order ("-" or nothing: none).</summary>
        internal static List<double[]> Parse(string text)
        {
            var list = new List<double[]>();
            if (string.IsNullOrWhiteSpace(text) || text.Trim() == None) return list;
            foreach (var part in (text ?? "").Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var f = part.Split(',');
                var step = new double[Fields.Length];
                for (int i = 0; i < Fields.Length && i < f.Length; i++)
                {
                    double v;
                    if (double.TryParse(f[i].Trim(), NumberStyles.Float, CI, out v)) step[i] = v;
                }
                list.Add(step);
            }
            return list;
        }

        /// <summary>The steps as the setting keeps them ("-": none).</summary>
        internal static string Format(IEnumerable<double[]> steps)
        {
            if (steps == null || !steps.Any()) return None;
            return string.Join("|", steps.Select(s => string.Join(",", Enumerable.Range(0, Fields.Length)
                .Select(i => (i < s.Length ? s[i] : 0).ToString("0.###", CI)))));
        }
    }
}
