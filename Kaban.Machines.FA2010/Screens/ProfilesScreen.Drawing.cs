using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Kaban.Profiles.Data;
using Kaban.Profiles.Geometry;
using Kaban.Profiles.Machining;
using Kaban.UI.Controls;
using Kaban.UI.Dxf;

namespace Kaban.Machines.FA2010.Screens
{
    /// <summary>
    /// THE DRAWING: the profile's section as the library reads it - turned and
    /// mirrored as its record says, Y across (0 at the outside face), Z up (0 at
    /// the bed), the layers that are not the profile in grey - with its reference
    /// points and every accessory placement on it: the arrow the tool comes in
    /// by, and for the selected one the cut itself and the walls it goes through.
    ///
    /// The end milling tools (the contour milled around the profile's end) work on
    /// the same drawing, as before: edit, choose the border, build the program.
    /// </summary>
    public partial class ProfilesScreen
    {
        private readonly DxfViewerControl _viewer = new DxfViewerControl();
        private Button _btnUndo, _btnRedo, _btnDelSel, _btnToolpath, _btnShowRefs, _btnShowAcc;
        private bool _showRefs = true, _showAcc = true;

        private void BuildDrawing()
        {
            var drawPane = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            // A ToolStrip, not a wrapping panel: squeezed, the extra buttons go under ">>".
            var bar = new ToolStrip
            {
                Dock = DockStyle.Top, GripStyle = ToolStripGripStyle.Hidden, CanOverflow = true,
                LayoutStyle = ToolStripLayoutStyle.HorizontalStackWithOverflow, AutoSize = false, Height = 40,
                BackColor = Color.White, Renderer = new ToolStripProfessionalRenderer()
            };
            Action<Control> add = c => bar.Items.Add(new ToolStripControlHost(c) { AutoSize = false, Size = c.Size });
            Action gap = () => bar.Items.Add(new ToolStripSeparator());
            Func<string, string, EventHandler, Button> Tool = (emoji, tip, click) => UiStyle.ToolButton(emoji, tip, _tips, click);

            // what is shown on it
            _btnShowRefs = Tool("\uD83D\uDCCD", T("Prf_TipShowRefs", "Show the reference points"), (s, e) =>
            {
                _showRefs = !_showRefs;
                _btnShowRefs.BackColor = _showRefs ? UiStyle.AccentBack : SystemColors.Control;
                _viewer.Invalidate();
            });
            _btnShowRefs.BackColor = UiStyle.AccentBack;
            add(_btnShowRefs);
            _btnShowAcc = Tool("\uD83D\uDD29", T("Prf_TipShowAcc", "Show the accessories: where each tool comes in (the selected one with its cut and the walls it goes through)"), (s, e) =>
            {
                _showAcc = !_showAcc;
                _btnShowAcc.BackColor = _showAcc ? UiStyle.AccentBack : SystemColors.Control;
                _viewer.Invalidate();
            });
            _btnShowAcc.BackColor = UiStyle.AccentBack;
            add(_btnShowAcc);
            gap();
            // how the drawing is read: these change the PROFILE (saved with Details)
            add(EditTool("\u27F3", T("Prf_TipTurnCw", "Turn the drawing 90\u00B0 clockwise (the profile's drawing setting - save it in Details)"),
                         (s, e) => { if (_current != null) _cmbTurn.SelectedIndex = (_cmbTurn.SelectedIndex + 1) % 4; }));
            add(EditTool("\u27F2", T("Prf_TipTurnCcw", "Turn the drawing 90\u00B0 counter-clockwise (saved in Details)"),
                         (s, e) => { if (_current != null) _cmbTurn.SelectedIndex = (_cmbTurn.SelectedIndex + 3) % 4; }));
            add(EditTool("\u21CB", T("Prf_TipMirror", "Mirror the drawing: the outside face must be on the left (saved in Details)"),
                         (s, e) => { if (_current != null) _chkMirror.Checked = !_chkMirror.Checked; }));
            gap();
            add(Tool("\uD83D\uDD0D", T("Prf_TipZoomIn", "Zoom in (the wheel zooms too)"), (s, e) => _viewer.ZoomIn()));
            add(Tool("\uD83D\uDD0E", T("Prf_TipZoomOut", "Zoom out"), (s, e) => _viewer.ZoomOut()));
            add(Tool("\u2317", T("Prf_TipFit", "Fit to window"), (s, e) => _viewer.ZoomFit()));
            var btnMeasure = Tool("\uD83D\uDCCF", T("Prf_TipMeasure", "Measure - RIGHT click two points, LEFT click copies Y / Z"), null);
            btnMeasure.Click += (s, e) =>
            {
                _viewer.MeasureMode = !_viewer.MeasureMode;
                _viewer.Cursor = _viewer.MeasureMode ? Cursors.Cross : Cursors.Default;
                btnMeasure.BackColor = _viewer.MeasureMode ? UiStyle.AccentBack : SystemColors.Control;
            };
            add(btnMeasure);
            gap();

            // ---- the end milling tools (the contour around the profile's end) ----
            var btnRoute = Tool("\u27B0", T("Prf_TipRoute", "End milling: show the border route (red)"), null);
            btnRoute.Click += (s, e) =>
            {
                _viewer.ShowBorderRoute = !_viewer.ShowBorderRoute;
                btnRoute.BackColor = _viewer.ShowBorderRoute ? UiStyle.AccentBack : SystemColors.Control;
                _viewer.Invalidate();
            };
            add(btnRoute);
            var btnLine = Tool("\u2215", T("Prf_TipAddLine", "End milling: add a line - two clicks, each snapping to the nearest element end"), null);
            btnLine.Click += (s, e) =>
            {
                _viewer.AddLineMode = !_viewer.AddLineMode;
                btnLine.BackColor = _viewer.AddLineMode ? Color.FromArgb(210, 240, 210) : SystemColors.Control;
            };
            add(btnLine);
            _btnDelSel = Tool("\uD83D\uDDD1", T("Prf_TipDelSel", "End milling: delete the SELECTED elements (drag right-to-left to catch everything a box touches, Del works too)"), (s, e) => DeleteSelection());
            add(_btnDelSel);
            _btnUndo = Tool("\u21B6", T("Prf_TipUndo", "Undo (Ctrl+Z)"), (s, e) => { _viewer.Undo(); UpdateEditButtons(); });
            _btnRedo = Tool("\u21B7", T("Prf_TipRedo", "Redo (Ctrl+Y)"), (s, e) => { _viewer.Redo(); UpdateEditButtons(); });
            add(_btnUndo);
            add(_btnRedo);
            _btnToolpath = Tool("\uD83D\uDFE2", T("Prf_TipToolpath", "End milling: show the tool path (green, drawn at the real tool width)"), null);
            _btnToolpath.Click += (s, e) =>
            {
                _viewer.ShowToolpath = !_viewer.ShowToolpath;
                _btnToolpath.BackColor = _viewer.ShowToolpath ? UiStyle.AccentBack : SystemColors.Control;
                _viewer.Invalidate();
            };
            add(_btnToolpath);
            add(Tool("\u2705", T("Prf_TipUseBorder", "End milling: use the SELECTED elements as the border route"), (s, e) => UseSelectionAsBorder()));
            add(Tool("\u26D3", T("Prf_TipConnected", "Select the whole CONNECTED run from what is selected"), (s, e) => Status(_viewer.SelectConnected() + " " + T("Prf_ElementsSelected", "element(s) selected."))));
            add(Tool("\u2194", T("Prf_TipBetween", "Select everything BETWEEN the two selected elements"), (s, e) =>
            {
                int n = _viewer.SelectBetween();
                Status(n > 0 ? n + " " + T("Prf_ElementsSelected", "element(s) selected.") : T("Prf_SelectTwo", "Select exactly two elements first."));
            }));
            add(Tool("\u26D4", T("Prf_TipExclude", "End milling: do NOT cut the selected elements"), (s, e) => { Status(_viewer.SetSelectedIncluded(false) + " " + T("Prf_NotCut", "element(s) will not be cut.")); GenerateEndMill(); }));
            add(Tool("\u2714", T("Prf_TipInclude", "End milling: cut the selected elements again"), (s, e) => { Status(_viewer.SetSelectedIncluded(true) + " " + T("Prf_CutAgain", "element(s) back in the cut.")); GenerateEndMill(); }));
            add(Tool("\u2702", T("Prf_TipSplit", "Split the selected element at the point you click next"), (s, e) => { _viewer.SplitMode = true; Status(T("Prf_ClickSplit", "Click where the element should be split.")); }));
            add(Tool("\u2699", T("Prf_TipCutSettings", "End milling: cut settings for the SELECTED elements (depth, offsets, skip)"), (s, e) => EditElementCut()));
            add(Tool("\u21BA", T("Prf_TipRevertDrawing", "Throw the drawing's edits away (the file is never changed)"), (s, e) => RevertDrawing()));

            _viewer.Dock = DockStyle.Fill;
            _viewer.AxisNames = new[] { "Y", "Z" };
            _viewer.GeometryChanged += (s, e) =>
            {
                UpdateEditButtons();
                if (_endProgram != null && !string.IsNullOrEmpty(_txtEndMill.Text)) GenerateEndMill();
            };
            _viewer.SelectionChanged += (s, e) => UpdateEditButtons();
            _viewer.ToolpathPicked += (s, pathIndex) => SelectGcodeLineFor(pathIndex);
            _viewer.MeasureTaken += (s, d) => Status(string.Format(T("Prf_Measured", "Measured distance: {0:F2} mm"), d));
            _viewer.PaintOverlay += PaintOverlay;
            _viewer.PointPicked += (s, p) => OnPointPicked(p);
            _viewer.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape && _viewer.PickMode)
                {
                    _viewer.PickMode = false;
                    _pickCode = null;
                    _viewer.Cursor = Cursors.Default;
                    Status(T("Prf_PickCancelled", "Picking cancelled."));
                }
            };
            drawPane.Controls.Add(_viewer);
            drawPane.Controls.Add(bar);
            AddPane("drawing", T("Prf_PaneDrawing", "Drawing")).Controls.Add(drawPane);
        }

        /// <summary>The profile's section on the viewer (nothing when it has no drawing).</summary>
        private void ShowDrawing()
        {
            _viewer.SetDocument(_section != null ? DxfDocument.FromSection(_section) : null);
            if (_numToolRadius != null) _viewer.ToolDiameter = (double)_numToolRadius.Value * 2;
            UpdateEditButtons();
            // the end milling program's path belongs to this drawing
            RefreshToolpath();
            _viewer.Invalidate();
        }

        private void UpdateEditButtons()
        {
            if (_btnUndo != null) _btnUndo.Enabled = _viewer.CanUndo;
            if (_btnRedo != null) _btnRedo.Enabled = _viewer.CanRedo;
            if (_btnDelSel != null) _btnDelSel.BackColor = _viewer.SelectionCount > 0 ? Color.FromArgb(250, 214, 214) : SystemColors.Control;
        }

        private void DeleteSelection()
        {
            int n = _viewer.DeleteSelected();
            Status(n > 0 ? n + " " + T("Prf_ElementsDeleted", "element(s) deleted.") : T("Prf_SelectFirst", "Select elements first (click, or drag a region)."));
        }

        /// <summary>Throws the drawing's edits away: the section again, as the library reads it.</summary>
        private void RevertDrawing()
        {
            if (_section == null) { Status(T("Prf_NoOriginal", "No drawing to go back to."), true); return; }
            if (!Kaban.UI.Profiles.Ask.YesNo(FindForm(), T("Prf_PaneDrawing", "Drawing"), T("Prf_AskRevertDrawing", "Discard the drawing's edits and show the file again?"))) return;
            ShowDrawing();
            Status(T("Prf_Reverted", "The drawing is as the file again."));
        }

        /// <summary>Takes the CURRENT selection as the end milling border route.</summary>
        private void UseSelectionAsBorder()
        {
            if (_viewer.Document == null) return;
            int n = _viewer.SetBorderFromSelection();
            if (n > 0)
            {
                _viewer.ShowBorderRoute = true;
                GenerateEndMill();
                Status(n + " " + T("Prf_BorderFixed", "element(s) fixed as the border route - program rebuilt."));
            }
            else Status(T("Prf_SelectBorder", "Select the elements that form the border first (click, or drag a box)."), true);
            _viewer.Invalidate();
        }

        /// <summary>Per-element cut settings of the end milling contour, for every selected element.</summary>
        private void EditElementCut()
        {
            var doc = _viewer.Document;
            if (doc == null || _viewer.SelectionCount == 0) { Status(T("Prf_SelectElements", "Select the element(s) first."), true); return; }
            doc.EnsureMeta();
            var current = doc.Meta[_viewer.SelectedElements.First()];
            using (var dlg = new Form
            {
                Text = T("Prf_CutSettings", "Cut settings") + " (" + _viewer.SelectionCount + ")", Width = 360, Height = 260,
                FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false
            })
            {
                UiScale.ApplyOnLoad(dlg);
                int y = 14;
                Func<string, decimal, decimal, NumericUpDown> num = (caption, val, min) =>
                {
                    dlg.Controls.Add(new Label { Text = caption, Left = 12, Top = y + 4, Width = 180, TextAlign = ContentAlignment.MiddleRight });
                    var n = new NumericUpDown { Left = 200, Top = y, Width = 110, DecimalPlaces = 2, Minimum = min, Maximum = 500, Value = Math.Max(min, Math.Min(500, val)), TextAlign = HorizontalAlignment.Right };
                    dlg.Controls.Add(n);
                    y += 30;
                    return n;
                };
                var nDepth = num(T("Prf_CutDepth", "Cut depth (mm) :"), (decimal)(current.Depth ?? (double)_numCutDepth.Value), 0);
                var nDepthEnd = num(T("Prf_CutDepthEnd", "Depth at the end (mm) :"), (decimal)(current.DepthEnd ?? current.Depth ?? (double)_numCutDepth.Value), 0);
                var nStart = num(T("Prf_CutStart", "Start offset (mm) :"), (decimal)current.StartOffset, -500);
                var nEnd = num(T("Prf_CutEnd", "End offset (mm) :"), (decimal)current.EndOffset, -500);
                var chkSkip = new CheckBox { Text = T("Prf_CutSkip", "Do not cut this element"), Left = 200, Top = y, Width = 200, Checked = !current.Included };
                dlg.Controls.Add(chkSkip);
                y += 30;
                var ok = UiStyle.DialogButton(T("Btn_Ok", "OK"), DialogResult.OK);
                ok.Location = new Point(200, y);
                dlg.Controls.Add(ok);
                dlg.AcceptButton = ok;
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                foreach (var i in _viewer.SelectedElements)
                {
                    if (i < 0 || i >= doc.Meta.Count) continue;
                    var m = doc.Meta[i];
                    m.Depth = (double)nDepth.Value;
                    m.DepthEnd = Math.Abs((double)nDepthEnd.Value - (double)nDepth.Value) < 0.001 ? (double?)null : (double)nDepthEnd.Value;
                    m.StartOffset = (double)nStart.Value;
                    m.EndOffset = (double)nEnd.Value;
                    m.Included = !chkSkip.Checked;
                }
            }
            GenerateEndMill();
            _viewer.Invalidate();
        }

        // =====================================================================
        // ON THE DRAWING: reference points and placements
        // =====================================================================

        private static readonly Color RefConfirmed = Color.FromArgb(90, 220, 120);
        private static readonly Color RefOpen = Color.FromArgb(255, 190, 60);
        private static readonly Color RefInherited = Color.FromArgb(120, 180, 255);

        internal static Color StatusColour(string status)
        {
            switch ((status ?? "").ToUpperInvariant())
            {
                case "CONFIRMED": case "OVERRIDDEN": return Color.FromArgb(80, 210, 110);
                case "PROPOSED": return Color.FromArgb(255, 160, 40);
                case "REJECTED": return Color.FromArgb(120, 120, 120);
                default: return Color.FromArgb(80, 190, 255);
            }
        }

        private void PaintOverlay(object sender, PaintEventArgs e)
        {
            if (_current == null || _section == null) return;
            var g = e.Graphics;
            var smoothing = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                if (_showAcc) DrawPlacements(g);
                if (_showRefs) DrawReferences(g);
            }
            catch { /* an overlay never breaks the drawing */ }
            g.SmoothingMode = smoothing;
        }

        private static bool IsPlane(string code, out bool vertical)
        {
            vertical = code == "OUTSIDE_SURFACE" || code == "INSIDE_SURFACE" || code == "CENTER";
            return vertical || code == "BED_DATUM";
        }

        private void DrawReferences(Graphics g)
        {
            double W = _section.Width, H = _section.Height;
            var selected = new HashSet<string>(SelectedRefCodes(), StringComparer.OrdinalIgnoreCase);
            using (var font = new Font("Segoe UI", 8f, FontStyle.Bold))
                foreach (var r in _references)
                {
                    Color c = r.Inherited ? RefInherited : r.Confirmed ? RefConfirmed : RefOpen;
                    bool sel = selected.Contains(r.Code);
                    var p = _viewer.ScreenOf(r.Y, r.Z);
                    bool vertical;
                    using (var pen = new Pen(Color.FromArgb(sel ? 230 : 140, c), sel ? 2f : 1f) { DashStyle = DashStyle.Dash })
                    using (var brush = new SolidBrush(c))
                    {
                        if (IsPlane(r.Code, out vertical))
                        {
                            if (vertical)
                            {
                                var a = _viewer.ScreenOf(r.Y, -2); var b = _viewer.ScreenOf(r.Y, H + 4);
                                g.DrawLine(pen, a, b);
                                g.DrawString(RefShort(r.Code), font, brush, b.X + 2, b.Y - 2);
                            }
                            else
                            {
                                var a = _viewer.ScreenOf(-4, r.Z); var b = _viewer.ScreenOf(W + 4, r.Z);
                                g.DrawLine(pen, a, b);
                                g.DrawString(RefShort(r.Code), font, brush, b.X + 2, b.Y - font.Height / 2f);
                            }
                            continue;
                        }
                        float s = sel ? 7 : 5;
                        var diamond = new[] { new PointF(p.X, p.Y - s), new PointF(p.X + s, p.Y), new PointF(p.X, p.Y + s), new PointF(p.X - s, p.Y) };
                        g.FillPolygon(brush, diamond);
                        using (var outline = new Pen(Color.Black, 1f)) g.DrawPolygon(outline, diamond);
                        g.DrawString(RefShort(r.Code), font, brush, p.X + s + 2, p.Y - font.Height - 1);
                    }
                }
        }

        /// <summary>The screen direction a tool travels coming in from a side.</summary>
        private static PointF Inward(string side)
        {
            switch ((side ?? "TOP").ToUpperInvariant())
            {
                case "BOTTOM": return new PointF(0, -1);
                case "OUTSIDE": return new PointF(1, 0);
                case "INSIDE": return new PointF(-1, 0);
                default: return new PointF(0, 1);
            }
        }

        /// <summary>How wide the cut is across the face: a hole's diameter, a slot's width (its length when turned 90 degrees).</summary>
        internal static double CutWidth(Library.PlacementInfo p)
        {
            var m = p.Shape();
            bool turned = Math.Abs(Math.IEEERemainder(p.Rotation, 180)) > 45;
            switch (m.Type)
            {
                case "ROUND_HOLE": return m.P("diameter_mm") ?? 5;
                case "SLOT": case "POCKET": return (turned ? m.P("length_mm") : m.P("width_mm")) ?? 5;
                default: return m.P("width_mm") ?? 6;
            }
        }

        /// <summary>The cut's rectangle on the section: across the face, from the surface to the bottom.</summary>
        internal RectangleF CutArea(Library.PlacementInfo p, SideProgram.DepthInfo d, double dy, double dz)
        {
            double W = _section.Width, H = _section.Height, half = CutWidth(p) / 2;
            double y0, y1, z0, z1;
            switch ((p.Side ?? "TOP").ToUpperInvariant())
            {
                case "BOTTOM": y0 = d.Across - half; y1 = d.Across + half; z0 = d.Surface; z1 = d.Bottom; break;
                case "OUTSIDE": z0 = d.Across - half; z1 = d.Across + half; y0 = d.Surface; y1 = d.Bottom; break;
                case "INSIDE": z0 = d.Across - half; z1 = d.Across + half; y0 = W - d.Bottom; y1 = W - d.Surface; break;
                default: y0 = d.Across - half; y1 = d.Across + half; z0 = H - d.Bottom; z1 = H - d.Surface; break;
            }
            y0 += dy; y1 += dy; z0 += dz; z1 += dz;
            var a = _viewer.ScreenOf(y0, z0);
            var b = _viewer.ScreenOf(y1, z1);
            return RectangleF.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));
        }

        /// <summary>A point along the tool's way, <paramref name="dist"/> from the face it comes in by.</summary>
        private PointF AlongRay(string side, double across, double dist)
        {
            double W = _section.Width, H = _section.Height;
            switch ((side ?? "TOP").ToUpperInvariant())
            {
                case "BOTTOM": return _viewer.ScreenOf(across, dist);
                case "OUTSIDE": return _viewer.ScreenOf(dist, across);
                case "INSIDE": return _viewer.ScreenOf(W - dist, across);
                default: return _viewer.ScreenOf(across, H - dist);
            }
        }

        private void DrawPlacements(Graphics g)
        {
            var sel = SelectedPlacement;
            using (var font = new Font("Segoe UI", 8f))
                foreach (var p in _placements.OrderBy(x => sel != null && x.Id == sel.Id ? 1 : 0))
                {
                    bool isSel = sel != null && p.Id == sel.Id;
                    if (p.Status == "REJECTED" && !isSel) continue;
                    var c = StatusColour(p.Status);
                    var dir = Inward(p.Side);
                    var at = _viewer.ScreenOf(p.Y, p.Z);

                    if (isSel)
                    {
                        var d = SideProgram.Depths(p.ToOperation(), _section, _section.Width, _section.Height, _settings != null ? _settings.Breakthrough : 0.5);
                        if (d.Ok)
                        {
                            // the cut, and the walls on the way
                            var rect = CutArea(p, d, 0, 0);
                            using (var fill = new SolidBrush(Color.FromArgb(80, 255, 70, 70))) g.FillRectangle(fill, rect);
                            using (var edge = new Pen(Color.FromArgb(230, 255, 90, 90), 1.5f)) g.DrawRectangle(edge, rect.X, rect.Y, rect.Width, rect.Height);
                            if (d.Walls != null)
                                using (var wall = new Pen(Color.FromArgb(255, 235, 60), 4f))
                                using (var ink = new SolidBrush(Color.FromArgb(255, 235, 60)))
                                    foreach (var w in d.Walls.Where(w => w.Material && w.End > d.Surface && w.Start < d.Bottom))
                                    {
                                        var a = AlongRay(p.Side, d.Across, Math.Max(w.Start, d.Surface));
                                        var b = AlongRay(p.Side, d.Across, Math.Min(w.End, d.Bottom));
                                        g.DrawLine(wall, a, b);
                                        g.DrawString(Mm(w.Thickness), font, ink, Math.Max(a.X, b.X) + 5, (a.Y + b.Y) / 2 - font.Height / 2f);
                                    }
                        }
                        // the move not saved yet: where it would go
                        if (Math.Abs(_pendingDy) > 1e-9 || Math.Abs(_pendingDz) > 1e-9)
                        {
                            var ghost = _viewer.ScreenOf(p.Y + _pendingDy, p.Z + _pendingDz);
                            using (var pen = new Pen(Color.White, 2f) { DashStyle = DashStyle.Dash, EndCap = LineCap.ArrowAnchor })
                                g.DrawLine(pen, ghost.X - dir.X * 34, ghost.Y - dir.Y * 34, ghost.X, ghost.Y);
                            using (var ink = new SolidBrush(Color.White))
                                g.DrawString((_pendingDy >= 0 ? "+" : "") + Mm(_pendingDy) + " / " + (_pendingDz >= 0 ? "+" : "") + Mm(_pendingDz), font, ink, ghost.X + 8, ghost.Y + 4);
                        }
                    }
                    float len = isSel ? 40 : 26;
                    using (var pen = new Pen(c, isSel ? 3f : 2f) { EndCap = LineCap.ArrowAnchor, DashStyle = p.Status == "REJECTED" ? DashStyle.Dot : DashStyle.Solid })
                        g.DrawLine(pen, at.X - dir.X * len, at.Y - dir.Y * len, at.X, at.Y);
                    using (var ink = new SolidBrush(c))
                    {
                        var label = p.Accessory + (p.Seq > 1 || _placements.Count(x => x.AccessoryId == p.AccessoryId) > 1 ? " #" + p.Seq : "");
                        var start = new PointF(at.X - dir.X * len, at.Y - dir.Y * len);
                        g.DrawString(label, font, ink, start.X + (dir.X < 0 ? 4 : dir.X > 0 ? -4 - g.MeasureString(label, font).Width : 4),
                                     start.Y + (dir.Y > 0 ? -font.Height - 1 : dir.Y < 0 ? 2 : -font.Height - 2));
                    }
                }
        }
    }
}
