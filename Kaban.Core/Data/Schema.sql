-- Kaban HMI database schema (PostgreSQL). Run once:  psql -d kaban -f Schema.sql
-- Tables mirror the legacy screens: users, machine profiles (with per-head
-- G-code style steps), and named machine-parameter records.

CREATE TABLE IF NOT EXISTS hmi_users (
    id            SERIAL PRIMARY KEY,
    username      TEXT UNIQUE NOT NULL,
    password_hash TEXT NOT NULL,           -- PBKDF2, same format as UserStore
    level         TEXT NOT NULL CHECK (level IN ('Operator','Maintenance','Admin')),
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS profiles (
    id            SERIAL PRIMARY KEY,
    profile_code  TEXT UNIQUE NOT NULL,    -- e.g. 0001000007
    company_name  TEXT NOT NULL,           -- e.g. DECEUNINCK
    serial        TEXT,                    -- e.g. Elegant
    profile_type  TEXT,                    -- e.g. KASA / KANAT
    sub_type      TEXT,
    width_mm      NUMERIC(8,2),
    height_mm     NUMERIC(8,2),
    mould_diff_mm NUMERIC(8,2),
    melting_s     NUMERIC(8,2),
    welding_s     NUMERIC(8,2),
    temperature_c NUMERIC(6,1),
    clamping_mm   NUMERIC(8,2),
    gcode         TEXT,
    dxf_file      TEXT,                    -- stored path of the profile drawing
    options       JSONB DEFAULT '{}'::jsonb   -- stripping/auxiliary flags etc.
);

CREATE TABLE IF NOT EXISTS profile_steps (
    id            SERIAL PRIMARY KEY,
    profile_id    INT NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    head          TEXT NOT NULL DEFAULT 'Main',   -- Main / Head1..Head4 / Auxiliary
    step_no       INT NOT NULL,
    op            TEXT NOT NULL,           -- e.g. [ABS X,Y,Z]
    x_mm          NUMERIC(9,3),
    z_mm          NUMERIC(9,3),
    r_mm          NUMERIC(9,3),
    speed         TEXT,
    y1            NUMERIC(9,3),
    y2            NUMERIC(9,3),
    end_check     BOOLEAN NOT NULL DEFAULT false,
    UNIQUE (profile_id, head, step_no)
);

CREATE TABLE IF NOT EXISTS parameter_records (
    id            SERIAL PRIMARY KEY,
    name          TEXT UNIQUE NOT NULL,
    saved_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    machine       TEXT NOT NULL,           -- module id, e.g. FA2010
    payload       JSONB NOT NULL           -- address -> value snapshot
);
