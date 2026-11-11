#!/usr/bin/env python3
"""
MD3020's pages, from the HMI program the machine runs today - the old program's
_MD3030 project (11 November: "Can you fulfil "Kaban.Machines.MD3020" project
using our current operating program "_MD3030" project as well").

The old pages (tools/legacy/MD3030/, their designers) are TABLES: a column a
kind of thing (Back, Axis Position, Forward, Actual Torque...), a row an axis or a
unit, the rows in sections (General, Head 1 ... Head 4). They are made here the
same way - the same rows, columns, sections and devices, each cell one of the
program's own controls:

  B  a button (ButtonHMI): 'M45/W0.0' - its command and its lamp; '<' / '>' an
     arrow (Back / Forward), 'v' / '^' (Down / Up), instead of a text
  V  a value shown (NumericDisplayHMI): 'D20 32 /10000 2' - address, bits,
     divisor, decimals
  N  a value typed in (NumericInputHMI), the same way
  L  a lamp (LampHMI)
  P  a servo's load or torque (MeterHMI, %)
  S  an option bit (CheckBoxHMI)

PAGES below is the specification, written out row by row from the old designers;
--check compares it with them: every device a visible control of an old page
uses must be here, and nothing may be here that the old page does not have,
except what EXPECTED lists. What is set on a control by hand is kept when the
pages are written again (a control is found again by its name).

How the old controls divide (KabanDevBase, read 11 November):
  KabanLedLabel       raw / (Divisor * 10), FloatingPoint decimals
  KabanNumericButton  raw / Divisor, Mask F<n> / n<n> decimals
  KabanProgressBar    |raw / Divisor| on a 0 - 100 bar (DirectShow)

Run:  python3 tools/legacy_md3030_pages.py            (writes the pages)
      python3 tools/legacy_md3030_pages.py --check    (compares with the old pages)
"""
import os, re, sys
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import legacy_fa2010_pages as L
from legacy_fa2010_pages import cs, dnum, ident

MD = os.path.join(L.ROOT, 'Kaban.Machines.MD3020')
L.MANUAL = os.path.join(MD, 'Screens', 'Manual')
L.PARAMS = os.path.join(MD, 'Screens', 'Parameters')
L.OLD = os.path.join(L.ROOT, 'tools', 'legacy', 'MD3030')
L.NS = 'Kaban.Machines.MD3020.Screens.'
L.TOOL = 'tools/legacy_md3030_pages.py'
L.OLD_REL = 'tools/legacy/MD3030/'
L.MADE = '11 November'
L.TYPES['MeterHMI'] = 'Kaban.UI.Controls.MeterHMI'
L.OWNED['MeterHMI'] = {'Name', 'PlcNumber', 'Address', 'Divisor', 'DecimalPlaces', 'Suffix', 'ScaleMinimum', 'ScaleMaximum',
                       'Signed', 'Location', 'Size', 'TabIndex'}
L.OWNED['ButtonHMI'] = L.OWNED['ButtonHMI'] | {'Direction'}
L.OWNED['NumericInputHMI'] = L.OWNED['NumericInputHMI'] | {'MinLimit.Type', 'MinLimit.StaticValue', 'MaxLimit.Type', 'MaxLimit.StaticValue'}
L.OWNED['LabelHMI'] = {'Name', 'TranslationKey', 'Text', 'Font', 'ForeColor', 'TextAlign', 'Location', 'Size', 'BackColor'}

# ============================================================================
#  THE SPECIFICATION'S PIECES
# ============================================================================

def B(spec, text=None, span=1, note=None):
    """A button: 'M45/W0.0' (command / lamp), 'M100!Set'; '<' '>' 'v' '^' as the text: an arrow."""
    s = L.side(spec)
    arrow = {'<': 'Left', '>': 'Right', 'v': 'Down', '^': 'Up'}.get(text)
    return dict(kind='btn', cmd=s['cmd'], lamp=s['lamp'], action=s['action'], text=None if arrow else text,
                arrow=arrow, span=span, note=note)

def V(spec, unit='mm', span=1, note=None):
    return dict(kind='disp', val=L.value(spec), unit=unit, span=span, note=note)

def N(spec, unit='mm', lo=None, hi=None, span=1, note=None):
    return dict(kind='entry', val=L.value(spec), unit=unit, lo=lo, hi=hi, span=span, note=note)

def Lp(addr, span=1, note=None):
    return dict(kind='lamp', addr=addr, span=span, note=note)

def P(addr, span=1, note=None):
    return dict(kind='meter', addr=addr, span=span, note=note)

def S(addr, span=1, note=None):
    return dict(kind='opt', addr=addr, span=span, note=note)

def Row(caption, *cells):
    return dict(kind='row', caption=caption, cells=list(cells))

def Sec(title):
    return dict(kind='sec', title=title)

def Panel(key, cols, rows, title=None):
    """A table: cols [(heading, width)] - the first the rows' captions -, its rows and sections."""
    return dict(key=key, cols=cols, rows=rows, title=title)

PAGES = []

def page(cls, folder, old, caption, *bands, **kw):
    """A page: its class, folder (Manual / Parameters), the old page(s) it comes from, BANDS of panels."""
    p = dict(cls=cls, folder=folder, old=old if isinstance(old, list) else [old], caption=caption, bands=list(bands))
    p.update(kw)
    PAGES.append(p)
    return p

# column widths
CAP, AX, ARW, POS, LOAD, NUM, BTN = 150, 46, 72, 104, 120, 104, 96

# ---------------------------------------------------------------- MANUAL ----

page('ManualGeneral', 'Manual', 'ucGeneral', 'General',
    [Panel('General', [('Name', CAP), ('Axis', AX), ('Back', ARW), ('Overload', LOAD), ('Forward', ARW), ('Actual Position', POS)], [
        Sec('Servo Engine X'),
        Row('X1', None, B('M41', '<'), P('D6634'), B('M22', '>'), V('D400 32 /10000 2')),
        Row('X2', None, B('M41', '<'), P('D6636'), B('M22', '>'), V('D320 32 /10000 2')),
        Row('Brake Cylinder', None, B('M260', 'Brake Cylinder', span=3)),
        Sec('Servo Engine Y1'),
        Row('Y1', None, B('M42', '<'), P('D6630'), B('M23', '>'), V('D340 32 /10000 2')),
        Row('Brake Cylinder', None, B('M261', 'Brake Cylinder', span=3)),
        Sec('Servo Engine Y2'),
        Row('Y2', None, B('M43', '<'), P('D6632'), B('M24', '>'), V('D360 32 /10000 2')),
        Row('Brake Cylinder', None, B('M262', 'Brake Cylinder', span=3)),
        Sec('Conveyor'),
        Row('Conveyor Up/Down', None, B('M207', '<'), None, B('M206', '>')),
        Row('Conveyor Run', None, B('M208', 'Conveyor Run', span=3))])])

# a head's six axes on Manual Movement: (axis, back, its position, forward, its torque)
def head_rows(n, rows):
    out = [Sec('Head %d' % n)]
    for ax, back, pos, fwd, load in rows:
        out.append(Row(ax, B(back, '<'), V(pos + ' 32 /10000 2'), B(fwd, '>'), P(load)))
    return out

AXIS_COLS = [('Axis', AX), ('Back', ARW), ('Axis Position', POS), ('Forward', ARW), ('Actual Torque', LOAD)]

page('ManualMovement', 'Manual', 'ucManualMovement', 'Manual Movement',
    [Panel('Heads_1_2', AXIS_COLS,
        head_rows(1, [('X', 'M45/W0.0', 'D20', 'M26', 'D6641'), ('X2', 'M223/W1.6', 'D460', 'M222', 'D6656'),
                      ('Y1', 'M27/W0.1', 'D40', 'M46', 'D6642'), ('Y2', 'M28/W0.2', 'D60', 'M47', 'D6643'),
                      ('Z1', 'M44', 'D0', 'M25/W0.3', 'D6640'), ('Z2', 'M242', 'D420', 'M243/W0.4', 'D6658')]) +
        head_rows(2, [('X', 'M49/W2.0', 'D100', 'M30', 'D6645'), ('X2', 'M246/W3.6', 'D470', 'M247', 'D6657'),
                      ('Y1', 'M31/W2.1', 'D120', 'M50', 'D6646'), ('Y2', 'M32/W2.2', 'D140', 'M51', 'D6647'),
                      ('Z1', 'M48', 'D80', 'M29/W2.3', 'D6644'), ('Z2', 'M236', 'D440', 'M237/W2.4', 'D7194')]) +
        [Sec('General'),
         Row('X', B('M41', '<'), V('D320 32 /10000 2'), B('M22', '>')),
         Row('Y1', B('M42', '<'), V('D340 32 /10000 2'), B('M23', '>')),
         Row('Y2', B('M43', '<'), V('D360 32 /10000 2'), B('M24', '>'))]),
     Panel('Heads_3_4', AXIS_COLS,
        head_rows(3, [('X', 'M53/W4.0', 'D180', 'M34', 'D6649'), ('X2', 'M248/W5.6', 'D480', 'M249', 'D7195'),
                      ('Y1', 'M35/W4.1', 'D200', 'M54', 'D6650'), ('Y2', 'M36/W4.2', 'D220', 'M55', 'D6651'),
                      ('Z1', 'M52', 'D160', 'M33/W4.3', 'D6648'), ('Z2', 'M238', 'D492', 'M239/W4.4', 'D7192')]) +
        head_rows(4, [('X', 'M57/W6.0', 'D260', 'M38', 'D6653'), ('X2', 'M250/W7.6', 'D490', 'M251', 'D7190'),
                      ('Y1', 'M39/W6.1', 'D280', 'M58', 'D6654'), ('Y2', 'M40/W6.2', 'D300', 'M59', 'D6655'),
                      ('Z1', 'M56', 'D240', 'M37/W6.3', 'D6652'), ('Z2', 'M240', 'D380', 'M241/W6.4', 'D7191')]))],
    [Panel('Units', [('Name', 190), ('Back', ARW), ('Forward', ARW)], [
        Row('Milling Cutters', B('M200', 'Milling Cutters', span=2)),
        Row('Resistance Lower Down/Up', B('M233', '<'), B('M234', '>')),
        Row('Profile Tightening', B('M201', '<'), B('M202', '>')),
        Row('Front Clamping', B('M230', '<'), B('M231', '>')),
        Row('Profile Emptying', B('M204', '<'), B('M205', '>')),
        Row('Profile Ear Clamping', B('M217', '<'), B('M218', '>')),
        Row('Mold Shifting', B('M228', '<'), B('M229', '>')),
        Row('Conveyor Up/Down', B('M207', '<'), B('M206', '>')),
        Row('Conveyor Run', B('M208', 'Conveyor Run', span=2)),
        Row('Thermocouple Lower', V('D5 16 /10 1', '°C', span=2)),
        Row('Thermocouple Upper', V('D9 16 /10 1', '°C', span=2))]),
     Panel('Piston_Sensors', [('Piston Sensors', 176)] + [('Head %d|%s' % (h, side), 46) for h in (1, 2, 3, 4) for side in ('Left', 'Right')], [
        Row('Upper Clamp Is Up', Lp('W0.6'), Lp('W0.7'), Lp('W2.6'), Lp('W2.7'), Lp('W4.6'), Lp('W4.7'), Lp('W6.6'), Lp('W6.7')),
        Row('Down Resistance Is Up', Lp('W0.5', span=2), Lp('W2.5', span=2), Lp('W4.5', span=2), Lp('W6.5', span=2)),
        Row('Front Clamp Is Front', Lp('W1.0'), Lp('W1.1'), Lp('W3.0'), Lp('W3.1'), Lp('W5.0'), Lp('W5.1'), Lp('W7.0'), Lp('W7.1')),
        Row('Ear Clamp Is Down', Lp('W1.2'), Lp('W1.3'), Lp('W3.2'), Lp('W3.3'), Lp('W5.2'), Lp('W5.3'), Lp('W7.2'), Lp('W7.3')),
        Row('Mold Shifting Is Back', Lp('W1.4'), Lp('W1.5'), Lp('W3.4'), Lp('W3.5'), Lp('W5.4'), Lp('W5.5'), Lp('W7.4'), Lp('W7.5')),
        Row('Profile Emptying Is Up', Lp('W1.A', span=2), Lp('W3.A', span=2), Lp('W5.A', span=2), Lp('W7.A', span=2))])])

# The old program's Servo Situations window (its Home and Manual pages' button): every servo's
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

def calib_rows(rows, extra):
    """Axis Calibration's rows: (axis, back, position, forward, encoder, measured value, calibrate, *extra)."""
    out = []
    for r in rows:
        ax, back, pos, fwd, enc, meas, cal = r[:7]
        out.append(Row(ax, B(back, '<'), V(pos + ' 32 /10000 2'), B(fwd, '>'), N(enc + ' 32 /10000 2'), N(meas + ' 32 /10000 2'),
                       B(cal, 'Calibrate'), *extra(r[7:])))
    return out

CAL_COLS = [('Axis', AX), ('Back', ARW), ('Axis Position', POS), ('Forward', ARW), ('Encoder', NUM), ('Measured Value', NUM),
            ('Calibrate', BTN)]

def head_cal(n, rows):
    return [Sec('Head %d' % n)] + calib_rows(rows, lambda e: [N(e[0] + ' 32 /10000 2'), N(e[1] + ' 32 /10000 2'),
                                                             B('M100', 'Send Reference'), P(e[2])])

page('ParamAxisCalibration', 'Parameters', 'ucAxisParameters', 'Axis Calibration',
    [Panel('General', CAL_COLS + [('Encoder Channel', NUM), ('Offset', NUM)],
        [Sec('General')] + calib_rows([
            ('X', 'M41', 'D320', 'M22', 'D2800', 'D2580', 'M60', 'D2816', 'D2808'),
            ('X2', 'M41', 'D320', 'M22', 'D2802', 'D2580', 'M87', 'D2817', 'D2810'),
            ('Y1', 'M42', 'D340', 'M23', 'D2812', 'D2582', 'M61', 'D2818', 'D2812'),
            ('Y2', 'M43', 'D360', 'M24', 'D2014', 'D2584', 'M62', 'D2819', 'D2814')],
            lambda e: [N(e[0] + ' 16 /1 0', ''), N(e[1] + ' 32 /10000 2')]))],
    [Panel('Heads', CAL_COLS + [('Starting Limit', NUM), ('End Limit', NUM), ('Send Reference', BTN + 20),
                                ('Sensor Reference Distance', 164)],
        head_cal(1, [('X', 'M45', 'D20', 'M26', 'D2000', 'D2548', 'M64', 'D528', 'D526', 'D6601'),
                     ('X2', 'M223', 'D460', 'M222', 'D2826', 'D7096', 'M91', 'D542', 'D540', 'D6623'),
                     ('Y1', 'M27', 'D40', 'M46', 'D2004', 'D2552', 'M65', 'D532', 'D530', 'D6602'),
                     ('Y2', 'M28', 'D60', 'M47', 'D2006', 'D2554', 'M66', 'D532', 'D530', 'D6603'),
                     ('Z1', 'M44', 'D0', 'M25', 'D2002', 'D2550', 'M63', 'D524', 'D522', 'D6600'),
                     ('Z2', 'M242', 'D420', 'M243', 'D2038', 'D7044', 'M79', 'D536', 'D534', 'D6619')]) +
        head_cal(2, [('X', 'M49', 'D100', 'M30', 'D2008', 'D2556', 'M68', 'D528', 'D526', 'D6605'),
                     ('X2', 'M246', 'D470', 'M247', 'D2828', 'D7098', 'M92', 'D542', 'D540', 'D6624'),
                     ('Y1', 'M31', 'D120', 'M50', 'D2012', 'D2560', 'M69', 'D532', 'D530', 'D6606'),
                     ('Y2', 'M32', 'D140', 'M51', 'D2014', 'D2562', 'M70', 'D532', 'D530', 'D6607'),
                     ('Z1', 'M48', 'D80', 'M29', 'D2010', 'D2558', 'M67', 'D524', 'D522', 'D6604'),
                     ('Z2', 'M236', 'D440', 'M237', 'D2040', 'D7046', 'M80', 'D536', 'D534', 'D6620')]) +
        head_cal(3, [('X', 'M53', 'D180', 'M34', 'D2016', 'D2564', 'M72', 'D528', 'D526', 'D6609'),
                     ('X2', 'M248', 'D480', 'M249', 'D2830', 'D7100', 'M93', 'D542', 'D540', 'D6625'),
                     ('Y1', 'M35', 'D200', 'M54', 'D2020', 'D2568', 'M73', 'D532', 'D530', 'D6610'),
                     ('Y2', 'M36', 'D220', 'M55', 'D2022', 'D2570', 'M74', 'D532', 'D530', 'D6611'),
                     ('Z1', 'M52', 'D160', 'M33', 'D2018', 'D2566', 'M71', 'D524', 'D522', 'D6608'),
                     ('Z2', 'M238', 'D492', 'M239', 'D2042', 'D7048', 'M81', 'D536', 'D534', 'D6621')]) +
        head_cal(4, [('X', 'M57', 'D260', 'M38', 'D2024', 'D2572', 'M76', 'D528', 'D526', 'D6613'),
                     ('X2', 'M250', 'D490', 'M251', 'D2832', 'D7102', 'M94', 'D542', 'D540', 'D6626'),
                     ('Y1', 'M39', 'D280', 'M58', 'D2028', 'D2576', 'M77', 'D532', 'D530', 'D6614'),
                     ('Y2', 'M40', 'D300', 'M59', 'D2030', 'D2578', 'M78', 'D532', 'D530', 'D6615'),
                     ('Z1', 'M56', 'D240', 'M37', 'D2026', 'D2574', 'M75', 'D524', 'D522', 'D6612'),
                     ('Z2', 'M240', 'D380', 'M241', 'D2044', 'D7050', 'M82', 'D536', 'D534', 'D6622')]))])

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

# What --check accepts as deliberate differences from the old designers (none yet).
EXPECTED = {}

# ============================================================================
#  LAYOUT
# ============================================================================
X0, Y0 = 16, 16
ROW_H, CELL_H, SEC_H, COLHEAD_H, GAP_X, PANEL_GAP, BAND_GAP, TITLE_H = 40, 34, 30, 30, 6, 32, 24, 30

def panel_width(p):
    return sum(w for _, w in p['cols']) + GAP_X * (len(p['cols']) - 1)

def panel_height(p):
    h = (TITLE_H if p.get('title') else 0) + COLHEAD_H + 4 + (COLHEAD_H - 6 if grouped(p) else 0)
    for r in p['rows']:
        h += SEC_H if r['kind'] == 'sec' else ROW_H
    return h

def grouped(p):
    """Columns under a common heading ('Head 1|Left', 'Head 1|Right'): the heading over them, theirs under it."""
    return any('|' in h for h, _ in p['cols'])

def col_x(p, x):
    xs = []
    for _, w in p['cols']:
        xs.append(x); x += w + GAP_X
    return xs

def cell_name(pg, p, sec, row, ci, kind):
    pre = {'btn': 'btn', 'disp': 'disp', 'entry': 'num', 'lamp': 'lamp', 'meter': 'load', 'opt': 'opt'}[kind]
    parts = [p['key']] + ([sec] if sec else []) + [row, p['cols'][ci][0].replace('|', ' ')]
    return pre + '_'.join(ident(x) for x in parts if x)

class Grid:
    def __init__(self, pg, current):
        self.pg = pg
        shown = dict(pg, old=pg['old'][0])           # the Emitter's header names one old page
        self.em = L.Emitter(shown, current)
        self.current = current

    def old(self, name):
        b = self.current.get(name)
        return name if b and b['page'] == self.pg['cls'] else None

    def label(self, text, x, y, w, h, small=False, align='MiddleLeft', bold=True, prefix='lbl'):
        n = self.em.unique(prefix + ident(text)[:40])
        self.em.heading(n, text, x, y, w, h, small=small, align=align)
        if not bold:
            self.em.body.append('            this.%s.Font = new System.Drawing.Font("Segoe UI", 9.5F);' % n)
        return n

    def cell(self, name, c, x, y, w, caption):
        em, k = self.em, c['kind']
        name = em.unique(name)
        old = self.old(name)
        cy = y + (ROW_H - CELL_H) // 2
        if k == 'btn':
            em.decl(name, 'ButtonHMI')
            em.header(name, c.get('note'))
            em.p(name, 'Name', cs(name))
            em.p(name, 'PlcNumber', '1')
            if c['arrow']:
                em.p(name, 'Direction', 'Kaban.UI.Controls.ButtonGlyphs.Direction.' + c['arrow'])
            else:
                em.p(name, 'TranslationKey', cs(c['text']))
                em.p(name, 'FallbackText', cs(c['text']))
            b = em.bit()
            em.body.append('            %s.Address = %s;' % (b, cs(c['cmd'])))
            em.body.append('            %s.Action = Kaban.Core.Enums.BitActionType.%s;' % (b, L.ACTION[c['action']]))
            em.body.append('            this.%s.BitActions.Add(%s);' % (name, b))
            if c['lamp']: em.p(name, 'Lamp1Address', cs(c['lamp']))
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, cy))
            em.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, CELL_H))
            em.p(name, 'LogAction', 'true')
            em.kept(name, 'ButtonHMI', old)
            em.security(name, 'ButtonHMI', old, 'Maintenance' if self.pg['folder'] == 'Parameters' else 'Operator')
            em.add(name)
        elif k == 'disp':
            v = c['val']
            em.decl(name, 'NumericDisplayHMI')
            em.header(name, c.get('note') or '%s: %s, raw / %g.' % (caption, v['addr'], v['div']))
            em.p(name, 'Name', cs(name))
            em.p(name, 'PlcNumber', '1')
            em.p(name, 'Address', cs(v['addr']))
            em.p(name, 'DataType', 'Kaban.Core.Enums.DataTypeEnum.Bin_%d' % v['bits'])
            em.p(name, 'Divisor', dnum(v['div']))
            em.p(name, 'DecimalPlaces', str(v['dec']))
            em.p(name, 'Digits', '7' if v['bits'] == 32 else '5')
            if c['unit']: em.p(name, 'Suffix', cs(c['unit']))
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, cy))
            em.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, CELL_H))
            em.kept(name, 'NumericDisplayHMI', old)
            em.add(name)
        elif k == 'entry':
            v = c['val']
            em.decl(name, 'NumericInputHMI')
            em.header(name + '  (%s)' % v['addr'], c.get('note'))
            em.p(name, 'BackColor', 'System.Drawing.Color.White')
            em.p(name, 'BorderStyle', 'System.Windows.Forms.BorderStyle.Fixed3D')
            em.p(name, 'Name', cs(name))
            em.p(name, 'PlcNumber', '1')
            em.p(name, 'Address', cs(v['addr']))
            em.p(name, 'DataType', 'Kaban.Core.Enums.DataTypeEnum.Bin_%d' % v['bits'])
            em.p(name, 'AutoSize', 'false')
            em.p(name, 'Digits', str(max(5, v['dec'] + 4)))
            em.p(name, 'DecimalPlaces', str(v['dec']))
            em.p(name, 'Divisor', dnum(v['div']))
            em.p(name, 'Suffix', cs(c['unit']))
            em.p(name, 'UnitType', 'Kaban.Core.Enums.UnitType.' + ({'mm': 'Length', 's': 'Time', '°C': 'Temperature'}.get(c['unit'], 'None')))
            em.p(name, 'TextAlign', 'System.Drawing.ContentAlignment.MiddleRight')
            em.p(name, 'LogAction', 'true')
            for lim, val in (('MinLimit', c.get('lo')), ('MaxLimit', c.get('hi'))):
                if val is not None:
                    em.p(name, lim + '.Type', 'Kaban.Core.Enums.LimitSourceType.Static')
                    em.p(name, lim + '.StaticValue', dnum(val))
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, cy + 1))
            em.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, CELL_H - 2))
            em.kept(name, 'NumericInputHMI', old)
            em.security(name, 'NumericInputHMI', old, 'Maintenance' if self.pg['folder'] == 'Parameters' else 'Operator')
            em.add(name)
        elif k == 'lamp':
            em.decl(name, 'LampHMI')
            em.header(name, c.get('note'))
            em.p(name, 'Name', cs(name))
            em.p(name, 'PlcNumber', '1')
            em.p(name, 'Address', cs(c['addr']))
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x + (w - 30) // 2, y + (ROW_H - 30) // 2))
            em.p(name, 'Size', 'new System.Drawing.Size(30, 30)')
            em.kept(name, 'LampHMI', old)
            em.add(name)
        elif k == 'meter':
            em.decl(name, 'MeterHMI')
            em.header(name, c.get('note') or '%s: %s, |raw| on 0 - 100 %% as the old page\'s bar.' % (caption, c['addr']))
            em.p(name, 'Name', cs(name))
            em.p(name, 'PlcNumber', '1')
            em.p(name, 'Address', cs(c['addr']))
            em.p(name, 'Divisor', '1D')
            em.p(name, 'DecimalPlaces', '0')
            em.p(name, 'Suffix', '"%"')
            em.p(name, 'ScaleMinimum', '0D')
            em.p(name, 'ScaleMaximum', '100D')
            em.p(name, 'Signed', 'true')
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y + (ROW_H - 26) // 2))
            em.p(name, 'Size', 'new System.Drawing.Size(%d, 26)' % w)
            em.kept(name, 'MeterHMI', old)
            em.add(name)
        elif k == 'opt':
            em.decl(name, 'CheckBoxHMI')
            em.header(name + '  (%s)' % c['addr'], c.get('note'))
            em.p(name, 'Name', cs(name))
            em.p(name, 'PlcNumber', '1')
            em.p(name, 'Address', cs(c['addr']))
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x + (w - 44) // 2, y + (ROW_H - 32) // 2))
            em.p(name, 'Size', 'new System.Drawing.Size(44, 32)')
            em.p(name, 'LogAction', 'true')
            em.kept(name, 'CheckBoxHMI', old)
            em.security(name, 'CheckBoxHMI', old, 'Maintenance')
            em.add(name)
        return name

    def panel(self, p, x, y):
        pg = self.pg
        if p.get('title'):
            self.label(p['title'], x, y, panel_width(p), TITLE_H - 4)
            y += TITLE_H
        xs = col_x(p, x)
        if grouped(p):
            ci = 0
            while ci < len(p['cols']):
                group = p['cols'][ci][0].split('|')[0] if '|' in p['cols'][ci][0] else None
                cj = ci
                while group and cj + 1 < len(p['cols']) and p['cols'][cj + 1][0].split('|')[0] == group: cj += 1
                if group:
                    self.label(group, xs[ci], y, xs[cj] + p['cols'][cj][1] - xs[ci], COLHEAD_H - 8, small=True, align='MiddleCenter',
                               prefix='hd')
                ci = cj + 1
            y += COLHEAD_H - 6
        for ci, (head, w) in enumerate(p['cols']):
            self.label(head.split('|')[-1], xs[ci], y, w, COLHEAD_H - 4, small=True, align='MiddleLeft' if ci == 0 else 'MiddleCenter',
                       prefix='hd')
        y += COLHEAD_H + 4
        sec = None
        for r in p['rows']:
            if r['kind'] == 'sec':
                sec = r['title']
                self.label(sec, x, y + 2, panel_width(p), SEC_H - 4)
                y += SEC_H
                continue
            self.label(r['caption'], xs[0], y, p['cols'][0][1], ROW_H, bold=False, prefix='cap')
            ci = 1
            for c in r['cells']:
                if c is None:
                    ci += 1
                    continue
                span = c.get('span', 1)
                w = sum(p['cols'][j][1] for j in range(ci, ci + span)) + GAP_X * (span - 1)
                caption = ' '.join(t for t in (p.get('title'), sec, r['caption'], p['cols'][ci][0].replace('|', ' ')) if t)
                self.cell(cell_name(pg, p, sec, r['caption'], ci, c['kind']), c, xs[ci], y, w, caption)
                ci += span
            assert ci <= len(p['cols']), (pg['cls'], p['key'], r['caption'])
            y += ROW_H
        return y

    def write(self):
        y = Y0
        right = 0
        for band in self.pg['bands']:
            x = X0
            bottom = y
            for p in band:
                bottom = max(bottom, self.panel(p, x, y))
                x += panel_width(p) + PANEL_GAP
                right = max(right, x - PANEL_GAP)
            y = bottom + BAND_GAP
        folder = L.MANUAL if self.pg['folder'] == 'Manual' else L.PARAMS
        self.em.write(os.path.join(folder, self.pg['cls'] + '.Designer.cs'), (right + X0, y))
        return self.em

# ============================================================================
#  CHECK: the old pages' devices
# ============================================================================
SHOWN_TYPES = ('KabanButton', 'KabanLambButton', 'KabanLamb', 'KabanLedLabel', 'KabanNumericButton', 'KabanSwitchButton', 'KabanProgressBar')

def old_controls(old):
    """The visible controls of an old page: name -> dict(t, Device, LambDevice, Divisor, ...)."""
    src = open(os.path.join(L.OLD, old + '.Designer.cs'), encoding='utf-8-sig').read().replace('\r', '')
    ctl = {}
    for m in re.finditer(r'this\.(\w+) = new ([\w.]+)\((?:this\.components)?\);', src): ctl[m.group(1)] = dict(t=m.group(2).split('.')[-1])
    parent = {m.group(2): m.group(1) for m in re.finditer(r'this\.(\w+)\.Controls\.Add\(this\.(\w+)\);', src)}
    for m in re.finditer(r'^\s*this\.(\w+)\.([\w.]+) = (.+?);\s*$', src, re.M):
        if m.group(1) in ctl: ctl[m.group(1)].setdefault(m.group(2), m.group(3).strip().strip('"'))
    def shown(n, d=0):
        c = ctl.get(n)
        if c is None: return True
        if c.get('Visible') == 'false': return False
        return shown(parent.get(n), d + 1) if parent.get(n) and d < 10 else True
    return {n: c for n, c in ctl.items() if c['t'] in SHOWN_TYPES and shown(n)}

# the process page's options: toggle switches the page's code reads and writes (ucProcessParameters.cs)
CODE_DEVICES = {'ucProcessParameters': {'L3', 'L0', 'L5', 'L6', 'L13'}}

def old_devices(old):
    out = set(CODE_DEVICES.get(old, ()))
    for c in old_controls(old).values():
        for k in ('Device', 'LambDevice'):
            if c.get(k): out.add(c[k])
    return out

def cells(pg):
    for band in pg['bands']:
        for p in band:
            for r in p['rows']:
                if r['kind'] == 'row':
                    for c in r['cells']:
                        if c: yield c

def spec_devices(pg):
    out = set()
    for c in cells(pg):
        k = c['kind']
        if k == 'btn':
            out.add(c['cmd'])
            if c['lamp']: out.add(c['lamp'])
        elif k in ('disp', 'entry'): out.add(c['val']['addr'])
        else: out.add(c['addr'])
    return out

def divisors(pg):
    """The old pages' values against the spec: address -> (divisor, decimals) as each shows it."""
    problems = []
    old = {}
    for o in pg['old']:
        for c in old_controls(o).values():
            dev = c.get('Device')
            if not dev: continue
            if c['t'] == 'KabanLedLabel':
                div = float(c.get('Divisor', '1').rstrip('D')) * 10
                dec = int(c.get('FloatingPoint', '0'))
                bits = 32 if 'Bit_32' in c.get('DataType', '') else 16
                old.setdefault(dev, set()).add(('V', div, dec, bits))
            elif c['t'] == 'KabanNumericButton':
                div = float(c.get('Divisor', '1').rstrip('D'))
                mm = re.search(r'(\d+)', c.get('Mask', '') or '')
                dec = int(mm.group(1)) if mm else 0
                bits = 32 if 'Bit_32' in c.get('DataType', '') else 16
                old.setdefault(dev, set()).add(('N', div, dec, bits))
    for c in cells(pg):
        if c['kind'] not in ('disp', 'entry'): continue
        v = c['val']
        seen = old.get(v['addr'])
        if not seen: continue
        kind = 'V' if c['kind'] == 'disp' else 'N'
        if not any(k == kind and d == v['div'] for k, d, _, _ in seen):
            problems.append('%s: divisor %g, the old page %s' % (v['addr'], v['div'], sorted(seen)))
    return problems

def check():
    bad = 0
    for pg in PAGES:
        o = set().union(*[old_devices(x) for x in pg['old']])
        s = spec_devices(pg)
        exp = EXPECTED.get(pg['cls'], {})
        missing = o - s - exp.get('missing', set())
        extra = s - o - exp.get('extra', set())
        print('%-26s old %3d  spec %3d  missing %s  extra %s' % (pg['cls'], len(o), len(s), sorted(missing) or '-', sorted(extra) or '-'))
        bad += len(missing) + len(extra)
        for pr in divisors(pg):
            print('    ' + pr); bad += 1
    return bad

# ============================================================================
#  MAIN
# ============================================================================
def main():
    if '--check' in sys.argv:
        sys.exit(1 if check() else 0)
    for d in (L.MANUAL, L.PARAMS):
        os.makedirs(d, exist_ok=True)
    for pg in PAGES:
        folder = L.MANUAL if pg['folder'] == 'Manual' else L.PARAMS
        path = os.path.join(folder, pg['cls'] + '.Designer.cs')
        current = L.parse_current(path)
        for n, b in current.items(): b['own'] = n
        em = Grid(pg, current).write()
        print('%-26s %3d controls' % (pg['cls'], len(em.decls)))
        for d in em.dropped: print('    not carried over: ' + d)

if __name__ == '__main__':
    main()
