#!/usr/bin/env python3
"""
The cylinder step-by-step helpers of every cylinder alarm, for people to review:
one page per alarm, as the HMI opens it, in Turkish and in English, with an
index per language.

    python3 tools/review/build_review.py <program folder> <out folder>

<program folder> is a built Kaban.Machines.FA2010\\bin\\Debug (it carries H500
and H800 in its subfolders). Needs mono (mcs) and an X display for the forms -
it is the build machine's tool, not the panel's.
"""
import csv
import html
import io
import os
import re
import shutil
import subprocess
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
MACHINES = ['FA2010', 'H500', 'H800']
LANGS = ['TR', 'EN']

TEXT = {
    'TR': dict(
        title='Silindir alarmları - adım adım yardım sayfaları',
        intro='Her silindir alarmı için HMI\'ın açtığı adım adım yardım sayfası, alarm listesinden açıldığı gibi '
              '(alarm anındaki sinyal durumları olmadan). Sayfalar, ünitenin valf ve geri bildirim tipine ait şablondan '
              '(Help/Templates/TR) ve makinenin çizim numaralarından (Wiring.csv) doldurulur. Solda sinyalin yolu '
              '(PLC çıkışı, röle, bobin, valf, hava, silindir, sensör) yukarıdan aşağı; her kontrolün yanında nereye bakılacağını '
              've nasıl kontrol edileceğini gösteren bir çizim var. Sayfalar operatörün göreceği gibidir: PLC\'nin iç cihazları '
              '(M, D) yalnızca Admin girişinde görünür.',
        ask='İncelerken lütfen şunlara bakın: adımlar doğru sırada mı, her adımda ne yapılacağı açık mı, '
            'numaralar (çıkış, röle, bobin, sensör, kablo, sayfa) çizimlerle uyuşuyor mu, eksik ya da gereksiz bir adım var mı, '
            'Türkçe anlaşılır mı, çizimler doğru yeri gösteriyor ve işe yarıyor mu. Notlarınızı alarm numarasıyla birlikte yazın.',
        machine='Makine', alarm='Alarm', text='Alarm metni', unit='Ünite', move='Hareket', template='Şablon (valf / geri bildirim)',
        page='Sayfa', open='aç', by_template='Şablonlara göre', count='alarm',
        notes='Notlar',
        note_sub='H500 ve H800 ünitelerinde valf ve geri bildirim tipi girilmemiş ve bu makinelerin çizim numaraları '
                 '(Wiring.csv) yok: sayfaları genel şablondan gelir, kablo / röle / bobin numaraları yoktur; hareket adları '
                 'butonların oklarından gelir (Sağ / Sol). H500 ünitelerinin lambaları komut bitlerini gösteriyor (ör. M151), '
                 'bu yüzden sayfalar o bitleri sensör olarak anar.',
        check='Kontrol edilecekler',
        check_intro='Bu alarmların metni, sayfanın hareketinin (butonun çizimlerde sürdüğü valf çıkışından) tersini söylüyor: '
                    'ünitedeki hata bitleri ya da alarm metinleri yer değiştirmiş olabilir.',
        says='alarm metni', page_move='sayfanın hareketi',
        note_md='MD3020\'nin silindir alarmı yok.',
        other='English', other_link='../EN/index.html'),
    'EN': dict(
        title='Cylinder alarms - step-by-step help pages',
        intro='For every cylinder alarm, the step-by-step help page the HMI opens, as it opens from the alarm list '
              '(without the signal states at the alarm). Each page is filled from the template of the unit\'s valve and '
              'feedback type (Help/Templates/EN) and the machine\'s drawing numbers (Wiring.csv). On the left, the signal path '
              '(PLC output, relay, coil, valve, air, cylinder, sensor) from top to bottom; beside each check a drawing of where to '
              'look and how to check it. The pages are as an operator sees them: the PLC\'s internal devices (M, D) show only '
              'when an Admin is signed in.',
        ask='When reviewing, please look at: are the steps in the right order, is it clear what to do in each, do the '
            'numbers (output, relay, coil, sensor, wire, sheet) match the drawings, is a step missing or not needed, do the '
            'pictures show the right place and help. Write your notes with the alarm number.',
        machine='Machine', alarm='Alarm', text='Alarm text', unit='Unit', move='Movement', template='Template (valve / feedback)',
        page='Page', open='open', by_template='By template', count='alarms',
        notes='Notes',
        note_sub='The H500 and H800 units have no valve and feedback type set, and these machines have no drawing numbers '
                 '(Wiring.csv): their pages come from the general template, without wire / relay / coil numbers; the movement '
                 'names come from the buttons\' arrows (Right / Left). The H500 units\' lamps show their command bits (M151...), '
                 'so the pages name those bits as the sensors.',
        check='To check',
        check_intro='These alarms\' text says the opposite of the page\'s movement (taken from the valve output the button drives '
                    'on the drawings): the unit\'s error bits or the alarm texts may be swapped.',
        says='alarm text', page_move='page\'s movement',
        note_md='MD3020 has no cylinder alarms.',
        other='Türkçe', other_link='../TR/index.html'),
}

STYLE = ("body{font-family:'Segoe UI',Arial,sans-serif;font-size:15px;color:#1e2329;margin:18px 22px;max-width:1100px;line-height:1.4}"
         "h1{font-size:1.4em}h2{font-size:1.15em;margin:1.2em 0 .4em;padding:.25em .5em;border-radius:4px;background:#eef1f5}"
         "table{border-collapse:collapse;width:100%}td,th{border-bottom:1px solid #e3e7ee;padding:.3em .5em;text-align:left;vertical-align:top}"
         "th{background:#f5f6f8}code{font-family:Consolas,monospace;font-weight:bold;color:#1f4e79}.ref{color:#5a6270}"
         ".ask{background:#fff7d6;border:1px solid #e0c060;border-radius:6px;padding:.5em .8em}a{color:#1f5fa8}")


def run(bin_dir, out_dir):
    work = os.path.join(out_dir, '_run')
    shutil.rmtree(work, ignore_errors=True)
    shutil.copytree(bin_dir, work)
    refs = ['-r:' + os.path.join(bin_dir, f) for f in sorted(os.listdir(bin_dir)) if f.endswith(('.dll', '.exe'))]
    subprocess.check_call(['mcs', '-nowarn:CS0168,CS0219,CS0414,CS0162', '-out:' + os.path.join(work, 'Review.exe')] + refs +
                          ['-r:System.Windows.Forms.dll', '-r:System.Drawing.dll', '-r:System.Data.dll',
                           os.path.join(HERE, 'CylinderHelpReview.cs')])
    for lang in LANGS:
        for m in MACHINES:
            subprocess.check_call(['mono', 'Review.exe', out_dir, m, lang], cwd=work)
    shutil.rmtree(work, ignore_errors=True)


OPPOSITE = [(r'\bup\b', r'\bdown\b'), (r'\b(fw|forward)\b', r'\b(bw|back|backward)\b'), (r'\bclamp(ing)?\b', r'\breleas')]


def swapped(alarm, move):
    """The alarm's text names the opposite of the movement (English texts)."""
    a, m = alarm.lower(), move.lower()
    for x, y in OPPOSITE:
        for mine, other in ((x, y), (y, x)):
            if re.search(mine, m) and re.search(other, a) and not re.search(mine, a):
                return True
    return False


def index(out_dir):
    manifests = {}
    for lang in LANGS:
        rows = []
        for m in MACHINES:
            path = os.path.join(out_dir, lang, m, 'manifest.tsv')
            if os.path.exists(path):
                rows += [r for r in csv.reader(io.open(path, encoding='utf-8'), delimiter='\t') if r]
                os.remove(path)
        manifests[lang] = rows
    flagged = [(r[0], r[1], r[2], r[4]) for r in manifests.get('EN', []) if swapped(r[2], r[4])]
    for lang in LANGS:
        t = TEXT[lang]
        rows = manifests[lang]
        e = html.escape
        out = ['<!DOCTYPE html><html lang="%s"><head><meta charset="utf-8"><title>%s</title><style>%s</style></head><body>'
               % (lang.lower(), e(t['title']), STYLE),
               '<p style="float:right"><a href="%s">%s</a></p><h1>%s</h1><p>%s</p><p class="ask">%s</p>'
               % (t['other_link'], t['other'], e(t['title']), e(t['intro']), e(t['ask']))]
        for m in MACHINES:
            mine = [r for r in rows if r[0] == m]
            if not mine:
                continue
            out.append('<h2>%s - %d %s</h2><table><tr><th>%s</th><th>%s</th><th>%s</th><th>%s</th><th>%s</th></tr>'
                       % (e(m), len(mine), e(t['count']), e(t['alarm']), e(t['text']), e(t['unit']), e(t['move']), e(t['template'])))
            for r in sorted(mine, key=lambda r: (len(r[1]), r[1])):
                out.append('<tr><td><a href="%s/%s"><code>%s</code></a></td><td>%s</td><td>%s</td><td>%s</td><td>%s</td></tr>'
                           % (e(m), e(r[8]), e(r[1]), e(r[2]), e(r[3]), e(r[4]), e(r[5].replace('Cylinder_', '').replace('.html', ''))))
            out.append('</table>')
        out.append('<h2>%s</h2><table><tr><th>%s</th><th>%s</th></tr>' % (e(t['by_template']), e(t['template']), e(t['page'])))
        by = {}
        for r in rows:
            by.setdefault(r[5], []).append(r)
        for name in sorted(by):
            links = ', '.join('<a href="%s/%s">%s %s</a>' % (e(r[0]), e(r[8]), e(r[0]), e(r[1])) for r in by[name])
            out.append('<tr><td>%s</td><td>%s</td></tr>' % (e(name.replace('Cylinder_', '').replace('.html', '')), links))
        out.append('</table>')
        if flagged:
            out.append('<h2>%s</h2><p>%s</p><table><tr><th>%s</th><th>%s</th><th>%s (EN)</th><th>%s (EN)</th></tr>'
                       % (e(t['check']), e(t['check_intro']), e(t['machine']), e(t['alarm']), e(t['says']), e(t['page_move'])))
            for m, a, text, move in flagged:
                f = next((r[8] for r in rows if r[0] == m and r[1] == a), '')
                out.append('<tr><td>%s</td><td><a href="%s/%s"><code>%s</code></a></td><td>%s</td><td>%s</td></tr>' % (e(m), e(m), e(f), e(a), e(text), e(move)))
            out.append('</table>')
        out.append('<h2>%s</h2><p class="ref">%s</p><p class="ref">%s</p></body></html>' % (e(t['notes']), e(t['note_sub']), e(t['note_md'])))
        with io.open(os.path.join(out_dir, lang, 'index.html'), 'w', encoding='utf-8-sig') as f:
            f.write('\n'.join(out))
        print('%s: %d pages' % (lang, len(rows)))


if __name__ == '__main__':
    bin_dir, out_dir = sys.argv[1], sys.argv[2]
    os.makedirs(out_dir, exist_ok=True)
    run(bin_dir, out_dir)
    index(out_dir)
