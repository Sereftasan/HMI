#!/usr/bin/env python3
"""
MK2010's pages, from the HMI program the machine runs today - the old program's
_MK2010 project (11 November: "can you add "MK2010" project from "_MK2010" as
well").

The old pages (tools/legacy/MK2010/, their designers) are tables; PAGES below
writes them out row by row and tools/legacy_grid_pages.py makes them - its
docstring says how (the pieces B V N Lp P S, the check, what is kept).

  Home                      ucJobNew (Home > Job Preparations): the heads'
                            temperatures, the axes, the profile's times, the job's
                            width X / Y and quantity (the profile list and the
                            barcode come with the profile recipes - not yet)
  Manual - Manual Control   ucManuelControl (Manual Control)
  Manual - References       ucReferance
  Parameters                ucProductionParameters, ucCalibration, ucSystemSettings,
                            ucSystemSettings2, ucDemo, ucMachineRuntime (Reports)

Run:  python3 tools/legacy_mk2010_pages.py            (writes the pages)
      python3 tools/legacy_mk2010_pages.py --check    (compares with the old pages)
"""
import os, sys
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import legacy_grid_pages as G
from legacy_grid_pages import B, V, N, Lp, P, S, Row, Sec, Panel, page, Ax, Cy

G.setup('Kaban.Machines.MK2010', 'MK2010', 'tools/legacy_mk2010_pages.py', '11 November')

# column widths
CAP, ARW, POS, NUM, BTN, LMP = 170, 96, 104, 104, 104, 30
AXU = ARW * 2 + POS + 2 * G.GAP_X                 # an axis unit as wide as Manual Control's (its Back, Forward, Positions)

# ------------------------------------------------------------------ HOME ----

page('HomeJob', 'Home', 'ucJobNew', 'Home',
    [Panel('Heads', [('Name', 110), ('Temperature', NUM), ('Actual', NUM)], [
        Row('Head %d' % h, V('D%d 16 /10 1' % (214 + h), '°C'), V('D%d 16 /10 1' % (380 + h), '°C')) for h in (1, 2, 3, 4)]),
     Panel('Axes', [('Axis', 110), ('Target', NUM), ('Actual', NUM)], [
        Row('X', V('D32 32 /10 1'), V('D50 32 /10 1')),
        Row('Y1', V('D34 32 /10 1'), V('D52 32 /10 1')),
        Row('Y2', V('D36 32 /10 1'), V('D54 32 /10 1'))])],
    [Panel('Profile', [('Profile', 150), ('Set', NUM), ('Actual', NUM)], [
        Row('Profile Width', V('D346 16 /10 1')),
        Row('Melting', V('D349 16 /10 1', 's'), V('TN58 16 /10 1', 's')),
        Row('Welding', V('D350 16 /10 1', 's'), V('TN6 16 /10 1', 's'))]),
     Panel('Job', [('Job', 110), ('Value', NUM), ('Done', NUM)], [
        # its limits as the old page: D144 / D146 and D148 / D150 (the raw limits, read as the value)
        Row('Width X', N('D1200 32 /10 1', lo='D144', hi='D146')),
        Row('Width Y', N('D1202 32 /10 1', lo='D148', hi='D150')),
        Row('Quantity', N('D334 16 /1 0', ''), V('D335 16 /10 1', ''))])],
    level='Operator')

# -------------------------------------------------------------- THE AXES ----
#
# 12 November (the user, MK2010's Calibration page beside it: "We gotta update these controls to axis setting controls
# first and change their manual movements to axis unit controls in the manual movement pages"): X, Y1, Y2 as axis
# settings rows on Calibration - their jog (Manual Control's), position (raw / 10, as the old page), the real value and
# Calibrate - and axis units on Manual Control. Their motion modules, reference sensors and servo alarms are not on
# the old pages: they come with the machine's drawings and alarm list.
# 17 November ("complete ... MK2010"): each one's send to reference (the old References page's, M30 ... M32 - "X EKSENI
# SIFIRLA" in the program's comments). Their reference switches X6 / X7 / X10 stay the lamps beside them: normally closed
# (the old page's negative lamps), which an axis row's reference sensor would show the other way round.
for number, cap, en, tr, back, fwd, pos, cal, calibrate, goref in (
        (1, 'Axis X', 'Axis X', 'X Ekseni', 'M41', 'M40', 'D50', 'D154', 'M91', 'M30'),
        (2, 'Axis Y1', 'Axis Y1', 'Y1 Ekseni', 'M43', 'M42', 'D52', 'D156', 'M92', 'M31'),
        (3, 'Axis Y2', 'Axis Y2', 'Y2 Ekseni', 'M45', 'M44', 'D54', 'D158', 'M93', 'M32')):
    key = 'Axis_MK2010_' + cap.split()[-1]
    G.AXES[number] = dict(number=number, caption=cap, key=key, back=back, fwd=fwd, pos=pos, cal=cal, calibrate=calibrate, goref=goref,
                          div=10, dec=1)
    G.TRANSLATIONS[key] = (en, tr)

# ---------------------------------------------------------------- MANUAL ----

# ------------------------------------------------------------ THE CYLINDERS ----
#
# 12 November - MK2010's pneumatic drawing (MK 2010, 29.06.2015: a sheet per head, the gasket presses on sheets 5 - 6),
# its electrical drawing (V3, 13.03.2018) and its PLC program (MK2010 V6.4, 30.07.2026 - the device comments read
# from the project: the sensors' and outputs' names, the alarm list M600 ... M635). Every valve is a valve of its own
# (1/8 5/2 24 VDC, no valve island). A head's cylinders, a unit each - the old page's On / Off and its four heads'
# lamps; the machine's commands are common to the heads, as the old page's, and so are its alarms (one bit for the
# four heads' strips): every head's unit carries the bit, the alarm's help offers each one's page.
#   supporting strip (Dayama Çıtası) P1  Y1.n single 5/2 (spring)       back X11 ... X14, forward (down) X31 ... X34
#   side table (Yan Tabla) P52 / P2      Y2.n / Y3.n forward (3 / 6 bar), Y4.n back - single valves, both off: both
#                                        sides open (as a 5/3 open centre); each cylinder's back sensor X15 ... / X21 ...
#   heater plate (Isıtıcı Plaka) P3      Y5.n / Y6.n double 5/2 (the table stops P8 / P9 on it too); back X25 ...,
#                                        forward X35 ...
#   top presses (Üst Baskı) P4 / P5      Y7.n down, Y8.n up - single valves (as a 5/3 open centre); no sensor in the PLC
#   profile lifting P6, emptying P7      Y10.n / Y11.n, Y12.n / Y13.n double 5/2; no sensor in the PLC
#   sliding sash press (Sürme Kanat)     not on the 2015 pneumatic sheets - back X54 ... X57
#   gasket presses                       heads 1 - 4 and 2 - 3 in pairs (the PLC's outputs Y40 ... Y45): forward / back
#                                        back X60 / X61, the group pistons up X64 / X65, the press pistons up X62 / X63;
#                                        the sheets' single valves per head do not match the PLC's pairs - not set
# A toggle is two buttons - its Reset back, its Set forward (the coil on; a single coil's spring off); a unit not on the
# drawings keeps the old On / Off beside its sensor.
HEADX = {  # per head 1 - 4 (octal, as the old page writes them)
    'strip_back': ['X0011', 'X0012', 'X0013', 'X0014'], 'strip_fwd': ['X0031', 'X0032', 'X0033', 'X0034'],
    'table1_back': ['X0015', 'X0016', 'X0017', 'X0020'], 'table2_back': ['X0021', 'X0022', 'X0023', 'X0024'],
    'heater_back': ['X0025', 'X0026', 'X0027', 'X0030'], 'heater_fwd': ['X0035', 'X0036', 'X0037', 'X0040'],
    'sash_back': ['X0054', 'X0055', 'X0056', 'X0057']}

def _t(key, en, tr):
    G.TRANSLATIONS[key] = (en, tr)
    return key

def head_units(n):
    i = n - 1
    def key(part, en, tr): return _t('Unit_MK2010_H%d%s' % (n, part), 'Head %d %s' % (n, en), '%d. Kafa %s' % (n, tr))
    return [
        ('Supporting Strip', Cy(key('SupportingStrip', 'Supporting Strip', 'Dayama Çıtası'), a='M101!Rst', b='M101!Set', lampA=HEADX['strip_back'][i],
            lampB=HEADX['strip_fwd'][i], valve='SingleSolenoid', feedback='TwoEndMagnetic', errA='M602', errB='M600', arrows='UpDown',
            note='Y1.%d (5/2 spring): its coil takes it down - M101 set (the coil on), reset (off: the spring back) - back %s, forward %s; did not come back M602, '
                 'did not go down M600 (the four heads\' one bit each).' % (n, HEADX['strip_back'][i], HEADX['strip_fwd'][i]))),
        ('Side Table 1', Cy(key('SideTable1', 'Side Table 1', 'Yan Tabla 1'), a='M102!Rst', b='M102!Set', lampA=HEADX['table1_back'][i],
            valve='ThreeWayOpenCenter', feedback='SingleMagnetic', errA='M601',
            note='P52: Y2.%d / Y3.%d forward (3 / 6 bar), Y4.%d back - single valves, both off both sides open (as a 5/3 open centre); '
                 'M102 set forward, reset back; back sensor %s; did not come back M601.' % (n, n, n, HEADX['table1_back'][i]))),
        ('Side Table 2', Cy(key('SideTable2', 'Side Table 2', 'Yan Tabla 2'), a='M102!Rst', b='M102!Set', lampA=HEADX['table2_back'][i],
            valve='ThreeWayOpenCenter', feedback='SingleMagnetic', errA='M601')),
        ('Heater Plate', Cy(key('HeaterPlate', 'Heater Plate', 'Isıtıcı Plaka'), a='M104!Rst', b='M104!Set', lampA=HEADX['heater_back'][i],
            lampB=HEADX['heater_fwd'][i], valve='DoubleSolenoid', feedback='TwoEndMagnetic', errA='M604', errB='M603',
            note='P3 (and the table stops P8 / P9): Y5.%d / Y6.%d double 5/2; back %s, forward %s; back failed M604, forward M603.'
                 % (n, n, HEADX['heater_back'][i], HEADX['heater_fwd'][i]))),
        ('Top Press', Cy(key('TopPress', 'Top Press', 'Üst Baskı'), a='M103!Rst', b='M103!Set', valve='ThreeWayOpenCenter',
            feedback='NoSensor', arrows='UpDown',
            note='P4 / P5: Y7.%d down, Y8.%d up - single valves (as a 5/3 open centre); no sensor among the PLC\'s inputs.' % (n, n))),
        ('Profile Lifting', Cy(key('ProfileLifting', 'Profile Lifting', 'Profil Kaldırma'), a='M105!Rst', b='M105!Set', valve='DoubleSolenoid',
            feedback='NoSensor', arrows='DownUp',
            note='P6: Y10.%d / Y11.%d double 5/2 - the sheet draws two sensors, the PLC has none.' % (n, n))),
        ('Profile Emptying', Cy(key('ProfileEmptying', 'Profile Emptying', 'Profil Boşaltma'), a='M100!Rst', b='M100!Set',
            valve='DoubleSolenoid', feedback='NoSensor',
            note='P7: Y12.%d / Y13.%d double 5/2 - the old Profile Release (M100); no sensor in the PLC.' % (n, n))),
        ('Sliding Sash Press', Cy(key('SlidingSashPress', 'Sliding Sash Press', 'Sürme Kanat Baskı'), a='M196!Alt', lampA=HEADX['sash_back'][i],
            errA='M635', errB='M634',
            note='Not on the 2015 pneumatic sheets (its valve and sensor type not set): M196 On / Off, back %s; did not press M634, '
                 'did not go back M635.' % HEADX['sash_back'][i]))]

def gasket_units(pair, idx):
    k = pair.replace('-', '')
    def key(part, en, tr): return _t('Unit_MK2010_G%s%s' % (k, part), 'Heads %s %s' % (pair, en), '%s. Kafa %s' % (pair, tr))
    return [
        ('Gasket Forward/Back', Cy(key('ForwardBack', 'Gasket Forward/Back', 'Conta İleri-Geri'), a='M109!Alt', lampA=['X0060', 'X0061'][idx],
            errA=['M623', 'M624'][idx], note='The forward / back pistons of heads %s: back %s; not back %s.' % (pair, ['X0060', 'X0061'][idx], ['M623', 'M624'][idx]))),
        ('Gasket Group', Cy(key('Group', 'Gasket Group', 'Conta Grubu'), a='M108!Alt', lampA=['X0064', 'X0065'][idx], errA=['M621', 'M622'][idx],
            note='The group pistons of heads %s: up %s; not up %s.' % (pair, ['X0064', 'X0065'][idx], ['M621', 'M622'][idx]))),
        ('Gasket Press', Cy(key('Press', 'Gasket Press', 'Conta Baskı'), a='M108!Alt', lampA=['X0062', 'X0063'][idx], errA=['M625', 'M626'][idx],
            note='The press pistons of heads %s: up %s; not up %s.' % (pair, ['X0062', 'X0063'][idx], ['M625', 'M626'][idx])))]

UNIT_W = 216
def units_rows():
    heads = [head_units(n) for n in (1, 2, 3, 4)]
    rows = [Row(heads[0][r][0], *[heads[n][r][1] for n in range(4)], h=G.UNIT_ROW_H) for r in range(len(heads[0]))]
    g14, g23 = gasket_units('1-4', 0), gasket_units('2-3', 1)
    rows += [Row(g14[r][0], g14[r][1], g23[r][1], h=G.UNIT_ROW_H) for r in range(len(g14))]
    return rows

G.TRANSLATIONS.update({
    'Supporting Strip': ('Supporting Strip', 'Dayama Çıtası'), 'Side Table 1': ('Side Table 1', 'Yan Tabla 1'),
    'Side Table 2': ('Side Table 2', 'Yan Tabla 2'), 'Heater Plate': ('Heater Plate', 'Isıtıcı Plaka'),
    'Profile Emptying': ('Profile Emptying', 'Profil Boşaltma'), 'Gasket Forward/Back': ('Gasket Forward/Back', 'Conta İleri-Geri'),
    'Gasket Group': ('Gasket Group', 'Conta Grubu'), 'Gasket Press': ('Gasket Press', 'Conta Baskı'),
    'Cylinders': ('Cylinders', 'Silindirler'), 'Unit': ('Unit', 'Ünite'), 'Heads 1-4 / 2-3': ('Heads 1-4 / 2-3', 'Kafa 1-4 / 2-3')})

page('ManualControl', 'Manual', 'ucManuelControl', 'Manual Control',
    [Panel('Axes', [('Name', CAP), ('Back', ARW), ('Forward', ARW), ('Positions', POS), ('Limit', 70)], [
        # an axis unit each (12 November): its jog and position from its row on Calibration; the limit switch beside it
        Row('Axis X', Ax(1, span=3), Lp('X0006', inv=True), h=G.UNIT_ROW_H),
        Row('Axis Y1', Ax(2, span=3), Lp('X0007', inv=True), h=G.UNIT_ROW_H),
        Row('Axis Y2', Ax(3, span=3), Lp('X0010', inv=True), h=G.UNIT_ROW_H),
        # (17 November) the old table's sensor rows repeated the cylinders' (Head 1-2 Conveyor X0031 / X0011: head 1's
        # supporting strip; Head 3-4 Conveyor X0035 / X0025: its heater plate; Unit Up / Head Up / Unit Back: the gasket
        # presses' X0064 / X0065, X0062 / X0063, X0060 / X0061) - they are on the units below; Gasket Pressing once
        Row('Gasket Pressing', Lp('M316')),
        Row('Lubrication', None, B('M1005/M1005', 'Lubrication'))]),
     Panel('Others', [('Manual Control', 190), ('On/Off', 70)], [
        Row('Automatic On/Off', B('M34/M34!Alt', 'On/Off')),
        Row('Conveyor', B('M106/M106!Alt', 'On/Off'))])],
    [Panel('Units', [('Unit', 150)] + [('Head %d' % h, UNIT_W) for h in (1, 2, 3, 4)], units_rows(), title='Cylinders')])

# the old page's lamps of the cylinders are the units' now; their On / Off the units' buttons
G.EXPECTED['ManualControl'] = {'missing': set(), 'extra': set()}

# ------------------------------------------------------------ THE ALARMS ----
# The PLC program's alarm bits (M600 ... M635, their comments in the program, Turkish as written there).
G.ALARMS[:] = [(a, tag, en, tr) for a, tag, en, tr in [
    ('M600', 'Supporting_strips_did_not_go_down', 'Supporting strips did not go down', 'Dayama çıtaları inmedi'),
    ('M601', 'Side_tables_did_not_come_back', 'Side tables did not come back', 'Yan tablalar geri gelmedi'),
    ('M602', 'Supporting_strips_did_not_come_back', 'Supporting strips did not come back', 'Dayama çıtaları geri gelmedi'),
    ('M603', 'Heaters_did_not_go_forward', 'Heaters did not go forward', 'Isıtıcılar ileri gelmedi'),
    ('M604', 'Heaters_did_not_come_back', 'Heaters did not come back', 'Isıtıcılar geri gelmedi'),
    ('M605', 'CF_access_fault', 'CF card access fault', 'CF erişim arızası'),
    ('M607', 'X_minus_limit', 'X - limit', 'X - limit'),
    ('M608', 'Y1_minus_limit', 'Y1 - limit', 'Y1 - limit'),
    ('M609', 'Y2_minus_limit', 'Y2 - limit', 'Y2 - limit'),
    ('M610', 'X_plus_limit', 'X + limit', 'X + limit'),
    ('M611', 'Y1_plus_limit', 'Y1 + limit', 'Y1 + limit'),
    ('M612', 'Y2_plus_limit', 'Y2 + limit', 'Y2 + limit'),
    ('M613', 'No_empty_recipe', 'No empty recipe', 'Boş reçete yok'),
    ('M614', 'X_inverter_fault', 'X inverter fault', 'X inverter arızası'),
    ('M615', 'Y1_inverter_fault', 'Y1 inverter fault', 'Y1 inverter arızası'),
    ('M616', 'Y2_inverter_fault', 'Y2 inverter fault', 'Y2 inverter arızası'),
    ('M617', 'Conveyors_fault', 'Conveyors fault', 'Konveyörler arızası'),
    ('M618', 'Air_pressure_fault', 'Air pressure fault', 'Basınç arızası'),
    ('M619', 'Lubrication_switch_fault', 'Lubrication switch fault', 'Yağlama switch arızası'),
    ('M620', 'Oil_level_low', 'Oil level low', 'Yağ seviyesi düşük'),
    ('M621', 'Heads_1_4_gasket_group_not_up', 'Heads 1-4 gasket group piston not up', '1-4. conta grup pistonu yukarıda değil'),
    ('M622', 'Heads_2_3_gasket_group_not_up', 'Heads 2-3 gasket group piston not up', '2-3. conta grup pistonu yukarıda değil'),
    ('M623', 'Heads_1_4_gasket_not_back', 'Heads 1-4 gasket forward-back piston not back', '1-4. conta baskı ileri-geri pistonu geride değil'),
    ('M624', 'Heads_2_3_gasket_not_back', 'Heads 2-3 gasket forward-back piston not back', '2-3. conta baskı ileri-geri pistonu geride değil'),
    ('M625', 'Heads_1_4_gasket_press_not_up', 'Heads 1-4 gasket press piston not up', '1-4. conta baskı pistonu yukarıda değil'),
    ('M626', 'Heads_2_3_gasket_press_not_up', 'Heads 2-3 gasket press piston not up', '2-3. conta baskı pistonu yukarıda değil'),
    ('M627', 'Cooling_inverter_fault', 'Cooling inverter fault', 'Soğutma inverter arızası'),
    ('M628', 'Conveyor_switch_fault', 'Conveyor switch fault', 'Konveyör switch arızası'),
    ('M629', 'Data_transfer_sequence_error', 'Data transfer sequence error', 'Veri aktarım sırası hatası'),
    ('M631', 'X_axis_jammed', 'X axis jammed', 'X ekseni sıkıştı'),
    ('M632', 'Y1_axis_jammed', 'Y1 axis jammed', 'Y1 ekseni sıkıştı'),
    ('M633', 'Y2_axis_jammed', 'Y2 axis jammed', 'Y2 ekseni sıkıştı'),
    ('M634', 'Sliding_sash_press_did_not_press', 'Sliding sash press did not press', 'Sürme kanat baskı basamadı'),
    ('M635', 'Sliding_sash_press_did_not_go_back', 'Sliding sash press did not go back', 'Sürme kanat baskı geri gidemedi')]]

# 17 November: an axis unit each (its jog and position from its row - the jog new on this page), its reference button
# (Send to reference, M30 ... M32), its reference switch (X6 / X7 / X10, normally closed) and Home OK (M22 / M20 / M21). The
# old Limit lamps M100 / M102 / M104 were Manual Control's toggles (profile emptying, side tables, heaters): left out.
G.EXPECTED['ManualReference'] = {'missing': {'M100', 'M102', 'M104'},
                                 'extra': {'M40', 'M41', 'M42', 'M43', 'M44', 'M45', 'X0006', 'X0007', 'X0010'}}
_t('Reference', 'Reference', 'Referans')
_t('Reference Switch', 'Reference Switch', 'Referans Switch')
_t('Home OK', 'Home OK', 'Referans OK')
page('ManualReference', 'Manual', 'ucReferance', 'References',
    [Panel('References', [('Name', CAP), ('Axis', AXU), ('Reference', 100), ('Reference Switch', 124), ('Home OK', 80)], [
        Row('Axis X', Ax(1), B('M30/M30', 'Reference'), Lp('X0006', inv=True), Lp('M22'), h=G.UNIT_ROW_H),
        Row('Axis Y1', Ax(2), B('M31/M31', 'Reference'), Lp('X0007', inv=True), Lp('M20'), h=G.UNIT_ROW_H),
        Row('Axis Y2', Ax(3), B('M32/M32', 'Reference'), Lp('X0010', inv=True), Lp('M21'), h=G.UNIT_ROW_H),
        # the old page's code sets the three
        Row('X, Y1, Y2', None, B('M30!Set,M31!Set,M32!Set', 'Reference All', span=2))])])

# ------------------------------------------------------------ PARAMETERS ----

def opt_rows(rows):
    return [Row(caption, S(addr)) for caption, addr in rows]

page('ParamProduction', 'Parameters', 'ucProductionParameters', 'Production Parameters',
    [Panel('Temperatures', [('Name', 260), ('Value', NUM)], [
        Row('Temperature Tolerance', N('D206 16 /10 1', '°C')),
        Row('Head 1 Additional Temperature', N('D211 16 /10 1', '°C')),
        Row('Head 2 Additional Temperature', N('D212 16 /10 1', '°C')),
        Row('Head 3 Additional Temperature', N('D213 16 /10 1', '°C')),
        Row('Head 4 Additional Temperature', N('D214 16 /10 1', '°C')),
        Row('Width (X) Offset for U Profile', N('D264 16 /10 1'))]),
     # toggle switches, off / on as the old page names them (ucProductionParameters.cs)
     Panel('Options', [('Option (off / on)', 260), ('On', 60)], opt_rows([
        ('Gasket Press - Passive / Active', 'M1008'),
        ('Tol. Dimension - Net / Welding', 'M1001'),
        ('Barcode - Off / On', 'M1004'),
        ('Profile - Normal / U', 'M1034'),
        ('Mould - Single / Multiple', 'M1030')]))])

# an axis settings row per axis (12 November): Actual - its position, Real - its calibration value, Calibrate; its jog
# (Manual Control's) new on this page
G.EXPECTED['ParamCalibration'] = {'extra': {'M40', 'M41', 'M42', 'M43', 'M44', 'M45'}}
G.TRANSLATIONS['Axes'] = ('Axes', 'Eksenler')
# (17 November: its send to reference - the Ref column - too)
G.EXPECTED['ParamCalibration']['extra'] |= {'M30', 'M31', 'M32'}
page('ParamCalibration', 'Parameters', 'ucCalibration', 'Calibration',
    [G.AxisRows('Axes', [G.AR(G.AXES[n]) for n in (1, 2, 3)], headings=G.AXIS_HEADINGS + [('Ref', 'Ref', 968, 44)])],
    [Panel('Calibration', [('Axes', CAP), ('Calibrate', BTN + 20)], [
        # the old page's code sets the three
        Row('X, Y1, Y2', B('M91!Set,M92!Set,M93!Set', 'Calibrate All'), key='All')])])

page('ParamSystem', 'Parameters', 'ucSystemSettings', 'System Parameters',
    [Panel('System', [('Name', 280), ('Value', NUM)], [
        Row('Profile Feeding Y Position', N('D324 32 /10 1')),
        Row('Unloading X Movement Interval', N('D318 32 /10 1')),
        Row('Unloading X Move. (U Profile)', N('D320 32 /10 1')),
        Row('Top Press Operation Delay', N('D329 16 /10 1', 's')),
        Row('Additional Melting Time', N('D353 16 /10 1', 's')),
        Row('Profile Length Tolerance', N('D1206 32 /10 1')),
        Row('Early Starting Factor', N('D756 16 /10 1', 's')),
        Row('Tolerance Positioning Speed', N('D757 16 /1 0', '%')),
        Row('Temperature Value for 4mA', N('D292 32 /10 1', '°C')),
        Row('Temperature Value for 20mA', N('D290 32 /10 1', '°C')),
        Row('Robot Minimum Dimension', N('D220 32 /10 1')),
        Row('Robot Maximum Dimension', N('D224 32 /10 1')),
        Row('Conveyor Starting Delay', N('D326 16 /10 1', 's')),
        Row('Conveyor Starting Delay After Stopping', N('D378 16 /10 1', 's')),
        Row('Conveyor Stopping Delay', N('D327 16 /10 1', 's')),
        Row('Gasket Pressing Delaying', N('D208 16 /10 1', 's'))]),
     Panel('Functions', [('Name', 300), ('On', 70)], [
        Row('Heater Auto-tuning', B('M35/M35!Alt', 'On/Off')),
        Row('All Profile Code', B('M1016/M1016!Alt', 'On/Off'))] + opt_rows([
        # toggle switches (ucSystemSettings.cs); Sliding Sash Press writes M1016 there, as All Profile Code
        ('Machine Series - MK / MV', 'M1022'),
        ('Gasket Equipment - No / Yes', 'M1000'),
        ('X1 Feeding with Y - No / Yes', 'M1026'),
        ('Conveyor Stop - Time / Switch', 'M1002'),
        ('U Profile - No / Yes', 'M1032'),
        ('Tolerance After Melting Time Finishes - After / Before', 'M1024'),
        ('Sliding Sash Press - No / Yes', 'M1016'),
        ('X Must Be Greater - No / Yes', 'M1028')]))])

page('ParamSystem2', 'Parameters', 'ucSystemSettings2', 'System Parameters 2',
    [Panel('System2', [('Name', 260), ('X', NUM), ('Y', NUM)], [
        Row('Software Limit (-)', N('D360 32 /10 1'), N('D362 32 /10 1')),
        Row('Software Limit (+)', N('D366 32 /10 1'), N('D368 32 /10 1')),
        Row('Gasket Pressing Min. Length', N('D408 32 /10 1'), N('D372 32 /10 1')),
        Row('Lubrication Frequency (min.)', N('D200 32 /60 1', 'min'), N('D202 32 /60 1', 'min')),
        Row('Lubrication Period', N('D204 16 /1 0', ''), V('D47 16 /10 1', '')),
        Row('Lubrication Tank Capacity', N('D356 16 /1 0', ''), V('D357 16 /10 1', '')),
        Row('Maintenance Period (hours)', N('D338 32 /3600 1', 'h'), V('D342 32 /36000 1', 'h')),
        Row('General Maintenance Period (hours)', N('D336 32 /3600 1', 'h'), V('D340 32 /36000 1', 'h')),
        Row('Automatic Operation Speed (%)', N('D752 32 /1 0', '%'), N('D754 32 /1 0', '%')),
        Row('Manual Operation Speed (%)', N('D742 16 /320 1', '%'), N('D745 16 /320 1', '%')),
        Row('Reference 1st Speed (%)', N('D740 16 /320 1', '%'), N('D743 32 /320 1', '%')),
        Row('Reference 2nd Speed (%)', N('D749 16 /320 1', '%'), N('D750 16 /320 1', '%')),
        Row('Encoder Multiplier', N('D306 32 /1 0', ''), N('D310 32 /1 0', '')),
        Row('Encoder Divisor', N('D308 32 /1 0', ''), N('D312 32 /1 0', ''))]),
     Panel('Functions2', [('Name', 300), ('On', 70)], [
        Row('Separate Supporting and Heater Plates', B('M1018/M1018!Alt', 'On/Off')),
        Row('Maximum 20 Series', B('M1037/M1037!Alt', 'On/Off'))])],
    level='Admin')

page('ParamDemo', 'Parameters', 'ucDemo', 'Demo',
    [Panel('Demo', [('Demo', 160), ('On', 70)], [
        Row('Demo', B('M99/M99!Alt', 'On/Off'))])])

page('ParamReports', 'Parameters', 'ucMachineRuntime', 'Reports',
    [Panel('Reports', [('Report Name', 260), ('Value', NUM), ('', NUM)], [
        Row('Processed Corner Number', V('D162 16 /10 1', ''), B('M134', 'Remove')),
        Row('Total Processed Corner Number', V('D164 16 /10 1', '')),
        # hours and minutes (the old labels: the hours as they are, the seconds / 60)
        Row('Total Operation Time', V('D296 32 /1 0', 'h'), V('D298 32 /60 0', 'min')),
        Row('Total Sw-On Time', V('D226 32 /1 0', 'h'), V('D228 32 /60 0', 'min'))])],
    level='Admin')

# the old pages' toggle switches: devices their code reads and writes
G.CODE_DEVICES.update({
    'ucProductionParameters': {'M1008', 'M1001', 'M1004', 'M1034', 'M1030'},
    'ucSystemSettings': {'M1022', 'M1000', 'M1026', 'M1002', 'M1032', 'M1024', 'M1016', 'M1028'},
    'ucReferance': {'M30', 'M31', 'M32'},
    'ucCalibration': {'M91', 'M92', 'M93'},
})

if __name__ == '__main__':
    G.main()
