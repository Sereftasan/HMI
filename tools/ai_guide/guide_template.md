# Kaban HMI - page design guide for an AI

*Made from the program itself on {{DATE}} by `tools/ai_guide/MakeAiPageGuide.cs` (the controls, their
properties and the examples are read from the code and the real pages; the rest is `tools/ai_guide/guide_template.md`).
Make it again after the controls change.*

You are helping to build pages of **Kaban HMI**, the operator panel program of Kaban Makina's aluminium / PVC profile
processing machines. This file tells you everything the program expects of a page: which of our controls to use for
which function, their properties, how a page is laid out, how its addresses, scaling, translations and security work,
and what to hand back. **Use only what this file describes; where something is not given, ask - never invent a PLC
address, a property or a control.**

## How to use this file

Attach it to the chat, together with what the page is made from, for example:

- another HMI's page: its designer file (`.Designer.cs`, `.xaml`, a WinCC / GT Designer export ...), a screenshot or a PDF of it;
- a device list or a PLC label list (`Label;DataType;Address`), the machine's I/O list (`IoList.csv`: `Label;Kind;Address;Read;Plc`);
- one of our pages of the same kind as a model (for example `Kaban.Machines.FA2010\Screens\Manual\ManualCutting.Designer.cs`) - optional: the examples below show the style.

Then ask, for example: *"Using the Kaban HMI page design guide, make the FA2010 Manual Movement page 'Exit' from the attached
old designer file: the .cs and .Designer.cs, the .csproj lines, the tab line for FA2010Module.cs and the Translations.csv rows."*

## 1. The program in short

- **C# / .NET Framework 4.x / WinForms**, built in Visual Studio. Projects: `Kaban.Core` (no WinForms in it), `Kaban.UI`
  (folder `KabanHMI`: the controls, namespace `Kaban.UI.Controls`), and one project per machine, `Kaban.Machines.<ID>`
  (FA2010, H500, H800, MD3020, SB3012), whose pages are in `Screens\<Folder>\`.
- **A page is a `UserControl` in two files** (a partial class):
  - `<Page>.cs` - only the constructor calling `InitializeComponent()`;
  - `<Page>.Designer.cs` - every control: a private field, created in `InitializeComponent`, its properties set, added with
    `this.Controls.Add(...)` (or to its group box). It must open in the Visual Studio designer: no code but the designer's own.
- **The HMI reads every PLC address of the page on show itself** (a scanner, one batched read): a control needs only its
  addresses - no code behind, no timers, no events.
- **Where a page appears:** a machine's screens are tabs, made in `<ID>Module.cs`, for example
  `new Screens.GeneratedPage("Tab_ManualExit", "Exit", SecurityLevel.Operator, () => new Screens.Manual.ManualExit())`
  in `ManualPages` (Manual Movement, `Screens\Manual`, Operator) or `MachineSettings` (Machine Parameters,
  `Screens\Parameters`, Maintenance). The tab's caption is the translation `Tab_<Class>`.
- **The project file is the old style: every file is listed.** A page adds:

```xml
<Compile Include="Screens\Manual\ManualExit.cs">
  <SubType>UserControl</SubType>
</Compile>
<Compile Include="Screens\Manual\ManualExit.Designer.cs">
  <DependentUpon>ManualExit.cs</DependentUpon>
</Compile>
```

- **Translations:** `Kaban.Machines.<ID>\Translations.csv` (the machine's words) and `KabanHMI\CommonTranslations.csv`
  (shared ones), one row a key: `Key;EN;TR;RU;DE;IT;ES` - never a `;` inside a text. A caption's key is usually its
  English text (`DescriptionText`, `TitleTextKey`, `TranslationKey` = "Profile Clamping 6"); a missing translation
  shows the English.

## 2. Which control for which function

| On the machine / on the old page | Our control | How |
|---|---|---|
| A cylinder or any two-way movement: two buttons (back / forward, down / up, open / close, release / clamp), its end sensors, maybe a position | **CylinderUnitHMI** | One unit per cylinder. Button **A** (left) = back / down / open / release, **B** (right) = forward / up / close / clamp. `CommandAAddress` / `CommandBAddress` with `CommandAAction` / `CommandBAction`; the end sensors as `Lamp1AAddress` / `Lamp1BAddress` (and `Lamp2...`); `PositionAddress` for a position. `ValveType` and `FeedbackType` from the electrical and pneumatic drawings; the PLC's error bits as `ErrorAAddress` / `ErrorBAddress`. Size 360 x 64. |
| A servo axis on a Manual Movement page | **AxisUnitHMI** | Only `AxisNumber` (and `PlcNumber`): its buttons, position, name and reference sensor come from that axis' AxisSettingHMI row. Size 360 x 64. |
| A servo axis' settings: limits, reference, calibration, its codes | **AxisSettingHMI** | One row per axis on the Axis Parameters page; `HMIAxisIndex` is the axis number the AxisUnitHMI uses. |
| A pneumatic positioning: a cylinder with a position sensor sent to a target (a mould, a stop) - on the old page a unit with Target / Send / Calibration / Calibrate / Tolerance / Stopping Offset beside it | **PneumaticPositioningHMI** | One row per positioning, the rows one under the other 44 px apart, the five heading Labels above them (section 3). `CommandBackAddress` / `CommandForwardAddress` (`VerticalMovement` for down / up), `PositionAddress` with its scaling, `TargetPosition` + `PositioningStartAddress`, `CalibrationAddress` + `CalibrateAddress`, `PositioningToleranceAddress`, `PositioningDirectionReferenceAddress` (the old "Stopping Offset"), their scaling in `Divisor` / `DecimalPlaces`; `ValveType` / `FeedbackType` and the error bits as a cylinder unit's. Never rebuild it from loose fields and buttons. |
| A push button: a function on / off, start, reset, a jog, one-way movement | **ButtonHMI** | `BitActions`: one `Kaban.Core.Models.BitAction` a bit - `Momentary` (on while pressed), `Alternate` (each press toggles), `SET` (on; the PLC switches it off), `RESET`. Its state or sensor as `Lamp1Address`. `TranslationKey` + `FallbackText` = its caption. A row of an old page with ONE button is a ButtonHMI, with TWO opposite buttons a CylinderUnitHMI. |
| A sensor, a state, an output shown | **LampHMI** | `Address` of the bit; its caption `DescriptionText`. Size 36 x 36, the caption drawn beside it. |
| A value typed in: a parameter, a speed, a target, a time | **NumericInputHMI** | `Address` (a D word), `DataType` (`Bin_16`, or `Bin_32` for a double word), `Divisor`, `DecimalPlaces`, `Suffix`, `UnitType`, `MinLimit` / `MaxLimit`; `DescriptionText` its caption (drawn to its left). |
| A value only shown: a position, a counter, a code | **NumericDisplayHMI** | As NumericInputHMI, without the input; `DataFormat = Hexadecimal` for an inverter's or a drive's error code. |
| An option bit on / off (machine options) | **CheckBoxHMI** | `Address`, `DescriptionText`, `DescriptionSide = Left` on parameter pages. Many options at once: **CheckListHMI**. |
| A choice of a few values written to one word | **ComboBoxHMI** | `Options`, one per line: `TranslationKey|Fallback|Value`. |
| A heading, a section's title on a page | **LabelHMI** | `TranslationKey` = `Text` = its English text; bold Segoe UI 10 (a column title: 8, grey). |
| A group of small buttons belonging together (0° 90° 180° 270°, Drills 1 - 4) | **GroupBoxHMI** with ButtonHMI inside | `TitleTextKey`; the buttons are added to the group (`this.grp.Controls.Add(...)`). Size 360 x 64. |
| A load, a pressure, a percentage as a gauge | **MeterHMI** (shown) / **MeterInputHMI** (set) | `Address`, `Divisor`, `ScaleMinimum` / `ScaleMaximum`, bands. |

Do **not** use stock WinForms controls (`Button`, `Label`, `TextBox`, `CheckBox` ...) for anything bound to the PLC.

## 3. Laying a page out

From our pages (FA2010's Manual Movement and Machine Parameters, made by `tools/legacy_fa2010_pages.py`):

- **Absolute pixels at 100 %**: `this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;` - the HMI scales the whole
  screen itself (80 % - 150 %) and would otherwise scale twice. `this.AutoScroll = true;`, `this.BackColor = System.Drawing.Color.White;`,
  `this.Size` = the right and bottom of the last control plus the margin.
- **A Manual Movement page reads as a table, top to bottom, then the next column:** margin 16 px left and top; units
  **360 x 64**, columns **390 px** apart (a unit and a 30 px gutter), **10 px** between rows; a lamp row 36 high, a button
  row 40, a heading 26. A column longer than about 650 px goes on at the top of the next column - reading order, never
  snaking left and right. A section of the old page is a LabelHMI heading above its rows.
- **A page of pneumatic positionings** (PneumaticPositioningHMI): the rows at x 16, the first at y 54, each 40 high and **44 px** apart;
  above them five plain `System.Windows.Forms.Label`s, Segoe UI 8 bold, grey (110, 116, 124), `TextAlign = BottomCenter`, 32 high at y 16,
  `Tag = "PositioningHeading:Manual"` ("Manual movement"), `"...:Target"` ("Target position"), `"...:Calibration"` ("Calibration"),
  `"...:Tolerance"` ("Positioning tolerance"), `"...:DirectionRef"` ("Direction ref") - the rows put them over their columns and
  translate them. Names `pp<Name>`.
- **A Machine Parameters page:** a NumericInputHMI box **116 x 26** a row, **36 px** apart, its caption (DescriptionText)
  drawn to its left - the box placed so that the captions of a column fit; columns side by side with 40 px between;
  headings above a column; option tick boxes (CheckBoxHMI 44 x 32, `DescriptionSide = Left`) in the same rows.
- **Names:** `unit<Name>` (CylinderUnitHMI, AxisUnitHMI), `btn<Name>`, `lamp<Name>`, `num<Name>` (NumericInputHMI),
  `disp<Name>` (NumericDisplayHMI), `opt<Name>` (CheckBoxHMI), `grp<Name>`, `lbl<Name>`, `ax<Name>` (AxisSettingHMI);
  `<Name>` from its caption without spaces (`unitProfile_Clamping_6`). Every control has a `Name`.
- **Order in the file:** the fields created first, then each control's block (a `// name` comment header, its properties),
  then the page's own properties and `this.Controls.Add(...)` in reading order, then the field declarations.

## 4. Addresses, values, security

- **`PlcNumber`** - the machine's own PLC number as its project sees it: **1** = its main PLC (2 = a second one). The HMI
  maps it to the panel's station (a sub-machine's 1 is its own PLC) - never put a station number of the panel here.
- **Addresses are Mitsubishi device names as strings**: `"M695"`, `"D16"`, `"L3"`, `"B16"`. **A physical input or output
  is read through the relay the PLC program copies it into** - the `Read` column of the machine's `IoList.csv` (FA2010's input
  `X60` is read as `M232`): a lamp of a sensor gets that relay; only where the `Read` column is empty, the X / Y address itself
  (some property descriptions below say "(X)" or "(Y)" - the same rule applies). Manual commands are M bits (labels `MAN_...`),
  sensors `IN_...`, outputs `OUT_...`, values the HMI sets `PC_...`.
- **Scaling** - the control divides the raw PLC value once: `Divisor` and `DecimalPlaces` as the PLC program scales it
  (a word in 0.01 mm: `Divisor = 100`, `DecimalPlaces = 2`, `Suffix = "mm"`; a time in 0.1 s: `Divisor = 10`, 1 decimal,
  `Suffix = "s"`). A double word (two D's) is `DataType = Bin_32`. `UnitType` (Length, Time ...) lets a user see inches or
  minutes - the PLC keeps its own unit.
- **`RequiredSecurity`**: `Operator` on Manual Movement, `Maintenance` on Machine Parameters (calibration, options), `Admin`
  for what only the builder sets. **`LogAction = true`** on everything that writes to the PLC (the log says who did what).
- **Conditions** - `EnableConditions` / `VisibilityConditions`: flags of `SystemCondition` (below) and the machine's own named
  conditions; leave them `None` unless the old page greyed or hid the control (or an option decides whether the machine has the part).
  A machine's own condition is written as its number, for example
  `this.ppConveyor_Back_Vertical.VisibilityConditions = ((Kaban.Core.Enums.SystemCondition)(524288L));` - the ones registered now:

{{MACHINE_CONDITIONS}}

  A new one is registered in `<ID>Module.cs` (`RegisterConditions`: `MachineConditionMap.RegisterMachineBit(ctx.AllocateMachineBit("Tag"), ...)`),
  after the machine's others so their numbers stay; ask for it rather than inventing a number.
- **Help**: `ToolTipTextKey` / tips - a sentence for a user, translated like the captions.

## 5. The controls

{{CONTROLS}}

## 6. The values of the enums

{{ENUMS}}

## 7. What to hand back

1. The complete `<Page>.cs` and `<Page>.Designer.cs` (namespace `Kaban.Machines.<ID>.Screens.<Folder>`).
2. The `.csproj` lines (section 1).
3. The tab line for `<ID>Module.cs` (section 1).
4. The `Translations.csv` rows of every new caption: `Key;EN;TR;RU;DE;IT;ES` (the Turkish when the old page or the device
   list has it, else leave it empty - English shows).
5. A short table of the addresses used: the control, the address, where it came from (the old page, the device list).
6. What could not be mapped or placed, and every assumption - instead of an invented address.

**Rules:** keep the old page's addresses exactly as they are; one of our controls per function (section 2); every control
named; `AutoScaleMode.None`; nothing of our controls' types in a `.resx`; no code but the designer's; the layout of section 3.

## 8. One of our pages, whole

{{PAGES}}
