#!/usr/bin/env python3
"""
Converts Cylinder_Troubleshooting_Manual_EN.pdf into the HTML help document the
HMI opens, with every valve and feedback VARIANT block tagged.

WHY HTML
--------
The manual's own instructions say details 1.4 (valve) and 1.7 (feedback) are
ALTERNATIVES: identify the machine's valve and feedback type and read only the
matching rows. The alarm already knows both - they are the parameters of its
troubleshooting code - so the program can do that selection for the operator.

That cannot be done to the PDF: the variant blocks share pages with other
details, so removing pages removes unrelated content with them. An HTML copy
with one tagged table per block can be filtered exactly, and it keeps the
tables, the formatting and the valve symbols.

RE-RUN THIS whenever the PDF is revised; the PDF stays the source of truth.

    python3 tools/convert_cylinder_manual.py KabanHMI/Help/Cylinder_Troubleshooting_Manual_EN.pdf \
            KabanHMI/Help/Cylinder_Troubleshooting.html
"""
import base64, html, re, sys
import pdfplumber

# Variant name in the manual -> the digit the alarm's code carries.
# The digits are the ones already stored in machines' Alarms.json files; they
# must never be renumbered, only added to.
VALVES = [
    (r"^\(1\)\s*Double solenoid 5/2", 1),
    (r"^\(2\)\s*Single solenoid 5/2", 2),
    (r"^\(3\)\s*Double solenoid 5/3", 3),
]
FEEDBACKS = [
    (r"^Single magnetic sensor", 1),
    (r"^Two end magnetic sensors", 2),
    (r"^Single inductive sensor", 3),
    (r"^Analogue magnetic sensor", 4),
    (r"^Magnetic sensor \+ pressure switch", 5),
    (r"^Incremental encoder \+ reference sensor", 6),
]

FOOTER = re.compile(r"CYLINDER MOVEMENT FAILURE TROUBLESHOOTING MANUAL\s*\u2014\s*\d+\s*$")


def is_bold(w):
    return "Bold" in (w.get("fontname") or "")


def is_blue(w):
    c = w.get("non_stroking_color")
    # The manual's titles are a dark navy (0.12, 0.31, 0.47): blue by BALANCE,
    # not by brightness - a "blue channel above 0.5" test missed every one.
    return isinstance(c, (list, tuple)) and len(c) == 3 and c[2] > c[0] + 0.2 and c[2] >= c[1]


def lines_of(words):
    """Groups words into visual lines, top to bottom."""
    lines = []
    for w in sorted(words, key=lambda w: (round(w["top"]), w["x0"])):
        if lines and abs(lines[-1][0]["top"] - w["top"]) < 3:
            lines[-1].append(w)
        else:
            lines.append([w])
    return [sorted(l, key=lambda w: w["x0"]) for l in lines]


def render_line(line):
    """One line as HTML, bold and blue runs kept."""
    out, run, style = [], [], None
    def flush():
        if not run:
            return
        text = html.escape(" ".join(run))
        if style == "title":
            out.append('<strong class="vt">' + text + "</strong>")
        elif style == "bold":
            out.append("<strong>" + text + "</strong>")
        else:
            out.append(text)
    for w in line:
        s = "title" if (is_bold(w) and is_blue(w)) else ("bold" if is_bold(w) else None)
        if s != style:
            flush()
            run, style = [], s
        run.append(w["text"])
    flush()
    return " ".join(out)


def paragraphs(words, right_edge):
    """
    Lines into paragraphs. A line that stops well short of the right edge ended
    a paragraph; one that reaches it merely wrapped. List items "(1)", bullets
    and a change between bold title and body text also start a new paragraph.
    """
    paras, cur = [], []
    prev = None
    for line in lines_of(words):
        text = " ".join(w["text"] for w in line)
        starts_item = re.match(r"^(\(\d+\)|\u2022|\u2013|-)\s", text) is not None
        kind = "title" if all(is_bold(w) and is_blue(w) for w in line) else "body"
        if prev is not None:
            # A line ending in a hyphen continues, however short it is: the word
            # after it was simply too long to fit ("dropped mid-" / "stroke").
            ended_short = prev["x1"] < right_edge - 40 and not prev["hyphen"]
            if starts_item or ended_short or kind != prev["kind"]:
                paras.append(cur)
                cur = []
        cur.append(line)
        prev = {"x1": line[-1]["x1"], "kind": kind,
                "hyphen": re.search(r"[A-Za-z]-$", text) is not None}
    if cur:
        paras.append(cur)
    return [join_lines(render_line(l) for l in p) for p in paras]


def join_lines(parts):
    """
    Joins wrapped lines. A line ending in a hyphen after a letter is a compound
    split by the wrap - "blow-" / "by" - and is joined WITHOUT a space; otherwise
    the page shows "blow- by". The hyphen itself is real and is kept.
    """
    acc = ""
    for part in parts:
        if acc.endswith("-") and len(acc) > 1 and acc[-2].isalpha():
            acc += part
        else:
            acc = (acc + " " + part) if acc else part
    return acc.strip()


def cell_html(page, bbox, images):
    words = page.within_bbox(bbox).extract_words(
        extra_attrs=["fontname", "size", "non_stroking_color"], keep_blank_chars=False)
    parts = []
    for img in images:
        cx, cy = (img["x0"] + img["x1"]) / 2, (img["top"] + img["bottom"]) / 2
        if bbox[0] <= cx <= bbox[2] and bbox[1] <= cy <= bbox[3]:
            parts.append('<img class="sym" src="%s" alt="">' % img["uri"])
    parts += ["<p>" + p + "</p>" for p in paragraphs(words, bbox[2] - 4) if p]
    return "".join(parts)


def page_images(page):
    found = []
    for im in page.images:
        try:
            data = im["stream"].get_rawdata()
        except Exception:
            continue
        found.append({"x0": im["x0"], "x1": im["x1"], "top": im["top"], "bottom": im["bottom"],
                      "uri": "data:image/jpeg;base64," + base64.b64encode(data).decode("ascii")})
    return found


def table_grid(page, table, images):
    """Rows of cells; None where a merged cell from above covers the slot."""
    grid = []
    for row in table.rows:
        cells = []
        for bbox in row.cells:
            cells.append(None if bbox is None else cell_html(page, bbox, images))
        grid.append(cells)
    return grid


def render_block(rows, header):
    """One table; vertical spans rebuilt from the None slots."""
    ncol = max(len(r) for r in rows)
    span = {}                                   # (row, col) -> rowspan
    for c in range(ncol):
        owner = None
        for r, row in enumerate(rows):
            cell = row[c] if c < len(row) else None
            if cell is None and owner is not None:
                span[owner] += 1
            elif cell is not None:
                owner = (r, c)
                span[owner] = 1
    # Drop rows with nothing in them - the gap between two tables detected as
    # one leaves an empty row behind. Done BEFORE the spans are counted, so a
    # merged cell that covered the dropped row shrinks with it.
    rows = [r for r in rows if any(c for c in r if c)]
    ncol = max(len(r) for r in rows)
    span = {}
    for c in range(ncol):
        owner = None
        for r, row in enumerate(rows):
            cell = row[c] if c < len(row) else None
            if cell is None and owner is not None:
                span[owner] += 1
            elif cell is not None:
                owner = (r, c)
                span[owner] = 1
    # The PDF's own column proportions, on every block: the blocks are one table
    # split for filtering, and should read as one.
    out = ['<table class="detail"><colgroup>' + "".join(
        '<col style="width:%s%%">' % w for w in (5.3, 18.0, 20.0, 19.3, 14.7, 22.7)) + "</colgroup>"]
    if header:
        out.append("<thead><tr>" + "".join("<th>%s</th>" % h for h in header) + "</tr></thead>")
    out.append("<tbody>")
    for r, row in enumerate(rows):
        out.append("<tr>")
        for c in range(ncol):
            cell = row[c] if c < len(row) else None
            if cell is None:
                continue
            rs = span.get((r, c), 1)
            cls = ' class="no"' if c == 0 else (' class="name"' if c == 1 else "")
            out.append("<td%s%s>%s</td>" % (cls, ' rowspan="%d"' % rs if rs > 1 else "", cell))
        out.append("</tr>")
    out.append("</tbody></table>")
    return "".join(out)


def variant_of(detail_no, name_html):
    plain = re.sub(r"<[^>]+>", "", name_html)
    plain = html.unescape(plain).strip()
    table = VALVES if detail_no == "1.4" else FEEDBACKS if detail_no == "1.7" else []
    kind = "valve" if detail_no == "1.4" else "feedback"
    for pattern, digit in table:
        if re.search(pattern, plain):
            return "%s:%d" % (kind, digit)
    if table:
        raise SystemExit("Unrecognised %s variant in %s: %r - add it to the tables above."
                         % (kind, detail_no, plain[:60]))
    return None


def text_outside(page, tables, images):
    """Headings, paragraphs and figures that are not inside a table."""
    boxes = [t.bbox for t in tables]
    def outside(obj):
        cx, cy = (obj["x0"] + obj["x1"]) / 2, (obj["top"] + obj["bottom"]) / 2
        return not any(b[0] <= cx <= b[2] and b[1] <= cy <= b[3] for b in boxes)
    words = [w for w in page.extract_words(extra_attrs=["fontname", "size", "non_stroking_color"])
             if outside(w)]
    items = []
    for line in lines_of(words):
        text = " ".join(w["text"] for w in line)
        if FOOTER.search(text):
            continue
        size = max(w["size"] for w in line)
        items.append({"top": line[0]["top"], "line": line, "size": size})
    for img in images:
        if outside(img):
            items.append({"top": img["top"], "img": img})
    return sorted(items, key=lambda i: i["top"])


def convert(pdf_path, out_path):
    blocks = []            # rendered HTML pieces, in document order
    header = None
    stats = {"blocks": 0, "variants": []}
    current = None         # the detail block being assembled (may continue across pages)

    def close_block():
        nonlocal current
        if current is None:
            return
        no = current["no"]
        var = variant_of(no, current["name"]) if no in ("1.4", "1.7") else None
        attrs = ' data-detail="%s"' % no
        if var:
            attrs += ' data-variant="%s"' % var
            stats["variants"].append(var)
        blocks.append("<section class=\"block\"%s>%s</section>"
                      % (attrs, render_block(current["rows"], header)))
        stats["blocks"] += 1
        current = None

    with pdfplumber.open(pdf_path) as pdf:
        for page in pdf.pages:
            images = page_images(page)
            tables = page.find_tables()

            # THE COLUMNS COME FROM THE HEADER ROW, not from the drawn rules.
            # LibreOffice draws cell borders as short segments and several are
            # missed, so on its own pdfplumber read some Cause cells as running
            # across four columns - folding that row's check method, result and
            # action into the cause, interleaved line by line. Every detail table
            # shares the header's layout, so its column edges are given
            # explicitly; horizontal rules are still read from the page, which is
            # what the row spans depend on.
            head = next((tb for tb in tables
                         if len(tb.rows[0].cells) == 6 and tb.rows[0].cells[0] is not None
                         and page.within_bbox(tb.rows[0].cells[0]).extract_text().startswith("Detail")), None)
            if head is not None:
                xs = [c[0] for c in head.rows[0].cells] + [head.rows[0].cells[-1][2]]
                tables = page.find_tables({"vertical_strategy": "explicit",
                                           "explicit_vertical_lines": xs,
                                           "horizontal_strategy": "lines"})
            flow = text_outside(page, tables, images)
            for t in tables:
                flow.append({"top": t.bbox[1], "table": t})
            flow.sort(key=lambda i: i["top"])

            for item in flow:
                if "line" in item:
                    close_block()
                    line = item["line"]
                    text = render_line(line)
                    if item["size"] >= 15:
                        blocks.append("<h1>%s</h1>" % re.sub(r"</?strong[^>]*>", "", text))
                    elif all(is_bold(w) and is_blue(w) for w in line) and text.startswith("<strong") and "Fig." in text:
                        blocks.append('<p class="caption">%s</p>' % text)
                    elif all(is_bold(w) and is_blue(w) for w in line):
                        blocks.append("<h2>%s</h2>" % re.sub(r"</?strong[^>]*>", "", text))
                    else:
                        # merge wrapped body lines into the previous paragraph
                        if blocks and blocks[-1].startswith("<p>"):
                            blocks[-1] = "<p>" + join_lines([blocks[-1][3:-4], text]) + "</p>"
                        else:
                            blocks.append("<p>%s</p>" % text)
                    continue
                if "img" in item:
                    close_block()
                    blocks.append('<p class="figure"><img src="%s" alt=""></p>' % item["img"]["uri"])
                    continue

                grid = table_grid(page, item["table"], images)
                first = re.sub(r"<[^>]+>", "", grid[0][0] or "")
                if first.startswith("Detail No."):
                    header = [re.sub(r"<[^>]+>", "", c or "") for c in grid[0]]
                    for row in grid[1:]:
                        no = re.sub(r"<[^>]+>", "", row[0] or "").strip()
                        # A repeated header row - two tables detected as one, or a
                        # table continued on a new page - is a header, not a detail.
                        if no.startswith("Detail No."):
                            close_block()
                            continue
                        starts = row[0] is not None and no != ""
                        if starts:
                            close_block()
                            current = {"no": no, "name": row[1] or "", "rows": [row]}
                        elif current is not None:
                            # a block continued from the previous page: its number
                            # and name cells span down into these rows
                            if row[0] == "":
                                row[0] = None
                            if len(row) > 1 and row[1] == "":
                                row[1] = None
                            current["rows"].append(row)
                else:
                    close_block()
                    # boxes on page 1 (CAUTION, POINT, Alarm) - two columns, no spans
                    label = re.sub(r"<[^>]+>", "", grid[0][0] or "").strip().rstrip(":").lower()
                    cls = {"caution": "caution", "point": "point"}.get(label, "info")
                    blocks.append('<table class="%s">' % cls + "".join(
                        "<tr>" + "".join("<td>%s</td>" % (c or "") for c in row if c is not None) + "</tr>"
                        for row in grid) + "</table>")
        close_block()

    page = PAGE_TEMPLATE.replace("{BODY}", "\n".join(blocks))
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(page)
    return stats


PAGE_TEMPLATE = """<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8">
<title>Cylinder movement failure troubleshooting</title>
<style>
 body{font-family:Segoe UI,Arial,sans-serif;max-width:1100px;margin:1.5em auto;padding:0 1.2em;color:#23262c;line-height:1.45;font-size:14px}
 h1{font-size:1.45em;margin:.2em 0 .8em}
 h2{font-size:1.15em;color:#1f4e79;margin:1.6em 0 .4em}
 p{margin:.25em 0}
 table{border-collapse:collapse;width:100%;margin:.6em 0 1em}
 table.detail{table-layout:fixed}
 td,th{border:1px solid #444;padding:.35em .5em;vertical-align:middle;text-align:left}
 th{background:#dcdcdc;text-align:center}
 table.caution td:first-child{background:#f4b6bf;font-weight:bold;text-align:center;width:14%}
 table.point td:first-child{background:#eeeeee;font-weight:bold;text-align:center;width:14%}
 table.info td:first-child{font-weight:bold;width:20%}
 td.no{text-align:center}
 strong.vt{color:#1f4e79}
 img.sym{display:block;max-width:150px;margin:.2em auto .4em}
 p.figure{text-align:center} p.figure img{max-width:100%}
 p.caption{text-align:center;color:#1f4e79}
 .tailored{background:#eef5ff;border:1px solid #9cc2f0;padding:.6em .9em;margin:0 0 1em}
</style></head><body>
{BODY}
</body></html>
"""

if __name__ == "__main__":
    src = sys.argv[1] if len(sys.argv) > 1 else "KabanHMI/Help/Cylinder_Troubleshooting_Manual_EN.pdf"
    dst = sys.argv[2] if len(sys.argv) > 2 else "KabanHMI/Help/Cylinder_Troubleshooting.html"
    s = convert(src, dst)
    print("blocks:", s["blocks"])
    print("variants:", ", ".join(s["variants"]))

