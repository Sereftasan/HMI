#!/usr/bin/env python3
"""Regenerates every machine's pages from its PLC label list."""
import os, sys, collections
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from machine_pages import *
from emit_pages import Emitter

ROOT = os.path.join(HERE, "..")

def build(machine, ns_suffix="Screens"):
    src = os.path.join(ROOT, "Kaban.Machines." + machine, "PlcLabels.csv")
    if not os.path.exists(src):
        print("  %s: no PlcLabels.csv, skipped" % machine); return
    rows = load_labels(src)
    ns = "Kaban.Machines.%s.%s" % (machine, ns_suffix)
    out = os.path.join(ROOT, "Kaban.Machines." + machine, "Screens")
    em = Emitter(ns, out, machine + "/PlcLabels.csv")

    made = []
    for page, fields in build_movement(rows).items():
        cls = "Manual" + ident(page)
        made.append((cls, em.emit(cls, fields)))

    options, params = build_parameters(rows)
    if options: made.append(("MachineOptions", em.emit("MachineOptions", options)))
    if params:
        # split a long parameter list so no page needs scrolling twice
        half = (len(params) + 1) // 2
        if len(params) > 40:
            made.append(("MachineParameters",  em.emit("MachineParameters",  params[:half])))
            made.append(("MachineParameters2", em.emit("MachineParameters2", params[half:])))
        else:
            made.append(("MachineParameters", em.emit("MachineParameters", params)))

    status = build_status(rows)
    if status: made.append(("MachineStatus", em.emit("MachineStatus", status)))

    print("  %s:" % machine)
    for cls, n in made: print("      %-22s %3d controls" % (cls, n))
    return [c for c, _ in made]

if __name__ == "__main__":
    for m in sys.argv[1:] or ["H800", "H500", "MD3020", "FA2010"]:
        build(m)

