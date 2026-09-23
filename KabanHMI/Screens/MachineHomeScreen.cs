using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Screens
{
    /// <summary>
    /// The screen an operator looks at all day.
    ///
    /// It is built from a DESCRIPTION rather than hand-laid per machine, because
    /// every machine wants the same five things and only the addresses differ:
    ///
    ///     cycle control      start / pause / cancel, and the state lamp that
    ///                        says which of them is true right now
    ///     axis positions     live, large enough to read across the shop
    ///     speed override     the one setting an operator changes hourly
    ///     job                what is loaded, and a way to load another
    ///     recent trend       a small chart, so a drifting axis is visible
    ///                        before it becomes a scrapped profile
    ///
    /// A machine supplies a MachineHomeSpec; anything it leaves empty is simply
    /// not drawn, so a machine with no override or no job file still gets a
    /// sensible screen instead of dead controls.
    /// </summary>
    public class MachineHomeScreen : UserControl
    {
        public class AxisSpec
        {
            public string Caption = "";
            public string Address = "";
            public DataTypeEnum DataType = DataTypeEnum.Bin_32;
            public double Divisor = 10000;
            public int DecimalPlaces = 1;
            public string Suffix = "mm";
            /// <summary>Optional: the axis's reference/home sensor.</summary>
            public string HomedAddress = "";
        }

        public class MachineHomeSpec
        {
            public string MachineName = "";
            public int PlcNumber = 1;

            public string StartAddress = "", PauseAddress = "", CancelAddress = "", ResetAddress = "";
            public string RunningLamp = "", PausedLamp = "", AlarmLamp = "", ReadyLamp = "";

            public readonly List<AxisSpec> Axes = new List<AxisSpec>();

            public string OverrideAddress = "";
            public double OverrideDivisor = 1;

            public string JobNameAddress = "";      // word block holding the job name
            public string JobCountAddress = "";
            public string JobDoneAddress = "";

            /// <summary>Address the trend chart samples. Usually the leading axis.</summary>
            public string TrendAddress = "";
            public double TrendDivisor = 10000;
            public string TrendCaption = "";
        }

        private readonly MachineHomeSpec _spec;
        private readonly TrendChart _trend = new TrendChart();
        private readonly Timer _sampler = new Timer();

        public MachineHomeScreen(MachineHomeSpec spec)
        {
            _spec = spec ?? new MachineHomeSpec();
            BackColor = Color.White;
            AutoScroll = true;
            Size = new Size(1060, 700);
            Build();
        }

        private void Build()
        {
            int x = 16, y = 16;

            // ---------- cycle control -------------------------------------
            var cycle = NewGroup("Cycle", x, y, 500, 150);
            Controls.Add(cycle);

            int bx = 14;
            if (_spec.StartAddress != "")
                cycle.Controls.Add(CycleButton("Start", _spec.StartAddress, bx, 30,
                                               Color.FromArgb(40, 130, 70), out bx));
            if (_spec.PauseAddress != "")
                cycle.Controls.Add(CycleButton("Pause", _spec.PauseAddress, bx, 30,
                                               Color.FromArgb(180, 130, 30), out bx));
            if (_spec.CancelAddress != "")
                cycle.Controls.Add(CycleButton("Cancel", _spec.CancelAddress, bx, 30,
                                               Color.FromArgb(170, 60, 60), out bx));
            if (_spec.ResetAddress != "")
                cycle.Controls.Add(CycleButton("Reset", _spec.ResetAddress, bx, 30,
                                               Color.FromArgb(90, 100, 120), out bx));

            int lx = 14;
            foreach (var lamp in new[]
                     {
                         new { Cap = "Ready",   Addr = _spec.ReadyLamp,   On = Color.FromArgb(0, 150, 80) },
                         new { Cap = "Running", Addr = _spec.RunningLamp, On = Color.FromArgb(0, 150, 80) },
                         new { Cap = "Paused",  Addr = _spec.PausedLamp,  On = Color.FromArgb(210, 160, 40) },
                         new { Cap = "Alarm",   Addr = _spec.AlarmLamp,   On = Color.FromArgb(200, 60, 60) }
                     })
            {
                if (lamp.Addr == "") continue;
                var l = new LampHMI
                {
                    PlcNumber = _spec.PlcNumber,
                    Address = lamp.Addr,
                    DescriptionText = lamp.Cap,
                    OnColor = lamp.On,
                    Location = new Point(lx, 96),
                    Size = new Size(114, 26)
                };
                cycle.Controls.Add(l);
                lx += 120;
            }

            // ---------- axis positions ------------------------------------
            y += 160;
            int axisH = 44 + Math.Max(1, _spec.Axes.Count) * 38;
            var axes = NewGroup("Axis positions", x, y, 500, axisH);
            Controls.Add(axes);

            int ay = 30;
            foreach (var a in _spec.Axes)
            {
                var d = new NumericDisplayHMI
                {
                    PlcNumber = _spec.PlcNumber,
                    Address = a.Address,
                    DataType = a.DataType,
                    Divisor = a.Divisor,
                    DecimalPlaces = a.DecimalPlaces,
                    Digits = 7,
                    Suffix = a.Suffix,
                    UnitType = a.Suffix == "mm" ? UnitType.Length : UnitType.None,
                    DescriptionText = a.Caption,
                    Location = new Point(14, ay),
                    Size = new Size(330, 32),
                    Font = new Font("Segoe UI", 12F, FontStyle.Bold)
                };
                axes.Controls.Add(d);

                // The home sensor sits beside the number on purpose: a position
                // means something different before and after homing, and an
                // operator reading one without the other is the usual way a
                // machine gets driven into a hard stop.
                if (a.HomedAddress != "")
                    axes.Controls.Add(new LampHMI
                    {
                        PlcNumber = _spec.PlcNumber,
                        Address = a.HomedAddress,
                        DescriptionText = "homed",
                        OnColor = Color.FromArgb(0, 150, 80),
                        Location = new Point(352, ay + 3),
                        Size = new Size(120, 26)
                    });
                ay += 38;
            }

            // ---------- job ------------------------------------------------
            int rx = 536;
            var job = NewGroup("Job", rx, 16, 500, 150);
            Controls.Add(job);

            var lblJob = new Label
            {
                Text = "(no job loaded)",
                Location = new Point(14, 32),
                Size = new Size(360, 26),
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 44, 52)
            };
            job.Controls.Add(lblJob);

            var browse = UiStyle.Round(new Button
            {
                Text = LanguageManager.GetString("Btn_OpenJob", "Open job file..."),
                Location = new Point(14, 66),
                Size = new Size(170, 34),
                BackColor = Color.White
            });
            browse.Click += (s, e) => BrowseJob(lblJob);
            job.Controls.Add(browse);

            if (_spec.JobDoneAddress != "" && _spec.JobCountAddress != "")
            {
                job.Controls.Add(new NumericDisplayHMI
                {
                    PlcNumber = _spec.PlcNumber, Address = _spec.JobDoneAddress,
                    DataType = DataTypeEnum.Bin_16, Divisor = 1, DecimalPlaces = 0, Digits = 5,
                    UnitType = UnitType.None, DescriptionText = "Completed",
                    Location = new Point(200, 66), Size = new Size(140, 30)
                });
                job.Controls.Add(new NumericDisplayHMI
                {
                    PlcNumber = _spec.PlcNumber, Address = _spec.JobCountAddress,
                    DataType = DataTypeEnum.Bin_16, Divisor = 1, DecimalPlaces = 0, Digits = 5,
                    UnitType = UnitType.None, DescriptionText = "Total",
                    Location = new Point(348, 66), Size = new Size(140, 30)
                });
            }

            // ---------- speed override -------------------------------------
            var ovr = NewGroup("Speed override", rx, 176, 500, 110);
            Controls.Add(ovr);

            if (_spec.OverrideAddress != "")
            {
                var num = new NumericInputHMI
                {
                    PlcNumber = _spec.PlcNumber,
                    Address = _spec.OverrideAddress,
                    DataType = DataTypeEnum.Bin_16,
                    Divisor = _spec.OverrideDivisor,
                    DecimalPlaces = 0,
                    Digits = 4,
                    Suffix = "%",
                    UnitType = UnitType.None,
                    DescriptionText = "Override",
                    Location = new Point(14, 34),
                    Size = new Size(240, 34),
                    RequiredSecurity = SecurityLevel.Operator
                };
                ovr.Controls.Add(num);

                // Fixed steps beside the box: on a panel, tapping "50%" is one
                // action where typing it is four, and the operator's other hand
                // is usually on the profile.
                int px = 270;
                foreach (var pct in new[] { 25, 50, 75, 100 })
                {
                    var b = UiStyle.Round(new Button
                    {
                        Text = pct + "%",
                        Location = new Point(px, 34),
                        Size = new Size(50, 34),
                        BackColor = Color.White
                    });
                    int value = pct;
                    b.Click += (s, e) =>
                    {
                        var drv = PlcManager.Instance.GetDriver(_spec.PlcNumber);
                        if (drv == null)
                        {
                            MessageBox.Show(this, PlcManager.Instance.DescribeAccess(_spec.PlcNumber),
                                            "Override", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }
                        drv.Write(_spec.OverrideAddress, (int)(value * _spec.OverrideDivisor));
                        AuditLogger.Log("speed override set to " + value + "%.");
                    };
                    ovr.Controls.Add(b);
                    px += 56;
                }
            }
            else
            {
                ovr.Controls.Add(new Label
                {
                    Text = "This machine has no speed override.",
                    Location = new Point(14, 36), Size = new Size(400, 24),
                    ForeColor = Color.FromArgb(120, 126, 134)
                });
            }

            // ---------- trend ----------------------------------------------
            var trendBox = NewGroup(string.IsNullOrEmpty(_spec.TrendCaption)
                                    ? "Recent movement" : _spec.TrendCaption,
                                    rx, 296, 500, 240);
            Controls.Add(trendBox);
            _trend.Location = new Point(12, 28);
            _trend.Size = new Size(474, 198);
            _trend.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            trendBox.Controls.Add(_trend);

            if (_spec.TrendAddress != "")
            {
                // Sampled on a timer rather than driven by the scanner: the chart
                // wants an even time base, and the scanner's rate changes with how
                // many controls the open page happens to have.
                _sampler.Interval = 500;
                _sampler.Tick += (s, e) =>
                {
                    var drv = PlcManager.Instance.GetDriverForMonitoring(_spec.PlcNumber);
                    if (drv == null) return;
                    int raw;
                    if (drv.TryRead(_spec.TrendAddress, out raw))
                        _trend.Push(raw / (_spec.TrendDivisor == 0 ? 1 : _spec.TrendDivisor));
                };
                _sampler.Start();
            }
            else
            {
                _trend.Message = "No trend source configured.";
            }
        }

        private ButtonHMI CycleButton(string caption, string address, int x, int y,
                                      Color colour, out int nextX)
        {
            var b = new ButtonHMI
            {
                PlcNumber = _spec.PlcNumber,
                FallbackText = caption,
                Location = new Point(x, y),
                Size = new Size(112, 52),
                ColorActive = colour,
                LogAction = true,
                RequiredSecurity = SecurityLevel.Operator
            };
            b.BitActions.Add(new Kaban.Core.Models.BitAction
            {
                Address = address,
                Action = BitActionType.Momentary
            });
            nextX = x + 120;
            return b;
        }

        private GroupBoxHMI NewGroup(string title, int x, int y, int w, int h)
        {
            return new GroupBoxHMI
            {
                // The machine's own station, like every other control here - a
                // frame given an enable or visible address must read THIS machine.
                PlcNumber = _spec.PlcNumber,
                TitleText = title,
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = Color.White
            };
        }

        private void BrowseJob(Label target)
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Title = LanguageManager.GetString("Btn_OpenJob", "Open job file...");
                dlg.Filter = "Job files (*.job;*.csv;*.xml)|*.job;*.csv;*.xml|All files (*.*)|*.*";
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                target.Text = System.IO.Path.GetFileName(dlg.FileName);
                AuditLogger.Log("job file selected: " + dlg.FileName);

                // Deliberately only SELECTS. Sending a job to the PLC is a
                // machine-specific transfer, and a file browser that silently
                // started a cycle would be the wrong kind of convenient.
                MessageBox.Show(this,
                    LanguageManager.GetString("Msg_JobSelected",
                        "Job file selected. Sending it to the machine is not wired up yet."),
                    LanguageManager.GetString("Btn_OpenJob", "Open job file..."),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _sampler.Stop(); _sampler.Dispose(); }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// A small rolling line chart. Deliberately tiny and dependency-free - the
    /// point is to see that a value is moving and roughly where, not to replace
    /// a proper trend tool.
    /// </summary>
    public class TrendChart : Control
    {
        private readonly Queue<double> _points = new Queue<double>();
        private const int Capacity = 240;          // 2 minutes at 500ms

        public string Message = "";

        public TrendChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            BackColor = Color.White;
        }

        public void Push(double v)
        {
            _points.Enqueue(v);
            while (_points.Count > Capacity) _points.Dequeue();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);

            var frame = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var pen = new Pen(Color.FromArgb(210, 214, 220))) g.DrawRectangle(pen, frame);

            if (_points.Count < 2)
            {
                TextRenderer.DrawText(g, string.IsNullOrEmpty(Message) ? "waiting for data..." : Message,
                    Font, frame, Color.FromArgb(140, 146, 154),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            var data = _points.ToArray();
            double min = double.MaxValue, max = double.MinValue;
            foreach (var v in data) { if (v < min) min = v; if (v > max) max = v; }
            // A flat trace still needs a scale, or it divides by zero and vanishes.
            if (Math.Abs(max - min) < 1e-9) { max = min + 1; min -= 1; }

            using (var grid = new Pen(Color.FromArgb(238, 240, 243)))
                for (int i = 1; i < 4; i++)
                {
                    int gy = (int)(Height * i / 4.0);
                    g.DrawLine(grid, 1, gy, Width - 2, gy);
                }

            var pts = new PointF[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                float px = 2 + (Width - 5f) * i / Math.Max(1, data.Length - 1);
                float py = (float)(Height - 3 - (data[i] - min) / (max - min) * (Height - 8));
                pts[i] = new PointF(px, py);
            }
            using (var line = new Pen(Color.FromArgb(30, 120, 220), 1.8f)) g.DrawLines(line, pts);

            TextRenderer.DrawText(g, max.ToString("0.0"), Font,
                new Point(4, 2), Color.FromArgb(130, 136, 144));
            TextRenderer.DrawText(g, min.ToString("0.0"), Font,
                new Point(4, Height - 18), Color.FromArgb(130, 136, 144));
            TextRenderer.DrawText(g, data[data.Length - 1].ToString("0.0"), Font,
                new Point(Width - 60, 2), Color.FromArgb(30, 120, 220));
        }
    }
}

