# Requests for Claude

Write here what is missing or wrong, whenever you notice it - one line or a paragraph each, in any order.
Claude reads this file at the start of every batch, does the items, and moves each one to "Done" with its commit.

## To do

## Done
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
