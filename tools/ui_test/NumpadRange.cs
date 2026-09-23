using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.UI.Forms;

class R {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    [STAThread] static void Main(){
        // The reported case, and worse: long negative bounds with four decimals.
        Show("reported",  -25.0,     333.0,     4);
        Show("long",      -99999.0,  99999.0,   4);
        Show("worst",     -999999.99, 999999.99, 4);
        Show("min only",  -25.0,     (double?)null, 4);
        Show("max only",  (double?)null, 333.0,  4);
        Show("none",      (double?)null, (double?)null, 2);
        Console.WriteLine(fail==0?"\nRANGE ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }

    static void Show(string what, double? lo, double? hi, int dec){
        var f = new NumpadForm("Input", "0", lo, hi, true, true, false);
        f.DecimalPlaces = dec;
        f.Show(); Application.DoEvents();

        // The range line is the topmost Label inside the dark readout panel.
        // Selecting it by font name fails under Mono, which substitutes the family.
        // The widest label in the dark readout panel is the range line; the other
        // one there is the close cross.
        var range = Flatten(f).OfType<Label>()
                     .Where(l => l.Parent is Panel && l.Parent.BackColor.R < 60 && l.Width > 100)
                     .OrderBy(l => l.Top).FirstOrDefault();
        if (range == null) { Check(what + ": range label found", false); f.Close(); return; }

        var used = TextRenderer.MeasureText(range.Text, range.Font).Width;
        Console.WriteLine("  {0,-9} \"{1}\"  {2}pt  {3}/{4}px",
                          what, range.Text, range.Font.Size, used, range.Width);
        Check("  " + what + " fits its box", used <= range.Width, used + " of " + range.Width);
        f.Close();
    }

    static System.Collections.Generic.IEnumerable<Control> Flatten(Control c){
        foreach (Control k in c.Controls){ yield return k; foreach (var d in Flatten(k)) yield return d; }
    }
}

