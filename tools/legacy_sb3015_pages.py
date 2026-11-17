#!/usr/bin/env python3
"""
SB3015's pages, from the HMI program the machine runs today - the old program's
_SB3015 project in KabanMachinesSystem (17 November: "Can you complete SB3015 and
MK2010 projects using KabanMachineSystem project (I want you to use cylinder unit
HMI and axis unit HMI controls instead of the controls in KabanMachineSystem (same
as our other machine projects)").

The old pages (tools/legacy/SB3015/, their designers) are tables; PAGES below writes
them out row by row and tools/legacy_grid_pages.py makes them (its docstring says how).
A cylinder is a cylinder unit (Cy), an axis an axis unit (Ax) whose buttons, position,
reference sensor and codes come from its row on Axis Settings (AR), a motor on an
inverter an inverter unit (Iv) - its fault code gives its alarm and help.

  Manual - Entry Conveyor, Robot, Profile Recognition, Milling, Cutting, Exit, Other
           (ucEntryConveyor ... ucOther; the old Servo Status is on Axis Settings now)
  Parameters - Axis Settings (ucAxisParameters + ucToolSettingParameters - the old
           "Tool Settings" tab: backlash, calibration, reference - + ucServoStatus),
           Axis Speeds, Encoders, Entry, Process, Other, Robot Axis Calibration
  The manual pages' bar (ucHeadControlButtons): Screens/Shared/JogBar, by hand

There are no drawings of SB3015 here: a unit's valve and sensor types are read from the
old page - two command buttons a double solenoid, one a single solenoid (the spring the
other way); a lamp on both sides two end sensors, on one side one sensor - and its
error bits from the machine's alarm list (Alarms.json, the Industry 4 program's): the
first movement named (Back / release / up) on the left button (A), the other on the
right (B). tools/legacy/SB3015/README.md lists what to check.

Run:  python3 tools/legacy_sb3015_pages.py            (writes the pages)
      python3 tools/legacy_sb3015_pages.py --check    (compares with the old pages)
"""
import os, sys
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import legacy_grid_pages as G
from legacy_grid_pages import B, V, N, Lp, P, S, Row, Sec, Panel, page, Ax, Cy, Iv, Enc, EncU, PPos, EncRows

G.setup('Kaban.Machines.SB3015', 'SB3015', 'tools/legacy_sb3015_pages.py', '17 November')

# column widths
CAP, UNIT, NUM, BTN, VAL = 210, 270, 110, 110, 104
UH = G.UNIT_ROW_H

def t(en, tr):
    """A text and its Turkish (the machine's Translations.csv)."""
    G.TRANSLATIONS[en] = (en, tr)
    return en

def u(key, en, tr):
    """A unit's name key and its texts."""
    k = 'Unit_SB3015_' + key
    G.TRANSLATIONS[k] = (en, tr)
    return k

def cy(key, en, tr, a=None, b=None, lampA=None, lampB=None, errA=None, errB=None, pos=None, note=None, **kw):
    """A cylinder unit; its valve and sensors read from the old page's buttons and lamps (no drawings here): two commands a
    double solenoid, one a single solenoid; a lamp on each side two end sensors, on one side one; none and no position no
    sensor."""
    cmds = [x for x in (a, b) if x]
    valve = kw.pop('valve', 'DoubleSolenoid' if len(cmds) == 2 else 'SingleSolenoid')
    lamps = [x for x in (lampA, lampB) if x]
    feedback = kw.pop('feedback', 'TwoEndMagnetic' if len(lamps) == 2 else 'SingleMagnetic' if lamps else (None if pos else 'NoSensor'))
    return Cy(u(key, en, tr), a=a, b=b, lampA=lampA, lampB=lampB, valve=valve, feedback=feedback, errA=errA, errB=errB, pos=pos,
              note=note, **kw)

def iv(key, en, tr, **kw):
    return Iv(u(key, en, tr), **kw)

# the common texts of the tables
for en, tr in (('Name', 'İsim'), ('Unit', 'Ünite'), ('Value', 'Değer'), ('Entry Conveyor', 'Giriş Konveyörü'), ('Robot', 'Robot'),
               ('Profile Recognition', 'Profil Tanıma'), ('Milling', 'Frezeleme'), ('Cutting', 'Kesim'), ('Exit', 'Çıkış'),
               ('Other', 'Diğer'), ('Unit 1', 'Ünite 1'), ('Unit 2', 'Ünite 2'), ('Exit Robot', 'Çıkış Robotu'),
               ('Entry Robot', 'Giriş Robotu'), ('Axes', 'Eksenler'), ('Backlash', 'Boşluk'), ('Axis', 'Eksen'),
               ('Free Speed', 'Serbest Hız'), ('Process Speed', 'İşlem Hızı'), ('Target Position', 'Hedef Pozisyon'),
               ('Send', 'Gönder'), ('Machine Options', 'Makine Seçenekleri'),
               ('Robot Bolt Heights', 'Robot Cıvata Yükseklikleri'), ('Robot Height', 'Robot Yüksekliği'), ('Position', 'Pozisyon'),
               ('Error Quantity', 'Hata Miktarı'), ('Exit Conveyor', 'Çıkış Konveyörü'),
               ('Distances Between Units', 'Üniteler Arası Mesafeler'), ('Encoders', 'Enkoderler'), ('All Axes', 'Tüm Eksenler')):
    t(en, tr)

# -------------------------------------------------------------- THE AXES ----
#
# Axis Parameters (the jog, position, Start / Finish Limit, Send Reference), the old Tool Settings tab (backlash,
# Calibration, Reference) and Servo Status (load, reference request, Z phase offset, error / servo error / warning codes)
# - one axis settings row each. A position is raw / 10000 mm (/ 100000 degrees on the A axes), as the old pages divide it.
# Their error bits: the alarm list's (M801 Robot X ... M812 Cutting Z). V Cutting's Y / Z (an option the old page hides,
# MachineInfo.IsVCutting) are not brought over.
AX = [
    # number, caption, key, EN, TR, back, fwd, ref, side, pos, min, max, home, cal, goref, load, homereq, zdist, axerr, servoerr, warn, err, angle
    (1, 'Robot X', 'RobotX', 'Robot X', 'Robot X', 'M604', 'M605', 'M330', 'Forward', 'W0', 'D314', 'D312', 'D316', 'D580', 'B0',
     'WA', 'W8.3', 'D73', 'W4', 'WB', 'W5', 'M801', False),
    (2, 'Milling A', 'MillingA', 'Milling Angle', 'Freze Açısı', 'M607', 'M606', 'M331', 'Back', 'W10', 'D320', 'D318', 'D322', 'D582', 'B1',
     'W1A', 'W18.3', 'D74', 'W14', 'W1B', 'W15', 'M809', True),
    (3, 'Milling 1 Y', 'Milling1Y', 'Milling 1 Y', 'Freze 1 Y', 'M609', 'M608', 'M332', 'Back', 'W20', 'D326', 'D324', 'D328', 'D584', 'B2',
     'W2A', 'W28.3', 'D75', 'W24', 'W2B', 'W25', 'M803', False),
    (4, 'Milling 1 Z', 'Milling1Z', 'Milling 1 Z', 'Freze 1 Z', 'M611', 'M610', 'M333', 'Back', 'W30', 'D332', 'D330', 'D334', 'D586', 'B3',
     'W3A', 'W38.3', 'D76', 'W34', 'W3B', 'W35', 'M804', False),
    # its reference request: W58.3 - the old page reads W68.3 here too (Milling 2 Z's), the other words of the row are W5x
    (5, 'Milling 2 Y', 'Milling2Y', 'Milling 2 Y', 'Freze 2 Y', 'M615', 'M614', 'M335', 'Back', 'W50', 'D344', 'D342', 'D346', 'D590', 'B5',
     'W5A', 'W58.3', 'D78', 'W54', 'W5B', 'W55', 'M806', False),
    (6, 'Milling 2 Z', 'Milling2Z', 'Milling 2 Z', 'Freze 2 Z', 'M617', 'M616', 'M336', 'Back', 'W60', 'D350', 'D348', 'D352', 'D592', 'B6',
     'W6A', 'W68.3', 'D79', 'W64', 'W6B', 'W65', 'M807', False),
    (7, 'Cutting Z', 'CuttingZ', 'Cutting Z', 'Kesim Z', 'M625', 'M624', 'M340', 'Back', 'WA0', 'D374', 'D372', 'D376', 'D600', 'B10',
     'WAA', 'WA8.3', 'D77', 'WA4', 'WAB', 'WA5', 'M812', False),
    # no Z phase offset, servo error or warning on the old page
    (8, 'Cutting A', 'CuttingA', 'Cutting Angle', 'Kesim Açısı', 'M623', 'M622', 'M339', 'Back', 'W90', 'D368', 'D366', 'D370', 'D598', 'B9',
     'W9A', 'W98.3', None, 'W94', None, None, 'M811', True),
    (9, 'Exit Robot X', 'ExitRobotX', 'Exit Robot X', 'Çıkış Robotu X', 'M731', 'M732', 'M343', 'Forward', 'WD0', 'D380', 'D378', 'D382', 'D606',
     'B13', 'WDA', 'WD8.3', 'D80', 'WD4', 'WDB', 'WD5', 'M808', False)]

for (n, cap, key, en, tr, back, fwd, ref, side, pos, lo, hi, home, cal, goref, load, homereq, zdist, axerr, servoerr, warn, err,
     angle) in AX:
    k = 'Axis_SB3015_' + key
    G.TRANSLATIONS[k] = (en, tr)
    G.AXES[n] = dict(number=n, caption=cap, key=k, back=back, fwd=fwd, ref=ref, side=side, pos=pos, min=lo, max=hi, home=home,
                     cal=cal, goref=goref, load=load, homereq=homereq, zdist=zdist, axerr=axerr, servoerr=servoerr, warn=warn, err=err,
                     div=100000 if angle else 10000, dec=2, unit='°' if angle else 'mm')

# ------------------------------------------------------ THE ENCODERS ----
#
# The old Encoders tab's five cylinders, as FA2010's (its program's devices the same: M646 / M645 D4 D477 D479, M547 / M546
# D113 D153 M53 D133 M33 D753 D793 ...): the three clamps measured by an encoder are CylinderEncoderSettingHMI rows (the
# manual pages show them by number, EncU), the two front verticals positioned by their analogue sensors
# PneumaticPositioningHMI rows. The verticals: a 5/3 closed centre valve (they stop at the target, as FA2010's drawings
# have the same cylinders), back down and forward up (FA2010's) - so the height alarms' down bit is Back's.
ENC_FRONT = Enc(1, u('PRFrontClamping', 'Profile Recognition Front Clamping', 'Profil Tanıma Ön Sıkma'), 'M646/M217', 'M645',
                'D4 16 /10 1', ref='D477', tol='D479', valve='DoubleSolenoid', feedback='EncoderPlusReference', errA='M845', errB='M844',
                note='Back M646 (release, its sensor M217), Forward M645 (clamp); releasing alarm M845, clamping M844. Its reference: '
                     'the old page\'s Calibration Position.')
ENC_TOP = Enc(2, u('PRTopClamping', 'Profile Recognition Top Clamping', 'Profil Tanıma Üst Sıkma'), 'M648/M216', 'M647',
              'D6 16 /10 1', ref='D478', tol='D480', valve='DoubleSolenoid', feedback='EncoderPlusReference', errA='M847', errB='M846',
              note='Back M648 (release, M216), Forward M647 (clamp); releasing alarm M847, clamping M846.')
ENC_RIGHT_TOP = Enc(3, u('ExitRobotTopClamping', 'Exit Robot Top Clamping', 'Çıkış Robotu Üst Sıkma'), 'M657/M233', 'M656/M234',
                    'D17 16 /10 1', ref='D515', setdiv=(100, 2), valve='DoubleSolenoid', feedback='MagneticPlusPressureSwitch',
                    errA='M854', errB='M853',
                    note='Cutting\'s Right Top Clamping (the alarm list\'s exit robot top clamping): Back M657 (release, M233), Forward '
                         'M656 (clamp, M234 - taken as its pressure switch); releasing alarm M854, clamping M853; its reference D515 '
                         '(raw / 100, as the old page).')
PP_LEFT = PPos(u('CuttingLeftFrontVertical', 'Cutting Left Front Vertical', 'Kesim Sol Ön Dikey'), 'M547', 'M546', 'D113', 'D153',
               'M53', 'D133', 'M33', 'D753', 'D793', valve='ThreeWayClosedCenter', feedback='AnalogueMagnetic', errA='M864', errB='M863',
               note='Its height alarms: down M864 (Back), up M863 (Forward).')
PP_RIGHT = PPos(u('CuttingRightFrontVertical', 'Cutting Right Front Vertical', 'Kesim Sağ Ön Dikey'), 'M553', 'M552', 'D116', 'D156',
                'M56', 'D136', 'M36', 'D756', 'D796', valve='ThreeWayClosedCenter', feedback='AnalogueMagnetic', errA='M866', errB='M865',
                note='Its height alarms: down M866 (Back), up M865 (Forward).')

def vertical(pp, en, tr):
    """A front vertical on the manual Cutting page: a cylinder unit of its positioning's movement and position (down / up)."""
    return Cy(pp['title'], a=pp['a']['cmd'], b=pp['b']['cmd'], pos=pp['pos']['addr'] + ' 16 /100 2', valve=pp['valve'],
              feedback=pp['feedback'], errA=pp['errA'], errB=pp['errB'], arrows='DownUp',
              note='%s: down %s, up %s, its position %s (raw / 100) - positioned on Parameters > Positioning; %s' % (
                  en, pp['a']['cmd'], pp['b']['cmd'], pp['pos']['addr'], pp['note']))

# the axis settings page's headings (AxisSettingHMI.Headings' keys; AlignHeaders puts each over its column)
AXIS_HEADINGS = [('Axis', 'Axis', 0, 132), ('Position', 'Position', 208, 120), ('Current', 'Current', 330, 80),
                 ('Min', 'Min', 484, 86), ('Max', 'Max', 574, 86), ('Reference', 'Reference', 664, 86),
                 ('Calibration', 'Calibration', 754, 86), ('Ref', 'Ref', 844, 44), ('ZDistance', 'Z distance', 892, 76),
                 ('AxisAlarm', 'Axis alarm', 970, 70), ('ServoAlarm', 'Servo alarm', 1042, 70), ('AxisWarning', 'Axis warning', 1114, 70)]

# --------------------------------------------------------------- MANUAL ----

UNIT_COLS = [('Name', CAP), ('Unit', UNIT)]
VALUE_COLS = [('Name', CAP), ('Unit', UNIT), ('Value', VAL)]

page('ManualEntryConveyor', 'Manual', 'ucEntryConveyor', 'Entry Conveyor',
    [Panel('EntryConveyor', UNIT_COLS, [
        Row(t('Rollers Up/Down', 'Makaralar Yukarı/Aşağı'), cy('EntryRollers', 'Entry Conveyor Rollers', 'Giriş Konveyörü Makaraları',
            a='M745', b='M744', lampA='M205', lampB='M204', errA='M900', errB='M901', arrows='UpDown',
            note='Rollers Up/Down: Back M745 (up, its sensor M205), Forward M744 (down, M204); not up M900, not down M901 - the old '
                 'page\'s Back taken as up.'), h=UH),
        Row(t('Conveyor Motor', 'Konveyör Motoru'), iv('EntryConveyorMotor', 'Entry Conveyor Motor', 'Giriş Konveyör Motoru',
            a='M640', b='M639/M206', fault='D666', error='M827',
            note='Entry Conveyor Motor: back M640, forward M639 (lit by M206); its inverter\'s error D666, the alarm M827.'), h=UH),
        Row(t('Conveyor Forward Button', 'Konveyör İleri Butonu'), Lp('M345')),
        Row(t('High Profile Control SN', 'Yüksek Profil Kontrol SN'), Lp('M208')),
        Row(t('Conveyor Limit SN', 'Konveyör Limit SN'), Lp('M344'))])])

page('ManualRobot', 'Manual', 'ucRobot', 'Robot',
    [Panel('Robot', VALUE_COLS, [
        Row(t('Axis X', 'X Ekseni'), Ax(1), None, h=UH),
        Row(t('Height', 'Yükseklik'), cy('EntryRobotHeight', 'Entry Robot Clamping Height', 'Giriş Robotu Sıkma Yüksekliği',
            a='M540!Alt', lampA='M252', text='Height', feedback='SingleMagnetic',
            note='Height: M540 on / off (its lamp M252) - its up / down alarms M861 / M862 are not given to it: which way the '
                 'one button moves it is not on the old page.'), V('D110 16 /1 0', ''), h=UH),
        Row(t('Bolt Level Motor', 'Cıvata Seviye Motoru'), B('M759', 'Bolt Level Motor'), None),
        Row(t('Bolt Level', 'Cıvata Seviyesi'), N('D150 16 /1 0', ''), B('M50', 'GO!')),
        Row(t('Clamping', 'Sıkma'), cy('EntryRobotClamping', 'Entry Robot Clamping', 'Giriş Robotu Sıkma',
            a='M642', b='M641', lampA='M209', lampB='M210', errA='M841', errB='M840',
            note='Clamping: Back M642 (open, M209), Forward M641 (clamp, M210); clamp opening alarm M841, clamping alarm M840.'), None, h=UH),
        Row(t('Clamped Profile Control SN', 'Sıkılı Profil Kontrol SN'), Lp('M211'), None),
        Row(t('Sawdust Blowing', 'Talaş Üfleme'), B('M697', 'Sawdust Blowing'), None),
        Row(t('Bar First End Read SN', 'Bar İlk Uç Okuma SN'), Lp('M323'), None),
        Row(t('Bar Last End Read SN', 'Bar Son Uç Okuma SN'), Lp('M324'), None)])])

page('ManualProfileRecognition', 'Manual', 'ucProfileRecognition', 'Profile Recognition',
    [Panel('ProfileRecognition', UNIT_COLS, [
        Row(t('Unit Up/Down', 'Ünite Yukarı/Aşağı'), cy('PRUnit', 'Profile Recognition Unit', 'Profil Tanıma Ünitesi',
            a='M643', b='M644', lampA='M215', lampB='M214', errA='M842', errB='M843', arrows='UpDown',
            note='Unit Up/Down: Back M643 (up, M215), Forward M644 (down, M214); up movement alarm M842, down M843.'), h=UH),
        Row(t('Back Support', 'Arka Destek'), cy('PRBackSupport', 'Profile Recognition Back Support', 'Profil Tanıma Arka Destek',
            a='M751', b='M752', lampA='M212', lampB='M213', errA='M902', errB='M903',
            note='Back Support: Back M751 (M212), Forward M752 (M213) - the old page wrote "m751"; back alarm M902, forward M903.'), h=UH),
        Row(t('Front Clamping', 'Ön Sıkma'), EncU(1), h=UH),
        Row(t('Top Clamping', 'Üst Sıkma'), EncU(2), h=UH),
        Row(t('High Air Pressure', 'Yüksek Hava Basıncı'), B('M719!Alt', 'High Air Pressure')),
        Row(t('Sawdust Blowing', 'Talaş Üfleme'), B('M661', 'Sawdust Blowing'))])])

page('ManualMilling', 'Manual', 'ucMilling', 'Milling',
    [Panel('Milling', VALUE_COLS, [
        Row(t('Angle', 'Açı'), Ax(2), None, h=UH),
        Row(t('Change Degree', 'Açı Değiştir'), B('M673', '0°'), B('M674', '90°'), key='ChangeDegree1'),
        Row('', B('M675', '180°'), None, key='ChangeDegree2'),
        Sec('Unit 1'),
        Row('Y', Ax(3), None, key='Unit1Y', h=UH),
        Row('Z', Ax(4), None, key='Unit1Z', h=UH),
        Row(t('Milling Motor', 'Freze Motoru'), iv('Milling1Motor', 'Milling 1 Motor', 'Freze 1 Motoru', a='M649!Alt', fault='D667',
            error='M828', val='D18 16 /10 2', note='Milling 1 Motor: M649 on / off, its value D18 (raw / 10); its inverter\'s error '
            'D667, the alarm M828.'), None,
            key='Unit1Motor', h=UH),
        Sec('Unit 2'),
        Row('Y', Ax(5), None, key='Unit2Y', h=UH),
        Row('Z', Ax(6), None, key='Unit2Z', h=UH),
        Row(t('Milling Motor', 'Freze Motoru'), iv('Milling2Motor', 'Milling 2 Motor', 'Freze 2 Motoru', a='M650!Alt', error='M829', val='D19 16 /10 2',
            note='Milling 2 Motor: M650 on / off, its value D19 (raw / 10); the alarm M829 - the old page shows no error code for its '
                 'inverter.'), None, key='Unit2Motor', h=UH)]),
     Panel('Clampings', UNIT_COLS, [
        Row(t('Front Clamp - Milling Active', 'Ön Sıkma - Frezeleme Aktif'), cy('FrontClampMilling', 'Front Clamp - Milling Active',
            'Ön Sıkma - Frezeleme Aktif', a='M753', b='M754', lampA='M227', lampB='M226', errA='M905', errB='M904',
            note='The front clamp\'s milling / cutting selection - Cutting\'s "Front Clamp - Cutting Active" the other way round: '
                 'Back M753 (M227), Forward M754 (M226); its alarms M905 / M904.'), h=UH),
        Row(t('Front Clamping', 'Ön Sıkma'), cy('CuttingLeftFrontClamping', 'Cutting Left Front Clamping', 'Kesim Sol Ön Sıkma',
            a='M653', b='M652', lampA='M222', lampB='M223', errA='M850', errB='M849',
            note='Front Clamping (Cutting\'s Left Front Clamping): Back M653 (release, M222), Forward M652 (clamp, M223); releasing '
                 'alarm M850, clamping M849.'), h=UH),
        Row(t('Top Clamping', 'Üst Sıkma'), cy('MillingTopClamping', 'Milling Top Clamping', 'Freze Üst Sıkma',
            a='M730', b='M729', lampA='M224', lampB='M225', errA='M887', errB='M886',
            note='Top Clamping: Back M730 (release, M224), Forward M729 (clamp, M225); releasing alarm M887, clamping M886.'), h=UH),
        Row(t('High Air Pressure', 'Yüksek Hava Basıncı'), B('M756!Alt', 'High Air Pressure'))])])

CUT_LEFT = [
    Row(t('Cutting Angle', 'Kesim Açısı'), Ax(8), h=UH),
    Row(t('Change Degree', 'Açı Değiştir'), B('M181', '45°'), key='ChangeDegree45'),
    Row('', B('M182', '90°'), key='ChangeDegree90'),
    Row('', B('M183', '135°'), key='ChangeDegree135'),
    Row(t('Cutting Z', 'Kesim Z'), Ax(7), h=UH),
    Row(t('Front Clamp - Cutting Active', 'Ön Sıkma - Kesim Aktif'), cy('FrontClampCutting', 'Front Clamp - Cutting Active',
        'Ön Sıkma - Kesim Aktif', a='M754', b='M753', lampA='M226', lampB='M227', errA='M904', errB='M905',
        note='The front clamp\'s milling / cutting selection: Back M754 (M226), Forward M753 (M227); back alarm M904, forward M905.'), h=UH),
    Row(t('Left Back Support', 'Sol Arka Destek'), cy('CuttingLeftBackSupport', 'Cutting Left Back Support', 'Kesim Sol Arka Destek',
        a='M747', b='M748', lampA='M230', lampB='M231', errA='M873', errB='M872',
        note='Left Back Support: Back M747 (M230), Forward M748 (M231); back alarm M873, forward M872.'), h=UH),
    Row(t('Left Front Vertical', 'Sol Ön Dikey'), vertical(PP_LEFT, 'Left Front Vertical', 'Sol Ön Dikey'), h=UH),
    Row(t('Left Front Clamping', 'Sol Ön Sıkma'), cy('CuttingLeftFrontClamping', 'Cutting Left Front Clamping', 'Kesim Sol Ön Sıkma',
        a='M653', b='M652', lampA='M222', lampB='M223', errA='M850', errB='M849'), h=UH),
    Row(t('Left Bottom Support', 'Sol Alt Destek'), cy('CuttingLeftBottomSupport', 'Cutting Left Bottom Support', 'Kesim Sol Alt Destek',
        a='M761', b='M762', lampA='M248', errA='M907', errB='M908',
        note='Left Bottom Support: Back M761 (M248), Forward M762; back alarm M907, forward M908.'), h=UH),
    Row(t('Left Top Clamping', 'Sol Üst Sıkma'), cy('CuttingLeftTopClamping', 'Cutting Left Top Clamping', 'Kesim Sol Üst Sıkma',
        a='M672', b='M671', lampA='M228', lampB='M229', errA='M859', errB='M858',
        note='Left Top Clamping: Back M672 (release, M228), Forward M671 (clamp, M229); releasing alarm M859, clamping M858.'), h=UH),
    Row(t('Left Top Small Clamping', 'Sol Üst Küçük Sıkma'), cy('CuttingLeftTopSmallClamping', 'Cutting Left Top Small Clamping',
        'Kesim Sol Üst Küçük Sıkma', a='M699', b='M698', lampA='M237', errA='M892', errB='M891',
        note='Left Top Small Clamping: Back M699 (release, M237), Forward M698 (clamp); releasing alarm M892, clamping M891.'), h=UH)]


CUT_RIGHT = [
    Row(t('Right Back Support', 'Sağ Arka Destek'), cy('CuttingRightBackSupport', 'Cutting Right Back Support', 'Kesim Sağ Arka Destek',
        a='M749', b='M750', lampA='M235', lampB='M236', errA='M875', errB='M874',
        note='Right Back Support: Back M749 (M235), Forward M750 (M236); back alarm M875, forward M874.'), h=UH),
    Row(t('Right Front Vertical', 'Sağ Ön Dikey'), vertical(PP_RIGHT, 'Right Front Vertical', 'Sağ Ön Dikey'), h=UH),
    Row(t('Right Front Clamping', 'Sağ Ön Sıkma'), cy('CuttingRightFrontClamping', 'Cutting Right Front Clamping', 'Kesim Sağ Ön Sıkma',
        a='M655', b='M654', lampA='M232', lampB='M242', errA='M852', errB='M851',
        note='Right Front Clamping: Back M655 (release, M232), Forward M654 (clamp, M242); releasing alarm M852, clamping M851.'), h=UH),
    Row(t('Right Top Clamping', 'Sağ Üst Sıkma'), EncU(3), h=UH),
    Row(t('Right Top Small Clamping', 'Sağ Üst Küçük Sıkma'), cy('CuttingRightTopSmallClamping', 'Cutting Right Top Small Clamping',
        'Kesim Sağ Üst Küçük Sıkma', a='M701', b='M700', lampA='M238', errA='M894', errB='M893',
        note='Right Top Small Clamping: Back M701 (release, M238), Forward M700 (clamp); releasing alarm M894, clamping M893.'), h=UH),
    Row(t('Right Front Vertical Clamping', 'Sağ Ön Dikey Sıkma'), cy('CuttingRightFrontVerticalClamping',
        'Cutting Right Front Vertical Clamping', 'Kesim Sağ Ön Dikey Sıkma', a='M758', b='M757', lampA='M249', errA='M890', errB='M889',
        note='Right Front Vertical Clamping: Back M758 (release, M249), Forward M757 (clamp); releasing alarm M890, clamping M889.'), h=UH),
    Row(t('Saw Motor', 'Testere Motoru'), iv('SawMotor', 'Saw Motor', 'Testere Motoru', a='M636!Alt', fault='D669', error='M830',
        note='Saw Motor: M636 on / off; its inverter\'s error D669, the alarm M830.'), h=UH),
    Row(t('Profile Press Roller FW/Back', 'Profil Baskı Makarası İleri/Geri'), cy('PressRollerFwBack', 'Profile Press Roller FW/Back',
        'Profil Baskı Makarası İleri/Geri', a='M738', b='M737', lampA='M239', lampB='M240', errA='M897', errB='M896',
        note='Profile Press Roller FW/Back: Back M738 (M239), Forward M737 (M240); back alarm M897, forward M896.'), h=UH),
    Row(t('Profile Press Roller Up', 'Profil Baskı Makarası Yukarı'), cy('PressRollerUp', 'Profile Press Roller Up/Down',
        'Profil Baskı Makarası Yukarı/Aşağı', b='M739', lampA='M241', errA='M899', errB='M898', arrows='DownUp',
        note='Profile Press Roller Up: M739 up (a single solenoid - the spring takes it down), its down sensor M241 on the left '
             'as the old page; down alarm M899, up M898.'), h=UH),
    Row(t('LeftOver Ejection', 'Artık Atma'), B('M706', 'LeftOver Ejection')),
    Row(t('Blowing', 'Üfleme'), B('M746', 'Blowing')),
    Row(t('Front High Air Pressure', 'Ön Yüksek Hava Basıncı'), B('M651', 'Front High Air Pressure')),
    Row(t('Top High Air Pressure', 'Üst Yüksek Hava Basıncı'), B('M716', 'Top High Air Pressure'))]

page('ManualCutting', 'Manual', 'ucCutting', 'Cutting',
    [Panel('CuttingLeft', UNIT_COLS, CUT_LEFT), Panel('CuttingRight', UNIT_COLS, CUT_RIGHT)])

page('ManualExit', 'Manual', 'ucExit', 'Exit',
    [Panel('Exit', VALUE_COLS, [
        Row(t('Exit Robot', 'Çıkış Robotu'), Ax(9), None, h=UH),
        Row(t('Height', 'Yükseklik'), cy('ExitRobotHeight', 'Exit Robot Clamping Height', 'Çıkış Robotu Sıkma Yüksekliği',
            a='M558!Alt', lampA='M253', text='Height', feedback='SingleMagnetic',
            note='Height: M558 on / off (its lamp M253) - its up / down alarms M867 / M868 are not given to it.'), V('D119 16 /1 0', ''), h=UH),
        Row(t('Bolt Level Motor', 'Cıvata Seviye Motoru'), B('M760', 'Bolt Level Motor'), None),
        Row(t('Bolt Level', 'Cıvata Seviyesi'), N('D159 16 /1 0', ''), B('M59', 'GO!')),
        Row(t('Clamping', 'Sıkma'), cy('ExitRobotFrontClamping', 'Exit Robot Front Clamping', 'Çıkış Robotu Ön Sıkma',
            a='M659', b='M658', lampA='M245', lampB='M246', errA='M870', errB='M869',
            note='Clamping: Back M659 (release, M245), Forward M658 (clamp, M246); releasing alarm M870, clamping M869.'), None, h=UH),
        Row(t('Clamped Profile Control SN', 'Sıkılı Profil Kontrol SN'), Lp('M243'), None),
        Row(t('Released Profile Control SN', 'Bırakılmış Profil Kontrol SN'), Lp('M247'), None),
        Row(t('Profile Ejection', 'Profil Atma'), cy('ProfileEjection', 'Profile Ejection', 'Profil Atma', b='M707', lampA='M218',
            lampB='M219', errA='M888', errB='M876',
            note='Profile Ejection: M707 forward (a single solenoid), back sensor M218, forward M219; back alarm M888, forward M876.'),
            None, h=UH),
        Row(t('Conveyor Motor', 'Konveyör Motoru'), iv('ExitConveyorMotor', 'Exit Conveyor Motor', 'Çıkış Konveyör Motoru', a='M660',
            fault='D671', error='M831', note='Exit Conveyor Motor: M660 (held); its inverter\'s error D671, the alarm M831.'), None, h=UH),
        Row(t('Sawdust Conveyor Thermic', 'Talaş Konveyörü Termik'), Lp('M244'), None),
        Row(t('Sawdust Blowing', 'Talaş Üfleme'), B('M736', 'Sawdust Blowing'), None)])])

page('ManualOther', 'Manual', 'ucOther', 'Other',
    [Panel('Other', [('Name', CAP), ('Value', BTN)], [
        Row(t('Horn', 'Korna'), B('M638', 'Horn')),
        Row(t('Emergency Stop', 'Acil Stop'), Lp('M200')),
        Row(t('Air Pressure', 'Hava Basıncı'), Lp('M201'))])])

# ----------------------------------------------------------- PARAMETERS ----

def n_rows(rows, unit='mm'):
    return [Row(t(en, tr), N(spec, un if un is not None else unit)) for en, tr, spec, un in rows]

# Axis Settings: the three old pages' rows in one; Send All to Reference (M22) under them; the backlash (raw / 10 mm, / 100
# degrees on the A axes - not the row's divisor, so a table of its own)
G.EXPECTED['ParamAxisSettings'] = {
    # Milling 2 Y's reference request (the old page's W68.3 is Milling 2 Z's)
    'extra': {'W58.3'}}
page('ParamAxisSettings', 'Parameters', ['ucAxisParameters', 'ucToolSettingParameters', 'ucServoStatus'], 'Axis Settings',
    [G.AxisRows('Axes', [Sec(t('Entry Robot', 'Giriş Robotu')), G.AR(G.AXES[1]),
                         Sec(t('Milling', 'Frezeleme')), G.AR(G.AXES[2]), G.AR(G.AXES[3]), G.AR(G.AXES[4]), G.AR(G.AXES[5]), G.AR(G.AXES[6]),
                         Sec(t('Cutting', 'Kesim')), G.AR(G.AXES[7]), G.AR(G.AXES[8]),
                         Sec(t('Exit Robot', 'Çıkış Robotu')), G.AR(G.AXES[9])], headings=AXIS_HEADINGS)],
    [Panel('AllAxes', [('Axes', CAP), ('Send', BTN + 40)], [
        Row(t('All Axes', 'Tüm Eksenler'), B('M22!Set', 'Send All to Reference'), key='SendAll')]),
     Panel('Backlash', [('Axis', CAP), ('Backlash', NUM)], [
        Row(t('Robot X', 'Robot X'), N('D562 16 /10 1'), key='BacklashRobotX'),
        Row(t('Milling Angle', 'Freze Açısı'), N('D563 16 /100 2', '°'), key='BacklashMillingA'),
        Row(t('Milling 1 Y', 'Freze 1 Y'), N('D564 16 /10 1'), key='BacklashMilling1Y'),
        Row(t('Milling 1 Z', 'Freze 1 Z'), N('D565 16 /10 1'), key='BacklashMilling1Z'),
        Row(t('Milling 2 Y', 'Freze 2 Y'), N('D567 16 /10 1'), key='BacklashMilling2Y'),
        Row(t('Milling 2 Z', 'Freze 2 Z'), N('D568 16 /10 1'), key='BacklashMilling2Z'),
        Row(t('Cutting Z', 'Kesim Z'), N('D572 16 /10 1'), key='BacklashCuttingZ'),
        Row(t('Cutting Angle', 'Kesim Açısı'), N('D571 16 /100 2', '°'), key='BacklashCuttingA'),
        Row(t('Exit Robot X', 'Çıkış Robotu X'), N('D575 16 /10 1'), key='BacklashExitRobotX')])])

page('ParamAxisSpeeds', 'Parameters', 'ucAxisSpeeds', 'Axis Speeds',
    [Panel('Speeds', [('Name', CAP), ('Free Speed', NUM + 10), ('Process Speed', NUM + 10)], [
        Row(t('Robot X', 'Robot X'), N('D418 32 /100000 2', 'm/min'), N('D420 32 /100000 2', 'm/min')),
        Row(t('Milling Angle', 'Freze Açısı'), N('D426 32 /1000 2', '°/min'), None),
        Row(t('Millings YZ', 'Frezeler YZ'), N('D424 32 /100000 2', 'm/min'), None),
        Row(t('Cutting', 'Kesim'), N('D428 32 /100000 2', 'm/min'), None),
        Row(t('Cutting Angle', 'Kesim Açısı'), N('D430 32 /1000 2', '°/min'), None),
        Row(t('Exit Robot', 'Çıkış Robotu'), N('D432 32 /100000 2', 'm/min'), N('D434 32 /100000 2', 'm/min'))])])

# the old Encoders tab: its clamps' encoders, and its front verticals' positionings on a page of their own (a page of each kind:
# each kind's rows put their headings over themselves)
# the clamps' sensors: on their rows now (the old Encoders page showed none - the manual pages did)
G.EXPECTED['ParamEncoders'] = G.EXPECTED['ParamPositioning'] = {'extra': {'M216', 'M217', 'M233', 'M234'}}
page('ParamEncoders', 'Parameters', 'ucEncodersParameters', 'Encoders',
    [EncRows('Encoders', [ENC_FRONT, ENC_TOP, ENC_RIGHT_TOP])])
page('ParamPositioning', 'Parameters', 'ucEncodersParameters', 'Positioning',
    [EncRows('Positioning', [PP_LEFT, PP_RIGHT])])

page('ParamEntry', 'Parameters', 'ucEntryParameters', 'Entry Parameters',
    [Panel('Entry', [('Name', 300), ('Value', NUM)], [Sec('Robot')] + n_rows([
        ('R. Movement Amount / Rotation', 'R. Tur Başına Hareket Miktarı', 'D678 32 /10000 3', None),
        ('Deceleration Time', 'Yavaşlama Süresi', 'D560 32 /1 0', 'ms'),
        ('Length Meas. Sensor Position', 'Boy Ölçüm Sensörü Pozisyonu', 'D828 32 /10000 1', None),
        ('Length Meas. Sensor - End Distance', 'Boy Ölçüm Sensörü - Uç Mesafesi', 'D834 32 /10000 1', None),
        ('Bar End Clamping Amount', 'Bar Ucu Sıkma Miktarı', 'D830 32 /10000 1', None),
        ('Prf. Recognition Liberation Position', 'Profil Tanıma Serbest Bırakma Pozisyonu', 'D508 32 /10000 1', None),
        ('Conveyor Extrication Position', 'Konveyör Çıkarma Pozisyonu', 'D504 32 /10000 1', None)]))])

page('ParamProcess', 'Parameters', 'ucProcessParameters', 'Process Parameters',
    [Panel('Process', [('Name', 300), ('Value', NUM)], [Sec('Milling')] + n_rows([
        ('Center Z Position', 'Merkez Z Pozisyonu', 'D802 32 /100 2', None),
        ('Center - Profile Y Distance', 'Merkez - Profil Y Mesafesi', 'D804 32 /100 2', None),
        ('Center - Profile Z Distance', 'Merkez - Profil Z Mesafesi', 'D806 32 /100 2', None),
        ('0° Y Offset', '0° Y Ofset', 'D812 32 /10000 2', None),
        ('0° Z Offset', '0° Z Ofset', 'D814 32 /10000 2', None),
        ('90° Y Offset', '90° Y Ofset', 'D816 32 /10000 2', None),
        ('90° Z Offset', '90° Z Ofset', 'D818 32 /10000 2', None),
        ('180° Y Offset', '180° Y Ofset', 'D820 32 /10000 2', None),
        ('180° Z Offset', '180° Z Ofset', 'D822 32 /10000 2', None),
        ('270° Y Offset', '270° Y Ofset', 'D824 32 /10000 2', None),
        ('270° Z Offset', '270° Z Ofset', 'D826 32 /10000 2', None),
        ('Milling Clamping Position', 'Frezeleme Sıkma Pozisyonu', 'D552 32 /10000 2', None),
        ('Clampless Speed Override', 'Sıkmasız Hız Oranı', 'D510 16 /1 0', '%')]) + [Sec('Cutting')] + n_rows([
        ('Saw Diameter', 'Testere Çapı', 'D524 32 /10000 1', None),
        ('Min. Cut Length to Conveyor', 'Konveyöre Min. Kesim Boyu', 'D556 32 /10000 1', None),
        ('Min. Waste Profile to Conveyor', 'Konveyöre Min. Atık Profil', 'D538 32 /10000 1', None)]) +
        [Sec('Distances Between Units')] + n_rows([
        ('Length Meas. Sensor - Milling', 'Boy Ölçüm Sensörü - Frezeleme', 'D475 32 /10000 2', None),
        ('Milling - Cutting Center', 'Frezeleme - Kesim Merkezi', 'D446 32 /100 2', None)]))])

page('ParamOther', 'Parameters', 'ucOtherParameters', 'Other Parameters',
    [Panel('Other', [('Name', 300), ('Value', NUM)], [Sec('Exit Robot')] + n_rows([
        ('Robot End Cut Center Position', 'Robot Uç Kesim Merkez Pozisyonu', 'D836 32 /10000 2', None),
        ('Right Clamp Clearing Position', 'Sağ Sıkma Boşaltma Pozisyonu', 'D682 32 /10000 1', None),
        ('Cut Piece Holding Position', 'Kesilen Parça Tutma Pozisyonu', 'D810 32 /10000 2', None),
        ('Short Piece Holding Position', 'Kısa Parça Tutma Pozisyonu', 'D808 32 /10000 2', None),
        ('Conveyor Liberation Position', 'Konveyör Bırakma Pozisyonu', 'D842 32 /10000 2', None),
        ('Prf. End Min. Clamping Amount', 'Profil Ucu Min. Sıkma Miktarı', 'D838 32 /10000 2', None),
        ('Cut Piece Releasing Position', 'Kesilen Parça Bırakma Pozisyonu', 'D542 32 /10000 1', None),
        ('Waste Profile Offset', 'Atık Profil Ofseti', 'D544 32 /10000 1', None)]) + [Sec('Exit Conveyor')] + n_rows([
        ('Conveyor Length', 'Konveyör Uzunluğu', 'D498 32 /10000 1', None),
        ('Discharging Plate Height', 'Boşaltma Plakası Yüksekliği', 'D549 16 /10 1', None)]) + [Sec('Robot Height')] + n_rows([
        ('Mounted Bolt Number', 'Takılı Cıvata Sayısı', 'D844 16 /1 0', ''),
        ('Entry Robot Bolt Positioning Distance', 'Giriş Robotu Cıvata Konumlama Mesafesi', 'D845 16 /10 1', None),
        ('Entry Support Reference Position', 'Giriş Destek Referans Pozisyonu', 'D868 16 /1 1', None),
        ('Exit Robot Bolt Positioning Distance', 'Çıkış Robotu Cıvata Konumlama Mesafesi', 'D846 16 /10 1', None),
        ('Exit Support Reference Position', 'Çıkış Destek Referans Pozisyonu', 'D869 16 /1 1', None)])),
     Panel('BoltHeights', [('Robot Bolt Heights', 120), ('Value', NUM)],
        [Row(str(i), N('D%d 16 /100 2' % (847 + i)), key='Bolt%d' % i) for i in range(1, 13)], title='Robot Bolt Heights'),
     Panel('Options', [('Machine Options', 300), ('On', 70)], [
        Row(t('Exit Conveyor End Closed', 'Çıkış Konveyörü Sonu Kapalı'), S('L22')),
        Row(t('Shorten Leftover Profile', 'Artık Profili Kısalt'), S('L41')),
        Row(t('Clampless Short Profile Unload', 'Sıkmasız Kısa Profil Boşaltma'), S('L44')),
        Row(t('Cutting Right Top Clamping Long Profile', 'Kesim Sağ Üst Sıkma Uzun Profil'), S('L28')),
        Row(t('Top Clamping Reclamp', 'Üst Sıkma Yeniden Sıkma'), S('L30'))], title='Machine Options')])

# The old page's left table is made by its code (ucRobotAxisCalibration.RenderDynamicControls): 199 rows of the robot's
# position (R31200 ...) and its error at it (R31600 ...), every second word, raw / 100.
ROBOT_ROWS = 199
G.CODE_DEVICES['ucRobotAxisCalibration'] = set()
def robot_table(first, last):
    return Panel('Table%d' % first, [('No', 60), ('Position', NUM + 16), ('Error Quantity', NUM + 16)],
        [Row(str(i), N('R%d 32 /100 4' % (31200 + 2 * (i - 1))), N('R%d 32 /100 4' % (31600 + 2 * (i - 1))), key='Point%d' % i)
         for i in range(first, last + 1)])

# (the measuring above, the table in three columns under it - 67, 66, 66 points)
page('ParamRobotAxisCalibration', 'Parameters', 'ucRobotAxisCalibration', 'Robot Axis Calibration',
    [Panel('Measuring', [('Name', 260), ('Value', NUM), ('', NUM)], [
        Row(t('Target Position', 'Hedef Pozisyon'), N('D38 32 /100 2'), B('M23!Set', 'Calc!')),
        Row(t('Go to Position', 'Pozisyona Git'), B('M25!Set', 'GO!'), B('M26!Set', 'GO!')),
        Row('', V('D40 32 /10 4'), V('D42 32 /10 4'), key='GoTo'),
        Row(t('Down Reference', 'Alt Referans'), V('D44 32 /10 4'), V('D46 32 /10 4')),
        Row(t('Up Reference', 'Üst Referans'), None, V('D48 32 /10 4')),
        Row(t('Target Fixed Quantity', 'Hedef Sabit Miktar'), V('D58 32 /10 4'), None),
        Row(t('Pos. Auto. During Measuring', 'Ölçüm Sırasında Otomatik Pozisyon'), S('L7'), None),
        Row(t('Appoint Target Pos. During Measuring', 'Ölçüm Sırasında Hedef Pozisyon Ata'), S('L8'), None),
        Row(t('Robot Measuring Active', 'Robot Ölçüm Aktif'), S('L9'), None)])],
    [robot_table(1, 67), robot_table(68, 133), robot_table(134, ROBOT_ROWS)])
# the table's words are new to the check (the old designer has none - its code makes them)
G.EXPECTED['ParamRobotAxisCalibration'] = {'extra': {'R%d' % (31200 + 2 * i) for i in range(ROBOT_ROWS)} |
                                                    {'R%d' % (31600 + 2 * i) for i in range(ROBOT_ROWS)}}

# ----------------------------------------------------------------- HOME ----
# The old Home is the job preparation (the shared ucJobPreparations of the old program: the job list and the profile library
# - not brought over yet). Here: the axes' positions and the machine's state lamps.
page('HomeOverview', 'Home', 'ucHome', 'Home',
    [Panel('Positions', [('Axis', CAP), ('Position', NUM + 10)], [
        Row(t(a['caption'], G.TRANSLATIONS[a['key']][1]), V('%s 32 /%d 2' % (a['pos'], a['div']), a['unit']), key='Pos' + str(n))
        for n, a in sorted(G.AXES.items())]),
     Panel('State', [('Name', CAP), ('State', 70)], [
        Row(t('Emergency Stop', 'Acil Stop'), Lp('M200')),
        Row(t('Air Pressure', 'Hava Basıncı'), Lp('M201'))])],
    level='Operator')
G.EXPECTED['HomeOverview'] = {'extra': {a['pos'] for a in G.AXES.values()} | {'M200', 'M201'}}

# the machine's name, its tabs (SB3015Module) and the manual pages' bar's buttons (Screens/Shared/JogBar, by hand)
G.TRANSLATIONS.update({
    'Machine_SB3015': ('Profile Machining Centre', 'Profil İşleme Merkezi'),
    'Tab_SB3015_ManualEntryConveyor': ('Entry Conveyor', 'Giriş Konveyörü'), 'Tab_SB3015_ManualRobot': ('Robot', 'Robot'),
    'Tab_SB3015_ManualProfileRecognition': ('Profile Recognition', 'Profil Tanıma'), 'Tab_SB3015_ManualMilling': ('Milling', 'Frezeleme'),
    'Tab_SB3015_ManualCutting': ('Cutting', 'Kesim'), 'Tab_SB3015_ManualExit': ('Exit', 'Çıkış'), 'Tab_SB3015_ManualOther': ('Other', 'Diğer'),
    'Tab_SB3015_ParamAxisSettings': ('Axis Settings', 'Eksen Ayarları'), 'Tab_SB3015_ParamAxisSpeeds': ('Axis Speeds', 'Eksen Hızları'),
    'Tab_SB3015_ParamEncoders': ('Encoders', 'Enkoderler'), 'Tab_SB3015_ParamPositioning': ('Positioning', 'Konumlama'),
    'Tab_SB3015_ParamEntry': ('Entry', 'Giriş'),
    'Tab_SB3015_ParamProcess': ('Process', 'İşlem'), 'Tab_SB3015_ParamOther': ('Other', 'Diğer'),
    'Tab_SB3015_ParamRobotAxisCalibration': ('Robot Axis Calibration', 'Robot Ekseni Kalibrasyonu'),
    'JOG': ('JOG', 'JOG'), '10 mm': ('10 mm', '10 mm'), '1 mm': ('1 mm', '1 mm'), '0.1 mm': ('0.1 mm', '0.1 mm'),
    '0.01 mm': ('0.01 mm', '0.01 mm'), 'Reset': ('Reset', 'Reset'), 'Security ByPass': ('Security ByPass', 'Güvenlik Bypass')})
G.TRANSLATIONS.update({
    'Send All to Reference': ('Send All to Reference', 'Tümünü Referansa Gönder'), 'Calc!': ('Calc!', 'Hesapla!'), 'GO!': ('GO!', 'GİT!'),
    'Height': ('Height', 'Yükseklik'), 'No': ('No', 'No'), 'On': ('On', 'Açık'), 'State': ('State', 'Durum')})

if __name__ == '__main__':
    G.main()
