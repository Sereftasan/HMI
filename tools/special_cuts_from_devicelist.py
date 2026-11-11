#!/usr/bin/env python3
"""
The special cut recipes of FA2010's device list ("Kırlangıç Kesim" sheet, FA2010_DeviceList_22.09.14_FX5.xls) as the HMI keeps them
(Kaban.Machines.FA2010/SpecialCutSamples.csv - the Profiles page's Special cuts tab adds them with "Device list samples").

The sheet's formulas name its value cells: K2 T (the saw blade), K3 D (saw - V cut centre distance), K4 W (the profile's full width),
K5 O (the cut length offset), K6 R and K7 L (the job file's right / left values) - they become the recipes' letters.
Its recipes' codes come from their names (10 November: "'Right 90 Left Arrow' means the previous pieces right cut ... is 90 degrees
normal cut and the following cut pieces left cut is an arrow special cut"): "Right <a> Left <b>" the right code <a>, the left code <b>;
"First End <b>" the left code <b> at the bar's start (right 0); another ("Back Open Y") the special cut itself at both ends (11 November: "Yes") - an angle
as its number, a special cut by its NAME (the Profiles page gives it the job files' code when the samples are added). Column C beside a
recipe's name (Excel's value, the .xls) is its saw dust width x10 (10 November: "42 ... it means 4.2mm"). The starting angle is the
recipe's first normal cut's (the HMI works it out).

  soffice --headless --convert-to xlsx FA2010_DeviceList_22.09.14_FX5.xls     (Excel's formulas are kept in the .xlsx)
  python3 tools/special_cuts_from_devicelist.py FA2010_DeviceList_22.09.14_FX5.xlsx [FA2010_DeviceList_22.09.14_FX5.xls] > Kaban.Machines.FA2010/SpecialCutSamples.csv
  (the .xls for column C's values: LibreOffice cannot work its formula out - #N/A in the .xlsx)
"""
import re, sys
import openpyxl

LETTERS = {'K2': 'T', 'K3': 'D', 'K4': 'W', 'K5': 'O', 'K6': 'R', 'K7': 'L'}

def formula(v):
    """A cell as a recipe's formula: Excel's =-$K$4/2-$K$5 as -W/2-O, a number as itself."""
    if v is None: return ''
    if isinstance(v, str) and v.startswith('='):
        f = v[1:].replace('$', '')
        f = re.sub(r'K[2-7]\b', lambda m: LETTERS[m.group(0)], f)
        if re.search(r'[A-Z]{1,2}\d', f): raise ValueError('a cell the recipes have no letter for: ' + v)
        return f
    if isinstance(v, float) and v == int(v): v = int(v)
    return str(v)

def num(v):
    try: return float(v)
    except (TypeError, ValueError): return 0.0

def codes(name):
    """A sample's left and right codes from its name: 'Right 90 Left Arrow' -> ('Arrow', '90')."""
    m = re.match(r'^Right (.+?) Left (.+)$', name)
    if m: return m.group(2).strip(), m.group(1).strip()
    m = re.match(r'^First End (.+)$', name)
    if m: return m.group(1).strip(), '0'
    return name, name

def dust_widths(xls):
    """Column C beside each recipe's name in the .xls (Excel's cached value): x10 -> mm."""
    if not xls: return {}
    import xlrd
    ws = xlrd.open_workbook(xls).sheet_by_name('Kırlangıç Kesim')
    out = {}
    for r in range(ws.nrows):
        a, c = ws.cell_value(r, 0), ws.cell_value(r, 2)
        if isinstance(a, str) and a.strip() and isinstance(c, (int, float)) and c: out[a.strip()] = '%g' % (c / 10.0)
    return out

def main(path, xls=None):
    dust = dust_widths(xls)
    ws = openpyxl.load_workbook(path)['Kırlangıç Kesim']
    recipes, cur = [], None
    for r in range(1, ws.max_row + 1):
        a, b = ws.cell(r, 1).value, ws.cell(r, 2).value
        if isinstance(a, str) and a.strip() and ws.cell(r, 5).value and 'Tutma' in str(ws.cell(r, 5).value):
            cur = {'name': a.strip(), 'holding': formula(ws.cell(r, 6).value), 'offset': formula(ws.cell(r, 9).value),
                   'note': ws.cell(r, 3).value, 'steps': []}
            recipes.append(cur)
        elif cur is not None and isinstance(b, (int, float)) and 1 <= b <= 7:
            t = lambda c: str(ws.cell(r, c).value or '').strip().lower() in ('t', 'true', '1')
            cur['steps'].append({'cmd': int(b), 'left': t(4), 'right': t(5), 'r1': formula(ws.cell(r, 6).value),
                                 'r2': formula(ws.cell(r, 7).value), 'angle': num(ws.cell(r, 8).value), 'v': formula(ws.cell(r, 9).value)})
    out = ['# The special cut recipes of FA2010_DeviceList_22.09.14_FX5.xls (sheet "Kırlangıç Kesim") - tools/special_cuts_from_devicelist.py.',
           '# Their codes from their names ("Right 90 Left Arrow": right 90, left Arrow - a special cut by its name, given the job files\' code',
           '# where they are added; "Back Open Y" / "Front Open Y" at both ends), the saw dust width column C / 10 (42: 4.2 mm),',
           '# the starting angle their first normal cut\'s.',
           '# recipe;no;name;left code;right code;starting angle;exit robot holding;robot X offset;saw dust width;with L value;with R value',
           '# step;command;left clamp;right clamp;robot 1;robot 2;angle;V cut Y']
    for i, rc in enumerate(recipes, 1):
        left, right = codes(rc['name'])
        first = next((s['angle'] for s in rc['steps'] if s['cmd'] == 4), 0)
        out.append(';'.join(['recipe', str(i), rc['name'], left, right, '%g' % first, rc['holding'], rc['offset'], dust.get(rc['name'], ''), '0', '0']))
        for s in rc['steps']:
            keep = lambda f, used: f if used and f not in ('0', '') else ''
            cmd = s['cmd']
            out.append(';'.join(['step', str(cmd), '1' if s['left'] else '0', '1' if s['right'] else '0',
                                 keep(s['r1'], cmd in (3, 7)), keep(s['r2'], cmd in (3, 6)), ('%g' % s['angle']) if cmd == 4 else '0',
                                 keep(s['v'], cmd == 5)]))
    sys.stdout.write('\r\n'.join(out) + '\r\n')

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else None)
