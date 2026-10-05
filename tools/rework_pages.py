#!/usr/bin/env python3
"""
Reworks the machines' designer pages. Every step is exact and reported; the
steps that GUESS (error bits by name) are reported for review.

  axes     number each machine's AxisSettingHMI rows 1..N
  convert  a manual unit whose two jog addresses EXACTLY equal an axis row's
           becomes an AxisUnitHMI of that number; a single lamp on it becomes
           that row's reference sensor, on the same side
  resize   H800 / MD3020 units to FA2010's 360x64 on FA2010's grid
  h500     H500's loose command buttons rebuilt as FA2010-style units
  errors   error bits matched to units and axes by alarm name  (review!)
"""
import io, os, re, sys, json, glob

ROOT = sys.argv[1] if len(sys.argv) > 1 else "."
CTRL = "Kaban.UI.Controls."
GRID_X = (16, 526)            # FA2010's two columns
GRID_Y0, GRID_PITCH = 16, 74  # FA2010's first row and row pitch
UNIT_SIZE = (360, 64)


# ---------------------------------------------------------------- designer IO
class Page:
    """A designer file, read as text with its line endings remembered."""
    def __init__(self, path):
        self.path = path
        raw = io.open(path, encoding="utf-8-sig", newline="").read()
        self.eol = "\r\n" if "\r\n" in raw else "\n"
        self.text = raw.replace("\r\n", "\n")
        self.changed = False

    def save(self):
        if self.changed:
            io.open(self.path, "w", encoding="utf-8-sig", newline="").write(self.text.replace("\n", self.eol))

    def controls(self, typename):
        return re.findall(r"this\.(\w+) = new (?:%s)?%s\(\);" % (re.escape(CTRL), typename), self.text)

    def prop(self, var, name):
        m = re.search(r"this\.%s\.%s = (.+?);\n" % (re.escape(var), re.escape(name)), self.text)
        return m.group(1).strip() if m else None

    def str_prop(self, var, name):
        v = self.prop(var, name)
        if v is None or v == "null": return ""
        m = re.fullmatch(r'"(.*)"', v)
        return m.group(1) if m else v

    def set_prop(self, var, name, value_expr, after=None):
        """Sets or adds `this.var.name = value;` - added after the given property, or the Name line."""
        line = "this.%s.%s = %s;" % (var, name, value_expr)
        pat = r"([ \t]*)this\.%s\.%s = .+?;\n" % (re.escape(var), re.escape(name))
        if re.search(pat, self.text):
            self.text = re.sub(pat, lambda m: m.group(1) + line + "\n", self.text, count=1)
        else:
            anchor = after or "Name"
            m = re.search(r"([ \t]*)this\.%s\.%s = .+?;\n" % (re.escape(var), re.escape(anchor)), self.text)
            if not m:
                m = re.search(r"([ \t]*)this\.%s\.\w+ = .+?;\n" % re.escape(var), self.text)
            self.text = self.text[:m.end()] + m.group(1) + line + "\n" + self.text[m.end():]
        self.changed = True

    def drop_props(self, var, keep):
        def keep_or_drop(m):
            return m.group(0) if m.group(2) in keep else ""
        self.text = re.sub(r"([ \t]*)this\.%s\.(\w+)(?: = .+?|\..+?);\n" % re.escape(var), keep_or_drop, self.text)
        self.changed = True

    def retype(self, var, old, new):
        """Changes a control's type in its field declaration and its construction."""
        for a, b in [("private %s%s %s;" % (CTRL, old, var), "private %s%s %s;" % (CTRL, new, var)),
                     ("this.%s = new %s%s();" % (var, CTRL, old), "this.%s = new %s%s();" % (var, CTRL, new))]:
            assert a in self.text, "%s: cannot retype %s" % (self.path, a)
            self.text = self.text.replace(a, b)
        self.changed = True


def machine_pages(machine, sub="Screens"):
    return [Page(p) for p in sorted(glob.glob(os.path.join(ROOT, "Kaban.Machines.%s" % machine, sub, "**", "*.Designer.cs"), recursive=True))]


# ---------------------------------------------------------------- 1. axes
def number_axes(machine, log):
    axes = []
    for page in machine_pages(machine):
        for var in page.controls("AxisSettingHMI"):
            n = len(axes) + 1
            page.set_prop(var, "HMIAxisIndex", str(n), after="AxisNameKey")   # the row's (it was AxisNumber)
            axes.append({"page": page, "var": var, "number": n,
                         "station": int(page.prop(var, "PlcNumber") or 1),
                         "name": page.str_prop(var, "AxisNameKey"),
                         "back": page.str_prop(var, "CommandBackAddress"),
                         "forward": page.str_prop(var, "CommandForwardAddress")})
        page.save()
    log.append("%s: %d axis rows numbered 1..%d" % (machine, len(axes), len(axes)))
    return axes


# ---------------------------------------------------------------- 2. convert
def convert_axis_units(machine, axes, log, review):
    by_pair = {}
    for a in axes:
        if a["back"] and a["forward"]:
            by_pair[(a["station"], a["back"].upper(), a["forward"].upper())] = (a, False)
            by_pair[(a["station"], a["forward"].upper(), a["back"].upper())] = (a, True)
    converted = 0
    for page in machine_pages(machine):
        for var in page.controls("CylinderUnitHMI"):
            key = (int(page.prop(var, "PlcNumber") or 1),
                   page.str_prop(var, "CommandAAddress").upper(), page.str_prop(var, "CommandBAddress").upper())
            hit = by_pair.get(key)
            if not hit: continue
            axis, reversed_ = hit
            lamp_a, lamp_b = page.str_prop(var, "Lamp1AAddress"), page.str_prop(var, "Lamp1BAddress")
            # the unit's single lamp is the reference sensor; A is the back side
            ref, side = "", None
            if lamp_a and not lamp_b: ref, side = lamp_a, ("Forward" if reversed_ else "Back")
            elif lamp_b and not lamp_a: ref, side = lamp_b, ("Back" if reversed_ else "Forward")
            elif lamp_a and lamp_b:
                review.append("| %s | %s | lamps on BOTH sides (%s, %s) - reference sensor not set, choose one |"
                              % (machine, var, lamp_a, lamp_b))
            if ref:
                ap = axis["page"]; ap.__init__(ap.path)   # re-read: it may have been saved since
                ap.set_prop(axis["var"], "ReferenceSensorAddress", '"%s"' % ref, after="HMIAxisIndex")
                ap.set_prop(axis["var"], "ReferenceSensorSide", "Kaban.UI.Controls.AxisReferenceSide." + side,
                            after="ReferenceSensorAddress")
                ap.save()
            page.drop_props(var, {"Location", "Size", "Name", "TabIndex", "PlcNumber"})
            page.retype(var, "CylinderUnitHMI", "AxisUnitHMI")
            page.set_prop(var, "AxisNumber", str(axis["number"]), after="Name")
            converted += 1
            log.append("  %s %s -> axis %d '%s'%s%s" % (os.path.basename(page.path), var, axis["number"], axis["name"],
                       " (buttons were reversed)" if reversed_ else "",
                       ", reference sensor %s on the %s side" % (ref, side.lower()) if ref else ""))
        page.save()
    log.append("%s: %d manual units became axis units" % (machine, converted))


# ---------------------------------------------------------------- 3. resize
def regrid(page, vars_in_order, log, what):
    """FA2010's layout: two columns, 74 px pitch, starting where the first unit was."""
    ys = [int(m.group(2)) for v in vars_in_order
          for m in [re.search(r"this\.%s\.Location = new System\.Drawing\.Point\((\d+), (\d+)\);" % v, page.text)] if m]
    y0 = min(ys) if ys else GRID_Y0
    for i, v in enumerate(vars_in_order):
        x, y = GRID_X[i % 2], y0 + (i // 2) * GRID_PITCH
        page.set_prop(v, "Location", "new System.Drawing.Point(%d, %d)" % (x, y))
        page.set_prop(v, "Size", "new System.Drawing.Size(%d, %d)" % UNIT_SIZE)
    log.append("  %s: %d %s -> 360x64 on the FA2010 grid" % (os.path.basename(page.path), len(vars_in_order), what))


def resize_units(machine, log):
    for page in machine_pages(machine):
        units = page.controls("CylinderUnitHMI") + page.controls("AxisUnitHMI")
        if not units: continue
        def pos(v):
            m = re.search(r"this\.%s\.Location = new System\.Drawing\.Point\((\d+), (\d+)\);" % v, page.text)
            return (int(m.group(2)), int(m.group(1))) if m else (0, 0)
        regrid(page, sorted(units, key=pos), log, "units")
        page.save()


# ---------------------------------------------------------------- 4. H500
def rebuild_h500(log):
    """
    H500's manual page was a column of loose 300x36 buttons. Rebuilt as FA2010
    units: two buttons of the same caption in a row become one two-button unit
    (A then B); a single command becomes a one-button unit on the B side, whose
    forward arrow reads as "do it". Same 360x64 size, same grid.
    """
    for page in [Page(p) for p in sorted(glob.glob(os.path.join(ROOT, "Kaban.Machines.H500", "Screens", "Manual*.Designer.cs")))]:
        buttons = []
        for v in page.controls("ButtonHMI"):
            act = re.search(r'this\.%s\.BitActions\.Add\(new [\w.]*BitAction \{ Address = "([^"]*)", Action = ([\w.]+) \}\);' % v, page.text)
            buttons.append({"var": v, "text": page.str_prop(v, "FallbackText") or v,
                            "addr": act.group(1) if act else "", "action": act.group(2) if act else "Kaban.Core.Enums.BitActionType.Momentary",
                            "extra": len(re.findall(r"this\.%s\.BitActions\.Add" % v, page.text)) - 1,
                            "lamp": page.str_prop(v, "Lamp1Address"), "plc": page.prop(v, "PlcNumber") or "1",
                            "sec": page.prop(v, "RequiredSecurity"), "log": page.prop(v, "LogAction")})
        if not buttons: continue
        groups, i = [], 0
        while i < len(buttons):
            if i + 1 < len(buttons) and buttons[i]["text"] == buttons[i + 1]["text"]:
                groups.append((buttons[i], buttons[i + 1])); i += 2
            else:
                groups.append((None, buttons[i])); i += 1
        # ---- remove the loose buttons
        first_new = page.text.index("this.%s = new " % buttons[0]["var"])
        first_block = page.text.index("// %s\n" % buttons[0]["var"]) 
        first_block = page.text.rfind("\n", 0, page.text.rfind("//", 0, first_block)) + 1
        first_decl = page.text.index("private %sButtonHMI %s;" % (CTRL, buttons[0]["var"]))
        first_decl = page.text.rfind("\n", 0, first_decl) + 1
        for b in buttons:
            v = re.escape(b["var"])
            page.text = re.sub(r"[ \t]*this\.%s = new [\w.]*ButtonHMI\(\);\n" % v, "\x01NEW\x01" if b is buttons[0] else "", page.text, count=1)
            page.text = re.sub(r"[ \t]*//[ \t]*\n[ \t]*// %s\n[ \t]*//[ \t]*\n" % v, "\x01BLK\x01" if b is buttons[0] else "", page.text, count=1)
            page.text = re.sub(r"[ \t]*this\.%s\.[^\n]*\n" % v, "", page.text)
            page.text = re.sub(r"[ \t]*this\.Controls\.Add\(this\.%s\);\n" % v, "", page.text)
            page.text = re.sub(r"[ \t]*private [\w.]*ButtonHMI %s;\n" % v, "\x01DCL\x01" if b is buttons[0] else "", page.text, count=1)
        # ---- the units
        news, blocks, decls, used = [], [], [], set()
        ind = "            "
        for n, (a, b) in enumerate(groups):
            base = "u" + re.sub(r"\W+", "_", b["text"]).strip("_")
            var = base; k = 2
            while var in used: var = "%s%d" % (base, k); k += 1
            used.add(var)
            x, y = GRID_X[n % 2], GRID_Y0 + (n // 2) * GRID_PITCH
            L = ["// ", "// " + var, "// ",
                 'this.%s.Name = "%s";' % (var, var), "this.%s.PlcNumber = %s;" % (var, b["plc"]),
                 'this.%s.TitleTextKey = "%s";' % (var, b["text"])]
            for side, btn in (("A", a), ("B", b)):
                if btn is None: continue
                L.append('this.%s.Command%sAddress = "%s";' % (var, side, btn["addr"]))
                L.append("this.%s.Command%sAction = %s;" % (var, side, btn["action"]))
                if btn["lamp"]: L.append('this.%s.Lamp1%sAddress = "%s";' % (var, side, btn["lamp"]))
                if btn["extra"] > 0: log.append("  NOTE %s: button '%s' had %d more bit action(s) - only the first is kept" % (var, btn["var"], btn["extra"]))
            if b["sec"]: L.append("this.%s.RequiredSecurity = %s;" % (var, b["sec"]))
            if b["log"]: L.append("this.%s.LogAction = %s;" % (var, b["log"]))
            L += ["this.%s.Location = new System.Drawing.Point(%d, %d);" % (var, x, y),
                  "this.%s.Size = new System.Drawing.Size(%d, %d);" % (var, UNIT_SIZE[0], UNIT_SIZE[1]),
                  "this.Controls.Add(this.%s);" % var]
            news.append(ind + "this.%s = new %sCylinderUnitHMI();" % (var, CTRL))
            blocks.append("\n".join(ind + l for l in L))
            decls.append("        private %sCylinderUnitHMI %s;" % (CTRL, var))
            log.append("  %-28s %s" % (var, ("A: %s + B: %s" % (a["var"], b["var"])) if a else "B: %s" % b["var"]))
        page.text = page.text.replace("\x01NEW\x01", "\n".join(news) + "\n")
        page.text = page.text.replace("\x01BLK\x01", "\n".join(blocks) + "\n")
        page.text = page.text.replace("\x01DCL\x01", "\n".join(decls) + "\n")
        page.changed = True
        page.save()
        log.append("H500 %s: %d buttons -> %d units" % (os.path.basename(page.path), len(buttons), len(groups)))


# ---------------------------------------------------------------- 5. errors
STOP = {"alarm", "alarmi", "error", "fault", "failure", "fail", "moving", "movement", "move", "motion", "not",
        "the", "of", "and", "did", "time", "timeout", "hata", "hatasi", "on", "off", "at", "position", "complete"}
NOT_A_UNIT = {"inverter", "emergency", "pressure", "level", "oil", "thermal", "overload", "door", "safety",
              "run", "motor", "battery", "communication", "comm", "plc", "power", "fuse", "cooling", "temperature"}
TO_B = {"forward", "ileri", "clamp", "close", "down", "push", "extend", "advance", "in", "lock"}
TO_A = {"back", "backward", "geri", "releas", "open", "up", "retract", "return", "out", "unlock"}
AXIS_EXTRA = {"servo", "drive", "axis", "amplifier", "encoder"}

def stem(w):
    w = re.sub(r"^(\d+)(st|nd|rd|th)$", r"\1", w)
    for suf in ("ings", "ing", "es", "s", "e"):
        if len(w) > 4 and w.endswith(suf): return w[: -len(suf)]
    return w

def words(s):
    return [stem(w) for w in re.findall(r"[a-z0-9]+", (s or "").lower().replace("/", " "))]

def translations(machine):
    tr = {}
    for f in [os.path.join(ROOT, "KabanHMI", "CommonTranslations.csv"), os.path.join(ROOT, "Kaban.Machines.%s" % machine, "Translations.csv")]:
        if os.path.exists(f):
            for line in io.open(f, encoding="utf-8-sig"):
                p = line.rstrip("\r\n").split(";")
                if len(p) > 1 and p[0].strip(): tr[p[0].strip()] = p[1].strip()
    return tr

def load_alarms(machine):
    f = os.path.join(ROOT, "Kaban.Machines.%s" % machine, "Alarms.json")
    out = []
    for x in json.load(io.open(f, encoding="utf-8-sig")):
        addr = x.get("PlcAddress") or x.get("Address") or ""
        text = (x.get("Translations") or {}).get("EN") or x.get("DescriptionEN") or x.get("TagCode") or x.get("Code") or ""
        if addr: out.append({"addr": addr, "text": text, "plc": int(x.get("PlcNumber") or 1)})
    return out

# Direction classes, English and Turkish, as they appear in alarm texts and in
# the PLC's own device labels (MAN_ProfilDondurmeUnite3_Asagi = unit 3, down).
DIR_CLASSES = {
    "up": ("up", "yukari", "cikamad", "cik", "raise", "lift"),
    "down": ("down", "asagi", "inemed", "lower"),
    "forward": ("forward", "ileri", "fw", "advance", "extend", "push"),
    "back": ("back", "backward", "geri", "return", "retract"),
    "open": ("open", "acil", "acma", "ac"),
    "close": ("close", "kapa", "kapan"),
    "clamp": ("clamp", "sik", "sikma", "sikis"),
    "release": ("releas", "birak", "gevse"),
}

def dir_class(word_list):
    found = []
    for w in word_list:
        for cls, stems in DIR_CLASSES.items():
            if any(w == s or (len(s) > 3 and w.startswith(s)) for s in stems):
                found.append(cls); break
    # the clamp NOUN yields to a movement word ("Clamp Releasing")
    if len(found) > 1: found = [c for c in found if c != "clamp"] or found
    return found[0] if found else None

def labels(machine):
    lab = {}
    f = os.path.join(ROOT, "Kaban.Machines.%s" % machine, "PlcLabels.csv")
    if os.path.exists(f):
        for line in io.open(f, encoding="utf-8-sig"):
            p = line.rstrip("\r\n").split(";")
            if len(p) >= 3: lab[p[2].strip().upper()] = p[0].strip()
    return lab

def label_words(label):
    return [w.lower() for w in re.findall(r"[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\d+", label or "")]

def link_errors(machine, review):
    tr = translations(machine)
    lab = labels(machine)
    units, axes = [], []
    # Start clean, so running this again never leaves an old link behind.
    for page in machine_pages(machine):
        before = page.text
        page.text = re.sub(r"[ \t]*this\.\w+\.Error[AB]?Address = [^\n]*\n", "", page.text)
        if page.text != before: page.changed = True; page.save()
    for page in machine_pages(machine):
        pname = re.sub(r"^Manual", "", os.path.basename(page.path).split(".")[0])
        for v in page.controls("CylinderUnitHMI"):
            title = page.str_prop(v, "TitleTextKey")
            has_a, has_b = bool(page.str_prop(v, "CommandAAddress")), bool(page.str_prop(v, "CommandBAddress"))
            units.append({"page": page, "var": v, "title": tr.get(title, title), "plc": int(page.prop(v, "PlcNumber") or 1),
                          "tw": set(words(tr.get(title, title))) - STOP, "pw": set(words(pname.replace("_", " "))) - STOP,
                          "sides": ("A" if has_a else "") + ("B" if has_b else ""), "taken": {},
                          # what each side DOES, from the PLC's own label on its command bit
                          "dir": {s: dir_class(label_words(lab.get(page.str_prop(v, "Command%sAddress" % s).upper(), "")))
                                  for s in "AB"}})
        for v in page.controls("AxisSettingHMI"):
            key = page.str_prop(v, "AxisNameKey")
            axes.append({"page": page, "var": v, "title": tr.get(key, key), "plc": int(page.prop(v, "PlcNumber") or 1),
                         "tw": set(words(tr.get(key, key))) - STOP, "taken": None})
    linked, unsure, skipped = [], [], 0
    for al in load_alarms(machine):
        aw = [w for w in words(al["text"])]
        aset = set(aw) - STOP
        if not aset: continue
        # ---- an axis: every word of its name in the alarm, nothing else but servo words
        ax = [x for x in axes if x["plc"] == al["plc"] and x["tw"] and x["tw"] <= aset and not (aset - x["tw"] - AXIS_EXTRA)]
        if len(ax) == 1 and ax[0]["taken"] is None:
            ax[0]["taken"] = al; linked.append((al, ax[0], "axis", "name matches axis exactly")); continue
        # ---- a unit: only alarms that describe a movement
        if aset & NOT_A_UNIT: skipped += 1; continue
        dirs = [("B", w) for w in aw if w in TO_B] + [("A", w) for w in aw if w in TO_A]
        # "Clamp Releasing": clamp names the part, releasing is the movement. The
        # noun counts as a direction only when nothing else in the wording does.
        if len(dirs) > 1: dirs = [d for d in dirs if d[1] != "clamp"] or dirs
        if not dirs and not re.search(r"moving|movement|failure", al["text"], re.I): skipped += 1; continue
        name = aset - (TO_A - {"clamp"}) - (TO_B - {"clamp"})   # "clamp" stays: it names the part
        aclass = dir_class(aw)
        name = {w for w in name if not any(w.startswith(s) for s in ("inemed", "cikamad", "gidemed", "gidilemed"))}
        scored = []
        for u in units:
            if u["plc"] != al["plc"]: continue
            title = u["tw"] - (TO_A - {"back"} - {"clamp"}) - (TO_B - {"clamp"})
            if not title: continue
            in_title = len(name & title)
            in_page = len((name - title) & u["pw"])
            if in_title == 0: continue
            recall = (in_title + 0.5 * in_page) / len(name)          # how much of the alarm is explained
            precision = in_title / len(title)                         # how little the title adds
            f1 = 2 * recall * precision / (recall + precision)
            if "clamp" in name and "clamp" in title: f1 += 0.1        # same kind of part
            scored.append((round(f1, 3), u))
        scored.sort(key=lambda s: -s[0])
        # WHERE THE PLC SAYS WHAT A BUTTON DOES, that decides: only units with a
        # side labelled with the alarm's direction remain - two units with the
        # same title (up/down and forward/back of one rotation unit) separate here.
        by_label = [(s, u) for s, u in scored if aclass and aclass in u["dir"].values()]
        if by_label: scored = by_label
        if not scored or scored[0][0] < 0.6:
            unsure.append((al, "no unit's name fits well enough", scored[:2])); continue
        if len(scored) > 1 and scored[1][0] > scored[0][0] - 0.10:
            unsure.append((al, "two units fit almost equally", scored[:2])); continue
        u = scored[0][1]
        labelled = [s for s in "AB" if aclass and u["dir"].get(s) == aclass]
        if labelled: side, why = labelled[0], "PLC label of its command says '%s'" % aclass
        elif len(u["sides"]) == 1: side, why = u["sides"], "the unit has only this button"
        elif dirs:
            sides = {d for d, _ in dirs}
            if len(sides) > 1: unsure.append((al, "wording points both ways", scored[:1])); continue
            side = sides.pop(); why = "'%s' -> %s" % (dirs[0][1], side) + (" (up/down assumed)" if dirs[0][1] in ("up", "down") else "")
        else:
            unsure.append((al, "no direction word", scored[:1])); continue
        if side in u["taken"]:
            unsure.append((al, "side %s already has %s" % (side, u["taken"][side]["addr"]), scored[:1])); continue
        u["taken"][side] = al; linked.append((al, u, side, why))
    # ---- write, and report
    for al, target, side, why in linked:
        pg = target["page"]
        if side == "axis": pg.set_prop(target["var"], "ErrorAddress", '"%s"' % al["addr"], after="HMIAxisIndex")
        else: pg.set_prop(target["var"], "Error%sAddress" % side, '"%s"' % al["addr"], after="TitleTextKey")
    for pg in {id(x["page"]): x["page"] for x in units + axes}.values(): pg.save()
    review.append("\n## %s - %d linked, %d for you to decide, %d not unit alarms (inverter, e-stop, pressure...)\n" % (machine, len(linked), len(unsure), skipped))
    review.append("| Alarm | Text | Linked to | Side | Why |\n|---|---|---|---|---|")
    for al, t_, side, why in sorted(linked, key=lambda l: l[0]["addr"]):
        review.append("| %s | %s | %s `%s` | %s | %s |" % (al["addr"], al["text"], t_["title"], t_["var"],
                      "axis error" if side == "axis" else ("left (A)" if side == "A" else "right (B)"), why))
    if unsure:
        review.append("\n**Not linked - please decide:**\n\n| Alarm | Text | Reason | Best candidates |\n|---|---|---|---|")
        for al, why, cands in unsure:
            review.append("| %s | %s | %s | %s |" % (al["addr"], al["text"], why, ", ".join("%s (%.2f)" % (c[1]["title"], c[0]) for c in cands) or "-"))
    return len(linked), len(unsure)


if __name__ == "__main__":
    steps = sys.argv[2:] or ["axes", "convert", "resize"]
    log, review = [], []
    for m in ["FA2010", "H800", "MD3020"]:
        axes = number_axes(m, log) if "axes" in steps or "convert" in steps else []
        if "convert" in steps: convert_axis_units(m, axes, log, review)
    if "resize" in steps:
        for m in ["H800", "MD3020"]: resize_units(m, log)
    if "h500" in steps: rebuild_h500(log)
    if "errors" in steps:
        rev = ["# Error bits linked by name - please review\n",
               "Generated by tools/rework_pages.py. Direction comes from the alarm's wording: clamp, forward,",
               "close, down -> right (B); release, back, open, up -> left (A). Up/down is the least certain.",
               "Anything not linked is listed with its best candidates. Correct a link in the designer: the",
               "unit's Help category, ErrorAAddress / ErrorBAddress (axes: ErrorAddress on the axis row)."]
        for m in ["FA2010", "H500", "H800", "MD3020"]:
            if os.path.exists(os.path.join(ROOT, "Kaban.Machines.%s" % m, "Alarms.json")):
                n, u = link_errors(m, rev); log.append("%s: %d error bits linked, %d for review" % (m, n, u))
        io.open(os.path.join(ROOT, "tools", "error_links_review.md"), "w", encoding="utf-8").write("\n".join(rev) + "\n")
    print("\n".join(log))
    if review: print("\nFOR REVIEW:\n" + "\n".join(review))

