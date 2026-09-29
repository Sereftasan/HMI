using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

namespace Kaban.UI.Screens
{
    /// <summary>
    /// THE AXIS MONITOR (side page): every axis's position and motor current,
    /// beside whatever page is on screen.
    ///
    /// Nothing is added to it by hand. The axes are the rows of the machines'
    /// Axis Settings pages (AxisSettingHMI), found by the same discovery that
    /// lets a manual-page axis unit mirror its row; an axis is listed while its
    /// row's visibility conditions are met. Three columns - name, position,
    /// current - drawn exactly as on the Axis Settings row, so the same axis
    /// looks the same on both.
    /// </summary>
    public class AxisMonitorPage : UserControl
    {
        private readonly FlowLayoutPanel _list = new FlowLayoutPanel();
        private readonly Label _empty = new Label();

        /// <summary>Column widths: the Axis Settings row's own.</summary>
        internal const int WName = AxisSettingHMI.WAxis, WPosition = AxisSettingHMI.WPosition, WMeter = AxisSettingHMI.WMeter,
                           Gap = AxisSettingHMI.Gap, Left0 = 4;

        public AxisMonitorPage()
        {
            BackColor = Color.White;
            Font = UiStyle.Body;
            Dock = DockStyle.Fill;

            var head = new Panel { Dock = DockStyle.Top, Height = 26, BackColor = Color.White };
            int x = Left0;
            head.Controls.Add(Heading(LanguageManager.GetString("Col_Axis", "Axis"), x, WName));
            x += WName + Gap + Gap;
            head.Controls.Add(Heading(LanguageManager.GetString("Col_AxisPosition", "Position"), x, WPosition));
            x += WPosition + Gap;
            head.Controls.Add(Heading(LanguageManager.GetString("Col_AxisCurrent", "Current"), x, WMeter));

            _list.Dock = DockStyle.Fill;
            _list.FlowDirection = FlowDirection.TopDown;
            _list.WrapContents = false;
            _list.AutoScroll = true;
            _list.BackColor = Color.White;
            _list.Padding = new Padding(0, 0, 0, 8);

            _empty.Dock = DockStyle.Fill;
            _empty.TextAlign = ContentAlignment.MiddleCenter;
            _empty.ForeColor = Color.FromArgb(110, 116, 126);
            _empty.Padding = new Padding(16);
            _empty.Visible = false;
            _empty.Text = LanguageManager.GetString("Msg_NoAxes", "No axis is set up on the Axis Settings pages.");

            Controls.Add(_list);
            Controls.Add(_empty);
            Controls.Add(head);
            Build();
        }

        private static Label Heading(string text, int x, int w)
        {
            return new Label
            {
                Text = text, Location = new Point(x, 4), Size = new Size(w, 18), TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = Color.FromArgb(110, 116, 124)
            };
        }

        /// <summary>The rows listed (tests).</summary>
        internal IList<AxisMonitorRow> Rows { get { return _list.Controls.OfType<AxisMonitorRow>().ToList(); } }

        /// <summary>The axes, grouped by machine when more than one machine has axes.</summary>
        private void Build()
        {
            AlarmHelpLinks.EnsureDiscovered();
            var axes = AlarmHelpLinks.AllAxes.ToList();
            _empty.Visible = axes.Count == 0;
            _list.Visible = axes.Count > 0;

            Func<AlarmHelpLinks.AxisInfo, string> machineOf = a =>
            {
                var st = PlcManager.Instance.GetStation(a.Station);
                string id = st != null ? st.OwnerModuleId : "";
                return string.IsNullOrEmpty(id) ? "" : LanguageManager.GetString("Machine_" + id, id);
            };
            var machines = axes.Select(machineOf).Distinct().ToList();
            string current = null;
            foreach (var a in axes)
            {
                string m = machineOf(a);
                if (machines.Count > 1 && m != current)
                {
                    current = m;
                    _list.Controls.Add(new Label
                    {
                        Text = m, AutoSize = false, Size = new Size(WName + WPosition + WMeter + 3 * Gap, 24),
                        Margin = new Padding(Left0, 8, 0, 0), TextAlign = ContentAlignment.BottomLeft,
                        Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(40, 46, 56)
                    });
                }
                _list.Controls.Add(new AxisMonitorRow(a) { Margin = new Padding(0, 0, 0, 0) });
            }
        }
    }

    /// <summary>
    /// One axis on the monitor: its name, its position and its current, as the
    /// Axis Settings row draws them. Hidden while the row's visibility
    /// conditions are not met.
    /// </summary>
    public class AxisMonitorRow : Panel, IConditionalControl
    {
        public int PlcNumber { get; private set; }
        public SystemCondition VisibilityConditions { get; private set; }
        public string NameKey { get; private set; }

        internal readonly NumericDisplayHMI Position;
        internal readonly MeterHMI Current;

        public AxisMonitorRow(AlarmHelpLinks.AxisInfo a)
        {
            PlcNumber = a.Station;
            VisibilityConditions = a.Visibility;
            NameKey = a.NameKey;
            Size = new Size(AxisMonitorPage.Left0 + AxisMonitorPage.WName + AxisMonitorPage.WPosition + AxisMonitorPage.WMeter + 4 * AxisMonitorPage.Gap + 8,
                            AxisSettingHMI.RowHeight);
            BackColor = Color.White;

            int x = AxisMonitorPage.Left0;
            var name = new Label
            {
                Text = LanguageManager.GetString(a.NameKey ?? "", a.NameKey ?? ""),
                Location = new Point(x, 8), Size = new Size(AxisMonitorPage.WName, 24),
                TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 10F, FontStyle.Regular)
            };
            Controls.Add(name);
            x += AxisMonitorPage.WName + AxisMonitorPage.Gap + AxisMonitorPage.Gap;

            Position = new NumericDisplayHMI
            {
                PlcNumber = a.Station,
                Address = a.Position,
                DataType = a.PositionType,
                Divisor = a.Divisor <= 0 ? 1 : a.Divisor,
                DecimalPlaces = a.Decimals,
                Digits = a.Digits,
                Suffix = a.Suffix,
                UnitType = a.Suffix == "mm" ? UnitType.Length : UnitType.None,
                ComparedAddress = a.RealPosition ?? "",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };
            Position.SetBounds(x, 4, AxisMonitorPage.WPosition, AxisSettingHMI.RowHeight - 8);
            Controls.Add(Position);
            x += AxisMonitorPage.WPosition + AxisMonitorPage.Gap;

            if (!string.IsNullOrWhiteSpace(a.Current))
            {
                Current = new MeterHMI
                {
                    PlcNumber = a.Station,
                    Address = a.Current,
                    Divisor = a.CurrentDivisor <= 0 ? 1 : a.CurrentDivisor,
                    Caption = ""
                };
                Current.UseCurrentBands();
                if (a.CurrentFullScale > 0) Current.ScaleMaximum = a.CurrentFullScale;
                Current.SetBounds(x, 4, AxisMonitorPage.WMeter, AxisSettingHMI.RowHeight - 8);
                Controls.Add(Current);
            }
        }

        public void UpdateState(Func<string, int> readPlc, Func<SystemCondition, bool> checkGlobal)
        {
            bool vis = VisibilityConditions == SystemCondition.None || checkGlobal(VisibilityConditions);
            if (Visible != vis) Visible = vis;
        }
    }
}
