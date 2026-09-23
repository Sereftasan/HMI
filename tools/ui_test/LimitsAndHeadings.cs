using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Models;
using Kaban.UI.Controls;

class P {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    [STAThread] static void Main(){
        Console.WriteLine("--- 1. a device name typed into a limit becomes a PLC TAG ---");
        var conv = TypeDescriptor.GetConverter(typeof(DynamicValue));
        var tag = conv.ConvertFrom("D200") as DynamicValue;
        Check("\"D200\" is a Tag, not an HmiVariable",
              tag != null && tag.Type == LimitSourceType.Tag && tag.TagAddress == "D200",
              tag == null ? "null" : tag.Type + " / " + tag.TagAddress);
        var num = conv.ConvertFrom("100") as DynamicValue;
        Check("\"100\" is still a constant",
              num.Type == LimitSourceType.Static && Math.Abs(num.StaticValue - 100) < 1e-9);
        var hv = conv.ConvertFrom("@Speed") as DynamicValue;
        Check("\"@Speed\" is still an HMI variable",
              hv.Type == LimitSourceType.HmiVariable && hv.TagAddress == "Speed");

        Console.WriteLine("--- 2. and the limit then actually fires ---");
        var plc = new Dictionary<string,int>{{"D100",5000},{"D200",0},{"D201",0},{"D202",1000},{"D203",0}};
        Func<string,int> read = a => { int v; return plc.TryGetValue(a, out v) ? v : 0; };
        Func<SystemCondition,bool> cond = c => true;

        var f = new NumericInputHMI { Address="D100", DataType=DataTypeEnum.Bin_16, Divisor=10, DecimalPlaces=2 };
        f.MinLimit = (DynamicValue)conv.ConvertFrom("D200");
        f.MaxLimit = (DynamicValue)conv.ConvertFrom("D202");
        f.UpdateState(read, cond);
        Check("500.00 against a tag max of 100.00 is flagged", f.IsOutOfLimits, f.Text);
        plc["D100"] = 500;
        f.UpdateState(read, cond);
        Check("50.00 is not", !f.IsOutOfLimits, f.Text);

        Console.WriteLine("--- 3. the alarm settings grid fits its headings ---");
        var page = new Kaban.UI.Settings.AlarmSettingsControl();
        var host = new Form { ClientSize = new Size(1200, 500) };
        host.Controls.Add(page);
        host.Show();
        Application.DoEvents();

        DataGridView grid = null;
        foreach (Control c in page.Controls) { grid = FindGrid(c); if (grid != null) break; }
        Check("found the grid", grid != null);
        if (grid != null) {
            var font = grid.ColumnHeadersDefaultCellStyle.Font ?? grid.Font;
            foreach (DataGridViewColumn c in grid.Columns) {
                int need = TextRenderer.MeasureText(c.HeaderText, font).Width + 24;
                Check("  \"" + c.HeaderText + "\" fits", c.Width >= need,
                      "width " + c.Width + ", needs " + need);
            }
        }
        host.Close();

        Console.WriteLine(fail==0?"\nPROOF ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
    static DataGridView FindGrid(Control c){
        var g = c as DataGridView; if (g != null) return g;
        foreach (Control k in c.Controls) { var r = FindGrid(k); if (r != null) return r; }
        return null;
    }
}

