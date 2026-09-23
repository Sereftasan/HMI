# Profile & accessory system — design

**Status: schema written and executed; UI not built.**
`docs/profile_db/` now holds a working SQLite schema, a reference resolver and a
15-check test suite that all pass. The design below is what those implement.

**Revised for GROUP-BASED assignment** after your follow-up: accessory positions
are now defined once per *profile group x accessory group* as a RULE against
named datums, so a profile added to a group inherits every placement
automatically from its own DXF. The earlier per-pair table is kept only as the
override layer.

I could not read the shared ChatGPT conversation — the page renders its content
client-side, so only the title came through ("Profil Aksesuar Veritabanı
Tasarımı"). If anything was settled there that contradicts this, send me that
part as text and I will rework it.

---

## 1. The problem, restated

A macro (water drainage slot, handle pocket, hinge holes) has to be placed at a
**Y and Z position on the profile**. Those positions change with:

- the **profile** — vendor, series, type (frame / sash / mullion), and its own
  height/width/cross-section;
- the **accessory** — vendor and series, since a Roto hinge and a Siegenia hinge
  sit differently even on the same profile.

So Y/Z is not a property of the profile, nor of the accessory. **It is a property
of the pair.** That single observation decides the whole schema: if you store Y/Z
on either side alone you will be re-typing the same numbers forever.

The hard requirement you set — *"values appointed approximately according to
their groups, edited easily for first trials, real production as soon as
possible"* — matters more than schema elegance. A design that is correct but
needs 400 rows typed before the first cut is a failed design.

---

## 2. Proposed structure: three layers, with inheritance

### Layer 1 — Profile catalogue

```
ProfileVendor      id, name                                   (Deceuninck, Rehau, …)
ProfileSeries      id, vendor_id, name, material              (PVC | ALU)
Profile            id, series_id, code, name,
                   type,                                      (Frame|Sash|Mullion|Transom|Bead)
                   width_mm, height_mm,
                   hardware_axis_y, hardware_axis_z,          ← the datum, see §3
                   glazing_ref_y,  glazing_ref_z,
                   dxf_path, dxf_status                       (None|Imported|Confirmed)
```

### Layer 2 — Accessory catalogue

```
AccessoryVendor    id, name                                   (Roto, Siegenia, …)
AccessorySeries    id, vendor_id, name
Accessory          id, series_id, code, name,
                   type,                                      (WaterSlot|Handle|Hinge|Lock|Striker)
                   macro_id,                                  → the G-code macro to run
                   default_dy, default_dz                     ← OFFSET from the datum
```

Note `default_dy/dz` are **offsets from the profile's hardware axis**, not
absolute coordinates. A handle is "22 mm above the hardware axis" on almost
every frame profile ever made; storing it that way means one number covers a
whole vendor's range instead of one number per profile.

### Layer 3 — The pair, and only where it differs

```
AccessoryPlacement id, profile_id, accessory_id,
                   y_mm, z_mm,                                ← resolved values
                   source,                                    (Inherited|GroupDefault|Measured|Confirmed)
                   entry_speed, milling_speed,                ← nullable, see §5
                   note, edited_by, edited_at
```

**This table starts empty.** Rows appear only when someone overrides something.

### Resolution order (this is the important part)

When the machine asks "where does accessory A go on profile P?", resolve in this
order and **stop at the first hit**:

1. `AccessoryPlacement` row for exactly (P, A) — `Confirmed` or `Measured`
2. Group default for (P.type, A.type) within the same profile series
3. Group default for (P.type, A.type) across the vendor
4. `Accessory.default_dy/dz` applied to `P.hardware_axis`
5. Nothing → refuse to run the macro, and say which of the four was missing

Step 4 is what gets you cutting on day one: a newly added profile with a correct
hardware axis inherits *every* accessory position immediately, approximately
right. Steps 1–3 are how those approximations get corrected, one number at a
time, as trials happen.

Every resolved value carries its `source` to the screen. An operator seeing
**"Y 35.0 (inherited)"** treats it differently from **"Y 34.2 (confirmed)"** —
and that distinction is exactly what makes it safe to ship approximations.

---

## 3. The datum problem — read this before agreeing to anything

Everything above depends on **one agreed origin per profile**. If the hardware
axis is measured from the outside face on one profile and the rebate on the
next, the inheritance is worse than useless: it will be confidently wrong.

I would fix the datum as:

- **Y = 0** at the **outside surface**, positive toward the inside
- **Z = 0** at the **bottom of the profile as it sits on the machine bed**
- **Hardware axis** = the Euro-groove centreline, as the single reference all
  accessory offsets are measured from

That matches the markers you already sketched (`OUTSIDE_SURFACE`,
`INSIDE_SURFACE`, `HARDWARE_AXIS`, `GLAZING_REFERENCE`).

**This is the one decision I would not let software make.** It has to be written
down, drawn on a reference sheet, and agreed with whoever measures profiles —
before the first fifty rows exist. Changing it afterwards invalidates every
stored number.

---

## 4. The three screens

### 4.1 Profile browser
Tree by vendor → series → type, list on the right. Search by code. Nothing
clever, but it is where people will spend their time, so it needs to be quick.

### 4.2 Profile editor
Two panes: the numbers on the left, **the DXF drawn on the right with the four
datum points marked on it**. The drawing is what makes an error obvious — a
hardware axis typed as 35 when it should be 53 is invisible in a text box and
unmissable on a cross-section.

### 4.3 Placement matrix — the one that decides whether this is usable
Rows = accessories, columns = profiles (or the reverse), cells = Y/Z:

- **colour-coded by `source`**, so inherited vs confirmed is visible at a glance;
- **editable in place**, like the PLC monitor grid — an engineer correcting ten
  positions after a trial should not open ten dialogs;
- **CSV export/import**, same as the monitor. First commissioning is realistically
  someone filling a spreadsheet at a desk, and pretending otherwise just means
  they do it anyway and retype it.

---

## 5. Feed and speed from DXF layers

You want milling entry/feed speed to come from the profile's DXF layers rather
than hand-editing each macro.

Workable, with one caveat. What a DXF layer honestly tells you is **geometry** —
which contour is outer wall, inner wall, reinforcement chamber. What it cannot
tell you is the **material and wall thickness** at that point, which is what
actually sets the feed. So:

```
MillingClass    id, name,               (PVC_OuterWall, PVC_Chamber, ALU_3mm, …)
                entry_speed, feed_speed, spindle_rpm, plunge_rate

ProfileLayerMap id, series_id, dxf_layer_name, milling_class_id
```

Map layer→class **once per series** (not per profile — a vendor's layer naming is
consistent within a series), then every profile in that series gets its feeds
automatically. `AccessoryPlacement.entry_speed/milling_speed` stays as the
per-case override for the one pocket that always chatters.

This keeps the speeds in a table an engineer can reason about, rather than
inferred by software from geometry it cannot fully see.

---

## 6. DXF import — what is reliable, and what is not

Your instinct to put marker text in a sample DXF per profile group is a good one.
I would split the import into three stages by **confidence**, because they are
genuinely different problems:

### Stage A — marker text (reliable, build first)
Read TEXT/MTEXT entities matching `OUTSIDE_SURFACE 0 0`, `HARDWARE_AXIS 22 35`,
and take the coordinates. This is parsing, not recognition. It is deterministic
and testable, and if the CAD side is disciplined about placing the markers it
solves most of the problem on its own.

### Stage B — geometric inference (mostly reliable)
With no markers: bounding box gives width/height; the outermost closed polyline
gives the outer contour; the largest interior chamber is usually the
reinforcement. Good enough to **propose** values.

### Stage C — "recognise it like a human would" (where I would push back)
A model that looks at an unlabelled cross-section and decides where the hardware
axis is, is a real project — training data, validation, failure modes — and its
failure mode is silent: a plausible-looking wrong number that gets cut into a
customer's profile.

My recommendation: **do not start here.** Do A and B, and have the editor
*propose* values that a human confirms against the drawing (§4.2). Then, once
there are a few hundred confirmed profiles, that corpus is exactly the training
set that makes C tractable — and you will have gotten the data by doing useful
work rather than by a labelling exercise.

If you want C sooner, the honest framing is: it proposes, a human always
confirms, and `dxf_status` never reaches `Confirmed` without a person. The AI
never silently writes a cutting coordinate.

---

## 7. Storage: SQLite, not CSV

The rest of the HMI uses CSV, and for label lists that is right. This is
different — it is relational (four foreign keys deep), it needs transactions, and
two people will eventually edit it. I would use **SQLite**: one file, no server,
copies with the machine, and `System.Data.SQLite` works on .NET 4.7.2 in both
bitnesses.

Keep CSV **import/export** for every table anyway, for exactly the spreadsheet
reason in §4.3.

---

## 8. Suggested order of work

| # | Deliverable | Why first |
|---|---|---|
| 1 | Agree the datum (§3) on paper | Everything else depends on it, and it is not a software decision |
| 2 | Schema + SQLite layer + CSV import/export | Nothing can be entered until this exists |
| 3 | Profile browser + editor, no DXF | Data entry becomes possible |
| 4 | Placement matrix with inheritance + `source` colouring | This is the screen that makes trials fast |
| 5 | DXF viewer in the editor (render only) | Makes bad numbers visible; reuses the existing DXF code |
| 6 | DXF Stage A — marker parsing | Removes most manual entry |
| 7 | Layer→MillingClass map, feeds | Removes per-macro editing |
| 8 | DXF Stage B — geometric proposals | Diminishing returns, still useful |
| 9 | Stage C, only if 1–8 are in production | Needs the corpus from 1–8 |

Steps 1–4 are what get you to real production soonest; 5–7 are where the effort
stops being data entry.

---

## 9. What I need from you before writing code

1. **The datum** (§3) — confirm or correct it.
2. **Is `AccessoryPlacement` really the pair?** I have assumed hinge position
   depends on both profile and hinge brand. If in practice it depends only on the
   profile, the schema gets simpler and I would rather know now.
3. **Roughly how many** profiles and accessories at first commissioning — 50 or
   5000 changes whether the matrix screen needs virtualisation.
4. **A sample DXF** with your markers as you would actually draw them. Stage A is
   only as good as its match with real files, and I would rather parse a real one
   than invent a format you then have to conform to.
5. **Does an accessory ever need more than one Y/Z** (a hinge with two screw
   positions)? If so `AccessoryPlacement` needs a child point table, and that is
   much cheaper to decide now than to retrofit.

---

# 10. REVISION — group-based assignment (what is actually built)

The version above stored Y/Z per profile-accessory PAIR. That is still right as
an *override*, but it is not enough for what you asked next: a profile added to a
group should get its accessories **automatically, from its DXF**.

## What changed

Placement is now a **rule**, written once per (profile group x accessory group):

> *handle group* sits on the **HARDWARE_AXIS**, offset +0 Y, +35 Z, CENTRED along
> the bar

A rule holds no profile-specific number, so it survives every new profile. When
one is added, its own datums are read from its DXF and the rule is evaluated
against them.

**The proof, from the test output:**

```
Roto NT handle    Y=22.0   Z=76.0   AUTO (CENTRED)
```

The reference profile's hardware axis is at Z=35, which would give 70. The new
profile's axis is at Z=41, so the handle resolves to **76**. The placement
followed the new profile's geometry rather than copying a number. That is the
whole design in one value.

## The four resolution layers, in order

| # | Source | When it applies |
|---|---|---|
| 1 | `accessory_placement` with status `CONFIRMED`/`OVERRIDDEN` | a person has decided; nothing overrides this |
| 2 | `placement_rule` for this profile group x accessory group | the normal path |
| 3 | `placement_type_default` for (profile type x accessory type) | a brand-new vendor with no rules yet |
| 4 | nothing | refuse, and say which datum was missing |

Layer 3 is what makes a new vendor useful on day one: the test shows a frame
profile with **no group rules at all** still receiving its drainage placement.

## Safety, which is the part I would not compromise on

- **Production reads a view, not the table.** `v_placement_effective` exposes
  only `CONFIRMED` and `OVERRIDDEN`. A value computed from an AI-extracted datum
  that nobody has reviewed **cannot reach a cutter** — and because it is a view
  rather than a convention, a future caller cannot forget it.
- **AI-derived placements are born `PROPOSED`**, carry their confidence, and
  queue in `v_placement_review` worst-first.
- **Deviation from the group's reference profile is recorded** on every
  placement. A profile whose datums sit far from its group's reference is
  probably in the wrong group — much better caught here than after a cut.
- **Every change is audited** to `placement_history` by trigger.

## What did NOT change

Sections 3 (the datum), 5 (feeds from layers), 6 (DXF stages A/B/C) and 9 (the
five questions) all stand. Section 3 especially: **the coordinate datum is still
the one decision that has to be made on paper before rows exist**, and this
revision makes it more load-bearing, not less — every rule is now expressed
against those datums, so getting them inconsistent corrupts every group at once
rather than one profile at a time.

