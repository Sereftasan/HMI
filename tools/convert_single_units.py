#!/usr/bin/env python3
"""
One-button "units" become the controls they are (29 September).

CylinderUnitHMI (was FunctionUnitHMI) is for cylinders. The generated manual
pages also used it for plain functions - a motor, a cooling, a horn, a reset,
an inverter's error code - with one button or none. This turns each of those
into what it is, in place, keeping its bits and actions:

  * one command            -> ButtonHMI: the same bit with the same action
                              (Momentary / Alternate / SET ...), the same lamp,
                              relay and output, security, logging, conditions;
                              caption = the unit's title
  * one command + a value  -> that ButtonHMI and a NumericDisplayHMI beside it
  * no command, a value    -> a NumericDisplayHMI with the title as its label
                              (the inverters' error codes)

A unit stays a cylinder unit when it has BOTH commands, or anything only a
cylinder has: an error bit (its '?' help needs the unit), a valve or feedback
type, or a lamp on the side without a command (the second end sensor of a
single-solenoid cylinder).

The designer code is written the way Visual Studio writes it (a local
BitAction variable per action), so the pages open in the designer.
Run from the repository root; prints what it changed. Idempotent.
"""
import glob, re, sys

CTL = "Kaban.UI.Controls."
CYL = CTL + "CylinderUnitHMI"


def props_of(src, name):
    """Ordered (prop, value, line) of this.<name>.<prop> = value; (single-line assignments)."""
    out = []
    for m in re.finditer(r'^[ \t]*this\.' + re.escape(name) + r'\.(\w+)((?:\.\w+)*) = (.+?);\r?\n', src, re.M):
        # this.X.Prop = v;  and the designer's  this.X.Prop.Sub = v;  (nested objects: triggers, offsets)
        out.append((m.group(1) + m.group(2), m.group(3), m.group(0)))
    return out


def s(v):
    """The value of a C# string literal ('' for null / "null")."""
    v = v.strip()
    if v == 'null':
        return ''
    if v.startswith('"') and v.endswith('"'):
        v = v[1:-1]
    return '' if v == 'null' else v


def is_cylinder(p):
    get = lambda k: s(p.get(k, '""'))
    a, b = get('CommandAAddress'), get('CommandBAddress')
    if a and b:
        return True
    if get('ErrorAAddress') or get('ErrorBAddress'):
        return True
    if 'ValveType' in p or 'FeedbackType' in p:
        return True
    # A lamp on the side that has no command: a cylinder's other end sensor.
    side = 'A' if a else 'B'
    other = 'B' if side == 'A' else 'A'
    if get('Lamp1' + other + 'Address') or get('Lamp2' + other + 'Address'):
        return True
    return False


def convert_file(path, log):
    raw = open(path, 'rb').read()
    bom = raw[:3] == b'\xef\xbb\xbf'
    src = raw.decode('utf-8-sig')
    crlf = '\r\n' in src
    src = src.replace('\r\n', '\n')
    names = re.findall(r'this\.(\w+) = new ' + re.escape(CYL) + r'\(\);', src)
    if not names:
        return False
    used = set(int(n) for n in re.findall(r'\bbitAction(\d+)\b', src))
    next_action = [max(used) + 1 if used else 1]
    locals_decl = []
    changed = False

    for name in names:
        plist = props_of(src, name)
        p = {k: v for k, v, _ in plist}
        if is_cylinder(p):
            continue
        get = lambda k: s(p.get(k, '""'))
        side = 'A' if get('CommandAAddress') else 'B'
        cmd = get('Command' + side + 'Address')
        action = p.get('Command' + side + 'Action', 'Kaban.Core.Enums.BitActionType.Momentary')
        title = get('TitleTextKey')
        loc = re.match(r'new System\.Drawing\.Point\((\-?\d+), (\-?\d+)\)', p.get('Location', 'new System.Drawing.Point(0, 0)'))
        size = re.match(r'new System\.Drawing\.Size\((\d+), (\d+)\)', p.get('Size', 'new System.Drawing.Size(360, 64)'))
        x, y = int(loc.group(1)), int(loc.group(2))
        w, h = int(size.group(1)), int(size.group(2))
        bh = 40
        by = y + max(0, (h - bh) // 2)
        value = get('PositionAddress')
        ind = '            '
        common = []
        if 'RequiredSecurity' in p: common.append(('RequiredSecurity', p['RequiredSecurity']))
        if 'LogAction' in p: common.append(('LogAction', p['LogAction']))

        def display_block(var, dx, dw, label):
            b = [ind + '// ', ind + '// ' + var + '  (was a one-button unit: its value)', ind + '// ']
            b.append(ind + 'this.%s.Name = "%s";' % (var, var))
            b.append(ind + 'this.%s.PlcNumber = %s;' % (var, p.get('PlcNumber', '1')))
            b.append(ind + 'this.%s.Address = "%s";' % (var, value))
            if 'PositionDataType' in p: b.append(ind + 'this.%s.DataType = %s;' % (var, p['PositionDataType']))
            if 'PositionDivisor' in p: b.append(ind + 'this.%s.Divisor = %s;' % (var, p['PositionDivisor']))
            if 'PositionDecimalPlaces' in p: b.append(ind + 'this.%s.DecimalPlaces = %s;' % (var, p['PositionDecimalPlaces']))
            if 'PositionDigits' in p: b.append(ind + 'this.%s.Digits = %s;' % (var, p['PositionDigits']))
            # No "mm" and no length unit: the generator put them on every value,
            # and neither an inverter's error code nor a motor's value is a length
            # (one converted to inches would even show a wrong number).
            if label: b.append(ind + 'this.%s.DescriptionText = "%s";' % (var, label))
            if 'VisibilityConditions' in p: b.append(ind + 'this.%s.VisibilityConditions = %s;' % (var, p['VisibilityConditions']))
            b.append(ind + 'this.%s.Location = new System.Drawing.Point(%d, %d);' % (var, x + dx, by))
            b.append(ind + 'this.%s.Size = new System.Drawing.Size(%d, %d);' % (var, dw, bh))
            return b

        block = []
        new_type = CTL + 'ButtonHMI'
        extra = None
        if cmd:
            bw = w if not value else w - 130
            n = next_action[0]; next_action[0] += 1
            local = 'bitAction%d' % n
            locals_decl.append(ind + 'Kaban.Core.Models.BitAction %s = new Kaban.Core.Models.BitAction();' % local)
            block += [ind + 'this.%s.Name = "%s";' % (name, name),
                      ind + 'this.%s.PlcNumber = %s;' % (name, p.get('PlcNumber', '1')),
                      ind + 'this.%s.TranslationKey = "%s";' % (name, title),
                      ind + 'this.%s.FallbackText = "%s";' % (name, title),
                      ind + '%s.Address = "%s";' % (local, cmd),
                      ind + '%s.Action = %s;' % (local, action),
                      ind + 'this.%s.BitActions.Add(%s);' % (name, local)]
            for src_p, dst_p in (('Lamp1%sAddress', 'Lamp1Address'), ('Lamp1%sTipKey', 'Lamp1Tip'),
                                 ('Lamp2%sAddress', 'Lamp2Address'), ('Lamp2%sTipKey', 'Lamp2Tip'),
                                 ('Relay%sAddress', 'RelayAddress'), ('Output%sAddress', 'OutputAddress'),
                                 ('Button%sTipKey', 'ToolTipText')):
                v = get(src_p % side)
                if v: block.append(ind + 'this.%s.%s = "%s";' % (name, dst_p, v))
            for k in ('VisibilityConditions', 'EnableConditions'):
                own, btn = p.get(k, ''), p.get('Button' + side + k, '')
                vals = [v for v in (own, btn) if v and not v.endswith('.None')]
                if vals: block.append(ind + 'this.%s.%s = %s;' % (name, k, ' | '.join(vals)))
            if p.get('ConfirmationNeeded', 'false') == 'true':
                block.append(ind + 'this.%s.ConfirmationNeeded = true;' % name)
            block.append(ind + 'this.%s.Location = new System.Drawing.Point(%d, %d);' % (name, x, by))
            block.append(ind + 'this.%s.Size = new System.Drawing.Size(%d, %d);' % (name, bw, bh))
            for k, v in common: block.append(ind + 'this.%s.%s = %s;' % (name, k, v))
            if value:
                extra = name + '_Value'
                extra_block = display_block(extra, bw + 10, 120, '')
        else:
            new_type = CTL + 'NumericDisplayHMI'
            if not value:
                log.append('  %s: %s has neither a command nor a value - left as it is' % (path, name))
                continue
            block = display_block(name, 210, w - 210, title)[3:]

        # The type: instantiation and field.
        src = src.replace('this.%s = new %s();' % (name, CYL), 'this.%s = new %s();' % (name, new_type)
                          + ('\n' + ind + 'this.%s = new %s();' % (extra, CTL + 'NumericDisplayHMI') if extra else ''), 1)
        src = re.sub(r'(private )' + re.escape(CYL) + r'( ' + re.escape(name) + r';)',
                     r'\g<1>' + new_type + r'\2' + ('\n        private %sNumericDisplayHMI %s;' % (CTL, extra) if extra else ''),
                     src, count=1)
        # The properties: the old lines out, the new block where the first one was.
        first = plist[0][2]
        at = src.index(first)
        for _, _, line in plist:
            src = src.replace(line, '', 1)
        src = src[:at] + '\n'.join(block) + '\n' + src[at:]
        if extra:
            add = ind + 'this.Controls.Add(this.%s);\n' % name
            i = src.index(add) + len(add)
            src = src[:i] + '\n'.join(extra_block) + '\n' + ind + 'this.Controls.Add(this.%s);\n' % extra + src[i:]
        log.append('  %s: %s -> %s%s' % (path.split('/')[-1], name, new_type.split('.')[-1], ' + ' + extra if extra else ''))
        changed = True

    if locals_decl:
        m = re.search(r'private void InitializeComponent\(\)\n\s*\{\n', src)
        src = src[:m.end()] + '\n'.join(locals_decl) + '\n' + src[m.end():]
    if changed:
        if crlf:
            src = src.replace('\n', '\r\n')
        open(path, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + src.encode('utf-8'))
    return changed


if __name__ == '__main__':
    log = []
    files = sorted(glob.glob('Kaban.Machines.*/Screens/**/*.Designer.cs', recursive=True))
    n = sum(1 for f in files if convert_file(f, log))
    print('\n'.join(log))
    print('%d file(s) changed' % n)
