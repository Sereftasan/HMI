using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;
using Kaban.Profiles.Machining;
using Kaban.UI.Controls;
using Kaban.UI.Dxf;
using Kaban.UI.Profiles;

namespace Kaban.Machines.FA2010.Screens
{
    /// <summary>
    /// THE PROGRAMS of the profile, made here and saved as files - never sent to
    /// the PLC:
    ///   ACCESSORIES - the side machining of one bar: every confirmed placement
    ///     (a TRIAL program: every one not rejected), at its places along the bar,
    ///     with the best mounted tool, in passes, entering each wall at its entry
    ///     speed and each chamber at the approach feed (the walls are the
    ///     drawing's, the speeds the machine's feeds);
    ///   END MILLING - the contour milled around the profile's end, from the
    ///     drawing's border (kept with the profile, per machine).
    /// </summary>
    public partial class ProfilesScreen
    {
        private TabControl _progTabs;
        private NumericUpDown _numBar, _numToolRadius, _numCutDepth;
        private CheckBox _chkTrial;
        private TextBox _txtSide, _txtEndMill;
        private RichTextBox _lstNotes;
        private Button _btnSaveEndMill;
        private SideProgram.Result _sideResult;
        private DxfDocument.GCodeProgram _endProgram;
        private readonly List<int> _pathLineIndex = new List<int>();   // drawn point -> code line

        private void BuildPrograms()
        {
            _progTabs = new TabControl { Dock = DockStyle.Fill, Font = UiStyle.Small, Padding = new Point(12, 4) };
            var sideTab = new TabPage(T("Prf_TabSide", "Accessories")) { BackColor = Color.White };
            var endTab = new TabPage(T("Prf_TabEndMill", "End milling")) { BackColor = Color.White };
            _progTabs.TabPages.Add(sideTab);
            _progTabs.TabPages.Add(endTab);
            Func<ToolStrip> newBar = () => new ToolStrip
            {
                Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, CanOverflow = true,
                LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow, AutoSize = false, Height = 40,
                BackColor = Color.White, Renderer = new ToolStripProfessionalRenderer()
            };
            // A caption and its input are ONE hosted control: never separated by the overflow.
            Func<string, Control, int, Panel> field = (caption, input, width) =>
            {
                var holder = new Panel { Width = width, Height = 30 };
                var lbl = new Label { Text = caption, AutoSize = false, Width = width - input.Width - 6, Height = 24, Top = 4, Left = 0, TextAlign = ContentAlignment.MiddleRight, Font = UiStyle.Small };
                input.Left = lbl.Width + 4; input.Top = 3;
                holder.Controls.Add(lbl);
                holder.Controls.Add(input);
                return holder;
            };
            Func<Font, TextBox> code = font => new TextBox
            {
                Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Both, WordWrap = false, HideSelection = false, ReadOnly = true,
                Font = font, BackColor = Color.FromArgb(28, 30, 34), ForeColor = Color.FromArgb(190, 230, 190)
            };

            // ---- the side machining program ------------------------------------------------------
            var sbar = newBar();
            Action<Control> addS = c => sbar.Items.Add(new ToolStripControlHost(c) { AutoSize = false, Size = c.Size });
            _numBar = new NumericUpDown { DecimalPlaces = 0, Minimum = 100, Maximum = 12000, Value = 1500, Increment = 100, Width = 70, TextAlign = HorizontalAlignment.Right };
            Kaban.UI.Controls.UiStyle.SetTip(_tips, _numBar, T("Prf_TipBar", "The bar's length: where the operations land along it (from the start, the end, centred, spread)"));
            _numBar.ValueChanged += (s, e) => { if (_sideResult != null) GenerateSide(); };
            addS(field(T("Prf_BarLength", "Bar (mm) :"), _numBar, 150));
            _chkTrial = new CheckBox { Text = T("Prf_Trial", "trial"), AutoSize = false, Width = 70, Height = 24, Font = UiStyle.Small };
            Kaban.UI.Controls.UiStyle.SetTip(_tips, _chkTrial, T("Prf_TipTrial", "A TRIAL program: every placement not rejected, confirmed or not - for a test piece, to check the positions before confirming them"));
            _chkTrial.CheckedChanged += (s, e) => { if (_sideResult != null) GenerateSide(); };
            addS(_chkTrial);
            addS(UiStyle.ToolButton("\u2699", T("Prf_TipGenerateSide", "Make the program"), _tips, (s, e) => GenerateSide()));
            addS(UiStyle.ToolButton("\uD83D\uDCE4", T("Prf_TipExportSide", "Save the program as a file (.nc)"), _tips, (s, e) => ExportProgram(_txtSide.Text, false)));
            addS(UiStyle.ToolButton("\uD83D\uDCCB", T("Prf_TipCopyProgram", "Copy the program"), _tips, (s, e) => CopyText(_txtSide.Text)));
            _txtSide = code(new Font("Consolas", 9F));
            // what the program could not do, in red, and what it did
            _lstNotes = new RichTextBox
            {
                Dock = DockStyle.Bottom, Height = 84, Font = UiStyle.Small, ReadOnly = true, BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(248, 249, 251), ScrollBars = RichTextBoxScrollBars.Vertical, DetectUrls = false
            };
            sideTab.Controls.Add(_txtSide);
            sideTab.Controls.Add(_lstNotes);
            sideTab.Controls.Add(sbar);

            // ---- the end milling program --------------------------------------------------------------
            var ebar = newBar();
            Action<Control> addE = c => ebar.Items.Add(new ToolStripControlHost(c) { AutoSize = false, Size = c.Size });
            _numToolRadius = new NumericUpDown { DecimalPlaces = 2, Minimum = 0, Maximum = 100, Value = 3, Width = 62, TextAlign = HorizontalAlignment.Right };
            _numToolRadius.ValueChanged += (s, e) => RegenerateEndMill();
            addE(field(T("Prf_ToolRadius", "Tool radius (mm) :"), _numToolRadius, 170));
            _numCutDepth = new NumericUpDown { DecimalPlaces = 2, Minimum = 0, Maximum = 500, Value = 5, Width = 62, TextAlign = HorizontalAlignment.Right };
            _numCutDepth.ValueChanged += (s, e) => RegenerateEndMill();
            addE(field(T("Prf_CutDepthShort", "Cut depth (mm) :"), _numCutDepth, 170));
            addE(UiStyle.ToolButton("\u2699", T("Prf_TipGenerateEnd", "Make the program from the drawing as it is now"), _tips, (s, e) => GenerateEndMill()));
            _btnSaveEndMill = EditTool("\uD83D\uDCBE", T("Prf_TipSaveEnd", "Keep the program with the profile (for this machine)"), (s, e) => SaveEndMill());
            addE(_btnSaveEndMill);
            addE(UiStyle.ToolButton("\uD83D\uDCE4", T("Prf_TipExportEnd", "Save the program as a file (.nc)"), _tips, (s, e) => ExportProgram(_txtEndMill.Text, true)));
            addE(UiStyle.ToolButton("\uD83D\uDCCB", T("Prf_TipCopyProgram", "Copy the program"), _tips, (s, e) => CopyText(_txtEndMill.Text)));
            _txtEndMill = code(new Font("Consolas", 9F));
            _txtEndMill.ReadOnly = false;
            _txtEndMill.TextChanged += (s, e) => RefreshToolpath();
            _txtEndMill.KeyUp += (s, e) => SyncFromGcodeCaret();
            _txtEndMill.Click += (s, e) => SyncFromGcodeCaret();
            _txtEndMill.MouseUp += (s, e) => SyncFromGcodeCaret();
            endTab.Controls.Add(_txtEndMill);
            endTab.Controls.Add(ebar);

            AddPane("program", T("Prf_PanePrograms", "Programs")).Controls.Add(_progTabs);
        }

        /// <summary>One line under the program (a problem in red).</summary>
        private void Note(string text, bool problem = false)
        {
            _lstNotes.SelectionStart = _lstNotes.TextLength;
            _lstNotes.SelectionColor = problem ? Color.FromArgb(170, 30, 30) : Color.FromArgb(70, 76, 86);
            _lstNotes.AppendText((_lstNotes.TextLength > 0 ? "\n" : "") + text);
        }

        private static void CopyText(string text)
        {
            try { if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text); } catch { }
        }

        private void FillPrograms()
        {
            _sideResult = null;
            _txtSide.Text = "";
            _lstNotes.Clear();
            _endProgram = null;
            _txtEndMill.Text = "";
            if (_current == null || !_libraryOk) return;
            int open = _placements.Count(p => p.Status != "REJECTED");
            int confirmed = _placements.Count(p => p.Status == "CONFIRMED" || p.Status == "OVERRIDDEN");
            Note(open == 0 ? T("Prf_NoOps", "No accessory operations on this profile.")
                : string.Format(T("Prf_OpsState", "{0} of {1} operation(s) confirmed. \u2699 makes the program ({2})."), confirmed, open,
                                _chkTrial.Checked ? T("Prf_TrialWord", "trial: every one not rejected") : T("Prf_ProductionWord", "production: the confirmed ones")));

            // the end milling program kept with the profile, for this machine
            try
            {
                var kept = _instance > 0 ? Library.EndMillProgram(_current.Id, _instance) : null;
                if (kept != null)
                {
                    var r = Library.Real(kept, "tool_radius_mm");
                    var d = Library.Real(kept, "cut_depth_mm");
                    _filling = true;
                    if (r.HasValue) _numToolRadius.Value = Math.Max(_numToolRadius.Minimum, Math.Min(_numToolRadius.Maximum, (decimal)r.Value));
                    if (d.HasValue) _numCutDepth.Value = Math.Max(_numCutDepth.Minimum, Math.Min(_numCutDepth.Maximum, (decimal)d.Value));
                    _filling = false;
                    _txtEndMill.Text = Library.Text(kept, "gcode");
                }
            }
            catch (Exception ex) { _filling = false; Status(ex.Message, true); }
        }

        // ---- the side machining program ------------------------------------------------------------

        /// <summary>Makes the side machining program of one bar of the profile.</summary>
        internal SideProgram.Result GenerateSide()
        {
            if (_current == null || !_libraryOk) return null;
            var ops = _placements.Select(p => p.ToOperation()).ToList();
            double w = _section != null ? _section.Width : _current.EffectiveWidth, h = _section != null ? _section.Height : _current.EffectiveHeight;
            var settings = _settings ?? Library.ProgramSettings(_instance, _current);
            _sideResult = SideProgram.Generate(_current.Code, (double)_numBar.Value, ops, _section, w, h,
                                               Library.Tools(_instance), Library.Faces(_instance), Library.Feeds(_instance), settings, _chkTrial.Checked);
            _txtSide.Text = _sideResult.Code;
            _lstNotes.Clear();
            foreach (var p in _sideResult.Problems) Note("\u26A0 " + p, true);
            foreach (var n in _sideResult.Notes) Note(n);
            Note(string.Format(T("Prf_ProgramMade", "{0} operation(s), {1} cut(s) on a {2} mm bar{3}."), _sideResult.Operations, _sideResult.Cuts,
                                             ((double)_numBar.Value).ToString("0", CI), _chkTrial.Checked ? " - " + T("Prf_TrialWordShort", "TRIAL") : ""));
            _progTabs.SelectedIndex = 0;
            Status(_sideResult.Ok ? T("Prf_SideOk", "Side machining program made.") : T("Prf_SideProblems", "The program is made without the operations listed under it - see why."), !_sideResult.Ok);
            return _sideResult;
        }

        /// <summary>A program saved as a file - to the machine's program folder when one is set.</summary>
        private void ExportProgram(string text, bool endMill)
        {
            if (_current == null || string.IsNullOrWhiteSpace(text)) { Status(T("Prf_NothingToSave", "Make the program first (\u2699)."), true); return; }
            string folder = _libraryOk && _instance > 0 ? Library.Setting(_instance, "program_folder", "") : "";
            string name = Safe(_current.Code) + (endMill ? "_END" : _chkTrial.Checked ? "_SIDE_TRIAL" : "_SIDE") + ".nc";
            using (var dlg = new SaveFileDialog { Filter = T("Prf_NcFilter", "CNC programs") + " (*.nc)|*.nc|" + T("Prf_AllFiles", "All files") + " (*.*)|*.*", FileName = name })
            {
                if (folder.Length > 0 && Directory.Exists(folder)) dlg.InitialDirectory = folder;
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(dlg.FileName, text.Replace("\r\n", "\n").Replace("\n", "\r\n"), new UTF8Encoding(false));
                    AuditLogger.Log("profile library: program of " + _current.Code + " saved as " + dlg.FileName + ".");
                    Status(string.Format(T("Prf_ProgramSaved", "Saved as {0}."), dlg.FileName));
                }
                catch (Exception ex) { Status(ex.Message, true); }
            }
        }

        private static string Safe(string s)
        {
            var bad = Path.GetInvalidFileNameChars();
            return new string((s ?? "").Select(ch => bad.Contains(ch) ? '_' : ch).ToArray());
        }

        // ---- the end milling program ------------------------------------------------------------------

        private void RegenerateEndMill()
        {
            if (_filling) return;
            _viewer.ToolDiameter = (double)_numToolRadius.Value * 2;
            if (_viewer.Document != null && _viewer.Document.Polylines.Count > 0 && !string.IsNullOrEmpty(_txtEndMill.Text)) GenerateEndMill();
            else _viewer.Invalidate();
        }

        /// <summary>The end milling program from the drawing as it is (its edits included).</summary>
        private void GenerateEndMill()
        {
            if (_viewer.Document == null || _viewer.Document.Polylines.Count == 0) { Status(T("Prf_NeedDrawing", "The profile has no drawing."), true); return; }
            // every figure on this page is in MILLIMETRES
            _endProgram = _viewer.Document.GenerateBorderProgram((double)_numToolRadius.Value, (double)_numCutDepth.Value, 800, 5, true);
            _txtEndMill.Text = _endProgram.Code;              // TextChanged rebuilds the path and the line map
            _viewer.ToolDiameter = (double)_numToolRadius.Value * 2;
            _viewer.ShowToolpath = true;
            if (_btnToolpath != null) _btnToolpath.BackColor = UiStyle.AccentBack;
            _viewer.Invalidate();
            _progTabs.SelectedIndex = 1;
            int arcs = 0, moves = 0;
            foreach (var l in _endProgram.Code.Split('\n'))
            {
                var t = l.TrimStart();
                if (t.StartsWith("G02", System.StringComparison.Ordinal) || t.StartsWith("G03", System.StringComparison.Ordinal)) arcs++;
                else if (t.StartsWith("G1 X", System.StringComparison.Ordinal) || t.StartsWith("G0 X", System.StringComparison.Ordinal)) moves++;
            }
            Status(string.Format(T("Prf_EndMade", "End milling program: tool radius {0:F2} mm, cut depth {1:F2} mm, {2} arcs, {3} moves - \uD83D\uDCBE keeps it with the profile."),
                                 _numToolRadius.Value, _numCutDepth.Value, arcs, moves));
        }

        private void SaveEndMill()
        {
            if (_current == null || !CanEdit || _instance <= 0) return;
            if (string.IsNullOrWhiteSpace(_txtEndMill.Text)) { Status(T("Prf_NothingToSave", "Make the program first (\u2699)."), true); return; }
            Library.SaveEndMillProgram(_current.Id, _instance, _txtEndMill.Text, (double)_numToolRadius.Value, (double)_numCutDepth.Value, User);
            AuditLogger.Log("profile library: end milling program of " + _current.Code + " kept.");
            Status(string.Format(T("Prf_EndSaved", "The end milling program is kept with {0}."), _current.Code));
        }

        // ---- the program text <-> the drawing ------------------------------------------------------------

        // Line and character conversions on OUR line array (TextBox's own disagree about line ends).
        private static int LineStartIndex(string[] lines, int line)
        {
            int idx = 0;
            for (int i = 0; i < line && i < lines.Length; i++) idx += lines[i].Length + Environment.NewLine.Length;
            return idx;
        }

        private static int LineFromIndex(string[] lines, int charIndex)
        {
            int idx = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                int next = idx + lines[i].Length + Environment.NewLine.Length;
                if (charIndex < next) return i;
                idx = next;
            }
            return Math.Max(0, lines.Length - 1);
        }

        /// <summary>DRAWING -> G-CODE: selects the program line that cut that point.</summary>
        private void SelectGcodeLineFor(int pathIndex)
        {
            if (pathIndex < 0 || pathIndex >= _pathLineIndex.Count) return;
            int line = _pathLineIndex[pathIndex];
            string[] all = _txtEndMill.Lines;
            if (line < 0 || line >= all.Length) return;
            _progTabs.SelectedIndex = 1;
            _txtEndMill.Select(LineStartIndex(all, line), all[line].Length);
            _txtEndMill.ScrollToCaret();
            Status(T("Prf_ProgramLine", "Program line") + " " + (line + 1) + ": " + all[line]);
        }

        /// <summary>G-CODE -> DRAWING: highlights the geometry of the caret's line.</summary>
        private void SyncFromGcodeCaret()
        {
            if (_pathLineIndex.Count == 0) return;
            int line = LineFromIndex(_txtEndMill.Lines, _txtEndMill.SelectionStart);
            int idx = _pathLineIndex.IndexOf(line);
            if (idx < 0)
                for (int i = 0; i < _pathLineIndex.Count; i++)
                    if (_pathLineIndex[i] >= line) { idx = i; break; }
            if (idx < 0) return;
            _viewer.HighlightedRoutePoint = idx;
            _viewer.ShowToolpath = true;
            if (_btnToolpath != null) _btnToolpath.BackColor = UiStyle.AccentBack;
            _viewer.Invalidate();
        }

        /// <summary>
        /// The drawn tool path from the program TEXT, remembering which line made
        /// each point - one array indexes both, so a click lands on its own line.
        /// </summary>
        private void RefreshToolpath()
        {
            _pathLineIndex.Clear();
            var pts = new List<DxfDocument.PointD>();
            if (_txtEndMill != null)
            {
                string[] lines = _txtEndMill.Lines;
                double x = 0, y = 0;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0 || line.StartsWith("(", System.StringComparison.Ordinal)) continue;
                    if (!(line.StartsWith("G0", System.StringComparison.Ordinal) || line.StartsWith("G1", System.StringComparison.Ordinal) || line.StartsWith("G2", System.StringComparison.Ordinal) || line.StartsWith("G3", System.StringComparison.Ordinal))) continue;
                    bool moved = false;
                    foreach (var tok in line.Split(' '))
                    {
                        if (tok.Length < 2) continue;
                        double v;
                        if (!double.TryParse(tok.Substring(1), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) continue;
                        if (tok[0] == 'X') { x = v; moved = true; }
                        else if (tok[0] == 'Y') { y = v; moved = true; }
                    }
                    if (!moved) continue;
                    pts.Add(new DxfDocument.PointD(x, y));
                    _pathLineIndex.Add(i);
                }
            }
            // The cutter's EDGE, not its centre line (G41 offsets by the radius).
            double radius = _numToolRadius != null ? (double)_numToolRadius.Value : 0;
            _viewer.Toolpath = radius > 0.0001 ? DxfDocument.OffsetPolyline(pts, -radius) : pts;
            _viewer.HighlightedRoutePoint = -1;
            _viewer.Invalidate();
        }
    }
}
