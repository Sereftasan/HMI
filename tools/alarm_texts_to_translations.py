"""The alarm texts from each machine's Alarms.json into its Translations.csv (30 October).

22 October: the alarm texts in Alarms.json were "inherited from the Industry 4 program, kept as they are for
now ... The two alarm handlings - Industry 4 logging and the HMI's - are to be combined when Industry 4 logging
comes into this program; the alarm texts go to the translation files then." - 30 October: "Yes".

For every Kaban.Machines.*/Alarms.json:
  - each alarm's texts become a row of the machine's Translations.csv, its key the one the HMI shows it by:
    Alarm_<ModuleId>.<TagCode> (the loader scopes an alarm's code by its machine);
  - English as the HMI had it; the other languages from the Industry 4 program's own translations of the
    alarm's bit (Dashboard/Translation_JSON.csv - its Turkish is the operators' words, "Robot X Arıza", where
    Alarms.json had the PLC's tag names, "Eksen1_Ariza") when the machine has them, else Alarms.json's;
  - Alarms.json keeps the alarm itself - its device, code, severity, help - and gets "Log": where the PLC's log
    records it (the state code, its value's name, the bit), read from the engineer's comment
    ("StateCode 4 / First 32 Errors (1) bit 0"); its "Translations" go.
Run again it changes nothing (a file without texts is left as it is; the rows are written once, in place).

    python3 tools/alarm_texts_to_translations.py [repository folder]
"""
import json, os, re, sys

LANGS = ['EN', 'TR', 'RU', 'DE', 'IT', 'ES']
COMMENT = re.compile(r'^\s*StateCode\s+(\d+)\s*/\s*(.+?)\s+bit\s+(\d+)\s*$', re.I)


def read_text(path):
    raw = open(path, 'rb').read()
    bom = raw.startswith(b'\xef\xbb\xbf')
    text = raw[3:].decode('utf-8') if bom else raw.decode('utf-8')
    return text, bom, '\r\n' in text


def write_text(path, text, bom, crlf):
    text = text.replace('\r\n', '\n')
    if crlf: text = text.replace('\n', '\r\n')
    open(path, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + text.encode('utf-8'))


def module_id(folder):
    for name in os.listdir(folder):
        if not name.endswith('Module.cs'): continue
        src = open(os.path.join(folder, name), encoding='utf-8-sig').read()
        m = re.search(r'ModuleId\s*(?:=>|\{\s*get\s*\{\s*return)\s*"([^"]+)"', src)
        if m: return m.group(1)
    return os.path.basename(folder).split('.')[-1]


def log_place(comment):
    m = COMMENT.match(comment or '')
    return {'Code': int(m.group(1)), 'Value': m.group(2).strip(), 'Bit': int(m.group(3))} if m else None


def industry4(folder):
    """(code, value, bit) -> the bit's name, and name -> language -> text (the dashboard's own files)."""
    smap = os.path.join(folder, 'Dashboard', 'StateMap.json')
    tj = os.path.join(folder, 'Dashboard', 'Translation_JSON.csv')
    if not (os.path.exists(smap) and os.path.exists(tj)): return {}, {}
    bits = {}
    for e in json.loads(read_text(smap)[0]):
        for p in e.get('Parameters') or []:
            for i, b in enumerate(p.get('Bits') or []):
                bits[(e['StateCode'], p.get('Description', ''), i)] = b
    lines = read_text(tj)[0].splitlines()
    names = {}
    for line in lines[1:]:
        f = line.split(';')
        if len(f) < 3: continue
        # Reference;English;Türkçe;Русский;Deutsch;Italiano;Español
        names[f[0]] = {lang: (f[i + 1].strip() if i + 1 < len(f) else '') for i, lang in enumerate(LANGS)}
    return bits, names


def migrate(folder):
    path = os.path.join(folder, 'Alarms.json')
    csv = os.path.join(folder, 'Translations.csv')
    if not os.path.exists(path) or not os.path.exists(csv): return 0
    text, bom, crlf = read_text(path)
    entries = json.loads(text)
    if not any(e.get('Translations') for e in entries): return 0
    mod = module_id(folder)
    bits, names = industry4(folder)
    rows = []
    out = []
    for e in entries:
        place = e.get('Log') or log_place(e.get('Comment'))
        texts = dict((k.upper(), v) for k, v in (e.get('Translations') or {}).items())
        if texts:
            row = {lang: (texts.get(lang) or '').strip() for lang in LANGS}
            ref = bits.get((place['Code'], place['Value'], place['Bit'])) if place else None
            known = names.get(ref) if ref else None
            if known:
                for lang in LANGS[1:]:
                    if known.get(lang): row[lang] = known[lang]
            for lang in LANGS:
                assert ';' not in row[lang] and '\n' not in row[lang], (e['TagCode'], lang, row[lang])
            rows.append(('Alarm_' + mod + '.' + e['TagCode'].strip(), row))
        kept = {}
        for k, v in e.items():
            if k == 'Translations': continue
            kept[k] = v
            if k == 'PlcNumber' and place and 'Log' not in e: kept['Log'] = place
        if place and 'Log' not in kept: kept['Log'] = place
        out.append(kept)

    # the rows: where a key is already, in place; the others at the end, in the alarms' order
    ctext, cbom, ccrlf = read_text(csv)
    lines = ctext.replace('\r\n', '\n').split('\n')
    while lines and lines[-1] == '': lines.pop()
    head = lines[0].split(';')
    cols = [h.strip().upper() for h in head[1:]]
    at = {l.split(';', 1)[0]: i for i, l in enumerate(lines)}
    for key, row in rows:
        line = ';'.join([key] + [row.get(c, '') for c in cols])
        if key in at: lines[at[key]] = line
        else: lines.append(line)
    write_text(csv, '\n'.join(lines) + '\n', cbom, ccrlf)
    write_text(path, json.dumps(out, indent=2, ensure_ascii=False) + '\n', bom, crlf)
    return len(rows)


def main():
    root = sys.argv[1] if len(sys.argv) > 1 else os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    for name in sorted(os.listdir(root)):
        if not name.startswith('Kaban.Machines.'): continue
        n = migrate(os.path.join(root, name))
        print('%-24s %d alarm text(s) into Translations.csv' % (name, n))


if __name__ == '__main__':
    main()
