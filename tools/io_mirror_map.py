#!/usr/bin/env python3
"""
Derives the PHYSICAL-I/O -> INTERNAL-RELAY mirror table from the machine's ST
interface block, e.g.

    K2M200:=K2X20;   K2M208:=K2X30;   ...   K2M510:=K2Y200;

K2 means TWO digits = 8 bits, so each statement mirrors 8 consecutive points.
X and Y addresses are OCTAL on Mitsubishi (X27 is followed by X30); M is decimal.

Why this matters to the HMI: the screens must bind to the M mirrors, never to X or
Y directly. That is the whole point of the interface block - the electrician can
re-wire an input and only the ST line changes, while every HMI page keeps working.
"""
import re, sys

# the FA2010 interface block, exactly as written in the PLC
FA2010_ST = """
K2M200:=K2X20;K2M208:=K2X30;
K2M216:=K2X40;K2M224:=K2X50;
K2M232:=K2X60;K2M240:=K2X70;
K2M248:=K2X100;K2M252:=K2X110;
K2M260:=K2X120;K2M268:=K2X130;
K2M350:=K2Y0;K2M360:=K2Y10;
K2M370:=K2Y20;K2M380:=K2Y30;
K2M390:=K2Y40;K2M400:=K2Y50;
K2M410:=K2Y60;K2M420:=K2Y70;
K2M430:=K2Y100;K2M440:=K2Y110;
K2M450:=K2Y120;K2M460:=K2Y130;
K2M470:=K2Y140;K2M480:=K2Y150;
K2M490:=K2Y160;K2M500:=K2Y170;
K2M510:=K2Y200;
"""

STMT = re.compile(r'K(\d)M(\d+)\s*:=\s*K(\d)([XY])([0-7]+)', re.I)


def parse(st_text):
    """-> list of (m_base, io_letter, io_base_octal, bit_count)"""
    out = []
    for m in STMT.finditer(st_text):
        digits = int(m.group(1))
        out.append((int(m.group(2)), m.group(4).upper(), m.group(5), digits * 4))
    return out


def octal_add(octal_str, offset):
    """X addresses are octal: X27 + 1 = X30."""
    return format(int(octal_str, 8) + offset, 'o')


def expand(statements):
    """-> {('X'|'Y', octal address): mirror M address}, plus any overlap warnings."""
    mapping = {}
    owner = {}
    warnings = []
    for m_base, letter, io_base, count in statements:
        for i in range(count):
            io = letter + octal_add(io_base, i)
            m = 'M' + str(m_base + i)
            if m in owner and owner[m] != io:
                warnings.append("%s is written by BOTH %s and %s" % (m, owner[m], io))
            owner[m] = io
            if io in mapping and mapping[io] != m:
                warnings.append("%s is mirrored to BOTH %s and %s" % (io, mapping[io], m))
            mapping[io] = m
    return mapping, warnings


def build(st_text=FA2010_ST):
    mapping, warnings = expand(parse(st_text))
    return mapping, warnings


if __name__ == "__main__":
    mapping, warnings = build()
    print("%d physical points mirrored" % len(mapping))
    for w in warnings:
        print("  WARNING:", w)
    for io in sorted(mapping, key=lambda a: (a[0], int(a[1:], 8)))[:6]:
        print("   ", io, "->", mapping[io])

