using System;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Drivers.Simulation;
using Kaban.UI.Controls;
using Kaban.UI.Helpers;

class M2 {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}
    static SimulationDriver drv;
    static int V(string a){ int v; drv.TryRead(a, out v); return v; }
    static DateTime t0 = new DateTime(2026, 1, 1, 8, 0, 0);
    static void Press(string a, bool on){ drv.Write(a, on ? 1 : 0); }
    static void Run(double from, double to, double stepSec = 0.1){
        for (double s = from; s <= to + 1e-9; s += stepSec) SimulationMotion.Tick(t0.AddSeconds(s));
    }

    [STAThread] static void Main(){
        drv = new SimulationDriver(9); drv.Connect();
        PlcManager.Instance.RegisterDriver(9, drv);
        SimulationMotion.Clear();

        var clamp = new CylinderUnitHMI { PlcNumber = 9,
            CommandAAddress = "M100", CommandAAction = BitActionType.Momentary, Lamp1AAddress = "M110",
            CommandBAddress = "M101", CommandBAction = BitActionType.Momentary, Lamp1BAddress = "M111" };
        var latched = new CylinderUnitHMI { PlcNumber = 9,
            CommandAAddress = "M200", CommandAAction = BitActionType.SET, Lamp1AAddress = "M210",
            CommandBAddress = "M201", CommandBAction = BitActionType.Alternate, Lamp1BAddress = "M211" };
        var axis = new AxisSettingHMI { PlcNumber = 9, CommandBackAddress = "M300", CommandForwardAddress = "M301",
            PositionAddress = "D300", PositionDataType = DataTypeEnum.Bin_32, Divisor = 100 };
        SimulationMotion.Register(clamp); SimulationMotion.Register(latched); SimulationMotion.Register(axis);
        drv.Write("M111", 1);                                    // the unit starts at the B end

        Console.WriteLine("--- a unit, momentary: the far sensor lets go at once, the near one answers after a second ---");
        Press("M100", true); Run(0, 0.2);
        Check("the right lamp goes out as soon as the left button is pressed", V("M111") == 0);
        Check("  and the left lamp is not on yet", V("M110") == 0);
        Run(0.3, 0.9);
        Check("still not on before a second", V("M110") == 0);
        Run(1.0, 1.1);
        Check("the left lamp comes on after one second of holding", V("M110") == 1);
        Press("M100", false);
        Press("M101", true); Run(1.2, 1.4);
        Check("pressing right now drops the left lamp at once", V("M110") == 0 && V("M111") == 0);
        Press("M101", false); Run(1.5, 1.6);
        Check("let go early: neither lamp - the cylinder is mid stroke", V("M110") == 0 && V("M111") == 0);

        Console.WriteLine("--- SET and ALTERNATE: one press is the whole press ---");
        drv.Write("M211", 1);
        Press("M200", true); Run(2.0, 2.1);
        Check("SET lights its lamp immediately", V("M210") == 1 && V("M211") == 0);
        Press("M201", true); Run(2.2, 2.3);
        Check("ALTERNATE likewise", V("M211") == 1 && V("M210") == 0);

        Console.WriteLine("--- an axis counts while its button is held ---");
        Run(2.4, 3.0);                                           // ticking with nothing pressed
        Press("M301", true); Run(3.1, 5.0);                      // then held for 1.9 s
        double mm = ((V("D301") << 16) | (V("D300") & 0xFFFF)) / 100.0;
        Check("the forward (right) button counts UP", mm > 44 && mm < 52, mm.ToString("0.00") + " mm after 1.9 s at 25 mm/s");
        Press("M301", false); double held = mm;
        Run(5.1, 5.6);
        double now = ((V("D301") << 16) | (V("D300") & 0xFFFF)) / 100.0;
        Check("released: it stands still", Math.Abs(now - held) < 0.01, now.ToString("0.00"));
        Press("M300", true); Run(6.0, 7.0);
        double back = ((V("D301") << 16) | (V("D300") & 0xFFFF)) / 100.0;
        Check("the back (left) button counts DOWN", back < now - 20, back.ToString("0.00") + " mm");
        Press("M300", false);

        Console.WriteLine("--- a real PLC is never moved by this ---");
        SimulationMotion.Clear();
        var real = new CylinderUnitHMI { PlcNumber = 7, CommandAAddress = "M100", Lamp1AAddress = "M110" };
        SimulationMotion.Register(real);
        SimulationMotion.Tick(t0.AddSeconds(9));
        Check("a station with no simulated driver is left alone", true);

        Console.WriteLine(fail==0?"\nMOTION ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

