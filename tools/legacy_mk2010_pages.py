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
from legacy_grid_pages import B, V, N, Lp, P, S, Row, Sec, Panel, page

G.setup('Kaban.Machines.MK2010', 'MK2010', 'tools/legacy_mk2010_pages.py', '11 November')

# column widths
CAP, ARW, POS, NUM, BTN, LMP = 170, 96, 104, 104, 104, 30

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

# ---------------------------------------------------------------- MANUAL ----

# a unit's sensors, a lamp a head (the old page's four small lamps: its hidden rows name them Head 1 ... 4)
def heads(*addrs):
    return [Lp(a) for a in addrs]

page('ManualControl', 'Manual', 'ucManuelControl', 'Manual Control',
    [Panel('Axes', [('Name', CAP), ('Back', ARW), ('Forward', ARW), ('Positions', POS), ('Limit', 70)], [
        Row('Axis X', B('M41/M41', '<'), B('M40/M40', '>'), V('D50 32 /10 1'), Lp('X0006', inv=True)),
        Row('Axis Y1', B('M43/M43', '<'), B('M42/M42', '>'), V('D52 32 /10 1'), Lp('X0007', inv=True)),
        Row('Axis Y2', B('M45/M45', '<'), B('M44/M44', '>'), V('D54 32 /10 1'), Lp('X0010', inv=True)),
        Row('Head 1-2 Conveyor', Lp('X0031'), Lp('X0011')),
        Row('Head 3-4 Conveyor', Lp('X0035'), Lp('X0025')),
        Row('Gasket Pressing', Lp('M316'), Lp('M316')),
        Row('Unit Up', Lp('X0064'), Lp('X0065')),
        Row('Head Up', Lp('X0062'), Lp('X0063')),
        Row('Unit Back', Lp('X0060'), Lp('X0061')),
        Row('Lubrication', None, B('M1005/M1005', 'Lubrication'))]),
     Panel('Units', [('Manual Control', 190), ('On/Off', 70)] + [('Head %d' % h, LMP + 8) for h in (1, 2, 3, 4)], [
        Row('Supporting Plate', B('M101/M101!Alt', 'On/Off'), *heads('X0031', 'X0032', 'X0033', 'X0034')),
        Row('', None, *heads('X0011', 'X0012', 'X0013', 'X0014'), key='Supporting Plate 2'),
        Row('Back Plate', B('M102/M102!Alt', 'On/Off'), *heads('X0021', 'X0022', 'X0023', 'X0024')),
        Row('', None, *heads('X0015', 'X0016', 'X0017', 'X0020'), key='Back Plate 2'),
        Row('Top Press', B('M103/M103!Alt', 'On/Off')),
        Row('Heater Plates', B('M104/M104!Alt', 'On/Off'), *heads('X0035', 'X0036', 'X0037', 'X0040')),
        Row('', None, *heads('X0025', 'X0026', 'X0027', 'X0030'), key='Heater Plates 2'),
        Row('Sliding Sash Press', B('M196/M196!Alt', 'On/Off'), *heads('X0054', 'X0055')),
        Row('', None, *heads('X0056', 'X0057'), key='Sliding Sash Press 2'),
        Row('Automatic On/Off', B('M34/M34!Alt', 'On/Off')),
        Row('Profile Lifting', B('M105/M105!Alt', 'On/Off')),
        Row('Profile Release', B('M100/M100!Alt', 'On/Off')),
        Row('Conveyor', B('M106/M106!Alt', 'On/Off')),
        Row('Gasket Pressing İ-G', B('M109/M109!Alt', 'On/Off'), *heads('X0060', 'X0061')),
        Row('Gasket Pressing Y-A', B('M108/M108!Alt', 'On/Off'), *heads('X0064', 'X0065')),
        Row('', None, *heads('X0062', 'X0063'), key='Gasket Pressing Y-A 2')])])

page('ManualReference', 'Manual', 'ucReferance', 'References',
    [Panel('References', [('Name', CAP), ('Forward', ARW), ('Positions', POS), ('Limit', 70), ('Reference OK', 110)], [
        Row('Axis X', B('M30/M30', '>'), V('D50 32 /10 1'), Lp('M100'), Lp('M22')),
        Row('Axis Y1', B('M31/M31', '>'), V('D52 32 /10 1'), Lp('M102'), Lp('M20')),
        Row('Axis Y2', B('M32/M32', '>'), V('D54 32 /10 1'), Lp('M104'), Lp('M21')),
        # the old page's code sets the three
        Row('X, Y1, Y2', B('M30!Set,M31!Set,M32!Set', 'Reference All'))])])

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

page('ParamCalibration', 'Parameters', 'ucCalibration', 'Calibration',
    [Panel('Calibration', [('Axis', CAP), ('Actual', NUM), ('Real', NUM), ('Calibrate', BTN)], [
        Row('Axis X', V('D50 32 /10 1'), N('D154 32 /10 1'), B('M91', 'Calibrate')),
        Row('Axis Y1', V('D52 32 /10 1'), N('D156 32 /10 1'), B('M92', 'Calibrate')),
        Row('Axis Y2', V('D54 32 /10 1'), N('D158 32 /10 1'), B('M93', 'Calibrate')),
        # the old page's code sets the three
        Row('', None, None, B('M91!Set,M92!Set,M93!Set', 'Calibrate All'), key='All')])])

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
