# The old MK2010 HMI's pages

The program the MK2010 runs today: the `_MK2010` project of the old
KabanMachinesSystem solution (11 November - "can you add "MK2010" project from
"_MK2010" as well"). Its page designers and the code around them are kept here as
they are (CRLF, as the old solution has them); `tools/legacy_mk2010_pages.py`
holds the pages row by row (made by `tools/legacy_grid_pages.py`), `--check`
compares them with these designers device by device.

| Old page (designer)                               | New page                                    |
|---------------------------------------------------|---------------------------------------------|
| ucJobNew (Home > Job Preparations)                | Home                                        |
| ucManuelControl (Home > Manuel Control)           | Manual Movement - Manual Control            |
| ucReferance (Manuel Control > References)         | Manual Movement - References                |
| ucHeadControlButtons (the manual pages' bar)      | the bar above Manual Movement (the speed D409) |
| ucProductionParameters (Machine Parameters)       | Machine Parameters - Production Parameters  |
| ucCalibration                                     | Machine Parameters - Calibration            |
| ucSystemSettings                                  | Machine Parameters - System Parameters      |
| ucSystemSettings2 (behind the old password)       | Machine Parameters - System Parameters 2 (Admin) |
| ucDemo                                            | Machine Parameters - Demo                   |
| ucMachineRuntime (Reports, behind the password)   | Machine Parameters - Reports (Admin)        |

The toggle switches of Production Parameters and System Parameters are option
bits their code reads and writes (M1008 ... M1030, M1022 ... M1028); Reference All
and Calibrate All set the three axes' bits (M30 M31 M32, M91 M92 M93).

Not brought over yet: the Home page's profile list and barcode (ucJobNew: a
profile chosen or read from a barcode - its code, X and Y - writes the profile's
code to R1500, its width, mould difference, mode bytes, heating time, welding time
and temperature to R1502 ... R1507, D120 = D124 = 0, M86 = 1, and the sizes to D1200
/ D1202): the profiles' settings are in the old program's database. The alarm
texts (M600, three words) are there too. Not machine pages: ucWeldingParameters
and the Companies, barcode reader and label pages.

What the old pages show that looks wrong (kept as they are - to ask):

* System Parameters: Sliding Sash Press's switch writes M1016 - All Profile Code's
  bit (its hidden button was M1036).
* Manual Control: the left table's sensor rows repeat the right one's (Head 1-2
  Conveyor X0031 / X0011 - Supporting Plate's Head 1; Head 3-4 Conveyor X0035 /
  X0025 - Heater Plates'; Gasket Pressing M316 twice).
* References: the Limit lamps M100 / M102 / M104 are Manual Control's Profile
  Release, Back Plate and Heater Plates.
* Reports: the processed corners D162 / D164 were shown / 10.

## 12 November: the drawings and the PLC program

Sent by the user: the pneumatic drawing (MK 2010, 29.06.2015, 6 sheets), the
electrical drawing (V3, 13.03.2018), the GOT's screen project
(MK2010_V4.2_26.06.16_CiftliProfilKaldirma.GTX) and the PLC project
(MK2010_V6.4_26.07.30_AgaogluBarkod.gx3).

* The PLC project (.gx3) is a zip of SQLite files: its device comments (every
  X / Y / M / D's name, written in the program) are in
  PLC_V6.4_DeviceComments.txt here; its program is there as an instruction list
  (read in part - enough to see the manual toggles M100 ... M109 drive the outputs).
* The GOT's project (.GTX) is encrypted - it could not be read.

From them: the alarm list (M600 ... M635, as the program names them) in
Alarms.json; Manual Control's cylinders a unit per head (Supporting Strip, Side
Table 1 / 2, Heater Plate, Top Press, Profile Lifting, Profile Emptying, Sliding
Sash Press) and the gasket presses per pair of heads (1-4, 2-3) - see
tools/legacy_mk2010_pages.py for each one's valve, sensors and alarm bits. An
alarm bit is common to the four heads (the PLC has one per movement): every head's
unit carries it and the alarm's help offers each one's page.

To check:

* The toggles' direction: M10x on = forward / down (the strips down, the side
  tables and heaters forward, the top presses down, the profile lifted).
* The side tables (old "Back Plate"): Y2 / Y3 forward at 3 / 6 bar, Y4 back -
  single valves, taken as a 5/3 open centre; the top presses (Y7 down, Y8 up) too.
* Not on the 2015 pneumatic sheets, so their valve and sensor types are not set:
  the sliding sash press (back X54 ... X57) and the gasket presses (the sheets show
  single valves per head, the program drives heads 1-4 and 2-3 in pairs).
* The profile lifting and emptying cylinders: the sheets draw sensors, the PLC has
  none among its inputs - taken as no sensor.
* The axes are inverter-driven (X / Y1 / Y2 inverter faults X41 ... X43, M614 ...
  M616): no servo help is given to their rows; their reference switches X6 / X7 /
  X10 are the old page's Limit lamps.

