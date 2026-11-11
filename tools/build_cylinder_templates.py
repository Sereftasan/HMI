#!/usr/bin/env python3
"""
Writes the cylinder step-by-step templates - one per valve and feedback pair,
per language - from the master templates in tools/cylinder_templates:

    tools/cylinder_templates/Cylinder_master.en.html  ->  KabanHMI/Help/Templates/EN/Cylinder_<valve>_<feedback>.html
    tools/cylinder_templates/Cylinder_master.tr.html  ->  KabanHMI/Help/Templates/TR/...

The master holds every pair; a pair's file keeps only what applies to its
valve and feedback ({{#V3}}, {{#F5}} ... are decided here), so a person reading
or changing "5/3 valve, pressure switch" sees only that.

A pair's file may be changed by hand. This script remembers what it wrote
(.generated beside the masters) and does not write over a file changed since -
it says which ones; carry the change into the master, or pass --force.

    python3 tools/build_cylinder_templates.py            # all languages
    python3 tools/build_cylinder_templates.py --check    # only say what would change
"""
import hashlib
import io
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
SRC = os.path.join(HERE, 'cylinder_templates')
OUT = os.path.join(ROOT, 'KabanHMI', 'Help', 'Templates')
STATE = os.path.join(SRC, '.generated')

VALVES = [  # digit, file part, words (EN, TR)
    (0, 'Valve-NotSet', 'valve type not set', 'valf tipi girilmemiş'),
    (1, '5-2-Double', '5/2 double solenoid', '5/2 çift bobinli'),
    (2, '5-2-Spring', '5/2 single solenoid, spring return', '5/2 tek bobinli, yay geri dönüşlü'),
    (3, '5-3', '5/3, centre position', '5/3, orta konumlu'),
]
FEEDBACKS = [
    (0, 'Feedback-NotSet', 'feedback not set (the unit\'s lamps, or none)', 'geri bildirim girilmemiş (ünitenin lambaları ya da hiçbiri)'),
    (1, 'OneMagnetic', 'one magnetic sensor', 'tek manyetik sensör'),
    (2, 'TwoMagnetic', 'two magnetic sensors (both ends)', 'iki manyetik sensör (iki uç)'),
    (3, 'OneInductive', 'one inductive sensor', 'tek endüktif sensör'),
    (4, 'Analogue', 'analogue position sensor (4-20 mA)', 'analog konum sensörü (4-20 mA)'),
    (5, 'PressureSwitch', 'magnetic sensor + pressure switch (clamping)', 'manyetik sensör + basınç anahtarı (sıkma)'),
    (6, 'Encoder', 'encoder + reference sensor', 'enkoder + referans sensörü'),
    (7, 'NoSensor', 'no sensor (nothing confirms the movement but the time)', 'sensör yok (hareketi yalnızca süre onaylar)'),
]


def flags(v, f):
    """The pair's own flags - decided in its file - and the ones that cannot be set for it."""
    d = {'V%d' % i: v == i for i in range(4)}
    d.update({'F%d' % i: f == i for i in range(8)})
    d.update(Double=v in (1, 3), FiveThree=v == 3, SpringValve=v == 2,
             PositionFb=f in (4, 6), MagneticFb=f in (1, 2), InductiveFb=f == 3, SwitchFb=f == 5)
    if v != 2:
        d['Spring'] = False          # only a spring valve has a spring movement
    if f != 5:
        d['IsSwitch'] = False        # only the pressure switch feedback has a switch
        d['OffSwitch'] = False
    return d


TAG = re.compile(r'\{\{([#^/])\s*([A-Za-z0-9_]+)\s*\}\}')
STANDALONE = re.compile(r'^[ \t]*(\{\{[#^/!][^}]*\}\}[ \t]*)+$')


def resolve(text, known):
    """Keeps or drops the sections whose flag is known; the others stay as they are."""
    out = []
    # (name, inverted, keep-children, emit-tags)
    stack = [(None, False, True, True)]
    lines = text.split('\n')
    for li, line in enumerate(lines):
        alone = bool(STANDALONE.match(line))
        pos = 0
        piece = []
        dropped = False
        for m in TAG.finditer(line):
            if stack[-1][2]:
                piece.append(line[pos:m.start()])
            elif line[pos:m.start()]:
                dropped = True
            pos = m.end()
            kind, name = m.group(1), m.group(2)
            parent_keep = stack[-1][2]
            if kind in '#^':
                if name in known:
                    keep = parent_keep and (known[name] if kind == '#' else not known[name])
                    stack.append((name, kind == '^', keep, False))
                else:
                    stack.append((name, kind == '^', parent_keep, True))
                    if parent_keep:
                        piece.append(m.group(0))
            else:
                top = stack.pop()
                if top[0] != name:
                    raise SystemExit('{{/%s}} closes {{%s}} (line %d)' % (name, top[0], li + 1))
                if top[3] and stack[-1][2]:
                    piece.append(m.group(0))
        if stack[-1][2]:
            piece.append(line[pos:])
        elif line[pos:]:
            dropped = True
        joined = ''.join(piece)
        if alone:
            # a line of section tags: keep it only for the tags still on it
            if joined.strip():
                out.append(joined.strip())
        else:
            if joined or (stack[-1][2] and not dropped):
                out.append(joined)
    if len(stack) != 1:
        raise SystemExit('{{#%s}} is not closed' % stack[-1][0])
    return '\n'.join(out)


def header(lang, v, f):
    if lang == 'tr':
        return ('<!--doc\n  KABAN HMI - SİLİNDİR HAREKETİ, ADIM ADIM - ŞABLON\n'
                '  Valf: %s.  Geri bildirim: %s.\n\n'
                '  Bu dosyayı HMI, bir silindir alarmının yardımı açılınca doldurur: {{...}} yerine\n'
                '  ünitenin kendi cihazları ve çizimlerin numaraları (makinenin Wiring.csv dosyası) gelir.\n'
                '  Sözcükler burada değiştirilebilir; {{...}} işaretleri ve <div class="step" ...> satırları\n'
                '  olduğu gibi kalmalıdır. Sözdizimi: tools/cylinder_templates/README.md.\n'
                '  tools/build_cylinder_templates.py tüm çiftleri tools/cylinder_templates/Cylinder_master.tr.html\n'
                '  dosyasından yeniden yazar (elle değiştirilmiş bir dosyanın üzerine yazmaz).\n-->\n') % (v[3], f[3])
    return ('<!--doc\n  KABAN HMI - CYLINDER MOVEMENT, STEP BY STEP - TEMPLATE\n'
            '  Valve: %s.  Feedback: %s.\n\n'
            '  The HMI fills this file when a cylinder alarm\'s help is opened: the {{...}} get the\n'
            '  unit\'s own devices and the drawings\' numbers (the machine\'s Wiring.csv).\n'
            '  The words may be changed here; the {{...}} marks and the <div class="step" ...> lines\n'
            '  must stay as they are. Syntax: tools/cylinder_templates/README.md.\n'
            '  tools/build_cylinder_templates.py writes every pair again from\n'
            '  tools/cylinder_templates/Cylinder_master.en.html (not over a file changed by hand).\n-->\n') % (v[2], f[2])


PROJECTS = ['Kaban.Machines.FA2010', 'Kaban.Machines.H500', 'Kaban.Machines.H800', 'Kaban.Machines.MD3020', 'Kaban.Machines.MK2010']


def update_projects():
    """Each machine project copies every template beside the program (Help\\Templates\\<language>\\...)."""
    files = []
    for lang in sorted(os.listdir(OUT)):
        folder = os.path.join(OUT, lang)
        if os.path.isdir(folder):
            files += ['%s\\%s' % (lang, f) for f in sorted(os.listdir(folder)) if f.endswith('.html')]
    marks = re.compile(r'(<!-- templates:begin.*?-->\r?\n)(.*?)([ \t]*<!-- templates:end -->)', re.S)
    for name in PROJECTS + ['KabanHMI/Kaban.UI']:
        path = os.path.join(ROOT, name + '.csproj') if '/' in name else os.path.join(ROOT, name, name + '.csproj')
        raw = io.open(path, 'rb').read()
        nl = '\r\n' if b'\r\n' in raw else '\n'
        text = raw.decode('utf-8-sig')
        if name.endswith('Kaban.UI'):
            items = ''.join('    <None Include="Help\\Templates\\%s" />%s' % (f, nl) for f in files)
        else:
            items = ''.join('    <None Include="..\\KabanHMI\\Help\\Templates\\%s">%s'
                            '      <Link>Help\\Templates\\%s</Link>%s'
                            '      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>%s'
                            '      <Visible>false</Visible>%s'
                            '    </None>%s' % (f, nl, f, nl, nl, nl, nl) for f in files)
        new, n = marks.subn(lambda m: m.group(1) + items + m.group(3), text)
        if n != 1:
            print('  no templates:begin / templates:end marks in ' + path)
            continue
        if new != text:
            data = new.encode('utf-8')
            if raw.startswith(b'\xef\xbb\xbf'):
                data = b'\xef\xbb\xbf' + data
            io.open(path, 'wb').write(data)
            print('  ' + os.path.relpath(path, ROOT) + ': %d templates' % len(files))


def include(text):
    """<!--include Drawings.html--> -> that file of the masters' folder, without its own note."""
    def one(m):
        part = io.open(os.path.join(SRC, m.group(1)), encoding='utf-8').read().replace('\r\n', '\n')
        part = re.sub(r'<!--doc.*?-->\n?', '', part, count=1, flags=re.S)
        return part.rstrip('\n')
    return re.sub(r'<!--include ([A-Za-z0-9_.-]+)-->', one, text)


def sha(text):
    return hashlib.sha1(text.encode('utf-8')).hexdigest()


def main():
    check = '--check' in sys.argv
    force = '--force' in sys.argv
    state = {}
    if os.path.exists(STATE):
        state = json.load(io.open(STATE, encoding='utf-8'))
    changed, kept, refused = 0, 0, []
    for lang in ('en', 'tr'):
        master = os.path.join(SRC, 'Cylinder_master.%s.html' % lang)
        if not os.path.exists(master):
            continue
        text = io.open(master, encoding='utf-8').read().replace('\r\n', '\n')
        text = re.sub(r'<!--doc.*?-->\n?', '', text, count=1, flags=re.S)
        text = include(text)
        folder = os.path.join(OUT, lang.upper())
        os.makedirs(folder, exist_ok=True)
        for v in VALVES:
            for f in FEEDBACKS:
                name = 'Cylinder_%s_%s.html' % (v[1], f[1])
                path = os.path.join(folder, name)
                body = header(lang, v, f) + resolve(text, flags(v[0], f[0]))
                rel = lang.upper() + '/' + name
                if os.path.exists(path):
                    now = io.open(path, encoding='utf-8').read().replace('\r\n', '\n')
                    if now == body:
                        kept += 1
                        continue
                    if not force and state.get(rel) and state[rel] != sha(now):
                        refused.append(rel)
                        continue
                changed += 1
                if not check:
                    with io.open(path, 'w', encoding='utf-8', newline='\r\n') as out:
                        out.write(body)
                    state[rel] = sha(body)
    if not check:
        with io.open(STATE, 'w', encoding='utf-8') as out:
            json.dump(state, out, indent=1, sort_keys=True)
        update_projects()
    print('%d written, %d unchanged%s' % (changed, kept, ' (check only)' if check else ''))
    for r in refused:
        print('  NOT written - changed by hand since it was made: ' + r)
    return 1 if refused else 0


if __name__ == '__main__':
    sys.exit(main())
