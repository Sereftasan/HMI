#!/usr/bin/env python3
"""
A machine's pages made from the old program's TABLE pages (11 November: MD3020 from
the old _MD3030 project; MK2010 from _MK2010). An old page is a table: a column a
kind of thing (Back, Axis Position, Forward, Actual Torque...), a row an axis or a
unit, the rows in sections (General, Head 1 ... Head 4). The new page is made the
same way - the same rows, columns, sections and devices, each cell one of the
program's own controls:

  B  a button (ButtonHMI): 'M45/W0.0' - its command and its lamp; 'M30!Set,M31!Set'
     several commands; '<' / '>' an arrow (Back / Forward), 'v' / '^' (Down / Up),
     instead of a text
  V  a value shown (NumericDisplayHMI): 'D20 32 /10000 2' - address, bits,
     divisor, decimals
  N  a value typed in (NumericInputHMI), the same way; lo / hi its limits - a number,
     or a device holding the raw limit ('D144')
  Lp a lamp (LampHMI); inv=True lit while the bit is OFF (the old KabanLambNegative)
  P  a servo's load or torque (MeterHMI, %)
  S  an option bit (CheckBoxHMI)
  Spd a speed in % on a slider (SpeedOverrideHMI)
  Ax  an axis on a manual page (AxisUnitHMI): its number on the axis settings page -
      its buttons, position, reference sensor and error come from that row
  Cy  a cylinder (CylinderUnitHMI): its two commands and lamps, its valve and feedback
      as the drawings show them, its error bits from the alarm list (12 November: "change
      ButtonHMI controls (and the related LampHMI controls as the feedback and LabelHMI
      controls as the description text ...) to cylinder unit HMI controls ... so we can
      create the alarm helpers accordingly"); 'M104!Rst' - a toggle bit reset (its other
      side 'M104!Set')
  AxisRows  the axis settings page's rows (AxisSettingHMI), one AR per axis, in sections

A machine's tool (tools/legacy_md3030_pages.py, tools/legacy_mk2010_pages.py) calls
setup() with its project and its old pages' folder, writes its PAGES with these
pieces and calls main(): --check compares the spec with the old designers - every
device a visible control of an old page uses must be there and nothing more (except
what EXPECTED lists), the buttons' actions and the values' divisors as the old
controls'; without it the pages are written, keeping what was set on a control by
hand (found again by its name).

How the old controls divide (KabanDevBase, read 11 November):
  KabanLedLabel       raw / (Divisor * 10), FloatingPoint decimals
  KabanNumericButton  raw / Divisor, Mask F<n> / n<n> decimals
  KabanProgressBar    |raw / Divisor| on a 0 - 100 bar (DirectShow)
"""
import os, re, sys
sys.dont_write_bytecode = True
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import legacy_fa2010_pages as L
from legacy_fa2010_pages import cs, dnum, ident

SCREENS = None          # the machine's Screens folder (setup)

MACHINE = None          # the machine's project folder (setup)

def setup(project, old, tool, made):
    """project: 'Kaban.Machines.MD3020'; old: the old pages' folder under tools/legacy ('MD3030')."""
    global SCREENS, MACHINE
    machine = os.path.join(L.ROOT, project)
    MACHINE = machine
    SCREENS = os.path.join(machine, 'Screens')
    L.MANUAL = os.path.join(SCREENS, 'Manual')
    L.PARAMS = os.path.join(SCREENS, 'Parameters')
    L.OLD = os.path.join(L.ROOT, 'tools', 'legacy', old)
    L.NS = project + '.Screens.'
    L.TOOL = tool
    L.OLD_REL = 'tools/legacy/%s/' % old
    L.MADE = made

L.TYPES['MeterHMI'] = 'Kaban.UI.Controls.MeterHMI'
L.OWNED['MeterHMI'] = {'Name', 'PlcNumber', 'Address', 'Divisor', 'DecimalPlaces', 'Suffix', 'ScaleMinimum', 'ScaleMaximum',
                       'Signed', 'Location', 'Size', 'TabIndex'}
L.OWNED['ButtonHMI'] = L.OWNED['ButtonHMI'] | {'Direction'}
L.OWNED['NumericInputHMI'] = L.OWNED['NumericInputHMI'] | {'MinLimit.Type', 'MinLimit.StaticValue', 'MinLimit.TagAddress',
                                                           'MaxLimit.Type', 'MaxLimit.StaticValue', 'MaxLimit.TagAddress'}
L.OWNED['LampHMI'] = L.OWNED['LampHMI'] | {'OnColor', 'OffColor'}
L.OWNED['ButtonHMI'] = L.OWNED['ButtonHMI'] | {'ConfirmationNeeded'}
L.OWNED['InverterUnitHMI'] = L.OWNED['InverterUnitHMI'] | {'RequiredSecurity', 'FaultCodeAddress', 'ErrorAddress', 'RunLampAddress'}
L.TYPES['SpeedOverrideHMI'] = 'Kaban.UI.Controls.SpeedOverrideHMI'
L.OWNED['SpeedOverrideHMI'] = {'Name', 'PlcNumber', 'Address', 'BackColor', 'FallbackText', 'TranslationKey', 'Location', 'Size', 'LogAction', 'TabIndex'}
L.OWNED['LabelHMI'] = {'Name', 'TranslationKey', 'Text', 'Font', 'ForeColor', 'TextAlign', 'Location', 'Size', 'BackColor'}
L.OWNED['CylinderUnitHMI'] = L.OWNED['CylinderUnitHMI'] | {'Arrows', 'ButtonATextKey', 'ValveType', 'FeedbackType', 'ErrorAAddress', 'ErrorBAddress',
                                                           'RequiredSecurity', 'OutputAAddress', 'OutputBAddress'}
# (17 November, SB3015) the cylinders with an encoder and the pneumatic positionings: their error bits, valve, feedback and
# security are the spec's too (FA2010's tool took them from the manual page's unit)
for _t in ('CylinderEncoderSettingHMI', 'PneumaticPositioningHMI'):
    L.OWNED[_t] = L.OWNED[_t] | {'ErrorBackAddress', 'ErrorForwardAddress', 'ValveType', 'FeedbackType', 'RequiredSecurity'}
L.TYPES['AxisSettingHMI'] = 'Kaban.UI.Controls.AxisSettingHMI'
L.OWNED['AxisSettingHMI'] = {'Name', 'PlcNumber', 'AxisNameKey', 'HMIAxisIndex', 'MotionNumber', 'MotionAxisIndex', 'CommandBackAddress',
                             'CommandForwardAddress', 'PositionAddress', 'PositionDataType', 'MinLimitAddress', 'MaxLimitAddress',
                             'CalibrationAddress', 'CalibrateAddress', 'ZDistanceAddress', 'ZDistanceDivisor', 'ZDistanceFullScale',
                             'ReferenceSensorAddress', 'ReferenceSensorSide', 'ErrorAddress', 'Divisor', 'DecimalPlaces', 'Digits',
                             'SettingDataType', 'Suffix', 'BackColor', 'Location', 'Size', 'TabIndex', 'RequiredSecurity', 'ReferenceAddress'}

# ============================================================================
#  THE SPECIFICATION'S PIECES
# ============================================================================

def B(spec, text=None, span=1, note=None, confirm=False):
    """A button: 'M45/W0.0' (command / lamp), 'M100!Set', 'M30!Set,M31!Set' (several commands, the
    lamp after the first); '<' '>' 'v' '^' as the text: an arrow."""
    parts = [L.side(x) for x in spec.split(',')]
    s = parts[0]
    arrow = {'<': 'Left', '>': 'Right', 'v': 'Down', '^': 'Up'}.get(text)
    return dict(kind='btn', cmd=s['cmd'], lamp=s['lamp'], action=s['action'], text=None if arrow else text,
                more=[(x['cmd'], x['action']) for x in parts[1:]], arrow=arrow, span=span, note=note, confirm=confirm)

def V(spec, unit='mm', span=1, note=None):
    return dict(kind='disp', val=L.value(spec), unit=unit, span=span, note=note)

def N(spec, unit='mm', lo=None, hi=None, span=1, note=None):
    return dict(kind='entry', val=L.value(spec), unit=unit, lo=lo, hi=hi, span=span, note=note)

def Lp(addr, span=1, note=None, inv=False):
    """A lamp; inv: lit while the bit is OFF (the old KabanLambNegative - a limit switch's normally closed contact)."""
    return dict(kind='lamp', addr=addr, span=span, note=note, inv=inv)

def P(addr, span=1, note=None):
    return dict(kind='meter', addr=addr, span=span, note=note)

def Spd(addr, span=1, note=None):
    """A speed in % set with a slider (SpeedOverrideHMI): the old page's track bar."""
    return dict(kind='speed', addr=addr, span=span, note=note)

def S(addr, span=1, note=None):
    return dict(kind='opt', addr=addr, span=span, note=note)

def Ax(number, span=1, note=None):
    """An axis on a manual page (AxisUnitHMI): its number (HMIAxisIndex) on the axis settings page, whose row gives it the rest."""
    return dict(kind='axis', number=number, span=span, note=note)

L.ACTION['Reset'] = 'RESET'

def _side(spec):
    """L.side, and '!Rst' - a toggle bit's two sides as two buttons: Set on one, Reset on the other (MK2010)."""
    if spec and spec.endswith('!Rst'):
        d = L.side(spec[:-4])
        d['action'] = 'Reset'
        return d
    return L.side(spec)

def Cy(title, a=None, b=None, lampA=None, lampB=None, valve=None, feedback=None, errA=None, errB=None, pos=None, arrows=None,
       text=None, span=1, note=None, outA=None, outB=None):
    """A cylinder (CylinderUnitHMI): title - its name's key; a / b - the left and right buttons' commands ('M201', momentary;
    'M200!Alt'); lampA / lampB - the sensor on each; valve / feedback - CylinderValve / CylinderFeedback names; errA / errB -
    the error bits of each movement; pos - a position shown under it ('SD4500 32 /100 2'); arrows - 'DownUp' (A down, B up),
    'LeftRight' (the default); text - a one-button unit's caption."""
    sa = _side(a) if a else None
    sb = _side(b) if b else None
    return dict(kind='cyl', title=title, a=sa, b=sb, lampA=lampA, lampB=lampB, valve=valve, feedback=feedback, errA=errA, errB=errB,
                pos=L.value(pos) if pos else None, arrows=arrows, text=text, span=span, note=note, outA=outA, outB=outB)

def Iv(title, fault=None, a=None, b=None, run=None, val=None, error=None, text='Run', span=1, note=None):
    """An inverter's motor (InverterUnitHMI): title - its name's key; fault - the inverter's fault code word (its alarm and
    step-by-step help come from it); a / b - the buttons ('M636!Alt', 'M640', 'M639/M206' with a lamp; a one-button motor
    has A, captioned text); run - its run lamp; val - a value beside the buttons ('D18 16 /10 2'); error - the PLC's alarm bit
    for the inverter."""
    return dict(kind='inv', title=title, fault=fault, a=_side(a) if a else None, b=_side(b) if b else None, run=run,
                val=L.value(val) if val else None, error=error, text=text, span=span, note=note)

def Enc(number, title, back, fwd, pos, ref=None, tol=None, valve=None, feedback=None, errA=None, errB=None, setdiv=(10, 1), note=None):
    """A cylinder with an encoder (17 November, SB3015 - FA2010's CylinderEncoderSettingHMI): a row of an EncRows panel - its number,
    its movement ('M646/M217': command / sensor, back and forward), position ('D4 16 /10 1') and settings: ref - the reference
    (the old pages' calibration position), tol - the measuring tolerance; errA / errB its movements' error bits; setdiv - the
    settings' divisor and decimals. A manual page shows it as EncU(number)."""
    # (FA2010's tool's L.Enc registers FA2010's own encoders as it is imported: the machine's are kept here)
    it = dict(kind='enc', number=number, title=title, a=L.side(back), b=L.side(fwd), pos=L.value(pos), ref=ref, tol=tol, start=None,
              unclamp=None, note=note, valve=valve, feedback=feedback, errA=errA, errB=errB, setdiv=setdiv)
    assert number not in ENCODERS, number
    ENCODERS[number] = it
    return it

ENCODERS = {}                        # the machine's: number -> its Enc row (an EncU finds it)

def PPos(title, back, fwd, pos, target, send, cal, calibrate, tol, offset, valve=None, feedback=None, errA=None, errB=None, note=None):
    """A cylinder positioned by its analogue sensor (FA2010's PneumaticPositioningHMI): a row of an EncRows panel - its movement,
    position (raw / 100), target position and its send bit, calibration and its calibrate bit, tolerance, direction reference."""
    it = L.Mould(title, back, fwd, pos, target, send, cal, calibrate, tol, offset, valve=valve, feedback=feedback)
    it.update(errA=errA, errB=errB, note=note)
    return it

def EncRows(key, items, title=None):
    """A table of Enc rows or of PPos rows (one kind - each kind's rows put their own headings over their columns): the rows
    lay their columns out themselves, as an axis settings page's."""
    kind = items[0]['kind']
    assert all(i['kind'] == kind for i in items), key
    sections = L.ENC_SECTIONS if kind == 'enc' else L.PP_SECTIONS
    # an encoder column no row has: no heading over it (the movement's always)
    used = {'Manual'} | {k for k, f in (('Reference', 'ref'), ('Tolerance', 'tol'), ('MinimumLimit', 'start'), ('Unclamping', 'unclamp'))
                         if any(i.get(f) for i in items)}
    if kind == 'enc': sections = [x for x in sections if x[0] in used]
    namew = L.pp_name_width([dict(title=L_text(i['title'])) for i in items])
    w = namew + L.PP_GAP + L.PP_GROUP_GAP + sum(sw for _, _, sw in sections) + L.PP_GROUP_GAP * (len(sections) - 1)
    return dict(key=key, rows=items, title=title, encrows=kind, cols=[('', w)], namew=namew, sections=sections)

def L_text(key):
    """A title key's English (its width on the page)."""
    return TRANSLATIONS.get(key, (key,))[0]

def EncU(number, span=1, note=None):
    """A cylinder with an encoder on a manual page (CylinderEncoderUnitHMI): its number - its row (Enc) gives it the rest."""
    return dict(kind='encunit', number=number, span=span, note=note)

def Row(caption, *cells, **kw):
    """A row: its caption, its cells (None: an empty one); key: the names' part when the caption is empty; h: its height
    (a unit's row: UNIT_ROW_H)."""
    return dict(kind='row', caption=caption, cells=list(cells), key=kw.get('key'), h=kw.get('h'))

def AR(axis):
    """An axis settings row (AxisSettingHMI) - axis: a dict of its number, name key, motion module and axis, devices (AXIS_FIELDS)."""
    return dict(kind='axrow', axis=axis)

def AxisRows(key, rows, title=None, headings=None):
    """The axis settings page's rows: AR(...) and Sec(...) - the headings over them (AxisSettingHMI.AlignHeaders puts each
    over its column), a row an axis; headings - the page's own (AXIS_HEADINGS' form) when its rows have more columns."""
    return dict(key=key, rows=rows, title=title, axisrows=True, cols=[('Axis', AXIS_ROW_W)], headings=headings)

def Sec(title):
    return dict(kind='sec', title=title)

def Panel(key, cols, rows, title=None):
    """A table: cols [(heading, width)] - the first the rows' captions -, its rows and sections."""
    return dict(key=key, cols=cols, rows=rows, title=title)

PAGES = []

def page(cls, folder, old, caption, *bands, **kw):
    """A page: its class, folder (Manual / Parameters / Home), the old page(s) it comes from, BANDS of panels;
    level=: who changes its values (default Maintenance on Parameters, else Operator)."""
    p = dict(cls=cls, folder=folder, old=old if isinstance(old, list) else [old], caption=caption, bands=list(bands))
    p.update(kw)
    PAGES.append(p)
    return p



# THE MACHINE'S AXES (its tool fills it): HMI axis number -> dict(caption, key (its name's translation key), back, fwd, pos,
# ref / side (its reference sensor and the button it is lit on), min, max, cal (the calibration value), calibrate, zdist,
# err (its servo's error bit), motion (module, axis on it)). An Ax cell and an AR row are made from it.
AXES = {}
AXIS_FIELDS = [('CommandBackAddress', 'back'), ('CommandForwardAddress', 'fwd'), ('PositionAddress', 'pos'),
               ('MinLimitAddress', 'min'), ('MaxLimitAddress', 'max'), ('CalibrationAddress', 'cal'), ('CalibrateAddress', 'calibrate'),
               ('ZDistanceAddress', 'zdist'), ('ReferenceSensorAddress', 'ref'), ('ErrorAddress', 'err'), ('ReferenceAddress', 'home'),
               # (17 November, SB3015's Servo Status and Axis Parameters) send to reference, the load, the reference request,
               # the motion module's axis error / warning and the servo's alarm codes
               ('GoToReferenceAddress', 'goref'), ('CurrentAddress', 'load'), ('HomeRequestAddress', 'homereq'),
               ('AxisAlarmAddress', 'axerr'), ('AxisWarningAddress', 'warn'), ('ServoAlarmAddress', 'servoerr')]
# the axis settings page's headings (AxisSettingHMI.Headings' keys and the old page's words)
# (key, text, x, width - where the designer shows them; AlignHeaders moves each over its column)
AXIS_HEADINGS = [('Axis', 'Axis', 0, 132), ('Position', 'Position', 208, 120), ('Min', 'Min', 484, 86), ('Max', 'Max', 574, 86),
                 ('Reference', 'Reference', 664, 86), ('Calibration', 'Calibration', 754, 86), ('Cal', 'Cal', 844, 44),
                 ('ZDistance', 'Z distance', 892, 76)]

# THE MACHINE'S OWN TEXTS (its tool fills them): key -> (EN, TR), added to its Translations.csv when missing
TRANSLATIONS = {}
# ITS ALARM LIST (its tool fills it): [(address, tag, EN, TR)] -> Alarms.json and its texts (Alarm_<machine>.<tag>)
ALARMS = []

# What --check accepts as deliberate differences from the old designers: page class -> {'missing': .., 'extra': ..}
EXPECTED = {}
# devices an old page's code reads and writes (toggle switches): old page -> set
CODE_DEVICES = {}


# ============================================================================
#  LAYOUT
# ============================================================================
X0, Y0 = 16, 16
ROW_H, CELL_H, SEC_H, COLHEAD_H, GAP_X, PANEL_GAP, BAND_GAP, TITLE_H = 40, 34, 30, 30, 6, 32, 24, 30

UNIT_ROW_H, UNIT_H = 72, 64          # a row of units (AxisUnitHMI, CylinderUnitHMI) and the unit in it
AXIS_ROW_W, AXIS_ROW_GAP = 1000, 4   # an axis settings row (it lays its columns out itself) and the space under it

def row_h(r):
    if r['kind'] == 'sec': return SEC_H
    if r['kind'] == 'axrow': return 40 + AXIS_ROW_GAP
    return r.get('h') or ROW_H

def panel_width(p):
    return sum(w for _, w in p['cols']) + GAP_X * (len(p['cols']) - 1)

def panel_height(p):
    if p.get('encrows'):
        return (TITLE_H if p.get('title') else 0) + L.PP_HEAD_H + 6 + L.PP_PITCH * len(p['rows'])
    h = (TITLE_H if p.get('title') else 0) + COLHEAD_H + 4 + (COLHEAD_H - 6 if grouped(p) else 0)
    for r in p['rows']:
        h += row_h(r)
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
    pre = {'btn': 'btn', 'disp': 'disp', 'entry': 'num', 'lamp': 'lamp', 'meter': 'load', 'opt': 'opt', 'speed': 'spd', 'axis': 'ax',
           'cyl': 'unit', 'inv': 'unit', 'encunit': 'unit'}[kind]
    parts = [p['key']] + ([sec] if sec else []) + [row, p['cols'][ci][0].replace('|', ' ')]
    return pre + '_'.join(ident(x) for x in parts if x)

class Grid:
    def __init__(self, pg, current):
        self.pg = pg
        shown = dict(pg, old=pg['old'][0])           # the Emitter's header names one old page
        self.em = L.Emitter(shown, current)
        self.current = current

    def level(self):
        return self.pg.get('level') or ('Maintenance' if self.pg['folder'] == 'Parameters' else 'Operator')

    def old(self, name):
        b = self.current.get(name)
        return name if b and b['page'] == self.pg['cls'] else None

    def label(self, text, x, y, w, h, small=False, align='MiddleLeft', bold=True, prefix='lbl'):
        n = self.em.unique(prefix + ident(text)[:40])
        self.em.heading(n, text, x, y, w, h, small=small, align=align)
        if not bold:
            self.em.body.append('            this.%s.Font = new System.Drawing.Font("Segoe UI", 9.5F);' % n)
        return n

    def cell(self, name, c, x, y, w, caption, h=ROW_H):
        em, k = self.em, c['kind']
        name = em.unique(name)
        old = self.old(name)
        cy = y + (h - CELL_H) // 2
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
            for cmd, act in c.get('more', ()):
                b = em.bit()
                em.body.append('            %s.Address = %s;' % (b, cs(cmd)))
                em.body.append('            %s.Action = Kaban.Core.Enums.BitActionType.%s;' % (b, L.ACTION[act]))
                em.body.append('            this.%s.BitActions.Add(%s);' % (name, b))
            if c['lamp']: em.p(name, 'Lamp1Address', cs(c['lamp']))
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, cy))
            em.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, CELL_H))
            em.p(name, 'LogAction', 'true')
            if c.get('confirm'): em.p(name, 'ConfirmationNeeded', 'true')
            em.kept(name, 'ButtonHMI', old)
            em.security(name, 'ButtonHMI', old, self.level())
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
                if isinstance(val, str):          # a device holding the raw limit
                    em.p(name, lim + '.Type', 'Kaban.Core.Enums.LimitSourceType.Tag')
                    em.p(name, lim + '.TagAddress', cs(val))
                elif val is not None:
                    em.p(name, lim + '.Type', 'Kaban.Core.Enums.LimitSourceType.Static')
                    em.p(name, lim + '.StaticValue', dnum(val))
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, cy + 1))
            em.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, CELL_H - 2))
            em.kept(name, 'NumericInputHMI', old)
            em.security(name, 'NumericInputHMI', old, self.level())
            em.add(name)
        elif k == 'lamp':
            em.decl(name, 'LampHMI')
            em.header(name, c.get('note'))
            em.p(name, 'Name', cs(name))
            em.p(name, 'PlcNumber', '1')
            em.p(name, 'Address', cs(c['addr']))
            if c.get('inv'):
                # lit while the bit is off: off grey when on, green when off
                em.p(name, 'OnColor', 'System.Drawing.Color.FromArgb(((int)(((byte)(225)))), ((int)(((byte)(225)))), ((int)(((byte)(225)))))')
                em.p(name, 'OffColor', 'System.Drawing.Color.FromArgb(((int)(((byte)(76)))), ((int)(((byte)(175)))), ((int)(((byte)(80)))))')
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x + (w - 30) // 2, y + (h - 30) // 2))
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
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y + (h - 26) // 2))
            em.p(name, 'Size', 'new System.Drawing.Size(%d, 26)' % w)
            em.kept(name, 'MeterHMI', old)
            em.add(name)
        elif k == 'speed':
            em.decl(name, 'SpeedOverrideHMI')
            em.header(name, c.get('note'))
            em.p(name, 'Name', cs(name))
            em.p(name, 'PlcNumber', '1')
            em.p(name, 'Address', cs(c['addr']))
            em.p(name, 'BackColor', 'System.Drawing.Color.White')
            em.p(name, 'FallbackText', '"Speed"')
            em.p(name, 'TranslationKey', '"Lbl_Speed"')
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, cy))
            em.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, CELL_H + 2))
            em.p(name, 'LogAction', 'true')
            em.kept(name, 'SpeedOverrideHMI', old)
            em.security(name, 'SpeedOverrideHMI', old, self.level())
            em.add(name)
        elif k == 'axis':
            a = AXES.get(c['number'], {})
            em.decl(name, 'AxisUnitHMI')
            em.header(name, c.get('note') or '%s: axis %d of the axis settings page (its buttons, position, reference sensor and error come from there).'
                      % (a.get('caption', caption), c['number']))
            em.p(name, 'BackColor', 'System.Drawing.Color.White')
            em.p(name, 'Name', cs(name))
            em.p(name, 'AxisNumber', str(c['number']))
            em.p(name, 'PlcNumber', '1')
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y + (h - UNIT_H) // 2))
            em.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, UNIT_H))
            em.kept(name, 'AxisUnitHMI', old)
            em.add(name)
        elif k == 'cyl':
            em.decl(name, 'CylinderUnitHMI')
            em.header(name, c.get('note'))
            em.p(name, 'BackColor', 'System.Drawing.Color.White')
            em.p(name, 'Name', cs(name))
            em.p(name, 'PlcNumber', '1')
            em.p(name, 'TitleTextKey', cs(c['title']))
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y + (h - UNIT_H) // 2))
            em.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, UNIT_H))
            em.p(name, 'LogAction', 'true')
            if c.get('text'):
                em.p(name, 'Arrows', 'Kaban.Core.Enums.ButtonArrowStyle.Custom')
                em.p(name, 'ButtonATextKey', cs(c['text']))
            elif c.get('arrows'):
                em.p(name, 'Arrows', 'Kaban.Core.Enums.ButtonArrowStyle.' + c['arrows'])
            for sd, k2, lamp in ((c['a'], 'A', c.get('lampA')), (c['b'], 'B', c.get('lampB'))):
                if sd:
                    em.p(name, 'Command%sAddress' % k2, cs(sd['cmd']))
                    em.p(name, 'Command%sAction' % k2, 'Kaban.Core.Enums.BitActionType.' + L.ACTION[sd['action']])
                if lamp:
                    em.p(name, 'Lamp1%sAddress' % k2, cs(lamp))
            v = c.get('pos')
            if v:
                em.p(name, 'PositionAddress', cs(v['addr']))
                em.p(name, 'PositionDataType', 'Kaban.Core.Enums.DataTypeEnum.Bin_%d' % v['bits'])
                em.p(name, 'PositionDivisor', dnum(v['div']))
                em.p(name, 'PositionDecimalPlaces', str(v['dec']))
                em.p(name, 'PositionDigits', '7')
                em.p(name, 'PositionSuffix', '"mm"')
                em.p(name, 'PositionUnitType', 'Kaban.Core.Enums.UnitType.Length')
            if c.get('valve'): em.p(name, 'ValveType', 'Kaban.UI.Controls.CylinderValve.' + c['valve'])
            if c.get('feedback'): em.p(name, 'FeedbackType', 'Kaban.UI.Controls.CylinderFeedback.' + c['feedback'])
            # the valve's coils (the step-by-step help's outputs): the PLC's outputs that drive them
            if c.get('outA'): em.p(name, 'OutputAAddress', cs(c['outA']))
            if c.get('outB'): em.p(name, 'OutputBAddress', cs(c['outB']))
            if c.get('errA'): em.p(name, 'ErrorAAddress', cs(c['errA']))
            if c.get('errB'): em.p(name, 'ErrorBAddress', cs(c['errB']))
            em.kept(name, 'CylinderUnitHMI', old)
            em.p(name, 'RequiredSecurity', 'Kaban.Core.Enums.SecurityLevel.' + self.level())
            em.add(name)
        elif k == 'inv':
            em.decl(name, 'InverterUnitHMI')
            em.header(name, c.get('note') or '%s: an inverter\'s motor - its fault code %s gives its alarm and help.' % (caption, c.get('fault') or 'none'))
            em.p(name, 'BackColor', 'System.Drawing.Color.White')
            em.p(name, 'Name', cs(name))
            em.p(name, 'PlcNumber', '1')
            em.p(name, 'TitleTextKey', cs(c['title']))
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y + (h - UNIT_H) // 2))
            em.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, UNIT_H))
            em.p(name, 'LogAction', 'true')
            if not c.get('b'):
                em.p(name, 'Arrows', 'Kaban.Core.Enums.ButtonArrowStyle.Custom')
                em.p(name, 'ButtonATextKey', cs(c['text']))
            for sd, k2 in ((c.get('a'), 'A'), (c.get('b'), 'B')):
                if not sd: continue
                em.p(name, 'Command%sAddress' % k2, cs(sd['cmd']))
                em.p(name, 'Command%sAction' % k2, 'Kaban.Core.Enums.BitActionType.' + L.ACTION[sd['action']])
                if sd.get('lamp') and sd['lamp'] != sd['cmd']: em.p(name, 'Lamp1%sAddress' % k2, cs(sd['lamp']))
            if c.get('fault'): em.p(name, 'FaultCodeAddress', cs(c['fault']))
            if c.get('error'): em.p(name, 'ErrorAddress', cs(c['error']))
            if c.get('run'): em.p(name, 'RunLampAddress', cs(c['run']))
            v = c.get('val')
            if v:
                em.p(name, 'PositionAddress', cs(v['addr']))
                em.p(name, 'PositionDataType', 'Kaban.Core.Enums.DataTypeEnum.Bin_%d' % v['bits'])
                em.p(name, 'PositionDivisor', dnum(v['div']))
                em.p(name, 'PositionDecimalPlaces', str(v['dec']))
                em.p(name, 'PositionDigits', '6')
                em.p(name, 'PositionTipKey', '""')
            em.kept(name, 'InverterUnitHMI', old)
            em.p(name, 'RequiredSecurity', 'Kaban.Core.Enums.SecurityLevel.' + self.level())
            em.add(name)
        elif k == 'encunit':
            em.decl(name, 'CylinderEncoderUnitHMI')
            em.header(name, c.get('note') or '%s: encoder %d of the Encoders page - its buttons, sensors, position, valve, feedback and '
                      'error bits come from its row there.' % (caption, c['number']))
            em.p(name, 'BackColor', 'System.Drawing.Color.White')
            em.p(name, 'Name', cs(name))
            em.p(name, 'EncoderNumber', str(c['number']))
            em.p(name, 'PlcNumber', '1')
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y + (h - UNIT_H) // 2))
            em.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, UNIT_H))
            em.kept(name, 'CylinderEncoderUnitHMI', old)
            em.add(name)
        elif k == 'opt':
            em.decl(name, 'CheckBoxHMI')
            em.header(name + '  (%s)' % c['addr'], c.get('note'))
            em.p(name, 'Name', cs(name))
            em.p(name, 'PlcNumber', '1')
            em.p(name, 'Address', cs(c['addr']))
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x + (w - 44) // 2, y + (h - 32) // 2))
            em.p(name, 'Size', 'new System.Drawing.Size(44, 32)')
            em.p(name, 'LogAction', 'true')
            em.kept(name, 'CheckBoxHMI', old)
            em.security(name, 'CheckBoxHMI', old, self.pg.get('level', 'Maintenance'))
            em.add(name)
        return name

    def axis_rows(self, p, x, y):
        """The axis settings rows: the headings (Tag AxisHeading:<column> - AlignHeaders puts each over its column), the sections, the rows."""
        em = self.em
        if p.get('title'):
            self.label(p['title'], x, y, AXIS_ROW_W, TITLE_H - 4)
            y += TITLE_H
        for key, text, hx, hw in (p.get('headings') or AXIS_HEADINGS):
            # plain labels, as every axis settings page's: AlignHeaders moves, names and translates them (Col_...)
            n = em.unique('lblHead' + key)
            em.decl(n, 'Label')
            em.header(n)
            em.p(n, 'Font', 'new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold)')
            em.p(n, 'ForeColor', 'System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(116)))), ((int)(((byte)(124)))))')
            em.p(n, 'Location', 'new System.Drawing.Point(%d, %d)' % (x + hx, y))
            em.p(n, 'Name', cs(n))
            em.p(n, 'Size', 'new System.Drawing.Size(%d, 18)' % hw)
            em.p(n, 'Tag', cs('AxisHeading:' + key))
            em.p(n, 'Text', cs(text))
            em.p(n, 'TextAlign', 'System.Drawing.ContentAlignment.MiddleCenter')
            em.add(n)
        y += COLHEAD_H + 4
        for r in p['rows']:
            if r['kind'] == 'sec':
                self.label(r['title'], x, y + 2, AXIS_ROW_W, SEC_H - 4)
                y += SEC_H
                continue
            a = r['axis']
            name = em.unique('ax' + ident(a['caption']))
            old = self.old(name)
            em.decl(name, 'AxisSettingHMI')
            em.header(name, a.get('note'))
            em.p(name, 'AxisNameKey', cs(a['key']))
            em.p(name, 'BackColor', 'System.Drawing.Color.White')
            for prop, f in AXIS_FIELDS:
                if a.get(f): em.p(name, prop, cs(a[f]))
            if a.get('side'): em.p(name, 'ReferenceSensorSide', 'Kaban.UI.Controls.AxisReferenceSide.' + a['side'])
            em.p(name, 'DecimalPlaces', str(a.get('dec', 2)))
            em.p(name, 'Digits', '8')
            em.p(name, 'Divisor', dnum(a.get('div', 10000)))
            em.p(name, 'HMIAxisIndex', str(a['number']))
            em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y))
            if a.get('motion'):
                em.p(name, 'MotionAxisIndex', str(a['motion'][1]))
                em.p(name, 'MotionNumber', str(a['motion'][0]))
            em.p(name, 'Name', cs(name))
            em.p(name, 'PlcNumber', '1')
            em.p(name, 'PositionDataType', 'Kaban.Core.Enums.DataTypeEnum.Bin_32')
            em.p(name, 'SettingDataType', 'Kaban.Core.Enums.DataTypeEnum.Bin_32')
            em.p(name, 'Size', 'new System.Drawing.Size(%d, 40)' % AXIS_ROW_W)
            em.p(name, 'Suffix', cs(a.get('unit', 'mm')))
            if a.get('load'):
                # the load as the old page's bar: |raw| on 0 - 100 %
                em.p(name, 'CurrentDivisor', dnum(a.get('loaddiv', 1)))
                em.p(name, 'CurrentFullScale', '100D')
            if a.get('zdist'):
                em.p(name, 'ZDistanceDivisor', '1D')
                em.p(name, 'ZDistanceFullScale', '100D')
            em.kept(name, 'AxisSettingHMI', old)
            em.p(name, 'RequiredSecurity', 'Kaban.Core.Enums.SecurityLevel.' + self.level())
            em.add(name)
            y += 40 + AXIS_ROW_GAP
        return y

    def enc_rows(self, p, x, y):
        """The Enc / PPos rows: the headings (tagged EncoderHeading: / PositioningHeading: and their column - the rows put each
        over its column), the rows under them."""
        em = self.em
        if p.get('title'):
            self.label(p['title'], x, y, panel_width(p), TITLE_H - 4)
            y += TITLE_H
        tag = 'EncoderHeading:' if p['encrows'] == 'enc' else 'PositioningHeading:'
        hx = x + p['namew'] + L.PP_GAP + L.PP_GROUP_GAP
        for i, (key, text, w) in enumerate(p['sections']):
            n = em.unique('lblHead' + ident(p['key']) + key)
            em.decl(n, 'Label')
            em.header(n)
            em.p(n, 'Font', 'new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Bold)')
            em.p(n, 'ForeColor', 'System.Drawing.Color.FromArgb(((int)(((byte)(110)))), ((int)(((byte)(116)))), ((int)(((byte)(124)))))')
            em.p(n, 'Location', 'new System.Drawing.Point(%d, %d)' % (hx, y))
            em.p(n, 'Name', cs(n))
            em.p(n, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, L.PP_HEAD_H))
            em.p(n, 'Tag', cs(tag + key))
            em.p(n, 'Text', cs(text))
            em.p(n, 'TextAlign', 'System.Drawing.ContentAlignment.BottomCenter')
            em.add(n)
            hx += w + (L.PP_GROUP_GAP if i < len(p['sections']) - 1 else 0)
        y += L.PP_HEAD_H + 6
        w = panel_width(p)
        for it in p['rows']:
            if it['kind'] == 'enc':
                name = em.unique('enc' + ident(L_text(it['title'])))
                self.enc_row(name, it, x, y, w)
            else:
                name = em.unique('pp' + ident(L_text(it['title'])))
                old = self.old(name)
                em.positioning(name, it, x, y, w, old)
                for prop, f in (('ErrorBackAddress', 'errA'), ('ErrorForwardAddress', 'errB')):
                    if it.get(f): em.p(name, prop, cs(it[f]))
                if it.get('note'): em.body.append('            // ' + it['note'])
                em.p(name, 'RequiredSecurity', 'Kaban.Core.Enums.SecurityLevel.' + self.level())
            y += L.PP_PITCH
        return y

    def enc_row(self, name, it, x, y, w):
        """A CylinderEncoderSettingHMI row (as FA2010's tool writes one, with its error bits and its settings' own divisor)."""
        em = self.em
        old = self.old(name)
        em.decl(name, 'CylinderEncoderSettingHMI')
        a, b = it['a'], it['b']
        em.header(name, '%s: encoder %d - %s / %s, position %s; reference %s, measuring tolerance %s.%s' % (
            L_text(it['title']), it['number'], a['cmd'], b['cmd'], it['pos']['addr'], it.get('ref') or '-', it.get('tol') or '-',
            ('\n' + it['note']) if it.get('note') else ''))
        em.p(name, 'BackColor', 'System.Drawing.Color.White')
        em.p(name, 'Name', cs(name))
        em.p(name, 'PlcNumber', '1')
        em.p(name, 'EncoderNumber', str(it['number']))
        em.p(name, 'TitleTextKey', cs(it['title']))
        em.p(name, 'Location', 'new System.Drawing.Point(%d, %d)' % (x, y))
        em.p(name, 'Size', 'new System.Drawing.Size(%d, %d)' % (w, L.PP_ROW_H))
        em.p(name, 'CommandBackAddress', cs(a['cmd']))
        em.p(name, 'CommandForwardAddress', cs(b['cmd']))
        if a.get('lamp') and a['lamp'] != a['cmd']: em.p(name, 'BackSensorAddress', cs(a['lamp']))
        if b.get('lamp') and b['lamp'] != b['cmd']: em.p(name, 'ForwardSensorAddress', cs(b['lamp']))
        pos = it['pos']
        em.p(name, 'PositionAddress', cs(pos['addr']))
        em.p(name, 'PositionDataType', 'Kaban.Core.Enums.DataTypeEnum.Bin_%d' % pos['bits'])
        em.p(name, 'PositionDivisor', dnum(pos['div']))
        em.p(name, 'PositionDecimalPlaces', str(pos['dec']))
        for key, prop in (('ref', 'ReferenceAddress'), ('tol', 'MeasuringToleranceAddress'), ('start', 'MinimumLimitAddress'),
                          ('unclamp', 'UnclampingIntervalAddress')):
            if it.get(key): em.p(name, prop, cs(it[key]))
        em.p(name, 'Divisor', dnum(it['setdiv'][0]))
        em.p(name, 'DecimalPlaces', str(it['setdiv'][1]))
        if it.get('valve'): em.p(name, 'ValveType', 'Kaban.UI.Controls.CylinderValve.' + it['valve'])
        if it.get('feedback'): em.p(name, 'FeedbackType', 'Kaban.UI.Controls.CylinderFeedback.' + it['feedback'])
        for prop, f in (('ErrorBackAddress', 'errA'), ('ErrorForwardAddress', 'errB')):
            if it.get(f): em.p(name, prop, cs(it[f]))
        em.kept(name, 'CylinderEncoderSettingHMI', old)
        em.p(name, 'RequiredSecurity', 'Kaban.Core.Enums.SecurityLevel.' + self.level())
        em.add(name)

    def panel(self, p, x, y):
        pg = self.pg
        if p.get('axisrows'):
            return self.axis_rows(p, x, y)
        if p.get('encrows'):
            return self.enc_rows(p, x, y)
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
            rh = row_h(r)
            if r['caption']:
                self.label(r['caption'], xs[0], y, p['cols'][0][1], rh, bold=False, prefix='cap')
            ci = 1
            for c in r['cells']:
                if c is None:
                    ci += 1
                    continue
                span = c.get('span', 1)
                w = sum(p['cols'][j][1] for j in range(ci, ci + span)) + GAP_X * (span - 1)
                caption = ' '.join(t for t in (p.get('title'), sec, r['caption'], p['cols'][ci][0].replace('|', ' ')) if t)
                self.cell(cell_name(pg, p, sec, r.get('key') or r['caption'], ci, c['kind']), c, xs[ci], y, w, caption, rh)
                ci += span
            assert ci <= len(p['cols']), (pg['cls'], p['key'], r['caption'])
            y += rh
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
        self.em.write(os.path.join(SCREENS, self.pg['folder'], self.pg['cls'] + '.Designer.cs'), (right + X0, y))
        return self.em

# ============================================================================
#  CHECK: the old pages' devices
# ============================================================================
SHOWN_TYPES = ('KabanButton', 'KabanLambButton', 'KabanLamb', 'KabanLambNegative', 'KabanLambAlternative', 'KabanLedLabel',
               'KabanNumericButton', 'KabanSwitchButton', 'KabanProgressBar', 'KabanTrackBar')

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
    # a control never added to a container is not on the page (17 November: SB3015's Milling has four)
    return {n: c for n, c in ctl.items() if c['t'] in SHOWN_TYPES and n in parent and shown(n)}

def old_devices(old):
    out = set(CODE_DEVICES.get(old, ()))
    for c in old_controls(old).values():
        for k in ('Device', 'LambDevice'):
            # (17 November: SB3015's pages wrote some in small letters - "m751")
            if c.get(k) and c[k] != 'null': out.add(c[k].upper())
    return out

def cells(pg):
    for band in pg['bands']:
        for p in band:
            for r in p['rows']:
                if r['kind'] == 'row':
                    for c in r['cells']:
                        if c: yield c
                elif r['kind'] == 'axrow':
                    yield dict(kind='axrow', axis=r['axis'])
                elif r['kind'] in ('enc', 'mould'):
                    yield r

# an axis's devices an old page showed: its jog buttons, position, limits, calibration, reference sensor and its distance
# (its error bit and motion module come from the alarm list and the drawings, not from an old page); div / dec: its
# values' divisor and decimals (10000 / 2 unless given)
AXIS_SHOWN = ('back', 'fwd', 'pos', 'min', 'max', 'cal', 'calibrate', 'zdist', 'ref', 'home', 'goref', 'load', 'homereq', 'axerr', 'warn',
              'servoerr')

def spec_devices(pg):
    out = set()
    for c in cells(pg):
        k = c['kind']
        if k == 'btn':
            out.add(c['cmd'])
            out |= {cmd for cmd, _ in c.get('more', ())}
            if c['lamp']: out.add(c['lamp'])
        elif k in ('disp', 'entry'): out.add(c['val']['addr'])
        elif k == 'axis':
            a = AXES[c['number']]
            out |= {a[f] for f in ('back', 'fwd', 'pos', 'ref') if a.get(f)}
        elif k == 'axrow':
            out |= {c['axis'][f] for f in AXIS_SHOWN if c['axis'].get(f)}
        elif k == 'cyl':
            out |= {sd['cmd'] for sd in (c['a'], c['b']) if sd}
            out |= {l for l in (c.get('lampA'), c.get('lampB')) if l}
            if c.get('pos'): out.add(c['pos']['addr'])
        elif k in ('enc', 'mould', 'encunit'):
            e = ENCODERS[c['number']] if k == 'encunit' else c
            out |= {sd['cmd'] for sd in (e['a'], e['b'])}
            out |= {sd['lamp'] for sd in (e['a'], e['b']) if sd.get('lamp')}
            out.add(e['pos']['addr'])
            if k != 'encunit':
                out |= {e[f] for f in ('ref', 'tol', 'start', 'unclamp', 'target', 'send', 'cal', 'calibrate', 'offset') if e.get(f)}
        elif k == 'inv':
            out |= {sd['cmd'] for sd in (c['a'], c['b']) if sd}
            out |= {sd['lamp'] for sd in (c['a'], c['b']) if sd and sd.get('lamp')}
            out |= {d for d in (c.get('fault'), c.get('run')) if d}
            if c.get('val'): out.add(c['val']['addr'])
        else: out.add(c['addr'])
    return out

def divisors(pg):
    """The old pages' values against the spec: address -> (divisor, decimals) as each shows it."""
    problems = []
    old = {}
    for o in pg['old']:
        for c in old_controls(o).values():
            dev = (c.get('Device') or '').upper()
            if not dev: continue
            if c['t'] == 'KabanLedLabel':
                # DirectShow: the raw value as it is
                div = 1.0 if c.get('DirectShow') == 'true' else float(c.get('Divisor', '1').rstrip('D')) * 10
                dec = 0 if c.get('DirectShow') == 'true' else int(c.get('FloatingPoint', '0'))
                bits = 32 if 'Bit_32' in c.get('DataType', '') else 16
                old.setdefault(dev, set()).add(('V', div, dec, bits))
            elif c['t'] == 'KabanNumericButton':
                div = float(c.get('Divisor', '1').rstrip('D'))
                mm = re.search(r'(\d+)', c.get('Mask', '') or '')
                dec = int(mm.group(1)) if mm else 0
                bits = 32 if 'Bit_32' in c.get('DataType', '') else 16
                old.setdefault(dev, set()).add(('N', div, dec, bits))
    for c in cells(pg):
        # an encoder's / a positioning's settings: the row's divisor (a positioning's: 100)
        if c['kind'] in ('enc', 'mould'):
            div = float(c['setdiv'][0]) if c['kind'] == 'enc' else 100.0
            for f in ('ref', 'tol', 'start', 'unclamp', 'target', 'cal', 'offset'):
                seen = old.get(c.get(f) or '')
                if seen and not any(k == 'N' and d == div for k, d, _, _ in seen):
                    problems.append('%s: divisor %g, the old page %s' % (c[f], div, sorted(seen)))
            continue
        if c['kind'] not in ('disp', 'entry'): continue
        v = c['val']
        seen = old.get(v['addr'])
        if not seen: continue
        kind = 'V' if c['kind'] == 'disp' else 'N'
        if not any(k == kind and d == v['div'] for k, d, _, _ in seen):
            problems.append('%s: divisor %g, the old page %s' % (v['addr'], v['div'], sorted(seen)))
    return problems

OLD_ACTION = {'Momentary': 'Momentary', 'Alternate': 'Alternate', 'Set': 'Set'}

def actions(pg):
    """The old buttons' actions against the spec's (a button's command and how it acts)."""
    problems = []
    old = {}
    for o in pg['old']:
        for c in old_controls(o).values():
            if c['t'] in ('KabanButton', 'KabanLambButton') and c.get('Device'):
                old.setdefault(c['Device'].upper(), set()).add(OLD_ACTION.get(c.get('ButtonType', 'Momentary').split('.')[-1], '?'))
    for c in cells(pg):
        if c['kind'] in ('cyl', 'inv'):
            for sd in (c['a'], c['b']):
                seen = old.get(sd['cmd']) if sd else None
                # an old toggle (Alternate) made two buttons - Set on one side, Reset on the other
                if seen and sd['action'] in ('Set', 'Reset') and seen == {'Alternate'} and c['a'] and c['b'] and c['a']['cmd'] == c['b']['cmd']:
                    continue
                if seen and sd['action'] not in seen:
                    problems.append('%s: %s, the old page %s' % (sd['cmd'], sd['action'], sorted(seen)))
            continue
        if c['kind'] != 'btn': continue
        # (a button of several commands - Reference All, Calibrate All - was the old page's code: its first one only)
        seen = old.get(c['cmd'])
        if seen and not c.get('more') and c['action'] not in seen:
            problems.append('%s: %s, the old page %s' % (c['cmd'], c['action'], sorted(seen)))
    return problems

def check():
    bad = 0
    for pg in PAGES:
        o = set().union(*[old_devices(x) for x in pg['old']])
        s = spec_devices(pg)
        # pages made from one old page (12 November: Manual Movement's cylinders on a page of their own) - together
        for other in PAGES:
            if other is not pg and set(other['old']) == set(pg['old']): s |= spec_devices(other)
        exp = EXPECTED.get(pg['cls'], {})
        missing = o - s - exp.get('missing', set())
        extra = s - o - exp.get('extra', set())
        print('%-26s old %3d  spec %3d  missing %s  extra %s' % (pg['cls'], len(o), len(s), sorted(missing) or '-', sorted(extra) or '-'))
        bad += len(missing) + len(extra)
        for pr in divisors(pg) + actions(pg):
            print('    ' + pr); bad += 1
    return bad

# ============================================================================
#  MAIN
# ============================================================================
def write_texts():
    """The machine's own texts and its alarm list's: rows added to its Translations.csv when the key is not there yet (the
    rows there are kept as they are - a text changed by hand stays); ALARMS written to its Alarms.json."""
    import json
    path = os.path.join(MACHINE, 'Translations.csv')
    raw = open(path, 'rb').read()
    nl = '\r\n' if b'\r\n' in raw else '\n'
    text = raw.decode('utf-8-sig')
    have = {l.split(';', 1)[0] for l in text.splitlines()}
    name = os.path.basename(MACHINE).split('.')[-1]
    rows = dict(TRANSLATIONS)
    for addr, tag, en, tr in ALARMS:
        rows['Alarm_%s.%s' % (name, tag)] = (en, tr)
    add = [k for k in rows if k not in have]
    for k in add:
        en, tr = rows[k]
        assert ';' not in en + tr, k
    if add:
        if not text.endswith(('\n', '\r')): text += nl
        text += ''.join('%s;%s;%s;;;;%s' % (k, rows[k][0], rows[k][1], nl) for k in add)
        open(path, 'wb').write(b'\xef\xbb\xbf' + text.encode('utf-8'))
        print('Translations.csv: %d rows added' % len(add))
    if ALARMS:
        items = [dict(PlcAddress=addr, TagCode=tag, Severity='Fault', PlcNumber=1) for addr, tag, en, tr in ALARMS]
        data = json.dumps(items, ensure_ascii=False, indent=2).replace('\n', nl) + nl
        apath = os.path.join(MACHINE, 'Alarms.json')
        araw = open(apath, 'rb').read()
        bom = araw.startswith(b'\xef\xbb\xbf')
        open(apath, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + data.encode('utf-8'))
        print('Alarms.json: %d alarms' % len(items))

def main():
    if '--check' in sys.argv:
        sys.exit(1 if check() else 0)
    write_texts()
    for pg in PAGES:
        folder = os.path.join(SCREENS, pg['folder'])
        os.makedirs(folder, exist_ok=True)
        path = os.path.join(folder, pg['cls'] + '.Designer.cs')
        current = L.parse_current(path)
        for n, b in current.items(): b['own'] = n
        em = Grid(pg, current).write()
        print('%-26s %3d controls' % (pg['cls'], len(em.decls)))
        for d in em.dropped: print('    not carried over: ' + d)

