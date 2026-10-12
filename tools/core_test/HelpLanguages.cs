using System; using System.IO; using Kaban.Core.Helpers; using TC = Kaban.Core.Helpers.TroubleshootingCatalog;
class L2 {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}
    static string Open(string file){ return Path.GetFileName(new TC.HelpDocument { FileName = file }.FullPath); }
    [STAThread] static void Main(){
        // The program only switches to a language it has translations for.
        LanguageManager.LoadCommonTranslations(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CommonTranslations.csv"));
        Console.WriteLine("--- one name, the file of the HMI's language ---");
        LanguageManager.ChangeLanguage("EN");
        Check("English HMI opens the English sheet", Open("Alarm_Robot_Clamping_Failure.html") == "Alarm_Robot_Clamping_Failure.en.html", Open("Alarm_Robot_Clamping_Failure.html"));
        LanguageManager.ChangeLanguage("TR");
        Check("the HMI really switched to Turkish", LanguageManager.CurrentLanguage == "TR", LanguageManager.CurrentLanguage);
        Check("Turkish HMI opens the Turkish sheet", Open("Alarm_Robot_Clamping_Failure.html") == "Alarm_Robot_Clamping_Failure.tr.html", Open("Alarm_Robot_Clamping_Failure.html"));
        Check("  and it exists", new TC.HelpDocument { FileName = "Alarm_Robot_Clamping_Failure.html" }.Exists);
        Check("a sheet not yet translated falls back to English", Open("Unit_Robot_Top_Clamping.html") == "Unit_Robot_Top_Clamping.en.html", Open("Unit_Robot_Top_Clamping.html"));
        LanguageManager.ChangeLanguage("RU");
        Check("Russian HMI, nothing translated yet: English", Open("Axis_Robot_X.html") == "Axis_Robot_X.en.html", Open("Axis_Robot_X.html"));
        Check("a file with no language at all is opened as it is", Open("Cylinder_Troubleshooting.html") == "Cylinder_Troubleshooting.html");
        Check("a name that already says its language is taken as given", Open("Alarm_Robot_Clamping_Failure.tr.html") == "Alarm_Robot_Clamping_Failure.tr.html");
        Check("a missing document is still reported missing", !new TC.HelpDocument { FileName = "Not_There.html" }.Exists);
        Console.WriteLine(fail==0?"\nLANGUAGE ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

