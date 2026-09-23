#!/usr/bin/env python3
"""
Checks every .csproj the way Visual Studio will read it - run before shipping.

Two mistakes this catches, both of which have happened:
  1. A .cs file on disk but not in its project (or listed but deleted). A build
     that compiles every file on disk passes; Visual Studio compiles only the
     project's list and fails - "metadata file Kaban.UI.dll could not be found"
     in some OTHER project is the symptom.
  2. XML inserted in the wrong place. A block added before the first "</Project>"
     lands inside a <ProjectReference> (whose GUID element is also <Project>);
     MSBuild ignores it silently, so the build passes and the target never runs.
"""
import glob, os, re, sys
import xml.dom.minidom as md

root = sys.argv[1] if len(sys.argv) > 1 else "."
problems = 0
for proj in sorted(glob.glob(os.path.join(root, "**", "*.csproj"), recursive=True)):
    if "/packages/" in proj.replace("\\", "/"):
        continue
    d = os.path.dirname(proj)
    text = open(proj, encoding="utf-8-sig").read()
    try:
        doc = md.parseString(text.encode("utf-8"))
    except Exception as e:
        print("NOT VALID XML  %s: %s" % (proj, e)); problems += 1; continue

    listed = {x.replace("\\", "/") for x in re.findall(r'<Compile Include="([^"]+\.cs)"', text)}
    on_disk = {os.path.relpath(p, d).replace("\\", "/") for p in glob.glob(os.path.join(d, "**", "*.cs"), recursive=True)
               if not re.search(r"[\\/](obj|bin|packages)[\\/]", p)}
    for f in sorted(on_disk - listed): print("NOT IN PROJECT %s: %s" % (proj, f)); problems += 1
    for f in sorted(listed - on_disk): print("MISSING FILE   %s: %s" % (proj, f)); problems += 1

    for ref in doc.getElementsByTagName("ProjectReference"):
        for g in ref.getElementsByTagName("Project"):
            if any(c.nodeType == c.ELEMENT_NODE for c in g.childNodes):
                print("BROKEN REF     %s: %s" % (proj, ref.getAttribute("Include"))); problems += 1

print("check_projects: %s" % ("OK" if problems == 0 else "%d problem(s)" % problems))
sys.exit(1 if problems else 0)

