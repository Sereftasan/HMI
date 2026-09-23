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
3 = 5/3 closed centre, 4 = 5/3 open centre.

**Feedback** (`f`): 1 = single magnetic, 2 = magnetic both ends, 3 = single
inductive, 4 = analogue magnetic, 5 = pressure switch, 9 = none (timed).

So `1.2.1` opens the cylinder manual at the general section, **§1.4 for that
valve** and **§1.7 for that sensor** — the two choices the manual asks the reader
to make, answered from what the builder already knew.

`2.n.0` opens the drive manual; the drive's own alarm number is on the axis
settings page, shown in the manual's own hexadecimal form (`50.2`, `1A.5`).

An alarm may carry several codes, and `HelperKeys` names one-off files outright.
Rows with nothing to open show no button.

