# AxisSettingHMI

A **designer control**, not a page generator. Drop it on a form, set its
addresses in the property grid, and it lays itself out — `OnHandleCreated` builds
the row, so nothing has to be called from code behind.

```
axis | backlash | ◀ | position | ▶ | min | max | reference | calibration |
calibrate | send-ref | current | Z distance
```

It draws **no column headings**. Those are ordinary Labels on the page, so they
can be edited, moved or removed — a control should not dictate the page around it.

There is no `SectionName` either: `AxisName` is the label, and a page that wants
"Robot" or "Milling 1" above a group adds its own Label.

## Three value groups, because they are three different quantities

| Group | Fields | Why separate |
|---|---|---|
| **Main** | position, min, max, reference, calibration | Same physical quantity on the same axis — one divisor, one decimal setting, one digit count, entered once |
| **Backlash** | `BacklashDivisor`, `BacklashDecimalPlaces`, `BacklashDataType` | Hundredths of a millimetre in a narrower register; on the position's divisor it would read `0.00` on every axis |
| **Meters** | `CurrentDivisor`/`FullScale`, `ZDistanceDivisor`/`FullScale` | Motor current is a servo-load percentage from a status word; Z-signal distance is a millimetre offset. Different families, different scales |

The meters show the **real reading** as text ("3.20 mm") while the bar shows the
proportion — an engineer needs the number; the bar is only the picture.

**Reference and calibration take their entry limits from the axis's own MIN/MAX
words**, read live. Raise a travel limit and the reference range widens with it.
A reference outside travel is a machine that homes into a hard stop.

Empty address = field not drawn, exactly like `CylinderUnitHMI`.

## FA2010 — real devices

`Kaban.Machines.FA2010/Screens/AxisSettings.Designer.cs` is generated from
**`tools/FA2010_DeviceList.xls`, sheet `AxisParam`** — the machine's own axis
table. All 16 axes, every device real:

| | |
|---|---|
| position | `W0`, `WB0`, `WC0`, `W10` … |
| reference value / button | `D316` / `B0` (Alternate) |
| calibration value / button | `D580` / `M140` (SET) |
| limits | `D314` / `D312` |
| motor current | `WA`, `WBA`, `WCA` … |
| Z-phase offset | `D73`, `D85`, `D86` … |

Divisor and decimals come from each axis's own **Katsayı** and **Gösterim**
columns, so the angle axes (`Freze 1 Açı`, `Freze 2 Açı`, `Kesim Açı`) correctly
get ÷10000 with 3 decimals and a degree suffix while the linear axes get ÷1000
with 2 and mm. That distinction is in the machine's table and would have been
lost by assuming one rule for all.

Backlash is empty on every FA2010 row — the sheet has no backlash device, which
matches it being set in the drive.


## Column grouping

The row reads as five groups, separated by wider gaps:

```
axis ‖ ◀ position ▶ ‖ min max reference ‖ calibration cal ref ‖ backlash ‖ current Z
```

Those are the things an engineer sets together. Sixteen evenly spaced fields is a
row nobody can scan; the gaps do the grouping a box around each set would cost
vertical space to draw.

**Reference activation is a checkbox**, not a button — it is an Alternate bit the
operator arms and leaves armed, and a button that stays pressed looks broken.

**Jog arrows follow the mechanism.** `VerticalMovement = true` turns them into
▲/▼, so an operator jogging a head downward is not looking at a left arrow.

## FA2010 — real devices throughout

- Positions, references, calibrations, limits, motor current and Z-phase offsets
  from `FA2010_DeviceList.xls` sheet **AxisParam**.
- **Jog bits from the `ManualMovement` sheet** — the *same* bits the manual pages
  drive, so an axis behaves identically on both screens.
- Divisors follow the machine convention: **10000 for a millimetre axis, 100000
  for an angle**, with 2 and 3 decimals respectively.
- Axis names as the machine calls them: Robot X/Y/Z, Milling 1 Angle, Milling
  1X/1Y/1Z, Milling 2X/2Y/2Z, Cutting Angle, Cutting Z, V Cutting Y/Z, Exit X.

Backlash is empty on every row — the sheet has no backlash device, consistent
with it being set in the drive.

