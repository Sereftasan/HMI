# Kaban HMI - page design guide for an AI

*Made from the program itself on 29 October 2026 by `tools/ai_guide/MakeAiPageGuide.cs` (the controls, their
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
  (FA2010, H500, H800, MD3020, SB3012, SB3015), whose pages are in `Screens\<Folder>\`.
- **A page is a `UserControl` in two files** (a partial class):
  - `<Page>.cs` - only the constructor calling `InitializeComponent()`;
  - `<Page>.Designer.cs` - every control: a private field, created in `InitializeComponent`, its properties set, added with
    `this.Controls.Add(...)` (or to its group box). It must open in the Visual Studio designer: no code but the designer's own.
- **The HMI reads every PLC address of the page on show itself** (a scanner, one batched read): a control needs only its
  addresses - no code behind, no timers, no events.
- **Where a page appears:** a machine's screens are tabs, made in `<ID>Module.cs`, for example
  `new Screens.GeneratedPage("Tab_ManualExit", "Exit", SecurityLevel.Operator, () => new Screens.Manual.ManualExit())`
  in `ManualPages` (Manual Movement, `Screens\Manual`, Operator) or `MachineSettings` (Machine Parameters,
  `Screens\Parameters`, Maintenance). The tab's caption is the translation `Tab_<Class>`. A machine's **Home** is
  `Screens\HomeScreen` (`Home` in `<ID>Module.cs`); a machine mounted under another (H500 under FA2010) has its own
  pages and translations, its `PlcNumber` 1 being its own PLC.
- **The machine's own bar** (a `MachineBarHMI`: FA2010's `Screens\Shared\AxisJogBar` - the axes' speed, JOG and steps) is
  designed once and shown by the program above the Manual Movement tabs (`ManualBar` in `<ID>Module.cs`) and on the Axis
  Monitor - **never put it, or any other UserControl of the machine's project, into a page's designer file**: Visual Studio's
  designer cannot load it there and saves the page without it. A page that shows it (Machine Parameters > Axis) has a plain
  `System.Windows.Forms.Panel` where it goes, and its constructor puts the bar in the panel's place - the one thing a page's
  constructor does besides `InitializeComponent()`.
- **The project file is the old style: every file is listed.** A page adds:

```xml
<Compile Include="Screens\Manual\ManualExit.cs">
  <SubType>UserControl</SubType>
</Compile>
<Compile Include="Screens\Manual\ManualExit.Designer.cs">
  <DependentUpon>ManualExit.cs</DependentUpon>
</Compile>
```

- **Translations:** `Kaban.Machines.<ID>\Translations.csv` (the machine's words - H500's in H500's own file) and
  `KabanHMI\CommonTranslations.csv` (shared ones), one row a key: `Key;EN;TR;RU;DE;IT;ES` - never a `;` inside a text.
  A caption's key is usually its English text (`DescriptionText`, `TitleTextKey`, `TranslationKey` = "Profile Clamping 6");
  a missing translation shows the English.
  - **Every new caption comes with its Turkish**, in Kaban's words - FA2010's and H500's files hold them: Sıkma
    (clamping), Referans, Kalibre Et, Pozisyon / Poz., Konveyör, Enkoder, Vidalama (screwing), Delme (drilling),
    Kurtarma (clearing), İleri / Geri, Yukarı / Aşağı. The other languages may stay empty (they show the English).
  - **A Turkish caption about as long as the English**: the page's places are sized for the English text
    ("Kalibrasyon Poz.", not "Kalibrasyon Pozisyonu"; "Vidalama 1-2 Güvenlik Mesafesi").
  - **A key another machine's file has already** (FA2010's "Conveyor") is copied with its whole row: under one panel
    the files are merged, and a blank cell of the later one would hide the other's text.
  - A page of our controls follows a language change by itself; nothing to do for it on the page.
  - An **alarm's** text is a row of the machine's `Translations.csv` too, `Alarm_<ID>.<TagCode>` (FA2010's
    `Alarm_FA2010.Robot_X_Error`); its `Alarms.json` entry keeps the device, code, severity, help and `"Log"` (where the PLC's
    log records it) - no texts.

## 2. Which control for which function

| On the machine / on the old page | Our control | How |
|---|---|---|
| A cylinder or any two-way movement: two buttons (back / forward, down / up, open / close, release / clamp), its end sensors, maybe a position | **CylinderUnitHMI** | One unit per cylinder. Button **A** (left) = back / down / open / release, **B** (right) = forward / up / close / clamp. `CommandAAddress` / `CommandBAddress` with `CommandAAction` / `CommandBAction`; the end sensors as `Lamp1AAddress` / `Lamp1BAddress` (and `Lamp2...`). **No position**: a cylinder whose position is read is a PneumaticPositioningHMI (an analogue sensor, sent to a target) or a CylinderEncoderUnitHMI (an encoder). `ValveType` and `FeedbackType` from the electrical and pneumatic drawings; the PLC's error bits as `ErrorAAddress` / `ErrorBAddress`. Size 360 x 64. |
| A servo axis on a Manual Movement page | **AxisUnitHMI** | Only `AxisNumber` (and `PlcNumber`): its buttons, position, name and reference sensor come from that axis' AxisSettingHMI row. Size 360 x 64. |
| A servo axis' settings: limits, reference, calibration, its codes | **AxisSettingHMI** | One row per axis on the Axis Parameters page; `HMIAxisIndex` is the axis number the AxisUnitHMI uses. |
| A cylinder with an encoder on a Manual Movement page (a clamp whose position is shown: the old page's unit with a position) | **CylinderEncoderUnitHMI** | Only `EncoderNumber` (and `PlcNumber`, `TitleTextKey` for the title on this page): everything else comes from its CylinderEncoderSettingHMI row. Size 360 x 64. |
| A cylinder with an encoder's settings: reference, tolerance, start (minimum) limit, unclamping interval | **CylinderEncoderSettingHMI** | One row per encoder on Machine Parameters > Encoders, numbered (`EncoderNumber`); its movement, sensors, position, valve, feedback and error bits are entered here, once. |
| A pneumatic positioning: a cylinder with a position sensor sent to a target (a mould, a stop) - on the old page a unit with Target / Send / Calibration / Calibrate / Tolerance / Stopping Offset beside it | **PneumaticPositioningHMI** | One row per positioning, the rows one under the other 44 px apart, the five heading Labels above them (section 3). `CommandBackAddress` / `CommandForwardAddress` (`VerticalMovement` for down / up), `PositionAddress` with its scaling, `TargetPosition` + `PositioningStartAddress`, `CalibrationAddress` + `CalibrateAddress`, `PositioningToleranceAddress`, `PositioningDirectionReferenceAddress` (the old "Stopping Offset"), their scaling in `Divisor` / `DecimalPlaces`; `ValveType` / `FeedbackType` and the error bits as a cylinder unit's. Never rebuild it from loose fields and buttons. |
| A motor driven by an inverter: run (or the two directions), its run lamp, its speed or frequency, its inverter's fault code | **InverterUnitHMI** | One control for the motor - never a unit, a lamp and a fault code display side by side. `CommandAAddress` (and `CommandBAddress` for two directions), `ButtonATextKey` ("Run"), `RunLampAddress`, `FaultCodeAddress` (the word the PLC copies the inverter's fault into - never shown as a number), `ErrorAddress` (the motor's alarm bit), the value as `PositionAddress` with its scaling. Size 360 x 64. |
| A push button: a function on / off, start, reset, a jog, one-way movement | **ButtonHMI** | `BitActions`: one `Kaban.Core.Models.BitAction` a bit - `Momentary` (on while pressed), `Alternate` (each press toggles), `SET` (on; the PLC switches it off), `RESET`. Its state or sensor as `Lamp1Address`. `TranslationKey` + `FallbackText` = its caption. A row of an old page with ONE button is a ButtonHMI, with TWO opposite buttons a CylinderUnitHMI. |
| A sensor, a state, an output shown | **LampHMI** | `Address` of the bit; its caption `DescriptionText`. Size 36 x 36, the caption drawn beside it. |
| A value typed in: a parameter, a speed, a target, a time | **NumericInputHMI** | `Address` (a D word), `DataType` (`Bin_16`, or `Bin_32` for a double word), `Divisor`, `DecimalPlaces`, `Suffix`, `UnitType`, `MinLimit` / `MaxLimit`; `DescriptionText` its caption (drawn to its left). |
| A value only shown: a position, a counter, a code | **NumericDisplayHMI** | As NumericInputHMI, without the input; `DataFormat = Hexadecimal` for an inverter's or a drive's error code. |
| An option bit on / off (machine options) | **CheckBoxHMI** | `Address`, `DescriptionText`, `DescriptionSide = Left` on parameter pages. Many options at once: **CheckListHMI**. |
| A choice of a few values written to one word | **ComboBoxHMI** | `Options`, one per line: `TranslationKey|Fallback|Value`. |
| A heading, a section's title on a page | **LabelHMI** | `TranslationKey` = `Text` = its English text; bold Segoe UI 10 (a column title: 8, grey). |
| A group of small buttons belonging together (0° 90° 180° 270°, Drills 1 - 4) | **GroupBoxHMI** with ButtonHMI inside | `TitleTextKey`; the buttons are added to the group (`this.grp.Controls.Add(...)`). Size 360 x 64. |
| A load, a pressure, a percentage as a gauge | **MeterHMI** (shown) / **MeterInputHMI** (set) | `Address`, `Divisor`, `ScaleMinimum` / `ScaleMaximum`, bands. |
| The machine's Start, Pause and Cancel on a Home page | **ButtonHMI** | Start: the old program's bit, `SET` (`Momentary` where the old page pulsed it), `Lamp1Address` the bit that says the machine works, `EnableTrigger` the "ready" bit where the old page waited for it. Pause: `Alternate`, lit by its own bit. Cancel: `SET` or `Momentary`, asked first only while the machine works - `ConfirmationAddress` (the working bit) with `ConfirmationTextKey` + `ConfirmationText`. Keys `Btn_HomeStart`, `Btn_HomePause`, `Btn_Cancel`. Size 130 x 56. |
| The machine's working speed in percent (the old page's speed slider) | **SpeedOverrideHMI** | `Address` the speed word (FA2010: D408); - and + step through `Steps`. Size 340 x 56. |
| A second value of a unit (a measurement, another position) on a Manual Movement page | **NumericDisplayHMI** | Under the unit on its own row, 150 x 40 at the unit's right edge, its caption (`DescriptionText`) to its left (H500's "X1 Second Value"). |
| A value typed in with its own button beside it (Calibration Position + Calibrate, Position To Go + GO!) | **NumericInputHMI** + **ButtonHMI** | One row the unit's width: the box (122 x 32, its caption to its left), the button 90 x 40 at the right edge (H500's Heads). |

Do **not** use stock WinForms controls (`Button`, `Label`, `TextBox`, `CheckBox` ...) for anything bound to the PLC.

## 3. Laying a page out

From our pages (FA2010's Manual Movement and Machine Parameters, made by `tools/legacy_fa2010_pages.py`, and H500's, by
`tools/legacy_h500_pages.py`):

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
- **The Encoders page** (CylinderEncoderSettingHMI): as the pneumatic positionings' - the rows at x 16 from y 54, 44 px apart, five
  heading Labels tagged `"EncoderHeading:Manual"` ("Manual movement"), `"...:Reference"`, `"...:Tolerance"` ("Measuring tolerance"),
  `"...:MinimumLimit"` ("Minimum limit"), `"...:Unclamping"` ("Unclamping interval"). Names `enc<Name>`.
  Both pages become a table when shown (as an axis page): what stands **above the first row** stays at the top while the rows
  scroll, the rows' names stay at the left while they scroll sideways - so put the headings (and nothing else) above the rows.
- **A Machine Parameters page:** a NumericInputHMI box **116 x 26** a row, **36 px** apart, its caption (DescriptionText)
  drawn to its left - the box placed so that the captions of a column fit; columns side by side with 40 px between;
  headings above a column; option tick boxes (CheckBoxHMI 44 x 32, `DescriptionSide = Left`) in the same rows.
- **A Home page** - only what the machine is run with, no pictures or tables: Start, Pause and Cancel 130 x 56 from x 16,
  y 24, 10 px apart, then the speed (SpeedOverrideHMI, 340 x 56) 30 px after Cancel; `this.Size` the last control's
  right and bottom plus 24.
- **Names:** `unit<Name>` (CylinderUnitHMI, AxisUnitHMI), `btn<Name>`, `lamp<Name>`, `num<Name>` (NumericInputHMI),
  `disp<Name>` (NumericDisplayHMI), `opt<Name>` (CheckBoxHMI), `grp<Name>`, `lbl<Name>`, `ax<Name>` (AxisSettingHMI);
  `<Name>` from its caption without spaces (`unitProfile_Clamping_6`). Every control has a `Name`.
- **Order in the file:** the fields created first, then each control's block (a `// name` comment header, its properties),
  then the page's own properties and `this.Controls.Add(...)` in reading order, then the field declarations.
- **A finger as well as a mouse** (HMI Settings > Features > Touch screen): our controls are a finger's size and the HMI
  turns a long press into the right-click, a tap on something greyed into its reason, a drag into scrolling - so a page of
  our controls needs nothing for it. Never make a function reachable only by a right-click, a double click or a key, and
  never put what a user must know only in a tooltip.

## 4. Addresses, values, security

- **`PlcNumber`** - the machine's own PLC number as its project sees it: **1** = its main PLC (2 = a second one). The HMI
  maps it to the panel's station (a sub-machine's 1 is its own PLC) - never put a station number of the panel here.
- **A device of the machine's OTHER PLC** in a property: the address, `@` and that PLC's number - `"X12@2"` is X12 of PLC 2
  on a control whose `PlcNumber` is 1 (FA2010's back horizontal moulds' reference sensors). Only where the device really is
  on the other PLC; never a dot for it (`D100.5` is bit 5 of D100).
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
  | `524288L` (1 << 19) | Clamping Mould Option | FA2010 | PLC 1 `L27` = 1 |
  | `1048576L` (1 << 20) | Mullion Notching Option | FA2010 | PLC 1 `L5` = 1 |
  | `2097152L` (1 << 21) | Conveyor With Clamps | FA2010 | PLC 1 `L27` = 0 |

  A new one is registered in `<ID>Module.cs` (`RegisterConditions`: `MachineConditionMap.RegisterMachineBit(ctx.AllocateMachineBit("Tag"), ...)`),
  after the machine's others so their numbers stay; ask for it rather than inventing a number.
- **Help**: `ToolTipTextKey` / tips - a sentence for a user, translated like the captions.

## 5. The controls

### CylinderUnitHMI

A cylinder (any two-way movement) on a Manual Movement page: a frame with its title, two command buttons (A left, B right), the end sensors as lamps on the buttons, the "?" with its helpers and its alarms - and no position (a cylinder whose position is read is a PneumaticPositioningHMI or a CylinderEncoderUnitHMI). Its ValveType and FeedbackType decide which sensors it needs and how the HMI watches its movements.
On our pages: 45 of them; size 360 x 64 (45).

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
| **Parts** | | | |
| `Arrows` | ButtonArrowStyle | LeftRight | Arrow pair used on the two buttons. Custom keeps whatever captions you type. |
| **Conditions** | | | |
| `VisibilityConditions` | SystemCondition | None | Shown only while these SystemCondition flags are met (None: always). |
| `EnableConditions` | SystemCondition | None | Usable only while these are met, greyed otherwise (None: always). |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | Operator | The lowest level allowed to use it: Operator (Manual Movement), Maintenance (Machine Parameters), Admin. |
| `ConfirmationNeeded` | bool | False | Asks "Are you sure?" before a SET / RESET / ALTERNATE or word write (never before a momentary jog). |
| `LogAction` | bool | True | true: every write it makes goes to the log with the user's name - set it on everything that writes. |

An example from Manual\ManualMilling.Designer.cs:

```csharp
this.unitRoller_Up_Down = new Kaban.UI.Controls.CylinderUnitHMI();
this.unitRoller_Up_Down.Arrows = Kaban.Core.Enums.ButtonArrowStyle.LeftRight;
this.unitRoller_Up_Down.BackColor = System.Drawing.Color.White;
this.unitRoller_Up_Down.BaseOffset = 0;
this.unitRoller_Up_Down.ButtonAEnableConditions = Kaban.Core.Enums.SystemCondition.None;
this.unitRoller_Up_Down.ButtonAEnableTrigger.DescriptionKey = "";
this.unitRoller_Up_Down.ButtonAEnableTrigger.MonitorAddress = "";
this.unitRoller_Up_Down.ButtonAEnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
this.unitRoller_Up_Down.ButtonAEnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
this.unitRoller_Up_Down.ButtonAEnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
this.unitRoller_Up_Down.ButtonAEnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
this.unitRoller_Up_Down.ButtonAEnableTrigger.ValA = 0D;
this.unitRoller_Up_Down.ButtonAEnableTrigger.ValB = 0D;
this.unitRoller_Up_Down.ButtonATextKey = "◀";
this.unitRoller_Up_Down.ButtonATipKey = "";
this.unitRoller_Up_Down.ButtonAVisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
this.unitRoller_Up_Down.ButtonBEnableConditions = Kaban.Core.Enums.SystemCondition.None;
this.unitRoller_Up_Down.ButtonBEnableTrigger.DescriptionKey = "";
this.unitRoller_Up_Down.ButtonBEnableTrigger.MonitorAddress = "";
this.unitRoller_Up_Down.ButtonBEnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
this.unitRoller_Up_Down.ButtonBEnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
this.unitRoller_Up_Down.ButtonBEnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
this.unitRoller_Up_Down.ButtonBEnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
this.unitRoller_Up_Down.ButtonBEnableTrigger.ValA = 0D;
this.unitRoller_Up_Down.ButtonBEnableTrigger.ValB = 0D;
this.unitRoller_Up_Down.ButtonBTextKey = "▶";
this.unitRoller_Up_Down.ButtonBTipKey = "";
this.unitRoller_Up_Down.ButtonBVisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
this.unitRoller_Up_Down.CommandAAction = Kaban.Core.Enums.BitActionType.Momentary;
this.unitRoller_Up_Down.CommandAAddress = "M705";
this.unitRoller_Up_Down.CommandBAction = Kaban.Core.Enums.BitActionType.Momentary;
this.unitRoller_Up_Down.CommandBAddress = "M704";
this.unitRoller_Up_Down.ConfirmationNeeded = false;
this.unitRoller_Up_Down.EnableConditions = Kaban.Core.Enums.SystemCondition.None;
this.unitRoller_Up_Down.FeedbackType = Kaban.UI.Controls.CylinderFeedback.NoSensor;
this.unitRoller_Up_Down.Lamp1AAddress = "";
this.unitRoller_Up_Down.Lamp1ATipKey = "";
this.unitRoller_Up_Down.Lamp1BAddress = "";
this.unitRoller_Up_Down.Lamp1BTipKey = "";
this.unitRoller_Up_Down.Lamp2AAddress = "";
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
this.unitUnit_1_Angle.AxisNumber = 4;
this.unitUnit_1_Angle.BackColor = System.Drawing.Color.White;
this.unitUnit_1_Angle.Location = new System.Drawing.Point(14, 343);
this.unitUnit_1_Angle.Name = "unitUnit_1_Angle";
this.unitUnit_1_Angle.Size = new System.Drawing.Size(360, 64);
this.unitUnit_1_Angle.TabIndex = 7;
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
| `PositionAddress` | string | "" | Live axis position. Divided by the row's Divisor, as its limits, reference and calibration. |
| `PositionDataType` | DataTypeEnum | Bin_32 | Bin_16 = one word, Bin_32 = a double word. |
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
| **Load Side Encoder** | | | |
| `LoadSideEncoderAddress` | string | "" | The load-side encoder's value (read by the PLC itself, in its own count): the Load Position column, chosen in the Position heading's list. |
| `LoadSideEncoderDivisor` | double | 10000 | Raw-to-display divisor of the load-side encoder's value. Not set: the row's Divisor (the usual case - the PLC scales the encoder to the axis's count). Reset or 0: back to the Divisor. |
| **Commands** | | | |
| `CalibrateAddress` | string | "" | Applies the calibration value to the axis. |
| `GoToReferenceAddress` | string | "" | Reference activation selection (latched). |
| `HomeRequestAddress` | string | "" | The PLC's home (reference) request bit: while TRUE the Ref box's border blinks red. Read only. |
| **Meters** | | | |
| `CurrentAddress` | string | "" | Servo load / motor current. Empty hides the bar. |
| `CurrentDivisor` | double | 10 | The reading over this is the percentage: the motion module's motor current (Md.104) is in 0.1 % - 10 (212 is 21.2 %). |
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
| `EnableConditions` | SystemCondition | None | Usable only while these are met, greyed otherwise (None: always). |
| `BackEnableConditions` | SystemCondition | None | EXTRA conditions of the back (down) jog, added to the row's. |
| `BackEnableTrigger` | PlcTrigger |  | A PLC signal that must be true before the back (down) jog can be pressed. |
| `ForwardEnableConditions` | SystemCondition | None | EXTRA conditions of the forward (up) jog, added to the row's. |
| `ForwardEnableTrigger` | PlcTrigger |  | A PLC signal that must be true before the forward (up) jog can be pressed. |
| `CalibrateEnableConditions` | SystemCondition | None | EXTRA conditions of the Calibrate button, added to the row's. |
| `CalibrateEnableTrigger` | PlcTrigger |  | A PLC signal that must be true before Calibrate can be pressed. |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | Maintenance | The lowest level allowed to use it: Operator (Manual Movement), Maintenance (Machine Parameters), Admin. |

An example from Parameters\AxisSettings.Designer.cs:

```csharp
this.axRobot_X = new Kaban.UI.Controls.AxisSettingHMI();
this.axRobot_X.AxisAlarmAddress = "W4";
this.axRobot_X.AxisName = "Axis_RobotX";
this.axRobot_X.AxisNameKey = "Axis_RobotX";
this.axRobot_X.AxisWarningAddress = "W5";
this.axRobot_X.BackColor = System.Drawing.Color.White;
this.axRobot_X.BackEnableConditions = Kaban.Core.Enums.SystemCondition.None;
this.axRobot_X.BackEnableTrigger.DescriptionKey = "";
this.axRobot_X.BackEnableTrigger.MonitorAddress = "";
this.axRobot_X.BackEnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
this.axRobot_X.BackEnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
this.axRobot_X.BackEnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
this.axRobot_X.BackEnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
this.axRobot_X.BackEnableTrigger.ValA = 0D;
this.axRobot_X.BackEnableTrigger.ValB = 0D;
this.axRobot_X.CalibrateAddress = "M140";
this.axRobot_X.CalibrateEnableConditions = Kaban.Core.Enums.SystemCondition.None;
this.axRobot_X.CalibrateEnableTrigger.DescriptionKey = "";
this.axRobot_X.CalibrateEnableTrigger.MonitorAddress = "";
this.axRobot_X.CalibrateEnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
this.axRobot_X.CalibrateEnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
this.axRobot_X.CalibrateEnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
this.axRobot_X.CalibrateEnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
this.axRobot_X.CalibrateEnableTrigger.ValA = 0D;
this.axRobot_X.CalibrateEnableTrigger.ValB = 0D;
this.axRobot_X.CalibrationAddress = "D580";
this.axRobot_X.CommandBackAddress = "M604";
this.axRobot_X.CommandForwardAddress = "M605";
this.axRobot_X.CurrentAddress = "W0A";
this.axRobot_X.CurrentDivisor = 10D;
this.axRobot_X.CurrentFullScale = 100D;
this.axRobot_X.DecimalPlaces = 2;
this.axRobot_X.Digits = 8;
this.axRobot_X.Divisor = 10000D;
this.axRobot_X.EnableConditions = Kaban.Core.Enums.SystemCondition.None;
this.axRobot_X.EntryMaximum.StaticValue = 0D;
this.axRobot_X.EntryMaximum.TagAddress = "";
this.axRobot_X.EntryMaximum.Type = Kaban.Core.Enums.LimitSourceType.None;
this.axRobot_X.EntryMinimum.StaticValue = 0D;
this.axRobot_X.EntryMinimum.TagAddress = "";
```

### PneumaticPositioningHMI

A pneumatic positioning - a cylinder with a position sensor (analogue or encoder) and a 5/3 valve that the PLC drives to a target, used on our machines as an axis - as ONE ROW, laid out like an AxisSettingHMI row: its name (TitleTextKey) at the left end, then Manual movement (CommandBackAddress / CommandForwardAddress and the position: a CylinderUnitHMI inside, so its ValveType, FeedbackType, error bits, help, alarms and coupling are a cylinder unit's), Target position (TargetPosition + PositioningStartAddress, a SET button), Calibration (CalibrationAddress + CalibrateAddress, a SET button), Positioning tolerance (PositioningToleranceAddress) and Direction ref (PositioningDirectionReferenceAddress - the old pages' 'Stopping Offset': the PLC compares the target with it to choose the direction, a target above it approached the way the position increases, one below it the other way). The rows of a page share one names' column (the widest name) and the headings over their columns: plain Labels tagged "PositioningHeading:Manual" (Target, Calibration, Tolerance, DirectionRef) above the first row - made at run time when the page has none. A row whose VisibilityConditions are not met is taken out and the rows under it move up. Rows 40 high, 44 apart.
On our pages: 16 of them; size 1101 x 40 (16).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Valve & Feedback** | | | |
| `ValveType` | CylinderValve | Unspecified | The valve: it decides the actions (a pneumatic positioning: 3WayClosedCenter, a 5/3 closed centre). On an error the '?' opens the cylinder manual with only this valve's part. |
| `FeedbackType` | CylinderFeedback | Unspecified | The feedback: it decides the lamps and the position (AnalogueMagnetic, EncoderPlusReference ...). On an error the '?' opens the cylinder manual with only this feedback's part. |
| **Device** | | | |
| `PlcNumber` | int | 1 | The controller of the cylinder (1 unless the machine has several). |
| **Unit** | | | |
| `TitleTextKey` | string | "" | The name at the left end of the row - a translation key, usually its English text. |
| `NameWidth` | int | 0 | The width of the names' column at 100 %: 0 fits the page's widest name (every row of the page uses the widest of theirs). |
| **Movement** | | | |
| `CommandBackAddress` | string | "" | Command bit of the BACK (or DOWN, or RELEASE) movement - the left button, momentary. |
| `CommandForwardAddress` | string | "" | Command bit of the FORWARD (or UP, or CLAMP) movement - the right button, momentary. |
| `VerticalMovement` | bool | False | A vertical movement: the buttons show down and up instead of back and forward. |
| `BackSensorAddress` | string | "" | The sensor at the back (down) end, or the reference sensor - a lamp on the left button. |
| `ForwardSensorAddress` | string | "" | The sensor at the forward (up) end, or the pressure switch - a lamp on the right button. |
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
| `PositioningDirectionReferenceAddress` | string | "" | Word of the direction reference: the PLC compares the target with it to choose the direction - a target above it is approached the way the position increases, a target below it the other way (the old pages' 'Stopping Offset'). |
| **Value** | | | |
| `SettingDataType` | DataTypeEnum | Bin_16 | The sections' words: one word (Bin_16) or two (Bin_32). |
| `Divisor` | double | 1 | The sections' raw values are divided by this (0.1 mm: 10, 0.01 mm: 100). |
| `DecimalPlaces` | int | 0 | Decimals shown. |
| `Digits` | int | 6 | Digits shown in all (the box's width follows). |
| `Suffix` | string | "mm" | Their unit (not drawn: the headings say what they are, the position shows the unit). |
| `UnitType` | UnitType | Length | The unit family: the values follow the unit chosen in Unit Settings, as the position does. |
| **Conditions** | | | |
| `VisibilityConditions` | SystemCondition | None | The row is shown only while these are met (an option the machine may not have); the rows under it move up. |
| `EnableConditions` | SystemCondition | None | The buttons and the fields can be used only while these are met. |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | Operator | Who may move it (and send it to a target). |
| `SettingsSecurity` | SecurityLevel | Maintenance | Who may change its settings (and calibrate it). |
| `LogAction` | bool | True | Every press and every value written is in the log (who did what). |

An example from Manual\ManualClamping_Moulds.Designer.cs:

```csharp
this.ppLeft_Profile_Recognition_Front_Vertical = new Kaban.UI.Controls.PneumaticPositioningHMI();
this.ppLeft_Profile_Recognition_Front_Vertical.BackColor = System.Drawing.Color.White;
this.ppLeft_Profile_Recognition_Front_Vertical.CalibrateAddress = "M30";
this.ppLeft_Profile_Recognition_Front_Vertical.CalibrationAddress = "D130";
this.ppLeft_Profile_Recognition_Front_Vertical.CommandBackAddress = "M541";
this.ppLeft_Profile_Recognition_Front_Vertical.CommandForwardAddress = "M540";
this.ppLeft_Profile_Recognition_Front_Vertical.DecimalPlaces = 2;
this.ppLeft_Profile_Recognition_Front_Vertical.Divisor = 100D;
this.ppLeft_Profile_Recognition_Front_Vertical.ErrorBackAddress = "M915";
this.ppLeft_Profile_Recognition_Front_Vertical.ErrorForwardAddress = "M916";
this.ppLeft_Profile_Recognition_Front_Vertical.FeedbackType = Kaban.UI.Controls.CylinderFeedback.AnalogueMagnetic;
this.ppLeft_Profile_Recognition_Front_Vertical.Location = new System.Drawing.Point(16, 54);
this.ppLeft_Profile_Recognition_Front_Vertical.Name = "ppLeft_Profile_Recognition_Front_Vertical";
this.ppLeft_Profile_Recognition_Front_Vertical.PositionAddress = "D110";
this.ppLeft_Profile_Recognition_Front_Vertical.PositionDecimalPlaces = 2;
this.ppLeft_Profile_Recognition_Front_Vertical.PositionDivisor = 100D;
this.ppLeft_Profile_Recognition_Front_Vertical.PositioningDirectionReferenceAddress = "D790";
this.ppLeft_Profile_Recognition_Front_Vertical.PositioningStartAddress = "M50";
this.ppLeft_Profile_Recognition_Front_Vertical.PositioningToleranceAddress = "D750";
this.ppLeft_Profile_Recognition_Front_Vertical.Size = new System.Drawing.Size(1101, 40);
this.ppLeft_Profile_Recognition_Front_Vertical.TabIndex = 5;
this.ppLeft_Profile_Recognition_Front_Vertical.TargetPosition = "D150";
this.ppLeft_Profile_Recognition_Front_Vertical.TitleTextKey = "Left Profile Recognition Front Vertical";
this.ppLeft_Profile_Recognition_Front_Vertical.ValveType = Kaban.UI.Controls.CylinderValve.ThreeWayClosedCenter;
this.ppLeft_Profile_Recognition_Front_Vertical.VerticalMovement = true;
```

### CylinderEncoderUnitHMI

A cylinder with an encoder (a clamp or a stop whose position an encoder counts, its reference sensor setting the count) on a Manual Movement page, looking like a CylinderUnitHMI: set only its EncoderNumber (and PlcNumber) - its buttons, sensors, position and scaling, valve, feedback, error bits and helpers are taken from the CylinderEncoderSettingHMI row with the same EncoderNumber. Its own: TitleTextKey (the title on this page; empty: the row's name) and the conditions it is shown and used under on this page.
On our pages: 11 of them; size 360 x 64 (11).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Encoder** | | | |
| `EncoderNumber` | int | 0 | The encoder's number: the EncoderNumber of its CylinderEncoderSettingHMI row on Machine Parameters > Encoders. Everything else is taken from that row. |
| `PlcNumber` | int | 1 | Controller of the encoder (1 unless the machine has several). |
| **Unit** | | | |
| `TitleTextKey` | string | "" | The title on this page - a translation key, usually its English text. Empty: the name of its encoder's row. |
| **Conditions** | | | |
| `VisibilityConditions` | SystemCondition | None | Its buttons and position are shown only while these are met (on this page). |
| `EnableConditions` | SystemCondition | None | Its buttons can be used only while these are met (on this page). |

An example from Manual\ManualExit.Designer.cs:

```csharp
this.unitRobot_Top_Clamping = new Kaban.UI.Controls.CylinderEncoderUnitHMI();
this.unitRobot_Top_Clamping.BackColor = System.Drawing.Color.White;
this.unitRobot_Top_Clamping.EnableConditions = Kaban.Core.Enums.SystemCondition.ManualMode;
this.unitRobot_Top_Clamping.EncoderNumber = 8;
this.unitRobot_Top_Clamping.Location = new System.Drawing.Point(14, 13);
this.unitRobot_Top_Clamping.Name = "unitRobot_Top_Clamping";
this.unitRobot_Top_Clamping.Size = new System.Drawing.Size(360, 64);
this.unitRobot_Top_Clamping.TabIndex = 0;
this.unitRobot_Top_Clamping.TitleTextKey = "Robot Top Clamping";
this.unitRobot_Top_Clamping.VisibilityConditions = Kaban.Core.Enums.SystemCondition.ManualMode;
```

### CylinderEncoderSettingHMI

A cylinder with an encoder as ONE ROW of Machine Parameters > Encoders, laid out like an AxisSettingHMI row (and like PneumaticPositioningHMI, the same base): its name, Manual movement (CommandBackAddress / CommandForwardAddress, the sensors as BackSensorAddress / ForwardSensorAddress, the position; ValveType, FeedbackType, the error bits and help as a cylinder unit's), Reference (ReferenceAddress), Measuring tolerance (MeasuringToleranceAddress), Minimum limit (MinimumLimitAddress - the old pages' 'Start Limit'), Unclamping interval (UnclampingIntervalAddress). EncoderNumber is the number a CylinderEncoderUnitHMI names: the cylinder is described once, here. Headings: plain Labels tagged "EncoderHeading:Manual" (Reference, Tolerance, MinimumLimit, Unclamping). Rows 40 high, 44 apart.
On our pages: 11 of them; size 926 x 40 (11).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Valve & Feedback** | | | |
| `ValveType` | CylinderValve | Unspecified | The valve: it decides the actions (a pneumatic positioning: 3WayClosedCenter, a 5/3 closed centre). On an error the '?' opens the cylinder manual with only this valve's part. |
| `FeedbackType` | CylinderFeedback | Unspecified | The feedback: it decides the lamps and the position (AnalogueMagnetic, EncoderPlusReference ...). On an error the '?' opens the cylinder manual with only this feedback's part. |
| **Device** | | | |
| `PlcNumber` | int | 1 | The controller of the cylinder (1 unless the machine has several). |
| `EncoderNumber` | int | 0 | The encoder's number on the HMI. A CylinderEncoderUnitHMI with this number (its EncoderNumber) on a Manual Movement page shows this cylinder. |
| **Unit** | | | |
| `TitleTextKey` | string | "" | The name at the left end of the row - a translation key, usually its English text. |
| `NameWidth` | int | 0 | The width of the names' column at 100 %: 0 fits the page's widest name (every row of the page uses the widest of theirs). |
| **Movement** | | | |
| `CommandBackAddress` | string | "" | Command bit of the BACK (or DOWN, or RELEASE) movement - the left button, momentary. |
| `CommandForwardAddress` | string | "" | Command bit of the FORWARD (or UP, or CLAMP) movement - the right button, momentary. |
| `VerticalMovement` | bool | False | A vertical movement: the buttons show down and up instead of back and forward. |
| `BackSensorAddress` | string | "" | The sensor at the back (down) end, or the reference sensor - a lamp on the left button. |
| `ForwardSensorAddress` | string | "" | The sensor at the forward (up) end, or the pressure switch - a lamp on the right button. |
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
| **Reference** | | | |
| `ReferenceAddress` | string | "" | Word of the reference - the position the encoder's count takes at its reference sensor. |
| **Measuring tolerance** | | | |
| `MeasuringToleranceAddress` | string | "" | Word of the measuring tolerance (the old pages' 'Tolerance'). |
| **Minimum limit** | | | |
| `MinimumLimitAddress` | string | "" | Word of the minimum limit (the old pages' 'Start Limit'). |
| **Unclamping interval** | | | |
| `UnclampingIntervalAddress` | string | "" | Word of the unclamping interval. |
| **Value** | | | |
| `SettingDataType` | DataTypeEnum | Bin_16 | The sections' words: one word (Bin_16) or two (Bin_32). |
| `Divisor` | double | 1 | The sections' raw values are divided by this (0.1 mm: 10, 0.01 mm: 100). |
| `DecimalPlaces` | int | 0 | Decimals shown. |
| `Digits` | int | 6 | Digits shown in all (the box's width follows). |
| `Suffix` | string | "mm" | Their unit (not drawn: the headings say what they are, the position shows the unit). |
| `UnitType` | UnitType | Length | The unit family: the values follow the unit chosen in Unit Settings, as the position does. |
| **Conditions** | | | |
| `VisibilityConditions` | SystemCondition | None | The row is shown only while these are met (an option the machine may not have); the rows under it move up. |
| `EnableConditions` | SystemCondition | None | The buttons and the fields can be used only while these are met. |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | Operator | Who may move it (and send it to a target). |
| `SettingsSecurity` | SecurityLevel | Maintenance | Who may change its settings (and calibrate it). |
| `LogAction` | bool | True | Every press and every value written is in the log (who did what). |

An example from Parameters\ParamEncoders.Designer.cs:

```csharp
this.encProfile_Recognition_Front = new Kaban.UI.Controls.CylinderEncoderSettingHMI();
this.encProfile_Recognition_Front.BackColor = System.Drawing.Color.White;
this.encProfile_Recognition_Front.BackSensorAddress = "M225";
this.encProfile_Recognition_Front.CommandBackAddress = "M646";
this.encProfile_Recognition_Front.CommandForwardAddress = "M645";
this.encProfile_Recognition_Front.DecimalPlaces = 1;
this.encProfile_Recognition_Front.Divisor = 10D;
this.encProfile_Recognition_Front.EncoderNumber = 2;
this.encProfile_Recognition_Front.ErrorBackAddress = "M845";
this.encProfile_Recognition_Front.ErrorForwardAddress = "M844";
this.encProfile_Recognition_Front.FeedbackType = Kaban.UI.Controls.CylinderFeedback.MagneticPlusPressureSwitch;
this.encProfile_Recognition_Front.ForwardSensorAddress = "M240";
this.encProfile_Recognition_Front.Location = new System.Drawing.Point(16, 98);
this.encProfile_Recognition_Front.MeasuringToleranceAddress = "D479";
this.encProfile_Recognition_Front.MinimumLimitAddress = "D483";
this.encProfile_Recognition_Front.Name = "encProfile_Recognition_Front";
this.encProfile_Recognition_Front.PositionAddress = "D4";
this.encProfile_Recognition_Front.PositionDecimalPlaces = 1;
this.encProfile_Recognition_Front.PositionDivisor = 10D;
this.encProfile_Recognition_Front.ReferenceAddress = "D477";
this.encProfile_Recognition_Front.Size = new System.Drawing.Size(926, 40);
this.encProfile_Recognition_Front.TabIndex = 6;
this.encProfile_Recognition_Front.TitleTextKey = "Profile Recognition Front";
this.encProfile_Recognition_Front.UnclampingIntervalAddress = "D51";
this.encProfile_Recognition_Front.ValveType = Kaban.UI.Controls.CylinderValve.ThreeWayClosedCenter;
```

### InverterUnitHMI

A motor driven by an inverter (a Mitsubishi FR-A800) on a Manual Movement page, looking like a CylinderUnitHMI: its command buttons (one - run - or two, the directions: CommandAAddress / CommandBAddress, their captions ButtonATextKey / ButtonBTextKey), its run lamp before the name (RunLampAddress), its value between the buttons when it has one (the Position... properties: a speed, a frequency), FaultCodeAddress - the word the PLC copies the inverter's fault code into (its data code: 16 = E.OC1) - and ErrorAddress, the motor's alarm bit. The fault code is NEVER shown as a number: while it is not zero both buttons blink red, the unit's "?" opens the fault's entry and the inverter's manual, and the HMI raises the motor's alarm itself. One control instead of a unit, a run lamp and an error display. No valve, no feedback.
On our pages: 6 of them; size 360 x 64 (6).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Inverter** | | | |
| `FaultCodeAddress` | string | "" | The word holding the inverter's fault as its DATA CODE (FR-A800 manual 6.4: 16 = E.OC1; 0 = no fault). |
| `ErrorAddress` | string | "" | The alarm bit of the inverter's fault (optional): SET by the HMI when the fault comes, like an axis's error bit; the PLC's alarm for it is then not listed twice. |
| `RunLampAddress` | string | "" | The inverter running (its RUN output, or the PLC's bit for it): a lamp before the unit's name. Empty: no lamp. |
| `RunLampTipKey` | string | "Running" | The run lamp's tooltip (translation key or text). |
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| `BaseOffset` | int | 0 | ROW INDEX added to every address in its own number system (M decimal, X/Y octal). |
| **Unit** | | | |
| `TitleTextKey` | string | "Motor" | The frame's title: a translation key - usually the English text. |
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
| `HelpBFiles` | string | "" | Help files for the B movement (in the machine's Help folder): shown with the manual when ErrorB is set, and behind the unit's '?' at any time. |
| `ButtonBVisibilityConditions` | SystemCondition | None | EXTRA visibility conditions for button B, added on top of the unit's (inherited conditions always apply). |
| `ButtonBEnableConditions` | SystemCondition | None | EXTRA enable conditions for button B, added on top of the unit's (inherited conditions always apply). |
| `ButtonBEnableTrigger` | PlcTrigger |  | PLC signal that must be true before button B can be pressed. |
| **Value** | | | |
| `PositionAddress` | string | "" | Word address of the encoder value. Empty hides the readout. |
| `PositionDataType` | DataTypeEnum | Bin_16 | Bin_16 = one word, Bin_32 = a double word. |
| `PositionDivisor` | double | 1 |  |
| `PositionDecimalPlaces` | int | 0 | Decimals of the position. |
| `PositionDigits` | int | 5 | Digits of the position in all. |
| `PositionSuffix` | string | "" | The position's unit ("mm"). |
| `PositionUnitType` | UnitType | None | Unit family for the position readout. When set (e.g. Length), the value follows the unit chosen in Unit Settings and the suffix changes with it; PositionSuffix is then only used as the fallback when no unit is active. |
| `PositionOffset` | DynamicValue |  | Address offset for the position tag (Static / Tag / HmiVariable), same as NumericDisplayHMI.AddressOffset. |
| `PositionStyle` | UnitPositionStyle | Standard | Standard: black bold digits on grey, like the Axis Settings rows. Readout: red digits on black. |
| `PositionTipKey` | string | "" | The position's tooltip (a translation key, or the text). |
| **Parts** | | | |
| `Arrows` | ButtonArrowStyle | LeftRight | Arrow pair used on the two buttons. Custom keeps whatever captions you type. |
| **Conditions** | | | |
| `VisibilityConditions` | SystemCondition | None | Shown only while these SystemCondition flags are met (None: always). |
| `EnableConditions` | SystemCondition | None | Usable only while these are met, greyed otherwise (None: always). |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | Operator | The lowest level allowed to use it: Operator (Manual Movement), Maintenance (Machine Parameters), Admin. |
| `ConfirmationNeeded` | bool | False | Asks "Are you sure?" before a SET / RESET / ALTERNATE or word write (never before a momentary jog). |
| `LogAction` | bool | True | true: every write it makes goes to the log with the user's name - set it on everything that writes. |

An example from Manual\ManualMilling.Designer.cs:

```csharp
this.unitUnit_1_Milling_Motor = new Kaban.UI.Controls.InverterUnitHMI();
this.unitUnit_1_Milling_Motor.Arrows = Kaban.Core.Enums.ButtonArrowStyle.Custom;
this.unitUnit_1_Milling_Motor.BackColor = System.Drawing.Color.White;
this.unitUnit_1_Milling_Motor.ButtonATextKey = "Run";
this.unitUnit_1_Milling_Motor.CommandAAction = Kaban.Core.Enums.BitActionType.Alternate;
this.unitUnit_1_Milling_Motor.CommandAAddress = "M649";
this.unitUnit_1_Milling_Motor.ErrorAddress = "M828";
this.unitUnit_1_Milling_Motor.FaultCodeAddress = "D667";
this.unitUnit_1_Milling_Motor.Lamp1AAddress = "M230";
this.unitUnit_1_Milling_Motor.Location = new System.Drawing.Point(14, 639);
this.unitUnit_1_Milling_Motor.LogAction = true;
this.unitUnit_1_Milling_Motor.Name = "unitUnit_1_Milling_Motor";
this.unitUnit_1_Milling_Motor.PlcNumber = 1;
this.unitUnit_1_Milling_Motor.PositionAddress = "D18";
this.unitUnit_1_Milling_Motor.PositionDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
this.unitUnit_1_Milling_Motor.PositionDecimalPlaces = 2;
this.unitUnit_1_Milling_Motor.PositionDigits = 6;
this.unitUnit_1_Milling_Motor.PositionDivisor = 100D;
this.unitUnit_1_Milling_Motor.PositionTipKey = "";
this.unitUnit_1_Milling_Motor.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
this.unitUnit_1_Milling_Motor.Size = new System.Drawing.Size(360, 64);
this.unitUnit_1_Milling_Motor.TabIndex = 11;
this.unitUnit_1_Milling_Motor.TitleTextKey = "Milling Motor";
```

### ButtonHMI

A push button writing bits (BitActions) and / or words (WordActions): momentary, alternate, SET or RESET; a lamp of its state or sensor on it (Lamp1Address). Its caption: TranslationKey + FallbackText (or ButtonText).
On our pages: 124 of them; size 360 x 40 (51), 300 x 36 (33), 79 x 32 (12).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| **Action** | | | |
| `BitActions` | List<BitAction> |  | The bits the button writes: one BitAction (Address, Action) each. |
| `WordActions` | List<WordAction> |  | The words the button writes (a value each): leave empty for a plain button. |
| `HmiFunction` | HmiButtonFunction | None | An HMI function the button switches, no PLC bit: CylinderForwardBack - a cylinder started moves back and forth, each way just under its alarm delay (Maintenance and Admin). |
| **Display** | | | |
| `DescriptionText` | string | "" | Its caption, drawn beside it: a translation key - usually the English text, shown as written when it has no translation. |
| `ButtonText` | string | "" | The same as FallbackText. |
| `FallbackText` | string | "" | Its caption when the key has no translation (the English text). |
| `TranslationKey` | string | "" | The key of its text in the translation files - usually the English text itself. |
| `TwoLineCaption` | bool | False | The caption on two lines, broken at its space nearest the middle (any language) - a narrower button. |
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
| `ConfirmationAddress` | string | "" | Asks only while this bit is on (a Cancel asked while the machine works: M21 on FA2010); empty: as ConfirmationNeeded says. |
| `ConfirmationTextKey` | string | "" | The question asked: a translation key (Msg_CancelOperation); empty: "Are you sure?". |
| `ConfirmationText` | string | "" | The question in English, shown when its key has no translation. |
| `LogAction` | bool | False | true: every write it makes goes to the log with the user's name - set it on everything that writes. |
| `ResetOnUserChange` | bool | False | Its latched bits (Alternate / SET actions) are set to 0 when the signed-in user changes (Safety Bypass). |
| **Help** | | | |
| `ToolTipText` | string | "" | Its tooltip: a translation key, or the text. |

A `BitAction` (`Kaban.Core.Models.BitAction`): `Address` (a bit), `Action` (BitActionType). A `WordAction` writes a value to a word.

An example from Parameters\AxisSettings.Designer.cs:

```csharp
this.btnSend_Axes_To_Reference = new Kaban.UI.Controls.ButtonHMI();
this.btnSend_Axes_To_Reference.ActiveBackColor = System.Drawing.Color.White;
this.btnSend_Axes_To_Reference.AlertBlink = false;
this.btnSend_Axes_To_Reference.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
Kaban.Core.Models.BitAction bitAction1 = new Kaban.Core.Models.BitAction();   // declared at the top of InitializeComponent
bitAction1.Action = Kaban.Core.Enums.BitActionType.SET;
bitAction1.Address = "M22";
bitAction1.AddressOffset.StaticValue = 0D;
bitAction1.AddressOffset.TagAddress = "";
bitAction1.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
this.btnSend_Axes_To_Reference.BitActions.Add(bitAction1);
this.btnSend_Axes_To_Reference.BlinkCondition = Kaban.Core.Enums.SystemCondition.None;
this.btnSend_Axes_To_Reference.ButtonText = "Send Axes to Reference";
this.btnSend_Axes_To_Reference.ColorActive = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
this.btnSend_Axes_To_Reference.ColorDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(250)))));
this.btnSend_Axes_To_Reference.ColorPassive = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
this.btnSend_Axes_To_Reference.ConfirmationAddress = "";
this.btnSend_Axes_To_Reference.ConfirmationNeeded = false;
this.btnSend_Axes_To_Reference.ConfirmationText = "";
this.btnSend_Axes_To_Reference.ConfirmationTextKey = "";
this.btnSend_Axes_To_Reference.CornerRadius = 4;
this.btnSend_Axes_To_Reference.DescriptionText = "";
this.btnSend_Axes_To_Reference.Direction = Kaban.UI.Controls.ButtonGlyphs.Direction.None;
this.btnSend_Axes_To_Reference.DisplayAddress = null;
this.btnSend_Axes_To_Reference.DisplayOffset.StaticValue = 0D;
this.btnSend_Axes_To_Reference.DisplayOffset.TagAddress = "";
this.btnSend_Axes_To_Reference.DisplayOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
this.btnSend_Axes_To_Reference.EnableConditions = Kaban.Core.Enums.SystemCondition.None;
this.btnSend_Axes_To_Reference.EnableTrigger.DescriptionKey = "";
this.btnSend_Axes_To_Reference.EnableTrigger.MonitorAddress = "";
this.btnSend_Axes_To_Reference.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
this.btnSend_Axes_To_Reference.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
this.btnSend_Axes_To_Reference.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
this.btnSend_Axes_To_Reference.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
this.btnSend_Axes_To_Reference.EnableTrigger.ValA = 0D;
this.btnSend_Axes_To_Reference.EnableTrigger.ValB = 0D;
this.btnSend_Axes_To_Reference.ErrorAddress = "";
this.btnSend_Axes_To_Reference.FallbackText = "Send Axes to Reference";
this.btnSend_Axes_To_Reference.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
this.btnSend_Axes_To_Reference.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
```

### LampHMI

A bit shown as a lamp (a sensor, a state, an output), its caption (DescriptionText) beside it.
On our pages: 110 of them; size 300 x 26 (96), 36 x 36 (14).

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

An example from Manual\ManualRobot.Designer.cs:

```csharp
this.lampBar_First_End_Read_SN = new Kaban.UI.Controls.LampHMI();
this.lampBar_First_End_Read_SN.Address = "M323";
this.lampBar_First_End_Read_SN.AddressOffset.StaticValue = 0D;
this.lampBar_First_End_Read_SN.AddressOffset.TagAddress = "";
this.lampBar_First_End_Read_SN.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
this.lampBar_First_End_Read_SN.BackColor = System.Drawing.Color.Transparent;
this.lampBar_First_End_Read_SN.CornerRadius = 4;
this.lampBar_First_End_Read_SN.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Right;
this.lampBar_First_End_Read_SN.DescriptionText = "Bar First End Read SN";
this.lampBar_First_End_Read_SN.ImageActive = null;
this.lampBar_First_End_Read_SN.ImagePassive = null;
this.lampBar_First_End_Read_SN.Location = new System.Drawing.Point(14, 362);
this.lampBar_First_End_Read_SN.Name = "lampBar_First_End_Read_SN";
this.lampBar_First_End_Read_SN.OffColor = System.Drawing.Color.FromArgb(((int)(((byte)(225)))), ((int)(((byte)(225)))), ((int)(((byte)(225)))));
this.lampBar_First_End_Read_SN.OnColor = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
this.lampBar_First_End_Read_SN.PlcNumber = 1;
this.lampBar_First_End_Read_SN.Size = new System.Drawing.Size(36, 36);
this.lampBar_First_End_Read_SN.TabIndex = 5;
this.lampBar_First_End_Read_SN.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
this.lampBar_First_End_Read_SN.ToolTipTextKey = "";
this.lampBar_First_End_Read_SN.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
```

### NumericInputHMI

A value typed in (a word or a double word): the keypad on a tap, limits, scaling, units, confirmation, conditions. Its caption (DescriptionText) is drawn to its left.
On our pages: 199 of them; size 116 x 26 (104), 440 x 32 (91), 122 x 32 (2).

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

An example from Parameters\ParamConveyor.Designer.cs:

```csharp
this.numMinimum_Bar_Length = new Kaban.UI.Controls.NumericInputHMI();
this.numMinimum_Bar_Length.Address = "D438";
this.numMinimum_Bar_Length.AddressOffset.StaticValue = 0D;
this.numMinimum_Bar_Length.AddressOffset.TagAddress = "";
this.numMinimum_Bar_Length.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
this.numMinimum_Bar_Length.BackColor = System.Drawing.Color.White;
this.numMinimum_Bar_Length.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
this.numMinimum_Bar_Length.ConfirmWrite = false;
this.numMinimum_Bar_Length.CornerRadius = 4;
this.numMinimum_Bar_Length.Cursor = System.Windows.Forms.Cursors.Hand;
this.numMinimum_Bar_Length.DataFormat = Kaban.Core.Enums.DisplayFormatEnum.Signed_Decimal;
this.numMinimum_Bar_Length.DataType = Kaban.Core.Enums.DataTypeEnum.Bin_32;
this.numMinimum_Bar_Length.DecimalPlaces = 1;
this.numMinimum_Bar_Length.DescriptionColon = true;
this.numMinimum_Bar_Length.DescriptionText = "Minimum Bar Length";
this.numMinimum_Bar_Length.Digits = 5;
this.numMinimum_Bar_Length.Divisor = 10000D;
this.numMinimum_Bar_Length.EnableConditions = Kaban.Core.Enums.SystemCondition.None;
this.numMinimum_Bar_Length.EnableTrigger.DescriptionKey = "";
this.numMinimum_Bar_Length.EnableTrigger.MonitorAddress = "";
this.numMinimum_Bar_Length.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
this.numMinimum_Bar_Length.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
this.numMinimum_Bar_Length.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
this.numMinimum_Bar_Length.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
this.numMinimum_Bar_Length.EnableTrigger.ValA = 0D;
this.numMinimum_Bar_Length.EnableTrigger.ValB = 0D;
this.numMinimum_Bar_Length.FillZero = false;
this.numMinimum_Bar_Length.IsEditing = false;
this.numMinimum_Bar_Length.Location = new System.Drawing.Point(281, 22);
this.numMinimum_Bar_Length.LogAction = true;
this.numMinimum_Bar_Length.MaxLimit.StaticValue = 0D;
this.numMinimum_Bar_Length.MaxLimit.TagAddress = "";
this.numMinimum_Bar_Length.MaxLimit.Type = Kaban.Core.Enums.LimitSourceType.None;
this.numMinimum_Bar_Length.MinLimit.StaticValue = 0D;
this.numMinimum_Bar_Length.MinLimit.TagAddress = "";
this.numMinimum_Bar_Length.MinLimit.Type = Kaban.Core.Enums.LimitSourceType.None;
this.numMinimum_Bar_Length.Name = "numMinimum_Bar_Length";
this.numMinimum_Bar_Length.OutOfLimitsWarning = false;
this.numMinimum_Bar_Length.PlcNumber = 1;
this.numMinimum_Bar_Length.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
```

### NumericDisplayHMI

A value only shown (a word or a double word): scaled, in decimal or hexadecimal (an error code).
On our pages: 3 of them; size 150 x 40 (2), 120 x 40 (1).

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

An example from Manual\ManualEntry_Conveyor.Designer.cs:

```csharp
this.dispX1_Second_Value = new Kaban.UI.Controls.NumericDisplayHMI();
this.dispX1_Second_Value.Name = "dispX1_Second_Value";
this.dispX1_Second_Value.PlcNumber = 1;
this.dispX1_Second_Value.Address = "D156";
this.dispX1_Second_Value.DataType = Kaban.Core.Enums.DataTypeEnum.Bin_32;
this.dispX1_Second_Value.Divisor = 100D;
this.dispX1_Second_Value.DecimalPlaces = 1;
this.dispX1_Second_Value.Digits = 6;
this.dispX1_Second_Value.Suffix = "mm";
this.dispX1_Second_Value.DescriptionText = "X1 Second Value";
this.dispX1_Second_Value.Location = new System.Drawing.Point(226, 122);
this.dispX1_Second_Value.Size = new System.Drawing.Size(150, 40);
```

### CheckBoxHMI

One bit as a tick box (an option on / off), written on a click.
On our pages: 69 of them; size 440 x 28 (43), 44 x 32 (26).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| `Address` | string | "" | The PLC device it reads / writes ("D16", "M232"). |
| `AlertAddress` | string | "" | A bit asking for this box: while it is TRUE the border blinks red (e.g. the PLC's home request on the axis row's Ref box). |
| **Display** | | | |
| `DescriptionText` | string | "" | Its caption, drawn beside it: a translation key - usually the English text, shown as written when it has no translation. |
| `DescriptionSide` | DescriptionSide | Right | Which side of it the caption is drawn. |
| **Conditions** | | | |
| `VisibilityConditions` | SystemCondition | None | Shown only while these SystemCondition flags are met (None: always). |
| `EnableConditions` | SystemCondition | None | Usable only while these are met, greyed otherwise (None: always). |
| `EnableTrigger` | PlcTrigger |  | A PLC bit that must be ON before it can be used (leave empty). |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | Operator | The lowest level allowed to use it: Operator (Manual Movement), Maintenance (Machine Parameters), Admin. |
| `LogAction` | bool | True | true: every write it makes goes to the log with the user's name - set it on everything that writes. |
| **Help** | | | |
| `ToolTipCaption` | string |  | Text for the TOOLTIP only, never drawn on the control. |

An example from Parameters\Options.Designer.cs:

```csharp
this.optVithBlade = new Kaban.UI.Controls.CheckBoxHMI();
this.optVithBlade.Address = "L46";
this.optVithBlade.CheckAlign = System.Drawing.ContentAlignment.MiddleCenter;
this.optVithBlade.CornerRadius = 4;
this.optVithBlade.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Left;
this.optVithBlade.DescriptionText = "with Blade";
this.optVithBlade.EnableConditions = Kaban.Core.Enums.SystemCondition.MachineBit20;
this.optVithBlade.EnableTrigger.DescriptionKey = "";
this.optVithBlade.EnableTrigger.MonitorAddress = "";
this.optVithBlade.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
this.optVithBlade.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
this.optVithBlade.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
this.optVithBlade.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
this.optVithBlade.EnableTrigger.ValA = 0D;
this.optVithBlade.EnableTrigger.ValB = 0D;
this.optVithBlade.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
this.optVithBlade.Location = new System.Drawing.Point(319, 75);
this.optVithBlade.LogAction = true;
this.optVithBlade.Name = "optVithBlade";
this.optVithBlade.PlcNumber = 1;
this.optVithBlade.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;
this.optVithBlade.Size = new System.Drawing.Size(44, 32);
this.optVithBlade.TabIndex = 64;
this.optVithBlade.ToolTipCaption = null;
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
On our pages: 18 of them; size 360 x 26 (10), 326 x 26 (3), 377 x 26 (3).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Display** | | | |
| `TranslationKey` | string | "" | The key of its text in the translation files - usually the English text itself. |

An example from Parameters\ParamOther.Designer.cs:

```csharp
this.lblExit_Conveyor = new Kaban.UI.Controls.LabelHMI();
this.lblExit_Conveyor.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold);
this.lblExit_Conveyor.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(70)))), ((int)(((byte)(78)))));
this.lblExit_Conveyor.Location = new System.Drawing.Point(16, 16);
this.lblExit_Conveyor.Name = "lblExit_Conveyor";
this.lblExit_Conveyor.Size = new System.Drawing.Size(326, 26);
this.lblExit_Conveyor.TabIndex = 0;
this.lblExit_Conveyor.Text = "Exit Conveyor";
this.lblExit_Conveyor.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
this.lblExit_Conveyor.TranslationKey = "Exit Conveyor";
```

### GroupBoxHMI

A frame like a CylinderUnitHMI's around controls that belong together (a row of small buttons); its title TitleTextKey. The controls inside are added to it.
On our pages: 6 of them; size 360 x 64 (4), 405 x 245 (1), 390 x 447 (1).

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

An example from Manual\ManualCutting.Designer.cs:

```csharp
this.groupBoxVCuıtting = new Kaban.UI.Controls.GroupBoxHMI();
this.groupBoxVCuıtting.SuspendLayout();
this.groupBoxVCuıtting.Address = null;
this.groupBoxVCuıtting.BackColor = System.Drawing.Color.White;
this.groupBoxVCuıtting.Controls.Add(this.unitV_Cutting_Y);
this.groupBoxVCuıtting.Controls.Add(this.unitV_Cutting_Z);
this.groupBoxVCuıtting.Controls.Add(this.unitV_Cutting_X);
this.groupBoxVCuıtting.Controls.Add(this.unitV_Cutting_Saw_Motor);
this.groupBoxVCuıtting.Controls.Add(this.unitMullion_End_Milling_Unit);
this.groupBoxVCuıtting.Controls.Add(this.btnMullion_Blade_Change);
this.groupBoxVCuıtting.Emoji = "";
this.groupBoxVCuıtting.EnableAddress = "";
this.groupBoxVCuıtting.EnableConditions = Kaban.Core.Enums.SystemCondition.None;
this.groupBoxVCuıtting.Location = new System.Drawing.Point(845, 14);
this.groupBoxVCuıtting.Name = "groupBoxVCuıtting";
this.groupBoxVCuıtting.Padding = new System.Windows.Forms.Padding(10, 22, 10, 8);
this.groupBoxVCuıtting.PlcNumber = 1;
this.groupBoxVCuıtting.Size = new System.Drawing.Size(390, 447);
this.groupBoxVCuıtting.TabIndex = 40;
this.groupBoxVCuıtting.TitleText = "V Cutting";
this.groupBoxVCuıtting.TitleTextKey = "V Cutting";
this.groupBoxVCuıtting.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
this.groupBoxVCuıtting.VisibleAddress = "";
this.groupBoxVCuıtting.Paint += new System.Windows.Forms.PaintEventHandler(this.groupBoxHMI1_Paint);
this.groupBoxVCuıtting.ResumeLayout(false);
this.groupBoxVCuıtting.PerformLayout();
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

### SpeedOverrideHMI

The machine's working speed in percent on a Home page (the old program's speed track bar): its caption, - and + stepping through Steps (0, 1, 2, 4 ... 95, 100 by default) and the percentage read back from Address, so a value set elsewhere is shown as it is.
On our pages: 2 of them; size 248 x 42 (1), 248 x 50 (1).

| Property | Type | Default | What it is |
|---|---|---|---|
| **Device** | | | |
| `PlcNumber` | int | 1 | The machine's own PLC number: 1 = its main PLC (2 = a second one). |
| `Address` | string | "" | The PLC device it reads / writes ("D16", "M232"). |
| **Display** | | | |
| `TranslationKey` | string | "Lbl_Speed" | The key of its text in the translation files - usually the English text itself. |
| `FallbackText` | string | "Speed" | Its caption when the key has no translation (the English text). |
| **Input** | | | |
| `Steps` | int[] |  | The percentages - and + step through, lowest first (leave the default: the old track bar's). |
| **Security** | | | |
| `RequiredSecurity` | SecurityLevel | Operator | The lowest level allowed to use it: Operator (Manual Movement), Maintenance (Machine Parameters), Admin. |
| `LogAction` | bool | True | true: every write it makes goes to the log with the user's name - set it on everything that writes. |

An example from Screens\HomeScreen.Designer.cs:

```csharp
this.speedOverride = new Kaban.UI.Controls.SpeedOverrideHMI();
this.speedOverride.Address = "D408";
this.speedOverride.BackColor = System.Drawing.Color.White;
this.speedOverride.FallbackText = "Speed";
this.speedOverride.Location = new System.Drawing.Point(14, 13);
this.speedOverride.LogAction = true;
this.speedOverride.Name = "speedOverride";
this.speedOverride.PlcNumber = 1;
this.speedOverride.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
this.speedOverride.Size = new System.Drawing.Size(248, 42);
this.speedOverride.Steps = new int[] {
this.speedOverride.TabIndex = 3;
this.speedOverride.TranslationKey = "Lbl_Speed";
```

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
- **Kaban.UI.Controls.HmiButtonFunction**: `None`, `CylinderForwardBack`
- **Kaban.UI.Controls.ButtonHMI.LampDirectionPosition**: `Left`, `Right`, `Top`, `Bottom`
- **Kaban.Core.Enums.SecurityLevel**: `None`, `Operator`, `Maintenance`, `Admin`
- **Kaban.Core.Enums.SystemCondition** (flags: combine with `|`): `None` - None (Always Active), `SafetyOK` - Safety Relays OK, `ManualMode` - Manual Mode Active, `AutoMode` - Auto Mode Active, `AdminUser` - Admin User Logged In, `MachineIsRunning` - Machine is Running, `MaintenanceUser` - Maintenance User Logged In, `AxisStepMode` - Axes in Step Mode (not JOG), `MachineBit16` ... `MachineBit63` - a machine's own conditions (its RegisterMachineBit rules, in the order it allocates them)
- **Kaban.UI.Controls.UnitPositionStyle**: `Readout`, `Standard`
- **Kaban.Core.Enums.UnitType**: `None`, `Length`, `Weight`, `Temperature`, `Pressure`, `Time`

## 7. What to hand back

1. The complete `<Page>.cs` and `<Page>.Designer.cs` (namespace `Kaban.Machines.<ID>.Screens.<Folder>`).
2. The `.csproj` lines (section 1).
3. The tab line for `<ID>Module.cs` (section 1).
4. The `Translations.csv` rows of every new caption: `Key;EN;TR;RU;DE;IT;ES` - always with the Turkish (section 1;
   the old page's or the device list's when they have it); the other languages may stay empty (English shows).
5. A short table of the addresses used: the control, the address, where it came from (the old page, the device list).
6. What could not be mapped or placed, and every assumption - instead of an invented address.

**Rules:** keep the old page's addresses exactly as they are; one of our controls per function (section 2); every control
named; `AutoScaleMode.None`; nothing of our controls' types in a `.resx`; no code but the designer's (and the machine's bar,
section 1); no UserControl of the machine's project in a designer file; the layout of section 3.

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
            this.unitGripper = new Kaban.UI.Controls.CylinderEncoderUnitHMI();
            this.btnSawdust_Blowing = new Kaban.UI.Controls.ButtonHMI();
            this.lampBar_First_End_Read_SN = new Kaban.UI.Controls.LampHMI();
            this.lampBar_Last_End_Read_SN = new Kaban.UI.Controls.LampHMI();
            this.unitGripper_Angle = new Kaban.UI.Controls.CylinderUnitHMI();
            this.SuspendLayout();
            // 
            // unitAxis_X
            // 
            this.unitAxis_X.AxisNumber = 1;
            this.unitAxis_X.BackColor = System.Drawing.Color.White;
            this.unitAxis_X.Location = new System.Drawing.Point(14, 16);
            this.unitAxis_X.Name = "unitAxis_X";
            this.unitAxis_X.Size = new System.Drawing.Size(360, 64);
            this.unitAxis_X.TabIndex = 0;
            // 
            // unitAxis_Y
            // 
            this.unitAxis_Y.AxisNumber = 2;
            this.unitAxis_Y.BackColor = System.Drawing.Color.White;
            this.unitAxis_Y.Location = new System.Drawing.Point(14, 90);
            this.unitAxis_Y.Name = "unitAxis_Y";
            this.unitAxis_Y.Size = new System.Drawing.Size(360, 64);
            this.unitAxis_Y.TabIndex = 1;
            // 
            // unitAxis_Z
            // 
            this.unitAxis_Z.AxisNumber = 3;
            this.unitAxis_Z.BackColor = System.Drawing.Color.White;
            this.unitAxis_Z.Location = new System.Drawing.Point(14, 164);
            this.unitAxis_Z.Name = "unitAxis_Z";
            this.unitAxis_Z.Size = new System.Drawing.Size(360, 64);
            this.unitAxis_Z.TabIndex = 2;
            // 
            // unitGripper
            // 
            this.unitGripper.BackColor = System.Drawing.Color.White;
            this.unitGripper.EncoderNumber = 1;
            this.unitGripper.Location = new System.Drawing.Point(14, 238);
            this.unitGripper.Name = "unitGripper";
            this.unitGripper.Size = new System.Drawing.Size(360, 64);
            this.unitGripper.TabIndex = 3;
            this.unitGripper.TitleTextKey = "Gripper";
            // 
            // btnSawdust_Blowing
            // 
            this.btnSawdust_Blowing.ActiveBackColor = System.Drawing.Color.White;
            this.btnSawdust_Blowing.AlertBlink = false;
            this.btnSawdust_Blowing.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            bitAction1.Action = Kaban.Core.Enums.BitActionType.Momentary;
            bitAction1.Address = "M697";
            bitAction1.AddressOffset.StaticValue = 0D;
            bitAction1.AddressOffset.TagAddress = "";
            bitAction1.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.btnSawdust_Blowing.BitActions.Add(bitAction1);
            this.btnSawdust_Blowing.BlinkCondition = Kaban.Core.Enums.SystemCondition.None;
            this.btnSawdust_Blowing.ButtonText = "Sawdust Blowing";
            this.btnSawdust_Blowing.ColorActive = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.btnSawdust_Blowing.ColorDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(248)))), ((int)(((byte)(249)))), ((int)(((byte)(250)))));
            this.btnSawdust_Blowing.ColorPassive = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.btnSawdust_Blowing.ConfirmationAddress = "";
            this.btnSawdust_Blowing.ConfirmationNeeded = false;
            this.btnSawdust_Blowing.ConfirmationText = "";
            this.btnSawdust_Blowing.ConfirmationTextKey = "";
            this.btnSawdust_Blowing.CornerRadius = 4;
            this.btnSawdust_Blowing.DescriptionText = "";
            this.btnSawdust_Blowing.Direction = Kaban.UI.Controls.ButtonGlyphs.Direction.None;
            this.btnSawdust_Blowing.DisplayAddress = null;
            this.btnSawdust_Blowing.DisplayOffset.StaticValue = 0D;
            this.btnSawdust_Blowing.DisplayOffset.TagAddress = "";
            this.btnSawdust_Blowing.DisplayOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.btnSawdust_Blowing.EnableConditions = Kaban.Core.Enums.SystemCondition.None;
            this.btnSawdust_Blowing.EnableTrigger.DescriptionKey = "";
            this.btnSawdust_Blowing.EnableTrigger.MonitorAddress = "";
            this.btnSawdust_Blowing.EnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.btnSawdust_Blowing.EnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.btnSawdust_Blowing.EnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.btnSawdust_Blowing.EnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.btnSawdust_Blowing.EnableTrigger.ValA = 0D;
            this.btnSawdust_Blowing.EnableTrigger.ValB = 0D;
            this.btnSawdust_Blowing.ErrorAddress = "";
            this.btnSawdust_Blowing.FallbackText = "Sawdust Blowing";
            this.btnSawdust_Blowing.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.btnSawdust_Blowing.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(240)))), ((int)(((byte)(241)))), ((int)(((byte)(243)))));
            this.btnSawdust_Blowing.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSawdust_Blowing.Font = new System.Drawing.Font("Segoe UI", 9.5F);
            this.btnSawdust_Blowing.ForeColor = System.Drawing.Color.Black;
            this.btnSawdust_Blowing.ForeDisabled = System.Drawing.Color.FromArgb(((int)(((byte)(176)))), ((int)(((byte)(182)))), ((int)(((byte)(190)))));
            this.btnSawdust_Blowing.HoverBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(120)))), ((int)(((byte)(220)))));
            this.btnSawdust_Blowing.ImageActive = null;
            this.btnSawdust_Blowing.ImagePassive = null;
            this.btnSawdust_Blowing.IsActivePage = false;
            this.btnSawdust_Blowing.Lamp1Address = "";
            this.btnSawdust_Blowing.Lamp1Tip = "";
            this.btnSawdust_Blowing.Lamp2Address = "";
            this.btnSawdust_Blowing.Lamp2Tip = "";
            this.btnSawdust_Blowing.LampDirection = Kaban.UI.Controls.ButtonHMI.LampDirectionPosition.Left;
            this.btnSawdust_Blowing.Location = new System.Drawing.Point(14, 312);
            this.btnSawdust_Blowing.LogAction = true;
            this.btnSawdust_Blowing.Name = "btnSawdust_Blowing";
            this.btnSawdust_Blowing.OutputAddress = "";
            this.btnSawdust_Blowing.OutputBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(40)))), ((int)(((byte)(170)))), ((int)(((byte)(70)))));
            this.btnSawdust_Blowing.PlcNumber = 1;
            this.btnSawdust_Blowing.RelayAddress = "";
            this.btnSawdust_Blowing.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.btnSawdust_Blowing.RestingBorderColor = System.Drawing.Color.FromArgb(((int)(((byte)(168)))), ((int)(((byte)(176)))), ((int)(((byte)(186)))));
            this.btnSawdust_Blowing.RoundLeftOnly = false;
            this.btnSawdust_Blowing.RoundRightOnly = false;
            this.btnSawdust_Blowing.ShowCaption = true;
            this.btnSawdust_Blowing.Size = new System.Drawing.Size(360, 40);
            this.btnSawdust_Blowing.TabIndex = 4;
            this.btnSawdust_Blowing.ToolTipText = "";
            this.btnSawdust_Blowing.TranslationKey = "Sawdust Blowing";
            this.btnSawdust_Blowing.UseVisualStyleBackColor = false;
            this.btnSawdust_Blowing.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            // 
            // lampBar_First_End_Read_SN
            // 
            this.lampBar_First_End_Read_SN.Address = "M323";
            this.lampBar_First_End_Read_SN.AddressOffset.StaticValue = 0D;
            this.lampBar_First_End_Read_SN.AddressOffset.TagAddress = "";
            this.lampBar_First_End_Read_SN.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.lampBar_First_End_Read_SN.BackColor = System.Drawing.Color.Transparent;
            this.lampBar_First_End_Read_SN.CornerRadius = 4;
            this.lampBar_First_End_Read_SN.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Right;
            this.lampBar_First_End_Read_SN.DescriptionText = "Bar First End Read SN";
            this.lampBar_First_End_Read_SN.ImageActive = null;
            this.lampBar_First_End_Read_SN.ImagePassive = null;
            this.lampBar_First_End_Read_SN.Location = new System.Drawing.Point(14, 362);
            this.lampBar_First_End_Read_SN.Name = "lampBar_First_End_Read_SN";
            this.lampBar_First_End_Read_SN.OffColor = System.Drawing.Color.FromArgb(((int)(((byte)(225)))), ((int)(((byte)(225)))), ((int)(((byte)(225)))));
            this.lampBar_First_End_Read_SN.OnColor = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.lampBar_First_End_Read_SN.PlcNumber = 1;
            this.lampBar_First_End_Read_SN.Size = new System.Drawing.Size(36, 36);
            this.lampBar_First_End_Read_SN.TabIndex = 5;
            this.lampBar_First_End_Read_SN.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lampBar_First_End_Read_SN.ToolTipTextKey = "";
            this.lampBar_First_End_Read_SN.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            // 
            // lampBar_Last_End_Read_SN
            // 
            this.lampBar_Last_End_Read_SN.Address = "M324";
            this.lampBar_Last_End_Read_SN.AddressOffset.StaticValue = 0D;
            this.lampBar_Last_End_Read_SN.AddressOffset.TagAddress = "";
            this.lampBar_Last_End_Read_SN.AddressOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.lampBar_Last_End_Read_SN.BackColor = System.Drawing.Color.Transparent;
            this.lampBar_Last_End_Read_SN.CornerRadius = 4;
            this.lampBar_Last_End_Read_SN.DescriptionSide = Kaban.Core.Enums.DescriptionSide.Right;
            this.lampBar_Last_End_Read_SN.DescriptionText = "Bar Last End Read SN";
            this.lampBar_Last_End_Read_SN.ImageActive = null;
            this.lampBar_Last_End_Read_SN.ImagePassive = null;
            this.lampBar_Last_End_Read_SN.Location = new System.Drawing.Point(14, 408);
            this.lampBar_Last_End_Read_SN.Name = "lampBar_Last_End_Read_SN";
            this.lampBar_Last_End_Read_SN.OffColor = System.Drawing.Color.FromArgb(((int)(((byte)(225)))), ((int)(((byte)(225)))), ((int)(((byte)(225)))));
            this.lampBar_Last_End_Read_SN.OnColor = System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))));
            this.lampBar_Last_End_Read_SN.PlcNumber = 1;
            this.lampBar_Last_End_Read_SN.Size = new System.Drawing.Size(36, 36);
            this.lampBar_Last_End_Read_SN.TabIndex = 7;
            this.lampBar_Last_End_Read_SN.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lampBar_Last_End_Read_SN.ToolTipTextKey = "";
            this.lampBar_Last_End_Read_SN.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            // 
            // unitGripper_Angle
            // 
            this.unitGripper_Angle.Arrows = Kaban.Core.Enums.ButtonArrowStyle.LeftRight;
            this.unitGripper_Angle.BackColor = System.Drawing.Color.White;
            this.unitGripper_Angle.BaseOffset = 0;
            this.unitGripper_Angle.ButtonAEnableConditions = Kaban.Core.Enums.SystemCondition.None;
            this.unitGripper_Angle.ButtonAEnableTrigger.DescriptionKey = "";
            this.unitGripper_Angle.ButtonAEnableTrigger.MonitorAddress = "";
            this.unitGripper_Angle.ButtonAEnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.unitGripper_Angle.ButtonAEnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.unitGripper_Angle.ButtonAEnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.unitGripper_Angle.ButtonAEnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.unitGripper_Angle.ButtonAEnableTrigger.ValA = 0D;
            this.unitGripper_Angle.ButtonAEnableTrigger.ValB = 0D;
            this.unitGripper_Angle.ButtonATextKey = "◀";
            this.unitGripper_Angle.ButtonATipKey = "";
            this.unitGripper_Angle.ButtonAVisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            this.unitGripper_Angle.ButtonBEnableConditions = Kaban.Core.Enums.SystemCondition.None;
            this.unitGripper_Angle.ButtonBEnableTrigger.DescriptionKey = "";
            this.unitGripper_Angle.ButtonBEnableTrigger.MonitorAddress = "";
            this.unitGripper_Angle.ButtonBEnableTrigger.MonitorDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.unitGripper_Angle.ButtonBEnableTrigger.OpA = Kaban.Core.Enums.ComparisonOperator.None;
            this.unitGripper_Angle.ButtonBEnableTrigger.OpB = Kaban.Core.Enums.ComparisonOperator.None;
            this.unitGripper_Angle.ButtonBEnableTrigger.TriggerType = Kaban.Core.Enums.TriggerType.None;
            this.unitGripper_Angle.ButtonBEnableTrigger.ValA = 0D;
            this.unitGripper_Angle.ButtonBEnableTrigger.ValB = 0D;
            this.unitGripper_Angle.ButtonBTextKey = "▶";
            this.unitGripper_Angle.ButtonBTipKey = "";
            this.unitGripper_Angle.ButtonBVisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            this.unitGripper_Angle.CommandAAction = Kaban.Core.Enums.BitActionType.Momentary;
            this.unitGripper_Angle.CommandAAddress = "M752";
            this.unitGripper_Angle.CommandBAction = Kaban.Core.Enums.BitActionType.Momentary;
            this.unitGripper_Angle.CommandBAddress = "M751";
            this.unitGripper_Angle.ConfirmationNeeded = false;
            this.unitGripper_Angle.EnableConditions = Kaban.Core.Enums.SystemCondition.None;
            this.unitGripper_Angle.FeedbackType = Kaban.UI.Controls.CylinderFeedback.TwoEndMagnetic;
            this.unitGripper_Angle.Lamp1AAddress = "M262";
            this.unitGripper_Angle.Lamp1ATipKey = "";
            this.unitGripper_Angle.Lamp1BAddress = "M263";
            this.unitGripper_Angle.Lamp1BTipKey = "";
            this.unitGripper_Angle.Lamp2AAddress = "";
            this.unitGripper_Angle.Lamp2ATipKey = "";
            this.unitGripper_Angle.Lamp2BAddress = "";
            this.unitGripper_Angle.Lamp2BTipKey = "";
            this.unitGripper_Angle.Location = new System.Drawing.Point(14, 454);
            this.unitGripper_Angle.LogAction = true;
            this.unitGripper_Angle.Name = "unitGripper_Angle";
            this.unitGripper_Angle.OutputAAddress = "";
            this.unitGripper_Angle.OutputBAddress = "";
            this.unitGripper_Angle.PlcNumber = 1;
            this.unitGripper_Angle.PositionAddress = "";
            this.unitGripper_Angle.PositionDataType = Kaban.Core.Enums.DataTypeEnum.Bin_16;
            this.unitGripper_Angle.PositionDecimalPlaces = 0;
            this.unitGripper_Angle.PositionDigits = 5;
            this.unitGripper_Angle.PositionDivisor = 1D;
            this.unitGripper_Angle.PositionOffset.StaticValue = 0D;
            this.unitGripper_Angle.PositionOffset.TagAddress = "";
            this.unitGripper_Angle.PositionOffset.Type = Kaban.Core.Enums.LimitSourceType.None;
            this.unitGripper_Angle.PositionSuffix = "";
            this.unitGripper_Angle.PositionTipKey = "Current position";
            this.unitGripper_Angle.PositionUnitType = Kaban.Core.Enums.UnitType.None;
            this.unitGripper_Angle.RelayAAddress = "";
            this.unitGripper_Angle.RelayBAddress = "";
            this.unitGripper_Angle.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.unitGripper_Angle.Size = new System.Drawing.Size(360, 64);
            this.unitGripper_Angle.TabIndex = 9;
            this.unitGripper_Angle.TitleTextKey = "Gripper Angle";
            this.unitGripper_Angle.TitleTipKey = "";
            this.unitGripper_Angle.ValveType = Kaban.UI.Controls.CylinderValve.DoubleSolenoid;
            this.unitGripper_Angle.VisibilityConditions = Kaban.Core.Enums.SystemCondition.None;
            // 
            // ManualRobot
            // 
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
            this.Size = new System.Drawing.Size(1240, 840);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private Kaban.UI.Controls.AxisUnitHMI unitAxis_X;
        private Kaban.UI.Controls.AxisUnitHMI unitAxis_Y;
        private Kaban.UI.Controls.AxisUnitHMI unitAxis_Z;
        private Kaban.UI.Controls.CylinderEncoderUnitHMI unitGripper;
        private Kaban.UI.Controls.ButtonHMI btnSawdust_Blowing;
        private Kaban.UI.Controls.LampHMI lampBar_First_End_Read_SN;
        private Kaban.UI.Controls.LampHMI lampBar_Last_End_Read_SN;
        private Kaban.UI.Controls.CylinderUnitHMI unitGripper_Angle;
    }
}
```
