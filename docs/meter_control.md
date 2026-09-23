# MeterHMI / MeterInputHMI

## One control or two? — two, sharing a base

A single control with an "Editable" switch would carry Minimum, Maximum, Step
and the arrow buttons on **every** instance, including the hundred that only ever
show a servo load. Half the property grid would do nothing — and a property that
does nothing on most instances is one somebody eventually sets on the wrong one.

The deciding argument is what a mistake costs: an editable meter **writes to the
PLC**. Making that one checkbox away from a read-only monitor means a single
mis-set flag turns a display into a control surface on a live machine.

So `MeterHMI` monitors, `MeterInputHMI` commands, and `MeterBaseHMI` holds what
is genuinely shared — the scale, the colour bands, the drawing and the tooltip.
They look identical because they should; they differ where it matters, which is
whether they can move anything.

`MeterInputHMI` adds up/down arrows (nudging a speed by 5% should be one tap, not
an on-screen keyboard over the value you are watching), typed entry, and a
**clamp applied before the write leaves** so a held arrow or a mistyped figure
cannot command a speed the machine cannot take.

## Band shapes — two different judgements

A single warning/alarm pair cannot express what these machines need.

| Shape | Meaning | Example |
|---|---|---|
| `Rising` | more is worse | **motor current** — green to 60, amber to 80, red beyond |
| `Falling` | less is worse | air pressure |
| `Window` | **both ends** are bad, the middle is the target | **reference Z distance** — red 0–10 and 90–100, amber 10–20 and 80–90, green 20–80 |

`Window` is the one that forced the redesign. The Z-signal distance should sit in
the *middle* of a motor revolution: stopping at 2% or at 98% both mean the next
reference could land on the other side of the pulse. Forcing that into a
warning/alarm pair would put green at one end of a scale where one end is the
worst place to be.

`UseCurrentBands()` and `UseZDistanceBands()` set those two presets in one call.

The zones are painted **faintly behind the bar**, not just marked with ticks, so
an operator can see how much room is left before trouble — and, for a Window
meter, that the safe place is the middle.

## Tooltip

A standard progress bar cannot say what it is showing, so a bar reading 60% is a
number with no provenance. Hovering gives the caption, the device, the live
value, the station, the scale and the band thresholds.

