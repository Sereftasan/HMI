# KabanMachinesSystem → Kaban HMI: what the old program has, and where it is now

Written 19 November 2026 after the request "analyze KabanMachineSystem allover and add everything you find useful for our new
HMI program". The old solution is `C:\Users\stasan\Desktop\Özgür_Temporary\C#_Codes\KabanMachinesSystem\KabanMachinesSystem`
(KabanMachinesSystem.sln). It has:
- a shared program: KabanMachinesSystem, .Business, .Common, .Data (PostgreSQL, 135 tables), .Import;
- KabanDevBase (its controls), KabanLabelDesigner, KabanDXFViewer, netDxf and ScrewingOptimizationSolution;
- one project per machine.

Status: ✔ in the new HMI · ◐ partly · ✖ not yet (and why).

## The machine runtime (FA2010 runs the old _ZC2010 job screen)

| Old | What it does | New HMI |
|-----|--------------|---------|
| ucJobPreparations.SendBar (TransferProvider_FXCpu) | Writes the bar's data, then L0 off, L1 "Is Geldi" on, L10 "Son Bar Degil" | ✔ JobSender (19 Nov: the L0 / L1 / L10 handshake added) |
| Check_NextJob | L0 "Is Gonder" while M21 "Makine Aktif" → the next bar, the bar's time D91 | ✔ JobRun: the Jobs page "Automatic", Stop automatic |
| Check_UnitsGCodeStatus / Check_SawGCodeStatus | L101 / L103 / L105 → milling and special cut steps | ✔ JobMilling |
| Check_PrintBarcode / CheckLeftOverBarcode | L2 + D386 → piece label; L24 + D388 → leftover label | ✔ LabelTriggers + JobLabels (off until switched on) |
| CuttingListHeader / Items, Customer Report | What was sent / cut, per customer and order | ✔ ProductionLog, HMI Settings > Production |
| General Information | Power-on D93, working time D104, bars and metres cut | ✔ Production page: the counters and the sums |
| KabanFileSystemWatcher | The window software's job files from a transfer folder | ✔ JobInbox (the Jobs page's inbox button) |
| KabanTransferCleaner | Empties the transfer folder | ✔ Old job files moved into its Archive (7 / 30 / 90 days) |
| KabanFileReader | Bar, Profile, Rec, VCut, SpecialCut, Screw(2-5), Hole(2-5) | ✔ KbnFile; Screw / Hole lines read and kept (19 Nov) |
| Waste mode (L26 PC_FireKesimModu, L100, measured length D89) | A leftover measured, a job made from the pieces left | ✖ It needs the old optimiser (OptimizationStrategy / Optimizer). Next step if wanted |
| ReadProfileInformation (D481 / D482 measured, D8 / D9 defined) | Shows the measured profile | ◐ Records 6 / 20 log D481 / D482; not shown on Home |
| ControlSameProfile_DifferentType | Warns about the same profile code of another type | ✖ The new library has one profile a code |

## Settings pages

| Old page | New HMI |
|----------|---------|
| Barcode Reader Settings + Test | ✔ HMI Settings > Barcodes |
| Label designer (KabanLabelDesigner) | ✔ HMI Settings > Labels (EPL as the old one, ZPL, copies = the old Barcode Count) |
| Cleaning Settings | ✔ HMI Settings > Maintenance (any task, by days or a PLC counter) |
| Backup Settings (pg_dump, the program zipped) | ✔ HMI Settings > Backup (the HMI's data as one zip, daily, restore) |
| User Settings | ✔ Users and Preferences |
| Localization (UI, messages, alarms, database) | ✔ HMI Settings > Translation |
| Machine Settings: V cut, mullion blade, special cutting, notching | ✔ Machine Parameters > Options (machine conditions) |
| Machine Settings: Use Printer / Barcode Count / Leftover | ✔ The labels on / off, copies, the leftover label |
| Machine Settings: Use Screwing / Screwing type | ✖ See "screwing" below |
| Machine Settings: inch system | ✔ Units (HMI Settings > Users and Preferences) |
| Parameters definers (machine / profile parameters) | ✔ The profile library's machine settings (StructuredTypes "Profile Parameters") |
| Saw Settings, V cutting | ✔ The Jobs page's cutting settings, the special cut recipes |
| Drill Bit Settings, machine tools | ✔ The library's tools and mounted tools |
| Export Settings (profiles / receipts as CSV, import) | ◐ The library editor's tables; Backup keeps all of it |
| External Receipt Mapping (XLSX / XML) | ✖ Receipt (accessory) imports for CNC machines - not FA2010's |
| Work / Break Settings (breaks a weekday) | ✖ The dashboard's shifts cover the planned time |
| Machine Logs: Robot Gripper Logs (L3) | ✖ A machine-specific log (L3 on FA2010 is the conveyor roller check) |
| Machine Logs: Color Guide | ✔ The dashboard's state colours |

## The shared program

| Old | New HMI |
|-----|---------|
| Alarms, Alarm History, Error Descriptions | ✔ Alarms page, alarm history, troubleshooting help |
| PLC popups (PopupProvider, Custom popups) | ✔ PlcPopups (Popups.json) |
| Companies / profiles / series / groups / accessories / receipts / mapping | ✔ The profile library (Profiles, Profile groups pages) |
| Profile alternative codes, notch settings, clone | ✔ / ◐ Mullion notching ✔, clone ✔ (library editor), alternative codes ✖ |
| DXF reader / CAD / CAM / EyeShot / G-code converter / universal tool path | ◐ The DXF viewer (KabanHMI\Dxf); G-code for the CNC machines ✖ (FA1010 / SA3030-type machines are not in the new HMI) |
| Unilink importer, profile / receipt JSON importers | ✖ Third-party CNC data imports - with a CNC machine project |
| Optimization (StopperOptimization, OptimizerDoubleMilling) | ✖ ZU2010 / ZU3010 (stopper saws) |
| LicenceHelper | ✖ Left out on purpose |
| Remote support (TeamViewer / AnyDesk menu) | ✔ The "?" menu |

## Screwing (H500 / H100 behind a cutting machine)
The old FA2010 handed every piece to the screwing machine:
- TransferScrewing.SendToScewingDB wrote a ScrewingTransfer row per piece: its Kaban barcode, its Screw / Hole positions and its profile's
  screwing configuration.
- The H500 operator scanned the piece's label; ucScrewingHome.Check_AddNewScrewingProcess found the row and sent the positions to the H500's
  FX5 PLC.

The new HMI has these parts so far:
- The Screw / Hole lines are read (KbnPiece.Screws).
- Each piece's code (KabanBarcode) is on its label.
- The barcode readers pass a scan to a page that takes it.

The H500 side is not built yet. It needs the H500 PLC's devices for the positions, and the profiles' screwing configurations: Screwing
Configuration, Screw Positions, ScrewingManuelConfiguration. **Next step** when the line with an H500 is to be run from this HMI.

## Machines of the old solution

| Old project | New HMI |
|-------------|---------|
| _FA2010 (+ _ZC2010's job screen) | ✔ Kaban.Machines.FA2010 |
| _H500, _H800 | ✔ Kaban.Machines.H500, H800 (pages); H500's screwing job ✖ (above) |
| _MD3030 | ✔ Kaban.Machines.MD3020 |
| _MK2010 | ✔ Kaban.Machines.MK2010 |
| _SB3015 | ✔ Kaban.Machines.SB3015 |
| _AC1050, _FA1010, _FA3010, _GD1010, _H100, _SA3030, _ZC2010, _ZC4040, _ZM1060, _ZU2010, _ZU3010 | ✖ Not asked for yet. Each can be brought over the same way: its pages from its designers (tools\legacy_grid_pages.py), its cylinders and axes as units, its alarms from its list |

## Added on 19 November (this batch)
1. FA2010's bar handshake (L0 / L1 / L10) and the automatic run (the next bar when the PLC asks, a bar's working time D91).
2. The production log and HMI Settings > Production (bars sent / finished, pieces cut, leftovers, sums, the machines' counters, CSV export).
3. Job files' Screw / Hole lines read and kept. FA2010 no longer refuses those bars.
4. HMI Settings > Backup (the HMI's data as one zip, daily, restore).
5. The transfer folder's old job files moved into its Archive.
6. Label copies (the old Barcode Count).
