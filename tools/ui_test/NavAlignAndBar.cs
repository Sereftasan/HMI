using System;
using System.Drawing;
using System.Windows.Forms;
using Kaban.UI.Controls;
class A {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}
    [STAThread] static void Main(){
        Console.WriteLine("--- a nav button keeps its alignment ---");
        var b = new ButtonHMI { Width = 220, Height = 44 };
        b.TextAlign = ContentAlignment.MiddleLeft;
        b.Text = "HMI Settings";
        Check("TextAlign survives setting the caption",
              b.TextAlign == ContentAlignment.MiddleLeft, b.TextAlign.ToString());
        Check("caption captured", b.DisplayText == "HMI Settings", b.DisplayText);
        Check("base.Text stays empty", ((Control)b).Text == "", "["+((Control)b).Text+"]");
        b.ShowCaption = false;
        Check("collapsed hides it without losing it",
              b.DisplayText == "HMI Settings");

        Console.WriteLine("--- BarView uses the field's own limits ---");
        var n = new NumericInputHMI { Width = 200, Height = 32, BarView = true };
        Check("BarView is on", n.BarView);
        Check("no separate BarMinimum/BarMaximum to keep in step",
              n.GetType().GetProperty("BarMinimum") == null &&
              n.GetType().GetProperty("BarMaximum") == null);
        Check("BarStep is still settable", n.GetType().GetProperty("BarStep") != null);
        Console.WriteLine(fail==0?"\nALIGN ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

