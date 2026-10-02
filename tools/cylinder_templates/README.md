# Cylinder step-by-step help: the templates

When a cylinder alarm's help is opened, the HMI writes a step-by-step page for the
movement that failed. Since 2 October the words of that page are not in the
program: they are in a **template per valve and feedback pair, per language**.

    KabanHMI/Help/Templates/EN/Cylinder_<valve>_<feedback>.html    (copied beside the program: Help\Templates\EN)
    KabanHMI/Help/Templates/TR/Cylinder_<valve>_<feedback>.html

| valve part      | the unit's valve                      | feedback part      | the unit's feedback                          |
|-----------------|---------------------------------------|--------------------|----------------------------------------------|
| `Valve-NotSet`  | not set                               | `Feedback-NotSet`  | not set (the unit's lamps are used)          |
| `5-2-Double`    | 5/2 double solenoid                   | `OneMagnetic`      | one magnetic sensor                          |
| `5-2-Spring`    | 5/2 single solenoid, spring return    | `TwoMagnetic`      | a magnetic sensor at each end                |
| `5-3`           | 5/3, centre position                  | `OneInductive`     | one inductive sensor                         |
|                 |                                       | `Analogue`         | analogue position sensor (4-20 mA)           |
|                 |                                       | `PressureSwitch`   | magnetic sensor + pressure switch (clamping) |
|                 |                                       | `Encoder`          | encoder + reference sensor                   |
|                 |                                       | `NoSensor`         | no sensor: only the time confirms the move   |

The numbers - output, card terminal, relay, coil, valve, hoses, cylinder,
sensors, wires and sheets - are **not** in the templates: they come from the
unit (its command, output and lamps) and from the machine's `Wiring.csv`, as
the Inputs / Outputs page uses them. A text of the drawings can have its own
column per language (`Measure.TR`, `Pressure.TR`, `Via.TR`); the drawings' own
labels stay as drawn and the Turkish page marks them `çizimde:`.

The HMI uses the template of the HMI's language and falls back to English when
a language has none. A template changed while the program runs is used for the
next page opened.

## The page (2 October)

* **The title is the alarm** as the alarm list shows it; under it, small and in
  italics, when the alarm came and when the page was made. An Admin also sees
  the alarm's bit after the title - `(M873)`.
* **On the left, the signal path** from the PLC's output down to the sensor that
  confirms the end - PLC output, relay, coil, valve, air, cylinder, sensor - each
  box a link to its check, with its step number. The HMI's command (the button's
  bit) is not on it, nor anywhere on the page: the help is for the real
  operation, which that bit has no part in.
* **Each check has a drawing** of where the number is on the machine and how it
  is checked (the card's LED row, the terminal and the meter, the relay on its
  rail, the coil connector, the 5/2 valve and its override, the air unit, the
  cylinder and its sensors, the input's LED), and a few short lines with a
  picture for what to do: look, measure, by hand, listen, make the movement
  again.
* **The PLC's internal devices** (M, D, ...) are named only when an Admin is
  signed in; its real inputs and outputs (X34, Y21) always.

The drawings and the page's look are shared by the languages:
`tools/cylinder_templates/Drawings.html`, put into each template by the build
script where a master has its include line. Each drawing is a phrase
(`DrawIoPage`, `DrawCard`, `DrawTerminal`, `DrawRelay`, `DrawCoil`, `DrawValve`,
`DrawAir`, `DrawCylinder`, `DrawSensor`; the look is `Style`); their few words
are each master's `W_IoPage`, `W_Press`, `W_ValveShort`, `W_FlowControl`,
`W_AirSwitch`. They are drawn after the machine's own parts, but they show where
to look - not one machine exactly.

## Changing the words

Open the pair's file in a text editor (it reads as a page in a browser, with
the fields still in it) and change the words. Keep:

* the `{{...}}` marks - they are where the unit's values go, and decide what is
  shown;
* each `<div class="step" id="A1" data-part="A" ...>` line and the
  `<a class="go" href="#A3"></a>` links - the program numbers the steps, leaves
  out the steps an alarm the HMI raised itself does not need, and points each
  answer at the step it names.

A change for **every** pair is made in the master -
`tools/cylinder_templates/Cylinder_master.en.html` / `.tr.html` - and written
out with

    python3 tools/build_cylinder_templates.py            # writes the pairs' files and the projects' file lists
    python3 tools/build_cylinder_templates.py --check    # only says what would change

The script remembers what it wrote (`.generated`) and does **not** write over
a pair's file changed by hand since: it names those files - carry the change
into the master first, or pass `--force`.

## The syntax

| mark                          | meaning                                                                 |
|-------------------------------|-------------------------------------------------------------------------|
| `{{Name}}`                    | a value, already safe for HTML                                          |
| `{{#Name}} .. {{/Name}}`      | shown when Name is set / true; for a list, once per item                |
| `{{^Name}} .. {{/Name}}`      | shown when Name is not set / false                                      |
| `{{>Name}}`                   | a phrase defined in the file: `<!--def Name--> .. <!--/def-->`          |
| `{{[SEP}} a {{|}} b {{]}}`    | the parts that are not empty, joined by SEP                             |
| `{{! .. }}`, `<!--doc .. -->` | notes, left out                                                         |

A line with only section marks on it leaves no line in the page.

Some phrases are used by the program itself to build small pieces - a
sensor's name (`SensorName`), "Step 4" (`StepLink`), ON / OFF (`On`, `Off`),
the words of an electrical line (`Wire`, `Sheet`, `Relay`, `CoilWord`...), the
movement words of the drawings translated (`MoveWords`: `Up = Yukarı`) and the
words of the drawings' data (`DataWords`: `double solenoid = çift bobinli`).
Every template must define them (the test `tools/ui_test/CylinderTemplates.cs`
checks it).

## The fields

The movement: `Unit`, `Move`, `OtherMove`, `Glyph` / `ButtonSign` (the button's
arrow, or its word), `OtherGlyph` / `OtherSign`, `HasButton`, `Spring` (a spring
valve's return: its coil must switch OFF), `Conditions` (what the movement waits
for; their rules only for an Admin). `Command` and `OtherCommand` are always
empty since 2 October (kept so an older template still reads).

The output and the valve: `Out`, `OutText`, `OutWire`, `OutSheet`, `OutCard`,
`OutTerminal`, `OutTerminalOther`, `OtherOut`, `Relay`, `RelaySheet`, `Coil`,
`CoilWire`, `CoilSheet`, `ValveTag`, `PnSheet`, `CylName`, `Hoses`, `HosesMany`,
`ClosedCentre`, `OpenCentre`, `Pressure`, `Measure`, `Position`, `ZeroWire`,
`SensorWire`, `MainAir`, `MainAirTag`, `MainAirCode`, `MainAirText`.

The sensors: `HasMustOn` / `MustOn` (the sensors that must come ON - a list
with `Name`, `Where`, `Text`, `Io`, `Dev`, `CopiedTo`, `IsSwitch`, `Wire`,
`Card`, `Key`, `Next`, `Last`), `HasMustOff` / `MustOff`, `HasStart`,
`HasSensors`, `StartAndOn`, `MustOnMany`, `EndNotSensed`, `OnNames`, `OnSlash`,
`OffNames`, `OffSlash`, `StartNames`, `StartSlash`, `OffSwitch`, `OffKey`,
`TimeKey`, `HasRef`, `RefName`, `RefWhere`, `RefIo`.

For the drawings: `OutPlain` (Y21), `OutCardTag` (U4), `OutCardModel`, `OutCpu`
(an FX5U CPU or a module), `OutHasLed`, `OutLed` (21) and the LEDs beside it in
its row `OutLedL2`, `OutLedL1`, `OutLedR1`, `OutLedR2`, `OutLedTop`;
`RelayL2` .. `RelayR2` (the relays beside it on the rail); `HoseA`, `HoseB`,
`HoseANo` (16), `HoseAPort` (A), `HoseBNo`, `HoseBPort`; `OnTag`, `StartTag`,
`SwitchTag`; per sensor (in `MustOn`, `MustOff`) `TagPlain`, `IoPlain`, `InCardTag`,
`InCardModel`, `InCpu`, `InHasLed`, `InLed`, `InLedL2` .. `InLedR2`, `InLedTop`.

The alarm: `Title` (the alarm's text), `Admin`, `HasError`, `Error` and
`ErrorShown` (the alarm's bit - empty below Admin), `ErrorText`, `AlarmTime`,
`AlarmDate`, `Created` (when the page was made),
`HmiAlarm` (raised by the HMI itself), `Failure1` .. `Failure6` (which signal it
saw fail), `LookF`, `SeenRising`, `Seen` / `SeenRows` (the states at the alarm
and now), `Hint`, `StartKey`, `StepOneNote`, `Why...` (where to start, and why).

The parts list: `HasParts`, `OutLine`, `OtherLine`, `PneuLine`, `PartSensors`,
`MainAirLine`, `Drawn`.

The pair (decided in a pair's file, set by the program for the master): `V0`
.. `V3`, `F0` .. `F7`, `Double`, `FiveThree`, `SpringValve`, `PositionFb`,
`MagneticFb`, `InductiveFb`, `SwitchFb`.

## For a review

`tools/review/build_review.py <bin folder> <out folder>` writes every cylinder
alarm's page, as the HMI opens it, in Turkish and in English, with an index
per language.
