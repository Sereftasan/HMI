# Requests for Claude

Write here what is missing or wrong, whenever you notice it - one line or a paragraph each, in any order.
Claude reads this file at the start of every batch, does the items, and moves each one to "Done" with its commit.

## To do

## Done
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
  - 1ab2298 - "Could not find type 'Kaban.Machines.FA2010.Screens.Shared.AxisJogBar'": the bar is in a library of its own
    (Kaban.Machines.FA2010.Shared) - Visual Studio cannot load a control from the 32-bit machine program.
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
