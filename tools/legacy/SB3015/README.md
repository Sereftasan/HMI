# The old SB3015 HMI's pages

The program the SB3015 runs today: the `_SB3015` project of the old KabanMachinesSystem
solution (17 November - "Can you complete SB3015 and MK2010 projects using
KabanMachineSystem project (I want you to use cylinder unit HMI and axis unit HMI controls
instead of the controls in KabanMachineSystem (same as our other machine projects)").
Its page designers and the code around them are kept here as they are (CRLF, as the old
solution has them); `tools/legacy_sb3015_pages.py` holds the pages row by row and writes
them, `--check` compares them with these designers device by device.

| Old page (designer)                                   | New page                                          |
|-------------------------------------------------------|---------------------------------------------------|
| ucEntryConveyor (Manual > Entry Conveyor)             | Manual - Entry Conveyor                           |
| ucRobot (Manual > Robot)                              | Manual - Robot                                    |
| ucProfileRecognition                                  | Manual - Profile Recognition                      |
| ucMilling                                             | Manual - Milling                                  |
| ucCutting                                             | Manual - Cutting                                  |
| ucExit                                                | Manual - Exit                                     |
| ucOther                                               | Manual - Other                                    |
| ucHeadControlButtons (the manual pages' bar)          | the bar above the Manual tabs (Screens\Shared\JogBar) |
| ucAxisParameters + ucToolSettingParameters (Tool Settings: backlash, calibration, reference) + ucServoStatus (Manual > Servo Status) | Machine Parameters - Axis Settings: an axis settings row per axis, the backlash under them |
| ucAxisSpeeds                                          | Machine Parameters - Axis Speeds                  |
| ucEncodersParameters                                  | Machine Parameters - Encoders (the three clamps measured by an encoder) and Positioning (the two front verticals) |
| ucEntryParameters, ucProcessParameters, ucOtherParameters | Machine Parameters - Entry, Process, Other    |
| ucRobotAxisCalibration (its table made by its code: 199 points R31200 / R31600 ...) | Machine Parameters - Robot Axis Calibration |
| ucHome (Job Preparations)                             | Home: the axes' positions, Emergency Stop and Air Pressure (see below) |

What each became:

* An axis: an axis settings row (its jog, position, limits, reference, calibration, reference
  sensor, send to reference, load, reference request, Z phase distance, the motion module's
  error / warning and the servo's alarm codes, its alarm bit M801 ... M812) and an axis unit
  on its manual page.
* A cylinder: a cylinder unit, its error bits from the alarm list (Alarms.json - the Industry 4
  program's, 90 alarms, kept). A clamp with an encoder (Profile Recognition front / top, the
  right top clamping): an encoder row (CylinderEncoderSettingHMI) on Encoders and an encoder
  unit on its manual page, as FA2010's; a front vertical: a pneumatic positioning
  (PneumaticPositioningHMI) - its target, send, calibration, calibrate, tolerance, direction.
* A motor on an inverter (the conveyors, the milling motors, the saw): an inverter unit - its
  fault code word (D666 ... D671) and its alarm bit (M827 ... M831).
* The alarms' reset: M102, the old bar's Reset (lit by M800) - the Alarms page's Reset pulses it.

Not brought over:

* ucClampingMoulds (commented out of the old Manual) and ucYZCrossRollers (an empty tab).
* V Cutting's Y / Z axes on Axis Parameters (shown only with the option IsVCutting).
* Settings > ucToolSettings: the milling tool calibration calculator (its values are not the PLC's).
* Home: the job preparation (the old program's shared job list and profile library), and
  sending a job to the PLC.
* Lubrication (hidden on Other) and Entry's hidden D800 / D801.

Decided here (17 November - the user: "decide how to do them yourself"; no drawings of SB3015 here, so the units' types
are read from the old pages - a machine that differs: change its row in tools/legacy_sb3015_pages.py and run it again):

* Valve: two command buttons a double solenoid (5/2), one a single solenoid; sensors: a lamp
  on each side two end sensors, on one side one sensor. The front verticals: a 5/3 closed
  centre (they stop at their target); the encoder clamps: double solenoid with the encoder and
  its reference sensor (the right top clamping's second lamp M234 taken as a pressure switch).
* Which way is Back: each unit's left button is the old page's Back column. The error bits:
  the first movement the alarm list names on the left (back / release / up) - the rollers' and
  Profile Recognition's unit's Back taken as up. The front verticals: back down, forward up
  (FA2010's drawings show the same cylinders so) - so their height alarms' down bits (M864 /
  M866) are Back's.
* The robots' Height (M540 / M558, one on / off button): on is up (the coil lifts the clamp to its
  upper height, the spring brings it down) - the up alarms M861 / M867 on the button, the down
  alarms M862 / M868 on the spring's side.
* Milling 2 Y's reference request: W58.3 (the old page reads W68.3, Milling 2 Z's).
* The bar's JOG: the old one wrote nothing itself (its lamp M174); here it clears the step bits
  M170 ... M173. Security ByPass (M175): the old bar showed it to Developer, Service and Admin
  users only - an Admin's here (greyed for the others).
* The front clamp's milling / cutting selection is one cylinder (M753 / M754): Milling and
  Cutting show it with their buttons the other way round, as the old pages did.
