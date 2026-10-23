#!/usr/bin/env python3
"""The keys the program uses with an English text of its own, missing from the translation files -> added.

23 October: "most of the control tooltips (apart from the HMI controls) doesn't have translation keys so they
are shown in English only". A text written as T("Tip_TrnSave", "Save: ...") shows its English wherever the
translation files have no row for its key - and HMI Settings > Translation, which lists the files' rows, could
not show it to translate (nor the AI briefing, nor "A text missing"). This adds each such key with its English
text (its other languages empty: English is shown until they are translated - on the panel, by the AI, or in
the files):

    python tools/missing_keys.py            (from the project folder; --check: list them, add nothing)

A key used in one machine's project only goes into that machine's Translations.csv, the rest into
KabanHMI/CommonTranslations.csv. The texts as the code writes them: its "\\r\\n" as the files write them, a ';'
(the files' separator) as ','. A text made of more than its literals (a value added to it) is not added.
Files: UTF-8 with a BOM, their line endings kept.
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import translation_uses as tu  # noqa: E402

CALL = re.compile(r'\b(?:T|GetString|LanguageText)\(\s*"([A-Za-z][A-Za-z0-9_.]{1,80})"\s*,\s*')
LITERAL = re.compile(r'(@?)"((?:[^"\\]|\\.|"")*)"')


def literals(src, at):
    """The literals joined with + from at; None when the text is more than literals."""
    parts = []
    i = at
    while True:
        m = LITERAL.match(src, i)
        if not m:
            return None
        verbatim, body = m.group(1), m.group(2)
        if verbatim:
            body = body.replace('""', '"').replace("\r\n", "\\r\\n").replace("\n", "\\n")
        else:
            body = body.replace('\\"', '"').replace("\\\\", "\\")
        parts.append(body)
        i = m.end()
        j = i
        while j < len(src) and src[j] in " \t\r\n":
            j += 1
        if j < len(src) and src[j] == "+":
            j += 1
            while j < len(src) and src[j] in " \t\r\n":
                j += 1
            i = j
            continue
        # the call's end (or its next argument): the text is these literals
        if j < len(src) and src[j] in "),":
            return "".join(parts)
        return None


def used():
    """Every key with its English text, and the projects using it."""
    found = {}
    for project in tu.projects():
        name = os.path.basename(project)
        for path in tu.sources(project):
            src = open(path, encoding="utf-8-sig", errors="replace").read()
            for m in CALL.finditer(src):
                key = m.group(1)
                text = literals(src, m.end())
                if text is None or not text.strip():
                    continue
                entry = found.setdefault(key, {"text": text, "projects": set(), "where": os.path.relpath(path, ROOT)})
                entry["projects"].add(name)
    return found


def target(projects):
    if len(projects) == 1:
        p = next(iter(projects))
        csv = os.path.join(ROOT, p, "Translations.csv")
        if p.startswith("Kaban.Machines.") and os.path.isfile(csv):
            return csv
    return os.path.join(ROOT, "KabanHMI", "CommonTranslations.csv")


def main():
    check = "--check" in sys.argv
    have = tu.keys()
    missing = {k: v for k, v in used().items() if k not in have}
    by_file = {}
    for k in sorted(missing):
        by_file.setdefault(target(missing[k]["projects"]), []).append(k)
    for path, keys in sorted(by_file.items()):
        print("%s: %d key(s)" % (os.path.relpath(path, ROOT), len(keys)))
        if check:
            for k in keys:
                print("  %s;%s" % (k, missing[k]["text"]))
            continue
        raw = open(path, "rb").read()
        newline = "\r\n" if b"\r\n" in raw else "\n"           # kept as the file has them
        lines = raw.decode("utf-8-sig").splitlines()
        columns = len(lines[0].split(";"))
        for k in keys:
            text = missing[k]["text"].replace(";", ",").strip()
            lines.append(";".join([k, text] + [""] * (columns - 2)))
        with open(path, "w", encoding="utf-8-sig", newline=newline) as f:
            f.write("\n".join(lines) + "\n")
    print("%d key(s) %s" % (len(missing), "missing" if check else "added"))


if __name__ == "__main__":
    main()
