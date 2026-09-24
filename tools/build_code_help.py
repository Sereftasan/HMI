#!/usr/bin/env python3
"""
Builds the per-code help pages the HMI opens from an axis's drive codes.

    python tools/build_code_help.py            (from the solution folder)

WHY THIS EXISTS
---------------
An axis reports three codes (Axis Settings, last three columns):

    Axis alarm    [Md.23] Axis error No.    FX5-40SSC-S manual, 12.5  -> Axis_Error_Codes.en.html
    Axis warning  [Md.24] Axis warning No.  FX5-40SSC-S manual, 12.4  -> Axis_Warning_Codes.en.html
    Servo alarm   [Md.114] Servo alarm      MR-J4 troubleshooting 1.4 / 1.5 -> Servo_Alarm_Codes.en.html

The manuals are PDFs of hundreds of pages. An operator with a stopped axis
needs the ONE entry for the code on the screen, not page one of a book. So the
tables of those chapters are read out here, once, into HTML pages with one
<section class="code" data-code="..."> per code; the HMI copies just the
matching section into a page of its own (Kaban.Core AxisCodeHelp.Extract).

    <section class="code" data-code="16.3" data-group="16" data-kind="alarm"
             data-name="Encoder initial communication error 1 - ..." data-page="32">

data-code is the code as the HMI prints it, data-group the servo alarm number
(a detail the manual does not list falls back to its alarm's sections),
data-kind "alarm" or "warning" (a warning draws the "?" amber), data-name the
text of the menu entry, data-alarm the servo alarm's own name (what an unlisted
detail is called), data-page the PDF page the section came from.

Run it again when a manual is replaced by a newer revision. It needs
pdfplumber (pip install pdfplumber). The output is plain HTML: a translator can
copy a file as .tr.html and translate it, like every other help document.

The PDFs themselves stay in the Help folder: every section links to the page
it came from, so the whole manual is one click away.
"""
import html
import os
import re
import sys
from collections import OrderedDict

import pdfplumber

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
HELP = os.path.join(ROOT, "KabanHMI", "Help")

MRJ4_PDF = "MR_J4_TroubleShooting.pdf"            # SH-030109-L, encrypted: read only
SSC_PDF = "FX5_40SSC_Troubleshooting.pdf"          # IB-0300253-B, chapter 12 (page 1 = cover)

# 1-based PDF pages of the tables.
MRJ4_PAGES = range(24, 121)                        # 1.4 Remedies for alarms + 1.5 Remedies for warnings
MRJ4_WARNINGS_FROM = 99                            # 1.5 starts here
SSC_WARNING_PAGES = range(9, 17)                   # 12.4 List of Warning Codes
SSC_ERROR_PAGES = range(17, 42)                    # 12.5 List of Error Codes
# The excerpt starts with the manual's cover, then its page 544 (chapter 12):
# printed page = excerpt page + 542.
SSC_PRINTED_OFFSET = 542


# ---------------------------------------------------------------------------
# Text of one table cell, with its line structure
# ---------------------------------------------------------------------------
BULLETS = ("\u2022", "\u30fb", "\u00b7", "\uff65")
# Symbol-font characters the manuals use, as the characters they draw.
GLYPHS = {"\uf0c9": "\u2014", "\uf06d": "\u03bc", "\u00b5": "\u03bc", "\uf0b4": "\u00d7", "\u2002": " ",
          "\uf0e0": "\u2192", "\uf0ae": "\u2192", "\uf0ab": "\u2194", "\uf0d3": "III",
          "\uf0a3": "\u2264", "\uf0b3": "\u2265", "\u02da": "\u00b0"}


def _lines(page, bbox):
    """
    The cell's characters as lines of words: [(x0, x1, [word])], a word being
    (text, is_superscript, space_before, x0, x1). A word ends at a space
    character or at a visible gap - the manuals set some spaces as characters
    and some only as distance.
    """
    x0, top, x1, bottom = bbox
    chars = [c for c in page.chars
             if c["x0"] >= x0 - 0.5 and c["x1"] <= x1 + 0.5 and c["top"] >= top - 0.5 and c["bottom"] <= bottom + 0.5]
    ink = [c for c in chars if c["text"].strip() != ""]
    if not ink:
        return []
    body = sorted(c["size"] for c in ink)[len(ink) // 2]
    rows = []
    for c in sorted(chars, key=lambda c: (round(c["bottom"]), c["x0"])):
        sup = c["text"].strip() != "" and c["size"] < body * 0.85
        base = c["bottom"] + (1.6 if sup else 0)          # a superscript sits above the line
        for r in rows:
            if abs(r["base"] - base) <= 2.2:
                r["chars"].append((c, sup))
                break
        else:
            rows.append({"base": base, "chars": [(c, sup)]})
    rows.sort(key=lambda r: r["base"])
    out = []
    for r in rows:
        cs = sorted(r["chars"], key=lambda t: t[0]["x0"])
        words, cur, last, pending_space = [], None, None, False
        for c, sup in cs:
            if c["text"].strip() == "":
                pending_space = True
                continue
            gap = c["x0"] - last["x1"] if last is not None else 0
            split = pending_space or gap > c["size"] * 0.22
            if cur is None or split or sup != cur[1]:
                if cur is not None:
                    words.append(tuple(cur))
                cur = [c["text"], sup, split, c["x0"], c["x1"]]
            else:
                cur[0] += c["text"]
                cur[4] = c["x1"]
            last, pending_space = c, False
        if cur is not None:
            words.append(tuple(cur))
        if words:
            out.append((words[0][3], words[-1][4], words))
    return out


def _render_words(words):
    s = ""
    for i, (w, sup, space_before, _, _) in enumerate(words):
        w = "".join(GLYPHS.get(ch, ch) for ch in w)
        t = html.escape(w)
        if sup:
            t = "<sup>" + t + "</sup>"
        if i > 0 and space_before and not sup:
            s += " "
        s += t
    return s


def cell_html(page, bbox):
    """
    A cell's text as HTML: wrapped lines joined, real line breaks kept,
    bullets as a list.

    A line is a REAL break when the next line's first word would have fitted
    at the end of it - the author pressed Enter, the column did not wrap - or
    when it follows a finished sentence or a "[...]" heading.
    """
    lines = _lines(page, bbox)
    if not lines:
        return ""
    right = bbox[2] - 2.5
    paras = []                          # [kind, html, x0]
    for i, (lx0, lx1, words) in enumerate(lines):
        text = _render_words(words)
        first = words[0][0]
        bullet = first[:1] in BULLETS
        if bullet:
            text = re.sub(r"^(<sup>)?[" + "".join(BULLETS) + r"]\s*", "", text)
            if text == "" and len(words) > 1:
                text = _render_words(words[1:])
            paras.append(["li", text, lx0])
            continue
        if paras:
            prev = paras[-1]
            px1 = lines[i - 1][1]
            size = max(5.0, (words[0][4] - words[0][3]) / max(1, len(first)) * 1.9)
            fits = px1 + size * 0.28 + (words[0][4] - words[0][3]) < right - 2
            ended = re.search(r"[.:]$", prev[1]) and re.match(r"[A-Z0-9\[(\"]", first)
            heading = re.match(r"^\[[^\]]*\]$", _render_words(lines[i - 1][2]))   # "[Operation status ...]"
            hard = fits or ended or heading or re.match(r"^\(\d+\)", first)
            if prev[0] == "li" and lx0 > prev[2] + 2 and not ended:
                hard = False                  # indented continuation of a bullet
            if not hard:
                joiner = "" if prev[1].endswith("-") and not prev[1].endswith(" -") else " "
                prev[1] += joiner + text
                continue
        paras.append(["p", text, lx0])
    out, in_list = [], False
    for kind, text, _ in paras:
        if kind == "li":
            if not in_list:
                out.append('<ul>')
                in_list = True
            out.append("<li>" + text + "</li>")
        else:
            if in_list:
                out.append("</ul>")
                in_list = False
            out.append('<div>' + text + '</div>')
    if in_list:
        out.append("</ul>")
    return "".join(out)


def plain(page, bbox):
    return re.sub(r"<[^>]+>", "", cell_html(page, bbox)).replace("&amp;", "&").strip()


# ---------------------------------------------------------------------------
# A table as a grid of cells with their spans
# ---------------------------------------------------------------------------
def _snap(values, tol=1.6):
    out = []
    for v in sorted(values):
        if not out or v - out[-1] > tol:
            out.append(v)
    return out


def _index(edges, v, tol=1.6):
    for i, e in enumerate(edges):
        if abs(e - v) <= tol:
            return i
    return min(range(len(edges)), key=lambda i: abs(edges[i] - v))


def grid(page, table):
    xs = _snap([c[0] for c in table.cells] + [c[2] for c in table.cells])
    ys = _snap([c[1] for c in table.cells] + [c[3] for c in table.cells])
    cells = []
    for b in table.cells:
        r0, r1 = _index(ys, b[1]), _index(ys, b[3])
        c0, c1 = _index(xs, b[0]), _index(xs, b[2])
        if r1 <= r0 or c1 <= c0:
            continue
        cells.append({"r0": r0, "r1": r1, "c0": c0, "c1": c1, "bbox": b})
    cells.sort(key=lambda c: (c["r0"], c["c0"]))
    return cells, len(ys) - 1, len(xs) - 1


def rows_html(page, cells, r_from, r_to, header=False):
    """<tr>s for grid rows [r_from, r_to), spans clipped to that range."""
    out = []
    for r in range(r_from, r_to):
        tds = []
        for c in cells:
            starts_here = c["r0"] == r or (r == r_from and c["r0"] < r_from < c["r1"])
            if not starts_here:
                continue
            span = min(c["r1"], r_to) - max(c["r0"], r_from)
            attrs = ""
            if span > 1:
                attrs += ' rowspan="%d"' % span
            if c["c1"] - c["c0"] > 1:
                attrs += ' colspan="%d"' % (c["c1"] - c["c0"])
            tag = "th" if header else "td"
            tds.append("<%s%s>%s</%s>" % (tag, attrs, cell_html(page, c["bbox"]), tag))
        out.append("<tr>" + "".join(tds) + "</tr>")
    return "\n".join(out)


# ---------------------------------------------------------------------------
# MR-J4: one section per alarm DETAIL (16.3), grouped by alarm (16)
# ---------------------------------------------------------------------------
CODE_RE = re.compile(r"^([0-9A-F]{2})\.([0-9A-F])$")


def build_mrj4():
    path = os.path.join(HELP, MRJ4_PDF)
    pdf = pdfplumber.open(path)
    alarms = OrderedDict()          # "16" -> {name, content, details: OrderedDict("16.3" -> {...})}
    for pn in MRJ4_PAGES:
        page = pdf.pages[pn - 1]
        for t in page.find_tables():
            cells, nrows, ncols = grid(page, t)
            if nrows < 3 or ncols < 5:
                continue
            first = [c for c in cells if c["r0"] == 0]
            head = plain(page, first[0]["bbox"]) if first else ""
            m = re.match(r"Alarm No\.:\s*([0-9A-F]{2})\b", head)
            if not m:
                continue
            no = m.group(1)
            name = ""
            for c in first[1:]:
                txt = plain(page, c["bbox"])
                if txt.startswith("Name:"):
                    name = txt[5:].strip()
            content = ""
            for c in cells:
                if c["r0"] == 1 and c["c0"] > 0:
                    content = cell_html(page, c["bbox"])
            # the column header row: the one whose first cell reads "Detail No."
            hdr = next((c["r0"] for c in cells if c["c0"] == 0 and plain(page, c["bbox"]).startswith("Detail")), 2)
            header = rows_html(page, cells, hdr, hdr + 1, header=True)
            starts = []
            for c in cells:
                if c["c0"] == 0 and c["r0"] > hdr:
                    code = plain(page, c["bbox"]).replace(" ", "")
                    if CODE_RE.match(code):
                        starts.append((c["r0"], c["r1"], code))
            starts.sort()
            a = alarms.setdefault(no, {"name": name, "content": content, "details": OrderedDict(), "page": pn})
            for i, (r0, r1, code) in enumerate(starts):
                r_end = starts[i + 1][0] if i + 1 < len(starts) else nrows
                dname = ""
                for c in cells:
                    if c["r0"] == r0 and c["c0"] == 1:
                        dname = plain(page, c["bbox"])
                d = a["details"].setdefault(code, {"name": dname, "parts": [], "page": pn})
                d["parts"].append((header, rows_html(page, cells, r0, r_end)))
    return alarms


def printed_mrj4(pn):
    # The alarm chapter's page 1-16 is PDF page 23.
    return "1-%d" % (pn - 7)


def write_mrj4(alarms):
    n = 0
    body = []
    for no, a in alarms.items():
        warning = a["page"] >= MRJ4_WARNINGS_FROM
        for code, d in a["details"].items():
            title = "AL. %s &mdash; %s" % (code, html.escape(d["name"] or a["name"]))
            body.append('<section class="code" data-code="%s" data-group="%s" data-kind="%s" data-alarm="%s" data-name="%s" data-page="%d">'
                        % (code, no, "warning" if warning else "alarm", html.escape(a["name"], quote=True),
                           html.escape(d["name"] or a["name"], quote=True), d["page"]))
            body.append('<h2 id="AL%s">%s</h2>' % (code, title))
            body.append('<div class="alarm"><b>%s %s: %s</b>%s</div>'
                        % ("Warning" if warning else "Alarm", no, html.escape(a["name"]), a["content"]))
            for header, rows in d["parts"]:
                body.append('<table>\n%s\n%s\n</table>' % (header, rows))
            body.append('<p class="src">MR-J4 Troubleshooting (SH-030109-L), page %s &middot; '
                        '<a href="%s#page=%d">open the manual at this page</a></p>'
                        % (printed_mrj4(d["page"]), MRJ4_PDF, d["page"]))
            body.append('</section>')
            n += 1
    intro = """<p>The code in the <b>Servo alarm</b> column of Axis Settings is the number on the servo amplifier's display:
<b>16.3</b> is alarm 16, detail 3. This page is the part of the MR-J4 troubleshooting manual for that code.</p>
<div class="safety"><b>CAUTION (from the manual).</b> When any alarm has occurred, eliminate its cause, ensure safety, and
deactivate the alarm before restarting operation. If [AL. 25 Absolute position erased] occurs, always make home position
setting again. As soon as an alarm occurs, make the Servo-off status and interrupt the main circuit power.</div>
<div class="note"><b>Do not cycle the power repeatedly</b> after [AL. 30 Regenerative error], [AL. 45 Main circuit device
overheat], [AL. 46 Servo motor overheat], [AL. 50 Overload 1] or [AL. 51 Overload 2]. Remove the cause and allow about
30 minutes for cooling before resuming the operation.</div>
<p class="how">Work down the table: check each <b>Cause</b> in order with its <b>Check method</b>; the <b>Check result</b>
tells you the <b>Action</b>, or which cause to check next. <b>Target</b> lists the amplifier types the row applies to
(<b>[B]</b> is the SSCNET III/H amplifier, MR-J4-_B_).</p>"""
    write("Servo_Alarm_Codes.en.html", "Servo amplifier alarms and warnings (MR-J4)", intro, "\n".join(body))
    return n


# ---------------------------------------------------------------------------
# FX5-40SSC-S: one section per error / warning code (1904H)
# ---------------------------------------------------------------------------
HEX_RE = re.compile(r"^([0-9A-F]{4})H$")

ERROR_CLASSES = [
    (0x1080, 0x1080, "IF (interface) errors"), (0x18B0, 0x18B1, "IF (interface) errors"),
    (0x1900, 0x193F, "Positioning control common errors"), (0x1940, 0x197F, "Home position return errors"),
    (0x1980, 0x198F, "JOG, inching and manual pulse generator operation errors"),
    (0x1990, 0x19EF, "Positioning operation errors"), (0x19F0, 0x19FF, "Block start data errors"),
    (0x1A00, 0x1A0F, "Condition data errors"), (0x1A10, 0x1A5F, "Positioning data errors"),
    (0x1A60, 0x1A9F, "Basic parameter errors"), (0x1AA0, 0x1AFF, "Detailed parameter errors"),
    (0x1B00, 0x1B3F, "Home position return parameter errors"), (0x1B40, 0x1B9F, "Expansion/Common parameter errors"),
    (0x1BA0, 0x1BDF, "Synchronous control input axis errors"), (0x1BE0, 0x1C3F, "Synchronous control output axis errors"),
    (0x1C80, 0x1C8F, "Servo amplifier errors"), (0x3000, 0x30FF, "Initial process errors (moderate)"),
]
WARNING_CLASSES = [
    (0x0900, 0x093F, "Positioning control common warnings"),
    (0x0980, 0x098F, "JOG, inching and manual pulse generator operation warnings"),
    (0x0990, 0x09EF, "Positioning operation warnings"), (0x09F0, 0x09FF, "Block start data warnings"),
    (0x0A10, 0x0A5F, "Positioning data warnings"), (0x0BA0, 0x0BDF, "Synchronous control input axis warnings"),
    (0x0BE0, 0x0C3F, "Synchronous control output axis warnings"), (0x0C40, 0x0C7F, "Cam data operation warnings"),
    (0x0C80, 0x0C8F, "Servo amplifier warnings"),
]


def classify(code, classes):
    v = int(code, 16)
    for lo, hi, name in classes:
        if lo <= v <= hi:
            return "%s (%04XH&ndash;%04XH)" % (name, lo, hi) if lo != hi else name
    return ""


def columns(page):
    """The table's column lines: the long verticals, plus the rules' two ends."""
    vs = _snap([e["x0"] for e in page.edges if e["orientation"] == "v" and e["bottom"] - e["top"] > 150], 2.0)
    hs = [e for e in page.edges if e["orientation"] == "h" and e["x1"] - e["x0"] > 300]
    left = min(e["x0"] for e in hs)
    right = max(e["x1"] for e in hs)
    return [left] + [v for v in vs if left + 5 < v < right - 5] + [right]


def build_ssc(pages):
    pdf = pdfplumber.open(os.path.join(HELP, SSC_PDF))
    codes = OrderedDict()           # "1904" -> {name, rows: [(details_html, remedy_html)], page}
    notes = {}                      # "*1" -> text
    last = None
    for pn in pages:
        page = pdf.pages[pn - 1]
        xs = columns(page)
        ts = page.find_tables({"vertical_strategy": "explicit", "explicit_vertical_lines": xs,
                               "horizontal_strategy": "lines"})
        bottom = 0
        for t in ts:
            if t.bbox[2] - t.bbox[0] < 300:
                continue
            bottom = max(bottom, t.bbox[3])
            cells, nrows, ncols = grid(page, t)
            by_row = {}
            for c in cells:
                by_row.setdefault(c["r0"], []).append(c)
            for r in sorted(by_row):
                row = sorted(by_row[r], key=lambda c: c["c0"])
                col = {c["c0"]: c for c in row}
                if 0 in col:
                    code = plain(page, col[0]["bbox"]).replace(" ", "")
                    if not HEX_RE.match(code):
                        continue            # the header row
                    code = code[:4]
                    name = plain(page, col[1]["bbox"]) if 1 in col else ""
                    last = codes.setdefault(code, {"name": name, "rows": [], "page": pn})
                if last is None:
                    continue
                det = cell_html(page, col[2]["bbox"]) if 2 in col else ""
                rem = cell_html(page, col[3]["bbox"]) if 3 in col else ""
                if det or rem:
                    last["rows"].append((det, rem))
        # footnotes under the table: "*1  text..."
        if bottom and bottom + 10 < page.height - 60:
            below = page.crop((0, bottom + 1, page.width, page.height - 60))
            txt = below.extract_text() or ""
            cur = None
            for line in txt.split("\n"):
                m = re.match(r"^\*(\d)\s+(.*)", line.strip())
                if m:
                    cur = "*" + m.group(1)
                    notes[cur] = m.group(2)
                elif cur and line.strip() and not line.strip().startswith(("Servo amplifier", "For details", "12 ")):
                    notes[cur] += " " + line.strip()
                else:
                    cur = None
    return codes, notes


def write_ssc(codes, notes, file_name, title, what, classes, intro):
    kind = "warning" if what == "Warning" else "alarm"
    body = []
    for code, c in codes.items():
        cls = classify(code, classes)
        body.append('<section class="code" data-code="%s" data-group="%s" data-kind="%s" data-name="%s" data-page="%d">'
                    % (code, code, kind, html.escape(c["name"], quote=True), c["page"]))
        body.append('<h2 id="C%s">%sH &mdash; %s</h2>' % (code, code, html.escape(c["name"])))
        if cls:
            body.append('<p class="cls">%s</p>' % cls)
        body.append('<table>\n<tr><th>%s details and causes</th><th>Remedy</th></tr>' % what)
        for det, rem in c["rows"]:
            body.append('<tr><td>%s</td><td>%s</td></tr>' % (det, rem))
        body.append('</table>')
        used = sorted(set(re.findall(r"<sup>(\*\d)</sup>", "".join(d + r for d, r in c["rows"]))))
        for n in used:
            if n in notes:
                body.append('<p class="fn"><sup>%s</sup> %s</p>' % (n, html.escape(notes[n])))
        body.append('<p class="src">MELSEC iQ-F FX5 Simple Motion Module User\'s Manual (Application), IB-0300253-B, '
                    'page %d &middot; <a href="%s#page=%d">open the manual at this page</a></p>'
                    % (c["page"] + SSC_PRINTED_OFFSET, SSC_PDF, c["page"]))
        body.append('</section>')
    write(file_name, title, intro, "\n".join(body))
    return len(codes)


# ---------------------------------------------------------------------------
STYLE = """<style>
 body{font-family:"Segoe UI",Arial,sans-serif;max-width:1100px;margin:1.5em auto;padding:0 1.5em;color:#23262c;line-height:1.45;font-size:14px}
 h1{font-size:1.45em;border-bottom:2px solid #1e78dc;padding-bottom:.3em}
 h2{font-size:1.2em;margin:1.6em 0 .5em;background:#f2f5f9;padding:.45em .7em;border-left:4px solid #1e78dc}
 table{border-collapse:collapse;width:100%;margin:.5em 0 .8em}
 th,td{border:1px solid #9aa1ab;padding:.35em .5em;vertical-align:top;text-align:left}
 th{background:#e3e8ef;font-weight:600}
 td ul{margin:.1em 0 .1em 1.1em;padding:0} td li{margin:.05em 0}
 .alarm{background:#fafbfc;border:1px solid #d5d9df;padding:.5em .8em;margin:.4em 0}
 .alarm ul{margin:.2em 0 0 1.2em;padding:0}
 .cls{color:#5b6270;margin:.2em 0 .4em}
 .src{color:#5b6270;font-size:.9em} .fn{font-size:.92em;color:#3b4250}
 .safety{background:#fdecec;border-left:4px solid #d62424;padding:.6em .9em;margin:.8em 0}
 .note{background:#fff8e1;border-left:4px solid #e0a800;padding:.6em .9em;margin:.8em 0}
 .how{background:#e9f3fd;border-left:4px solid #1e78dc;padding:.6em .9em;margin:.8em 0}
 .missing{background:#fff3cd;border-left:4px solid #d7a528;padding:.7em 1em;margin:1em 0}
 @media print{h2{break-after:avoid}section{break-inside:avoid-page}}
</style>"""


def write(name, title, intro, body):
    page = """<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8">
<title>%s</title>
<!-- GENERATED by tools/build_code_help.py from the manufacturer's manual - edit
     the script, not this file, or the next run will undo the edit. A translation
     (.tr.html) is a copy of this file and is not touched by the script. -->
%s
</head><body>
<h1>%s</h1>
%s
%s
</body></html>
""" % (html.escape(title), STYLE, html.escape(title), intro, body)
    with open(os.path.join(HELP, name), "w", encoding="utf-8", newline="\r\n") as f:
        f.write(page)


def main():
    alarms = build_mrj4()
    n1 = write_mrj4(alarms)

    w, wn = build_ssc(SSC_WARNING_PAGES)
    e, en = build_ssc(SSC_ERROR_PAGES)
    clear = """<div class="how"><b>Clearing it.</b> Remove the cause first, then reset. The module clears the code when
the PLC sets <b>[Cd.5] Axis error reset</b> for the axis; which button does that on this machine is set by its PLC
program.</div>"""
    n2 = write_ssc(w, wn, "Axis_Warning_Codes.en.html", "Axis warnings (Simple Motion module)", "Warning", WARNING_CLASSES,
                   """<p>The code in the <b>Axis warning</b> column of Axis Settings is the Simple Motion module's warning
number ([Md.24] Axis warning No.). A warning does not stop the axis, but the operation it refers to is not carried out.
This page is the part of the module's manual for that code.</p>""" + clear +
                   """<p class="note">Codes from the <b>servo amplifier</b> are not in this list: they are in the
<b>Servo alarm</b> column and have their own help.</p>""")
    n3 = write_ssc(e, en, "Axis_Error_Codes.en.html", "Axis errors (Simple Motion module)", "Error", ERROR_CLASSES,
                   """<p>The code in the <b>Axis alarm</b> column of Axis Settings is the Simple Motion module's error
number ([Md.23] Axis error No.). This page is the part of the module's manual for that code.</p>""" + clear +
                   """<p class="note"><b>1C80H</b> means the servo amplifier itself raised an alarm: see the
<b>Servo alarm</b> column for its code.</p>""")
    print("servo alarm details: %d, axis warnings: %d, axis errors: %d" % (n1, n2, n3))


if __name__ == "__main__":
    sys.exit(main())
