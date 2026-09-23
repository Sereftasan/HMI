#!/usr/bin/env python3
"""
Rebuilds machine screens from a DEVICE MAP extracted from the previous
program's designer files (tools/extract_legacy_devices.py).

WHY THIS EXISTS
---------------
The old program encodes the real machine: which relay drives which clamp, which
word holds which position, and - by the control class chosen - whether a thing
is a MOVEMENT, a STATE or a SETTING. That last part is the valuable bit, and it
is what the current FA2010 pages get wrong: every field became a movement unit
with a position readout, so a clamp that is simply open or shut shows a spurious
"0.0 mm", and a cutting ANGLE is labelled in millimetres.

CONTROL CHOICE, which is the whole point of the rebuild:

    old kabanLambButton  (button + built-in lamp)  -> FunctionUnitHMI when it
                            pairs with an opposite command, else ButtonHMI with
                            Lamp1Address - the lamp IS the feedback
    old kabanButton      (momentary/alternate)     -> ButtonHMI
    old kabanLedLabel    (read-only word)          -> NumericDisplayHMI
    old kabanNumericButton (writable word)         -> NumericInputHMI
    old kabanSwitchButton (on/off setting)         -> CheckBoxHMI
    sensor bit with no command                     -> LampHMI

A position readout is emitted ONLY where the old page actually had a word
device for that field. That single rule removes every phantom "0.0 mm".

UNITS are inferred from the caption, not assumed: anything reading Angle or
Degree becomes UnitType.Angle with a degree suffix. Getting this wrong is how
"Cutting Angle ... 45.0 mm" happened.
"""
import csv, os, re, sys, unicodedata, collections

# ----------------------------------------------------------------------
def ident(text, fallback="X"):
    s = unicodedata.normalize("NFKD", text or "")
    s = "".join(c for c in s if not unicodedata.combining(c))
    for a, b in [("ı","i"),("İ","I"),("ş","s"),("Ş","S"),("ğ","g"),("Ğ","G"),
                 ("ç","c"),("Ç","C"),("ö","o"),("Ö","O"),("ü","u"),("Ü","U")]:
        s = s.replace(a, b)
    s = re.sub(r"[^A-Za-z0-9]+", "_", s).strip("_")
    if not s: s = fallback
    if s[0].isdigit(): s = "N" + s
    return s

ANGLE_WORDS = ("angle", "degree", "aci", "derece")
SPEED_WORDS = ("speed", "hiz", "rpm", "feed")
TIME_WORDS  = ("time", "sure", "delay", "timer")

def unit_for(caption):
    """(UnitType, suffix, decimals) inferred from what the field is called."""
    c = (caption or "").lower()
    if any(w in c for w in ANGLE_WORDS): return ("Angle", "\\u00B0", 1)
    if any(w in c for w in SPEED_WORDS): return ("None", "", 0)
    if any(w in c for w in TIME_WORDS):  return ("None", "s", 1)
    return ("Length", "mm", 1)

def divisor_for(data_type, unit_type):
    """
    A position word is an encoder count on these machines. 32-bit axis values
    scale by 10000, 16-bit by 10. Angles carry one decimal in the PLC already.

    These are STARTING VALUES, deliberately uniform per data type rather than
    guessed per field - the previous pages had four different divisors with no
    pattern behind them, which is worse than one consistent assumption that is
    easy to correct on the machine.
    """
    if unit_type == "Angle": return 10.0
    return 10000.0 if data_type.endswith("32") else 10.0

BIT = re.compile(r"^[MXYRB]\d", re.I)
WORD = re.compile(r"^[DWZ]\d", re.I)

# ----------------------------------------------------------------------
def load(path):
    with open(path, encoding="utf-8-sig") as fh:
        return list(csv.DictReader(fh, delimiter=";"))

def group_fields(rows):
    """
    Collapse the raw control list into FIELDS - one per caption.

    The old pages put a forward button, a back button and their lamps on one
    row under a single caption, so the caption is what identifies a field. A
    field with two command bits becomes a movement unit; anything else does not.
    """
    fields = collections.OrderedDict()
    for r in rows:
        cap = (r["caption"] or "").strip()
        if not cap or cap in ("GO!", "Refresh Page"): 
            cap = cap or r["device"]
        key = cap
        f = fields.setdefault(key, dict(caption=cap, commands=[], lamps=[],
                                        words=[], switches=[], inputs=[]))
        kind = r["ctrl_kind"]
        dev  = r["device"]
        if kind == "kabanSwitchButton" or kind.startswith("ksb"):
            f["switches"].append(r)
        elif kind == "kabanNumericButton":
            f["inputs"].append(r)
        elif kind == "kabanLedLabel":
            f["words"].append(r)
        elif kind == "kabanLambButton":
            f["commands"].append(r); f["lamps"].append(r)
        elif kind.startswith("kabanButton") or kind.startswith("btn"):
            f["commands"].append(r)
        elif WORD.match(dev):
            f["words"].append(r)
        else:
            f["lamps"].append(r)
    return fields

# ----------------------------------------------------------------------
HEADER = """\ufeff// <auto-generated>
//   Generated by tools/generate_from_legacy.py
//   Source: the previous program's designer files, via
//           tools/extract_legacy_devices.py -> {src}
//
//   Addresses and captions come from the OLD MACHINE and are real. Divisors and
//   units are STARTING VALUES chosen per data type (see the generator's notes);
//   correct them on the machine and they will survive, because a page stops
//   being regenerated once you remove it from the generator's page list.
// </auto-generated>

namespace {ns}
{{
    partial class {cls}
    {{
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {{
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }}

        #region Component Designer generated code

        private void InitializeComponent()
        {{
"""

FOOTER = """            this.AutoScroll = true;
            this.BackColor = System.Drawing.Color.White;
            this.Name = "{cls}";
            this.Size = new System.Drawing.Size(1040, 690);
            this.ResumeLayout(false);
        }}

        #endregion
"""

def emit_page(ns, cls, fields, src, plc=1):
    decl, body, names = [], [], []
    x, y, col_w, row_h = 16, 16, 500, 0
    max_y = 690

    for cap, f in fields.items():
        base = ident(cap)[:38]
        n = base
        i = 2
        while n in names: n = base + str(i); i += 1
        names.append(n)

        cmds  = [c for c in f["commands"] if BIT.match(c["device"])]
        lamps = [c for c in f["lamps"] if BIT.match(c["device"])]
        words = [c for c in f["words"] if WORD.match(c["device"])]
        inputs= [c for c in f["inputs"] if WORD.match(c["device"])]
        switches = [c for c in f["switches"] if BIT.match(c["device"])]

        ut, suffix, dec = unit_for(cap)

        # ---- a SETTING: writable word ----------------------------------
        if inputs and not cmds:
            for k, w in enumerate(inputs[:4]):
                nm = "num" + n + (str(k+1) if k else "")
                decl.append('            this.{0} = new Kaban.UI.Controls.NumericInputHMI();'.format(nm))
                dv = divisor_for(w["data_type"], ut)
                body.append(BLOCK_NUM.format(nm=nm, cap=cap.replace('"','')[:46], dev=w["device"],
                    dt="Bin_32" if w["data_type"].endswith("32") else "Bin_16",
                    div=dv, dec=dec, suffix=suffix, ut=ut, x=x, y=y, plc=plc, kind="NumericInputHMI"))
                y += 40
            row_h = 40
        # ---- an on/off SETTING ----------------------------------------
        elif switches:
            for k, s in enumerate(switches[:4]):
                nm = "chk" + n + (str(k+1) if k else "")
                decl.append('            this.{0} = new Kaban.UI.Controls.CheckBoxHMI();'.format(nm))
                body.append(BLOCK_CHK.format(nm=nm, cap=cap.replace('"','')[:46],
                                             dev=s["device"], x=x, y=y, plc=plc))
                y += 34
            row_h = 34
        # ---- a MOVEMENT: two opposing commands -------------------------
        elif len(cmds) >= 2:
            nm = "unit" + n
            decl.append('            this.{0} = new Kaban.UI.Controls.FunctionUnitHMI();'.format(nm))
            pos = ""
            if words:
                w = words[0]
                pos = POS.format(nm=nm, dev=w["device"],
                    dt="Bin_32" if w["data_type"].endswith("32") else "Bin_16",
                    div=divisor_for(w["data_type"], ut), dec=dec, suffix=suffix, ut=ut)
            body.append(BLOCK_UNIT.format(nm=nm, cap=cap.replace('"','')[:46],
                a=cmds[0]["device"], amode=cmds[0]["button_type"] or "Momentary",
                b=cmds[1]["device"], bmode=cmds[1]["button_type"] or "Momentary",
                lampa=('            this.{0}.Lamp1AAddress = "{1}";\n'.format(nm, lamps[0]["device"]) if lamps else ""),
                lampb=('            this.{0}.Lamp1BAddress = "{1}";\n'.format(nm, lamps[1]["device"]) if len(lamps)>1 else ""),
                pos=pos, x=x, y=y, plc=plc))
            y += 72; row_h = 72
        # ---- a single COMMAND, lamp shows its feedback -----------------
        elif len(cmds) == 1:
            nm = "btn" + n
            decl.append('            this.{0} = new Kaban.UI.Controls.ButtonHMI();'.format(nm))
            body.append(BLOCK_BTN.format(nm=nm, cap=cap.replace('"','')[:46], dev=cmds[0]["device"],
                mode=cmds[0]["button_type"] or "Momentary",
                lamp=('            this.{0}.Lamp1Address = "{1}";\n'.format(nm, lamps[0]["device"]) if lamps else ""),
                x=x, y=y, plc=plc))
            y += 44; row_h = 44
        # ---- a read-only VALUE ----------------------------------------
        elif words:
            w = words[0]
            nm = "dsp" + n
            decl.append('            this.{0} = new Kaban.UI.Controls.NumericDisplayHMI();'.format(nm))
            body.append(BLOCK_NUM.format(nm=nm, cap=cap.replace('"','')[:46], dev=w["device"],
                dt="Bin_32" if w["data_type"].endswith("32") else "Bin_16",
                div=divisor_for(w["data_type"], ut), dec=dec, suffix=suffix, ut=ut,
                x=x, y=y, plc=plc, kind="NumericDisplayHMI"))
            y += 40; row_h = 40
        # ---- a pure STATE: lamp only -----------------------------------
        elif lamps:
            nm = "lmp" + n
            decl.append('            this.{0} = new Kaban.UI.Controls.LampHMI();'.format(nm))
            body.append(BLOCK_LAMP.format(nm=nm, cap=cap.replace('"','')[:46],
                                          dev=lamps[0]["device"], x=x, y=y, plc=plc))
            y += 36; row_h = 36
        else:
            names.pop(); continue

        if y > max_y - row_h:
            y = 16; x += col_w

    fielddecl = "\n".join("        private Kaban.UI.Controls.{0} {1};".format(
        re.search(r"new Kaban\.UI\.Controls\.(\w+)\(\)", d).group(1),
        re.search(r"this\.(\w+) =", d).group(1)) for d in decl)

    return (HEADER.format(ns=ns, cls=cls, src=src) +
            "\n".join(decl) + "\n            this.SuspendLayout();\n" +
            "".join(body) + FOOTER.format(cls=cls) + "\n" + fielddecl + "\n    }\n}\n")

BLOCK_UNIT = """            // 
            // {nm}
            // 
            this.{nm}.BackColor = System.Drawing.Color.White;
            this.{nm}.Name = "{nm}";
            this.{nm}.PlcNumber = {plc};
            this.{nm}.TitleTextKey = "{cap}";
            this.{nm}.Location = new System.Drawing.Point({x}, {y});
            this.{nm}.Size = new System.Drawing.Size(468, 64);
            this.{nm}.LogAction = true;
            this.{nm}.CommandAAddress = "{a}";
            this.{nm}.CommandAAction = Kaban.Core.Enums.BitActionType.{amode};
{lampa}            this.{nm}.CommandBAddress = "{b}";
            this.{nm}.CommandBAction = Kaban.Core.Enums.BitActionType.{bmode};
{lampb}{pos}            this.{nm}.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.Controls.Add(this.{nm});
"""

POS = """            this.{nm}.PositionAddress = "{dev}";
            this.{nm}.PositionDataType = Kaban.Core.Enums.DataTypeEnum.{dt};
            this.{nm}.PositionDivisor = {div}D;
            this.{nm}.PositionDecimalPlaces = {dec};
            this.{nm}.PositionDigits = 6;
            this.{nm}.PositionSuffix = "{suffix}";
            this.{nm}.PositionUnitType = Kaban.Core.Enums.UnitType.{ut};
"""

BLOCK_BTN = """            // 
            // {nm}
            // 
            this.{nm}.Name = "{nm}";
            this.{nm}.PlcNumber = {plc};
            this.{nm}.FallbackText = "{cap}";
            this.{nm}.Address = "{dev}";
            this.{nm}.BitAction = Kaban.Core.Enums.BitActionType.{mode};
{lamp}            this.{nm}.Location = new System.Drawing.Point({x}, {y});
            this.{nm}.Size = new System.Drawing.Size(220, 38);
            this.{nm}.LogAction = true;
            this.{nm}.RequiredSecurity = Kaban.Core.Enums.SecurityLevel.Operator;
            this.Controls.Add(this.{nm});
"""

BLOCK_LAMP = """            // 
            // {nm}
            // 
            this.{nm}.Name = "{nm}";
            this.{nm}.PlcNumber = {plc};
            this.{nm}.Address = "{dev}";
            this.{nm}.DescriptionText = "{cap}";
            this.{nm}.Location = new System.Drawing.Point({x}, {y});
            this.{nm}.Size = new System.Drawing.Size(220, 30);
            this.Controls.Add(this.{nm});
"""

BLOCK_CHK = """            // 
            // {nm}
            // 
            this.{nm}.Name = "{nm}";
            this.{nm}.PlcNumber = {plc};
            this.{nm}.Address = "{dev}";
            this.{nm}.DescriptionText = "{cap}";
            this.{nm}.Location = new System.Drawing.Point({x}, {y});
            this.{nm}.Size = new System.Drawing.Size(260, 28);
            this.{nm}.LogAction = true;
            this.Controls.Add(this.{nm});
"""

BLOCK_NUM = """            // 
            // {nm}
            // 
            this.{nm}.Name = "{nm}";
            this.{nm}.PlcNumber = {plc};
            this.{nm}.Address = "{dev}";
            this.{nm}.DataType = Kaban.Core.Enums.DataTypeEnum.{dt};
            this.{nm}.Divisor = {div}D;
            this.{nm}.DecimalPlaces = {dec};
            this.{nm}.Digits = 6;
            this.{nm}.Suffix = "{suffix}";
            this.{nm}.UnitType = Kaban.Core.Enums.UnitType.{ut};
            this.{nm}.DescriptionText = "{cap}";
            this.{nm}.Location = new System.Drawing.Point({x}, {y});
            this.{nm}.Size = new System.Drawing.Size(300, 34);
            this.Controls.Add(this.{nm});
"""

CODE_BEHIND = """\ufeff// <auto-generated>
//   Generated by tools/generate_from_legacy.py - see the .Designer.cs.
// </auto-generated>

using System.Windows.Forms;

namespace {ns}
{{
    public partial class {cls} : UserControl
    {{
        public {cls}()
        {{
            InitializeComponent();
        }}
    }}
}}
"""

def build(map_csv, pages, ns, outdir, plc=1):
    rows = load(map_csv)
    made = []
    for legacy_page, cls in pages.items():
        page_rows = [r for r in rows if r["page"] == legacy_page]
        if not page_rows: continue
        fields = group_fields(page_rows)
        os.makedirs(outdir, exist_ok=True)
        with open(os.path.join(outdir, cls + ".Designer.cs"), "w",
                  encoding="utf-8", newline="\r\n") as fh:
            fh.write(emit_page(ns, cls, fields, os.path.basename(map_csv), plc))
        with open(os.path.join(outdir, cls + ".cs"), "w",
                  encoding="utf-8", newline="\r\n") as fh:
            fh.write(CODE_BEHIND.format(ns=ns, cls=cls))
        made.append((cls, len(fields), len(page_rows)))
    return made

if __name__ == "__main__":
    print("use tools/build_machine_pages.py")

