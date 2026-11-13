#!/usr/bin/env python3
"""
Writes a GX Works3 project's (.gx3) program out as readable text: each ladder
rung as its condition and what it drives, each inline ST box and ST program as
its source - every label with its device where it has one.

    python3 tools/gx3_program.py <project.gx3> <out.txt> [--title TEXT]

How GX Works3 keeps a program (12 November, read from MD3030 V1.01):
    *_LDDB.db  a ladder POU: table LadderBlocks, a rung per row (blocktype 0 a rung,
               2 a statement - "[Title]..." -, 5 the END), its text
                   V1:<n>:<n token lengths>:<the n tokens>:cb{fg=fg{dim=WxH:es=[...]:vs=[...]}}
               every '#' in the body is the next token: an instruction (a = LD,
               b = LDI, c = OUT, SET, RST, MOV, <>, ...), a label (_lid/<list>/<row>,
               LabelData.db's) or a device prefix (d{s=#:a=<number>}); es the
               elements (ce{op=ct{..}} a contact, ce{op=cl{..}} a coil or an
               instruction, ist{st=#} an inline ST box) at pos=<column>,<row>; vs
               the vertical links (v{pos=c,r}: on the left of column c, rows r-1 and r)
               table IstBlocks: the inline ST boxes' source (id _guid/<guid>)
    *_STDB.db  an ST POU: table StBlocks, its source
The POUs' own names are not in these files (only in the compiled program): a POU is
named here by its database and its titles.
"""
import argparse, collections, glob, os, re, shutil, sqlite3, sys, tempfile, zipfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

CONTACTS = {'a': '', 'b': '/'}
COILS = {'c': 'OUT'}


def unpack(gx3):
    if os.path.isdir(gx3):
        return gx3, None
    tmp = tempfile.mkdtemp(prefix='gx3p_')
    with zipfile.ZipFile(gx3) as z:
        for name in z.namelist():
            base = os.path.basename(name)
            if base == 'LabelData.db' or re.search(r'_(LDDB|STDB)\.db$', base):
                with z.open(name) as src, open(os.path.join(tmp, base), 'wb') as dst:
                    shutil.copyfileobj(src, dst)
    return tmp, tmp


def label_names(folder):
    """{(list id, row id): 'Name(Device)'}"""
    con = sqlite3.connect(os.path.join(folder, 'LabelData.db'))
    cols = collections.defaultdict(dict)
    for lid, rid, col, s in con.execute('select LabelID, RowID, ColumnID, ColumnStrValue from ColumnDataTbl where ColumnID in (2, 7)'):
        cols[(str(lid), int(rid))][col] = (s or '').strip()
    out = {}
    for k, v in cols.items():
        name = v.get(2, '')
        dev = v.get(7, '')
        out[k] = '%s(%s)' % (name, dev) if dev else name
    return out


# ---- the rung's text: V1:<n>:<lengths>:<tokens>:<body> --------------------------------------------

def split_block(data):
    if not data.startswith('V1:'):
        return [], data
    p = 3
    j = data.index(':', p)
    n = int(data[p:j])
    p = j + 1
    lens = []
    for _ in range(n):
        j = data.index(':', p)
        lens.append(int(data[p:j]))
        p = j + 1
    toks = []
    for ln in lens:
        toks.append(data[p:p + ln])
        p += ln + 1
    return toks, data[p:]


class Parser(object):
    """name{key=value:...}, [a:b], scalars; every '#' is the next token."""
    def __init__(self, text, tokens):
        self.s, self.i, self.tok = text, 0, iter(tokens)

    def value(self):
        s = self.s
        if s.startswith('[', self.i):
            self.i += 1
            items = []
            while not s.startswith(']', self.i):
                items.append(self.value())
                if s.startswith(':', self.i):
                    self.i += 1
            self.i += 1
            return items
        m = re.compile(r'[^{}\[\]:=]*').match(s, self.i)
        word = m.group(0)
        self.i = m.end()
        if s.startswith('{', self.i):
            self.i += 1
            node = {'_': word}
            while not s.startswith('}', self.i):
                m = re.compile(r'(\w+)=').match(s, self.i)
                if not m:           # a bare word inside (not expected) - skip it
                    self.i += 1
                    continue
                self.i = m.end()
                node[m.group(1)] = self.value()
                if s.startswith(':', self.i):
                    self.i += 1
            self.i += 1
            return node
        if word == '#':
            return next(self.tok, '?')
        # a scalar may hold ',' (pos=1,2) - read up to the next separator
        return word


class Renderer(object):
    def __init__(self, labels):
        self.labels = labels

    def lab(self, t):
        m = re.match(r'_lid/(\d+)/(\d+)$', t or '')
        if m:
            return self.labels.get((m.group(1), int(m.group(2))), t)
        return t

    def arg(self, a):
        if not isinstance(a, dict):
            return self.lab(a)
        k = a['_']
        if k in ('l', 'pl'):
            return self.lab(a.get('id', '?'))
        if k == 'd':
            pre, num = a.get('s', '?'), a.get('a', '0')
            try:
                n = int(num)
                if pre in ('X', 'Y'):
                    num = '%o' % n
                elif pre in ('B', 'W', 'SB', 'SW'):
                    num = '%X' % n
                elif pre == 'Us':
                    return 'U%d' % n
            except ValueError:
                pass
            return pre + num
        if k == 'c':
            s, v = a.get('s', ''), a.get('v', '')
            if s == 'Dots':
                return v
            if s.startswith('H'):
                try:
                    return 'H%X' % int(v)
                except ValueError:
                    return 'H' + v
            if s.startswith('E'):
                return 'E' + v
            if s.startswith('"'):
                return s
            return 'K' + v if s.startswith('K') else (s + v)
        if k == 'M':
            return '%s.%s' % (self.arg(a.get('b')), self.arg(a.get('m')))
        if k == 'B':
            return '%s\\%s' % (self.arg(a.get('b')), self.arg(a.get('e')).replace('G', 'G', 1))
        if k == 'I':
            return '%s.%s' % (self.arg(a.get('b')), self.arg(a.get('m')))
        if k == 'A':
            idx = a.get('i') or []
            return '%s[%s]' % (self.arg(a.get('a')), ','.join(self.arg(x) for x in idx))
        return k + '{?}'

    def contact(self, op):
        inner = op.get('op', {}) if isinstance(op, dict) else {}
        return inner

    def element(self, e, ist):
        """('c', condition text) for a contact, ('o', output text) for a coil, an instruction or an ST box."""
        s = e.get('s')
        if not isinstance(s, dict):
            return None
        if s['_'] == 'ist':
            g = s.get('st', '')
            src = ist.get(g.replace('.I+ST', '').lower(), '')
            return ('o', 'ST {\n%s\n}' % src.strip() if src else 'ST ' + g)
        if s['_'] != 'ce':
            return None
        op = s.get('op', {})
        args = [self.arg(a) for a in (s.get('args') or [])]
        kind = op.get('_') if isinstance(op, dict) else ''
        name = op.get('op', '?') if isinstance(op, dict) else '?'
        edge = op.get('ct', 'a') if isinstance(op, dict) else 'a'
        if kind == 'ct':
            pre = {'p': '↑', 'f': '↓'}.get(edge, '')
            if name in CONTACTS:
                txt = CONTACTS[name] + (args[0] if args else '?')
                if name == 'b' and pre:
                    txt = pre + '/' + (args[0] if args else '?')
                elif pre:
                    txt = pre + txt
            elif len(args) == 2:
                txt = '(%s %s %s)' % (args[0], name, args[1])
            else:
                txt = '%s(%s)' % (name, ', '.join(args))
            return ('c', txt)
        if kind == 'cl':
            name = COILS.get(name, name)
            # a timer's coil (OUT T, its set value) and a word's or a timer's reset
            name = {'OUT__16': 'TIMER', 'OUTH__16': 'TIMER(fast)', 'RST__16': 'RST', 'RST__32': 'RST'}.get(name, name)
            return ('o', name + (' ' + ' '.join(args) if args else ''))
        return None


def AND(a, b):
    if a == 'TRUE':
        return b
    if b == 'TRUE':
        return a
    return '%s & %s' % (wrap(a), wrap(b))


def wrap(x):
    depth = 0
    for ch in x:
        if ch == '(':
            depth += 1
        elif ch == ')':
            depth -= 1
        elif ch == '|' and depth == 0:
            return '(' + x + ')'
    return x


def OR(xs):
    xs = list(dict.fromkeys(xs))
    if 'TRUE' in xs:
        return 'TRUE'
    return xs[0] if len(xs) == 1 else ' | '.join(xs)


def rung(tree, r, ist):
    fg = tree.get('fg', {}) if isinstance(tree, dict) else {}
    if not isinstance(fg, dict):
        return []
    try:
        w, h = [int(x) for x in str(fg.get('dim', '0x0')).split('x')]
    except ValueError:
        return []
    cells = {}
    for e in fg.get('es') or []:
        try:
            c, rr = [int(x) for x in str(e.get('pos', '0,0')).split(',')]
        except ValueError:
            continue
        el = r.element(e, ist)
        if el:
            cells[(c, rr)] = el
    verts = set()
    for v in fg.get('vs') or []:
        try:
            verts.add(tuple(int(x) for x in str(v.get('pos')).split(',')))
        except (ValueError, AttributeError):
            pass
    w = max([w] + [c + 2 for c, _ in cells])
    h = max([h] + [rr + 1 for _, rr in cells])
    power = {}           # (column, row): the condition that reaches that node, or absent
    level = set()        # the nodes power reached along their own row (not only down a vertical link)
    outs = []
    for c in range(w + 1):
        if c == 0:
            # the left rail feeds a row whose first element is at column 0, or one no vertical link
            # reaches before its first element (a line from the rail - lines are not stored)
            col = {}
            for rr in range(h):
                first = min([cc for cc, r2 in cells if r2 == rr] or [None]) if any(r2 == rr for _, r2 in cells) else None
                if first is None:
                    continue
                fed = any((cc, rr) in verts or (cc, rr + 1) in verts for cc in range(1, first + 1))
                if first == 0 or not fed:
                    col[rr] = 'TRUE'
                    level.add((0, rr))
        else:
            col = {}
            for rr in range(h):
                src = power.get((c - 1, rr))
                if src is None:
                    continue
                el = cells.get((c - 1, rr))
                if el is None:
                    # an empty cell: a line goes on along its row - but where the power only came down a
                    # vertical link and the next column's node is a link's too, the cell is blank (the link
                    # passes it by) - lines are not stored, so this is how a blank is told from a line
                    if (c - 1, rr) in level:
                        col[rr] = src
                        level.add((c, rr))
                    elif not ((c, rr) in verts or (c, rr + 1) in verts):
                        col[rr] = src
                elif el[0] == 'c':
                    col[rr] = AND(src, el[1])
                    level.add((c, rr))
            # vertical links join the nodes of a column into one
            groups = []
            rr = 0
            while rr < h:
                g = [rr]
                while (c, g[-1] + 1) in verts:
                    g.append(g[-1] + 1)
                groups.append(g)
                rr = g[-1] + 1
            for g in groups:
                got = [col[x] for x in g if x in col]
                if got:
                    joined = OR(got)
                    for x in g:
                        col[x] = joined
        for rr, v in col.items():
            power[(c, rr)] = v
        for rr in range(h):
            el = cells.get((c, rr))
            if el and el[0] == 'o' and (c, rr) in power:
                outs.append((power[(c, rr)], el[1]))
    return outs


def dump_ladder(path, r, out):
    con = sqlite3.connect(path)
    ist = {}
    for gid, data in con.execute('select id, data from IstBlocks'):
        ist[gid.replace('_guid/', '').replace('-', '').lower()] = (data or '').replace('\r\n', '\n')
    n = 0
    for bt, data in con.execute('select blocktype, data from LadderBlocks order by pos'):
        toks, body = split_block(data or '')
        if bt == 2:
            m = re.search(r'\[Title\]([^:]*)', data or '')
            if m:
                out.append('\n-- %s' % m.group(1).strip())
            else:
                t = [x for x in toks if x.strip()]
                if t:
                    out.append('   // ' + ' '.join(t))
            continue
        if bt != 0:
            continue
        n += 1
        try:
            tree = Parser(body, toks).value()
        except (StopIteration, ValueError, IndexError):
            out.append('%4d  (not read)' % n)
            continue
        if not isinstance(tree, dict):
            continue
        outs = rung(tree, r, ist)
        if not outs:
            continue
        by = collections.OrderedDict()
        for cond, o in outs:
            by.setdefault(cond, []).append(o)
        first = True
        for cond, os_ in by.items():
            lead = '%4d  ' % n if first else '      '
            first = False
            simple = [o for o in os_ if not o.startswith('ST {')]
            sts = [o for o in os_ if o.startswith('ST {')]
            if simple:
                out.append('%s%s  =>  %s' % (lead, cond, '; '.join(simple)))
                lead = '      '
            for s in sts:
                out.append('%s%s  =>  %s' % (lead, cond, s.replace('\n', '\n          ')))
                lead = '      '
    return n


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('gx3')
    ap.add_argument('out')
    ap.add_argument('--title', default='PLC program')
    a = ap.parse_args()
    folder, tmp = unpack(a.gx3)
    try:
        r = Renderer(label_names(folder))
        pous = []
        for path in glob.glob(os.path.join(folder, '*_LDDB.db')):
            out = []
            n = dump_ladder(path, r, out)
            body = [x for x in out if not x.startswith('\n--')]
            if body:
                titles = [x[4:] for x in out if x.startswith('\n--')]
                pous.append((-len('\n'.join(out)), os.path.basename(path)[:-8], titles, n, out))
        sts = []
        for path in glob.glob(os.path.join(folder, '*_STDB.db')):
            con = sqlite3.connect(path)
            src = '\n'.join((d or '').replace('\r\n', '\n') for (d,) in con.execute('select data from StBlocks order by pos'))
            if src.strip():
                sts.append((-len(src), os.path.basename(path)[:-8], src))
        with open(a.out, 'w', encoding='utf-8', newline='\n') as f:
            f.write('# %s - its program as text (tools/gx3_program.py): each rung its condition => what it drives\n' % a.title)
            f.write('# & and, | or, / not (a b contact), ↑ ↓ rising / falling edge; a label (device) where it has one;\n')
            f.write('# TIMER t K<n> a timer\'s coil (n x 100 ms), ST {...} an inline ST box (run while its condition is on).\n')
            f.write('# Read from the rungs\' grids: a blank cell is told from a line (not stored) by the links beside it - check a\n')
            f.write('# rung that matters in GX Works3.\n')
            f.write('# %d ladder POUs, %d ST POUs; a POU named by its database and its titles (its own name is not in these files)\n'
                    % (len(pous), len(sts)))
            for _, name, titles, n, out in sorted(pous):
                f.write('\n\n==== ladder %s (%d rungs)%s\n' % (name, n, (': ' + ', '.join(titles)) if titles else ''))
                f.write('\n'.join(out) + '\n')
            for _, name, src in sorted(sts):
                f.write('\n\n==== ST %s\n%s\n' % (name, src))
        print('%s: %d ladder POUs, %d ST POUs' % (a.out, len(pous), len(sts)))
    finally:
        if tmp:
            shutil.rmtree(tmp, ignore_errors=True)
    return 0


if __name__ == '__main__':
    sys.exit(main())
