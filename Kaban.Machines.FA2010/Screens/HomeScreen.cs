using System;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Machines.FA2010.Jobs;
using Kaban.UI.Controls;

namespace Kaban.Machines.FA2010.Screens
{
    public partial class HomeScreen : UserControl
    {
        /// <summary>
        /// The job opened on the Jobs page, its bars drawn (11 November: "Yes" - as the old program's home page showed the job, its bars with
        /// their cuts and accessory operations). Put in the designer's plain panel jobBarsPlace by the program (an FA2010 control in a page's
        /// designer file would have Visual Studio's designer run it - CLAUDE.md).
        /// </summary>
        private JobBarsView _jobBars;
        private Label _jobHead;

        public HomeScreen()
        {
            InitializeComponent();

            // This line does nothing at runtime, but it forces the Visual Studio Designer 
            // to load the Machine project and run the MachineConfig constructor.
            if (DesignMode || System.ComponentModel.LicenseManager.UsageMode == System.ComponentModel.LicenseUsageMode.Designtime)
            {
                MachineConfig.Initialize();
                return;
            }
            PlaceJobBars();
        }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        private void PlaceJobBars()
        {
            _jobHead = new Label { Dock = DockStyle.Top, Height = 26, Font = new Font(UiStyle.Body, FontStyle.Bold), ForeColor = Color.FromArgb(40, 50, 65),
                                   TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0) };
            _jobBars = new JobBarsView { Dock = DockStyle.Fill };
            jobBarsPlace.Controls.Add(_jobBars);
            jobBarsPlace.Controls.Add(_jobHead);
            // a bar clicked here: the Jobs page selects it (Send to the machine sends the selected one)
            _jobBars.BarClicked += i => CurrentJob.Select(i);
            Action changed = () => { if (!IsDisposed) Later(ShowJob); };
            Action selected = () => { if (!IsDisposed) Later(() => _jobBars.Selected = CurrentJob.Selected); };
            CurrentJob.Changed += changed;
            CurrentJob.SelectionChanged += selected;
            Disposed += (s, e) => { CurrentJob.Changed -= changed; CurrentJob.SelectionChanged -= selected; };
            ShowJob();
        }

        private void Later(Action a)
        {
            if (IsHandleCreated) BeginInvoke(a); else a();
        }

        /// <summary>The job's name and its bars - or where one is opened.</summary>
        internal void ShowJob()
        {
            if (_jobBars == null || _jobBars.IsDisposed) return;
            int bars = CurrentJob.Plans.Count;
            _jobHead.Text = CurrentJob.Name.Length == 0
                ? T("Home_NoJob", "No job opened - the Jobs page opens one (its bars are drawn here).")
                : string.Format(T("Home_Job", "Job {0}: {1} bar(s) - {2} can go to the machine"), CurrentJob.Name, bars, CurrentJob.Plans.FindAll(p => p.Ok).Count);
            _jobBars.SetPlans(CurrentJob.Plans, CurrentJob.Selected);
        }
    }
}
