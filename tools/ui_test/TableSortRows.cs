using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Settings;

// 22 October: "'An unexpected error occurred' when the AI's answer's import is clicked - it worked once,
// then it stopped working: NullReferenceException at TranslationReviewForm.UpdateOk". A table sorted
// once kept its sort; put back while the review was being made, its cells' events ran before the OK
// button was there. And a sort moved only the cells' values: whatever rode on a row - the review's
// change, Alarm Settings' alarm definition, a profile's id - stayed behind.
class TS {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        d = d ?? "";
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    static object P(object o, string n) { return o.GetType().GetProperty(n, Any).GetValue(o, null); }
    static DataGridView G(TranslationReviewForm f) { return (DataGridView)P(f, "Grid"); }
    static List<TranslationReviewForm.Item> It(TranslationReviewForm f) { return (List<TranslationReviewForm.Item>)P(f, "Items"); }
    static string Ok(TranslationReviewForm f) { return (string)P(f, "OkText"); }
    static void Inv(object o, string n, params object[] a) { o.GetType().GetMethod(n, Any).Invoke(o, a); }

    static void ClickHeader(DataGridView g, string column) {
        var e = new DataGridViewCellMouseEventArgs(g.Columns[column].Index, -1, 5, 5, new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
        typeof(DataGridView).GetMethod("OnColumnHeaderMouseClick", Any).Invoke(g, new object[] { e });
    }

    [STAThread]
    static void Main() {
        string dir = Path.Combine(Path.GetTempPath(), "sortrows_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Kaban.UI.Helpers.ColumnChooser.Folder = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");

        Console.WriteLine("--- a table sorted: each row moves whole ---");
        var form = new Form { Width = 600, Height = 300 };
        var g = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
        g.Columns.Add("Name", "Name"); g.Columns.Add("Delay", "Delay");
        form.Controls.Add(g);
        foreach (var n in new[] { "Charlie", "Alpha", "Bravo" })
        {
            int at = g.Rows.Add(n, n.Length.ToString());
            g.Rows[at].Tag = "def-" + n;
            g.Rows[at].Cells["Delay"].ReadOnly = n == "Alpha";
            g.Rows[at].Cells["Delay"].ToolTipText = "tip " + n;
            g.Rows[at].Cells["Delay"].Tag = n.ToLowerInvariant();
            if (n == "Bravo") g.Rows[at].Cells["Delay"].Style.BackColor = Color.LightGray;
        }
        UiStyle.EnableTableTools(g, "SortRowsTest");
        form.Show(); Application.DoEvents();
        ClickHeader(g, "Name"); Application.DoEvents();
        Func<int, string> name = r => Convert.ToString(g.Rows[r].Cells["Name"].Value);
        Check("sorted by Name: Alpha, Bravo, Charlie", name(0) == "Alpha" && name(1) == "Bravo" && name(2) == "Charlie", name(0) + name(1) + name(2));
        Check("  each row's Tag with it (Alarm Settings' definition: saved into the alarm shown)",
              Enumerable.Range(0, 3).All(r => (string)g.Rows[r].Tag == "def-" + name(r)), string.Join(",", Enumerable.Range(0, 3).Select(r => g.Rows[r].Tag)));
        Check("  a cell's read-only, tooltip, tag and colour with it",
              g.Rows[0].Cells["Delay"].ReadOnly && !g.Rows[1].Cells["Delay"].ReadOnly && !g.Rows[2].Cells["Delay"].ReadOnly &&
              g.Rows[1].Cells["Delay"].ToolTipText == "tip Bravo" && (string)g.Rows[2].Cells["Delay"].Tag == "charlie" &&
              g.Rows[1].Cells["Delay"].Style.BackColor == Color.LightGray && g.Rows[0].Cells["Delay"].Style.BackColor != Color.LightGray &&
              g.Rows[2].Cells["Delay"].Style.BackColor != Color.LightGray, g.Rows[1].Cells["Delay"].Style.BackColor.ToString());
        g.Rows[1].Cells["Delay"].Style.BackColor = Color.Yellow;
        Check("  the colour moved is the cell's own (changed: that cell alone)", g.Rows[1].Cells["Delay"].Style.BackColor == Color.Yellow &&
              g.Rows[0].Cells["Delay"].Style.BackColor != Color.Yellow && g.Rows[2].Cells["Delay"].Style.BackColor != Color.Yellow);
        ClickHeader(g, "Name"); Application.DoEvents();
        Check("  again (descending): still whole", name(0) == "Charlie" && (string)g.Rows[0].Tag == "def-Charlie" && g.Rows[2].Cells["Delay"].ReadOnly &&
              g.Rows[1].Cells["Delay"].Style.BackColor == Color.Yellow);
        form.Close();

        Console.WriteLine("--- the translations' review, sorted once ---");
        Func<string, string, string, string, bool, TranslationReviewForm.Item> item = (key, lang, now, nw, take) =>
            new TranslationReviewForm.Item { Row = new TranslationEdits.Row { Key = key, Owner = TranslationEdits.General }, Lang = lang, Now = now, New = nw, Take = take };
        Func<List<TranslationReviewForm.Item>> items = () => new List<TranslationReviewForm.Item>
        {
            item("Btn_Save", "TR", "Kayıt", "Kaydet", true),
            item("Btn_Cancel", "TR", "İptal et", "İptal", false),
            item("Lbl_Speed", "DE", "Geschw.", "Geschwindigkeit", true),
            item("Ttl_Alarms", "TR", "Alarm", "Alarmlar", false),
        };
        var first = new TranslationReviewForm("Review", "", items(), 2, "Write {0} + {1}");
        first.Show(); Application.DoEvents();
        ClickHeader(G(first), "Key"); Application.DoEvents();
        Check("the first review sorted by Key (kept)", Convert.ToString(G(first).Rows[0].Cells["Key"].Value) == "Btn_Cancel" &&
              File.Exists(Path.Combine(dir, "TranslationReview.cols")) && File.ReadAllText(Path.Combine(dir, "TranslationReview.cols")).Contains("sort\tKey"));
        Check("  no change ticked or unticked by the sort; no text moved to another key",
              It(first)[1].Take == false && It(first)[3].Take == false && It(first)[0].Take && It(first)[2].Take &&
              It(first)[0].New == "Kaydet" && It(first)[1].New == "İptal" && It(first)[2].New == "Geschwindigkeit" && Ok(first) == "Write 2 + 2",
              string.Join(",", It(first).Select(i => i.Row.Key + "=" + i.New + (i.Take ? "+" : "-"))) + " " + Ok(first));
        first.Close();

        TranslationReviewForm next = null;
        string error = null;
        var list = items();
        try { next = new TranslationReviewForm("Review", "", list, 2, "Write {0} + {1}"); }
        catch (Exception ex) { error = ex.GetType().Name + ": " + ex.Message; }
        Check("the next review opens (its sort put back before the OK button was there: the NullReferenceException)", next != null && error == null, error);
        if (next == null) { Console.WriteLine(fail + " FAILED"); Environment.Exit(1); }
        next.Show(); Application.DoEvents();
        var grid = G(next);
        Func<int, string> keyAt = r => Convert.ToString(grid.Rows[r].Cells["Key"].Value);
        Check("  sorted as kept: Btn_Cancel, Btn_Save, Lbl_Speed, Ttl_Alarms", keyAt(0) == "Btn_Cancel" && keyAt(1) == "Btn_Save" && keyAt(2) == "Lbl_Speed" && keyAt(3) == "Ttl_Alarms",
              keyAt(0) + "," + keyAt(1));
        Check("  the button counts as given: 2 empty cells + 2 ticked", Ok(next) == "Write 2 + 2", Ok(next));
        // Btn_Cancel's row (row 0) unticked; ticking it ticks Btn_Cancel's change, not Btn_Save's (the first change)
        grid.Rows[0].Cells["Take"].Value = true;
        Check("a row ticked: ITS change (Btn_Cancel), not the change of the row's place (Btn_Save)", list[1].Take && list[0].Take && !list[3].Take && Ok(next) == "Write 2 + 3",
              string.Join(",", list.Select(i => i.Row.Key + (i.Take ? "+" : "-"))));
        grid.Rows[3].Cells["New"].Value = "Alarm listesi";
        Check("a text corrected on Ttl_Alarms' row: Ttl_Alarms' text, ticked with it", list[3].New == "Alarm listesi" && list[3].Take && list[0].New == "Kaydet" &&
              Ok(next) == "Write 2 + 4", list[3].New + " " + Ok(next));
        Inv(next, "Tick", 2, false);
        Check("  Tick (the tests): the change's own row (Lbl_Speed, row 2)", !list[2].Take && !(bool)grid.Rows[2].Cells["Take"].Value && (bool)grid.Rows[1].Cells["Take"].Value);
        Inv(next, "TickAll", false);
        Check("  untick all: every change", list.All(i => !i.Take) && Ok(next) == "Write 2 + 0");
        next.Close();

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "TABLE SORT ROWS ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
