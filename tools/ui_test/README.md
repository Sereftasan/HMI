# Interaction cue test

Renders controls off-screen and inspects the PIXELS, because these two rules are
about appearance and cannot be checked by reading the code:

- **Cursor follows Enabled** — hand while usable, plain arrow once disabled, so an
  interlocked button is obvious *before* it is clicked rather than after nothing
  happens. A text box keeps its I-beam; a hand there would be wrong.
- **Blue means one thing** — the border of the control the pointer is over.
  Checks that a `GroupBoxHMI` never lights up (a frame is not clickable and would
  compete with the control inside it), that plain WinForms controls DO get an
  outline drawn by their parent, that a control which paints its own border is
  not outlined twice, and that re-showing a page does not stack handlers.
- **Lamp clearance** — measures the background between `ButtonHMI`'s frame and its
  sensor lamp. Scanned at one QUARTER height, not the midline: with two lamps the
  midline is the split between them, which is empty and reads as 0px.

## Running

Needs `mono` and a display. On a headless box:

```
apt-get install -y xvfb mono-mcs
Xvfb :99 -screen 0 1024x768x24 &
mcs -out:C.exe -r:Kaban.Core.dll -r:Kaban.UI.dll \
    -r:System.Windows.Forms.dll -r:System.Drawing.dll InteractionCues.cs
DISPLAY=:99 mono C.exe
```

Mono's WinForms is not pixel-identical to Microsoft's, so this proves the LOGIC
(who gets an outline, what the cursor is, how far apart two shapes are) rather
than the exact look. The look still wants a human eye on Windows.

