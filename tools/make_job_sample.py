#!/usr/bin/env python3
"""Writes docs/fa2010/JobSample_Library.kbn - a job file (.kbn, the window software's) of the profile library's own profiles
and accessories (6 November: "they are the recipe IDs but they don't match to my library, it is as sample job file. Can you
create a file which match to ours?"). Every bar's profile code is a profile of the library, every "Rec" an accessory of its
group (v_group_accessory) - checked against the library given:

    python tools/make_job_sample.py [C:\\ProgramData\\KabanHMI\\Kaban.db]

Three windows (ML8 Functional 1200 x 1400 with a mullion, Elegant 900 x 1200, SUPREME S67 sash), lengths and angles in
tenths as the window software writes them, CRLF.
"""
import os
import sqlite3
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, 'docs', 'fa2010', 'JobSample_Library.kbn')


def t(mm):
    return str(int(round(mm * 10)))


def a(deg):
    return '%04d' % int(round(deg * 10))


class Job:
    def __init__(self):
        self.lines = []
        self.bars = 0
        self.seq = 0

    def bar(self, code, name, length, colour='White', quantity=1):
        if self.lines:
            self.lines.append(' ')
        self.bars += 1
        self.lines.append(';'.join(['Bar', str(self.bars), code, name, str(quantity), colour, t(length), '0', '']))

    def piece(self, length, left, right, position, order, line, customer, frame, note='', recs=(), other=None):
        self.seq += 1
        barcode = '%s-%s-%02d' % (order, position, self.seq)
        self.lines.append(';'.join(['Profile', t(length), t(other if other is not None else length), a(left), a(right), '1', str(self.seq), order, str(line), customer,
                                    position, note, str(frame), '', '0', barcode, str(self.seq), '', '', '', 'KABAN', '']))
        for code, pos in recs:
            self.lines.append('Rec;%s;%s;' % (code, t(pos)))


def build():
    j = Job()
    cust = 'KABAN TEST'
    # window 1: ML8 Functional 1200 x 1400, one opening sash, a mullion
    j.bar('ML8-F-4501', 'Functional outer frame 77', 6500)
    j.piece(1200, 45, 45, 'UST', 'S2026-001', 1, cust, 1, other=1400)
    j.piece(1200, 45, 45, 'ALT', 'S2026-001', 1, cust, 1, 'Su Tahliye', [('GEN-DRN-5X30', 150), ('GEN-DRN-5X30', 600), ('GEN-DRN-5X30', 1050)], other=1400)
    j.piece(1400, 45, 45, 'SOL', 'S2026-001', 1, cust, 1, other=1200)
    j.piece(1400, 45, 45, 'SAG', 'S2026-001', 1, cust, 1, 'Karsilik', [('NX-STR-STD', 120), ('NX-STR-STD', 1280)], other=1200)
    j.bar('ML8-S-4510', 'Functional casement sash 87', 6500)
    j.piece(1150, 45, 45, 'UST', 'S2026-001', 1, cust, 1, '', [('NX-CD-STD', 80)], other=1350)
    j.piece(1150, 45, 45, 'ALT', 'S2026-001', 1, cust, 1, '', [('NX-CD-STD', 80)], other=1350)
    j.piece(1350, 45, 45, 'SOL', 'S2026-001', 1, cust, 1, 'Mentese', [('NX-HNG-STD', 120), ('NX-HNG-STD', 1230)], other=1150)
    j.piece(1350, 45, 45, 'SAG', 'S2026-001', 1, cust, 1, 'Kol', [('NX-ESP-STD', 675), ('NX-HDL-92', 675), ('NX-LCK-35', 675)], other=1150)
    j.bar('ML8-M-4520', 'Functional mullion', 6500, quantity=2)
    j.piece(1310, 90, 90, 'ORTA', 'S2026-001', 1, cust, 1, 'Orta kayit')
    j.piece(1310, 90, 90, 'ORTA', 'S2026-001', 2, cust, 2, 'Orta kayit')
    # window 2: Elegant 900 x 1200
    j.bar('0001000007', 'Elegant frame (KASA)', 6000)
    j.piece(900, 45, 45, 'UST', 'S2026-002', 1, cust, 3, other=1200)
    j.piece(900, 45, 45, 'ALT', 'S2026-002', 1, cust, 3, 'Su Tahliye', [('GEN-DRN-6X25', 150), ('GEN-DRN-6X25', 750)], other=1200)
    j.piece(1200, 45, 45, 'SOL', 'S2026-002', 1, cust, 3, other=900)
    j.piece(1200, 45, 45, 'SAG', 'S2026-002', 1, cust, 3, other=900)
    j.bar('0001000104', 'Elegant sash (KANAT)', 6000)
    j.piece(850, 45, 45, 'UST', 'S2026-002', 1, cust, 3, other=1150)
    j.piece(850, 45, 45, 'ALT', 'S2026-002', 1, cust, 3, other=1150)
    j.piece(1150, 45, 45, 'SOL', 'S2026-002', 1, cust, 3, other=850)
    j.piece(1150, 45, 45, 'SAG', 'S2026-002', 1, cust, 3, 'Kol', [('GEN-HDL-SQ', 575)], other=850)
    # window 3: SUPREME S67 sash 700 x 1000
    j.bar('S67-S-310', 'SUPREME S67 sash', 6000)
    j.piece(700, 45, 45, 'UST', 'S2026-003', 1, cust, 4, other=1000)
    j.piece(700, 45, 45, 'ALT', 'S2026-003', 1, cust, 4, other=1000)
    j.piece(1000, 45, 45, 'SOL', 'S2026-003', 1, cust, 4, 'Mentese', [('TIT-HNG-STD', 125), ('TIT-HNG-STD', 875)], other=700)
    j.piece(1000, 45, 45, 'SAG', 'S2026-003', 1, cust, 4, 'Kol', [('NX-HDL-92', 500)], other=700)
    return j


def check(j, db):
    c = sqlite3.connect(db)
    problems = []
    group = None
    for line in j.lines:
        f = line.split(';')
        if f[0] == 'Bar':
            row = c.execute('SELECT group_id FROM profile WHERE code=? AND is_active=1', (f[2],)).fetchone()
            if row is None:
                problems.append('no profile ' + f[2])
                group = None
            else:
                group = row[0]
        elif f[0] == 'Rec' and group is not None:
            ok = c.execute('SELECT 1 FROM v_group_accessory ga JOIN accessory a ON a.id = ga.accessory_id WHERE ga.profile_group_id=? AND a.code=?',
                           (group, f[1])).fetchone()
            if ok is None:
                problems.append('%s is not an accessory of the group of the bar above it' % f[1])
    return problems


def main():
    j = build()
    if len(sys.argv) > 1:
        p = check(j, sys.argv[1])
        if p:
            print('\n'.join(p))
            sys.exit(1)
        print('every profile and accessory is in ' + sys.argv[1])
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, 'w', newline='\r\n', encoding='utf-8') as fh:
        fh.write('\n'.join(j.lines) + '\n')
    print(OUT + ': %d bars, %d lines' % (j.bars, len(j.lines)))


if __name__ == '__main__':
    main()
