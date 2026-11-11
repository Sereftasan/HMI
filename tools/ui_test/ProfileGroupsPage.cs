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
using Kaban.UI.Profiles;

// THE PROFILE GROUPS PAGE (11 November: "create the screens closer to [the old program] (with the profile group and accessory group
// relations)" - "I want you to check them from the program"): as the old program's Preparation > Group Mapping - a tree of brand > series >
// profile group > its profiles / accessory groups / accessories; a group's accessory groups and accessories ticked on the right (one of a
// ticked accessory group comes with it, unticked off for that group only); an accessory's operations; the library's tables from buttons
// above as the old Company Profiles' toolbar (Profile brands, Profile series ...); nothing changed below Maintenance.
class PGP {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) {
        var t = o as Type ?? o.GetType();
        while (t != null) {
            var f = t.GetField(n, Any); if (f != null) return f.GetValue(o is Type ? null : o);
            var p = t.GetProperty(n, Any); if (p != null) return p.GetValue(o is Type ? null : o, null);
            t = t.BaseType;
        }
        throw new MissingMemberException(n);
    }
    static object Call(object o, string n, params object[] a) {
        var t = o as Type ?? o.GetType();
        var mi = t.GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length);
        return mi.Invoke(o is Type ? null : o, a);
    }
    static void Pump(int ms) { var until = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < until) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static IEnumerable<TreeNode> All(TreeNodeCollection nodes) { foreach (TreeNode n in nodes) { yield return n; foreach (var c in All(n.Nodes)) yield return c; } }

    [STAThread]
    static void Main(string[] args) {
        var dir = Path.Combine(Path.GetTempPath(), "pgp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        AuditLogger.LogDirectory = Path.Combine(dir, "logs");
        Db.ConnectionTarget = Path.Combine(dir, "Kaban.db");
        Db.SqliteChoice = Db.SqliteLibrary.Native;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        SecurityManager.Login("setter", SecurityLevel.Maintenance);
        Ask.Hook = (title, prompt, initial) => initial == "yes" ? "yes" : initial;

        var form = new Form { ClientSize = new Size(1270, 860), StartPosition = FormStartPosition.Manual, Location = new Point(0, 0) };
        var page = new Kaban.Machines.FA2010.Screens.ProfileGroupsScreen();
        form.Controls.Add(page);
        form.Show();
        Pump(300);
        Call(page, "EnsureLibrary");
        Pump(100);
        var tree = (TreeView)Get(page, "_tree");
        var nodes = All(tree.Nodes).ToList();
        Func<TreeNode, string> kind = n => (string)Get(n.Tag, "Kind");
        var frame = Library.Profiles().First(p => p.Code == "ML8-F-4501");
        Check("the tree: brand > series > profile group > its profiles / accessory groups / accessories one by one",
              tree.Nodes.Count > 0 && kind(tree.Nodes[0]) == "vendor" && nodes.Any(n => kind(n) == "series") && nodes.Count(n => kind(n) == "group") >= 3 &&
              nodes.Where(n => kind(n) == "group").All(g => g.Nodes.Count == 3 && kind(g.Nodes[0]) == "profiles" && kind(g.Nodes[1]) == "accgroups" && kind(g.Nodes[2]) == "accessories"),
              string.Join(" | ", nodes.Where(n => kind(n) == "group").Select(n => n.Text)));
        var groupNode = nodes.First(n => kind(n) == "group" && (long)Get(n.Tag, "Id") == frame.GroupId);
        Check("  the frame's group: its profiles under it (ML8-F-4501), the counts on it", groupNode.Nodes[0].Nodes.Cast<TreeNode>().Any(n => n.Text.StartsWith("ML8-F-4501")) &&
              groupNode.Text.Contains("profile(s)") && groupNode.Text.Contains("accessory(ies)"), groupNode.Text);

        Check("a group selected: its accessory groups and accessories ticked on the right", (bool)Call(page, "SelectGroup", frame.GroupId) && (long)Get(page, "GroupId") == frame.GroupId);
        Pump(50);
        var accGroups = (DataGridView)Get(page, "_accGroups");
        var accessories = (DataGridView)Get(page, "_accessories");
        var head = (Label)Get(page, "_head");
        Check("  the head: brand / series / group (type), its profiles", head.Text.Contains("ML8") || head.Text.Contains("/"), head.Text);
        var before = Library.GroupAccessories(frame.GroupId).Count(r => Convert.ToInt32(r["is_enabled"]) == 1);
        Check("  every accessory group a row, every accessory a row; the group's ticked", accGroups.Rows.Count > 0 && accessories.Rows.Count > 0 &&
              accessories.Rows.Cast<DataGridViewRow>().Count(r => Convert.ToBoolean(r.Cells["on"].Value)) == before,
              accGroups.Rows.Count + " / " + accessories.Rows.Count + " / " + before);

        // an accessory group not attached yet: ticked
        int ag = accGroups.Rows.Cast<DataGridViewRow>().First(r => !Convert.ToBoolean(r.Cells["on"].Value) && Convert.ToInt32(r.Cells["count"].Value) > 0).Index;
        string agName = Convert.ToString(accGroups.Rows[ag].Cells["name"].Value);
        long agId = (long)accGroups.Rows[ag].Tag;
        accGroups.Rows[ag].Cells["on"].Value = true;
        Pump(150);
        Check("an accessory group ticked: attached to the profile group (the old program's ticked receipts)",
              Library.AccessoryGroupsOf(frame.GroupId).Any(r => Convert.ToInt64(r["id"]) == agId && Convert.ToInt32(r["attached"]) == 1), agName);
        nodes = All(tree.Nodes).ToList();
        groupNode = nodes.First(n => kind(n) == "group" && (long)Get(n.Tag, "Id") == frame.GroupId);
        Check("  the tree: it under the group's accessory groups, its accessories under it", groupNode.Nodes[1].Nodes.Cast<TreeNode>().Any(n => n.Text == agName && n.Nodes.Count > 0));
        var accRows = accessories.Rows.Cast<DataGridViewRow>().Where(r => Convert.ToString(r.Cells["group"].Value) == agName).ToList();
        Check("  its accessories ticked, \"with its accessory group\"", accRows.Count > 0 && accRows.All(r => Convert.ToBoolean(r.Cells["on"].Value) && Convert.ToString(r.Cells["how"].Value).Contains("with its accessory group")),
              string.Join(" | ", accRows.Select(r => r.Cells["code"].Value + ":" + r.Cells["how"].Value)));
        // one of them off for this group only
        int one = accRows[0].Index;
        string code = Convert.ToString(accessories.Rows[one].Cells["code"].Value);
        long accId = (long)accessories.Rows[one].Tag;
        accessories.Rows[one].Cells["on"].Value = false;
        Pump(150);
        var r1 = accessories.Rows.Cast<DataGridViewRow>().First(r => (long)r.Tag == accId);
        Check("one of them unticked: off for this group only (the accessory group stays)", !Convert.ToBoolean(r1.Cells["on"].Value) && Convert.ToString(r1.Cells["how"].Value).Contains("off for this group") &&
              !Library.GroupAccessories(frame.GroupId).Any(r => Convert.ToInt64(r["accessory_id"]) == accId && Convert.ToInt32(r["is_enabled"]) == 1) &&
              Library.AccessoryGroupsOf(frame.GroupId).Any(r => Convert.ToInt64(r["id"]) == agId && Convert.ToInt32(r["attached"]) == 1), code + ": " + r1.Cells["how"].Value);
        r1.Cells["on"].Value = true;
        Pump(150);
        r1 = accessories.Rows.Cast<DataGridViewRow>().First(r => (long)r.Tag == accId);
        Check("  ticked again: with its accessory group once more", Convert.ToBoolean(r1.Cells["on"].Value) && Convert.ToString(r1.Cells["how"].Value).Contains("with its accessory group"));
        // the accessory group off again
        accGroups.Rows.Cast<DataGridViewRow>().First(r => (long)r.Tag == agId).Cells["on"].Value = false;
        Pump(150);
        Check("the accessory group unticked: off the profile group, its accessories too", !Library.AccessoryGroupsOf(frame.GroupId).Any(r => Convert.ToInt64(r["id"]) == agId && Convert.ToInt32(r["attached"]) == 1) &&
              !Convert.ToBoolean(accessories.Rows.Cast<DataGridViewRow>().First(r => (long)r.Tag == accId).Cells["on"].Value));
        // one accessory given one by one
        var single = accessories.Rows.Cast<DataGridViewRow>().First(r => !Convert.ToBoolean(r.Cells["on"].Value));
        long singleId = (long)single.Tag;
        single.Cells["on"].Value = true;
        Pump(150);
        var s1 = accessories.Rows.Cast<DataGridViewRow>().First(r => (long)r.Tag == singleId);
        Check("an accessory ticked by itself: given one by one (in the tree's one-by-one list)", Convert.ToBoolean(s1.Cells["on"].Value) && Convert.ToString(s1.Cells["how"].Value).Contains("one by one") &&
              All(tree.Nodes).First(n => kind(n) == "group" && (long)Get(n.Tag, "Id") == frame.GroupId).Nodes[2].Nodes.Cast<TreeNode>().Any(n => n.Text == Convert.ToString(s1.Cells["code"].Value)));
        s1.Cells["on"].Value = false;
        Pump(150);
        Check("  unticked: taken off", !Library.GroupAccessories(frame.GroupId).Any(r => Convert.ToInt64(r["accessory_id"]) == singleId));
        Check("  each change in the log", File.ReadAllText(Directory.GetFiles(AuditLogger.LogDirectory).First()).Contains("accessory group"));

        // an accessory: its operations; a profile: what it is
        var accNode = All(tree.Nodes).First(n => kind(n) == "accessory");
        tree.SelectedNode = accNode;
        Pump(50);
        var ops = (DataGridView)Get(page, "_ops");
        Check("an accessory selected in the tree: its operations (macro, where, along the bar)", ((Panel)Get(page, "_accessoryView")).Visible && ops.Rows.Count > 0 &&
              Convert.ToString(ops.Rows[0].Cells["macro"].Value).Length > 0, accNode.Text + ": " + ops.Rows.Count);
        var profNode = All(tree.Nodes).First(n => kind(n) == "profile" && n.Text.StartsWith("ML8-F-4501"));
        tree.SelectedNode = profNode;
        Pump(50);
        Check("a profile selected: its brand, series, group, size, its places", ((Panel)Get(page, "_profileView")).Visible && ((Label)Get(page, "_profileText")).Text.Contains("Size"),
              ((Label)Get(page, "_profileText")).Text);

        if (args.Length > 0) {
            Call(page, "SelectGroup", frame.GroupId);
            All(tree.Nodes).First(n => kind(n) == "group" && (long)Get(n.Tag, "Id") == frame.GroupId).ExpandAll();
            Pump(100);
            form.Activate(); Pump(300);
            using (var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height)) {
                using (var g = Graphics.FromImage(bmp)) g.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, bmp.Size);
                bmp.Save(args[0]);
            }
        }

        SecurityManager.Login("op", SecurityLevel.Operator);
        Pump(150);
        Call(page, "SelectGroup", frame.GroupId);
        Pump(50);
        Check("below Maintenance: shown, the ticks not changed", accGroups.Columns["on"].ReadOnly && accessories.Columns["on"].ReadOnly);
        form.Close();
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nPROFILE GROUPS PAGE ALL PASS" : "\nPROFILE GROUPS PAGE " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
