using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using TC = Kaban.Core.Helpers.TroubleshootingCatalog;

class G {
    static int fail=0;
    static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d.Replace("\r\n"," / ")+"]":""));if(!ok)fail++;}
    static T F<T>(object o, string name){
        return (T)o.GetType().GetField(name, BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    }

    [STAThread] static void Main(){
        Func<string,int> read = a => 0;
        Func<SystemCondition,bool> cond = c => true;

        Console.WriteLine("--- a button on a DEAD station greys itself and says why ---");
        var b = new ButtonHMI { PlcNumber = 11, RequiredSecurity = SecurityLevel.None, FallbackText = "Jog" };
        ConnectionMonitor.ReportRead(11, false, "not connected");
        b.UpdateState(read, cond);
        Check("soft-disabled", !F<bool>(b, "_softEnabled"));
        Check("the reason names the failure", F<string>(b, "_disabledReason").Contains("C02"), F<string>(b, "_disabledReason"));
        Check("arrow cursor, not a hand", b.Cursor == Cursors.Default);
        Check("the write path refuses it", PlcManager.Instance.GetDriverForWrite(11) == null);

        Console.WriteLine("--- and comes back by itself ---");
        ConnectionMonitor.ReportRead(11, true);
        b.UpdateState(read, cond);
        Check("soft-enabled again", F<bool>(b, "_softEnabled"));
        Check("reason cleared", F<string>(b, "_disabledReason") == "");
        Check("hand cursor", b.Cursor == Cursors.Hand);

        Console.WriteLine("--- a numeric field follows the same rule ---");
        var n = new NumericInputHMI { PlcNumber = 12, Address = "D100", RequiredSecurity = SecurityLevel.None };
        ConnectionMonitor.ReportRead(12, false, "read failed");
        n.UpdateState(read, cond);
        Check("soft-disabled", !F<bool>(n, "_softEnabled"));
        Check("reason names C03", F<string>(n, "_disableReason").Contains("C03"), F<string>(n, "_disableReason"));

        Console.WriteLine("--- no evidence, no refusal ---");
        var fresh = new ButtonHMI { PlcNumber = 13, RequiredSecurity = SecurityLevel.None };
        fresh.UpdateState(read, cond);
        Check("a station never polled is not locked", F<bool>(fresh, "_softEnabled"));

        Console.WriteLine("--- the help opens ONE section, as a real file ---");
        var doc = TC.ResolveConnection("C02");
        var path = TC.ExtractSection(doc);
        Check("a path, not a URL fragment", !path.Contains("#"), path);
        Check("  that exists", File.Exists(path));
        var html = File.ReadAllText(path);
        Check("  holding C02", html.Contains("id=\"C02\""));
        Check("  and NOT the other codes", !html.Contains("id=\"C01\"") && !html.Contains("id=\"C03\"") && !html.Contains("id=\"C04\""));
        Check("  with the safety warning kept", html.Contains("Before anything else"));

        var pdf = new TC.HelpDocument { FileName = "Cylinder_Troubleshooting_Manual_EN.pdf", Section = "1.4" };
        var pdfPath = TC.ExtractSection(pdf);
        Check("a PDF section opens the PDF itself, with no fragment", pdfPath == pdf.FullPath && !pdfPath.Contains("#"), pdfPath);

        Console.WriteLine(fail==0?"\nGATE ALL PASS":"\n"+fail+" FAILED");
        Environment.Exit(fail==0?0:1);
    }
}

