using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Kaban.Core.Helpers
{
    /// <summary>
    /// THE FAILED MOVEMENT, ON THE MACHINE (30 September).
    ///
    /// The cylinder manual speaks of "the output", "the relay", "the target
    /// sensor". For a movement that failed on this machine they are known - the
    /// unit names its command and sensors, the drawings (WiringTable) say which
    /// output its command drives, through which relay and coil, on which valve,
    /// hoses and cylinder. Chain gathers them for the step-by-step troubleshooter
    /// (TroubleshootingCatalog.Steps.cs).
    /// </summary>
    public static partial class TroubleshootingCatalog
    {
        /// <summary>What the manual's words mean for one movement on this machine.</summary>
        private sealed class Chain
        {
            public MovementInfo Mv;
            /// <summary>The valve as it acts (an island's valve: the valve it is - BaseValve).</summary>
            public ValveType Valve;
            /// <summary>The valve is on a valve island (12 November): the island powers its coil and feeds its air.</summary>
            public bool Island;
            public FeedbackType Feedback;
            /// <summary>The coil outputs: this movement's and the other side's (a double solenoid's second coil).</summary>
            public WiringTable.Point Out, OtherOut;
            public MovementInfo.Device OutDevice, OtherDevice;
            public readonly List<KeyValuePair<MovementInfo.Device, WiringTable.Point>> On = new List<KeyValuePair<MovementInfo.Device, WiringTable.Point>>();
            public readonly List<KeyValuePair<MovementInfo.Device, WiringTable.Point>> Off = new List<KeyValuePair<MovementInfo.Device, WiringTable.Point>>();
            public WiringTable.Point MainAir;
            public string ErrorText = "";

            /// <summary>The point that carries the cylinder: valve, hoses, cylinder, measurement, pressure.</summary>
            public WiringTable.Point Air
            {
                get
                {
                    if (Out != null && (Out.Valve.Length > 0 || Out.Cylinder.Length > 0)) return Out;
                    if (OtherOut != null && (OtherOut.Valve.Length > 0 || OtherOut.Cylinder.Length > 0)) return OtherOut;
                    return Out ?? OtherOut;
                }
            }

            public bool DoubleCoil
            {
                get { return Valve == ValveType.DoubleSolenoid || Valve == ValveType.ClosedCentre || Valve == ValveType.OpenCentre; }
            }

            public static Chain Build(MovementInfo mv, ValveType valve, FeedbackType feedback)
            {
                var c = new Chain { Mv = mv, Valve = (ValveType)BaseValve((int)valve), Island = IslandValve((int)valve), Feedback = feedback };
                Func<MovementInfo.Device, MovementInfo.Device, WiringTable.Point> driven = (output, command) =>
                {
                    var w = output != null && !string.IsNullOrWhiteSpace(output.Address) ? WiringTable.Find(mv.Station, output.Address) : null;
                    return w ?? (command != null ? WiringTable.OutputFor(mv.Station, command.Address) : null);
                };
                c.Out = driven(mv.Output, mv.Command);
                c.OtherOut = driven(mv.OtherOutput, mv.OtherCommand);
                c.OutDevice = mv.Output ?? (c.Out != null ? new MovementInfo.Device { Address = c.Out.Address } : null);
                c.OtherDevice = mv.OtherOutput ?? (c.OtherOut != null ? new MovementInfo.Device { Address = c.OtherOut.Address } : null);
                foreach (var d in mv.Feedback) c.On.Add(new KeyValuePair<MovementInfo.Device, WiringTable.Point>(d, WiringTable.Find(mv.Station, d.Address)));
                foreach (var d in mv.OppositeFeedback) c.Off.Add(new KeyValuePair<MovementInfo.Device, WiringTable.Point>(d, WiringTable.Find(mv.Station, d.Address)));
                // The machine's main air switch, as its drawing names it ("6 bar Pressure Input").
                c.MainAir = WiringTable.Points(mv.Station)
                                      .FirstOrDefault(p => p.IsInput && p.Text.IndexOf("Pressure Input", StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrWhiteSpace(mv.Error))
                {
                    var def = AlarmManager.FindByStationAndAddress(mv.Station, mv.Error);
                    if (def != null) c.ErrorText = !string.IsNullOrWhiteSpace(def.TranslationKey) ? LanguageManager.GetString(def.TranslationKey, def.TranslationKey) : def.AlarmCode ?? "";
                }
                return c;
            }
        }

        // ---- small builders -------------------------------------------------

        private static string E(string s) { return WebUtility.HtmlEncode(s ?? ""); }
        private static string Code(string address) { return "<code>" + E((address ?? "").Trim()) + "</code>"; }

        private static string State(int? v, bool wantOn)
        {
            if (!v.HasValue) return "";
            bool on = v.Value != 0;
            string cls = on == wantOn ? (on ? "st-on" : "st-off") : "st-bad";
            return "<span class=\"" + cls + "\">" + (on ? "ON" : "OFF") + "</span>";
        }

        private static string CylinderName(WiringTable.Point air)
        {
            if (air == null) return "";
            return air.Cylinder.Length > 0 ? air.Cylinder : air.Text;
        }
    }
}
