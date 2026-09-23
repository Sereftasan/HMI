# Kaban HMI — briefing for a new assistant

Copy this whole file into a fresh chat, attach `HMI_Fixed.zip`, and state what you
want changed. It exists because the previous session ran out of room; everything an
assistant needs to work on this program safely is below.

---

## 1. What this is

A Windows HMI for Kaban machine tools (PVC/aluminium profile machining centres).
**C# / .NET Framework 4.x / WinForms**, built in Visual Studio. It runs on a panel
PC bolted to the machine and talks to Mitsubishi PLCs through **MX Component**
(`ActUtlType`, a 32-bit COM library).

The operator uses it to jog axes by hand, set machine parameters, watch alarms, and
(on FA2010) turn a DXF profile drawing into a cutting program.

### Non-negotiable environment facts

| Fact | Why it matters |
|---|---|
| MX Component COM is **32-bit only** | Every machine exe must be x86 / `Prefer32Bit=true`. A 64-bit build gets `0x80040154 REGDB_E_CLASSNOTREG`. There is a build guard (`KabanAssert32Bit`) in each machine csproj — do not remove it. |
| A failed `Open()` **poisons** the `ActUtlType` instance | It then returns `0xF0000003` for ever. The driver creates a **fresh control** on every retry (`RecreateControl()`). Do not "optimise" that away. |
| `0xF0000002` = station not in the MX utility; `0xF0000003` = control unusable | Different problems, different fixes. |
| PLC round trip ≈ **15 ms** | All reads are batched into ONE `ReadDeviceRandom` call and run on a **worker thread**. Never do PLC I/O on the UI thread. |
| `Kaban.Core` must not reference **WinForms** | It is the UI-free layer. Adding `using System.Windows.Forms` there breaks the whole solution build (this has happened). |

---

## 2. Solution layout

```
Kaban.Core                     no UI. Enums, models, helpers, the machine contract.
  Helpers/  PlcManager, AlarmManager, LanguageManager, SecurityManager,
            UnitManager, PlcLabelTable, PlcValueConverter, HmiLayout, UserStore
  Machines/ IMachineModule, MachineRegistry
KabanHMI  (assembly name Kaban.UI)   all shared UI
  Controls/  the HMI control family (see §4)
  Forms/     MainForm (the shell), SimulationMonitorForm, InputForm
  Screens/   StandardScreens, MachineTabScreen, MachineAlarmScreen
  Settings/  SettingsScreen + every settings page
  Dxf/       DxfDocument, DxfViewerControl (the profile workbench)
Kaban.Drivers.MitsubishiActUtl  the real PLC driver + Interop.ActUtlTypeLib.dll
Kaban.Drivers.Simulation        a fake PLC for the bench
Kaban.Drivers.Siemens           S7-1200/1500 over ISO-on-TCP  (UNTESTED on hardware)
Kaban.Drivers.Delta             Delta AS/DVP over Modbus TCP  (UNTESTED on hardware)
Kaban.Machines.FA2010           a machine: screens, Alarms.json, PlcLabels.csv
Kaban.Machines.SB3012 / SB3015  more machines
Kaban.Machines.H500 / H800      sub-machines (mountable inside a host machine)
tools/                          generators (see §8) + FA2010_DeviceList.xls
```

Each `Kaban.Machines.*` is its **own exe**. A machine can also **mount**
sub-machines, which then appear as tabs inside the host's pages.

**A machine's `Program.cs` is the one place a deployment is described** — the
drivers this exe offers, the machines it mounts, and the fallback connections.
Comment out a `MountSub` line and that unit's tabs, alarms, translations,
settings and monitor tab all disappear together. Nothing downstream hardcodes
any of it, which is what lets any machine be main or sub without editing the
machine project.

---

## 3. The shell and navigation — read this before adding a page

`MainForm` builds the left navigation from `MachineRegistry.NavigationGroups()`.

A screen is a `MachineScreen` with a **`Role`**. Screens sharing a role from
different machines collapse into ONE navigation button whose **tabs are the
machines**. That is what keeps the left bar identical on every machine:

```
Home · Manual Movement · Alarms · Machine Parameters · HMI Settings
                                                     (+ machine-specific pages)
```

- `Role = ScreenRoles.Home | Manual | Alarms | MachineParameters` → becomes a
  shared entry, with a TAB per machine named after the machine.
- `Role = ScreenRoles.HmiSettings` → emitted by the MAIN machine ONLY, so it is a
  single button with no tab strip. It is the HMI *program's* settings and one set
  serves every mounted machine; do not emit it per machine.
- `Role = ""` → keeps its own button (FA2010's **Profiles** does this).
- With no sub-machines mounted there is **no tab strip at all** — one tab would be noise.

**Do not hand-write these four screens in a machine project.** Call
`Kaban.UI.Screens.StandardScreens.Build(ctx, options)` and supply only what is
genuinely yours:

```csharp
public IEnumerable<MachineScreen> GetScreens(IMachineMountContext ctx)
{
    return StandardScreens.Build(ctx, new StandardScreens.Options
    {
        Home        = () => new Screens.HomeScreen(),
        ManualPages = new ISettingsPage[] { /* your manual tabs */ },
        MachineSettings = new ISettingsPage[] { /* your parameter tabs */ },
        Extra       = MachineSpecificScreens(ctx)   // e.g. Profiles, Role = ""
    });
}
```

**HMI Settings** (units, users, logs, station connections, PLC diagnostics, AI
Prompt, Manual, About) is ONE left-bar button, identical everywhere. **Machine
Parameters** is a second button whose tabs are the machines.

`SettingsScreen` is the ONE tab implementation in the program. Reuse it; do not
write another tab control. Tabs are outlined left/top/right and look the SAME at
every nesting level — active is white and merges into its page. The active tab is
bold with a BLACK rule drawn under the caption (never a font underline — it
slices descenders; never blue — see below).

**Blue means one thing: the border of the control the pointer is over**
(`UiStyle.HoverBorderColor`). Do not use blue for selection, emphasis or accent
fills — use weight or a neutral tone.

- Pages get it automatically: `UiStyle.ApplyInteractionCues(page)` runs from
  `MainForm.LoadScreen` and from `SettingsScreen` when a tab page is built. Do not
  wire hover per control.
- **Containers never light up.** A `GroupBoxHMI` or a `FunctionUnitHMI` frame is
  not clickable; outlining it competes with the control inside.
- A control that paints its own border implements `UiStyle.IDrawsOwnHoverBorder`
  so the walk does not outline it a second time.
- **Cursor follows Enabled** — hand while usable, arrow once disabled, so an
  interlocked button reads as dead before it is clicked. Text boxes keep the
  I-beam.

---

## 4. The control family (`Kaban.UI.Controls`)

Use these, never stock WinForms controls, on any machine screen.

| Control | Default size | Purpose |
|---|---|---|
| `ButtonHMI` | 170×36 | command button, optional sensor lamps in a side bar |
| `FunctionUnitHMI` | 360×64 | a movement: two command buttons + lamps + position readout |
| `NumericInputHMI` | 170×36 (115–150×18–26 on generated pages) | editable value |
| `NumericDisplayHMI` | 170×36 | read-only value |
| `LampHMI` | 36×36 | indicator, square, caption on the RIGHT |
| `CheckBoxHMI` | 36×36 | toggle, caption on the RIGHT |
| `GroupBoxHMI`, `ComboBoxHMI` | — | containers/selection |

Shared properties on anything PLC-bound:

- `PlcNumber` — station (1 = the machine's own PLC)
- `Address`, `DataType` (`DataTypeEnum.Bin_16` / `Bin_32`)
- `Divisor`, `DecimalPlaces`, `Digits`, `Suffix`, `UnitType`
- `RequiredSecurity` (`None`/`Operator`/`Maintenance`/`Admin`), `LogAction`
- `VisibilityConditions`, `EnableConditions`, `EnableTrigger`
- `DescriptionText` — the caption. **It is also the translation key** and falls back
  to its own text. There is no separate key property. `" :"` is appended
  automatically for LEFT-side captions only.

`FunctionUnitHMI` specifics: `CommandAAddress`/`CommandBAddress` with
`CommandAAction`/`CommandBAction` (`BitActionType.Momentary` | `Alternate`),
`Lamp1AAddress`/`Lamp1BAddress` for the sensors, `PositionAddress` +
`PositionDataType` + `PositionDivisor` + `PositionDecimalPlaces` +
`PositionSuffix`, and `Arrows` (`ButtonArrowStyle.LeftRight` default, `UpDown`, …).

### Scaling — get this right or values are wrong by 10×

Reading is **exactly** `displayed = raw / Divisor`. Decimal places are a *display
format*, not a second scaling. Writing is the single inverse, `raw = typed ×
Divisor`. (A bug where the write path applied decimals but not the divisor made
2500 store as 25 — do not reintroduce it.)

Conventions in the parameter pages:

| Kind of parameter | Divisor | Decimals | Suffix |
|---|---|---|---|
| servo / linear position | 10000 | 2 | mm |
| cylinder / clamp position | 100 | 2 | mm |
| times | 10 | 1 | sec |

### Fail-closed enabling

A PLC-bound control stays disabled until the scanner gives it fresh data. A control
with **no** PLC binding is always live (that is why navigation buttons render
normally). If you add a new binding property, include it in `IsPlcBound`.

---

## 5. PLC access

The **simulation monitor** (admin → user menu → Simulation Monitor, or F12) shows
`Label / DataType / Address / Value` with **one tab per CONTROLLER** — "FA2010
PLC1", "H500 PLC1" — built from the mounted modules, not from station numbers. A
row whose label reads `NULL` is an address bound by a screen but absent from the
device list — a useful smell. All four columns are editable (in the grid, or via
the popup on double-click); the trailing blank line adds a device; two buttons
export/import the tab as CSV. Rows can be removed from the view (Del or the
context menu; it hides the row, it does not clear the PLC image) and dragged into
any order. Order, labels and hidden rows persist under
`CommonAppData\KabanHMI\Monitor\`.

**Values write straight through**, on a real CPU as on the simulator — the page is
admin-only. Real stations are read on a WORKER THREAD via `TryReadMulti`; never
put PLC I/O on the monitor's 250ms timer.

### Which controller is which — read before touching stations

Two numbers, and confusing them costs an afternoon:

| | |
|---|---|
| **StationId** | the HMI's handle. A control's `PlcNumber`; the key in `PlcManager`. |
| **LocalNumber** | the module's OWN PLC number — the "1" the engineer says. H500's PLC1 sits on station 3. |
| **LogicalStation** | MX Component's Communication Setting Utility number, for `ActLogicalStationNumber`. |

`PlcConnectionSettings.Endpoints()` is the controller list (main machine first)
and drives BOTH the monitor's tabs and Settings → HMI Settings → **Station
Connection**, where the driver and station number/address per controller are set.
Everything is keyed `ModuleId|LocalPlcNumber`, never by station id — the station
id is the thing that page is allowed to change.

`PlcDriverCatalog` is what fills the driver combo box. Kinds are registered
EXPLICITLY in the machine's `Program.cs`, not reflected: Core and UI must not
reference a driver assembly.

- `PlcManager.Instance.GetDriver(station)` for writes (security-checked),
  `GetDriverForMonitoring(station)` for reads (works when nobody is signed in).
- `PlcScanner` (in `KabanHMI/Plc/`) collects the addresses of the **visible**
  controls, per station, and reads them in one batch on a worker thread. Results
  are applied on the UI thread.
- **A screen that swaps content in place (a tab change) must call
  `PlcScanner.NotifyContentChanged()`**, or the new controls are not in the scan
  batch and will never update. This bit us once already.

### The PLC label list

`Kaban.Machines.<M>/PlcLabels.csv` — the engineer's device list:

```
Label;DataType;Address
MAN_GirisRobotuX_Ileri;Bit;M604
IN_AcilStop;Bit;M200
```

**Every address in this file is one the HMI can bind to directly.** There is no
"mirror" concept in the running program, and you should not introduce one.

The reason it matters: the PLC copies its physical I/O into internal relays in an
ST interface block, so the machine can be re-wired without touching a screen —

```
K2M200:=K2X20;  K2M208:=K2X30;  ...  K2M510:=K2Y200;
```

`K2` = 8 bits; X/Y are **octal**, M is decimal. So the engineer's list may say a
sensor is at `X20`, but the HMI must read `M200`.

That substitution is done **once, while the CSV is built** —
`tools/build_plc_labels.py` — not at runtime. A physical point that the ST block
does *not* copy is **left out** of the CSV entirely, because binding a control to
`X…` would fail silently on the machine. So when you set a `Lamp1AAddress` or an
`OutputAddress`, take it from `PlcLabels.csv` and use it as-is.

`PlcLabelTable` (Kaban.Core) loads the file and gives you `ResolveAddress(label)`,
`LabelOf(address)` and the data type — used by the simulator monitor, tooltips and
the AI prompt page.

> Known issue in the FA2010 ST block: `K2M248:=K2X100` covers M248–M255, but the
> next line `K2M252:=K2X110` starts four bits too low, so **X104–X107**
> (`IN_Freze1Y_RefSN`, `IN_Freze1Z_RefSN`, `IN_Freze1A_RefSN`, `IN_Freze2X_RefSN`)
> are overwritten and cannot be read from the HMI. The fix belongs in the PLC:
> `K2M256:=K2X110`. `X1130/X1132/X1133` are not copied at all.

---

## 6. Alarms

- `Alarms.json` per machine, generated from the customer's workbook. Entries carry
  `PlcAddress`, `TagCode`, `Severity`, `PlcNumber`, and translations.
- **M800** is the machine-error *summary* bit, **not an alarm** — it drives the
  pulsing Alarms button and the caption reset button, and must not appear in the
  list. Alarms start at M801.
- **M102** is the momentary alarm reset on FA2010; each machine has its own.
- `MachineAlarmScreen` is used by every machine: Active / History tabs, sortable
  columns (multi-level: newest click leads, earlier sorts survive, only the leading
  column shows ▲/▼), per-machine reset, saved column widths.
- Severity and a per-alarm **helper link** live in `AlarmSettings.cfg` (Settings →
  Machine Parameters → machine → Alarm Settings) — deliberately *not* in
  `Alarms.json`, so regenerating from the workbook never discards site tuning.

---

## 7. Translations, units, security

- `KabanHMI/CommonTranslations.csv` (shared) and
  `Kaban.Machines.<M>/Translations.csv` (per machine), both
  `Key;EN;TR;RU;DE;IT;ES`, UTF-8 **with BOM**, CRLF.
- A blank cell falls back to **English**, so adding a language can never blank a
  screen. Machine translations override common ones.
- Tab captions use `Tab_<PageClassName>`.
- **Do not write these files with shell `printf`** — it emitted literal `\xc4\xb1`
  into the Turkish text once. Use Python with an explicit encoding.
- Units: values are **stored in reference units** (mm, s, °C) and only *displayed*
  in the operator's choice, so switching units never alters a machine setting.
  `UnitManager.ToDisplay` / `ToReference`. `EnsureDefaults()` runs at startup so no
  family is ever unset (an unnamed unit produced blank screens).
- Security: `SecurityManager.HasAccess(level)`. Users are PBKDF2-hashed in
  `CommonAppData\KabanHMI\Users.dat` — hash and salt only, never plaintext.
  `AuditLogger` writes daily files under `CommonAppData\KabanHMI\AuditLogs`.

---

## 8. The DXF profile workbench (FA2010 Profiles page)

`DxfDocument` + `DxfViewerControl`. Floating panes (table / details / drawing /
G-code) that the operator arranges and which persist.

Per-element metadata (`ElementMeta`) drives the cut:

- `Added` — drawn by the operator, not in the source file (bright cyan)
- `Deleted` — soft-deleted: still drawn, dimmed, out of the cut
- `Included` — false = "do not cut" (dashed amber)
- `Depth` / `DepthEnd` — cut depth, different values taper along the element
- `StartOffset` / `EndOffset` — begin earlier/later, overrun or stop short

Facts that are easy to get wrong:

- **Depth is RADIAL** — how far the cut reaches *into* the profile. The contour is
  offset inward by it and `G41` applies the tool radius, so the cutter's outer edge
  lands exactly that far inside the border. All figures on the page are **mm**.
- The program is walked **element by element**: enter at the start, cut to the
  **end**, then travel to the next element's start.
- Offsetting uses **miter joins**. Averaging edge normals falls short by
  cos(half-angle) — a 90° corner only reached 0.71 of the requested depth.
- Chaining tolerance is **0.2** mm. At 0.05 a hand-drawn bridging line silently
  failed to join and the route ignored it.
- An **explicit border** (set from a selection) remembers its *elements*, so a
  later "do not cut" rebuilds it. Storing only the point list made exclusions
  silently ineffective.
- Selection tools: click, Ctrl-click, rubber band (left = window, right =
  crossing), **select connected run**, **select between two elements**, split an
  element at a clicked point, ESC clears. A drag must suppress the `Click` that
  WinForms raises afterwards, or the selection is wiped.

---

## 9. Generators (`tools/`)

Screens for FA2010 are **generated from the engineering workbook**, not hand-typed
— 122 movement rows and 78 parameters by hand is where address slips hide.

```
tools/generate_pages.py <FA2010_DeviceList.xls> Kaban.Machines.FA2010
tools/build_plc_labels.py <label list> Kaban.Machines.FA2010
tools/io_mirror_map.py            # X/Y -> M from the ST block
```

`generate_pages.py` emits **real designer files** — `Foo.cs` (constructor) +
`Foo.Designer.cs` (fields + `InitializeComponent`) — registered with
`SubType`/`DependentUpon`, so they open on the Visual Studio design surface.
**Re-running overwrites them**: change the workbook, or drop a page from the
generator to keep it by hand.

Designer rules the WinForms designer enforces, which a generated file must obey:

1. a **private field** per control;
2. every control created in `InitializeComponent` and added via
   `this.Controls.Add(this.<field>)`;
3. `this.AutoScaleMode = AutoScaleMode.None` — these pages are hosted inside
   another auto-scaling container, and `Font` scaling **doubles up** (this made
   every generated control ~1.5× too big);
4. **nothing from another project in the `.resx`** — a resx naming
   `Kaban.Core.Models.DynamicValue` fails to compile whenever Kaban.Core has not
   built yet. `DynamicValueConverter` supports `InstanceDescriptor` so the designer
   emits a constructor call instead.

Layout convention: fill the **left column top to bottom, then continue at the top
of the next column**. Units 360×64 / 10px apart; inputs 5 digits wide, 22px apart;
columns 150px apart; captions 330px; margins 16px. Rows per column come from
`HmiLayout.PageHeight` (detected from the screen, overridable in HMI Settings).

---

## 10. Verification — run these after ANY change

The previous session kept a battery of self-checking programs. They exist because
each one caught a real bug.

```
python3 tools/../verify_projects.py    # csproj XML, references, 32-bit guard
verify_winforms.py                     # no adds into read-only control collections
verify_designer.py                     # generated pages stay designer-loadable
verify_alarms.py                       # alarm data vs the customer workbook
python3 tools/verify_plc_setup.py      # project wiring, stations, translations
python3 tools/protocol_tests/run_delta.py   # Delta driver vs a Modbus mock
sudo python3 tools/protocol_tests/run_s7.py # Siemens driver vs an S7 mock (port 102)
```

`verify_plc_setup.py` caught two real bugs the day it was written: three projects
sharing one `ProjectGuid`, and translation rows containing the `;` separator.

Lessons those checks encode — worth knowing before you "fix" something:

- `ToolStrip.Controls` is **read-only on .NET Framework** but not on Mono. Adding
  to it compiles, runs on Mono, and throws `NotSupportedException` on Windows.
- `Control.Visible` is **false whenever any ancestor is hidden**. Deriving state
  from it (e.g. "is this pane minimised?") corrupts saved layouts. Use an explicit
  field.
- A `ComboBox` bound with `DataSource` has its `SelectedIndex` **reset when it is
  added** to a parent (the CurrencyManager). Populate `Items` and select *after*
  adding.
- Re-measuring a label inside its own `SizeChanged` handler recurses until the
  stack overflows. Guard re-entrancy.
- An `AutoScroll` panel **moves its children's `Location`**. Anything that
  positions a control relative to another must measure against
  `AutoScrollPosition`, and restore `AutoSize` when there is room again.

---

## 11. Style expected of the code

- Comments explain **why**, not what — especially where a non-obvious constraint
  drove the choice (COM bitness, WinForms quirks, the mirror indirection).
- **No `// FIX:` or `// NEW:` markers.** They date a comment to a release nobody
  remembers and narrate an edit rather than stating a constraint. If the sentence
  carries a real reason, write it in the present tense; if it only says "we
  changed this", it belongs in `CHANGES.md`, not in the code.
- A comment that names a DRIVER or a station number for a particular controller is
  almost always wrong now: both are settings (HMI Settings → Station Connection).
- Never silently swallow a PLC error; log through `AuditLogger`.
- Fail closed: if data is stale or permission is missing, disable and say why in
  the tooltip.
- Tooltip layout is a contract: label (only when the screen does not show it),
  operator help, blank line, `----- For Admin -----`, then the wiring as
  `Property:<tab>Value` lines.
- New feature → add or extend one of the checking programs in §10.

---

## 12. What is still open

- Fill `RU/DE/IT/ES` for the UI screens. Alarm texts are translated; screen keys
  are mostly EN/TR with the rest falling back to English.
- **Regenerate FA2010's pages from the current device list.** The `.xls` in
  `tools/` is out of date, and only a short sample list is committed. Save the real
  list as `Label<TAB>DataType<TAB>Address`, then:
  `python3 tools/build_plc_labels.py <list> Kaban.Machines.FA2010`
  followed by `python3 tools/generate_pages.py tools/FA2010_DeviceList.xls
  Kaban.Machines.FA2010`. The generator resolves every command, lamp and position
  address through `PlcLabels.csv`, so the pages pick up the current devices and any
  X/Y become the right internal relay. It prints anything it could not resolve
  rather than binding to a dead address.
- Fix the ST mirror overlap in the PLC (§5) and re-run the label build.
- The manual (HMI Settings → Manual) is plain text; a PDF with screenshots was
  requested.
- The FA2010 **Profiles** page is wanted on other machines with small differences
  — it currently lives in `Kaban.Machines.FA2010`. Moving `ProfilesScreen` and the
  DXF workbench into `Kaban.UI` with the machine-specific parts (columns, G-code
  post-processing) behind an options object is the clean route, and has not been
  done yet.
- Page back/forward navigation does not always land on the right page.
- **The Siemens and Delta drivers have never run against real hardware.** They
  pass mock-server protocol tests, which proves the framing and not the CPU. On an
  S7: clear "Optimized block access" on any DB you read and permit PUT/GET under
  Protection — both block an otherwise correct connection. On a Delta: the
  device→Modbus address bases in `DeltaAddress.Map` are the first thing to check
  if a value reads back wrong.
- Idle auto-logout on the panel was intended and never built. `MainForm` used to
  carry `_idleLogoutTimer`, `_lastActivity` and a 10-minute constant with no timer
  wiring at all, so it never logged anyone out; the dead fields were removed and
  the intent recorded here.
- `Examples/GirisKonvExample.cs` is compiled by nothing and kept on purpose: it
  encodes the real X/Y octal and M decimal address stepping for Sikma 1..7.

---

## 13. How to work with the person

They are the machine builder: they know the PLC, the mechanics and the operators
far better than any assistant will. They report symptoms precisely and expect the
**cause** to be found rather than the symptom patched. Several times the reported
"still broken" was correct and the earlier diagnosis was wrong.

- Investigate before editing. Measure (render to a bitmap, dump the route, print
  the config) rather than re-reading code and guessing.
- If a fix cannot be verified, say so plainly.
- Tell them when the real defect is in *their* data or PLC (it has happened: the
  ST overlap, an out-of-date workbook, all-minimised layout files).
- Keep `CHANGES.md` appended, and repackage the zip after each round — the working
  container has been reset mid-session before.

