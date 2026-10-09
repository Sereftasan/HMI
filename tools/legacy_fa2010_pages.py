#!/usr/bin/env python3
"""
FA2010's MANUAL MOVEMENT and MACHINE PARAMETERS pages, corrected from the
pages of the HMI the machine runs today (8 October).

The first pages were generated from the old device list (tools/generate_pages.py
reads FA2010_DeviceList.xls). The machine has been upgraded since that list was
made, and the generator laid every page out as one long run of units, so the
pages had layout problems and some wrong addresses. The OLD HMI's page designers
(tools/legacy/FA2010/uc*.Designer.cs, from the program in use) are now the
specification: the rows, their order, their sections, every button, lamp and
value, and how each value is divided.

  * PAGES below is that specification, written out row by row from the old
    designers (ucYZCrossRollers and ucOther came as text and are only here).
    --check compares it with the old designers: every device the old pages
    show must be here, and nothing may be here that they do not show, except
    the corrections listed in EXPECTED.
  * What the current pages added by hand is KEPT: every property of a control
    that is not one of the page's own (the valve and feedback types, error
    bits, help files, conditions, triggers, tips, comments...). A control is
    found again by its name, then by its addresses, then by its title.
  * Controls the old pages do not have are left out, and listed (--report).

Old-designer conventions used:
  KabanLambButton  a button with a lamp (LambDevice): the lamp is the side's
                   lamp; a lamp on its own command is the button's own state
  KabanLamb        a lamp; one column wide beside a button: that side's lamp
  KabanLedLabel    a value: raw / Divisor, FloatingPoint decimals - with a
                   Divisor of 1 the decimals are implied (raw / 10^FloatingPoint)
  KabanNumericButton  an entry: raw / Divisor, Mask n<decimals>
  KabanSwitchButton   a machine option bit (L...)
  Back column      button A (left arrow), Forward column button B (right arrow)

Run:  python3 tools/legacy_fa2010_pages.py            (writes the pages)
      python3 tools/legacy_fa2010_pages.py --check    (compares with the old pages)
"""
import os, re, sys, glob, json

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FA = os.path.join(ROOT, 'Kaban.Machines.FA2010')
MANUAL = os.path.join(FA, 'Screens', 'Manual')
PARAMS = os.path.join(FA, 'Screens', 'Parameters')
OLD = os.path.join(ROOT, 'tools', 'legacy', 'FA2010')

# ============================================================================
#  THE SPECIFICATION
# ============================================================================

def side(spec):
    """'M640' / 'M640/M221' / '/M238' / 'M719!Alt' / 'M645/M348+M240' -> dict(cmd, lamp, lamp2, action)"""
    if spec is None: return None
    act = 'Momentary'
    if '!' in spec:
        spec, a = spec.split('!')
        act = {'Alt': 'Alternate', 'Set': 'Set', 'Mom': 'Momentary'}[a]
    cmd, _, lamp = spec.partition('/')
    lamp, _, lamp2 = lamp.partition('+')
    return dict(cmd=cmd or None, lamp=lamp or None, lamp2=lamp2 or None, action=act)

def value(spec):
    """'D4 16 /10 1' -> address, bits, divisor, decimals"""
    a, bits, div, dec = spec.split()
    return dict(addr=a, bits=int(bits), div=float(div.strip('/')), dec=int(dec))

def Cyl(title, a=None, b=None, pos=None, name=None, note=None, valve=None, feedback=None):
    """A unit: Back (A) and Forward (B) as 'command/lamp', its position as 'D4 16 /10 1'.
    valve / feedback: the drawings' types for a unit that has none yet."""
    return dict(kind='cyl', title=title, a=side(a), b=side(b), pos=value(pos) if pos else None, name=name, note=note,
                valve=valve, feedback=feedback)

def Axis(number, title, name=None):
    return dict(kind='axis', number=number, title=title, name=name)

def Btn(title, addr, lamp=None, val=None, name=None, note=None):
    s = side(addr)
    return dict(kind='btn', title=title, cmd=s['cmd'], action=s['action'], lamp=lamp,
                val=value(val) if val else None, name=name, note=note)

def Lamp(title, addr, name=None):
    return dict(kind='lamp', title=title, addr=addr, name=name)

def Hex(title, addr, name=None):
    return dict(kind='hex', title=title, addr=addr, name=name)

def Group(title, buttons, action='Mom', name=None):
    """A row of small buttons under one caption (Change Degree 0/90/180/270)."""
    return dict(kind='group', title=title, buttons=buttons,
                action={'Alt': 'Alternate', 'Set': 'Set', 'Mom': 'Momentary'}[action], name=name)

def Mould(title, back, fwd, pos, target, send, cal, calibrate, tol, offset, valve=None, feedback=None, optional=False):
    """A clamping mould: a pneumatic positioning (PneumaticPositioningHMI, 9 October) - its
    manual movement, target position, calibration, tolerance and direction ref ("Stopping
    Offset" on the old page). optional: fitted only with the Clamping Mould option (L27) -
    the back moulds, vertical and horizontal; the front vertical ones are always fitted."""
    return dict(kind='mould', title=title, a=side(back), b=side(fwd), pos=value(pos + ' 16 /10 2'),
                target=target, send=send, cal=cal, calibrate=calibrate, tol=tol, offset=offset, name=None,
                valve=valve, feedback=feedback, optional=optional)

ENCODERS = {}                        # number -> its row's spec (CylinderEncoderUnitHMI finds it)

def Enc(number, title, back, fwd, pos, ref=None, tol=None, start=None, unclamp=None, note=None, name=None, valve=None, feedback=None):
    """A cylinder with an encoder: a CylinderEncoderSettingHMI row of Machine Parameters >
    Encoders (9 October) - its number, its manual movement (back and forward as
    'command/lamp', its position) and its settings: reference, measuring tolerance
    (tol), minimum limit (start - the old page's Start Limit), unclamping interval."""
    it = dict(kind='enc', number=number, title=title, a=side(back), b=side(fwd), pos=value(pos),
              ref=ref, tol=tol, start=start, unclamp=unclamp, note=note, name=name, valve=valve, feedback=feedback)
    assert number not in ENCODERS, number
    ENCODERS[number] = it
    return it

def EncU(number, title, name=None):
    """A cylinder with an encoder on a Manual Movement page: a CylinderEncoderUnitHMI that takes
    everything from encoder <number>'s row - its title here is the page's own."""
    return dict(kind='encunit', number=number, title=title, name=name)

def Num(caption, addr, bits, div, dec, unit='mm', name=None):
    return dict(kind='num', title=caption, addr=addr, bits=bits, div=float(div), dec=dec, unit=unit, name=name)

def Opt(caption, addr, name=None):
    return dict(kind='opt', title=caption, addr=addr, name=name)

def Col(heading, *items):
    return dict(heading=heading, items=list(items))

def Head(text, name=None):
    """A section heading inside a column (several sections one under another)."""
    return dict(kind='head', title=text, name=name)

# A page: its class, the old designer it comes from, and BANDS of COLUMNS
# (a band's columns start level; a column taller than MAX_H continues in the
# next column).
PAGES = []

def page(cls, folder, old, caption, *bands, **kw):
    p = dict(cls=cls, folder=folder, old=old, caption=caption, bands=list(bands))
    p.update(kw)
    PAGES.append(p)
    return p

# ---------------------------------------------------------------- MANUAL ----

page('ManualEntry_Conveyor', 'Manual', 'ucEntryConveyor', 'Entry Conveyor', [Col(None,
    Cyl('Conveyor Motor', 'M640', 'M639/M221'),
    Lamp('Conveyor Motor Run', 'M203'),
    Hex('Conveyor Inverter Error', 'D666'),
    Lamp('Conveyor Limit SN', 'M344'),
    Cyl('Profile Clampings', 'M721', 'M720'),
    Cyl('Profile Clamping 1', 'M690/M207', 'M683/M214'),
    Cyl('Profile Clamping 2', 'M691/M208', 'M684/M215'),
    Cyl('Profile Clamping 3', 'M692/M209', 'M685/M216'),
    Cyl('Profile Clamping 4', 'M693/M210', 'M686/M217'),
    Cyl('Profile Clamping 5', 'M694/M211', 'M687/M218'),
    Cyl('Profile Clamping 6', 'M695/M212', 'M688/M219'),
    EncU(10, 'Profile Clamping 7'),
    # The old page lit the rolls' DOWN sensors (M346, 2SN.94 / 2SN.95) on the Up
    # button; M745 is the Rolls Down valve (Y42), so they light Down - as since
    # the drawings review of 30 September (REVIEW 4r).
    Cyl('Rollers Up/Down', 'M745/M346', 'M744', name='unitRoller_Up_Down'))])

page('ManualRobot', 'Manual', 'ucRobot', 'Robot', [Col(None,
    Axis(1, 'Axis X'), Axis(2, 'Axis Y'), Axis(3, 'Axis Z'),
    EncU(1, 'Gripper'),
    Btn('Sawdust Blowing', 'M697'),
    Lamp('Bar First End Read SN', 'M323'),
    Lamp('Bar Last End Read SN', 'M324'),
    # The old page (and the device list) lit the 0 degree sensor M262 on Forward
    # (M751); the drawings pair the 0 degree valve Y127 (M752) with the 0 degree
    # sensor SN95 (X122 = M262) and 90 (M751, Y130) with SN96 (X123 = M263) - as
    # since 30 September (REVIEW 4r).
    Cyl('Gripper Angle', 'M752/M262', 'M751/M263'))])

page('ManualProfile_Recognition', 'Manual', 'ucProfileRecognition', 'Profile Recognition', [Col(None,
    Cyl('Unit Up/Down', 'M643/M224', 'M644'),
    Btn('High Air Pressure', 'M719!Alt'),
    EncU(2, 'Front Clamping'),
    EncU(3, 'Top Clamping'),
    Cyl('Roller Up/Down', 'M703/M223', 'M702'),
    Btn('Sawdust Blowing', 'M661'),
    Btn('Left End Encoders Active', 'M724!Alt', lamp='M724'),
    Cyl('Left Top FW/Back', 'M743/M203', 'M742/M206'),
    EncU(11, 'Left Top Clamping'))])

page('ManualMilling', 'Manual', 'ucMilling', 'Milling',
    [Col(None,
        # The old page lit M229 (X55): nothing is wired to X55 on the drawings and
        # the roller (P21) has no sensor - no lamp, as since 30 September.
        Cyl('Roller Up/Down', 'M705', 'M704'),
        Btn('High Air Pressure', 'M716!Alt'),
        EncU(4, 'Top Clamping'),
        EncU(5, 'Front Clamping')),
     Col(None,
        Group('Drills to Changing Pos.', [('M176', '1'), ('M177', '2'), ('M178', '3'), ('M179', '4')], 'Set'),
        Btn('Machine Cleaning Pos.', 'M180!Alt'))],
    [Col('Unit 1',
        Axis(4, 'Unit 1 Angle'), Axis(5, 'Unit 1 X'), Axis(6, 'Unit 1 Y'), Axis(7, 'Unit 1 Z'),
        Btn('Milling Motor', 'M649!Alt', lamp='M230', val='D18 16 /100 2', name='btnUnit_1_Milling_Motor'),
        Hex('Inverter Error', 'D667', name='dispUnit_1_Inverter_Error'),
        Btn('1st Tool Cooling', 'M710!Alt', name='btnUnit_1_Tool_1_Cooling'),
        Btn('2nd Tool Cooling', 'M711!Alt', name='btnUnit_1_Tool_2_Cooling'),
        Group('Change Degree', [('M673', '0°'), ('M674', '90°'), ('M675', '180°'), ('M676', '270°')], name='grpUnit_1_Change_Degree')),
     Col('Unit 2',
        Axis(8, 'Unit 2 Angle'), Axis(9, 'Unit 2 X'), Axis(10, 'Unit 2 Y'), Axis(11, 'Unit 2 Z'),
        Btn('Milling Motor', 'M650!Alt', lamp='M231', val='D19 16 /100 2', name='btnUnit_2_Milling_Motor'),
        Hex('Inverter Error', 'D668', name='dispUnit_2_Inverter_Error'),
        Btn('1st Tool Cooling', 'M712!Alt', name='btnUnit_2_Tool_1_Cooling'),
        Btn('2nd Tool Cooling', 'M713!Alt', name='btnUnit_2_Tool_2_Cooling'),
        Group('Change Degree', [('M677', '0°'), ('M678', '90°'), ('M679', '180°'), ('M680', '270°')], name='grpUnit_2_Change_Degree'))])

page('ManualCutting', 'Manual', 'ucCutting', 'Cutting',
    [Col('Cutting',
        Axis(12, 'Cutting Angle'), Axis(13, 'Cutting Z'),
        Btn('High Air Pressure', 'M651'),
        Cyl('Left Top Small Clamping', 'M699/M230', 'M698'),
        EncU(6, 'Left Top Clamping'),
        EncU(7, 'Right Front Clamping'),
        EncU(9, 'Right Front Vert. Clamping', name='unitRight_Front_Vert_Clamping'),
        Btn('Saw Motor', 'M636!Alt'),
        Hex('Saw Inverter Error', 'D669'),
        Btn('Saw Cooling', 'M714!Alt'),
        Btn('LeftOver Ejection', 'M706'),
        Btn('Blowing', 'M746'),
        Cyl('Profile Press Roller FW/Back', 'M738/M235', 'M737/M236'),
        # A single solenoid: Up (M739); the spring brings it down, so its DOWN sensor
        # (M237, X65) is on the down side - the old page lit it on Up.
        Cyl('Profile Press Roller Up', '/M237', 'M739'),
        Group('Change Degree', [('M181', '45°'), ('M182', '90°'), ('M183', '135°')])),
     Col('V Cutting',
        Axis(14, 'V Cutting Y'), Axis(15, 'V Cutting Z'), Axis(17, 'V Cutting X'),
        Btn('Saw Motor', 'M637!Alt', name='btnV_Cutting_Saw_Motor'),
        Hex('Saw Inverter Error', 'D670', name='dispV_Cutting_Saw_Inverter_Error'),
        # P58, 5/2 double solenoid: M748 drives it down (Y154), M747 up (Y153);
        # its sensors on the drawings are SN106 down (X133 = M271) and SN105 up
        # (X132 = M270) - the old page lit M276 / M277, which are no inputs.
        Cyl('Mullion End Milling Unit', 'M748/M271', 'M747/M270', valve='DoubleSolenoid', feedback='TwoEndMagnetic'),
        # P60, a single solenoid (Y155); SN108 on X135 (M273) is down.
        Btn('Mullion Blade Change', 'M749', lamp='M273'))])

page('ManualExit', 'Manual', 'ucExit', 'Exit', [Col(None,
    EncU(8, 'Robot Top Clamping'),
    Cyl('Robot Top Small Clamping', 'M701/M231', 'M700'),
    Axis(16, 'Exit Robot', name='unitRobot_Movement'),
    Cyl('Roller Up/Down', 'M709/M234', 'M708'),
    Cyl('Profile Ejection', '/M238', 'M707/M239'),
    Cyl('Short Profile Support', 'M682/M204', 'M681/M205'),
    Btn('Conveyor Motor', 'M660', lamp='M243'),
    Hex('Conveyor Inverter Error', 'D671'),
    Lamp('Sawdust Conveyor Thermic', 'M242'),
    Btn('Sawdust Blowing', 'M736'))])

page('ManualOther', 'Manual', None, 'Other', [Col(None,
    Btn('Lubrication', 'M668'),
    Btn('Inverters Reset', 'M670'),
    Btn('Horn', 'M638'),
    Lamp('Emergency Stop', 'M200'),
    Lamp('Air Pressure', 'M201'),
    Lamp('Start Button', 'M202'),
    Lamp('Cooler Alarm', 'M347', name='lampCooler_Error'))])

M = lambda n, a, b, pos, t, s, c, k, tol, off, **kw: Mould(n, a, b, pos, t, s, c, k, tol, off, **kw)
page('ManualClamping_Moulds', 'Manual', 'ucClampingMoulds', 'Clamping Moulds', [Col(None,
    M('Left Profile Recognition Front Vertical', 'M541', 'M540', 'D110', 'D150', 'M50', 'D130', 'M30', 'D750', 'D790'),
    M('Left Profile Recognition Back Vertical', 'M543', 'M542', 'D111', 'D151', 'M51', 'D131', 'M31', 'D751', 'D791', optional=True),
    M('Left Profile Recognition Back Horizontal', 'M545', 'M544', 'D112', 'D152', 'M52', 'D132', 'M32', 'D752', 'D792', optional=True),
    M('Conveyor Front Vertical', 'M547', 'M546', 'D113', 'D153', 'M53', 'D133', 'M33', 'D753', 'D793'),
    M('Conveyor Back Vertical', 'M549', 'M548', 'D114', 'D154', 'M54', 'D134', 'M34', 'D754', 'D794', optional=True),
    M('Conveyor Back Horizontal', 'M551', 'M550', 'D115', 'D155', 'M55', 'D135', 'M35', 'D755', 'D795', optional=True),
    M('Profile Recognition Front Vertical', 'M553', 'M552', 'D116', 'D156', 'M56', 'D136', 'M36', 'D756', 'D796'),
    M('Profile Recognition Back Vertical', 'M555', 'M554', 'D117', 'D157', 'M57', 'D137', 'M37', 'D757', 'D797', optional=True),
    M('Profile Recognition Back Horizontal', 'M557', 'M556', 'D118', 'D158', 'M58', 'D138', 'M38', 'D758', 'D798', optional=True),
    M('Cutting Left Front Vertical', 'M559', 'M558', 'D119', 'D159', 'M59', 'D139', 'M39', 'D759', 'D799'),
    M('Cutting Left Back Vertical', 'M561', 'M560', 'D120', 'D160', 'M60', 'D140', 'M40', 'D760', 'D800', optional=True),
    M('Cutting Left Back Horizontal', 'M563', 'M562', 'D121', 'D161', 'M61', 'D141', 'M41', 'D761', 'D801', optional=True),
    M('Cutting Right Front Vertical', 'M565', 'M564', 'D122', 'D162', 'M62', 'D142', 'M42', 'D762', 'D802'),
    M('Cutting Right Back Vertical', 'M567', 'M566', 'D123', 'D163', 'M63', 'D143', 'M43', 'D763', 'D803', optional=True),
    M('Cutting Right Back Horizontal', 'M569', 'M568', 'D124', 'D164', 'M64', 'D144', 'M44', 'D764', 'D804', optional=True),
    M('Right Front Vertical Clamping', 'M585', 'M584', 'D128', 'D167', 'M67', 'D147', 'M47', 'D846', 'D807',
      # P57 Output Front Up-Down Mold (Y151 up M584 / Y152 down M585): 5/3 closed
      # centre with an analogue sensor, as the drawings show it.
      valve='ThreeWayClosedCenter', feedback='AnalogueMagnetic'))],
    table=True)

page('ManualYZ_Cross_Rollers', 'Manual', None, 'YZ Cross Rollers', [Col(None,
    Cyl('Profile Recognition', 'M574/M266', 'M575'),
    Lamp('Profile Recognition Activation SN', 'M265'),
    Cyl('Milling', 'M576/M267', 'M577'),
    Cyl('Cutting Right', 'M580/M268', 'M581'))])

# ------------------------------------------------------------ PARAMETERS ----
L32, L16 = 32, 16

# 9 October: "remove 'Parameters' of all the tab titles ... Entry (we should divide this page as
# Conveyor and Robot instead actually since they are totally different sections anyway and move the
# right side of Robot ones to the left bottom as well because the page height will be enough then),
# Process (we should divide this page as Milling and Cutting instead actually)" - the old Entry and
# Process pages are two tabs each, a column each, the tab their heading.
page('ParamConveyor', 'Parameters', 'ucEntryParameters', 'Conveyor',
    [Col(None,
        Num('Minimum Bar Length', 'D438', L32, 10000, 1),
        Num('1st Clamping Position', 'D452', L32, 10000, 1),
        Num('2nd Clamping Position', 'D454', L32, 10000, 1),
        Num('3rd Clamping Position', 'D456', L32, 10000, 1),
        Num('4th Clamping Position', 'D458', L32, 10000, 1),
        Num('5th Clamping Position', 'D460', L32, 10000, 1),
        Num('6th Clamping Position', 'D462', L32, 10000, 1),
        Num('7th Clamping Position', 'D464', L32, 10000, 1))])

page('ParamRobot', 'Parameters', 'ucEntryParameters', 'Robot',
    [Col(None,
        Num('Gripper Length', 'D506', L32, 10000, 1),
        Num('Encoder Reference', 'D516', L16, 10, 1),
        Num('Length Measuring Dist.', 'D384', L32, 10000, 1),
        Num('Z Profile Lifting Amount', 'D522', L32, 10000, 1),
        Num('Y Profile Back Clearing Amount', 'D578', L32, 10000, 1),
        Num('1st Clamp Releasing Distance', 'D630', L32, 10000, 1),
        Num('Recognition Extrication Pos.', 'D508', L32, 10000, 1),
        Num('Y Profile Clearing Pos.', 'D626', L32, 10000, 1),
        Num('Z Profile Clearing Pos.', 'D550', L32, 10000, 1),
        Num('Cross Rollers Robot Y Offset', 'D782', L32, 10000, 1),
        Num('Robot Height', 'D664', L32, 10000, 0),
        Num('Left Front Extrication Pos.', 'D520', L32, 10000, 1),
        Num('Conveyor Extrication Position', 'D504', L32, 10000, 1),
        Num('Acceleration Time', 'D558', L32, 1, 0, 'ms'),
        Num('Deceleration Time', 'D560', L32, 1, 0, 'ms'),
        Num('R. Movement Amount / Rotation', 'D678', L32, 10000, 3),
        Num('Linear Encoder Tolerance', 'D628', L32, 10000, 1),
        Num('90 Degree Gripper Y Offset', 'D702', L32, 10000, 1),
        Num('90 Degree Gripper Z Offset', 'D704', L32, 10000, 1),
        Num('Cross Rollers Robot Z Offset', 'D784', L32, 10000, 1))])

page('ParamMilling', 'Parameters', 'ucProcessParameters', 'Milling',
    [Col(None,
        Num('X Top Clamping Clearing Pos.', 'D534', L32, 10000, 1),
        Num('X Top Clamping Distance', 'D552', L32, 100, 1),
        Num('Max. Milling Length w/o Clamp', 'D440', L32, 10000, 1),
        Num('Clampless Speed Override', 'D510', L16, 1, 0, '%'),
        Num('1st Tool Length', 'D526', L32, 10000, 1),
        Num('2nd Tool Length', 'D528', L32, 10000, 1),
        Num('3rd Tool Length', 'D530', L32, 10000, 1),
        Num('4th Tool Length', 'D532', L32, 10000, 1),
        Num('Tool Cooling Time', 'D62', L16, 10, 1, 's'),
        Num('Tool Cooling Period', 'D63', L16, 10, 1, 's'),
        Num('Motor Cooling Stopping Delay', 'D442', L16, 1, 0, 's'),
        Num('1st Milling Unit Motor Center Dislocation', 'D674', L32, 10000, 2),
        Num('2nd Milling Unit Motor Center Dislocation', 'D676', L32, 10000, 2))])

page('ParamCutting', 'Parameters', 'ucProcessParameters', 'Cutting',
    [Col(None,
        Num('Saw Diameter', 'D524', L32, 10000, 1),
        Num('Min. Cut Length to Conveyor', 'D556', L32, 10000, 1),
        Num('Min. Waste Profile to Conveyor', 'D538', L32, 10000, 1),
        Num('45° Target Position', 'D620', L32, 100000, 2, '°'),
        Num('90° Target Position', 'D622', L32, 100000, 2, '°'),
        Num('135° Target Position', 'D624', L32, 100000, 2, '°'),
        Num('Last Piece Cutting Offset', 'D448', L32, 10000, 1),
        Num('Right Front Clamp Min. Length', 'D518', L32, 10000, 1),
        Num('Milling X - Cutting Front Vertical Clamping Distance', 'D680', L32, 10000, 1),
        Num('Long Profile Ejection Min Length', 'D686', L32, 10000, 1),
        Num('Saw Right Top Clamp Clearing Position', 'D682', L32, 10000, 1),
        Num('Saw Cooling Time', 'D68', L16, 10, 1, 's'),
        Num('Saw Cooling Period', 'D69', L16, 10, 1, 's'))])

E = Enc
# 9 October: a CylinderEncoderSettingHMI row an encoder, as the axis rows of Axis Parameters; the
# manual pages' CylinderEncoderUnitHMI take everything from them by number. Each row's movement is
# the manual page's (its sides and lamps as reviewed against the drawings on 30 September): the old
# Encoders page jogged them without lamps. Its old first row, Robot Position (M604 / M605, D22), is
# Robot X - its axis unit on Manual Movement - Robot, D22 its load-side encoder, shown in the tooltip
# of its position - so it is gone from here.
page('ParamEncoders', 'Parameters', 'ucEncodersParameters', 'Encoders',
    [Col(None,
        # The old page jogged this row with M657/M656 - the Exit's Robot Top Clamping, copied
        # from the row below. The gripper is M642/M641 (Manual Movement - Robot); its position
        # in 0.1 mm with ONE decimal, as here (the old Robot page showed two).
        E(1, 'Robot Gripper', 'M642/M222', 'M641', 'D21 16 /10 1', ref='D516'),
        # Forward: the pressure switch the drawings wire (SW1 = SN83 on X70 = M240), as since
        # 30 September; the old page lit M348, which is no input.
        E(2, 'Profile Recognition Front', 'M646/M225', 'M645/M240', 'D4 16 /10 1', ref='D477', tol='D479', start='D483', unclamp='D51'),
        E(3, 'Profile Recognition Top', 'M648/M226', 'M647', 'D6 16 /10 1', ref='D478', tol='D480', start='D484', unclamp='D50'),
        E(4, 'Milling Top', 'M730/M228', 'M729', 'D14 16 /10 1', ref='D511', unclamp='D53'),
        # Forward: the pressure switch (SN84 on X73 = M243), as since 30 September; the old
        # page lit M349, which is no input.
        E(5, 'Milling-Cutting Left Front', 'M653/M227', 'M652/M243', 'D20 16 /10 1', ref='D512', unclamp='D52'),
        E(6, 'Cutting Left Top', 'M672/M245', 'M671', 'D15 16 /10 1', ref='D513', unclamp='D54'),
        E(7, 'Cutting Right Front', 'M655/M232', 'M654', 'D16 16 /10 1', ref='D514', unclamp='D55'),
        E(8, 'Exit Carrying Top', 'M657/M233', 'M656', 'D17 16 /10 1', ref='D515', unclamp='D56'),
        E(9, 'Cutting Front Right Vertical', 'M659/M246', 'M658', 'D7 16 /10 1', ref='D517', unclamp='D64'),
        # The old page's "Left Encoders" (Profile Recognition Front and Top), its buttons the
        # other way round: release / clamp, up / down - as the manual pages and the drawings
        # have them (M696 release, M689 clamp; M741 up, M740 down).
        E(10, 'Left Profile Recognition Front', 'M696/M213', 'M689/M220', 'D4 16 /10 1', ref='D546', name='encLeft_Profile_Recognition_Front'),
        E(11, 'Left Profile Recognition Top', 'M741/M345', 'M740', 'D6 16 /10 1', ref='D547', name='encLeft_Profile_Recognition_Top'))],
    table=True)

page('ParamOther', 'Parameters', 'ucOtherParameters', 'Other Parameters',
    [Col(None,
        Head('Exit Conveyor'),
        Num('Conveyor Length', 'D498', L32, 10000, 1),
        Num('Discharging Plate Height', 'D549', L16, 10, 1),
        Num('Cut Piece Releasing Position', 'D542', L32, 10000, 1),
        Num('Waste Profile Releasing Pos.', 'D544', L32, 10000, 1),
        Num('Min. Cutting Short Profile Pos.', 'D500', L32, 10000, 1),
        Head('Distances Between Units'),
        Num('Length Meas. Sensor - Milling', 'D475', L32, 10000, 1),
        Num('Milling - Cutting Center', 'D446', L32, 100, 2),
        Num('Milling - V Cutting Center', 'D536', L32, 100, 2),
        Head('Machine Lubrication'),
        Num('Lubrication Period', 'D485', L32, 60, 0, 'min'),
        Num('Pulse Number', 'D487', L16, 1, 0, ''),
        Num('Tank Pulse Capacity', 'D488', L16, 1, 0, ''),
        Num('Robot Lubrication Ratio', 'D688', L16, 1, 0, '')),
     Col(None,
        Head('Machine Options'),
        Opt('Mullion Notching', 'L5'),
        Opt('Cooling', 'L6'),
        Opt('Profile Carrying Roller Sensor', 'L3'),
        Opt('Profile Recog. Carrying Roller', 'L13'),
        Opt('Milling Carrying Roller', 'L14'),
        Opt('Exit Carrying Roller', 'L15'),
        Opt('Left Profile Recognition', 'L19'),
        Opt('Sliding Conveyor Rollers', 'L18'),
        Opt('Reverse Machine', 'L37'),
        Opt('Special Reverse Machine', 'L25'),
        Opt('Cutting Right Top Clamping Long', 'L28'),
        Opt('Major Profile Ejection Tray Operates Individually', 'L29'),
        Opt('Top Clamping Reclamp', 'L30'),
        Opt('Gripper 0/90 Degree Turn', 'L31'),
        Opt('Clamping Mould', 'L27'),
        Opt('Cross Roller', 'L32')),
     Col(None,
        Head('Middle Register Notch Parameters'),
        Num('Mullion X Left Cut Offset', 'D840', L32, 10000, 1),
        Num('Mullion X Right Cut Offset', 'D842', L32, 10000, 1),
        Num('Mullion X Right Cut Robot 1 Retraction Amount', 'D844', L32, 10000, 1),
        Num('Mullion X Right Cut Outfeed Robot Start Position', 'D852', L32, 10000, 1),
        Num('Main Saw Blade Thickness', 'D847', L16, 100, 1),
        Num('Mullion Notching Saw Blade Thickness', 'D848', L16, 100, 1),
        Num('Mullion Notching Saw Blade Diameter', 'D849', L16, 100, 1),
        Num('Mullion Notching Single Cut Speed', 'D850', L16, 100, 1, ''),
        Head('Milling X Zero - Cross Roller Distance', name='lblMilling_X_Zero_Cross_Roller_Distance'),
        Num('Entry Conveyor 1', 'D768', L32, 10000, 1, name='numCross_Roller_Entry_Conveyor_1'),
        Num('Entry Conveyor 2', 'D770', L32, 10000, 1, name='numCross_Roller_Entry_Conveyor_2'),
        Num('Profile Recognition', 'D772', L32, 10000, 1, name='numCross_Roller_Profile_Recognition'),
        Num('Milling', 'D774', L32, 10000, 1, name='numCross_Roller_Milling'),
        Num('Cutting Left', 'D776', L32, 10000, 1, name='numCross_Roller_Cutting_Left'),
        Num('Cutting Right', 'D778', L32, 10000, 1, name='numCross_Roller_Cutting_Right'),
        Num('Exit', 'D780', L32, 10000, 1, name='numCross_Roller_Exit'))])

# What --check accepts as deliberate differences from the old designers.
EXPECTED = {
    # the lamps kept from the drawings review (30 September): see the rows
    'ucRobot': {'extra': {'M263'}, 'pairs': {('M752', 'M262'), ('M751', 'M263')}},
    'ucEntryConveyor': {'pairs': {('M745', 'M346')}},
    'ucProfileRecognition': {'extra': {'M240'}, 'missing': {'M348'}, 'pairs': {('M645', 'M240')}},
    'ucMilling': {'extra': {'M243'}, 'missing': {'M229', 'M349'}, 'pairs': {('M652', 'M243')}},
    # the gripper row (M657/M656 stay on Exit Carrying Top); the rows' lamps, the manual pages'
    # (9 October); the Robot Position row is Robot X's axis unit (D22 in its tooltip); the left
    # encoders' buttons release / clamp and up / down, as the manual pages have them
    'ucEncodersParameters': {'extra': {'M642', 'M641', 'M222', 'M225', 'M240', 'M226', 'M228', 'M227', 'M243', 'M245',
                                       'M232', 'M233', 'M246', 'M213', 'M220', 'M345'},
                             'missing': {'M604', 'M330', 'M605', 'D22'},
                             'swapped': {('M696', 'M689'), ('M741', 'M740')}},
    # V Cutting Z's and the Exit Robot's lamps: the axis rows' reference sensors
    # now (M259 - the old Cutting page lit the Exit Robot's M258 - and M343, as
    # on the old Axis Parameters page).
    'ucCutting': {'missing': {'M258', 'M276', 'M277'}, 'extra': {'M271', 'M270', 'M273'},
                  'pairs': {('M748', 'M271'), ('M747', 'M270')}},
    'ucExit': {'missing': {'M258'}},
}

# ============================================================================
#  GEOMETRY
# ============================================================================
X0, Y0 = 16, 16
UNIT_W, UNIT_H = 360, 64
COL_PITCH = 390                  # a unit and a 30 px gutter
GAP = 10
HEAD_H = 26
MAX_H = 650                      # a column taller than this continues beside it
PARAM_ROW = 36
PARAM_BOX = (116, 26)
CHAR_W = 6.4                     # a caption's width, per character (Segoe UI 9)

def height(item):
    k = item['kind']
    if k in ('cyl', 'axis', 'group', 'mould', 'enc', 'encunit'): return UNIT_H
    if k == 'lamp': return 36
    if k == 'head': return HEAD_H - 2
    return 40                       # btn, hex

# ============================================================================
#  THE CURRENT PAGES (what is kept)
# ============================================================================
DECL = re.compile(r'^\s*this\.(\w+) = new ([\w.]+)\(\);\s*$')
PROP = re.compile(r'^\s*this\.(\w+)\.([\w.]+) = (.*);\s*$')

def parse_current(path):
    """name -> dict(type, lines=[(prop, [comments], text)], file)"""
    blocks, order = {}, []
    if not os.path.exists(path): return blocks
    src = open(path, encoding='utf-8-sig').read()
    body = src.split('private void InitializeComponent()', 1)[-1]
    pending = []
    for line in body.split('\n'):
        m = DECL.match(line)
        if m:
            blocks[m.group(1)] = dict(type=m.group(2).split('.')[-1], lines=[], file=os.path.basename(path), page=os.path.basename(path).split('.')[0])
            order.append(m.group(1))
            continue
        s = line.strip()
        if s.startswith('//'):
            # the generator's block headers ("// ", "// name ...") are not kept
            t = s[2:].strip()
            if not t or re.match(r'^\w+(\s+\(.*\))?$', t) or t.startswith('AutoScaleMode') or t.startswith('another') or t.startswith('then') or t.startswith('came out') or t.startswith('from the toolbox') or t.startswith('The layout'):
                pending = []
                continue
            pending.append(line.rstrip())
            continue
        m = PROP.match(line)
        if m and m.group(1) in blocks:
            blocks[m.group(1)]['lines'].append((m.group(2), pending, line.rstrip()))
            pending = []
        else:
            pending = []
    return blocks

def prop_value(block, prop):
    for p, _, text in block['lines']:
        if p == prop:
            return PROP.match(text).group(3).strip().strip('"')
    return None

def bit_addresses(path, name):
    """The bit addresses of a ButtonHMI, in either designer style."""
    src = open(path, encoding='utf-8-sig').read()
    out = []
    for m in re.finditer(r'this\.' + name + r'\.BitActions\.Add\((\w+)\);', src):
        a = re.search(re.escape(m.group(1)) + r'\.Address = "([^"]*)";', src)
        if a: out.append(a.group(1))
    for m in re.finditer(r'this\.' + name + r'\.BitActions\.Add\(new [\w.]+ \{ Address = "([^"]*)"', src):
        out.append(m.group(1))
    return out

def keys_of_current(block, path, name):
    t = block['type']; v = lambda p: prop_value(block, p)
    if t == 'CylinderUnitHMI': return {x for x in (v('CommandAAddress'), v('CommandBAddress')) if x}
    if t == 'ButtonHMI': return set(bit_addresses(path, name))
    if t in ('LampHMI', 'NumericDisplayHMI', 'NumericInputHMI', 'CheckBoxHMI'): return {x for x in (v('Address'),) if x}
    if t == 'AxisUnitHMI': return {'axis:' + (v('AxisNumber') or '')}
    if t in ('PneumaticPositioningHMI', 'CylinderEncoderSettingHMI'): return {x for x in (v('CommandBackAddress'), v('CommandForwardAddress')) if x}
    if t == 'CylinderEncoderUnitHMI': return {'enc:' + (v('EncoderNumber') or '')}
    return set()

def title_of_current(block):
    for p in ('TitleTextKey', 'TitleText', 'TranslationKey', 'FallbackText', 'DescriptionText'):
        x = prop_value(block, p)
        if x: return norm(x)
    return ''

def norm(t):
    return re.sub(r'[^a-z0-9]+', '', (t or '').lower())

# ============================================================================
#  NAMES
# ============================================================================
def ident(text):
    s = text.replace('°', '').replace('%', '')
    s = re.sub(r'[^A-Za-z0-9]+', '_', s).strip('_')
    if s and s[0].isdigit(): s = 'N' + s
    return s

PREFIX = {'cyl': 'unit', 'axis': 'unit', 'encunit': 'unit', 'btn': 'btn', 'lamp': 'lamp', 'hex': 'disp', 'group': 'grp',
          'mould': 'pp', 'enc': 'enc', 'num': 'num', 'opt': 'opt'}

# ============================================================================
#  EMITTING
# ============================================================================
TYPES = {'CylinderUnitHMI': 'Kaban.UI.Controls.CylinderUnitHMI', 'AxisUnitHMI': 'Kaban.UI.Controls.AxisUnitHMI',
         'ButtonHMI': 'Kaban.UI.Controls.ButtonHMI', 'LampHMI': 'Kaban.UI.Controls.LampHMI',
         'NumericDisplayHMI': 'Kaban.UI.Controls.NumericDisplayHMI', 'NumericInputHMI': 'Kaban.UI.Controls.NumericInputHMI',
         'CheckBoxHMI': 'Kaban.UI.Controls.CheckBoxHMI', 'GroupBoxHMI': 'Kaban.UI.Controls.GroupBoxHMI',
         'LabelHMI': 'Kaban.UI.Controls.LabelHMI', 'PneumaticPositioningHMI': 'Kaban.UI.Controls.PneumaticPositioningHMI',
         'CylinderEncoderSettingHMI': 'Kaban.UI.Controls.CylinderEncoderSettingHMI',
         'CylinderEncoderUnitHMI': 'Kaban.UI.Controls.CylinderEncoderUnitHMI',
         'Label': 'System.Windows.Forms.Label'}

# The properties a page sets itself; everything else a control carries is kept.
OWNED = {
    'CylinderUnitHMI': {'BackColor', 'Name', 'PlcNumber', 'TitleTextKey', 'TitleText', 'Location', 'Size', 'LogAction',
                        'CommandAAddress', 'CommandAAction', 'CommandBAddress', 'CommandBAction', 'Lamp1AAddress', 'Lamp1BAddress',
                        'Lamp2AAddress', 'Lamp2BAddress',
                        'PositionAddress', 'PositionDataType', 'PositionDecimalPlaces', 'PositionDigits', 'PositionSuffix',
                        'PositionUnitType', 'PositionDivisor', 'TabIndex'},
    'AxisUnitHMI': {'Name', 'AxisNumber', 'PlcNumber', 'Location', 'Size', 'BackColor', 'TabIndex'},
    'ButtonHMI': {'Name', 'PlcNumber', 'TranslationKey', 'FallbackText', 'Text', 'Lamp1Address', 'Location', 'Size', 'LogAction', 'TabIndex'},
    'LampHMI': {'Name', 'PlcNumber', 'Address', 'DescriptionText', 'Location', 'Size', 'TabIndex'},
    'NumericDisplayHMI': {'Name', 'PlcNumber', 'Address', 'DataType', 'DataFormat', 'Divisor', 'DecimalPlaces', 'Digits',
                          'DescriptionText', 'Location', 'Size', 'Suffix', 'UnitType', 'TabIndex', 'Text'},
    'NumericInputHMI': {'Name', 'PlcNumber', 'Address', 'DataType', 'DescriptionText', 'TranslationKey', 'Text', 'AutoSize',
                        'Digits', 'DecimalPlaces', 'Divisor', 'Suffix', 'UnitType', 'TextAlign', 'LogAction', 'Location',
                        'Size', 'BackColor', 'BorderStyle', 'TabIndex', 'DescriptionSide'},
    'CheckBoxHMI': {'Name', 'PlcNumber', 'Address', 'DescriptionText', 'DescriptionSide', 'Location', 'Size', 'LogAction', 'TabIndex'},
    'PneumaticPositioningHMI': {'BackColor', 'Name', 'PlcNumber', 'TitleTextKey', 'Location', 'Size', 'TabIndex',
                                'CommandBackAddress', 'CommandForwardAddress', 'VerticalMovement',
                                'PositionAddress', 'PositionDataType', 'PositionDivisor', 'PositionDecimalPlaces',
                                'TargetPosition', 'PositioningStartAddress', 'CalibrationAddress', 'CalibrateAddress',
                                'PositioningToleranceAddress', 'PositioningDirectionReferenceAddress', 'Divisor', 'DecimalPlaces'},
    'CylinderEncoderSettingHMI': {'BackColor', 'Name', 'PlcNumber', 'EncoderNumber', 'TitleTextKey', 'Location', 'Size', 'TabIndex',
                                  'CommandBackAddress', 'CommandForwardAddress', 'BackSensorAddress', 'ForwardSensorAddress',
                                  'PositionAddress', 'PositionDataType', 'PositionDivisor', 'PositionDecimalPlaces',
                                  'ReferenceAddress', 'MeasuringToleranceAddress', 'MinimumLimitAddress', 'UnclampingIntervalAddress',
                                  'Divisor', 'DecimalPlaces'},
    'CylinderEncoderUnitHMI': {'BackColor', 'Name', 'EncoderNumber', 'PlcNumber', 'TitleTextKey', 'Location', 'Size', 'TabIndex'},
}

# A control of another type that a new one replaces: its own additions, renamed
# (the clamping moulds' cylinder units, 9 October: their valve and feedback, error
# bits and security go to the pneumatic positioning that replaces each of them).
CARRIED = {
    ('CylinderUnitHMI', 'PneumaticPositioningHMI'): {
        'ErrorAAddress': 'ErrorBackAddress', 'ErrorBAddress': 'ErrorForwardAddress',
        'HelpAFiles': 'HelpBackFiles', 'HelpBFiles': 'HelpForwardFiles',
        'Lamp1AAddress': 'BackSensorAddress', 'Lamp1BAddress': 'ForwardSensorAddress',
        'ValveType': 'ValveType', 'FeedbackType': 'FeedbackType', 'RequiredSecurity': 'RequiredSecurity',
        'VisibilityConditions': 'VisibilityConditions', 'EnableConditions': 'EnableConditions',
        'ConfirmationNeeded': None, 'LogAction': 'LogAction'},
    # 9 October: a cylinder with an encoder - what describes the cylinder goes to its encoder's
    # row (Machine Parameters > Encoders) ...
    ('CylinderUnitHMI', 'CylinderEncoderSettingHMI'): {
        'ErrorAAddress': 'ErrorBackAddress', 'ErrorBAddress': 'ErrorForwardAddress',
        'HelpAFiles': 'HelpBackFiles', 'HelpBFiles': 'HelpForwardFiles',
        'ValveType': 'ValveType', 'FeedbackType': 'FeedbackType', 'RequiredSecurity': 'RequiredSecurity'},
    # ... what the page wanted of it stays with the unit on the page (False: carried to the row)
    ('CylinderUnitHMI', 'CylinderEncoderUnitHMI'): {
        'VisibilityConditions': 'VisibilityConditions', 'EnableConditions': 'EnableConditions',
        'ErrorAAddress': False, 'ErrorBAddress': False, 'HelpAFiles': False, 'HelpBFiles': False,
        'ValveType': False, 'FeedbackType': False, 'RequiredSecurity': False},
}

ACTION = {'Momentary': 'Momentary', 'Alternate': 'Alternate', 'Set': 'SET'}   # Kaban.Core.Enums.BitActionType

PAGE_CODE = '''// <auto-generated>
//   Generated by tools/legacy_fa2010_pages.py from the page of the HMI the machine runs today.
//   This is a normal WinForms DESIGNER file: open the page in Visual Studio and
//   every control appears on the design surface with its properties editable.
//   Re-running the generator overwrites it, so make lasting changes in the
//   workbook (or stop generating this page and keep it by hand).
// </auto-generated>

using System.Windows.Forms;

namespace %s
{
    public partial class %s : UserControl
    {
        public %s()
        {
            InitializeComponent();
        }
    }
}
'''

def cs(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'

def dnum(x):
    return ('%gD' % x) if x != int(x) else ('%dD' % int(x))

class Emitter:
    def __init__(self, page, current):
        self.page = page
        self.current = current          # name -> block (all candidate pages)
        self.decls = []                 # (name, type)
        self.fields = []
        self.body = []
        self.adds = []                  # this.Controls.Add lines
        self.bits = 0
        self.needs_resources = False
        self.kept_from = {}             # new name -> old name
        self.tab = 0
        self.dropped = []               # an old control's additions the new one has no place for

    def decl(self, name, t):
        assert name not in {n for n, _ in self.decls}, name
        self.decls.append((name, t))

    def unique(self, name):
        names = {n for n, _ in self.decls}
        base, i = name, 2
        while name in names:
            name = '%s_%d' % (base, i); i += 1
        return name

    def bit(self):
        self.bits += 1
        return 'bitAction%d' % self.bits

    def header(self, name, note=None):
        self.body.append('            // ')
        self.body.append('            // ' + name)
        self.body.append('            // ')
        if note:
            for l in note.split('\n'):
                self.body.append('            // ' + l)

    def p(self, name, prop, val):
        self.body.append('            this.%s.%s = %s;' % (name, prop, val))

    def kept(self, name, t, old, have=None, report=True):
        """The old control's own additions, renamed (none of the properties in <have>)."""
        if not old: return
        b = self.current[old]
        carried = CARRIED.get((b['type'], t))
        if b['type'] != t and carried is None: return
        own = OWNED.get(b['type'], set())
        have = set(have or ())
        for prop, comments, text in b['lines']:
            if prop in own or prop.startswith('BitActions'): continue
            new = prop
            if carried is not None:
                new = carried.get(prop)
                if new is False: continue            # carried to another control
                if not new:
                    if report: self.dropped.append('%s.%s (no %s property for it)' % (b['own'], prop, t))
                    continue
            if new in have: continue
            have.add(new)
            for c in comments: self.body.append(c)
            text = re.sub(r'this\.' + re.escape(b['own']) + r'\.' + re.escape(prop) + r'\b', 'this.' + name + '.' + new, text)
            text = re.sub(r'this\.' + re.escape(b['own']) + r'\.', 'this.' + name + '.', text)
            if 'resources.GetObject' in text: self.needs_resources = True
            self.body.append(text)
        return have

    def security(self, name, t, old, level):
        if old and self.current[old]['type'] == t and prop_value(self.current[old], 'RequiredSecurity'):
            return                       # kept with the rest
        self.p(name, 'RequiredSecurity', 'Kaban.Core.Enums.SecurityLevel.' + level)

    def add(self, name, parent='this'):
        self.adds.append((parent, name))

    # ------------------------------------------------------------ controls --
    def cylinder(self, name, item, x, y, old, w=UNIT_W):
        self.decl(name, 'CylinderUnitHMI')
        self.header(name, item.get('note'))
        self.p(name, 'BackColor', 'System.Drawing.Color.White')
        self.p(name, 'Name', cs(name))
        self.p(name, 'PlcNumber', '1')
        self.p(name, 'TitleTextKey', cs(item['title']))
        self.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y))
        self.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, UNIT_H))
        self.p(name, 'LogAction', 'true')
        for s, k in ((item.get('a'), 'A'), (item.get('b'), 'B')):
            if not s: continue
            if s['cmd']:
                self.p(name, 'Command%sAddress' % k, cs(s['cmd']))
                self.p(name, 'Command%sAction' % k, 'Kaban.Core.Enums.BitActionType.' + ACTION[s['action']])
            if s['lamp'] and s['lamp'] != s['cmd']:
                self.p(name, 'Lamp1%sAddress' % k, cs(s['lamp']))
            if s.get('lamp2'):
                self.p(name, 'Lamp2%sAddress' % k, cs(s['lamp2']))
        pos = item.get('pos')
        if pos:
            self.p(name, 'PositionAddress', cs(pos['addr']))
            self.p(name, 'PositionDataType', 'Kaban.Core.Enums.DataTypeEnum.Bin_%d' % pos['bits'])
            self.p(name, 'PositionDivisor', dnum(pos['div']))
            self.p(name, 'PositionDecimalPlaces', str(pos['dec']))
            self.p(name, 'PositionDigits', '6' if pos['bits'] == 16 else '8')
            self.p(name, 'PositionSuffix', '"mm"')
            self.p(name, 'PositionUnitType', 'Kaban.Core.Enums.UnitType.Length')
        have = self.kept(name, 'CylinderUnitHMI', old) or set()
        if item.get('valve') and 'ValveType' not in have:
            self.p(name, 'ValveType', 'Kaban.UI.Controls.CylinderValve.' + item['valve'])
        if item.get('feedback') and 'FeedbackType' not in have:
            self.p(name, 'FeedbackType', 'Kaban.UI.Controls.CylinderFeedback.' + item['feedback'])
        self.security(name, 'CylinderUnitHMI', old, 'Operator')
        self.add(name)

    def axis(self, name, item, x, y, old):
        self.decl(name, 'AxisUnitHMI')
        self.header(name, '%s: axis %d of Machine Parameters - Axis Parameters (its buttons, position and reference sensor come from there).' % (item['title'], item['number']))
        self.p(name, 'BackColor', 'System.Drawing.Color.White')
        self.p(name, 'Name', cs(name))
        self.p(name, 'AxisNumber', str(item['number']))
        self.p(name, 'PlcNumber', '1')
        self.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y))
        self.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (UNIT_W, UNIT_H))
        self.kept(name, 'AxisUnitHMI', old)
        self.add(name)

    def button(self, name, text, addr, action, x, y, w, h, old, lamp=None, parent='this', level='Operator', note=None):
        self.decl(name, 'ButtonHMI')
        self.header(name, note)
        self.p(name, 'Name', cs(name))
        self.p(name, 'PlcNumber', '1')
        self.p(name, 'TranslationKey', cs(text))
        self.p(name, 'FallbackText', cs(text))
        b = self.bit()
        self.body.append('            %s.Address = %s;' % (b, cs(addr)))
        self.body.append('            %s.Action = Kaban.Core.Enums.BitActionType.%s;' % (b, ACTION[action]))
        self.body.append('            this.%s.BitActions.Add(%s);' % (name, b))
        if lamp: self.p(name, 'Lamp1Address', cs(lamp))
        self.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y))
        self.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, h))
        self.p(name, 'LogAction', 'true')
        self.kept(name, 'ButtonHMI', old)
        self.security(name, 'ButtonHMI', old, level)
        self.add(name, parent)

    def lamp(self, name, item, x, y, old):
        self.decl(name, 'LampHMI')
        self.header(name)
        self.p(name, 'Name', cs(name))
        self.p(name, 'PlcNumber', '1')
        self.p(name, 'Address', cs(item['addr']))
        self.p(name, 'DescriptionText', cs(item['title']))
        self.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y))
        self.p(name, 'Size', 'new System.Drawing.Size(36, 36)')
        self.kept(name, 'LampHMI', old)
        self.add(name)

    def display(self, name, title, addr, bits, div, dec, x, y, w, old, hexa=False, suffix=None, note=None):
        self.decl(name, 'NumericDisplayHMI')
        self.header(name, note)
        self.p(name, 'Name', cs(name))
        self.p(name, 'PlcNumber', '1')
        self.p(name, 'Address', cs(addr))
        self.p(name, 'DataType', 'Kaban.Core.Enums.DataTypeEnum.Bin_%d' % bits)
        if hexa:
            self.p(name, 'DataFormat', 'Kaban.Core.Enums.DisplayFormatEnum.Hexadecimal')
            self.p(name, 'Digits', '4')
        else:
            self.p(name, 'Divisor', dnum(div))
            self.p(name, 'DecimalPlaces', str(dec))
            self.p(name, 'Digits', '6')
        if suffix: self.p(name, 'Suffix', cs(suffix))
        if title: self.p(name, 'DescriptionText', cs(title))
        self.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y))
        self.p(name, 'Size', 'new System.Drawing.Size(%d, 40)' % w)
        self.kept(name, 'NumericDisplayHMI', old)
        self.add(name)

    def entry(self, name, title, addr, bits, div, dec, unit, x, y, w, h, old, level='Maintenance', describe=True):
        self.decl(name, 'NumericInputHMI')
        self.header(name + '  (%s)' % addr)
        self.p(name, 'BackColor', 'System.Drawing.Color.White')
        self.p(name, 'BorderStyle', 'System.Windows.Forms.BorderStyle.Fixed3D')
        self.p(name, 'Name', cs(name))
        self.p(name, 'PlcNumber', '1')
        self.p(name, 'Address', cs(addr))
        self.p(name, 'DataType', 'Kaban.Core.Enums.DataTypeEnum.Bin_%d' % bits)
        if describe:
            self.p(name, 'DescriptionText', cs(title))
            if old and prop_value(self.current[old], 'TranslationKey') is not None:
                self.p(name, 'TranslationKey', cs(title))
        self.p(name, 'AutoSize', 'false')
        self.p(name, 'Digits', str(max(5, dec + 4)))
        self.p(name, 'DecimalPlaces', str(dec))
        self.p(name, 'Divisor', dnum(div))
        self.p(name, 'Suffix', cs(unit))
        self.p(name, 'UnitType', 'Kaban.Core.Enums.UnitType.' + ({'mm': 'Length', 's': 'Time', 'ms': 'Time', 'min': 'Time'}.get(unit, 'None')))
        self.p(name, 'TextAlign', 'System.Drawing.ContentAlignment.MiddleRight')
        self.p(name, 'LogAction', 'true')
        self.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y))
        self.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, h))
        self.kept(name, 'NumericInputHMI', old)
        self.security(name, 'NumericInputHMI', old, level)
        self.add(name)

    def option(self, name, title, addr, x, y, old):
        self.decl(name, 'CheckBoxHMI')
        self.header(name + '  (%s)' % addr)
        self.p(name, 'Name', cs(name))
        self.p(name, 'PlcNumber', '1')
        self.p(name, 'Address', cs(addr))
        self.p(name, 'DescriptionText', cs(title))
        self.p(name, 'DescriptionSide', 'Kaban.Core.Enums.DescriptionSide.Left')
        self.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y))
        self.p(name, 'Size', 'new System.Drawing.Size(44, 32)')
        self.p(name, 'LogAction', 'true')
        self.kept(name, 'CheckBoxHMI', old)
        self.security(name, 'CheckBoxHMI', old, 'Maintenance')
        self.add(name)

    def group(self, name, item, x, y):
        self.decl(name, 'GroupBoxHMI')
        self.header(name)
        self.p(name, 'Name', cs(name))
        self.p(name, 'PlcNumber', '1')
        self.p(name, 'TitleTextKey', cs(item['title']))
        self.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y))
        self.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (UNIT_W, UNIT_H))
        self.add(name)

    def positioning(self, name, item, x, y, w, old):
        """A clamping mould as a PneumaticPositioningHMI row."""
        t = 'PneumaticPositioningHMI'
        self.decl(name, t)
        self.header(name, '%s: %s / %s, position %s; target %s (%s), calibration %s (%s), tolerance %s, direction ref %s.%s' % (
            item['title'], item['a']['cmd'], item['b']['cmd'], item['pos']['addr'], item['target'], item['send'], item['cal'],
            item['calibrate'], item['tol'], item['offset'],
            '\nFitted only with the Clamping Mould option (L27): shown only while it is on - the rows under it move up.' if item.get('optional') else ''))
        self.p(name, 'BackColor', 'System.Drawing.Color.White')
        self.p(name, 'Name', cs(name))
        self.p(name, 'PlcNumber', '1')
        self.p(name, 'TitleTextKey', cs(item['title']))
        self.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y))
        self.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, PP_ROW_H))
        self.p(name, 'CommandBackAddress', cs(item['a']['cmd']))
        self.p(name, 'CommandForwardAddress', cs(item['b']['cmd']))
        if 'Vertical' in item['title']:
            self.p(name, 'VerticalMovement', 'true')       # back = down, forward = up (the drawings' valves)
        pos = item['pos']
        self.p(name, 'PositionAddress', cs(pos['addr']))
        self.p(name, 'PositionDataType', 'Kaban.Core.Enums.DataTypeEnum.Bin_%d' % pos['bits'])
        self.p(name, 'PositionDivisor', dnum(pos['div']))
        self.p(name, 'PositionDecimalPlaces', str(pos['dec']))
        self.p(name, 'TargetPosition', cs(item['target']))
        self.p(name, 'PositioningStartAddress', cs(item['send']))
        self.p(name, 'CalibrationAddress', cs(item['cal']))
        self.p(name, 'CalibrateAddress', cs(item['calibrate']))
        self.p(name, 'PositioningToleranceAddress', cs(item['tol']))
        self.p(name, 'PositioningDirectionReferenceAddress', cs(item['offset']))
        self.p(name, 'Divisor', '100D')
        self.p(name, 'DecimalPlaces', '2')
        have = self.kept(name, t, old) or set()
        if item.get('valve') and 'ValveType' not in have:
            self.p(name, 'ValveType', 'Kaban.UI.Controls.CylinderValve.' + item['valve'])
        if item.get('feedback') and 'FeedbackType' not in have:
            self.p(name, 'FeedbackType', 'Kaban.UI.Controls.CylinderFeedback.' + item['feedback'])
        if item.get('optional') and 'VisibilityConditions' not in have:
            self.body.append('            // %d: the machine condition "Clamping Mould option is on" (L27, FA2010Module.RegisterConditions)' % CLAMPING_MOULD_OPTION)
            self.p(name, 'VisibilityConditions', '((Kaban.Core.Enums.SystemCondition)(%dL))' % CLAMPING_MOULD_OPTION)
        self.add(name)

    def encoder_row(self, name, item, x, y, w, old, source=None):
        """A cylinder with an encoder as a CylinderEncoderSettingHMI row (9 October). source: the
        manual page's unit it was until then - what was set on it describes the cylinder."""
        t = 'CylinderEncoderSettingHMI'
        self.decl(name, t)
        a, b = item['a'], item['b']
        self.header(name, '%s: encoder %d - %s / %s, position %s; reference %s, measuring tolerance %s, minimum limit %s, unclamping interval %s.%s' % (
            item['title'], item['number'], a['cmd'], b['cmd'], item['pos']['addr'], item.get('ref') or '-', item.get('tol') or '-',
            item.get('start') or '-', item.get('unclamp') or '-', ('\n' + item['note']) if item.get('note') else ''))
        self.p(name, 'BackColor', 'System.Drawing.Color.White')
        self.p(name, 'Name', cs(name))
        self.p(name, 'PlcNumber', '1')
        self.p(name, 'EncoderNumber', str(item['number']))
        self.p(name, 'TitleTextKey', cs(item['title']))
        self.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y))
        self.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, PP_ROW_H))
        self.p(name, 'CommandBackAddress', cs(a['cmd']))
        self.p(name, 'CommandForwardAddress', cs(b['cmd']))
        if a.get('lamp') and a['lamp'] != a['cmd']: self.p(name, 'BackSensorAddress', cs(a['lamp']))
        if b.get('lamp') and b['lamp'] != b['cmd']: self.p(name, 'ForwardSensorAddress', cs(b['lamp']))
        pos = item['pos']
        self.p(name, 'PositionAddress', cs(pos['addr']))
        self.p(name, 'PositionDataType', 'Kaban.Core.Enums.DataTypeEnum.Bin_%d' % pos['bits'])
        self.p(name, 'PositionDivisor', dnum(pos['div']))
        self.p(name, 'PositionDecimalPlaces', str(pos['dec']))
        for key, prop in (('ref', 'ReferenceAddress'), ('tol', 'MeasuringToleranceAddress'), ('start', 'MinimumLimitAddress'),
                          ('unclamp', 'UnclampingIntervalAddress')):
            if item.get(key): self.p(name, prop, cs(item[key]))
        self.p(name, 'Divisor', '10D')
        self.p(name, 'DecimalPlaces', '1')
        have = self.kept(name, t, old) or set()
        # the first time: what the manual page's unit carried (valve, feedback, error bits, help)
        if source and not (old and self.current[old]['type'] == t):
            have |= self.kept(name, t, source, have, report=False) or set()
        if item.get('valve') and 'ValveType' not in have:
            self.p(name, 'ValveType', 'Kaban.UI.Controls.CylinderValve.' + item['valve'])
        if item.get('feedback') and 'FeedbackType' not in have:
            self.p(name, 'FeedbackType', 'Kaban.UI.Controls.CylinderFeedback.' + item['feedback'])
        self.add(name)

    def encoder_unit(self, name, item, x, y, old):
        """A cylinder with an encoder on a manual page: a CylinderEncoderUnitHMI of its encoder's row."""
        t = 'CylinderEncoderUnitHMI'
        self.decl(name, t)
        enc = ENCODERS[item['number']]
        self.header(name, '%s: encoder %d of Machine Parameters - Encoders (%s): its buttons, sensors, position, valve, feedback and error bits come from there.' % (
            item['title'], item['number'], enc['title']))
        self.p(name, 'BackColor', 'System.Drawing.Color.White')
        self.p(name, 'Name', cs(name))
        self.p(name, 'EncoderNumber', str(item['number']))
        self.p(name, 'PlcNumber', '1')
        self.p(name, 'TitleTextKey', cs(item['title']))
        self.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y))
        self.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (UNIT_W, UNIT_H))
        self.kept(name, t, old)
        self.add(name)

    def pp_heading(self, key, text, x, y, w, tag='PositioningHeading:'):
        """A heading over a column of a page's cylinder rows (the rows put it over the column)."""
        name = 'lblHead_' + key
        self.decl(name, 'Label')
        self.header(name)
        self.p(name, 'Font', 'new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold)')
        self.p(name, 'ForeColor', 'System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(116)))), ((int)(((byte)(124)))))')
        self.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y))
        self.p(name, 'Name', cs(name))
        self.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, PP_HEAD_H))
        self.p(name, 'Tag', cs(tag + key))
        self.p(name, 'Text', cs(text))
        self.p(name, 'TextAlign', 'System.Drawing.ContentAlignment.BottomCenter')
        self.add(name)

    def heading(self, name, text, x, y, w, h=HEAD_H, small=False, align='MiddleLeft'):
        self.decl(name, 'LabelHMI')
        self.header(name)
        self.p(name, 'Name', cs(name))
        self.p(name, 'TranslationKey', cs(text))
        self.p(name, 'Text', cs(text))
        if small:
            self.p(name, 'Font', 'new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold)')
            self.p(name, 'ForeColor', 'System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(116)))), ((int)(((byte)(124)))))')
        else:
            self.p(name, 'Font', 'new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold)')
            self.p(name, 'ForeColor', 'System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(70)))), ((int)(((byte)(78)))))')
        self.p(name, 'TextAlign', 'System.Drawing.ContentAlignment.' + align)
        self.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y))
        self.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, h))
        self.add(name)

    # ---------------------------------------------------------------- file --
    def write(self, path, size):
        cls = self.page['cls']; ns = 'Kaban.Machines.FA2010.Screens.' + self.page['folder']
        out = []
        out.append('// <auto-generated>')
        out.append('//   Generated by tools/legacy_fa2010_pages.py from the page of the HMI the')
        out.append('//   machine runs today (%s), 8 October.' % (('tools/legacy/FA2010/' + self.page['old'] + '.Designer.cs') if self.page['old'] else 'the old ' + self.page['caption'] + ' page'))
        out.append('//   This is a normal WinForms DESIGNER file: open the page in Visual Studio')
        out.append('//   and every control is on the design surface with its properties editable.')
        out.append('//   Re-running the tool keeps what was added to a control (valve and feedback')
        out.append('//   types, error bits, conditions, help...) and sets its rows, addresses and')
        out.append('//   layout again from the specification in the tool.')
        out.append('// </auto-generated>')
        out.append('')
        out.append('namespace ' + ns)
        out.append('{')
        out.append('    partial class ' + cls)
        out.append('    {')
        out.append('        private System.ComponentModel.IContainer components = null;')
        out.append('')
        out.append('        protected override void Dispose(bool disposing)')
        out.append('        {')
        out.append('            if (disposing && (components != null)) components.Dispose();')
        out.append('            base.Dispose(disposing);')
        out.append('        }')
        out.append('')
        out.append('        #region Component Designer generated code')
        out.append('')
        out.append('        private void InitializeComponent()')
        out.append('        {')
        for i in range(1, self.bits + 1):
            out.append('            Kaban.Core.Models.BitAction bitAction%d = new Kaban.Core.Models.BitAction();' % i)
        if self.needs_resources:
            out.append('            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(%s));' % cls)
        for n, t in self.decls:
            out.append('            this.%s = new %s();' % (n, TYPES[t]))
        groups = [n for n, t in self.decls if t == 'GroupBoxHMI']
        for g in groups: out.append('            this.%s.SuspendLayout();' % g)
        out.append('            this.SuspendLayout();')
        out.extend(self.body)
        for parent, n in self.adds:
            if parent != 'this': out.append('            this.%s.Controls.Add(this.%s);' % (parent, n))
        out.append('            // ')
        out.append('            // ' + cls)
        out.append('            // ')
        out.append('            // AutoScaleMode.None on purpose: these pages are hosted INSIDE another')
        out.append('            // auto-scaling container (the tab host), which would scale them twice.')
        out.append('            // The layout is in absolute pixels anyway.')
        out.append('            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;')
        out.append('            this.AutoScroll = true;')
        out.append('            this.BackColor = System.Drawing.Color.White;')
        for parent, n in self.adds:
            if parent == 'this': out.append('            this.Controls.Add(this.%s);' % n)
        out.append('            this.Name = %s;' % cs(cls))
        out.append('            this.Size = new System.Drawing.Size(%d, %d);' % size)
        for g in groups: out.append('            this.%s.ResumeLayout(false);' % g)
        out.append('            this.ResumeLayout(false);')
        out.append('            this.PerformLayout();')
        out.append('        }')
        out.append('')
        out.append('        #endregion')
        out.append('')
        for n, t in self.decls:
            out.append('        private %s %s;' % (TYPES[t], n))
        out.append('    }')
        out.append('}')
        out.append('')
        text = '\n'.join(out)
        # a new page (9 October: Conveyor, Robot, Milling, Cutting): its code file as well
        code = path.replace('.Designer.cs', '.cs')
        if not os.path.exists(code):
            open(code, 'w', encoding='utf-8', newline='\n').write(PAGE_CODE % (ns, cls, cls))
        # A BOM only when the page needs it (a degree sign, an arrow): the
        # compiler then cannot read it in the wrong code page.
        open(path, 'w', encoding='utf-8-sig' if any(ord(c) > 127 for c in text) else 'utf-8', newline='\n').write(text)

# ============================================================================
#  MATCHING: which current control each new one is
# ============================================================================
SPEC_TYPE = {'cyl': 'CylinderUnitHMI', 'mould': 'PneumaticPositioningHMI', 'enc': 'CylinderEncoderSettingHMI',
             'encunit': 'CylinderEncoderUnitHMI', 'axis': 'AxisUnitHMI',
             'btn': 'ButtonHMI', 'lamp': 'LampHMI', 'hex': 'NumericDisplayHMI', 'num': 'NumericInputHMI', 'opt': 'CheckBoxHMI'}
# what a spec item may be matched with on the current pages: its own type, or the
# type it replaced (a mould was a CylinderUnitHMI until 9 October)
MATCHES = {'mould': ('PneumaticPositioningHMI', 'CylinderUnitHMI'),
           'enc': ('CylinderEncoderSettingHMI', 'CylinderUnitHMI'),
           'encunit': ('CylinderEncoderUnitHMI', 'CylinderUnitHMI')}

def spec_keys(item):
    k = item['kind']
    if k in ('cyl', 'mould', 'enc'): return {s['cmd'] for s in (item.get('a'), item.get('b')) if s and s['cmd']}
    if k == 'encunit':
        enc = ENCODERS[item['number']]
        return {'enc:%d' % item['number']} | {s['cmd'] for s in (enc['a'], enc['b']) if s and s['cmd']}
    if k == 'btn': return {item['cmd']}
    if k in ('lamp', 'hex', 'num', 'opt'): return {item['addr']}
    if k == 'axis': return {'axis:%d' % item['number']}
    return set()

def assign(pages, current, paths):
    """(page cls, item name) -> current control name. Greedy, best score first."""
    pairs = []
    for pg in pages:
        for it in all_items(pg):
            t = SPEC_TYPE.get(it['kind'])
            if not t: continue
            types = MATCHES.get(it['kind'], (t,))
            keys = spec_keys(it)
            for name, b in current.items():
                if b['type'] not in types: continue
                score = 0
                if name == it['name']: score += 100
                ck = keys_of_current(b, paths[name], b['own'])
                score += 50 * len(keys & ck)
                if norm(it['title']) == title_of_current(b): score += 20
                if b['page'] == pg['cls'] or b['page'].replace('Param', '') in pg['cls']: score += 5
                if score >= 50: pairs.append((score, pg['cls'], it['name'], name))
    pairs.sort(key=lambda x: -x[0])
    used_new, used_old, out = set(), set(), {}
    for score, cls, new, old in pairs:
        if (cls, new) in used_new or old in used_old: continue
        used_new.add((cls, new)); used_old.add(old)
        out[(cls, new)] = old
    return out

def all_items(pg):
    for band in pg['bands']:
        for col in band:
            for it in col['items']:
                yield it

PREFIX['head'] = 'lbl'

def name_items(pg):
    used = set()
    for it in all_items(pg):
        if not it.get('name'):
            it['name'] = PREFIX[it['kind']] + ident(it['title'])
        base, i = it['name'], 2
        while it['name'] in used:
            it['name'] = '%s_%d' % (base, i); i += 1
        used.add(it['name'])

# ============================================================================
#  LAYOUT AND WRITING
# ============================================================================
def split_columns(band):
    """A column taller than MAX_H goes on in the next column."""
    out = []
    for col in band:
        cur = dict(heading=col['heading'], items=[], cont=False)
        h = 0
        for it in col['items']:
            ih = height(it) + GAP
            if cur['items'] and h + ih > MAX_H:
                out.append(cur)
                cur = dict(heading=None, items=[], cont=bool(col['heading']), of=col['heading'])
                h = 0
            cur['items'].append(it); h += ih
        out.append(cur)
    return out

def write_manual(pg, current, match):
    em = Emitter(pg, current)
    old = lambda it: match.get((pg['cls'], it['name']))
    y = Y0
    right = 0
    if pg.get('table'):
        return write_moulds(pg, em, old)
    for bi, band in enumerate(pg['bands']):
        cols = split_columns(band)
        has_head = any(c['heading'] or c.get('cont') for c in cols)
        band_bottom = y
        for ci, col in enumerate(cols):
            x = X0 + ci * COL_PITCH
            cy = y
            if has_head:
                if col['heading']:
                    em.heading(em.unique('lbl' + ident(col['heading'])), col['heading'], x, cy, UNIT_W)
                cy += HEAD_H + 6
            for it in col['items']:
                k, n = it['kind'], it['name']
                if k == 'head':
                    em.heading(n, it['title'], x, cy, UNIT_W)
                elif k == 'cyl':
                    em.cylinder(n, it, x, cy, old(it))
                elif k == 'axis':
                    em.axis(n, it, x, cy, old(it))
                elif k == 'encunit':
                    em.encoder_unit(n, it, x, cy, old(it))
                elif k == 'btn':
                    if it.get('val'):
                        v = it['val']
                        em.button(n, it['title'], it['cmd'], it['action'], x, cy, 230, 40, old(it), lamp=it.get('lamp'))
                        em.display(n + '_Value', None, v['addr'], v['bits'], v['div'], v['dec'], x + 240, cy, 120, None,
                                   note='%s: the value the old page showed beside it (%s, raw / %g).' % (it['title'], v['addr'], v['div']))
                    else:
                        em.button(n, it['title'], it['cmd'], it['action'], x, cy, UNIT_W, 40, old(it), lamp=it.get('lamp'))
                elif k == 'lamp':
                    em.lamp(n, it, x, cy, old(it))
                elif k == 'hex':
                    em.display(n, it['title'], it['addr'], 16, 1, 0, x + UNIT_W - 110, cy, 110, old(it), hexa=True,
                               note='The inverter\'s error code, in hexadecimal as the old page showed it.')
                elif k == 'group':
                    em.group(n, it, x, cy)
                    nb = len(it['buttons'])
                    bw = (UNIT_W - 20 - (nb - 1) * 8) // nb
                    for i, (addr, text) in enumerate(it['buttons']):
                        bn = n.replace('grp', 'btn', 1) + '_' + ident(text)
                        em.button(bn, text, addr, it['action'], 10 + i * (bw + 8), 24, bw, 32, None, parent=n)
                cy += height(it) + GAP
            band_bottom = max(band_bottom, cy)
            right = max(right, x + UNIT_W)
        y = band_bottom + 8
    path = os.path.join(MANUAL, pg['cls'] + '.Designer.cs')
    em.write(path, (right + X0, y))
    return em

# A PneumaticPositioningHMI row (KabanHMI/Controls/PneumaticPositioningHMI.cs): the axis
# rows' height and widths. The rows lay themselves out (the names' column as wide as
# the page's widest name) and put the headings over their columns; the numbers here
# give the designer the same picture.
PP_ROW_H, PP_PITCH, PP_HEAD_H = 40, 44, 32
PP_SECTIONS = [('Manual', 'Manual movement', 248), ('Target', 'Target position', 134), ('Calibration', 'Calibration', 134),
               ('Tolerance', 'Positioning tolerance', 86), ('DirectionRef', 'Direction ref', 86)]
PP_GAP, PP_GROUP_GAP = 4, 18
# FA2010's machine condition for the Clamping Mould option (L27): its fourth machine
# bit (16 RobotIsOutside, 17 MillingUnitInside, 18 MillingUnitInside_PLC2, 19 this).
CLAMPING_MOULD_OPTION = 1 << 19

def pp_name_width(items):
    """The names' column the rows will make (the widest name in Segoe UI 10), roughly."""
    return min(420, max(132, max(int(len(it['title']) * 7.2) + 8 for it in items)))

def write_moulds(pg, em, old):
    """Clamping Moulds: one PneumaticPositioningHMI row a mould (9 October), the headings over them."""
    items = pg['bands'][0][0]['items']
    namew = pp_name_width(items)
    x = X0 + namew + PP_GAP + PP_GROUP_GAP
    for i, (key, text, w) in enumerate(PP_SECTIONS):
        em.pp_heading(key, text, x, Y0, w)
        x += w + (PP_GROUP_GAP if i < len(PP_SECTIONS) - 1 else 0)
    roww = x - X0
    y = Y0 + PP_HEAD_H + 6
    for it in items:
        em.positioning(it['name'], it, X0, y, roww, old(it))
        y += PP_PITCH
    em.write(os.path.join(MANUAL, pg['cls'] + '.Designer.cs'), (X0 + roww + X0, y + 8))
    return em

def caption_w(text):
    return int(len(text) * CHAR_W) + 12

def write_params(pg, current, match):
    em = Emitter(pg, current)
    old = lambda it: match.get((pg['cls'], it['name']))
    if pg.get('table'):
        return write_encoders(pg, em, old)
    # column i's caption width: its widest caption over every band
    ncol = max(len(b) for b in pg['bands'])
    capw = [0] * ncol
    boxw = [44] * ncol                   # a column of options only: the tick box
    for band in pg['bands']:
        for ci, col in enumerate(band):
            for it in col['items']:
                if it['kind'] == 'head':
                    continue
                capw[ci] = max(capw[ci], caption_w(it['title']))
                if it['kind'] == 'num': boxw[ci] = PARAM_BOX[0]
    for band in pg['bands']:
        for ci, col in enumerate(band):
            for t in [col['heading']] + [it['title'] for it in col['items'] if it['kind'] == 'head']:
                if t: capw[ci] = max(capw[ci], caption_w(t) - boxw[ci])
    xs, x = [], X0
    for ci in range(ncol):
        xs.append(x)
        x += capw[ci] + boxw[ci] + 40
    right = x
    y = Y0
    for bi, band in enumerate(pg['bands']):
        bottom = y
        for ci, col in enumerate(band):
            cx = xs[ci]; cy = y
            leading = col['items'] and col['items'][0]['kind'] == 'head'
            if any(c['heading'] for c in band) and not leading:
                if col['heading']:
                    em.heading(em.unique('lbl' + ident(col['heading'])), col['heading'], cx, cy, capw[ci] + boxw[ci])
                cy += HEAD_H + 8
            bx = cx + capw[ci]
            for it in col['items']:
                if it['kind'] == 'head':
                    if cy > y: cy += 6
                    em.heading(it['name'], it['title'], cx, cy, capw[ci] + boxw[ci])
                    cy += HEAD_H + 8
                elif it['kind'] == 'num':
                    em.entry(it['name'], it['title'], it['addr'], it['bits'], it['div'], it['dec'], it['unit'], bx, cy, PARAM_BOX[0], PARAM_BOX[1], old(it))
                    cy += PARAM_ROW
                else:
                    em.option(it['name'], it['title'], it['addr'], bx + boxw[ci] - 44, cy - 3, old(it))
                    cy += PARAM_ROW
            bottom = max(bottom, cy)
        y = bottom + 14
    em.write(os.path.join(PARAMS, pg['cls'] + '.Designer.cs'), (right, y))
    return em

# Machine Parameters > Encoders (9 October): a CylinderEncoderSettingHMI row an encoder, laid out
# as the pneumatic positionings' rows; the five headings over them.
ENC_SECTIONS = [('Manual', 'Manual movement', 248), ('Reference', 'Reference', 86), ('Tolerance', 'Measuring tolerance', 86),
                ('MinimumLimit', 'Minimum limit', 86), ('Unclamping', 'Unclamping interval', 86)]

def manual_source(em, item):
    """The manual page's CylinderUnitHMI an encoder row was until 9 October (its commands), or None."""
    keys = spec_keys(item)
    for k, b in em.current.items():
        if b['type'] == 'CylinderUnitHMI' and b['page'].startswith('Manual') and keys_of_current(b, None, b['own']) == keys:
            return k
    return None

def write_encoders(pg, em, old):
    items = [it for band in pg['bands'] for col in band for it in col['items']]
    namew = pp_name_width(items)
    x = X0 + namew + PP_GAP + PP_GROUP_GAP
    for i, (key, text, w) in enumerate(ENC_SECTIONS):
        em.pp_heading(key, text, x, Y0, w, tag='EncoderHeading:')
        x += w + (PP_GROUP_GAP if i < len(ENC_SECTIONS) - 1 else 0)
    roww = x - X0
    y = Y0 + PP_HEAD_H + 6
    for it in items:
        em.encoder_row(it['name'], it, X0, y, roww, old(it), manual_source(em, it))
        y += PP_PITCH
    em.write(os.path.join(PARAMS, pg['cls'] + '.Designer.cs'), (X0 + roww + X0, y + 8))
    return em

# ============================================================================
#  CHECKING AGAINST THE OLD DESIGNERS
# ============================================================================
def old_devices(old):
    """Every device a VISIBLE control of an old page uses."""
    path = os.path.join(OLD, old + '.Designer.cs')
    src = open(path, encoding='utf-8-sig').read()
    types, visible, parent, props = {}, {}, {}, {}
    for m in re.finditer(r'this\.(\w+) = new ([\w.]+)\(\);', src): types[m.group(1)] = m.group(2).split('.')[-1]
    for m in re.finditer(r'this\.(\w+)\.Controls\.Add\(this\.(\w+)\);', src): parent[m.group(2)] = m.group(1)
    for m in re.finditer(r'this\.(\w+)\.Visible = false;', src): visible[m.group(1)] = False
    for m in re.finditer(r'this\.(\w+)\.(Device|LambDevice) = "([^"]*)";', src): props.setdefault(m.group(1), {})[m.group(2)] = m.group(3)
    def shown(n, d=0):
        if visible.get(n) is False: return False
        p = parent.get(n)
        return True if not p or d > 10 else shown(p, d + 1)
    out = set()
    for n, pr in props.items():
        if types.get(n) not in ('KabanButton', 'KabanLambButton', 'KabanLamb', 'KabanLedLabel', 'KabanNumericButton', 'KabanSwitchButton'): continue
        if not shown(n): continue
        for v in pr.values():
            if v: out.add(v.replace('W0B0', 'WB0').replace('W0C0', 'WC0'))
    return out

AXIS_DEVICES = {1: ('M604', 'M605', 'M330', 'W0'), 2: ('M603', 'M602', 'M341', 'WB0'), 3: ('M601', 'M600', 'M342', 'WC0'),
                4: ('M607', 'M606', 'M331', 'W10'), 5: ('M619', 'M618', 'M337', 'W70'), 6: ('M609', 'M608', 'M332', 'W20'),
                7: ('M611', 'M610', 'M333', 'W30'), 8: ('M613', 'M612', 'M334', 'W40'), 9: ('M621', 'M620', 'M338', 'W80'),
                10: ('M615', 'M614', 'M335', 'W50'), 11: ('M617', 'M616', 'M336', 'W60'), 12: ('M623', 'M622', 'M339', 'W90'),
                13: ('M625', 'M624', 'M340', 'WA0'), 14: ('M627', 'M626', 'M257', 'WE0'), 15: ('M629', 'M628', 'M259', 'WF0'),
                16: ('M731', 'M732', 'M343', 'WD0'), 17: ('M631', 'M630', 'M269', 'D838')}

def encoder_devices(enc):
    """An encoder row's devices: its commands, lamps and position (a manual page's unit of it shows these)."""
    out = {x for s in (enc['a'], enc['b']) if s for x in (s['cmd'], s['lamp'], s.get('lamp2')) if x}
    out.add(enc['pos']['addr'])
    return out

def spec_devices(pg):
    out = set()
    for it in all_items(pg):
        k = it['kind']
        for s in (it.get('a'), it.get('b')):
            if s:
                out |= {x for x in (s['cmd'], s['lamp'], s.get('lamp2')) if x}
        if it.get('pos'): out.add(it['pos']['addr'])
        if k == 'btn':
            out.add(it['cmd'])
            if it.get('lamp'): out.add(it['lamp'])
            if it.get('val'): out.add(it['val']['addr'])
        if k in ('lamp', 'hex', 'num', 'opt'): out.add(it['addr'])
        if k == 'axis': out |= set(AXIS_DEVICES[it['number']])
        if k == 'encunit': out |= encoder_devices(ENCODERS[it['number']])
        if k == 'group': out |= {a for a, _ in it['buttons']}
        for key in ('target', 'send', 'cal', 'calibrate', 'tol', 'offset', 'ref', 'start', 'unclamp'):
            if it.get(key): out.add(it[key])
        if k == 'enc' and it.get('tol'): out.add(it['tol'])
    return out

def old_controls(old):
    """The visible controls of an old page: type, x, Device, LambDevice, divisor, decimals."""
    path = os.path.join(OLD, old + '.Designer.cs')
    src = open(path, encoding='utf-8-sig').read()
    ctl = {}
    for m in re.finditer(r'this\.(\w+) = new ([\w.]+)\(\);', src): ctl[m.group(1)] = dict(t=m.group(2).split('.')[-1])
    parent = {m.group(2): m.group(1) for m in re.finditer(r'this\.(\w+)\.Controls\.Add\(this\.(\w+)\);', src)}
    for m in re.finditer(r'this\.(\w+)\.([\w.]+) = (.+?);\s*$', src, re.M):
        if m.group(1) in ctl: ctl[m.group(1)][m.group(2)] = m.group(3).strip().strip('"')
    def absx(n, d=0):
        c = ctl.get(n); mm = re.search(r'Point\((-?\d+), (-?\d+)\)', c.get('Location', '')) if c else None
        x = int(mm.group(1)) if mm else 0
        return x + (absx(parent[n], d + 1) if n in parent and parent[n] in ctl and d < 10 else 0)
    def shown(n, d=0):
        c = ctl.get(n)
        if c is None: return True
        if c.get('Visible') == 'false': return False
        return shown(parent.get(n), d + 1) if parent.get(n) and d < 10 else True
    out = []
    for n, c in ctl.items():
        if c['t'] in ('KabanButton', 'KabanLambButton', 'KabanLamb', 'KabanLedLabel', 'KabanNumericButton', 'KabanSwitchButton') and shown(n):
            c['x'] = absx(n); c['name'] = n
            for k in ('Device', 'LambDevice'):
                if c.get(k): c[k] = c[k].replace('W0B0', 'WB0').replace('W0C0', 'WC0')
            out.append(c)
    return out

def roles(pg):
    """Side order, lamp pairs and divisors of the spec against the old page."""
    problems = []
    oc = old_controls(pg['old'])
    xs = {c['Device']: c['x'] for c in oc if c.get('Device') and c['t'] in ('KabanButton', 'KabanLambButton')}
    pairs = {(c['Device'], c['LambDevice']) for c in oc if c['t'] == 'KabanLambButton' and c.get('LambDevice')}
    vals = {}
    for c in oc:
        if c['t'] in ('KabanLedLabel', 'KabanNumericButton') and c.get('Device'):
            div = float(c.get('Divisor', '1') or 1)
            if c['t'] == 'KabanLedLabel':
                fp = int(c.get('FloatingPoint', '0') or 0)
                vals.setdefault(c['Device'], set()).add((div if div > 1 else 10 ** fp, fp))
            else:
                mm = re.match(r'n(\d)', c.get('Mask', 'n0'))
                vals.setdefault(c['Device'], set()).add((div, int(mm.group(1)) if mm else 0))
    acts = {c['Device']: c.get('ButtonType', '').split('.')[-1] for c in oc if c.get('Device') and c['t'] in ('KabanButton', 'KabanLambButton')}
    for it in all_items(pg):
        a, b = it.get('a'), it.get('b')
        if a and b and a['cmd'] and b['cmd'] and a['cmd'] in xs and b['cmd'] in xs and not xs[a['cmd']] < xs[b['cmd']] \
                and (a['cmd'], b['cmd']) not in EXPECTED.get(pg['old'], {}).get('swapped', set()):
            problems.append('%s: Back %s is not left of Forward %s' % (it['title'], a['cmd'], b['cmd']))
        for s in (a, b):
            # (an encoder row's lamps are its manual page's, checked there - 9 October)
            if s and s['cmd'] and s['lamp'] and s['cmd'] in xs and (s['cmd'], s['lamp']) not in pairs and it['kind'] != 'enc' \
                    and (s['cmd'], s['lamp']) not in EXPECTED.get(pg['old'], {}).get('pairs', set()):
                problems.append('%s: %s/%s is not a lamp button of the old page' % (it['title'], s['cmd'], s['lamp']))
            if s and s['cmd'] and s['cmd'] in acts and acts[s['cmd']] and acts[s['cmd']] != s['action']:
                problems.append('%s: %s is %s on the old page, %s here' % (it['title'], s['cmd'], acts[s['cmd']], s['action']))
        if it['kind'] == 'btn' and it['cmd'] in acts and acts[it['cmd']] and acts[it['cmd']] != it['action']:
            problems.append('%s: %s is %s on the old page, %s here' % (it['title'], it['cmd'], acts[it['cmd']], it['action']))
        if it['kind'] == 'group':
            for addr, _ in it['buttons']:
                if acts.get(addr) and acts[addr] != it['action']:
                    problems.append('%s: %s is %s on the old page, %s here' % (it['title'], addr, acts[addr], it['action']))
        checks = []
        if it.get('pos'): checks.append((it['pos']['addr'], (it['pos']['div'], it['pos']['dec'])))
        if it.get('val'): checks.append((it['val']['addr'], (it['val']['div'], it['val']['dec'])))
        if it['kind'] == 'num': checks.append((it['addr'], (it['div'], it['dec'])))
        if it['kind'] == 'mould':
            for k in ('target', 'cal', 'tol', 'offset'): checks.append((it[k], (100.0, 2)))
        if it['kind'] == 'enc':
            for k in ('ref', 'tol', 'start', 'unclamp'):
                if it.get(k): checks.append((it[k], (10.0, 1)))
        for addr, dv in checks:
            if addr in vals and dv not in vals[addr]:
                problems.append('%s: %s divided %s here, %s on the old page' % (it['title'], addr, dv, sorted(vals[addr])))
    return problems

def check():
    bad = 0
    olds = []
    for pg in PAGES:
        if pg['old'] and pg['old'] not in olds: olds.append(pg['old'])
    for old in olds:
        # an old page may be two tabs now (Entry: Conveyor and Robot, 9 October): all of them
        pages = [pg for pg in PAGES if pg['old'] == old]
        for pg in pages:
            for pr in roles(pg):
                print('   ', old, pr); bad += 1
        o = old_devices(old)
        s = set().union(*[spec_devices(pg) for pg in pages])
        exp = EXPECTED.get(old, {})
        missing = o - s - exp.get('missing', set())
        extra = s - o - exp.get('extra', set())
        # an axis brings its whole row: the old pages show only some of it
        extra = {x for x in extra if not any(x in AXIS_DEVICES[it['number']] for pg in pages for it in all_items(pg) if it['kind'] == 'axis')}
        print('%-24s old %3d  spec %3d  missing %s  extra %s' % (old, len(o), len(s), sorted(missing) or '-', sorted(extra) or '-'))
        bad += len(missing) + len(extra)
    return bad

# ============================================================================
#  MAIN
# ============================================================================
def main():
    for pg in PAGES: name_items(pg)
    if '--check' in sys.argv:
        sys.exit(1 if check() else 0)
    paths = sorted(glob.glob(os.path.join(MANUAL, '*.Designer.cs')) + glob.glob(os.path.join(PARAMS, 'Param*.Designer.cs')))
    current, where = {}, {}
    for p in paths:
        for n, b in parse_current(p).items():
            key = n if n not in current else b['page'] + ':' + n
            b['own'] = n
            current[key] = b; where[key] = p
    manual = {k: v for k, v in current.items() if os.sep + 'Manual' + os.sep in where[k]}
    params = {k: v for k, v in current.items() if os.sep + 'Parameters' + os.sep in where[k]}
    match = assign([p for p in PAGES if p['folder'] == 'Manual'], manual, where)
    match.update(assign([p for p in PAGES if p['folder'] == 'Parameters'], params, where))
    used = set(match.values())
    # read before the pages are written (a button's bits are read from its file)
    keysets = {k: keys_of_current(b, where[k], b['own']) for k, b in current.items()}
    keys = {k: ', '.join(sorted(v)) for k, v in keysets.items()}
    made = set()
    for pg in PAGES:
        em = (write_manual if pg['folder'] == 'Manual' else write_params)(pg, current, match)
        made |= {(pg['cls'], n) for n, _ in em.decls}
        print('%-26s %3d controls' % (pg['cls'], len(em.decls)))
        for d in em.dropped: print('    not carried over: ' + d)
    # what the earlier pages had and these do not (a control this tool made
    # itself is not one: run again, the list is empty and the file is kept)
    # nor one whose devices a page still has (the moulds' separate fields, now parts of their rows;
    # a page made two, 9 October; a manual unit now its encoder's)
    devices = set().union(*[spec_devices(pg) for pg in PAGES])
    def kept_devices(k):
        ks = keysets[k]
        return bool(ks) and ks <= devices
    left = sorted((current[k]['page'], current[k]['own'], current[k]['type'], k) for k in current
                  if k not in used and current[k]['type'] in SPEC_TYPE.values() and (current[k]['page'], current[k]['own']) not in made
                  and not kept_devices(k))
    if not left:
        print('nothing left out')
        return
    with open(os.path.join(OLD, 'left_out.txt'), 'w', encoding='utf-8') as f:
        f.write('Controls of the earlier generated pages that the pages in use do not have\n')
        f.write('(left out by tools/legacy_fa2010_pages.py, 8 October):\n\n')
        for page_, n, t, k in left:
            addr = keys[k]
            f.write('  %-26s %-50s %-18s %s\n' % (page_, n, t, addr))
    print('left out: %d (tools/legacy/FA2010/left_out.txt)' % len(left))

if __name__ == '__main__':
    main()
