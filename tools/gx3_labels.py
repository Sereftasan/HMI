#!/usr/bin/env python3
"""
Reads a GX Works3 project (.gx3) - its global labels and its device comments -
and writes them out for the HMI.

A .gx3 file is a zip of SQLite databases:
    LabelData.db   the labels: the global label list is LabelTypeID 5122, a row
                   per label - column 2 its name, 3 its data type, 7 the device
                   it is assigned to, 9 its comment
    *_DC.db        the device comments (DEVICE_DATA / COMMENT_DATA); the texts
                   are Turkish (cp1254) read back as cp1252, so they are re-decoded
The program itself (*_MilDB.db) is an instruction list that mostly names labels,
not devices - it is not read here.

    python3 tools/gx3_labels.py <project.gx3> --dump <out.txt> [--title TEXT]
        every global label (device, label, comment), then every device comment
    python3 tools/gx3_labels.py <project.gx3> --csv <machine project folder> [--title TEXT]
        <folder>/PlcLabels.csv - the global labels that have a device, as
        Label;DataType;Address, with the PLC Monitor's divisor and decimal places
        wherever the machine's pages show that device scaled (its Designer files)

No "#heading;" lines are written: a heading puts its labels in the PLC Monitor's
tab of what the HMI sends - the PC_ labels go there by their prefix already.
"""
import argparse, collections, glob, io, os, re, shutil, sqlite3, sys, tempfile, zipfile

GLOBAL_LABELS = 5122
# the label list's data type codes (column 3) -> the HMI's names (PlcRowEditorForm.KnownDataTypes)
DATA_TYPES = {1: 'Bit', 2: 'Word [Unsigned]', 4: 'Word [Signed]', 5: 'Double Word [Signed]',
              3: 'Double Word [Unsigned]', 6: 'Float [Single Precision]'}
# the device comments' device codes: (prefix, number base)
DEVICES = {1: ('M', 10), 2: ('SM', 10), 8: ('B', 16), 16: ('X', 8), 17: ('Y', 8), 32: ('D', 10),
           33: ('SD', 10), 48: ('W', 16), 49: ('SW', 16), 3: ('L', 10), 4: ('F', 10), 5: ('V', 10),
           6: ('S', 10), 7: ('T', 10), 9: ('C', 10), 34: ('R', 10), 36: ('ZR', 10), 37: ('Z', 10)}
MODULE_BUFFER = (35, 208)   # U\G: the modules' own buffer memory names (the SSC modules' parameter list)


def unpack(gx3):
    """The databases this needs, copied out of the project into a folder of their own."""
    if os.path.isdir(gx3):
        return gx3, None
    tmp = tempfile.mkdtemp(prefix='gx3_')
    with zipfile.ZipFile(gx3) as z:
        for name in z.namelist():
            base = os.path.basename(name)       # never a path out of the folder
            if base == 'LabelData.db' or base.endswith('_DC.db'):
                with z.open(name) as src, open(os.path.join(tmp, base), 'wb') as dst:
                    shutil.copyfileobj(src, dst)
    return tmp, tmp


def fix_text(s):
    s = (s or '').replace('\r', ' ').replace('\n', ' ').strip()
    try:
        return s.encode('cp1252').decode('cp1254')
    except (UnicodeEncodeError, UnicodeDecodeError):
        return s


def read_labels(folder):
    """[(device, label, data type code, comment)] - the global labels, in the list's order."""
    path = os.path.join(folder, 'LabelData.db')
    if not os.path.exists(path):
        return []
    con = sqlite3.connect(path)
    ids = [r[0] for r in con.execute('select LabelID from LabelTbl where LabelTypeID=?', (GLOBAL_LABELS,))]
    if not ids:
        return []
    rows = collections.OrderedDict()
    q = ('select c.LabelID, c.RowID, c.ColumnID, c.ColumnStrValue, c.ColumnIntValue, r.RowNo '
         'from ColumnDataTbl c left join RowTbl r on r.LabelID = c.LabelID and r.RowID = c.RowID '
         'where c.LabelID in (%s) order by c.LabelID, r.RowNo, c.RowID' % ','.join('?' * len(ids)))
    for lid, rid, col, s, i, _ in con.execute(q, ids):
        rows.setdefault((lid, rid), {})[col] = (s or '', i)
    out = []
    for cols in rows.values():
        name = cols.get(2, ('', 0))[0].strip()
        dev = cols.get(7, ('', 0))[0].strip()
        code = cols.get(3, ('', 0))[1]
        comment = ' '.join(fix_text(cols[c][0]) for c in sorted(cols) if c >= 9 and c <= 28 and cols[c][0].strip())
        if name:
            out.append((dev, name, code, comment.replace('\r', ' ').replace('\n', ' ').strip()))
    return out


def read_comments(folder):
    """[(device, comment)] and how many module buffer memory comments were left out."""
    out, skipped = [], 0
    for path in sorted(glob.glob(os.path.join(folder, '*_DC.db'))):
        con = sqlite3.connect(path)
        q = ('select d.DevCode, d.ExtCode, d.ExtNo, d.DevNoHigh, d.DevNoLow, d.BitNo, m.CmtData '
             'from DEVICE_DATA d join COMMENT_DATA m on m.DeviceSEQ = d.SEQ '
             'where m.DelFlag is null or m.DelFlag = 0 order by d.DevCode, d.ExtCode, d.DevNoHigh, d.DevNoLow, d.BitNo')
        for code, ext, extno, hi, lo, bit, text in con.execute(q):
            text = fix_text(text)
            if not text:
                continue
            if (code, ext) == MODULE_BUFFER:
                skipped += 1
                continue
            n = (hi or 0) * 65536 + (lo or 0)
            if code in DEVICES:
                prefix, base = DEVICES[code]
                num = {8: '%o', 16: '%X'}.get(base, '%d') % n
                dev = prefix + num
                if ext:
                    dev += ' (local, program %d)' % extno
            else:
                dev = '(device code %d) %d' % (code, n)
            out.append((dev, text))
    return out, skipped


def designer_scales(project):
    """{address: (divisor, decimals)} - how the machine's pages show each device (the commonest way)."""
    seen = collections.defaultdict(collections.Counter)
    for path in glob.glob(os.path.join(project, 'Screens', '**', '*.Designer.cs'), recursive=True):
        props = collections.defaultdict(dict)
        with io.open(path, encoding='utf-8-sig') as f:
            for line in f:
                m = re.match(r'\s*this\.(\w+)\.(Address|Divisor|DecimalPlaces) = (.*);', line)
                if m:
                    props[m.group(1)][m.group(2)] = m.group(3).strip()
        for p in props.values():
            if 'Address' not in p or 'Divisor' not in p:
                continue
            addr = p['Address'].strip('"').strip().upper()
            try:
                div = float(p['Divisor'].rstrip('DdFfMm'))
                dec = int(p.get('DecimalPlaces', '0'))
            except ValueError:
                continue
            if addr and div > 0:
                seen[addr][(div, dec)] += 1
    return {a: c.most_common(1)[0][0] for a, c in seen.items()}


KIND_ORDER = ['X', 'Y', 'M', 'L', 'B', 'F', 'SM', 'T', 'C', 'D', 'W', 'R', 'ZR', 'U', 'SD', 'SW']
KIND_TITLES = {'X': 'Inputs (X, octal)', 'Y': 'Outputs (Y, octal)', 'M': 'Internal relays (M)',
               'B': 'Link relays (B, hexadecimal)', 'L': 'Latching relays (L)', 'SM': 'Special relays (SM)', 'D': 'Data registers (D)',
               'W': 'Link registers (W, hexadecimal)', 'U': "Modules' buffer memory (U\\G)",
               'SD': 'Special registers (SD)', 'R': 'File registers (R)'}


def kind_of(dev):
    m = re.match(r'(U\d+\\G|SM|SD|SW|ZR|[A-Z])', dev)
    k = m.group(1) if m else ''
    return 'U' if k.startswith('U') else k


def sort_key(dev):
    k = kind_of(dev)
    rest = dev[len(k):] if k != 'U' else dev
    nums = re.findall(r'[0-9A-F]+', rest)
    base = 8 if k in ('X', 'Y') else 16 if k in ('B', 'W', 'SW') else 10
    key = []
    for n in nums:
        try:
            key.append(int(n, base))
        except ValueError:
            key.append(int(n, 16))
    return (KIND_ORDER.index(k) if k in KIND_ORDER else len(KIND_ORDER), k, key)


def write_dump(path, title, labels, comments, skipped):
    with io.open(path, 'w', encoding='utf-8', newline='\n') as f:
        f.write('# %s - its global labels (device, label, comment), then its device comments\n\n' % title)
        for dev, name, code, comment in sorted((l for l in labels if l[0]), key=lambda l: sort_key(l[0])):
                f.write('%s\t%s\t%s\n' % (dev, name, comment))
        f.write('\n# the device comments')
        if skipped:
            f.write(" (%d comments of the modules' buffer memory - the positioning modules' own parameter names - left out)" % skipped)
        f.write('\n')
        for dev, text in comments:
            f.write('%s\t%s\n' % (dev, text))


def write_csv(project, title, labels):
    scales = designer_scales(project)
    groups = collections.OrderedDict()
    seen = set()
    dropped = 0
    for dev, name, code, comment in labels:
        dtype = DATA_TYPES.get(code)
        if not dev or not dtype or ';' in name:
            dropped += 1
            continue
        if name.lower() in seen:
            dropped += 1
            continue
        seen.add(name.lower())
        groups.setdefault(kind_of(dev), []).append((dev, name, dtype))
    path = os.path.join(project, 'PlcLabels.csv')
    n = scaled = 0
    with io.open(path, 'w', encoding='utf-8-sig', newline='') as f:
        def w(line):
            f.write(line + '\r\n')
        w('# %s - the PLC program\'s global labels (generated by tools/gx3_labels.py)' % title)
        w('# Label;DataType;Address - and, where the machine\'s pages show the device scaled, ;Divisor;Decimal places')
        w('# (those two come from the pages, not from the PLC program)')
        w('Label;DataType;Address')
        for kind in sorted(groups, key=lambda k: (KIND_ORDER.index(k) if k in KIND_ORDER else 99, k)):
            w('')
            w('# ' + KIND_TITLES.get(kind, kind))
            for dev, name, dtype in sorted(groups[kind], key=lambda r: sort_key(r[0])):
                s = scales.get(dev.upper())
                if s and dtype != 'Bit':
                    div, dec = s
                    w('%s;%s;%s;%s;%d' % (name, dtype, dev, ('%g' % div), dec))
                    scaled += 1
                else:
                    w('%s;%s;%s' % (name, dtype, dev))
                n += 1
    print('%s: %d labels (%d with the pages\' scaling), %d left out (no device, a type the HMI does not read, or a repeated name)'
          % (path, n, scaled, dropped))


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[1])
    ap.add_argument('gx3')
    ap.add_argument('--dump')
    ap.add_argument('--csv')
    ap.add_argument('--title', default='PLC program')
    a = ap.parse_args()
    folder, tmp = unpack(a.gx3)
    try:
        labels = read_labels(folder)
        if a.dump:
            comments, skipped = read_comments(folder)
            write_dump(a.dump, a.title, labels, comments, skipped)
            print('%s: %d labels, %d device comments' % (a.dump, sum(1 for l in labels if l[0]), len(comments)))
        if a.csv:
            write_csv(a.csv, a.title, labels)
    finally:
        if tmp:
            shutil.rmtree(tmp, ignore_errors=True)
    return 0


if __name__ == '__main__':
    sys.exit(main())
