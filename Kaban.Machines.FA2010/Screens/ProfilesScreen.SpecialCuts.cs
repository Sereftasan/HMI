using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Machines.FA2010.Jobs;
using Kaban.Profiles.Data;
using Kaban.Profiles.Jobs;
using Kaban.UI.Controls;

namespace Kaban.Machines.FA2010.Screens
{
    /// <summary>
    /// THE SPECIAL CUT RECIPES ON THE PROFILES PAGE (7 November: "The special cut recipes should be edited in the profiles page btw (just like
    /// the accessories, they are defined as independent from the profiles but they have their values calculated according the machine and
    /// profile parameters dynamically with the job operation)"): a tab of the Accessories pane - the machine's recipes (the library's machine
    /// settings), the values of the profile on display under the recipe (W its Cut Full Width, T / O / D the Jobs page's cutting settings).
    /// </summary>
    public partial class ProfilesScreen
    {
        private SpecialCutsEditor _specialCuts;
        private TabControl _accTabs;

        /// <summary>The Accessories pane: its accessories, and the special cut recipes beside them.</summary>
        private Control WithSpecialCuts(Control accessories)
        {
            _accTabs = new TabControl { Dock = DockStyle.Fill, Font = UiStyle.Small, Padding = new System.Drawing.Point(12, 4) };
            var accTab = new TabPage(T("Prf_TabAccessories", "Accessories")) { BackColor = System.Drawing.Color.White };
            accTab.Controls.Add(accessories);
            var cutTab = new TabPage(T("Prf_TabSpecialCuts", "Special cuts (<>)")) { BackColor = System.Drawing.Color.White, Name = "specialCuts" };
            _specialCuts = new SpecialCutsEditor(false);
            _specialCuts.SaveHook = (list, types) =>
            {
                if (!_libraryOk || _instance <= 0 || !CanEdit) return false;
                JobSettings.SaveRecipes(_instance, list);
                JobSettings.SaveTypes(_instance, types);
                AuditLogger.Log(string.Format(CultureInfo.InvariantCulture, "profile library: the special cut recipes saved - {0} recipe(s) ({1})", list.Count,
                                              string.Join(", ", list.Select(r => r.No.ToString(CultureInfo.InvariantCulture)))));
                Status(string.Format(T("Prf_RecipesSaved", "{0} special cut recipe(s) saved."), list.Count));
                return true;
            };
            _specialCuts.LoadHook = () => _libraryOk && _instance > 0 ? JobSettings.Of(_instance).Recipes : new List<SpecialCutRecipe>();
            _specialCuts.TypesLoadHook = () => _libraryOk && _instance > 0 ? JobSettings.Of(_instance).Types : new List<SpecialCutType>();
            cutTab.Controls.Add(_specialCuts);
            _accTabs.TabPages.Add(accTab);
            _accTabs.TabPages.Add(cutTab);
            return _accTabs;
        }

        /// <summary>The recipes read from the library (the library opened, or read again) - unless some are being changed.</summary>
        private void LoadSpecialCuts()
        {
            if (_specialCuts == null || _specialCuts.Dirty) return;
            var job = _libraryOk && _instance > 0 ? JobSettings.Of(_instance) : null;
            _specialCuts.LoadRecipes(job != null ? job.Recipes : new List<SpecialCutRecipe>(), job != null ? job.Types : new List<SpecialCutType>());
            _specialCuts.ApplyAccess(CanEdit && _libraryOk);
            ShowSpecialCutValues();
        }

        /// <summary>The values the recipes' formulas take for the profile on display.</summary>
        private void ShowSpecialCutValues()
        {
            if (_specialCuts == null) return;
            if (_current == null || !_libraryOk || _instance <= 0) { _specialCuts.SetValues(null, ""); return; }
            var job = JobSettings.Of(_instance);
            var machine = Library.MachineSettings(_current.Id, _instance);
            Library.MachineValue w;
            double width = machine.TryGetValue(JobPlanner.WidthMember, out w) && w != null ? JobSettings.Num(w.Value) ?? 0 : 0;
            _specialCuts.SetValues(new FormulaValues { T = job.SawThickness ?? 0, O = job.VerticalShare, D = job.VSawsDistance, W = width }, _current.Code);
        }
    }
}
