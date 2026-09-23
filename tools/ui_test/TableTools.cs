using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Kaban.UI.Controls;

public class Row {
    public string Machine { get; set; }
    public string Severity { get; set; }
    public int Seq { get; set; }
}

class T {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}

    [STAThread] static void Main(){
        string key = "UnitTestTable";
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "KabanHMI", "Layouts", key + ".cols");
        if (File.Exists(path)) File.Delete(path);

        var data = new BindingList<Row> {
            new Row{ Machine="B", Severity="Fault",   Seq=1 },
            new Row{ Machine="A", Severity="Warning", Seq=2 },
            new Row{ Machine="B", Severity="Warning", Seq=3 },
            new Row{ Machine="A", Severity="Fault",   Seq=4 },
        };
        var grid = new DataGridView { DataSource = data, Dock = DockStyle.Fill };
        var form = new Form { ClientSize = new Size(700, 300) };
        form.Controls.Add(grid);
        form.Show(); Application.DoEvents();

        UiStyle.EnableTableTools(grid, key);
        Application.DoEvents();

        Check("columns are programmatic-sort so clicks are ours",
              grid.Columns.Cast<DataGridViewColumn>().All(c => c.SortMode == DataGridViewColumnSortMode.Programmatic));
        Check("user may reorder columns", grid.AllowUserToOrderColumns);

        // Click Severity, then Machine: expect "by severity WITHIN machine".
        Click(grid, "Severity"); Application.DoEvents();
        Click(grid, "Machine");  Application.DoEvents();

        var seen = data.Select(r => r.Machine + "/" + r.Severity).ToList();
        Console.WriteLine("      order: " + string.Join("  ", seen));
        Check("primary key is the LAST clicked column",
              seen[0].StartsWith("A") && seen[3].StartsWith("B"), seen[0] + " .. " + seen[3]);
        Check("the earlier key still orders within it",
              seen[0] == "A/Fault" && seen[1] == "A/Warning", seen[0] + ", " + seen[1]);
        Check("only the primary column shows an arrow",
              grid.Columns.Cast<DataGridViewColumn>()
                  .Count(c => c.HeaderCell.SortGlyphDirection != SortOrder.None) == 1);

        // Hide a column, then check it is remembered.
        grid.Columns["Severity"].Visible = false;
        typeof(UiStyle).GetMethod("SaveLayout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?.Invoke(null, new object[]{ grid, key, new System.Collections.Generic.List<string>{"Machine"},
                                         new System.Collections.Generic.Dictionary<string,bool>{{"Machine",false}} });
        Check("a layout file was written", File.Exists(path));
        if (File.Exists(path)) {
            var text = File.ReadAllText(path);
            Check("it records the hidden column", text.Contains("Severity\t") && text.Contains("\t0"));
            Check("and the sort keys", text.Contains("sort\tMachine"));
        }
        Console.WriteLine("--- an UNBOUND grid sorts too (the alarm history) ---");
        // The history grid adds rows directly instead of binding a list, so
        // DataSource is null and the bound path could never touch it.
        // Start from nothing: a layout saved by an earlier run carries its sort
        // keys back, so the first click would TOGGLE that sort rather than
        // establish one - correct behaviour, but not what this case is testing.
        var upath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                 "KabanHMI", "Layouts", "UnitTestUnbound.cols");
        if (File.Exists(upath)) File.Delete(upath);

        var ug = new DataGridView { Dock = DockStyle.Fill };
        ug.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time", HeaderText = "Time" });
        ug.Columns.Add(new DataGridViewTextBoxColumn { Name = "Sev",  HeaderText = "Sev" });
        ug.Rows.Add("2026-09-17 10:05", "Warning");
        ug.Rows.Add("2026-09-17 09:05", "Fault");
        ug.Rows.Add("2026-09-17 11:05", "Fault");
        var uf = new Form { ClientSize = new Size(600, 240) };
        uf.Controls.Add(ug); uf.Show(); Application.DoEvents();

        UiStyle.EnableTableTools(ug, "UnitTestUnbound");
        Click(ug, "Time"); Application.DoEvents();
        var times = ug.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow)
                      .Select(r => Convert.ToString(r.Cells["Time"].Value)).ToList();
        Console.WriteLine("      " + string.Join("  ", times));
        Check("rows really moved", times[0].EndsWith("09:05") && times[2].EndsWith("11:05"),
              times[0] + " .. " + times[2]);
        Check("and times sort chronologically, not alphabetically",
              times[1].EndsWith("10:05"), times[1]);

        // THE RECORD MUST STAY TOGETHER. The old sort moved row objects through
        // Rows.Clear(), which detaches them - dates came back ordered while the
        // text beside them stayed put, so every row still looked plausible and was
        // wrong. Each time here has a unique partner, so any mix-up shows.
        var pairs = ug.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow)
                      .Select(r => Convert.ToString(r.Cells["Time"].Value) + " | " +
                                   Convert.ToString(r.Cells["Sev"].Value)).ToList();
        Console.WriteLine("      " + string.Join("   ", pairs));
        Check("09:05 still carries its own Fault",   pairs[0] == "2026-09-17 09:05 | Fault");
        Check("10:05 still carries its own Warning", pairs[1] == "2026-09-17 10:05 | Warning");
        Check("11:05 still carries its own Fault",   pairs[2] == "2026-09-17 11:05 | Fault");

        // and sorting a SECOND time must not scramble it either
        Click(ug, "Sev"); Application.DoEvents();
        var after = ug.Rows.Cast<DataGridViewRow>().Where(r => !r.IsNewRow)
                      .Select(r => Convert.ToString(r.Cells["Time"].Value) + " | " +
                                   Convert.ToString(r.Cells["Sev"].Value)).ToList();
        Check("records survive a second sort",
              after.All(x => x == "2026-09-17 09:05 | Fault" ||
                             x == "2026-09-17 10:05 | Warning" ||
                             x == "2026-09-17 11:05 | Fault") && after.Distinct().Count() == 3,
              string.Join("  ", after));
        uf.Close();

        form.Close();
        Console.WriteLine(fail==0?"\nTABLE ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }

    static void Click(DataGridView g, string col){
        var m = typeof(DataGridView).GetMethod("OnColumnHeaderMouseClick",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        m.Invoke(g, new object[]{ new DataGridViewCellMouseEventArgs(
            g.Columns[col].Index, -1, 1, 1, new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0)) });
    }
}

