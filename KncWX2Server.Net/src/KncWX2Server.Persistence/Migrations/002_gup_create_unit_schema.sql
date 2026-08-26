-- Schema required by the legacy dbo.gup_create_unit procedure.
-- Every column below is copied from the 2014 Database2 schema; defaults that
-- the SQL Server procedure relied on are represented explicitly where needed.

CREATE TABLE IF NOT EXISTS GUnitNickName
(
    UnitUID  INTEGER NOT NULL PRIMARY KEY,
    NickName TEXT NULL,
    RegDate  TEXT NOT NULL,
    FOREIGN KEY (UnitUID) REFERENCES GUnit(UnitUID) ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS MUK_GUnitNickName_NickName
    ON GUnitNickName (NickName);

CREATE TABLE IF NOT EXISTS GDeletedNickNameHistory
(
    NickName TEXT    NOT NULL,
    UnitUID  INTEGER NOT NULL,
    RegDate  TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_GDeletedNickNameHistory_NickName_RegDate
    ON GDeletedNickNameHistory (NickName, RegDate DESC);

CREATE TABLE IF NOT EXISTS GDenyOption
(
    UnitUID    INTEGER NOT NULL,
    QuestionNo INTEGER NOT NULL,
    CodeNo     INTEGER NOT NULL,
    FOREIGN KEY (UnitUID) REFERENCES GUnit(UnitUID) ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE IF NOT EXISTS GQuests
(
    UnitUID   INTEGER NOT NULL,
    QuestID   INTEGER NOT NULL,
    SubQuest0 INTEGER NOT NULL,
    SubQuest1 INTEGER NOT NULL,
    SubQuest2 INTEGER NOT NULL,
    SubQuest3 INTEGER NOT NULL,
    SubQuest4 INTEGER NOT NULL,
    RegDate   TEXT NOT NULL,
    FOREIGN KEY (UnitUID) REFERENCES GUnit(UnitUID) ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE IF NOT EXISTS GSkillSlot
(
    UnitUID INTEGER NOT NULL PRIMARY KEY,
    Slot01  INTEGER NOT NULL,
    Slot02  INTEGER NOT NULL,
    Slot03  INTEGER NOT NULL,
    FOREIGN KEY (UnitUID) REFERENCES GUnit(UnitUID) ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE IF NOT EXISTS GSpirit
(
    unitUID INTEGER NOT NULL PRIMARY KEY,
    Spirit  INTEGER NOT NULL,
    RegDate TEXT    NOT NULL,
    Flag    INTEGER NOT NULL CHECK (Flag IN (0, 1)),
    FOREIGN KEY (unitUID) REFERENCES GUnit(UnitUID) ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE IF NOT EXISTS GResurrectionStoneCnt
(
    StartCnt   INTEGER NULL,
    SupplyCnt  INTEGER NULL,
    StartSpirit INTEGER NULL
);

-- The SQL Server source uses these defaults when gup_create_unit omits the columns.
-- SQLite migrations create the defaults directly so equivalent inserts can omit them.
-- The pre-existing 001 migration may already have these columns; the defaults are
-- therefore also supplied by the service implementation for compatibility.

CREATE INDEX IF NOT EXISTS IX_GUnitNickName_UnitUID
    ON GUnitNickName (UnitUID);

CREATE INDEX IF NOT EXISTS IX_GDenyOption_UnitUID
    ON GDenyOption (UnitUID);

CREATE INDEX IF NOT EXISTS IX_GQuests_UnitUID
    ON GQuests (UnitUID);

CREATE INDEX IF NOT EXISTS IX_GSpirit_unitUID
    ON GSpirit (unitUID);
