#!/usr/bin/env python3
"""The program's lines naming a translation key -> KabanHMI/TranslationUses.csv.

HMI Settings > Translation, "Where is it used?" (Ctrl+F), lists the program's lines naming a
key. Running from its build it reads the source itself; a panel has no source, so this list
ships beside the program (22 October: "ship a list of program lines so it works without the
source - regenerated each batch").

    python tools/translation_uses.py            (from the project folder)

Key;File;Line;Kind;Text - Kind: Message, Status, Heading, Tooltip, Menu or Text (the page
shows it in the panel's language); Text: the line as it is (it may hold ';' - the rest of the
line after the fourth one). The keys: every key of the translation files (CommonTranslations.csv,
each machine's Translations.csv). Kept in step with KabanHMI/Helpers/TextUses.cs (InCode,
KindOfLine).
"""
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "KabanHMI", "TranslationUses.csv")
SKIP = re.compile(r"[\\/](bin|obj|\.vs|packages)[\\/]", re.I)
QUOTED = re.compile(r'"([^"\\\s]{2,120})"')


def read(path):
    with open(path, encoding="utf-8-sig", errors="replace") as f:
        return f.read().splitlines()


def keys():
    files = [os.path.join(ROOT, "KabanHMI", "CommonTranslations.csv")]
    for d in sorted(os.listdir(ROOT)):
        p = os.path.join(ROOT, d, "Translations.csv")
        if os.path.isfile(p):
            files.append(p)
    found = set()
    for p in files:
        if not os.path.isfile(p):
            continue
        for line in read(p)[1:]:
            k = line.split(";")[0].strip()
            if k:
                found.add(k)
    return found


def projects():
    for d in sorted(os.listdir(ROOT)):
        p = os.path.join(ROOT, d)
        if os.path.isdir(p) and any(f.endswith(".csproj") for f in os.listdir(p)):
            yield p


def sources(project):
    for base, dirs, files in os.walk(project):
        dirs.sort()
        for f in sorted(files):
            if not f.endswith(".cs"):
                continue
            path = os.path.join(base, f)
            if SKIP.search(path[len(project):] + os.sep):
                continue
            yield path


def kind(lines, at):
    """As TextUses.KindOfLine: the statement may begin a line or two above."""
    text = " ".join(lines[max(0, at - 2):at + 1])
    line = lines[at]
    if re.search(r"MessageBox\.Show|Ask\.(Tell|YesNo|Text)|\.Tell\(", text):
        return "Message"
    if re.search(r"SetStatus|_status\.Text|Status\s*\(", text):
        return "Status"
    if "HeaderText" in line:
        return "Heading"
    if re.search(r"SetTip|ToolTip|Tip_|ToolButton\(", line):
        return "Tooltip"
    if re.search(r"ToolStripMenuItem|Item\(", line):
        return "Menu"
    return "Text"


def main():
    wanted = keys()
    rows = []
    for project in projects():
        for path in sources(project):
            lines = read(path)
            rel = os.path.relpath(path, ROOT).replace("/", "\\")
            for i, line in enumerate(lines):
                s = line.strip()
                if s.startswith("//"):
                    continue
                for k in sorted(set(QUOTED.findall(line))):
                    if k not in wanted:
                        continue
                    t = s if len(s) <= 180 else s[:177] + "..."
                    rows.append((k, rel, i + 1, kind(lines, i), t))
    rows.sort(key=lambda r: (r[0].lower(), r[1].lower(), r[2]))
    with open(OUT, "w", encoding="utf-8-sig", newline="") as f:
        f.write("Key;File;Line;Kind;Text\r\n")
        for r in rows:
            f.write("%s;%s;%d;%s;%s\r\n" % r)
    print("%d line(s) of %d key(s) -> %s" % (len(rows), len({r[0] for r in rows}), os.path.relpath(OUT, ROOT)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
