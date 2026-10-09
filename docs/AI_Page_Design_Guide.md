# Kaban HMI - page design guide for an AI

*Made from the program itself on 9 October 2026 by `tools/ai_guide/MakeAiPageGuide.cs` (the controls, their
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

  | Number | Name (as the conditions list shows it) | Machine | Read from |
  |---|---|---|---|
  | `65536L` (1 << 16) | Robot is at Outside | FA2010 | PLC 1 `X50` = 1 |
  | `131072L` (1 << 17) | Milling Unit is NOT Inside | FA2010 | PLC 1 `M22` = 0 |
  | `262144L` (1 << 18) | Milling Unit is NOT Inside_PLC2 | FA2010 | PLC 2 `M23` = 0 |
  | `524288L` (1 << 19) | Clamping Mould option is on | FA2010 | PLC 1 `L27` = 1 |

  A new one is registered in `<ID>Module.cs` (`RegisterConditions`: `MachineConditionMap.RegisterMachineBit(ctx.AllocateMachineBit("Tag"), ...)`),
  after the machine's others so their numbers stay; ask for it rather than inventing a number.
- **Help**: `ToolTipTextKey` / tips - a sentence for a user, translated like the captions.

## 5. The controls

### CylinderUnitHMI

A cylinder (any two-way movement) on a Manual Movement page: a frame with its title, two command buttons (A left, B right), the end sensors as lamps on the buttons, a position display, the "?" with its helpers and its alarms. Its ValveType and FeedbackType decide which sensors it needs and how the HMI watches its movements.
On our pages: 74 of them; size 360 x 64 (74).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Valve & Feedback** | | | |
| `ValveType` | CylinderValve | Unspecified | The valve: it decides the ACTIONS. SingleSolenoid - one coil, one action (A or B; the spring makes the other movement). DoubleSolenoid (5/2), 3WayClosedCenter / 3WayOpenCenter (5/3, double solenoid) - a coil for each movement, both actions. |
| `FeedbackType` | CylinderFeedback | Unspecified | The feedback: it decides the LAMPS and the POSITION. SingleMagnetic / SingleInductive - one lamp (A or B). TwoEndMagnetic / MagneticPlusPressureSwitch - a lamp on each action. AnalogueMagnetic - no lamp, the Position. |
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| `BaseOffset` | int | 0 | ROW INDEX added to every address in its own number system (M decimal, X/Y octal). |
| **Unit** | | | |
| `TitleTextKey` | string | "Function" | The frame's title: a translation key - usually the English text. |
| `TitleTipKey` | string | "" | The title's tooltip (a translation key, or the text). |
| **Action A** | | | |
| `CommandAAddress` | string | "" | Bit address of command A (M). Empty hides the button. |
| `CommandAAction` | BitActionType | Momentary | How button A writes its command bit: Momentary (while pressed), Alternate, SET, RESET. |
| `ButtonATextKey` | string | "◀" | Button A caption. Translation KEY with literal fallback. |
| `ButtonATipKey` | string | "" | Its tooltip (a translation key, or the text). |
| `Lamp1AAddress` | string | "" | Input sensor lamp 1 on button A (X). Empty hides it. |
| `Lamp1ATipKey` | string | "" | Its tooltip (a translation key, or the text). |
| `Lamp2AAddress` | string | "" | Input sensor lamp 2 on button A (X). Empty hides it. |
| `Lamp2ATipKey` | string | "" | Its tooltip (a translation key, or the text). |
| `RelayAAddress` | string | "" | Internal relay bit (M): shades the button slightly darker when ON. Empty = uses CommandAAddress. |
| `OutputAAddress` | string | "" | Physical output bit (Y): shades darker still and turns text white when ON. |
| `ErrorAAddress` | string | "" | Error bit set when the A (left button) movement fails. Its '?' shows on the top frame line, and button A's border blinks red. A single solenoid's spring-return movement can fail too: it may have an error with no command. |
| `HelpAFiles` | string | "" | Help files for the A movement (in the machine's Help folder): shown with the manual when ErrorA is set, and behind the unit's '?' at any time. |
| `ButtonAVisibilityConditions` | SystemCondition | None | EXTRA visibility conditions for button A, added on top of the unit's (inherited conditions always apply). |
| `ButtonAEnableConditions` | SystemCondition | None | EXTRA enable conditions for button A, added on top of the unit's (inherited conditions always apply). |
| `ButtonAEnableTrigger` | PlcTrigger |  | PLC signal that must be true before button A can be pressed. |
| **Action B** | | | |
| `CommandBAddress` | string | "" | Bit address of command B (M). Empty hides the button. |
| `CommandBAction` | BitActionType | Momentary | How button B writes its command bit: Momentary (while pressed), Alternate, SET, RESET. |
| `ButtonBTextKey` | string | "▶" | Button A caption. Translation KEY with literal fallback. |
| `ButtonBTipKey` | string | "" | Its tooltip (a translation key, or the text). |
| `Lamp1BAddress` | string | "" | Input sensor lamp 1 on button B (X). Empty hides it. |
| `Lamp1BTipKey` | string | "" | Its tooltip (a translation key, or the text). |
| `Lamp2BAddress` | string | "" | Input sensor lamp 2 on button B (X). Empty hides it. |
| `Lamp2BTipKey` | string | "" | Its tooltip (a translation key, or the text). |
| `RelayBAddress` | string | "" | Internal relay bit (M): shades the button slightly darker when ON. Empty = uses CommandAAddress. |
| `OutputBAddress` | string | "" | Physical output bit (Y): shades darker still and turns text white when ON. |
| `ErrorBAddress` | string | "" | Error bit set when the B (right button) movement fails. Its '?' shows on the top frame line, and button B's border blinks red. A single solenoid's spring-return movement can fail too: it may have an error with no command. |
| `HelpBFiles` | string | "" | Help files for the B movement (in the machine's Help folder): shown with the manual when ErrorB is set, and behind the unit's '?' at any time. |
| `ButtonBVisibilityConditions` | SystemCondition | None | EXTRA visibility conditions for button B, added on top of the unit's (inherited conditions always apply). |
| `ButtonBEnableConditions` | SystemCondition | None | EXTRA enable conditions for button B, added on top of the unit's (inherited conditions always apply). |
| `ButtonBEnableTrigger` | PlcTrigger |  | PLC signal that must be true before button B can be pressed. |
| **Position** | | | |
| `PositionStyle` | UnitPositionStyle | Standard | Standard: black bold digits on grey, like the Axis Settings rows. Readout: red digits on black. |
| `PositionAddress` | string | "" | Word address of the encoder value. Empty hides the readout. |
| `PositionDataType` | DataTypeEnum | Bin_16 | Bin_16 = one word, Bin_32 = a double word. |
| `PositionDivisor` | double | 1 |  |
| `PositionDecimalPlaces` | int | 0 | Decimals of the position. |
| `PositionDigits` | int | 5 | Digits of the position in all. |
| `PositionSuffix` | string | "" | The position's unit ("mm"). |
| `PositionUnitType` | UnitType | None | Unit family for the position readout. When set (e.g. Length), the value follows the unit chosen in Unit Settings and the suffix changes with it; PositionSuffix is then only used as the fallback when no unit is active. |
| `PositionOffset` | DynamicValue |  | Address offset for the position tag (Static / Tag / HmiVariable), same as NumericDisplayHMI.AddressOffset. |
| `PositionTipKey` | string | "Current position" | The position's tooltip (a translation key, or the text). |
| **Parts** | | | |
| `Arrows` | ButtonArrowStyle | LeftRight | Arrow pair used on the two buttons. Custom keeps whatever captions you type. |
| **Conditions** | | | |
| `VisibilityConditions` | SystemCondition | None | Shown only while these SystemCondition flags are met (None: always). |
| `EnableConditions` | SystemCondition | None | Usable only while these are met, greyed otherwise (None: always). |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | Operator | The lowest level allowed to use it: Operator (Manual Movement), Maintenance (Machine Parameters), Admin. |
| `ConfirmationNeeded` | bool | False | Asks "Are you sure?" before a SET / RESET / ALTERNATE or word write (never before a momentary jog). |
| `LogAction` | bool | True | true: every write it makes goes to the log with the user's name - set it on everything that writes. |

An example from Manual\ManualEntry_Conveyor.Designer.cs:

```csharp
this.unitProfile_Clamping_7 = new Kaban.UI.Controls.CylinderUnitHMI();
this.unitProfile_Clamping_7.BackColor = System.Drawing.Color.White;
this.unitProfile_Clamping_7.Name = "unitProfile_Clamping_7";
this.unitProfile_Clamping_7.PlcNumber = 1;
this.unitProfile_Clamping_7.TitleTextKey = "Profile Clamping 7";
this.unitProfile_Clamping_7.Location = new System.Drawing.Point(406, 164);
this.unitProfile_Clamping_7.Size = new System.Drawing.Size(360, 64);
this.unitProfile_Clamping_7.LogAction = true;
this.unitProfile_Clamping_7.CommandAAddress = "M696";
this.unitProfile_Clamping_7.CommandAAction = Kaban.Core.Enums.BitActionType.Momentary;
this.unitProfile_Clamping_7.Lamp1AAddress = "M213";
this.unitProfile_Clamping_7.CommandBAddress = "M689";
this.unitProfile_Clamping_7.CommandBAction = Kaban.Core.Enums.BitActionType.Momentary;
this.unitProfile_Clamping_7.Lamp1BAddress = "M220";
this.unitProfile_Clamping_7.PositionAddress = "D4";
this.unitProfile_Clamping_7.PositionDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
this.unitProfile_Clamping_7.PositionDivisor = 10D;
this.unitProfile_Clamping_7.PositionDecimalPlaces = 1;
this.unitProfile_Clamping_7.PositionDigits = 6;
this.unitProfile_Clamping_7.PositionSuffix = "mm";
this.unitProfile_Clamping_7.PositionUnitType = Kaban.Core.Enums.UnitType.Length;
this.unitProfile_Clamping_7.FeedbackType = Kaban.UI.Controls.CylinderFeedback.TwoEndMagnetic;
this.unitProfile_Clamping_7.ValveType = Kaban.UI.Controls.CylinderValve.DoubleSolenoid;
this.unitProfile_Clamping_7.ErrorAAddress = "M875";
this.unitProfile_Clamping_7.ErrorBAddress = "M874";
this.unitProfile_Clamping_7.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
```

### AxisUnitHMI

A servo axis on a Manual Movement page, looking like a CylinderUnitHMI: set only its AxisNumber - its jog bits, position, name, error bit and reference sensor are taken from the AxisSettingHMI row with the same HMIAxisIndex.
On our pages: 26 of them; size 360 x 64 (26).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| `AxisNumber` | int | 0 | The axis: the HMIAxisIndex of its AxisSettingHMI row on the Axis Parameters page. |

An example from Manual\ManualMilling.Designer.cs:

```csharp
this.unitUnit_1_Angle = new Kaban.UI.Controls.AxisUnitHMI();
this.unitUnit_1_Angle.BackColor = System.Drawing.Color.White;
this.unitUnit_1_Angle.Name = "unitUnit_1_Angle";
this.unitUnit_1_Angle.AxisNumber = 4;
this.unitUnit_1_Angle.PlcNumber = 1;
this.unitUnit_1_Angle.Location = new System.Drawing.Point(16, 328);
this.unitUnit_1_Angle.Size = new System.Drawing.Size(360, 64);
```

### AxisSettingHMI

One servo axis as one row of the Axis Parameters page: jog buttons, position, current, limits, reference, calibration, its codes (servo alarm, axis alarm, axis warning), and the simple motion module's values. One row per axis; the rows of a page line up as a table.
On our pages: 26 of them; size 1382 x 40 (17), 1064 x 40 (9).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| `HMIAxisIndex` | int | 0 | The axis's index on the HMI. An AxisUnitHMI with this number (its AxisNumber) mirrors this row. |
| `MotionNumber` | int | 0 | The simple motion module's number as GX Works3 numbers it (the n of Un\G; 1 for U1). 0: none. |
| `MotionAxisIndex` | int | 0 | This axis's number on the simple motion module: 1 - 8. 0: none. |
| **Axis** | | | |
| `AxisNameKey` | string | "X" | Translation key for the axis name. |
| `VerticalMovement` | bool | False | Vertical axis: the jog arrows become up/down. |
| `ReferenceSensorAddress` | string | "" | Reference sensor input. Lit on the jog button of the side below. |
| `ReferenceSensorSide` | AxisReferenceSide | Back | The side the reference sensor is on. |
| **Movement** | | | |
| `CommandBackAddress` | string | "" | Jog back / minus. |
| `CommandForwardAddress` | string | "" | Jog forward / plus. |
| `PositionAddress` | string | "" | Live axis position. |
| `PositionDataType` | DataTypeEnum | Bin_32 | Bin_16 = one word, Bin_32 = a double word. |
| `PositionDivisor` | double | 0 | Raw-to-display divisor of the position only. 0: the row's Divisor. |
| `LoadEncoderValueAddress` | string | "" | Load-side encoder value. Shown in the position's tooltip with its difference, in the axis's unit to its last count. |
| **Value** | | | |
| `SettingDataType` | DataTypeEnum | Bin_32 | Word width of the setting registers. |
| `Divisor` | double | 10000 | The raw PLC value is divided by it (100: shown in 0.01 steps). |
| `DecimalPlaces` | int | 2 | Decimals shown. |
| `Digits` | int | 8 | Digits shown in all (the box's width follows). |
| `Suffix` | string | "mm" | The unit written after the value ("mm", "s"). |
| `EntryMinimum` | DynamicValue |  | The lowest value that may be typed into the row's settings: a number (-500), or the device holding it (D318), read live. Empty: no bound but the field's digits. |
| `EntryMaximum` | DynamicValue |  | The highest value that may be typed into the row's settings: a number (6500), or the device holding it (D320), read live. Empty: no bound but the field's digits. |
| **Settings** | | | |
| `MinLimitAddress` | string | "" | Minimum travel limit. |
| `MaxLimitAddress` | string | "" | Maximum travel limit. |
| `ReferenceAddress` | string | "" | Reference (home) value. |
| `CalibrationAddress` | string | "" | Calibration value. |
| `TravelPerRevolutionAddress` | string | "" | Travel per motor revolution, in the axis's unit (its divisor, to the last count). Empty hides the field. |
| `BacklashAddress` | string | "" | Backlash, in the axis's unit (its divisor, to the last count). Empty hides the field. |
| **Commands** | | | |
| `CalibrateAddress` | string | "" | Applies the calibration value to the axis. |
| `GoToReferenceAddress` | string | "" | Reference activation selection (latched). |
| `HomeRequestAddress` | string | "" | The PLC's home (reference) request bit: while TRUE the Ref box's border blinks red. Read only. |
| **Meters** | | | |
| `CurrentAddress` | string | "" | Servo load / motor current. Empty hides the bar. |
| `CurrentDivisor` | double | 1 |  |
| `CurrentFullScale` | double | 100 | Reading that counts as 100% on the bar. |
| `ZDistanceAddress` | string | "" | Reference Z signal distance. Empty hides the bar. |
| `ZDistanceDivisor` | double | 1000 |  |
| `ZDistanceFullScale` | double | 10 | Distance that counts as 100% on the bar. |
| **Diagnostics** | | | |
| `ErrorAddress` | string | "" | Error bit set when this axis faults. |
| `ServoAlarmAddress` | string | "" | Servo amplifier alarm code word (hexadecimal). |
| `AxisAlarmAddress` | string | "" | Axis alarm code word (hexadecimal). |
| `AxisWarningAddress` | string | "" | Axis warning code word (hexadecimal). |
| **Conditions** | | | |
| `VisibilityConditions` | SystemCondition | None | Shown only while these SystemCondition flags are met (None: always). |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | Maintenance | The lowest level allowed to use it: Operator (Manual Movement), Maintenance (Machine Parameters), Admin. |

An example from Parameters\AxisSettings.Designer.cs:

```csharp
this.axV_Cutting_X = new Kaban.UI.Controls.AxisSettingHMI();
this.axV_Cutting_X.AxisName = "Axis_VCuttingX";
this.axV_Cutting_X.AxisNameKey = "Axis_VCuttingX";
this.axV_Cutting_X.HMIAxisIndex = 17;
this.axV_Cutting_X.BackColor = System.Drawing.Color.White;
this.axV_Cutting_X.CommandBackAddress = "M631";
this.axV_Cutting_X.CommandForwardAddress = "M630";
this.axV_Cutting_X.DecimalPlaces = 2;
this.axV_Cutting_X.Digits = 8;
this.axV_Cutting_X.Divisor = 10000D;
this.axV_Cutting_X.GoToReferenceAddress = "B16";
this.axV_Cutting_X.Location = new System.Drawing.Point(0, 738);
this.axV_Cutting_X.MaxLimitAddress = "D828";
this.axV_Cutting_X.MinLimitAddress = "D830";
this.axV_Cutting_X.Name = "axV_Cutting_X";
this.axV_Cutting_X.PlcNumber = 1;
this.axV_Cutting_X.PositionAddress = "D838";
this.axV_Cutting_X.PositionDataType = Kaban.Core.Enums.DataTypeEnum.Bin_32;
this.axV_Cutting_X.ReferenceSensorAddress = "M269";
this.axV_Cutting_X.ReferenceSensorSide = Kaban.UI.Controls.AxisReferenceSide.Forward;
this.axV_Cutting_X.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
this.axV_Cutting_X.SettingDataType = Kaban.Core.Enums.DataTypeEnum.Bin_32;
this.axV_Cutting_X.Size = new System.Drawing.Size(1382, 40);
this.axV_Cutting_X.Suffix = "mm";
this.axV_Cutting_X.TabIndex = 29;
this.axV_Cutting_X.VerticalMovement = false;
this.axV_Cutting_X.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
```

### PneumaticPositioningHMI

A pneumatic positioning - a cylinder with a position sensor (analogue or encoder) and a 5/3 valve that the PLC drives to a target, used on our machines as an axis - as ONE ROW, laid out like an AxisSettingHMI row: its name (TitleTextKey) at the left end, then Manual movement (CommandBackAddress / CommandForwardAddress and the position: a CylinderUnitHMI inside, so its ValveType, FeedbackType, error bits, help, alarms and coupling are a cylinder unit's), Target position (TargetPosition + PositioningStartAddress, a SET button), Calibration (CalibrationAddress + CalibrateAddress, a SET button), Positioning tolerance (PositioningToleranceAddress) and Direction ref (PositioningDirectionReferenceAddress - the old pages' 'Stopping Offset'). The rows of a page share one names' column (the widest name) and the headings over their columns: plain Labels tagged "PositioningHeading:Manual" (Target, Calibration, Tolerance, DirectionRef) above the first row - made at run time when the page has none. A row whose VisibilityConditions are not met is taken out and the rows under it move up. Rows 40 high, 44 apart.
On our pages: 16 of them; size 1078 x 40 (16).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Valve & Feedback** | | | |
| `ValveType` | CylinderValve | Unspecified | The valve: it decides the actions (a pneumatic positioning: 3WayClosedCenter, a 5/3 closed centre). On an error the '?' opens the cylinder manual with only this valve's part. |
| `FeedbackType` | CylinderFeedback | Unspecified | The feedback: it decides the lamps and the position (a pneumatic positioning: AnalogueMagnetic, or EncoderPlusReference). On an error the '?' opens the cylinder manual with only this feedback's part. |
| **Device** | | | |
| `PlcNumber` | int | 1 | The controller of the positioning (1 unless the machine has several). |
| **Unit** | | | |
| `TitleTextKey` | string | "" | The name at the left end of the row - a translation key, usually its English text. |
| `NameWidth` | int | 0 | The width of the names' column at 100 %: 0 fits the page's widest name (every row of the page uses the widest of theirs). |
| **Movement** | | | |
| `CommandBackAddress` | string | "" | Command bit of the BACK (or DOWN) movement - the left button, momentary. |
| `CommandForwardAddress` | string | "" | Command bit of the FORWARD (or UP) movement - the right button, momentary. |
| `VerticalMovement` | bool | False | A vertical movement: the buttons show down and up instead of back and forward. |
| `BackSensorAddress` | string | "" | The sensor at the back (down) end - a lamp on the left button. Empty for an analogue sensor. |
| `ForwardSensorAddress` | string | "" | The sensor at the forward (up) end - a lamp on the right button. Empty for an analogue sensor. |
| `ErrorBackAddress` | string | "" | Error bit set when the back (down) movement fails: the '?' turns red and the left button blinks. |
| `ErrorForwardAddress` | string | "" | Error bit set when the forward (up) movement fails: the '?' turns red and the right button blinks. |
| `HelpBackFiles` | string | "" | Help files of the back movement (in the machine's Help folder): shown with the manual when its error is set, and behind the '?' at any time. |
| `HelpForwardFiles` | string | "" | Help files of the forward movement (in the machine's Help folder): shown with the manual when its error is set, and behind the '?' at any time. |
| **Position** | | | |
| `PositionAddress` | string | "" | Word of the position read (the analogue sensor, the encoder). |
| `PositionDataType` | DataTypeEnum | Bin_16 | Bin_16 = one word, Bin_32 = a double word. |
| `PositionDivisor` | double | 1 | The position's raw value is divided by this (0.1 mm: 10). |
| `PositionDecimalPlaces` | int | 0 | Decimals of the position. |
| `PositionDigits` | int | 6 | Digits of the position in all. |
| `PositionSuffix` | string | "mm" | The position's unit ("mm"). |
| `PositionUnitType` | UnitType | Length | The unit family: the position follows the unit chosen in Unit Settings. |
| **Target position** | | | |
| `TargetPosition` | string | "" | Word of the target position - where the positioning goes. |
| `PositioningStartAddress` | string | "" | Bit that starts the positioning to the target (SET: the PLC resets it). |
| **Calibration** | | | |
| `CalibrationAddress` | string | "" | Word of the calibration position - what the position reads after a calibration. |
| `CalibrateAddress` | string | "" | Bit that calibrates: the position read now becomes the calibration position (SET: the PLC resets it). |
| **Positioning tolerance** | | | |
| `PositioningToleranceAddress` | string | "" | Word of the positioning tolerance - how far from the target still counts as there. |
| **Direction ref** | | | |
| `PositioningDirectionReferenceAddress` | string | "" | Word of the positioning's direction reference (the old pages' 'Stopping Offset'). |
| **Value** | | | |
| `SettingDataType` | DataTypeEnum | Bin_16 | The target, calibration, tolerance and direction ref words: one word (Bin_16) or two (Bin_32). |
| `Divisor` | double | 1 | Their raw values are divided by this (0.01 mm: 100). |
| `DecimalPlaces` | int | 0 | Decimals shown. |
| `Digits` | int | 6 | Digits shown in all (the box's width follows). |
| `Suffix` | string | "mm" | Their unit (not drawn: the headings say what they are, the position shows the unit). |
| `UnitType` | UnitType | Length | The unit family: the values follow the unit chosen in Unit Settings, as the position does. |
| **Conditions** | | | |
| `VisibilityConditions` | SystemCondition | None | The row is shown only while these are met (an option the machine may not have); the rows under it move up. |
| `EnableConditions` | SystemCondition | None | The buttons and the fields can be used only while these are met. |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | Operator | Who may move it and send it to a target. |
| `SettingsSecurity` | SecurityLevel | Maintenance | Who may change the calibration, calibrate, and change the tolerance and the direction ref. |
| `LogAction` | bool | True | Every press and every value written is in the log (who did what). |

An example from Manual\ManualClamping_Moulds.Designer.cs:

```csharp
this.ppLeft_Profile_Recognition_Front_Vertical = new Kaban.UI.Controls.PneumaticPositioningHMI();
this.ppLeft_Profile_Recognition_Front_Vertical.BackColor = System.Drawing.Color.White;
this.ppLeft_Profile_Recognition_Front_Vertical.Name = "ppLeft_Profile_Recognition_Front_Vertical";
this.ppLeft_Profile_Recognition_Front_Vertical.PlcNumber = 1;
this.ppLeft_Profile_Recognition_Front_Vertical.TitleTextKey = "Left Profile Recognition Front Vertical";
this.ppLeft_Profile_Recognition_Front_Vertical.Location = new System.Drawing.Point(16, 54);
this.ppLeft_Profile_Recognition_Front_Vertical.Size = new System.Drawing.Size(1078, 40);
this.ppLeft_Profile_Recognition_Front_Vertical.CommandBackAddress = "M541";
this.ppLeft_Profile_Recognition_Front_Vertical.CommandForwardAddress = "M540";
this.ppLeft_Profile_Recognition_Front_Vertical.VerticalMovement = true;
this.ppLeft_Profile_Recognition_Front_Vertical.PositionAddress = "D110";
this.ppLeft_Profile_Recognition_Front_Vertical.PositionDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
this.ppLeft_Profile_Recognition_Front_Vertical.PositionDivisor = 10D;
this.ppLeft_Profile_Recognition_Front_Vertical.PositionDecimalPlaces = 2;
this.ppLeft_Profile_Recognition_Front_Vertical.TargetPosition = "D150";
this.ppLeft_Profile_Recognition_Front_Vertical.PositioningStartAddress = "M50";
this.ppLeft_Profile_Recognition_Front_Vertical.CalibrationAddress = "D130";
this.ppLeft_Profile_Recognition_Front_Vertical.CalibrateAddress = "M30";
this.ppLeft_Profile_Recognition_Front_Vertical.PositioningToleranceAddress = "D750";
this.ppLeft_Profile_Recognition_Front_Vertical.PositioningDirectionReferenceAddress = "D790";
this.ppLeft_Profile_Recognition_Front_Vertical.Divisor = 100D;
this.ppLeft_Profile_Recognition_Front_Vertical.DecimalPlaces = 2;
this.ppLeft_Profile_Recognition_Front_Vertical.ErrorForwardAddress = "M916";
this.ppLeft_Profile_Recognition_Front_Vertical.ErrorBackAddress = "M915";
this.ppLeft_Profile_Recognition_Front_Vertical.ValveType = Kaban.UI.Controls.CylinderValve.ThreeWayClosedCenter;
this.ppLeft_Profile_Recognition_Front_Vertical.FeedbackType = Kaban.UI.Controls.CylinderFeedback.AnalogueMagnetic;
this.ppLeft_Profile_Recognition_Front_Vertical.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
```

### ButtonHMI

A push button writing bits (BitActions) and / or words (WordActions): momentary, alternate, SET or RESET; a lamp of its state or sensor on it (Lamp1Address). Its caption: TranslationKey + FallbackText (or ButtonText).
On our pages: 97 of them; size 360 x 40 (38), 300 x 36 (33), 79 x 32 (12).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| **Action** | | | |
| `BitActions` | List<BitAction> |  | The bits the button writes: one BitAction (Address, Action) each. |
| `WordActions` | List<WordAction> |  | The words the button writes (a value each): leave empty for a plain button. |
| **Display** | | | |
| `DescriptionText` | string | "" | Its caption, drawn beside it: a translation key - usually the English text, shown as written when it has no translation. |
| `ButtonText` | string | "" | The same as FallbackText. |
| `FallbackText` | string | "" | Its caption when the key has no translation (the English text). |
| `TranslationKey` | string | "" | The key of its text in the translation files - usually the English text itself. |
| `Direction` | Direction | None | Direction arrow drawn on the button. |
| `DisplayAddress` | string |  |  |
| **State** | | | |
| `Lamp1Address` | string | "" | Bit of input sensor lamp 1 (small square drawn on the button). Empty hides it. |
| `Lamp1Tip` | string | "" | Tooltip for lamp 1 (translation key or literal text). |
| `Lamp2Address` | string | "" | Bit of input sensor lamp 2. Empty hides it. |
| `Lamp2Tip` | string | "" | Tooltip for lamp 2 (translation key or literal text). |
| `LampDirection` | LampDirectionPosition | Left | Which corner of the button the two sensor lamps sit in. |
| `RelayAddress` | string | "" | Bit of the internal command relay (M). ON = button shades slightly darker. Empty falls back to DisplayAddress. |
| `OutputAddress` | string | "" | Bit of the physical output (Y). ON = button shades darker still and the text turns white. |
| `ErrorAddress` | string | "" | The alarm (error) bit of what this button drives: shown in its tooltip with the alarm's text. A cylinder or axis unit sets it on its buttons. |
| **Conditions** | | | |
| `VisibilityConditions` | SystemCondition | None | Shown only while these SystemCondition flags are met (None: always). |
| `EnableConditions` | SystemCondition | None | Usable only while these are met, greyed otherwise (None: always). |
| `EnableTrigger` | PlcTrigger |  | A PLC bit that must be ON before it can be used (leave empty). |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | None | The lowest level allowed to use it: Operator (Manual Movement), Maintenance (Machine Parameters), Admin. |
| `ConfirmationNeeded` | bool | False | Asks "Are you sure?" before a SET / RESET / ALTERNATE or word write (never before a momentary jog). |
| `LogAction` | bool | False | true: every write it makes goes to the log with the user's name - set it on everything that writes. |
| **Help** | | | |
| `ToolTipText` | string | "" | Its tooltip: a translation key, or the text. |

A `BitAction` (`Kaban.Core.Models.BitAction`): `Address` (a bit), `Action` (BitActionType). A `WordAction` writes a value to a word.

An example from Manual\ManualMilling.Designer.cs:

```csharp
this.btnUnit_1_Milling_Motor = new Kaban.UI.Controls.ButtonHMI();
this.btnUnit_1_Milling_Motor.Name = "btnUnit_1_Milling_Motor";
this.btnUnit_1_Milling_Motor.PlcNumber = 1;
this.btnUnit_1_Milling_Motor.TranslationKey = "Milling Motor";
this.btnUnit_1_Milling_Motor.FallbackText = "Milling Motor";
Kaban.Core.Models.BitAction bitAction7 = new Kaban.Core.Models.BitAction();   // declared at the top of InitializeComponent
bitAction7.Address = "M649";
bitAction7.Action = Kaban.Core.Enums.BitActionType.Alternate;
this.btnUnit_1_Milling_Motor.BitActions.Add(bitAction7);
this.btnUnit_1_Milling_Motor.Lamp1Address = "M230";
this.btnUnit_1_Milling_Motor.Location = new System.Drawing.Point(16, 624);
this.btnUnit_1_Milling_Motor.Size = new System.Drawing.Size(230, 40);
this.btnUnit_1_Milling_Motor.LogAction = true;
this.btnUnit_1_Milling_Motor.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
```

### LampHMI

A bit shown as a lamp (a sensor, a state, an output), its caption (DescriptionText) beside it.
On our pages: 108 of them; size 300 x 26 (96), 36 x 36 (12).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| `Address` | string |  | The PLC device it reads / writes ("D16", "M232"). |
| `AddressOffset` | DynamicValue |  | Leave empty: an offset added to the address at run time. |
| **Display** | | | |
| `DescriptionText` | string | "" | Its caption, drawn beside it: a translation key - usually the English text, shown as written when it has no translation. |
| `DescriptionSide` | DescriptionSide | Right | Which side of it the caption is drawn. |
| `OnColor` | Color | ff4caf50 |  |
| `OffColor` | Color | ffe1e1e1 |  |
| **Conditions** | | | |
| `VisibilityConditions` | SystemCondition | None | Shown only while these SystemCondition flags are met (None: always). |
| **Help** | | | |
| `ToolTipTextKey` | string | "" | Its tooltip: a translation key, or the text. |

An example from Manual\ManualExit.Designer.cs:

```csharp
this.lampSawdust_Conveyor_Thermic = new Kaban.UI.Controls.LampHMI();
this.lampSawdust_Conveyor_Thermic.Name = "lampSawdust_Conveyor_Thermic";
this.lampSawdust_Conveyor_Thermic.PlcNumber = 1;
this.lampSawdust_Conveyor_Thermic.Address = "M242";
this.lampSawdust_Conveyor_Thermic.DescriptionText = "Sawdust Conveyor Thermic";
this.lampSawdust_Conveyor_Thermic.Location = new System.Drawing.Point(16, 560);
this.lampSawdust_Conveyor_Thermic.Size = new System.Drawing.Size(36, 36);
this.lampSawdust_Conveyor_Thermic.AddressOffset = ((Kaban.Core.Models.DynamicValue)(resources.GetObject("lampSawdust_Conveyor_Thermic.AddressOffset")));
this.lampSawdust_Conveyor_Thermic.BackColor = System.Drawing.Color.Transparent;
this.lampSawdust_Conveyor_Thermic.CornerRadius = 4;
this.lampSawdust_Conveyor_Thermic.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Right;
this.lampSawdust_Conveyor_Thermic.ImageActive = null;
this.lampSawdust_Conveyor_Thermic.ImagePassive = null;
this.lampSawdust_Conveyor_Thermic.OffColor = System.Drawing.Color.FromArgb(((int)(((byte)(225)))), ((int)(((byte)(225)))), ((int)(((byte)(225)))));
this.lampSawdust_Conveyor_Thermic.OnColor = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
this.lampSawdust_Conveyor_Thermic.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
this.lampSawdust_Conveyor_Thermic.ToolTipTextKey = "";
this.lampSawdust_Conveyor_Thermic.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
```

### NumericInputHMI

A value typed in (a word or a double word): the keypad on a tap, limits, scaling, units, confirmation, conditions. Its caption (DescriptionText) is drawn to its left.
On our pages: 218 of them; size 440 x 32 (112), 116 x 26 (81), 104 x 26 (23).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| `Address` | string |  | The PLC device it reads / writes ("D16", "M232"). |
| `DataType` | DataTypeEnum | Bin_16 | Bin_16 = one word, Bin_32 = a double word (two words). |
| `AddressOffset` | DynamicValue |  | Leave empty: an offset added to the address at run time. |
| **Display** | | | |
| `DescriptionText` | string | "" | Its caption, drawn beside it: a translation key - usually the English text, shown as written when it has no translation. |
| `DescriptionColon` | bool | True | Appends " :" to the description automatically (see AttachedDescription). |
| `DataFormat` | DisplayFormatEnum | Signed_Decimal |  |
| `Divisor` | double | 1 | The raw PLC value is divided by it (100: shown in 0.01 steps). |
| `UnitType` | UnitType | None | The value's family (Length, Time ...): a user may see it in another unit; the PLC keeps its own. |
| `Digits` | int | 5 | Digits shown in all (the box's width follows). |
| `DecimalPlaces` | int | 0 | Decimals shown. |
| `FillZero` | bool | False |  |
| `Suffix` | string | "" | The unit written after the value ("mm", "s"). |
| `ShowSuffix` | bool | True |  |
| `BarView` | bool | False | Draw the field as a card: heading, minus, value, slider over MinLimit..MaxLimit, plus. |
| `BarHeader` | bool | True | Bar view: a heading strip with the DescriptionText and a gauge picture (the text leaves the left side). |
| `BarStep` | double | 5 | Bar view: how much one press of minus or plus moves the value, in the value's own unit. |
| **Input** | | | |
| `MinLimit` | DynamicValue |  | The lowest value accepted (a number, a PLC word, or none). |
| `MaxLimit` | DynamicValue |  | The highest value accepted (a number, a PLC word, or none). |
| `IsAngle` | bool | False | An angle in degrees: 0 - 360 always, and a MinLimit above the MaxLimit is a range through 0 (270 ... 360 / 0 ... 45). |
| `OutOfLimitsWarning` | bool | False |  |
| **Formula** | | | |
| `MathVariables` | List<MathVariable> |  | The names the formulas use. A is the value itself (listed first, fixed); add B, C ... - each a constant or a PLC device to read. New items are named with the next free letter. |
| `ReadFormula` | string | "val" | How the PLC's raw value becomes the number shown, e.g. '(A - 1) / B'. A (or val, x) is the raw value; B, C ... are MathVariables. Set, it replaces the Divisor for the value AND for Tag-based limits - give WriteFormula the inverse. |
| `WriteFormula` | string | "val" | How the number typed becomes the raw value written to the PLC, e.g. '(A * B) + 1'. A (or val, x) is the number typed (in the reference unit); B, C ... are MathVariables. Set, it replaces the Divisor: the result is written as it is. |
| `FormulaCheck` | string | "OK" | The formulas checked: every name defined, each formula readable, the two the inverse of each other. |
| **Conditions** | | | |
| `VisibilityConditions` | SystemCondition | None | Shown only while these SystemCondition flags are met (None: always). |
| `EnableConditions` | SystemCondition | None | Usable only while these are met, greyed otherwise (None: always). |
| `EnableTrigger` | PlcTrigger |  | A PLC bit that must be ON before it can be used (leave empty). |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | None | The lowest level allowed to use it: Operator (Manual Movement), Maintenance (Machine Parameters), Admin. |
| `ConfirmWrite` | bool | False | Asks before the value is written. |
| `LogAction` | bool | False | true: every write it makes goes to the log with the user's name - set it on everything that writes. |
| **Help** | | | |
| `ToolTipTextKey` | string | "" | Its tooltip: a translation key, or the text. |

An example from Parameters\ParamProcess.Designer.cs:

```csharp
this.numX_Top_Clamping_Clearing_Pos = new Kaban.UI.Controls.NumericInputHMI();
this.numX_Top_Clamping_Clearing_Pos.BackColor = System.Drawing.Color.White;
this.numX_Top_Clamping_Clearing_Pos.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
this.numX_Top_Clamping_Clearing_Pos.Name = "numX_Top_Clamping_Clearing_Pos";
this.numX_Top_Clamping_Clearing_Pos.PlcNumber = 1;
this.numX_Top_Clamping_Clearing_Pos.Address = "D534";
this.numX_Top_Clamping_Clearing_Pos.DataType = Kaban.Core.Enums.DataTypeEnum.Bin_32;
this.numX_Top_Clamping_Clearing_Pos.DescriptionText = "X Top Clamping Clearing Pos.";
this.numX_Top_Clamping_Clearing_Pos.AutoSize = false;
this.numX_Top_Clamping_Clearing_Pos.Digits = 5;
this.numX_Top_Clamping_Clearing_Pos.DecimalPlaces = 1;
this.numX_Top_Clamping_Clearing_Pos.Divisor = 10000D;
this.numX_Top_Clamping_Clearing_Pos.Suffix = "mm";
this.numX_Top_Clamping_Clearing_Pos.UnitType = Kaban.Core.Enums.UnitType.Length;
this.numX_Top_Clamping_Clearing_Pos.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
this.numX_Top_Clamping_Clearing_Pos.LogAction = true;
this.numX_Top_Clamping_Clearing_Pos.Location = new System.Drawing.Point(360, 50);
this.numX_Top_Clamping_Clearing_Pos.Size = new System.Drawing.Size(116, 26);
this.numX_Top_Clamping_Clearing_Pos.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
```

### NumericDisplayHMI

A value only shown (a word or a double word): scaled, in decimal or hexadecimal (an error code).
On our pages: 10 of them; size 110 x 40 (6), 170 x 36 (2), 120 x 40 (2).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| `Address` | string |  | The PLC device it reads / writes ("D16", "M232"). |
| `DataType` | DataTypeEnum | Bin_16 | Bin_16 = one word, Bin_32 = a double word (two words). |
| `AddressOffset` | DynamicValue |  | Leave empty: an offset added to the address at run time. |
| **Display** | | | |
| `DescriptionText` | string | "" | Its caption, drawn beside it: a translation key - usually the English text, shown as written when it has no translation. |
| `DescriptionColon` | bool | True | Appends " :" to the description automatically (see AttachedDescription). |
| `DataFormat` | DisplayFormatEnum | Signed_Decimal |  |
| `Divisor` | double | 1 | The raw PLC value is divided by it (100: shown in 0.01 steps). |
| `UnitType` | UnitType | None | The value's family (Length, Time ...): a user may see it in another unit; the PLC keeps its own. |
| `Digits` | int | 5 | Digits shown in all (the box's width follows). |
| `DecimalPlaces` | int | 0 | Decimals shown. |
| `FillZero` | bool | False |  |
| `Suffix` | string | "" | The unit written after the value ("mm", "s"). |
| `ShowSuffix` | bool | True |  |
| **Conditions** | | | |
| `VisibilityConditions` | SystemCondition | None | Shown only while these SystemCondition flags are met (None: always). |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | None | The lowest level allowed to use it: Operator (Manual Movement), Maintenance (Machine Parameters), Admin. |
| `LogAction` | bool | False | true: every write it makes goes to the log with the user's name - set it on everything that writes. |
| **Help** | | | |
| `ToolTipTextKey` | string | "" | Its tooltip: a translation key, or the text. |

An example from Manual\ManualExit.Designer.cs:

```csharp
this.dispConveyor_Inverter_Error = new Kaban.UI.Controls.NumericDisplayHMI();
this.dispConveyor_Inverter_Error.Name = "dispConveyor_Inverter_Error";
this.dispConveyor_Inverter_Error.PlcNumber = 1;
this.dispConveyor_Inverter_Error.Address = "D671";
this.dispConveyor_Inverter_Error.DataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
this.dispConveyor_Inverter_Error.DataFormat = Kaban.Core.Enums.DisplayFormatEnum.Hexadecimal;
this.dispConveyor_Inverter_Error.Digits = 4;
this.dispConveyor_Inverter_Error.DescriptionText = "Conveyor Inverter Error";
this.dispConveyor_Inverter_Error.Location = new System.Drawing.Point(266, 510);
this.dispConveyor_Inverter_Error.Size = new System.Drawing.Size(110, 40);
this.dispConveyor_Inverter_Error.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
```

### CheckBoxHMI

One bit as a tick box (an option on / off), written on a click.
On our pages: 59 of them; size 440 x 28 (43), 44 x 32 (16).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| `Address` | string | "" | The PLC device it reads / writes ("D16", "M232"). |
| `AlertAddress` | string | "" | A bit asking for this box: while it is TRUE the border blinks red (e.g. the PLC's home request on the axis row's Ref box). |
| **Display** | | | |
| `DescriptionText` | string | "" | Its caption, drawn beside it: a translation key - usually the English text, shown as written when it has no translation. |
| `DescriptionSide` | DescriptionSide | Right | Which side of it the caption is drawn. |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | Operator | The lowest level allowed to use it: Operator (Manual Movement), Maintenance (Machine Parameters), Admin. |
| `LogAction` | bool | True | true: every write it makes goes to the log with the user's name - set it on everything that writes. |
| **Help** | | | |
| `ToolTipCaption` | string |  | Text for the TOOLTIP only, never drawn on the control. |

An example from Parameters\ParamOther.Designer.cs:

```csharp
this.optMullion_Notching = new Kaban.UI.Controls.CheckBoxHMI();
this.optMullion_Notching.Name = "optMullion_Notching";
this.optMullion_Notching.PlcNumber = 1;
this.optMullion_Notching.Address = "L5";
this.optMullion_Notching.DescriptionText = "Mullion Notching";
this.optMullion_Notching.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
this.optMullion_Notching.Location = new System.Drawing.Point(707, 47);
this.optMullion_Notching.Size = new System.Drawing.Size(44, 32);
this.optMullion_Notching.LogAction = true;
this.optMullion_Notching.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
```

### CheckListHMI

Many option bits as rows of 'caption : [x]' in columns - one control for a whole list of options. Options: one per line, TranslationKey|Fallback|Address.

| Property | Type | Default | What it is |
|---|---|---|---|
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| `Options` | string | "" | One per line (see above). |
| **Display** | | | |
| `Columns` | int | 1 |  |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | Operator | The lowest level allowed to use it: Operator (Manual Movement), Maintenance (Machine Parameters), Admin. |
| `LogAction` | bool | True | true: every write it makes goes to the log with the user's name - set it on everything that writes. |

### ComboBoxHMI

A choice written to one word: Options one per line, TranslationKey|Fallback|Value; the PLC's value shown as the chosen one.

| Property | Type | Default | What it is |
|---|---|---|---|
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| `Address` | string | "" | The PLC device it reads / writes ("D16", "M232"). |
| `Options` | string | "" | One per line (see above). |
| **Display** | | | |
| `DescriptionText` | string | "" | Its caption, drawn beside it: a translation key - usually the English text, shown as written when it has no translation. |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | Operator | The lowest level allowed to use it: Operator (Manual Movement), Maintenance (Machine Parameters), Admin. |
| `LogAction` | bool | True | true: every write it makes goes to the log with the user's name - set it on everything that writes. |

### LabelHMI

A heading or a fixed text on a page, translated (TranslationKey).
On our pages: 22 of them; size 360 x 26 (6), 104 x 32 (4), 326 x 26 (3).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Display** | | | |
| `TranslationKey` | string | "" | The key of its text in the translation files - usually the English text itself. |

An example from Parameters\ParamProcess.Designer.cs:

```csharp
this.lblMilling = new Kaban.UI.Controls.LabelHMI();
this.lblMilling.Name = "lblMilling";
this.lblMilling.TranslationKey = "Milling";
this.lblMilling.Text = "Milling";
this.lblMilling.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
this.lblMilling.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(70)))), ((int)(((byte)(78)))));
this.lblMilling.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
this.lblMilling.Location = new System.Drawing.Point(16, 16);
this.lblMilling.Size = new System.Drawing.Size(460, 26);
```

### GroupBoxHMI

A frame like a CylinderUnitHMI's around controls that belong together (a row of small buttons); its title TitleTextKey. The controls inside are added to it.
On our pages: 4 of them; size 360 x 64 (4).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| **Display** | | | |
| `TitleTextKey` | string | "" | The frame's title: a translation key - usually the English text. |
| `Emoji` | string | "" | Optional emoji/symbol shown before the caption. When set, the caption becomes bold to stand out. |
| **Conditions** | | | |
| `VisibilityConditions` | SystemCondition | None | Shown only while these SystemCondition flags are met (None: always). |
| `VisibleAddress` | string | "" | Bit that must be ON for the group to be visible. Optional. |
| `EnableConditions` | SystemCondition | None | Usable only while these are met, greyed otherwise (None: always). |
| `EnableAddress` | string | "" | Bit that must be ON for the group to be enabled. Optional. |

An example from Manual\ManualMilling.Designer.cs:

```csharp
this.grpDrills_to_Changing_Pos = new Kaban.UI.Controls.GroupBoxHMI();
this.grpDrills_to_Changing_Pos.SuspendLayout();
this.grpDrills_to_Changing_Pos.Name = "grpDrills_to_Changing_Pos";
this.grpDrills_to_Changing_Pos.PlcNumber = 1;
this.grpDrills_to_Changing_Pos.TitleTextKey = "Drills to Changing Pos.";
this.grpDrills_to_Changing_Pos.Location = new System.Drawing.Point(406, 16);
this.grpDrills_to_Changing_Pos.Size = new System.Drawing.Size(360, 64);
this.grpDrills_to_Changing_Pos.Controls.Add(this.btnDrills_to_Changing_Pos_N1);
this.grpDrills_to_Changing_Pos.Controls.Add(this.btnDrills_to_Changing_Pos_N2);
this.grpDrills_to_Changing_Pos.Controls.Add(this.btnDrills_to_Changing_Pos_N3);
this.grpDrills_to_Changing_Pos.Controls.Add(this.btnDrills_to_Changing_Pos_N4);
this.grpDrills_to_Changing_Pos.ResumeLayout(false);
```

### MeterHMI

A gauge showing a value (a load, a pressure) with its bands - read only.

| Property | Type | Default | What it is |
|---|---|---|---|
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| `Address` | string | "" | The PLC device it reads / writes ("D16", "M232"). |
| **Display** | | | |
| `Caption` | string | "" |  |
| `ShowValue` | bool | True | Show the numeric value inside the bar. |
| `Divisor` | double | 1 | The raw PLC value is divided by it (100: shown in 0.01 steps). |
| `DecimalPlaces` | int | 0 | Decimals shown. |
| `Suffix` | string | "%" | The unit written after the value ("mm", "s"). |
| `ScaleMinimum` | double | 0 | Display value at the left end of the bar. |
| `ScaleMaximum` | double | 100 | Display value that fills the bar. |
| **Bands** | | | |
| `Bands` | BandShape | None | Which way the value is judged. |
| `LowAlarm` | double | 10 | Window/Falling: at or below this it is RED. |
| `LowWarning` | double | 20 | Window/Falling: at or below this it is AMBER. |
| `HighWarning` | double | 80 | Window/Rising: at or above this it is AMBER. |
| `HighAlarm` | double | 90 | Window/Rising: at or above this it is RED. |

### MeterInputHMI

A gauge whose value is set (with its arrows and limits) - it writes to the PLC.

| Property | Type | Default | What it is |
|---|---|---|---|
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| `Address` | string | "" | The PLC device it reads / writes ("D16", "M232"). |
| **Display** | | | |
| `Caption` | string | "" |  |
| `ShowValue` | bool | True | Show the numeric value inside the bar. |
| `Divisor` | double | 1 | The raw PLC value is divided by it (100: shown in 0.01 steps). |
| `DecimalPlaces` | int | 0 | Decimals shown. |
| `Suffix` | string | "%" | The unit written after the value ("mm", "s"). |
| `ScaleMinimum` | double | 0 | Display value at the left end of the bar. |
| `ScaleMaximum` | double | 100 | Display value that fills the bar. |
| **Input** | | | |
| `Minimum` | double | 0 | Lowest value the operator may set. |
| `Maximum` | double | 100 | Highest value the operator may set. |
| `Step` | double | 5 | How much one arrow press moves the value. |
| **Bands** | | | |
| `Bands` | BandShape | None | Which way the value is judged. |
| `LowAlarm` | double | 10 | Window/Falling: at or below this it is RED. |
| `LowWarning` | double | 20 | Window/Falling: at or below this it is AMBER. |
| `HighWarning` | double | 80 | Window/Rising: at or above this it is AMBER. |
| `HighAlarm` | double | 90 | Window/Rising: at or above this it is RED. |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | Operator | The lowest level allowed to use it: Operator (Manual Movement), Maintenance (Machine Parameters), Admin. |
| `LogAction` | bool | True | true: every write it makes goes to the log with the user's name - set it on everything that writes. |

## 6. The values of the enums

- **Kaban.UI.Controls.AxisReferenceSide**: `Back`, `Forward`
- **Kaban.UI.Controls.MeterBaseHMI.BandShape**: `None`, `Rising`, `Falling`, `Window`
- **Kaban.Core.Enums.BitActionType**: `Momentary`, `Alternate`, `SET`, `RESET`
- **Kaban.Core.Enums.ButtonArrowStyle**: `LeftRight`, `UpDown`, `DownUp`, `OpenClose`, `Custom`
- **Kaban.UI.Controls.CylinderFeedback**: `Unspecified`, `SingleMagnetic`, `TwoEndMagnetic`, `SingleInductive`, `AnalogueMagnetic`, `MagneticPlusPressureSwitch`, `EncoderPlusReference`, `NoSensor`
- **Kaban.UI.Controls.CylinderValve**: `Unspecified`, `DoubleSolenoid`, `SingleSolenoid`, `ThreeWayClosedCenter (shown as 3WayClosedCenter)`, `ThreeWayOpenCenter (shown as 3WayOpenCenter)`
- **Kaban.Core.Enums.DataTypeEnum**: `Bin_16`, `Bin_32`
- **Kaban.Core.Enums.DescriptionSide**: `Left`, `Right`
- **Kaban.UI.Controls.ButtonGlyphs.Direction**: `None`, `Left`, `Right`, `Up`, `Down`
- **Kaban.Core.Enums.DisplayFormatEnum**: `Signed_Decimal`, `Unsigned_Decimal`, `Hexadecimal`, `Octal`, `Binary`, `Real`
- **Kaban.UI.Controls.ButtonHMI.LampDirectionPosition**: `Left`, `Right`, `Top`, `Bottom`
- **Kaban.Core.Enums.SecurityLevel**: `None`, `Operator`, `Maintenance`, `Admin`
- **Kaban.Core.Enums.SystemCondition** (flags: combine with `|`): `None` - None (Always Active), `SafetyOK` - Safety Relays OK, `ManualMode` - Manual Mode Active, `AutoMode` - Auto Mode Active, `AdminUser` - Admin User Logged In, `MachineIsRunning` - Machine is Running, `RobotIsSafe` - Robot is at Safe Position, `MillingUnitIsBack` - Milling Unit is at Back
- **Kaban.UI.Controls.UnitPositionStyle**: `Readout`, `Standard`
- **Kaban.Core.Enums.UnitType**: `None`, `Length`, `Weight`, `Temperature`, `Pressure`, `Time`

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

`Kaban.Machines.FA2010\Screens\Manual\ManualRobot.cs`:

```csharp
// <auto-generated>
//   Generated by tools/generate_pages.py from FA2010_DeviceList.xls  (sheet: ManualMovement)
//   This is a normal WinForms DESIGNER file: open the page in Visual Studio and
//   every control appears on the design surface with its properties editable.
//   Re-running the generator overwrites it, so make lasting changes in the
//   workbook (or stop generating this page and keep it by hand).
// </auto-generated>

using System.Windows.Forms;

namespace Kaban.Machines.FA2010.Screens.Manual
{
    public partial class ManualRobot : UserControl
    {
        public ManualRobot()
        {
            InitializeComponent();
        }
    }
}
```

`Kaban.Machines.FA2010\Screens\Manual\ManualRobot.Designer.cs`:

```csharp
// <auto-generated>
//   Generated by tools/legacy_fa2010_pages.py from the page of the HMI the
//   machine runs today (tools/legacy/FA2010/ucRobot.Designer.cs), 8 October.
//   This is a normal WinForms DESIGNER file: open the page in Visual Studio
//   and every control is on the design surface with its properties editable.
//   Re-running the tool keeps what was added to a control (valve and feedback
//   types, error bits, conditions, help...) and sets its rows, addresses and
//   layout again from the specification in the tool.
// </auto-generated>

namespace Kaban.Machines.FA2010.Screens.Manual
{
    partial class ManualRobot
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            Kaban.Core.Models.BitAction bitAction1 = new Kaban.Core.Models.BitAction();
            this.unitAxis_X = new Kaban.UI.Controls.AxisUnitHMI();
            this.unitAxis_Y = new Kaban.UI.Controls.AxisUnitHMI();
            this.unitAxis_Z = new Kaban.UI.Controls.AxisUnitHMI();
            this.unitGripper = new Kaban.UI.Controls.CylinderUnitHMI();
            this.btnSawdust_Blowing = new Kaban.UI.Controls.ButtonHMI();
            this.lampBar_First_End_Read_SN = new Kaban.UI.Controls.LampHMI();
            this.lampBar_Last_End_Read_SN = new Kaban.UI.Controls.LampHMI();
            this.unitGripper_Angle = new Kaban.UI.Controls.CylinderUnitHMI();
            this.SuspendLayout();
            // 
            // unitAxis_X
            // 
            // Axis X: axis 1 of Machine Parameters - Axis Parameters (its buttons, position and reference sensor come from there).
            this.unitAxis_X.BackColor = System.Drawing.Color.White;
            this.unitAxis_X.Name = "unitAxis_X";
            this.unitAxis_X.AxisNumber = 1;
            this.unitAxis_X.PlcNumber = 1;
            this.unitAxis_X.Location = new System.Drawing.Point(16, 16);
            this.unitAxis_X.Size = new System.Drawing.Size(360, 64);
            // 
            // unitAxis_Y
            // 
            // Axis Y: axis 2 of Machine Parameters - Axis Parameters (its buttons, position and reference sensor come from there).
            this.unitAxis_Y.BackColor = System.Drawing.Color.White;
            this.unitAxis_Y.Name = "unitAxis_Y";
            this.unitAxis_Y.AxisNumber = 2;
            this.unitAxis_Y.PlcNumber = 1;
            this.unitAxis_Y.Location = new System.Drawing.Point(16, 90);
            this.unitAxis_Y.Size = new System.Drawing.Size(360, 64);
            // 
            // unitAxis_Z
            // 
            // Axis Z: axis 3 of Machine Parameters - Axis Parameters (its buttons, position and reference sensor come from there).
            this.unitAxis_Z.BackColor = System.Drawing.Color.White;
            this.unitAxis_Z.Name = "unitAxis_Z";
            this.unitAxis_Z.AxisNumber = 3;
            this.unitAxis_Z.PlcNumber = 1;
            this.unitAxis_Z.Location = new System.Drawing.Point(16, 164);
            this.unitAxis_Z.Size = new System.Drawing.Size(360, 64);
            // 
            // unitGripper
            // 
            this.unitGripper.BackColor = System.Drawing.Color.White;
            this.unitGripper.Name = "unitGripper";
            this.unitGripper.PlcNumber = 1;
            this.unitGripper.TitleTextKey = "Gripper";
            this.unitGripper.Location = new System.Drawing.Point(16, 238);
            this.unitGripper.Size = new System.Drawing.Size(360, 64);
            this.unitGripper.LogAction = true;
            this.unitGripper.CommandAAddress = "M642";
            this.unitGripper.CommandAAction = Kaban.Core.Enums.BitActionType.Momentary;
            this.unitGripper.Lamp1AAddress = "M222";
            this.unitGripper.CommandBAddress = "M641";
            this.unitGripper.CommandBAction = Kaban.Core.Enums.BitActionType.Momentary;
            this.unitGripper.PositionAddress = "D21";
            this.unitGripper.PositionDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.unitGripper.PositionDivisor = 10D;
            this.unitGripper.PositionDecimalPlaces = 2;
            this.unitGripper.PositionDigits = 6;
            this.unitGripper.PositionSuffix = "mm";
            this.unitGripper.PositionUnitType = Kaban.Core.Enums.UnitType.Length;
            this.unitGripper.FeedbackType = Kaban.UI.Controls.CylinderFeedback.EncoderPlusReference;
            this.unitGripper.ValveType = Kaban.UI.Controls.CylinderValve.DoubleSolenoid;
            this.unitGripper.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            // 
            // btnSawdust_Blowing
            // 
            this.btnSawdust_Blowing.Name = "btnSawdust_Blowing";
            this.btnSawdust_Blowing.PlcNumber = 1;
            this.btnSawdust_Blowing.TranslationKey = "Sawdust Blowing";
            this.btnSawdust_Blowing.FallbackText = "Sawdust Blowing";
            bitAction1.Address = "M697";
            bitAction1.Action = Kaban.Core.Enums.BitActionType.Momentary;
            this.btnSawdust_Blowing.BitActions.Add(bitAction1);
            this.btnSawdust_Blowing.Location = new System.Drawing.Point(16, 312);
            this.btnSawdust_Blowing.Size = new System.Drawing.Size(360, 40);
            this.btnSawdust_Blowing.LogAction = true;
            this.btnSawdust_Blowing.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            // 
            // lampBar_First_End_Read_SN
            // 
            this.lampBar_First_End_Read_SN.Name = "lampBar_First_End_Read_SN";
            this.lampBar_First_End_Read_SN.PlcNumber = 1;
            this.lampBar_First_End_Read_SN.Address = "M323";
            this.lampBar_First_End_Read_SN.DescriptionText = "Bar First End Read SN";
            this.lampBar_First_End_Read_SN.Location = new System.Drawing.Point(16, 362);
            this.lampBar_First_End_Read_SN.Size = new System.Drawing.Size(36, 36);
            // 
            // lampBar_Last_End_Read_SN
            // 
            this.lampBar_Last_End_Read_SN.Name = "lampBar_Last_End_Read_SN";
            this.lampBar_Last_End_Read_SN.PlcNumber = 1;
            this.lampBar_Last_End_Read_SN.Address = "M324";
            this.lampBar_Last_End_Read_SN.DescriptionText = "Bar Last End Read SN";
            this.lampBar_Last_End_Read_SN.Location = new System.Drawing.Point(16, 408);
            this.lampBar_Last_End_Read_SN.Size = new System.Drawing.Size(36, 36);
            // 
            // unitGripper_Angle
            // 
            this.unitGripper_Angle.BackColor = System.Drawing.Color.White;
            this.unitGripper_Angle.Name = "unitGripper_Angle";
            this.unitGripper_Angle.PlcNumber = 1;
            this.unitGripper_Angle.TitleTextKey = "Gripper Angle";
            this.unitGripper_Angle.Location = new System.Drawing.Point(16, 454);
            this.unitGripper_Angle.Size = new System.Drawing.Size(360, 64);
            this.unitGripper_Angle.LogAction = true;
            this.unitGripper_Angle.CommandAAddress = "M752";
            this.unitGripper_Angle.CommandAAction = Kaban.Core.Enums.BitActionType.Momentary;
            this.unitGripper_Angle.Lamp1AAddress = "M262";
            this.unitGripper_Angle.CommandBAddress = "M751";
            this.unitGripper_Angle.CommandBAction = Kaban.Core.Enums.BitActionType.Momentary;
            this.unitGripper_Angle.Lamp1BAddress = "M263";
            this.unitGripper_Angle.FeedbackType = Kaban.UI.Controls.CylinderFeedback.TwoEndMagnetic;
            this.unitGripper_Angle.ValveType = Kaban.UI.Controls.CylinderValve.DoubleSolenoid;
            this.unitGripper_Angle.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            // 
            // ManualRobot
            // 
            // AutoScaleMode.None on purpose: these pages are hosted INSIDE another
            // auto-scaling container (the tab host), which would scale them twice.
            // The layout is in absolute pixels anyway.
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.AutoScroll = true;
            this.BackColor = System.Drawing.Color.White;
            this.Controls.Add(this.unitAxis_X);
            this.Controls.Add(this.unitAxis_Y);
            this.Controls.Add(this.unitAxis_Z);
            this.Controls.Add(this.unitGripper);
            this.Controls.Add(this.btnSawdust_Blowing);
            this.Controls.Add(this.lampBar_First_End_Read_SN);
            this.Controls.Add(this.lampBar_Last_End_Read_SN);
            this.Controls.Add(this.unitGripper_Angle);
            this.Name = "ManualRobot";
            this.Size = new System.Drawing.Size(392, 536);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private Kaban.UI.Controls.AxisUnitHMI unitAxis_X;
        private Kaban.UI.Controls.AxisUnitHMI unitAxis_Y;
        private Kaban.UI.Controls.AxisUnitHMI unitAxis_Z;
        private Kaban.UI.Controls.CylinderUnitHMI unitGripper;
        private Kaban.UI.Controls.ButtonHMI btnSawdust_Blowing;
        private Kaban.UI.Controls.LampHMI lampBar_First_End_Read_SN;
        private Kaban.UI.Controls.LampHMI lampBar_Last_End_Read_SN;
        private Kaban.UI.Controls.CylinderUnitHMI unitGripper_Angle;
    }
}
```
