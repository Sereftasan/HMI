#!/bin/bash
# Makes docs/AI_Page_Design_Guide.md from the program itself (Mono on Linux; on Windows build
# MakeAiPageGuide.cs as a console program referencing the machine's output folder and run it there).
# usage: tools/ai_guide/make_guide.sh <the built machine folder, e.g. Kaban.Machines.FA2010/bin/Debug> [output]
set -e
# the controls are made to read their defaults: Mono needs an X display (a virtual one will do)
if [ -z "$DISPLAY" ]; then export DISPLAY=:99; pgrep Xvfb >/dev/null || (Xvfb :99 -screen 0 1280x1024x24 >/dev/null 2>&1 &); sleep 1; fi
REPO=$(cd "$(dirname "$0")/../.." && pwd)
BIN=$(cd "$1" && pwd)
OUT=${2:-$REPO/docs/AI_Page_Design_Guide.md}
RUN=$(mktemp -d); cp -r "$BIN"/. "$RUN"/
REFS=""; for f in "$BIN"/*.dll "$BIN"/*.exe; do REFS="$REFS -r:$f"; done
mcs -nowarn:CS0168,CS0219,CS0414,CS0162 -out:"$RUN/MakeAiPageGuide.exe" $REFS -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Data.dll "$REPO/tools/ai_guide/MakeAiPageGuide.cs"
(cd "$RUN" && mono MakeAiPageGuide.exe "$REPO" "$OUT")
rm -rf "$RUN"
