#!/usr/bin/env python3
"""Builds KabanHMI/Help/Inverter_Fault_Codes.en.html - the help for an inverter's fault code.

14 October: "We should have a control for inverter as well (just like CylinderUnitHMI) so we
could automatically set its error and open a proper helper for it (using its error code) ...
part 6.4 shows the codes stored to the error code device address ("Data code") and part 6.5
shows the troubleshootings for these errors. We should create a helper according to the alarm
code (at the same format as the servo axis error troubleshooting page), give the whole document
as the second helper element."

The machines' inverters are Mitsubishi FR-A800s. The PLC copies the inverter's fault into a word
as the DATA CODE of 6.4 "List of fault displays" (decimal: 16 = E.OC1). One section per data
code, the way Servo_Alarm_Codes is made: <section class="code" data-code="16" data-alarm="E.OC1"
data-name="..."> - AxisCodeHelp (AxisCodeKind.InverterFault) finds the code's section by it and
shows that section alone; the whole manual is the help's second entry.

The texts follow 6.5 "Causes and corrective actions" of the FR-A800 Instruction Manual
(Detailed), IB-0600503ENG-N, in short words of our own - the manual itself stays the reference:
every section links its page, in the copy in the Help folder (KabanHMI\Help\FR-A800_Instruction_Manual.pdf,
not in git: 23 MB) and online. The data codes and pages are 6.4's of that edition (15 October:
checked against the user's PDF; E.SCF added, E.EHR - an FR-A800-E Ethernet model's fault, not in
this manual - removed). A code the list does not have opens the "not listed" note and the manual.

Run:  python3 tools/build_inverter_help.py
"""
import html, os

MANUAL_URL = "https://dl.mitsubishielectric.com/dl/fa/document/manual/inv/ib0600503eng/ib0600503engn.pdf"
MANUAL_FILE = "FR-A800_Instruction_Manual.pdf"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "KabanHMI", "Help", "Inverter_Fault_Codes.en.html")

PM = "(PM sensorless vector control)"

# 6.4's "Refer to page" of each data code (the printed page; the PDF's page is one more: its cover).
PAGE = {16: 805, 17: 806, 18: 806, 20: 806, 32: 807, 33: 807, 34: 807, 48: 808, 49: 808, 64: 808,
        80: 809, 81: 809, 82: 809, 96: 810, 97: 810, 98: 810, 99: 810, 112: 811, 128: 811, 129: 811,
        144: 811, 145: 811, 160: 812, 161: 812, 162: 812, 163: 812, 164: 812, 165: 812, 166: 812,
        167: 812, 168: 812, 172: 813, 176: 813, 177: 813, 178: 813, 179: 813, 192: 814, 193: 814,
        194: 814, 196: 814, 197: 814, 198: 815, 199: 815, 200: 815, 201: 815, 202: 815, 253: 815,
        208: 816, 209: 816, 210: 816, 211: 817, 212: 817, 213: 817, 214: 817, 215: 817, 216: 817,
        217: 817, 218: 817, 219: 817, 220: 817, 222: 818, 224: 818, 225: 818, 228: 818, 229: 818,
        230: 819, 241: 819, 242: 819, 243: 819, 245: 814, 246: 814, 247: 814, 251: 819}
PDF_PAGE_OFFSET = 1

# (displays and data codes, name, when it trips, check points, corrective actions)
F = [
 ([("E.OC1", 16)], "Overcurrent trip during acceleration",
  "The output current reached about 235 % of the rated current while the motor was ACCELERATING; the output is shut off.",
  ["The acceleration is too sudden (on a lift: the downward acceleration time is too long).",
   "A short circuit on the output side (motor cable, motor).",
   "Pr.3 Base frequency is 60 Hz for a 50 Hz motor.",
   "The stall prevention operation level (Pr.22) is too high, or the fast-response current limit is off.",
   "Frequent regenerative driving (the output voltage above the V/F reference raises the motor current).",
   "Vector control: the RS-485 terminals' power supply is shorted; the encoder's wiring or specification (power, resolution, differential / complementary) or the motor's U, V, W wiring is wrong.",
   "Real sensorless vector control: the direction is switched between forward and reverse during torque control.",
   "The inverter and the motor capacities do not match " + PM + ".",
   "A start command was given while the motor was still coasting " + PM + "."],
  ["Set a longer acceleration time (on a lift, a shorter downward acceleration time).",
   "If E.OC1 comes at every start: disconnect the motor and start again. Still there: contact the sales representative (the inverter).",
   "Check the output wiring: no short circuit.",
   "Set Pr.3 Base frequency to 50 Hz; set Pr.19 Base frequency voltage to the motor's rated voltage.",
   "Lower the stall prevention operation level (Pr.22) and switch the fast-response current limit on.",
   "Vector control: check the RS-485 terminal connection; wire and set the encoder and the motor to their specifications.",
   "Do not switch the direction during torque control under Real sensorless vector control.",
   "Choose an inverter and a motor of matching capacity " + PM + ".",
   "Give the start command after the motor has stopped, or use the automatic restart after instantaneous power failure / flying start (Pr.57) " + PM + "."]),
 ([("E.OC2", 17)], "Overcurrent trip during constant speed",
  "The output current reached about 235 % of the rated current at CONSTANT SPEED; the output is shut off.",
  ["A sudden change of the load (jammed or blocked mechanism).",
   "A short circuit on the output side.",
   "The stall prevention operation level (Pr.22) is too high, or the fast-response current limit is off.",
   "Vector control: the RS-485 terminals' power supply is shorted.",
   "Real sensorless vector control: the direction is switched during torque control.",
   "The inverter and the motor capacities do not match " + PM + ".",
   "A start command while the motor was coasting " + PM + "."],
  ["Keep the load steady: check the mechanism for jams, the belts, the bearings.",
   "Check the output wiring: no short circuit.",
   "Lower the stall prevention operation level (Pr.22) and switch the fast-response current limit on.",
   "Vector control: check the RS-485 terminal connection.",
   "Do not switch the direction during torque control under Real sensorless vector control.",
   "Choose an inverter and a motor of matching capacity " + PM + ".",
   "Start after the motor has stopped, or use the automatic restart / flying start (Pr.57) " + PM + "."]),
 ([("E.OC3", 18)], "Overcurrent trip during deceleration or stop",
  "The output current reached about 235 % of the rated current while DECELERATING or stopped; the output is shut off.",
  ["The speed is reduced too suddenly.",
   "A short circuit on the output side.",
   "The motor's mechanical brake closes too early (while the motor is still driven).",
   "The stall prevention operation level (Pr.22) is too high, or the fast-response current limit is off.",
   "Vector control: the RS-485 terminals' power supply is shorted.",
   "Real sensorless vector control: the direction is switched during torque control.",
   "The inverter and the motor capacities do not match, or a start command while coasting " + PM + "."],
  ["Set a longer deceleration time.",
   "Check the output wiring: no short circuit.",
   "Check the mechanical brake's timing.",
   "Lower the stall prevention operation level (Pr.22) and switch the fast-response current limit on.",
   "Vector control: check the RS-485 terminal connection.",
   "Do not switch the direction during torque control under Real sensorless vector control.",
   "Match the capacities; start after the motor has stopped or use the automatic restart (Pr.57) " + PM + "."]),
 ([("E.SCF", 20)], "Output short-circuit",
  "An output short circuit was detected while Pr.521 = 1; the output is shut off. (With Pr.521 = 0, the initial value, a short circuit trips as E.OC1, E.OC2 or E.OC3 instead.)",
  ["A short circuit on the output side (motor cable, motor)."],
  ["Check the output wiring: no short circuit.",
   "Then switch the control circuit power OFF to reset the inverter."]),
 ([("E.OV1", 32)], "Regenerative overvoltage trip during acceleration",
  "Regenerated energy or a surge on the supply raised the main circuit DC voltage to the trip level while ACCELERATING; the output is shut off.",
  ["The acceleration is too slow (e.g. the downward acceleration of a lift load).",
   "Pr.22 Stall prevention operation level is at or below the no-load current.",
   "Stall prevention works often on a load with a large inertia."],
  ["Set a shorter acceleration time.",
   "Use the regeneration avoidance function (Pr.882 to Pr.886).",
   "Set Pr.22 above the no-load current.",
   "Set Pr.154 Voltage reduction selection during stall prevention operation to 10 or 11."]),
 ([("E.OV2", 33)], "Regenerative overvoltage trip during constant speed",
  "Regenerated energy or a surge raised the main circuit DC voltage to the trip level at CONSTANT SPEED; the output is shut off.",
  ["A sudden change of the load.",
   "Pr.22 Stall prevention operation level is at or below the no-load current.",
   "Stall prevention works often on a load with a large inertia.",
   "The acceleration / deceleration time is too short."],
  ["Keep the load steady.",
   "Use the regeneration avoidance function (Pr.882 to Pr.886); use a brake unit or a power regeneration common converter (FR-CV) as needed.",
   "Set Pr.22 above the no-load current; set Pr.154 to 10 or 11.",
   "Set longer acceleration / deceleration times (under vector control a sudden acceleration may overshoot the speed and cause the overvoltage)."]),
 ([("E.OV3", 34)], "Regenerative overvoltage trip during deceleration or stop",
  "Regenerated energy raised the main circuit DC voltage to the trip level while DECELERATING or stopped; the output is shut off.",
  ["The speed is reduced too suddenly.",
   "Stall prevention works often on a load with a large inertia."],
  ["Set a longer deceleration time, to suit the load's moment of inertia; make the braking cycle longer.",
   "Use the regeneration avoidance function (Pr.882 to Pr.886).",
   "Use a brake unit or a power regeneration common converter (FR-CV) as needed.",
   "Set Pr.154 to 10 or 11."]),
 ([("E.THT", 48)], "Inverter overload trip (electronic thermal O/L relay function)",
  "The output transistors' temperature passed the protection level at the rated current or more, without an overcurrent trip (the inverter carries 150 % for 60 s); the output stops.",
  ["The acceleration / deceleration time is too short.",
   "The torque boost (Pr.0) is too large or too small.",
   "The load pattern selection (Pr.14) does not suit the machine.",
   "The motor is overloaded.",
   "Vector control: the encoder's wiring or specification, or the motor's U, V, W wiring, is wrong."],
  ["Set longer acceleration / deceleration times.",
   "Adjust the torque boost (Pr.0).",
   "Set the load pattern selection (Pr.14) to the machine's load.",
   "Reduce the load (check the mechanism for friction and jams).",
   "Vector control: wire and set the encoder and the motor to their specifications."]),
 ([("E.THM", 49)], "Motor overload trip (electronic thermal relay function)",
  "The electronic thermal relay judged the motor overheated - overload, or poor cooling at low speed - and its accumulated heat reached the limit; the output stops. A pre-alarm (TH) shows at 85 % of the Pr.9 setting. Several motors on one inverter, or a special motor, need an external thermal relay.",
  ["The motor is overloaded.",
   "Pr.71 Applied motor is not set to the motor fitted.",
   "The stall prevention operation setting does not suit the motor."],
  ["Reduce the load (check the mechanism for friction and jams).",
   "For a constant-torque motor, set it in Pr.71.",
   "Set the stall prevention operation level correctly.",
   "Check Pr.9 Electronic thermal O/L relay against the motor's rated current."]),
 ([("E.FIN", 64)], "Heatsink overheat",
  "The heatsink's temperature sensor tripped; the output stops. The FIN signal can warn at about 85 % of the trip temperature (Pr.190 to Pr.196 set to 26 / 126).",
  ["The ambient temperature is too high (the cabinet, its fans and filters).",
   "The heatsink is clogged.",
   "The cooling fan has stopped (\"FN\" on the operation panel)."],
  ["Keep the ambient temperature within the specification: check the cabinet's fans and filters.",
   "Clean the heatsink.",
   "Replace the cooling fan."]),
 ([("E.IPF", 80)], "Instantaneous power failure",
  "A power failure longer than 15 ms shut the output off to protect the control circuit (shorter ones are ridden through); the IPF signal is output. With a start signal ON, a failure of 100 ms or more restarts on power restoration when Pr.57 allows it.",
  ["The cause of the instantaneous power failure (the supply, its contactors and fuses, loose terminals)."],
  ["Remove the cause of the power failure.",
   "Prepare a backup power supply.",
   "Set Pr.57 for the automatic restart after instantaneous power failure."]),
 ([("E.UVT", 81)], "Undervoltage",
  "The supply voltage fell to about 150 V AC (300 V AC for the 400 V class) or below, or the jumper across P/+ and P1 is missing; the output is shut off and the IPF signal is output.",
  ["A large motor is started on a weak supply.",
   "The jumper across terminals P/+ and P1 is not fitted (it is removed only for a DC reactor)."],
  ["Check the supply (the transformer, the cables, the contactors). On an unstable supply the undervoltage level (the DC bus value) can be changed as the manual says.",
   "Keep the P/+ - P1 jumper fitted unless a DC reactor is connected.",
   "Still there: contact the sales representative."]),
 ([("E.ILF", 82)], "Input phase loss",
  "With Pr.872 Input phase loss protection selection = 1, one of the three input phases was lost; the output is shut off. Pr.872 = 0 (initial) switches the protection off.",
  ["A broken cable, a loose terminal or a blown fuse on the three-phase input."],
  ["Wire the input correctly; repair the broken part (cable, terminal, fuse)."]),
 ([("E.OLT", 96)], "Stall prevention stop",
  "The stall prevention held the output at 0.5 Hz for 3 s (V/F, Advanced magnetic flux vector control), or - under speed control - the torque limit held the frequency at Pr.865 Low speed detection (1.5 Hz) with the torque above Pr.874 OLT level setting (150 %) for 3 s; the output stops. \"OL\" shows while the stall prevention works.",
  ["The motor is overloaded (blocked, jammed).",
   "The Pr.865 and Pr.874 values (V/F and Advanced magnetic flux vector control: Pr.22).",
   "Under PM sensorless vector control: is a motor connected?"],
  ["Reduce the load: free the mechanism.",
   "Change Pr.22, Pr.865 and Pr.874.",
   "For a test without a motor, select the PM sensorless vector control test operation.",
   "Remedy the stall prevention warnings OL (overcurrent) or oL (overvoltage)."]),
 ([("E.SOT", 97)], "Loss of synchronism detection",
  "A PM motor lost its synchronism (PM sensorless vector control only); the output is shut off.",
  ["The PM motor is overloaded.",
   "A start command was given while the motor was coasting.",
   "Is a motor connected under PM sensorless vector control? Is it an MM-CF series motor?"],
  ["Set a longer acceleration time; reduce the load.",
   "If the inverter restarts while the motor coasts: set Pr.57 (not 9999) for the automatic restart after instantaneous power failure.",
   "Check the motor's connection; for a test without a motor select the PM sensorless vector control test operation.",
   "Drive an MM-CF series IPM motor, or run the offline auto tuning for another IPM motor."]),
 ([("E.LUP", 98)], "Upper limit fault detection",
  "The load passed the upper limit of the load characteristics fault detection; the output is shut off (off while Pr.1490 = 9999, the initial setting).",
  ["Too much load on the machine (a jam, friction).",
   "The load characteristics settings."],
  ["Inspect the machine.",
   "Set the load characteristics (Pr.1481 to Pr.1487) correctly."]),
 ([("E.LDN", 99)], "Lower limit fault detection",
  "The load fell below the lower limit of the load characteristics fault detection; the output is shut off (off while Pr.1491 = 9999, the initial setting).",
  ["The load is too light (a broken belt or coupling).",
   "The load characteristics settings."],
  ["Inspect the machine (belts, couplings).",
   "Set the load characteristics (Pr.1481 to Pr.1487) correctly."]),
 ([("E.BE", 112)], "Brake transistor alarm detection",
  "A fault in the brake circuit (e.g. a damaged brake transistor); the output is shut off. Cut the inverter's power at once. (On the separated converter types and IP55 models it means an internal circuit fault.)",
  ["Is the load inertia too large?",
   "Is the braking duty within its rating?"],
  ["Switch the power off and replace the inverter (contact the sales representative)."]),
 ([("E.GF", 128)], "Output side earth (ground) fault overcurrent",
  "An earth (ground) fault on the output side - the motor cable or the motor - caused an overcurrent; the output is shut off.",
  ["The motor and its cable for an earth fault (insulation; water, oil or chips in the terminal box)."],
  ["Remove the earth fault: repair or replace the cable or the motor."]),
 ([("E.LF", 129)], "Output phase loss",
  "One of the three output phases (U, V, W) is open; the output is shut off.",
  ["The output wiring (a broken cable, a loose terminal, a motor contactor or isolator open) - does the motor run normally?",
   "Is the motor much smaller than the inverter?",
   "A start command while the motor was coasting " + PM + "."],
  ["Wire the output correctly.",
   "Start after the motor has stopped, or use the automatic restart / flying start " + PM + "."]),
 ([("E.OHT", 144)], "External thermal relay operation",
  "An external thermal relay, or the motor's own thermal switch, opened its contact (the OH signal: \"7\" in one of Pr.178 to Pr.189); the output is shut off.",
  ["Is the motor overheated (load, cooling fan, ambient)?",
   "Is \"7\" (OH signal) set in the right one of Pr.178 to Pr.189?"],
  ["Reduce the load and the operating duty.",
   "The inverter does not restart when the relay closes again by itself: reset the inverter."]),
 ([("E.PTC", 145)], "PTC thermistor operation",
  "The PTC thermistor between terminals 2 and 10 read at or above the Pr.561 level for Pr.1016's detection time; the output is shut off (off while Pr.561 = 9999).",
  ["The PTC thermistor's connection.",
   "The Pr.561 and Pr.1016 settings.",
   "Is the motor overloaded?"],
  ["Reduce the load."]),
 ([("E.OPT", 160)], "Option fault",
  "An option does not agree with the settings - e.g. a torque command option missing, or a communication option fitted while the password lock (Pr.296) is 0 or 100; the output is shut off.",
  ["AC power wired to R/L1, S/L2, T/L3 while a high power factor converter (FR-HC2) or FR-CV is connected (Pr.30 = 2).",
   "The plug-in option selected for the torque command (Pr.804) is fitted?",
   "The vector control option and the FR-A8TP are fitted correctly; Pr.393 and Pr.862 are set right.",
   "Pr.296 is 0 or 100 while a communication option is fitted."],
  ["Correct Pr.30 and the wiring (AC power on the converter terminals may have damaged the inverter: contact the sales representative).",
   "Fit the plug-in option and check Pr.804.",
   "Fit the vector control option and the FR-A8TP correctly; set Pr.393 and Pr.862.",
   "Set Pr.296 to another value before fitting a communication option.",
   "Return the option's manufacturer setting switch to its initial position."]),
 ([("E.OP1", 161), ("E.OP2", 162), ("E.OP3", 163)], "Communication option fault",
  "A communication line fault in the option in connector 1 to 3 (the number), or a fault of the FR-A8APR or its resolver wiring; the output is shut off.",
  ["The option's function settings.",
   "Is the plug-in option seated firmly in its connector? (FR-A800-GF: the CC-Link IE Field board on the control board.)",
   "The communication cable (broken?) and its terminating resistor.",
   "FR-A8APR only: the resolver wiring."],
  ["Check the option's settings.",
   "Seat the option (the CC-Link IE Field board) firmly.",
   "Check the communication cable and fit the terminating resistor.",
   "Check the resolver wiring (FR-A8APR).",
   "The fault comes back after a reset: contact the sales representative."]),
 ([("E.16", 164), ("E.17", 165), ("E.18", 166), ("E.19", 167), ("E.20", 168)], "User definition error by the PLC function",
  "The inverter's own PLC function wrote 16 to 20 into its special register SD1214; the output is shut off (off while Pr.414 = 0, the initial setting).",
  ["Which part of the inverter's PLC program writes 16 to 20 into SD1214."],
  ["Remove the cause in the inverter's PLC program; write another value into SD1214."]),
 ([("E.PE", 176)], "Parameter storage device fault (control circuit board)",
  "The stored parameters are faulty (EEPROM failure); the output is shut off.",
  ["Too many parameter writes (e.g. written over communication all the time)."],
  ["Contact the sales representative.",
   "For frequent writes over communication, set Pr.342 = 1 (write to RAM; lost at power off)."]),
 ([("E.PUE", 177)], "PU disconnection",
  "The communication with the operation panel / parameter unit (PU connector) was broken, or the RS-485 errors on the PU connector passed the retry count / check time (Pr.75 decides whether this stops the inverter).",
  ["Is the operation panel or the parameter unit connected properly (cable, connector)?",
   "The Pr.75 setting."],
  ["Fit the operation panel / parameter unit and its cable securely."]),
 ([("E.RET", 178)], "Retry count excess",
  "The inverter could not resume within the number of retries set in Pr.67 (Pr.67 = 0: the retry function is off).",
  ["The cause of the fault that started the retries (see the inverter's fault history)."],
  ["Remove the cause of that fault."]),
 ([("E.PE2", 179)], "Parameter storage device fault (main circuit board)",
  "The stored parameters are faulty (EEPROM failure); the output is shut off.",
  ["-"],
  ["Contact the sales representative."]),
 ([("E.CPU", 192), ("E.5", 245), ("E.6", 246), ("E.7", 247)], "CPU fault",
  "A communication fault of the built-in CPU; the output is shut off.",
  ["Devices producing excessive electrical noise near the inverter (contactors without suppressors, welders, unshielded motor cables)."],
  ["Take noise countermeasures where such devices are present.",
   "Contact the sales representative."]),
 ([("E.CTE", 193)], "Operation panel power supply short circuit / RS-485 terminals power supply short circuit",
  "The power supply of the operation panel (PU connector) or of the RS-485 terminals is shorted; that output is cut.",
  ["Is the PU connector cable shorted?",
   "Are the RS-485 terminals connected correctly?"],
  ["Check the PU and its cable; check the RS-485 terminals' wiring.",
   "Reset: a PU connector short with the RES signal, an RS-485 reset or by switching the power off and on; an RS-485 terminal short with the STOP/RESET key, the RES signal or the power."]),
 ([("E.P24", 194)], "24 V DC power fault",
  "The 24 V DC output of the PC terminal is shorted: the output is cut and all the external contact inputs go OFF.",
  ["A short circuit on the PC terminal's output (the sensors and switches wired to it).",
   "Is an external 24 V supply at the correct voltage?"],
  ["Repair the shorted part.",
   "Supply 24 V at the correct voltage (a low input for a long time heats the internal circuit).",
   "Reset from the operation panel or by switching the power off and on - not with the RES signal."]),
 ([("E.CDO", 196)], "Abnormal output current detection",
  "The output current passed the Pr.150 Output current detection level (only with Pr.167 = 1).",
  ["The Pr.150, Pr.151, Pr.166 and Pr.167 settings - and why the current is that high (the load)."],
  ["Check the load; set Pr.150, Pr.151, Pr.166 and Pr.167 as the manual describes."]),
 ([("E.IOH", 197)], "Inrush current limit circuit fault",
  "The inrush current limit resistor overheated; the output is shut off.",
  ["Is the power switched on and off too often?",
   "FR-A840-03250 (110K) or larger: the 5 A fuse on the inrush contactor's supply - blown?",
   "Is the inrush contactor's supply circuit damaged?"],
  ["Make the circuit avoid frequent power on / off.",
   "Still there: contact the sales representative."]),
 ([("E.SER", 198)], "Communication fault (inverter)",
  "The RS-485 terminals' communication errors passed the Pr.335 retry count, or the communication was broken for the Pr.336 check interval.",
  ["The RS-485 terminals' wiring."],
  ["Wire the RS-485 terminals properly."]),
 ([("E.AIE", 199)], "Analog input fault",
  "30 mA or more into terminal 2, or 7.5 V or more into terminal 4, while that input is set for current (Pr.73, Pr.267).",
  ["The Pr.73 and Pr.267 settings, and the voltage / current input switches."],
  ["Feed less than 30 mA - or set Pr.73, Pr.267 and the switch to voltage input and feed a voltage."]),
 ([("E.USB", 200)], "USB communication fault",
  "The USB communication was broken for the time set in Pr.548.",
  ["Is the USB cable connected securely?"],
  ["Check Pr.548; connect the USB cable securely.",
   "Set Pr.548 longer, or 9999."]),
 ([("E.SAF", 201)], "Safety circuit fault",
  "A safety circuit fault: with the safety stop function used, the S1-SIC or S2-SIC circuit opened; without it, a shorting wire S1-PC or S2-PC is off.",
  ["Safety stop used: the safety relay module (the safety circuit) and its wiring.",
   "Safety stop not used: the shorting wires S1-PC and S2-PC."],
  ["Safety stop used: check the S1, S2 and SIC wiring and that the safety relay module works (Safety Stop Function Instruction Manual).",
   "Safety stop not used: short S1 to PC and S2 to PC with the shorting wires."]),
 ([("E.PBT", 202), ("E.13", 253)], "Internal circuit fault",
  "An internal circuit fault; the output is shut off.",
  ["-"],
  ["Contact the sales representative."]),
 ([("E.OS", 208)], "Overspeed occurrence",
  "The motor speed passed the Pr.374 Overspeed detection level (encoder feedback, Real sensorless vector, vector, PM sensorless vector control); the output is shut off. Off in the initial setting.",
  ["The Pr.374 setting.",
   "Pr.369 (Pr.851) Number of encoder pulses against the encoder fitted (encoder feedback / vector control)."],
  ["Set Pr.374 correctly.",
   "Set Pr.369 (Pr.851) correctly."]),
 ([("E.OSD", 209)], "Speed deviation excess detection",
  "Under vector control with Pr.285 set, the motor speed left the command (a load change) and could not be held; or the motor accelerated against a stop command (deceleration check, Pr.690).",
  ["The Pr.285 and Pr.853 settings.",
   "A sudden change of the load.",
   "Pr.369 (Pr.851) against the encoder's actual pulse count."],
  ["Set Pr.285 and Pr.853 correctly.",
   "Keep the load steady.",
   "Set Pr.369 (Pr.851) correctly."]),
 ([("E.ECT", 210)], "Signal loss detection (encoder)",
  "The encoder's signal was lost under orientation, encoder feedback or vector control; the output is shut off. Off in the initial setting.",
  ["The encoder signal (cable, connector).",
   "The encoder's specification.",
   "The switch on the vector control option.",
   "Is the encoder powered - not later than the inverter? Its supply voltage against its output voltage."],
  ["Remove the cause of the signal loss; connect securely.",
   "Use an encoder that meets the specification; set the option's switch correctly.",
   "Power the encoder with the inverter. If it must come later: confirm the signal, then set Pr.376 = 0 (no signal loss detection).",
   "Match the encoder's supply voltage to its output voltage."]),
 ([("E.OD", 211)], "Excessive position fault",
  "In position control the difference between the position command and the feedback passed Pr.427 Excessive level error; the output is shut off. Off in the initial setting.",
  ["The position encoder's mounting direction against the parameters.",
   "Is the load too large?",
   "The Pr.427 and Pr.369 (Pr.851) settings."],
  ["Check the parameters.",
   "Reduce the load.",
   "Set Pr.427 and Pr.369 (Pr.851) correctly."]),
 ([("E.MB%d" % i, 212 + i) for i in range(1, 8)], "Brake sequence fault",
  "A sequence fault of the brake sequence function (Pr.278 to Pr.285). Off in the initial setting.",
  ["The cause of the fault (the brake's opening / closing signals and their timing)."],
  ["Check the parameters and the wiring."]),
 ([("E.EP", 220)], "Encoder phase fault",
  "During offline auto tuning the rotation commanded and the rotation the encoder saw differ. Off in the initial setting.",
  ["The encoder cable's wiring.",
   "Pr.359 (Pr.852) Encoder rotation direction."],
  ["Wire the encoder correctly.",
   "Change Pr.359 (Pr.852)."]),
 ([("E.MP", 222)], "Magnetic pole position unknown",
  "The offset between the PM motor's magnetic pole and the encoder's home position is not known.",
  ["Was the encoder position tuning done, and did it end properly (Pr.1105 = 9999: it did not)?"],
  ["Run the encoder position tuning (Pr.373).",
   "Remove the cause of the tuning error and tune again."]),
 ([("E.EF", 224)], "External fault during output operation",
  "The X32 signal (Pr.178 to Pr.189 = 32) turned OFF while running. Off in the initial setting.",
  ["Why X32 is OFF (the external device that gives it)."],
  ["Make sure the machine can start safely, then turn X32 ON."]),
 ([("E.IAH", 225)], "Abnormal internal temperature",
  "The temperature inside the inverter reached its limit (standard and IP55 models).",
  ["The ambient temperature.",
   "Has the internal air circulation fan or the cooling fan stopped?"],
  ["Install the inverter where its environment suits it.",
   "Replace the internal air circulation fan or the cooling fan."]),
 ([("E.LCI", 228)], "4 mA input fault",
  "The analog current input stayed at 2 mA or less for the Pr.778 time (with Pr.573 = 2 or 3). Off in the initial setting.",
  ["A break in the analog current input wiring.",
   "Is Pr.778 too short?"],
  ["Check the current input's wiring.",
   "Set Pr.778 longer."]),
 ([("E.PCH", 229)], "Pre-charge fault",
  "The pre-charge took longer than Pr.764, or the measured value passed Pr.763 while pre-charging (with Pr.763 / Pr.764 set). Off in the initial setting.",
  ["Is Pr.764 too short, Pr.763 too small, Pr.127 too low?",
   "A break in the connection to the pump."],
  ["Set Pr.764 longer, Pr.763 larger, Pr.127 higher.",
   "Check the connection to the pump."]),
 ([("E.PID", 230)], "PID signal fault",
  "During PID control the measured value passed the upper / lower limit (Pr.131, Pr.132) or the deviation passed Pr.553 (Pr.554 decides the action). Off in the initial setting.",
  ["The meter (sensor) - failed or broken?",
   "The parameter settings."],
  ["Check the meter and its wiring.",
   "Set the parameters correctly."]),
 ([("E.1", 241), ("E.2", 242), ("E.3", 243)], "Option fault",
  "A contact fault between the inverter and the plug-in option in connector 1 to 3 (the number), a communication option not in connector 1, or the option's manufacturer switch changed. (FR-A800-GF: also the CC-Link IE Field board's contact.)",
  ["Is the plug-in option seated firmly in its connector?",
   "Excessive noise around the inverter.",
   "Is a communication option in connector 2 or 3?"],
  ["Seat the option firmly; move a communication option to connector 1.",
   "Take noise countermeasures.",
   "Return the option's manufacturer setting switch to its initial position.",
   "Still there: contact the sales representative."]),
 ([("E.11", 251)], "Opposite rotation deceleration fault",
  "Under Real sensorless vector control in torque control the rotation direction switched while the speed did not fall at low speed, overloading the inverter.",
  ["Was the direction switched forward / reverse during torque control under Real sensorless vector control?"],
  ["Do not switch the direction during torque control under Real sensorless vector control.",
   "Contact the sales representative."]),
 ([("E.PE6", 172)], "Internal storage device fault",
  "At an inverter reset: writing data failed because the power went OFF, or a data fault occurred in the storage device, during a parameter operation (Parameter clear, All parameter clear, Parameter copy, offline auto tuning, a parameter batch write from FR Configurator2) or while the set frequency was written.",
  ["Was the power turned OFF during a parameter operation?",
   "Is the EEPROM written too often (parameters or the set frequency written over and over by the PLC or communication)?"],
  ["Check the power supply and the devices on the power system: no fault.",
   "After a power-OFF during a parameter operation: read Pr.890. 7 or smaller - do an All parameter clear, then an inverter reset, and set the parameters that had been changed again.",
   "Any other cause (a power OFF/ON or an inverter reset), or Pr.890 is 8 or more: contact the sales representative."]),
 ([("E.ECA", 212)], "Encoder signal loss for orientation",
  "Vector control, machine end orientation control: the machine end encoder's signal was lost; the output is shut off. Not active in the initial setting.",
  ["Is the encoder signal lost (cable, connector)?",
   "Are the encoder's specifications correct?",
   "Is a connector loose?",
   "Is the switch of the Vector control compatible option set correctly?",
   "Is the encoder powered - and not powered later than the inverter?",
   "Is the encoder's supply voltage the same as its output voltage?"],
  ["Remedy the signal loss.",
   "Use an encoder that meets the specifications.",
   "Make the connection securely.",
   "Set the Vector control compatible option's switch correctly (manual page 89).",
   "Power the encoder, at the same time as the inverter. If it is powered later: check that its signal comes properly, then set Pr.376 Encoder signal loss detection enable/disable selection to 0 (the initial value) to disable the detection.",
   "Make the encoder's supply voltage the same as its output voltage."]),
]

STYLE = """ body{font-family:"Segoe UI",Arial,sans-serif;max-width:1100px;margin:1.5em auto;padding:0 1.5em;color:#23262c;line-height:1.45;font-size:14px}
 h1{font-size:1.45em;border-bottom:2px solid #1e78dc;padding-bottom:.3em}
 h2{font-size:1.2em;margin:1.6em 0 .5em;background:#f2f5f9;padding:.45em .7em;border-left:4px solid #1e78dc}
 header.alarm{margin:1.6em 0 .5em;background:#f2f5f9;padding:.45em .7em;border-left:4px solid #1e78dc}
 header.alarm h2{margin:0;padding:0;background:none;border:0}
 table{border-collapse:collapse;width:100%;margin:.5em 0 .8em}
 th,td{border:1px solid #9aa1ab;padding:.35em .5em;vertical-align:top;text-align:left}
 th{background:#e3e8ef;font-weight:600;width:11em}
 td ol{margin:.1em 0 .1em 1.4em;padding:0} td li{margin:.1em 0}
 .src{color:#5b6270;font-size:.9em} .fn{font-size:.92em;color:#3b4250}
 .missing{background:#fff3cd;border-left:4px solid #d7a528;padding:.7em 1em;margin:1em 0}
 .first{background:#eef6ee;border-left:4px solid #3c9a46;padding:.6em 1em;margin:.6em 0}
 @media print{h2{break-after:avoid}section{break-inside:avoid-page}}"""

FIRST = ("Before anything else: note the code, then RESET the inverter (its STOP/RESET key, the RES signal, "
         "or the machine's alarm reset) only after the cause is found - a fault that is reset and comes back "
         "at once is still there. Work on the motor or its cable only with the inverter's power OFF and its "
         "charge lamp out (wait at least 10 minutes).")


def e(t):
    return html.escape(t, quote=True)


def section(display, code, name, when, checks, actions, others):
    key = str(code) if code is not None else display
    hexs = (" (H%02X)" % code) if code is not None else ""
    data_code = ("%d%s" % (code, hexs)) if code is not None else "not in the source list - see 6.4 of the manual"
    out = ['<section class="code" data-code="%s" data-group="%s" data-kind="alarm" data-alarm="%s" data-name="%s">'
           % (e(key), e(key), e(display), e(name))]
    out.append('<header class="alarm"><h2>%s &mdash; %s</h2><div>Data code %s</div></header>' % (e(display), e(name), e(data_code)))
    out.append('<div class="first">%s</div>' % e(FIRST))
    out.append('<table>')
    out.append('<tr><th>Operation panel</th><td><b>%s</b>%s</td></tr>' %
               (e(display), (" &nbsp;<span class=\"fn\">(also %s)</span>" % e(", ".join(others))) if others else ""))
    out.append('<tr><th>Name</th><td>%s</td></tr>' % e(name))
    out.append('<tr><th>Description</th><td>%s</td></tr>' % e(when))
    out.append('<tr><th>Check point</th><td><ol>%s</ol></td></tr>' % "".join("<li>%s</li>" % e(c) for c in checks))
    out.append('<tr><th>Corrective action</th><td><ol>%s</ol></td></tr>' % "".join("<li>%s</li>" % e(a) for a in actions))
    out.append('</table>')
    page = PAGE.get(code)
    if page:
        at = "#page=%d" % (page + PDF_PAGE_OFFSET)
        out.append('<div class="src">FR-A800 Instruction Manual (Detailed), IB-0600503ENG, 6.5 Causes and corrective actions, '
                   'page %d: <a href="%s%s">open the page</a> (the copy in the Help folder) or <a href="%s%s">online</a></div>'
                   % (page, MANUAL_FILE, at, MANUAL_URL, at))
    else:
        out.append('<div class="src">FR-A800 Instruction Manual (Detailed), IB-0600503ENG, 6.5 Causes and corrective actions - '
                   '<a href="%s">the manual</a> (the copy in the Help folder: <a href="%s">%s</a>)</div>'
                   % (MANUAL_URL, MANUAL_FILE, MANUAL_FILE))
    out.append('</section>')
    return "\n".join(out)


def main():
    parts = ['<!DOCTYPE html>', '<html lang="en"><head><meta charset="utf-8">',
             '<title>Inverter faults (FR-A800)</title>',
             '<!-- GENERATED by tools/build_inverter_help.py - edit the script, not this file, or the next run will undo the\n'
             '     edit. A translation (.tr.html) is a copy of this file and is not touched by the script. -->',
             '<style>', STYLE, '</style>', '</head><body>',
             '<h1>Inverter faults (FR-A800)</h1>',
             '<p class="src">The inverter\'s fault as the PLC reads it: the <b>data code</b> of 6.4 "List of fault displays" '
             '(decimal; 16 = E.OC1). The checks and remedies follow 6.5 "Causes and corrective actions" of the '
             '<a href="%s">FR-A800 Instruction Manual (Detailed)</a> - the manual is the reference.</p>' % MANUAL_URL]
    count = 0
    for codes, name, when, checks, actions in F:
        for display, code in codes:
            others = [d for d, _ in codes if d != display]
            parts.append(section(display, code, name, when, checks, actions, others))
            count += 1
    parts.append('</body></html>')
    text = "\r\n".join(p.replace("\n", "\r\n") for p in parts) + "\r\n"
    with open(OUT, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    print("wrote %s: %d sections" % (os.path.normpath(OUT), count))


if __name__ == "__main__":
    main()
