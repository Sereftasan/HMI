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

def setup(project, old, tool, made):
    """project: 'Kaban.Machines.MD3020'; old: the old pages' folder under tools/legacy ('MD3030')."""
    global SCREENS
    machine = os.path.join(L.ROOT, project)
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
L.TYPES['SpeedOverrideHMI'] = 'Kaban.UI.Controls.SpeedOverrideHMI'
L.OWNED['SpeedOverrideHMI'] = {'Name', 'PlcNumber', 'Address', 'BackColor', 'FallbackText', 'TranslationKey', 'Location', 'Size', 'LogAction', 'TabIndex'}
L.OWNED['LabelHMI'] = {'Name', 'TranslationKey', 'Text', 'Font', 'ForeColor', 'TextAlign', 'Location', 'Size', 'BackColor'}

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

def Row(caption, *cells, **kw):
    """A row: its caption, its cells (None: an empty one); key: the names' part when the caption is empty."""
    return dict(kind='row', caption=caption, cells=list(cells), key=kw.get('key'))

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



# What --check accepts as deliberate differences from the old designers: page class -> {'missing': .., 'extra': ..}
EXPECTED = {}
# devices an old page's code reads and writes (toggle switches): old page -> set
CODE_DEVICES = {}


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
    pre = {'btn': 'btn', 'disp': 'disp', 'entry': 'num', 'lamp': 'lamp', 'meter': 'load', 'opt': 'opt', 'speed': 'spd'}[kind]
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
            em.security(name, 'CheckBoxHMI', old, self.pg.get('level', 'Maintenance'))
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
            if r['caption']:
                self.label(r['caption'], xs[0], y, p['cols'][0][1], ROW_H, bold=False, prefix='cap')
            ci = 1
            for c in r['cells']:
                if c is None:
                    ci += 1
                    continue
                span = c.get('span', 1)
                w = sum(p['cols'][j][1] for j in range(ci, ci + span)) + GAP_X * (span - 1)
                caption = ' '.join(t for t in (p.get('title'), sec, r['caption'], p['cols'][ci][0].replace('|', ' ')) if t)
                self.cell(cell_name(pg, p, sec, r.get('key') or r['caption'], ci, c['kind']), c, xs[ci], y, w, caption)
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
    return {n: c for n, c in ctl.items() if c['t'] in SHOWN_TYPES and shown(n)}

def old_devices(old):
    out = set(CODE_DEVICES.get(old, ()))
    for c in old_controls(old).values():
        for k in ('Device', 'LambDevice'):
            if c.get(k) and c[k] != 'null': out.add(c[k])
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
            out |= {cmd for cmd, _ in c.get('more', ())}
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
                old.setdefault(c['Device'], set()).add(OLD_ACTION.get(c.get('ButtonType', 'Momentary').split('.')[-1], '?'))
    for c in cells(pg):
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
def main():
    if '--check' in sys.argv:
        sys.exit(1 if check() else 0)
    for pg in PAGES:
        folder = os.path.join(SCREENS, pg['folder'])
        os.makedirs(folder, exist_ok=True)
        path = os.path.join(folder, pg['cls'] + '.Designer.cs')
        current = L.parse_current(path)
        for n, b in current.items(): b['own'] = n
        em = Grid(pg, current).write()
        print('%-26s %3d controls' % (pg['cls'], len(em.decls)))
        for d in em.dropped: print('    not carried over: ' + d)

