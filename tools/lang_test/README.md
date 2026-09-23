# Display-name translation chain test

Reproduces the exact sequence a compound machine (FA2010 + H500 + H800) goes
through at startup - MountMachines(), MainForm's own translation load,
BuildNavigation()'s first tab build, then a simulated login - and prints
DisplayNameOf(H500)/H800 at each step.

Found and confirmed a real bug: a plain `<ProjectReference>` copies a referenced
project's compiled DLL only, never its content items. FA2010's .csproj had no
`<None Include>` entries for H500's/H800's own `Translations.csv` or
`Alarms.json`, so `ctx.ResolveFile`'s per-module scoped lookup
(`<output>/<ModuleId>/<file>`) always missed and silently fell back to FA2010's
OWN file - meaning a mounted sub-machine's translations, and by the same
mechanism its ALARM DEFINITIONS, never actually loaded. `LanguageManager.Localize`
returning the raw key on a failed lookup turned that into "Machine_H500" on
screen instead of "Screwing_Holing".

Run against a directory laid out the way MSBuild actually produces it (see the
script's own `Environment.CurrentDirectory` and file copies) - a directory with
ONLY FA2010's own files reproduces the bug; adding `H500/Translations.csv` and
`H800/Translations.csv` (what the fixed .csproj now copies) proves the fix.

