using System;
using System.Collections.Generic;
using System.Linq;

namespace Kaban.Machines.FA2010.Jobs
{
    /// <summary>
    /// THE JOB OPENED ON THE JOBS PAGE, for the other pages (11 November: the bars drawn on the Home page too - "Yes", as the old program's
    /// home page showed the job): its name, its bars' plans, the bar selected. The Jobs page sets it; a page showing it follows its changes
    /// and may select a bar (the Jobs page selects it then).
    /// </summary>
    internal static class CurrentJob
    {
        internal static string Name { get; private set; } = "";
        internal static List<BarPlan> Plans { get; private set; } = new List<BarPlan>();
        internal static int Selected { get; private set; } = -1;
        /// <summary>Another job, or the same planned again.</summary>
        internal static event Action Changed;
        /// <summary>Another bar selected.</summary>
        internal static event Action SelectionChanged;

        internal static void Set(string name, IEnumerable<BarPlan> plans, int selected)
        {
            Name = name ?? "";
            Plans = (plans ?? new BarPlan[0]).ToList();
            Selected = Plans.Count == 0 ? -1 : Math.Max(0, Math.Min(selected, Plans.Count - 1));
            var h = Changed;
            if (h != null) h();
        }

        internal static void Select(int bar)
        {
            if (bar == Selected || bar < -1 || bar >= Plans.Count) return;
            Selected = bar;
            var h = SelectionChanged;
            if (h != null) h();
        }

        /// <summary>Nothing opened (tests).</summary>
        internal static void Clear() { Set("", null, -1); }
    }
}
