using System; using System.Windows.Forms; using Kaban.UI.Controls;
class N { static int fail=0;
  static void Check(string w,bool ok,string d=""){Console.WriteLine((ok?"  PASS  ":"  FAIL  ")+w+(d.Length>0?"   ["+d+"]":""));if(!ok)fail++;}
  [STAThread] static void Main(){
    var b = new ButtonHMI();
    b.Text = "HMI Settings";                       // the way MainForm does it
    Check("caption captured", b.DisplayText == "HMI Settings", "["+b.DisplayText+"]");
    Check("base.Text left EMPTY so WinForms draws nothing",
          ((Control)b).Text == "", "["+((Control)b).Text+"]");
    b.TranslationKey = "Btn_HmiSettings"; b.ApplyTranslation();
    Check("still empty after translating", ((Control)b).Text == "", "["+((Control)b).Text+"]");
    b.ShowCaption = false;
    Check("hiding does not restore it", ((Control)b).Text == "");
    Check("and the caption survives for expand", b.DisplayText.Length > 0, b.DisplayText);
    Console.WriteLine(fail==0?"\nNAV ALL PASS":"\n"+fail+" FAILED");
    Environment.Exit(fail==0?0:1); } }

