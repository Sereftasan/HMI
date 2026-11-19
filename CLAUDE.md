# Kaban HMI - how we work (for Claude and for you)

## Where the code is
- **C:\Kaban Files\_Projects\_HMI\HMI** is the project (it moved there on 14 October). Solution: `KabanHMI\KabanHMI.sln`.
- Claude works on a copy in its cloud workspace (it builds and tests there on Mono/Linux), then writes the changed
  files straight into this folder - they are the newest code at once. No zip any more.
- **The history** (every commit since the first upload, branch `master`) is in `_history\HMI.bundle`, renewed with
  each batch. Claude may not write into a `.git` folder itself, so **double-click `Update_Git_History.cmd`** when you
  want the history in Visual Studio's *Git Changes* / *Git Repository* windows: the first time it makes this folder
  a git repository, later it brings it up to Claude's newest commit. It overwrites no file.
- **Your own changes**: edit freely. Before each batch Claude looks for files changed in this folder since its last
  write and takes them in first - nothing of yours is overwritten. Committing them yourself is fine too (on `master`
  or a branch of your own); say so in your request.

## Requests
- A list of everything missing, in one message, is fine: Claude plans it as numbered items, makes one commit per
  item and reports per item, with the questions it has at the end.
- Something new while Claude is working: write it in **REQUESTS.md** (in this folder) instead of sending it then -
  Claude reads it at the start of the next batch. A message sent mid-batch gets merged into the running batch.
- Screenshots and files for a request (PNG, PDF, XLS, CSV ...): into the **`_requests`** folder, their names in the
  item in REQUESTS.md. In a chat message a picture can also be pasted directly.
- A bug: the page, the user level, the screen scale, and a screenshot from the real panel when you can - Claude's
  tests run on Linux, so Windows' look is the one thing it cannot see itself.
- A new screen or a UI change you are not sure of: ask for a mock-up first ("show me before you build").

## What each batch does (12 October)
- Done every time: the code, the tests of what changed, a commit per item, the files into this folder, a short report.
- **Only when you ask**: REVIEW_2026-09-24.md / CHANGES.md, the translations (new texts are English until then),
  the manuals (`KabanHMI\Help`, the machines' `Help`), the AI page design guide, the full test suite.

## For Claude, at the start of a session or a batch
1. Stage `_history\HMI.bundle` and clone it: that is the repository (`git clone HMI.bundle work`).
2. List this folder: a file newer than `_history\HMI.bundle` (written last in every batch) is the user's own change -
   stage it and take it in (bin, obj, .vs and packages are Visual Studio's).
3. Read `REQUESTS.md` and stage the `_requests` files it names (Read shows pictures and PDFs). Work; commit per item (the trailer lines as the session says); re-add the BOMs.
4. Write the changed files here (`device_commit_files`, up to 50 a call), move the done requests to "Done", and
   last a new `_history\HMI.bundle` (`git bundle create HMI.bundle master`).

## Conventions
- Files: UTF-8 with a byte order mark for `.cs`, `.csproj`, `.csv`; Windows line endings in this folder.
- Translations: `Key;EN;TR;RU;DE;IT;ES`, no `;` inside a text.
- The general manual names no machine; a machine's own chapter is in its project (`Kaban.Machines.FA2010\Help`).
- A device of a machine's other PLC in a property: `X12@2` (X12 of PLC 2) - never a dot (`D100.5` is bit 5 of D100).
- FA2010's own controls for its pages (Screens\Shared\AxisJogBar) are UserControls of the machine's project. A page
  does NOT hold one in its designer file (23 October): to show it, Visual Studio's designer runs the control's compiled
  code, and it failed there ("Method not found: '... SpeedOverrideHMI.set_RequiredSecurity(...)'", then "The variable
  'axisJogBar' is either undeclared or was never assigned") - so the page has a plain panel where the control goes
  (Axis Settings: `axisJogBarPlace`, moved and sized in the designer), and its constructor puts the control there.
  "Could not find type ..." or "Method not found ..." on another page: Build > Rebuild Solution, restart Visual Studio;
  if it stays, close it, delete the projects' `bin` and `obj` folders and
  `%LOCALAPPDATA%\Microsoft\VisualStudio\<version>\ProjectAssemblies`, and build again. Never "Ignore and Continue":
  the designer then saves the page without what it could not load.
  AxisJogBar is also shown by the program above the Manual Movement tabs and on the Axis Monitor.
- A page as wide / high as the window (11 November): the program docks every page in the window, so a control of a page
  follows the window by its Anchor (Properties > Anchor): Top, Bottom, Left, Right - it grows with the window; Bottom
  only - it keeps to the bottom, in the middle. FA2010's Home: the job's drawing (jobBarsPlace) on all four sides,
  Start / Pause / Cancel at the bottom.
- FA2010's pages are generated from the old program's: `tools\legacy_fa2010_pages.py` (it keeps what was set by hand) -
  except Machine Settings' Options, Conveyor, Robot, Milling, Cutting and Others, kept by hand (3 November: it does not
  write them). Its wiring list: `tools\build_wiring.py` -> `Kaban.Machines.FA2010\Wiring.csv`.
- MD3020's and MK2010's pages (11 November) come from the old program's `_MD3030` and `_MK2010` projects:
  `tools\legacy_md3030_pages.py` / `tools\legacy_mk2010_pages.py` write them from their tables (tools\legacy\MD3030,
  tools\legacy\MK2010 - the READMEs say which old page is which new one and what looks wrong there), a table a page's
  columns and rows of the program's own controls (the machinery: `tools\legacy_grid_pages.py`); `--check` compares the
  devices, the buttons' actions and the values' divisors with the old pages. They keep what was set on a control by hand
  (found by its name). The manual pages' bar: each machine's `Screens\Shared\JogBar`. MK2010 is a project of its own
  (in KabanHMI.sln, its own program like MD3020's). 12 November: their axes are axis settings rows (`AR`, the spec's
  AXES table: number, motion module and axis, reference sensor, servo alarm) and axis units on the manual pages (`Ax`);
  their cylinders cylinder units (`Cy`: valve and feedback from the drawings, error bits from the alarm list - which
  the spec also writes, ALARMS -> Alarms.json and its texts). A valve island's valves are CylinderValve Island... (5 - 8).
  SB3015's (17 November) the same way from the old `_SB3015` (`tools\legacy_sb3015_pages.py`, tools\legacy\SB3015 - its README
  lists what to check: no drawings, the units' types read from the old pages): its motors on an inverter are inverter units (`Iv`),
  its clamps with an encoder FA2010's encoder rows and units (`Enc` on Machine Parameters > Encoders, `EncU` on a manual page),
  its front verticals pneumatic positionings (`PPos`, Machine Parameters > Positioning).
- Machine Settings' rows (3 November): a page of settings is rows, UiStyle.SettingRowHeight (38) high, touching, a light
  line under each (UiStyle.RowRule, lighter than a table's GridRule) - every row 450 wide (the user's choice; a frame 474,
  its rows 12 in) - a switch: CheckBoxHMI SwitchRow; a value: NumericInputHMI.RowWidth (its caption is the row, words at the
  left, the field 8 px in from the right end). The Axis and Encoders tables: the same light line between their rows (UiStyle.RulesBetweenRows).
- The profile library, schema 3 (6 November): an ACCESSORY GROUP attached to a PROFILE GROUP (group_accessory_group; the Profiles page's
  Accessories + menu, the library editor's "Accessory groups of the profile groups") - every accessory of it, those added later too, on every
  profile of the group; the resolver and the counts read v_group_accessory (one by one or with their group; switched off for the group: off).
  profile_machine_setting: a profile's machine settings (FA2010's profile parameters) per machine instance.
  FA2010's Profile groups page (11 November, "closer to" the old program's Preparation > Group Mapping and Company Profiles' toolbar):
  a tree of brand > series > profile group > its profiles / accessory groups / accessories one by one; a group's accessory groups and
  accessories ticked on the right (one of a ticked accessory group unticked: off for that group only - group_accessory_assignment
  is_enabled 0); the library's tables from its buttons (Profile brands = vendors, Profile series, ...).
- FA2010's job list (6 November, the Jobs page): Send to the machine writes the profile's parameters (R25600), the milling operations'
  (R26000 + 10 each), the special cuts' (R25650 + 5 each), the bar's length and first / last right cut (D106, D95, D97) and the job list
  (D5000 / D6000 / D7000, 999999 last). Codes: a cut 1000 + 10 x angle (2800 - 10 x angle at a right end), a milling operation 1 - 100, a special
  cut 1000 + its number (1001 - 1010), a V cut 3000. On the PLC's request (JobMilling): L101 + D60 -> R0 / L102, L103 + D61 -> R12800 / L104
  (the milling steps), L105 + D65 -> R25700 / L106 (a special cut's steps). The special cut recipes (the old program's saw receipts, formulas of
  T D W O L R - the device list's "Kırlangıç Kesim" sheet) are the machine's settings in the library, edited on the Profiles page's
  Accessories pane, tab Special cuts (7 November: "just like the accessories"; entered, the old program's tables imported as CSV, or the device
  list's samples - Kaban.Machines.FA2010\SpecialCutSamples.csv, made by tools\special_cuts_from_devicelist.py).
  The profile's mullion notching steps (7 November, per profile: the profile machine setting "Mullion Notching Steps", the profile
  details' tab Mullion notching, its group's template's when it has none of its own) go with each bar into R25700 before its parameters
  (21 steps of 10 words, x100, as the old program's WriteNotchInfo) - the same area the saw's special cut steps come into on L105.
- FA2010's PlcLabels.csv is kept by hand since 22 October (tools\build_plc_labels.py would lose its tail): the structured labels of
  FA2010_DeviceList's Profiles & Operations (StructuredTypes.csv: Profile Parameters R25600 with R25615 Profile Correction Position, the job
  list D5000 / D6000 / D7000, milling parameters and steps, special cuts, mullion notching) and every PC_ device of its ManualMovement sheet,
  under "#heading;" lines - the PLC Monitor's collect menu "what the HMI writes to the PLC" puts them in the tab "HMI -> PLC" (6 November).
- The program's lines naming each translation key (HMI Settings > Translation, "Where is it used?" on a panel without
  the source): `tools\translation_uses.py` -> `KabanHMI\TranslationUses.csv`, made again in every batch.
- The keys the code uses with an English text of its own and the translation files lack (23 October: tooltips shown in
  English only, not in the Translation table): `tools\missing_keys.py` adds them with their English, every batch - their
  other languages stay empty (English shown) until translated.
- Alarm texts (30 October): rows of the machine's `Translations.csv`, `Alarm_<ModuleId>.<TagCode>` (moved from
  `Alarms.json` by `tools\alarm_texts_to_translations.py`, the Industry 4 program's Turkish with them). `Alarms.json`
  keeps the alarm - device, code, severity, help - and `"Log"`: where the PLC's log records it (state code, value,
  bit). An older installation's `Alarms.json` with its texts still reads; the translation file's text wins.
- Optional parts (30 October): HMI Settings > Options (was Features; the machine's Options tab's key, Tab_Options) - the
  Industry 4 logging in the HMI and the touch screen, a row each with a switch, both off until switched on; what they do
  is in the manual (9b), not on the page. A machine's logging: its Machine Parameters > Options, the Industry 4 Logging frame
  (5 November: the alarms page is "Alarms" again; its PLC log area is the module's DashboardDefinition.LogArea, set by the
  PLC programmer - 5 November: never a setting on the screen) - with a switch row on each record's PLC bit (FA2010: codes 0 - 6,
  8, 15, 16, 20, 22; L1000 + code), the PLC keeping them; 0 - 2 (the machine's states) always logged: the HMI switches them
  on as it connects (MachineLogger.WriteRequired). FA2010's records: docs\industry4\MachineLog_DeviceList.xlsx.
- Older and new machines (5 November): HMI Settings > Options, "Machine details from the HMI (new machines)" (HmiFeatures.MachineRecords,
  only with the Industry 4 logging on). Off, an older machine: its PLC logs the details. On, a new machine: the HMI takes them itself -
  MachineRecords (codes 1100+ in the machine's log database, named kinds - never the PLC's old codes; the dashboard lists them as events,
  StateMap.AddHmiRecords names them): slow / not confirmed cylinder movements and their times per shift (normal times learned from the first
  20, MovementNormals.cfg), axis alarms and overloads (Md.109 - 111 read only then), inverter faults, high motor loads and loads per shift
  (InverterUnitHMI.CurrentAddress - none on FA2010 yet), and why the machine stood still (StopReasons: a window after a 5-minute stop, from the
  PLC's state records). Switched and limited in the Industry 4 Logging frame (DashboardConfig "hmi_records"). The PLC keeps the states and the
  cuts on both. The plan: docs\industry4\HMI_INDUSTRY4_PLAN.md.
  A machine's own records on a new machine (5 November, the PLC program's MOV K16 / DMOV / CALL MakineLogKayit done by the HMI):
  LogRecordHMI rows in the Industry 4 Logging frame - RecordCode (the PLC's code: the dashboard treats it as that record; or 1200 - 1299),
  Trigger (a PlcTrigger as EnableTrigger; a bit ON with ResetTrigger - the default, 6 November - is a HANDSHAKE: the PLC holds it, the
  HMI takes the record and turns the bit off once it is in the database, 5 s at most if the database does not answer), Values (RecordNumber / RecordLamp, written into the designer file as
  "Values.Add(new RecordNumber(...))"); discovered with the pages (RecordTriggers), shown instead of the PLC's switch of the same record
  (MachineLogSettings.Arrange). FA2010: 6, 15, 16, 20, 22 - 6 and 20 come with L1006 / L1020 (the user's answer: the records' devices in
  MachineLog_DeviceList.xlsx); the measured width / height D481 / D482; FireBoyu, BoyOkundu, ProfilOlcum_OK, Profil_Dogru, the profile code
  and the required switch state not yet given devices (the user sets them in the designer).
- Tick boxes (30 October, the user's choice): on / off switches. CheckBoxHMI is a switch row by default (Look; narrower
  than 120 px: the switch alone, its caption beside it); every plain CheckBox is drawn as a switch (TouchInput.Sizes,
  PaintSwitchBox). Lay a list of options out as rows: as wide as the column, 38 - 46 high, touching.
- A control's caption is part of it (3 November, the user's choice): ActiveArea Row, the default of every NumericInputHMI
  and CheckBoxHMI - the pointer on the caption lights the field (the switch) and a click there is a click on it. Set
  Control (only the field or the switch answers, the caption is words) only where a page needs it; no page does now.
  A switch slides (a sideways drag: right ON, left OFF, never asked - ConfirmWrite asks only before a tap) on the switch,
  its row, or the caption beside a small one (3 November: "on the small switches as well"; touch: no page scrolls then).
- The keyboard on the screen (2 November): along the bottom of the screen, across its width; while it is there KeyboardRoom
  fits the maximised windows above it (the side panel, the page, the side page scroll there), scrolls the box into sight and
  moves a dialog above it. Windows' On-Screen Keyboard (WindowsKeyboard, osk.exe; the default), the HMI's own
  (TouchKeyboard) or Off (a keyboard of keys) - ViewPreferences.ScreenKeyboard: the top bar's mouse / finger menu and
  Users and Preferences > Screen (everyone's or a user's own). osk is placed by HKCU\Software\Microsoft\Osk before it starts;
  once open, Windows lets no ordinary program move or close it (uiAccess) - it stays until its own close button.
- A finger (2 November, a real touch screen): Windows gave a finger's mouse press only at its lift. In touch mode every
  window has all of Windows' gestures off (SetGestureConfig, id 0), and a hold-to-run ButtonHMI takes the finger's own
  pointer messages (TouchInput.Fingers: WM_POINTERDOWN ... UP) - pressed at the touch, released at the lift.
  Windows still held some presses back (the log: 304 ms), so the long press for a MENU is timed from the finger's
  WM_POINTERDOWN too, and its lift kept from Windows when shown so. No tooltip for a press, ever (2 November: pressing a
  hold-to-run button to see its tip would run it) - a tap on something greyed says why; tooltips: Notes and feedback. Two fingers zoom a control that is
  TouchInput.IPinchZoom (the dashboard's time line, which also has - / + / whole-range buttons). In touch mode a splitter
  given SizeGrips (the side page's border) is wider with one grip at its middle (a border sizing one way needs no corner
  grips); a box sized from its frame (SizeGrips.ForBox: a side page's own window, a FloatingPaneHMI) gets a wider frame with
  a grip at each corner (both ways) and the middle of each side.
- The cylinder step-by-step page (2 November): safety notice, then the parts of the movement with a drawing per part
  (Drawings.html Schem*, PartRows), then what the HMI saw - the sensors only (the valve's outputs drop with the alarm);
  no "where to start" / "how this page goes" (step 1 branches).
- An encoder the machine has only with an option, or one input switched between two encoders (30 October):
  CylinderEncoderSettingHMI.EncoderConditions (FA2010: L33, L34) and MeasuredConditions (Y120 / M450, the left or the
  right profile recognition unit). New machine conditions go after the last one (FA2010: bits 22 - 27 now), never between.
- Barcodes and labels (17 November, from the old KabanMachinesSystem): Kaban.Core\Barcodes (BarcodeReaders - Barcodes.cfg: the
  readers, the parts of a code; a page that wants codes registers with BarcodeReaders.Take, the newest asked first; BarcodeTargets: a
  machine's job size from a code - MK2010 / MD3020 register theirs in RegisterPlc) and Kaban.Core\Labels (LabelStore - ProgramData\KabanHMI\Labels\*.label;
  LabelWriter - EPL exactly as the old ZebraPrinterSender wrote it, or ZPL; LabelPrinters - COMn, Windows printer RAW, address:port, file:;
  LabelService - prints on a thread of its own). Kaban.UI: BarcodeHost (opens the readers, catches a typing scanner, notes), LabelTriggers
  (a PLC's label request - bit + value, the bit turned off; a bit that is also a LogRecordHMI handshake is left to the record and printed
  from RecordTriggers' edge), HMI Settings > Barcodes / Labels. FA2010's JobLabels: the step IDs (D5000) of each bar sent -> its pieces'
  values (JobLabels.cfg), its requests L2 + D386 and L24 + D388 / 10 registered OFF.
- HMI Settings > Maintenance (17 November, the old Cleaning Settings): MaintenancePlan (Maintenance.cfg) - a due task is an HMI alarm
  (HMI.MAINT.<id>, a warning), back 4 hours after an acknowledgement while not done. Remote support: the "?" menu (RemoteSupport -
  Supports\KabanSupport.exe / AnyDesk.exe beside the program). FA2010's transfer folder: JobInbox (JobInbox.cfg) - the Jobs page's
  inbox button sets it (Maintenance).
- The old program analysed as a whole (19 November): docs\KabanMachinesSystem_Analysis.md - every part, where it is now, what is next
  (the H500 screwing job, waste mode, the machines not brought over). FA2010's bar handshake (JobSender: after a bar L10 "Son Bar Degil",
  L0 "Is Gonder" off, L1 "Is Geldi" on) and JobRun (the Jobs page's Automatic: L0 while M21 asks for the next bar; D91 the bar's seconds).
  ProductionLog (ProgramData\KabanHMI\Production\yyyy-MM.csv; HMI Settings > Production, the machines' counters - ProductionLog.Counter),
  DataBackup (HMI Settings > Backup, DataBackup.cfg), job files' Screw / Hole lines (KbnPiece.Screws), the transfer folder's Archive.

## Open questions (answer here or in a message)
(none now)

Answered (30 October): the back horizontal moulds' reference sensors (X12@2, X13@2, X14@2) are at the end of the BACK
movement - as the program has them. Milling > Front Clamping has its encoder (D20) only with L33 (the older machines);
Cutting > Right Front Vert. Clamping (D7) only without L34; Entry Conveyor > Profile Clamping 7 is the left profile
recognition unit's front clamping, measured by the width encoder through K77 while Y120 selects the left unit (the
right unit is the default) - each built so. Cutting > Right Front Vert. Clamping's position without L34 is the
analogue sensor SN115 (wire 705, U19 FX5-8AD channel 6, sheet 34.5). L35 makes the Milling / Cutting Front Clamping's
valve a 5/3 open centre (CylinderUnitHMI.OptionValveType / OptionValveConditions).
- A machine condition judged off its page (a unit's help from the alarm list): MachineConditionMap.ReadAlways (read on
  every scan) and MetNow.
- A machine condition of several devices (7 November): MachineConditionMap.AddDevice / AddMachineBitDevice after the condition is
  registered (on its PLC) - met while every device's test is (ConditionRule.AnyDevice: one); the PLC Monitor shows a line for each
  further device under the condition's (WatchRow.ConditionPart, "part=" in the list).
  A device compared with another device (10 November: "D100 >= D700"): the registration's last argument a device (a text) instead of a
  number - ConditionRule.CompareAddress, read in the condition's data type from its PLC; FA2010's example RobotXPastReference (bit 27,
  W0 >= D316). The PLC Monitor shows the device compared with on a line of its own (ConditionCompare, "cmp=1").
- One copy of the HMI at a time (5 November, the user's choice): every machine's exe holds the mutex `Global\Kaban.HMI.OneCopy`
  (Helpers\SingleInstance, right after AppBootstrap.AwaitPreviousInstance in each Program.Main); a second start brings the running
  one to the front, says why and ends; a restart's new instance waits for it (the old one lets it go just before starting it).
  6 November ("make sure the application get closed before opening again"): never beside another copy - a restart waits 15 s for the old
  process and ENDS it if it lingers (its window closed and everything saved by then); a closed window's process ends within 5 s
  (AppBootstrap.EndSoonAfterClose, MainForm.EndAfterClose set by each Main); a start by hand waits up to 10 s for a copy that is closing.
- "Unable to copy ... Kaban.Machines.H500.exe ... locked by: Kaban.Machine.FA2010": an instance of the program is still
  running - close it (Task Manager: Kaban.Machines.FA2010.exe) and build again. A Restart of an instance Visual Studio
  debugs starts the new one outside the debugger; it says so when it starts. The FA2010 project checks first now
  (KabanHmiNotRunning) and says so in plain words.
- The AI prompts say how an alarm leaves the list (HmiAiPrompt.AlarmListRule): held by the PLC until reset.
