# Navigation structure test

Checks the shape of the left bar, which is easy to break silently: a role typo
or an extra `yield return` turns one button into two, or gives HMI Settings a tab
strip, and nothing fails to compile.

Asserts:

- **HMI Settings appears exactly once**, with NO tab strip, however many
  sub-machines are mounted. It is the HMI program's own settings - units, users,
  station connections - and the same values serve every machine.
- **Machine Parameters is tabbed per machine.**
- **Tabs read the translated machine names**, in English and Turkish. The main
  machine is named like any other; it used to read a literal "Main", which on a
  line with three machines is the one word that does not identify one.

Run with `mono` against the built `Kaban.Core.dll` and `Kaban.UI.dll`, with the
translation CSVs beside the exe (see tools/protocol_tests/README.md for the
build recipe).

## MachineStrip.cs

Covers the SECOND-level navigation menu (`MachineSelectorStrip`), which is easy
to break silently:

- It appears only when the open page actually spans several machines, and hides
  itself for HMI Settings or any page on a machine with no sub-machines - a
  one-entry menu asks a question with a single answer.
- Selecting raises the index the shell uses to switch the page.
- Every machine gets a DISTINCT icon, so the strip still distinguishes machines
  once the bar collapses to icons only. Asserted by comparing rendered pixels,
  because a shared generic glyph would compile perfectly and be useless.
- The page's own machine tab strip is hidden, while a page whose tabs are its own
  sub-pages (HMI Settings: Units, Users, Station Connection) keeps them. Offering
  the same choice in two places means two "you are here" marks to keep in step.

## RealMainForm.cs  -- the one that actually catches things

Builds the **real `MainForm`** via `BuildNavigation()`, shows it, and inspects the
live control tree. The earlier synthetic-panel tests passed while the app was
still visibly wrong, which is exactly the trap: a hand-built `Panel` does not
reproduce `MainForm`'s constructor order, its docked toggle/history strips, or
its resource loading.

It immediately found two real faults the synthetic test could not:

- **`_navHost` was null in `AddMenuButton`.** `MainForm`'s constructor does its
  setup inside a `try/catch`, so any unrelated startup failure (missing
  `PlcLabels.csv`, a PLC that will not open) skipped nav creation, and
  `BuildNavigation` - called afterwards from `Program.cs` - threw. Fixed with
  `EnsureNavHost()`, which both callers use so neither depends on the other
  having run.
- **Positions must be compared in SCREEN coordinates.** `Top` is
  parent-relative, so a strip in one container and buttons in another cannot be
  compared directly - that is how a "passing" test coexisted with a strip sitting
  above the buttons on screen.

Needs the embedded resources, which a plain `mcs` build does not produce:

```
resgen2 KabanHMI/Properties/Resources.resx /tmp/build/Kaban.UI.Properties.Resources.resources
mcs -target:library -out:Kaban.UI.dll ... \
    -resource:/tmp/build/Kaban.UI.Properties.Resources.resources,Kaban.UI.Properties.Resources.resources
```

The login section is driven by invoking `RefreshNavVisibility` directly rather
than through `Application.DoEvents()`: Mono's event loop does not return with a
shown form and running timers. The production path is
`SecurityManager.UserChanged -> OnUserChangedRefreshNav -> RefreshNavVisibility`.

