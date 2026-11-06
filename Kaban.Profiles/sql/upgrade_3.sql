-- =====================================================================
--  SCHEMA 3  (6 November 2026)
-- =====================================================================

-- AN ACCESSORY GROUP ATTACHED TO A PROFILE GROUP (the user: "Why can't we attach accessory groups to the profile groups directly so
-- any accessory would be attached to all the profiles as soon as they are defined using their accessory and profile groups"):
-- every accessory of the accessory group - those added later too - goes on every profile of the profile group.
CREATE TABLE group_accessory_group (
    id                  INTEGER PRIMARY KEY,
    profile_group_id    INTEGER NOT NULL REFERENCES profile_group(id) ON DELETE CASCADE,
    accessory_group_id  INTEGER NOT NULL REFERENCES accessory_group(id) ON DELETE CASCADE,
    is_enabled          INTEGER NOT NULL DEFAULT 1,
    UNIQUE (profile_group_id, accessory_group_id)
);

-- The accessories a profile group takes: given one by one (group_accessory_assignment), or with their accessory group - one
-- switched off for the group (an assignment row with is_enabled 0) stays off whichever way it came.
CREATE VIEW v_group_accessory AS
SELECT ga.profile_group_id, ga.accessory_id FROM group_accessory_assignment ga WHERE ga.is_enabled = 1
UNION
SELECT gg.profile_group_id, a.id AS accessory_id
FROM group_accessory_group gg JOIN accessory a ON a.group_id = gg.accessory_group_id AND a.is_active = 1
WHERE gg.is_enabled = 1
  AND NOT EXISTS (SELECT 1 FROM group_accessory_assignment x
                  WHERE x.profile_group_id = gg.profile_group_id AND x.accessory_id = a.id AND x.is_enabled = 0);

-- A PROFILE'S MACHINE SETTINGS (the old program's profile parameters - FA2010: R25600 ... R25649): a value per profile and machine,
-- by its code. A profile without one takes its group's reference profile's (the group's template), then its base profile's.
CREATE TABLE profile_machine_setting (
    profile_id      INTEGER NOT NULL REFERENCES profile(id) ON DELETE CASCADE,
    instance_id     INTEGER NOT NULL,
    code            TEXT NOT NULL,
    value           TEXT,
    updated_at      TEXT,
    PRIMARY KEY (profile_id, instance_id, code)
);
