using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Machines;
using Kaban.UI.Controls;

namespace Kaban.UI.Screens
{
    /// <summary>
    /// INPUTS / OUTPUTS: every physical point of the machine, lit while it is on -
    /// the side page an electrician keeps open beside a Manual Movement page to
    /// see a sensor change as a cylinder moves.
    ///
    /// The points come from each machine's IoList.csv (tools/build_io_list.py
    /// writes FA2010's from the device list and the PLC's I/O interface block):
    /// shown by their PHYSICAL address (X20), read at the relay the PLC copies
    /// them into (M200) - the screens bind to the mirrors, never to X or Y.
    ///
    /// Read-only, and read by the page scanner like any page's lamps: nothing
    /// here writes to the PLC.
    /// </summary>
    public class IoMonitorPage : UserControl
    {
        private readonly TextBox _filter = new TextBox();
        private readonly CheckBox _onlyOn = new CheckBox();
        private readonly Panel _body = new Panel();
        private readonly Label _empty = new Label();
        private readonly List<Section> _sections = new List<Section>();

        private sealed class Section
        {
            public Label Heading;
            public Label InputsCaption, OutputsCaption;
            public IoListHMI Inputs, Outputs;
        }

        public IoMonitorPage()
        {
            BackColor = Color.White;
            Font = UiStyle.Body;
            Dock = DockStyle.Fill;

            var top = new Panel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(8, 7, 8, 4), BackColor = Color.White };
            var find = new Label { Text = T("Lbl_IoFilter", "Find:"), AutoSize = true, Location = new Point(8, 11) };
            _filter.Location = new Point(56, 7);
            _filter.Width = 170;
            _onlyOn.Text = T("Chk_IoOnlyOn", "Only the ones on");
            _onlyOn.AutoSize = true;
            _onlyOn.Location = new Point(240, 9);
            top.Controls.AddRange(new Control[] { find, _filter, _onlyOn });
            top.Layout += (s, e) =>
            {
                _filter.Left = find.Right + 6;
                _onlyOn.Left = _filter.Right + 12;
            };
            UiStyle.Tips().SetToolTip(_filter, T("Tip_IoFilter", "Part of an address (X20, Y1) or of a name"));

            _body.Dock = DockStyle.Fill;
            // Up and down only: the lists are as wide as the panel.
            _body.AutoScroll = false;
            _body.HorizontalScroll.Maximum = 0;
            _body.HorizontalScroll.Enabled = false;
            _body.HorizontalScroll.Visible = false;
            _body.AutoScroll = true;
            _body.BackColor = Color.White;

            _empty.Dock = DockStyle.Fill;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.ForeColor = Color.FromArgb(110, 116, 126);
            _empty.Padding = new Padding(16);
            _empty.Visible = false;

            Controls.Add(_body);
            Controls.Add(_empty);
            Controls.Add(top);

            Build();

            _filter.TextChanged += (s, e) => ApplyFilter();
            _onlyOn.CheckedChanged += (s, e) => ApplyFilter();
            _body.Resize += (s, e) => LayoutSections();
        }

        private static string T(string key, string text) { return LanguageManager.GetString(key, text); }

        /// <summary>The lists shown (for tests): one per controller and kind.</summary>
        internal IEnumerable<IoListHMI> Lists
        {
            get { return _sections.SelectMany(s => new[] { s.Inputs, s.Outputs }).Where(l => l != null); }
        }

        private void Build()
        {
            foreach (var group in IoList.ForMountedMachines().GroupBy(p => p.Station).OrderBy(g => g.Key))
            {
                var ep = PlcConnectionSettings.Endpoints().FirstOrDefault(e => e.StationId == group.Key);
                var sec = new Section
                {
                    Heading = new Label
                    {
                        Text = ep != null ? Kaban.UI.Helpers.PlcEndpointNaming.CaptionWithStation(ep) : "Station " + group.Key,
                        Font = UiStyle.FontVariant(UiStyle.Body, FontStyle.Bold),
                        AutoSize = false, Height = 26, TextAlign = ContentAlignment.BottomLeft,
                        ForeColor = Color.FromArgb(40, 46, 56)
                    }
                };
                var inputs = group.Where(p => p.IsInput).ToList();
                var outputs = group.Where(p => !p.IsInput).ToList();
                if (inputs.Count > 0)
                {
                    sec.InputsCaption = Caption(string.Format(T("Hdr_IoInputs", "Inputs ({0})"), inputs.Count));
                    sec.Inputs = new IoListHMI(group.Key, inputs);
                    sec.Inputs.RowsChanged += (s, e) => LayoutSections();
                }
                if (outputs.Count > 0)
                {
                    sec.OutputsCaption = Caption(string.Format(T("Hdr_IoOutputs", "Outputs ({0})"), outputs.Count));
                    sec.Outputs = new IoListHMI(group.Key, outputs);
                    sec.Outputs.RowsChanged += (s, e) => LayoutSections();
                }
                foreach (var c in new Control[] { sec.Heading, sec.InputsCaption, sec.Inputs, sec.OutputsCaption, sec.Outputs })
                    if (c != null) _body.Controls.Add(c);
                _sections.Add(sec);
            }

            if (_sections.Count == 0)
            {
                _empty.Text = T("Msg_IoNone",
                    "No inputs and outputs are listed for this machine.\r\n\r\n" +
                    "They come from IoList.csv beside the program (Label, Kind, Address, Read, Plc) - " +
                    "tools/build_io_list.py writes it from the device list and the PLC's I/O interface block.");
                _empty.Visible = true;
                _body.Visible = false;
            }
            LayoutSections();
        }

        private static Label Caption(string text)
        {
            return new Label
            {
                Text = text, AutoSize = false, Height = 22, TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(90, 96, 106), Font = UiStyle.Small
            };
        }

        private void ApplyFilter()
        {
            foreach (var l in Lists) l.SetFilter(_filter.Text, _onlyOn.Checked);
            LayoutSections();
        }

        /// <summary>
        /// Inputs and outputs SIDE BY SIDE while there is room for both, one under
        /// the other when the panel is narrow.
        /// </summary>
        private void LayoutSections()
        {
            int gap = Math.Max(6, Font.Height / 2);
            // Room for the scroll bar whether or not it is showing yet: it appears as the lists are placed.
            int width = _body.ClientSize.Width - 2 * gap - SystemInformation.VerticalScrollBarWidth;
            if (width <= 0) return;
            bool twoColumns = width >= Font.Height * 26;
            int colWidth = twoColumns ? (width - gap) / 2 : width;
            int y = _body.AutoScrollPosition.Y + gap / 2;
            _body.SuspendLayout();
            foreach (var sec in _sections)
            {
                sec.Heading.SetBounds(gap, y, width, Font.Height + gap);
                y = sec.Heading.Bottom + 2;
                int yIn = y, yOut = y;
                if (sec.Inputs != null)
                {
                    sec.InputsCaption.SetBounds(gap, yIn, colWidth, Font.Height + 6);
                    sec.Inputs.SetBounds(gap, sec.InputsCaption.Bottom, colWidth, sec.Inputs.PreferredHeight);
                    yIn = sec.Inputs.Bottom + gap;
                }
                if (sec.Outputs != null)
                {
                    int x = twoColumns ? gap + colWidth + gap : gap;
                    int top = twoColumns ? yOut : yIn;
                    sec.OutputsCaption.SetBounds(x, top, colWidth, Font.Height + 6);
                    sec.Outputs.SetBounds(x, sec.OutputsCaption.Bottom, colWidth, sec.Outputs.PreferredHeight);
                    yOut = sec.Outputs.Bottom + gap;
                }
                y = Math.Max(yIn, twoColumns ? yOut : yIn);
                if (!twoColumns && sec.Outputs != null) y = yOut;
            }
            _body.ResumeLayout(true);
        }
    }

    /// <summary>One physical point of IoList.csv.</summary>
    public sealed class IoPoint
    {
        public string Label = "";
        public bool IsInput;
        /// <summary>The physical address: X20, Y0.</summary>
        public string Address = "";
        /// <summary>What the HMI reads: the relay the PLC copies it into, or the point itself.</summary>
        public string Read = "";
        /// <summary>The real station.</summary>
        public int Station = 1;
    }

    /// <summary>Reads the IoList.csv files of the mounted machines.</summary>
    public static class IoList
    {
        /// <summary>Every point of every mounted machine, each on its real station.</summary>
        public static List<IoPoint> ForMountedMachines()
        {
            var all = new List<IoPoint>();
            var read = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in MachineRegistry.Modules.OrderByDescending(x => x.IsMain))
            {
                string path;
                try { path = m.Context.ResolveFile("IoList.csv"); } catch { continue; }
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;
                // A mounted machine's OWN list only (in its folder): ResolveFile falls
                // back to the host's file, whose points are not on its controller.
                if (!m.IsMain && !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), m.Context.ModuleId, StringComparison.OrdinalIgnoreCase)) continue;
                if (!read.Add(Path.GetFullPath(path))) continue;
                var ctx = m.Context;
                all.AddRange(Load(path, local => ctx.Station(local)));
            }
            return all;
        }

        /// <summary>Label;Kind;Address;Read;Plc - comment lines (#) and the heading are skipped.</summary>
        public static List<IoPoint> Load(string path, Func<int, int> station)
        {
            var list = new List<IoPoint>();
            try
            {
                foreach (var raw in File.ReadAllLines(path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    var f = line.Split(';');
                    if (f.Length < 3 || f[0].Trim().Equals("Label", StringComparison.OrdinalIgnoreCase)) continue;
                    string address = f[2].Trim().ToUpperInvariant();
                    if (address.Length == 0) continue;
                    string kind = f[1].Trim();
                    int plc = 1;
                    if (f.Length > 4) int.TryParse(f[4].Trim(), out plc);
                    string read = f.Length > 3 ? f[3].Trim().ToUpperInvariant() : "";
                    list.Add(new IoPoint
                    {
                        Label = f[0].Trim(),
                        IsInput = kind.StartsWith("I", StringComparison.OrdinalIgnoreCase) ||
                                  (kind.Length == 0 && address.StartsWith("X")),
                        Address = address,
                        Read = read.Length > 0 ? read : address,
                        Station = station != null ? station(Math.Max(1, plc)) : Math.Max(1, plc)
                    });
                }
            }
            catch (Exception ex) { AuditLogger.Log("could not read the I/O list " + path + ": " + ex.Message); }
            return list;
        }
    }

    /// <summary>
    /// A list of I/O points drawn as rows - lamp, address, name - read by the page
    /// scanner as one control (hundreds of lamp controls would be hundreds of
    /// windows). Rows filtered out are not drawn but still read.
    /// </summary>
    public class IoListHMI : Control, IPlcExtraAddresses, UiStyle.IDisplayOnly
    {
        private readonly List<IoPoint> _points;
        private readonly Dictionary<string, int> _values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private List<IoPoint> _shown;
        private string _filter = "";
        private bool _onlyOn;
        private readonly ToolTip _tips = UiStyle.Tips();
        private int _tipRow = -1;

        public IoListHMI(int station, List<IoPoint> points)
        {
            PlcNumber = station;
            _points = points ?? new List<IoPoint>();
            _shown = _points.ToList();
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
            Font = UiStyle.Body;
        }

        public int PlcNumber { get; private set; }
        public string Address { get { return _points.Count > 0 ? _points[0].Read : ""; } }
        public IEnumerable<string> ExtraAddresses { get { return _points.Skip(1).Select(p => p.Read); } }

        /// <summary>The rows on show (for tests).</summary>
        public IReadOnlyList<IoPoint> Shown { get { return _shown; } }

        /// <summary>The point's state as last read: null before the first read.</summary>
        public bool? IsOn(IoPoint p)
        {
            int v;
            return _values.TryGetValue(p.Read, out v) ? v != 0 : (bool?)null;
        }

        public int RowHeight { get { return Font.Height + Math.Max(4, Font.Height * 2 / 5); } }
        public int PreferredHeight { get { return Math.Max(RowHeight, _shown.Count * RowHeight); } }

        public void SetFilter(string text, bool onlyOn)
        {
            _filter = (text ?? "").Trim();
            _onlyOn = onlyOn;
            Refilter();
        }

        /// <summary>Raised when the number of rows on show changes (the page re-lays out).</summary>
        public event EventHandler RowsChanged;

        private void Refilter()
        {
            int before = _shown.Count;
            var q = _filter;
            _shown = _points.Where(p =>
                (q.Length == 0 || p.Address.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 p.Label.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                 p.Read.Equals(q, StringComparison.OrdinalIgnoreCase)) &&
                (!_onlyOn || IsOn(p) == true)).ToList();
            Invalidate();
            if (_shown.Count != before && RowsChanged != null) RowsChanged(this, EventArgs.Empty);
        }

        public void UpdateState(Func<string, int> lookup, Func<SystemCondition, bool> checkGlobal)
        {
            bool changed = false;
            foreach (var p in _points)
            {
                int v = lookup(p.Read);
                int old;
                if (!_values.TryGetValue(p.Read, out old) || old != v) { _values[p.Read] = v; changed = true; }
            }
            if (!changed) return;
            if (_onlyOn) Refilter();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            int rh = RowHeight, lamp = Math.Max(8, Font.Height * 3 / 5);
            int addrWidth = TextRenderer.MeasureText("Y2000", Font).Width;
            bool down = ConnectionMonitor.IsKnownDown(PlcNumber);
            int first = Math.Max(0, e.ClipRectangle.Top / rh), last = Math.Min(_shown.Count - 1, e.ClipRectangle.Bottom / rh);
            var bold = UiStyle.FontVariant(Font, FontStyle.Bold);
            for (int i = first; i <= last; i++)
            {
                var p = _shown[i];
                int y = i * rh;
                if (i % 2 == 1)
                    using (var zebra = new SolidBrush(Color.FromArgb(247, 248, 250))) g.FillRectangle(zebra, 0, y, Width, rh);
                var on = IsOn(p);
                var ring = new Rectangle(lamp / 2, y + (rh - lamp) / 2, lamp, lamp);
                Color fill = on == true ? (p.IsInput ? Color.FromArgb(76, 175, 80) : Color.FromArgb(255, 170, 0))
                                        : Color.FromArgb(225, 225, 225);
                if (down || on == null) fill = Color.FromArgb(240, 240, 240);
                using (var b = new SolidBrush(fill)) g.FillEllipse(b, ring);
                using (var pen = new Pen(Color.FromArgb(down ? 175 : 25, down ? 181 : 28, down ? 190 : 34), Math.Max(1.2f, lamp / 10f)))
                    g.DrawEllipse(pen, ring);
                int x = ring.Right + lamp / 2 + 2;
                TextRenderer.DrawText(g, p.Address, bold, new Rectangle(x, y, addrWidth, rh), Color.FromArgb(40, 46, 56),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                x += addrWidth + 4;
                TextRenderer.DrawText(g, p.Label, Font, new Rectangle(x, y, Math.Max(0, Width - x - 2), rh), Color.FromArgb(60, 66, 76),
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int row = e.Y / Math.Max(1, RowHeight);
            if (row == _tipRow) return;
            _tipRow = row;
            if (row < 0 || row >= _shown.Count) { _tips.SetToolTip(this, ""); return; }
            var p = _shown[row];
            string tip = p.Address + "  " + p.Label;
            if (!string.Equals(p.Read, p.Address, StringComparison.OrdinalIgnoreCase))
                tip += "\n" + string.Format(LanguageManager.GetString("Tip_IoRead", "read at {0}, the relay the PLC copies it into"), p.Read);
            _tips.SetToolTip(this, tip);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _tipRow = -1;
            _tips.Hide(this);
        }
    }
}
