-- =====================================================================
--  SEED DATA
--
--  READ THIS BEFORE USING ANY COORDINATE BELOW.
--  -------------------------------------------
--  Two very different kinds of data are in this file.
--
--  1. STRUCTURE - vendor names, series, variants, profile types, accessory
--     categories. Taken from the manufacturers' public product pages
--     (Reynaers MasterLine 8 variants and construction depths; Roto NX
--     hardware categories). These are safe: they are catalogue facts, and a
--     wrong one is a naming annoyance.
--
--  2. COORDINATES - every hardware_axis, offset, hole position and depth.
--     THESE ARE MY OWN PLAUSIBLE EXAMPLES. They are NOT from any fabrication
--     manual, and I have no way to verify them.
--
--  I have deliberately NOT invented authoritative-looking machining figures.
--  A fabricated hinge coordinate that looks right is worse than an obviously
--  missing one: it can be confirmed by someone who assumes it was researched,
--  and then it gets cut into a customer's profile.
--
--  So every profile seeded here has dxf_status = 'NONE' and every resolved
--  placement will come out as AUTO - which the schema already keeps away from
--  production (only CONFIRMED rows reach a machine). The real numbers come
--  from the vendor's fabrication manual and a trial cut, entered by someone
--  who can check them.
--
--  Sources for the structural data:
--    reynaers.com/products/windows/masterline-8   (variants, technical info)
--    Reynaers MasterLine 8: fixed frame 77 mm (Functional/Block) or 87 mm
--    (Renaissance/Deco); casement frame 87 mm; min. frame-vent width 97 mm
--    roto-frank.com                                (NX hardware categories)
-- =====================================================================

-- ---------------------------------------------------------------------
-- PROFILE VENDORS
-- ---------------------------------------------------------------------
INSERT INTO profile_vendor (id, code, name) VALUES
 (1, 'REY', 'Reynaers Aluminium'),
 (2, 'SCH', 'Schueco'),
 (3, 'ALM', 'Alumil'),
 (4, 'GEN', 'Generic / in-house');

-- ---------------------------------------------------------------------
-- SERIES     depths are the published construction depths
-- ---------------------------------------------------------------------
INSERT INTO profile_series (id, vendor_id, code, name, material, system_depth_mm) VALUES
 (1, 1, 'ML8',    'MasterLine 8',  'ALU', 77),
 (2, 1, 'SL38',   'SlimLine 38',   'ALU', 38),
 (3, 2, 'AWS75',  'AWS 75.SI+',    'ALU', 75),
 (4, 3, 'S67',    'SUPREME S67',   'ALU', 67),
 (5, 4, 'PVC70',  'Generic PVC 70','PVC', 70);

-- ---------------------------------------------------------------------
-- VARIANTS   MasterLine 8's four design variants are its real ones
-- ---------------------------------------------------------------------
INSERT INTO profile_variant (id, series_id, code, name) VALUES
 (1, 1, 'FUNC', 'Functional'),
 (2, 1, 'REN',  'Renaissance'),
 (3, 1, 'DECO', 'Deco'),
 (4, 1, 'HV',   'Hidden Vent'),
 (5, 2, 'STD',  'Standard'),
 (6, 3, 'STD',  'Standard'),
 (7, 4, 'STD',  'Standard'),
 (8, 5, 'STD',  'Standard');

-- ---------------------------------------------------------------------
-- HARDWARE INTERFACES already seeded by the schema (EURO_GROOVE_16 = 1)
-- PROFILE GROUPS
--
-- Depths: Functional fixed frame 77, Renaissance/Deco 87, casement 87.
-- ---------------------------------------------------------------------
INSERT INTO profile_group
 (id, variant_id, name, profile_type, hardware_interface_id, default_width_mm, default_height_mm) VALUES
 ( 1, 1, 'ML8 Functional - Frame',    'FRAME',    1, 77,  65),
 ( 2, 1, 'ML8 Functional - Sash',     'SASH',     1, 87,  75),
 ( 3, 1, 'ML8 Functional - Mullion',  'MULLION',  1, 77,  90),
 ( 4, 1, 'ML8 Functional - Transom',  'TRANSOM',  1, 77,  90),
 ( 5, 1, 'ML8 Functional - Bead',     'BEAD',     NULL, 20, 20),
 ( 6, 2, 'ML8 Renaissance - Frame',   'FRAME',    1, 87,  65),
 ( 7, 2, 'ML8 Renaissance - Sash',    'SASH',     1, 87,  75),
 ( 8, 3, 'ML8 Deco - Frame',          'FRAME',    1, 87,  65),
 ( 9, 3, 'ML8 Deco - Sash',           'SASH',     1, 87,  75),
 (10, 4, 'ML8 Hidden Vent - Sash',    'SASH',     1, 87,  75),
 (11, 5, 'SlimLine 38 - Frame',       'FRAME',    1, 38,  50),
 (12, 5, 'SlimLine 38 - Sash',        'SASH',     1, 38,  55),
 (13, 6, 'AWS 75 - Frame',            'FRAME',    1, 75,  65),
 (14, 6, 'AWS 75 - Sash',             'SASH',     1, 75,  75),
 (15, 7, 'SUPREME S67 - Frame',       'FRAME',    1, 67,  62),
 (16, 7, 'SUPREME S67 - Sash',        'SASH',     1, 67,  70),
 (17, 8, 'PVC 70 - Frame',            'FRAME',    2, 70,  62),
 (18, 8, 'PVC 70 - Sash',             'SASH',     2, 70,  70);

-- ---------------------------------------------------------------------
-- MACHINING FAMILIES - how a profile is PROCESSED, independent of vendor
-- ---------------------------------------------------------------------
INSERT INTO machining_family (id, code, name, note) VALUES
 (1, 'AL_FRAME_OUTSIDE',  'Aluminium frame, outside entry',  'Drainage and striker from the outer face.'),
 (2, 'AL_SASH_GROOVE',    'Aluminium sash, groove entry',    'Hardware milled into the Euro groove.'),
 (3, 'PVC_FRAME_OUTSIDE', 'PVC frame, outside entry',        'Slower entry, chamber walls thinner.'),
 (4, 'PVC_SASH_GROOVE',   'PVC sash, groove entry',          NULL);

-- ---------------------------------------------------------------------
-- PROFILES
--
-- dxf_status = 'NONE' on every row ON PURPOSE: no DXF has been imported and
-- no datum measured, so nothing here can resolve to a cuttable placement.
-- Codes follow each vendor's visible article-number style but are EXAMPLES.
-- ---------------------------------------------------------------------
-- created_at is supplied explicitly: the column has no DB default by design
-- (see the portability note in schema_v2.sql), the C# seeder sets it the same way.
INSERT INTO profile (id, group_id, machining_family_id, code, description, width_mm, height_mm, dxf_status, is_active, created_at) VALUES
 ( 1,  1, 1, 'ML8-F-4501', 'Functional outer frame 77',          77,  65, 'NONE', 1, '2026-01-01T00:00:00Z'),
 ( 2,  1, 1, 'ML8-F-4502', 'Functional outer frame, high sill',  77,  78, 'NONE', 1, '2026-01-01T00:00:00Z'),
 ( 3,  2, 2, 'ML8-S-4510', 'Functional casement sash 87',        87,  75, 'NONE', 1, '2026-01-01T00:00:00Z'),
 ( 4,  2, 2, 'ML8-S-4511', 'Functional casement sash, heavy',    87,  86, 'NONE', 1, '2026-01-01T00:00:00Z'),
 ( 5,  3, 1, 'ML8-M-4520', 'Functional mullion',                 77,  90, 'NONE', 1, '2026-01-01T00:00:00Z'),
 ( 6,  4, 1, 'ML8-T-4530', 'Functional transom',                 77,  90, 'NONE', 1, '2026-01-01T00:00:00Z'),
 ( 7,  5, NULL,'ML8-B-4540','Glazing bead 20',                   20,  20, 'NONE', 1, '2026-01-01T00:00:00Z'),
 ( 8,  6, 1, 'ML8-F-4601', 'Renaissance outer frame 87',         87,  65, 'NONE', 1, '2026-01-01T00:00:00Z'),
 ( 9,  7, 2, 'ML8-S-4610', 'Renaissance casement sash',          87,  75, 'NONE', 1, '2026-01-01T00:00:00Z'),
 (10,  8, 1, 'ML8-F-4701', 'Deco outer frame 87',                87,  65, 'NONE', 1, '2026-01-01T00:00:00Z'),
 (11,  9, 2, 'ML8-S-4710', 'Deco casement sash',                 87,  75, 'NONE', 1, '2026-01-01T00:00:00Z'),
 (12, 10, 2, 'ML8-S-4810', 'Hidden vent sash',                   87,  75, 'NONE', 1, '2026-01-01T00:00:00Z'),
 (13, 11, 1, 'SL38-F-1001','SlimLine 38 frame',                  38,  50, 'NONE', 1, '2026-01-01T00:00:00Z'),
 (14, 12, 2, 'SL38-S-1010','SlimLine 38 sash',                   38,  55, 'NONE', 1, '2026-01-01T00:00:00Z'),
 (15, 13, 1, 'AWS75-F-201','AWS 75 outer frame',                 75,  65, 'NONE', 1, '2026-01-01T00:00:00Z'),
 (16, 14, 2, 'AWS75-S-210','AWS 75 casement sash',               75,  75, 'NONE', 1, '2026-01-01T00:00:00Z'),
 (17, 15, 1, 'S67-F-301',  'SUPREME S67 frame',                  67,  62, 'NONE', 1, '2026-01-01T00:00:00Z'),
 (18, 16, 2, 'S67-S-310',  'SUPREME S67 sash',                   67,  70, 'NONE', 1, '2026-01-01T00:00:00Z'),
 (19, 17, 3, 'PVC70-F-01', 'Generic PVC 70 frame',               70,  62, 'NONE', 1, '2026-01-01T00:00:00Z'),
 (20, 18, 4, 'PVC70-S-10', 'Generic PVC 70 sash',                70,  70, 'NONE', 1, '2026-01-01T00:00:00Z');

-- ---------------------------------------------------------------------
-- ACCESSORY VENDORS AND SERIES
-- ---------------------------------------------------------------------
INSERT INTO accessory_vendor (id, code, name) VALUES
 (1, 'ROT', 'Roto Frank'),
 (2, 'SIE', 'Siegenia'),
 (3, 'GU',  'Gretsch-Unitas'),
 (4, 'MAC', 'Maco'),
 (5, 'GEN', 'Generic');

INSERT INTO accessory_series (id, vendor_id, code, name, hardware_interface_id) VALUES
 (1, 1, 'NX',     'Roto NX',        1),
 (2, 1, 'NT',     'Roto NT',        1),
 (3, 2, 'TITAN',  'Siegenia Titan', 1),
 (4, 3, 'UNIJET', 'GU UNI-JET',     1),
 (5, 4, 'MM',     'Maco Multi-Matic',1),
 (6, 5, 'STD',    'Generic',        NULL);

-- ---------------------------------------------------------------------
-- ACCESSORY GROUPS  (functional categories)
-- ---------------------------------------------------------------------
INSERT INTO accessory_group (id, name, accessory_type) VALUES
 (1, 'Hinge',          'HINGE'),
 (2, 'Handle',         'HANDLE'),
 (3, 'Lock',           'LOCK'),
 (4, 'Striker',        'STRIKER'),
 (5, 'Espagnolette',   'ESPAGNOLETTE'),
 (6, 'Corner drive',   'CORNER_DRIVE'),
 (7, 'Stay',           'STAY'),
 (8, 'Restrictor',     'RESTRICTOR'),
 (9, 'Water drainage', 'WATER_DRAINAGE');

-- ---------------------------------------------------------------------
-- ACCESSORIES
-- ---------------------------------------------------------------------
INSERT INTO accessory (id, group_id, series_id, code, description, is_active) VALUES
 ( 1, 1, 1, 'NX-HNG-STD',  'Roto NX standard hinge',            1),
 ( 2, 1, 1, 'NX-HNG-HD',   'Roto NX heavy-duty hinge',          1),
 ( 3, 1, 3, 'TIT-HNG-STD', 'Siegenia Titan standard hinge',     1),
 ( 4, 2, 1, 'NX-HDL-92',   'Roto NX handle, 92 mm centres',     1),
 ( 5, 2, 6, 'GEN-HDL-SQ',  'Generic square-spindle handle',     1),
 ( 6, 3, 1, 'NX-LCK-35',   'Roto NX lock case, 35 mm backset',  1),
 ( 7, 4, 1, 'NX-STR-STD',  'Roto NX striker plate',             1),
 ( 8, 5, 1, 'NX-ESP-STD',  'Roto NX espagnolette',              1),
 ( 9, 6, 1, 'NX-CD-STD',   'Roto NX corner drive',              1),
 (10, 7, 3, 'TIT-STAY-STD','Siegenia Titan stay',               1),
 (11, 8, 6, 'GEN-RST-100', 'Generic opening restrictor',        1),
 (12, 9, 6, 'GEN-DRN-5X30','Drainage slot 5 x 30',              1),
 (13, 9, 6, 'GEN-DRN-6X25','Drainage slot 6 x 25',              1);

-- ---------------------------------------------------------------------
-- PARAMETRIC MACROS   shape only: no tool, no feed, no G-code
-- ---------------------------------------------------------------------
INSERT INTO macro (id, code, macro_type, name, params_json, through_profile, note) VALUES
 ( 1, 'DRILL_5',      'ROUND_HOLE', 'Drill 5 mm',        '{"diameter_mm":5,"depth_mm":12}',   0, NULL),
 ( 2, 'DRILL_6',      'ROUND_HOLE', 'Drill 6 mm',        '{"diameter_mm":6,"depth_mm":14}',   0, NULL),
 ( 3, 'DRILL_8',      'ROUND_HOLE', 'Drill 8 mm',        '{"diameter_mm":8,"depth_mm":16}',   0, NULL),
 ( 4, 'DRILL_10',     'ROUND_HOLE', 'Drill 10 mm',       '{"diameter_mm":10,"depth_mm":18}',  0, NULL),
 ( 5, 'SLOT_5X30',    'SLOT',       'Slot 5 x 30',       '{"length_mm":30,"width_mm":5,"depth_mm":6,"angle_deg":0}',  0, 'Drainage.'),
 ( 6, 'SLOT_6X25',    'SLOT',       'Slot 6 x 25',       '{"length_mm":25,"width_mm":6,"depth_mm":6,"angle_deg":0}',  0, 'Drainage.'),
 ( 7, 'SLOT_10X25',   'SLOT',       'Slot 10 x 25',      '{"length_mm":25,"width_mm":10,"depth_mm":8,"angle_deg":0}', 0, 'Hinge body.'),
 ( 8, 'SLOT_14X40',   'SLOT',       'Slot 14 x 40',      '{"length_mm":40,"width_mm":14,"depth_mm":10,"angle_deg":0}',0, 'Corner drive.'),
 ( 9, 'POCKET_32X32', 'POCKET',     'Handle pocket',     '{"length_mm":32,"width_mm":32,"depth_mm":10,"corner_r_mm":3}',0,NULL),
 (10, 'POCKET_16X235','POCKET',     'Lock case pocket',  '{"length_mm":235,"width_mm":16,"depth_mm":40,"corner_r_mm":2}',0,NULL),
 (11, 'ESPAG_GROOVE', 'CONTOUR',    'Espagnolette groove','{"path":"LINE","depth_mm":8}',      0, 'Runs the sash length.'),
 (12, 'FACE_MILL',    'CONTOUR',    'Face milling',      '{"path":"CONTOUR","depth_mm":2}',    0, NULL);

-- ---------------------------------------------------------------------
-- ACCESSORY OPERATIONS
--
-- EVERY OFFSET BELOW IS AN EXAMPLE, NOT A MANUFACTURER FIGURE.
-- The shape of the data is right - a hinge really is a slot plus fixing holes
-- referenced to the hardware axis - but the numbers must be replaced from the
-- fabrication manual before anything is confirmed for production.
-- ---------------------------------------------------------------------
INSERT INTO accessory_operation
 (id, accessory_id, macro_id, seq, position_mode, reference_code, offset_y_mm, offset_z_mm,
  x_strategy, x_base_mm, x_pitch_mm, x_count, rotation_deg, mirror) VALUES
 -- Roto NX standard hinge: body slot + two fixing holes
 ( 1,  1,  7, 1, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  0,   0, 'FROM_BOTH_ENDS', 120, NULL, 2, 0, 0),
 ( 2,  1,  1, 2, 'REFERENCE_OFFSET', 'HARDWARE_AXIS', -9,   0, 'FROM_BOTH_ENDS', 120, NULL, 2, 0, 0),
 ( 3,  1,  1, 3, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  9,   0, 'FROM_BOTH_ENDS', 120, NULL, 2, 0, 0),
 -- heavy-duty hinge: same pattern, three fixings
 ( 4,  2,  7, 1, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  0,   0, 'FROM_BOTH_ENDS', 140, NULL, 2, 0, 0),
 ( 5,  2,  2, 2, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',-12,   0, 'FROM_BOTH_ENDS', 140, NULL, 2, 0, 0),
 ( 6,  2,  2, 3, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  0,  12, 'FROM_BOTH_ENDS', 140, NULL, 2, 0, 0),
 ( 7,  2,  2, 4, 'REFERENCE_OFFSET', 'HARDWARE_AXIS', 12,   0, 'FROM_BOTH_ENDS', 140, NULL, 2, 0, 0),
 -- Siegenia Titan hinge
 ( 8,  3,  7, 1, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  0,   0, 'FROM_BOTH_ENDS', 125, NULL, 2, 0, 0),
 ( 9,  3,  1, 2, 'REFERENCE_OFFSET', 'HARDWARE_AXIS', -8,   0, 'FROM_BOTH_ENDS', 125, NULL, 2, 0, 0),
 -- handle: square pocket on the axis, plus two fixings
 (10,  4,  9, 1, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  0,   0, 'CENTRED',          0, NULL, 1, 0, 0),
 (11,  4,  1, 2, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  0, -46, 'CENTRED',          0, NULL, 1, 0, 0),
 (12,  4,  1, 3, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  0,  46, 'CENTRED',          0, NULL, 1, 0, 0),
 (13,  5,  9, 1, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  0,   0, 'CENTRED',          0, NULL, 1, 0, 0),
 -- lock case
 (14,  6, 10, 1, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  0,   0, 'CENTRED',          0, NULL, 1, 0, 0),
 (15,  6,  3, 2, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  0, -60, 'CENTRED',          0, NULL, 1, 0, 0),
 -- striker on the frame, mirroring the sash hinges
 (16,  7,  7, 1, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  0,   0, 'FROM_BOTH_ENDS', 120, NULL, 2, 0, 1),
 (17,  7,  1, 2, 'REFERENCE_OFFSET', 'HARDWARE_AXIS', -9,   0, 'FROM_BOTH_ENDS', 120, NULL, 2, 0, 1),
 -- espagnolette groove down the sash
 (18,  8, 11, 1, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  0,   0, 'CENTRED',          0, NULL, 1, 0, 0),
 -- corner drive
 (19,  9,  8, 1, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  0,   0, 'FROM_START',      80, NULL, 1, 0, 0),
 -- stay
 (20, 10,  1, 1, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  0,   0, 'FROM_START',     200, NULL, 1, 0, 0),
 -- restrictor
 (21, 11,  1, 1, 'REFERENCE_OFFSET', 'HARDWARE_AXIS',  0,   0, 'FROM_START',     300, NULL, 1, 0, 0),
 -- drainage: distributed along the frame, measured from the OUTSIDE face
 (22, 12,  5, 1, 'REFERENCE_OFFSET', 'OUTSIDE_SURFACE', 5,  3, 'DISTRIBUTED',    150,  600, 3, 0, 0),
 (23, 13,  6, 1, 'REFERENCE_OFFSET', 'OUTSIDE_SURFACE', 6,  3, 'DISTRIBUTED',    150,  600, 3, 0, 0);

-- ---------------------------------------------------------------------
-- WHICH ACCESSORIES EACH PROFILE GROUP TAKES
-- ---------------------------------------------------------------------
INSERT INTO group_accessory_assignment (profile_group_id, accessory_id, is_enabled) VALUES
 -- ML8 Functional sash: hinge, handle, lock, espagnolette, corner drive
 (2, 1, 1), (2, 4, 1), (2, 6, 1), (2, 8, 1), (2, 9, 1),
 -- ML8 Functional frame: striker + drainage
 (1, 7, 1), (1, 12, 1),
 -- Renaissance / Deco / Hidden Vent sashes
 (7, 1, 1), (7, 4, 1), (9, 1, 1), (9, 4, 1), (10, 2, 1), (10, 4, 1),
 (6, 7, 1), (6, 12, 1), (8, 7, 1), (8, 12, 1),
 -- SlimLine 38: lighter hardware
 (12, 1, 1), (12, 5, 1), (11, 7, 1), (11, 13, 1),
 -- AWS 75 - same Euro groove, so the same Roto hardware
 (14, 1, 1), (14, 4, 1), (13, 7, 1), (13, 12, 1),
 -- SUPREME S67
 (16, 3, 1), (16, 4, 1), (15, 7, 1), (15, 12, 1),
 -- PVC 70 (different interface, generic hardware)
 (18, 5, 1), (17, 13, 1);

-- NOTE: milling classes / feeds / speeds are NOT seeded here. They live in the
-- machine-side schema (schema_machines.sql), because a feed rate is a property
-- of a machine, a tool and a material - not of a Reynaers frame. An earlier
-- draft of this file seeded them and the load failed with "no such table:
-- milling_class", which is the boundary working exactly as intended.
