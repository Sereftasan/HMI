# Requests for Claude

Write here what is missing or wrong, whenever you notice it - one line or a paragraph each, in any order.
Claude reads this file at the start of every batch, does the items, and moves each one to "Done" with its commit.

## To do

## Done
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
