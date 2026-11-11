# The old MD3030 HMI's pages

The program the MD3020 runs today: the `_MD3030` project of the old
KabanMachinesSystem solution (sent on 11 November - "Can you fulfil
Kaban.Machines.MD3020 project using our current operating program _MD3030
project as well"). Its page designers and the code around them are kept here as
they are (CRLF, as the old solution has them); `tools/legacy_md3030_pages.py`
holds the pages row by row and writes them, `--check` compares them with these
designers device by device.

| Old page (designer)                          | New page                                           |
|----------------------------------------------|----------------------------------------------------|
| ucGeneral (Manual > General)                 | Manual Movement - General                          |
| ucManualMovement (Manual > Manual Movement)  | Manual Movement - Manual Movement                  |
| ucServos (the Servo Situations window)       | Manual Movement - Servos                           |
| ucHeadControlButtons (the manual pages' bar) | the bar above Manual Movement (Screens\Shared\JogBar) |
| ucAxisParameters (Parameters > Axis Calibration) | Machine Parameters - Axis Calibration          |
| ucGeneralParameters (Machine Parameters > Genel) | Machine Parameters - General                   |
| ucPositionDefinition (Machine Parameters > Position Defination) | Machine Parameters - Position Definition |
| ucSpeedParameters                            | Machine Parameters - Speed Parameters              |
| ucProcessParameters (toggle switches, L3 L0 L5 L6 L13 in its code) | Machine Parameters - Process Parameters |
| ucOffset                                     | Machine Parameters - Offset                        |

Not brought over (hidden in the old program, or not machine pages):

* ucHead1-4 (Manual > Head-1 ... Head-4, hidden tabs) and ucIOWatchs (Input Output
  Watch: the same old head pages, with placeholder lamps X50 / X46).
* ucTestMode (Home > Test Mode) and ucTesterParameters (Parameters > Test
  Parameters): hidden tabs. ucHeadParameters (hidden), ucOtherParameters (not used).

Still to come: the Home page - the job preparation (ucJobPreparations,
ucActiveJobScreen, ucJobList, ucJobDimentions, ucProfileInfoPanel) and the main
control panel (ucMainControlPanel: Start M316's lamp, Pause M111 / M84, Cancel M85,
the speed D408) - and sending a profile's recipe to the PLC (JobProvider,
TransferProvider_FXCpu: the profile's values D2092 ... D7060, its options
L12 ... L61, the receipts' table from R0, M101 / M102); ucMachineSettings (the
barcode's three fields) and ucAlarms.

What the old pages show that looks wrong (kept as they are - to ask):

* Axis Calibration: General Y2's encoder is D2014 - Head 2 Y2's; General Y1's
  Offset is D2812 - its own encoder; X2's measured value D2580 is X's.
* Axis Calibration: the heads share their Starting / End Limits (X D528 / D526, Y1
  and Y2 both D532 / D530, Z1 D524 / D522, Z2 D536 / D534, X2 D542 / D540) and Send
  Reference (M100); the Sensor Reference Distance column shows D6600 ... D6626.
* Torques: Head 2 Z2 is D7194 on Manual Movement but D7188 on Servos, Head 3 X2
  D7195 and D7189. Servos: the body's four encoders all D20.
* General (Genel): Melting Torque (D2586) twice - shown once.
* Offset: Head 4's actual encoders SD4680 / SD4710 were read as 16-bit, the
  other heads' as 32-bit - all 32-bit here. Axis Calibration's Encoder Channel
  (D2816 ...) had two decimals of a whole number - shown whole.
