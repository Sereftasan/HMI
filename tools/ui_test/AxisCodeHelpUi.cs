using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Models;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

// An axis's "?" follows its three drive codes: one menu entry per code that is
// not zero, none at all when all three are zero. The Axis Settings code cells
// open their own code. The alarm menu's reset pulses the machine's own reset bit.
class AX {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    static T Field<T>(object o, string n) { return (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o); }

    [STAThread]
    static void Main() {
        var host = new Form { ClientSize = new Size(900, 300) };

        Console.WriteLine("--- the axis '?' follows the drive codes ---");
        var axis = new FunctionUnitHMI { Kind = UnitKind.Axis, TitleTextKey = "Robot X",
                                         CommandAAddress = "M600", CommandBAddress = "M601",
                                         PositionAddress = "D100", ErrorBAddress = "M890" };
        axis.SetAxisCodes("W0B", "W4", "W5");
        host.Controls.Add(axis); host.Show(); Application.DoEvents();
        var badge = Field<AlarmHelpBadge>(axis, "_helpB");
        Check("the scanner is asked for the three code words", badge.ExtraAddresses.SequenceEqual(new[] { "W0B", "W4", "W5" }),
              string.Join(",", badge.ExtraAddresses));

        int m890 = 1, w0b = 0, w4 = 0, w5 = 0;
        Func<string, int> plc = a => a == "M890" ? m890 : a == "W0B" ? w0b : a == "W4" ? w4 : a == "W5" ? w5 : 0;

        badge.UpdateState(plc, c => true);
        Check("error bit set but every code 0: NO '?' - nothing to look up", !badge.IsActive && !badge.Visible);
        Check("  the position's border still blinks for the error", axis.PositionDisplay.AlertBlink);

        w0b = 0x163; badge.UpdateState(plc, c => true);
        Check("servo alarm 16.3: the '?' shows", badge.IsActive && badge.Visible);
        Check("  red", badge.MarkColor.ToArgb() == Color.FromArgb(214, 36, 36).ToArgb());
        var docs = badge.Documents();
        Check("  one menu entry: the 16.3 entry of the servo manual",
              docs.Count == 1 && docs[0].Code == "16.3" && docs[0].FileName == AxisCodeHelp.ServoAlarmDocument,
              string.Join(" | ", docs.Select(d => d.Title)));
        Check("  titled with the code, its name and the axis",
              docs.Count == 1 && docs[0].Title.Contains("Servo alarm 16.3") && docs[0].Title.Contains("Encoder") && docs[0].Title.Contains("Robot X"),
              docs.Count == 1 ? docs[0].Title : "");
        Check("  on the top frame line", badge.Top <= 4, "top=" + badge.Top);

        w4 = 0x1904; w5 = 0x0900; badge.UpdateState(plc, c => true);
        docs = badge.Documents();
        Check("three codes set: three entries, servo first, then axis alarm, then warning",
              docs.Count == 3 && docs[0].Code == "16.3" && docs[1].Code == "1904" && docs[2].Code == "0900",
              string.Join(",", docs.Select(d => d.Code)));
        Check("  each entry opens only its own code",
              docs.All(d => { var h = File.ReadAllText(TroubleshootingCatalog.ExtractSection(d));
                              return System.Text.RegularExpressions.Regex.Matches(h, "<section class=\"code\"").Count == 1; }));

        m890 = 0; w0b = 0; w4 = 0; badge.UpdateState(plc, c => true);
        Check("only a warning: the '?' stays", badge.IsActive);
        Check("  drawn AMBER - the axis has not been stopped", badge.MarkColor.ToArgb() == Color.FromArgb(214, 150, 20).ToArgb());
        Check("  and the position does not blink (no error bit)", !axis.PositionDisplay.AlertBlink);

        w5 = 0; badge.UpdateState(plc, c => true);
        Check("all three back to 0: the '?' goes", !badge.IsActive && !badge.Visible);

        w0b = 0xE61; badge.UpdateState(plc, c => true);
        Check("a servo WARNING code (E6.1 forced stop) is amber too", badge.IsActive && badge.MarkColor.ToArgb() == Color.FromArgb(214, 150, 20).ToArgb());
        w0b = 0; badge.UpdateState(plc, c => true);

        Console.WriteLine("--- a cylinder's '?' is unchanged: the error bit ---");
        var cyl = new FunctionUnitHMI { TitleTextKey = "Clamp", CommandAAddress = "M672", ErrorAAddress = "M892" };
        host.Controls.Add(cyl);
        var cb = Field<AlarmHelpBadge>(cyl, "_helpA");
        cb.UpdateState(a => a == "M892" ? 1 : 0, c => true);
        Check("shown while the error bit is set, button A blinks", cb.IsActive && cyl.ButtonA.AlertBlink);
        cb.UpdateState(a => 0, c => true);
        Check("gone with it", !cb.IsActive && !cyl.ButtonA.AlertBlink);

        Console.WriteLine("--- every function unit reads like the Axis Settings row ---");
        var fu = new FunctionUnitHMI { PositionAddress = "D15" };
        Check("default style is Standard", fu.PositionStyle == UnitPositionStyle.Standard);
        Check("  black bold digits on grey (not red on black)",
              fu.PositionDisplay.ForeColor.ToArgb() == Color.Black.ToArgb() &&
              fu.PositionDisplay.BackColor.ToArgb() == Color.Gainsboro.ToArgb() && fu.PositionDisplay.Font.Bold,
              fu.PositionDisplay.ForeColor + " on " + fu.PositionDisplay.BackColor);
        fu.PositionStyle = UnitPositionStyle.Readout;
        Check("  the red readout is still there for a page that asks for it", fu.PositionDisplay.ForeColor.ToArgb() == Color.Red.ToArgb());

        Console.WriteLine("--- the manual-page axis unit takes the codes from its Axis Settings row ---");
        AlarmHelpLinks.DiscoverAll();
        var rowForm = new Form();
        var row = new AxisSettingHMI { PlcNumber = 1, AxisNumber = 7, AxisNameKey = "Robot X",
                                       CommandBackAddress = "M600", CommandForwardAddress = "M601", PositionAddress = "D100",
                                       ErrorAddress = "M890", AxisAlarmAddress = "W4", AxisWarningAddress = "W5", ServoAlarmAddress = "W0B" };
        rowForm.Controls.Add(row);
        AlarmHelpLinks.Harvest(rowForm);
        var unit = new AxisUnitHMI { AxisNumber = 7 };
        var ub = Field<AlarmHelpBadge>(unit.Unit, "_helpB");
        Check("the axis unit resolved its row", unit.IsResolved);
        Check("  and watches the row's three code words", ub.ExtraAddresses.SequenceEqual(new[] { "W0B", "W4", "W5" }),
              string.Join(",", ub.ExtraAddresses));

        Console.WriteLine("--- the Axis Settings code cells ---");
        row.BuildRow();
        var cells = row.Controls.OfType<HexCodeHMI>().ToList();
        Check("three code cells: axis alarm, axis warning, servo alarm",
              cells.Select(c => c.CodeKind).SequenceEqual(new[] { AxisCodeKind.AxisError, AxisCodeKind.AxisWarning, AxisCodeKind.ServoAlarm }),
              string.Join(",", cells.Select(c => c.CodeKind)));
        var err = cells[0];
        err.UpdateState(a => a == "W4" ? 0x1904 : 0, c => true);
        Check("a non-zero code is a link: hand pointer, help ready", err.HasHelp && err.Cursor == Cursors.Hand);
        err.UpdateState(a => 0x51904, c => true);
        Check("  one 16-bit word: the neighbour's high half is not part of the code", err.HasHelp && AxisCodeHelp.Format(err.CodeKind, 0x51904 & 0xFFFF) == "1904H");
        err.UpdateState(a => 0, c => true);
        Check("zero: inert again", !err.HasHelp && err.Cursor == Cursors.Default);
        var plain = new HexCodeHMI { Address = "D1" };
        plain.UpdateState(a => 0x502, c => true);
        Check("a code cell of no kind has no help (as before)", !plain.HasHelp);

        Console.WriteLine("--- the alarm list's help for an axis alarm starts with the code ---");
        AlarmHelpLinks.ReadCodeNow = (st, a) => a == "W0B" ? 0x163 : (int?)0;
        var list = AlarmHelpLinks.DocumentsFor(1, "M890", null);
        Check("16.3's entry first, the whole servo manual after it",
              list.Count >= 2 && list[0].Code == "16.3" && list.Skip(1).Any(d => d.FileName == "MR_J4_TroubleShooting.pdf"),
              string.Join(" | ", list.Select(d => d.FileName + ":" + d.Code)));
        AlarmHelpLinks.ReadCodeNow = (st, a) => 0;
        list = AlarmHelpLinks.DocumentsFor(1, "M890", null);
        Check("no code set: just the manual", list.All(d => string.IsNullOrEmpty(d.Code)) && list.Count >= 1);

        Console.WriteLine("--- the alarm menu's reset: the machine's OWN reset bit ---");
        string dir = Path.Combine(Path.GetTempPath(), "ax_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "main.csv"), "Label;DataType;Address\nPC_AlarmReset;Bit;M102\n");
        File.WriteAllText(Path.Combine(dir, "h500.csv"), "Label;DataType;Address\nMAN_Reset;Bit;M36\nMAN_Lubrication;Bit;M102\n");
        File.WriteAllText(Path.Combine(dir, "other.csv"), "Label;DataType;Address\nPC_AlarmReset;Bit;M28\n");
        File.WriteAllText(Path.Combine(dir, "none.csv"), "Label;DataType;Address\nIN_Start;Bit;X0\n");
        PlcLabelTable.Load(Path.Combine(dir, "main.csv"));
        PlcLabelTable.LoadStation(Path.Combine(dir, "h500.csv"), 3);
        PlcLabelTable.LoadStation(Path.Combine(dir, "other.csv"), 6);
        PlcLabelTable.LoadStation(Path.Combine(dir, "none.csv"), 7);
        string why;
        Check("main machine: its PC_AlarmReset (M102)", AlarmReset.AddressFor(1, out why) == "M102");
        Check("a machine whose PC_AlarmReset is another bit: that bit", AlarmReset.AddressFor(6, out why) == "M28");
        Check("no label, M102 free: M102, as its Alarms page button", AlarmReset.AddressFor(7, out why) == "M102");
        var h500 = AlarmReset.AddressFor(3, out why);
        Check("H500 (M102 is its LUBRICATION): nothing is pulsed, and it says why",
              h500 == null && why.Contains("MAN_Lubrication"), why);

        var sim = new Kaban.Drivers.Simulation.SimulationDriver(7);
        PlcManager.Instance.RegisterDriver(7, sim);
        PlcManager.Instance.RegisterStation(7, "reset test", SecurityLevel.Operator);
        ConnectionMonitor.ReportRead(7, true);
        Func<int> m102 = () => { int v; sim.TryRead("M102", out v); return v; };
        SecurityManager.Logout();
        Check("nobody signed in: not sent, and the reason says so",
              !AlarmReset.Pulse(7, "test", out why) && m102() == 0 && why.Length > 0, why);
        SecurityManager.Login("op", SecurityLevel.Operator);
        Check("an operator: sent", AlarmReset.Pulse(7, "test", out why), why);
        Check("  the bit is ON", m102() == 1);
        var until = DateTime.Now.AddMilliseconds(900);
        while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(20); }
        Check("  and OFF again after the pulse", m102() == 0);
        Check("H500's station is not pulsed at all", !AlarmReset.Pulse(3, "H500", out why) && why.Contains("MAN_Lubrication"), why);

        try { Directory.Delete(dir, true); } catch { }
        host.Close();
        Console.WriteLine(fail == 0 ? "\nAXIS CODE HELP ALL PASS" : "\nAXIS CODE HELP " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
