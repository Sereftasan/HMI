# H800 — generated from the machine's own label list

`Kaban.Machines.H800/PlcLabels.csv` is the real thing: 302 labels exported from
`H800_LabelList.xlsx`, in the same `Label;DataType;Address` format the rest of
the program already uses.

`tools/build_h800_pages.py` turns it into four pages — **258 bound controls**:

| Page | Controls | What it holds |
|---|---|---|
| `ManualMovement` | 48 | 13 movement units + 35 single commands |
| `MachineStatus` | 106 | sensor and output lamps, axis position readouts |
| `MachineParameters` | 74 | the `PC_` word settings |
| `MachineOptions` | 30 | the `PC_` option switches |

## Why the label list beats reading the old screens

The old designer files give an address plus whatever caption happened to sit
next to a control. A label list carries the **engineer's own name**, and those
names encode the machine:

```
MAN__X1_Ileri / MAN__X1_Geri   one axis, two directions  -> a movement unit
IN_X1_RefSN                    that axis's reference sensor -> its lamp
X1_AktPoz                      that axis's actual position  -> its readout
```

So the six axes (X1 Y1 Z1 X2 Y2 Z2) assemble themselves complete with sensor and
position, by meaning rather than by screen position.

Turkish device names are translated by a word map in `tools/legacy_labels.py`
(`Ileri`→Forward, `Sikma`→Clamping, `Matkap`→Drill …). Unmapped words keep the
engineer's own spelling, which is better than a wrong guess.

## Control choice — the point of the exercise

- **movement pair** → `CylinderUnitHMI` (two commands, sensor lamp, position)
- **single command** → `ButtonHMI` with `Lamp1Address` on its own coil, so the
  button lights while the relay is energised
- **sensor / output** → `LampHMI`
- **`PC_` bit** → `CheckBoxHMI`
- **`PC_` word** → `NumericInputHMI`; read-only word → `NumericDisplayHMI`

## Two things deliberately not guessed

**Divisors** are uniform by data type — 32-bit encoder words ÷10000, 16-bit
settings ÷10. The previous FA2010 pages carried four different divisors with no
pattern, which is harder to correct than one consistent assumption.

**Angles are `UnitType.None` with a `°` suffix**, not `Length`. Marking an angle
as Length puts it through the mm/inch conversion, so switching units would
silently rescale every angle on the page.

