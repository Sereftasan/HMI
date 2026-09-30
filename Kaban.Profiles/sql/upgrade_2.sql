-- =====================================================================
--  SCHEMA 2  (30 September 2026) - what the profile page needs beyond v1
--
--  Portable ALTERs only (ADD COLUMN with a default), so a library created by
--  the first version is upgraded in place, and SQLite and PostgreSQL read the
--  same file.
-- =====================================================================

-- WHICH FACE an operation is machined from. The section's Y/Z say WHERE on the
-- profile; this says from which side the tool comes in:
--   TOP     tool from above, going down (-Z)      the usual hardware groove
--   BOTTOM  from below, going up (+Z)
--   OUTSIDE from the outside face (Y = 0), going in (+Y)   drainage slots
--   INSIDE  from the inside face, going out (-Y)
-- The operation's Y/Z is then the point where the tool meets the profile.
ALTER TABLE accessory_operation ADD COLUMN entry_side TEXT NOT NULL DEFAULT 'TOP';

-- How deep: 0 = the macro's depth_mm from the entry point; 1 = through the
-- first wall it meets; 2 = through the whole profile. (through_profile in
-- macro stays the macro's own advice; this is what the operation does.)
ALTER TABLE accessory_operation ADD COLUMN depth_mode INTEGER NOT NULL DEFAULT 0;

-- How the DXF is turned to read in profile coordinates (outside at Y = 0 on
-- the left, the bed at Z = 0 below): quarter turns clockwise, then a mirror.
ALTER TABLE profile ADD COLUMN dxf_rotation INTEGER NOT NULL DEFAULT 0;
ALTER TABLE profile ADD COLUMN dxf_mirror INTEGER NOT NULL DEFAULT 0;
-- DXF layers that are not the profile (dimensions, hatching, a gasket drawn
-- beside it): comma separated. They are drawn, but not cut through.
ALTER TABLE profile ADD COLUMN ignored_layers TEXT;
ALTER TABLE profile ADD COLUMN note TEXT;

-- Which milling unit a mounted tool is on (FA2010: two units, two tools each).
ALTER TABLE mounted_tool ADD COLUMN unit_no INTEGER NOT NULL DEFAULT 1;
ALTER TABLE mounted_tool ADD COLUMN note TEXT;

-- Which angle of a unit machines which face. Machine layer: the profile does
-- not care which way the head turns.
CREATE TABLE machine_face (
    id              INTEGER PRIMARY KEY,
    instance_id     INTEGER NOT NULL REFERENCES machine_instance(id) ON DELETE CASCADE,
    unit_no         INTEGER NOT NULL,
    entry_side      TEXT NOT NULL,                         -- allowed: TOP,BOTTOM,OUTSIDE,INSIDE
    angle_deg       REAL NOT NULL,
    is_enabled      INTEGER NOT NULL DEFAULT 1,
    note            TEXT,
    UNIQUE (instance_id, unit_no, entry_side)
);

-- The END milling program (the contour milled around the profile's end, drawn
-- on the Drawing pane) - per profile and machine. It used to be a column of the
-- old flat profile table.
CREATE TABLE end_mill_program (
    id              INTEGER PRIMARY KEY,
    profile_id      INTEGER NOT NULL REFERENCES profile(id) ON DELETE CASCADE,
    instance_id     INTEGER REFERENCES machine_instance(id) ON DELETE CASCADE,
    tool_radius_mm  REAL,
    cut_depth_mm    REAL,
    gcode           TEXT,
    updated_at      TEXT NOT NULL,
    updated_by      TEXT,
    UNIQUE (profile_id, instance_id)
);

-- Machine-wide settings of the side machining program (safe height, approach
-- feed, program form): key/value per machine instance, like parameter_value.
CREATE TABLE machine_setting (
    id              INTEGER PRIMARY KEY,
    instance_id     INTEGER NOT NULL REFERENCES machine_instance(id) ON DELETE CASCADE,
    code            TEXT NOT NULL,
    value           TEXT,
    UNIQUE (instance_id, code)
);
