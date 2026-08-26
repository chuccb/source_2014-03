-- Verified schema required by the legacy dbo.gup_delete_unit procedure.
-- Source: DataBase/Database2/Database2 schema objects, keys and indexes.

-- Legacy FK_GItem_GUnit is ON DELETE CASCADE / ON UPDATE CASCADE.
-- Migration 001 used RESTRICT, so rebuild GItem without changing stored rows.
CREATE TABLE GItem__migration
(
    ItemUID           INTEGER PRIMARY KEY AUTOINCREMENT,
    UnitUID           BIGINT  NOT NULL,
    ItemID            INTEGER NOT NULL,
    InventoryCategory INTEGER NOT NULL,
    SlotID            INTEGER NOT NULL,
    RegDate           TEXT    NOT NULL,
    DelDate           TEXT    NOT NULL,
    Deleted           INTEGER GENERATED ALWAYS AS (CASE WHEN RegDate = DelDate THEN 0 ELSE 1 END) STORED,
    FOREIGN KEY (UnitUID) REFERENCES GUnit(UnitUID) ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE __KncGItemSequence (Seq INTEGER NOT NULL);
INSERT INTO __KncGItemSequence (Seq)
SELECT COALESCE((SELECT seq FROM sqlite_sequence WHERE name = 'GItem'), 0);

INSERT INTO GItem__migration
    (ItemUID, UnitUID, ItemID, InventoryCategory, SlotID, RegDate, DelDate)
SELECT
    ItemUID, UnitUID, ItemID, InventoryCategory, SlotID, RegDate, DelDate
FROM GItem;

DROP TABLE GItem;
ALTER TABLE GItem__migration RENAME TO GItem;

CREATE INDEX IF NOT EXISTS IX_GItem_UnitUID ON GItem (UnitUID);
CREATE INDEX IF NOT EXISTS IX_GItem_ItemID ON GItem (ItemID);

INSERT OR REPLACE INTO sqlite_sequence (name, seq)
SELECT
    'GItem',
    CASE
        WHEN Seq > COALESCE((SELECT MAX(ItemUID) FROM GItem), 0)
            THEN Seq
        ELSE COALESCE((SELECT MAX(ItemUID) FROM GItem), 0)
    END
FROM __KncGItemSequence;
DROP TABLE __KncGItemSequence;

CREATE TABLE IF NOT EXISTS GTutor
(
    TeacherUID BIGINT NOT NULL,
    StudentUID BIGINT NOT NULL,
    RegDate    TEXT   NULL,
    LastDate   TEXT   NULL,
    DelDate    TEXT   NULL,
    Deleted    INTEGER GENERATED ALWAYS AS (CASE WHEN RegDate = DelDate THEN 0 ELSE 1 END) STORED
);

CREATE INDEX IF NOT EXISTS IXC_GTutor_TeacherUID
    ON GTutor (TeacherUID);

-- Legacy object name is misspelled, and the source index actually targets TeacherUID.
CREATE INDEX IF NOT EXISTS IX_GTutor_SutdentUID
    ON GTutor (TeacherUID);
