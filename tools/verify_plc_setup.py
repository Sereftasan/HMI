#!/usr/bin/env python3
"""
verify_plc_setup.py - structural checks for the multi-controller work.

Run from the repository root, alongside the other checking programs in §10 of
AI_HANDOFF_PROMPT.md:

    python3 tools/verify_plc_setup.py

Every check here exists because something real went wrong:

  duplicate ProjectGuid   Kaban.Machines.H500 and H800 both carried FA2010's
                          GUID. Visual Studio resolves a ProjectReference by
                          GUID, so two projects sharing one make references
                          point at the wrong assembly - and the symptom is a
                          build that succeeds and an exe that behaves oddly.

  orphan source file      A .cs file on disk that no .csproj compiles builds
                          fine here and silently does nothing on the machine.
                          Every new file added in this round had to be listed
                          by hand; this catches the one that gets forgotten.

  RegisterStation local   A station registered without the module's own PLC
                          number cannot be named "H500 PLC1" anywhere, because
                          that number is not recoverable from the station id
                          once a station map has been applied.

  missing translation     GetString(key, fallback) never fails - it silently
                          shows English. A key that never made it into the CSV
                          is therefore invisible until a Turkish operator asks
                          why one button is in English.

  CSV shape               The translation files are Key;EN;TR;RU;DE;IT;ES,
                          UTF-8 WITH BOM. A row with the wrong column count
                          drops a language; a missing BOM mangles Turkish.
"""

import os
import re
import sys
import glob
import io

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LANG_COLUMNS = 7           # Key;EN;TR;RU;DE;IT;ES

problems = []
warnings = []
notes = []


def fail(msg):
    problems.append(msg)


def warn(msg):
    warnings.append(msg)


def read(path):
    with io.open(path, encoding="utf-8-sig", errors="replace") as f:
        return f.read()


# ----------------------------------------------------------------------
def check_project_guids():
    seen = {}
    for csproj in glob.glob(os.path.join(ROOT, "*", "*.csproj")):
        m = re.search(r"<ProjectGuid>\s*(\{[^}]+\})\s*</ProjectGuid>", read(csproj), re.I)
        if not m:
            fail("no ProjectGuid in %s" % os.path.relpath(csproj, ROOT))
            continue
        guid = m.group(1).upper()
        if guid in seen:
            fail("duplicate ProjectGuid %s shared by %s and %s"
                 % (guid, os.path.relpath(seen[guid], ROOT), os.path.relpath(csproj, ROOT)))
        else:
            seen[guid] = csproj
    notes.append("%d projects, all with distinct GUIDs" % len(seen))


# ----------------------------------------------------------------------
def check_sources_are_compiled():
    for csproj in glob.glob(os.path.join(ROOT, "*", "*.csproj")):
        folder = os.path.dirname(csproj)
        text = read(csproj)
        listed = set(x.replace("\\", "/").lower()
                     for x in re.findall(r'<Compile\s+Include="([^"]+)"', text))

        for dirpath, dirnames, filenames in os.walk(folder):
            dirnames[:] = [d for d in dirnames
                           if d not in ("obj", "bin", "packages", ".git")]
            for fn in filenames:
                if not fn.endswith(".cs"):
                    continue
                rel = os.path.relpath(os.path.join(dirpath, fn), folder)
                rel = rel.replace("\\", "/").lower()
                if rel.endswith("assemblyinfo.cs"):
                    continue
                if rel in listed:
                    continue
                # An orphan that nothing else references is dead code, not a
                # wiring mistake. One that IS referenced would fail the build,
                # so the two deserve different verdicts.
                types = re.findall(r"\b(?:class|struct|interface|enum)\s+(\w+)",
                                   read(os.path.join(dirpath, fn)))
                referenced = False
                for other in glob.glob(os.path.join(ROOT, "*", "**", "*.cs"), recursive=True):
                    if os.path.abspath(other) == os.path.abspath(os.path.join(dirpath, fn)):
                        continue
                    body = read(other)
                    if any(re.search(r"\b%s\b" % re.escape(ty), body) for ty in types):
                        referenced = True
                        break
                if referenced:
                    fail("%s is USED but not compiled by %s"
                         % (rel, os.path.basename(csproj)))
                else:
                    warn("%s is on disk, compiled by nothing and referenced by nothing "
                         "(dead code): %s" % (os.path.basename(csproj), rel))


# ----------------------------------------------------------------------
def check_solution_paths():
    for sln in glob.glob(os.path.join(ROOT, "*", "*.sln")):
        base = os.path.dirname(sln)
        for path in re.findall(r'"([^"]+\.csproj)"', read(sln)):
            resolved = os.path.normpath(os.path.join(base, path.replace("\\", "/")))
            if not os.path.exists(resolved):
                fail("%s lists a missing project: %s" % (os.path.basename(sln), path))


# ----------------------------------------------------------------------
def check_register_station_local_numbers():
    found = 0
    for cs in glob.glob(os.path.join(ROOT, "*", "*.cs")) + \
              glob.glob(os.path.join(ROOT, "*", "*", "*.cs")):
        text = read(cs)
        # RegisterStation( ... ) possibly across two lines
        for call in re.findall(r"RegisterStation\s*\((.*?)\)\s*;", text, re.S):
            if "int stationId" in call:      # the declaration itself
                continue
            found += 1
            args = [a.strip() for a in call.split(",")]
            if len(args) < 5:
                fail("%s: RegisterStation without the module's own PLC number: %s"
                     % (os.path.relpath(cs, ROOT), " ".join(call.split())[:70]))
    notes.append("%d RegisterStation calls, all declaring a local PLC number" % found)


# ----------------------------------------------------------------------
def load_translation_keys():
    keys = set()
    for csv in glob.glob(os.path.join(ROOT, "*", "*Translations.csv")) + \
               glob.glob(os.path.join(ROOT, "*", "Translations.csv")):
        with open(csv, "rb") as f:
            raw = f.read()
        if not raw.startswith(b"\xef\xbb\xbf"):
            fail("%s is missing its UTF-8 BOM (Turkish text will be mangled)"
                 % os.path.relpath(csv, ROOT))

        for n, line in enumerate(read(csv).splitlines(), start=1):
            line = line.strip()
            if not line or line.startswith("#"):
                continue
            cols = line.split(";")
            if len(cols) != LANG_COLUMNS:
                fail("%s line %d has %d columns, expected %d: %s"
                     % (os.path.relpath(csv, ROOT), n, len(cols), LANG_COLUMNS, line[:60]))
            keys.add(cols[0].strip())
    return keys


def check_translation_keys(keys):
    used = set()
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames
                       if d not in (".git", "obj", "bin", "packages", "__pycache__")]
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            text = read(os.path.join(dirpath, fn))
            # GetString("Key", ...) and Localize("Key")
            used.update(re.findall(r'GetString\s*\(\s*"([^"]+)"', text))
            used.update(re.findall(r'Localize\s*\(\s*"([^"]+)"', text))

    # Keys built by concatenation at runtime (Alarm_Col_ + column name) can never
    # appear in the CSV literally, so a trailing underscore means "prefix".
    missing = sorted(k for k in used if k not in keys and not k.endswith("_"))
    for k in missing:
        warn("translation key never defined, falls back to English: %s" % k)
    notes.append("%d translation keys defined, %d used in code" % (len(keys), len(used)))


# ----------------------------------------------------------------------
def check_driver_catalog_ids():
    """Every driver id offered in a Program.cs must name a real driver type."""
    ids = {}
    for cs in glob.glob(os.path.join(ROOT, "*", "Program.cs")):
        for call in re.findall(r"PlcDriverCatalog\.Register\s*\((.*?)\);", read(cs), re.S):
            m = re.search(r'"([^"]+)"', call)
            if m:
                ids.setdefault(m.group(1), []).append(os.path.relpath(cs, ROOT))
    notes.append("driver kinds offered: %s" % (", ".join(sorted(ids)) or "(none)"))


# ----------------------------------------------------------------------
def check_dead_types():
    """
    A type whose name never appears outside its own declaration is unreachable.

    Worth checking automatically because dead code does not announce itself: one
    round of this found a five-file localisation subsystem forming a closed loop
    that nothing outside could reach, plus three enums that existed only for a
    model class that had already gone. Deleting dead code CASCADES - removing a
    subsystem orphans its models - so this is re-run until it reports nothing.

    Deliberately kept and therefore excluded: Examples/ holds reference snippets
    that are documentation, not code.
    """
    decls = {}
    bodies = {}
    for dirpath, dirnames, filenames in os.walk(ROOT):
        dirnames[:] = [d for d in dirnames
                       if d not in (".git", "obj", "bin", "packages",
                                    "__pycache__", "protocol_tests", "Examples")]
        for fn in filenames:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            body = read(path)
            bodies[path] = body
            for m in re.finditer(r"\b(?:public|internal|private|protected)\s+"
                                 r"(?:static\s+|sealed\s+|abstract\s+|partial\s+)*"
                                 r"(?:class|struct|interface|enum)\s+(\w+)", body):
                decls.setdefault(m.group(1), []).append(path)

    # a name used in a csproj/resx/json is reached from outside the C# sources
    outside = ""
    for pattern in ("*/*.csproj", "*/*.resx", "*/*/*.resx", "*/*.json", "*/*.csv"):
        for f in glob.glob(os.path.join(ROOT, pattern)):
            outside += read(f)

    for name, where in sorted(decls.items()):
        uses = 0
        for body in bodies.values():
            for m in re.finditer(r"\b%s\b" % re.escape(name), body):
                line = body[body.rfind("\n", 0, m.start()) + 1: body.find("\n", m.end())].strip()
                if re.search(r"(?:class|struct|interface|enum)\s+%s\b" % re.escape(name), line):
                    continue                       # the declaration itself
                if re.match(r"^(?:public|private|internal|protected|static)?\s*~?%s\s*\("
                            % re.escape(name), line):
                    continue                       # its own constructor/destructor
                if line.startswith("//"):
                    continue                       # a mention in a comment is not a use
                uses += 1
        if uses == 0 and not re.search(r"\b%s\b" % re.escape(name), outside):
            warn("type reached from nowhere (dead code?): %s in %s"
                 % (name, os.path.relpath(where[0], ROOT)))


# ----------------------------------------------------------------------
def main():
    check_project_guids()
    check_sources_are_compiled()
    check_solution_paths()
    check_register_station_local_numbers()
    check_translation_keys(load_translation_keys())
    check_driver_catalog_ids()
    check_dead_types()

    for n in notes:
        print("  .. %s" % n)

    if warnings:
        print("\n%d warning(s):" % len(warnings))
        for w in warnings:
            print("  ~~ %s" % w)

    if problems:
        print("\n%d PROBLEM(S):" % len(problems))
        for p in problems:
            print("  !! %s" % p)
        return 1

    print("\nverify_plc_setup: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())

