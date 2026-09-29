#!/usr/bin/env python3
"""
Generates the FA2010 Manual Movement and Machine Parameter screens straight from
the engineering device list (FA2010_DeviceList.xls).

Why generate rather than hand-write: the workbook already IS the specification -
group, field, back/forward buttons with their momentary/alternate mode, the
sensors that light the lamps, and the position device with its length/divisor.
Typing 122 movement rows and 78 parameters by hand would invite exactly the kind
of address slip that is expensive to find on a machine.

  ManualMovement -> one CylinderUnitHMI per field (button A = Geri/back,
                    button B = Ileri/forward, lamps = the sensors, position =
                    the device with its 16/32-bit type and divisor)
  Parameters     -> one NumericInputHMI per parameter, grouped by section

Run:  python3 tools/generate_pages.py <path to FA2010_DeviceList.xls>
"""
import re, sys, os, unicodedata

DEV = re.compile(r'^[DWMRZ]\d+[A-F0-9]*$', re.I)

def ascii_id(text, fallback="X"):
    """A safe C# identifier from Turkish/English field text."""
    s = unicodedata.normalize("NFKD", text or "")
    s = "".join(c for c in s if not unicodedata.combining(c))
    for a, b in [("ı","i"),("İ","I"),("ş","s"),("Ş","S"),("ğ","g"),("Ğ","G"),
                 ("ç","c"),("Ç","C"),("ö","o"),("Ö","O"),("ü","u"),("Ü","U")]:
        s = s.replace(a, b)
    s = re.sub(r"[^A-Za-z0-9]+", "_", s).strip("_")
    if not s: s = fallback
    if s[0].isdigit(): s = "N" + s
    return s

def read_manual(sh):
    """ManualMovement sheet -> [(group, [field dicts])]"""
    groups, order, group = {}, [], ""
    for r in range(2, sh.nrows):
        cell = lambda c: str(sh.cell_value(r, c)).strip()
        g = cell(0).replace("\n", " ").strip()
        if g:
            group = g
            if g not in groups: groups[g] = []; order.append(g)
        field = cell(1)
        if not field or not group: continue
        item = dict(
            name=field,
            back_sensor=cell(2), back_button=cell(3), back_mode=cell(4),
            fwd_button=cell(5), fwd_mode=cell(6), fwd_sensor=cell(7),
            device=cell(8), length=cell(9), divisor=cell(10), display=cell(11),
        )
        if any([item["back_button"], item["fwd_button"], item["device"],
                item["back_sensor"], item["fwd_sensor"]]):
            groups[group].append(item)
    return [(g, groups[g]) for g in order if groups[g]]

def read_parameters(sh):
    """Parameters sheet -> [(section, [parameter dicts])]"""
    sections, order = {}, []
    section, sub = "", ""
    for r in range(sh.nrows):
        cells = [str(sh.cell_value(r, c)).strip() for c in range(sh.ncols)]
        try: stop = cells.index(".")
        except ValueError: stop = len(cells)
        left = cells[:stop]
        if not any(left): continue
        devs = [i for i, v in enumerate(left) if DEV.match(v)]
        if not devs:
            head = [v for v in left if v]
            if len(head) >= 2: section, sub = head[0], head[1]
            elif head: sub = head[0]
            continue
        i = devs[0]
        key = sub or section or "Parameters"
        key = MERGE_INTO.get(key, key)          # fold sub-sections into their parent
        if key not in sections: sections[key] = []; order.append(key)
        sections[key].append(dict(
            en=left[i-2] if i >= 2 else "",
            tr=left[i-1] if i >= 1 else "",
            device=left[i],
            type=left[i+1] if i+1 < len(left) else "Word [Signed]",
        ))
    return [(s, sections[s]) for s in order if sections[s]]

def data_type(text):
    """'32 bit' / 'Double Word' -> Bin_32, everything else 16-bit."""
    t = (text or "").lower()
    return "Bin_32" if ("32" in t or "double" in t) else "Bin_16"

def decimals_from(divisor, display):
    """
    The DECIMALS come from the sample display ("Gosterim"): 1234.5 -> 1, 123.45 -> 2.
    The workbook's own divisor column does not encode them.
    """
    if display and "." in display:
        return len(display.split(".")[-1].strip())
    return 0


def control_divisor(sheet_divisor, decimals):
    """
    The control divides the raw PLC value ONCE to produce the displayed number,
    so its divisor must also absorb the decimal places:
        control divisor = workbook divisor x 10^decimals
    e.g. Axis Y: workbook 1000 with a 123.4 sample -> 10000, matching a
    hand-placed control. Using the workbook figure alone showed values 10x
    (or 100x) too large.
    """
    try:
        d = float(sheet_divisor)
    except (TypeError, ValueError):
        d = 1.0
    if d <= 0: d = 1.0
    return d * (10 ** decimals)

# --- how a parameter is scaled and labelled -------------------------------
# The workbook does not say; it follows from what the parameter IS, and the
# machine builder confirmed the three cases:
#   servo / linear positions  raw is ten-thousandths -> divisor 10000, "mm"
#   cylinder positions        raw is hundredths      -> divisor   100, "mm"
#   times                     raw is tenths          -> divisor    10, "sec"
TIME_WORDS = ("time", "delay", "duration", "sure", "zaman", "gecikme", "bekleme")
CYLINDER_WORDS = ("cylinder", "clamp", "clamping", "silindir", "sikma", "piston")

def parameter_scale(english, turkish):
    """Returns (divisor, decimals, suffix, unit type)."""
    t = ((english or "") + " " + (turkish or "")).lower()
    if any(w in t for w in TIME_WORDS):
        return 10.0, 1, "sec", "Time"
    if any(w in t for w in CYLINDER_WORDS):
        return 100.0, 2, "mm", "Length"
    return 10000.0, 2, "mm", "Length"


# A few workbook headings are Turkish only; give them an English name so the class
# and the tab caption read like the rest.
# Sections the workbook splits but which belong on ONE tab.
MERGE_INTO = {
    "Kesim Boy Ofseti": "Cutting (Kesim)",
}

TURKISH_ONLY = {
    "Kesim Boy Ofseti": "Cut Length Offset",
    "Testere Kalinligi": "Saw Thickness",
    "Testere Kalınlığı": "Saw Thickness",
}


def split_bilingual(text):
    """
    'Entry Conveyor (Giris Konveyoru)' -> ('Entry Conveyor', 'Giris Konveyoru').
    The workbook writes both languages in one cell, which is why the tabs showed
    English AND Turkish at once.
    """
    text = (text or "").strip()
    m = re.match(r"^(.*?)\s*\((.*)\)\s*$", text)
    if m:
        return m.group(1).strip(), m.group(2).strip()
    if text in TURKISH_ONLY:
        return TURKISH_ONLY[text], text          # English name, Turkish original
    return text, text


def mode_of(text):
    return "Alternate" if (text or "").strip().lower().startswith("alt") else "Momentary"

def load_label_addresses(project):
    """
    <project>/PlcLabels.csv -> {LABEL: address}.

    The workbook the pages are generated from can be out of date, and a physical
    input listed there as X20 is not something the HMI can read. Where the label
    list knows a device, ITS address wins - which is also how X/Y become the
    internal relay the PLC copies them into.
    """
    import io as _io
    path = os.path.join(project, "PlcLabels.csv")
    table = {}
    if not os.path.exists(path):
        return table
    with _io.open(path, encoding="utf-8-sig") as f:
        for line in f:
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            parts = line.split(";") if ";" in line else line.split("\t")
            if len(parts) < 3 or parts[0].strip().lower() == "label":
                continue
            table[parts[0].strip().upper()] = parts[2].strip().upper()
    return table


LABELS = {}
UNKNOWN = []


def plc_address(value):
    """
    Resolves whatever the workbook holds into the address a control should use:
    a LABEL becomes its address, a physical X/Y becomes its internal relay, and
    anything the label list does not know is reported rather than silently kept.
    """
    v = (value or "").strip()
    if not v:
        return ""
    hit = LABELS.get(v.upper())
    if hit:
        return hit
    up = v.upper()
    if up.startswith("X") or up.startswith("Y"):
        # a physical point with no entry in the label list: the HMI cannot read it
        UNKNOWN.append(v)
        return ""
    return v


def cs_string(s):
    return '"' + (s or "").replace("\\", "\\\\").replace('"', '\\"') + '"'


HEADER = """// <auto-generated>
//   Generated by tools/generate_pages.py from {src}  (sheet: {sheet})
//   This is a normal WinForms DESIGNER file: open the page in Visual Studio and
//   every control appears on the design surface with its properties editable.
//   Re-running the generator overwrites it, so make lasting changes in the
//   workbook (or stop generating this page and keep it by hand).
// </auto-generated>
"""

# ---------------------------------------------------------------- page geometry
# Controls fill the LEFT column top to bottom, then continue at the top of the
# right column - reading order, rather than snaking left/right down the page.
# The usable height comes from HmiLayout.PageHeight so it follows the real screen
# (and can be set in HMI Settings when the panel size cannot be detected).
UNIT_W, UNIT_H, UNIT_GAP = 360, 64, 10         # CylinderUnitHMI, toolbox size
INPUT_W, INPUT_H, INPUT_GAP = 116, 26, 22      # NumericInputHMI - 5 digits, not 8
DESC_W = 330                                   # captions were being squeezed; give them room
COL_GAP = 150                                  # the page is wide - use it
TOP_MARGIN, LEFT_MARGIN = 16, 16

# Usable page height the layout is generated FOR. Positions are baked into the
# designer files so the design surface shows the real thing, so this is a build
# time choice: pass a different value if the panel is shorter/taller.
# (HmiLayout detects the running screen and is what the HMI Settings page edits.)
PAGE_HEIGHT = 820

USING = ["using System.Drawing;", "using System.Windows.Forms;",
         "using Kaban.Core.Enums;", "using Kaban.UI.Controls;"]


def designer_file(ns, cls, title, fields, body, src, sheet):
    """The .Designer.cs half: field declarations + InitializeComponent."""
    L = [HEADER.format(src=os.path.basename(src), sheet=sheet),
         "namespace " + ns,
         "{",
         "    partial class " + cls,
         "    {",
         "        private System.ComponentModel.IContainer components = null;",
         "",
         "        protected override void Dispose(bool disposing)",
         "        {",
         "            if (disposing && (components != null)) components.Dispose();",
         "            base.Dispose(disposing);",
         "        }",
         "",
         "        #region Component Designer generated code",
         "",
         "        private void InitializeComponent()",
         "        {"]
    for typ, var in fields:
        L.append("            this." + var + " = new " + typ + "();")
    L.append("            this.SuspendLayout();")
    L += body
    L += ["            // ",
          "            // " + cls,
          "            // ",
          "            // AutoScaleMode.None on purpose: these pages are hosted INSIDE",
          "            // another auto-scaling container (the tab host), and WinForms",
          "            // then scales the contents twice - which is why a generated unit",
          "            // came out about half again as wide as the same control dragged",
          "            // from the toolbox. The layout is in absolute pixels anyway.",
          "            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;",
          "            this.AutoScroll = true;",
          "            this.BackColor = System.Drawing.Color.White;",
          "            this.Name = " + cs_string(cls) + ";",
          "            this.Size = new System.Drawing.Size(1000, 620);",
          "            this.ResumeLayout(false);",
          "            this.PerformLayout();",
          "        }",
          "",
          "        #endregion",
          ""]
    for typ, var in fields:
        L.append("        private " + typ + " " + var + ";")
    L += ["    }", "}", ""]
    return "\n".join(L)


def code_file(ns, cls, src, sheet):
    """The .cs half the designer opens: just the constructor."""
    return "\n".join([
        HEADER.format(src=os.path.basename(src), sheet=sheet),
        "using System.Windows.Forms;",
        "",
        "namespace " + ns,
        "{",
        "    public partial class " + cls + " : UserControl",
        "    {",
        "        public " + cls + "()",
        "        {",
        "            InitializeComponent();",
        "        }",
        "    }",
        "}",
        ""])


def emit_manual(groups, src, out_dir, ns_root):
    ns = ns_root + ".Screens.Manual"
    written = []
    rows_per_col = max(1, (PAGE_HEIGHT - TOP_MARGIN) // (UNIT_H + UNIT_GAP))
    for group, items in groups:
        cls = "Manual" + ascii_id(split_bilingual(group)[0])
        # No page caption: the (bold) tab already names the page, and repeating it
        # only cost vertical space.
        fields = []
        body = []
        col, row = 0, 0
        for it in items:
            has_buttons = bool(it["back_button"] or it["fwd_button"])
            if not has_buttons and not it["device"]:
                # SENSOR-ONLY row: the workbook lists a condition with nothing to
                # operate, so a two-button unit would be misleading - it is a LAMP.
                sensor = it["back_sensor"] or it["fwd_sensor"]
                if not sensor: continue
                var = "lamp" + ascii_id(it["name"])
                fields.append(("Kaban.UI.Controls.LampHMI", var))
                body += ["            // ", "            // " + var + "  (indicator only)", "            // ",
                         "            this." + var + ".Name = " + cs_string(var) + ";",
                         "            this." + var + ".PlcNumber = 1;",
                         "            this." + var + ".Address = " + cs_string(sensor) + ";",
                         "            this." + var + ".DescriptionText = " + cs_string(it["name"]) + ";",
                         "            this." + var + ".Location = new System.Drawing.Point(" +
                         str(LEFT_MARGIN + col * (UNIT_W + COL_GAP)) + ", " +
                         str(TOP_MARGIN + row * (UNIT_H + UNIT_GAP) + 14) + ");",
                         "            this." + var + ".Size = new System.Drawing.Size(36, 36);",
                         "            this.Controls.Add(this." + var + ");"]
                row += 1
                if row >= rows_per_col: row = 0; col += 1
                continue

            var = "unit" + ascii_id(it["name"])
            fields.append(("Kaban.UI.Controls.CylinderUnitHMI", var))
            b = ["            // ", "            // " + var, "            // ",
                 "            this." + var + ".BackColor = System.Drawing.Color.White;",
                 "            this." + var + ".Name = " + cs_string(var) + ";",
                 "            this." + var + ".PlcNumber = 1;",
                 "            this." + var + ".TitleTextKey = " + cs_string(it["name"]) + ";",
                 "            this." + var + ".Location = new System.Drawing.Point(" +
                 str(LEFT_MARGIN + col * (UNIT_W + COL_GAP)) + ", " +
                 str(TOP_MARGIN + row * (UNIT_H + UNIT_GAP)) + ");",
                 "            this." + var + ".Size = new System.Drawing.Size(360, 64);",
                 "            this." + var + ".LogAction = true;"]
            if plc_address(it["back_button"]):
                b += ["            this." + var + ".CommandAAddress = " + cs_string(plc_address(it["back_button"])) + ";",
                      "            this." + var + ".CommandAAction = Kaban.Core.Enums.BitActionType." + mode_of(it["back_mode"]) + ";"]
            if plc_address(it["back_sensor"]):
                b += ["            this." + var + ".Lamp1AAddress = " + cs_string(plc_address(it["back_sensor"])) + ";"]
            if plc_address(it["fwd_button"]):
                b += ["            this." + var + ".CommandBAddress = " + cs_string(plc_address(it["fwd_button"])) + ";",
                      "            this." + var + ".CommandBAction = Kaban.Core.Enums.BitActionType." + mode_of(it["fwd_mode"]) + ";"]
            if plc_address(it["fwd_sensor"]):
                b += ["            this." + var + ".Lamp1BAddress = " + cs_string(plc_address(it["fwd_sensor"])) + ";"]
            if it["device"]:
                dec = decimals_from(it["divisor"], it["display"])
                div = control_divisor(it["divisor"], dec)
                b += ["            this." + var + ".PositionAddress = " + cs_string(plc_address(it["device"])) + ";",
                      "            this." + var + ".PositionDataType = Kaban.Core.Enums.DataTypeEnum." + data_type(it["length"]) + ";",
                      "            this." + var + ".PositionDecimalPlaces = " + str(dec) + ";",
                      "            this." + var + ".PositionDigits = 6;",
                      "            this." + var + ".PositionSuffix = \"mm\";",
                      "            this." + var + ".PositionUnitType = Kaban.Core.Enums.UnitType.Length;"]
                if div != 1.0:
                    b += ["            this." + var + ".PositionDivisor = " + repr(div) + "D;"]
            b += ["            this." + var + ".RequiredSecurity = Kaban.Core.Enums.SecurityLevel." +
                  ("Operator" if has_buttons else "None") + ";",
                  "            this.Controls.Add(this." + var + ");"]
            body += b
            row += 1
            if row >= rows_per_col: row = 0; col += 1
        with open(os.path.join(out_dir, cls + ".Designer.cs"), "w", encoding="utf-8-sig", newline="\r\n") as f:
            f.write(designer_file(ns, cls, group, fields, body, src, "ManualMovement"))
        with open(os.path.join(out_dir, cls + ".cs"), "w", encoding="utf-8-sig", newline="\r\n") as f:
            f.write(code_file(ns, cls, src, "ManualMovement"))
        en, tr = split_bilingual(group)
        written.append((cls, group, len(items), "Tab_" + cls, en, tr))
    return written


def emit_parameters(sections, src, out_dir, ns_root):
    ns = ns_root + ".Screens.Parameters"
    written = []
    rows_per_col = max(1, (PAGE_HEIGHT - TOP_MARGIN) // (INPUT_H + INPUT_GAP))
    for section, items in sections:
        cls = "Param" + ascii_id(split_bilingual(section)[0])
        fields = []
        body = []
        col, row = 0, 0
        used = {}
        for it in items:
            base = "num" + ascii_id(it["en"] or it["tr"] or it["device"])
            used[base] = used.get(base, 0) + 1
            var = base if used[base] == 1 else base + str(used[base])
            fields.append(("Kaban.UI.Controls.NumericInputHMI", var))
            label = it["en"] or it["tr"]
            div, dec, suffix, unit = parameter_scale(it["en"], it["tr"])
            body += ["            // ", "            // " + var + "  (" + it["device"] + ")", "            // ",
                     "            this." + var + ".BackColor = System.Drawing.Color.White;",
                     "            this." + var + ".BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;",
                     "            this." + var + ".Name = " + cs_string(var) + ";",
                     "            this." + var + ".PlcNumber = 1;",
                     "            this." + var + ".Address = " + cs_string(plc_address(it["device"])) + ";",
                     "            this." + var + ".DataType = Kaban.Core.Enums.DataTypeEnum." + data_type(it["type"]) + ";",
                     "            this." + var + ".DescriptionText = " + cs_string(label) + ";",
                     "            this." + var + ".AutoSize = false;",     # a fixed box keeps the column straight
                     "            this." + var + ".Digits = 5;",          # fixed width: figures line up
                     "            this." + var + ".DecimalPlaces = " + str(dec) + ";",
                     "            this." + var + ".Divisor = " + repr(div) + "D;",
                     "            this." + var + ".Suffix = " + cs_string(suffix) + ";",
                     "            this." + var + ".UnitType = Kaban.Core.Enums.UnitType." + unit + ";",
                     "            this." + var + ".TextAlign = System.Drawing.ContentAlignment.MiddleRight;",
                     "            this." + var + ".RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Maintenance;",
                     "            this." + var + ".LogAction = true;",
                     "            this." + var + ".Location = new System.Drawing.Point(" +
                     str(LEFT_MARGIN + DESC_W + col * (DESC_W + INPUT_W + COL_GAP)) + ", " +
                     str(TOP_MARGIN + row * (INPUT_H + INPUT_GAP)) + ");",
                     "            this." + var + ".Size = new System.Drawing.Size(" +
                     str(INPUT_W) + ", " + str(INPUT_H) + ");",
                     "            this.Controls.Add(this." + var + ");"]
            row += 1
            if row >= rows_per_col: row = 0; col += 1
        with open(os.path.join(out_dir, cls + ".Designer.cs"), "w", encoding="utf-8-sig", newline="\r\n") as f:
            f.write(designer_file(ns, cls, section, fields, body, src, "Parameters"))
        with open(os.path.join(out_dir, cls + ".cs"), "w", encoding="utf-8-sig", newline="\r\n") as f:
            f.write(code_file(ns, cls, src, "Parameters"))
        en, tr = split_bilingual(section)
        written.append((cls, section, len(items), "Tab_" + cls, en, tr))
    return written


def main():
    global PAGE_HEIGHT
    for a in sys.argv[1:]:
        if a.startswith("--page-height="):
            PAGE_HEIGHT = int(a.split("=", 1)[1])
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    src = args[0] if args else "FA2010_DeviceList.xls"
    project = args[1] if len(args) > 1 else "Kaban.Machines.FA2010"
    ns = project
    import xlrd
    wb = xlrd.open_workbook(src)

    global LABELS
    LABELS = load_label_addresses(project)
    if LABELS:
        print("using %d PLC labels from %s/PlcLabels.csv" % (len(LABELS), project))
    else:
        print("WARNING: no PlcLabels.csv in %s - the workbook's own addresses are used\n"
              "         as they are, which may be out of date." % project)

    man_dir = os.path.join(project, "Screens", "Manual")
    par_dir = os.path.join(project, "Screens", "Parameters")
    os.makedirs(man_dir, exist_ok=True)
    os.makedirs(par_dir, exist_ok=True)

    manual = emit_manual(read_manual(wb.sheet_by_name("ManualMovement")), src, man_dir, ns)
    params = emit_parameters(read_parameters(wb.sheet_by_name("Parameters")), src, par_dir, ns)

    # Tab captions go into the machine translation file, one row per page, so the
    # tab strip shows ONE language - the workbook writes both in a single cell.
    write_tab_translations(project, manual + params)

    if UNKNOWN:
        seen = sorted(set(UNKNOWN))
        print("\n%d physical address(es) had no entry in the label list and were "
              "LEFT UNBOUND\n(the HMI cannot read X/Y directly):" % len(seen))
        for a in seen[:15]:
            print("   ", a)

    print("Manual movement pages:")
    for cls, group, n, key, en, tr in manual: print(f"   {cls:<32} {en:<26} {n:>3} units")
    print("Parameter pages:")
    for cls, sect, n, key, en, tr in params: print(f"   {cls:<32} {en[:34]:<36} {n:>3} inputs")
    return manual, params


def write_tab_translations(project, pages):
    """Adds/updates a Tab_<Page> row per generated page in Translations.csv."""
    path = os.path.join(project, "Translations.csv")
    header = "Key;EN;TR;RU;DE;IT;ES"
    rows = {}
    order = []
    if os.path.exists(path):
        with open(path, encoding="utf-8-sig") as f:
            lines = [l.rstrip("\r\n") for l in f if l.strip()]
        if lines:
            header = lines[0]
            for line in lines[1:]:
                key = line.split(";")[0]
                rows[key] = line
                order.append(key)
    ncols = len(header.split(";"))
    for cls, title, n, key, en, tr in pages:
        cells = [key, en, tr] + [""] * (ncols - 3)
        if key not in rows: order.append(key)
        rows[key] = ";".join(cells)
    with open(path, "w", encoding="utf-8-sig", newline="") as f:
        f.write(header + "\r\n")
        for key in order:
            f.write(rows[key] + "\r\n")
    print(f"   -> {len(pages)} tab captions written to {path}")

if __name__ == "__main__":
    main()

