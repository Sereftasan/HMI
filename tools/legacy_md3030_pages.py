#!/usr/bin/env python3
"""
MD3020's pages, from the HMI program the machine runs today - the old program's
_MD3030 project (11 November: "Can you fulfil "Kaban.Machines.MD3020" project
using our current operating program "_MD3030" project as well").

The old pages (tools/legacy/MD3030/, their designers) are tables; PAGES below
writes them out row by row and tools/legacy_grid_pages.py makes them - its
docstring says how (the pieces B V N Lp P S, the check, what is kept).

Run:  python3 tools/legacy_md3030_pages.py            (writes the pages)
      python3 tools/legacy_md3030_pages.py --check    (compares with the old pages)
"""
import os, sys
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import legacy_grid_pages as G
from legacy_grid_pages import B, V, N, Lp, P, S, Spd, Row, Sec, Panel, page, Ax, Cy

G.setup('Kaban.Machines.MD3020', 'MD3030', 'tools/legacy_md3030_pages.py', '11 November')

# column widths
CAP, AX, ARW, POS, LOAD, NUM, BTN = 150, 46, 72, 104, 120, 104, 96

# ------------------------------------------------------------------ HOME ----

# The old Home > Job Preparations (11 November: "can you update this main page as well"): its main control
# panel (ucMainControlPanel - Start writes the frame's sizes and sets M83, Pause M111 and M84, Cancel asks and
# sets M85; Start lit by M316, the speed D408), the frame's sizes (ucJobDimentions - at most 4100 x 2300) and the
# active job's values (ucActiveJobScreen). The profile's settings and its name come with the profile recipes.
page('HomeJob', 'Home', ['ucMainControlPanel', 'ucJobDimentions', 'ucActiveJobScreen'], 'Home',
    [Panel('Cycle', [('Cycle', 110), ('Start', 130), ('Pause', 130), ('Cancel', 130)], [
        Row('Machine', B('M83/M316!Set', 'Start'), B('M111/M111!Alt,M84!Set', 'Pause'), B('M85!Set', 'Cancel', confirm=True)),
        Row('Speed', Spd('D408', span=3))]),
     Panel('Job', [('Frame', 150), ('Value', NUM)], [
        Row('Frame Width (X)', N('D1200 32 /10 1', lo=0, hi=4100)),
        Row('Frame Depth (Y)', N('D1202 32 /10 1', lo=0, hi=2300)),
        Row('Online Time', V('D1854 32 /10 1', 's'))])],
    [Panel('Heads', [('Head', 110), ('Thermocouple Lower', 130), ('Thermocouple Upper', 130)], [
        Row('Head %d' % h, V('D%d 16 /10 2' % (3 + h), '°C'), V('D%d 16 /10 2' % (7 + h), '°C')) for h in (1, 2, 3, 4)]),
     Panel('Axes', [('Axis', 110), ('Actual Position', 130)], [
        Row('X', V('D320 32 /10000 1')),
        Row('Y1', V('D340 32 /10000 1')),
        Row('Y2', V('D360 32 /10000 1'))]),
     Panel('Profile', [('Profile', 130), ('Value', NUM)], [
        Row('Temperature', V('D349 16 /10 0', '°C')),
        Row('Profile Width', V('D346 16 /10 1')),
        Row('Melting', V('D2408 16 /10 1', 's')),
        Row('Welding', V('D2414 16 /10 0', 's'))])])

# ------------------------------------------------------------- THE AXES ----
#
# 12 November: "We gotta update these controls to axis setting controls first and change their manual movements
# to axis unit controls in the manual movement pages" and "enter the axis unit control motion numbers according to
# the image" - the image: the PLC's three FX5-80SSC-S simple motion modules,
#   module 1: Y11 Y12 Y21 Y22 Y31 Y32 Y41 Y42      (every head's Y1 / Y2)
#   module 2: X1 Z1 X2 Z2 GX1 GX2 RZ1 RZ2          (heads 1 - 2's X / Z, the body's X1 / X2, heads 1 - 2's resistance Z)
#   module 3: RZ3 GY1 GY2 RZ4 X3 Z3 X4 Z4          (the body's Y1 / Y2, heads 3 - 4)
# The old page's head rows Z1 / Z2 are the head's Z and its resistance Z (RZ: the drawings' "Rezistans Ekseni",
# its reference W0.4); its X2 is not a servo (the image has none, nor the alarm list) - its buttons and sensor are
# the stripping knife's (the drawings: "Sıyırma Bıçağı" back W1.6 / forward W1.7), so it is a cylinder below; its
# old calibration values stay on Axis Calibration as they were. The error bits are the alarm list's servo alarms.

def _ax(number, caption, key, en, tr, back, fwd, pos, motion, err, ref=None, side=None, cal=None, calibrate=None,
        lo=None, hi=None, zdist=None):
    G.AXES[number] = dict(number=number, caption=caption, key=key, back=back, fwd=fwd, pos=pos, motion=motion, err=err, ref=ref,
                          side=side, cal=cal, calibrate=calibrate, min=lo, max=hi, zdist=zdist)
    G.TRANSLATIONS[key] = (en, tr)

# the body: X1 and X2 one gantry (one pair of jog bits), Y1 and Y2 each its own. X1's position is D400 as on the old
# General and Servos pages (the old Axis Calibration showed D320, X2's, on both X rows).
_ax(1, 'Body X1', 'Axis_MD3020_BodyX1', 'Body X1', 'Gövde X1', 'M41', 'M22', 'D400', (2, 5), 'M621', cal='D2580', calibrate='M60')
_ax(2, 'Body X2', 'Axis_MD3020_BodyX2', 'Body X2', 'Gövde X2', 'M41', 'M22', 'D320', (2, 6), 'M752', cal='D2580', calibrate='M87')
_ax(3, 'Body Y1', 'Axis_MD3020_BodyY1', 'Body Y1', 'Gövde Y1', 'M42', 'M23', 'D340', (3, 2), 'M622', cal='D2582', calibrate='M61')
_ax(4, 'Body Y2', 'Axis_MD3020_BodyY2', 'Body Y2', 'Gövde Y2', 'M43', 'M24', 'D360', (3, 3), 'M623', cal='D2584', calibrate='M62')

# a head's five servos: (axis, back, position, forward, encoder, measured value, calibrate, starting / end limit,
# sensor reference distance) as the old Axis Calibration's rows; X2 is the stripping knife's (kept apart)
HEAD_CAL = {
    1: [('X', 'M45', 'D20', 'M26', 'D2000', 'D2548', 'M64', 'D528', 'D526', 'D6601'),
        ('X2', 'M223', 'D460', 'M222', 'D2826', 'D7096', 'M91', 'D542', 'D540', 'D6623'),
        ('Y1', 'M27', 'D40', 'M46', 'D2004', 'D2552', 'M65', 'D532', 'D530', 'D6602'),
        ('Y2', 'M28', 'D60', 'M47', 'D2006', 'D2554', 'M66', 'D532', 'D530', 'D6603'),
        ('Z1', 'M44', 'D0', 'M25', 'D2002', 'D2550', 'M63', 'D524', 'D522', 'D6600'),
        ('Z2', 'M242', 'D420', 'M243', 'D2038', 'D7044', 'M79', 'D536', 'D534', 'D6619')],
    2: [('X', 'M49', 'D100', 'M30', 'D2008', 'D2556', 'M68', 'D528', 'D526', 'D6605'),
        ('X2', 'M246', 'D470', 'M247', 'D2828', 'D7098', 'M92', 'D542', 'D540', 'D6624'),
        ('Y1', 'M31', 'D120', 'M50', 'D2012', 'D2560', 'M69', 'D532', 'D530', 'D6606'),
        ('Y2', 'M32', 'D140', 'M51', 'D2014', 'D2562', 'M70', 'D532', 'D530', 'D6607'),
        ('Z1', 'M48', 'D80', 'M29', 'D2010', 'D2558', 'M67', 'D524', 'D522', 'D6604'),
        ('Z2', 'M236', 'D440', 'M237', 'D2040', 'D7046', 'M80', 'D536', 'D534', 'D6620')],
    3: [('X', 'M53', 'D180', 'M34', 'D2016', 'D2564', 'M72', 'D528', 'D526', 'D6609'),
        ('X2', 'M248', 'D480', 'M249', 'D2830', 'D7100', 'M93', 'D542', 'D540', 'D6625'),
        ('Y1', 'M35', 'D200', 'M54', 'D2020', 'D2568', 'M73', 'D532', 'D530', 'D6610'),
        ('Y2', 'M36', 'D220', 'M55', 'D2022', 'D2570', 'M74', 'D532', 'D530', 'D6611'),
        ('Z1', 'M52', 'D160', 'M33', 'D2018', 'D2566', 'M71', 'D524', 'D522', 'D6608'),
        ('Z2', 'M238', 'D492', 'M239', 'D2042', 'D7048', 'M81', 'D536', 'D534', 'D6621')],
    4: [('X', 'M57', 'D260', 'M38', 'D2024', 'D2572', 'M76', 'D528', 'D526', 'D6613'),
        ('X2', 'M250', 'D490', 'M251', 'D2832', 'D7102', 'M94', 'D542', 'D540', 'D6626'),
        ('Y1', 'M39', 'D280', 'M58', 'D2028', 'D2576', 'M77', 'D532', 'D530', 'D6614'),
        ('Y2', 'M40', 'D300', 'M59', 'D2030', 'D2578', 'M78', 'D532', 'D530', 'D6615'),
        ('Z1', 'M56', 'D240', 'M37', 'D2026', 'D2574', 'M75', 'D524', 'D522', 'D6612'),
        ('Z2', 'M240', 'D380', 'M241', 'D2044', 'D7050', 'M82', 'D536', 'D534', 'D6622')]}
# the old Manual Movement's torques, its axes in the same order (X, X2, Y1, Y2, Z1, Z2)
HEAD_LOAD = {1: ['D6641', 'D6656', 'D6642', 'D6643', 'D6640', 'D6658'], 2: ['D6645', 'D6657', 'D6646', 'D6647', 'D6644', 'D7194'],
             3: ['D6649', 'D7195', 'D6650', 'D6651', 'D6648', 'D7192'], 4: ['D6653', 'D7190', 'D6654', 'D6655', 'D6652', 'D7191']}
# per head: its X / Z / RZ on the motion modules (the Y1 / Y2 are module 1's 2n-1 / 2n), its first input word on the
# head's Festo valve island (head 1 W0 / W1, head 2 W2 / W3 ...)
HEAD_MOTION = {1: dict(X=(2, 1), Z1=(2, 2), Z2=(2, 7)), 2: dict(X=(2, 3), Z1=(2, 4), Z2=(2, 8)),
               3: dict(X=(3, 5), Z1=(3, 6), Z2=(3, 1)), 4: dict(X=(3, 7), Z1=(3, 8), Z2=(3, 4))}
HEAD_AXES = ['X', 'Y1', 'Y2', 'Z1', 'Z2']          # the head's servos, in the old page's order (no X2)
AXIS_WORDS = {'X': ('X', 'X'), 'Y1': ('Y1', 'Y1'), 'Y2': ('Y2', 'Y2'), 'Z1': ('Z', 'Z'), 'Z2': ('Resistance Z', 'Rezistans Z')}

def head_axis(n, ax):
    return 5 + 5 * (n - 1) + HEAD_AXES.index(ax)

for n in (1, 2, 3, 4):
    w = 2 * (n - 1)
    for ax, back, pos, fwd, enc, meas, cal, lo, hi, dist in HEAD_CAL[n]:
        if ax == 'X2': continue
        en, tr = AXIS_WORDS[ax]
        motion = (1, 2 * n - 1) if ax == 'Y1' else (1, 2 * n) if ax == 'Y2' else HEAD_MOTION[n][ax]
        # the servo alarms: per head Z, X, Y1, Y2 (M605 ... M620), the resistance Z's M757 ... M760
        err = {'Z1': 605, 'X': 606, 'Y1': 607, 'Y2': 608}.get(ax)
        err = 'M%d' % (err + 4 * (n - 1)) if err else 'M%d' % (757 + n - 1)
        # the reference sensors on the head's island inputs (Wn.0 X, .1 Y1, .2 Y2 lit on Back; .3 Z, .4 RZ on Forward)
        bit = {'X': 0, 'Y1': 1, 'Y2': 2, 'Z1': 3, 'Z2': 4}[ax]
        _ax(head_axis(n, ax), 'Head %d %s' % (n, ax), 'Axis_MD3020_H%d%s' % (n, ax.replace('Z1', 'Z').replace('Z2', 'RZ')),
            'Head %d %s' % (n, en), '%d. Kafa %s' % (n, tr), back, fwd, pos, motion, err, ref='W%d.%d' % (w, bit),
            side='Forward' if ax in ('Z1', 'Z2') else 'Back', cal=meas, calibrate=cal, lo=lo, hi=hi, zdist=dist)

# ------------------------------------------------------------ THE CYLINDERS ----
#
# 12 November: "cylinder unit control's valve and feedback types according to the drawings. The pneumatic valves are
# valve islands here" - the pneumatic drawing (MD 3030, 30.06.2025, a sheet per head) and the electrical one (24.07.2024:
# the heads' Festo inputs on sheets 16 - 18, the island's slots on sheet 28):
#   Y1.n / Y2.n  5/3 open centre ("A.M.")   top press left  - back sensor (magnetic) + the profile height encoder
#   Y3.n / Y4.n  5/3 open centre            top press right - the same
#   Y5.n / Y6.n  5/2 double                 front clamps left + right (one valve) - a back sensor each (magnetic)
#   Y7.n / Y8.n  5/2 double                 small top VERTICAL presses left + right - a down sensor each (inductive)
#   Y9.n / Y10.n 5/2 double                 small top HORIZONTAL presses left + right - a back sensor each (magnetic)
#                (the pneumatic sheet names both pairs "Dikey"; sheet 28's island slots: 4 Küçük Üst Dikey, 5 Yatay)
#   Y11.n        5/2 spring                 support plate under the lower resistance - a down sensor (inductive)
#   Y12.n        5/2 spring                 profile emptying - no sensor drawn (the old page's lamp W1.A is not on the drawings)
#   Y13.n        5/2 spring                 stripping knife - back and forward sensors (magnetic)
#   Y14.2/.3/.4  5/2 spring                 the body's Y1 / Y2 / X brakes - no sensors
# A cylinder of each side is a unit of its own - the alarm list has a bit for each (left, right; head by head). The
# machine's commands are common to the heads: a unit's buttons move that cylinder on every head, as the old page's did.
# The encoders measuring the profile's height are the top presses' positions (the high-speed counters SD4500 ...,
# the Offset page's "Actual"); their up sensor is the encoder's reference.

CYL_TR = {}
def _t(key, en, tr):
    G.TRANSLATIONS[key] = (en, tr)
    return key

def head_units(n):
    """Head n's cylinders: (row caption, left unit, right unit)."""
    w0, w1 = 'W%d' % (2 * (n - 1)), 'W%d' % (2 * (n - 1) + 1)
    k = n - 1
    def two(m): return 2 * k + m            # the alarm list: left, right, head after head
    isl2, isl1, isl3 = 'IslandDoubleSolenoid', 'IslandSingleSolenoid', 'IslandThreeWayOpenCenter'
    def key(part, en, tr): return _t('Unit_MD3020_H%d%s' % (n, part), 'Head %d %s' % (n, en), '%d. Kafa %s' % (n, tr))
    rows = []
    rows.append(('Top Press',
        Cy(key('TopPressL', 'Left Top Press', 'Sol Üst Baskı'), a='M201', b='M202', lampB=w0 + '.6', valve=isl3,
           feedback='EncoderPlusReference', errA='M%d' % (660 + two(0)), errB='M%d' % (668 + two(0)),
           pos='SD%d 32 /100 2' % (4500 + 60 * k), arrows='DownUp',
           note='Y1.%d / Y2.%d (5/3 open centre, on the island): down M201, up M202 - up sensor %s.6, the profile height encoder '
                'SD%d; down failed M%d, up failed M%d.' % (n, n, w0, 4500 + 60 * k, 660 + two(0), 668 + two(0))),
        Cy(key('TopPressR', 'Right Top Press', 'Sağ Üst Baskı'), a='M201', b='M202', lampB=w0 + '.7', valve=isl3,
           feedback='EncoderPlusReference', errA='M%d' % (660 + two(1)), errB='M%d' % (668 + two(1)),
           pos='SD%d 32 /100 2' % (4530 + 60 * k), arrows='DownUp',
           note='Y3.%d / Y4.%d (5/3 open centre, on the island): down M201, up M202 - up sensor %s.7, the encoder SD%d.'
                % (n, n, w0, 4530 + 60 * k))))
    rows.append(('Front Clamp',
        Cy(key('FrontClampL', 'Left Front Clamp', 'Sol Ön Sıkma'), a='M230', b='M231', lampA=w1 + '.0', valve=isl2,
           feedback='SingleMagnetic', errA='M%d' % (725 + two(0)), errB='M%d' % (717 + two(0)),
           note='Y5.%d / Y6.%d (5/2 double, on the island, both front clamps): back M230, clamp M231 - back sensor %s.0; '
                'opening failed M%d, clamping failed M%d.' % (n, n, w1, 725 + two(0), 717 + two(0))),
        Cy(key('FrontClampR', 'Right Front Clamp', 'Sağ Ön Sıkma'), a='M230', b='M231', lampA=w1 + '.1', valve=isl2,
           feedback='SingleMagnetic', errA='M%d' % (725 + two(1)), errB='M%d' % (717 + two(1)))))
    rows.append(('Small Vertical',
        Cy(key('SmallVerticalL', 'Left Small Vertical', 'Sol Küçük Dikey'), a='M218', b='M217', lampA=w1 + '.2', valve=isl2,
           feedback='SingleInductive', errA='M%d' % (741 + two(0)), errB='M%d' % (733 + two(0)), arrows='DownUp',
           note='Y7.%d / Y8.%d (5/2 double, on the island, both small vertical presses - the old Profile Ear Clamping): down M218, '
                'up M217 - down sensor %s.2; down failed M%d, up failed M%d.' % (n, n, w1, 741 + two(0), 733 + two(0))),
        Cy(key('SmallVerticalR', 'Right Small Vertical', 'Sağ Küçük Dikey'), a='M218', b='M217', lampA=w1 + '.3', valve=isl2,
           feedback='SingleInductive', errA='M%d' % (741 + two(1)), errB='M%d' % (733 + two(1)), arrows='DownUp')))
    rows.append(('Small Horizontal',
        Cy(key('SmallHorizontalL', 'Left Small Horizontal', 'Sol Küçük Yatay'), a='M228', b='M229', lampA=w1 + '.4', valve=isl2,
           feedback='SingleMagnetic', errA='M%d' % (684 + two(0)), errB='M%d' % (676 + two(0)),
           note='Y9.%d / Y10.%d (5/2 double, on the island, both small horizontal presses - the old Mould Shifting): back M228, '
                'forward M229 - back sensor %s.4; back failed M%d, forward failed M%d.' % (n, n, w1, 684 + two(0), 676 + two(0))),
        Cy(key('SmallHorizontalR', 'Right Small Horizontal', 'Sağ Küçük Yatay'), a='M228', b='M229', lampA=w1 + '.5', valve=isl2,
           feedback='SingleMagnetic', errA='M%d' % (684 + two(1)), errB='M%d' % (676 + two(1)))))
    # one coil each: the old page's two buttons kept (the PLC's commands for each movement) - see the README
    rows.append(('Support Plate',
        Cy(key('SupportPlate', 'Support Plate', 'Dayama Plakası'), a='M233', b='M234', lampA=w0 + '.5', valve=isl1,
           feedback='SingleInductive', errA='M%d' % (636 + k), errB='M%d' % (640 + k), arrows='DownUp',
           note='Y11.%d (5/2 spring, on the island - the old Resistance Lower Down / Up): down M233, up M234 - down sensor %s.5; '
                'down failed M%d, up failed M%d.' % (n, w0, 636 + k, 640 + k)), None))
    kb, kf = HEAD_CAL[n][1][1], HEAD_CAL[n][1][3]          # the head's own commands (the old page's X2 row)
    rows.append(('Stripping Knife',
        Cy(key('StrippingKnife', 'Stripping Knife', 'Sıyırma Bıçağı'), a=kb, b=kf, lampA=w1 + '.6', lampB=w1 + '.7', valve=isl1,
           feedback='TwoEndMagnetic', errA='M%d' % (632 + k), errB='M%d' % (628 + k),
           note='Y13.%d (5/2 spring, on the island - the old page\'s "X2", the head\'s own commands): back %s, forward %s - back %s.6, '
                'forward %s.7; back failed M%d, forward failed M%d.' % (n, kb, kf, w1, w1, 632 + k, 628 + k)), None))
    rows.append(('Profile Emptying',
        Cy(key('ProfileEmptying', 'Profile Emptying', 'Profil Boşaltma'), a='M204', b='M205', valve=isl1, feedback='NoSensor',
           note='Y12.%d (5/2 spring, on the island): back M204, forward M205 - no sensor on the drawings (the old page\'s %s.A '
                'is not there), so no error bit.' % (n, w1)), None))
    return rows

CYL_COLS = [('Unit', 104), ('Left', 220), ('Right', 220)]
G.TRANSLATIONS.update({
    'Unit': ('Unit', 'Ünite'), 'Top Press': ('Top Press', 'Üst Baskı'), 'Front Clamp': ('Front Clamp', 'Ön Sıkma'),
    'Small Vertical': ('Small Vertical', 'Küçük Dikey'), 'Small Horizontal': ('Small Horizontal', 'Küçük Yatay'),
    'Support Plate': ('Support Plate', 'Dayama Plakası'), 'Stripping Knife': ('Stripping Knife', 'Sıyırma Bıçağı'),
    'Axis Unit': ('Axis Unit', 'Eksen Ünitesi'), 'RZ': ('RZ', 'RZ'), 'Axes': ('Axes', 'Eksenler'), 'All': ('All', 'Tümü'),
    "Heads X2 (the stripping knife's buttons on the old page)": ("Heads X2 (the stripping knife's buttons on the old page)",
                                                                 "Kafaların X2'si (eski sayfada sıyırma bıçağının butonları)")})

def head_panel(n):
    return Panel('Head_%d' % n, CYL_COLS, [Row(cap, l, r, h=G.UNIT_ROW_H) for cap, l, r in head_units(n)], title='Head %d' % n)

# ---------------------------------------------------------------- MANUAL ----

def brake(key, en, tr, cmd, valve_tag):
    return Cy(_t(key, en, tr), a=cmd, valve='IslandSingleSolenoid', feedback='NoSensor', text='Brake Cylinder',
              note='%s (5/2 spring, on the island) - no sensor on the drawings.' % valve_tag)

GEN_COLS = [('Name', CAP), ('Axis', 300), ('Overload', LOAD)]
page('ManualGeneral', 'Manual', 'ucGeneral', 'General',
    [Panel('General', GEN_COLS, [
        Sec('Servo Engine X'),
        Row('X1', Ax(1), P('D6634'), h=G.UNIT_ROW_H),
        Row('X2', Ax(2), P('D6636'), h=G.UNIT_ROW_H),
        Row('Brake Cylinder', brake('Unit_MD3020_BrakeX', 'Body X Brake', 'Gövde X Freni', 'M260', 'Y14.4, two cylinders'), h=G.UNIT_ROW_H),
        Sec('Servo Engine Y1'),
        Row('Y1', Ax(3), P('D6630'), h=G.UNIT_ROW_H),
        Row('Brake Cylinder', brake('Unit_MD3020_BrakeY1', 'Body Y1 Brake', 'Gövde Y1 Freni', 'M261', 'Y14.2'), h=G.UNIT_ROW_H),
        Sec('Servo Engine Y2'),
        Row('Y2', Ax(4), P('D6632'), h=G.UNIT_ROW_H),
        Row('Brake Cylinder', brake('Unit_MD3020_BrakeY2', 'Body Y2 Brake', 'Gövde Y2 Freni', 'M262', 'Y14.3'), h=G.UNIT_ROW_H)]),
     Panel('Conveyor', [('Name', CAP), ('Back', ARW), ('Forward', ARW)], [
        Row('Conveyor Up/Down', B('M207', '<'), B('M206', '>')),
        Row('Conveyor Run', B('M208', 'Conveyor Run', span=2))], title='Conveyor')])

# a head's servos on Manual Movement: an axis unit each (its axis settings row gives it its buttons, position,
# reference sensor and error) and its torque
def head_rows(n):
    out = [Sec('Head %d' % n)]
    loads = dict(zip(['X', 'X2', 'Y1', 'Y2', 'Z1', 'Z2'], HEAD_LOAD[n]))
    for ax in HEAD_AXES:
        out.append(Row(ax.replace('Z1', 'Z').replace('Z2', 'RZ'), Ax(head_axis(n, ax)), P(loads[ax]), h=G.UNIT_ROW_H))
    return out

AXIS_COLS = [('Axis', AX), ('Axis Unit', 300), ('Actual Torque', LOAD)]

page('ManualMovement', 'Manual', 'ucManualMovement', 'Manual Movement',
    [Panel('Heads_1_2', AXIS_COLS, head_rows(1) + head_rows(2)),
     Panel('Heads_3_4', AXIS_COLS, head_rows(3) + head_rows(4))],
    [Panel('Body', AXIS_COLS, [Sec('General')] + [Row(c, Ax(i), None, h=G.UNIT_ROW_H) for i, c in ((1, 'X1'), (2, 'X2'), (3, 'Y1'), (4, 'Y2'))]),
     Panel('Units', [('Name', 190), ('Back', ARW), ('Forward', ARW)], [
        Row('Milling Cutters', B('M200', 'Milling Cutters', span=2)),
        Row('Conveyor Up/Down', B('M207', '<'), B('M206', '>')),
        Row('Conveyor Run', B('M208', 'Conveyor Run', span=2)),
        Row('Thermocouple Lower', V('D5 16 /10 1', '°C', span=2)),
        Row('Thermocouple Upper', V('D9 16 /10 1', '°C', span=2))])])

# the old Manual Movement's units and piston sensors, a head's cylinders each (its page of its own: 44 units)
page('ManualCylinders', 'Manual', 'ucManualMovement', 'Cylinders',
    [head_panel(1), head_panel(2)],
    [head_panel(3), head_panel(4)])

# The old page's head X2 (the stripping knife's buttons), its position and torque: Servos shows them; the
# profile emptying's lamps W1.A ... W7.A are not on the drawings (no sensor). New from the drawings: the stripping
# knives' forward sensors (W1.7 ...) and the top presses' encoders (SD4500 ..., the Offset page's). X1's position D400.
G.EXPECTED['ManualMovement'] = G.EXPECTED['ManualCylinders'] = {
    'missing': {'D460', 'D470', 'D480', 'D490', 'D6656', 'D6657', 'D7195', 'D7190', 'W1.A', 'W3.A', 'W5.A', 'W7.A'},
    'extra': {'D400', 'W1.7', 'W3.7', 'W5.7', 'W7.7'} | {'SD%d' % (4500 + 30 * i) for i in range(8)}}

# The old Servo Situations window (its Home and Manual pages' button): every servo's
# position, torque and encoder.
def servo_panel(key, title, rows):
    return Panel(key, [('Axis', AX), ('Actual Position', POS), ('Actual Torque', LOAD), ('Actual Encoder', POS)],
                 [Row(ax, V(pos + ' 32 /10000 2'), P(load), V(enc + ' 32 /10000 2')) for ax, pos, load, enc in rows], title=title)

page('ManualServos', 'Manual', 'ucServos', 'Servos',
    [servo_panel('Head_1', 'Head 1', [('X1', 'D20', 'D6641', 'D48'), ('X2', 'D460', 'D6656', 'D56'), ('Z1', 'D0', 'D6640', 'D50'),
                                     ('Z2', 'D420', 'D6658', 'D64'), ('Y1', 'D40', 'D6642', 'D24'), ('Y2', 'D60', 'D6643', 'D26')]),
     servo_panel('Head_2', 'Head 2', [('X1', 'D100', 'D6645', 'D52'), ('X2', 'D470', 'D6657', 'D58'), ('Z1', 'D80', 'D6644', 'D54'),
                                     ('Z2', 'D440', 'D7188', 'D66'), ('Y1', 'D120', 'D6646', 'D28'), ('Y2', 'D140', 'D6647', 'D30')])],
    [servo_panel('Head_3', 'Head 3', [('X1', 'D180', 'D6649', 'D84'), ('X2', 'D480', 'D7189', 'D74'), ('Z1', 'D160', 'D6648', 'D86'),
                                     ('Z2', 'D492', 'D7192', 'D68'), ('Y1', 'D200', 'D6650', 'D32'), ('Y2', 'D220', 'D6651', 'D34')]),
     servo_panel('Head_4', 'Head 4', [('X1', 'D260', 'D6653', 'D88'), ('X2', 'D490', 'D7190', 'D76'), ('Z1', 'D240', 'D6652', 'D90'),
                                     ('Z2', 'D380', 'D7191', 'D78'), ('Y1', 'D280', 'D6654', 'D44'), ('Y2', 'D300', 'D6655', 'D46')])],
    [servo_panel('Body_X', 'Body', [('X1', 'D400', 'D6634', 'D20'), ('X2', 'D320', 'D6636', 'D20')]),
     servo_panel('Body_Y', 'Body', [('Y1', 'D340', 'D6630', 'D20'), ('Y2', 'D360', 'D6632', 'D20')])])

# ------------------------------------------------------------ PARAMETERS ----

# Axis Calibration: an axis settings row per servo (12 November) - its jog, position, Starting / End Limit (Min /
# Max), Measured Value (its calibration) with Calibrate, the Sensor Reference Distance (its Z distance); the
# motion module and axis, the reference sensor and the error bit added. The old page's Encoder column, the body's
# Encoder Channel and Offset, and the heads' X2 rows (the stripping knife's) as they were, under them.
def enc_rows():
    out = [Sec('General')]
    for cap, enc, chan, off in (('X1', 'D2800', 'D2816', 'D2808'), ('X2', 'D2802', 'D2817', 'D2810'),
                                ('Y1', 'D2812', 'D2818', 'D2812'), ('Y2', 'D2014', 'D2819', 'D2814')):
        out.append(Row(cap, N(enc + ' 32 /10000 2'), N(chan + ' 16 /1 0', ''), N(off + ' 32 /10000 2'), key='Body ' + cap))
    for n in (1, 2, 3, 4):
        out.append(Sec('Head %d' % n))
        for ax, back, pos, fwd, enc, *_ in HEAD_CAL[n]:
            if ax == 'X2': continue
            out.append(Row(ax.replace('Z1', 'Z').replace('Z2', 'RZ'), N(enc + ' 32 /10000 2'), key='H%d %s' % (n, ax)))
    return out

def x2_rows():
    out = []
    for n in (1, 2, 3, 4):
        ax, back, pos, fwd, enc, meas, cal, lo, hi, dist = HEAD_CAL[n][1]
        out.append(Row('Head %d' % n, B(back, '<'), V(pos + ' 32 /10000 2'), B(fwd, '>'), N(enc + ' 32 /10000 2'), N(meas + ' 32 /10000 2'),
                       B(cal, 'Calibrate'), N(lo + ' 32 /10000 2'), N(hi + ' 32 /10000 2'), P(dist)))
    return out

page('ParamAxisCalibration', 'Parameters', 'ucAxisParameters', 'Axis Calibration',
    [G.AxisRows('Axes', [G.AR(G.AXES[i]) for i in (1, 2, 3, 4)] +
                [r for n in (1, 2, 3, 4) for r in [Sec('Head %d' % n)] + [G.AR(G.AXES[head_axis(n, ax)]) for ax in HEAD_AXES]])],
    [Panel('Encoders', [('Axis', AX), ('Encoder', NUM), ('Encoder Channel', NUM), ('Offset', NUM)], enc_rows(), title='Encoder'),
     Panel('Reference', [('Axes', 90), ('Send Reference', BTN + 40)], [Row('All', B('M100', 'Send Reference'))], title='Send Reference')],
    [Panel('Head_X2', [('X2', 70), ('Back', ARW), ('Axis Position', POS), ('Forward', ARW), ('Encoder', NUM), ('Measured Value', NUM),
                       ('Calibrate', BTN), ('Starting Limit', NUM), ('End Limit', NUM), ('Sensor Reference Distance', 164)], x2_rows(),
           title='Heads X2 (the stripping knife\'s buttons on the old page)')])

# the reference sensors (the drawings) are new on this page; X1's position D400 (see the axes)
G.EXPECTED['ParamAxisCalibration'] = {'extra': {a['ref'] for a in G.AXES.values() if a.get('ref')} | {'D400'}}

page('ParamGeneral', 'Parameters', 'ucGeneralParameters', 'General',
    [Panel('Frame', [('Name', 220), ('X', NUM), ('Y', NUM)], [
        Row('Minimum Frame Size', N('D514 32 /10 1'), N('D516 32 /10 1')),
        Row('Maximum Frame Size', N('D518 32 /10 1'), N('D520 32 /10 1')),
        Row('X Unloading Progress Amount', N('D2416 32 /10000 1')),
        Row('Unloading Movement Interval', N('D2594 32 /10 1'))]),
     Panel('Maintenance', [('Machine Maintenance Parameters', 250), ('Value', NUM), ('Send', ARW)], [
        Row('Conveyor Stop', B('M500', 'Conveyor Stop')),
        Row('Conveyor Working Time', N('D507 16 /10 1', 's')),
        Row('Profile Length Measurement Torque', N('D7024 16 /1 0', '%')),
        Row('Melting Torque', N('D2586 16 /1 0', '%')),
        Row('Welding Torque', N('D2587 16 /1 0', '%')),
        Row('U Profile Mold Offset', N('D2592 32 /10000 1')),
        Row('Main Cutting Offset', N('D7042 32 /10000 1')),
        Row('Minimum Jaw Distance', N('D7056 32 /10000 1')),
        Row('Melting Amount', N('D7058 32 /10000 1')),
        Row('Tool Cutting Amount', N('D7060 32 /10000 1')),
        Row('Milling Thickness', N('D502 16 /1 0'), B('M105', 'Send'))])])

page('ParamPositionDefinition', 'Parameters', 'ucPositionDefinition', 'Position Definition',
    [Panel('Positions', [('Position Name', 200), ('Head X', NUM), ('Head Y', NUM), ('Head Z1', NUM)], [
        Row('Starting Position', N('D1940 32 /10 1'), N('D1944 32 /10 1'), N('D1942 32 /10 1'))])])

page('ParamSpeed', 'Parameters', 'ucSpeedParameters', 'Speed Parameters',
    [Panel('Speeds', [('Name', 260), ('Axis Speed', NUM)], [
        Sec('Heads'),
        Row('Manual Movement Speed', N('D2418 32 /100 1', '')),
        Row('Idle Speed', N('D2420 32 /100 1', '')),
        Row('Amount of Milling Cuts per Blade', N('D2760 32 /1000 2', '')),
        Row('Milling Cutter Amount', N('D2762 16 /1 0', '')),
        Row('Milling RPM', N('D2763 32 /1 0', '')),
        Row('Tool Cutting Speed', N('D2422 32 /100 1', '')),
        Row('Melting Approach Speed', N('D2404 32 /100 1', '')),
        Row('Melting Speed', N('D2406 32 /100 1', '')),
        Row('Welding Approach Speed', N('D2410 32 /100 1', '')),
        Row('Welding Speed', N('D2412 32 /100 1', '')),
        Row('Profile Stripping Speed', N('D2650 32 /100 1', '')),
        Sec('Body X'),
        Row('Manual Movement Speed', N('D2424 32 /100 1', '')),
        Row('Idle Speed', N('D2428 32 /100 1', '')),
        Row('Profile Discharge Speed', N('D2430 32 /100 1', '')),
        Sec('Body Y'),
        Row('Manual Movement Speed', N('D2426 32 /100 1', '')),
        Row('Idle Speed', N('D2432 32 /100 1', ''))])])

page('ParamProcess', 'Parameters', 'ucProcessParameters', 'Process Parameters',
    [Panel('Process', [('Name', 260), ('On', 60)], [
        Row('Test Mode', S('L3')),
        Row('Dimension With Weld Tolerance', S('L0')),
        Row('Barrier Control ByPass', S('L5')),
        Row('Loading with X1 Y', S('L6')),
        Row('U Profile', S('L13'))])])

HEAD_YS = [('Head %d|Y%d' % (h, y), NUM) for h in (1, 2, 3, 4) for y in (1, 2)]

def per_head(*addrs):
    return [N(a + ' 32 /10000 2') for a in addrs]

def per_head2(*addrs):
    return [N(a + ' 32 /10000 2', span=2) for a in addrs]

page('ParamOffset', 'Parameters', 'ucOffset', 'Offset',
    [Panel('Positioning', [('Positioning Offset', 210)] + HEAD_YS, [
        Row('Profile Placement Offset', *per_head('D2052', 'D2054', 'D2152', 'D2154', 'D2252', 'D2254', 'D2352', 'D2354')),
        Row('Cutting Position', *per_head('D2056', 'D2058', 'D2156', 'D2158', 'D2256', 'D2258', 'D2356', 'D2358')),
        Row('Gasket Cutting Position', *per_head('D6702', 'D6704', 'D6706', 'D6708', 'D6710', 'D6712', 'D6714', 'D6716')),
        Row('Press Position Before Melting', *per_head('D2770', 'D2772', 'D2774', 'D2776', 'D2778', 'D2780', 'D2782', 'D2784')),
        Row('Melting Position', *per_head('D2068', 'D2070', 'D2168', 'D2170', 'D2268', 'D2270', 'D2368', 'D2370')),
        Row('Welding Position', *per_head('D2076', 'D2078', 'D2176', 'D2178', 'D2276', 'D2278', 'D2376', 'D2378')),
        Row('Spring Mould X Position', *per_head2('D6718', 'D6720', 'D6722', 'D6724')),
        Row('Spring Mould Z Position', *per_head2('D6726', 'D6728', 'D6730', 'D6732')),
        Row('Gasket Cutting X Position', *per_head2('D6734', 'D6736', 'D6738', 'D6740')),
        Row('Gasket Cutting Z Position', *per_head2('D6742', 'D6744', 'D6746', 'D6748')),
        Row('Top Strip Z Position', *per_head2('D2656', 'D2660', 'D2664', 'D2668')),
        Row('Bottom Strip Z Position', *per_head2('D2658', 'D2662', 'D2666', 'D2670'))])],
    [Panel('Temperature', [('Resistance Temperature Offset', 250), ('Value', NUM), ('Actual', NUM)], [
        Row('Resistance Temperature Tolerance', N('D501 16 /10 1', '°C'), B('M135', 'Send')),
        Row('Head 1 Temperature Offset', N('D600 16 /10 1', '°C'), V('D4 16 /10 2', '°C')),
        Row('Head 2 Temperature Offset', N('D601 16 /10 1', '°C'), V('D5 16 /10 2', '°C')),
        Row('Head 3 Temperature Offset', N('D602 16 /10 1', '°C'), V('D6 16 /10 2', '°C')),
        Row('Head 4 Temperature Offset', N('D603 16 /10 1', '°C'), V('D7 16 /10 2', '°C'))]),
     Panel('Encoder', [('Encoder Offset', 190), ('Left', NUM), ('Right', NUM), ('Actual Left', NUM), ('Actual Right', NUM)], [
        Row('Head 1 Encoder Offset', N('D1856 16 /100 2'), N('D1858 16 /100 2'), V('SD4500 32 /100 2'), V('SD4530 32 /100 2')),
        Row('Head 2 Encoder Offset', N('D1860 16 /100 2'), N('D1862 16 /100 2'), V('SD4560 32 /100 2'), V('SD4590 32 /100 2')),
        Row('Head 3 Encoder Offset', N('D1864 16 /100 2'), N('D1866 16 /100 2'), V('SD4620 32 /100 2'), V('SD4650 32 /100 2')),
        Row('Head 4 Encoder Offset', N('D1868 16 /100 2'), N('D1870 16 /100 2'), V('SD4680 32 /100 2'), V('SD4710 32 /100 2'))])],
    [Panel('Press', [('Name', 190), ('Down', ARW), ('Up', ARW)], [
        Row('Upper Press', B('M201/M201', 'v'), B('M202/M202', '^'))])])

# ------------------------------------------------------------ THE ALARMS ----
#
# 12 November - the alarm list the user sent (M600 ... M762), the ones the units and axes above own: every servo's
# alarm (its axis row's error bit - its help the servo manual for that axis), every cylinder's failed movements
# (the unit's error bits - its help the step-by-step page for its valve and feedback), the conveyor's and the oil
# tank's. Left, right, head after head where the list has a bit for each side.
MOVE_WORDS = {   # the unit's part of its key -> (A's failure, B's failure) in EN / TR
    'TopPress': (('did not go down', 'aşağı inemedi'), ('did not go up', 'yukarı çıkamadı')),
    'FrontClamp': (('did not open', 'açılamadı'), ('did not clamp', 'sıkamadı')),
    'SmallVertical': (('did not go down', 'aşağı inemedi'), ('did not go up', 'yukarı çıkamadı')),
    'SmallHorizontal': (('did not go back', 'geri gidemedi'), ('did not go forward', 'ileri gidemedi')),
    'SupportPlate': (('did not go down', 'aşağı inemedi'), ('did not go up', 'yukarı çıkamadı')),
    'StrippingKnife': (('did not go back', 'geri gidemedi'), ('did not go forward', 'ileri gidemedi'))}

def _alarms():
    import re as _re
    out = {}
    for a in G.AXES.values():
        en, tr = G.TRANSLATIONS[a['key']]
        out[a['err']] = (_re.sub(r'\W+', '_', en) + '_Servo_Alarm', en + ' servo alarm', tr + ' servo alarmı')
    for pg in G.PAGES:
        for c in G.cells(pg):
            if c['kind'] != 'cyl': continue
            part = next((k for k in MOVE_WORDS if k in c['title']), None)
            if not part: continue
            en, tr = G.TRANSLATIONS[c['title']]
            for err, (wen, wtr) in ((c.get('errA'), MOVE_WORDS[part][0]), (c.get('errB'), MOVE_WORDS[part][1])):
                if err: out[err] = (_re.sub(r'\W+', '_', en + ' ' + wen).strip('_'), en + ' ' + wen, tr + ' ' + wtr)
    out['M624'] = ('Conveyor_did_not_go_up', 'Conveyor did not go up', 'Konveyör yukarı çıkamadı')
    out['M625'] = ('Conveyor_did_not_go_down', 'Conveyor did not go down', 'Konveyör aşağı inemedi')
    out['M626'] = ('Conveyor_is_not_running', 'Conveyor is not running', 'Konveyör çalışmıyor')
    out['M627'] = ('Oil_tank_empty', 'Oil tank empty', 'Yağ tankı boş')
    G.ALARMS[:] = [(addr,) + out[addr] for addr in sorted(out, key=lambda x: int(x[1:]))]

_alarms()

# the process page's options: toggle switches the page's code reads and writes (ucProcessParameters.cs)
G.CODE_DEVICES.update({'ucProcessParameters': {'L3', 'L0', 'L5', 'L6', 'L13'},
                       # the main control panel's Start, Pause and Cancel (ucMainControlPanel.cs, the Constants: M83 M84 M85, Start lit by M316)
                       'ucMainControlPanel': {'M83', 'M84', 'M85', 'M316'},
                       # the active job's online time (ucActiveJobScreen.cs: D1854 / 10 seconds)
                       'ucActiveJobScreen': {'D1854'}})

if __name__ == '__main__':
    G.main()
