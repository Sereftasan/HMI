using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.Profiles.Data;
using Kaban.Profiles.Geometry;
using Kaban.Profiles.Machining;
using Kaban.UI.Controls;
using Kaban.UI.Profiles;

// THE PROFILES PAGE on the profile library (30 September): the table from the
// library, a profile's drawing turned the right way, its reference points found
// and confirmed, an accessory placed on it, moved in a trial and confirmed, the
// programs made - and nothing changed below the Maintenance level.
class PP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) {
        var t = o.GetType();
        while (t != null) {
            var f = t.GetField(n, Any); if (f != null) return f.GetValue(o);
            var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(o, null);
            t = t.BaseType;
        }
        throw new MissingMemberException(n);
    }
    static object Call(object o, string n, params object[] a) {
        var mi = o.GetType().GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length);
        return mi.Invoke(o, a);
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static readonly Queue<string> answers = new Queue<string>();
    static readonly List<string> asked = new List<string>();

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "pp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        Db.ConnectionTarget = Path.Combine(dir, "Kaban.db");
        Db.SqliteChoice = Db.SqliteLibrary.Native;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        // every question answered here: yes to a yes/no, the next queued text to a prompt
        Ask.Hook = (title, prompt, initial) => { asked.Add(prompt); if (initial == "yes") return "yes"; return answers.Count > 0 ? answers.Dequeue() : initial; };

        var form = new Form { ClientSize = new Size(1600, 1000) };
        var page = new Kaban.Machines.FA2010.Screens.ProfilesScreen();
        form.Controls.Add(page);
        form.Show();
        Pump(300);
        Call(page, "EnsureLibrary");
        Pump(100);

        Console.WriteLine("--- the library opened, the table filled ---");
        Check("a new library asks whether to load the examples", asked.Any(a => a.Contains("example catalogue")), asked.FirstOrDefault());
        var table = (System.Data.DataTable)Get(page, "_table");
        Check("every profile of the library in the table", table.Rows.Count == Library.Profiles().Count && table.Rows.Count >= 3, table.Rows.Count.ToString());
        var grid = (DataGridView)Get(page, "_grid");
        Check("grouped by vendor and series (caption rows)", grid.Rows.Cast<DataGridViewRow>().Any(r => r.Tag == null && Convert.ToString(r.Cells[0].Value).Contains("DECEUNINCK")));
        long instance = (long)Get(page, "_instance");
        Check("the machine in the library: four tool places, eight faces", instance > 0 && Library.Tools(instance).Count == 4 && Library.Faces(instance).Count == 8,
              Library.Tools(instance).Count + " / " + Library.Faces(instance).Count);
        var panes = (IDictionary)Get(page, "_panes");
        Check("five panes: table, drawing, details, accessories, programs", panes.Count == 5 && panes.Contains("accessories"), string.Join(",", panes.Keys.Cast<string>()));

        Console.WriteLine("--- a profile and its drawing ---");
        var door = Library.Profiles().First(p => p.Code == "0001020307");
        Call(page, "SelectProfile", door.Id);
        Pump(100);
        var section = (Section)Get(page, "_section");
        Check("its drawing read (the sample DXF)", section != null && section.Width > 50, section == null ? "none" : section.Width + " x " + section.Height);
        var sizeLabel = (Label)Get(page, "_lblSize");
        Check("  lying on its side: the page says so", sizeLabel.Text.Contains("on its side"), sizeLabel.Text);
        var viewer = (Kaban.UI.Dxf.DxfViewerControl)Get(page, "_viewer");
        Check("  on the viewer, axes Y and Z", viewer.Document != null && viewer.Document.Polylines.Count > 0 && viewer.AxisNames[0] == "Y");
        ((Action)Get(page, "_sizeFix"))();                    // "Turn it"
        Pump(50);
        section = (Section)Get(page, "_section");
        Check("turned: 82 wide, 115 high, as the profile", section != null && Math.Abs(section.Width - 82) < 1 && Math.Abs(section.Height - 115) < 1, section.Width + " x " + section.Height);
        Check("  the details say it agrees", sizeLabel.Text.StartsWith("\u2714"), sizeLabel.Text);
        Check("  not saved yet", (bool)Get(page, "_detailsDirty"));
        Check("saved", (bool)Call(page, "SaveDetails"));
        var saved = Library.Profile(door.Id);
        Check("  its turn kept with the profile", saved.Rotation == 1 && saved.DxfStatus != "NONE", saved.Rotation + " " + saved.DxfStatus);

        Console.WriteLine("--- its reference points ---");
        var refs = Library.References(door.Id);
        Check("found on the saved drawing: the faces, the centre, the bed", new[] { "OUTSIDE_SURFACE", "INSIDE_SURFACE", "CENTER", "BED_DATUM" }.All(c => refs.Any(r => r.Code == c)),
              string.Join(",", refs.Select(r => r.Code)));
        Check("  nobody has confirmed them", refs.All(r => !r.Confirmed));
        var inside = refs.First(r => r.Code == "INSIDE_SURFACE");
        Check("  the inside face at the profile's width", Math.Abs(inside.Y - 82) < 1, inside.Y.ToString());
        var refGrid = (DataGridView)Get(page, "_refGrid");
        Check("the table lists every kind of point", refGrid.Rows.Count >= ReferenceFinder.Codes.Length, refGrid.Rows.Count.ToString());
        Call(page, "SetReferenceByHand", "HARDWARE_AXIS", 22.0, 115.0);
        Pump(100);
        refs = Library.References(door.Id);
        var hw = refs.FirstOrDefault(r => r.Code == "HARDWARE_AXIS");
        Check("a point set by hand", hw != null && hw.Source == "MANUAL" && Math.Abs(hw.Y - 22) < 1e-6 && !hw.Confirmed);
        Call(page, "ConfirmReferences", refs.Select(r => r.Code).ToList());
        refs = Library.References(door.Id);
        Check("confirmed, every one", refs.Count > 0 && refs.All(r => r.Confirmed && r.ConfirmedBy == "setter"), string.Join(",", refs.Where(r => !r.Confirmed).Select(r => r.Code)));
        Call(page, "FindReferences");
        var again = Library.References(door.Id);
        Check("  looking again keeps what was confirmed", again.All(r => r.Confirmed) && Math.Abs(again.First(r => r.Code == "HARDWARE_AXIS").Y - 22) < 1e-6);

        Console.WriteLine("--- the group's accessory on it ---");
        var pls = (List<Library.PlacementInfo>)Get(page, "_placements");
        Check("the group's handle placed on the hardware axis", pls.Count == 1 && pls[0].Macro == "POCKET_32X32" && Math.Abs(pls[0].Y - 22) < 0.01,
              pls.Count + " " + string.Join(";", pls.Select(p => p.Macro + "@" + p.Y)) + " " + string.Join(" | ", (List<string>)Get(page, "_resolveProblems")));
        Check("  worked out, not confirmed", pls[0].Status == "AUTO", pls[0].Status);
        var plGrid = (DataGridView)Get(page, "_plGrid");
        Check("  in the table with its depth and walls", plGrid.Rows.Count == 1 && Convert.ToString(plGrid.Rows[0].Cells["depth"].Value).StartsWith("from"),
              Convert.ToString(plGrid.Rows[0].Cells["depth"].Value));
        Call(page, "SelectPlacement", pls[0].Id);
        Call(page, "Nudge", 0.5, 0.0);
        Call(page, "Nudge", 0.5, -0.1);
        Check("moved a little: shown, not kept", Math.Abs((double)Get(page, "_pendingDy") - 1) < 1e-9 && Math.Abs((double)Get(page, "_pendingDz") + 0.1) < 1e-9 && Library.Placements(door.Id)[0].Y == pls[0].Y);
        var bmp = new Bitmap(viewer.Width, viewer.Height);
        viewer.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
        Check("  the drawing paints it (and the cut) without a fault", bmp.Width > 0);
        Check("kept as the profile's correction", (bool)Call(page, "SaveMove", "the trial piece was 1 mm short"));
        var moved = Library.Placements(door.Id)[0];
        Check("  the placement moved by it", Math.Abs(moved.Y - 23) < 1e-6 && Math.Abs(moved.CorrY - 1) < 1e-6 && Math.Abs(moved.CorrZ + 0.1) < 1e-6, moved.Y + " / " + moved.CorrY);
        Check("  the accessory itself untouched", Convert.ToDouble(Library.Scalar("SELECT offset_y_mm FROM accessory_operation WHERE id = @p0", moved.OperationId)) == 0);
        Call(page, "SelectPlacement", moved.Id);
        Check("confirmed", (bool)Call(page, "ConfirmPlacement", "ok on the trial piece"));
        Check("  CONFIRMED, by the user", Library.Placements(door.Id)[0].Status == "CONFIRMED" && Library.Placements(door.Id)[0].ConfirmedBy == "setter");

        Console.WriteLine("--- the programs ---");
        Library.Exec("UPDATE mounted_tool SET diameter_mm = 10, usable_length_mm = 40 WHERE instance_id = @p0 AND position = 'U1-T1'", instance);
        var side = (SideProgram.Result)Call(page, "GenerateSide");
        Check("the side program has the confirmed pocket", side != null && side.Operations == 1 && side.Code.Contains("POCKET_32X32") && side.Code.Contains("M30"),
              side == null ? "null" : string.Join(" | ", side.Problems));
        Check("  centred on the bar", side.Code.Contains("AT X750"));
        Call(page, "GenerateEndMill");
        var endText = ((TextBox)Get(page, "_txtEndMill")).Text;
        Check("the end milling program from the drawing", endText.Contains("G"), endText.Length.ToString());
        Call(page, "SaveEndMill");
        Check("  kept with the profile", Library.EndMillProgram(door.Id, instance) != null);

        Console.WriteLine("--- corrections taken away ---");
        var corr = Library.Corrections(door.Id);
        Check("the correction reaches the profile", corr.Count == 1);
        Call(page, "RemoveCorrection", Library.Id(corr[0], "id").Value);
        var back = Library.Placements(door.Id)[0];
        Check("  taken away: the placement back, to be confirmed again", Math.Abs(back.Y - 22) < 1e-6 && back.Status == "AUTO", back.Y + " " + back.Status);

        Console.WriteLine("--- new profiles ---");
        answers.Enqueue("TEST-1");
        Call(page, "AddProfile");
        var added = Library.Profiles().FirstOrDefault(p => p.Code == "TEST-1");
        Check("a new profile in the group of the one shown", added != null && added.GroupId == door.GroupId);
        Check("  shown", ((Library.ProfileInfo)Get(page, "_current")).Code == "TEST-1");
        Call(page, "SelectProfile", door.Id);
        answers.Enqueue("TEST-2");
        Call(page, "CopyProfile");
        var copy = Library.Profiles().First(p => p.Code == "TEST-2");
        var inherited = Library.References(copy.Id);
        Check("based on another: its points inherited", copy.BaseProfileId == door.Id && inherited.Count > 0 && inherited.All(r => r.Inherited));
        Call(page, "DeactivateProfile");
        Check("taken out of use: not in the table", !Library.Profiles().Any(p => p.Code == "TEST-2") && !table.Rows.Cast<System.Data.DataRow>().Any(r => Convert.ToString(r["code"]) == "TEST-2"));

        Console.WriteLine("--- below the Maintenance level ---");
        SecurityManager.Login("op", SecurityLevel.Operator);
        Call(page, "SelectProfile", door.Id);
        Call(page, "ApplyAccess");
        Check("nothing can be changed", !((Button)Get(page, "_btnSaveDetails")).Enabled && !((Button)Get(page, "_btnFindRefs")).Enabled &&
              ((List<Button>)Get(page, "_nudgeButtons")).All(b => !b.Enabled) && !((Button)Get(page, "_btnAddAcc")).Enabled);
        Check("  and a call does nothing", !(bool)Call(page, "SaveDetails") && !(bool)Call(page, "ConfirmPlacement", "x"));
        SecurityManager.Login("setter", SecurityLevel.Maintenance);

        Console.WriteLine("--- the library's tables ---");
        using (var lib = new LibraryEditorForm(instance))
        {
            lib.Show();
            Pump(100);
            var vendors = (TableEditor)Call(lib, "EditorOf", "vendors");
            int before = vendors.Grid.Rows.Count;
            vendors.AddRow();
            vendors.Grid.CancelEdit();                       // the new row opens for typing; the test sets the cells instead
            vendors.Grid.EndEdit();
            var row = vendors.Grid.Rows[vendors.Grid.Rows.Count - 1];
            row.Cells["code"].Value = "TST";
            row.Cells["name"].Value = "Test vendor";
            string err;
            Check("a vendor added and saved", vendors.Save(out err) && Library.Rows("SELECT id FROM profile_vendor WHERE code='TST'").Count == 1, err);
            Check("  the table read again", vendors.Grid.Rows.Count == before + 1);
            var tools = (TableEditor)Call(lib, "EditorOf", "tools");
            Check("this machine's tools only", tools.Grid.Rows.Count == 4, tools.Grid.Rows.Count.ToString());
            int tick = tools.Grid.Columns["is_active"].Index;
            typeof(DataGridView).GetMethod("OnCellClick", Any).Invoke(tools.Grid, new object[] { new DataGridViewCellEventArgs(tick, 0) });
            Check("a tick switched by a click", tools.Dirty && !(bool)tools.Grid.Rows[0].Cells["is_active"].Value);
            Check("  and saved", tools.Save(out err) && Convert.ToInt64(Library.Scalar("SELECT COUNT(*) FROM mounted_tool WHERE instance_id = @p0 AND is_active = 0", instance)) == 1, err);
            typeof(DataGridView).GetMethod("OnCellClick", Any).Invoke(tools.Grid, new object[] { new DataGridViewCellEventArgs(tick, 0) });
            tools.Save(out err);
            Check("  and back", Convert.ToInt64(Library.Scalar("SELECT COUNT(*) FROM mounted_tool WHERE instance_id = @p0 AND is_active = 0", instance)) == 0, err);
            vendors.Grid.Rows[vendors.Grid.Rows.Count - 1].Selected = true;
            vendors.Grid.CurrentCell = vendors.Grid.Rows[vendors.Grid.Rows.Count - 1].Cells[0];
            vendors.ConfirmDelete = n => true;
            vendors.DeleteRows();
            Check("removed after asking", vendors.Save(out err) && Library.Rows("SELECT id FROM profile_vendor WHERE code='TST'").Count == 0, err);
            var groups = (TableEditor)Call(lib, "EditorOf", "groups");
            var inUse = groups.Grid.Rows.Cast<DataGridViewRow>().First(r => Convert.ToString(r.Cells["name"].Value) == "TecnoCorr2 - Door sash");
            groups.Grid.ClearSelection();
            inUse.Selected = true;
            groups.ConfirmDelete = n => true;
            groups.DeleteRows();
            Check("a group its profiles still use is not removed", !groups.Save(out err) && Library.Rows("SELECT id FROM profile_group WHERE id = @p0", door.GroupId).Count == 1, err);
            groups.Reload();
            lib.Close();
        }

        Console.WriteLine("--- the arrangement ---");
        var entry = (Dictionary<string, string>)Call(page, "CaptureArrangement");
        Check("an arrangement is of this page (its version) with its five panes", entry.ContainsKey("#v") && entry.ContainsKey("accessories") && entry.ContainsKey("drawing"));
        entry.Remove("#v");
        Call(page, "RestoreArrangement", entry);
        Check("  one of the old page is not restored (default tiling instead)", ((Control)((IDictionary)Get(page, "_panes"))["table"]).Left == 0);

        form.Close();
        Console.WriteLine(fail == 0 ? "ALL PASS" : fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
