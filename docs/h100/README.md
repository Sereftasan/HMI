# H100 — extracted from the old program

`H100_device_map.csv` — every PLC-bound control in the old `_H100` project:
**76 controls, 68 with the on-screen caption recovered.**

Columns: `page; control; device; data_type; divisor; floating_point; digits;
mask; read_mode; button_type; caption`

Captions were recovered by pairing each bound control with the nearest label on
the same screen row, preferring one to its left — how these pages are laid out.
The 8 without a caption are lamps and status cells that never had one.

## The pages to rebuild

| Old page | Bound controls | New-program equivalent |
|---|---|---|
| `ucScrewingEntryConveyor` | 31 | Manual Movement |
| `ucScrewingParameter` | 27 | Machine Parameters |
| `ucScrewingConfiguration` | 9 | Machine Parameters (2nd tab) |
| `ucScrewingAxisPositions` | 2 | Home / positions |
| `ucScrewingControls` | 2 | Home |
| `ucScrewingMainControlPanel` | 2 | Home |
| `ucScrewingHeadControlButtons` | 1 | Manual Movement |
| `ucAlarmsScrewing` / `AlarmPanelScrewing` | 2 | Alarms |

Device families: **M** (35 bits) and **D** (32 words). Word controls are
`Bit_16` except D172/D330/D334/D338 which are `Bit_32` — those four are the
32-bit ones, and they are the axis/position values.

## What this file does NOT answer: the divisors

Every numeric control in the old H100 has `Divisor = 1` and `FloatingPoint = 0`.

**The old program does no scaling at all** — the PLC holds each value already in
display units. So there is nothing here to copy: the divisors that need
correcting in the new program cannot be derived from this file, because the old
one never had any.

Setting a divisor correctly needs one fact per device that is not in either
codebase: *what the PLC register actually holds*. `D22` reading `1234` is
12.34 mm with `Divisor = 100`, or 1234 mm with `Divisor = 1`, and nothing in the
source distinguishes them. That comes from the PLC program or the machine
documentation.

