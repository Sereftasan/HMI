# Kaban HMI - how we work (for Claude and for you)

## Where the code is
- **C:\Kaban Files\_Projects\_HMI\HMI** is the project (it moved there on 14 October). Solution: `KabanHMI\KabanHMI.sln`.
- Claude works on a copy in its cloud workspace (it builds and tests there on Mono/Linux), then writes the changed
  files straight into this folder - they are the newest code at once. No zip any more.
- **The history** (every commit since the first upload, branch `master`) is in `_history\HMI.bundle`, renewed with
  each batch. Claude may not write into a `.git` folder itself, so **double-click `Update_Git_History.cmd`** when you
  want the history in Visual Studio's *Git Changes* / *Git Repository* windows: the first time it makes this folder
  a git repository, later it brings it up to Claude's newest commit. It overwrites no file.
- **Your own changes**: edit freely. Before each batch Claude looks for files changed in this folder since its last
  write and takes them in first - nothing of yours is overwritten. Committing them yourself is fine too (on `master`
  or a branch of your own); say so in your request.

## Requests
- A list of everything missing, in one message, is fine: Claude plans it as numbered items, makes one commit per
  item and reports per item, with the questions it has at the end.
- Something new while Claude is working: write it in **REQUESTS.md** (in this folder) instead of sending it then -
  Claude reads it at the start of the next batch. A message sent mid-batch gets merged into the running batch.
- Screenshots and files for a request (PNG, PDF, XLS, CSV ...): into the **`_requests`** folder, their names in the
  item in REQUESTS.md. In a chat message a picture can also be pasted directly.
- A bug: the page, the user level, the screen scale, and a screenshot from the real panel when you can - Claude's
  tests run on Linux, so Windows' look is the one thing it cannot see itself.
- A new screen or a UI change you are not sure of: ask for a mock-up first ("show me before you build").

## What each batch does (12 October)
- Done every time: the code, the tests of what changed, a commit per item, the files into this folder, a short report.
- **Only when you ask**: REVIEW_2026-09-24.md / CHANGES.md, the translations (new texts are English until then),
  the manuals (`KabanHMI\Help`, the machines' `Help`), the AI page design guide, the full test suite.

## For Claude, at the start of a session or a batch
1. Stage `_history\HMI.bundle` and clone it: that is the repository (`git clone HMI.bundle work`).
2. List this folder: a file newer than `_history\HMI.bundle` (written last in every batch) is the user's own change -
   stage it and take it in (bin, obj, .vs and packages are Visual Studio's).
3. Read `REQUESTS.md` and stage the `_requests` files it names (Read shows pictures and PDFs). Work; commit per item (the trailer lines as the session says); re-add the BOMs.
4. Write the changed files here (`device_commit_files`, up to 50 a call), move the done requests to "Done", and
   last a new `_history\HMI.bundle` (`git bundle create HMI.bundle master`).

## Conventions
- Files: UTF-8 with a byte order mark for `.cs`, `.csproj`, `.csv`; Windows line endings in this folder.
- Translations: `Key;EN;TR;RU;DE;IT;ES`, no `;` inside a text.
- The general manual names no machine; a machine's own chapter is in its project (`Kaban.Machines.FA2010\Help`).
- A device of a machine's other PLC in a property: `X12@2` (X12 of PLC 2) - never a dot (`D100.5` is bit 5 of D100).
- FA2010's pages are generated from the old program's: `tools\legacy_fa2010_pages.py` (it keeps what was set by hand).
  Its wiring list: `tools\build_wiring.py` -> `Kaban.Machines.FA2010\Wiring.csv`.

## Open questions (answer here or in a message)
1. The back horizontal moulds' reference sensors (X12@2, X13@2, X14@2) are on the rows' **back** side - is any of
   them at the front?
2. Cutting > Right Front Vert. Clamping and Entry Conveyor > Profile Clamping 7 show a position the drawings measure
   nothing for: change their feedback type, or are the drawings missing something?
