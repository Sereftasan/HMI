#!/usr/bin/env python3
"""
FA2010's cylinder units checked against the machine's drawings (29 September).

Every CylinderUnitHMI on the FA2010 pages is given the valve and the feedback
the drawings show for the cylinder its command bits drive, and its lamps are
set to that cylinder's sensors - the drawings are the machine as it is now; the
pages came from an older machine and were never fully tested.

  * Which cylinder: the command bit's label in the PLC program (MAN_x on M..)
    names the output it drives (OUT_x on Y..) - Wiring.csv's Commands column -
    and the output is on the electrical drawing (134581 rev. 9) with its valve
    on the pneumatic drawing (134628 rev. 9).
  * Valve: 5/2 double solenoid -> DoubleSolenoid, 5/2 single -> SingleSolenoid,
    5/3 -> ThreeWayClosedCenter (8 October: the machine's 5/3 valves are
    closed centre; ThreeWayOpenCenter for an open-centre one).
  * Feedback: the sensors the electrical drawing wires for that cylinder; an
    encoder (sheet 11) or an analogue sensor (sheets 34, 35) for its position.
  * Lamps: a sensor is read at the relay the PLC copies it into (IoList.csv:
    X51 -> M225), as every lamp on these pages is.

NOT CHANGED here, and listed in REVIEW (4r) for a decision: command bits
(they are the machine's behaviour), titles, positions, and units whose
cylinder is not on the drawings or whose title does not match what their
command bits drive.

Run from the repository root. Prints what it changed; idempotent.
"""
import re, sys

CTL = 'Kaban.UI.Controls.'
PAGES = 'Kaban.Machines.FA2010/Screens/Manual/'

D, S, T3 = 'DoubleSolenoid', 'SingleSolenoid', 'ThreeWayClosedCenter'
SM, SI, TWO, PSW, ANA, ENC, NONE = ('SingleMagnetic', 'SingleInductive', 'TwoEndMagnetic', 'MagneticPlusPressureSwitch',
                                    'AnalogueMagnetic', 'EncoderPlusReference', 'NoSensor')
CLEAR = None

# page, unit, valve, feedback, {lamp property: relay or CLEAR}, why
UNITS = [
    # ---------------------------------------------------------------- molds --
    # Every mold cylinder is on a 5/3 open-centre valve with piloted check
    # valves (pneumatic sheets 6 - 8). Its position is an SDAT-MHS analogue
    # sensor (sheets 34, 35) - or, for the three back forward-reverse molds of
    # profile recognition, milling / cutting and cutting / output, an encoder
    # with a reference sensor (sheet 38), wired to the second controller. The
    # "lamps" were the mold's tolerance (D75x) and target (D15x) words - the
    # device list's mold rows have other columns than the cylinder rows.
    ('ManualClamping_Moulds', 'unitLeft_Profile_Recognition_Front_Vertical', T3, ANA,
     {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'P37, Y73/Y74; SN110 (wire 700)'),
    ('ManualClamping_Moulds', 'unitLeft_Profile_Recognition_Back_Vertical', T3, ANA,
     {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'P38, Y75/Y76; SN115 (wire 708)'),
    ('ManualClamping_Moulds', 'unitLeft_Profile_Recognition_Back_Horizontal', T3, ANA,
     {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'P39, Y77/Y78; SN116 (wire 709)'),
    ('ManualClamping_Moulds', 'unitConveryor_Front_Vertical', T3, ANA,
     {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'P40, Y79/Y80; SN111 (wire 701)'),
    ('ManualClamping_Moulds', 'unitConveryor_Back_Vertical', T3, ANA,
     {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'P41, Y81/Y82; SN117 (wire 710)'),
    ('ManualClamping_Moulds', 'unitConveryor_Back_Horizontal', T3, ANA,
     {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'P42, Y83/Y84; SN118 (wire 711)'),
    ('ManualClamping_Moulds', 'unitProfile_Recognition_Front_Vertical', T3, ANA,
     {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'P40, Y85/Y86; SN112 (wire 702)'),
    ('ManualClamping_Moulds', 'unitProfile_Recognition_Back_Vertical', T3, ANA,
     {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'P40, Y87/Y88; SN119 (wire 712)'),
    ('ManualClamping_Moulds', 'unitProfile_Recognition_Back_Horizontal', T3, ENC,
     {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'P42, Y89/Y90; encoder + reference SN109 on the 2nd controller'),
    ('ManualClamping_Moulds', 'unitCutting_Left_Front_Vertical', T3, ANA,
     {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'P40, Y91/Y92; SN113 (wire 703)'),
    ('ManualClamping_Moulds', 'unitCutting_Left_Back_Vertical', T3, ANA,
     {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'P40, Y93/Y94; SN120 (wire 713)'),
    ('ManualClamping_Moulds', 'unitCutting_Left_Back_Horizontal', T3, ENC,
     {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'P42, Y95/Y96; encoder + reference SN.110 on the 2nd controller'),
    ('ManualClamping_Moulds', 'unitCutting_Right_Front_Vertical', T3, ANA,
     {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'P40, Y97/Y98; SN114 (wire 704)'),
    ('ManualClamping_Moulds', 'unitCutting_Right_Back_Vertical', T3, ANA,
     {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'P40, Y99/Y100; SN121 (wire 714)'),
    ('ManualClamping_Moulds', 'unitCutting_Right_Back_Horizontal', T3, ENC,
     {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'P42, Y101/Y102; encoder + reference SN111 on the 2nd controller'),
    # The two small presses on this page drive the top small clampings (M699 /
    # M698 -> Y111 / Y110, M701 / M700 -> Y113 / Y112): 5/2 double solenoid,
    # one up sensor each. Their D765 / D165 words were mold columns too.
    ('ManualClamping_Moulds', 'unitCutting_Left_Top_Small_Press', D, SM,
     {'Lamp1AAddress': 'M230', 'Lamp1BAddress': CLEAR}, 'P1, Y3/Y4; SN64 on X56'),
    ('ManualClamping_Moulds', 'unitCutting_Right_Top_Small_Press', D, SM,
     {'Lamp1AAddress': 'M231', 'Lamp1BAddress': CLEAR}, 'P2, Y5/Y6; SN65 on X57'),
    # -------------------------------------------------------------- cutting --
    ('ManualCutting', 'unitLeft_Top_Clamping', T3, ENC, {'Lamp1AAddress': 'M245'},
     'P20, Y38/Y39 5/3 closed centre; encoder X6/X7, up sensor SN85 on X75'),
    ('ManualCutting', 'unitLeft_Top_Small_Clamping', D, SM, {'Lamp1AAddress': 'M230'}, 'P1, Y3/Y4; SN64 on X56'),
    ('ManualCutting', 'unitRight_Front_Clamping', T3, ENC, {'Lamp1AAddress': 'M232'},
     'P22, Y43/Y44 5/3 closed centre; encoder X14/X15, back sensor SN76 on X60'),
    ('ManualCutting', 'unitRight_Front_Vert_Clamping', D, SM, {'Lamp1AAddress': 'M246'},
     'P24, Y48/Y49; back sensor SN88 on X76'),
    ('ManualCutting', 'unitProfile_Press_Roller_FW_Back', D, TWO, {'Lamp1AAddress': 'M235', 'Lamp1BAddress': 'M236'},
     'P28, Y59/Y60; SN79 on X63 (back), SN80 on X64 (forward)'),
    ('ManualCutting', 'unitProfile_Press_Roller_Up', S, SM, {'Lamp1AAddress': 'M237'},
     'P29, Y61 5/2 single solenoid; down sensor SN91 on X65'),
    # ------------------------------------------------------- entry conveyor --
    # The roll-down sensors 2SN.94 / 2SN.95 (sheet 17) are read through the
    # 2nd simple motion module; the program's relay for them is M346, which the
    # page showed on the UP movement.
    ('ManualEntry_Conveyor', 'unitRoller_Up_Down', D, SI, {'Lamp1AAddress': 'M346', 'Lamp1BAddress': CLEAR},
     'P32 - P35, Y69/Y70; roll-down sensors 2SN.94 / 2SN.95 (3-wire)'),
    ('ManualEntry_Conveyor', 'unitProfile_Clamping_6', D, TWO, {'Lamp1AAddress': 'M212', 'Lamp1BAddress': 'M219'},
     'P12, Y20/Y21; SN46 on X34 (back), SN53 on X43 (forward)'),
    ('ManualEntry_Conveyor', 'unitProfile_Clamping_7', D, TWO, {'Lamp1AAddress': 'M213', 'Lamp1BAddress': 'M220'},
     'P13, Y22/Y23; SN47 on X35 (back), SN54 on X44 (forward)'),
    # ----------------------------------------------------------------- exit --
    ('ManualExit', 'unitRobot_Top_Clamping', T3, ENC, {'Lamp1AAddress': 'M233'},
     'P23, Y45/Y46 5/3 closed centre; encoder X16/X17, up sensor SN77 on X61'),
    ('ManualExit', 'unitRobot_Top_Small_Clamping', D, SM, {'Lamp1AAddress': 'M231'}, 'P2, Y5/Y6; SN65 on X57'),
    # M707 pushes the short (P25, Y65) or the long (P26 / P27, Y62) release
    # cylinders, both 5/2 single solenoid: back SN81 + SN86 in series on X66,
    # short forward SN82 on X67 (long forward SN98 on X121).
    ('ManualExit', 'unitProfile_Ejection', S, TWO, {'Lamp1AAddress': 'M238', 'Lamp1BAddress': 'M239'},
     'P25 - P27, Y47 / Y50 single solenoid; SN81+SN86 on X66, SN82 on X67'),
    ('ManualExit', 'unitShort_Profile_Support', D, TWO, {'Lamp1AAddress': 'M204', 'Lamp1BAddress': 'M205'},
     'P36, Y71/Y72; SN38 on X24 (back), SN39 on X25 (forward)'),
    # -------------------------------------------------------------- milling --
    # P21's valve is on the pneumatic drawing (Y40/Y41, sheet 3); the electrical
    # one has no output wired to it (U11 Y10 / Y11 are free) and no sensor
    # (X55 is free) - the lamp read an input nothing is wired to.
    ('ManualMilling', 'unitRoller_Up_Down', D, NONE, {'Lamp1AAddress': CLEAR}, 'P21, Y40/Y41; no sensor, outputs not wired'),
    ('ManualMilling', 'unitTop_Clamping', T3, ENC, {'Lamp1AAddress': 'M228'},
     'P18, Y33/Y34 5/3 closed centre; encoder X4/X5, up sensor SN62 on X54'),
    # P19: back sensor SN61 on X53, pressure switches SW2 / SW3 = SN84 on X73
    # (M243, the program's KesimSolOn_BasincSW) and SN112 on X136. M349 is not
    # an input.
    ('ManualMilling', 'unitFront_Clamping', T3, PSW, {'Lamp1AAddress': 'M227', 'Lamp1BAddress': 'M243'},
     'P19, Y35/Y36 5/3 closed centre; SN61 on X53, pressure switch SN84 on X73'),
    # --------------------------------------------------- profile recognition --
    # P16: back sensor SN59 on X51, pressure switch SW1 = SN83 on X70 (M240,
    # ProfilTanimaOn_BasincSW), width encoder through K77. M348 is not an input.
    ('ManualProfile_Recognition', 'unitFront_Clamping', T3, PSW, {'Lamp1AAddress': 'M225', 'Lamp1BAddress': 'M240'},
     'P16, Y28/Y29 5/3 closed centre; SN59 on X51, pressure switch SN83 on X70'),
    ('ManualProfile_Recognition', 'unitTop_Clamping', T3, ENC, {'Lamp1AAddress': 'M226'},
     'P17, Y31/Y32 5/3 closed centre; height encoder through K78, back sensor SN60 on X52'),
    ('ManualProfile_Recognition', 'unitLeft_Top_FW_Back', D, TWO, {'Lamp1AAddress': 'M203', 'Lamp1BAddress': 'M206'},
     'P31, Y67/Y68; SN89 on X23 (back), SN90 on X26 (forward)'),
    # P30: the left unit's height encoder (through K78) and its up sensor SN93,
    # read through the 2nd simple motion module into M345.
    ('ManualProfile_Recognition', 'unitLeft_Top_Clamping', D, ENC, {'Lamp1AAddress': 'M345'},
     'P30, Y65/Y66; height encoder through K78, up sensor SN93'),
    # ---------------------------------------------------------------- robot --
    ('ManualRobot', 'unitGripper', D, ENC, {'Lamp1AAddress': 'M222'},
     'P14, Y24/Y25; encoder X2/X3 (profile start measurement), back sensor SN56 on X46'),
    # The 0 degree button (M752 -> Y127) showed the 90 degree sensor and the
    # other way round: SN95 on X122 is angle 0 (M262), SN96 on X123 angle 90 (M263).
    ('ManualRobot', 'unitGripper_Angle', D, TWO, {'Lamp1AAddress': 'M262', 'Lamp1BAddress': 'M263'},
     'P62, Y117/Y118; SN95 on X122 (0 deg), SN96 on X123 (90 deg)'),
    # ------------------------------------------------------- cross rollers --
    # Three cross rollers are drawn: 1. (Y143 / Y144, back sensor SN.101 on
    # X126), 2. (Y145 / Y146, SN.102 on X127), 3. (Y147 / Y150, SN.103 on X130)
    # - the program's CarprazRulo3 (profile recognition), 4 (milling exit) and
    # 6 (cutting right). Their lamps read the NEXT roller's sensor, the
    # mullion milling's reference and its unit-down sensor.
    ('ManualYZ_Cross_Rollers', 'unitProfile_Recognition', D, SI, {'Lamp1AAddress': 'M266'},
     'P54, Y107/Y108; SN.101 on X126 (3-wire)'),
    ('ManualYZ_Cross_Rollers', 'unitMilling', D, SI, {'Lamp1AAddress': 'M267'}, 'P55, Y109/Y110; SN.102 on X127'),
    ('ManualYZ_Cross_Rollers', 'unitCutting_Right', D, SI, {'Lamp1AAddress': 'M268'}, 'P56, Y111/Y112; SN.103 on X130'),
    # Rollers 1, 2, 5, 7-8 are not on the drawings (their command labels are
    # disabled in the program: _MAN_CarprazRulo1 ...); their lamps read other
    # rollers' sensors (and M358 / M359, which nothing writes). Cleared; the
    # units are left for you to remove or keep.
    ('ManualYZ_Cross_Rollers', 'unitEntry_Conveyor_1', None, None, {'Lamp1AAddress': CLEAR}, 'not drawn'),
    ('ManualYZ_Cross_Rollers', 'unitEntry_Conveyor_2', None, None, {'Lamp1AAddress': CLEAR}, 'not drawn'),
    ('ManualYZ_Cross_Rollers', 'unitCutting_Left', None, None, {'Lamp1AAddress': CLEAR}, 'not drawn'),
    ('ManualYZ_Cross_Rollers', 'unitExit', None, None, {'Lamp1AAddress': CLEAR, 'Lamp1BAddress': CLEAR}, 'not drawn'),
]

ORDER_AFTER = ('TitleTextKey', 'Name')


def apply(src, name, valve, feedback, lamps):
    changes = []

    def line_re(prop):
        return re.compile(r'^([ \t]*)this\.' + re.escape(name) + r'\.' + prop + r' = (.+?);\r?\n', re.M)

    def set_prop(src, prop, value):
        m = line_re(prop).search(src)
        if m:
            if m.group(2) == value:
                return src
            changes.append('%s: %s -> %s' % (prop, m.group(2), value))
            return src[:m.start(2)] + value + src[m.end(2):]
        # Insert after the title (or the name): where the designer writes it.
        for after in ORDER_AFTER:
            a = line_re(after).search(src)
            if a:
                nl = '\r\n' if src[a.start():a.end()].endswith('\r\n') else '\n'
                changes.append('%s: (none) -> %s' % (prop, value))
                ins = '%sthis.%s.%s = %s;%s' % (a.group(1), name, prop, value, nl)
                return src[:a.end()] + ins + src[a.end():]
        raise SystemExit('no place for %s.%s' % (name, prop))

    def clear_prop(src, prop):
        m = line_re(prop).search(src)
        if not m:
            return src
        changes.append('%s: %s -> (none)' % (prop, m.group(2)))
        return src[:m.start()] + src[m.end():]

    if valve:
        src = set_prop(src, 'ValveType', CTL + 'CylinderValve.' + valve)
    if feedback:
        src = set_prop(src, 'FeedbackType', CTL + 'CylinderFeedback.' + feedback)
    for prop, value in lamps.items():
        src = clear_prop(src, prop) if value is None else set_prop(src, prop, '"%s"' % value)
    return src, changes


def main():
    by_page = {}
    for u in UNITS:
        by_page.setdefault(u[0], []).append(u)
    total = 0
    for page, units in by_page.items():
        path = PAGES + page + '.Designer.cs'
        raw = open(path, 'rb').read()
        bom = raw.startswith(b'\xef\xbb\xbf')
        src = raw.decode('utf-8-sig')
        for _, name, valve, feedback, lamps, why in units:
            if ('this.%s = new %sCylinderUnitHMI()' % (name, CTL)) not in src:
                raise SystemExit('%s: no cylinder unit %s' % (page, name))
            src, changes = apply(src, name, valve, feedback, lamps)
            for c in changes:
                print('%s.%s  %s   [%s]' % (page, name, c, why))
            total += len(changes)
        out = src.encode('utf-8')
        if bom:
            out = b'\xef\xbb\xbf' + out
        if out != raw:
            open(path, 'wb').write(out)
    print('%d change(s)' % total)


if __name__ == '__main__':
    main()
