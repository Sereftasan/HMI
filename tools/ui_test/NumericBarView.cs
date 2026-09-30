using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Core.Interfaces;
using Kaban.Core.Models;
using Kaban.UI.Controls;

// A numeric input in BAR VIEW (29 September): a card - heading with its name,
// minus, the value, a slider over the limits, plus - and each of them writes
// exactly as the keypad does (limits, security, formula, log).
class BV {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    class Plc : IPlcDriver, INonBlockingDriver {
        public readonly Dictionary<string, int> Mem = new Dictionary<string, int>();
        public readonly List<string> Writes = new List<string>();
        public bool IsConnected { get { return true; } }
        public bool IsHealthy { get { return true; } }
        public bool Connect() { return true; }
        public void Disconnect() { }
        public int Read(string a) { int v; return Mem.TryGetValue(a, out v) ? v : 0; }
        public int[] ReadMulti(string[] a) { return a.Select(Read).ToArray(); }
        public int Write(string a, int v) { Mem[a] = v; Writes.Add(a + "=" + v); return 0; }
        public bool TryRead(string a, out int v) { v = Read(a); return true; }
        public bool TryReadMulti(string[] a, out int[] v) { v = ReadMulti(a); return true; }
    }

    class Host : System.ComponentModel.Design.IDesignerHost {
        public bool Loading { get; set; }
        public bool InTransaction { get { return false; } }
        public System.ComponentModel.IContainer Container { get { return null; } }
        public System.ComponentModel.IComponent RootComponent { get { return null; } }
        public string RootComponentClassName { get { return ""; } }
        public string TransactionDescription { get { return ""; } }
        public event EventHandler Activated { add { } remove { } }
        public event EventHandler Deactivated { add { } remove { } }
        public event EventHandler LoadComplete { add { } remove { } }
        public event System.ComponentModel.Design.DesignerTransactionCloseEventHandler TransactionClosed { add { } remove { } }
        public event System.ComponentModel.Design.DesignerTransactionCloseEventHandler TransactionClosing { add { } remove { } }
        public event EventHandler TransactionOpened { add { } remove { } }
        public event EventHandler TransactionOpening { add { } remove { } }
        public void Activate() { }
        public System.ComponentModel.IComponent CreateComponent(Type t) { return null; }
        public System.ComponentModel.IComponent CreateComponent(Type t, string n) { return null; }
        public System.ComponentModel.Design.DesignerTransaction CreateTransaction() { return null; }
        public System.ComponentModel.Design.DesignerTransaction CreateTransaction(string d) { return null; }
        public void DestroyComponent(System.ComponentModel.IComponent c) { }
        public System.ComponentModel.Design.IDesigner GetDesigner(System.ComponentModel.IComponent c) { return null; }
        public Type GetType(string n) { return null; }
        public void AddService(Type t, object o) { }
        public void AddService(Type t, object o, bool p) { }
        public void AddService(Type t, System.ComponentModel.Design.ServiceCreatorCallback c) { }
        public void AddService(Type t, System.ComponentModel.Design.ServiceCreatorCallback c, bool p) { }
        public void RemoveService(Type t) { }
        public void RemoveService(Type t, bool p) { }
        public object GetService(Type t) { return null; }
    }
    class DesignSite : System.ComponentModel.ISite {
        public readonly Host Host = new Host();
        public System.ComponentModel.IComponent Component { get { return null; } }
        public System.ComponentModel.IContainer Container { get { return null; } }
        public bool DesignMode { get { return true; } }
        public string Name { get; set; }
        public object GetService(Type t) { return t == typeof(System.ComponentModel.Design.IDesignerHost) ? Host : null; }
    }

    static Plc plc = new Plc();
    static void Scan(NumericInputHMI f) { f.UpdateState(plc.Read, c => true); }
    static T Get<T>(object o, string n) {
        var p = o.GetType().GetProperty(n, Any);
        return p != null ? (T)p.GetValue(o, null) : (T)o.GetType().GetField(n, Any).GetValue(o);
    }
    static void Mouse(NumericInputHMI f, string what, Point p) {
        var e = new MouseEventArgs(MouseButtons.Left, 1, p.X, p.Y, 0);
        typeof(Control).GetMethod("On" + what, Any, null, new[] { typeof(MouseEventArgs) }, null).Invoke(f, new object[] { e });
    }
    static Point Mid(Rectangle r) { return new Point(r.X + r.Width / 2, r.Y + r.Height / 2); }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }

    [STAThread]
    static void Main() {
        AuditLogger.LogDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "bv_" + Guid.NewGuid().ToString("N"));
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        SecurityManager.Login("admin", SecurityLevel.Admin);
        PlcManager.Instance.RegisterDriver(1, plc);

        var form = new Form { ClientSize = new Size(520, 160), BackColor = Color.White };
        var f = new NumericInputHMI
        {
            PlcNumber = 1, Address = "D100", DescriptionText = "Speed Override", Suffix = "%",
            BarView = true, BarStep = 5, Location = new Point(40, 30), Size = new Size(420, 84), RequiredSecurity = SecurityLevel.Operator
        };
        f.MinLimit.Type = LimitSourceType.Static; f.MinLimit.StaticValue = 0;
        f.MaxLimit.Type = LimitSourceType.Static; f.MaxLimit.StaticValue = 100;
        form.Controls.Add(f);
        form.Show();
        plc.Mem["D100"] = 75;
        Scan(f);
        Pump(100);

        Console.WriteLine("--- the card ---");
        var minus = Get<Rectangle>(f, "MinusArea"); var val = Get<Rectangle>(f, "ValueArea");
        var track = Get<Rectangle>(f, "TrackArea"); var plus = Get<Rectangle>(f, "PlusArea");
        int head = Get<int>(f, "HeaderHeight");
        Check("a heading strip with the name above the controls", head >= 22 && minus.Top >= head, head + " / " + minus);
        Check("minus, value, slider, plus - left to right, on one line",
              minus.Right < val.Left && val.Right < track.Left && track.Right < plus.Left && minus.Top == plus.Top && val.Top == minus.Top,
              minus + " " + val + " " + track + " " + plus);
        Check("  the buttons square, the slider taking the room left", minus.Width == minus.Height && plus.Width == plus.Height && track.Width > 120, track.Width.ToString());
        var parts = new object[4];
        var partAt = typeof(NumericInputHMI).GetMethod("PartAt", Any);
        Check("each part found where it is drawn",
              string.Join(",", new[] { minus, val, track, plus }.Select(r => partAt.Invoke(f, new object[] { Mid(r) }).ToString())) == "Minus,Value,Track,Plus");
        var args = new object[] { null, null };
        typeof(NumericInputHMI).GetMethod("ValueParts", Any).Invoke(f, args);
        Check("the value and its unit apart: '75' and '%'", (string)args[0] == "75" && (string)args[1] == "%", args[0] + " | " + args[1]);
        Check("  the slider at 75% of 0 ... 100", Math.Abs((double)typeof(NumericInputHMI).GetMethod("BarFraction", Any).Invoke(f, new object[] { 75.0 }) - 0.75) < 1e-9);
        var descr = form.Controls.OfType<Label>().FirstOrDefault(l => l.Text.StartsWith("Speed Override"));
        Check("the name is in the heading, not also on the left", descr == null || !descr.Visible || descr.Parent == null);

        if (Environment.GetEnvironmentVariable("BARSHOT") != null)
        {
            using (var bmp = new Bitmap(f.Width + 40, f.Height + 40))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.TranslateTransform(20, 20);
                typeof(NumericInputHMI).GetMethod("PaintCard", Any).Invoke(f, new object[] { g });
                bmp.Save(Environment.GetEnvironmentVariable("BARSHOT"));
            }
        }

        Console.WriteLine("--- plus and minus write a step, as the keypad would ---");
        plc.Writes.Clear();
        Mouse(f, "MouseDown", Mid(plus)); Mouse(f, "MouseUp", Mid(plus));
        Check("plus: 75 + 5 = 80 written to D100", plc.Writes.LastOrDefault() == "D100=80", string.Join(" ", plc.Writes));
        Scan(f);
        Mouse(f, "MouseDown", Mid(minus)); Mouse(f, "MouseUp", Mid(minus));
        Check("minus: 80 - 5 = 75", plc.Writes.LastOrDefault() == "D100=75", string.Join(" ", plc.Writes));
        plc.Mem["D100"] = 98; Scan(f);
        Mouse(f, "MouseDown", Mid(plus)); Mouse(f, "MouseUp", Mid(plus));
        Check("at the top: 98 + 5 stops at the limit, 100", plc.Writes.LastOrDefault() == "D100=100", string.Join(" ", plc.Writes));

        Console.WriteLine("--- held, it repeats ---");
        plc.Mem["D100"] = 20; Scan(f); plc.Writes.Clear();
        Mouse(f, "MouseDown", Mid(plus));
        Pump(900);
        Mouse(f, "MouseUp", Mid(plus));
        int n = plc.Writes.Count;
        Check("held for 0.9 s: several steps, each written", n >= 3, n + ": " + string.Join(" ", plc.Writes));
        Pump(300);
        Check("  and none after the finger lifts", plc.Writes.Count == n);
        f.ConfirmWrite = true;
        var timerField = typeof(NumericInputHMI).GetField("_repeat", Any);
        var t = (Timer)timerField.GetValue(f);
        Check("  (ConfirmWrite set: a press asks, and does not repeat)", t != null && !t.Enabled);
        f.ConfirmWrite = false;

        Console.WriteLine("--- the slider: dragged, written once when released ---");
        plc.Mem["D100"] = 75; Scan(f); plc.Writes.Clear();
        int x50 = track.X + track.Width / 2, y = Mid(track).Y;
        Mouse(f, "MouseDown", new Point(track.X + track.Width / 4, y));
        Mouse(f, "MouseMove", new Point(track.X + track.Width / 3, y));
        Mouse(f, "MouseMove", new Point(x50, y));
        Check("nothing written while dragging", plc.Writes.Count == 0, string.Join(" ", plc.Writes));
        typeof(NumericInputHMI).GetMethod("ValueParts", Any).Invoke(f, args);
        Check("  the value shows where the knob is: 50", (string)args[0] == "50", (string)args[0]);
        Mouse(f, "MouseUp", new Point(x50, y));
        Check("released at the middle: 50 written, once", plc.Writes.Count == 1 && plc.Writes[0] == "D100=50", string.Join(" ", plc.Writes));
        Check("  on BarStep's grid (a knob between 45 and 50 lands on one of them)",
              new[] { 0.46, 0.48, 0.52 }.All(fr => { double v = (double)typeof(NumericInputHMI).GetMethod("ValueAtX", Any).Invoke(f, new object[] { track.X + (int)(track.Width * fr) }); return v % 5 == 0; }));

        Console.WriteLine("--- the same checks as the keypad ---");
        plc.Writes.Clear();
        bool ok = (bool)typeof(NumericInputHMI).GetMethod("WriteBarValue", Any).Invoke(f, new object[] { 150.0 });
        Check("150 asked for: held to the limit, 100", ok && plc.Writes.LastOrDefault() == "D100=100", string.Join(" ", plc.Writes));
        f.Divisor = 10; f.DecimalPlaces = 1; f.BarStep = 0.5;
        f.MaxLimit.StaticValue = 100;
        plc.Mem["D100"] = 755; Scan(f); plc.Writes.Clear();
        Mouse(f, "MouseDown", Mid(plus)); Mouse(f, "MouseUp", Mid(plus));
        Check("Divisor 10: 75.5 + 0.5 is written as 760", plc.Writes.LastOrDefault() == "D100=760", string.Join(" ", plc.Writes));
        f.Divisor = 1; f.DecimalPlaces = 0; f.BarStep = 5;
        f.ReadFormula = "A / B"; f.WriteFormula = "A * B";
        f.MathVariables.Add(new MathVariable { Name = "B", IsConstant = true, ConstantValue = 2 });
        plc.Mem["D100"] = 100; Scan(f); plc.Writes.Clear();
        Mouse(f, "MouseDown", Mid(plus)); Mouse(f, "MouseUp", Mid(plus));
        Check("a WriteFormula applies: shown 50, + 5 = 55 is written as 110", plc.Writes.LastOrDefault() == "D100=110", string.Join(" ", plc.Writes));
        f.ReadFormula = "val"; f.WriteFormula = "val"; f.MathVariables.Clear();

        SecurityManager.Logout();
        plc.Mem["D100"] = 50; Scan(f); plc.Writes.Clear();
        Mouse(f, "MouseDown", Mid(plus)); Mouse(f, "MouseUp", Mid(plus));
        Mouse(f, "MouseDown", Mid(track)); Mouse(f, "MouseUp", Mid(track));
        Check("signed out (Operator needed): nothing is written", plc.Writes.Count == 0, string.Join(" ", plc.Writes));
        SecurityManager.Login("admin", SecurityLevel.Admin);

        Console.WriteLine("--- bar view off: the field as before ---");
        f.BarView = false;
        Pump(50);
        descr = form.Controls.OfType<Label>().FirstOrDefault(l => l.Text.StartsWith("Speed Override"));
        Check("the name back on the left", descr != null && descr.Parent == form, descr == null ? "none" : descr.Text);
        Check("  and no part to press", partAt.Invoke(f, new object[] { Mid(plus) }).ToString() == "None");

        Console.WriteLine("--- a field switched to the bar view at its old size (30 September) ---");
        var small = new NumericInputHMI
        {
            PlcNumber = 1, Address = "D200", DescriptionText = "Saw Diameter", Suffix = "mm", BarView = true, BarStep = 1,
            Location = new Point(40, 120), Size = new Size(116, 26), RequiredSecurity = SecurityLevel.Operator
        };
        small.MinLimit.Type = LimitSourceType.Static; small.MinLimit.StaticValue = 500;
        small.MaxLimit.Type = LimitSourceType.Static; small.MaxLimit.StaticValue = 700;
        form.ClientSize = new Size(520, 320);
        form.Controls.Add(small);
        plc.Mem["D200"] = 600; Scan(small); Pump(50);
        var sm = Get<Rectangle>(small, "MinusArea"); var sv = Get<Rectangle>(small, "ValueArea");
        var st = Get<Rectangle>(small, "TrackArea"); var sp = Get<Rectangle>(small, "PlusArea");
        Check("116 x 26: minus, value, plus - and the slider still there, under the value",
              Get<object>(small, "Shape").ToString() == "Small" && st.Width >= 50 && st.Top >= sv.Bottom && sm.Right < sv.Left && sv.Right < sp.Left,
              sm + " " + sv + " " + st + " " + sp);
        Check("  every part inside the field", new[] { sm, sv, st, sp }.All(r => new Rectangle(0, 0, 116, 26).Contains(r)));
        plc.Writes.Clear();
        Mouse(small, "MouseDown", new Point(st.Right + 2, st.Y + st.Height / 2)); Mouse(small, "MouseUp", new Point(st.Right + 2, st.Y + st.Height / 2));
        Check("  the thin slider drags: past its end writes the maximum (700)", plc.Writes.LastOrDefault() == "D200=700", string.Join(" ", plc.Writes));
        small.Size = new Size(200, 90);
        var tv = Get<Rectangle>(small, "ValueArea"); var tt = Get<Rectangle>(small, "TrackArea"); var tm = Get<Rectangle>(small, "MinusArea");
        Check("200 x 90: the value above, minus - slider - plus under it",
              Get<object>(small, "Shape").ToString() == "Tall" && tv.Bottom <= tm.Top && tt.Top == tm.Top && tt.Width >= 30, tv + " " + tm + " " + tt);
        small.Size = new Size(420, 84);
        Check("420 x 84: the card on one line", Get<object>(small, "Shape").ToString() == "Wide");

        var site = new DesignSite();
        var designed = new NumericInputHMI { Size = new Size(116, 26) };
        designed.Site = site;
        site.Host.Loading = true;
        designed.BarView = true;
        Check("in the designer, while the page loads: the size as saved", designed.Size == new Size(116, 26), designed.Size.ToString());
        designed.BarView = false;
        site.Host.Loading = false;
        designed.BarView = true;
        Check("switched on in the designer: the field grows to the card (380 x 72)", designed.Size == new Size(380, 72), designed.Size.ToString());
        var big = new NumericInputHMI { Size = new Size(500, 100) };
        big.Site = new DesignSite();
        big.BarView = true;
        Check("  a larger field keeps its size", big.Size == new Size(500, 100), big.Size.ToString());
        var running = new NumericInputHMI { Size = new Size(116, 26), BarView = true };
        Check("  in the running program nothing is resized", running.Size == new Size(116, 26));

        form.Close();
        Console.WriteLine(fail == 0 ? "\nNUMERIC BAR VIEW ALL PASS" : "\nNUMERIC BAR VIEW " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
