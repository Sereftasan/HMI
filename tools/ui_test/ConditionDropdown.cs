using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;
using Kaban.UI.Design;

// THE CONDITION DROPDOWNS in the designer (30 September): EnableConditions listed
// nothing ("No logic found") when another condition dropdown had been opened
// first - the machine's conditions were cleared at each opening and loaded by a
// MachineConfig.Initialize() that runs only once.
class CD {
    static int fail = 0;
    static void Check(string w, bool ok, string d = "") {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + w + (d.Length > 0 ? "   [" + d + "]" : "")); if (!ok) fail++;
    }

    class EditorService : IWindowsFormsEditorService {
        public Control Shown;
        public void CloseDropDown() { }
        public void DropDownControl(Control control) { Shown = control; }
        public DialogResult ShowDialog(Form dialog) { return DialogResult.Cancel; }
    }
    class Types : ITypeResolutionService {
        public Assembly GetAssembly(AssemblyName name) { return null; }
        public Assembly GetAssembly(AssemblyName name, bool throwOnError) { return null; }
        public string GetPathOfAssembly(AssemblyName name) { return null; }
        public Type GetType(string name) { return GetType(name, false, false); }
        public Type GetType(string name, bool throwOnError) { return GetType(name, throwOnError, false); }
        public Type GetType(string name, bool throwOnError, bool ignoreCase) {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { var t = a.GetType(name, false, ignoreCase); if (t != null) return t; }
            return null;
        }
        public void ReferenceAssembly(AssemblyName name) { }
    }
    class Host : IDesignerHost {
        public string Root = "";
        public bool Loading { get { return false; } }
        public bool InTransaction { get { return false; } }
        public IContainer Container { get { return null; } }
        public IComponent RootComponent { get { return null; } }
        public string RootComponentClassName { get { return Root; } }
        public string TransactionDescription { get { return ""; } }
        public event EventHandler Activated { add { } remove { } }
        public event EventHandler Deactivated { add { } remove { } }
        public event EventHandler LoadComplete { add { } remove { } }
        public event DesignerTransactionCloseEventHandler TransactionClosed { add { } remove { } }
        public event DesignerTransactionCloseEventHandler TransactionClosing { add { } remove { } }
        public event EventHandler TransactionOpened { add { } remove { } }
        public event EventHandler TransactionOpening { add { } remove { } }
        public void Activate() { }
        public IComponent CreateComponent(Type t) { return null; }
        public IComponent CreateComponent(Type t, string n) { return null; }
        public DesignerTransaction CreateTransaction() { return null; }
        public DesignerTransaction CreateTransaction(string d) { return null; }
        public void DestroyComponent(IComponent c) { }
        public IDesigner GetDesigner(IComponent c) { return null; }
        public Type GetType(string n) { return null; }
        public void AddService(Type t, object o) { }
        public void AddService(Type t, object o, bool p) { }
        public void AddService(Type t, ServiceCreatorCallback c) { }
        public void AddService(Type t, ServiceCreatorCallback c, bool p) { }
        public void RemoveService(Type t) { }
        public void RemoveService(Type t, bool p) { }
        public object GetService(Type t) { return null; }
    }
    class Provider : IServiceProvider {
        public readonly EditorService Editor = new EditorService();
        public readonly Host Host = new Host();
        public readonly Types Types = new Types();
        public object GetService(Type t) {
            if (t == typeof(IWindowsFormsEditorService)) return Editor;
            if (t == typeof(IDesignerHost)) return Host;
            if (t == typeof(ITypeResolutionService)) return Types;
            return null;
        }
    }
    class Context : ITypeDescriptorContext {
        public object Instance { get; set; }
        public PropertyDescriptor PropertyDescriptor { get; set; }
        public IContainer Container { get { return null; } }
        public void OnComponentChanged() { }
        public bool OnComponentChanging() { return true; }
        public object GetService(Type t) { return null; }
    }

    static List<string> Open(object control, string property, string root) {
        var p = new Provider();
        p.Host.Root = root;
        var ctx = new Context { Instance = control, PropertyDescriptor = TypeDescriptor.GetProperties(control)[property] };
        new UnifiedConditionEditor().EditValue(ctx, p, ctx.PropertyDescriptor.GetValue(control));
        return p.Editor.Shown == null ? new List<string>() : p.Editor.Shown.Controls.OfType<CheckBox>().Select(c => c.Text).ToList();
    }

    [STAThread]
    static void Main() {
        // the FA2010 assembly has to be in the domain for the type lookups
        var fa = typeof(Kaban.Machines.FA2010.FA2010Module).Assembly;
        const string page = "Kaban.Machines.FA2010.Screens.Parameters.ParamProcess";
        var field = new NumericInputHMI();

        var visibility = Open(field, "VisibilityConditions", page);
        Check("VisibilityConditions lists the machine's conditions", visibility.Count >= 5 && visibility.Contains("Robot is at Outside"), string.Join(" | ", visibility));
        var enable = Open(field, "EnableConditions", page);
        Check("EnableConditions opened after it lists the same", enable.Count == visibility.Count && enable.SequenceEqual(visibility), string.Join(" | ", enable));
        var again = Open(field, "VisibilityConditions", page);
        Check("and Visibility again, still all of them", again.SequenceEqual(visibility));
        var button = new ButtonHMI();
        Check("a button's EnableConditions too", Open(button, "EnableConditions", page).Count == visibility.Count);

        MachineConditionMap.Clear();   // as a second designer window might leave it
        var afterClear = Open(field, "EnableConditions", page);
        Check("conditions cleared elsewhere: loaded again", afterClear.SequenceEqual(visibility), afterClear.Count.ToString());

        var ticked = new NumericInputHMI { EnableConditions = SystemCondition.ManualMode };
        var p2 = new Provider(); p2.Host.Root = page;
        var ctx2 = new Context { Instance = ticked, PropertyDescriptor = TypeDescriptor.GetProperties(ticked)["EnableConditions"] };
        new UnifiedConditionEditor().EditValue(ctx2, p2, ticked.EnableConditions);
        var boxes = p2.Editor.Shown.Controls.OfType<CheckBox>().ToList();
        Check("the value set is ticked", boxes.Count(b => b.Checked) == 1 && boxes.First(b => b.Checked).Text == EnumHelper.GetDescription(SystemCondition.ManualMode),
              string.Join(",", boxes.Where(b => b.Checked).Select(b => b.Text)));

        Console.WriteLine(fail == 0 ? "\nCONDITION DROPDOWN ALL PASS" : "\nCONDITION DROPDOWN " + fail + " FAILED");
        Environment.Exit(fail == 0 ? 0 : 1);
    }
}
