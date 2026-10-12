using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kaban.UI.Forms;

class N {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    [STAThread] static void Main(){
        Audit("decimal", new NumpadForm("Input", "12.50", 0, 100, true, true, false),
              new[]{"7","8","9","DEL","4","5","6","AC","1","2","3","ENT","0",".","+/-"});
        Audit("hex", new NumpadForm("Input", "1A5", null, null, false, false, true),
              new[]{"7","8","9","A","DEL","4","5","6","B","AC","1","2","3","C","ENT","0","D","E","F"});
        Console.WriteLine(fail==0?"\nPAD ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }

    static void Audit(string what, NumpadForm f, string[] expected){
        Console.WriteLine("--- " + what + " ---");
        f.Show(); Application.DoEvents();

        var all = Flatten(f).ToList();
        var keys = all.OfType<Button>().ToList();

        var labels = keys.Select(b => b.Text).ToList();
        foreach (var e in expected)
            if (!labels.Contains(e)) { Check("key '" + e + "' present", false); }
        Check("every expected key is present", expected.All(e => labels.Contains(e)),
              labels.Count + " keys");

        // The faces are "<" and ">" now; what matters is that they live in the
        // readout panel rather than among the number keys.
        Check("caret arrows are in the READOUT, not the key grid",
              keys.Any(b => b.Text == "<" && b.Parent != f) &&
              keys.Any(b => b.Text == ">" && b.Parent != f));

        // nothing may overlap, and nothing may hang off the form
        var grid = keys.Where(b => b.Parent == f).ToList();
        bool overlap = false;
        for (int i = 0; i < grid.Count && !overlap; i++)
            for (int j = i + 1; j < grid.Count; j++)
                if (grid[i].Bounds.IntersectsWith(grid[j].Bounds)) { overlap = true; break; }
        Check("no two keys overlap", !overlap);

        var client = f.ClientRectangle;
        Check("every key is inside the form",
              grid.All(b => client.Contains(b.Bounds)),
              grid.Count + " keys in " + client.Width + "x" + client.Height);

        // All the WORD keys must share a font: DEL, AC and ENT sit in one column
        // and a size difference between them reads as a mistake.
        var words = grid.Where(b => b.Text.Length > 1).ToList();
        Check("word keys share one font size",
              words.Select(b => b.Font.Size).Distinct().Count() == 1,
              string.Join(", ", words.Select(b => b.Text + "=" + b.Font.Size)));
        var digits = grid.Where(b => b.Text.Length == 1).ToList();
        Check("value keys share one font size, larger than the word keys",
              digits.Select(b => b.Font.Size).Distinct().Count() == 1 &&
              digits[0].Font.Size > words[0].Font.Size,
              digits[0].Font.Size + " vs " + words[0].Font.Size);

        var ent = grid.FirstOrDefault(b => b.Text == "ENT");
        var seven = grid.FirstOrDefault(b => b.Text == "7");
        if (ent != null && seven != null)
            Check("ENT is taller than a normal key", ent.Height > seven.Height,
                  ent.Height + " vs " + seven.Height);

        Console.WriteLine("      form " + f.Width + " x " + f.Height);
        f.Close();
    }

    static IEnumerable<Control> Flatten(Control c){
        foreach (Control k in c.Controls){ yield return k; foreach (var d in Flatten(k)) yield return d; }
    }
}

