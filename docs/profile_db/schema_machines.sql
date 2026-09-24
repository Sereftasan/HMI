-- =====================================================================
--  KABAN - MACHINE SCHEMA
--
--  The other half of the split. `schema_v2.sql` describes PROFILES and
--  ACCESSORIES - facts true of a Reynaers frame whoever is cutting it. This
--  file describes MACHINES: what they are, what they can do, and the
--  per-machine numbers that turn a profile into a job.
--
--  Both live in ONE database. Machines share the library and own their own
--  rows here.
--
--
--  THE PROBLEM THIS SOLVES
--  -----------------------
--  Kaban builds nine PVC machine categories plus an aluminium range: machining
--  centres, cutting, welding, corner cleaning, mullion/cleaning units, milling,
--  reinforcement screwing, assembly, and welding+cleaning centres.
--
--  They need overlapping but different data. A corner cleaner needs knife
--  depths and a cleaning path; a welder needs plate temperature, weld and cool
--  times; a saw needs blade angles. All of them need the profile.
--
--  The obvious design - a table per machine category - is what makes adding a
--  machine a schema migration, and a migration means a release, on every panel
--  PC already in the field. Nine categories become fifteen, each with a table
--  nobody outside that machine ever queries.
--
--
--  WHAT IS DONE INSTEAD
--  --------------------
--  A machine declares CAPABILITIES. A capability owns a set of PARAMETER
--  DEFINITIONS - name, unit, type, default, range - which are DATA. A new
--  machine series is then a few INSERTs, not a migration:
--
--      machine_series       'SB3012'
--      series_capability    SB3012 does CUTTING
--      parameter_definition CUTTING has blade_angle_deg, kerf_mm, ...   (already there)
--      parameter_value      per profile, per capability
--
--  Adding a capability nobody has built before is still just INSERTs into
--  capability + parameter_definition.
--
--  THE HONEST TRADE-OFF: parameter_value is a key/value table, and key/value
--  tables lose column typing and are clumsy to query in bulk. That is accepted
--  HERE and nowhere else, because these values are read a screenful at a time
--  for one profile on one machine - never aggregated across thousands of rows.
--  Where data IS queried in bulk and its shape is stable and shared - the
--  profile library - it stays strongly typed. Using key/value for everything
--  would be much worse than either.
-- =====================================================================

PRAGMA foreign_keys = ON;

-- =====================================================================
-- 1. THE MACHINE CATALOGUE
-- =====================================================================

-- The nine PVC categories and the aluminium range, as Kaban sells them.
CREATE TABLE machine_category (
    id          INTEGER PRIMARY KEY,
    code        TEXT NOT NULL UNIQUE,
    name_tr     TEXT NOT NULL,
    name_en     TEXT NOT NULL,
    branch      TEXT NOT NULL              -- allowed: PVC,ALU,BOTH
);

INSERT INTO machine_category (id, code, name_tr, name_en, branch) VALUES
 ( 1,'MACHINING_CENTRE','Isleme Merkezleri',                 'Machining Centres',            'BOTH'),
 ( 2,'CUTTING',         'Kesim Makineleri',                  'Cutting Machines',             'BOTH'),
 ( 3,'WELDING',         'Kaynak Makineleri',                 'Welding Machines',             'PVC'),
 ( 4,'CORNER_CLEANING', 'Kose Temizleme Makineleri',         'Corner Cleaning Machines',     'PVC'),
 ( 5,'MULLION_CLEANING','Orta Kayit ve Temizleme Uniteleri', 'Mullion & Cleaning Units',     'PVC'),
 ( 6,'MILLING',         'Freze Makineleri',                  'Milling Machines',             'BOTH'),
 ( 7,'REINF_SCREWING',  'Destek Saci Vidalama Makineleri',   'Reinforcement Screwing',       'PVC'),
 ( 8,'ASSEMBLY',        'Montaj Uniteleri',                  'Assembly Units',               'BOTH'),
 ( 9,'WELD_CLEAN_CENTRE','Kaynak ve Temizleme Merkezleri',   'Welding & Cleaning Centres',   'PVC');

-- A product line: FA2010, SB3012, H500 ... The HMI build corresponds to one.
CREATE TABLE machine_series (
    id              INTEGER PRIMARY KEY,
    category_id     INTEGER NOT NULL REFERENCES machine_category(id),
    code            TEXT NOT NULL UNIQUE,      -- FA2010, SB3012, H500
    name            TEXT NOT NULL,
    branch          TEXT NOT NULL,             -- allowed: PVC,ALU,BOTH
    axis_count      INTEGER,
    note            TEXT
);

-- A physical machine on a customer's floor. Rarely more than one row per
-- database, but a welding+cleaning CENTRE is several machines in a line under
-- one HMI, and this is what lets that be described rather than special-cased.
CREATE TABLE machine_instance (
    id              INTEGER PRIMARY KEY,
    series_id       INTEGER NOT NULL REFERENCES machine_series(id),
    serial_no       TEXT,
    name            TEXT NOT NULL,             -- what the operator calls it
    module_id       TEXT,                      -- the HMI module: FA2010, H500 ...
    is_main         INTEGER NOT NULL DEFAULT 0,
    commissioned_at TEXT,
    note            TEXT
);
CREATE INDEX ix_instance_series ON machine_instance(series_id);

-- =====================================================================
-- 2. CAPABILITIES - what a machine can DO
-- =====================================================================
-- Deliberately finer-grained than the sales category: a welding+cleaning
-- centre welds AND cleans corners, and a machining centre both cuts and mills.
-- Data requirements follow the duty, not the catalogue.

CREATE TABLE capability (
    code        TEXT PRIMARY KEY,
    name        TEXT NOT NULL,
    description TEXT,
    -- Does this duty need the profile library at all? An assembly unit may not.
    needs_profile INTEGER NOT NULL DEFAULT 1
);

INSERT INTO capability (code, name, description, needs_profile) VALUES
 ('CUT',            'Cutting',              'Saw the bar to length and angle.',            1),
 ('MILL_ACCESSORY', 'Accessory milling',    'Hardware pockets, holes and slots.',          1),
 ('MILL_CONTOUR',   'Contour milling',      'Profile end and edge milling.',               1),
 ('WELD',           'Welding',              'Fuse PVC corners.',                           1),
 ('CORNER_CLEAN',   'Corner cleaning',      'Remove the weld bead from a corner.',         1),
 ('MULLION_CLEAN',  'Mullion cleaning',     'Clean a mullion/transom joint.',              1),
 ('REINF_SCREW',    'Reinforcement screwing','Screw steel reinforcement into a chamber.',  1),
 ('DRAINAGE',       'Drainage slotting',    'Water drainage slots.',                       1),
 ('ASSEMBLE',       'Assembly',             'Fit hardware and glazing.',                   0);

-- Which duties a SERIES performs. This is the join that decides which screens
-- and which data a given HMI build shows - so a corner cleaner never has to
-- carry, or even see, the cutting parameters.
CREATE TABLE series_capability (
    series_id       INTEGER NOT NULL REFERENCES machine_series(id) ON DELETE CASCADE,
    capability_code TEXT NOT NULL REFERENCES capability(code) ON DELETE CASCADE,
    PRIMARY KEY (series_id, capability_code)
);

-- =====================================================================
-- 3. PARAMETERS AS DATA - the extension point
-- =====================================================================
-- A parameter is DECLARED, not compiled in. Adding "knife heat time" to corner
-- cleaning is one INSERT, and every corner cleaner gets the field, the unit,
-- the range check and the default without a code change.

CREATE TABLE parameter_definition (
    id              INTEGER PRIMARY KEY,
    capability_code TEXT NOT NULL REFERENCES capability(code) ON DELETE CASCADE,
    code            TEXT NOT NULL,
    name            TEXT NOT NULL,
    data_type       TEXT NOT NULL,             -- allowed: REAL,INT,BOOL,TEXT,ENUM
    unit            TEXT,                      -- mm, deg, s, C, mm/min
    default_value   TEXT,
    min_value       REAL,
    max_value       REAL,
    enum_values     TEXT,                      -- comma separated, ENUM only
    -- Does the value vary per PROFILE, or is it one setting for the machine?
    scope           TEXT NOT NULL DEFAULT 'PROFILE',   -- allowed: PROFILE,MACHINE,BOTH
    -- Ordering and grouping for the generated screen, so a new parameter needs
    -- no layout work either.
    ui_group        TEXT,
    ui_order        INTEGER NOT NULL DEFAULT 0,
    required        INTEGER NOT NULL DEFAULT 0,
    note            TEXT,
    UNIQUE (capability_code, code)
);

-- A value: for a machine series (or instance), a capability, and optionally a
-- profile. profile_id NULL = the machine-wide setting.
CREATE TABLE parameter_value (
    id              INTEGER PRIMARY KEY,
    definition_id   INTEGER NOT NULL REFERENCES parameter_definition(id) ON DELETE CASCADE,
    series_id       INTEGER REFERENCES machine_series(id) ON DELETE CASCADE,
    instance_id     INTEGER REFERENCES machine_instance(id) ON DELETE CASCADE,
    profile_id      INTEGER,                   -- -> profile.id in the library
    value           TEXT,
    status          TEXT NOT NULL DEFAULT 'AUTO',  -- allowed: AUTO,PROPOSED,CONFIRMED
    confirmed_by    TEXT,
    confirmed_at    TEXT,
    updated_at      TEXT NOT NULL,
    UNIQUE (definition_id, series_id, instance_id, profile_id)
);
CREATE INDEX ix_paramvalue_lookup ON parameter_value(definition_id, series_id, profile_id);

-- Resolution order, most specific first:
--    instance + profile  ->  series + profile  ->  instance  ->  series
--    ->  parameter_definition.default_value
-- Same principle as the profile library's correction chain: a machine that
-- needed one number tuned must not require the other forty to be restated.

-- =====================================================================
-- 4. TOOLS AND FEEDS - machine-side by definition
-- =====================================================================
CREATE TABLE mounted_tool (
    id              INTEGER PRIMARY KEY,
    instance_id     INTEGER NOT NULL REFERENCES machine_instance(id) ON DELETE CASCADE,
    position        TEXT NOT NULL,             -- spindle / magazine slot
    tool_code       TEXT NOT NULL,
    tool_type       TEXT NOT NULL,             -- allowed: DRILL,END_MILL,SAW,KNIFE,SCREWDRIVER,PROBE
    diameter_mm     REAL,
    flute_count     INTEGER,
    usable_length_mm REAL,
    max_rpm         REAL,
    is_active       INTEGER NOT NULL DEFAULT 1,
    UNIQUE (instance_id, position)
);

CREATE TABLE milling_class (
    id              INTEGER PRIMARY KEY,
    code            TEXT NOT NULL UNIQUE,
    material        TEXT NOT NULL,             -- allowed: PVC,ALU,STEEL,COMPOSITE
    name            TEXT NOT NULL,
    note            TEXT
);

-- The feed to use, for a machine, a tool type and a material class. This is the
-- row that must never live in the profile library: it depends on all three, and
-- FA2010 and SB3012 legitimately disagree about it for the same profile.
CREATE TABLE feed_strategy (
    id                INTEGER PRIMARY KEY,
    series_id         INTEGER REFERENCES machine_series(id) ON DELETE CASCADE,
    instance_id       INTEGER REFERENCES machine_instance(id) ON DELETE CASCADE,
    milling_class_id  INTEGER NOT NULL REFERENCES milling_class(id) ON DELETE CASCADE,
    tool_type         TEXT NOT NULL,
    entry_speed       REAL NOT NULL,
    feed_speed        REAL NOT NULL,
    spindle_rpm       REAL,
    plunge_rate       REAL,
    thin_wall_factor  REAL NOT NULL DEFAULT 1.0,   -- multiplier below thin_wall_mm
    thin_wall_mm      REAL NOT NULL DEFAULT 1.5,
    UNIQUE (series_id, instance_id, milling_class_id, tool_type)
);

-- Which DXF layer means which milling class, per profile series.
CREATE TABLE layer_milling_map (
    id               INTEGER PRIMARY KEY,
    profile_series_id INTEGER NOT NULL,        -- -> profile_series.id in the library
    dxf_layer_name   TEXT NOT NULL,
    milling_class_id INTEGER NOT NULL REFERENCES milling_class(id) ON DELETE CASCADE,
    UNIQUE (profile_series_id, dxf_layer_name)
);

-- =====================================================================
-- 5. HOW A MACHINE IS PLACED IN SPACE
-- =====================================================================
-- Profile coordinates -> machine coordinates. The library stores only profile
-- space; this is the one place the two meet.
CREATE TABLE machine_fixture (
    id              INTEGER PRIMARY KEY,
    instance_id     INTEGER NOT NULL REFERENCES machine_instance(id) ON DELETE CASCADE,
    name            TEXT NOT NULL,
    origin_x_mm     REAL NOT NULL DEFAULT 0,
    origin_y_mm     REAL NOT NULL DEFAULT 0,
    origin_z_mm     REAL NOT NULL DEFAULT 0,
    rotation_deg    REAL NOT NULL DEFAULT 0,
    mirror_y        INTEGER NOT NULL DEFAULT 0,
    mirror_z        INTEGER NOT NULL DEFAULT 0,
    reference_side  TEXT NOT NULL DEFAULT 'LEFT',   -- allowed: LEFT,RIGHT,CENTRE
    is_default      INTEGER NOT NULL DEFAULT 0,
    UNIQUE (instance_id, name)
);

-- =====================================================================
-- 6. CAPABILITY-SPECIFIC DATA THAT IS NOT A FLAT PARAMETER
-- =====================================================================
-- Most per-machine data is a number and belongs in parameter_value. A PATH is
-- not - it is an ordered list - so corner cleaning gets a real table. The test
-- for adding one: "is this a sequence or a shape, rather than a value?"

CREATE TABLE corner_clean_program (
    id              INTEGER PRIMARY KEY,
    profile_id      INTEGER NOT NULL,          -- -> profile.id in the library
    series_id       INTEGER REFERENCES machine_series(id) ON DELETE CASCADE,
    name            TEXT NOT NULL,
    corner_type     TEXT NOT NULL,             -- allowed: OUTER,INNER,T_JOINT,MULLION
    status          TEXT NOT NULL DEFAULT 'AUTO',  -- allowed: AUTO,PROPOSED,CONFIRMED
    updated_at      TEXT NOT NULL,
    UNIQUE (profile_id, series_id, corner_type, name)
);

CREATE TABLE corner_clean_step (
    id              INTEGER PRIMARY KEY,
    program_id      INTEGER NOT NULL REFERENCES corner_clean_program(id) ON DELETE CASCADE,
    seq             INTEGER NOT NULL,
    tool_type       TEXT NOT NULL,             -- allowed: KNIFE,MILL,SCRAPER,BRUSH
    start_y_mm      REAL NOT NULL,
    start_z_mm      REAL NOT NULL,
    end_y_mm        REAL NOT NULL,
    end_z_mm        REAL NOT NULL,
    depth_mm        REAL NOT NULL,
    feed_mm_min     REAL,
    note            TEXT,
    UNIQUE (program_id, seq)
);

-- =====================================================================
-- 7. WHAT A GIVEN HMI SHOULD SHOW
-- =====================================================================
-- The HMI knows its own series code. Everything else follows from these joins:
--
--   which screens?      series_capability -> capability
--   which parameters?   series_capability -> parameter_definition (ui_group/order)
--   which profiles?     the shared library, filtered by branch (PVC / ALU)
--
-- So a new machine series needs: one machine_series row, its series_capability
-- rows, and any parameter_definition rows its duties do not already have.
-- No migration, no new screen code, no change to any other machine.

-- =====================================================================
-- 8. SEED: the current machines and their duties
-- =====================================================================
INSERT INTO machine_series (id, category_id, code, name, branch, axis_count) VALUES
 (1, 1, 'FA2010', 'FA2010 Machining Centre',        'PVC', 4),
 (2, 2, 'SB3012', 'SB3012 Cutting Machine',         'PVC', 2),
 (3, 2, 'SB3015', 'SB3015 Cutting Machine',         'PVC', 2),
 (4, 4, 'H500',   'H500 Screwing / Holing Unit',    'PVC', 2),
 (5, 4, 'H800',   'H800 3D Hinge Drilling Unit',    'PVC', 3);

INSERT INTO series_capability (series_id, capability_code) VALUES
 (1,'CUT'), (1,'MILL_ACCESSORY'), (1,'MILL_CONTOUR'), (1,'DRAINAGE'),
 (2,'CUT'),
 (3,'CUT'),
 (4,'REINF_SCREW'), (4,'MILL_ACCESSORY'),
 (5,'MILL_ACCESSORY');

-- Parameter definitions. Adding a machine that cleans corners needs NONE of
-- this repeated - it just declares CORNER_CLEAN and inherits the lot.
INSERT INTO parameter_definition
 (capability_code, code, name, data_type, unit, default_value, min_value, max_value, scope, ui_group, ui_order, required) VALUES
 ('CUT','blade_angle_left',  'Left blade angle',   'REAL','deg','45', 0, 180,'PROFILE','Blades',1,1),
 ('CUT','blade_angle_right', 'Right blade angle',  'REAL','deg','45', 0, 180,'PROFILE','Blades',2,1),
 ('CUT','kerf_mm',           'Blade kerf',         'REAL','mm', '4.2',0,  20,'MACHINE','Blades',3,1),
 ('CUT','cut_feed',          'Cutting feed',       'REAL','mm/min','1200',10,20000,'PROFILE','Feeds',4,0),
 ('CUT','clamp_pressure',    'Clamp pressure',     'REAL','bar','4',  0,  10,'PROFILE','Clamping',5,0),

 ('MILL_ACCESSORY','safe_z_mm',    'Safe Z height',     'REAL','mm','30', 0, 300,'MACHINE','Motion',1,1),
 ('MILL_ACCESSORY','approach_feed','Approach feed',     'REAL','mm/min','3000',10,20000,'MACHINE','Feeds',2,0),
 ('MILL_ACCESSORY','peck_depth_mm','Peck depth',        'REAL','mm','4',  0.1,50,'PROFILE','Drilling',3,0),
 ('MILL_ACCESSORY','coolant',      'Coolant',           'BOOL',NULL,'0', NULL,NULL,'PROFILE','Motion',4,0),

 ('MILL_CONTOUR','contour_feed',   'Contour feed',      'REAL','mm/min','2000',10,20000,'PROFILE','Feeds',1,0),
 ('MILL_CONTOUR','finish_pass_mm', 'Finishing allowance','REAL','mm','0.3',0,5,'PROFILE','Quality',2,0),

 ('WELD','plate_temp_c',     'Plate temperature',  'REAL','C', '250',150, 300,'PROFILE','Heat',1,1),
 ('WELD','heat_time_s',      'Heating time',       'REAL','s', '25', 1,  120,'PROFILE','Heat',2,1),
 ('WELD','weld_time_s',      'Welding time',       'REAL','s', '35', 1,  180,'PROFILE','Heat',3,1),
 ('WELD','cool_time_s',      'Cooling time',       'REAL','s', '40', 1,  300,'PROFILE','Heat',4,1),
 ('WELD','weld_seam_mm',     'Weld seam allowance','REAL','mm','3',  0,   10,'PROFILE','Geometry',5,1),
 ('WELD','clamp_pressure',   'Clamp pressure',     'REAL','bar','4', 0,   10,'PROFILE','Clamping',6,0),

 ('CORNER_CLEAN','knife_depth_mm',  'Knife depth',    'REAL','mm','2.5',0,10,'PROFILE','Knife',1,1),
 ('CORNER_CLEAN','knife_feed',      'Knife feed',     'REAL','mm/min','900',10,10000,'PROFILE','Knife',2,0),
 ('CORNER_CLEAN','groove_depth_mm', 'Groove depth',   'REAL','mm','1.5',0,10,'PROFILE','Groove',3,0),
 ('CORNER_CLEAN','bead_width_mm',   'Weld bead width','REAL','mm','3',  0,10,'PROFILE','Groove',4,0),
 ('CORNER_CLEAN','top_clean',       'Clean top face', 'BOOL',NULL,'1',NULL,NULL,'PROFILE','Faces',5,0),
 ('CORNER_CLEAN','bottom_clean',    'Clean bottom face','BOOL',NULL,'1',NULL,NULL,'PROFILE','Faces',6,0),

 ('MULLION_CLEAN','mullion_depth_mm','Cleaning depth','REAL','mm','2',0,10,'PROFILE','Knife',1,1),
 ('MULLION_CLEAN','mullion_feed',    'Cleaning feed', 'REAL','mm/min','800',10,10000,'PROFILE','Knife',2,0),

 ('REINF_SCREW','screw_pitch_mm',  'Screw pitch',     'REAL','mm','300',50,1000,'PROFILE','Screws',1,1),
 ('REINF_SCREW','edge_distance_mm','First screw from end','REAL','mm','80',0,500,'PROFILE','Screws',2,1),
 ('REINF_SCREW','screw_torque_nm', 'Screw torque',    'REAL','Nm','6', 0,  50,'PROFILE','Screws',3,0),
 ('REINF_SCREW','pilot_dia_mm',    'Pilot hole diameter','REAL','mm','3.2',0,10,'PROFILE','Screws',4,0),

 ('DRAINAGE','slot_pitch_mm',   'Slot pitch',        'REAL','mm','600',100,3000,'PROFILE','Slots',1,1),
 ('DRAINAGE','first_slot_mm',   'First slot from end','REAL','mm','150',0,2000,'PROFILE','Slots',2,1),
 ('DRAINAGE','slot_feed',       'Slot feed',         'REAL','mm/min','1500',10,20000,'PROFILE','Feeds',3,0),

 ('ASSEMBLE','station_count',   'Assembly stations', 'INT',NULL,'2',1,20,'MACHINE','Line',1,0);

INSERT INTO milling_class (id, code, material, name, note) VALUES
 (1,'PVC_OUTER_WALL','PVC','PVC outer wall',NULL),
 (2,'PVC_CHAMBER',   'PVC','PVC chamber',   NULL),
 (3,'ALU_OUTER_WALL','ALU','Aluminium outer wall',NULL),
 (4,'ALU_CHAMBER',   'ALU','Aluminium chamber',NULL),
 (5,'ALU_THIN_WALL', 'ALU','Aluminium thin wall','Below 1.5 mm.'),
 (6,'STEEL_REINF',   'STEEL','Steel reinforcement',NULL);

-- EXAMPLE feeds. Like every coordinate in the library seed, these are starting
-- points to be tuned on the machine - not manufacturer figures.
INSERT INTO feed_strategy
 (series_id, milling_class_id, tool_type, entry_speed, feed_speed, spindle_rpm, plunge_rate, thin_wall_factor, thin_wall_mm) VALUES
 (1, 1, 'END_MILL', 1500, 4000, 16000, 700, 0.6, 1.5),
 (1, 2, 'END_MILL', 2000, 5000, 16000, 900, 0.6, 1.5),
 (1, 3, 'END_MILL',  800, 2200, 18000, 350, 0.5, 1.5),
 (1, 4, 'END_MILL', 1200, 3000, 18000, 500, 0.5, 1.5),
 (1, 5, 'END_MILL',  500, 1400, 20000, 200, 0.5, 1.5),
 (1, 1, 'DRILL',     900, 1800, 12000, 400, 0.7, 1.5),
 (1, 6, 'DRILL',     200,  600, 12000, 100, 0.5, 2.0);
