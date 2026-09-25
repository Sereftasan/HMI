# HMI — Fixes & Restored Functionality

All changes were made against the codebase from `HMI.rar`. Every project compiles,
and the core logic (auth, lockout, permissions, alarm engine, JSON loader, limit
calculation) passed a 26-test runtime smoke test.

> **BEFORE COMMISSIONING:** the three seeded accounts (`operator`, `maintenance`,
> `admin`) all ship with password **`1234`**. Change them via
> `UserStore.AddOrUpdateUser(...)` / `UserStore.ChangePassword(...)` before the
> machine goes into service. The in-memory `UserStore` should also be replaced
> with a persistent store (encrypted file / DB / Active Directory) — see the
> comment block at the top of `UserStore.cs`.

---

## 1. CRITICAL — Real authentication (was: everyone auto-logged-in as Operator)

**Was:** `MainForm_Load` ran `SecurityManager.Login("Dev", SecurityLevel.Operator)`
unconditionally. No login form existed; Maintenance/Admin levels were unreachable.

**Now:**
- `Kaban.Core/Helpers/UserStore.cs` *(new)* — credential store. Passwords are
  PBKDF2-hashed with per-user random salt (100k iterations, SHA-256), compared in
  constant time. No plaintext is ever stored.
- `Kaban.Core/Helpers/UserAccount.cs` *(new)* — account model.
- `Kaban.Core/Helpers/SecurityManager.cs` — added `TryLogin(user, pass, out error)`
  (the only UI entry point), brute-force lockout (5 failures → 60 s lock), audit
  logging of every login/logout/failed attempt. `Login(user, level)` is kept for
  trusted internal use only and is documented as such.
- `KabanHMI/Forms/LoginForm.cs` *(new)* — login dialog.
- `KabanHMI/Forms/MainForm.cs` — starts logged **out**; Login/Logout button and
  current-user label added to the top panel; **idle auto-logout after 10 minutes**
  of no mouse/keyboard activity (shared factory terminals shouldn't stay unlocked).

## 2. CRITICAL — PLC permission gatekeeper was decorative

**Was:** `PlcManager.GenelPermission` / `VidalamaPermission` were `= true` and never
changed anywhere, so `CheckPermission()` always passed.

**Now:** they derive from the live security session — General stations require a
logged-in Operator+, station 3 (Vidalama) requires Maintenance+. Manual override
setters kept for hardware key-switch integration (`ClearPermissionOverrides()`
returns to session-driven mode). Unknown station IDs in `Connect`/`Disconnect`
are no longer silently ignored — they're audit-logged.

## 3. CRITICAL — Audit logging existed but was never called

**Now called from:** every login/logout/failed login, every PLC write from
`ButtonHMI`/`NumericInputHMI` when `LogAction = true`, alarm acknowledgements,
comm-lost/restored transitions, unregistered-station attempts, unhandled exceptions.

`AuditLogger` itself fixed: absolute path
(`%ProgramData%\KabanHMI\AuditLogs\HmiAudit_YYYY-MM-DD.txt`) instead of a
working-directory-relative file, daily files, 90-day retention cleanup, lock for
thread safety.

## 4. CRITICAL — Alarm screens showed hardcoded fake data

**Was:** each machine's `AlarmScreen` generated three fake test alarms and never
looked at the PLC. `Alarms.json` existed but nothing read it. The grid's
Description column bound to a nonexistent property (`DescriptionKey`) so it
always rendered blank.

**Now:**
- `Kaban.Core/Helpers/AlarmManager.cs` *(new)* — central alarm engine: registered
  conditions are evaluated on every scan tick from live PLC data; alarms raise,
  don't duplicate, auto-clear, and keep a bounded 500-entry history.
  `AcknowledgeAll()` is security-gated and audit-logged.
- `Kaban.Core/Helpers/AlarmDefinitionLoader.cs` *(new)* — restores the missing
  JSON loader: reads `Alarms.json` (tolerating the `//` comments in the shipped
  file), registers alarms with `AlarmManager` and the EN/TR texts with
  `LanguageManager`. The `System.Text.Json` reference already in `Kaban.Core`
  finally has a consumer again.
- `Kaban.Machines.FA2010/Alarms.json` — **repaired** (the last entry was missing
  its closing brace; the file was not valid JSON and could never have parsed).
- All three `AlarmScreen.cs` (SB3012/SB3015/FA2010) — now bind the grid to
  `AlarmManager.ActiveAlarms`; the Description column binds to the real
  `Description` property; the Acknowledge button goes through the security-gated
  `AcknowledgeAll()`.
- `LanguageManager.AddTranslation(...)` *(new method)* — runtime registration for
  JSON-sourced texts; `MainForm` re-applies them after the CSV load (which clears
  the dictionary).

## 5. HIGH — PLC read failure was indistinguishable from a real 0

**Was:** `MitsubishiActUtl.Read()` returned `0` on any error — a cut cable looked
exactly like "pressure = 0".

**Now:**
- `IPlcDriver` gains `TryRead`, `TryReadMulti`, and `IsHealthy`. (Old `Read`/
  `ReadMulti` kept for compatibility.)
- `PlcScanner` uses `TryReadMulti`; on failure it keeps showing the **last good**
  values instead of zeros, exposes `CommunicationHealthy` + a state-change event,
  and audit-logs comm lost/restored.
- `MainForm` shows a red **"PLC COMMUNICATION LOST"** banner while unhealthy.
- Alarms are only evaluated on healthy scans (never raised/cleared from bad data).

## 6. HIGH — RESTORED: the deleted write path & limit-calculation engine

This is the functionality your colleague stripped out (your reference file), ported
onto this codebase's actual types (`DynamicValue`, `DataFormat`, `Kaban.UI.*`
namespaces — the reference file used older names like `DynamicLimit`/`Format`):

- `Kaban.Core/Helpers/InputLimitsCalculation.cs` *(new)* — resolves Min/Max limits
  from Static values, live PLC tags (re-scaled through `ReadFormula` so the
  comparison matches the displayed scaling), or HMI internal variables.
- `Kaban.Core/Models/MathVariable.cs` *(new)* — named formula variables (B, C, …)
  that are either constants or live PLC tags.
- `KabanHMI/Controls/NumericInputHMI.cs` — `OnClick` now: reads limits **live from
  the PLC at click time** (fails closed with a clear message if the read fails, so
  an unverifiable value can never be written), opens the real `InputForm` keypad
  (which enforces the limits and the negative-sign rule), applies `WriteFormula`
  via NCalc (with `val`/`A` + MathVariables), honors `ConfirmWrite`, converts via
  `PlcValueConverter` (16/32-bit), writes to the **effective** (offset-applied)
  address so the write target always matches what's displayed, verifies the PLC
  accepted the write, and audit-logs when `LogAction` is set. New designer
  properties: `ReadFormula`, `WriteFormula`, `MathVariables`.
- The NCalc 1.3.8 package was already vendored and referenced but unused — now
  it's consumed again.
- `PlcScanner` also polls Tag-based MathVariables so their values are in the scan
  cache.

## 7. HIGH — Buttons never actually wrote to the PLC

**Was:** `ButtonHMI.OnClick` had the write call commented out
(`// PlcService.WriteBit(...)`) and just did `Console.WriteLine`.

**Now:** `SET`/`RESET`/`Alternate` bit actions and word actions execute real
driver writes to the effective (offset-applied) address. `Alternate` reads the
current state with `TryRead` and refuses to toggle blind if the read fails.
`Momentary` writes 1 on MouseDown and 0 on MouseUp (and is excluded from Click so
it can't double-fire). Failed writes show a warning instead of silently vanishing.
`ConfirmationNeeded` and `LogAction` are honored.

## 8. HIGH — No global exception handler

`Kaban.UI/AppBootstrap.cs` *(new)* installs `Application.ThreadException` +
`AppDomain.UnhandledException` handlers (audit-log + friendly dialog instead of a
crash). Wired into **all four** entry points — `Kaban.UI`, FA2010, SB3012, SB3015
— since each machine EXE has its own `Main()`.

## 9. Machine startup bugs (found while wiring the above)

- **SB3012 / SB3015 never registered any PLC driver** — every `GetDriver()` call
  returned null; no screen could ever read a tag. They now register the
  `SimulationDriver` (the only driver those projects reference — swap in the real
  driver when those profiles go to hardware).
- **SB3012's `MachineConfig` static constructor never ran** (nothing touched the
  class), so none of its machine conditions were ever registered.
  `Program.cs` now calls `MachineConfig.Initialize()`.
- FA2010's `Program.cs` loads `Alarms.json` at startup.

## 10. Smaller fixes

- `SimulationDriver` updated to the new `IPlcDriver` (never fails once connected).
- `PlcScanner`'s deep-extraction local functions converted to equivalent lambdas
  (identical behavior; also lets older compilers parse the file).
- Empty `Kaban.Drivers.Siemens` / `Kaban.Drivers.Mitsubishi` stubs left as-is —
  flagged: neither implements `IPlcDriver`; don't wire a Siemens machine to them
  until implemented.

## Verification performed

- `Kaban.Core`, `Kaban.Drivers.Simulation`, `Kaban.Drivers.MitsubishiActUtl`
  (against an ActUtl interface stub — the real COM lib is Windows-only),
  `Kaban.UI`, and all three machine EXEs compile.
- 26 runtime tests passed: wrong/correct password, lockout after 5 failures,
  password change, level checks, session-driven Vidalama/General permissions,
  alarm raise/no-duplicate/auto-clear/history/gated-acknowledge, `Alarms.json`
  loading + EN translation resolution, static/tag/formula/unset limit resolution.
- Not covered by automated tests (please verify on a real bench): WinForms
  behavior (login dialog flow, comm banner, keypad UX) and real Mitsubishi
  hardware I/O — the ActUtl COM component only exists on Windows with
  MX Component installed.

## 11. NEW — FunctionUnitHMI composite control (dual-action)

`KabanHMI/Controls/FunctionUnitHMI.cs` — one drop-in control per machine function.
Layout: [Description][ACK] | [Btn A][OUT][POS] | [Btn B][OUT][POS] | [position].

- **Dual-action support:** Action A + Action B groups (e.g. SIK/AC clamp
  cylinders — two commands, two output lamps, two end-position lamps). Leave
  Action B empty for single-action functions. Any empty address hides its element.
- **BaseOffset = row index over column-organized symbol tables** (the way PLC
  programs actually come: all SIK bits together, all inputs together...).
  Configure unit #1's absolute addresses; each copy only changes BaseOffset.
  PlcAddressHelper steps each device in its own number system — X/Y octal
  (X27+1=X30, Y7+1=Y10), M/D decimal. Verified against the real GirisKonv
  symbol table: all 42 addresses of Sikma 1–7 reproduce exactly.
- Built from existing ButtonHMI/LampHMI/NumericDisplayHMI, so PlcScanner scans
  it automatically; security, confirmation, audit logging (default ON) and
  translations flow through. Every caption/tooltip accepts a translation key or
  literal text.
- `Examples/GirisKonvExample.cs` — reference snippet generating the user's 7
  clamp units in a loop (not compiled into any project; copy into a screen).

## 12. Kaban production-style restyle (matching the ZU2010 screens)

- **FunctionUnitHMI** is now a flat table ROW matching the production layout:
  `| İsim | Geri ▼ |GRD| İleri ▲ |İLR| Konum |` — white cells with 1px gray grid
  lines, bold name on the left, and a red-on-black seven-segment style position
  readout. Action B (Geri) sits left of Action A (İleri), per the production
  column order. The OUTPUT bit of each action now drives a green indicator bar
  INSIDE its button (like the production toggle buttons), so the separate OUT
  lamps were removed; end-position sensors remain as small square lamps.
- **FunctionTableHMI** (new): hosts rows under the production-style column
  header (İsim | Geri | İleri | Konumlar — translatable), keeps all rows and
  the header aligned via shared column widths, scrolls, `AddUnit()`/
  `ClearUnits()`.
- **ButtonHMI**: flat style, thin gray border; FIXED a regression — the
  `DisplayAddress`/`ColorActive` active state was never evaluated. The display
  bit now drives the indicator bar; `BlinkCondition` blinks it red.
- **LampHMI**: hardcoded Lime/Gray replaced with configurable `OnColor`/
  `OffColor` (defaults: Kaban green / flat light gray), active text turns white.
- `Examples/GirisKonvExample.cs` updated to build the 7 clamps inside a
  FunctionTableHMI.
- NOT verified visually (no WinForms rendering in this environment): cell
  proportions, fonts, and the exact green/gray tones need a quick check in the
  VS designer against the real panel.

## 13. Sketch-based redesign: merged button unit + in-button state

- **ButtonHMI** now carries ALL per-button state (per the sketch):
  - `RelayAddress` (M): ON → button shades slightly darker (falls back to the
    legacy `DisplayAddress` so old forms keep working).
  - `OutputAddress` (Y): ON → shades darker still and text turns white.
  - `Lamp1Address`/`Lamp2Address` (+`Lamp1Tip`/`Lamp2Tip`, `LampCorner`): two
    small sensor lamps painted on a corner of the button, each with its own
    hover tooltip; `ToolTipText` covers the button body.
  - The previous top indicator bar was removed in favor of this shading scheme.
  - PlcScanner polls the four new state addresses.
- **FunctionUnitHMI** redesigned as the sketched merged block: title on top,
  buttons A/B stacked in one frame; with a single command the button stretches
  over the whole frame. All lamps/outputs wire INTO the buttons. Optional
  red-on-black encoder readout below. End-position lamp/OUT-lamp properties
  from the table design are gone (replaced by the in-button state).
- **FunctionTableHMI removed** (belonged to the abandoned table layout).
- **Test components removed from screens**: FA2010 + SB3012 HomeScreen designer
  files reset to empty screens (and the orphaned `buttonHMI1_Click` handler
  deleted) so every form opens in the VS designer without errors. AlarmScreen
  controls were kept — they are functional (Acknowledge/Reset), not test items.
- Example rewritten: 7 clamp units in a FlowLayoutPanel using the new
  properties (relay defaults to the command bit; Y outputs drive shading; the
  X sensors are the embedded lamps).
- NOT visually verified here (no WinForms rendering): darkening factors (16% /
  38%), lamp size (13px), and layout proportions may need designer tuning.

## 14. FunctionUnitHMI layout corrected per sketch clarification

- Title now sits ON the group frame (real GroupBox caption), buttons are
  SIDE BY SIDE: A in the left slot, B in the right slot.
- Single-command units keep the other slot EMPTY (reserved, not stretched) so
  button glyphs/texts align vertically across neighboring units. Implemented
  via ButtonHMI.SlotDisabled (hidden, non-serialized) so the scanner's
  visibility handling can't re-show an empty slot.
- Sensor lamps sit on the OUTER corners: A top-left, B top-right.
- Default unit size 320x84; example updated.

## 15. Production-style lamps + PLC message fix + single-field translation

**Sensor lamps restyled** (ButtonHMI): the small corner squares are replaced by
tall thin vertical bars on the inner side of the button, vertically centered,
with a subtle recessed bevel (light top/left, dark bottom/right) — matching the
production panel. Two lamps render side by side (7px wide, 4px gap). Green when
the input bit is ON, gray when OFF.

**PLC "not available" message fixed**: the dialog used to always say
"disconnected or locked out" even when the real reason was simply not being
logged in. PlcManager.DiagnoseAccess / DescribeAccess now return the accurate,
translatable reason — NotLoggedIn / InsufficientLevel / NotRegistered /
Disconnected — and ButtonHMI/NumericInputHMI show that. (The MX Component test
in your screenshot passes, so station 1 was connected; the button was blocked
because no user was logged in.)

**Single-field translation model** (the big one): every visible text is now its
OWN translation key, shown verbatim when the key isn't in the language file.
- New `LanguageManager.Localize(keyOrText)` — translate if the key exists, else
  return the text as-is.
- ALL texts AND tooltips go through it: FunctionUnitHMI title, button captions,
  the button body tooltip, and every lamp/title/position tooltip.
- The confusing separate `TranslationKey` / `ButtonATranslationKey` /
  `ButtonBTranslationKey` fields are hidden from the designer (kept as
  non-serialized aliases so existing .Designer.cs files still compile). Use the
  single `TitleText` / `ButtonAText` / `ButtonBText` (and the `*Tip`) fields.

## 16. Designer "Method not found: Localize" — stale build

That VS error ("Failed to create component 'FunctionUnitHMI' ... Method not
found: LanguageManager.Localize") is a STALE DLL, not a code bug: the WinForms
designer loads the last *compiled* Kaban.Core.dll, and it was still the version
from before Localize() was added (the Core project hadn't rebuilt, or its build
failed and VS kept the old DLL). The method IS present in source and in a fresh
build.

Fix on the machine:
  1. Build → Clean Solution, then Build → Rebuild Solution.
  2. Make sure Kaban.Core builds with NO errors BEFORE opening any screen in
     the designer (Core must compile first; the designer uses its DLL).
  3. Close and reopen the designer window (or restart VS) so it reloads the
     freshly built DLLs.

Hardening added so this can't blank a whole form again: FunctionUnitHMI.
ApplyTranslation() is now wrapped in try/catch and falls back to raw text if a
translation/DLL mismatch ever throws, so the control still renders in the
designer instead of failing to instantiate.

## 17. REAL fix for "Method not found: Localize" + all button props exposed

**Why #16's try/catch didn't work:** a MissingMethodException is thrown when the
CALLING method (ApplyTranslation) is JIT-compiled - before its first line runs -
so a try/catch *inside* that method can never catch it. The only real fix is to
not reference the missing method at all.

**Fix:** FunctionUnitHMI and ButtonHMI no longer call the new
LanguageManager.Localize(). They use GetString(key, key) instead (present in
every Core build since day one), which behaves identically (translate if the key
exists, else show the text literally). The UI assembly now has ZERO dependency
on any Core method the designer's cached DLL might predate - so the control
instantiates even against an older Kaban.Core.dll. (A clean Rebuild is still
good practice, but no longer required just to open the screen.)

**All ButtonHMI properties now available on the unit's buttons:** FunctionUnitHMI
exposes `ButtonA`, `ButtonB` (and `PositionDisplay`) as expandable, serializable
sub-objects in the property grid. Every ButtonHMI property - colors, images,
WordActions, blink, triggers, fonts, etc. - is editable directly. The unit's
Action A/B convenience fields still set the common wiring (command/relay/output/
sensor-lamp addresses + security); anything else set on the button is preserved.

## 18. FunctionUnitHMI layout fixes + relay fallback

- **Smaller defaults**: unit 320x84 -> 260x66, position font 13 -> 11, so buttons
  fit the frame instead of overflowing.
- **Position readout only renders when a device is entered** (PositionAddress
  set) - it no longer appears on the button when empty, and no longer steals a
  bottom strip. Buttons now fill the FULL frame height.
- **Position is centered BETWEEN the two buttons** when present: layout becomes
  [ A | position | B ] (back left, forward right, position middle - like the
  reference screen). With no position it's a clean [ A | B ] 50/50 split.
  Verified geometry: 2-btn no-pos = two 121px buttons; 2-btn +pos = two 78px
  buttons with an 80px readout centered between them; buttons always full height.
- **Blank filler for empty slots**: a missing command renders a flat blank cell
  the same size as a real button, so 1-button and 2-button units look identical
  (the only visual difference is the centered readout when a position device is
  added, which slightly narrows the buttons to fit).
- **PositionOffset** added (DynamicValue, Static/Tag/HmiVariable) - the position
  tag now supports an address offset just like NumericDisplayHMI.AddressOffset;
  the scanner already polls it generically.
- **Relay fallback**: RelayAddress/RelayBAddress are an edge case - when empty,
  the command bit (CommandAAddress/CommandBAddress) is used automatically as the
  relay for the darker-on-press feedback. Set them only when the machine has a
  separate internal relay bit distinct from the command.

## 19. Layout overhang + floating position box fixed

- **Right-edge overhang fixed**: LayoutChildren no longer mixes DisplayRectangle
  with independent area.Right math (which disagreed and pushed button B / the
  blank filler past the frame). It now partitions ONE consistent width into
  A | (pos) | B, giving the rounding remainder to B so the slots sum EXACTLY to
  the frame width. Verified: zero overhang at 180/260/340px widths, with and
  without position.
- **Floating black position box fixed**: NumericDisplayHMI now starts Visible=
  false and is only shown (and brought to front) when PositionAddress is set, so
  it can't paint over a button before layout runs.
- Position width auto-clamps so buttons always keep >=40px even in narrow units.
- Uses GroupBox ClientSize (reliable at design time) instead of DisplayRectangle.

## 20. Login button overlap + stale-instance self-correction

- **Login/Logout button no longer overlaps the language combo & Unit Settings**:
  it was anchored to the far-right edge, landing on top of cmbLanguage /
  btnUnitSettings (both at x>=1130). It's now positioned to the LEFT of that
  right-hand block (16px gap), with the user label left of the button.
- **Stale placed units self-correct**: FunctionUnitHMI now re-runs LayoutChildren
  on OnHandleCreated / OnParentChanged (not just OnResize), so a unit placed with
  an older version of the control (overflowing buttons / floating position box in
  the top unit of the screenshot) fixes its child bounds at load/design time.
  NOTE: the unit's OWN saved Size is still whatever the .Designer.cs holds - if a
  specific old unit still looks off, delete & re-drop it (or reset its Size); the
  internal layout then adapts correctly (the bottom unit in the screenshot, placed
  fresh, already renders right).

## 21. Button state redesign (2-color) + nav highlight + unit sizing

- **New 2-color button scheme** (was 3 confusing shades + gray hover):
  - Relay (M) ON or active nav page -> DARKER background.
  - Output (Y) ON -> bold GREEN border (no more third background shade).
  - HOVER -> blinking bold BLUE border. The default WinForms gray hover/press
    fill is neutralized (MouseOver/DownBackColor synced to the back color) so
    hover no longer collides with the pressed look.
  - New props: OutputBorderColor, HoverBorderColor, IsActivePage.
- **Active navigation page** now shows the darker background (MainForm tracks the
  menu buttons and sets IsActivePage on the selected one); nav buttons also get
  the blinking hover border.
- **FunctionUnit sizing**: buttons no longer stretch full-height (capped at 40px,
  vertically centered); position readout enlarged (width 80->110, height 26->30)
  so numbers are readable; default unit 300x62. Overhang re-verified at 0.

## 22. Border/hover refinement + rounded nav + bigger position display

- **Green removed from button borders**: green is now reserved for the sensor/
  output LAMPS only. The button's output/relay state shows via background darken;
  no green border competes with hover.
- **Hover = slight thin border, no blink, no bold**: 1px HoverBorderColor (blue)
  on mouse-over, drawn once (blink timer removed entirely).
- **Rounded nav buttons**: ButtonHMI gained CornerRadius (rounded region + a
  subtle 1px RestingBorderColor outline that turns blue on hover). Nav buttons
  use CornerRadius=6 with a small margin. Function-unit buttons keep square
  corners (radius 0 = hover-only border).
- **Position display bigger**: font 11 -> 18pt bold, height 30 -> 40px; unit
  default 300x62 -> 300x72 and button-height cap 40 -> 46 so the taller readout
  fits centered. Overhang re-verified 0.

## 23. Phantom position box, sizing, digits, rounded frame, active-nav border, lamp spacing

- **Phantom position readout fixed for real**: ButtonA/ButtonB/PositionDisplay
  were exposed with Content serialization, so the designer wrote their Visible/
  bounds into the .Designer.cs and overrode runtime logic (a black box appeared
  on units with no position tag). Switched to Hidden serialization - still fully
  editable in the property grid, but runtime ApplySettings/LayoutChildren is now
  authoritative. No position address -> no box.
- **Wider unit / taller readout**: default 300x72 -> 360x78; position height
  40 -> 52; button-height cap 46 -> 54, so two buttons + the readout fit better.
- **PositionDigits property added**: like NumericDisplayHMI.Digits. The readout
  width now AUTO-FITS the digit count (+ decimals via PositionDecimalPlaces) so
  it only takes the space it needs; narrower monospace font (DejaVu Sans Mono /
  Consolas fallback) so numbers use less width.
- **Rounded unit frame**: FunctionUnitHMI outer corners get a small (8px) radius.
- **Active nav border darker**: the selected nav page now draws a darker, 2px
  rounded outline (was only a background change, looked borderless).
- **Sensor lamps closer to the edge**: inset 6 -> 3px, inter-lamp gap 4 -> 2px,
  so they hug the button end and free up center text space.

## 24. Position readout fully auto-sized (removed manual PositionWidth/PositionHeight)

You were right - PositionWidth and PositionHeight shouldn't be manual properties;
they're derived from the unit. Both REMOVED from the property grid.
- **Height** = the button row height, always. The readout is now the exact same
  height as the buttons beside it (fixes the taller-than-buttons misalignment).
- **Width** = auto-fit to the FULL formatted string: integer digits (PositionDigits)
  + decimal point + decimals (PositionDecimalPlaces) + suffix (PositionSuffix).
  This fixes the clipped "12.35 mm" - the suffix and decimals are now measured, so
  nothing gets cut off, and the box is only as wide as it needs to be.
- **Font** auto-scales to the row height (~0.5x), so the digits fill the box
  regardless of unit size.
- Added a reentrancy guard around LayoutChildren (the dynamic font change could
  otherwise re-enter layout).
Remaining Position properties are only the meaningful ones: Address, DataType,
Divisor, Digits, DecimalPlaces, Suffix, Offset, Tip.

## 25. Suffix rendering + button/readout balance + taller lamps

- **PositionSuffix now renders**: BaseNumericHMI formatted the number but never
  appended Suffix - it's now added (" mm") to every NumericDisplayHMI's text.
  (Affects all numeric displays, not just the position readout.)
- **Readout no longer dominates**: position width is capped at 42% of the unit
  (plus a 60px hard floor for the buttons), so buttons keep a fair width instead
  of being squeezed thin. The width auto-fit sample now matches the real text
  (includes the space + suffix), so measuring is accurate.
- **Numbers less wide**: position font scale 0.5 -> 0.42 of row height.
- **Sensor lamps taller**: bar height ratio 0.62 -> 0.80 of button height, so they
  read clearly on the taller buttons.

## 26. Phantom box (real fix), centered digits, rounded frame, suffix note

- **Phantom position box - hardened**: the small-size early-return in
  LayoutChildren skipped hiding the readout, and ApplySettings didn't force it
  away when empty. Now: no PositionAddress -> the readout is hidden AND parked
  off-screen (-1000,-1000) in ApplySettings, and the early-return also hides it.
  A stale/early layout can no longer leave a black box over the caption. (Old
  PLACED units still need a re-drop; fresh ones are clean.)
- **Digits centered**: position readout TextAlign set to MiddleCenter (base
  numeric display defaults to MiddleRight).
- **Rounded frame for real**: the stock GroupBox draws square corners that hid
  the rounded region. Replaced it with a custom RoundedGroupBox (Panel that
  paints a rounded border + caption), so the unit frame is visibly rounded.
- **Suffix**: the append fix (" mm") is in BaseNumericHMI.UpdateState, which runs
  when the SCANNER reads the PLC - so the suffix shows at RUNTIME (against the
  simulator/PLC), not necessarily in the static designer preview.

## 27. Caption on borderline, centered digits, color unification, nav-form connect

- **Caption back ON the border line**: RoundedGroupBox now draws the frame's top
  edge through the vertical middle of the caption (classic GroupBox look) instead
  of below it, so the label straddles the border again.
- **Digits centering**: removed the readout's right padding (0,0,6,0) that was
  pushing text off-center; now truly MiddleCenter. Font scale 0.42 -> 0.36 so
  digits are narrower and position vs non-position units differ less in button
  width.
- **Color consistency**: left menu panel now matches the top panel color, so the
  white content area is the only distinct surface.
- **Active nav button = white** (ActiveBackColor), blending into the form content.
- **Nav buttons connect to the form**: RoundLeftOnly rounds only the LEFT corners
  (right side square, zero right margin) so the button reaches the content edge
  like a "{" embracing the form.

## 28. Button overflow fix + nav-form connection

- **Buttons fit again**: RoundedGroupBox had Padding(4,22,4,4) while LayoutChildren
  positions children absolutely from ClientSize - the two combined double-offset
  the buttons and pushed them past the frame. Panel Padding set to 0; layout's own
  topPad/botPad is the single source of truth. Verified buttons fit at 66/72/78px.
- **Nav connects to the form**: (1) the menu panel's right edge is now extended to
  meet the content panel (gap closed), so the docked nav button's square right edge
  sits flush against the form; (2) the active (white) nav button omits its RIGHT
  border entirely, so it merges seamlessly into the white content area as an open
  "{" shape.

## 29. Nav active-color bug (root cause) + guaranteed button fit

- **Nav active inconsistency fixed at the root**: the scanner targeted the whole
  FORM (new PlcScanner(this)), so it recursed into the menu panel and ran
  UpdateState on the nav buttons - and UpdateState darkened the active one,
  overriding the white ActiveBackColor from the setter. Whichever nav button got
  scanned looked dark (Home), the other stayed white (Alarms). Two fixes:
  (1) scanner now targets pnlContent only, so nav buttons are never scanned;
  (2) UpdateState uses ActiveBackColor for IsActivePage instead of darkening.
  Now both nav buttons behave identically (white when active).
- **Button fit guaranteed**: max button height 54 -> 44, default unit 360x78 ->
  360x74. Verified buttons fit with 8-18px clearance below the frame at every
  height 60-90px. (Overflowing units in the screenshot are stale placed
  instances - re-drop them.)

## 30. Button-height ROOT CAUSE found (via diagnostics), two-layer translations, Key-suffix, disabled tooltips

**Button overflow - actual root cause (found with instrumentation, not guessing):**
Added env-gated layout tracing (KABAN_LAYOUT_DEBUG=1) and ran the control headless
under Xvfb. The trace revealed: on the FIRST layout pass (OnHandleCreated/
OnParentChanged), the Dock.Fill RoundedGroupBox had NOT been resized to the unit
yet - it still reported its default 200x100. LayoutChildren computed against that
100px height while the unit was only 74px, so buttons overflowed. FIX: compute
layout from the UNIT's own ClientSize (this.ClientSize) instead of the not-yet-
resized child groupbox. Confirmed by measurement: overflow is now negative
(clearance) on every pass and every unit size. This is the real fix; the earlier
size-tweaks were treating symptoms.

**Two-layer translations**: LanguageManager now loads a MACHINE file and a COMMON
file. Lookups check machine first, then common, then fall back to the literal
text. LoadMachineTranslations / LoadCommonTranslations (old LoadTranslations still
works = machine layer). MainForm loads CommonTranslations.csv + Translations.csv.
A CommonTranslations.csv (from your provided keys) is included and copied to output.
Verified with 8 tests incl. machine-override-of-common and language switching.

**"...Key" suffix**: FunctionUnitHMI text/tip properties renamed to end in "Key"
(TitleTextKey, ButtonATextKey, ButtonATipKey, Lamp1ATipKey, ...) to remind you they
are translation keys. Old names kept as hidden aliases so existing forms still load.

**Auto disabled-reason tooltips** (the feature those Sys_* keys were for): hovering
a DISABLED ButtonHMI now shows why - Sys_Disabled + Sys_ReqSec (security level) /
Sys_ReqState (machine state) / Sys_WaitSignal (trigger address), all translated.

## 31. Role-based automatic tooltips (ported from KabanHMI_26_03_09)

The 26_03_09 build had a feature the upgraded version was missing: tooltips that
change with the LOGGED-IN ROLE. Ported and extended to all four control families.

**ADMIN (SecurityLevel.Admin) -> technical diagnostic view.** Hovering any control
shows its full wiring, so an engineer can inspect bindings without opening the
designer:
- ButtonHMI: visibility/enable conditions, enable trigger, required security,
  relay (M), output (Y), both sensor lamp addresses, display address + offset,
  and an enumerated list of Bit Actions / Word Actions. If the button is currently
  blocked, the reason is appended too.
- NumericInputHMI: the above plus write condition, enable trigger, min/max limits
  and the write formula.
- NumericDisplayHMI (via BaseNumericHMI): address, address offset, visibility
  condition, security level.
- LampHMI: address, address offset, visibility condition.

**OPERATOR -> translated, actionable view.** No technical detail. If a control is
blocked they see WHY, through the Sys_* keys in CommonTranslations.csv:
Sys_Disabled + Sys_Reason + one of Sys_ReqSec (security level) / Sys_ReqState
(machine state) / Sys_WaitSignal (trigger the machine is waiting on). Otherwise
they see the control's own ToolTipTextKey (translated, literal fallback).

Notes:
- Tooltips are rebuilt when the ROLE changes, not just when the hovered region
  changes, so logging in as admin updates them without restarting.
- Adapted to this codebase's names: PlcTrigger.MonitorAddress / .DescriptionKey
  (26_03_09 used .Address / .Description), and Kaban.UI.Controls namespace.
- New ToolTipTextKey property on LampHMI and BaseNumericHMI (translation key with
  literal fallback), consistent with the "...Key" naming convention.
- Verified with an automated test under Xvfb: 7/7 pass, confirming operators never
  receive technical detail and that the admin/operator tooltips genuinely differ.

## 32. SAFETY: enable conditions were not enforced + per-button conditions, grid cleanup, region tooltips, page-change layout

### (a) CRITICAL - buttons were operable with unmet conditions
Two compounding defects, both found by tracing rather than inspection:

1. **The scan loop was permission-gated.** PlcScanner.ScanLoop called
   PlcManager.GetDriver(1), which runs CheckPermission() -> GenelPermission ->
   requires an Operator login. If that failed the loop returned IMMEDIATELY, so
   UpdateState never ran on ANY control: no conditions evaluated, no triggers
   checked, no disabled reasons built. Monitoring is not commanding, so reading
   must not be permission-gated. Added PlcManager.GetDriverForMonitoring(id)
   (no permission check) and pointed the scanner at it. WRITES still go through
   GetDriver() and stay permission-checked.
2. **Controls defaulted to ENABLED (fail-open).** ButtonHMI/NumericInputHMI had
   _softEnabled = true, so a control the scanner had never evaluated was fully
   clickable. Now FAIL-CLOSED (false): nothing is operable until a scan proves
   its conditions are met. Until then the tooltip explains why, via the new
   Sys_NoData key ("Waiting for PLC data." / "PLC verisi bekleniyor.").

This is why the trigger and enable conditions in your screenshot had no effect
and no operator tooltip appeared.

### (b) Per-button conditions that ADD to, never remove, the unit's
FunctionUnitHMI gained ButtonAVisibilityConditions / ButtonAEnableConditions /
ButtonAEnableTrigger (and the B equivalents). They are OR-ed with the unit-level
conditions, so a button can only become MORE restricted - the inherited
conditions cannot be deleted from a sub-button, and one button's extra never
affects the other. These live on the UNIT, so unlike the old sub-button editing
they actually persist in the .Designer.cs.

### (c) Sub-component property grid cleaned up
New FunctionUnitChildConverter for ButtonA/ButtonB/PositionDisplay:
- HIDES everything the unit owns - PlcNumber (a unit is one PLC family, so a
  per-child station made no sense), addresses, actions, conditions, security and
  geometry (serializing geometry is what caused the stale bounds / floating box).
- ORDERS the rest: HMI appearance properties get a leading space so they sort to
  the TOP of the grid, above the inherited WinForms properties.

### (d) Region-specific admin tooltips
Hovering a sensor lamp now shows ONLY that lamp (address + live ON/OFF state)
instead of the entire unit dump; the button body shows just that button's wiring.

### (e) Buttons broke after a page change
Tracing showed that navigating back re-adds the screen WITHOUT firing any
resize/parent/visible event on the units, so they kept the bounds they held while
detached. Added FunctionUnitHMI.Relayout(), called from MainForm.LoadScreen for
every unit on the newly shown screen.

Verified: 12/12 condition & fail-closed tests, 9/9 role-tooltip tests, 8/8
translation tests, 42/42 address tests, and the layout harness still reports
clearance (no overflow) on every pass.

## 33. BUILD FIX: unregistered source file (CS0234 / CS0006)

`Design\FunctionUnitChildConverter.cs` was created but never added to
Kaban.UI.csproj. Visual Studio compiles only what the .csproj lists, so it never
saw the file -> CS0234 (type not in namespace Kaban.UI.Design) in Kaban.UI, and
the CS0006 in Kaban.Machines.FA2010 was just the knock-on effect of Kaban.UI
failing to produce its DLL. Registered the file.

ROOT CAUSE OF THE CLASS OF BUG: the verification builds here used a filesystem
glob (find *.cs), which compiles files the .csproj may not reference - so a
missing registration compiled clean locally and only failed in VS. Fixed the
process too: builds now run from the .csproj <Compile Include> lists, exactly
like Visual Studio, and an audit compares every project's file list against disk
in both directions (unregistered files AND ghost entries). Audit is clean for all
five projects; all build from their csproj lists.

Note: KabanHMI\Forms\CSharpInputBox.cs is on disk but intentionally NOT in the
csproj - it is unreferenced dead code from the original project, so it was left
excluded rather than silently pulled into the build.

## 34. MULTI-MACHINE ARCHITECTURE: main machines + mountable sub-machines

Yes - the solution now composes machines instead of hardcoding one. New in
Kaban.Core\Machines:

**IMachineModule** - one machine's complete definition: PLC stations, condition
bits, alarms, translations and screens. The SAME contract serves a main machine
and a sub-machine, so a sub-machine can be promoted to a main machine later just
by adding an entry-point project, and a main machine can be mounted into a bigger
line without code changes.

**MachineRegistry** - mounts one main machine plus any number of sub-machines and
hands each a MOUNT CONTEXT that removes every collision:
- `Station(local)` - a module always talks about its OWN station 1,2,...; the host
  maps them: `MountSub(new VidalamaModule(), stationMap: new[]{3})` puts the
  module's station 1 on the line's physical station 3. The module is unchanged
  when mounted elsewhere.
- `AllocateMachineBit(tag)` - condition bits are ALLOCATED, never hardcoded.
  Previously every machine hardcoded `1L << 16`, so two machines in one HMI would
  silently claim the same bit.
- `Scoped(id)` - namespaces ids as `Vidalama.TorqueFault`, so two machines may use
  the same alarm codes / condition names.
- `ResolveFile(name)` - each module loads its OWN Alarms.json / Translations.csv
  (from a per-module subfolder if present).

**Supporting fixes this required:**
- `PlcManager` permissions are now DECLARATIVE. `RegisterStation(id, name, level,
  owner)` replaces the hardcoded `if (plcNumber == 3) return VidalamaPermission`.
  Each module declares the level needed to COMMAND its stations, so multiple
  restricted stations, or a screwdriver on a different station number, need no
  core change. `SetPermissionOverride`/`ClearPermissionOverrides` remain for
  key-switch hardware. (GenelPermission/VidalamaPermission are gone.)
- `AlarmDefinitionLoader.Load(path, replaceExisting, moduleId)` can APPEND -
  previously every load cleared the list, so the last machine wiped the others.
- `LanguageManager.AddMachineTranslations(path)` appends a machine-layer file, so
  each mounted machine ships its own texts. Later files win, letting a host
  override a sub-machine's wording without editing it.
- `MainForm.BuildNavigation()` builds the whole menu from the mounted modules -
  main machine pages first, then each sub-machine's (prefixed with its display
  name so two "Home" pages are distinguishable). Views AND icons are created
  lazily, so mounting never loads resources for pages nobody opens.

**Demonstrated end to end:** `Kaban.Machines.Vidalama` is a real, separate project
(a library, mountable anywhere) with its own module, screen, Alarms.json and
Translations.csv. FA2010's Program.cs now reads:

    MachineRegistry.MountMain(new FA2010Module());
    MachineRegistry.MountSub(new VidalamaModule(), stationMap: new[]{ 3 });
    shell.BuildNavigation();

FA2010 itself was converted to `FA2010Module` and uses ctx.Station()/
AllocateMachineBit() throughout, so it too can be mounted as a sub-machine.

Verified by 19 automated tests: station remapping, per-station permission
enforcement by login level, key-switch override, NO duplicate condition bits
across modules, id namespacing, composed navigation ordering, and rejection of
duplicate/second-main mounts. All previous suites still pass and all six projects
build from their .csproj file lists.

## 35. Full-solution audit (build-accurate) + cleanup

Rebuilt the entire solution from each .csproj's file list (Visual-Studio-accurate,
not a glob) and audited every project against disk. Findings and fixes:

1. **Removed a broken orphan project directory** `Kaban.Drivers.Mitsubishi/`.
   It duplicated `Kaban.Drivers.MitsubishiActUtl/` but its .csproj still referenced
   the never-created `Class1.cs` (its MitsubishiActUtl.cs held only the empty VS
   template `class Class1 {}`). It was not in the .sln and nothing referenced it,
   but it was a build trap. Deleted. The real driver (Kaban.Drivers.MitsubishiActUtl)
   is untouched and is the one the .sln and all machines reference.

2. **Unified FA2010 design-time config with the runtime module.** MachineConfig.cs
   (called ONLY under the VS Designer by HomeScreen) kept its own hardcoded copy of
   the conditions with fixed bits (1L<<16, ...), which could silently drift from
   FA2010Module's allocated bits. It now delegates to
   MachineRegistry.MountMain(new FA2010Module()), so the designer sees exactly what
   runs. (No runtime effect - the call is DesignMode-guarded.)

Verified after changes:
- All 8 assemblies compile from their .csproj lists (Core, UI, Simulation,
  MitsubishiActUtl, Vidalama, FA2010/SB3012/SB3015).
- csproj-vs-disk audit clean for every project.
- 15/15 multi-machine composition tests pass (station mapping, per-station
  permissions, override, no duplicate condition bits, namespacing, nav ordering,
  duplicate/second-main rejection).
- 8/8 safety tests pass (fail-closed default, per-button inherited conditions,
  enable-trigger + condition enforcement).

Notes (NOT changed - flagged for a product decision, not defects):
- `Kaban.Drivers.Siemens` is an empty placeholder (`class Siemens {}`, no
  IPlcDriver implementation) and is unreferenced. A real Siemens driver is a
  feature to build, not a bug to fix.
- SB3012/SB3015 still use the original direct AddMenuButton + runtime
  MachineConfig.Initialize() path (no sub-machine support). This is self-consistent
  and runs correctly; converting them to IMachineModule (like FA2010) is a
  migration to do when they need mountable sub-machines.

## 36. Generic sub-machine naming, shared translation file, testable simulation driver

### (a) CS0006 fixed - sub-machine project was never in the solution
`Kaban.Machines.Vidalama` existed on disk and FA2010 referenced it, but it was
never added to KabanHMI.sln. Visual Studio therefore never built it, so its DLL
did not exist and FA2010 failed with
"Metadata file '...Kaban.Machines.Vidalama.dll' could not be found".
Both sub-machine projects are now registered in the .sln with project entries,
Debug/Release configurations, and nesting under the 03_Machines solution folder.

### (b) "Vidalama" renamed to generic sub-machine names
The word was a customer/equipment-specific term baked into the framework. Now:
  Kaban.Machines.Vidalama  ->  Kaban.Machines.SubMachine1
                           +   Kaban.Machines.SubMachine2  (second one added)
Renamed throughout: namespaces, module classes (SubMachine1Module), screens,
ModuleIds, translation keys, alarm keys, csproj/AssemblyInfo, and the remaining
references in Kaban.Core and MainForm comments. Zero occurrences of the old term
remain. The modules read as reusable TEMPLATES - replace the addresses and screen
contents with whatever the real unit is (screwdriver, connection robot, ...).
SubMachine2 uses distinct addresses (M20/M21, Y10-Y12, X20, D20) so the two are
clearly separable when testing.

FA2010 now mounts BOTH, demonstrating multiple sub-machines on one HMI:
    MachineRegistry.MountSub(new SubMachine1Module(), stationMap: new[]{ 3 });
    MachineRegistry.MountSub(new SubMachine2Module(), stationMap: new[]{ 4 });

### (c) Translation files: one shared common file, one per machine
CommonTranslations.csv was DUPLICATED inside Kaban.Machines.FA2010, so the shared
keys were effectively owned by one machine. Now:
  - ONE shared /CommonTranslations.csv at solution root; each machine .csproj
    links it (<Link>CommonTranslations.csv</Link>) so it copies to that machine's
    output. Edit once, every machine gets it.
  - Each machine keeps its OWN Translations.csv (FA2010, SubMachine1, SubMachine2).
  - Removed the duplicate FA2010/CommonTranslations.csv and a vestigial root
    Translations.csv.
  - Sub-machine files copy into a per-module SUBFOLDER (<Link>SubMachine1\...</Link>)
    which is what MountContext.ResolveFile() looks for first. Without this every
    machine's Translations.csv would overwrite the others in bin\Debug.
Verified against a simulated bin\Debug layout: common keys resolve, each
sub-machine loads its own file, sub-machines do not overwrite each other, and
EN/TR switching works across all layers.

### (d) SimulationDriver rewritten as a usable dummy PLC (multi-CPU testing)
Kept the original behaviour and added what testing actually needs:
  - THREAD SAFETY: all state guarded by a lock. The scanner reads on a background
    timer thread while the UI writes; Dictionary is documented as unsafe for that
    pattern (can corrupt state or throw). NOTE: I could not force a failure on
    Mono, so this is correct defensive practice rather than a reproduced crash.
  - Case/whitespace-insensitive addresses - "m100", "M100" and " M100 " were three
    separate cells before, so writes appeared to vanish on read.
  - Seed(addr,val) / Seed(dictionary) to preset values so enable-conditions and
    triggers can actually be satisfied in a test.
  - LoadSeedFile(path): "ADDRESS = VALUE" lines with # and // comments, so a test
    scenario changes without recompiling. A documented SimSeed.Station2.txt ships
    with FA2010.
  - AddMirror(command, feedback): writing a command bit drives its feedback bit,
    so pressing a button lights lamps and satisfies conditions instead of the
    screen looking dead.
  - AddRamp(addr,min,max,stepPerSecond): value advances with time so position
    readouts visibly move. Computed from elapsed time on READ - no background
    thread, so it stays deterministic and race-free.
  - Each instance has its own memory, so SimulationDriver(1) and (2) are genuinely
    independent CPUs.
FA2010's Program.cs has a UseSimulation switch that registers simulated stations
1-4 (CPU1, CPU2, and the two sub-machines) with sensible seeds, so the whole HMI
including multi-CPU paths runs with no hardware attached.

Verified: all 9 projects build from their .csproj lists, csproj-vs-disk audit
clean, 16/16 simulation-driver tests (incl. concurrency), 16/16 multi-machine
composition tests (now covering two sub-machines), 8/8 safety tests, 8/8
translation-layout tests.

## 37. Standalone sub-machines, mixed PLC1/PLC2 drivers, Simulation Monitor, component showcase

### (a) Sub-machines are now STANDALONE machines that can also be mounted
Previously they were Library projects - they could only exist inside a host.
Both are now OutputType=WinExe with their own Program.cs, so each builds its own
.exe and its own bin\Debug and can be run/debugged on its own (mounting itself as
MAIN, station 1:1). A .NET project can reference an .exe as an assembly, so
FA2010 still references them and mounts their Module classes as SUB machines to
form a combo machine - the standalone entry point is simply ignored in that case.
Same source, both roles, no duplication. Verified by a dual-mode test.

### (b) PLC1 = Mitsubishi, PLC2 = Simulation (as requested)
FA2010 now registers station 1 with the Mitsubishi driver (visible in GX Works)
and stations 2-4 with SimulationDriver. The UseSimulation flag was removed in
favour of this mixed setup so both driver paths run side by side.

### (c) SIMULATION MONITOR - how to actually watch the simulator
A Mitsubishi station can be watched in GX Works; the in-memory simulator had no
equivalent, so there was no way to confirm a control reached station 2. Added
KabanHMI\Forms\SimulationMonitorForm.cs:
  - one tab per simulated station, live device memory, refreshed 4x/second
  - shows raw value AND ON/OFF state per address
  - EDITABLE: type a value + Enter to feed the HMI (turn a sensor on, set a
    position) - the equivalent of forcing a device in GX Works
  - writes made BY the HMI appear immediately
Opened with F12, or the "Sim Monitor" button that appears in the top bar only
when a simulated station is registered. The UI layer does not reference the
Simulation assembly - the driver is discovered by type name via reflection.

### (d) Component showcase on FA2010 HomeScreen
HomeScreen.Designer.cs now contains ONE OF EVERY control with its properties
filled in: ButtonHMI, LampHMI, NumericDisplayHMI, NumericInputHMI,
FunctionUnitHMI, DisplayPLC, HexTextBox, CustomTextBox, LabelHMI.
Laid out as TWO COLUMNS: the left targets PlcNumber=1 (Mitsubishi) and the right
targets PlcNumber=2 (Simulation), with otherwise identical configuration - so
pressing the right-hand controls and watching the Sim Monitor proves the
multi-PLC routing works. 16 matching translation keys added.

Verified: all 9 projects build from their .csproj lists, csproj audit clean,
dual-mode (standalone + mounted) tests pass, plus the simulation-driver,
safety and translation-layout suites.

## 38. Unit Settings did nothing - unit conversion wired end to end

The Unit Configuration dialog changed UnitManager's active unit, but NOTHING
listened. BaseNumericHMI declared a UnitType property and never used it: it
formatted the raw value and appended the hardcoded Suffix. So picking "inch" left
the display showing "222.2 mm".

**Fixed in both directions:**
- READ: BaseNumericHMI now converts the PLC value with UnitManager.ToDisplay(),
  uses the active unit's name as the suffix, and adds that unit's ExtraDecimals
  (inch needs more precision than mm). _lastNumericValue is deliberately kept in
  the REFERENCE unit so limits and formulas stay in one consistent scale.
- WRITE (safety-relevant): NumericInputHMI converts the typed value back with
  UnitManager.ToReference() before writing. Previously, typing "10" while the
  display showed inches wrote 10 - i.e. 10 mm instead of 254 mm - silently
  commanding the wrong position.
- KEYPAD LIMITS (safety-relevant): limits resolve in reference units but the
  operator types in display units, so they are now converted for the keypad.
  Before, with limits 0-1000 mm shown as inches, typing 500 passed validation
  (500 <= 1000) and was then written as 12700 mm, far outside the real limit.
- Suffix stripping when editing now removes the ACTIVE suffix ("inch"), not just
  the designer-set Suffix ("mm"), so the old value parsed correctly.
- Controls subscribe to UnitManager.UnitChanged and re-render immediately, so the
  change is visible without waiting for a scan (which never arrives when offline).
  Unsubscribed on Dispose.

**Convention bug fixed in UnitManager:** every unit used
"reference = display * Factor + Offset" (mm = inch * 25.4, gr = lbs * 453.592,
sec = min * 60) EXCEPT Fahrenheit, which was registered as Factor 1.8 / Offset 32
- the inverse direction. Converting 100 C would have produced 87.6 F instead of
212 F. Re-registered as Factor 5/9, Offset -160/9. Verified: 0 C -> 32 F,
100 C -> 212 F, -40 C -> -40 F.

**FunctionUnitHMI** gained PositionUnitType so the position readout follows Unit
Settings too (PositionSuffix remains the fallback when no unit family is set).
The FA2010 showcase uses it.

Verified by 22 conversion tests (all families, both directions, round-trips on
every length unit, event firing) plus a control-level test proving the displayed
text converts and refreshes live on a unit change with no new PLC scan.

## 39. CommonTranslations moved into Kaban.UI - one shared file for every machine

The common keys were being DUPLICATED into each machine's Translations.csv
(FA2010's copy repeated all 17 Btn_/Sys_/Unit_ keys), which meant translating the
same key once per machine - exactly what the two-layer lookup was meant to avoid.

**The file now lives in the Kaban.UI project** (KabanHMI\CommonTranslations.csv),
the library every machine already references:
  - Registered as an EmbeddedResource, so any machine referencing Kaban.UI has the
    shared keys even if the .csv is missing from its output folder.
  - Also registered as a copied file, so it can still be edited in the field
    without rebuilding.
  - Every machine .csproj links it from its new home
    (..\KabanHMI\CommonTranslations.csv), because old-style .csproj files do NOT
    propagate content from referenced projects to the referencing project's output.
  - MainForm prefers the on-disk file and falls back to the embedded copy
    (new LanguageManager.LoadCommonTranslationsFromStream + HasCommonTranslations;
    the CSV parser was refactored so file and stream share one code path).

**Removed the 17 duplicated common keys from FA2010\Translations.csv** - machine
files now contain ONLY machine-specific keys.

LOOKUP ORDER (this already worked; it is now actually exercised):
    machine Translations.csv  ->  shared CommonTranslations.csv  ->  literal text
A machine can still override a common key simply by defining it in its own file.

Verified by 11 tests: embedded resource present; embedded fallback populates the
common layer when the file is absent; a machine-only key resolves from the machine
file; a key absent from the machine file resolves from common; unknown keys fall
back to literal text; EN and TR both resolve across layers; and a machine file
overrides a common key when it defines one.

## 40. Window layout: panels now fill and follow the window; top bar stays right

The three panels used absolute Location/Size with Anchor = Top,Left, so nothing
followed the window: pnlContent stayed 1061x694 no matter how large the window
became (machine screens were clipped), and the login / language / Unit Settings
controls stayed at their designed x=1130, stranded mid-window once widened.

**Panels are now DOCKED:**
  pnlTop     -> Dock.Top   (full width, fixed 80px height)
  pnlMenu    -> Dock.Left  (fixed 204px width, full remaining height)
  pnlContent -> Dock.Fill  (everything that is left) + AutoScroll
Add order in the designer was changed to Fill -> Left -> Top, because WinForms
docks the LAST-added control FIRST; with the previous order the menu docked before
the top bar and the top bar would not have spanned the full width.

This also removed the manual `pnlMenu.Width = pnlContent.Left - pnlMenu.Left`
hack: docked panels are adjacent by construction, so the nav button still meets
the content edge, now at any window size.

**Top-bar controls stay pinned right:** cmbLanguage and btnUnitSettings gained
Anchor = Top|Right, and a new LayoutTopBar() recomputes the whole right-hand block
(user label, Login, combo, Unit Settings) from the CURRENT panel width on every
pnlTop.Resize. Anchors alone were not sufficient because the controls were
authored at absolute x=1130 on a panel that never resized.

pnlContent.AutoScroll = true so a screen larger than the window stays reachable
instead of being clipped.

Verified by 20 assertions at 1000x700, 1600x900 and 1920x1080: top panel spans the
full width, content reaches the right edge and the bottom, menu spans the full
height below the top bar, no gap between menu and content, and the combo / Unit
Settings / Login / user label all move right as the window grows without
overlapping each other.

## 41. Operator privacy, Sim Monitor connection, restyled top bar, Program Settings page

### (a) Device numbers hidden from non-admins
The write-confirmation dialog showed the PLC device number ("Write 555 to D210?")
to everyone. Engineering detail like that must not reach an operator. Now only
Admin sees the address; operators/maintenance get a translated, address-free
prompt ("Are you sure you want to change this value to 555?"). The same gating was
applied to the other NumericInputHMI dialogs that exposed an address or a raw
formula/exception (rejected-write, formula error, write error) - non-admins get a
plain "The value could not be written." Verified by 6 tests across levels and EN/TR.

### (b) Simulation Monitor now connects to the PLC2 controls
Two problems: the monitor only listed addresses the driver had already seen (its
seeds), and the PLC2 showcase controls used addresses (D100, D110, M10, X10, ...)
that were never seeded, so nothing lined up. Fixes:
  - Station 2 now seeds every address the PLC2 showcase controls use, so they
    appear in the monitor immediately and are wired end to end.
  - The monitor gained an "Address: [ ] [value] Set/Add" bar per station, so ANY
    address can be introduced and driven on demand - needed because an address
    only appears once a control has read or written it. Writing there feeds the
    HMI (and creates the cell). Verified the monitor-edit -> control-read round
    trip, new-address creation, and command->sensor mirroring (8 tests).

### (c) Top bar restyled; Unit Settings moved off it
The top bar had a cramped combo + Login + Unit Settings cluster. Now: a flat,
accent-blue Login button (turns gray "Logout" when signed in), a clean user label,
and a flat language combo, all vertically centred and pinned right. The Unit
Settings button was removed from the top bar.

### (d) Program Settings page with sub-pages (FA2010)
New left-nav "Settings" button (like Alarms), opening a SettingsScreen that hosts
sub-pages as a vertical tab list down the left side - click one, it shows on the
right. Framework pieces (all in Kaban.UI\Settings):
  - ISettingsPage: title key + fallback + required level + GetControl().
  - SettingsScreen(pages): builds one tab per page, hides pages the current user
    may not access, refreshes on login/language change, accent-bar on the active
    tab.
  - UnitSettingsPage: the old popup rebuilt as a sub-page (unit family combos,
    applied live through UnitManager).
  - UserSettingsPage (Admin), LogSettingsPage, MachineParameterPage,
    BarcodeSettingsPage, AboutPage: navigable placeholder sub-pages so the
    structure is in place to fill in per machine.
Only wired into FA2010 for now (as requested); copying the one GetScreens entry to
the other machines adds it there too. Verified security gating (operator sees a
subset, admin sees all 6) and that the Unit page renders its family combos.

All prior suites still pass; layout test updated for the new top bar (20/20).

## 42. ROOT CAUSE: no PLC driver was ever connected

The Simulation Monitor problem had a deeper cause than the missing seeds fixed in
§41. `PlcManager.RegisterDriver()` only stored the driver, and `PlcManager.Connect(id)`
existed but was NEVER CALLED anywhere in the application. Every driver therefore sat
at IsConnected = false, which means:
  - Write()    returned -1  -> monitor edits were silently refused
  - TryRead()  returned false -> controls saw comm-lost and stayed fail-closed

The simulator MASKED this: Seed() and Dump() touch the memory dictionary directly
without checking IsConnected, so values were plainly VISIBLE in the monitor grid
while every edit did nothing - exactly the reported "I can't change any value in
simulator monitor or they don't get shown on the related controls".

Reproduced before fixing:
    IsConnected after registration : False
    Monitor grid shows D100        : 1234   <- visible
    Monitor edit Write() returns   : -1     <- refused
    Control TryRead ok / value     : False / 0

**Fix 1 - RegisterDriver opens the connection.** It now calls Connect() (guarded by
try/catch and an audit-log entry on failure). Failure is deliberately non-fatal: a
real PLC that is not plugged in stays disconnected and the existing comm-lost /
fail-closed handling applies, which is the safe behaviour.

**Fix 2 - the scanner can now recover.** PlcScanner never retried a connection, so a
PLC that was off at startup, or that dropped out, stayed dead until the HMI was
restarted. It now retries roughly every 2 seconds (throttled, not every scan, so an
absent PLC cannot stall the UI) and reports comm-lost meanwhile.

Verified end to end: registration opens the connection, a monitor edit is accepted
AND applied, a control reads the value the monitor wrote, a monitor command bit
drives the mirrored sensor bit (so lamps react), and HMI writes appear in the
monitor grid. The fail-closed safety suite still passes - controls remain disabled
until a scan genuinely succeeds.

## 43. Startup-error fix, uniform nav, LampDirection, tabbed sub-pages, program-wide audit

### (a) "Startup Error ... 0xF0000003" popup eliminated
MitsubishiActUtl.Connect() THREW on failure, violating the IPlcDriver contract
(Connect() reports failure by returning false). Since §42 auto-connects at
registration, any PLC not reachable at launch (0xF0000003 = logical station not
yet defined / target or GX Works simulator not running yet) surfaced as a startup
popup - even though the scanner's 2-second retry then connected successfully,
which is why communication "was established" anyway. Connect() now returns false,
records LastConnectError, and audit-logs the code. A PLC that is off at launch is
a NORMAL state: comm-lost shows and the retry brings it up.

### (b) Navigation buttons share ONE format
Icon buttons were left-aligned while icon-less ones (sub-machine pages, Settings)
used centred text - two different designs in one menu. All nav buttons are now
left-aligned with the same text x-position (an absent icon just leaves the icon
slot empty), and the Settings page got the settings_24 icon.

### (c) LampCorner -> LampDirection, lamps stacked vertically
The four-corner LampCorner property is gone. ButtonHMI now has
LampDirection = Left | Right choosing WHICH END the lamp strip sits at, and when
both lamps are configured they are STACKED: lamp1 on top, lamp2 on the bottom,
with a small gap so they read as two indicators (a single lamp still uses the
full-height strip). FunctionUnitHMI puts button A's lamps on its outer-left and
button B's on its outer-right; the showcase was updated. Tooltip hit-regions
follow the new rectangles automatically (they use the same LampRect).

### (d) Active-page highlight fixed + sub-pages are now TOP TABS
TWO type-comparison bugs made navigation misbehave:
  - the active-button highlight matched buttons by screen TYPE, and
  - LoadScreen's "already shown" early-return ALSO compared types, so a second
    page of the same class was a dead click.
Both now compare INSTANCES. Verified with two same-type screens: exactly one
button active, and it is the clicked one.
SettingsScreen's sub-page navigation changed from a left column to a horizontal
TAB STRIP along the top (accent underline on the active tab) so second-level
navigation costs no screen width - and a future third level can repeat the same
pattern inside a sub-page. Also fixed a real leak: Dispose unsubscribed with
fresh lambdas (which removes nothing); handlers are now stored and removed.

### (e) Program-wide audit
Swept the whole solution: csproj-vs-disk clean both directions for all 9
projects; no remaining broken-unsubscribe patterns; no other type-comparison
hazards (the LoadScreen one found by the sweep was fixed above); no TODO/FIXME
left; no stale machine terms; all timers have Stop/Dispose paths. Full battery
of 13 regression suites passes (lamps/nav, settings tabs, connection, no-throw
connect, safety fail-closed, operator privacy, sim driver, monitor round-trip,
unit conversion x2, window layout, translation fallback, dual-mode machines).

## 44. x86 fix for MX Component, nav polish, new controls, DXF + PostgreSQL foundations

### (a) THE PLC FIX: machine exes now build as x86
The Communication Setting Utility (32-bit) test succeeded while the HMI failed:
MX Component logical stations live in the 32-bit registry view, and the AnyCPU
HMI ran as a 64-bit process that cannot see them. All five machine executables
now set PlatformTarget=x86 (Debug and Release). The stale
"Failed to connect to PLC Station 1." popup in MainForm_Load was also removed -
the comm banner + 2s retry own that state.

### (b) Navigation
Every nav row now has an icon (screens without one fall back to a generic
chevron), and the menu is COLLAPSIBLE: the toggle at its top switches between
"icon + label" (204px) and icon-only (56px) via SetNavCollapsed().

### (c) Spacing / tabs
FunctionUnitHMI vertical gaps tightened (label->buttons 12, buttons->border 6,
matching the sides); showcase numeric display / input / PLC display heights
unified at 46 = the demo buttons. Settings top tabs are now compact (32px strip,
28px tabs), NORMAL font, and the active tab is white with rounded TOP corners
merging into the white page below - the connected browser-tab look.

### (d) New controls
- GroupBoxHMI: container drawn like FunctionUnitHMI's frame (rounded border,
  caption on the border line). Caption is NORMAL font; setting its Emoji
  property prefixes the symbol AND turns the caption bold - deliberate
  prominence only.
- DescriptionText / DescriptionKey on BaseNumericHMI, ButtonHMI, LampHMI and
  CustomTextBox: an optional label attached to the control's LEFT edge. Design
  answer to "how should label and control match": the label AUTO-SIZES and its
  right edge hugs the control at a constant 8px gap, so short and long texts
  both sit tight (a fixed label column is what made the old screen drift
  apart). Row alignment comes from aligning the controls themselves.
- CheckListHMI: the machine-option list (image-10 style) - "Description : [x]"
  rows in 1..n columns declared as "Key|Fallback|Address" lines, read through
  the scanner like every control, written through PlcManager with the usual
  permission gate + audit log.

### (e) DXF tooling (Kaban.UI.Dxf)
DxfDocument: reader for LINE / LWPOLYLINE (incl. bulge arcs) / ARC / CIRCLE,
reduced to polylines; GetBounds, Rotate90, MirrorX, Distance, and
GetBorderRoute() = the longest closed polyline as an ordered machining route.
DxfViewerControl: black-background profile view (image-9 style), auto-fit,
two-click MEASURE mode raising MeasureTaken with the distance, Rotate90/Mirror
buttons-ready methods, red border-route overlay.
VERIFIED against the supplied 82_Dışa_Kapı.dxf: parses, bounds 115.00 x 82.00
units, rotate swaps the box, mirror preserves it, 3-4-5 distance exact. Note:
this particular file's outer contour is not a single closed polyline, so the
route extractor correctly returns empty for it - route extraction applies when
the drawing has a closed outer contour.

### (f) PostgreSQL foundation (Kaban.Core.Data)
HmiDatabase: parameterised Query/Execute over Npgsql loaded BY REFLECTION, so
the solution builds and runs without the package; IsAvailable reports false
when Npgsql or the server is missing. Install per machine project with
"Install-Package Npgsql", then run Data/Schema.sql (copied next to the exe):
hmi_users, profiles (incl. dxf_file path + JSONB options), profile_steps
(per-head G-code style steps matching the legacy screen), parameter_records.
HMI screens over these tables are the natural next step once the DB direction
is confirmed.

## 45. PLC diagnostics + scan freeze fix, nav toggle/history, tab paint fix, tooltips

### (a) PLC: scan freeze fixed + a diagnostic to pinpoint the connection
Two changes. FIRST, a regression fix: the scanner's reconnect logic returned
early when station 1 was down, freezing the ENTIRE scan (stale-data path and all
control updates included). Station-1-down now only skips the station-1 read.
SECOND, every failed connect now writes an audit line
    [PLC DIAG] Mitsubishi station 1 connect failed with 0x... (process is 32-bit)
The code + bitness in that line identifies the remaining problem: MX Component
stores logical stations PER BITNESS. The exes now build x86, correct when the
stations were configured in the 32-bit Communication Setting Utility
(C:\Program Files (x86)\MELSOFT). If the 64-bit utility was used instead
(C:\Program Files\MELSOFT), remove the two <PlatformTarget>x86</PlatformTarget>
lines from each machine .csproj (or Project Properties > Build > Platform
target > Any CPU) - the definitions then live in the 64-bit view.

### (b) Navigation toggle rebuilt + browser-style history
The toggle was at the BOTTOM and expanding broke captions. Cause 1: BringToFront
puts a Dock.Top control at the END of the dock order - WinForms docks the
LAST-added control FIRST, so SendToBack is what places a control at the TOP.
Cause 2: expanding re-translated captions from keys with the fallback lost,
rendering raw keys ("Btn_..."); captions are now SAVED on collapse and RESTORED
on expand. The toggle is now a hamburger glyph at the very top, above new
Back / Forward buttons that walk a browser-style NavigationHistory - and
settings TAB changes record history entries too, so Back steps back through
tabs and pages alike. Construction-time tab activation records nothing (guard
against ghost entries).

### (c) Settings tabs: captions were invisible until clicked
The tab Paint handler mutated Font/BackColor/Region during the paint cycle,
invalidating layout mid-paint - captions stayed blank until a click forced a
full repaint, and the nav highlight misbehaved alongside. All styling moved out
of Paint into explicit StyleTabs()/ShapeTab() calls; tabs are NORMAL weight in
both states (active = white + rounded top, connected to the page).

### (d) FunctionUnitHMI: regular caption, symmetric tight gaps
Caption font Bold -> Regular. The inner GroupBox already reserves the caption
band, so the extra pads shrink to 4/4 and the button row (still centred) caps at
46px - the standalone-control height. NOTE: units already placed keep their
designed Size; re-drop or set Height ~= caption + 46 + 8 to see the tight look.

### (e) ONE description property (key-or-literal)
DescriptionKey removed everywhere. DescriptionText alone remains on
BaseNumericHMI / ButtonHMI / LampHMI / CustomTextBox: enter a translation KEY
(rendered in the active language) or plain text (shown as-is when the key is
not in the file) - the same simple rule requested for control texts.

### (f) Tooltips: operator layer for everyone, admin extras appended, no NULL noise
Every user now sees the operator tooltip (translated tip, or the translated
disabled-reason while blocked). For admins the technical details are APPENDED
under a "----- For Admin -----" caption (key Tip_ForAdmin), and items with no
value (empty / None / NULL) are OMITTED instead of printed. Lamp tooltips
follow the same pattern.

## 46. PLC root cause from the WORKING build, nav polish, Profiles+DXF screen

### (a) PLC: x86 REVERTED - the working reference build is AnyCPU
The supplied working program (KabanHMI_26_03_09) connects to the SAME simulator
with PlatformTarget=AnyCPU and the same ActUtlType calls. So this site's MX
Component IS visible to 64-bit processes, and forcing x86 in change 44 was the
regression that matched "worked until a few days ago". All five machine exes are
AnyCPU again; nothing else in the connect path differs from the working build
(same ActLogicalStationNumber + Open + GetDevice). The "[PLC DIAG]" audit line
stays - if anything still fails it names the exact code and process bitness.
"Waiting for PLC data." is this HMI's own no-scan-yet message (Sys_NoData), i.e.
a symptom of the same connection, not a separate fault.

### (b) Navigation
Toggle: state-dependent glyph and position - expanded shows a collapse chevron
at the RIGHT end of its strip; collapsed shows the hamburger filling the compact
strip. History Back/Forward moved to the BOTTOM of the nav and became text
arrows, because text auto-greys when a button is disabled (images do not) - so
they now clearly look OFF when there is no page to go to.

### (c) Uniform control heights + symmetric FunctionUnit
ButtonHMI and BaseNumericHMI (inputs AND displays) now share DefaultSize
170x46, so freshly dropped controls line up. FunctionUnitHMI: the caption is
drawn INSIDE the panel (no reserved band), so the layout now derives
topPad = captionHeight + 6 and botPad = 6 = side gap, with the button row
ANCHORED (not centred) - top, bottom and side spaces are equal. Default unit
height is now 75 (caption + 46 + 2x6). Already-placed units keep their old
designed Size until re-dropped or resized.

### (d) Profiles screen with the DXF graph (FA2010)
New left-nav "Profiles" page: the profile table on the left (PostgreSQL
`profiles` when reachable; built-in DECEUNINCK-style sample rows otherwise, so
it works before the DB is set up), and on the right the DXF graph with the full
test toolbar: Rotate 90, Mirror, two-click Measure (result shown below) and the
border-Route overlay. Selecting a row loads its dxf_file. The supplied
82_Disa_Kapi.dxf ships in Profiles\ and is copied next to the exe;
Data\SampleData.sql inserts matching rows for the real database.

## 47. Bitness made explicit, 36px/4px standard, new controls, DXF tools fixed, Profiles v2

### (a) PLC: the log proves the process ran 32-BIT - now it cannot happen silently
The debug output loaded GAC_32\mscorlib: despite AnyCPU sources, the running
process was 32-bit (an x86 solution platform or an older download), and 32-bit
cannot see stations configured by the 64-bit utility -> 0xF0000003. Every
machine csproj now sets PlatformTarget=AnyCPU AND Prefer32Bit=false EXPLICITLY
in Debug and Release, and every Program.cs logs as its FIRST action:
    [PLC DIAG] HMI started as 64-bit process on 64-bit Windows.
After rebuilding, that line must say 64-bit; if it says 32-bit, the wrong
platform is selected in VS Configuration Manager (set it to Any CPU).

### (b) Uniform 36px / 4px standard
DefaultSize height 36 for ButtonHMI, BaseNumericHMI (input + display) and
CheckBoxHMI; LampHMI 36x36; FunctionUnitHMI inner buttons/display capped at 36
(default unit height 64); showcase resized to match. Corner radius default is
now 4px across ButtonHMI, FunctionUnit frame, GroupBoxHMI. Re-drop previously
placed controls to pick up the new defaults.

### (c) New PLC controls: CheckBoxHMI + ComboBoxHMI
CheckBoxHMI: one bit, click writes with security gate + audit, scan reflects,
reverts on denied permission, DescriptionText supported. ComboBoxHMI: options
as "TranslationKey|Fallback|Value" lines; selecting writes the value to a word
address; the scan selects the option matching the PLC value (and never fights
an open dropdown). Both discovered by the scanner automatically.

### (d) DXF tools: emoji toolbar, CCW added, Measure and Route FIXED
Toolbar buttons are now emoji with the old labels as tooltips, plus a CW
rotation: (CW), (CCW), mirror, measure, route. MEASURE root cause: the view
transform was computed at load, BEFORE docking sized the control, so clicks
mapped to wrong drawing points - the viewer now re-fits on every resize.
ROUTE root cause: this profile's border is drawn as many separate segments, not
one closed polyline - GetBorderRoute now CHAINS polylines whose endpoints
coincide and returns the longest loop: 373 points, closed, on the supplied
file (previously empty).

### (e) Profiles v2: filter, multi-sort, editable details
Filter boxes over Company / Serial / Type (contains-match, combinable);
header click sorts, SHIFT+click ADDS sort columns (multi-column). Right side:
a details editor (width, height, melting, welding, temperature, clamping
height) with Save - UPDATEs PostgreSQL when connected, edits in memory
otherwise - above the DXF graph. clamping_mm added to Schema.sql/SampleData.sql.
G-code steps grid (profile_steps) is the next planned panel.

## 48. MULTI-STATION SCANNER (the big one), global bitness properties, Explorer-style nav

### (a) The scanner read ONLY station 1 - now every station scans independently
Root cause of "no driver operating properly, not even the PC simulation": the
scanner was hard-wired to station 1. Controls' PlcNumber was ignored; every
control fed from the single station-1 batch. With station 1 failing (bitness),
NOTHING updated - PLC2 columns dead, Simulation Monitor edits changed driver
memory that no control ever read.
Rewritten: addresses are collected PER STATION (curPlc follows each control's
PlcNumber through the deep extraction), each station gets its own batch, read,
reconnect throttle, stale-data cache and health state, and every control updates
from ITS OWN station's data. One failing station no longer starves the others.
The comm banner shows lost while ANY tag-bearing station is down; alarms still
evaluate only on FRESH station-1 data. Per-station TRANSITION logs (never
spamming a steady state):
    [PLC DIAG] station 1 scan FAILED (not connected).
    [PLC DIAG] station 2 scan OK (11 tags).
Verified: a dead station 1 + live simulated station 2 -> station-2 control
updates, banner shows lost, a monitor write reaches the control next scan, and
bringing station 1 up restores everything (5 assertions), plus the full battery.

### (b) Bitness: GLOBAL csproj properties
The new startup line proved the process still ran 32-bit, while the .sln maps
everything to Any CPU. PlatformTarget=AnyCPU + Prefer32Bit=false now ALSO sit in
an UNCONDITIONED PropertyGroup in every machine csproj, so they apply whatever
configuration/platform VS resolves. After rebuilding, the first audit line must
read "started as 64-bit process"; only then can station 1 connect (0xF0000003 =
32-bit process cannot see 64-bit station definitions). Meanwhile the simulation
stations now run REGARDLESS thanks to (a).

### (c) Navigation: Explorer-style rows
Per feedback, the white "merged" active look is gone: the menu shares the top
bar's colour, the ACTIVE page is a DARKER row (209,214,221), and rows are square
at the panel edge with a SMALL 4px rounding on the right corners only (new
ButtonHMI.RoundRightOnly), like the Windows Explorer sidebar.

## 49. Driver diagnostics, description-position fix, unit-aware Profiles workbench

### (a) Mitsubishi: construction diagnostics + the reference question answered
The driver reference is the STANDARD VS mechanism (COMReference to ActUtlTypeLib
with Embed Interop Types) - the interop travels inside our own DLL, so there is
no separate file for Windows to distrust, and 0xF0000003 is returned BY MX
Component after the COM object loaded and Open() ran - the reference procedure
cannot produce it. To prove each stage, the driver now audit-logs at
construction: interop location, COM creation OK/FAILED, and process bitness.
The remaining fork is unchanged: the startup line must say 64-bit. Two
environment checks worth doing on the PC: run VS and the Communication Setting
Utility at the SAME elevation (station definitions live in per-user registry),
and right-click > Properties > Unblock (or PowerShell Unblock-File) on the
downloaded zip before extracting.

### (b) DescriptionText position hardened
The attached label could render far from its control: the designer assigns the
description BEFORE final Location/parenting, and DPI autoscaling moves controls
after creation. The description now re-anchors on HandleCreated (queued after
the layout pass), VisibleChanged, and on its own AutoSize SizeChanged.

### (c) Unit Settings: staged choices + emoji Save
Selections no longer apply on every click; the floppy-disk button (label in the
tooltip, DXF-toolbar style) applies all staged choices at once.

### (d) Profiles v3 - the database workbench
LEFT: toolbar with add (+) and delete (bin) profile buttons and a "Group by"
selector (Company/Serial/Type) that leads the sort and paints group separator
bands; filter row; multi-sort via SHIFT+click stays. RIGHT is now TABBED
(details and drawing did not fit together): [Details] identity fields, DXF file
box with an explorer Browse (...), and UNIT-AWARE parameters - the database
stores reference units (mm / s / degC) and the editor displays/edits in the
operator's ACTIVE units with the suffix in each caption, converting back on
save, exactly like the PLC numeric controls; emoji Save upserts
(INSERT ... ON CONFLICT UPDATE) or edits in memory without a DB. [Drawing] the
DXF graph with the emoji tools. Delete removes from PostgreSQL when connected.
Full drag-grouping/column-chooser like the old DevExpress HMI is a component-
level feature; this delivers the everyday 90% with stock WinForms - say the
word if a custom tree-grid is wanted next.

## 50. Top bar rework, grouped profile table, DXF recipe editing + G-code, real User/Log settings

### (a) Top bar + title
Window TITLE now carries the identity: "Kaban Makine - FA2010 - V1.0.0.0 -
admin (Admin)" (or "No user logged"); the user label next to Login is retired.
Login is a key-emoji button (blue = sign in, gray = sign out; label in the
tooltip); language is one toggle button cycling EN/TR showing the globe + the
ACTIVE code (Windows renders no country-flag emoji - regional indicators fall
back to plain letters - hence the globe). The top panel shrank 80 -> 48. The
Simulation Monitor is now a pager-emoji button in the right cluster next to
Login, VISIBLE ONLY for Admin (F12 is admin-gated the same way).

### (b) History arrows
Solid dark arrows (U+2B05/U+27A1 in Segoe UI Symbol, bold, near-black) - much
more visible, and they still grey out automatically when disabled.

### (c) Profile table: grouped list + column tools
"Group (n)" menu with CHECK items - tick any combination of Company/Serial/
Type; the tick ORDER is the grouping order; the list renders bold CAPTION rows
("Company: X | Serial: Y") with the member rows underneath. Filters (now
including Profile Code) sit EXACTLY under their columns, follow column
resize/reorder, and can be hidden; RIGHT-click any column header for the
column chooser (show/hide columns + show/hide filters). Columns reorder by
dragging (AllowUserToOrderColumns).

### (d) Details: unit AFTER the value
Parameter captions are clean ("Width :"); the active unit renders as its own
gray label after the numeric box and follows unit changes live.

### (e) Drawing: measure scheme, hover read-out, recipe editing, G-code
While measuring: RIGHT click places the two measure points; LEFT click opens a
copy menu (Copy X / Copy Z / Copy X;Z -> clipboard). The cursor's X/Z shows
top-left while hovering. New edit tools: DELETE ELEMENT (click a line/arc to
remove it) and ADD LINE (two clicks) - for building recipes from a drawing.
GEAR button generates border G-CODE: asks tool radius + cutting depth, follows
the chained border route, and applies the radius through the controller's
cutter compensation (G41 D1 ... G40) - the standard practice, so one program
serves every tool diameter; saves as .nc.

### (f) User Settings - real account management (admin page)
Account list + add / update level / reset password / delete, with guards: the
LAST admin cannot be deleted, nor the account you are logged in with. Accounts
now PERSIST across restarts (hash+salt only, never passwords) under
CommonAppData\KabanHMI.

### (g) Log Settings - designed for an HMI
Master ON/OFF switch, retention in days (old daily files auto-delete), the log
folder shown with one-click OPEN, one-click EXPORT of today's file, and a LIVE
tail viewer of the latest 200 entries - so [PLC DIAG] lines and user actions
are readable ON the machine without hunting files. Options persist and load at
startup.

## 51. PLC ROOT CAUSE FOUND (x86 leftover), DXF workbench, grouped list polish

### (a) THE Mitsubishi cause: a stale x86 line, and my own failed "revert"
Every machine csproj still contained, INSIDE the Debug|AnyCPU and Release|AnyCPU
groups:
    <PlatformTarget>AnyCPU</PlatformTarget>
    <Prefer32Bit>false</Prefer32Bit>
    <PlatformTarget>x86</PlatformTarget>     <-- last value WINS in MSBuild
So every build since change 44 was 32-bit, exactly as each log said, and a
32-bit process can never see logical stations registered in the 64-bit view
(0xF0000003). The change-46 "revert" silently failed: the edit script matched on
a CRLF line ending that Python had already normalised to LF, while its own
verification counted a substring WITHOUT the line ending - so it reported
success while changing nothing.
Fixed properly (CRLF preserved, then re-verified across every csproj and the
sln), and a BUILD GUARD now makes regression impossible:
    <Target Name="KabanAssertNot32Bit" BeforeTargets="Build">
      <Error Condition="'$(PlatformTarget)'=='x86' Or '$(Prefer32Bit)'=='true'" .../>
The build now FAILS loudly rather than shipping a silently-broken exe.

### (b) Navigation
Language is a COMBOBOX again (many languages are planned; a cycle button does
not scale). A collapsed menu now STAYS icon-only when the language changes -
ButtonHMI re-applies its own translation on that event, which used to refill the
captions and clip them over the icons. History arrows are solid triangles
(U+25C0 / U+25B6) and are explicitly greyed when disabled, because FlatStyle
buttons keep drawing their glyph in the normal colour otherwise.
Layout fix found by the tests: the top-bar layout gated on Control.Visible,
which is false until the form is shown - the login button could be placed
overlapping the language selector.

### (c) Profile list
Group captions are drawn as ONE full-width band with no column borders,
starting at x=0 - LEFT of the data columns, which are now indented by a narrow
empty row header, so grouping reads as a heading. Clicking a column header
sorts A-Z then Z-A (the glyph shows which); group columns always lead the sort,
so grouping is never broken by sorting. New RESET button clears grouping,
sorting and filters in one click. Numeric detail values are right-aligned.

### (d) DXF workbench
Selection: hover highlights the element under the cursor, click selects (CTRL
adds), left-drag rubber-bands a REGION and selects everything inside, Del or the
scissors button deletes the whole selection at once. Add-line SNAPS to the
nearest element endpoint, so new lines genuinely JOIN the geometry (magenta
snap marker). Zoom: mouse wheel about the cursor, plus zoom-in/out/fit buttons;
middle-drag pans. UNDO/REDO covers every operation - rotate, mirror, delete,
add-line (Ctrl+Z / Ctrl+Y or the toolbar).
The drawing is referenced to the MACHINE ORIGIN - X0 at the left edge, Z0 at the
bottom - on load and again after every rotate/mirror, with X/Z axes and ticks
drawn behind the profile.
G-CODE now lives WITH the profile: a new G-Code tab holds tool radius, depth,
generate, copy and save; the program is stored in the profiles table (new gcode
column) instead of a loose .nc file, and its tool path is parsed back and drawn
on the profile in GREEN so the cut can be checked visually.

### (e) Styling
Shared UiStyle helper gives plain WinForms buttons the same 4px rounded corners
as the HMI control family (toolbars, dialogs). The alarm page's Acknowledge and
Reset buttons now use the standard 170x36 size with emoji captions.

## 52. HOTFIX: repaired the project files broken by change 51's build guard

Change 51 inserted the 32-bit build guard with a replace-FIRST on "</Project>",
which matched the closing tag of "<Project>{GUID}</Project>" INSIDE the first
ProjectReference instead of the project's own closing tag. That stripped the
GUID metadata from the Kaban.Core reference in all five machine projects, so VS
reported:
    The specified project reference metadata for the reference
    "..\Kaban.Core\Kaban.Core.csproj" is missing or has an invalid value: Project

Fixed: every ProjectReference restored to <Project>{GUID}</Project>, the guard
re-inserted before the LAST </Project> of each file (rfind, not replace-first).

Also added a permanent structural verifier that now runs after every project-file
edit. It parses each csproj as XML and asserts: valid XML, every ProjectReference
carries both its GUID and Name, no x86 PlatformTarget or Prefer32Bit=true
survives anywhere, and the 32-bit build guard is present in every machine
project. This is the check that would have caught both the silent x86 leftover
and this breakage before shipping.

## 53. Mitsubishi: the bitness answer, proven from the working binary

### The evidence
Today's log shows the process finally running 64-bit - and then failing with
0x80040154 REGDB_E_CLASSNOTREG for CLSID {63885648-1785-41A4-82D5-C578D29E4DA8}
(ActUtlType): the class is NOT registered in the 64-bit COM view on that PC.
So the 64-bit push was the WRONG direction. Proof, taken from the customer's own
working build rather than theory - reading the PE/CLR header of
KabanHMI_26_03_09\bin\Debug\KabanHMI.exe:
    machine=I386, PE32, ILONLY, 32BITREQUIRED=True, 32BITPREFERRED=True
i.e. the program that connects runs as a 32-BIT process (AnyCPU with VS's
"Prefer 32-bit" default left CHECKED, since its csproj sets no Prefer32Bit).
MX Component's ActUtlType is registered 32-bit ONLY here, which explains both
observations: 32-bit -> COM object created (failure came later, at Open()),
64-bit -> class not registered at all.

### Changes
1. All machine projects now build as AnyCPU with Prefer32Bit=TRUE - byte-for-byte
   the working program's configuration. The build guard is INVERTED accordingly
   (fails the build if x64 or Prefer32Bit != true), and the project verifier
   enforces the same rule.
2. The interop is now referenced EXACTLY like the working build: the shipped
   Interop.ActUtlTypeLib.dll (EmbedInteropTypes=False, copied to output) instead
   of a build-time COMReference/tlbimp. Verified: the CLSID and typelib GUID
   inside that DLL are identical to ours, and the driver compiles cleanly
   against it, so no MX Component typelib registration is needed to BUILD.
3. CRASH FIXED: the COM object was created in the driver constructor outside any
   try/catch, so REGDB_E_CLASSNOTREG escaped as an UNHANDLED exception and took
   the whole HMI down at startup. Construction and startup registration are now
   guarded - a PC without MX Component starts normally, shows comm-lost for
   station 1 and keeps retrying. Covered by a new test that reproduces exactly
   that environment (no COM registered) and asserts the HMI survives.
4. New MxDiagnostics reports the FACTS instead of guesses, at startup and on the
   first failed connect: process bitness, whether the ActUtlType class is
   registered in the 32-bit and 64-bit views (with the DLL path), the logical
   stations actually stored in the registry (both hives, both views), and a
   plain-language meaning for the common Open() codes - notably
   0xF0000003 = "logical station number not found in THIS process's settings".

### What to check on the next run
The audit log will now contain an "MX Component environment" block. If station 1
still fails with 0xF0000003 while the 32-bit view shows the class registered,
the stations listed in that block tell us where the Communication Setting Utility
saved them - the remaining possibility being that the utility was run under a
different user or elevation than the HMI.

## 54. In-app PLC diagnostics, grid caption fix, visible button borders

### (a) PLC DIAGNOSTICS page (Settings > PLC Diagnostics, admin only)
Since the connection cannot be diagnosed from source alone, the diagnosis now
runs ON the machine:
  - the MX Component environment report (COM registration per bitness with the
    DLL path, and the logical stations actually stored in the registry across
    both hives and both views),
  - TEST one logical station and see the raw return code with its meaning,
  - SCAN logical stations 0..N and list which ones answer. If the station is not
    the expected 1, this finds the right number instead of guessing.
The page reflects over the registered station-1 driver, so it needs no direct
reference to the Mitsubishi assembly and works for any driver.

### (b) Navigation arrows
Enabled arrows are near-black (24,28,34); disabled are a much lighter grey
(206,211,217) - previously the two states were nearly indistinguishable.

### (c) Profile list
The group caption is now painted in RowPrePaint instead of CellPainting. Cell
painting is CLIPPED TO EACH CELL, which is exactly why the caption only appeared
inside the first column and disappeared when another column was clicked; the row
handler owns the whole row, so the band spans the grid, has no column borders and
does not react to cell selection.
The narrow indent column is shown ONLY while grouping is active and disappears
when grouping is cleared. Sorted columns now carry an up/down arrow in the
caption itself. Bug found by the tests while adding those arrows: the group
caption text was being built from the decorated header, so it read
"Company  (arrow): DECEUNINCK" - captions now use the plain column name.

### (d) Button borders and corners
The rounded look did not render because the border was drawn ON the region
boundary: a GraphicsPath Region quantises to whole pixels, so the right and
bottom edges were clipped away - leaving only the top and left lines visible.
Borders are now drawn 1px INSIDE the region at 1.6px width (2.2px for the active
page) with anti-aliasing, so all four edges and the corner radius render. Plain
WinForms buttons (toolbars, dialogs) no longer use FlatAppearance's square
border at all - UiStyle draws the same rounded border itself. The resting
outline colour was darkened to make the shape apparent.

## 55. Real arcs in G-code, synchronised drawing/program, custom window caption

### (a) PLC - the registry probe was guessing; now it searches
The log proved the class IS registered 32-bit (C:\Program Files (x86)\MELSOFT\
ACT\Control\ActUtlType.dll), the process IS 32-bit and the COM object IS created.
"No logical stations found" only meant MY candidate key names were wrong, so the
diagnostic now WALKS the Mitsubishi/MELSOFT vendor roots in both hives and both
registry views and prints every key that looks like station configuration.
The decisive next step is the PLC Diagnostics page's station SCAN: it opens each
logical station number directly and reports which answers, independent of where
the settings are stored.

### (b) Navigation
Raw translation keys after a language change are fixed at the source: ButtonHMI
used this.Text as the fallback for GetString(), and a collapsed menu has an EMPTY
caption - GetString(key, "") returns the KEY. Captions now keep a separate
FallbackText that cannot degrade. Disabled history arrows are much lighter
(226,230,235).

### (c) Title bar
The top strip IS the window caption now: title on the left, then the app controls
and the standard minimise / maximise / close buttons on the right, so the
separate strip no longer costs vertical space. Dragging, double-click maximise
and edge resizing are implemented for the borderless window; set
MainForm.UseCustomCaption = false to return to the standard Windows frame.
Login is a PERSON emoji that pulses gently until someone signs in and stops on
login. The language selector keeps its ComboBox (many languages) and now PAINTS
a small flag per language - Windows does not render country-flag emoji, so a
drawn flag is the only way to actually show one.

### (d) DXF drawing
ONE delete affordance: select (click, or drag a box) then delete - the separate
click-to-delete mode is gone. Region select follows the CAD convention: dragging
left-to-right takes only fully enclosed elements, right-to-left takes everything
the box touches, which is what makes clearing a busy drawing practical.
REVERT button reloads the original DXF; edits only ever live in memory, the
source file is never modified.
Border route now prefers a CLOSED contour over a longer open chain. That is why
added lines could not change it before: the untouched original outline always
won on length. Verified: deleting an edge and bridging it with two added lines
re-routes the border through the new geometry.

### (e) G-code
G02/G03 are emitted for real. DXF arcs are flattened to points when the file is
read, so the route is re-FITTED: runs of points sharing a circle become circular
moves. The fit is deliberately strict - absolute tolerance, a minimum 8 degree
sweep, and no arc larger than the part - because a loose fit turned slightly
bowed straight runs into 330 mm arcs on an 80 mm profile. Your file now yields
53 arcs and 47 straight moves instead of 373 tiny G01 steps.
The program panel sits BESIDE the drawing and the two are synchronised: clicking
the green path selects the program line that cut it, and moving the caret in the
program highlights the matching point on the profile. The green path is drawn at
the real TOOL WIDTH (2 x radius, translucent) with a thin centre line, so the
material actually removed is visible.

## 56. Automatic station scan, company caption, movable panes, sync fixed

### (a) PLC - the code side is now IDENTICAL to the working program
Your registry dump shows the utility HAS one station configured
(SWnD5-ACT\COMMUTL\LogicalStNo_0001, StationNumber=1, CpuType=528 = FX5U) in the
32-bit view where a 32-bit process looks, the control is registered there, and
the COM object is created - yet Open(1) returns 0xF0000003. The working build was
checked again: it opens LOGICAL STATION 1 with the same interop and the same call
order, so nothing in our code differs from it any more.
So the HMI now answers the question empirically WITHOUT operator action: after
the configured station fails, it scans logical stations 0..20 once and logs the
result:
    [PLC DIAG] scanning logical stations 0..20 (configured station 1 failed):
        station N: OPEN OK
      --> USE LOGICAL STATION: N
If a number answers, that is the setting to use. If none answers, MX Component
genuinely exposes no station to this process, and the fix is on the MX side
(recreate the logical station with the utility running at the SAME elevation as
the HMI). The 1 Hz failure spam is also throttled to the first failure plus one
line a minute.

### (b) Caption
The company logo is embedded and shown at the left of the caption and as the
window/taskbar icon; the caption bar is black with white text and window buttons,
and the navigation panel keeps its light colour. Login is a DRAWN person icon
(crisp at any DPI) that pulses at 300 ms until someone signs in, and logging out
now asks first, offering "switch user" as well. The language selector is a button
with a painted flag and the code (TR / EN) - no system dropdown arrow.

### (c) Profile page
The chosen profile now SURVIVES grouping, sorting, filtering and reset. Root
cause: clearing the grid raised SelectionChanged, and SelectedRow's "first row"
fallback overwrote the remembered profile. Grouping, sorting, filters and the
open profile are also saved to CommonAppData and restored next time the page or
the program opens.

### (d) Layout
Tabs are gone. The page is now four resizable panes - table | details | drawing |
program - split by draggable bars, so any area can be widened or squeezed to
nothing as the job needs. Each pane owns its toolbar, so the drawing buttons no
longer spill over the program panel.

### (e) Drawing <-> program synchronisation FIXED
The two were indexed in different spaces - 373 route points versus ~100 program
lines - so clicking the path selected a wrong line and the reverse direction did
not work at all. Both now use ONE map built from the program text: each drawn
point remembers the code line that produced it. Line/character conversion is done
on our own line array too, because TextBox.GetFirstCharIndexFromLine and
GetLineFromCharIndex disagree by one about terminators. The path is also easier
to hit (tolerance widened to the tool band).
Depth: verified reaching the program (Z-5.000 vs Z-12.500); changing tool radius
or depth now REGENERATES the program automatically instead of leaving a stale one.
Added lines and the border: rather than rely only on chaining heuristics, select
the elements that should be cut and press the tick - that fixes them as the
border route explicitly, and the program follows exactly those.

## 57. PLC LIKELY SOLVED: the control must be recreated after a failed Open

### (a) What the automatic scan proved
    station 1: OPEN OK          <- a FRESH control opens station 1 without trouble
    stations 0, 2..20: 0xF0000002 (no such logical station)
    --> USE LOGICAL STATION: 1
So station 1 was right all along, and MX Component is configured correctly. The
difference was the CONTROL INSTANCE: our driver created one ActUtlType at startup
and reused it for every retry. Once its first Open() failed, that control kept
returning 0xF0000003 for ever - 1097 times in your log - while the scan's
throwaway control opened the very same station a second later.
Fix: after any failed Open, the driver releases the control and creates a NEW one
before the next attempt (Close + ReleaseComObject + new ActUtlType). The retry
that follows therefore behaves exactly like the scan that succeeded.
Error meanings were corrected from this evidence too: 0xF0000002 = no such
logical station; 0xF0000003 = control unusable after an earlier failed Open.

### (b) Profile page: free-floating panes with named arrangements
The four areas - Profiles, Details, Drawing, G-Code - are now independent panes on
a canvas (new FloatingPaneHMI): drag by the caption, resize from any edge,
minimise to the caption bar, and click to bring to the front. They may overlap
and share the same space.
Arrangements are saved BY NAME from the toolbar: set the panes up for a job,
press save, give it a name, and pick it from the Layout combo next time that job
comes round. The chosen arrangement, and the pane geometry, survive page
navigation and program restart; the reset button puts everything back to the
default tiling.

### (c) Filter row
The filters moved INSIDE the column-header band, directly under the captions: the
header is made tall enough to hold them and the boxes are children of the grid, so
they track column resize/reorder and never cover a data row. Hiding them shrinks
the band again.

### (d) Caption controls
Login and logout icons were the wrong way round - the arrow now travels TOWARDS
the figure to sign in and AWAY to sign out. Logging out opens a compact chooser
with three icon buttons only (log out / change user / cancel), tooltips instead of
labels, no message text. Flags are 50% larger and drawn to scale, and the language
button carries a small caret so it still reads as a chooser.

### (e) Navigation arrows
The disabled arrow really is pale now: WinForms paints disabled button TEXT with
its own grey-out, which ignored our colour, so the triangle is drawn directly.
Verified by rendering the button and measuring the drawn pixels - dark 24/255 when
usable, pale 196/255 when not.

### (f) Drawing fixes
- Element selection was being swallowed by the tool path: the green band covers
  the whole border, so every click landed on the path instead of the geometry and
  the delete button stayed disabled. Geometry now takes priority; the path is
  picked only where no element sits under the cursor. Delete also stays enabled
  and explains what to select.
- Editing the drawing (adding a line, deleting elements) now REBUILDS the program
  immediately, as does changing tool radius or depth - previously the program kept
  its old contents, which made both look like they did nothing. The status line
  reports radius, depth, arc and move counts after every rebuild.

## 58. Logout popup crash fixed, element-aware drawing, layout arrangements

### (a) Crash on logout - FIXED
    System.ArgumentException at Graphics.DrawRectangle ... ShowLogoutChoice
The border Pen was created in a `using` block and then used by the Paint handler,
which runs later - by then the pen was disposed. The pen is now created inside the
handler. The chooser also opens ANCHORED under the login button like a dropdown
panel (right-aligned, kept on screen) and closes when focus moves away.

### (b) Flags
Rendered at 4x and scaled down with bicubic filtering, with a proper crescent
(white disc minus a red disc) and a real five-pointed star - the previous
low-resolution circles looked lumpy at 30x21.

### (c) Panes
Dragging past the page edge no longer flickers: the pane is clamped as a WHOLE
rectangle before being moved, so a drag simply stops at the border instead of
jumping out and being pulled back. Resizing respects the same limits.
Pane toolbars are now ToolStrips with the standard ">>" OVERFLOW button, so
squeezing a pane moves buttons into a menu instead of hiding them. "Tool radius"
and "Depth" are each a single hosted control containing the caption AND its input,
so they can never be separated.

### (d) Layout arrangements
"Default" is the first, built-in entry in the chooser and is never overwritten;
the reset button is gone because selecting Default does the same thing. SAVE now
updates the arrangement currently selected, and SAVE AS creates a new named one.
An arrangement stores the pane geometry AND the table's state: grouping, the full
sort list, column visibility, width and order, and the filter row.

### (e) Sorting priority
Clicking a column header no longer discards the previous sort: the clicked column
becomes the HIGHEST priority and earlier ones slide down to second, third and so
on (up to four). Clicking the leading column flips its direction.

### (f) Drawing: elements now carry meaning
Each element knows whether it came from the FILE or was ADDED by hand, whether it
is deleted, whether it takes part in the cut, and optionally its OWN cutting depth.
- Manually added lines are treated exactly like file geometry when the border is
  chained, so an edited contour drives the G-code (verified: deleting an edge and
  bridging it with two drawn lines re-routes the program through them).
- DELETE is now SOFT: the element stays in the drawing, drawn very dimly, so it is
  obvious what was removed and it can be restored.
- Colour coding: added = bright cyan and heavier, file = white, deleted = dim grey.
- PER-ELEMENT DEPTH: the program steps Z when the element under the tool asks for
  a different depth, so parts of a contour can be cut deeper or shallower, and an
  element can be left out of the cut entirely without deleting it.
- The green path is now drawn OFFSET by the tool radius (the cutter edge, not its
  centre line), which is what makes the tool visibly step into the border.
Column widths are explicit rather than Fill, so a width the operator sets - and a
saved arrangement - is restored exactly.

## 59. HOTFIX: NotSupportedException on the Profiles page (ToolStrip.Controls)

    System.NotSupportedException: Collection is read only.
    at ProfilesScreen.BuildRight() line 591

When the pane toolbars were converted to ToolStrips in change 58, one call was
left behind:
    bar.Controls.Add(Tool("...border route...", ...));      // two lines long
The edit that converted the others used a single-line pattern, so this two-line
call did not match. ToolStrip.Controls is READ-ONLY on .NET Framework, so the
Profiles page threw as soon as it was built. Fixed - the button is added as a
ToolStrip item like the rest.

Why the tests did not catch it: MONO permits ToolStrip.Controls.Add, so the page
constructed happily in the headless battery while failing on Windows. Two checks
now close that gap:
  - a source sweep (verify_winforms.py) that finds any add into a collection that
    is read-only on .NET Framework - ToolStrip, SplitContainer, ToolStripDropDown -
    and is run after every UI edit;
  - a runtime assertion in the pane test: a control hosted by a ToolStripControlHost
    legitimately appears in ToolStrip.Controls, so the tell-tale of a direct add is
    Controls outnumbering Items.

## 60. Logout dropdown, layout state that really persists, working area selection

### (a) Logout chooser
Rebuilt as a ToolStripDropDown anchored under the login button, so it CLOSES BY
ITSELF as soon as anything else on screen is clicked - a modal dialog could not do
that, because clicks on the owner window are blocked rather than delivered.
Two large drawn choices (a door with an outgoing arrow for log out; two figures
with swap arrows for change user) and a small dismiss cross in the corner. The
icons are drawn with GDI+ rather than copied from the linked stock art, so nothing
licensed is redistributed and they stay crisp at any DPI.

### (b) Layout persistence - the real gap
Named arrangements did save and restore correctly, but that is not how the page is
normally used: with "Default" selected nothing was ever written, because Default is
deliberately never overwritten. The live WORKING STATE is now saved automatically
(on navigating away and on close) and restored on open - panes, grouping, the full
sort list, column widths, visibility and order, and the filter row - with or
without a named arrangement. Internal entries are hidden from the chooser.
Verified by reopening the page twice: once with a named arrangement, once with
nothing named at all.

### (c) Area selection could never survive
Dragging a box did select the elements - and then WinForms raised Click straight
after MouseUp, whose handler cleared the selection again. That is why multi-select
and the delete button appeared dead. A drag now suppresses the click that follows.
ESC releases everything (selection, a half-drawn line, measure points), and the
rubber band also works while measuring.

### (d) Depth really works now
The guard only rebuilt the program when one had been generated in the same session
(_program != null). A profile whose G-code came from the DATABASE therefore ignored
every depth or radius change - exactly the case that was tried. Any change now
rebuilds whenever a drawing is loaded. Covered by a test that loads a stored
program and asserts the depth reaches the code.

## 61. Depth means cut-into-profile, popup usable at once, Default resets the table

### (a) Logout popup
The ToolStripDropDown never took activation, so the first click was spent
activating the window and only the second reached a button. It is a MODELESS FORM
now, activated on show, so the buttons work on the first click - and Deactivate
still closes it the moment anything else is clicked. The dismiss cross is larger
(26x22) and bold.

### (b) "Use the selected elements as the border route" only recoloured
It fixed the border and redrew it red, but never rebuilt the program, so the
G-code still followed the old contour. It now regenerates immediately, which is
what makes a selection containing MANUALLY ADDED lines actually drive the cut.

### (c) DEPTH now means what you meant by it
Your screenshot showed Z-2.000 following the parameter correctly, while the cut
stayed a full tool diameter wide - because the drawn path was offset by the TOOL
RADIUS and nothing depended on depth. Depth is now the CUT INTO THE PROFILE:
  - the programmed contour is offset inward by the depth (per element where an
    element has its own), and G41 then applies the radius, so the cutter's outer
    edge lands exactly that far inside the border;
  - the green band is drawn from the same geometry, so what you see is what is
    removed - 1 mm depth bites 1 mm, 6 mm bites 6 mm, regardless of tool size;
  - Z PLUNGE is now its own field, since the vertical depth and the bite into the
    profile are different things and one field could not mean both.
While proving this out, the offset itself turned out to be wrong: averaging the
two edge normals at a corner falls short by cos(half-angle), so a 90 degree corner
only reached 0.71 of the requested depth. It uses proper MITER joins now (with a
spike limit on sharp corners) and hits the requested depth exactly.
Per-element depth therefore shows up as geometry: an element set deeper bites
further into the profile, and the rest keep the global value.

### (d) "Default" arrangement
Choosing Default now restores the TABLE as well - grouping cleared, sorting
cleared, filters cleared and shown, columns back to their original visibility,
width and order - instead of only moving the panes and leaving the table as the
operator had rearranged it.

## 62. Logout is a dropdown menu; your supplied icons are in

### (a) Logout chooser = a menu, exactly like the language selector
No popup window and no cancel button any more. The login button drops a
ContextMenuStrip with two entries - Logout and Change user - which closes by
itself when anything else is clicked, so a cancel entry would only add clutter.
It is compact, works on the first click (a menu takes activation properly, which
the earlier dropdown did not), and matches the language chooser's behaviour.

### (b) The icons from User_Login_Button_Emojis.xlsx
The four images were matched to their labels by cell anchor and embedded in the
assembly, trimmed of transparent margin and scaled:
  Login  (A1)                   -> caption button while signed OUT
  Logout (main page)            -> caption button while signed IN
  Logout (combobox selection)   -> menu entry
  User Change (combobox)        -> menu entry
The hand-drawn glyphs remain only as a fallback if a resource is ever missing.

### (c) Build-script gap found while testing this
The icon assertions failed at first for a real reason: the headless build embedded
only CommonTranslations.csv, so resources declared in the csproj were invisible to
the tests even though MSBuild would include them. The script now reads EVERY
EmbeddedResource out of Kaban.UI.csproj, honouring an explicit LogicalName and
otherwise deriving MSBuild's default - which is how CommonTranslations.csv itself
is declared, so the first version of that change silently broke the translation
fallback test until the default form was handled too.

## 63. White caption icons, login on startup, close confirmation, caption double-click

### (a) Caption icons
The supplied artwork is a BLACK glyph on transparency, which all but disappears on
the black caption bar. It is now recoloured to WHITE (keeping the anti-aliased
alpha) and pre-scaled once at twice the drawn size, so the login/logout icon is as
crisp as the painted language flag instead of being resampled on every repaint.

### (b) Login offered at startup
The login dialog opens by itself once the HMI is up, since a shift normally starts
by signing in. It is an ordinary dialog, so anyone who only wants to watch the
machine can close it and continue unauthenticated.

### (c) Closing asks first
A machine screen shut by a mis-tap is disruptive, so the close button now asks -
a compact confirmation in the HMI's own style (icon buttons with tooltips). The
question is deliberately SKIPPED when Windows itself is shutting down or Task
Manager ends the process, so it can never block a system shutdown. The existing
close handler (which drops the PLC connection) was folded into this rather than
duplicated - and, as it happens, that handler had never been wired to the event at
all, so the PLC was not being disconnected cleanly before.

### (d) Caption double-click maximises
It was implemented earlier but could never fire: the drag handler hands the mouse
straight to Windows (WM_NCLBUTTONDOWN), which consumes the second click, so the
DoubleClick event never arrived. The second click is now detected in the mouse-down
handler itself (e.Clicks >= 2) before the drag begins.

## 64. Caption buttons on black, close confirmation as a corner dropdown

### (a) The white plate under the login icon
The login button still carried a coloured/white background, so a WHITE glyph on it
was invisible. It is black now, like the caption itself, with no border - the icon
simply sits on the bar. The "please sign in" pulse tints the ICON (white <-> light
blue) instead of flashing a coloured plate, which is what made it look like a stray
white button.

### (b) Simulation monitor button
Was a pager emoji on a cream plate, which fitted nothing. It is now a DRAWN white
monitor glyph (screen, stand and a small live trace) on black, so the admin button
matches the login and language controls on the same line.

### (c) Close confirmation is a dropdown
Same idea as the logout menu: the close button drops a single entry - "Close the
program", with a drawn power symbol - and clicking anywhere else cancels, so no
cancel entry is needed. Alt+F4 and the system menu are routed through it too: the
close is cancelled and the same dropdown is offered, so every route needs the same
second, deliberate click. A Windows shutdown is still never blocked.
Both this and the logout menu exist for one reason - an accidental tap must not end
a shift or close the machine screen.

## 65. The white square explained, and the HMI starts full screen

### (a) Why the login button stayed white
Not the button - the ARTWORK. The icons in the workbook are fully OPAQUE: a black
glyph on a solid WHITE field, with no transparency at all. Embedded as-is they
painted a white tile over the black caption, and recolouring them to white made
the whole tile white, hiding the glyph completely.
The white field is now keyed out into the alpha channel when the icons are
embedded (white -> transparent, black -> solid, so anti-aliased edges survive),
and the glyph is re-tinted white for the caption. On the menu entries the same
icons keep their black glyph on transparency, which is right on a light menu.
Tests now measure this directly: the caption artwork must be white AND mostly
transparent - a filled tile fails.

### (b) Starts maximised
The window opens maximised, since the HMI is normally run full screen. The caption
double-click and the maximise button still restore and re-maximise it.

## 66. Tooltip layout, adjustable navigation, parameter columns, table reset

### (a) "Waiting for PLC data" on every menu button
Navigation buttons have no PLC binding at all, so that default reason was pure
noise. It is now only used when the control really is bound to the PLC (a relay or
output address, a visibility/enable condition, an enable trigger, or bit/word
actions - with designer defaults like "None" treated as unbound).

### (b) Tooltip structure
    <label>              only when the screen does NOT already show the caption
    <operator help>      what it does, or why it is currently blocked
                         (blank line)
    ----- For Admin -----
    <wiring: addresses, conditions, security level, actions>
Lamp tooltips follow the same shape. The blank line before the admin caption is
part of the contract and is asserted by the tests.

### (c) Navigation width
A splitter on the menu's right edge: drag it to whatever width suits, and it STAYS
there - captions intact, not the icon-only collapse - and is restored next run.
The collapse toggle still works independently.

### (d) Parameters are proper table columns
Width, Height, Melting Time, Welding Time, Temperature and Clamping Height are now
columns of the profile table: hidden by default, switched on from the header menu,
and then filterable, groupable and sortable like any other column. Values follow
the operator's active units. The DXF path and the G-code are deliberately NOT
offered - they are documents, useless to filter or group by.

### (e) Reset button on the table pane
Next to the minimise button in the pane's title bar: clears grouping, sorting and
filters, and puts the columns back to their default set, visibility, width and
order. Everything else keeps its state until that button is pressed.

### (f) Pane drag flicker
The drag set the position and THEN pulled it back inside - two moves per mouse
event, which flickered. The target is clamped BEFORE it is applied now, so a drag
simply stops at the border.

### (g) Login menu
Now headed by the signed-in user with the person icon, above a separator, so the
dropdown reads as a user menu rather than two bare entries.

## 67. REAL machine alarms, six languages, machine-error indicator

### (a) Alarms generated for all three machines
Alarms.json is now generated from YOUR data instead of examples:
  MachineAlarmList.xlsx  - the PLC-side truth: which M bit exists + Turkish name
  <machine>.json         - the same bits IN ORDER, with English names
  Translation_JSON.csv   - EN / TR / RU / DE / IT / ES
The mapping is bit k of the error words = M(801 + k); it was verified against both
sources before generating (~95% agreement, the rest being entries one source lists
and the other leaves blank - all of those are still included).
    FA2010  158 alarms      SB3012  115 alarms      SB3015  91 alarms
Every entry carries its address, a readable TagCode, a severity, and up to six
translations. Turkish comes from the workbook verbatim; English uses the
translation file's corrected wording where it exists (it fixes "Lenght", "Rigth"
and double spaces in the raw bit names).
M800 is included on every machine as the machine-error summary bit.
SB3012 and SB3015 never loaded alarms or machine translations at all - both now do,
and all three ship Alarms.json + Translations.csv next to the exe.

### (b) Six languages, with an English fallback
The CSVs carry EN;TR;RU;DE;IT;ES. Where a language has no text for a key,
LanguageManager now falls back to ENGLISH rather than showing a blank or a raw key,
so adding a language never breaks a screen. ALARM_TRANSLATIONS_TODO.md lists every
alarm still needing RU/DE/IT/ES.

### (c) Machine error on the caption and the menu
While PLC 1's M800 is set:
  - the Alarms entry in the navigation pulses with a RED border (new
    ButtonHMI.AlertBlink; all pulsing buttons share one timer),
  - a pulsing red RESET button appears in the caption, left of the user control.
It is MOMENTARY, like a physical reset: it writes M102 = 1 and releases it 300 ms
later, and the press is audited. Both are driven from FRESH station-1 data only.

### (d) Flags
The supplied flags (UK, Turkey, Russia, Spain, France) are embedded and used by the
language selector. Germany and Italy were missing; rather than take artwork from
the web, they are generated exactly as their tricolours. France is embedded ready
for when French translations are added. The painted fallback remains for a language
with no image.

## 68. PERFORMANCE FIX + M800 is not an alarm

### (a) Why the HMI became unresponsive - and the real cause
Two things combined, and the alarm import exposed them:
  1. MitsubishiActUtl.TryReadMulti made ONE COM CALL PER ADDRESS. With the real
     alarm list the station-1 batch grew from ~11 devices to ~170, and at the
     ~15 ms round trip this PLC reports that is over TWO SECONDS of PLC traffic
     per scan.
  2. That work ran on a System.Windows.Forms.Timer - i.e. ON THE UI THREAD - so
     those seconds were taken directly out of the interface, every tick.
Fixes:
  - The driver now reads the whole batch with ReadDeviceRandom in a SINGLE COM
    call (falling back to the per-address loop only if that call is rejected).
  - The scan tick no longer talks to the PLC at all: reads and reconnect attempts
    run on a worker thread and only the results are applied on the UI thread. A
    tick is skipped while the previous scan is still in flight, so a slow or dead
    PLC can never queue up work or freeze the screen.
Covered by a new test that gives every COM call a realistic 15 ms cost: the UI
tick returns in under 50 ms, the batch is read in ONE call (zero per-address
reads), and overlapping ticks are skipped.

### (b) M800 removed from the alarm lists
M800 is the machine-error SUMMARY bit - it tells the HMI that an alarm exists,
which is what drives the pulsing Alarms entry and the reset button. It is not a
fault in its own right, so it no longer appears in the alarm list. The scanner
still reads it separately for the indicator.
    FA2010 157 alarms      SB3012 114 alarms      SB3015 90 alarms

## 69. Manual movement and parameter pages GENERATED from the device list

### (a) Reading the workbook
FA2010_DeviceList.xls is a real legacy BIFF file (code page 1254). Its sheets are
already a specification, so the pages are GENERATED from it rather than typed:
  ManualMovement  group | field | back sensor/button/mode | forward button/mode/
                  sensor | position device, length, divisor, sample display
  Parameters      section headings, then English + Turkish name, device and type
  (Alarms, Pop-ups, PLC_Labels, AxisParam and Profiles&Operations are also in the
   file and can be wired the same way when wanted.)
Typing 122 movement rows and 78 parameters by hand would invite exactly the kind
of address slip that is expensive to find on a machine, so tools/generate_pages.py
does it and can be re-run whenever the workbook changes. The workbook is kept in
tools/ next to the generator.

### (b) What was generated
9 manual-movement pages (122 FunctionUnitHMI) and 8 parameter pages
(78 NumericInputHMI):
    Entry Conveyor 13   Robot 7    Profile Recognition 11   Milling 29
    Cutting 20          Exit 10    Clamping Moulds 17       YZ Cross Rollers 8
    Other 7
Each function unit takes button A from the "Geri" column and button B from
"Ileri", with each button's MOMENTARY/ALTERNATE mode honoured (13 rows are
alternate, so a blanket "momentary" would have been wrong), the sensors as its
lamps, and the position device with its 16/32-bit type and divisor - e.g. Robot
Axis X: buttons M605/M604, back sensor M330, position W0 as 32-bit / 1000.
Parameter inputs are maintenance-gated and audited.
Both families are reached from ONE nav entry each, with the groups as tabs
(TabbedHubScreen), built LAZILY on first selection so pages the operator never
opens cost nothing in the scan batch.
Verified by a test that instantiates every generated page and checks the counts,
the security levels, and Axis X against the workbook row.

### (c) Alarm reset button
Was a thin outlined glyph - easy to miss on a machine that has just faulted. It is
now a filled, rounded, pulsing RED button with the reset icon AND a caption.

## 70. Designer-editable generated pages, plus the batch of UI fixes

### (a) The generated pages are now DESIGNER files
Each page is a normal WinForms partial class - Foo.cs with the constructor and
Foo.Designer.cs with the fields and InitializeComponent - registered with
SubType/DependentUpon, so it opens on the Visual Studio design surface with every
control selectable and every property editable in the grid. The generator still
produces them from the workbook, so a re-run overwrites: make lasting changes in
the workbook, or drop a page from the generator to keep it by hand.
A new check (verify_designer.py) asserts what the designer needs and what a
careless edit can silently break: the partial split, InitializeComponent, a FIELD
for every control, every control added to the page, and the csproj metadata.

### (b) Sizes and captions match the toolbox
Units are 360x64 and inputs 115x18 with AutoSize - the proportions of a
hand-placed control - laid out in two columns. FunctionUnitHMI now DEFAULTS to
the arrow captions used on the Home screen, and a new Arrows property switches
the pair to up/down, down/up or open/close in one step instead of typing glyphs.

### (c) Empty admin tooltips
The "----- For Admin -----" caption was written before the wiring lines, so a
control with nothing to report still showed the header. The admin lines are now
collected FIRST and the whole block is dropped when they are empty. Security
Level is omitted when it is None, since that is not a restriction.

### (d) Unit settings showed the wrong unit
The combo was populated with DataSource and its SelectedIndex set BEFORE it was
added to the page; adding it installs a CurrencyManager, which resets the position
to 0 - so the page always showed the first unit (mm) whatever the machine was set
to. It now uses Items and selects AFTER being added.

### (e) Profile page opening with everything minimised
FloatingPaneHMI derived Minimized from Content.Visible, and a control reports
Visible = false whenever any ancestor is hidden. Navigating away therefore made
every pane look minimised - and the working state was SAVED that way. Minimised is
an explicit field now, covered by a test that hides the page and asserts the saved
layout is unchanged.

### (f) Smaller things
- PositionUnitType was in category "    Position" (four spaces) instead of
  " Position", which is why the designer showed it as a separate group.
- NumericInputHMI.TranslationKey is retired: DescriptionText is ITSELF the key and
  falls back to its own text, so a second property could only drift out of step.
  The old property still loads existing designer files.
- Descriptions get " :" appended automatically (DescriptionColon to switch off).
- Manual Movement and Machine Parameters use the SAME horizontal tab strip as the
  Settings page - one tab implementation across the program - and have their own
  nav icons (a joystick and sliders) instead of repeating the settings cog.
- The blank position displays were not a bug: those rows have no position device
  in the workbook, and the unit correctly hides the display when unbound.

## 71. Toolbox-identical sizing, correct divisors, unit defaults

### (a) Why the generated units came out bigger
Not the size in code - both say 360x64 - but DOUBLE FONT SCALING. The generated
pages had AutoScaleMode.Font and are hosted INSIDE another auto-scaling container
(the tab host), so WinForms scaled their contents twice: about half again as wide,
and taller, which is also where the extra space under the buttons came from.
Generated pages now use AutoScaleMode.None; they are laid out in absolute pixels,
so there is nothing for scaling to do. A unit is now pixel-identical to one
dragged from the toolbox, and the tests assert 360x64 on all 122 of them.

### (b) Divisors were 10x (or 100x) out
The workbook's "Bolen" column does not include the decimal places - those are in
the "Gosterim" sample. The control divides the raw value ONCE, so its divisor has
to absorb both:
    control divisor = workbook divisor x 10^(decimals in the sample)
Robot Axis X: workbook 1000 with a 1234.5 sample -> 10000, matching a hand-placed
control. Decimals now come from the sample too, not guessed from the divisor.

### (c) Blank values until the units were touched
UnitManager.GetActive returned a NAMELESS placeholder for any family that had not
been chosen yet. An unnamed unit has no suffix and cannot be matched in the
settings combo, which is why picking a unit by hand made everything start working.
It now falls back to the first unit DEFINED for that family and remembers it, and
every machine calls UnitManager.EnsureDefaults() at startup so nothing depends on
a settings page having been opened.

## 72. Only the first tab updated, tab styling, disabled buttons, lamps, layout

### (a) Why only the FIRST tab showed values
The scanner deliberately scans only the controls it can currently SEE, and it
rebuilds that list when a PAGE is loaded. A tab change swaps the content in place,
which is not a page load - so a second tab's controls were never in the scan
batch and simply never updated. Screens that swap content now call
PlcScanner.NotifyContentChanged(), and the active scanner re-reads its tags.
Covered by a test with two tabs on different addresses: the second tab was blank
before this change and now shows its value.

### (b) Tabs
The active tab is BOLD (and re-measured, so the wider caption still fits), and the
duplicate heading inside every generated page is gone - the tab already names the
page, and repeating it only cost vertical space.

### (c) Disabled buttons inside FunctionUnitHMI
They were painted with SystemColors.ControlDark - DARKER than an available button,
so an unusable button stood out more than a usable one. A disabled button now uses
a pale fill (ColorDisabled), a washed-out caption (ForeDisabled) and a faint
border. Both are properties, so a screen can override them.

### (d) Indicator rows are LAMPS
Rows in the workbook with a sensor but no button and no position device are
conditions to watch, not things to operate. They are generated as LampHMI with
their label instead of a two-button unit: 122 units became 111 units + 11 lamps.

### (e) Profile page opening minimised - the actual cause
The shell builds every screen up front, so the Profiles page received a
VisibleChanged (and therefore a SAVE) with default pane bounds LONG before its
layout was ever restored - wiping the stored arrangement. Saving is now blocked
until the restore has run, and a pane refuses to restore to a height too small to
use. A test builds a page without ever showing it and asserts the stored
arrangement is untouched.

## 73. Shared navigation with sub-machine TABS, control polish, parameter scaling

### (a) One left bar for every machine
Screens now declare a ROLE (Home / Manual / Alarms / Settings), and screens sharing
a role become TABS of a single navigation entry instead of a button each. So the
left bar reads the same on every machine - Home, Manual Movement, Alarms, Settings -
and a mounted sub-machine appears as a TAB inside those pages rather than another
button. A page with no role (FA2010's Profiles) keeps its own button, which is how
a machine-specific screen stays in its own project.
With no sub-machines there is NO tab strip at all - a single tab would be noise.
Sub-machine tabs are named by the project (Screwing, Holing, Connection Robot)
through IMachineDisplayName, not "Sub Machine 1".
Settings is split the same way: HMI Settings (units, users, logs, diagnostics,
about - identical everywhere) and Machine Settings, with a machine's own pages.
Verified by a test that mounts a machine alone and then with two sub-machines: one
Home entry either way, tabs only when there is more than one machine, main first.

### (b) Parameter scaling
The workbook does not say how a parameter is scaled, so it follows from what it IS:
servo/linear positions divide by 10000 ("mm"), cylinder and clamping positions by
100 ("mm"), and times by 10 with the suffix "sec". Previously everything used 100,
which showed a 100 mm position as 10000 mm.

### (c) Bilingual tab captions
The workbook writes both languages in one cell ("Entry Conveyor (Giris Konveyoru)"),
which is why tabs showed English AND Turkish at once. The generator now splits them
and writes a Tab_<Page> row per page into the machine's Translations.csv, so the
strip shows only the selected language.

### (d) Controls
- Rounded corners on the numeric family, LampHMI, and the singled-out/empty cells
  inside FunctionUnitHMI, so nothing square sits beside a rounded neighbour.
- Lamps and checkboxes put their caption on the RIGHT (DescriptionSide), so the
  control itself lines up with the left edge of everything else on the page.
- Descriptions get padding and are clamped inside the page, so a long caption is no
  longer clipped at its start.
- Home page controls use the toolbox sizes (units 360x64, buttons and numerics
  170x36, lamps 36x36).

## 74. Settings combined, sub-machine tabs everywhere, column layout

NOTE: the build container was reset during this change set; the toolchain and the
headless build script were rebuilt from scratch and every project compiles again.

### (a) Settings is one entry: HMI | Machinery
Machine parameters are no longer a separate branch. Settings now has two tabs -
HMI (units, users, logs, diagnostics, about) and MACHINERY, which itself has a tab
per mounted machine (Main, Screwing, Holing...), built from the registry so a
sub-machine appears automatically. Machinery is maintenance-gated, so an operator
does not see it at all.

### (b) Home and Alarms get sub-machine tabs
Sub-machine modules now provide Home and Alarms screens as well as Manual, so all
three pages show a tab per machine.
On "one table or a tab per machine" for alarms: a TAB PER MACHINE is right for
daily work, because acknowledging and resetting are per-machine actions and mixing
machines in one grid makes it easy to reset the wrong one. Each tab has its own
reset (momentary M102 on that machine's station) and its own list; a combined
history view with a Machine column is the better place to trace a fault ACROSS
machines, and is the natural next step.
Alarm grid columns are explicit, resizable, and their widths are SAVED - a Fill
grid recomputes widths from weights, so an operator's width could never survive a
restart.

### (c) Tab and navigation styling
Nested tab strips sit tight against each other (the inner host no longer adds its
own padding, which was the gap). Active tabs on a PARENT level are shaded like the
left navigation - they are navigation - and only the deepest level stays white so
it merges into its page. The active left-navigation row is BOLD, the same signal
the tabs use.

### (d) Control layout on generated pages
Controls now fill the LEFT column top to bottom and continue at the TOP of the next
column, instead of snaking left/right. Columns are 60px apart and a parameter's
caption gets 250px, so long descriptions fit. A left caption stays RIGHT-ALIGNED
against its box even when it must be clipped - it loses its start, not its end -
and the gap between ":" and the box is halved. The automatic ":" is added only to
LEFT captions; a right-hand caption (lamps, checkboxes) already follows its
control. How many rows fit a column comes from the target page height
(tools/generate_pages.py --page-height=), and HmiLayout detects the running screen
for anything that needs it at runtime.

### (e) Alarm blink
Twice as fast (220ms): the old rate read as sluggish for something that should
catch the eye.

## 75. Build failure fixed, English page names, AI prompt builder

### (a) The build failure - root cause was MINE
The reported "Invalid Resx file ... DynamicValue cannot be located" and the
CS0006 in Kaban.Drivers.MitsubishiActUtl were both CASCADES: Kaban.Core itself
failed to build, so nothing downstream could resolve it. The cause was
HmiLayout.cs (added last change set) doing "using System.Windows.Forms" to read
the screen size - but Kaban.Core is the UI-FREE layer and does not reference
WinForms. My headless build passed that reference explicitly, which masked it.
Fixed properly: Kaban.Core no longer references any UI type. The shell measures
the screen and calls HmiLayout.DetectFrom(); an operator override still wins.
Kaban.Core is now compiled WITHOUT WinForms in the checks, so this cannot recur.

Two follow-ups on the resx itself, since it was fragile anyway:
  - the DynamicValue entries (all of them the default "None") are removed from
    every .resx, and the alias to Kaban.Core with them - a resx that names a type
    from another project fails whenever that project has not built yet;
  - DynamicValueConverter now supports InstanceDescriptor, so the designer writes
    such values as a CONSTRUCTOR CALL in InitializeComponent instead of putting
    them in the .resx at all.
Also removed a leftover debug logger in that converter which appended to
C:\Kaban_Debug.txt on the customer's machine every time the designer touched a
property.

### (b) English-only page names
The workbook writes both languages in one heading, so classes came out as
ParamDistances_Beetwen_Units_Uniteler_Arasi_Mesafeler. Names now use the ENGLISH
half only; the Turkish half still goes to the translation file, so the tab reads
Turkish when Turkish is selected. Headings that exist ONLY in Turkish are mapped
("Kesim Boy Ofseti" -> Cut Length Offset), so no class carries Turkish text.

### (c) AI Prompt builder (HMI Settings > AI Prompt, admin)
Builds a ready-to-paste prompt for generating or upgrading a page's Designer file.
It states what an assistant cannot guess: the two-file partial-class layout and the
designer's rules (a field per control, Controls.Add, AutoScaleMode.None, nothing
from another project in the .resx), the csproj entries with SubType/DependentUpon,
the Kaban control set with the properties that carry PLC addresses, the scaling
convention (10000 servo / 100 cylinder / 10 time), the column-major layout with
this machine's real page height, and the translation file format. It asks the AI to
FLAG assumptions rather than invent addresses. Choosing "Import a page from another
project" adds guidance to map foreign controls onto ours and to report anything
with no equivalent instead of dropping it.

## 76. One Settings button, outlined navigation, wider pages, manual

### (a) Settings and Machine Parameters are ONE entry
The earlier attempt at this was lost when the build container reset, so Settings
still carried the old flat list - which is also why the AI Prompt tab never
appeared. Now: a single SETTINGS button with two tabs inside it -
  HMI Settings         units, users, logs, PLC diagnostics, AI Prompt, Manual, About
  Machine Parameters   a tab per mounted machine (Main, Screwing, ...), and under
                       Main the eight generated parameter pages
- the same shape as Home and Alarms. The separate Machine Parameters nav button is
gone.

### (b) Navigation and tab styling
The active left-navigation row and the shaded (non-final) tab levels are now
OUTLINED. A flat block of colour read as a smudge; an outlined cell reads as a
button. Only the deepest tab level stays white and borderless, merging into its page.

### (c) Reset icon
Redrawn as the standard reset symbol - a near-closed circular arrow with a solid
head - instead of the bell-and-arc, and the caption moved into the tooltip so the
button is a compact 44px square again. Drawn with GDI+ rather than shipping the
licensed flaticon artwork; if you would rather use that exact file, send the PNG
and I will embed it like the login icons.

### (d) Page layout
Columns are 150px apart (was 60) and captions get 330px (was 250), so a long
description now fits beside its box. Numeric inputs are 150x26 with a FIXED width
and 8 digits, so the figures line up in a readable column instead of each box
being its own size. The value box has a full four-sided rounded border, which the
reading needs to look like a field. Lamps sit at the LEFT edge of their column,
in line with the controls above and below them, instead of floating mid-column.

### (e) Profile panes opening minimised - the last hole
The restore ran on HandleCreated, which can fire while the page is still 0-sized
(it is built before it is shown), so panes were placed against a zero canvas. The
restore now waits for the first layout pass with a REAL canvas size, however late
that is. A test opens the page for the first time ever, checks every pane is
placed and not minimised, then arranges, closes and reopens.

### (f) Settings sub-page captions, and the manual
The duplicate heading inside each settings sub-page is gone - the bold tab names it.
HMI Settings now also carries the MANUAL: navigation, signing in, alarms, units,
the Profiles workbench and tooltips, generated from the program itself so it cannot
drift out of date, and saveable to a file. It is visible to everyone; the AI Prompt
page next to it stays admin-only.

## 77. Cut model, alarm structure, control defaults

### (a) G-code: the tool now cuts THROUGH each element
The path ran start-to-start, so the body of an element was never cut. The route is
walked element by element: enter at the start, cut to the END at that element's
depth, then travel to the next element's start. Chaining shares endpoints, so an
element often contributes a single new point - without an explicit move for it
nothing was emitted at all, which is exactly what was seen.
"Z plunge" is GONE - the depth is the cut, and a second vertical figure only
invited the question of which one mattered. "Cut into profile" is now "Cut depth",
and every figure on the page is stated in MILLIMETRES: the drawing, the parameters
and the program share the DXF's own units.
Per element (select, then the gear button):
    Cut depth          how deep this element is cut
    Depth at the end   different from the start = the cut TAPERS along the element
    Start / End offset begin earlier or later, overrun or stop short
    Do not cut         leave it in the drawing but out of the cut
Excluding an element removes it from the ROUTE before chaining, so the tool never
travels there - simpler than cutting up to it and lifting over.
The scissors button SPLITS an element at the point you click, so half a contour can
be cut and the other half left. Metadata follows the split.

### (b) Profiles table
Saving a profile now refreshes the LIST (the grid is unbound because it renders
group captions, so edited values only appeared after a grouping or sorting action).
The sort arrow marks only the MOST RECENTLY sorted column - the earlier sorts are
still applied underneath, but marking them all hid which click had just taken
effect.

### (c) Alarms
The main machine now uses the SAME alarm page as the sub-machines, and its columns
sort on a header click (click again to reverse). The Alarms nav entry pulses with a
THIN border, and stops pulsing once the alarm page is open - it has done its job by
then. The reset button is larger with a white frame, so it is easy to hit on a
touch screen.
New ALARM SETTINGS page (Settings > Machine Parameters > Main): the severity each
alarm is reported with, and a HELPER LINK - a document or drawing the operator can
open when that alarm fires. Stored per machine in AlarmSettings.cfg, NOT in
Alarms.json, so regenerating the alarm list from the customer's workbook never
discards what was tuned on site.

### (d) Top bar
The Simulation Monitor moved into the user menu (admin only) - as a lone caption
button it looked unrelated to anything, especially once the alarm reset appeared.
The remaining buttons are spaced 18px apart for touch.

### (e) Controls
Lamps and checkboxes are square, exactly as tall as a ButtonHMI, and share the 4px
radius. Every HMI control now starts with an EMPTY caption - they all carry
DescriptionText, so a built-in label only had to be deleted on every drop.
The active tab is bold AND underlined.
"Cut Length Offset" is merged into the Cutting tab; parameter inputs are 5 digits
wide rather than 8.

## 78. Navigation borders, numeric write bug, added lines, bypass workflow

### (a) The numeric input wrote the WRONG VALUE - the serious one
Typing 2500 stored 25. Reading does exactly  displayed = raw / Divisor  (the
decimal places are a display FORMAT, not a second scaling), but the write path
never applied the divisor and DID apply the decimals - so the two directions
disagreed by the divisor. Writing is now the single inverse, raw = typed x Divisor,
and a round-trip test asserts what is typed comes back unchanged. A large value no
longer wraps inside the 16-bit word either (2500 with divisor 100 was producing
25,000,000, which wrapped to 30784).

### (b) Navigation and tab borders
The border WAS being drawn - in the pale DISABLED colour. Controls start
fail-closed until the scanner enables them, and a navigation button has no PLC
binding, so it stayed "soft disabled" for ever. A button with nothing wired to it
is now always painted live. Found by rendering a nav button to a bitmap and
reading the pixels rather than re-reading the paint code.
Tabs are outlined on the LEFT, TOP and RIGHT only - a tab is open at the bottom
where it meets its page, and a full box read as a floating button.

### (c) Crash: captions recursed until the stack overflowed
Re-measuring a caption raises SizeChanged, which was wired back to the positioning
code, which re-measured... A re-entrancy guard stops it. This took out any page
with a caption that needed re-measuring - including the parameter pages.

### (d) Scrolling no longer detaches a caption
An AutoScroll panel MOVES its children, so a control scrolled past the top-left
goes negative. Measuring the clip against Left alone shrank the label while
scrolled and left it shrunken when scrolled back. Everything is measured against
the scrolled origin now, and AutoSize is restored whenever there is room.

### (e) Sorting is levelled everywhere
Clicking a second column keeps the first sort underneath (up to four levels), and
only the column sorted LAST carries the arrow - in the profile table AND the alarm
tables. The profile table's arrows now work on every column, not just two.

### (f) Added lines really drive the border route
The chaining tolerance was 0.05, tight enough that a drawn line could silently
fail to join, so the route ignored it. It is 0.2 now - still far below any real
feature - and a test deletes an edge, bridges it with two drawn lines and asserts
the program follows them.

### (g) Bypassing elements is now practical
Four new tools: select the whole CONNECTED run from one click, select everything
BETWEEN two chosen elements, mark the selection "do not cut", and put it back.
Bypassed elements are drawn as dashed amber - present in the drawing, out of the
cut - and they leave the route entirely, so the tool never travels there.

### (h) Profile layout self-heals
An arrangement where EVERY pane is minimised is not something anyone sets on
purpose - it is what an older build wrote when it derived "minimised" from
Control.Visible. Such a layout is now discarded in favour of the default tiling,
which repairs the config files already written on site.

### (i) Turkish text repaired
Some rows had been written by a shell printf that emitted the ESCAPE TEXT rather
than the bytes, so "Alarm Ayarlari" appeared as "Alarm Ayarlar\\xc4\\xb1". Twenty
rows across the common translations are decoded back to proper UTF-8.

## 79. PLC label list, shared navigation, handoff prompt

### (a) A REAL BUG IN THE PLC INTERFACE BLOCK - please fix in the PLC
The ST mirror block was decoded and expanded:
    K2M248:=K2X100   covers M248..M255
    K2M252:=K2X110   covers M252..M259   <-- starts FOUR bits too low
M252..M255 are written twice, so X104..X107 are overwritten by X110..X113 and can
never be read from the HMI:
    X104 IN_Freze1Y_RefSN   X105 IN_Freze1Z_RefSN
    X106 IN_Freze1A_RefSN   X107 IN_Freze2X_RefSN
Suggested correction: K2M256:=K2X110.
Also X1130 / X1132 / X1133 (the cross-roller sensors) are not mirrored at all, so
they are invisible to the HMI as things stand.

### (b) The PLC label list is now first-class
tools/io_mirror_map.py decodes the ST block (K2 = 8 bits, X/Y OCTAL, M decimal).
tools/build_plc_labels.py turns the engineer's device list into
<machine>/PlcLabels.csv as  Label;DataType;Address;Mirror  - filling the mirror for
every physical point and warning about overlaps and unmapped I/O.
Kaban.Core PlcLabelTable loads it and resolves a LABEL or an X/Y address to the
internal relay the screens must actually use. The rule is now enforceable: a screen
binds to the M mirror, never to X or Y, which is the whole point of the interface
block.
NOTE: only a sample list is in the repo. Drop the full device list in and re-run
build_plc_labels.py, then generate_pages.py, to rebuild FA2010's pages against the
CURRENT devices (the .xls in tools/ is out of date, as you said).

### (c) Simulation monitor
Columns are Label / DataType / Address / Value. The label comes from PlcLabels.csv
and reads NULL when the address is not in the list - which is itself useful, as it
means a screen is bound to something outside the device list. The ON/OFF column is
gone: it said nothing the Value column did not, and a set bit now shows by the
value going green.

### (d) Shared navigation for every machine
Kaban.UI StandardScreens builds Home, Alarms, Manual Movement and Settings - with
their icons, roles, sort order and security - so a machine project supplies only
its own content. Sub-machines use it too, which is why they had no Settings page
and no icons before.

### (e) Fixes
- "Do not cut" really works now. An explicit border (set from a selection) was
  stored as a POINT LIST, so it knew nothing about later exclusions and the button
  appeared dead. It remembers its ELEMENTS and rebuilds when any is excluded.
- Bit and word actions read as ordinary admin-tooltip lines
  ("Bit Action:  Momentary M672") instead of under a heading of their own.
- The keypad only shows a RANGE when a real limit is set - "-2147483648 to
  2147483647" helped nobody - and formats the figures the way the control formats
  its value.
- The active tab has a drawn RULE beneath it instead of a font underline, which
  sat on the baseline and sliced through g, p and y. Table headers and the tab
  strip gained the same descender room. Tabs and the left navigation share one font.
- The alarm page hides the GRID when there are no alarms (an empty table read as a
  fault) and gained a HISTORY tab, newest first, exportable to CSV.
- Six-language fill for the UI keys; machine page captions translated. 95 rows
  remain English-only and fall back to English, which is safe.

### (f) AI_HANDOFF_PROMPT.md
A full briefing for a fresh assistant: the environment constraints that cannot be
guessed (COM bitness, the poisoned control, the 15 ms round trip, Kaban.Core having
no UI reference), the navigation/role model, the control family and its scaling
rules, the label-mirror indirection, the DXF cut model, the generators and the
designer rules they must obey, the verification programs and the bug each one
encodes, and what is still open. Attach it with the zip in a new chat.

## 80. Address translation is a BUILD step, not an HMI concept

### (a) The "Mirror" column is gone
I had misread the intent: the ST interface block is a fact about the PLC, not
something the HMI should carry. PlcLabels.csv is now simply

    Label;DataType;Address

and every address in it is one the HMI can bind to. tools/build_plc_labels.py does
the substitution ONCE while the file is built - a device the engineer lists at X20
is written out as M200 - so the running program never sees a physical address and
there is no Mirror field, no EffectiveAddress and no runtime indirection.
A physical point the ST block does NOT copy is LEFT OUT of the CSV rather than
emitted as X..., because a control bound to it would fail silently on the machine.
The builder reports those, so nothing disappears quietly.

### (b) The page generator now uses the label list
generate_pages.py resolves every command, lamp and position address through
PlcLabels.csv before writing a designer file: a label becomes its address, a
physical point becomes the internal relay, and anything unresolved is reported and
left unbound instead of being written out as a dead address.
This is what will bring FA2010's manual-movement lamp and output addresses up to
date once the current device list is dropped in - the .xls in tools/ is old, as
you said, and the label list now wins wherever the two disagree.

### (c) Simulation monitor
Rows can be REMOVED from the view (Del, or the context menu) - it hides the address
rather than clearing the PLC image, so a bench session can be narrowed to the few
devices being worked on - and DRAGGED into any order. The arrangement survives the
refresh, which a dictionary-ordered dump would otherwise undo every 200 ms.
"Show everything again" restores the full list.

### (d) Both prompts updated
AI_HANDOFF_PROMPT.md and the in-app AI Prompt page now describe the corrected
model: the label list holds only HMI-readable addresses, the X/Y translation is a
build step, and an assistant must never write X or Y into a control.

---

## 2026-08-06 - Multi-controller monitor, Station Connection, machine renames, two new drivers

### The change that made the rest possible

A station was registered as `ctx.Station(1)`, so only the DEPLOYMENT's station id
survived. The module's OWN PLC number - the "1" or "2" the engineer says out loud
- was thrown away at registration. That is why the monitor could only ever say
"Station 3" and never "H500 PLC1".

- `PlcManager.PlcStationInfo` gained `LocalNumber`; `RegisterStation` takes it as
  an optional 5th argument. All four call sites updated.
- `MachineRegistry.MountContext.Station()` now RECORDS every local -> real
  mapping it hands out, exposed as `MappedStations`. A module never declares "I
  have two PLCs" anywhere; it just calls `Station(1)` and `Station(2)`. Watching
  those calls is the only honest inventory of a machine's controllers.

### Simulation Monitor - rewritten (`SimulationMonitorForm`)

- **One tab per controller**, named "FA2010 PLC1", "FA2010 PLC2", "H500 PLC1".
  Built from the mounted modules, so a machine with no sub-machines simply has
  fewer tabs.
- **All four columns editable** - Label, DataType, Address, Value - in the grid,
  and in a per-row popup on double-click (`PlcRowEditorForm`), which is the usable
  option on a panel PC with an on-screen keyboard.
- **The bottom add/set bar is gone.** The list now ends in a blank line that is
  typed into directly; filling in its Address promotes it and a new blank appears.
- **Export CSV / Import CSV** buttons replace it. Export writes
  `Label;DataType;Address;Value`; import accepts that or a plain three-column
  `PlcLabels.csv` as-is. Imported values are only written after an explicit
  confirmation, so "add these to my list" can never command the machine as a side
  effect.
- Watch lists, row order and hidden rows now **persist between sessions** under
  `CommonAppData\KabanHMI\Monitor\<Module>_PLC<n>.csv`, keyed by module and local
  PLC number - never by station id, which is the thing Station Connection is
  allowed to change.
- Real stations are read on a **worker thread** via `TryReadMulti`. A PLC round
  trip is ~15ms; batching a screenful onto the 250ms UI timer would stall the HMI.
- Values write straight through on simulated and real stations alike (the page is
  admin-only). Writes to a real station are logged, and its tab carries a red
  banner so a live CPU is never mistaken for the bench.
- A label/type typed here is stored as an OVERRIDE only when it differs from
  `PlcLabels.csv`, so a corrected label list still flows through later.

### Station Connection (Settings -> HMI Settings, admin)

One row per controller: driver combo box, MX Component logical station number,
and a network address for the drivers that need one. Saved to
`CommonAppData\KabanHMI\PlcConnections.cfg`, applied at the next start.

The two numbers involved are deliberately kept apart on screen, because confusing
them wastes an afternoon: **StationId** is the HMI's own handle (a control's
`PlcNumber`); **LogicalStation** is what the MX Communication Setting Utility
stores. They frequently disagree.

### Machine names and renames

- `Kaban.Machines.SubMachine1` -> **`Kaban.Machines.H500`**, `SubMachine2` ->
  **`H800`** (folders, files, namespaces, classes, csproj, sln, alarms,
  translations).
- Display names are now translated in all six languages, and the MAIN machine
  supplies one too (it did not before, so the project id "FA2010" leaked onto the
  screen):
  - `Machine_FA2010` = Processing Center / Isleme Merkezi
  - `Machine_H500` = Screwing_Holing / Vidalama_Delme
  - `Machine_H800` = 3D_HingeDrilling / 3D_MenteseMatkap

### `Program.cs` is now the one place a deployment is described

FA2010's entry point has three commentable sections: which DRIVERS this exe
offers (which is exactly what populates the combo box), which MACHINES are
mounted, and the fallback connection for a PC with no saved settings. Comment out
a `MountSub` line and that unit's tabs, alarms, translations, settings pages and
monitor tab all disappear together.

Bench seeding now iterates over whatever ended up SIMULATED rather than over
hardcoded station numbers, so pointing PLC2 at a real CPU quietly stops seeding it.

### Two new drivers - NOT TESTED AGAINST HARDWARE

- **`Kaban.Drivers.Delta.DeltaModbusTcp`** - Delta AS/DVP over Modbus TCP.
- **`Kaban.Drivers.Siemens.SiemensS7`** - S7-1200/1500 over ISO-on-TCP (RFC1006).

Both are plain sockets with no SDK and no COM, deliberately: MX Component's
32-bit-only registration is already the most expensive constraint in this program
and a second SDK would bring its own version of it.

Verified against **mock servers that assert on malformed frames**
(`tools/protocol_tests/`): function codes, big-endian framing, Delta's octal X/Y,
signed decode, batch coalescing, S7 TSAP from rack/slot, PDU negotiation, bit vs
byte transport, DB access. What that does NOT prove is that a real CPU accepts
them. On an S7, PUT/GET must be permitted and the DB must not be
"optimized block access"; both are off/on by default in the way that blocks you.

### Bugs found on the way

- **Three projects shared one `ProjectGuid`.** `Kaban.Machines.H500` and `H800`
  both carried FA2010's. Visual Studio resolves a `ProjectReference` by GUID, so
  this could point a reference at the wrong assembly. Fixed to match the .sln.
- `CommonTranslations.csv` rows quoting `Label;DataType;Address` split into up to
  25 columns, because `;` is the separator. Caught by the new check below.
- 14 translation keys were used in code but never defined, so they showed English
  to every operator regardless of language (`Msg_NotLoggedIn`,
  `Msg_PlcDisconnected`, the `Ai_*` and `Sys_*` sets). Now defined in all six.

### New check (per section 10)

`tools/verify_plc_setup.py` - duplicate ProjectGuids, .cs files compiled by no
csproj, .sln entries pointing at missing projects, `RegisterStation` calls missing
the local PLC number, translation CSV column count and BOM, and keys used in code
but never defined. It caught two of the three bugs above.

### The standalone sub-machine entry points

`H500/Program.cs` and `H800/Program.cs` build their own .exe and can be run as a
MAIN machine on their own. They were still hardcoding a driver behind a
`UseSimulation` const and never registered any driver kinds - so run standalone,
their Station Connection page would have shown an EMPTY driver combo box.

Both now have the same three sections as FA2010: `RegisterDriverKinds()`,
`MountMain`, then `PlcConnectionSettings.ApplyAll(FallbackConnection)` and bench
seeding over whatever ended up simulated. `UseSimulation` survives as the
FALLBACK only - once the settings page has been used, the saved choice wins.
Both projects now reference the Siemens and Delta drivers.

This is what makes "any machine can be main or sub" true rather than nearly true:
the same assembly now behaves identically in both roles, and neither role is
described anywhere except an entry point.

### Verification run for this round

- All **11 build targets** compile (9 libraries + the H500/H800 standalone exes).
  The two exes had never been compiled in earlier rounds, so their `Program.cs`
  was unchecked until now.
- `tools/verify_plc_setup.py`: OK, one warning (the dead `CSharpInputBox.cs`).
- Controller-inventory and settings round-trip suite: 19/19.
- Delta and Siemens protocol suites against asserting mock servers: all pass.

### Refactor: dead code and stale comments removed

Every deletion below was established by MEASUREMENT, not by reading: a script
(`/tmp` throwaway, logic now folded into `tools/verify_plc_setup.py`) listed types
whose name never appears outside their own declaration, and the compiler listed
unused fields. Nothing was deleted on a hunch.

**493 lines across 11 files deleted outright:**

| File | Why |
|---|---|
| `Kaban.Core/Localization/LocalizationEngine.cs` | a SECOND, unreachable translation implementation |
| `Kaban.Core/Localization/MediaLink.cs` | its media model, reached only from the above |
| `Kaban.Core/Localization/AlarmEntry.cs` | its alarm model |
| `Kaban.Core/Localization/TranslationEntry.cs` | its UI-key model |
| `KabanHMI/Tools/LocalizationScanner.cs` | its form scanner |
| `Kaban.Core/Models/PlcMathConfig.cs` | referenced by nothing |
| `Kaban.Core/Models/PlcDisplayConfig.cs` | referenced by nothing |
| `Kaban.Core/Helpers/HmiDiagnosticHelper.cs` | referenced by nothing |
| `Kaban.Machines.FA2010/Screens/TabbedHubScreen.cs` | superseded by `MachineTabScreen` + `SettingsScreen` |
| `KabanHMI/Forms/CSharpInputBox.cs` | dead dialog; `InputForm` is the one used |
| `Kaban.Drivers.Siemens/Siemens.cs` | empty stub class, superseded by `SiemensS7.cs` |

The localisation island was the notable find: five files forming a closed loop that
nothing outside could reach. The program translates through `LanguageManager`
(36 files). Deleting it cascaded - `AlarmEntry`/`TranslationEntry` existed only to
feed it, and `MathOperationEnum`/`TermEnum`/`OpStyleEnum` existed only for
`PlcMathConfig` - so the sweep was repeated until it found nothing.

**Also removed:**

- `MachineParameterPage`, `BarcodeSettingsPage` - "coming soon" settings pages
  that nothing ever navigated to, plus their two translation keys.
- `MainForm._idleLogoutTimer` / `_lastActivity` / `IdleLogoutAfter` - **a
  half-built idle-logout feature.** Three fields and a 10-minute constant with NO
  timer wiring: nothing ticked, nothing read `_lastActivity`. It has never logged
  anyone out. Removing the fields removes no behaviour, but the INTENT was real
  and is recorded here rather than lost: **if auto-logout on an idle panel was
  wanted, it still needs building.**
- `PlcScanner._addressIndexMap` and `_reconnectTicks` - leftovers from superseded
  implementations. The scalar `_reconnectTicks` had been replaced by the
  per-station `_plcReconnectTicks` map; a comment now records why per-station
  matters (one shared counter let a healthy station reset a failing one's back-off).
- `SettingsScreen.ColAccent`, `DxfDocument.bestLen`, two unused `using System;`.

**Comments rewritten, not just deleted:**

- All 16 `// FIX:` and `// NEW:` markers are gone. `NEW:` dates a comment to a
  release nobody remembers; `FIX:` narrates a past edit. Where the sentence
  carried a real CONSTRAINT it was reworded into the present tense - "was a
  relative filename, so the location depended on the working directory" became
  "anchored to a fixed location, NOT a relative path: a relative path lands
  wherever the process was started from, which on a panel PC means nobody can
  find it". Where it only said "we changed this", it went.
- `PlcManager`'s header narrated a refactor away from "station 3 == the
  screwdriving unit". It now states the rule and why the core CANNOT special-case
  a station number: the same sub-machine sits on a different station on every host.
- `MainForm`'s monitor block still described a simulator-only window with
  per-station tabs. Both stopped being true this round.
- FA2010's `HomeScreen` told the operator "PLC 1 - Mitsubishi (watch in GX Works)"
  and "PLC 2 - Simulation (watch with F12)" in a designer comment AND in two
  translated labels. Which driver sits behind each is now a SETTING, so the screen
  cannot make that claim - the labels are now plain "PLC 1" / "PLC 2".

### Deliberately kept

- `Examples/GirisKonvExample.cs` - compiled by nothing, but it is documentation:
  it encodes the real X/Y octal and M decimal address stepping for Sikma 1..7,
  verified against the symbol table. Say the word and it goes.
- `ALARM_TRANSLATIONS_TODO.md` - a live list of 72 untranslated alarms.
- One `CS0219` warning in `AlarmScreen.Designer.cs` (`resources` assigned, never
  used). Generated by the WinForms designer, which rewrites the file on its own
  terms; editing it risks a designer round-trip for a cosmetic warning.

### Still open

- Both new drivers need a bring-up on real hardware.
- Idle auto-logout was intended and never built (see above).

---

## 2026-08-06 - Navigation split, machine-named tabs, one blue

### Machine names on the tabs

Tabs under Home / Alarms / Machine Parameters now read **Processing Center |
Screwing_Holing | 3D_HingeDrilling**, resolved through the translation files, so
they follow the operator's language.

The main machine used to read a literal **"Main"**. On a line with three
machines that is the one word which does not identify one - and its `TitleKey`
was `"Tab_MainMachine"`, so no amount of translating the machine name would have
changed it. `MachinePage` now takes its key from the module's own
`DisplayNameKey`, main and sub alike.

### "Settings" split into two left-bar buttons

| Before | After |
|---|---|
| **Settings** > HMI Settings \| Machine Parameters > (machine) | **Machine Parameters** > (machine)  ·  **HMI Settings** |

- **HMI Settings** is emitted by the MAIN machine only, so it is one button with
  no tab strip however many sub-machines are mounted. It is the HMI *program's*
  settings - units, users, logs, station connections, the manual - and one set
  serves every machine. Emitting it per machine and letting the role grouping
  collapse it would have produced a strip reading "Processing Center \|
  Screwing_Holing" over identical content, inviting the operator to wonder which
  machine's units they were setting.
- **Machine Parameters** is role-grouped like Home and Alarms, so mounting a
  sub-machine adds a TAB named after the machine rather than another button.
- New roles `ScreenRoles.HmiSettings` and `ScreenRoles.MachineParameters`
  replace `ScreenRoles.Settings`.

`FA2010Module.GetScreens` was hand-building all five navigation entries - exactly
what section 3 of the handoff says not to do, and how the Settings entry ended up
defined twice in two different shapes. It now calls `StandardScreens.Build` and
supplies only what is its own. `MachinerySettingsPage` and `MachineSettingsPage`
became unreachable and were deleted (the verify script caught both).

### One meaning for blue

Blue meant three unrelated things - the active tab's rule, some buttons' hover,
an accent fill - so it told the operator nothing. It now means exactly one:
**the border of whatever the pointer is over**, `UiStyle.HoverBorderColor`.

`UiStyle.HoverBorder(control)` is the single mechanism; every control that draws
a border now uses it, where before only `ButtonHMI` did:

- `BaseNumericHMI` (so `NumericInputHMI` and `NumericDisplayHMI`)
- `GroupBoxHMI`
- `RoundedGroupBox` - the frame around a `FunctionUnitHMI`
- `UiStyle.Round` - every plain toolbar and dialog button
- the left-nav expand/collapse toggle

Hover is tracked with an explicit flag rather than by hit-testing the cursor
during paint: a control repaints for many reasons the mouse has nothing to do
with, and testing `MousePosition` mid-paint made borders flicker blue when a
value changed under a stationary pointer. A child filling its parent swallows the
parent's enter/leave, so children forward theirs.

### Tabs

- **The same appearance at every level.** The active tab is white and merges into
  its page. Outer levels used to be shaded darker (`ColActiveParent`), which made
  the top strip read as a separate navigation bar rather than as the tab of the
  page below it.
- **The rule under the active tab is BLACK, not blue**, and is measured from the
  CAPTION rather than spanning the whole tab, sitting 2px under it. Spanning the
  full width left it floating clear of the word so the two did not read as one
  object.
- **20% shorter**: strip 38 -> 30, tab button 28 -> 22, top padding trimmed to
  match. The left-nav toggle strip went 38 -> 30 and its button 34 -> 27.

### Checks

`tools/nav_test/` asserts the shape of the left bar - one HMI Settings with no
tab strip, Machine Parameters tabbed per machine, and the tab captions in EN and
TR. A role typo or a stray `yield return` splits a button in two without failing
to compile, which is precisely the kind of thing that reaches a machine.

---

## 2026-08-07 - Interaction cues: hover borders, cursor, lamp clearance

### Blue marks the control, not the box around it

The previous round outlined `GroupBoxHMI` and the `FunctionUnitHMI` frame on
hover. That was wrong: a frame is not clickable, so outlining it says nothing
about where the click will land, and it competed with the leaf control actually
under the pointer. Both are back to a neutral border, and `UiStyle.HoverBorder`
no longer forwards a child's enter/leave up to its parent - the forwarding was
what made containers light up in the first place.

The blue border now marks exactly the control the click, the tooltip and the
keyboard focus belong to.

### It now applies to STANDARD WinForms controls too

`UiStyle.HoverOutline(control)` draws the outline from the PARENT, just outside
the control's bounds. That was the only way to reach controls we did not write: a
`TextBox` with a system border paints its own frame and gives no way in, a
`ComboBox`'s drop-down is a separate window, and a `DataGridView` repaints its own
edges. Drawing on the parent sidesteps all three and works for anything.

`UiStyle.ApplyInteractionCues(page)` walks a page and wires every interactive
control, called once from `MainForm.LoadScreen` and once from `SettingsScreen`
(tab pages are built on first selection, so they miss the MainForm walk).
Applying it centrally is the point - the old rule was really "whichever controls
remembered to implement it", which is why some turned blue and some did not.

Controls that already paint their own border - `ButtonHMI`, `BaseNumericHMI` -
declare `UiStyle.IDrawsOwnHoverBorder` so they are not outlined twice.

Already-wired controls are tracked in a reference-identity `HashSet`, NOT in
`control.Tag`: Tag belongs to whoever built the screen, and quietly taking it over
is the kind of thing that breaks a machine page months later.

### Cursor follows Enabled

Hand while a control is usable, plain arrow once it is disabled - so a button
greyed out by an interlock is obvious BEFORE it is clicked rather than after
nothing happens. WinForms does not do this on its own: `Cursor` is independent of
`Enabled`, so a disabled control keeps whatever hand it was given.

A `TextBoxBase` keeps its I-beam when enabled; a hand over a text field would be
a lie about what clicking does.

### Lamp clearance on ButtonHMI

`LampBarInset` 3 -> 6, and `LampBarHeightRatio` 0.80 -> 0.72 so the same few
pixels are bought at the top and bottom edges rather than only at the side.

At 3px the lamp merged into the 1.6px frame and read as a decorated edge. It is a
SENSOR reading - a separate thing that happens to be shown on the button - so it
has to sit visibly inside. Kept small deliberately: any more and it stops reading
as the END position of the movement that button commands.

Measured on a rendered button: **3px of clear background** between frame and lamp,
where there was none.

### New check

`tools/ui_test/` renders controls off-screen and inspects the pixels, because
these rules are about appearance and cannot be checked by reading the code. It
caught its own first version: the lamp gap was measured along the button's
midline, which with two lamps is exactly the empty split between them and read as
0px. Scanning at quarter height gives the real figure.

Mono's WinForms is not pixel-identical to Microsoft's, so the test proves the
LOGIC - who gets an outline, what the cursor is, how far apart two shapes are -
rather than the exact look. The look still wants a human eye on Windows.

---

## 2026-09-01 - Second-level machine menu, restart, tooltips, and a three-layer translation bug

### The sub-machine name bug - three faults, not one

Reported as "sub-machine names turn into a key after login". Reproduced with a
harness that replays the real startup sequence (`tools/lang_test/`), and it was
three separate faults stacked:

1. **FA2010's .csproj never copied H500's/H800's own `Translations.csv` or
   `Alarms.json` into its output.** A plain `<ProjectReference>` copies the
   compiled DLL and nothing else - a referenced project's content items do not
   travel. So `ctx.ResolveFile`'s per-module lookup
   (`<output>/<ModuleId>/<file>`) always missed and silently fell back to
   FA2010's OWN file, which has no `Machine_H500` row.
2. **`MainForm` then did a second, DESTRUCTIVE reload.**
   `LoadMachineTranslations` clears the machine layer first, so even a correct
   merge from step 1 would have been wiped back down to one file. Changed to
   `AddMachineTranslations`.
3. **`DisplayName` used `LanguageManager.Localize`**, which returns the raw KEY
   when a lookup fails - by design, since it also handles "this text is either a
   key or already literal wording". That is what put `Machine_H500` on screen.
   All three modules now use `GetString(DisplayNameKey, ModuleId)`, so the worst
   case is the readable `"H500"`.

**The same mechanism means a mounted sub-machine's ALARM DEFINITIONS were never
loading either** - `Alarms.json` resolves through the identical path. That is the
larger half of this bug and was not visible from the reported symptom.

### Second-level MACHINE menu (`MachineSelectorStrip`)

Machine selection moved out of the page's top tab strip and into the left bar,
under the page buttons, separated by a gap so it reads as a second menu.

- **It hides itself when there is nothing to choose** - HMI Settings is one page
  for the whole program, and a machine with no sub-machines has one Home. A
  one-entry menu asks a question with a single answer.
- **Every machine gets a distinct icon**, so the strip still distinguishes
  machines when the bar is collapsed to icons. A machine may supply its own via
  the new `IMachineIcon`; otherwise it gets a coloured tile with its initial,
  keyed off the module id so the same machine keeps the same tile between runs.
  A shared generic glyph would have compiled perfectly and been useless in
  exactly the mode the strip exists for.
- **The page's own machine tabs are gone** (`SettingsScreen.TabStripHidden`).
  Offering the same choice twice means two "you are here" marks to keep in step.
  A page whose tabs are its OWN sub-pages - HMI Settings' Units / Users /
  Station Connection - keeps them; only machine tabs were suppressed.

### Restart

- **Station Connection's save dialog now OFFERS the restart** rather than just
  mentioning one is needed. A driver cannot be swapped under screens already
  bound to the running one, so the setting is inert until then.
- **The close menu has "Restart the program"** beside "Close the program". Most
  reasons to shut an HMI down are really "make a setting take effect", and on a
  panel with no desktop the operator otherwise has to find the shortcut again.
- The relaunch waits ~1.5s before starting the new process, spent by the NEW
  process so the window still closes instantly: two instances would otherwise
  briefly hold the same PLC station, and MX Component refuses the second opener
  rather than sharing the channel.

### Tooltips

One shared factory, `UiStyle.Tips()`, replacing 22 scattered `new ToolTip()`
calls: **1s before showing**, **10s auto-hide**, no fade.

The delay is the fix for tips firing as the pointer merely crossed a control - a
screenful of HMI controls all carry tips, so the screen flickered yellow while
someone was moving toward a button. A second means a tip appears when someone has
actually stopped to ask what something is. The 10s expiry matters on a panel PC
where the pointer is parked and left: a tip that never times out sits over the
live values it was explaining.

`ButtonHMI` additionally hides its tip on mouse-leave. It rebuilds its tooltip
TEXT on every hover (to compose the admin wiring block), and re-setting the text
while a tip is on screen is exactly the case where WinForms lets it outlive the
pointer and hang over the next control.

### Layout and colour

- **Content inset** (`8,6,8,8`) so tables no longer butt against the navigation
  bar or sit welded to the tab line. NESTED strips still get zero, or a third
  level would start halfway across the screen. Applied on `OnParentChanged`, not
  in the constructor - `Parent` is still null there, so the nesting test cannot
  be answered yet.
- **Alarm Settings columns** switched from fixed widths to Fill, and every column
  now has a `MinimumWidth` measured from its own HEADING. The default minimum is
  5px, which is why "Helper link (file, page or URL)" opened clipped and had to
  be dragged before the grid could be read.
- **The active-tab rule is placed from the FONT's line box**
  (`UiStyle.DrawActiveCaptionRule`), not a hand-picked "height minus 2". That
  offset is what put an underline through p/g/q tails: it was tuned against one
  renderer, and another font size, DPI or renderer puts the glyph elsewhere. The
  rule is positioned from the font's own metrics, so it clears descenders the
  font CAN produce - not the ones this particular word happens to use, which
  would make the rule jump between captions.
- **Navigation bar lightened** 243 -> 249, with the inactive row 235 -> 240 to
  match. At 243 the bar and the white page read as a smudge, and an inactive row
  barely showed against it.
- **Distinct icons for the two settings buttons**: sliders for Machine
  Parameters (adjustable values), the gear for HMI Settings. They shared one
  icon, which is indistinguishable in collapsed mode.
- **Top-bar hover borders** now work. Login, language, alarm reset and the window
  buttons live on `pnlTop`, which sits outside `_currentScreen` - the per-page
  cue walk never reached them, so they needed their own call once every button
  exists. `PictureBox` was added to the container exclusion list: the company
  logo is decorative, and lighting it up would promise a click that does nothing.

### Checks

- `tools/lang_test/` - the display-name chain, against a directory laid out the
  way MSBuild actually produces it.
- `tools/nav_test/MachineStrip.cs` - the second-level menu: appears only when
  there is a choice, selection raises the right index, icons are distinct
  (compared as PIXELS), and the page's machine tabs are suppressed while
  non-machine tabs survive.
- `verify_plc_setup.py` caught `MachineSelectorStrip.cs` missing from
  `Kaban.UI.csproj` - it would have built here and done nothing in Visual Studio.

---

## 2026-09-01 - Machine strip placement, restart handoff, caption consistency

### Why the machine strip never appeared, and why it sat in the wrong place

Two separate faults, both found by testing the layout rather than reading it:

1. **`AutoSize` reported zero.** A Panel whose children are all `Dock.Top` has a
   preferred height of 0, so the strip collapsed to nothing however correct the
   rest of it was. Height is now computed in `ShowFor`, where the machine count
   is known.
2. **WinForms docks the HIGHEST child index FIRST**, so the highest index ends up
   closest to the top edge - the opposite of what "added last goes last"
   suggests. Adding the strip last put it *above* everything. The dock order is
   now set explicitly with `SetChildIndex`: toggle strip highest, page buttons in
   the middle, machine strip lowest. **No page button moves**; the strip lands
   under them with a 14px gap.

The **"MACHINE" caption is gone** - it disappeared in collapsed mode anyway, so
it was never what carried the distinction. The gap alone does that now.

### Restart actually restarts now

The old restart relaunched immediately after a fixed ~1.5s pause. That was not
enough: **the new instance opened its PLC stations while the old one still held
them, and MX Component refuses the second opener rather than sharing the
channel.** The HMI came back with the connection dead, which looked exactly like
a just-changed station setting having failed to apply - and closing and starting
by hand worked only because the human pause was longer.

The new process is now passed the old process's PID
(`AppBootstrap.AwaitPreviousInstance`, called first in every machine's `Main`)
and waits on the real process handle, up to 15s. How long release takes depends
on scanner threads finishing their current PLC round trip, which is not a fixed
number, so a guessed sleep was never going to be right.

### One restart notice for the whole program

`MainForm.NotifyRestartRequired(owner, what)` - any page, no knowledge of the
shell required. It **asks**, and never restarts on its own: a setting is often
changed in the middle of other work, and pulling the HMI out from under someone
because they saved a checkbox is not a trade the program gets to make.

### Smaller things

- **Station Connection**: the per-machine heading rows are gone. Each row already
  reads "Processing Center PLC1", so a heading above it repeated the row and
  turned four rows into seven. `AddMachineHeading` deleted with them.
- **Page Y alignment**: a machine page is wrapped in `SettingsScreen` to hold the
  per-machine pages, while a single-machine page is the raw view with no wrapper.
  The wrapper was adding an inset the unwrapped page did not have, so the two
  started at different heights. A hidden tab strip now contributes nothing.
- **Captions**: three colon styles were in use at once (`" : "`, `" :"`, `":"`).
  `UiStyle.Caption` is now the single source - tight colon, one space after - and
  `UiStyle.CaptionLabel` builds a RIGHT-aligned caption. Unit Settings and the
  login form were the two pages still left-aligning theirs, which leaves a ragged
  gap between each word and the control it belongs to. Report text in the AI
  Prompt page is deliberately untouched: its colons are column alignment in a
  monospace block, not captions.

### Checks

`tools/nav_test/StripLayout.cs` renders the strip and the menu and asserts the
things that were actually wrong: a non-zero height, docking *below* the page
buttons, zero padding on a hidden-tab page, and caption punctuation. It caught my
own first attempt at the dock order - the test failed with `strip.Top=0` against
buttons ending at 230, which is precisely the reported symptom.

---

## 2026-09-01 - Machine strip actually placed, restart made reliable, Save gated on change

Three items came back still broken. The common cause was that my previous test
built a synthetic `Panel` instead of the real `MainForm`, so it passed while the
program was visibly wrong. **`tools/nav_test/RealMainForm.cs` now builds the real
form**, and it found the faults immediately.

### Two real faults the synthetic test could not see

1. **`_navHost` was null inside `AddMenuButton`.** `MainForm`'s constructor does
   its setup inside a `try/catch`, so any unrelated startup failure - a missing
   `PlcLabels.csv`, a PLC that will not open - skipped navigation creation, and
   `BuildNavigation`, called afterwards from `Program.cs`, threw. On a machine
   that is a blank left bar. `EnsureNavHost()` now creates the column and the
   strip on first use, and BOTH callers go through it so neither depends on the
   other having run.
2. **Positions have to be compared in SCREEN coordinates.** `Top` is
   parent-relative, so a strip in one container and buttons in another cannot be
   compared directly - which is how a "passing" test coexisted with a strip
   sitting above the buttons.

### The navigation column is a FLOW now, not a dock stack

`Dock.Top` ordering is decided by child index in a direction that reads backwards
(the HIGHEST index docks closest to the edge), and every attempt to place the
strip by juggling that order put it somewhere else. The column is now a TopDown
`FlowLayoutPanel`, which lays children out in **add order**: page buttons, then
the strip. No later re-adding can reshuffle it, and no page button moved.

The separator above the machine list is now **three button heights** (135px), not
14px - at 14 it still read as a slightly loose row in the same group.

### A nav button that leads nowhere is hidden

Machine Parameters is Maintenance-only, so an operator was shown a button that
could only ever answer with a permission warning. `RefreshNavVisibility` hides
any group with no page the current user may open, and re-runs on every
login/logout.

### Restart: why it worked only sometimes

`FormClosing` disconnected **station 1 only**. Stations 2, 3 and 4 were left to
process teardown, which is not deterministic - a COM apartment is torn down on
its own schedule and a socket lingers until its handle is finalised. The incoming
instance waited correctly for the old PROCESS, but the old process had not
necessarily released the other stations by the time it exited, and MX Component
refuses a second opener on a station it still considers held.

Now: the scanner is stopped first (so a scan in flight cannot re-open a station
we just closed), then `PlcManager.DisconnectAll()` closes and disposes every
driver.

### Save is disabled until something changes

Saving an unchanged Station Connection page still wrote the file and still asked
to restart - so the operator was offered a restart for having *looked* at the
page. Save is now disabled until the page differs from the saved state, compared
value by value rather than tracked with a flag, so putting a control back to its
original setting correctly disables the button again.

---

## 2026-09-01 - Navigation layout restored; machine strip pinned and stable

### The page buttons are back exactly as they were

Last round I moved the nav column to a `FlowLayoutPanel` to get deterministic
ordering. It worked, and it changed the look - order, left inset and spacing all
shifted, because a flow panel does not honour `Dock`, margins or full-width
stretch the way the docked buttons did. That was a bad trade: the placement
problem was mine to solve without touching the buttons.

The buttons are docked in `pnlMenu` again, unchanged - `Dock.Top`, height 45,
margin (6,4,0,4), same order. The flow host is gone.

### The strip is placed without touching the buttons

`pnlMenu.Controls.SetChildIndex` gives the strip and its spacer the LOWEST
indices. WinForms docks the highest index first, so the lowest ones are placed
last - below every button - and no button's own position, spacing or order is
affected at all.

### Its position no longer moves with the logged-in user

A spacer above the strip absorbs the height of every nav button hidden for the
current user (`MachineStripGap + hidden * 45`). Verified on the real form: the
strip stays at y=403 whether four buttons are shown or three. The position of a
menu should not be a function of who is signed in.

### It no longer blinks on every navigation click

`ShowFor` was tearing the strip down and rebuilding it on each `LoadScreen`, even
when the same machines were on offer, so the list flashed out and back on every
click. It now compares the incoming group against the current one and returns
untouched when the machines are identical - the same button objects survive,
asserted by reference in the test. It hides only when the page genuinely has one
machine.

### Checks

`tools/nav_test/NavLayout.cs` asserts on the REAL `MainForm`: buttons docked on
the menu panel with one shared margin and uniform spacing, strip below the lowest
button, strip y identical across a login change, and the same button instances
kept when the machine set does not change.

---

## Profile & accessory system - written proposal

`docs/PROFILE_SYSTEM_PROPOSAL.md` answers the two design questions in writing
rather than in code, since both were asked as questions.

The short version: Y/Z belongs to the **profile-accessory PAIR**, not to either
side, so the schema is a three-layer catalogue with an override table that starts
EMPTY and inherits from group defaults - which is what allows approximate values
on day one and correction per trial. Every resolved value carries where it came
from (inherited / measured / confirmed) and shows that on screen.

On DXF: marker-text parsing is deterministic and worth building first; geometric
inference can propose values; full recognition of an unlabelled cross-section is
a real project whose failure mode is a plausible wrong number cut into a
customer's profile, so I recommend it last - by which point the confirmed
profiles from the earlier stages are the training corpus it needs.

The proposal ends with five questions that need answering before any of it is
written, the first being the coordinate datum - which is not a software decision.

---

## 2026-09-01 - Profile & accessory database: group-based schema, resolver, tests

`docs/profile_db/` - schema.sql, a reference resolver and a 15-check test suite
that all pass against real SQLite.

Revised from the earlier per-pair proposal after the follow-up request. Placement
is now a RULE per (profile group x accessory group) expressed against named
datums (HARDWARE_AXIS, OUTSIDE_SURFACE, BED_DATUM...), holding no
profile-specific number. Adding a profile to a group reads its datums from its
DXF and evaluates every rule against them, so its accessories appear with
nothing typed.

Proven by the test: the same handle rule gives Z=70 on the reference profile
(axis at 35) and Z=76 on a new one (axis at 41) - the placement follows the new
profile's geometry rather than copying a number.

Four resolution layers: confirmed override, group rule, type default, then
refuse. The type-default layer means a brand-new vendor with no rules still
produces sane numbers - shown by a frame profile receiving drainage with no group
rule defined.

Production reads `v_placement_effective`, which exposes only CONFIRMED and
OVERRIDDEN rows, so an AI-derived value nobody has reviewed cannot reach a
cutter. AI placements are born PROPOSED, carry confidence, and queue in
`v_placement_review`. Deviation from the group's reference profile is recorded on
every placement, which is how a mis-grouped profile gets caught before a cut.

NOTE: the shared ChatGPT conversation could not be read - that page renders
client-side and only its title came through. If anything was settled there that
contradicts this, it needs re-checking.

---

## 2026-09-03 - Profile library v2: machine-independent, multi-operation, correction chain

The full ChatGPT dialog arrived, and with it two requirements that changed the
design. Schema v1 is replaced.

### 1. The library is now MACHINE-INDEPENDENT (explicit requirement)

No tools, feeds, spindle speeds, G-code, controller dialects, station numbers or
machine coordinates. Those move to a machine-side schema that references profile
ids; §11 of `schema_v2.sql` lists them.

The test ASSERTS this rather than trusting the comment - it walks every table and
column and fails if a name matches tool / feed / spindle / rpm / plunge / gcode /
station. A feed rate is a property of a machine, a tool and a material, not of a
Reynaers frame; storing one in the library would mean FA2010 and SB3012 could not
share a profile without one of them being wrong.

Placement: a NEW `Kaban.Profiles` project, referenced by Kaban.UI so every
machine gets the same screens, hosted for now under FA2010's existing Profiles
nav entry. The database file goes in `CommonAppData\KabanHMI\Profiles.db` beside
the other shared settings, not in a machine's output folder.

### 2. An accessory is SEVERAL operations, not one

v1 stored a single Y/Z per accessory, which cannot express a hinge - three holes
and a slot at different offsets, each with its own repeat pattern along the bar.
`accessory_operation` now holds one row per cut. This was the largest gap.

### 3. Corrections stack; they never replace

`placement_correction` at SERIES / GROUP / PROFILE scope, summed:

    final = reference + operation offset + series + group + profile

Fixing one profile after a trial cut cannot silently move the same hinge on
ninety others, and re-measuring the accessory later still benefits every profile
already corrected. `base_*` is stored alongside the final value so a screen can
show *template 22.0 -> correction +0.7 -> final 22.7*, which is what makes the
trial-and-tune loop legible.

### 4. Also adopted from the dialog

- **Position modes** (REFERENCE_OFFSET / PROFILE_CENTER / FROM_INSIDE /
  RATIO_OF_WIDTH...) rather than free-form formulas - an expression language in a
  machine database is a support burden and a way for a typo to become a cutting
  coordinate.
- **Hardware interface** as the compatibility key. A Schueco profile declaring
  EURO_GROOVE_16 gets Roto NX accessories with no Schueco-Roto pairing entered.
- **Machining family**, independent of the function group, because a Reynaers and
  a Schueco frame can share machining rules while sharing nothing else.
- **Material intervals** along a ray, from the DXF - geometry lives in the
  library, the feed to use in each interval is a machine decision and does not.
- **Parametric macros** holding shape only; no tool, no G-code.
- **Profile fingerprint**, so a new DXF can offer "closest existing profile" as
  its template long before any trained model exists.
- **base_profile_id inheritance**, so a profile differing by two numbers does not
  restate the other twenty.

26 checks pass, including: a new profile materialising four operations with
nothing typed; the same hinge rule giving Z=35 on the reference profile and Z=41
on a new one; a confirmed value surviving a later +99 correction untouched; and a
missing datum refusing with "needs HARDWARE_AXIS" instead of inventing a number.

---

## 2026-09-04 - One database for all machines; EF Core portability; pane upgrades

### The database is now ONE database with two halves

`schema_v2.sql` (profile & accessory library) + `schema_machines.sql` (machines).
Both load into one file. The library says where a hinge sits on a Reynaers
frame - true whoever is cutting it; the machine layer says what a given machine
does about it.

The boundary is TESTED, not documented: `test_machines.py` walks every library
table and fails if a column matches feed / spindle / rpm / tool / plunge. An
earlier draft seeded `milling_class` into the library and the load failed with
"no such table" - the boundary catching a real mistake of mine.

### A new machine series is DATA, not a migration

Kaban builds nine PVC categories plus aluminium. A table per category would make
every new machine a schema migration - and a migration means a release on every
panel PC already in the field.

A series instead declares CAPABILITIES, and a capability owns PARAMETER
DEFINITIONS that are rows. The test adds a KT4000 corner-cleaning centre with two
INSERTs: no table is added (39 before, 39 after) and the machine immediately has
a complete 8-field parameter screen - knife depth, feed, groove depth, bead
width, face flags - with units, ranges and defaults, because CORNER_CLEAN already
declared them. FA2010 sees 14 parameters, SB3012 sees 5, H500 sees 8; a cutting
machine never carries milling fields.

Honest trade-off, stated in the README: `parameter_value` is key/value, which
loses column typing. Accepted THERE and nowhere else, because those values are
read a screenful at a time for one profile on one machine, never aggregated. The
library, which IS queried in bulk, stays strongly typed. Anything that is a
sequence or a shape rather than a value gets a real table -
`corner_clean_program`/`_step` is the worked example.

### EF Core portability (SQLite now, PostgreSQL later)

The schema no longer uses AUTOINCREMENT, `datetime('now')`, CHECK-IN
constraints, triggers or views - none of which EF Core round-trips identically
across SQLite, PostgreSQL and SQL Server. Enumerations are C# enums stored as
short strings; history rows are written by the repository; the two views are LINQ
queries.

Removing the DB defaults immediately broke the resolver and both test suites with
NOT NULL failures on `created_at`/`updated_at` - which is precisely the change
the C# port has to make, surfaced now rather than on a customer's Postgres.

Recommendation in the README: SQLite per machine for v1; PostgreSQL for the
shared library later - but each machine keeps a local SQLite cache and syncs,
because a machining centre must keep running when the network is down. The
server must never become a runtime dependency of the cutting head.

### Seed data

20 profiles across Reynaers ML8 (its four real variants and published depths),
SlimLine 38, Schueco AWS 75, Alumil S67 and a generic PVC 70; 13 accessories from
Roto NX/NT, Siegenia Titan, GU, Maco; 12 parametric macros; 23 accessory
operations; 31 group assignments.

Structure is from the manufacturers' public pages. EVERY COORDINATE IS AN EXAMPLE
of mine - none from a fabrication manual. I did not invent authoritative-looking
machining figures: a fabricated hinge coordinate that looks researched is worse
than an obviously missing one, because someone may confirm it without checking.
Every seeded profile therefore has dxf_status='NONE' and no datums, so nothing
resolves to a cuttable placement at all.

### Pane upgrades (FloatingPaneHMI + new PaneHostControl)

Three states instead of two: Normal / Maximized / Minimized.

- MAXIMIZE fills the page; restore returns to the exact previous bounds. Only one
  pane can be maximised - two both filling the page leaves the lower one
  unreachable with no way to tell it is there.
- MINIMIZE parks the pane at the BOTTOM-LEFT, panes lining up left to right in
  minimise order and wrapping upward. The tray is the host's job: a pane cannot
  place itself there without knowing about its siblings.
- Maximise -> minimise -> restore returns to the FLOATING bounds, not the
  maximised ones.
- NAMED LAYOUTS per page ("profile entry", "dxf work"), saved under
  CommonAppData\KabanHMI\Layouts. Tray order is saved too, or parked panes
  reshuffle on every load.

18 checks pass against real rendered controls.

---

## 2026-09-04 - Data layer built on ADO.NET (not EF Core), tested against a real database

EF Core could not be compiled or run here, so every line of it would have
shipped unverified into the part of the program that decides where a cutter
goes. `Kaban.Profiles` is plain ADO.NET instead: 24 checks pass against a real
SQLite file (`Kaban.Profiles/DataLayerTests.cs`).

The cost is hand-written SQL, contained on purpose - all of it in `Data/`, none
in the screens, all inside the portable subset. `Db.cs` is the only file that
names an engine; switching to PostgreSQL is two lines plus a review of that one
folder.

### Two real bugs the live test caught

Neither was visible by inspection, and both would have reached a machine.

**Foreign keys were OFF.** SQLite disables them per connection unless asked, so
every cascade and reference in the schema was decorative - an orphaned
accessory_operation would have been accepted and then resolved onto nothing. The
test now inserts a profile into a non-existent group and requires a refusal.

**REAL columns were read at 32-bit precision.** Mono's SQLite reader returns a
System.Single, and Convert.ToDouble on a float widens the ERROR with the value:
0.87 came back as 0.870000004768372. That is the path every Y and Z COORDINATE
takes out of the database. 4e-7 mm is far below what a cutter can do, but a
datum that changes merely by being read is what makes two screens disagree and
costs a day to find. Db.Real() round-trips through the float's shortest
round-trippable text; all 12 coordinate reads now go through it, and numeric
parameters are bound with an explicit DbType on the way in.

### The safety rules are now code, not prose

ProfileResolver writes AUTO or PROPOSED and never CONFIRMED - only Confirm()
does, and it writes the previous value to placement_history first. Machines read
CuttablePlacements(), which returns CONFIRMED/OVERRIDDEN only. A missing datum
is an error naming the datum ("NX-CD-STD op1: needs HARDWARE_AXIS"). Re-resolving
cannot overwrite a confirmed row - that is a WHERE clause, not a convention.


---

## 2026-09-24 - Code review: bugs, leaks and performance (conservative), plus six UI requests

A review of the whole solution. Scope agreed: **fix bugs, leaks and performance
problems with minimal structural change**; nothing redesigned, no project or file
removed. Every fix carries a "why" comment where it is made. New tests:
`tools/core_test/ReviewFixes.cs`, `tools/ui_test/ReviewFixesUi.cs`,
`tools/ui_test/UnitLookAndErrors.cs`, `tools/ui_test/HandCursors.cs`,
`tools/ui_test/AboutManual.cs`, `tools/nav_test/History.cs` (each was run against
the original build too, and fails there on the bugs it covers).

### Changes that affect how the machine behaves - please review

- **Jog (momentary) bits are always released.** A momentary bit was cleared only
  on mouse-up, through the permission-gated driver. Now the bits a press actually
  set are released on mouse-up, loss of mouse capture, hide, disable, parent
  change, handle destruction, dispose, and when the button's enable condition
  drops - through the monitoring driver, so a logout mid-press cannot block the
  release. Right-click no longer starts a jog. (`ButtonHMI`)
- **Alarms are evaluated per station.** Every alarm address used to be read from
  station 1 and every definition evaluated against it, so H500/H800 alarms
  (remapped to stations 3/4, same M801... addresses) were raised and cleared by
  FA2010's bits. (`PlcScanner`, `AlarmManager.Evaluate(station, lookup)`)
- **Conditions are read from the station they are registered on**, fail-closed
  when that station has no data (SB3012's reset button: MillingUnitIsBack is X25
  on PLC2, and was read from PLC1).
- **F12 (PLC memory monitor) needs Admin**, like its menu entry. It writes any
  device of any station.
- **Page security is per page instance.** It was keyed by class, and every
  multi-machine page is a SettingsScreen - so they all shared whichever group's
  level registered last (Back/Forward could open Machine Parameters without
  Maintenance).

### Bugs
- Audit log viewer and "export today" read `yyyy-MM-dd.txt`; the logger writes
  `HmiAudit_yyyy-MM-dd.txt` - the viewer was always empty. The viewer's read also
  blocked the logger's append (lost lines). (`AuditLogger.TodayFile`)
- Alarm Settings **Save never saved** ("Nothing to save": the Load overload every
  module uses never recorded the path); would also have written every machine's
  alarms into one file, and matched by address across machines. Now per file,
  per scoped code; `Comment` kept; non-ASCII text written readable.
- `MD3020/Alarms.json` was in the old format, so MD3020 had no alarms; converted.
- Numeric entry on a Windows with decimal comma (TR/DE/RU/IT/ES) refused every
  decimal; the write path parsed with thousands separators ("12.5" -> 125); hex
  fields were handed their decimal value. (`NumpadForm`, `NumericInputHMI`,
  `InputForm`)
- `ConnectionMonitor` used local time: the autumn clock change kept a dead
  station "live" for up to an hour. Now UTC, and thread-safe (32-bit torn reads).
- Back/Forward: restored only the outer page (no-op when already shown), found its
  window through `FindForm()` (null once away), and the machine strip always marked
  the main machine. A step is now a location: page + every tab on the way down.
  Identical steps are not recorded twice; login/language rebuilds add no steps.
- Users: a `;` in a name corrupted Users.dat; the last Admin could be demoted; a
  damaged account threw out of the login.
- Delta Modbus: a write the device refused was reported as success.
- `TroubleshootingCatalog.Resolve(fileName)` shadowed `Resolve(code)` - renamed
  `ResolveLanguageFile`; four help tests compile again.
- `MainForm.LoadEmbeddedImage` returned an Image whose stream was disposed.
- The scan worker could throw out of a pool thread (process exit) or leave
  `_scanBusy` set (screen frozen).

### Leaks and performance
- Kernel wait handle per driver call never disposed (`BoundedDriver`, `StaWorker`).
- Static `LanguageChanged` lambdas that could never be removed kept every
  `BaseNumericHMI`, `ButtonHMI`, `LabelHMI` (and their pages) alive.
- Per-click menus (caption bar, alarm help, column chooser) never disposed; one
  new Font per menu, per tab switch, per bold toggle, per readout size.
- Version device read on every 100 ms scan -> once a second.
- Alarm list refreshed once per alarm -> once per scan.
- Alarm help lookups probed the disk on every cell paint -> 2 s cache.
- Login icon re-tinted pixel by pixel on every paint -> cached.
- Condition checks: `Enum.GetValues` + boxing `HasFlag` per control per scan.
- Settings files (users, units, PLC connections, imports, Alarms.json) are
  written all-or-nothing (`SafeFile`), so a power cut cannot leave them empty.
- Npgsql connect timeout 15 s -> 3 s (checked from the UI thread).

### UI requests (2026-09-23)
1. Back/Forward load reliably and the machine strip marks the machine shown.
2. `AxisUnitHMI` position looks like the Axis Settings row (black bold on the
   read-only grey); only its border blinks red on an axis error.
3. Axis and function units render the same frame at the same size: the "?"
   no longer takes width from the sides; axis slots scale with the unit.
4. The "?" sits on the top frame line at the right end (the "i" moves left of
   it); the failing button's (cylinder) or the position's (axis) border blinks red.
5. Hand cursor on every enabled button/combo in every window, the caption bar
   and all dropdown menu items; disabled items show the arrow.
6. HMI Settings "Manual" tab removed; About has "Open the HMI manual", which opens
   `Help/Kaban_HMI_Manual.<lang>.html` (English shipped; add `.tr.html` etc.).

### Removed (dead code only)
`MainForm.PulseAlarmReset`, `DrawResetGlyph`, `DrawLanguageItem`, `_navSavedText`;
`MachineAlarmScreen.ResetAlarms`; `NumericInputHMI` prompt text,
`FormatCurrentValueForEdit`, `FormatLimit`; `ButtonHMI.Str`;
`HmiManualControl`/`HmiManualPage` (replaced by the HTML manual).

## 2026-09-24 (afternoon) - Axis code help, alarm menu reset, PLC Monitor, maximise and close

Requests of 24 September. New tests: `tools/core_test/AxisCodes.cs`,
`tools/ui_test/AxisCodeHelpUi.cs`, `tools/ui_test/PlcMonitor.cs`; `AboutManual`
and `UnitLookAndErrors` extended.

### Changes that affect how the machine behaves - please review

- **"Reset (n) Active Alarms" (caption-bar alarm menu) pulses the machine's alarm
  reset**, 300 ms, on the machine's own controller, then clears the list. The bit
  is the machine's `PC_AlarmReset` label, else M102 - unless the machine's label
  list gives M102 another job: H500's M102 is `MAN_Lubrication`, so H500 is not
  pulsed and the menu says why. Needs Operator; when nothing could be sent the
  list is still cleared and a message says the machine was not reset. The release
  is retried and logged if it fails. (`Helpers/AlarmReset.cs`, `MainForm`)
- **A button whose actions are all momentary does nothing on Click** - it used to
  ask "Are you sure?" after the jog when `ConfirmationNeeded` was set.
  `ConfirmationNeeded` is documented as not applying to jogs. (`ButtonHMI`)
- **Closing**: only the caption-bar X opens the Close / Restart menu; the taskbar,
  Alt+F4 and Windows shutdown close at once. (`MainForm`)

### Axis codes
- `tools/build_code_help.py` builds `Help/Servo_Alarm_Codes.en.html` (MR-J4 1.4/1.5),
  `Axis_Warning_Codes.en.html` and `Axis_Error_Codes.en.html` (FX5-40SSC-S 12.4/12.5)
  from the manuals' tables, one `<section class="code">` per code.
  `Help/FX5_40SSC_Troubleshooting.pdf` is chapter 12 of the FX5 manual.
- `Kaban.Core/Helpers/AxisCodeHelp.cs`: formats (`16.3`, `1904H`), finds, names and
  extracts one code's entry into a page of its own (with a `<base>` so the PDF
  link works from the temp copy). `TroubleshootingCatalog.HelpDocument.Code`.
- The axis unit's "?" follows the three code words (shown while any is non-zero,
  one menu entry per code, amber for warnings); the error bit still blinks the
  position. `AlarmHelpBadge.SetCodeWords`, `FunctionUnitHMI.SetAxisCodes`,
  `IPlcExtraAddresses` (scanner reads the words).
- Axis Settings code cells print each code as its manual does and open it on click
  (`HexCodeHMI.CodeKind`). The alarm list's help for an axis alarm reads the codes
  and lists their entries first (`AlarmHelpLinks.CodeDocuments`).

### Look and manual
- Function units' position drawn like Axis Settings (black bold on grey) by default.
- The manual names no machine; About composes it for the machines running, with
  each machine's own chapter (`Help/Manual_<ModuleId>.<lang>.html`).
- Maximise fills the monitor the window is on (was thrown off-screen on an
  extended display).
- Manual Movement / Cutting page back to the original.

### PLC Monitor
- Own tabs per controller (new/rename/delete/move); devices of the screen on
  display, of the whole program (one tab, or one tab per page) or of the label list
  added in one go (`Helpers/DeviceCollector.cs`); arrays with sequential addresses;
  copy/paste above the selected row or at the end; comment lines; double-click a
  value to edit it in place, anything else to open the editor on that field;
  toolbar of emoji buttons; double words' high words read on real PLCs and not
  listed separately on the simulator; only the visible tab refreshed. Old list
  files open as one tab "Main".

## 2026-09-24 (evening) - Alarm reset per machine; PLC Monitor fixes

### Alarm reset
- `PlcManager.SetAlarmResetDevice(station, address)` / `AlarmResetDevice(station)`:
  each machine's module declares its reset bit, like the version device.
  `FA2010Module` declares M102 on PLC1; H500, H800, MD3020, SB3012 and SB3015 have
  the line marked and commented out until their addresses are known.
- `AlarmReset.AddressFor` uses only the declared bit (no label lookup, no M102
  default). The Alarms page Reset button is bound to it; without one it is grey and
  says why. `Msg_ResetNoBit` reworded.

### PLC Monitor
- The tab on display is found from the selected page (`TabPage.Tag`), never by
  position; pages inserted after the strip has a window; the tab list re-read
  from the strip after every change. Fixes: devices added to an empty tab went to
  the previous tab; deleting a tab also deleted the previous one.
- Own `ToolTip` (the shared one never shows over the monitor window); Export and
  Import buttons (`Tip_MonExport`, `Tip_MonImport`).
- Right-click keeps a multi-selection.
- New tab inserted before the tab on display; every tab can be deleted
  (`#tabs:N` line in the list file; `Msg_MonNoTabs`); tabs dragged along the strip.
- Columns: fixed widths, re-orderable headings, the same layout on every tab, kept
  in `Monitor\Columns.cfg`; click a heading to sort (section by section under
  comment lines, PLC address order, values numeric).
- "LIVE PLC" banner and status text removed (`Msg_LiveStation`, `Lbl_LivePlc`
  deleted; `Msg_MonStatus` has one argument less).
- Tests: `tools/ui_test/PlcMonitor.cs` (64 checks), `tools/ui_test/AxisCodeHelpUi.cs`
  reset section rewritten (40 checks).

## 2026-09-24 (later) - Cylinder help by valve and feedback, one "?", login list, Cutting page sizes

- `tools/build_code_help.py`: the MR-J4 tables without their "Target" column;
  `Help/Servo_Alarm_Codes.en.html` regenerated.
- `LoginForm`: accounts in a drop-down list, the last user (`UserStore.LastUser`,
  `LastUser.txt`, written by `SecurityManager.TryLogin`) preselected, focus in the
  password; `LoginForm(chooseUser: true)` for Change user opens the list.
- `FunctionUnitHMI`: `ValveType` / `FeedbackType` (the former `Valve` / `Feedback`
  kept hidden); `CylinderValve` members renamed `DoubleSolenoid_5_2`,
  `SingleSolenoid_5_2`, `DoubleSolenoid_5_3` (values unchanged). `MovementFor(side)`,
  `HelpDocuments()`. One `UnitHelpBadge` (blue / red / amber) replaces the "i"
  (`UnitInfoBadge`, removed) and the visible `AlarmHelpBadge`s, which are now
  `Watcher`s of the error bits and drive codes.
- `TroubleshootingCatalog`: `MovementInfo`, `HelpDocument.Movement`; `Tailor` joins
  the detail tables into one chart (`JoinBlocks`), drops the kept variant's number
  and heads the copy with the failed movement's devices. `AlarmHelpLinks.Link.Movement`
  so the alarm list's help names the movement too.
- `ManualCutting`: 20 units 360 x 64 on the Milling page's grid.
- Tests: `tools/ui_test/CylinderHelpAndLogin.cs` (34 checks); `UnitHelpLinks`,
  `UnitLookAndErrors`, `AxisCodeHelpUi`, `UnitInfoAndAxisKind` follow the one "?".

## 2026-09-24 (evening) - PLC Monitor: delete menu, undo, find, one-list collect, keys

- Toolbar: 🔼 🔽 for move up/down; 🗑 opens a menu (selected rows / this tab / every
  tab of this controller / of every controller - `DeleteAllSheets`); ↩ Undo;
  🗃 add devices; 🔍 Find; 👁 show removed devices again.
- Undo: `Remember(what, monitors)` keeps `Serialize(mon)` before every list change;
  `Undo()` restores (`RestoreState`), 50 steps; Ctrl+Z.
- "The whole program - into this tab": with a heading per page, or one list without
  headings (`CollectFromProgram(perPage, headings)`). The label-list entry explains
  PlcLabels.csv.
- Find: `DeviceCollector.UsesOf(station)` (every use, with `PagePath` and
  `ControlPath` to the outermost unit; cached), `DeviceCollector.Locate`,
  `MainForm.ShowDeviceUse`, `SettingsScreen.ViewOf`, `Forms/DeviceFindForm.cs`,
  `Helpers/ControlHighlight.cs` (click-through blinking blue frame).
- Keys: Enter = open cell, Tab leaves the grid (`StandardTab`), tab strip Del / F2 /
  Ins / Ctrl+Left/Right, Ctrl+Z, Ctrl+F. A cancelled edit no longer stops the refresh.
- Screen titles in the device lists use the screen's own wording when a key has no
  translation (was the bare key).
- Tests: `tools/ui_test/PlcMonitor.cs` (undo, delete menu, keys, toolbar),
  `tools/ui_test/MonitorFind.cs` (Find on the real FA2010 program, page opened,
  unit framed).

## 2026-09-24 (night) - Unit help list names, blinking code cells, tooltips, demo types

- The unit's "?" list does not repeat the unit or axis name (`AlarmHelpBadge.Documents`,
  `FunctionUnitHMI.CylinderDocument` / `HelpDocuments`).
- `HexCodeHMI`: no corner "?"; the digits of a code with help blink (one shared timer).
- `UiStyle` tooltips: `ShowAlways = true` - the shared tooltip belongs to whichever
  window its first control was on, and did not show on the main window.
- Demo `ValveType` / `FeedbackType` on nine FA2010 units (see REVIEW 4f).
- Tests: `AxisCodeHelpUi` (blinking, titles), `CylinderHelpAndLogin` (titles, demo types).

## 2026-09-25 - Unit widths, steady "?", designer Help properties, condition sets, PDF copies

- `AxisUnitHMI` no longer sets slot widths; `FunctionUnitHMI` sizes every position
  display to "000000.0 mm" (one width for all units, axis units included).
- `UnitHelpBadge`: no blinking (red / amber steady).
- `FunctionUnitHMI.Kind`: hidden, not serialized. Categories: " Help" and the other
  custom categories take the leading space.
- `HelpFilesEditor`: a list dialog (add several, remove, reorder); language suffix dropped
  from stored names.
- `FunctionUnitHMI` / `GroupBoxHMI` Visibility/EnableConditions use
  `UnifiedConditionEditor` + `ConditionTextConverter`. `MachineConditionMap.Parts /
  NameOf / Unmet`; ButtonHMI and NumericInputHMI name only the unmet conditions.
- `AlarmHelpLinks.ViewerCopy`: help PDFs opened from `%TEMP%\KabanHMI_Help`.
- Tests: `tools/ui_test/UnitsAndDesigner.cs`; `MonitorFind` checks the real Robot Top
  Clamping's tailored manual.

## 2026-09-25 (afternoon) - PLC Monitor: "+" tab, redo, drawn pictures, window places

- `SimulationMonitorForm`: a "+" button after the last sheet tab (`PlaceAddTab`, follows
  renames, scrolling and the empty state); `TabPlace` Before / After / End and
  `AddSheetAt`; tab menu items "after this one" and "at the end".
- Redo: `_redo` stack filled by Undo, emptied by any new change (`Remember`); button beside
  Undo; Ctrl+Y and Ctrl+Shift+Z.
- `ToolGlyphs` *(new)*: drawn undo, redo, move up / down (arrow to a bar), copy, paste;
  `UiStyle.IconButton` gives them the emoji buttons' size, frame and hover. Find uses
  U+1F50E.
- `WindowLayout` *(new)*: window bounds (checked against the screens) and ListView column
  widths / order in `Monitor\Windows.cfg`. The monitor's minimum size is 300 x 260 (the
  toolbar overflows); Find's 360 x 220, its columns re-orderable.
- `MainForm.ShowSimulationMonitor` places the monitor only when it has no kept place.
- Tests: `PlcMonitor.cs` 110 checks.

## 2026-09-25 (evening) - Side panel, Inputs / Outputs, machine side pages, magnifying factor

- `MainForm.SidePanel.cs` *(new)*: the right-hand side bar (`_sideBar`, fold to a 14 px edge), the side
  panel docked between the caption and the menu with its `Splitter`, `ShowSidePage` / `ToggleSidePage` /
  `CloseSidePanel`, pop-out into `SidePageWindow` and back, `SidePanel.cfg` (width at factor 1,
  bar folded, pages kept in a window). Pages are built once and hidden, never closed.
- `SidePages` *(new)*: registry of side pages; built-ins `IoMonitor` (Maintenance) and `PlcMonitor`
  (Admin); machine pages via `ScreenRoles.SidePage` (excluded from `NavigationGroups`,
  `MachineRegistry.SidePages()`).
- `SimulationMonitorForm(bool embedded)`: a non-top-level form in the panel or a side window; Find embedded
  as a pane (`DeviceFindForm(..., embedded, close)`), dialogs owned by the window it is in, no reading
  while hidden, `SaveLists()`. `MainForm.ShowSimulationMonitor()` returns it; F12 toggles.
- `ControlHighlight`: four bars on the target's container instead of a layered top-most window.
- `PlcScanner.AddTarget / RemoveTarget`: the side panel and side windows are read with the page.
- `IoMonitorPage`, `IoListHMI` (one control, many points, `IPlcExtraAddresses`), `IoList` *(new)*;
  `Kaban.Machines.FA2010/IoList.csv` from `tools/build_io_list.py` *(new)*.
- Magnifier: `DisplaySettings` (Core, `Display.cfg`: Panel, Start, User=<name>;<factor>), `UiScale`
  (Apply once per tree: `Control.Scale`, own fonts, grid/list columns, toolbar items; watches later
  additions; re-runs resize handlers; keeps windows on screen), `UiScale.ApplyOnLoad` in the forms,
  `UiStyle` fonts and tooltips at the factor, `UiStyle.IconButton` glyphs scaled, runtime sizes in
  `MainForm` / `MachineSelectorStrip` through `UiScale.For`, nav width kept at factor 1.
  `LoginHandoff` (DPAPI, one minute, one use) and `MainForm.RestartWithFactor`; offer at sign-in
  (`MagnifierToOffer`); User Settings: panel factor and a factor per user (removed with the user).
- `LanguageManager`: `\r\n` / `\n` written in a translation become line breaks.
- Tests: `tools/ui_test/SidePanel.cs`, `tools/ui_test/Magnifier.cs`.
