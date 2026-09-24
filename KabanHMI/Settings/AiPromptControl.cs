using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Kaban.Core.Enums;
using Kaban.Core.Helpers;
using Kaban.UI.Controls;

namespace Kaban.UI.Settings
{
    /// <summary>
    /// Builds a ready-to-paste AI prompt for generating or upgrading a page's
    /// .Designer.cs file.
    ///
    /// The point is that an assistant cannot guess this program's conventions - the
    /// control names, which property carries a PLC address, how a raw value is
    /// scaled, or that a page must be a partial class split across two files. The
    /// prompt states all of it, so the generated file drops straight into the
    /// project and opens on the design surface.
    /// </summary>
    public class AiPromptControl : UserControl
    {
        private readonly ComboBox _task = new ComboBox();
        private readonly TextBox _pageName = new TextBox();
        private readonly TextBox _project = new TextBox();
        private readonly TextBox _notes = new TextBox();
        private readonly TextBox _output = new TextBox();

        public AiPromptControl()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.White;

            var form = new Panel { Dock = DockStyle.Top, Height = 176, Padding = new Padding(12, 10, 12, 0) };
            int y = 8;
            Func<string, Control, int, Control> row = (caption, ctl, width) =>
            {
                form.Controls.Add(new Label
                {
                    Text = caption,
                    Left = 8, Top = y + 4, Width = 150,
                    TextAlign = ContentAlignment.MiddleRight,
                    Font = new Font("Segoe UI", 9F)
                });
                ctl.Left = 164; ctl.Top = y; ctl.Width = width;
                form.Controls.Add(ctl);
                y += 30;
                return ctl;
            };

            _task.DropDownStyle = ComboBoxStyle.DropDownList;
            _task.Items.AddRange(new object[]
            {
                "Import a page from another HMI project",
                "Add controls to an existing page",
                "Convert a device/parameter list into a page",
                "Re-lay-out an existing page",
                "Explain / review a Designer file"
            });
            _task.SelectedIndex = 0;
            row(LanguageManager.GetString("Ai_Task", "What do you want to do") + ":", _task, 420);
            _pageName.Text = "ManualEntry_Conveyor";
            row(LanguageManager.GetString("Ai_Page", "Page (class) name") + ":", _pageName, 260);
            _project.Text = "Kaban.Machines.FA2010";
            row(LanguageManager.GetString("Ai_Project", "Machine project") + ":", _project, 260);
            _notes.Multiline = true;
            _notes.Height = 46;
            _notes.ScrollBars = ScrollBars.Vertical;
            row(LanguageManager.GetString("Ai_Notes", "Your notes for the AI") + ":", _notes, 560);

            var tips = UiStyle.Tips();
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(160, 4, 8, 4) };
            bar.Controls.Add(UiStyle.ToolButton("\u2699",
                LanguageManager.GetString("Ai_Build", "Build the prompt"), tips, (s, e) => Build()));
            bar.Controls.Add(UiStyle.ToolButton("\uD83D\uDCCB",
                LanguageManager.GetString("Ai_Copy", "Copy the prompt to the clipboard"), tips, (s, e) => Copy()));
            bar.Controls.Add(UiStyle.ToolButton("\uD83D\uDCBE",
                LanguageManager.GetString("Ai_Save", "Save the prompt to a text file"), tips, (s, e) => Save()));

            _output.Multiline = true;
            _output.ReadOnly = true;
            _output.ScrollBars = ScrollBars.Both;
            _output.WordWrap = false;
            _output.Dock = DockStyle.Fill;
            _output.Font = new Font("Consolas", 9F);
            _output.BackColor = Color.FromArgb(28, 30, 34);
            _output.ForeColor = Color.FromArgb(214, 222, 230);

            Controls.Add(_output);
            Controls.Add(bar);
            Controls.Add(form);

            Build();
        }

        private void Copy()
        {
            try { if (_output.TextLength > 0) Clipboard.SetText(_output.Text); } catch { }
        }

        private void Save()
        {
            using (var dlg = new SaveFileDialog { Filter = "Text file (*.txt)|*.txt", FileName = "kaban_ai_prompt.txt" })
            {
                if (dlg.ShowDialog(FindForm()) != DialogResult.OK) return;
                try { System.IO.File.WriteAllText(dlg.FileName, _output.Text); } catch { }
            }
        }

        private void Build()
        {
            string page = string.IsNullOrWhiteSpace(_pageName.Text) ? "MyPage" : _pageName.Text.Trim();
            string proj = string.IsNullOrWhiteSpace(_project.Text) ? "Kaban.Machines.FA2010" : _project.Text.Trim();

            var sb = new StringBuilder();
            sb.AppendLine("You are editing a C# WinForms HMI for Kaban machine tools (.NET Framework,");
            sb.AppendLine("Visual Studio). Produce a WinForms DESIGNER file that drops straight into the");
            sb.AppendLine("project and opens on the design surface.");
            sb.AppendLine();
            sb.AppendLine("TASK");
            sb.AppendLine("  " + Convert.ToString(_task.SelectedItem));
            sb.AppendLine("  Target page : " + page);
            sb.AppendLine("  Project     : " + proj);
            if (!string.IsNullOrWhiteSpace(_notes.Text))
            {
                sb.AppendLine("  Notes       : " + _notes.Text.Replace(Environment.NewLine, " "));
            }
            sb.AppendLine();
            sb.AppendLine("FILE LAYOUT (both files, exactly this shape)");
            sb.AppendLine("  " + proj + @"\Screens\<Folder>\" + page + ".cs");
            sb.AppendLine("      public partial class " + page + " : UserControl");
            sb.AppendLine("      { public " + page + "() { InitializeComponent(); } }");
            sb.AppendLine("  " + proj + @"\Screens\<Folder>\" + page + ".Designer.cs");
            sb.AppendLine("      partial class " + page + " { private void InitializeComponent() { ... } }");
            sb.AppendLine("  RULES the Visual Studio designer enforces:");
            sb.AppendLine("    - every control gets a PRIVATE FIELD in the Designer file;");
            sb.AppendLine("    - every control is created in InitializeComponent and added with");
            sb.AppendLine("      this.Controls.Add(this.<field>);");
            sb.AppendLine("    - set this.AutoScaleMode = AutoScaleMode.None (these pages are hosted");
            sb.AppendLine("      inside another auto-scaling container; Font scaling doubles up and");
            sb.AppendLine("      inflates every control);");
            sb.AppendLine("    - do NOT put values of types from other projects into the .resx.");
            sb.AppendLine("  The .csproj needs:");
            sb.AppendLine("    <Compile Include=\"...\\" + page + ".cs\"><SubType>UserControl</SubType></Compile>");
            sb.AppendLine("    <Compile Include=\"...\\" + page + ".Designer.cs\">");
            sb.AppendLine("      <DependentUpon>" + page + ".cs</DependentUpon></Compile>");
            sb.AppendLine();
            sb.AppendLine("CONTROLS (namespace Kaban.UI.Controls) - use these, not stock WinForms");
            sb.AppendLine("  FunctionUnitHMI   a movement: two command buttons, sensor lamps and a");
            sb.AppendLine("                    position readout. Size 360x64.");
            sb.AppendLine("      CommandAAddress / CommandBAddress   PLC bits for the two buttons");
            sb.AppendLine("      CommandAAction / CommandBAction     BitActionType.Momentary | Alternate");
            sb.AppendLine("      Lamp1AAddress / Lamp1BAddress       sensor bits shown as lamps");
            sb.AppendLine("      PositionAddress / PositionDataType  DataTypeEnum.Bin_16 | Bin_32");
            sb.AppendLine("      PositionDivisor / PositionDecimalPlaces / PositionSuffix");
            sb.AppendLine("      Arrows            ButtonArrowStyle.LeftRight | UpDown | ...");
            sb.AppendLine("  NumericInputHMI   an editable value. Size 115x18, AutoSize = true.");
            sb.AppendLine("  NumericDisplayHMI a read-only value. Size 170x36.");
            sb.AppendLine("      Address, DataType, Divisor, DecimalPlaces, Digits, Suffix, UnitType");
            sb.AppendLine("      DescriptionText   the caption - it is ALSO the translation key and");
            sb.AppendLine("                        falls back to its own text; \" :\" is added");
            sb.AppendLine("                        automatically. There is no separate key property.");
            sb.AppendLine("  ButtonHMI         Size 170x36.  LampHMI / CheckBoxHMI  Size 36x36,");
            sb.AppendLine("                    caption on the RIGHT (DescriptionSide.Right).");
            sb.AppendLine("  Every PLC-bound control also has:");
            sb.AppendLine("      PlcNumber        station number (1 = the machine's own PLC)");
            sb.AppendLine("      RequiredSecurity SecurityLevel.None | Operator | Maintenance | Admin");
            sb.AppendLine("      LogAction        true to audit operator writes");
            sb.AppendLine();
            sb.AppendLine("SCALING - the control divides the RAW PLC value once");
            sb.AppendLine("  servo / linear position   Divisor 10000, 2 decimals, Suffix \"mm\"");
            sb.AppendLine("  cylinder / clamp position Divisor   100, 2 decimals, Suffix \"mm\"");
            sb.AppendLine("  times                     Divisor    10, 1 decimal,  Suffix \"sec\"");
            sb.AppendLine("  32-bit devices (W, D pairs) use DataTypeEnum.Bin_32.");
            sb.AppendLine();
            sb.AppendLine("LAYOUT");
            sb.AppendLine("  Fill the LEFT column top to bottom, then continue at the TOP of the next");
            sb.AppendLine("  column - reading order, never snaking left/right.");
            sb.AppendLine("  Usable page height: " + HmiLayout.PageHeight + " px (this machine).");
            sb.AppendLine("  Function units: 360 wide, 64 high, 6 px apart, columns 60 px apart.");
            sb.AppendLine("  Numeric inputs: 115x18, 16 px apart, 250 px reserved for the caption.");
            sb.AppendLine("  Margins: 12 px left and top.");
            sb.AppendLine();
            sb.AppendLine("PLC DEVICES - USE THESE, DO NOT INVENT ADDRESSES");
            int labels = Kaban.Core.Helpers.PlcLabelTable.Count;
            if (labels > 0)
            {
                sb.AppendLine("  The machine's label list is attached/pasted below as");
                sb.AppendLine("  Label;DataType;Address  (" + labels + " devices).");
                sb.AppendLine("  Bind controls BY LABEL and use the address exactly as given.");
                sb.AppendLine("  Every address in that list is one the HMI can read: physical inputs");
                sb.AppendLine("  and outputs have ALREADY been translated to the internal relay the");
                sb.AppendLine("  PLC copies them into, so you will never see X or Y - and must never");
                sb.AppendLine("  write one into a control.");
                sb.AppendLine("  Naming convention in the list:");
                sb.AppendLine("    MAN_...  a manual command bit  -> FunctionUnitHMI command");
                sb.AppendLine("    IN_...   a sensor/input        -> lamp (use its mirror)");
                sb.AppendLine("    OUT_...  an output             -> lamp or status");
                sb.AppendLine("    PC_...   a value the HMI sets  -> NumericInputHMI");
                sb.AppendLine("    Enkoder_/..._Pozisyon          -> position readout");
                sb.AppendLine("  A movement is usually a PAIR: ..._Ileri / ..._Geri (forward/back) or");
                sb.AppendLine("  ..._Yukari / ..._Asagi (up/down), plus ..._SIK / ..._AC (clamp/release).");
                sb.AppendLine("  Pair them onto ONE FunctionUnitHMI (button A = back/down/release).");
            }
            else
            {
                sb.AppendLine("  PASTE the machine's label list after this prompt, as");
                sb.AppendLine("  Label;DataType;Address;Mirror. Do not invent addresses: if a control");
                sb.AppendLine("  is needed for a device that is not in the list, say so instead.");
            }
            sb.AppendLine();
            sb.AppendLine("TRANSLATIONS");
            sb.AppendLine("  " + proj + @"\Translations.csv, columns Key;EN;TR;RU;DE;IT;ES");
            sb.AppendLine("  A tab caption uses the key Tab_<PageClassName>.");
            sb.AppendLine("  Untranslated cells fall back to English, so leaving one blank is safe.");
            sb.AppendLine();
            sb.AppendLine("OUTPUT");
            sb.AppendLine("  1. the complete " + page + ".cs and " + page + ".Designer.cs;");
            sb.AppendLine("  2. the exact .csproj lines to add;");
            sb.AppendLine("  3. any new Translations.csv rows;");
            sb.AppendLine("  4. a short list of the PLC addresses used, so they can be checked");
            sb.AppendLine("     against the device list.");
            sb.AppendLine("  State any assumption you had to make instead of inventing an address.");

            sb.AppendLine();
            switch (_task.SelectedIndex)
            {
                case 0:   // import
                    sb.AppendLine("FOR THIS IMPORT");
                    sb.AppendLine("  Paste the source page's designer code (or a screenshot) after this");
                    sb.AppendLine("  prompt. Map its controls onto the Kaban controls above, keep the PLC");
                    sb.AppendLine("  addresses exactly as they are, and REPORT any control with no");
                    sb.AppendLine("  equivalent rather than dropping it silently.");
                    sb.AppendLine("  Re-lay the page out to the column convention above - do not copy the");
                    sb.AppendLine("  source coordinates, they will not fit this shell.");
                    break;
                case 1:   // add controls
                    sb.AppendLine("FOR ADDING CONTROLS");
                    sb.AppendLine("  Return the COMPLETE Designer file, not a fragment - the designer");
                    sb.AppendLine("  needs the field declarations and InitializeComponent to stay in step.");
                    sb.AppendLine("  Continue the existing column flow; do not re-position what is there.");
                    sb.AppendLine("  List which devices you used from the label list.");
                    break;
                case 2:   // from a device list
                    sb.AppendLine("FOR BUILDING FROM THE DEVICE LIST");
                    sb.AppendLine("  Group the devices by unit (conveyor, robot, milling, cutting, exit),");
                    sb.AppendLine("  one page per group, and pair forward/back bits onto one");
                    sb.AppendLine("  FunctionUnitHMI. A device with a sensor but NO command bit is a");
                    sb.AppendLine("  LampHMI, not a unit. Say which devices you could not place.");
                    break;
                case 3:   // re-layout
                    sb.AppendLine("FOR RE-LAYING OUT");
                    sb.AppendLine("  Keep every control, its name and every PLC property EXACTLY as it is -");
                    sb.AppendLine("  change only Location and Size. Report the before/after count so it is");
                    sb.AppendLine("  obvious nothing was lost.");
                    break;
                default:  // explain / review
                    sb.AppendLine("FOR THIS REVIEW");
                    sb.AppendLine("  Do not rewrite anything. Report: controls bound to X or Y instead of a");
                    sb.AppendLine("  mirror; addresses absent from the label list; scaling that disagrees");
                    sb.AppendLine("  with the table above; controls overlapping or off the page; and");
                    sb.AppendLine("  missing RequiredSecurity on anything that writes to the PLC.");
                    break;
            }

            _output.Text = sb.ToString();
        }
    }

    /// <summary>The AI-prompt builder as a tab of HMI Settings (admin-only).</summary>
    public class AiPromptPage : ISettingsPage
    {
        public string TitleKey { get { return "Set_AiPrompt"; } }
        public string TitleFallback { get { return "AI Prompt"; } }
        public SecurityLevel RequiredLevel { get { return SecurityLevel.Admin; } }
        public UserControl GetControl() { return new AiPromptControl(); }
    }
}

