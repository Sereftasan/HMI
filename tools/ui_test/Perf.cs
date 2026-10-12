using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Kaban.UI.Controls;

class B {
    [STAThread] static void Main(){
        var host = new Form { ClientSize = new Size(1300, 800) };
        host.Show();

        Time("200 ButtonHMI  create", () => {
            var p = new Panel { Size = new Size(1300, 800) };
            for (int i = 0; i < 200; i++)
                p.Controls.Add(new ButtonHMI { FallbackText = "Jog " + i,
                                               Left = (i % 10) * 120, Top = (i / 10) * 34,
                                               Width = 110, Height = 30 });
            host.Controls.Add(p); Application.DoEvents(); host.Controls.Remove(p); p.Dispose();
        });

        Time("200 NumericInputHMI create", () => {
            var p = new Panel { Size = new Size(1300, 800) };
            for (int i = 0; i < 200; i++)
                p.Controls.Add(new NumericInputHMI { Address = "D" + i, Digits = 6,
                                                     Left = (i % 10) * 120, Top = (i / 10) * 34,
                                                     Width = 110, Height = 30 });
            host.Controls.Add(p); Application.DoEvents(); host.Controls.Remove(p); p.Dispose();
        });

        Time("16 AxisSettingHMI create", () => {
            var p = new Panel { Size = new Size(1300, 800) };
            for (int i = 0; i < 16; i++) {
                var ax = new AxisSettingHMI {
                    AxisNameKey = "X" + i, PositionAddress = "W" + i,
                    CommandBackAddress = "M" + i, CommandForwardAddress = "M" + (i+1),
                    MinLimitAddress = "D" + i, MaxLimitAddress = "D" + (i+1),
                    ReferenceAddress = "D" + (i+2), CalibrationAddress = "D" + (i+3),
                    CalibrateAddress = "M" + (i+2), GoToReferenceAddress = "B" + i,
                    CurrentAddress = "W" + (i+10), ZDistanceAddress = "D" + (i+20),
                    AxisAlarmAddress = "W" + (i+30), AxisWarningAddress = "W" + (i+40),
                    ServoAlarmAddress = "W" + (i+50),
                    Top = i * 44, Left = 0, Width = 1280 };
                p.Controls.Add(ax);
            }
            host.Controls.Add(p); Application.DoEvents(); host.Controls.Remove(p); p.Dispose();
        });

        // repeated repaints: the cost that matters once a page is open
        var bench = new Panel { Size = new Size(1300, 600) };
        for (int i = 0; i < 200; i++)
            bench.Controls.Add(new ButtonHMI { FallbackText = "Run " + i,
                                               Left = (i % 10) * 120, Top = (i / 10) * 30,
                                               Width = 110, Height = 28 });
        host.Controls.Add(bench); Application.DoEvents();
        Time("200 buttons x 20 repaints", () => {
            for (int n = 0; n < 20; n++) { bench.Invalidate(true); bench.Update(); }
        });

        host.Close();
    }
    static void Time(string what, Action a){
        var sw = Stopwatch.StartNew(); a(); sw.Stop();
        Console.WriteLine("  {0,-28} {1,6} ms", what, sw.ElapsedMilliseconds);
    }
}

