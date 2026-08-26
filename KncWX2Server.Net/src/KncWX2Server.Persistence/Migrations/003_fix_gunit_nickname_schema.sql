-- 002 initially modeled GUnitNickName with constraints that are not present
-- in the legacy SQL Server schema. Rebuild it to preserve the source semantics.

ALTER TABLE GUnitNickName RENAME TO GUnitNickName_legacy_003;

CREATE TABLE GUnitNickName
(
    UnitUID  INTEGER NOT NULL,
    NickName TEXT    NULL,
    RegDate  TEXT    NOT NULL,
    FOREIGN KEY (UnitUID) REFERENCES GUnit(UnitUID) ON DELETE CASCADE ON UPDATE CASCADE
);

INSERT INTO GUnitNickName (UnitUID, NickName, RegDate)
SELECT UnitUID, NickName, RegDate
FROM GUnitNickName_legacy_003;

DROP TABLE GUnitNickName_legacy_003;

CREATE INDEX MUK_GUnitNickName_NickName
    ON GUnitNickName (NickName);
