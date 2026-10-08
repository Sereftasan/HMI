# The old FA2010 HMI's pages

The page designers of the HMI program the FA2010 runs today, sent on 8 October
as the specification of the new program's Manual Movement and Machine
Parameters pages:

| Old page (designer)             | New page                                   |
|---------------------------------|--------------------------------------------|
| ucEntryConveyor                 | Manual Movement - Entry Conveyor           |
| ucRobot                         | Manual Movement - Robot                    |
| ucProfileRecognition            | Manual Movement - Profile Recognition      |
| ucMilling                       | Manual Movement - Milling                  |
| ucCutting                       | Manual Movement - Cutting                  |
| ucExit                          | Manual Movement - Exit                     |
| ucOther *(given as text)*       | Manual Movement - Other                    |
| ucClampingMoulds                | Manual Movement - Clamping Moulds          |
| ucYZCrossRollers *(as text)*    | Manual Movement - YZ Cross Rollers         |
| ucAxisParameters                | Machine Parameters - Axis Parameters (AxisSettings, edited by hand) |
| ucEntryParameters               | Machine Parameters - Entry Parameters      |
| ucProcessParameters             | Machine Parameters - Process Parameters    |
| ucEncodersParameters            | Machine Parameters - Encoders Parameters   |
| ucOtherParameters               | Machine Parameters - Other Parameters      |
| ucParametersEntryPw *(as text)* | - (the HMI's sign-in and security levels)   |

`tools/legacy_fa2010_pages.py` holds them row by row and writes the pages;
`python3 tools/legacy_fa2010_pages.py --check` compares that specification with
these files, device by device (buttons, lamps, values, their divisors and
decimals, which side a button is on). `left_out.txt` lists what the earlier
pages (made from the old device list) had and these do not.

Where the specification differs from the old pages on purpose, the tool says why
at the row (and in `EXPECTED`): the lamps the electrical drawings place
differently (30 September, REVIEW 4r), the Encoders page's Robot Gripper row
(the old page jogged the Exit's top clamping), the reference sensors of the axes.
