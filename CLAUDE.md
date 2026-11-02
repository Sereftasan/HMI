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
- FA2010's pages are generated from the old program's: `tools\legacy_fa2010_pages.py` (it keeps what was set by hand).
  Its wiring list: `tools\build_wiring.py` -> `Kaban.Machines.FA2010\Wiring.csv`.
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
  is in the manual (9b), not on the page. A machine's PLC log area: its Machine Parameters > Alarms and Logs.
- Tick boxes (30 October, the user's choice): on / off switches. CheckBoxHMI is a switch row by default (Look; narrower
  than 120 px: the switch alone, its caption beside it); every plain CheckBox is drawn as a switch (TouchInput.Sizes,
  PaintSwitchBox). Lay a list of options out as rows: as wide as the column, 38 - 46 high, touching.
- The keyboard on the screen (2 November): along the bottom of the screen, across its width; while it is there KeyboardRoom
  fits the maximised windows above it (the side panel, the page, the side page scroll there), scrolls the box into sight and
  moves a dialog above it. Windows' On-Screen Keyboard (WindowsKeyboard, osk.exe; the default), the HMI's own
  (TouchKeyboard) or Off (a keyboard of keys) - ViewPreferences.ScreenKeyboard: the top bar's mouse / finger menu and
  Users and Preferences > Screen (everyone's or a user's own). osk is placed by HKCU\Software\Microsoft\Osk before it starts;
  once open, Windows lets no ordinary program move or close it (uiAccess) - it stays until its own close button.
- A finger (2 November, a real touch screen): Windows gave a finger's mouse press only at its lift. In touch mode every
  window has all of Windows' gestures off (SetGestureConfig, id 0), and a hold-to-run ButtonHMI takes the finger's own
  pointer messages (TouchInput.Fingers: WM_POINTERDOWN ... UP) - pressed at the touch, released at the lift.
  Windows still held some presses back (the log: 304 ms), so the long press (tooltip, menu) is timed from the finger's
  WM_POINTERDOWN too, and its lift kept from Windows when shown so. Two fingers zoom a control that is
  TouchInput.IPinchZoom (the dashboard's time line, which also has - / + / whole-range buttons). In touch mode a splitter
  given SizeGrips (the side page's border) is wider with one grip at its middle (a border sizing one way needs no corner
  grips); a box sized from its frame (SizeGrips.ForBox: a side page's own window, a FloatingPaneHMI) gets a wider frame with
  a grip at each corner (both ways) and the middle of each side.
- The cylinder step-by-step page (2 November): safety notice, then the parts of the movement with a drawing per part
  (Drawings.html Schem*, PartRows), then what the HMI saw - the sensors only (the valve's outputs drop with the alarm);
  no "where to start" / "how this page goes" (step 1 branches).
- An encoder the machine has only with an option, or one input switched between two encoders (30 October):
  CylinderEncoderSettingHMI.EncoderConditions (FA2010: L33, L34) and MeasuredConditions (Y120 / M450, the left or the
  right profile recognition unit). New machine conditions go after the last one (FA2010: bits 22 - 26 now), never between.

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
- "Unable to copy ... Kaban.Machines.H500.exe ... locked by: Kaban.Machine.FA2010": an instance of the program is still
  running - close it (Task Manager: Kaban.Machines.FA2010.exe) and build again. A Restart of an instance Visual Studio
  debugs starts the new one outside the debugger; it says so when it starts. The FA2010 project checks first now
  (KabanHmiNotRunning) and says so in plain words.
- The AI prompts say how an alarm leaves the list (HmiAiPrompt.AlarmListRule): held by the PLC until reset.
