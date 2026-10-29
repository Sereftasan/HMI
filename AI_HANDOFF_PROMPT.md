# Kaban HMI — briefing for a new assistant

Give this file to a fresh chat before asking for a change. It exists so that a new assistant can work on this program
safely without the history of the earlier sessions; everything it needs is below or in the files it names.
(Brought up to date 30 October 2026; the first version was written on 29 September.)

---

## 0. How the work is done (read first)

- **The project:** `C:\Kaban Files\_Projects\_HMI\HMI` on the builder's PC; solution `KabanHMI\KabanHMI.sln`.
  `CLAUDE.md` in that folder is the working agreement - read it whole. In short:
  - A list of requests in one message is planned as numbered items: **one commit per item, a short report per item,
    the questions at the end.**
  - **Before a batch**, files in the folder newer than `_history\HMI.bundle` are the builder's own changes: take them in
    first, never overwrite them.
  - The history (branch `master`) travels as `_history\HMI.bundle`, written **last** in every batch;
    `Update_Git_History.cmd` brings it into the folder's own git for Visual Studio.
  - Requests that arrive while a batch runs go into `REQUESTS.md` ("To do"); done items move to "Done" with their commits.
  - **Every batch:** run the tests of what changed; `python3 tools/missing_keys.py` (adds the keys the code uses with an
    English text of its own) and `python3 tools/translation_uses.py` (writes `KabanHMI\TranslationUses.csv`).
  - **New texts are English and Turkish** (the other languages stay empty - English is shown).
  - Files: `.cs`, `.csproj`, `.csv` are UTF-8 **with** BOM and CRLF; `.md` and `.html` CRLF without BOM.
- **The assistant builds and tests on Mono/Linux** (xbuild; the tests under `tools/` are C# programs compiled against the
  built machine folder and run under a virtual display). What only Windows shows - the real look, MX Component, the panel's
  touch screen - is the builder's to check: say so instead of claiming it works.
- **The builder** knows the PLC, the mechanics and the operators far better than an assistant will (section 13).

---

## 1. What this is

A Windows HMI for Kaban machine tools (PVC / aluminium profile machining centres). **C# / .NET Framework 4.x / WinForms**,
built in Visual Studio. It runs on a panel PC bolted to the machine (17" / 19" 1920 x 1080 screens) and talks to
Mitsubishi PLCs through **MX Component** (`ActUtlType`, a 32-bit COM library).

The operator uses it to run the machine, jog axes and cylinders by hand, set machine parameters, watch alarms (with
step-by-step help pages for each fault), look at the machine's log on a dashboard, and (on FA2010) keep a library of
profiles and turn a DXF profile drawing into a machining program.

### Non-negotiable environment facts

| Fact | Why it matters |
|---|---|
| MX Component COM is **32-bit only** | Every machine exe must be x86 / `Prefer32Bit=true`. A 64-bit build gets `0x80040154 REGDB_E_CLASSNOTREG`. There is a build guard (`KabanAssert32Bit`) in each machine csproj - do not remove it. |
| A failed `Open()` **poisons** the `ActUtlType` instance | It then returns `0xF0000003` for ever. The driver creates a **fresh control** on every retry (`RecreateControl()`). Do not "optimise" that away. |
| `0xF0000002` = station not in the MX utility; `0xF0000003` = control unusable | Different problems, different fixes. |
| PLC round trip ≈ **15 ms** | All reads are batched (one `ReadDeviceRandom` / block reads) and run on a **worker thread**. Never do PLC I/O on the UI thread. |
| `Kaban.Core` must not reference **WinForms** | It is the UI-free layer. `using System.Windows.Forms` there breaks the whole solution build. |
| The projects are **old-style csproj: every file is listed** | A file on disk but not in its project compiles here and fails in Visual Studio. `python3 tools/check_projects.py .` checks it. |

---

## 2. Solution layout

```
Kaban.Core                     no UI. Enums, models, helpers, the machine contract.
  Helpers/  PlcManager, AlarmManager, AlarmDefinitionLoader, AlarmGeneration, LanguageManager,
            TranslationEdits, SecurityManager, UserStore, UserRights, UnitManager, ViewPreferences,
            HmiFeatures, PlcLabelTable, StructuredTypes, PlcWriteLog, PlcBackup, SimpleMotion,
            TroubleshootingCatalog (the step-by-step help), WiringTable, NotesStore, AiChats, SafeFile ...
  Machines/ IMachineModule, MachineRegistry, ScreenRoles
KabanHMI  (assembly Kaban.UI)  all shared UI
  Controls/   the HMI control family (section 4) + UiStyle (the look, tooltips, menus)
  Forms/      MainForm (the shell, in partial files: SidePanel, Touch, Features, Help, Notes, Coupling,
              Magnifier, LastPlace ...), SimulationMonitorForm (= the PLC Monitor), NumpadForm, TouchKeyboard,
              PlcPopupForm, LoginForm
  Screens/    StandardScreens, MachineTabScreen, MachineAlarmScreen, AxisMonitorPage, IoMonitorPage, NotesPage
  Settings/   SettingsScreen + every HMI Settings / Machine Parameters page
  Dashboard/  the machine log: DashboardService, DashboardScreen, LogDatabase, StateMap, MachineLogger
              (the Industry 4 logger), HmiLogStore, MachineAlarmLog, reports
  Helpers/    TouchInput, UnitCoupling, UnitCycling, AlarmHelpLinks, AI prompts, NavigationHistory, SidePages ...
  Help/       the manual (Kaban_HMI_Manual.en.html, .tr.html), drive code pages, cylinder templates (Templates\EN, TR)
  Dxf/, Profiles/  the DXF workbench and the profile library's UI
Kaban.Profiles                  the profile library: its data, geometry and machining
Kaban.Drivers.MitsubishiActUtl  the real PLC driver + Interop.ActUtlTypeLib.dll
Kaban.Drivers.Simulation        a simulated PLC for the bench (it plays the PLC program's movements)
Kaban.Drivers.Siemens / Delta   S7 over ISO-on-TCP, Delta over Modbus TCP (UNTESTED on hardware)
Kaban.Machines.FA2010           the main machine: screens, Alarms.json, Translations.csv, PlcLabels.csv, IoList.csv,
                                Wiring.csv, Popups.json, Dashboard\ (StateMap.json, Translation_JSON.csv), Help\
Kaban.Machines.H500 / H800      sub-machines (mounted under FA2010)
Kaban.Machines.MD3020 / SB3012 / SB3015   other machines
tools/                          generators and tests (section 9, 10)
```

Each `Kaban.Machines.*` is its **own exe**. A machine can **mount** sub-machines, whose pages then appear per machine.
**A machine's `Program.cs` is the one place a deployment is described** - the drivers this exe offers, the machines it
mounts, the fallback connections. Comment out a `MountSub` line and that machine's pages, alarms, translations, settings
and monitor tabs all disappear together; nothing downstream hardcodes any of it.

---

## 3. The shell - read this before adding a page

The window (`MainForm`): the caption bar at the top (alarms, coupling, connection, user, screen scale, mouse / touch,
the `?` help, language, close), the **page menu** on the left with the **machine list** under it and Back / Forward at
its bottom, the page in the middle, the **side bar** at the right edge. The manual (`KabanHMI\Help\Kaban_HMI_Manual.en.html`)
describes all of it for a user and is the best map of the program.

- The menu is built from `MachineRegistry.NavigationGroups()`. A screen is a `MachineScreen` with a **`Role`**
  (`ScreenRoles`): `Home`, `Manual`, `Alarms`, `Dashboard`, `MachineParameters` - one menu button each, shown for the
  machine picked in the machine list; `HmiSettings` - emitted by the MAIN machine only (one set serves every machine);
  `SidePage` - an icon on the side bar, opening BESIDE the page; `AxisBar` - the machine's bar (below); `""` - a button of
  its own (FA2010's Profiles).
- **Do not hand-write the standard screens in a machine project.** Call `StandardScreens.Build(ctx, options)` and give only
  what is yours: `Home`, `ManualPages`, `MachineSettings` (tabs, `GeneratedPage("Tab_<Class>", "Caption", level, () => new ...)`),
  `ManualBar` (the machine's bar above the Manual Movement tabs) and `Extra`.
- **A machine's own bar** (`MachineBarHMI`; FA2010's `Screens\Shared\AxisJogBar`: the axes' speed, JOG, steps, Safety
  Bypass, Cylinder FW/Back) is designed once and shown above Manual Movement's tabs, on the Axis Monitor and on Machine
  Parameters > Axis. **No page holds a UserControl of the machine's project in its designer file** (23 October: Visual
  Studio's designer failed on it and saved the page without it); a page has a plain Panel where it goes and its constructor
  puts the bar there.
- **HMI Settings** tabs: Users and Preferences, Logs and Reports, PLC Connections, Config Import, AI, Translation,
  Features (Admin, last). **Machine Parameters** has the machine's own tabs and **Alarms and Logs** (`AlarmSettingsPage`).
- `SettingsScreen` is the ONE tab implementation; reuse it. Tabs that do not fit wrap onto a second row.
- **Navigation history** (`NavigationHistory`): Back / Forward return to the page, machine and tab; a link (Find, a note's
  control) is one step. The program reopens where it was (`LastPage.cfg`), side pages and their windows included.
- **Side pages** open beside the page (it narrows and scrolls, nothing is covered), can go into a window of their own, and
  the main pages can be put there too (`SidePages`, `SidePanel.cfg`).
- **The screen scale** (80 - 150 %, the panel's and a user's own) redraws everything; pages are designed at 100 % with
  `AutoScaleMode.None` and scroll when they do not fit.
- **Blue means one thing: the border of the control under the pointer** (`UiStyle.HoverBorderColor`) - not selection, not
  emphasis. `UiStyle.ApplyInteractionCues(page)` does it for every page; containers never light up; a control drawing its
  own border implements `UiStyle.IDrawsOwnHoverBorder`. The cursor follows `Enabled` (hand / arrow).
- **Greyed says why**: a disabled control's tooltip, a greyed menu entry's tooltip, and by touch a tap on it
  (`UiStyle.IExplainsDisabled`).
- **Optional parts** (`HmiFeatures`, `Features.cfg`, HMI Settings > Features, both off until switched on):
  - **Industry 4 logging in the HMI** - the machine loggers and the HMI's log in the machine's database (section 6);
  - **Touch screen** - `ViewPreferences.TouchMode` is true only while it is on AND "Screen used with" says Touch.
    `TouchInput` then makes menus, rows, tick boxes, toolbar buttons and tabs a finger's size, turns a held finger into
    the right-click (never on a jog button), a tap on something greyed into its reason, a drag into scrolling, opens the
    keyboard on the screen (`TouchKeyboard`) for a text box and the keypad for a number cell. Anything a user must do
    must be reachable by a tap - never only by a right-click, a double click or a key.

---

## 4. The control family (`Kaban.UI.Controls`)

Use these, never stock WinForms controls, for anything bound to the PLC. **`docs\AI_Page_Design_Guide.md`** lists every
control with its properties, defaults, sizes and a real example - it is generated from the program
(`tools/ai_guide/make_guide.sh <built machine folder>`; its hand-written parts are `tools/ai_guide/guide_template.md`).
Make it again after a control changes.

| Control | Purpose |
|---|---|
| `CylinderUnitHMI` | a cylinder: two command buttons (A left, B right), end sensors as lamps, its `?`, alarms, coupling; `ValveType` / `FeedbackType` decide how the HMI watches it |
| `AxisUnitHMI` | a servo axis on a manual page - everything from its `AxisSettingHMI` row (`AxisNumber`) |
| `AxisSettingHMI` | one axis as one row of Axis Settings: jog, position, current, limits, reference, calibration, codes, simple motion values |
| `PneumaticPositioningHMI` | a cylinder driven to a target position, as one row |
| `CylinderEncoderUnitHMI` / `CylinderEncoderSettingHMI` | a clamp whose position an encoder counts: the unit on a manual page, the row on Machine Parameters > Encoders |
| `InverterUnitHMI` | a motor on an inverter: run lamp, value, fault code (never shown as a number; its `?` and alarm come from it) |
| `ButtonHMI`, `LampHMI`, `NumericInputHMI`, `NumericDisplayHMI`, `CheckBoxHMI`, `CheckListHMI`, `ComboBoxHMI`, `LabelHMI`, `GroupBoxHMI`, `MeterHMI`, `MeterInputHMI`, `SpeedOverrideHMI` | the rest |

Shared properties on anything PLC-bound: `PlcNumber` (the machine's own PLC number: 1 = its main PLC), the address
properties, `DataType` (`Bin_16` / `Bin_32`), `Divisor`, `DecimalPlaces`, `Suffix`, `UnitType`, `RequiredSecurity`,
`LogAction`, `VisibilityConditions` / `EnableConditions` (`SystemCondition` flags and the machine's own registered
conditions), `EnableTrigger`. Captions are translation keys (`DescriptionText`, `TitleTextKey`, `TranslationKey`), usually
the English text itself.

- **A device of the machine's other PLC**: `X12@2` (X12 of PLC 2). Never a dot - `D100.5` is bit 5 of D100.
- **Scaling** - get this right or values are wrong by 10x: reading is exactly `displayed = raw / Divisor`; decimal places
  are a display format; writing is the single inverse `raw = typed x Divisor`. (A write path that applied the decimals and
  not the divisor once stored 2500 as 25.)
- **Fail closed**: a PLC-bound control stays disabled until the scanner gives it fresh data, and says why. A new binding
  property belongs in `IsPlcBound`.
- **A movement's alarm bit** (`ErrorAAddress` ...), its help files and its unit's helpers feed the `?` and the alarm list.

---

## 5. PLC access

- `PlcManager.Instance.GetDriver(station)` for writes (security-checked), `GetDriverForMonitoring(station)` for reads.
  Every write goes through the **PLC write log** (`PlcWriteLog`, `PlcWrites_yyyy-MM-dd.csv`, device by device, with the
  page and control that wrote it - `WriteSource`). Handshakes of the machine logger are deliberately not in it.
- `PlcScanner` (`KabanHMI\Plc`) collects the addresses of the **visible** controls per station and reads them in one batch
  on a worker thread; results are applied on the UI thread. **A screen that swaps content in place (a tab change) must call
  `PlcScanner.NotifyContentChanged()`**, or the new controls never update.
- Reconnection backs off (0.5, 1, 2, 4, then every 5 s); a controller counts as connected only while reads succeed.
- **Stations**: `StationId` is the HMI's handle (a control's `PlcNumber` mapped per machine), the module's **LocalNumber**
  is its own PLC number (H500's PLC 1 sits on station 3), **LogicalStation** is MX Component's number. Everything is keyed
  `ModuleId|LocalPlcNumber`. `PlcConnectionSettings.Endpoints()` is the controller list (HMI Settings > PLC Connections,
  with each controller's **Active** tick). Driver kinds are registered explicitly in the machine's `Program.cs`.
- **The PLC Monitor** (`SimulationMonitorForm`, Admin, a side page, `F12`): any device of any controller, read and
  written; a tab per controller with the user's own tabs, arrays, structured data types (`StructuredTypes.csv`), table
  views, Find (where a device or label is used), Undo, writes from a backup. Its lists are kept in
  `ProgramData\KabanHMI\Monitor\`.
- **The PLC label list** `PlcLabels.csv` (`Label;DataType;Address`) names the devices; **every address in it is one the HMI
  can bind to directly** - the PLC copies its physical I/O into internal relays (`K2M200:=K2X20` ...; X/Y octal, M
  decimal), so a sensor at `X20` is read at `M200`. The substitution is done once when the files are built
  (`tools/build_plc_labels.py`, the `Read` column of `IoList.csv`), never at run time.
  > Known issue in the FA2010 ST block: `K2M252:=K2X110` starts four bits too low, so X104 - X107 are overwritten; the fix
  > belongs in the PLC (`K2M256:=K2X110`). `X1130/X1132/X1133` are not copied at all.
- **PLC data backups** (`PlcBackup`, `.plcbak` in `ProgramData\KabanHMI\PlcBackups`): every device of the CPU and the
  simple motion modules' flash ROM data and cams; writing one back reads a fresh backup first on a real PLC and never
  writes inputs, outputs or system devices.
- **Messages from the PLC** (`PlcPopups`, the machine's `Popups.json`; FA2010: D71 trigger, D72 answer).

---

## 6. Alarms and the machine's log

- **`Alarms.json`** per machine: each alarm's device, `TagCode`, code, severity, `PlcNumber`, helper links and `"Log"` -
  where the PLC's log records it (state code, value, bit). **Its texts are rows of the machine's `Translations.csv`**,
  `Alarm_<ModuleId>.<TagCode>` (30 October, `tools/alarm_texts_to_translations.py`; FA2010's Turkish is the Industry 4
  program's operator words). An older `Alarms.json` with texts still loads; the translation file wins.
- **M800** is FA2010's machine-error *summary* bit, not an alarm - it drives the pulsing Alarms button. Each machine sets
  its own **reset device** (FA2010: `M102`, SET - the PLC switches it off).
- `MachineAlarmScreen`: Active and History. **History is read from the machine's log database** when it keeps one
  (`MachineAlarmLog`: the PLC's error records with the bits that were on, as the HMI's alarms), with the alarms the HMI
  raised itself; otherwise the HMI's own history since it started.
- **Alarms the HMI raises itself** (`AlarmGeneration`, per machine, `AlarmGeneration.cfg`): cylinder movements not
  confirmed within their delay (codes `M840.1` ... `.6`, the failed signal), axis drive codes, inverter faults; the HMI
  SETs the device's error bit so the PLC knows.
- **Help for an alarm** (`AlarmHelpLinks`, `TroubleshootingCatalog`): the files added first, then the HMI's step-by-step
  page for that fault (built from `Help\Templates\<lang>` and the machine's `Wiring.csv`), the drive / inverter code
  entries, the manuals, then **Create AI prompt** / **Ask <AI>** (`AlarmAiPrompt`).
- **The machine's log** (the "Industry 4" log: `main_log` / `log_details`, SQLite) is read by the **Dashboard**
  (`DashboardService`, settings per machine in `ProgramData\KabanHMI\Dashboard\<machine>.json`, its state map and texts in
  the machine's `Dashboard\StateMap.json` / `Translation_JSON.csv`) and its reports. It is written either by the old
  **Industry 4 Windows service** or - with HMI Settings > Features > Industry 4 logging on - by the HMI:
  `MachineLogger` (one per machine, the service's protocol record for record at the machine's **PLC log area**, FA2010
  `R27000`, set per machine on Machine Parameters > Alarms and Logs; it waits while the service runs on the PC) and
  `HmiLogStore` (the HMI's own log in the same database, codes 1000 - 1006, left out of the dashboard and the reports).
- The **audit log** (`AuditLogger`, `HmiAudit_yyyy-MM-dd.txt`) records every change with the user.

---

## 7. Translations, units, users

- `KabanHMI\CommonTranslations.csv` (shared) and `Kaban.Machines.<M>\Translations.csv` (per machine, its pages' and its
  alarms' texts), `Key;EN;TR;RU;DE;IT;ES`, never a `;` inside a text. A blank cell shows English; a machine's file
  overrides the common one. **HMI Settings > Translation** edits them on the panel (`TranslationEdits` writes each key back
  into the file it came from), with history, undo, import, the AI prompts and "Where is it used?" (`TextUses`, from the
  source or `TranslationUses.csv`).
- Code texts: `LanguageManager.GetString("Key", "English text")` - the English is the fallback and `missing_keys.py` adds
  the row. Tab captions are `Tab_<PageClassName>`.
- **Do not write these files with shell `printf`** (it once put literal `\xc4\xb1` into Turkish); use Python with an
  explicit encoding, and keep the BOM.
- Units: values are **stored in the machine's units** and only shown in the user's (`UnitManager.ToDisplay` /
  `ToReference`).
- Users (`UserStore`, PBKDF2 hashes in `Users.dat`), levels None / Operator / Maintenance / Admin (`SecurityManager`,
  `UserRights`: who may change which account). **View preferences** (`ViewPreferences`, `View.cfg`): everyone's and each
  user's own - language, captions, numbers, dates, units, tables, tooltips, sounds, the AI prompt, Screen used with.
- **The manual** is HTML per language in `KabanHMI\Help` (English and Turkish), with each machine's own chapter in its
  project (`Help\Manual_<ModuleId>.<lang>.html`); `HmiManual.ComposeManual` fills in the machines this program runs.
  Keep the two languages' sections (ids) the same - `ui_test/AboutManual` checks it.

---

## 8. The DXF profile workbench and the profile library (FA2010 Profiles page)

Floating panes (`FloatingPaneHMI`: profiles, drawing, details, accessories, programs) the operator arranges, named
layouts. The library (`Kaban.Profiles`): vendor > series > variant > group > profile; reference points found on the
drawing and confirmed by a person; accessories placed per group; programs made only from confirmed placements.
Facts that are easy to get wrong in the drawing code:

- **Depth is RADIAL** - how far the cut reaches into the profile; the contour is offset inward by it and `G41` applies the
  tool radius. All figures are mm.
- Offsetting uses **miter joins** (averaged normals fall short by cos(half-angle)). Chaining tolerance is **0.2 mm**.
- An explicit border remembers its *elements*, so a later "do not cut" rebuilds it.
- A drag must suppress the `Click` WinForms raises afterwards, or the selection is wiped.

---

## 9. Generators (`tools/`)

- **FA2010's pages are generated from the old program's** - `tools/legacy_fa2010_pages.py` (it keeps what was set by
  hand); H500's by `legacy_h500_pages.py`. Generated pages are **real designer files** (`Foo.cs` + `Foo.Designer.cs`,
  `SubType` / `DependentUpon`) and must stay designer-loadable: a private field per control, everything created in
  `InitializeComponent`, `AutoScaleMode.None` (the screen scale scales once), **nothing of another project in a `.resx`**.
- `build_wiring.py` -> `Wiring.csv` (where each point is on the drawings), `build_io_list.py` -> `IoList.csv`,
  `build_plc_labels.py`, `build_code_help.py` (drive code pages), `build_inverter_help.py`, `build_cylinder_templates.py`
  (the step-by-step templates from `tools/cylinder_templates`), `build_emoji_art.py` (the emoji pictures),
  `alarm_texts_to_translations.py`, `missing_keys.py`, `translation_uses.py`, `ai_guide/make_guide.sh`.

---

## 10. Verification

- **Tests** are C# programs under `tools/ui_test` (one page or helper each, ~120), `tools/nav_test` (the whole window:
  navigation, side pages, touch mode, features), `tools/core_test`, `tools/lang_test` and `tools/protocol_tests`
  (Delta and Siemens drivers against mocks). Each prints PASS / FAIL lines and exits non-zero on a failure. Run the ones
  of what changed; each exists because it caught a real bug.
- `python3 tools/check_projects.py .` - every file in its project, XML in the right place.
- `python3 tools/verify_plc_setup.py` - project wiring, stations, translation rows.

Lessons worth knowing before "fixing" something:

- `ToolStrip.Controls` is **read-only on .NET Framework** but not on Mono - it compiles, runs on Mono, throws on Windows.
- `Control.Visible` is **false whenever an ancestor is hidden** - never derive saved state from it.
- A `ComboBox` bound with `DataSource` resets `SelectedIndex` when added to a parent: fill `Items`, select after adding.
- Re-measuring a label inside its own `SizeChanged` recurses until the stack overflows. Guard re-entrancy.
- An `AutoScroll` panel moves its children's `Location`: measure against `AutoScrollPosition`.
- A method named `Paint` on a Control hides the `Paint` event (it happened in October).
- "Could not find type" / "Method not found" in Visual Studio's designer: Rebuild, restart Visual Studio, clear `bin`,
  `obj` and `ProjectAssemblies`; never "Ignore and Continue" (the designer saves the page without what it could not load).

---

## 11. Style expected of the code

- Comments explain **why** - the constraint, the request it came from (dated: "30 October: ...") - not what.
- **No `// FIX:` or `// NEW:` markers.** State a reason in the present tense; history belongs in the commit message.
- A comment naming a driver or a station number for a particular controller is almost always wrong: both are settings.
- Never silently swallow a PLC error; log through `AuditLogger`.
- Fail closed: stale data or missing permission disables, and the tooltip says why.
- Tooltip layout is a contract: the label (only when the screen does not show it), operator help, a blank line,
  `----- For Admin -----`, then the wiring as `Property:<tab>Value` lines.
- Settings files are written through `SafeFile` (complete first, then swapped in).
- A new feature comes with a test (section 10).

---

## 12. What is still open

- `RU/DE/IT/ES` for the screens (alarm texts have them); new texts are EN / TR only for now.
- The touch screen on the real panel: the builder tests it (switch it on first: HMI Settings > Features).
- The FA2010 ST mirror overlap (section 5), in the PLC.
- The Siemens and Delta drivers have never run against real hardware (mock-server tests only). On an S7: clear
  "Optimized block access" on the DBs read and permit PUT/GET; on a Delta, `DeltaAddress.Map` is the first suspect.
- The FA2010 jog bar's own buttons (`Btn_SafetyBypass`, `Btn_CylinderFwBack`) have no rows in the translation files yet.
- The questions at the end of `CLAUDE.md` ("Open questions") wait for the builder's answers.
- Idle auto-logout was intended once and never built.
- `Examples/GirisKonvExample.cs` is compiled by nothing and kept on purpose (the X/Y octal and M decimal stepping).

---

## 13. How to work with the person

They are the machine builder: they know the PLC, the mechanics and the operators far better than any assistant will. They
report symptoms precisely and expect the **cause** to be found rather than the symptom patched; several times the
reported "still broken" was right and the earlier diagnosis wrong.

- Investigate before editing. Measure (render to a bitmap, dump the values, print the config) rather than re-read code
  and guess.
- If a fix cannot be verified here (Windows' look, MX Component, the touch screen), say so plainly.
- Tell them when the real defect is in *their* data or PLC (it has happened: the ST overlap, an out-of-date workbook).
- Ask when a decision is theirs (a new screen or a UI change they are not sure of: a mock-up first); otherwise take the
  conventional choice, say so, and carry on.
