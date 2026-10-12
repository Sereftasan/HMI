# The old H500 HMI's pages

The page designers of the HMI program the H500 runs today (`_H500` of the
KabanMachinesSystem solution), its **H500 FX5** version - the pages the old
program shows when the machine is "H500 FX5" or "H550 FX5" - sent on 12
October as the specification of the new program's pages:

| Old page (designer)              | New page                                         |
|----------------------------------|--------------------------------------------------|
| ucScrewingEntryConveyorH500FX    | Manual Movement - Entry Conveyor                 |
| ucScrewingHeadsH500FX            | Manual Movement - Heads                          |
| ucScrewingParameterH500FX        | Machine Parameters - Parameters                  |
| ucMainControlPanelH500FX (+ ucScrewingMainControlPanel.cs) | Home - Start, Pause, Cancel |

`tools/legacy_h500_pages.py` holds the manual and parameter pages row by row
and writes them; `--check` compares them with these designers.

The old Machine Settings' *Configuration* tab (the profile and company tables
of the old program's database) is not brought over.
