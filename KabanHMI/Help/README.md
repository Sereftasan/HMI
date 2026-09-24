# Help documents

Files the alarm screen's **Help** column opens. They live beside the machine's
own files rather than on a share, because the network is unreachable exactly when
something has gone wrong.

Which document answers which kind of fault is set once per machine in
`TroubleshootingCatalog.DocumentForType` — not named on every alarm, or a manual
revision would mean editing dozens of entries.

## The code on an alarm

Three digits: `errorType . param1 . param2`

| Code | Meaning |
|---|---|
| `1.v.f` | cylinder movement — `v` valve type, `f` feedback type |
| `2.n.0` | servo fault on axis `n` |
| `3.0.0` | air pressure |
| `4.0.0` | safety circuit |
| `5.0.0` | inverter / motor |
| `9.0.0` | general |

**Valve** (`v`): 1 = 5/2 double solenoid, 2 = 5/2 single solenoid spring return,
3 = 5/3 centre position (4, the old "open centre", reads as 3).

**Feedback** (`f`): 1 = single magnetic, 2 = magnetic both ends, 3 = single
inductive, 4 = analogue magnetic, 5 = magnetic + pressure switch, 6 = encoder +
reference, 9 = none (timed).

So `1.2.1` opens the cylinder manual at the general section, **§1.4 for that
valve** and **§1.7 for that sensor** — the two choices the manual asks the reader
to make, answered from what the builder already knew.

`2.n.0` opens the drive manual; the drive's own alarm number is on the axis
settings page, shown in the manual's own hexadecimal form (`50.2`, `1A.5`).

An alarm may carry several codes, and `HelperKeys` names one-off files outright.
Rows with nothing to open show no button.

## A cylinder unit's own manual

A `FunctionUnitHMI` says what it is fitted with: **ValveType** and **FeedbackType**
(Properties window, *Help*). Its `?` - and the alarm list's help for its error
bits - then opens a copy of `Cylinder_Troubleshooting.html` with:

- only that valve's block of detail 1.4 and that feedback's block of 1.7;
- the details joined into ONE table under one heading row, 1.1 to 1.8
  (`TroubleshootingCatalog.Tailor` / `JoinBlocks`; each detail stays a
  `<tbody data-detail="...">`);
- for an alarm, a box at the top naming **the movement that failed** (ErrorA =
  button A, ErrorB = button B): its PLC bit, valve output, the sensors that should
  turn on (that button's lamps) and off (the other button's lamps).

The manual keeps its variants in `<section class="block" data-detail="1.4"
data-variant="valve:n">` / `feedback:n` - keep that markup when editing it. With
neither type set, the full manual opens.


## Axis codes: one entry, not the whole manual

The three code columns of Axis Settings each have their own document, built from
the manufacturer's manual by `tools/build_code_help.py`:

| Column | Document | From |
|---|---|---|
| Axis alarm (`1904H`) | `Axis_Error_Codes.en.html` | FX5-40SSC-S manual 12.5 (`FX5_40SSC_Troubleshooting.pdf`) |
| Axis warning (`0900H`) | `Axis_Warning_Codes.en.html` | FX5-40SSC-S manual 12.4 |
| Servo alarm (`16.3`) | `Servo_Alarm_Codes.en.html` | MR-J4 troubleshooting 1.4 / 1.5 (`MR_J4_TroubleShooting.pdf`) |

Each code is a `<section class="code" data-code="...">`; the HMI shows only the
section of the code on the screen (a servo detail that is not listed shows its
alarm's sections). Run the script again when a manual is replaced. A translation
is a copy named `.tr.html` and is left alone by the script.

## The HMI manual

`Kaban_HMI_Manual.en.html` names no machine. Opened from HMI Settings > About it
is filled in with the machines the program runs, plus each machine's own chapter:
`Manual_<ModuleId>.<language>.html`, kept in that machine's project `Help` folder.
