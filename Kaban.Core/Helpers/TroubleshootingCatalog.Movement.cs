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
    /// hoses and cylinder - so the tailored manual says them:
    ///
    ///   * at the top, the movement's devices WITH THEIR STATES just before the
    ///     error bit came on (the unit's watcher keeps them) and now - what was
    ///     missing - and which details to check first because of it;
    ///   * in every check of details 1.1 - 1.8, what to put the meter, the eye or
    ///     the hand on: the address and card terminal, the wire and sheet, the
    ///     relay, the coil, the valve, the hoses, the sensor, the encoder.
    /// </summary>
    public static partial class TroubleshootingCatalog
    {
        /// <summary>What the manual's words mean for one movement on this machine.</summary>
        private sealed class Chain
        {
            public MovementInfo Mv;
            public ValveType Valve;
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
                var c = new Chain { Mv = mv, Valve = valve, Feedback = feedback };
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
                    if (def != null) c.ErrorText = LanguageManager.GetString(def.TranslationKey, def.AlarmCode ?? "");
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

        /// <summary>"at the alarm ON &middot; now OFF", or "" when nothing is known.</summary>
        private static string States(MovementInfo.Device d, bool wantOn)
        {
            if (d == null || (!d.AtAlarm.HasValue && !d.Now.HasValue)) return "";
            var parts = new List<string>();
            if (d.AtAlarm.HasValue) parts.Add("at the alarm " + State(d.AtAlarm, wantOn));
            if (d.Now.HasValue) parts.Add("now " + State(d.Now, wantOn));
            return " <span class=\"states\">(" + string.Join(" &middot; ", parts) + ")</span>";
        }

        /// <summary>A sensor: its address, drawing tag, wire, sheet and card terminal, and the relay the HMI shows it at.</summary>
        private static string SensorText(KeyValuePair<MovementInfo.Device, WiringTable.Point> s, bool wantOn)
        {
            var d = s.Key; var p = s.Value;
            string name = !string.IsNullOrWhiteSpace(d.Name) ? d.Name : p != null ? p.Text : "";
            if (p == null)
                return Code(d.Address) + (name.Length > 0 ? " " + E(name) : "") + States(d, wantOn);
            var sb = new StringBuilder();
            sb.Append(Code(p.Address));
            if (p.Tag.Length > 0) sb.Append(" sensor " + E(p.Tag));
            if (p.Wire.Length > 0) sb.Append(", wire " + E(p.Wire));
            if (p.Sheet.Length > 0) sb.Append(", sheet " + E(p.Sheet));
            if (p.Card.Length > 0) sb.Append(" (" + E(p.Card) + ")");
            if (p.Via.Length > 0) sb.Append(" (read through " + E(p.Via) + ")");
            if (p.Text.Length > 0) sb.Append(" &ndash; " + E(p.Text));
            if (!string.Equals(p.Address, d.Address, StringComparison.OrdinalIgnoreCase))
                sb.Append("; the HMI shows it at " + Code(d.Address));
            // The page's own name for it, when it says something else.
            if (!string.IsNullOrWhiteSpace(d.Name) && !string.Equals(d.Name.Trim(), p.Text, StringComparison.OrdinalIgnoreCase))
                sb.Append(" (&quot;" + E(d.Name.Trim()) + "&quot;)");
            sb.Append(States(d, wantOn));
            return sb.ToString();
        }

        private static string Sensors(IEnumerable<KeyValuePair<MovementInfo.Device, WiringTable.Point>> list, bool wantOn)
        {
            return string.Join("; ", list.Select(s => SensorText(s, wantOn)));
        }

        private static string Note(params string[] lines)
        {
            var shown = lines.Where(l => !string.IsNullOrEmpty(l)).ToList();
            if (shown.Count == 0) return "";
            return "<div class=\"here\"><b>On this machine:</b> " + string.Join("<br>", shown) + "</div>";
        }

        private static string CylinderName(WiringTable.Point air)
        {
            if (air == null) return "";
            return air.Cylinder.Length > 0 ? air.Cylinder : air.Text;
        }

        // ---- what was missing ------------------------------------------------

        /// <summary>
        /// Which details to check first, from what was missing just before the
        /// alarm: the valve output never came on (the PLC did not drive it), both
        /// coils were on, the cylinder never left its start (the other end's
        /// sensor still on), or it left but its target sensor did not come on.
        /// Empty when the states are not known.
        /// </summary>
        private static List<string> StartOrder(Chain c, out string why)
        {
            why = "";
            var mv = c.Mv;
            int? output = c.OutDevice != null ? c.OutDevice.AtAlarm : null;
            int? other = c.OtherDevice != null ? c.OtherDevice.AtAlarm : null;
            bool anyOn = c.On.Any(s => s.Key.AtAlarm == 1);
            var opposite = c.Off.Where(s => s.Key.AtAlarm.HasValue).ToList();
            bool oppositeKnown = opposite.Count > 0;
            bool leftStart = oppositeKnown && opposite.All(s => s.Key.AtAlarm == 0);
            string outName = c.OutDevice != null ? Code(c.OutDevice.Address) : "the valve output";
            string target = c.On.Count > 0 ? string.Join(" / ", c.On.Select(s => Code(s.Value != null ? s.Value.Address : s.Key.Address))) : "the target feedback";
            string start = oppositeKnown ? string.Join(" / ", opposite.Select(s => Code(s.Value != null ? s.Value.Address : s.Key.Address))) : "";

            if (output == 0)
            {
                why = "The valve output " + outName + " was <b>OFF</b> when the movement failed: the PLC did not drive it " +
                      "(an interlock, a condition of the movement, or the output itself). Start at the output and work towards the valve.";
                return new List<string> { "1.6", "1.5", "1.3" };
            }
            if (output == 1 && other == 1 && c.DoubleCoil)
            {
                why = "<b>Both coils</b> were ON (" + outName + " and " + Code(c.OtherDevice.Address) + "): the valve cannot switch.";
                return new List<string> { "1.4", "1.6" };
            }
            if (anyOn)
            {
                why = "The target sensor " + target + " was ON, yet the movement was not confirmed: the other end's sensor, or the time allowed.";
                return new List<string> { "1.7", "1.8" };
            }
            if (output == 1 && oppositeKnown && !leftStart)
            {
                why = "The valve output " + outName + " was ON, but " + start + " at the start was <b>still ON</b>: the cylinder did not leave its start. " +
                      "Follow the chain from the output: relay, coil and valve, air, mechanics.";
                return new List<string> { "1.5", "1.3", "1.2", "1.1" };
            }
            if (output == 1 && leftStart)
            {
                why = "The valve output " + outName + " was ON and the cylinder left its start (" + start + " OFF), but " + target +
                      " did <b>not</b> come on: the target sensor, or the cylinder stopped on the way.";
                return new List<string> { "1.7", "1.1", "1.2", "1.8" };
            }
            if (output == 1)
            {
                why = "The valve output " + outName + " was ON, but " + target + " did <b>not</b> come on.";
                return new List<string> { "1.5", "1.3", "1.2", "1.1", "1.7" };
            }
            if (oppositeKnown && !leftStart)
            {
                why = start + " at the start was <b>still ON</b>: the cylinder did not leave its start.";
                return new List<string> { "1.6", "1.5", "1.3", "1.2", "1.1" };
            }
            if (leftStart)
            {
                why = "The cylinder left its start (" + start + " OFF), but " + target + " did <b>not</b> come on.";
                return new List<string> { "1.7", "1.1", "1.2", "1.8" };
            }
            return new List<string>();
        }

        // ---- the box at the top ----------------------------------------------

        /// <summary>
        /// The failed movement's devices, as a small table in the note at the top -
        /// each with where it is on the drawings and, when the unit watched it,
        /// its state just before the alarm and now - then what that says to
        /// check first.
        /// </summary>
        private static string MovementBox(Chain c, List<string> order, string why)
        {
            var mv = c.Mv;
            var rows = new List<string>();
            Action<string, string> row = (caption, value) =>
            {
                if (!string.IsNullOrEmpty(value)) rows.Add("<tr><td>" + caption + "</td><td>" + value + "</td></tr>");
            };
            Func<MovementInfo.Device, string> plain = d =>
            {
                if (d == null || string.IsNullOrWhiteSpace(d.Address)) return "";
                var label = PlcLabelTable.ByAddress(mv.Station, d.Address);
                var text = !string.IsNullOrWhiteSpace(d.Name) ? d.Name : label != null ? label.Label : "";
                return Code(d.Address) + (text.Length > 0 ? " " + E(text) : "");
            };
            Func<WiringTable.Point, MovementInfo.Device, bool, string> output = (p, d, wantOn) =>
            {
                if (p == null) return d != null ? plain(d) + States(d, wantOn) : "";
                return Code(p.Address) + (p.Text.Length > 0 ? " " + E(p.Text) : "") + States(d, wantOn) +
                       "<br><span class=\"ref\">" + E(p.Electrical()) + (p.Card.Length > 0 ? " &ndash; " + E(p.Card) : "") + "</span>";
            };

            string side = mv.Side == "A" ? "A (left button)" : mv.Side == "B" ? "B (right button)" : mv.Side;
            row("Movement that failed:", E((mv.Name ?? "").Trim()) +
                                         (side.Length > 0 ? (string.IsNullOrWhiteSpace(mv.Name) ? "" : " &mdash; ") + "movement " + side : ""));
            if (!string.IsNullOrWhiteSpace(mv.Error))
                row("Alarm (error bit):", Code(mv.Error) + (c.ErrorText.Length > 0 ? " " + E(c.ErrorText) : ""));
            row("Command (PLC bit):", plain(mv.Command) + States(mv.Command, true));
            row("Valve output:", output(c.Out, c.OutDevice, true));
            var air = c.Air;
            if (air != null && air.Pneumatic().Length > 0) row("Valve and cylinder:", E(air.Pneumatic()));
            if (c.DoubleCoil)
                row("Other coil (must be OFF):", output(c.OtherOut, c.OtherDevice, false));
            row("Must turn ON (feedback):", string.Join("<br>", c.On.Select(s => SensorText(s, true))));
            row("Must turn OFF (other end):", string.Join("<br>", c.Off.Select(s => SensorText(s, false))));
            if (air != null && air.Measure.Length > 0)
                row("Position measured by:", E(air.Measure) + (mv.Position.Length > 0 ? " &ndash; shown at " + Code(mv.Position) : ""));
            if (air != null && air.Pressure.Length > 0) row("Pressure:", E(air.Pressure));

            var sb = new StringBuilder();
            sb.Append("<p><b>This alarm" + (string.IsNullOrWhiteSpace(mv.Unit) ? "" : " - " + E(mv.Unit)) + "</b></p>");
            sb.Append("<table class=\"movement\">" + string.Join("", rows) + "</table>");
            bool states = c.On.Concat(c.Off).Any(s => s.Key.AtAlarm.HasValue) || (c.OutDevice != null && c.OutDevice.AtAlarm.HasValue);
            if (states)
                sb.Append("<p class=\"ref\">&quot;At the alarm&quot;: the last reading before the error bit came on" +
                          (mv.AlarmTime.HasValue ? " (" + mv.AlarmTime.Value.ToString("HH:mm:ss") + ")" : "") +
                          (mv.SeenRising ? "" : " &ndash; the alarm was already on when this page started reading, so these are from just after it") +
                          ". Red: not as it should be.</p>");
            if (why.Length > 0 && order.Count > 0)
                sb.Append("<div class=\"hint\"><b>Where to start:</b> " + why + " Check " +
                          string.Join(", ", order.Select(d => "<b>" + d + "</b>")) + " first (marked in the chart).</div>");
            bool drawn = c.Out != null || c.OtherOut != null || c.On.Concat(c.Off).Any(s => s.Value != null);
            if (drawn)
                sb.Append("<p class=\"ref\">Sheet 12.6 is page 12, column 6 of the electrical drawing; pneumatic sheet 2 E3 is page 2, zone E3 of the pneumatic drawing. " +
                          "0 V is wire 21, the sensors' +24 V is wire 20.</p>");
            sb.Append("<p>Each check below says, under <b>On this machine</b>, what to measure or look at for this movement.</p>");
            return sb.ToString();
        }

        // ---- the notes in the chart ------------------------------------------

        /// <summary>What check <paramref name="check"/> of a detail means on this machine, as HTML ("" for nothing).</summary>
        private static string Here(Chain c, string detail, int variant, int check)
        {
            var mv = c.Mv;
            var air = c.Air;
            var o = c.Out;
            string outCode = c.OutDevice != null ? Code(c.OutDevice.Address) : "";
            string cyl = CylinderName(air);
            string valve = air != null && air.Valve.Length > 0 ? "valve " + E(air.Valve) : "";
            string hoses = air != null && air.Hoses.Length > 0 ? (air.Hoses.Contains("/") ? "hoses " : "hose ") + E(air.Hoses) : "";
            string pn = air != null && air.PnSheet.Length > 0 ? "pneumatic sheet " + E(air.PnSheet) : "";
            string cmd = mv.Command != null ? Code(mv.Command.Address) : "";

            switch (detail)
            {
                case "1.1":
                    if (check == 1)
                        return Note(cyl.Length > 0 ? E(cyl) + (pn.Length > 0 ? " (" + pn + ")" : "") + "." : "",
                                    hoses.Length > 0 ? "Exhaust both sides: " + hoses + (valve.Length > 0 ? " at " + valve : "") + "." : "");
                    if (check == 2)
                        return Note(hoses.Length > 0 && cyl.Length > 0 ? "With " + hoses + " exhausted, move " + E(cyl) + " by hand." : "");
                    if (check == 3)
                        return Note(air != null && air.Pressure.Length > 0 ? "Its pressure: " + E(air.Pressure) + "." : "");
                    return "";

                case "1.2":
                    if (check == 1)
                        return Note(c.MainAir != null
                                        ? "Main air: " + E(c.MainAir.Text) + " &ndash; " + Code(c.MainAir.Address) +
                                          (c.MainAir.Tag.Length > 0 ? " " + E(c.MainAir.Tag) : "") +
                                          (c.MainAir.Wire.Length > 0 ? ", wire " + E(c.MainAir.Wire) : "") +
                                          (c.MainAir.Sheet.Length > 0 ? ", sheet " + E(c.MainAir.Sheet) : "") +
                                          (c.MainAir.Read.Length > 0 ? "; the HMI shows it at " + Code(c.MainAir.Read) : "") + "."
                                        : "",
                                    air != null && air.Pressure.Length > 0 ? "Clamping: " + E(air.Pressure) + "." : "");
                    if (check == 2)
                        return Note(hoses.Length > 0 ? E(hoses.Substring(0, 1).ToUpperInvariant() + hoses.Substring(1)) +
                                                       (valve.Length > 0 ? " between " + valve : "") +
                                                       (cyl.Length > 0 ? " and " + E(cyl) : "") + (pn.Length > 0 ? " (" + pn + ")" : "") +
                                                       "; the flow controls on both ports." : "");
                    return "";

                case "1.3":
                    if (o == null) return "";
                    if (check == 1)
                        return Note(o.Coil.Length > 0
                                        ? "Coil " + E(o.Coil) + (valve.Length > 0 ? " (" + valve + " on the pneumatic drawing)" : "") +
                                          (o.CoilWire.Length > 0 ? ": wire " + E(o.CoilWire) : "") + (o.CoilSheet.Length > 0 ? ", sheet " + E(o.CoilSheet) : "") + "." +
                                          (o.CoilWire.Length > 0 ? " With " + (cmd.Length > 0 ? cmd + " (the button) held" : "the movement commanded") +
                                                                   ": 24 V DC between wire " + E(o.CoilWire) + " and 0 V (wire 21), the coil's LED on." : "") +
                                          (valve.Length > 0 ? " Manual override: on " + valve + "." : "")
                                        : "");
                    if (check == 2)
                        return Note(valve.Length > 0 ? E(valve.Substring(0, 1).ToUpperInvariant() + valve.Substring(1)) + (pn.Length > 0 ? ", " + pn : "") + "." : "");
                    return "";

                case "1.4":
                    if (c.DoubleCoil && o != null)
                    {
                        var other = c.OtherOut;
                        return Note("This movement's coil: " + (o.Coil.Length > 0 ? E(o.Coil) + " from " : "") + outCode + States(c.OutDevice, true) + ".",
                                    other != null ? "The other coil: " + (other.Coil.Length > 0 ? E(other.Coil) + " from " : "") + Code(other.Address) +
                                                    States(c.OtherDevice, false) + " &ndash; must be OFF meanwhile." : "");
                    }
                    if (c.Valve == ValveType.SingleSolenoidSpring)
                    {
                        if (o != null)
                            return Note("Coil " + (o.Coil.Length > 0 ? E(o.Coil) + " from " : "") + outCode + States(c.OutDevice, true) +
                                        " must stay ON for the whole stroke.");
                        if (c.OtherOut != null)
                            return Note("This movement is the spring's: " + Code(c.OtherOut.Address) + States(c.OtherDevice, false) +
                                        " must turn OFF and stay OFF.");
                    }
                    return "";

                case "1.5":
                    if (o == null) return "";
                    if (o.Relay.Length == 0)
                        return check == 1 ? Note("The drawing shows no interface relay for " + Code(o.Address) + ".") : "";
                    if (check == 1)
                        return Note("Relay " + E(o.Relay) + (o.RelaySheet.Length > 0 ? " (sheet " + E(o.RelaySheet) + ")" : "") +
                                    ": its coil is fed by " + Code(o.Address) + (o.Wire.Length > 0 ? " through wire " + E(o.Wire) +
                                    " &ndash; 24 V DC between wire " + E(o.Wire) + " and 0 V (wire 21) while the movement is commanded" : "") + ".");
                    if (check == 2)
                        return Note("Its contact" + (o.CoilSheet.Length > 0 ? " (sheet " + E(o.CoilSheet) + ")" : "") +
                                    (o.CoilWire.Length > 0 ? " feeds wire " + E(o.CoilWire) : "") + (o.Coil.Length > 0 ? " to coil " + E(o.Coil) : "") + ".");
                    return "";

                case "1.6":
                    if (o == null) return check == 1 && cmd.Length > 0 ? Note("The command " + cmd + States(mv.Command, true) + ".") : "";
                    if (check == 1)
                        return Note("Output " + Code(o.Address) + (o.Text.Length > 0 ? " " + E(o.Text) : "") + States(c.OutDevice, true) + ".",
                                    cmd.Length > 0 ? "The HMI commands it with " + cmd + States(mv.Command, true) +
                                                     ": in the PLC, watch " + Code(o.Address) + "'s rung while the button is held." : "");
                    if (check == 2)
                        return Note(Code(o.Address) + (o.Card.Length > 0 ? " is " + E(o.Card) : "") +
                                    (o.Wire.Length > 0 ? "; wire " + E(o.Wire) + " leaves it" : "") + (o.Sheet.Length > 0 ? " (sheet " + E(o.Sheet) + ")" : "") + ".");
                    return "";

                case "1.7":
                    return Note(FeedbackLines(c, (FeedbackType)variant, check).ToArray());

                case "1.8":
                    if (check != 1) return "";
                    return Note(!string.IsNullOrWhiteSpace(mv.Error)
                                    ? "The alarm is " + Code(mv.Error) + (c.ErrorText.Length > 0 ? " " + E(c.ErrorText) : "") +
                                      "; its monitoring time is in the PLC program." : "",
                                c.On.Count > 0
                                    ? "Time the stroke from " + (c.OutDevice != null ? outCode : cmd.Length > 0 ? cmd : "the command") +
                                      " ON to " + string.Join(" / ", c.On.Select(s => Code(s.Value != null ? s.Value.Address : s.Key.Address))) + " ON."
                                    : "");
            }
            return "";
        }

        private static IEnumerable<string> FeedbackLines(Chain c, FeedbackType type, int check)
        {
            var air = c.Air;
            string measure = air != null && air.Measure.Length > 0 ? E(air.Measure) : "";
            string position = c.Mv.Position.Length > 0 ? "the HMI shows the position at " + Code(c.Mv.Position) : "";
            string on = Sensors(c.On, true), off = Sensors(c.Off, false);
            var all = c.On.Concat(c.Off).ToList();
            switch (type)
            {
                case FeedbackType.TwoMagnetic:
                    if (check == 1 && on.Length > 0) yield return "Must turn ON: " + on + ".";
                    if (check == 2 && off.Length > 0) yield return "Must turn OFF: " + off + ".";
                    break;
                case FeedbackType.SingleMagnetic:
                case FeedbackType.SingleInductive:
                    if (all.Count == 0) break;
                    var sensor = Sensors(all, c.On.Count > 0);
                    if (check == 1) yield return "The sensor: " + sensor + (c.On.Count > 0 ? " &ndash; at this movement's end." : " &ndash; at the other end.");
                    if (check == 2) yield return "The same sensor must go OFF at its far end: " + Sensors(all, c.On.Count > 0) + ".";
                    if (check == 3 && c.On.Count == 0)
                        yield return "This movement ends where there is no sensor: watch " + E(CylinderName(air).Length > 0 ? CylinderName(air) : "the cylinder") + " reach it.";
                    break;
                case FeedbackType.PressureSwitch:
                    if (check == 1 && air != null && air.Pressure.Length > 0) yield return E(air.Pressure) + ".";
                    if (check == 2 && all.Count > 0)
                        yield return "The sensors: " + string.Join("; ", new[] { on, off }.Where(x => x.Length > 0)) + ".";
                    if (check == 3)
                    {
                        if (air != null && air.Hoses.Length > 0) yield return "The clamp line: hoses " + E(air.Hoses) + (air.PnSheet.Length > 0 ? " (pneumatic sheet " + E(air.PnSheet) + ")" : "") + ".";
                        if (on.Length > 0) yield return "Must turn ON: " + on + ".";
                    }
                    if (check == 4 && on.Length > 0) yield return "Record " + on + ".";
                    break;
                case FeedbackType.AnalogueMagnetic:
                    if (measure.Length > 0) yield return measure + ".";
                    if (position.Length > 0 && check <= 2) yield return E(position.Substring(0, 1).ToUpperInvariant()) + position.Substring(1) + ".";
                    break;
                case FeedbackType.EncoderReference:
                    if ((check == 1 || check == 2) && measure.Length > 0) yield return measure + ".";
                    if ((check == 1 || check == 4) && position.Length > 0) yield return E(position.Substring(0, 1).ToUpperInvariant()) + position.Substring(1) + ".";
                    if (check == 3 && all.Count > 0) yield return "The reference sensor: " + Sensors(all, true) + ".";
                    break;
                default:
                    if (check == 1)
                    {
                        if (on.Length > 0) yield return "Must turn ON: " + on + ".";
                        if (off.Length > 0) yield return "Must turn OFF: " + off + ".";
                    }
                    break;
            }
        }

        /// <summary>
        /// The chart for one movement: each check's "On this machine", and the
        /// details to start with marked in their number cell.
        /// </summary>
        private static string DecorateChart(string html, Chain c, List<string> order)
        {
            return Regex.Replace(html, "<tbody class=\"block\"([^>]*)>(.*?)</tbody>", m =>
            {
                var attributes = m.Groups[1].Value;
                var detail = Regex.Match(attributes, "data-detail=\"([^\"]+)\"").Groups[1].Value;
                var variantMatch = Regex.Match(attributes, "data-variant=\"(?:valve|feedback):(\\d+)\"");
                int variant = variantMatch.Success ? int.Parse(variantMatch.Groups[1].Value) : 0;
                var body = m.Groups[2].Value;

                // Each check: its cause cell, then its check-method cell (the ones
                // without a class - the number and name cells have theirs).
                body = Regex.Replace(body,
                    "(<td rowspan=\"(\\d+)\">)((?:(?!</td>).)*)(</td>\\s*<td rowspan=\"\\2\">)((?:(?!</td>).)*)(</td>)",
                    cm =>
                    {
                        var number = Regex.Match(cm.Groups[3].Value, "^\\s*<p>\\s*\\((\\d)\\)");
                        int check = number.Success ? int.Parse(number.Groups[1].Value) : 1;
                        var note = Here(c, detail, variant, check);
                        return note.Length == 0 ? cm.Value
                            : cm.Groups[1].Value + cm.Groups[3].Value + cm.Groups[4].Value + cm.Groups[5].Value + note + cm.Groups[6].Value;
                    },
                    RegexOptions.Singleline);

                int rank = order.IndexOf(detail);
                if (rank >= 0)
                {
                    string label = rank == 0 ? "start here" : Ordinal(rank + 1);
                    body = Regex.Replace(body, "(<td class=\"no\"[^>]*>(?:(?!</td>).)*?)(</td>)",
                                         nm => nm.Groups[1].Value + "<p class=\"order\">" + label + "</p>" + nm.Groups[2].Value,
                                         RegexOptions.Singleline);
                    attributes += " data-order=\"" + (rank + 1) + "\"";
                }
                return "<tbody class=\"block\"" + attributes + ">" + body + "</tbody>";
            }, RegexOptions.Singleline);
        }

        private static string Ordinal(int n)
        {
            return n + (n == 2 ? "nd" : n == 3 ? "rd" : "th");
        }

        private const string MovementStyle =
            " .here{margin-top:.4em;padding:.3em .45em;background:#eef5fb;border-left:3px solid #2e75b6;font-size:.94em}" +
            " .here b{color:#1f4e79}" +
            " .order{display:inline-block;margin-top:.35em;padding:.05em .35em;background:#ffd966;border-radius:3px;font-weight:bold;font-size:.8em;white-space:nowrap}" +
            " .hint{background:#fff7d6;border:1px solid #e0c060;padding:.4em .6em;margin:.4em 0}" +
            " .st-on{color:#1a7f37;font-weight:bold} .st-off{color:#444;font-weight:bold} .st-bad{color:#c00000;font-weight:bold}" +
            " .states{white-space:nowrap}\n";
    }
}
