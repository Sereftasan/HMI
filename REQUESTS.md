# Requests for Claude

Write here what is missing or wrong, whenever you notice it - one line or a paragraph each, in any order.
Claude reads this file at the start of every batch, does the items, and moves each one to "Done" with its commit.

## To do

## Done
- 2 November, evening (the menu's words; Off; the keyboard in Users and Preferences; the preview):
  - fdf631a - The mouse / finger menu's words: Mouse, Touch; On-Screen Keyboard: Windows', HMI's (Turkish too).
  - fb40541 - On-Screen Keyboard: Off - a tap on a text box brings no keyboard (a keyboard of keys used); a number still
    brings the keypad.
  - 371177c - Users and Preferences > Screen: On-Screen Keyboard (Windows', HMI's, Off) with its preview - everyone's or
    a user's own; the top bar's menu keeps a user's own and says so.
  - 6ea195e - The preview beside a list again when its entries are pointed at: in touch mode a list opens as a finger's
    menu, which did not tell the page which entry was under the pointer.
- 2 November, later (the keyboard's letters; the program above it; Windows' own keyboard):
  - af035ea - The letters on their keys again (on Windows they stood at the left end, away from the keys); the keys across
    the whole bar.
  - 0a5f28b - The program laid out above the keyboard, as on a screen that much lower: the main window (and a page in a
    window of its own), maximised, ends at its top - the navigation panel, the page and the side page scroll above it; the
    box scrolled into sight; a dialog (the login) moved above it whole. All back when the keyboard goes.
  - 210c515 - Windows' On-Screen Keyboard as the keyboard on the screen (the default; the HMI's own is a choice in the top
    bar's mouse / finger menu, "Keyboard on the screen"): started along the bottom of the screen, the program above it.
    Once it is open Windows lets no ordinary program move or close it - it stays until its own close button is tapped.
- 2 November (the keyboard on the screen):
  - 2bf9901 - The keyboard always along the bottom of the screen, as wide as it, its keys in the middle; the page scrolls until the
    box you type into is above it (a dialog moves up), and back when the keyboard goes.
- 30 October, late (the build error again; the AI helper on alarms; the switches everywhere):
  - 5e22dc1 - The build's "Kaban.Core.dll ... being used by another process": the HMI still running from bin\Debug - the
    FA2010 project now says so first, in plain words (close it, Task Manager: Kaban.Machines.FA2010.exe).
  - a63c8b8 - The AI prompts: an alarm stays on the list until it is reset, even when its cause is gone (the PLC holds it).
  - 3b0edf6 - Your choice: every tick box an on / off switch - CheckBoxHMI a switch row by default (the Options pages all
    rows), every plain tick box of the program a switch.
- 30 October, night (the answers; the Options page's looks; touch menus; the locked build):
  - bd3cff9 - Answer 1: Cutting > Right Front Vert. Clamping's position without L34 is the analogue sensor SN115 (wire 705,
    U19 FX5-8AD channel 6) - its wiring row and step-by-step help say so.
  - 1b00842 - Answer 2: with L35 the Milling / Cutting Front Clamping's valve is 3WayOpenCenter - its "?", the alarm list's
    help, the AI prompt and the HMI's own alarm follow L35 (read on every scan).
  - 5534b58 - Answer 3: Machine Parameters > Options in four looks side by side to choose from - switch rows, tiles,
    switches beside their names, the tick boxes (Mullion Notching).
  - 05f0e0c - Touch: a menu entry's words in the middle of its row (the Program Exit menu's Close / Restart).
  - c95ebb4 - The build's "locked by Kaban.Machine.FA2010": a running instance holds the exes; a Restart of an instance
    Visual Studio debugs now says that it runs outside the debugger.
  - 86df0db - Test PagesDesignable up to date (the Axis page's bar 76 high).
- 30 October, evening (the clamps' encoders; the Options page; touch):
  - 382059a - Machine Parameters > Options: Cutting Left Front with Encoder (L33), Cutting Right Front Vertical without
    Encoder (L34), Cutting Left Front with Air Exhaust (L35), in Turkish as the PLC's labels name them.
  - 887bb89 - The clamps follow them: Milling > Front Clamping has a position only with L33, Cutting > Right Front Vert.
    Clamping only without L34 (else plain cylinders, their rows not on the Encoders page); the profile recognition's
    width / height read only by the unit selected (Y120 the left one, the right the default) - the other shows "–" and
    its tip says when it is read; Profile Clamping 7 is the left unit's front clamp. The HMI's own alarms follow.
    Wiring.csv: the left width encoder on P13; the older machines' X0 / X1 encoder with L33.
  - 812a3fa - The mouse / touch button and its menu: line drawings, as the other drop-down menus' pictures.
  - 894f7fa - HMI Settings > Features is "Options" (the machine's tab's words), a row per option with a switch at its right
    end, the description notes gone from the page (they are in the manual, 9b).
  - dbd501c - "Screen used with" appears (and goes) on Users and Preferences as the touch screen is switched - no restart.
  - d2dbe55 - Touch: the keyboard on the screen stays (it made itself the active window and so lost the text box).
  - 3b76e31 - Touch: a finger held still on a control shows its tooltip (no tooltips by a resting pointer in touch mode).
  - 58ee0b0 - Touch: a finger drag scrolls steadily - a finger's tremble no longer shakes the page or flips a table's row.
- 30 October, later (the answers to the questions):
  - d1fcb7b - FA2010's bar: "Cylinder FW/Back" is "Automatic ON/OFF" (Otomatik ON/OFF), the button a little wider so
    "Automatic" fits; Safety Bypass in Turkish (Güvenlik Bypass); the manuals, the AI prompt and the log with the new name.
  - b5d8127 - Features > Touch screen: it says whether this PC's screen has a touch function - only then the mouse /
    finger button on the top bar and "Screen used with" (as it already worked); the texts and the manuals say so.
  - The back horizontal moulds' reference sensors at the back movement's end: as the program has them, nothing changed.
  - The question on the clamps' positions asked again, with what the drawings show (CLAUDE.md, Open questions).
- 30 October (Industry 4 logging and touch optional; the answers to 29 October's questions):
  - f9d31e4 - HMI Settings > Features (Admin, the last tab): Industry 4 logging in the HMI and the touch screen, each
    with On / Off, what it does and what it does now - both off until switched on. Logging off: the Windows service
    logs, the HMI's log is in its files. Touch off: a mouse, no mouse / finger button, no "Screen used with".
  - 2f2ef3c - Machine Parameters > "Alarms and Logs" (the Alarms tab renamed): each machine's PLC log area (R27000 for
    FA2010), the KMSystem lookup and what its logger does - shown while the HMI logs.
  - d8d9aea - Question 1, yes: the alarm texts moved into each machine's Translations.csv (Alarm_<machine>.<tag>), the
    Industry 4 program's Turkish with them; Alarms.json keeps the alarm and where the PLC's log records it.
  - e3124e8 - Alarms > History read from the machine's log database (kept across restarts), with the alarms the HMI
    raised itself.
  - 06d05c5 - Question 2, yes: the English manual brought up to date; the whole manual and FA2010's chapter in Turkish.
  - 9380117 - Question 2: AI_HANDOFF_PROMPT.md rewritten for the program as it is; the AI page design guide made again
    (InverterUnitHMI, the machine's bar, touch).
  - 3ff17ba - The new texts in Turkish.
  - Question 3 (the touch test on the panel): yours to report - switch the touch screen on first (HMI Settings > Features).
- 29 October (the Industry 4 logger in the HMI; touch screens):
  - f718f75 - The machine's log taken from its PLC by the HMI (the Industry 4 service's job): the same protocol (R27000,
    10-word records, chains, bits, 32-bit values, multipliers, Active devices, the KMSystem Process ID lookup) into the
    same database and tables; it waits while the Industry 4 service runs on the PC. Dashboard settings: on / off, the log
    area, what it does now, KMSystem.
  - 57b7c6d - The HMI's own log in the same database and tables ("Same file, same tables"): codes 1000 + its kind
    (1001 sign-in ... 1006 a device written), left out of the dashboard, its tables and the reports - their formats as
    they were; All logs reads it back.
  - f114882 - Mouse or touch: View Preferences > Screen > Screen used with, and the mouse / finger button on the top
    bar beside the screen scale; in touch mode Windows' press-and-hold and pan are off (a jog held is held).
  - c3f15b2 - Touch: menus, table rows, tick boxes, toolbar buttons and tabs a finger's size.
  - 6aa34b2 - Touch: a long press opens the menu, a "⋮" on a table's row menu, a tap on something greyed says why,
    closed lists as large menus; Find in the notes opens on a tap.
  - c8ef793 - Touch: the keyboard on the screen (Turkish Q) when a text box is tapped; the keypad for number cells
    and number boxes.
  - 6b3d96f - Touch: a finger drag scrolls tables, lists and pages; a tap chooses the row.
  - 33861bc - The new texts in Turkish.
- 26 October, evening (the manual and the AI prompts):
  - c541acf - The HMI manual brought up to date with everything from 12 to 26 October: the notes page as redesigned,
    coupling and Cylinder FW/Back, motors (inverters), the help pages (one page per fault, files added first), the speed
    and screen scale sliders, the language preference, greyed menu entries, PLC data backups (7a), the Active tick,
    Config Import's compare, the Translation tab (9a), the axis pages' positions and module parameters, the PLC
    Monitor's structured types, tables and writes from a backup, the new files.
  - 621c77c - FA2010's chapter: the Home page, the bar above Manual Movement (speed, JOG, steps, Safety Bypass,
    Cylinder FW/Back), the motors, Robot X's Load Position, its structured data types.
  - 1bd99a1 - The AI prompts: the page on screen told as it is (button captions, the speed, positioning and encoder
    rows, axis rows, the machine's bar), coupling and Cylinder FW/Back, controllers not active, the levels as they are
    now; the alarm prompt's pages and its help list (added files first, each said added or the HMI's own).
- 26 October, afternoon (Notes and feedback):
  - d579ad9 - "There is an unsaved note entry." with Save it / Discard it / Cancel - asked as soon as Add is pressed;
    Remove takes every note chosen in the table (the ones the user may remove).
- 26 October (Notes and feedback):
  - 57735af - The page redesigned as the others: the notes in a table, Add / general note / View / Edit / Find / Remove /
    feedback file on top of it, with filters (search, kind, state, who); the note or the control picked in the Details tab.
    Add turns picking on (one control, Esc or a click on the page gives up); what was typed is asked about before it is
    thrown away; a note is changed or removed by its author or a level above; Find opens its control's page.
- 26 October (the answers to 24 October's questions):
  - 31d8e23 - The alarm list's Help: the files added listed first, then the step-by-step page (its name as before).
  - 38ca02d - Axis Monitor: Maintenance sees the PLC axis items and Motion monitor lists greyed (an Admin's choice); an
    Operator does not see them.
  - Translations: English and Turkish only for now (as said) - a new text gets its Turkish in its batch.
- 24 October:
  - 67bb4e3 - PLC Monitor, "Table viewed labels": the toolbar works there (up/down greyed at the first/last, add, copy,
    paste, delete); a comment line or another table above or below the one chosen (its menu too).
  - 9f0f1d1 - Add an array: its count fixed at the table's length (greyed); the editor's captions seen whole ("Arrays (how many)").
  - 1a573d4 - PLC Monitor: a table no longer blinks (drawn again only when a value shown changes).
  - 2671e9c - Write a backup into the PLC: Points + Start (the first device read, how many, the first device written to), as
    GX Works3 - or Start + End as before; the choice kept.
  - 2a2ecc1 - A unit's "?": the step-by-step page named as its error (the alarm's text), the files added listed first.
  - 46a5b09 - Menus: an entry that cannot be used is shown greyed, why in its tooltip (the PLC Monitor's row menu, the
    headings' menu, PLC Connection below Admin, the side pages, a unit's AI entries, Find, a motion value's menu).
  - 73fa0df - Translations: the new texts in Turkish (619); the other languages show English until translated.
- 23 October (evening):
  - b7f3fcb - PLC Monitor: "all under the next control / page - into this tab" - the rest of the screen darker, everything
    under the control, page button or tab title clicked added to the tab on show; a blinking cancel button (Esc too).
  - ceb144c - PLC data Read: the cam numbers typed for a PLC offered again.
  - b2fc50a - Safety backups: before a write into a real PLC with no backup of today, its data read and saved first (not
    the simulator); a translation file kept (ProgramData\KabanHMI\SafetyCopies) the first time it changes each day.
  - 876adc9 - PLC Monitor: "Write to PLC from backup" (the toolbar, the rows' menu) - the selected labels, a tab's or every
    tab's, from a backup chosen; only their devices; a real PLC's backup of today taken first.
  - 1054f65 - Back after a link (the notes page's translation table, a Find's use) returns to the page it was followed
    from; the Notes and Feedback page's Find (button, Ctrl+F): where the picked control's devices and texts are used.
  - 123377c - A language's flag (HMI Settings > Translation, the flag button): a country's (its emoji pasted too), stripes
    or a cross, or a picture; a language added shows its country's flag by itself when the language names it.
  - ac4e3d0 - Alarms and HMI Settings fit beside a side page (a table scrolls itself); the tooltips' keys were missing from
    the translation files (527 texts, added with their English - tools\missing_keys.py); tooltips follow a language change.
  - a136085 - Config Import: what it does said; each file compared with this program's; Compare... side by side; Backups...
  - 06ce6b8 - Cylinder FW/Back: each way just under the units' alarm delays (0.5 s less than the shortest), not 5 s.
  - 14659a8 - PLC Monitor: the labels added from a page named by the controls' captions ("Gripper Length"), not by the
    values they show ("50.0 mm - setting").
  - e00fc33 - PLC Monitor: a row already named so gets the caption when the screen's devices are added again.
  - LZ: not backed up on GX Simulator3 (it shows no LZ) - left as it is, as said.
- 23 October (afternoon):
  - b98a2a4 - PLC Monitor: the simulator's devices never written (0) and moving ones shown - they were "-"; a device a
    real PLC does not answer no longer blanks the whole list (asked alone, then left out of the batch a while).
  - c32e765 - PLC data: the long counters (LCN) and LZ read and written one by one on MX Component (its block call counts
    them as devices: GX Simulator3 refused them, LCN sized 63).
  - fcdf299 - HMI Settings > PLC Connections: the controllers' table itself - the Connection events and MX Component
    tabs gone (the connection events are in Logs and Reports).
  - db69fd9 - PLC data: the modules' cams - read through the cam data operation (the cams the axes use, Pr.440, and the
    numbers typed), "Cam data" in the Write tree written into the cam storage area; the simulator answers it too.
- 23 October (later, PLC data):
  - 7a5e4d3 - PLC data Read: the CPU's devices are read. The first backup on GX Simulator3 had none: its block calls kept
    MX Component's thread from the screen's reads until those gave up and the link was taken down. Block calls now go
    after the others; the sizes are asked with block calls; a lost link is waited for (nothing saved if it does not come
    back); a device the CPU does not answer is named.
  - 112e2a6 - PLC Connection: Read and Write only while the PLC is connected (a PLC not active kept them enabled).
  - d35ac8a - PLC data Write: the synchronous encoder axis and synchronous parameters written back (the Advanced
    Synchronous Control manual's Appendix 1), only the words it names.
  - 72232a4 - PLC data Write: the choice as a tree, as GX Works3's Select Data to Write - the CPU's devices; each module's
    groups with Details (axes, positioning data numbers) and its flash ROM; a row ticks all under it; Select all.
- 23 October:
  - 6fa1776 - Axis Settings: a parameter written no longer asks; "Write module n's flash ROM" blinks beside the Motion
    monitor list while a module of the page's axes has changes its flash ROM does not hold (its tooltip lists them);
    a click writes Un\G5900 = 1 (PLC READY no longer checked first).
  - f209d36 - PLC data Read: every device of the CPU (SM, SD, SB, SW, the contacts and coils, LC, Z, LZ too) and the
    modules' synchronous control parameters. Write: to the simulator every area, the inputs and outputs too.
  - 3ae1fa6 - FA2010 Axis Settings opens in the designer: a plain panel (axisJogBarPlace) where the jog bar goes; the page
    puts the AxisJogBar there when it is made.
- 22 October (evening, PLC data):
  - a5bec8e - Axis Settings: the simple motion parameters (Pr.1 - 3, 8 - 13, 45, 46) are an Admin's fields - typed in the
    cell's unit, checked against the manual's ranges, written to the module; then told when the module takes it (next
    start, or PLC READY on again) and offered the module's flash ROM (only while PLC READY is off, 25 per power on).
  - ce4eb08, 5fa445c - HMI Settings > PLC Connection: Read and Write at the end of each PLC's row. Read: the CPU's
    devices and its simple motion modules' data into a backup with the machine, PLC, CPU, serial number (typed), user,
    date and description. Write: a backup's areas ticked - all or the devices typed - to the same or other devices
    (R1000 - R1999 to D2500); never X / Y; the modules' flash ROM afterwards when ticked.
- 22 October (last):
  - cf0691a, 20ea6d4 - FA2010: your StructuredTypes.csv taken in; the labels follow your renamed types, the job list's
    three columns simple arrays (Double Word [Signed](0..499)).
  - a9556b9 - Structured data types: the list's order changed with up / down (Ctrl+Up / Ctrl+Down); a renamed type
    keeps its old name, so labels and saved tabs of the old name still find it.
  - ddb7357 - PLC Monitor: a tab with list and table labels has two tabs under it, "List viewed labels" and "Table
    viewed labels" (gone when either is empty); a table's caption no longer cut.
  - f7e555a - PLC Monitor: Value (HMI) shows a bit's (a structure's bit members') and a string's value in its own cell.
  - c7aabfa - Translation page: Undo (Ctrl+Z) and Redo (Ctrl+Y) on top - an AI import, a file import, a Save, a text
    typed, a language copied - undone in one step each.
- 22 October (night):
  - 421e20b - Axis units: the Position only again. One choice of positions (Position, Servo Encoder Value, Load
    Position) for the Axis Settings pages and the Axis Monitor; its list opens only with more than one to offer.
  - da8f86d - Side bar: the Axis Monitor's button only when an axis is set on the Axis Settings pages (not on H500).
  - 83679fb - PLC Monitor: the columns "Value (PLC)" and "Value (HMI)".
  - 93befda - PLC Monitor: table view per label - "Show this label in table view" on an array label moves it to the top
    of the tab as a table (its description the caption, the view buttons under it, the table under them), up / down
    among the tables, "List view" takes it back to the list, which stays at the bottom.
  - 72b5ad1 - FA2010: structured data types and labels for the Profiles & Operations sheet's tables (not the PLC device
    list). A new PLC Monitor row fills itself in from the label list (an address or a label typed).
  - The designer's "Method not found: ... set_RequiredSecurity": an older Kaban.UI loaded - CLAUDE.md, Conventions.
- 22 October (evening):
  - 32f7cc6 - HMI Settings > PLC Connection: an Active tick per PLC. A machine none of whose PLCs is active: not
    connected, not watched, its pages in no list (the navigation bar, the PLC Monitor ...) - from the next start.
  - 7c6bf40 - PLC Monitor: Divisor and Decimal Places per label (and per structure member, in Structures...); the
    Calculated Value column beside the Value (typed: written times the divisor). Any of the three can be taken off.
  - fced1e1 - "An unexpected error" on the translation imports: a sorted table moved its texts, not its rows - now a
    row moves whole in every table (Alarm Settings' definitions too); the review keeps each change on its row.
  - 4452f1a - PLC Monitor: a tab shown as a table - a line per index, a column per structure member (name over data
    type), calculated or PLC values, arrays of the same length added as columns ("Add an array..."), its list kept.
  - ec2a46b - Axis pages: the Position heading opens a list - Position, Servo Encoder Value (axes on a simple motion
    module), Load Position (a load-side encoder) - shown side by side; the axis units show them too. The encoder's
    tooltip and the Motion monitor's Feed / Real current value gone.
- 22 October (later):
  - 82fbd36 - Translation page: 📥 one import menu beside 🤖 - an older installation's folder (the keys still used, its
    languages asked), translation files, the AI's answer (files, copied). Empty cells written at once, every change
    confirmed in a list (ticked: the AI's, and what this panel's history says was typed here). 📤 "Into the project's
    files (Visual Studio)": the panel's texts into the project, each change confirmed. Save no longer writes the
    project's copy; Config Import no longer copies translation files.
  - f856f85 - Add a language: its name (offered from the code, to correct), kept and used for the AI.
  - e4926f4 - 🤖 and 📥 in colour (Windows drew the robot flat blue).
  - f6394bc - Notes and feedback: picking turns off when the page is closed (on again when it opens).
- 22 October - the Translation page:
  - 408c379 - Save writes the texts straight into their translation files (CommonTranslations.csv, a machine's
    Translations.csv, an Alarms.json - edited in place, comments and order kept); from Visual Studio's build the
    project's copy too. The first version's UserTranslations.csv is written in once and renamed .written.
  - c71c9c8 - 📋 copies a language's texts into the empty cells of the languages ticked (every row or the rows shown);
    "Add a language" asks which language's texts the new one begins with. Typed (orange) until Save.
  - 751e2d9 - A translation of the Notes page in force is an edit of the table (its history names the note).
  - 06883b5 - "Where is it used?" without the source: the program's lines from TranslationUses.csv, shipped beside it
    (`tools\translation_uses.py`, made again in every batch).
  - c573d32 - An Admin can reset the edit history (🗑 in its window; the list kept as a .bak beside it).
  - 4efaf94 - 🤖 asks an AI to translate the missing texts or check every translation of the rows shown: a briefing
    with their files, where each text is shown and the rules; 📥 imports its answer (its files or its answer copied),
    the texts typed into the table with the AI's note as the cell's tooltip, to check and Save.
  - 7c7edea - Notes and feedback: the menu's page buttons and the tabs are picked while picking, not used; the menu is
    darkened too. Picking off goes to another page.
- 21 October (later):
  - 2dae673 - Translation page: Where is it used? (Ctrl+F) - the pages and their controls, the menu, the tabs, the alarms,
    and the program's lines (message boxes, status lines ...) when the program runs from its build; a page opened on a double click.
  - 1d1005c - Notes and feedback: the page and the caption bar darker while picking, the control under the pointer bright.
  - 3af3f77 - Notes and feedback: its texts' link to the Translation page, the key's row marked there to edit.
- 21 October:
  - 9a33c14 - The coupling chain on the units: bold and dark, its ring faint.
  - fc52bb0 - HMI Settings > Translation (Maintenance and above): every translation, its owner (General / a machine),
    a column per language; edited on the panel (kept beside the files, in force at once), languages added and deleted,
    the edit history (who, when, before, after) and the export in the files' format.
- 19 October (evening, last):
  - 11ed86c - The axis current: 0.1 % (212 at the address is 21.2 %) and signed: -21.2 % green like 21.2 %.
- 19 October (later still):
  - 1c7e2eb - The cylinder help: the manual is a step of its own on the left (after the last one, its details 1.1 - 1.8),
    lit while the manual is read; a click unfolds it.
- 19 October (late night) - structured data types:
  - bb26a32 - Up / Down / Remove without the "reentrant call to SetCurrentCellAddressCore" error.
  - 20214df - Space between the Comment line and the labels' buttons.
  - 466dbc6, e82fe77 - A label past the length is an error (named in the problem line, OK refuses it); the length
    is no longer lengthened by itself.
  - 8e5794b - PLC Monitor: "Reset (0 into its devices)..." in the rows' right-click menu: a structure's every label,
    an array's every element, or one element / label; a Bit label only its bit. Asked first.
    3f21cdd - a structure's whole length is reset, the words no label uses too.
  - (The PLC Monitor's toolbar button for the structures: left out, as asked - they stay in the + menu.)
- 19 October (night) - structured data types:
  - 0746a59 - Each machine its own: StructuredTypes.csv beside its PlcLabels.csv (a mounted machine's in its folder,
    H500\StructuredTypes.csv), also written into the machine's project when the program runs from Visual Studio's
    build, so it is shipped with it. The first version's one file (ProgramData) becomes the main machine's.
  - b185dcc - Up / Down / Remove follow the label selected (they followed the row chosen before).
  - c8c6496 - The page redesigned: no captions, the buttons as pictures on top of the list and of the labels, the
    "Label" column, Name / Length / Comment a line each with the label on the left, no "... devices used" line.
- 19 October (evening):
  - Structured data types (Mitsubishi's) in the PLC Monitor: defined in "Structured data types..." (name, length,
    members at their offsets), chosen like a simple data type; an array of them steps by its length (R0, R16 ...).
- 19 October (later):
  - The row's "go to reference" (B0, B11 ...) read as the PLC's homing in progress: homing whatever the module does meanwhile.
  - One help per fault: an axis code's page holds the moment, the checks and the code's entry of the manual (last step);
    a cylinder movement's step-by-step page holds the cylinder manual folded at its end ("manual 1.6" opens it there).
- 17 October (late night) - "the format looks good but the info is false mostly":
  - The scanner: a read in flight when the page / a tab changed was looked up by the new batch's index (every device
    read another's value for that scan) - now by the batch it was read with. The likely cause of all three.
  - What the axis was doing: the motion module's word first (standby, BUSY OFF: standing still), both jog commands ON
    is no jog; the jog commands still shown, "they did not move it" when so.
  - (Taken back on 19 October: the rows' M330, M341, M342 ARE the sensors - M248... are overwritten in the PLC.)
- 19 October:
  - The reference sensor read at the Axis row's address again (M330 ...), the drawings' details kept.
  - The holding brake check only for a vertical axis (up / down arrows); the three angle axes have left / right arrows.
  - AxisUnitHMI: its row's arrows (it showed left / right on every axis).
  - The motion module's own software stroke limits (Pr.13 / Pr.12) in the help, beside the Axis page's (0 ... 0 on FA2010).
- 17 October (night):
  - The axis help by the moment of its code, visible: the code's own entry (alarm list, axis "?", the Axis page's code
    cell, the PLC's error bit) starts with what the axis was doing, where to start, the reference sensor's drawing
    details and a link to the step-by-step page; taken with the axis alarms off too; the template built into the program.
- 17 October (evening):
  - 6b9c0fd - Coupling only while Manual Movement, Machine Parameters > Axis or the Axis Monitor is shown: another
    page lets every coupling go. Cylinder FW/Back off when Manual Movement is closed; its button on the Manual
    Movement page only.
  - 5dd9f35 - The axes' bar: the steps, Safety Bypass and Cylinder FW/Back with their captions on two lines and
    narrower (ButtonHMI.TwoLineCaption); the bar 712 x 76, the Axis page's content 8 lower.
- 17 October:
  - 80f1e8d - Coupling: a chain whose unit does not match the units coupled is faded and does nothing (a click
    says why) - no more blinking of the ones that match. Axes couple only in step mode (not JOG: FA2010's JOG lamp
    M174 OFF) and on the same page, wherever each one stands; positioning / encoder cylinders when their
    FeedbackType is the same, wherever each one stands. Maintenance and Admin only: an operator sees no chain.
  - 20331ec - The caption bar's blue blinking chain while units are coupled: its menu lists them (one clicked is
    uncoupled) and last "Uncouple all".
  - 4186fab - Cylinder FW/Back, right of Safety Bypass: while on, a cylinder / positioning started moves back and
    forth every 5 s (its coupled units with it) until a button of it is pressed again or the function goes off.
    Both buttons for Maintenance and Admin only, both off when the user changes. The step buttons 60 wide.
- 16 October (night):
  - 4411c17 - The Axis lists (Axis page, Axis Monitor): an axis whose visibility conditions are not met is not offered.
  - 1814811 - Help pages: the step on show is marked by its beige box only - its number no longer blinks.
  - 59f87df - The Inputs / Outputs side page for every user level.
  - ec0cdb7 - Screen Scale (caption bar menu, Users and Preferences): the slider and percentage of the speed control.
  - c765a89 - Couple for manual movement: while units are coupled, the chains that can join blink; a chain that does
    not blink asks whether to let the coupling go and start again with its unit; with three or more coupled a chain
    offers "Uncouple '<unit>'" or "Uncouple all"; one unit is never left coupled alone; the chain stays at the right
    end of the frame line and the "?" comes and goes on its left.
- 16 October (evening):
  - 45e8d89 - HMI Settings > AI: an "Active" column. Not ticked: the chat stays in the table with its address but is
    not offered in the help lists ("Ask ...", the AI prompt's menu).
  - 87000f9 - The axis help by what the axis was doing when its code came - standing, homing, moved by hand, in the
    automatic cycle (the motion module's state and BUSY, the jog buttons, the machine running, the position and how it
    moved). The alarm's help, the axis "?" and its code cell open "Step by step - <axis> - <what it was doing>" first:
    an overload that got stuck while homing points at the reference sensor, with its drawing's details (sensor, input,
    card and terminal, wire, sheet, supply wires); by hand or in the cycle at a crash and the limits (at its limit:
    the limit beyond the mechanical end); a vertical axis standing still at its brake. The words are in
    Help\Templates\EN\Axis_Steps.html.
- 15 October (answers to the 14 October questions):
  - 61372f4 - Clamping Moulds: the back moulds hidden again while the Clamping Mould option (L27) is 0 - the rule is
    "L27 Equal 1" again and the 10 back rows carry its condition; Cutting Left Back Horizontal: the 5/3 closed-centre
    valve again; Robot Y's position divisor 10000.
  - 8962266 - Inverter fault help checked against your FR-A800 PDF: E.PE6 is 172, E.ECA 212 (their texts as printed),
    E.SCF (output short-circuit, 20) added, E.EHR removed (an Ethernet model's fault, not in this manual). Each fault
    links its page of the manual, in the Help folder's copy and online. The PDF stays out of git (23 MB).
  - 7328a83 - AxisSettingHMI: the position divided by the row's Divisor (PositionDivisor gone); a "Load Side Encoder"
    category with LoadSideEncoderAddress and LoadSideEncoderDivisor. 2ab9fbd - the encoder's divisor follows the
    Divisor until it is set (Robot X: D22 / 10000).
  - 3179458 - Conditions put on a page in Visual Studio (the Options page's "SystemCondition.1048576" errors): a
    machine's conditions are named in the enum now (MachineBit16 ... MachineBit63), so the designer writes them as code
    that compiles and reads them back.
  - 63fe4a1 - "Could not find type 'Kaban.Machines.FA2010.Screens.Shared.AxisJogBar'": no page holds the bar any more -
    it is above the Manual Movement tabs (over all of them) and on the Axis Monitor; Machine Parameters > Axis has a
    copy of its controls. The library project of 15 October (Kaban.Machines.FA2010.Shared) is gone.
  - a44b299 - Axis page: the speed and steps at the top, the PLC axis items / Motion monitor just before the column names.
  - 4965dc0 - Machine Parameters > Axis holds AxisJogBar itself again (no copy of its buttons): the program is "Any CPU"
    to Visual Studio, "Could not find type" was the machine not built / an older build loaded.
  - 8e4b637 - AxisJogBar: its parts 14 from the left and 13 from the top and bottom, as the Home page's speed.
  - c61f1a2, 63d5fb9 - The lists were made in Visual Studio's designer too, moving the Axis page down 40 on every
    opening (saved: the column names at 92, Robot X on Robot Y); no more - your save of the page taken in, the shift undone.
- 14 October (the Axis Settings page broken, two screenshots):
  - acbfe12 - Clamping Moulds: the position shown 10 times its value - divided by 100 as its sections now.
  - 9e1cd58, 87c0284 - Several conditions together: each control's tooltip (Admin) says each one met or not, and a
    flag with no rule on the machine "never met"; a sub-machine's machine bits numbered as mounted. Enable conditions
    and triggers for each Axis Settings row button (Back, Forward, Calibrate) and the axis units that follow them;
    a cylinder unit hidden whole by its visibility conditions (frame and caption too); CheckBoxHMI with visibility,
    enable conditions and a trigger.
  - dbda973 - SpeedOverrideHMI: one compact control - drag left / right, the percentage typed in the keypad.
  - 3e6412c - The servo speed / jog-step set as ONE control per machine (Screens\Shared\AxisJogBar): change it in its
    own designer, it changes on every page. On Robot, Milling, Cutting, Exit, Machine Parameters > Axis and the Axis
    Monitor. Axis Settings' first row back under its headings.
  - 392e54d - InverterUnitHMI: a motor's buttons, run lamp, value and inverter fault code as one control; the fault
    raises its alarm (M830.E.OC1) and its help is the code's troubleshooting (FR-A800 6.5, in the servo codes'
    format) then the whole manual. FA2010's six inverter sets replaced; the error code no longer on screen.
  - 2b5670e - HMI manual: the Contents go to their sections again (the copy's base address sent them to the Help
    folder); the section on screen is bold in the Contents.
  - 68ed4ad - AI help: a briefing to talk from (who is signed in, what the page shows, the step-by-step page and the
    manual as text, a conversation that starts with the likely cause and the first check); "Ask <AI>" pastes it into
    the chat by itself (Ctrl+V) - you check it and press Enter. Preferences > Help: "Paste into the chat".
- b6675a8, f39a294, da06455 - Users and Preferences, the Dashboard and H500's pages in Turkish, and the AI guide. The
  first two had their Turkish: they had been built in English before the kept language was put on the screen (now the
  language comes first, and a page made in code is built again after a change). H500's Entry Conveyor, Heads,
  Parameters and Home in Turkish (other languages English for now), the home buttons both machines share too; the AI
  page design guide: Turkish with every caption, the Home page's controls (SpeedOverrideHMI).
- 9ecd1b1, 6de47d7, 46e2c1a - From the HMI the machines run today (KabanMachinesSystem): H500's Manual Movement (Entry
  Conveyor, Heads) and Machine Parameters (Parameters) from its H500 FX5 pages (tools/legacy_h500_pages.py,
  tools/legacy/H500); FA2010's Home: Start M21, Pause M111, Cancel M24 (asked while the machine works) and the speed
  D408; H500's Home: Start M2 (while ready, M1), Pause M4, Cancel M3. No tables or pictures; not the Configuration tab.
- 8fb896b - Axis Monitor at 150%: the bottom scroll stopped short and put the rows back at the top, only sometimes
  (_requests\AxisMonitor_scroll_150%.png). It stopped with Robot X's back jog button at the left edge: opening the
  side page gave that button the focus, and Windows scrolls a panel back to its focused control whenever the focus
  is given to it again. The list takes the focus itself now and never jumps back to a control focused before (the
  Axis Settings and cylinder pages' rows the same).
- be89ade - Users and Preferences > Screen: Language under the screen scale - a user's own is put on the screen when
  they sign in (at once, no restart), everyone's again when they sign out. Everyone's is the flag's on the top bar,
  kept over a restart now (it came back in English).
- 70c199e - Found in the panel's audit log (12 October 10:24:27, the HMI restarted): typing a suggestion in the Notes
  page's texts table and then tapping another control crashed it. The table's typing is now ended first.
- 931ba43 - The description texts' bottom was cut off (Machine Parameters > Others at 80%, captions on the left):
  a caption is now as high as its letters at every screen scale (_requests\ScreenScale_80%.bmp).
