#!/usr/bin/env python3
"""
Builds a machine's screens from its PLC label list.

THE THREE THINGS THIS GETS RIGHT THAT THE FIRST ATTEMPT DID NOT
---------------------------------------------------------------
1. A COMMAND AND ITS SENSORS ARE ONE CONTROL.
   MAN_Tahliye1_Kapak_Yukari / _Asagi are the two directions of one cover, and
   IN_Tahliye1_Kapak_YukaridaSN / _AsagidaSN are that cover's end sensors. They
   belong on one FunctionUnitHMI - button, opposite button, and a lamp per end -
   not scattered across a movement page and a separate status page. Matching is
   by STEM: strip the MAN_/IN_ prefix, strip the direction word and the trailing
   SN, and what remains identifies the mechanism.

2. PAGES ARE GROUPED BY SUBSYSTEM.
   One page with 48 controls is unusable on a panel. Fields are grouped by the
   first token of their stem - Tahliye, Robot, Matkap, ProfilDondurme ... - into
   a handful of readable pages.

3. DEVICE FAMILY DECIDES WHERE A THING LIVES.
   L bits are machine options; D300-D800 words are machine parameters. That is
   the machine builder's own convention and it beats guessing from the name.

UNITS, for every machine:
   angle -> degrees, time -> seconds. Both are UnitType.None with a suffix,
   because marking them Length would put them through the mm/inch conversion and
   silently rescale them when the operator switches units.
"""
import csv, re, os, unicodedata, collections

# ----------------------------------------------------------------------
TR = [("ı","i"),("İ","I"),("ş","s"),("Ş","S"),("ğ","g"),("Ğ","G"),
      ("ç","c"),("Ç","C"),("ö","o"),("Ö","O"),("ü","u"),("Ü","U")]

def deturkish(s):
    s = unicodedata.normalize("NFKD", s or "")
    s = "".join(c for c in s if not unicodedata.combining(c))
    for a, b in TR: s = s.replace(a, b)
    return s

def ident(text, fallback="F"):
    s = re.sub(r"[^A-Za-z0-9]+", "_", deturkish(text)).strip("_")
    if not s: s = fallback
    if s[0].isdigit(): s = "N" + s
    return s

WORDS = {
 "Ileri":"Forward","Geri":"Back","Yukari":"Up","Asagi":"Down","Ac":"Open","Sik":"Clamp",
 "Sikma":"Clamping","Acma":"Release","Konveyor":"Conveyor","Konv":"Conveyor",
 "Yaglama":"Lubrication","Sogutma":"Cooling","Matkap":"Drill","Maktap":"Drill",
 "Freze":"Milling","Testere":"Saw","Tahliye":"Discharge","Bosaltma":"Unload",
 "ProfilDondurme":"Profile Rotation","Dondurme":"Rotation","Profil":"Profile",
 "Basi":"Head","Olcum":"Measurement","Vidalama":"Screwing","Kapak":"Cover",
 "Emniyet":"Safety","AcilStop":"Emergency Stop","Hava":"Air","Basinc":"Pressure",
 "Sure":"Time","Hiz":"Speed","Sayisi":"Count","Mesafe":"Distance","Poz":"Position",
 "Pozisyon":"Position","AktPoz":"Actual Position","RefSN":"Reference Sensor",
 "Aktif":"Enable","Pasif":"Disable","Test":"Test","Barkod":"Barcode",
 "Aksesuar":"Accessory","Bolme":"Partition","Secmeli":"Optional","Baslangic":"Start",
 "Bitis":"End","Is":"Job","Liste":"List","Listesi":"List","Listesini":"List",
 "Satir":"Row","Satiri":"Row","Sil":"Delete","Gonder":"Send","Algilama":"Detection",
 "Enkoder":"Encoder","Motor":"Motor","Motoru":"Motor","Kilit":"Lock",
 "Piston":"Cylinder","Pistonu":"Cylinder","Dayama":"Stopper","Dayamali":"With Stopper",
 "Deneme":"Trial","Makine":"Machine","Sensor":"Sensor","Uygun":"Ready","Hata":"Error",
 "Ariza":"Fault","Alarm":"Alarm","Reset":"Reset","Calis":"Run","Calisma":"Run",
 "Duraklat":"Pause","Iptal":"Cancel","Yuksek":"High","Dusuk":"Low","Ilerleme":"Advance",
 "Unite":"Unit","Unitesi":"Unit","Ust":"Top","Alt":"Bottom","Sol":"Left","Sag":"Right",
 "On":"Front","Arka":"Rear","Ic":"Inner","Dis":"Outer","Merkez":"Centre",
 "Talas":"Chip","Temizleme":"Cleaning","Bos":"Empty","Dolu":"Full",
 "Yukarida":"Up","Asagida":"Down","Ileride":"Forward","Geride":"Back",
 "Beraber":"Together","Aciyla":"With Angle","Kontrol":"Check","Bitiste":"At End",
 "der":"deg","Adim":"Step","Sayaci":"Counter","Toplam":"Total","Genislik":"Width",
 "Yukseklik":"Height","Uzunluk":"Length","Kalinlik":"Thickness","Servo":"Servo",
 "Inverter":"Inverter","Baslangici":"Start","Aci":"Angle","Derece":"Degree",
}

PREFIXES = ("MAN__","MAN_","IN_","OUT_","PC_","PCden_","PCye_","PLC_")

def strip_prefix(label):
    for p in PREFIXES:
        if label.startswith(p): return label[len(p):]
    return label

def humanise(label):
    s = deturkish(strip_prefix(label))
    out = []
    for part in [p for p in s.split("_") if p]:
        if part in WORDS: out.append(WORDS[part]); continue
        chunks = re.findall(r"[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\d+", part) or [part]
        out.append(" ".join(WORDS.get(c, c) for c in chunks))
    return re.sub(r"\s+", " ", " ".join(out)).strip()

# ----------------------------------------------------------------------
# Direction vocabulary. A command word and the sensor word for the same end of
# travel are different in Turkish (Yukari = "go up", Yukarida = "is up"), which
# is why matching has to know both.
FWD = {"ileri":"F","yukari":"F","ilerideh":"F","ileride":"F","yukarida":"F",
       "sik":"F","sikmasik":"F","kapali":"F","acma":"F"}
BACK= {"geri":"B","asagi":"B","geride":"B","asagida":"B",
       "ac":"B","sikmaac":"B","acik":"B","sikma":"B"}
DIRWORDS = set(FWD) | set(BACK)

def split_direction(label):
    """(stem, 'F'|'B'|None) - the mechanism and which way this bit drives it."""
    s = deturkish(strip_prefix(label))
    s = re.sub(r"SN$", "", s)
    tokens = [t for t in re.split(r"[_]+", s) if t]
    if not tokens: return ("", None)
    # the direction is usually the last token, sometimes glued on the end
    last = tokens[-1].lower()
    for d in (FWD, BACK):
        if last in d:
            return ("_".join(tokens[:-1]), d[last])
    m = re.match(r"^(.*?)(ileri|geri|yukari|asagi|ileride|geride|yukarida|asagida|sik|ac)$",
                 last, re.I)
    if m and m.group(1):
        d = m.group(2).lower()
        fam = FWD.get(d) or BACK.get(d)
        return ("_".join(tokens[:-1] + [m.group(1)]), fam)
    return ("_".join(tokens), None)

def norm(stem):
    """Comparison key: letters and digits only, lower case."""
    return re.sub(r"[^a-z0-9]", "", deturkish(stem).lower())

# ----------------------------------------------------------------------
def is_bit(dtype): return dtype.strip().lower().startswith("bit")
def is_32(dtype):  return "Double" in dtype or "32" in dtype

ANGLE = ("aci","angle","derece","degree","der","dondurme","rotation")
TIME  = ("sure","time","delay","gecikme","bekleme","duration")
SPEED = ("hiz","speed","rpm","feed","override")
COUNT = ("sayisi","sayaci","count","adet","adim","step","no","numara")

def unit_for(text):
    """
    (UnitType, suffix, decimals).

    Angles are DEGREES and times are SECONDS on every machine. Both are
    UnitType.None with a suffix rather than a real unit: the unit system
    converts Length between mm and inch, and putting an angle or a time through
    that would silently rescale it when the operator switches units.
    """
    t = deturkish(text or "").lower()
    if any(w in t for w in ANGLE): return ("None", "\u00B0", 1)
    if any(w in t for w in TIME):  return ("None", "s", 1)
    if any(w in t for w in SPEED): return ("None", "", 0)
    if any(w in t for w in COUNT): return ("None", "", 0)
    return ("Length", "mm", 1)

def divisor_for(dtype, unit, suffix):
    if suffix == "\u00B0": return 10.0        # angle: one decimal in the PLC
    if suffix == "s":      return 10.0        # time: tenths of a second
    if unit == "None":     return 1.0         # counts and speeds are plain
    return 10000.0 if is_32(dtype) else 10.0  # position: encoder counts

# ----------------------------------------------------------------------
# Subsystem grouping. First token of the stem, mapped to a page.
SUBSYSTEMS = [
 ("Axes",             ("x1","y1","z1","x2","y2","z2","x","y","z","profilbasiolcum","3d")),
 ("Drilling",         ("matkap","maktap","freze","talas","bicak","kesim")),
 ("Conveyor",         ("konveyor","konv","vidalama","vidalamakonv","vidalamakonveyor")),
 ("Profile Rotation", ("profildondurme","dondurme","profil")),
 ("Discharge",        ("tahliye","tahlite","robot","bosaltma")),
 ("Clamping",         ("sikma","kilit","dayama","yaglama")),
]

def subsystem(stem):
    n = norm(stem)
    for page, keys in SUBSYSTEMS:
        for k in keys:
            if n.startswith(k): return page
    return "Other"

# ----------------------------------------------------------------------
def load_labels(path):
    rows = []
    with open(path, encoding="utf-8-sig") as fh:
        for r in csv.reader(fh, delimiter=";"):
            if len(r) < 3 or not r[0].strip(): continue
            if r[0].strip().lower() == "label" or r[0].startswith("#"): continue
            rows.append(dict(label=r[0].strip(), dtype=r[1].strip(), addr=r[2].strip()))
    return rows

def build_movement(rows):
    """
    MAN_ commands folded together with their IN_ sensors into one field each.
    Returns {page: [field]}.
    """
    commands = [r for r in rows if r["label"].startswith("MAN")]
    sensors  = [r for r in rows if r["label"].startswith("IN_")]
    words    = [r for r in rows if not is_bit(r["dtype"])]

    # index the sensors by (stem, direction)
    sensor_by = collections.defaultdict(list)
    for s in sensors:
        stem, d = split_direction(s["label"])
        sensor_by[(norm(stem), d)].append(s)

    used_sensor = set()
    def take(stem, d):
        for s in sensor_by.get((norm(stem), d), []):
            if s["addr"] not in used_sensor:
                used_sensor.add(s["addr"]); return s
        return None

    fields, done = [], set()
    for c in commands:
        if c["addr"] in done: continue
        stem, d = split_direction(c["label"])
        partner = None
        if d:
            want = "B" if d == "F" else "F"
            for other in commands:
                if other["addr"] in done or other is c: continue
                s2, d2 = split_direction(other["label"])
                if d2 == want and norm(s2) == norm(stem):
                    partner = other; break

        if partner:
            done.add(c["addr"]); done.add(partner["addr"])
            fwd, back = (c, partner) if d == "F" else (partner, c)
            pos = next((w for w in words if norm(stem) and norm(w["label"]).startswith(norm(stem))
                        and "aktpoz" in norm(w["label"])), None)
            fields.append(dict(kind="unit", page=subsystem(stem), caption=humanise(stem) or humanise(c["label"]),
                               fwd=fwd, back=back,
                               lamp_fwd=take(stem, "F"), lamp_back=take(stem, "B"),
                               pos=pos))
        else:
            done.add(c["addr"])
            lamp = take(stem, d) if d else take(stem, None)
            fields.append(dict(kind="button", page=subsystem(stem),
                               caption=humanise(c["label"]), cmd=c, lamp=lamp))

    # sensors nobody claimed still have to be visible somewhere
    for s in sensors:
        if s["addr"] in used_sensor: continue
        stem, _ = split_direction(s["label"])
        fields.append(dict(kind="lamp", page=subsystem(stem), caption=humanise(s["label"]), cmd=s))

    pages = collections.OrderedDict()
    for name, _ in SUBSYSTEMS + [("Other", ())]:
        got = [f for f in fields if f["page"] == name]
        if got: pages[name] = got
    return pages

def in_param_range(addr):
    """D300-D800 is this machine builder's parameter block."""
    m = re.match(r"^D(\d+)$", addr, re.I)
    return bool(m) and 300 <= int(m.group(1)) <= 800

def build_parameters(rows):
    """
    Machine parameters and options.

    L bits and PC_ bits are OPTIONS (checkboxes); D300-D800 words are the
    PARAMETERS. Both are the machine builder's own convention, which is a better
    guide than anything inferable from the label text.
    """
    options, params = [], []
    for r in rows:
        low = r["label"].lower()
        if any(w in low for w in ("screenno","popupno","securitylevel","systemsignal",
                                  "barkod_okunan","programversiyonu")):
            continue
        if r["addr"].upper().startswith("L") and is_bit(r["dtype"]):
            options.append(dict(kind="check", caption=humanise(r["label"]), cmd=r))
        elif r["label"].startswith("PC_") and is_bit(r["dtype"]):
            options.append(dict(kind="check", caption=humanise(r["label"]), cmd=r))
        elif in_param_range(r["addr"]) and not is_bit(r["dtype"]):
            params.append(dict(kind="num", caption=humanise(r["label"]), cmd=r))
    return options, params

def build_status(rows):
    """Outputs and any remaining readable words."""
    out = []
    for r in rows:
        if r["label"].startswith("OUT_"):
            out.append(dict(kind="lamp", caption=humanise(r["label"]), cmd=r))
    return out

def axis_positions(rows):
    """The *_AktPoz words - what a home screen shows."""
    return [dict(kind="display", caption=humanise(r["label"]), cmd=r)
            for r in rows if "aktpoz" in norm(r["label"])]

