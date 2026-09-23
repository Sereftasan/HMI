#!/usr/bin/env python3
"""
Builds Manual Movement and Machine Parameter screens from a PLC LABEL LIST
(Label;DataType;Address) - the format H800_LabelList.xlsx already uses and the
one build_plc_labels.py produces.

WHY A LABEL LIST IS A BETTER SOURCE THAN THE OLD SCREENS
--------------------------------------------------------
Reading the previous program's designer files recovers addresses and whatever
caption happened to sit next to a control. A label list carries the ENGINEER'S
OWN NAME for each device, and those names encode the machine:

    MAN__X1_Ileri / MAN__X1_Geri     one axis, two directions -> a movement unit
    IN_X1_RefSN                      that axis's reference sensor -> its lamp
    X1_AktPoz                        that axis's actual position -> its readout

So a page can be assembled by MEANING rather than by screen position, and the
result is closer to the machine than the old layout was.

NAMING CONVENTIONS UNDERSTOOD (Turkish, as the engineers write them):
    MAN_    manual command          IN_   input/sensor       OUT_  output
    PC_     HMI-owned parameter     _Ileri forward   _Geri  back
    _AktPoz actual position         _RefSN reference sensor
    _Aktif  enable flag             Sure   time      Hiz    speed
"""
import csv, re, os, sys, unicodedata, collections

# ----------------------------------------------------------------------
TR = [("ı","i"),("İ","I"),("ş","s"),("Ş","S"),("ğ","g"),("Ğ","G"),
      ("ç","c"),("Ç","C"),("ö","o"),("Ö","O"),("ü","u"),("Ü","U")]

def ident(text, fallback="X"):
    s = unicodedata.normalize("NFKD", text or "")
    s = "".join(c for c in s if not unicodedata.combining(c))
    for a, b in TR: s = s.replace(a, b)
    s = re.sub(r"[^A-Za-z0-9]+", "_", s).strip("_")
    if not s: s = fallback
    if s[0].isdigit(): s = "N" + s
    return s

# Turkish device words -> English captions, so the pages read the way the rest
# of the program does. Anything unmapped keeps the engineer's own word, which is
# better than a bad guess.
WORDS = {
    "Ileri":"Forward", "Geri":"Back", "Yukari":"Up", "Asagi":"Down",
    "Sikma":"Clamping", "Acma":"Release", "Konveyor":"Conveyor",
    "Yaglama":"Lubrication", "Sogutma":"Cooling", "Matkap":"Drill",
    "Freze":"Milling", "Testere":"Saw", "Tahliye":"Discharge",
    "ProfilDondurme":"Profile Rotation", "Dondurme":"Rotation",
    "Profil":"Profile", "Basi":"Head", "Olcum":"Measurement",
    "Vidalama":"Screwing", "Kapak":"Cover", "Emniyet":"Safety",
    "AcilStop":"Emergency Stop", "Hava":"Air", "Basinc":"Pressure",
    "Sure":"Time", "Hiz":"Speed", "Sayisi":"Count", "Mesafe":"Distance",
    "Pozisyon":"Position", "AktPoz":"Actual Position", "RefSN":"Reference Sensor",
    "Aktif":"Enable", "Pasif":"Disable", "Test":"Test", "Barkod":"Barcode",
    "Aksesuar":"Accessory", "Bolme":"Partition", "Secmeli":"Optional",
    "Baslangic":"Start", "Bitis":"End", "Poza":"Position", "Is":"Job",
    "Liste":"List", "Satir":"Row", "Sil":"Delete", "Gonder":"Send",
    "Algilama":"Detection", "Enkoder":"Encoder", "Motor":"Motor",
    "Kilit":"Lock", "Pistonu":"Cylinder", "Piston":"Cylinder",
    "Dayama":"Stopper", "Dayamali":"With Stopper", "Deneme":"Trial",
    "Makine":"Machine", "Sensor":"Sensor", "Uygun":"Ready", "Hata":"Error",
    "Alarm":"Alarm", "Reset":"Reset", "Start":"Start", "Stop":"Stop",
    "Tahliye1":"Discharge 1", "Tahliye2":"Discharge 2", "Matkap1":"Drill 1",
    "Matkap2":"Drill 2", "Maktap1":"Drill 1", "Maktap2":"Drill 2",
    "Calis":"Run", "Calisma":"Run", "Yuksek":"High", "Dusuk":"Low",
    "Ilerleme":"Advance", "Iptal":"Cancel", "Satiri":"Row", "Satir":"Row",
    "Listesini":"List", "Listesi":"List", "Konv":"Conveyor", "Motoru":"Motor",
    "Homing":"Homing", "Unite1":"Unit 1", "Unite2":"Unit 2", "Unite3":"Unit 3",
    "Beraber":"Together", "Aciyla":"With Angle", "Kontrol":"Check",
    "Bitiste":"At Job End", "Yikama":"Washing", "Fircalama":"Brushing",
    "Kesim":"Cutting", "Delme":"Drilling", "Bekleme":"Wait", "Adim":"Step",
    "Sayaci":"Counter", "Toplam":"Total", "Gunluk":"Daily", "Uretim":"Production",
    "Genislik":"Width", "Yukseklik":"Height", "Uzunluk":"Length", "Kalinlik":"Thickness",
    "Merkez":"Centre", "Sol":"Left", "Sag":"Right", "Ust":"Top", "Alt":"Bottom",
    "On":"Front", "Arka":"Rear", "Ic":"Inner", "Dis":"Outer",
}

def humanise(label):
    """MAN__X1_Ileri -> 'X1 Forward'."""
    s = label
    for p in ("MAN__", "MAN_", "IN_", "OUT_", "PC_", "PLC_"):
        if s.startswith(p): s = s[len(p):]; break
    for a, b in TR: s = s.replace(a, b)
    parts = [p for p in s.split("_") if p]
    out = []
    for p in parts:
        if p in WORDS: out.append(WORDS[p]); continue
        # split CamelCase then map each word
        chunks = re.findall(r"[A-Z]?[a-z0-9]+|[A-Z]+(?![a-z])", p) or [p]
        out.append(" ".join(WORDS.get(c, c) for c in chunks))
    return " ".join(out).strip()

def is_32(dtype): return "Double" in dtype or "32" in dtype
def is_bit(dtype): return dtype.strip().lower().startswith("bit")

ANGLE = ("aci","angle","derece","degree","dondurme","rotation")
TIME  = ("sure","time","delay")
SPEED = ("hiz","speed","rpm")

def unit_for(label):
    l = label.lower()
    # UnitType has no Angle member, so an angle is UnitType.None with a degree
    # suffix. Marking it Length would put it through the mm/inch conversion and
    # silently rescale every angle when the operator switches units - which is
    # exactly the class of bug the old pages had.
    if any(w in l for w in ANGLE): return ("None", "\u00B0", 1)
    if any(w in l for w in TIME):  return ("None", "s", 1)
    if any(w in l for w in SPEED): return ("None", "", 0)
    return ("Length", "mm", 1)

def divisor_for(dtype, unit):
    """
    Starting values, uniform by data type. A 32-bit position on these machines
    is an encoder count (x10000); a 16-bit setting carries one decimal (x10).
    Deliberately consistent rather than guessed per field - the previous pages
    had four unexplained divisors, which is harder to correct than one rule.
    """
    if unit == "Angle": return 10.0
    return 10000.0 if is_32(dtype) else 10.0

# ----------------------------------------------------------------------
def load_labels(path):
    rows = []
    with open(path, encoding="utf-8-sig") as fh:
        for r in csv.reader(fh, delimiter=";"):
            if len(r) < 3 or not r[0].strip(): continue
            if r[0].strip().lower() == "label": continue
            if r[0].startswith("#"): continue
            rows.append(dict(label=r[0].strip(), dtype=r[1].strip(), addr=r[2].strip()))
    return rows

def build_movement_fields(rows):
    """
    Pair *_Ileri with *_Geri into movement units, attach the matching reference
    sensor and actual-position word, and leave everything else as a single
    command or a lamp.
    """
    man = [r for r in rows if r["label"].startswith("MAN")]
    sensors = {r["label"]: r for r in rows if r["label"].startswith("IN_")}
    words = {r["label"]: r for r in rows if not is_bit(r["dtype"])}

    used, fields = set(), []
    for r in man:
        if r["addr"] in used: continue
        lab = r["label"]
        m = re.match(r"^(.*?)_(Ileri|Yukari|Acma|Sikma)$", lab)
        if not m: continue
        stem, fwd_word = m.group(1), m.group(2)
        opposite = {"Ileri":"Geri", "Yukari":"Asagi", "Acma":"Sikma", "Sikma":"Acma"}[fwd_word]
        partner = next((x for x in man if x["label"] == stem + "_" + opposite), None)
        if not partner: continue

        axis = stem.replace("MAN__","").replace("MAN_","")
        sensor = next((s for k, s in sensors.items() if axis and axis.split("_")[0] in k and "RefSN" in k), None)
        pos = next((w for k, w in words.items() if axis and k.startswith(axis.split("_")[0]) and "AktPoz" in k), None)

        used.add(r["addr"]); used.add(partner["addr"])
        fields.append(dict(kind="unit", caption=humanise(stem), a=r, b=partner,
                           lamp=sensor, pos=pos))

    for r in man:
        if r["addr"] in used: continue
        used.add(r["addr"])
        fields.append(dict(kind="button", caption=humanise(r["label"]), a=r))

    # sensors that never became a lamp on a unit get their own lamp
    shown = {f.get("lamp", {}).get("addr") for f in fields if f.get("lamp")}
    for k, s in sensors.items():
        if s["addr"] in shown: continue
        fields.append(dict(kind="lamp", caption=humanise(s["label"]), a=s))
    return fields

def build_parameter_fields(rows):
    """PC_ words are the HMI-owned settings; PC_ bits are its option switches."""
    out = []
    for r in rows:
        if not r["label"].startswith("PC_"): continue
        low = r["label"].lower()
        if any(w in low for w in ("screenno","popupno","securitylevel","systemsignal",
                                  "barkod_okunan","programversiyonu")):
            continue                       # HMI plumbing, not an operator setting
        if is_bit(r["dtype"]):
            out.append(dict(kind="check", caption=humanise(r["label"]), a=r))
        else:
            out.append(dict(kind="num", caption=humanise(r["label"]), a=r))
    return out

def build_status_fields(rows):
    out = []
    for r in rows:
        if r["label"].startswith("OUT_"):
            out.append(dict(kind="lamp", caption=humanise(r["label"]), a=r))
        elif not is_bit(r["dtype"]) and re.match(r"^[XYZ]\d", r["label"]):
            out.append(dict(kind="display", caption=humanise(r["label"]), a=r))
    return out

