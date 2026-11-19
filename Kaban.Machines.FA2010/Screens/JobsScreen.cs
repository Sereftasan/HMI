using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Machines.FA2010.Jobs;
using Kaban.Profiles.Data;
using Kaban.Profiles.Jobs;
using Kaban.UI.Controls;
using Kaban.UI.Profiles;

namespace Kaban.Machines.FA2010.Screens
{
    /// <summary>
    /// THE JOB LIST (6 November: "They are send to the machine when the 'Send to the machine' button clicked for the job on the job list
    /// (we haven't got the job list on the program yet though, you can create it now using our old program ...)").
    ///
    /// A job file (.kbn, the window software's - KbnFile) opened here: its bars, the selected bar's pieces, and the job list the PLC gets for
    /// it - every cut and milling operation with its ID, position and code, 999999 after the last (JobPlanner, the old program's cut
    /// positions) - with what keeps a bar from going (an unknown profile, an accessory its group does not have, a placement nobody has
    /// confirmed). "Send to the machine" writes the bar: the profile's parameters, the milling operations', the job list (JobSender).
    ///
    /// The cutting settings (the saw blade, its vertical share, from what length a leftover is squared, the longest piece the machine takes)
    /// are the machine's, kept in the profile library - changed at the Maintenance level. Sending needs the Operator level; a TRIAL (milling
    /// positions nobody has confirmed, for a test piece) the Maintenance level.
    /// </summary>
    public class JobsScreen : UserControl
    {
        /// <summary>The PLC the job goes to.</summary>
        internal const int Station = 1;
        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;

        /// <summary>The jobs opened lately, and the bars sent (C:\ProgramData\KabanHMI\Jobs.cfg).</summary>
        internal static string StatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "KabanHMI", "Jobs.cfg");

        private readonly ToolTip _tips = UiStyle.Tips();
        private readonly Label _lblStatus = new Label();
        private readonly ComboBox _cmbRecent = new ComboBox();
        private readonly NumericUpDown _numSaw = Num(0, 20), _numShare = Num(-50, 50), _numSquare = Num(0, 9999), _numLongest = Num(0, 99999), _numVSaws = Num(0, 999);
        private readonly CheckBox _chkTrial = new CheckBox();
        private readonly CheckBox _chkAuto = new CheckBox { Name = "jobAutomatic" };
        private Button _btnStopRun;
        private Button _btnSend, _btnOpen, _btnReload, _btnInbox;
        private readonly DataGridView _bars = new DataGridView(), _pieces = new DataGridView(), _steps = new DataGridView();
        private readonly TextBox _notes = new TextBox();
        /// <summary>The job's bars drawn (10 November: "it draws a bar image for each job bar with the cut and accessory operation points marked").</summary>
        private readonly JobBarsView _pictures = new JobBarsView { Dock = DockStyle.Fill };
        private readonly Label _lblBar = new Label();
        private bool _filling, _libraryTried, _libraryOk;
        private string _libraryWhy = "";
        private long _instance;

        internal KbnJob Job;
        internal List<BarPlan> Plans = new List<BarPlan>();
        /// <summary>Bars sent: the job file's path -> bar index -> when.</summary>
        private readonly Dictionary<string, Dictionary<int, DateTime>> _sent = new Dictionary<string, Dictionary<int, DateTime>>(StringComparer.OrdinalIgnoreCase);
        /// <summary>The bars the machine has finished (the PLC asked for the next one after them - JobRun), by job file.</summary>
        private readonly Dictionary<string, Dictionary<int, DateTime>> _finished = new Dictionary<string, Dictionary<int, DateTime>>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _recent = new List<string>();
        private EventHandler _onUserChanged;

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }
        private static bool CanSet { get { return SecurityManager.HasAccess(SecurityLevel.Maintenance); } }
        private static bool CanSend { get { return SecurityManager.HasAccess(SecurityLevel.Operator); } }

        private static NumericUpDown Num(decimal min, decimal max)
        {
            return new NumericUpDown { Minimum = min, Maximum = max, DecimalPlaces = 2, Increment = 0.1M, Width = 92, Font = UiStyle.Small };
        }

        public JobsScreen()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;
            Padding = new Padding(8, 0, 8, 8);
            LoadState();

            // ---- two rows: the job and sending | the cutting settings -------------------------------------------
            var top = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.White };
            var left = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 6, 0, 0), BackColor = Color.White };
            var send = new FlowLayoutPanel { Dock = DockStyle.Right, WrapContents = false, AutoSize = true, FlowDirection = FlowDirection.RightToLeft,
                                             Padding = new Padding(0, 6, 0, 0), BackColor = Color.White };
            _btnOpen = UiStyle.ToolButton("\uD83D\uDCC2", T("Job_TipOpen", "Open a job file (.kbn) - the window software's job list for the machine"), _tips, (s, e) => OpenFile());
            left.Controls.Add(_btnOpen);
            _cmbRecent.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbRecent.Width = 340;
            _cmbRecent.Font = UiStyle.Small;
            _cmbRecent.Margin = new Padding(6, 5, 6, 0);
            _cmbRecent.SelectedIndexChanged += (s, e) =>
            {
                if (_filling || _cmbRecent.SelectedIndex < 0 || _cmbRecent.SelectedIndex >= _recent.Count) return;
                string p = _recent[_cmbRecent.SelectedIndex];
                if (Job == null || !string.Equals(Job.Path, p, StringComparison.OrdinalIgnoreCase)) OpenJob(p);
            };
            _tips.SetToolTip(_cmbRecent, T("Job_TipRecent", "The jobs opened lately"));
            left.Controls.Add(_cmbRecent);
            _btnReload = UiStyle.ToolButton("\uD83D\uDD04", T("Job_TipReload", "Read the job file and the profile library again"), _tips, (s, e) => { if (Job != null) OpenJob(Job.Path); });
            left.Controls.Add(_btnReload);
            // 17 November - the old program's transfer folder (KabanFileSystemWatcher): the window software's new job files come here
            _btnInbox = UiStyle.ToolButton("\uD83D\uDCE5", T("Job_TipInbox", "The transfer folder: the job files the window software writes there come to the top of the list"), _tips,
                                           (s, e) => InboxMenu().Show(_btnInbox, new Point(0, _btnInbox.Height)));
            _btnInbox.Name = "jobInbox";
            left.Controls.Add(_btnInbox);

            _btnSend = UiStyle.TextButton("\u25B6 " + T("Job_Send", "Send to the machine"), 0, (s, e) => SendSelected());
            _btnSend.Font = new Font(UiStyle.Body, FontStyle.Bold);
            _btnSend.Width = TextRenderer.MeasureText(_btnSend.Text, _btnSend.Font).Width + 36;
            _tips.SetToolTip(_btnSend, T("Job_TipSend", "Write the selected bar into the PLC: the profile's parameters, its milling operations, the job list"));
            send.Controls.Add(_btnSend);
            _chkTrial.Text = T("Job_Trial", "Trial");
            _chkTrial.AutoSize = true;
            _chkTrial.Font = UiStyle.Small;
            _chkTrial.Margin = new Padding(6, 9, 10, 0);
            _chkTrial.CheckedChanged += (s, e) => UpdateSend();
            _tips.SetToolTip(_chkTrial, T("Job_TipTrial",
                "Send milling positions nobody has confirmed on the Profiles page - for a test piece only (the Maintenance level)"));
            send.Controls.Add(_chkTrial);
            // 19 November - the old FA2010's job screen: the bars one after the other as the machine asks for them (JobRun)
            _chkAuto.Text = T("Job_Automatic", "Automatic");
            _chkAuto.AutoSize = true;
            _chkAuto.Font = UiStyle.Small;
            _chkAuto.Margin = new Padding(6, 9, 4, 0);
            _tips.SetToolTip(_chkAuto, T("Job_TipAutomatic",
                "Send: the selected bar now, then each next bar of the job not sent yet when the machine asks for it (Is Gonder L0, while Makine Aktif M21 is on)"));
            send.Controls.Add(_chkAuto);
            _btnStopRun = UiStyle.TextButton("\u25A0 " + T("Job_StopRun", "Stop automatic"), 0, (s, e) => JobRun.Stop(T("Job_RunStopped", "stopped by the operator")));
            _btnStopRun.Name = "jobStopRun";
            _btnStopRun.Width = TextRenderer.MeasureText(_btnStopRun.Text, _btnStopRun.Font).Width + 30;
            _btnStopRun.Visible = false;
            _tips.SetToolTip(_btnStopRun, T("Job_TipStopRun", "No further bar is sent: the bar in the machine is its last (L10 off)"));
            send.Controls.Add(_btnStopRun);
            top.Controls.Add(left);
            top.Controls.Add(send);

            var cutting = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 34, WrapContents = false, BackColor = Color.FromArgb(246, 247, 249), Padding = new Padding(4, 4, 0, 0) };
            cutting.Controls.Add(new Label { Text = T("Job_Cutting", "Cutting:"), AutoSize = true, Font = new Font(UiStyle.Small, FontStyle.Bold), Margin = new Padding(2, 6, 8, 0) });
            Action<string, string, NumericUpDown, string> setting = (key, text, num, tip) =>
            {
                cutting.Controls.Add(new Label { Text = T(key, text), AutoSize = true, Font = UiStyle.Small, ForeColor = Color.FromArgb(70, 80, 95), Margin = new Padding(8, 6, 2, 0) });
                num.Margin = new Padding(0, 2, 6, 0);
                cutting.Controls.Add(num);
                _tips.SetToolTip(num, T(key.Replace("Job_", "Job_Tip"), tip));
                num.ValueChanged += (s, e) => SettingsChanged();
            };
            setting("Job_Saw", "Saw blade mm", _numSaw, "The main saw blade's thickness: half of it, at the cut's angle, on each side of every cut");
            setting("Job_Share", "Vertical share mm", _numShare, "The saw's vertical share (the old program's): added at an angled cut over its tangent");
            setting("Job_Square", "Square a leftover from mm", _numSquare, "A leftover this long or longer is squared (a 90 degree cut) when the last cut is not");
            setting("Job_Longest", "Longest piece mm", _numLongest, "A longer leftover is cut into pieces the machine can take (0: no limit)");
            _numLongest.DecimalPlaces = 0;
            _numLongest.Increment = 100;
            _numSquare.DecimalPlaces = 0;
            _numSquare.Increment = 50;
            // the device list's "Kırlangıç Kesim" sheet: D "Testere - V_Kesim merkez mesafesi" - the special cut recipes' D (the recipes: the Profiles page)
            setting("Job_SawToVCut", "Saw - V cut distance mm", _numVSaws, "The distance between the saw and the V cut's centre - the special cut recipes' D (the recipes are on the Profiles page)");

            // ---- the bars | the bar's pieces, its job list and notes --------------------------------------------
            GridOf(_bars, new[] { "bar", "profile", "state", "pieces", "accessories", "length", "leftover", "name", "colour" },
                   new[] { T("Job_ColBar", "Bar"), T("Job_ColProfile", "Profile"), T("Job_ColState", "State"), T("Job_ColPieces", "Pieces"),
                           T("Job_ColAccessories", "Accessories"), T("Job_ColLength", "Length"), T("Job_ColLeftover", "Leftover"),
                           T("Job_ColName", "Name"), T("Job_ColColour", "Colour") },
                   new[] { 56, 104, 160, 50, 78, 60, 66, 190, 70 });
            _bars.SelectionChanged += (s, e) => { if (!_filling) ShowBar(); };
            GridOf(_pieces, new[] { "no", "length", "left", "right", "position", "order", "customer", "barcode", "accessories" },
                   new[] { "#", T("Job_ColLength", "Length"), T("Job_ColLeft", "Left \u00B0"), T("Job_ColRight", "Right \u00B0"), T("Job_ColPosition", "Position"),
                           T("Job_ColOrder", "Order"), T("Job_ColCustomer", "Customer"), T("Job_ColBarcode", "Barcode"), T("Job_ColAccessories", "Accessories") },
                   new[] { 36, 70, 56, 56, 70, 90, 140, 150, 220 });
            GridOf(_steps, new[] { "id", "position", "code", "piece", "what" },
                   new[] { T("Job_ColId", "ID (D5000)"), T("Job_ColPos", "Position (D6000)"), T("Job_ColCode", "Code (D7000)"), T("Job_ColPiece", "Piece"), T("Job_ColWhat", "What") },
                   new[] { 84, 110, 96, 50, 380 });
            foreach (var c in new[] { "length", "pieces", "accessories", "leftover" }) _bars.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            foreach (var c in new[] { "length", "left", "right" }) _pieces.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            foreach (var c in new[] { "id", "position", "code" }) _steps.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _notes.Multiline = true;
            _notes.ReadOnly = true;
            _notes.ScrollBars = ScrollBars.Vertical;
            _notes.Dock = DockStyle.Fill;
            _notes.Font = UiStyle.Small;
            _notes.BackColor = Color.FromArgb(250, 250, 251);
            _lblBar.Dock = DockStyle.Top;
            _lblBar.Height = 26;
            _lblBar.Font = new Font(UiStyle.Body, FontStyle.Bold);
            _lblBar.ForeColor = Color.FromArgb(40, 50, 65);
            _lblBar.TextAlign = ContentAlignment.MiddleLeft;

            var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterWidth = 6 };
            var right = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
            var lower = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
            split.Panel1.Controls.Add(_bars);
            right.Panel1.Controls.Add(_pieces);
            right.Panel1.Controls.Add(_lblBar);
            lower.Panel1.Controls.Add(_steps);
            lower.Panel2.Controls.Add(_notes);
            right.Panel2.Controls.Add(lower);
            split.Panel2.Controls.Add(right);
            // above them, across the page: every bar of the job drawn - a click on one selects it
            var pictures = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterWidth = 6 };
            pictures.Panel1.Controls.Add(_pictures);
            pictures.Panel2.Controls.Add(split);
            _pictures.BarClicked += i => { if (i != SelectedIndex) Select(i); };
            Controls.Add(pictures);
            Controls.Add(cutting);
            Controls.Add(top);
            _lblStatus.Dock = DockStyle.Bottom;
            _lblStatus.Height = 24;
            _lblStatus.Font = UiStyle.Small;
            _lblStatus.ForeColor = Color.FromArgb(70, 76, 86);
            Controls.Add(_lblStatus);
            Proportional(pictures, 0.42);
            Proportional(split, 0.40);
            Proportional(right, 0.34);
            Proportional(lower, 0.70);

            _onUserChanged = (s, e) => { if (IsHandleCreated) BeginInvoke(new Action(ApplyAccess)); };
            SecurityManager.UserChanged += _onUserChanged;
            Disposed += (s, e) => SecurityManager.UserChanged -= _onUserChanged;
            // a bar selected on another page (the Home page's drawing): selected here
            Action onSelected = () =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                int bar = CurrentJob.Selected;
                if (bar >= 0 && bar != SelectedIndex) BeginInvoke((Action)(() => Select(bar)));
            };
            CurrentJob.SelectionChanged += onSelected;
            Disposed += (s, e) => CurrentJob.SelectionChanged -= onSelected;
            // the library opened when the page is first shown (never while the program starts), the last job with it
            VisibleChanged += (s, e) => { if (Visible) EnsureLibrary(); };
            FillRecent();
            ApplyAccess();
            // the milling units' requests for the steps of the bar sent last - answered from the start, the page shown or not
            JobMilling.Start();
            // the automatic run's bars sent and finished, what it says (any thread)
            Action<int> runSent = i => OnUi(() => RunBarSent(i));
            Action<int, double> runFinished = (i, sec) => OnUi(() => RunBarFinished(i, sec));
            Action<string, bool> runSaid = (text, bad) => OnUi(() => { Status(T("Job_RunPrefix", "Automatic:") + " " + text, bad); ShowRun(); });
            JobRun.BarSent += runSent;
            JobRun.BarFinished += runFinished;
            JobRun.Said += runSaid;
            Disposed += (s, e) => { JobRun.BarSent -= runSent; JobRun.BarFinished -= runFinished; JobRun.Said -= runSaid; };
            // the transfer folder watched (its new job files to the top of the list, said in a note)
            Action<string> arrived = path => { try { if (IsHandleCreated && !IsDisposed) BeginInvoke((Action)(() => JobArrived(path))); } catch { } };
            JobInbox.Arrived += arrived;
            Disposed += (s, e) => JobInbox.Arrived -= arrived;
            JobInbox.Start();
            Status(T("Job_Hint", "Open a job file (.kbn) - the bars, their pieces and what each sends to the machine are shown."));
        }

        /// <summary>A split that keeps its proportion as the page is sized (and the one the operator drags it to).</summary>
        private static void Proportional(SplitContainer sc, double ratio)
        {
            bool setting = false;
            Action apply = () =>
            {
                int size = sc.Orientation == Orientation.Vertical ? sc.Width : sc.Height;
                if (size < 60) return;
                setting = true;
                try { sc.SplitterDistance = Math.Max(sc.Panel1MinSize, Math.Min(size - sc.Panel2MinSize - sc.SplitterWidth, (int)(size * ratio))); }
                catch { }
                finally { setting = false; }
            };
            sc.SizeChanged += (s, e) => apply();
            sc.SplitterMoved += (s, e) =>
            {
                int size = sc.Orientation == Orientation.Vertical ? sc.Width : sc.Height;
                if (!setting && size >= 60) ratio = (double)sc.SplitterDistance / size;
            };
            apply();
        }

        private static void GridOf(DataGridView g, string[] names, string[] headers, int[] widths)
        {
            g.Dock = DockStyle.Fill;
            g.AllowUserToAddRows = false;
            g.AllowUserToDeleteRows = false;
            g.AllowUserToResizeRows = false;
            g.ReadOnly = true;
            g.RowHeadersVisible = false;
            g.MultiSelect = false;
            g.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            UiStyle.StyleGrid(g);
            for (int i = 0; i < names.Length; i++)
                g.Columns.Add(new DataGridViewTextBoxColumn { Name = names[i], HeaderText = headers[i], Width = widths[i], SortMode = DataGridViewColumnSortMode.NotSortable });
        }

        private void Status(string text, bool bad = false)
        {
            _lblStatus.Text = text ?? "";
            _lblStatus.ForeColor = bad ? Color.FromArgb(180, 40, 40) : Color.FromArgb(70, 76, 86);
        }

        // =====================================================================
        // THE LIBRARY, THE SETTINGS
        // =====================================================================

        internal void EnsureLibrary()
        {
            if (_libraryTried) return;
            _libraryTried = true;
            string why = "";
            // a new library asks what the Profiles page asks (whichever page opens it first)
            _libraryOk = Library.IsReady || Library.Open(() => Ask.YesNo(FindForm(), T("Prf_Title", "Profiles"),
                T("Prf_AskExamples", "The profile library is new. Load the example catalogue?\r\n\r\n" +
                  "It holds hardware of several makers with EXAMPLE positions, and the three profiles this page had. " +
                  "Nothing in it is confirmed, and nothing unconfirmed goes into a production program.")), out why);
            _libraryWhy = why ?? "";
            if (_libraryOk)
            {
                try { _instance = Library.EnsureMachine("FA2010", "FA2010", ProfilesScreen.ToolPlaces, ProfilesScreen.MachineFaces()); }
                catch (Exception ex) { _libraryOk = false; _libraryWhy = ex.Message; }
            }
            FillSettings();
            if (Job == null && _recent.Count > 0 && File.Exists(_recent[0])) OpenJob(_recent[0]);
            else if (!_libraryOk) Status(T("Prf_NoLibrary", "The profile library could not be opened:") + " " + _libraryWhy, true);
        }

        private JobSettings Settings { get { return _libraryOk ? JobSettings.Of(_instance) : new JobSettings(); } }

        private void FillSettings()
        {
            _filling = true;
            try
            {
                var s = Settings;
                _numSaw.Value = Clamp(_numSaw, s.SawThickness ?? 0);
                _numShare.Value = Clamp(_numShare, s.VerticalShare);
                _numSquare.Value = Clamp(_numSquare, s.SquareFrom);
                _numLongest.Value = Clamp(_numLongest, s.Longest);
                _numVSaws.Value = Clamp(_numVSaws, s.VSawsDistance);
            }
            finally { _filling = false; }
        }

        private static decimal Clamp(NumericUpDown n, double v)
        {
            var d = (decimal)Math.Round(v, n.DecimalPlaces);
            return Math.Max(n.Minimum, Math.Min(n.Maximum, d));
        }

        private void SettingsChanged()
        {
            if (_filling || !_libraryOk) return;
            if (!CanSet) { FillSettings(); return; }
            var s = new JobSettings
            {
                SawThickness = _numSaw.Value > 0 ? (double)_numSaw.Value : (double?)null, VerticalShare = (double)_numShare.Value,
                SquareFrom = (double)_numSquare.Value, Longest = (double)_numLongest.Value, VSawsDistance = (double)_numVSaws.Value
            };
            s.Save(_instance);
            AuditLogger.Log(string.Format(CI, "[JOB] the cutting settings changed: saw blade {0} mm, vertical share {1} mm, square a leftover from {2} mm, longest piece {3} mm, V saws {4} mm",
                _numSaw.Value, _numShare.Value, _numSquare.Value, _numLongest.Value, _numVSaws.Value));
            Replan();
        }

        private void ApplyAccess()
        {
            foreach (var n in new[] { _numSaw, _numShare, _numSquare, _numLongest, _numVSaws }) n.Enabled = CanSet;
            _chkTrial.Enabled = CanSet;
            if (!CanSet) _chkTrial.Checked = false;
            UpdateSend();
        }

        // =====================================================================
        // THE JOB
        // =====================================================================

        private void OpenFile()
        {
            using (var d = new OpenFileDialog { Filter = T("Job_Filter", "Job files (*.kbn)|*.kbn|All files (*.*)|*.*"), Title = T("Job_TipOpen", "Open a job file (.kbn)") })
            {
                if (Job != null && File.Exists(Job.Path)) d.InitialDirectory = Path.GetDirectoryName(Job.Path);
                if (d.ShowDialog(FindForm()) == DialogResult.OK) OpenJob(d.FileName);
            }
        }

        /// <summary>Reads the job file, lays out every bar, shows it.</summary>
        internal void OpenJob(string path)
        {
            JobInbox.Opened(path);
            EnsureLibrary();
            try { Job = KbnFile.Read(path); }
            catch (Exception ex)
            {
                Status(string.Format(T("Job_ReadFailed", "The job file could not be read: {0}"), ex.Message), true);
                return;
            }
            _recent.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            _recent.Insert(0, path);
            while (_recent.Count > 12) _recent.RemoveAt(_recent.Count - 1);
            SaveState();
            FillRecent();
            AuditLogger.Log("[JOB] opened " + path + ": " + Job.Bars.Count + " bar(s)" + (Job.Problems.Count > 0 ? ", " + Job.Problems.Count + " line(s) not read" : ""));
            Replan();
        }

        /// <summary>A job file arrived in the transfer folder: at the top of the list, said; opened when no job is open.</summary>
        internal void JobArrived(string path)
        {
            _recent.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            _recent.Insert(0, path);
            while (_recent.Count > 12) _recent.RemoveAt(_recent.Count - 1);
            SaveState();
            string text = string.Format(T("Job_Arrived", "New job received: {0}"), Path.GetFileName(path));
            if (Job == null) OpenJob(path);
            else FillRecent();
            Status(text);
            try { if (Visible && FindForm() != null) Toast.Show(FindForm(), text); } catch { }
        }

        /// <summary>The transfer folder's menu: its new files, setting it (Maintenance), opening it.</summary>
        private ContextMenuStrip InboxMenu()
        {
            var menu = UiStyle.Menu();
            string folder = JobInbox.Folder;
            menu.Items.Add(UiStyle.MenuHeader(folder.Length > 0 ? folder : T("Job_InboxNone", "No transfer folder set")));
            var files = JobInbox.New;
            foreach (var f in files.Take(15))
            {
                var path = f;
                DateTime at = DateTime.MinValue;
                try { at = File.GetLastWriteTime(f); } catch { }
                menu.Items.Add(Path.GetFileName(f) + "   " + at.ToString("dd.MM HH:mm", CI), null, (s, e) => OpenJob(path));
            }
            if (folder.Length > 0 && files.Count == 0)
                menu.Items.Add(new ToolStripMenuItem(T("Job_InboxEmpty", "No new job file")) { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(UiStyle.Unavailable(new ToolStripMenuItem(T("Job_InboxSet", "Set the transfer folder..."), null, (s, e) => PickInbox()),
                                               CanSet, T("Job_InboxLevel", "The Maintenance level sets it")));
            if (folder.Length > 0)
            {
                menu.Items.Add(T("Job_InboxOpen", "Open the folder"), null, (s, e) =>
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true }); } catch { }
                });
                menu.Items.Add(T("Job_InboxClear", "Watch no folder"), null, (s, e) => { if (CanSet) JobInbox.SetFolder(""); });
                // the old ones into the folder's Archive (the old KabanTransferCleaner)
                var arch = new ToolStripMenuItem(T("Job_InboxArchive", "Old job files into its Archive"));
                foreach (var d in new[] { 0, 7, 30, 90 })
                {
                    int days = d;
                    var it = new ToolStripMenuItem(days == 0 ? T("Job_InboxArchiveNever", "never") : string.Format(T("Job_InboxArchiveDays", "older than {0} days"), days),
                                                   null, (s, e) => { if (CanSet) { JobInbox.SetArchiveDays(days); JobInbox.ArchiveOld(DateTime.Now); } })
                    { Checked = JobInbox.ArchiveDays == days, Enabled = CanSet };
                    arch.DropDownItems.Add(it);
                }
                menu.Items.Add(arch);
            }
            return menu;
        }

        private void PickInbox()
        {
            if (!CanSet) return;
            using (var d = new FolderBrowserDialog { Description = T("Job_InboxPick", "The folder the window software writes the job files (.kbn) into") })
            {
                if (JobInbox.Folder.Length > 0 && Directory.Exists(JobInbox.Folder)) d.SelectedPath = JobInbox.Folder;
                if (d.ShowDialog(FindForm()) != DialogResult.OK) return;
                JobInbox.SetFolder(d.SelectedPath);
                JobInbox.Scan();
                Status(string.Format(T("Job_InboxSetDone", "Watching {0} for new job files."), d.SelectedPath));
            }
        }

        private void FillRecent()
        {
            _filling = true;
            try
            {
                _cmbRecent.Items.Clear();
                foreach (var p in _recent) _cmbRecent.Items.Add(Path.GetFileName(p) + "  -  " + Path.GetDirectoryName(p));
                _cmbRecent.SelectedIndex = Job != null ? _recent.FindIndex(p => string.Equals(p, Job.Path, StringComparison.OrdinalIgnoreCase)) : -1;
            }
            finally { _filling = false; }
        }

        /// <summary>Every bar laid out again (the settings or the library changed) - the selection kept.</summary>
        internal void Replan()
        {
            if (Job == null) return;
            int keep = SelectedIndex;
            Plans = new List<BarPlan>();
            if (_libraryOk)
            {
                try { Plans = new JobPlanner(_instance, Settings).Plan(Job); }
                catch (Exception ex) { Status(ex.Message, true); }
            }
            FillBars(keep);
            int ready = Plans.Count(p => p.Ok), problems = Plans.Count(p => !p.Ok);
            string text = string.Format(T("Job_Summary", "{0}: {1} bar(s) - {2} can go to the machine, {3} cannot."), Job.Name, Job.Bars.Count, ready, problems);
            if (Job.Problems.Count > 0) text += "  " + string.Format(T("Job_LinesNotRead", "{0} line(s) of the file not read (the notes below)."), Job.Problems.Count);
            if (!_libraryOk) text += "  " + T("Prf_NoLibrary", "The profile library could not be opened:") + " " + _libraryWhy;
            Status(text, problems > 0 || Job.Problems.Count > 0 || !_libraryOk);
        }

        internal int SelectedIndex { get { return _bars.CurrentRow != null ? _bars.CurrentRow.Index : -1; } }

        internal void Select(int bar)
        {
            if (bar < 0 || bar >= _bars.Rows.Count) return;
            _bars.CurrentCell = _bars.Rows[bar].Cells[0];
            ShowBar();
        }

        private void OnUi(Action a)
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke(a); } catch { }
        }

        /// <summary>The run's bar sent: marked sent here when it is the job shown.</summary>
        private void RunBarSent(int i)
        {
            string path = JobRun.JobPath;
            Dictionary<int, DateTime> d;
            if (!_sent.TryGetValue(path, out d)) _sent[path] = d = new Dictionary<int, DateTime>();
            d[i] = DateTime.Now;
            SaveState();
            if (Job != null && string.Equals(Job.Path, path, StringComparison.OrdinalIgnoreCase) && i < _bars.Rows.Count && i < Plans.Count)
            {
                StateCell(_bars.Rows[i], Plans[i], d[i]);
                Status(string.Format(T("Job_RunSent", "Automatic: bar {0} sent - {1} more to go."), Plans[i].Bar.Title, JobRun.Queue.Count));
            }
            ShowRun();
        }

        /// <summary>A bar finished (the machine asked for the next one).</summary>
        private void RunBarFinished(int i, double seconds)
        {
            string path = JobRun.JobPath;
            Dictionary<int, DateTime> d;
            if (!_finished.TryGetValue(path, out d)) _finished[path] = d = new Dictionary<int, DateTime>();
            d[i] = DateTime.Now;
            SaveState();
            if (Job != null && string.Equals(Job.Path, path, StringComparison.OrdinalIgnoreCase) && i < _bars.Rows.Count && i < Plans.Count)
                StateCell(_bars.Rows[i], Plans[i], SentAt(i));
            ShowRun();
        }

        /// <summary>The Stop button while a run goes on.</summary>
        private void ShowRun()
        {
            _btnStopRun.Visible = JobRun.On;
        }

        private DateTime? FinishedAt(int bar)
        {
            Dictionary<int, DateTime> d;
            DateTime t;
            return Job != null && _finished.TryGetValue(Job.Path, out d) && d.TryGetValue(bar, out t) ? t : (DateTime?)null;
        }

        private DateTime? SentAt(int bar)
        {
            Dictionary<int, DateTime> d;
            DateTime t;
            return Job != null && _sent.TryGetValue(Job.Path, out d) && d.TryGetValue(bar, out t) ? t : (DateTime?)null;
        }

        private void FillBars(int keep)
        {
            _filling = true;
            try
            {
                _bars.Rows.Clear();
                for (int i = 0; i < Job.Bars.Count; i++)
                {
                    var b = Job.Bars[i];
                    var plan = i < Plans.Count ? Plans[i] : null;
                    int recs = b.Pieces.Sum(p => p.Recs.Count);
                    int r = _bars.Rows.Add(b.Title, b.Code, "", b.Pieces.Count, recs, b.Length.ToString("0.#", CI),
                                           plan != null && plan.Layout != null ? plan.Layout.Leftover.ToString("0.#", CI) : "", b.Name, b.Color);
                    StateCell(_bars.Rows[r], plan, SentAt(i));
                }
            }
            finally { _filling = false; }
            _pictures.SetPlans(Plans, Math.Max(0, Math.Min(keep, _bars.Rows.Count - 1)));
            CurrentJob.Set(Job != null ? Job.Name : "", Plans, Math.Max(0, Math.Min(keep, _bars.Rows.Count - 1)));       // the Home page draws it too
            if (_bars.Rows.Count > 0) Select(Math.Max(0, Math.Min(keep, _bars.Rows.Count - 1)));
            else ShowBar();
        }

        private void StateCell(DataGridViewRow row, BarPlan plan, DateTime? sent)
        {
            var cell = row.Cells["state"];
            if (plan == null) { cell.Value = ""; return; }
            if (!plan.Ok)
            {
                cell.Value = "\u26A0 " + string.Format(T("Job_StateProblems", "{0} problem(s)"), plan.Problems.Count);
                cell.Style.ForeColor = Color.FromArgb(180, 40, 40);
            }
            else if (FinishedAt(row.Index).HasValue && (!sent.HasValue || FinishedAt(row.Index).Value >= sent.Value))
            {
                var f = FinishedAt(row.Index).Value;
                cell.Value = "\u2714\u2714 " + string.Format(T("Job_StateFinished", "finished {0}"), f.ToString(f.Date == DateTime.Today ? "HH:mm" : "yyyy-MM-dd HH:mm", CI));
                cell.Style.ForeColor = Color.FromArgb(30, 120, 60);
            }
            else if (sent.HasValue)
            {
                cell.Value = "\u2714 " + string.Format(T("Job_StateSent", "sent {0}"), sent.Value.ToString(sent.Value.Date == DateTime.Today ? "HH:mm" : "yyyy-MM-dd HH:mm", CI));
                cell.Style.ForeColor = Color.FromArgb(30, 90, 170);
            }
            else if (plan.NeedsTrial)
            {
                cell.Value = T("Job_StateTrial", "trial only (unconfirmed)");
                cell.Style.ForeColor = Color.FromArgb(190, 110, 0);
            }
            else
            {
                cell.Value = T("Job_StateReady", "ready");
                cell.Style.ForeColor = Color.FromArgb(20, 130, 60);
            }
        }

        /// <summary>The selected bar's pieces, job list and notes.</summary>
        private void ShowBar()
        {
            // the page taken apart (its grids' selection is cleared as they go): nothing to show
            if (IsDisposed || Disposing || _steps.Columns.Count == 0 || _pieces.Columns.Count == 0) return;
            _pieces.Rows.Clear();
            _steps.Rows.Clear();
            int i = SelectedIndex;
            _pictures.Selected = i;
            CurrentJob.Select(i);
            if (Job == null || i < 0 || i >= Job.Bars.Count)
            {
                _lblBar.Text = "";
                _notes.Text = Job != null && Job.Problems.Count > 0 ? string.Join("\r\n", Job.Problems) : "";
                UpdateSend();
                return;
            }
            var b = Job.Bars[i];
            var plan = i < Plans.Count ? Plans[i] : null;
            _lblBar.Text = string.Format(T("Job_BarHead", "Bar {0}: {1} {2}, {3} mm"), b.Title, b.Code, b.Name, b.Length.ToString("0.#", CI)) +
                           (plan != null && plan.Profile != null ? "   (" + plan.Profile.Group + ")" : "");
            var types = Settings.Types;         // a special end's code with its name ("1 Arrow")
            Func<string, string> code = c => { double v; return double.TryParse(c, NumberStyles.Float, CI, out v) ? SpecialCuts.CodeText(v, types) : c; };
            for (int n = 0; n < b.Pieces.Count; n++)
            {
                var p = b.Pieces[n];
                var what = p.Recs.Select(r => r.Code + " @" + r.Position.ToString("0.#", CI)).ToList();
                what.AddRange(p.VCuts.Select(v => "V @" + v.Position.ToString("0.#", CI)));
                if (p.Special != null) what.Insert(0, "<> " + code(p.Special.LeftCode) + " / " + code(p.Special.RightCode));
                _pieces.Rows.Add(n + 1, p.CutLength.ToString("0.#", CI), p.LeftAngle.ToString("0.#", CI), p.RightAngle.ToString("0.#", CI), p.Position,
                                 (p.OrderNo + (p.OrderLineNo.Length > 0 ? "/" + p.OrderLineNo : "")).Trim('/'), p.Customer, p.Barcode, string.Join(", ", what));
            }
            var notes = new List<string>();
            if (plan != null)
            {
                foreach (var s in plan.Steps)
                {
                    int r = _steps.Rows.Add(s.No, s.Position.ToString("0.00", CI), s.Code, s.Piece >= 0 ? (s.Piece + 1).ToString(CI) : "", s.What);
                    if (s.Op != null && !s.Op.Confirmed) _steps.Rows[r].Cells["what"].Style.ForeColor = Color.FromArgb(190, 110, 0);
                    else if (s.Op == null) _steps.Rows[r].Cells["what"].Style.ForeColor = Color.FromArgb(60, 70, 85);
                }
                if (plan.Layout != null)
                {
                    int e = _steps.Rows.Add("", JobPlanner.EndMark.ToString(CI), "", "", T("Job_EndMark", "the end of the bar"));
                    _steps.Rows[e].DefaultCellStyle.ForeColor = Color.Gray;
                }
                foreach (var p in plan.Problems) notes.Add("\u26A0 " + p);
                foreach (var w in plan.Warnings) notes.Add("\u2022 " + w);
                if (plan.Layout != null)
                    notes.Add(string.Format(T("Job_LeftoverNote", "Leftover {0} mm, {1} step(s) (IDs {2} - {3}), {4} milling operation(s)."),
                        plan.Layout.Leftover.ToString("0.#", CI), plan.Steps.Count, plan.FirstNo, plan.LastNo, plan.Milling.Count));
                foreach (var m in plan.Milling)
                {
                    notes.Add(string.Format(CI, "  {0}: {1} ({2}) - {3}{4}, approach Y {5:0.##} Z {6:0.##}, length {7:0.##}, {8:0.##} m/min  [{9}]", m.Index, m.Label, m.MacroText,
                        m.Tool != null ? "tool " + m.ToolNo + " " + m.Tool.Position : "no tool", m.Face != null ? " at " + m.Face.Angle.ToString("0.#", CI) + "\u00B0" : "",
                        m.Y, m.Z, m.Length, m.Speed, m.Status));
                    // its steps, as the unit gets them when it asks (L101 / L103)
                    for (int k = 0; k < m.Steps.Count; k++) notes.Add("        " + (k + 1).ToString(CI) + ". " + m.Steps[k]);
                }
                // the profile's mullion notching steps (R25700, with the bar)
                if (plan.Notch.Count > 0)
                {
                    notes.Add(string.Format(T("Job_NotchNote", "Mullion notching: {0} step(s) of the profile (X1 X2 Z1 Z2 Y1 Y2 Step Speed)"), plan.Notch.Count));
                    for (int k = 0; k < plan.Notch.Count; k++)
                        notes.Add("        " + (k + 1).ToString(CI) + ". " + string.Join("  ", plan.Notch[k].Select(x => x.ToString("0.##", CI))));
                }
                // the special cuts: their parameters (R25650 ...) and the steps the saw gets when it asks (L105)
                foreach (var sp in plan.Special)
                {
                    notes.Add(string.Format(CI, "  {0} (code {1}) - {2}: angle {3:0.#}\u00B0, V cut Y {4:0.##}, holding {5:0.##}, X offset {6:0.##}", sp.Label, sp.Code,
                        sp.Joint.Describe(Settings.Types), sp.StartingAngle, sp.VCutY, sp.Holding, sp.XOffset));
                    for (int k = 0; k < sp.Steps.Count; k++)
                    {
                        var st = sp.Steps[k];
                        notes.Add(string.Format(CI, "        {0}. {1}{2}", k + 1, SpecialCutsEditor.CommandName(st.Command),
                            StepText(st)));
                    }
                }
            }
            if (Job.Problems.Count > 0) { notes.Add(""); notes.AddRange(Job.Problems.Select(p => "\u26A0 " + p)); }
            _notes.Text = string.Join("\r\n", notes);
            UpdateSend();
        }

        private static string StepText(SpecialStepValues st)
        {
            var parts = new List<string>();
            if (SpecialCutStep.Uses(st.Command, "LeftClamp") || SpecialCutStep.Uses(st.Command, "RightClamp"))
                parts.Add((st.LeftClamp ? "left" : "") + (st.LeftClamp && st.RightClamp ? " + " : "") + (st.RightClamp ? "right" : "") + (st.LeftClamp || st.RightClamp ? " clamp" : "no clamp"));
            if (SpecialCutStep.Uses(st.Command, "Robot1")) parts.Add(string.Format(CI, "robot 1 {0:0.##}", st.Robot1));
            if (SpecialCutStep.Uses(st.Command, "Robot2")) parts.Add(string.Format(CI, "robot 2 {0:0.##}", st.Robot2));
            if (SpecialCutStep.Uses(st.Command, "Angle")) parts.Add(string.Format(CI, "{0:0.#}\u00B0", st.Angle));
            if (SpecialCutStep.Uses(st.Command, "VCutY")) parts.Add(string.Format(CI, "Y {0:0.##}", st.VCutY));
            return parts.Count > 0 ? " - " + string.Join(", ", parts) : "";
        }

        private void UpdateSend()
        {
            int i = SelectedIndex;
            var plan = i >= 0 && i < Plans.Count ? Plans[i] : null;
            _btnSend.Enabled = CanSend && plan != null && plan.Ok && (!plan.NeedsTrial || _chkTrial.Checked);
        }

        // =====================================================================
        // SEND TO THE MACHINE
        // =====================================================================

        /// <summary>The selected bar into the PLC: what it writes asked first. True when written.</summary>
        internal bool SendSelected()
        {
            int i = SelectedIndex;
            if (Job == null || i < 0 || i >= Plans.Count) return false;
            var plan = Plans[i];
            string title = T("Job_Send", "Send to the machine");
            if (!CanSend) { Ask.Tell(FindForm(), title, T("Job_NoLevel", "Sending a job needs the Operator level.")); return false; }
            if (!plan.Ok)
            {
                Ask.Tell(FindForm(), title, string.Format(T("Job_CannotSend", "Bar {0} cannot go to the machine:"), plan.Bar.Title) + "\r\n\r\n" +
                         string.Join("\r\n", plan.Problems.Take(12).Select(p => "  " + p)) + (plan.Problems.Count > 12 ? "\r\n  ..." : ""));
                return false;
            }
            bool trial = plan.NeedsTrial;
            if (trial && !(_chkTrial.Checked && CanSet))
            {
                Ask.Tell(FindForm(), title, string.Format(T("Job_NeedsTrial",
                    "{0} milling operation(s) of bar {1} are not confirmed on the profile {2}. Confirm them on the Profiles page - or, at the Maintenance level, tick Trial to send them for a test piece."),
                    plan.Milling.Count(m => !m.Confirmed), plan.Bar.Title, plan.Bar.Code));
                return false;
            }
            string question = string.Format(T("Job_Ask", "Send bar {0} ({1}, {2} piece(s), {3} mm) to the machine?"), plan.Bar.Title, plan.Bar.Code, plan.Bar.Pieces.Count,
                                            plan.Bar.Length.ToString("0.#", CI)) + "\r\n\r\n" +
                              string.Format(T("Job_AskWhat", "{0} step(s) (IDs {1} - {2}) into the job list, {3} milling operation(s), the profile's parameters."),
                                            plan.Steps.Count, plan.FirstNo, plan.LastNo, plan.Milling.Count);
            if (plan.Notch.Count > 0)
                question += "\r\n" + string.Format(T("Job_AskNotch", "{0} mullion notching step(s) of the profile (R25700)."), plan.Notch.Count);
            if (plan.Special.Count > 0)
                question += "\r\n" + string.Format(T("Job_AskSpecial", "{0} special cut(s): their parameters - the saw gets their steps when it asks (L105)."), plan.Special.Count);
            if (trial) question += "\r\n\r\n" + T("Job_AskTrial", "TRIAL: milling positions nobody has confirmed - for a test piece only.");
            bool automatic = _chkAuto.Checked;
            if (automatic)
                question += "\r\n\r\n" + T("Job_AskAutomatic", "AUTOMATIC: then each next bar of the job not sent yet, when the machine asks for it.");
            if (!Kaban.UI.Settings.PlcBackupUi.IsSimulator(Station)) question += "\r\n\r\n" + T("Msg_MonResetLive", "This is the machine's PLC: it uses the new values at once.");
            if (!Ask.YesNo(FindForm(), title, question)) return false;
            if (automatic)
            {
                Dictionary<int, DateTime> sentOfJob;
                var skip = _sent.TryGetValue(Job.Path, out sentOfJob) ? sentOfJob.Keys.ToList() : new List<int>();
                var why = JobRun.Start(Job, Plans, i, skip, Station, CanSet && _chkTrial.Checked);
                if (why.Length > 0)
                {
                    Ask.Tell(FindForm(), title, string.Format(T("Job_SendFailed", "Bar {0} was not sent: {1}"), plan.Bar.Title, why));
                    Status(string.Format(T("Job_SendFailed", "Bar {0} was not sent: {1}"), plan.Bar.Title, why), true);
                    return false;
                }
                ShowRun();
                return true;
            }
            var r = JobSender.Send(plan, Station, Job.Name, trial, Job);
            if (!r.Ok)
            {
                Ask.Tell(FindForm(), title, string.Format(T("Job_SendFailed", "Bar {0} was not sent: {1}"), plan.Bar.Title, r.Message));
                Status(string.Format(T("Job_SendFailed", "Bar {0} was not sent: {1}"), plan.Bar.Title, r.Message), true);
                return false;
            }
            // watched for its finish (the PLC's request for the next bar) - nothing follows it
            JobRun.SentByHand(Job, Plans, i, Station);
            ShowRun();
            Dictionary<int, DateTime> d;
            if (!_sent.TryGetValue(Job.Path, out d)) _sent[Job.Path] = d = new Dictionary<int, DateTime>();
            d[i] = DateTime.Now;
            SaveState();
            StateCell(_bars.Rows[i], plan, d[i]);
            Status(string.Format(T("Job_Sent", "Bar {0} sent to the machine: {1} step(s), {2} milling operation(s){3}."), plan.Bar.Title, plan.Steps.Count, plan.Milling.Count,
                                 trial ? " - " + T("Job_Trial", "Trial") : ""));
            return true;
        }

        // =====================================================================
        // WHAT IS KEPT (Jobs.cfg)
        // =====================================================================

        private void LoadState()
        {
            _recent.Clear();
            _sent.Clear();
            _finished.Clear();
            try
            {
                if (!File.Exists(StatePath)) return;
                foreach (var line in File.ReadAllLines(StatePath))
                {
                    var t = line.Split('\t');
                    if (t.Length >= 2 && t[0] == "recent" && !_recent.Contains(t[1], StringComparer.OrdinalIgnoreCase)) _recent.Add(t[1]);
                    int bar;
                    DateTime at;
                    if (t.Length >= 4 && t[0] == "sent" && int.TryParse(t[2], NumberStyles.Integer, CI, out bar) &&
                        DateTime.TryParseExact(t[3], "yyyy-MM-dd HH:mm:ss", CI, DateTimeStyles.None, out at))
                    {
                        Dictionary<int, DateTime> d;
                        if (!_sent.TryGetValue(t[1], out d)) _sent[t[1]] = d = new Dictionary<int, DateTime>();
                        d[bar] = at;
                    }
                    if (t.Length >= 4 && t[0] == "finished" && int.TryParse(t[2], NumberStyles.Integer, CI, out bar) &&
                        DateTime.TryParseExact(t[3], "yyyy-MM-dd HH:mm:ss", CI, DateTimeStyles.None, out at))
                    {
                        Dictionary<int, DateTime> d;
                        if (!_finished.TryGetValue(t[1], out d)) _finished[t[1]] = d = new Dictionary<int, DateTime>();
                        d[bar] = at;
                    }
                }
            }
            catch (Exception ex) { AuditLogger.Log("[JOB] " + StatePath + " could not be read: " + ex.Message); }
        }

        private void SaveState()
        {
            try
            {
                var lines = _recent.Select(p => "recent\t" + p).ToList();
                // the bars sent of the jobs still in the list
                foreach (var kv in _sent.Where(k => _recent.Contains(k.Key, StringComparer.OrdinalIgnoreCase)))
                    foreach (var b in kv.Value.OrderBy(x => x.Key))
                        lines.Add("sent\t" + kv.Key + "\t" + b.Key.ToString(CI) + "\t" + b.Value.ToString("yyyy-MM-dd HH:mm:ss", CI));
                foreach (var kv in _finished.Where(k => _recent.Contains(k.Key, StringComparer.OrdinalIgnoreCase)))
                    foreach (var b in kv.Value.OrderBy(x => x.Key))
                        lines.Add("finished\t" + kv.Key + "\t" + b.Key.ToString(CI) + "\t" + b.Value.ToString("yyyy-MM-dd HH:mm:ss", CI));
                Directory.CreateDirectory(Path.GetDirectoryName(StatePath));
                File.WriteAllLines(StatePath, lines);
            }
            catch (Exception ex) { AuditLogger.Log("[JOB] " + StatePath + " could not be written: " + ex.Message); }
        }
    }
}
