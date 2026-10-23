using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Helpers;
using Kaban.UI.Settings;

// 23 October: "We need a language emoji addition tool so we can add flags to the new added languages"
class LFT {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") { d = d ?? ""; Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++; }
    const BindingFlags Any = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static object Get(object o, string n) { var t = o.GetType(); var pr = t.GetProperty(n, Any); return pr != null ? pr.GetValue(o, null) : t.GetField(n, Any).GetValue(o); }
    static object Call(object o, string n, params object[] a) { return o.GetType().GetMethods(Any).First(m => m.Name == n && m.GetParameters().Length == a.Length).Invoke(o, a); }
    static IEnumerable<Control> Tree(Control c) { foreach (Control k in c.Controls) { yield return k; foreach (var d in Tree(k)) yield return d; } }

    /// <summary>The flag as the program shows it, drawn at 60 x 40: the colour at (x, y) of it.</summary>
    static Color Shown(string code, int x, int y) {
        using (var b = new Bitmap(60, 40))
        {
            using (var g = Graphics.FromImage(b)) typeof(Kaban.UI.Forms.MainForm).GetMethod("DrawFlag", Any).Invoke(null, new object[] { g, new Rectangle(0, 0, 59, 39), code });
            return b.GetPixel(x, y);
        }
    }
    static bool Near(Color c, int r, int g, int b) { return Math.Abs(c.R - r) < 40 && Math.Abs(c.G - g) < 40 && Math.Abs(c.B - b) < 40; }
    static string S(Color c) { return c.R + "," + c.G + "," + c.B; }

    [STAThread]
    static void Main() {
        var dir = Path.Combine(Path.GetTempPath(), "lft_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        TranslationEdits.Folder = Path.Combine(dir, "Translations");
        AuditLogger.LogDirectory = dir;
        LanguageManager.LoadCommonTranslations("CommonTranslations.csv");
        SecurityManager.Login("maint", SecurityLevel.Maintenance);

        Console.WriteLine("--- the countries' flags ---");
        var all = LanguageFlags.Countries;
        bool drawn = true;
        foreach (var c in all) { try { using (LanguageFlags.Render(c.Draw)) { } } catch { drawn = false; } }
        Check("the program draws 40 and more countries' flags, each once, every one draws", all.Count >= 40 && all.Select(c => c.Code).Distinct().Count() == all.Count && drawn, all.Count + " countries");
        Check("  a flag emoji read: 🇵🇱 is PL - and back", LanguageFlags.CodeOfEmoji("🇵🇱") == "PL" && LanguageFlags.EmojiOf("SE") == "🇸🇪" && LanguageFlags.CodeOfEmoji("PL") == null);
        Check("  a language's country: SV Sweden, JA Japan, UK Ukraine, PL Poland; AR none (which country's?)",
              LanguageFlags.CountryOfLanguage("SV").Code == "SE" && LanguageFlags.CountryOfLanguage("ja").Code == "JP" && LanguageFlags.CountryOfLanguage("UK").Code == "UA" &&
              LanguageFlags.CountryOfLanguage("PL").Code == "PL" && LanguageFlags.CountryOfLanguage("AR") == null);

        Console.WriteLine("--- a language added: its flag ---");
        Check("PL (no picture of its own): Poland's drawn - white over red (it was a grey box)", Near(Shown("PL", 30, 8), 255, 255, 255) && Near(Shown("PL", 30, 32), 220, 20, 60),
              S(Shown("PL", 30, 8)) + " / " + S(Shown("PL", 30, 32)));
        Check("  SV: Sweden's - blue, its yellow cross", Near(Shown("SV", 5, 5), 0, 106, 167) && Near(Shown("SV", 24, 20), 254, 204, 0), S(Shown("SV", 5, 5)) + " / " + S(Shown("SV", 24, 20)));
        Check("  AR: none known - grey", Near(Shown("AR", 30, 20), 120, 130, 145), S(Shown("AR", 30, 20)));
        Check("  DE: the program's own picture as before", Near(Shown("DE", 30, 4), 0, 0, 0), S(Shown("DE", 30, 4)));

        Console.WriteLine("--- the tool ---");
        int changed = 0;
        LanguageFlags.Changed += (s, e) => changed++;
        var f = new TranslationFlagForm(new[] { "EN", "TR", "DE", "PL", "AR" }, "AR");
        f.Show(); Application.DoEvents();
        Check("opened on the language given", (string)Get(f, "Language") == "AR");
        Call(f, "Search", "🇫🇷");
        Check("  a flag emoji pasted in the search: that country alone", ((List<string>)Get(f, "CountriesListed")).SequenceEqual(new[] { "FR" }),
              string.Join(",", (List<string>)Get(f, "CountriesListed")));
        Call(f, "Search", "swe");
        Check("  its name typed: Sweden", ((List<string>)Get(f, "CountriesListed")).SequenceEqual(new[] { "SE" }));
        Call(f, "Search", "");
        Call(f, "ChooseCountry", "AE");
        var chosen = (Bitmap)Get(f, "Chosen");
        Check("  a country chosen: its flag shown as the new one", chosen != null && Near(chosen.GetPixel(3, 20), 255, 0, 0) && Near(chosen.GetPixel(40, 3), 0, 115, 47),
              chosen == null ? "none" : S(chosen.GetPixel(3, 20)) + " / " + S(chosen.GetPixel(40, 3)));
        Check("  kept (Maintenance): the language's flag - in Translations\\Flags, shown, said", (bool)Call(f, "SaveFlag") && File.Exists(LanguageFlags.FileOf("AR")) &&
              Near(Shown("AR", 3, 20), 255, 0, 0) && changed == 1, (string)Get(f, "StatusText"));

        Call(f, "ChoosePattern", LanguageFlags.Pattern.ThreeVertical, new[] { Color.FromArgb(0, 128, 0), Color.White, Color.FromArgb(200, 0, 0) });
        chosen = (Bitmap)Get(f, "Chosen");
        Check("stripes in the colours chosen", chosen != null && Near(chosen.GetPixel(5, 20), 0, 128, 0) && Near(chosen.GetPixel(30, 20), 255, 255, 255) && Near(chosen.GetPixel(55, 20), 200, 0, 0));
        Call(f, "ChoosePattern", LanguageFlags.Pattern.NordicCross, new[] { Color.FromArgb(0, 0, 160), Color.FromArgb(255, 255, 0) });
        chosen = (Bitmap)Get(f, "Chosen");
        Check("  a Nordic cross: the field, the cross", chosen != null && Near(chosen.GetPixel(3, 3), 0, 0, 160) && Near(chosen.GetPixel(20, 20), 255, 255, 0),
              chosen == null ? "none" : S(chosen.GetPixel(3, 3)) + " / " + S(chosen.GetPixel(20, 20)));

        var png = Path.Combine(dir, "wide.png");
        using (var wide = new Bitmap(300, 100))
        {
            using (var g = Graphics.FromImage(wide))
            {
                g.Clear(Color.Black);
                g.FillRectangle(Brushes.Orange, 75, 0, 150, 100);         // its middle, 150 x 100 (3:2)
            }
            wide.Save(png, System.Drawing.Imaging.ImageFormat.Png);
        }
        Check("a picture (300 x 100): its middle taken in the flags' shape", (bool)Call(f, "ChoosePicture", png) && ((Bitmap)Get(f, "Chosen")).Width == 60 &&
              Near(((Bitmap)Get(f, "Chosen")).GetPixel(2, 20), 255, 165, 0) && Near(((Bitmap)Get(f, "Chosen")).GetPixel(57, 20), 255, 165, 0));
        File.WriteAllText(Path.Combine(dir, "no.png"), "not a picture");
        Check("  not a picture: said", !(bool)Call(f, "ChoosePicture", Path.Combine(dir, "no.png")) && ((string)Get(f, "StatusText")).Contains("could not be read"));
        Call(f, "ChoosePicture", png);
        Call(f, "SaveFlag");
        Check("  kept: shown (orange), the picture 60 x 40", Near(Shown("AR", 30, 20), 255, 165, 0) && LanguageFlags.Picture("AR").Width == 60, S(Shown("AR", 30, 20)));

        SecurityManager.Login("op", SecurityLevel.Operator);
        Call(f, "ChooseCountry", "JP");
        Check("an Operator: not kept, said", !(bool)Call(f, "SaveFlag") && ((string)Get(f, "StatusText")).Contains("Maintenance"));
        SecurityManager.Login("maint", SecurityLevel.Maintenance);

        f.GetType().GetProperty("Language", Any).SetValue(f, "PL", null);
        Call(f, "ChooseCountry", "FR");
        Call(f, "SaveFlag");
        Check("another language's flag made: PL shown with it", Near(Shown("PL", 3, 20), 0, 85, 164), S(Shown("PL", 3, 20)));
        Check("  The program's own: the flag made taken away - Poland's again", (bool)Call(f, "RemoveFlag") && !File.Exists(LanguageFlags.FileOf("PL")) &&
              Near(Shown("PL", 30, 32), 220, 20, 60), S(Shown("PL", 30, 32)));
        f.Close();

        Console.WriteLine("--- on the Translation page ---");
        var page = new TranslationsControl();
        var host = new Form { Width = 1400, Height = 700 };
        page.Dock = DockStyle.Fill;
        host.Controls.Add(page);
        host.Show(); Application.DoEvents();
        var button = Tree(page).OfType<Button>().FirstOrDefault(b => b.Name == "trnFlag");
        TranslationFlagForm opened = null;
        TranslationFlagForm.Showing = x => opened = x;
        if (button != null) button.PerformClick();
        Application.DoEvents();
        Check("a flag button (the language in force's flag on it): opens the tool on that language", button != null && opened != null &&
              string.Equals((string)Get(opened, "Language"), LanguageManager.CurrentLanguage, StringComparison.OrdinalIgnoreCase));
        TranslationFlagForm.Showing = null;
        host.Close();

        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fail == 0 ? "\nLANGUAGE FLAG TOOL ALL PASS" : "\nLANGUAGE FLAG TOOL " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
