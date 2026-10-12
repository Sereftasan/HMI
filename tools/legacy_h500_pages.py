#!/usr/bin/env python3
"""
H500's MANUAL MOVEMENT and MACHINE PARAMETERS pages, from the pages of the HMI
the machine runs today (12 October: "H500 manual control and machine
parameters pages should be updated from this program totally"), its H500 FX5
version - the pages the old program shows for "H500 FX5" / "H550 FX5":

  tools/legacy/H500/ucScrewingEntryConveyorH500FX.Designer.cs  Manual Movement - Entry Conveyor
  tools/legacy/H500/ucScrewingHeadsH500FX.Designer.cs          Manual Movement - Heads
  tools/legacy/H500/ucScrewingParameterH500FX.Designer.cs      Machine Parameters - Parameters

The machinery is FA2010's (tools/legacy_fa2010_pages.py): PAGES below is the
specification, row by row from the old designers; --check compares it with
them device by device; what is set on a control by hand is kept when the pages
are written again.

How the old controls divide (KabanDevBase, read 12 October):
  KabanLedLabel       raw / (Divisor * 10), FloatingPoint decimals
  KabanNumericButton  raw / Divisor, Mask n<decimals>

Run:  python3 tools/legacy_h500_pages.py            (writes the pages)
      python3 tools/legacy_h500_pages.py --check    (compares with the old pages)
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import legacy_fa2010_pages as L
from legacy_fa2010_pages import page, Col, Cyl, Btn, Lamp, Num, Opt, Head, Disp, Entry

H500 = os.path.join(L.ROOT, 'Kaban.Machines.H500')
L.FA = H500
L.MANUAL = os.path.join(H500, 'Screens', 'Manual')
L.PARAMS = os.path.join(H500, 'Screens', 'Parameters')
L.OLD = os.path.join(L.ROOT, 'tools', 'legacy', 'H500')
L.NS = 'Kaban.Machines.H500.Screens.'
L.TOOL = 'tools/legacy_h500_pages.py'
L.OLD_REL = 'tools/legacy/H500/'
L.MADE = '12 October'
# FA2010's own specification goes; H500's comes in
del L.PAGES[:]
L.EXPECTED.clear()
L.ENCODERS.clear()

L16, L32 = 16, 32

# ---------------------------------------------------------------- MANUAL ----

# X1's buttons on the old page depend on L15: off, the lamp M2017 is on Back (M101) - as here;
# on, it is on Forward (M100). The old page hides all four in its designer and shows a pair.
X1_NOTE = ('X1: Back M101, Forward M100, its lamp M2017 on Back - the old page puts it on Forward\n'
           'while L15 is on (it shows one of two pairs of buttons).')

page('ManualEntry_Conveyor', 'Manual', 'ucScrewingEntryConveyorH500FX', 'Entry Conveyor',
    [Col('Servo Manual',
        Cyl('X1', 'M101/M2017', 'M100', pos='D240 32 /100 1', note=X1_NOTE),
        # the old page's second value on the X rows ("Lambs" column), D156 / D158
        Disp('X1 Second Value', 'D156 32 /100 1', note='X1\'s second value on the old page (D156, raw / 100) - its caption was only the column\'s, "Lambs".'),
        Cyl('X2', 'M103', 'M102/M2018', pos='D242 32 /100 1'),
        Disp('X2 Second Value', 'D158 32 /100 1', note='X2\'s second value on the old page (D158, raw / 100) - its caption was only the column\'s, "Lambs".'),
        Cyl('Z1', 'M105', 'M104/M2039', pos='D244 32 /100 1'),
        Cyl('Z2', 'M107', 'M106/M2040', pos='D246 32 /100 1'),
        Cyl('Y1', 'M109/M2021', 'M108', pos='D250 32 /100 2'),
        Cyl('Y2', 'M111/M2022', 'M110', pos='D252 32 /100 2'),
        Btn('Hole Drilling 1', 'M134!Alt', lamp='M134'),
        Btn('Hole Drilling 2', 'M135!Alt', lamp='M135')),
     Col('Reference',
        Btn('X1 Reference', 'B0!Alt', lamp='B0'),
        Btn('X2 Reference', 'B1!Alt', lamp='B1'),
        Btn('Z1 Reference', 'B2!Alt', lamp='B2'),
        Btn('Z2 Reference', 'B3!Alt', lamp='B3'),
        Btn('Y1 Reference', 'B4!Alt', lamp='B4'),
        Btn('Y2 Reference', 'B5!Alt', lamp='B5'),
        Btn('Home', 'M8!Set')),
     Col('Screwing Tightening',
        Btn('Conveyor', 'M112', lamp='M2006'),
        Cyl('Clampings Up/Down', 'M114/M2007', 'M113/M2008'),
        Btn('Clamping 1', 'M115', lamp='M2009'),
        Btn('Clamping 2', 'M116', lamp='M2010'),
        Btn('Clamping 3', 'M117', lamp='M2011'),
        Btn('Clamping 4', 'M118', lamp='M2012'),
        Btn('Clamping 5', 'M119', lamp='M2013'),
        Btn('Clamping 6', 'M120', lamp='M2014'),
        # the old page lights M2014 on Clamping 7 as on Clamping 6
        Btn('Clamping 7', 'M143', lamp='M2014', note='Clamping 7: the old page lights M2014 here, as on Clamping 6.'),
        Btn('Clampings Release', 'M121', lamp='M2015'),
        Head('Profile Initial Measurement'),
        Btn('Piston Forward', 'M136', lamp='M2016', val='D152 32 /100 1'))])

def head_column(n, start, high, up, feed, unload, torque, begin, done, updown, updown_lamp, press):
    return Col('Head %d' % n,
        Btn('Engine Start', start + '!Alt', lamp=start, name='btnHead_%d_Engine_Start' % n),
        Btn('High Speed', high + '!Alt', lamp=high, name='btnHead_%d_High_Speed' % n),
        Btn('Warehouse Up', up, name='btnHead_%d_Warehouse_Up' % n),
        Btn('Feed Screw', feed[0], lamp=feed[1], name='btnHead_%d_Feed_Screw' % n),
        Btn('Unload Screw', unload, name='btnHead_%d_Unload_Screw' % n),
        Lamp('Servo Torque Limit', torque, name='lampHead_%d_Servo_Torque_Limit' % n),
        Lamp('Start Screwing', begin, name='lampHead_%d_Start_Screwing' % n),
        Lamp('Screwing Finished', done, name='lampHead_%d_Screwing_Finished' % n),
        Btn('Screwing %d Up/Down' % n, updown, lamp=updown_lamp),
        Btn('Screwing %d Press' % n, press))

def positioning_column(n, back, fwd, pos, cal, calibrate, target, go):
    return Col('Screwing %d' % n,
        Cyl('Screwing %d Forward / Back' % n, back, fwd, pos=pos),
        Entry('Calibration Position', cal, 'Calibrate', calibrate, name='numScrewing_%d_Calibration' % n),
        Entry('Position To Go', target, 'GO!', go, name='numScrewing_%d_Position_To_Go' % n))

page('ManualHeads', 'Manual', 'ucScrewingHeadsH500FX', 'Heads',
    [head_column(1, 'M124', 'M126', 'M132', ('M130', 'M2035'), 'M128', 'M2033', 'M2029', 'M2031', 'M122', 'M2027', 'M138'),
     head_column(2, 'M125', 'M127', 'M133', ('M131', 'M2036'), 'M129', 'M2034', 'M2030', 'M2032', 'M123', 'M2028', 'M139')],
    [positioning_column(1, 'M109/M2021', 'M108', 'D250 32 /100 2', 'D160 16 /100 2', 'M9', 'D163 16 /100 2', 'M10!Set'),
     positioning_column(2, 'M111/M2022', 'M110', 'D252 32 /100 2', 'D164 16 /100 2', 'M11', 'D167 16 /100 2', 'M12!Set')])

# ------------------------------------------------------------ PARAMETERS ----

page('ParamMachine', 'Parameters', 'ucScrewingParameterH500FX', 'Parameters',
    [Col(None,
        Head('Axis Speed Limits'),
        Num('Manual Speed X', 'D142', L16, 1, 0, 'm/min'),
        Num('Automatic Speed X', 'D122', L16, 1, 0, 'm/min'),
        Num('Manual Speed Y', 'D143', L16, 1, 0, 'm/min'),
        Num('Automatic Speed Y', 'D144', L16, 1, 0, 'm/min'),
        Head('Screwing Profile Length Limits'),
        Num('Minimum Length', 'D100', L32, 100, 1),
        Num('Maximum Length', 'D102', L32, 100, 1),
        Head('Machine Operation Settings'),
        Num('Screwing 1 Reference Position', 'D110', L32, 100, 1),
        Num('Screwing 2 Reference Position', 'D118', L32, 100, 1),
        Num('Screwing 1 - Screwing 2 Safety Distance', 'D116', L32, 100, 1),
        Num('Screwing Maximum Pressing Time', 'D123', L16, 10, 1, 's'),
        Opt('Hole Drilling Available', 'L0'),
        Opt('Profile Initial Measurement', 'L1'),
        Opt('Screwing Servo', 'L4'),
        Opt('Feed Control Sensor', 'L2')),
     Col(None,
        Num('Clamping 2', 'D124', L32, 100, 1),
        Num('Clamping 3', 'D126', L32, 100, 1),
        Num('Clamping 4', 'D128', L32, 100, 1),
        Num('Clamping 5', 'D130', L32, 100, 1),
        Num('Clamping 6', 'D132', L32, 100, 1),
        Num('Clamping 7', 'D170', L32, 100, 1),
        Num('Screw 1 - Holing 1 Distance', 'D200', L32, 100, 1),
        Num('Screw 2 - Holing 2 Distance', 'D202', L32, 100, 1),
        Num('Holing Z-1 Reference Position', 'D204', L32, 100, 1),
        Num('Holing Z-2 Reference Position', 'D212', L32, 100, 1),
        Num('Z-1 Profile Left Recovering Position', 'D210', L32, 100, 1),
        Num('Profile Initial Measurement Ref. Pos.', 'D150', L32, 100, 1),
        Opt('X Encoder Active', 'L5'),
        Opt('Unit Y Pneumatic Position', 'L3'),
        Num('Drilling Encoder Accuracy', 'D234', L16, 100, 1),
        Num('Screwing Encoder Precision', 'D235', L16, 100, 1))])

# What --check accepts as deliberate differences from the old designers.
L.EXPECTED.update({
    # X1's buttons: hidden in the old designer, one pair shown by L15 (see X1_NOTE)
    'ucScrewingEntryConveyorH500FX': {'extra': {'M100', 'M101', 'M2017'}, 'pairs': {('M101', 'M2017')}},
})

if __name__ == '__main__':
    for d in (L.MANUAL, L.PARAMS):
        os.makedirs(d, exist_ok=True)
    L.main()
