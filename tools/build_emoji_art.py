#!/usr/bin/env python3
"""
The colour pictures of the emoji buttons (KabanHMI/Resources/Emoji).

Windows Forms draws an emoji with GDI, which knows only one colour per letter:
the toolbar emojis came out grey and soft. The program draws these pictures
instead - Microsoft's Fluent Emoji (flat), the emojis Windows 11 shows, MIT
licence (tools/emoji_art/LICENSE-fluentui-emoji.txt) - made a little paler, as
asked: "the emojis should have their best colours (the pale ones, though, not
too sparkling)".

    python3 tools/build_emoji_art.py                      # the PNGs from tools/emoji_art/svg
    python3 tools/build_emoji_art.py --from icons.json    # first take the SVGs from the
                                                          # @iconify-json/fluent-emoji-flat package

Writes, for each size in SIZES, one strip KabanHMI/Resources/Emoji/emoji_<px>.png
with the pictures side by side in the order of EMOJI, and emoji.txt (that order:
the picture's code points, then the other characters that use it), and lists
them in Kaban.UI.csproj. Needs cairosvg and Pillow.

To give a new emoji button its colours: add a line to EMOJI, run with --from.
"""
import io, json, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SVG = os.path.join(ROOT, 'tools', 'emoji_art', 'svg')
OUT = os.path.join(ROOT, 'KabanHMI', 'Resources', 'Emoji')
CSPROJ = os.path.join(ROOT, 'KabanHMI', 'Kaban.UI.csproj')

# (code points of the picture, its Fluent name, other characters drawn with it)
EMOJI = [
    ('1f4c2', 'open-file-folder', []),
    ('1f4be', 'floppy-disk', []),
    ('1f4e4', 'outbox-tray', []),
    ('270f', 'pencil', []),
    ('2699', 'gear', []),
    ('1f4cb', 'clipboard', []),
    ('25b6', 'play-button', []),
    ('23f9', 'stop-button', ['25a0']),            # the black square of "Stop"
    ('1f50d', 'magnifying-glass-tilted-left', []),
    ('1f50e', 'magnifying-glass-tilted-right', []),
    ('1f504', 'counterclockwise-arrows-button', []),
    ('2716', 'multiply', []),
    ('2714', 'check-mark', []),
    ('2705', 'check-mark-button', []),
    ('2795', 'plus', []),
    ('1f5d1', 'wastebasket', []),
    ('1f4da', 'books', []),
    ('1f4c4', 'page-facing-up', []),
    ('1f4d1', 'bookmark-tabs', []),
    ('1f441', 'eye', []),
    ('1f9ee', 'abacus', []),
    ('1f558', 'nine-oclock', []),
    ('1f3af', 'bullseye', []),
    ('1f4cd', 'round-pushpin', []),
    ('1f529', 'nut-and-bolt', []),
    ('1f4cf', 'straight-ruler', []),
    ('27b0', 'curly-loop', []),
    ('1f7e2', 'green-circle', []),
    ('26d3', 'chains', []),
    ('26d4', 'no-entry', []),
    ('2702', 'scissors', []),
]
SIZES = [16, 20, 24, 28, 32, 40, 48]
PALE = 0.15        # towards grey, then towards white: the colours stay, the glare goes


def take_svgs(icons_json):
    d = json.load(open(icons_json, encoding='utf-8'))
    w0, h0 = d.get('width', 32), d.get('height', 32)
    os.makedirs(SVG, exist_ok=True)
    for key, name, _ in EMOJI:
        ic = d['icons'][name]
        svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 %d %d">%s</svg>\n' % (
            ic.get('width', w0), ic.get('height', h0), ic['body'])
        open(os.path.join(SVG, key + '.svg'), 'w', encoding='utf-8').write(svg)


def pale(im):
    px = im.load()
    for y in range(im.height):
        for x in range(im.width):
            r, g, b, a = px[x, y]
            if a == 0: continue
            l = 0.3 * r + 0.59 * g + 0.11 * b
            r, g, b = (c + (l - c) * PALE for c in (r, g, b))
            r, g, b = (c + (255 - c) * PALE for c in (r, g, b))
            px[x, y] = (int(round(r)), int(round(g)), int(round(b)), a)
    return im


def main():
    import cairosvg
    from PIL import Image
    if '--from' in sys.argv:
        take_svgs(sys.argv[sys.argv.index('--from') + 1])
    os.makedirs(OUT, exist_ok=True)
    for px in SIZES:
        strip = Image.new('RGBA', (px * len(EMOJI), px), (0, 0, 0, 0))
        for i, (key, _, _) in enumerate(EMOJI):
            data = open(os.path.join(SVG, key + '.svg'), 'rb').read()
            im = Image.open(io.BytesIO(cairosvg.svg2png(bytestring=data, output_width=px, output_height=px))).convert('RGBA')
            strip.paste(pale(im), (i * px, 0))
        strip.save(os.path.join(OUT, 'emoji_%d.png' % px), optimize=True)
    with open(os.path.join(OUT, 'emoji.txt'), 'w', encoding='ascii', newline='\n') as f:
        f.write('# The pictures of emoji_<px>.png, left to right: code points, then other characters drawn with it.\n')
        f.write('# sizes ' + ' '.join(str(s) for s in SIZES) + '\n')
        for key, _, more in EMOJI:
            f.write(' '.join([key] + more) + '\n')
    # The project: one EmbeddedResource per file, between the markers.
    files = ['emoji_%d.png' % s for s in SIZES] + ['emoji.txt']
    raw = open(CSPROJ, 'rb').read()
    bom = raw.startswith(b'\xef\xbb\xbf')
    text = raw.decode('utf-8-sig')
    nl = '\r\n' if '\r\n' in text else '\n'
    items = ''.join('    <EmbeddedResource Include="Resources\\Emoji\\%s">%s      <LogicalName>Kaban.UI.Emoji.%s</LogicalName>%s    </EmbeddedResource>%s'
                    % (f, nl, f, nl, nl) for f in files)
    # the pictures' licence goes with the program (beside it in the output folder)
    import shutil
    shutil.copyfile(os.path.join(ROOT, 'tools', 'emoji_art', 'LICENSE-fluentui-emoji.txt'), os.path.join(OUT, 'LICENSE-fluentui-emoji.txt'))
    items += ('    <None Include="Resources\\Emoji\\LICENSE-fluentui-emoji.txt">%s      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>%s    </None>%s'
              % (nl, nl, nl))
    block = '    <!-- emoji art: tools/build_emoji_art.py -->' + nl + items + '    <!-- /emoji art -->' + nl
    if '<!-- emoji art:' in text:
        text = re.sub(r'    <!-- emoji art:.*?<!-- /emoji art -->\r?\n', lambda m: block, text, flags=re.S)
    else:
        anchor = '    <EmbeddedResource Include="Resources\\Flags\\EN.png">'
        assert text.count(anchor) == 1
        text = text.replace(anchor, block + anchor)
    open(CSPROJ, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + text.encode('utf-8'))
    print(len(EMOJI), 'pictures,', len(SIZES), 'sizes')


if __name__ == '__main__':
    main()
