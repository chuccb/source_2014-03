using KncWX2Server.Core.Common;

namespace KncWX2Server.Core.Common.Serialization;

/// <summary>Explicit TWHK 2014-03 wire serializer for KUnitInfo.</summary>
public static class KUnitInfoSerialization
{
    public static bool Put(this KSerializer serializer, KUnitInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return WriteIdentity(serializer, value)
            && WriteMatchAndRank(serializer, value)
            && WriteProgress(serializer, value)
            && WriteStatsAndCollections(serializer, value)
            && WriteSkillAndStatus(serializer, value)
            && WriteEventState(serializer, value);
    }

    public static bool Get(this KSerializer serializer, out KUnitInfo value)
    {
        value = new();
        var result = new KUnitInfo();

        if (!ReadIdentity(serializer, result)
            || !ReadMatchAndRank(serializer, result)
            || !ReadProgress(serializer, result)
            || !ReadStatsAndCollections(serializer, result)
            || !ReadSkillAndStatus(serializer, result)
            || !ReadEventState(serializer, result))
            return false;

        value = result;
        return true;
    }

    private static bool WriteIdentity(KSerializer serializer, KUnitInfo value) =>
        serializer.Put(value.OwnerUserUid)
        && serializer.Put(value.AuthLevel)
        && serializer.Put(value.UnitUid)
        && serializer.Put(value.KnmSerialNum)
        && serializer.Put(value.UnitClass)
        && serializer.PutWString(value.NickName)
        && serializer.PutWString(value.Ip)
        && serializer.Put(value.Port)
        && serializer.Put(value.Ed)
        && serializer.Put(value.Level)
        && serializer.Put(value.Exp);

    private static bool WriteMatchAndRank(KSerializer serializer, KUnitInfo value) =>
        serializer.Put(value.OfficialMatchCount)
        && serializer.Put(value.Rating)
        && serializer.Put(value.MaxRating)
        && serializer.Put(value.RPoint)
        && serializer.Put(value.APoint)
        && serializer.Put(value.IsWinBeforeMatch)
        && serializer.Put(value.Rank)
        && serializer.Put(value.KFactor)
        && serializer.Put(value.IsRedistributionUser)
        && serializer.Put(value.PastSeasonWin);

    private static bool WriteProgress(KSerializer serializer, KUnitInfo value) =>
        serializer.Put(value.SPoint)
        && serializer.Put(value.CsPoint)
        && serializer.Put(value.MaxCsPoint)
        && serializer.PutWString(value.CsPointEndDate)
        && serializer.Put(value.NowBaseLevelExp)
        && serializer.Put(value.NextBaseLevelExp)
        && serializer.Put(value.StraightVictories);

    private static bool WriteStatsAndCollections(KSerializer serializer, KUnitInfo value) =>
        serializer.Put(value.Stat)
        && serializer.Put(value.GameStat)
        && serializer.Put(value.LastPosition)
        && serializer.PutVector(value.BuffInfo, static (KSerializer s, KBuffInfo item) => s.Put(item))
        && serializer.Put(value.Win)
        && serializer.Put(value.Lose)
        && serializer.PutMap(value.DungeonClear, static (KSerializer s, int key) => s.Put(key), static (KSerializer s, KDungeonClearInfo item) => s.Put(item))
        && serializer.PutMap(value.TcClear, static (KSerializer s, int key) => s.Put(key), static (KSerializer s, KTCClearInfo item) => s.Put(item))
        && serializer.PutMap(value.DungeonPlay, static (KSerializer s, int key) => s.Put(key), static (KSerializer s, KDungeonPlayInfo item) => s.Put(item))
        && serializer.PutMap(value.EquippedItems, static (KSerializer s, int key) => s.Put(key), static (KSerializer s, KInventoryItemInfo item) => s.Put(item));

    private static bool WriteSkillAndStatus(KSerializer serializer, KUnitInfo value) =>
        serializer.Put(value.UnitSkillData)
        && serializer.Put(value.IsParty)
        && serializer.Put(value.SpiritMax)
        && serializer.Put(value.Spirit)
        && serializer.Put(value.IsGameBang)
        && serializer.Put(value.PcBangType)
        && serializer.Put(value.TitleId)
        && serializer.Put(value.UserGuildInfo)
        && serializer.PutWString(value.LastLoginTime)
        && serializer.Put(value.WarpVipEndTime)
        && serializer.Put(value.EventQuestClearCount)
        && serializer.Put(value.ExchangeCount);

    private static bool WriteEventState(KSerializer serializer, KUnitInfo value) =>
        serializer.Put(value.OldYearMissionRewardedLevel)
        && serializer.Put(value.NewYearMissionStepId)
        && serializer.Put(value.CheckPowerCount)
        && serializer.Put(value.CheckPowerTime)
        && serializer.Put(value.CheckPowerShowPopUp)
        && serializer.Put(value.CheckPowerScore);

    private static bool ReadIdentity(KSerializer serializer, KUnitInfo value) =>
        serializer.Get(out value.OwnerUserUid)
        && serializer.Get(out value.AuthLevel)
        && serializer.Get(out value.UnitUid)
        && serializer.Get(out value.KnmSerialNum)
        && serializer.Get(out value.UnitClass)
        && serializer.GetWString(out value.NickName)
        && serializer.GetWString(out value.Ip)
        && serializer.Get(out value.Port)
        && serializer.Get(out value.Ed)
        && serializer.Get(out value.Level)
        && serializer.Get(out value.Exp);

    private static bool ReadMatchAndRank(KSerializer serializer, KUnitInfo value) =>
        serializer.Get(out value.OfficialMatchCount)
        && serializer.Get(out value.Rating)
        && serializer.Get(out value.MaxRating)
        && serializer.Get(out value.RPoint)
        && serializer.Get(out value.APoint)
        && serializer.Get(out value.IsWinBeforeMatch)
        && serializer.Get(out value.Rank)
        && serializer.Get(out value.KFactor)
        && serializer.Get(out value.IsRedistributionUser)
        && serializer.Get(out value.PastSeasonWin);

    private static bool ReadProgress(KSerializer serializer, KUnitInfo value) =>
        serializer.Get(out value.SPoint)
        && serializer.Get(out value.CsPoint)
        && serializer.Get(out value.MaxCsPoint)
        && serializer.GetWString(out value.CsPointEndDate)
        && serializer.Get(out value.NowBaseLevelExp)
        && serializer.Get(out value.NextBaseLevelExp)
        && serializer.Get(out value.StraightVictories);

    private static bool ReadStatsAndCollections(KSerializer serializer, KUnitInfo value) =>
        serializer.Get(out value.Stat)
        && serializer.Get(out value.GameStat)
        && serializer.Get(out value.LastPosition)
        && serializer.GetVector(value.BuffInfo, static (KSerializer s, out KBuffInfo item) =>
        {
            item = default;
            return s.Get(out item);
        })
        && serializer.Get(out value.Win)
        && serializer.Get(out value.Lose)
        && GetDungeonClear(serializer, value.DungeonClear)
        && GetTcClear(serializer, value.TcClear)
        && GetDungeonPlay(serializer, value.DungeonPlay)
        && GetEquippedItems(serializer, value.EquippedItems);

    private static bool ReadSkillAndStatus(KSerializer serializer, KUnitInfo value)
    {
        if (!serializer.Get(out KUnitSkillData unitSkillData)
            || !serializer.Get(out value.IsParty)
            || !serializer.Get(out value.SpiritMax)
            || !serializer.Get(out value.Spirit)
            || !serializer.Get(out value.IsGameBang)
            || !serializer.Get(out value.PcBangType)
            || !serializer.Get(out value.TitleId)
            || !serializer.Get(out value.UserGuildInfo)
            || !serializer.GetWString(out value.LastLoginTime)
            || !serializer.Get(out value.WarpVipEndTime)
            || !serializer.Get(out value.EventQuestClearCount)
            || !serializer.Get(out value.ExchangeCount))
            return false;

        CopyUnitSkillData(value.UnitSkillData, unitSkillData);
        return true;
    }

    private static bool ReadEventState(KSerializer serializer, KUnitInfo value) =>
        serializer.Get(out value.OldYearMissionRewardedLevel)
        && serializer.Get(out value.NewYearMissionStepId)
        && serializer.Get(out value.CheckPowerCount)
        && serializer.Get(out value.CheckPowerTime)
        && serializer.Get(out value.CheckPowerShowPopUp)
        && serializer.Get(out value.CheckPowerScore);

    private static bool GetDungeonClear(KSerializer serializer, Dictionary<int, KDungeonClearInfo> value) =>
        serializer.GetMap(
            value.Clear,
            (int key, KDungeonClearInfo item) =>
            {
                value.TryAdd(key, item);
                return true;
            },
            static (KSerializer s, out int key, out KDungeonClearInfo item) =>
            {
                item = default;
                return s.Get(out key) && s.Get(out item);
            });

    private static bool GetTcClear(KSerializer serializer, Dictionary<int, KTCClearInfo> value) =>
        serializer.GetMap(
            value.Clear,
            (int key, KTCClearInfo item) =>
            {
                value.TryAdd(key, item);
                return true;
            },
            static (KSerializer s, out int key, out KTCClearInfo item) =>
            {
                item = default;
                return s.Get(out key) && s.Get(out item);
            });

    private static bool GetDungeonPlay(KSerializer serializer, Dictionary<int, KDungeonPlayInfo> value) =>
        serializer.GetMap(
            value.Clear,
            (int key, KDungeonPlayInfo item) =>
            {
                value.TryAdd(key, item);
                return true;
            },
            static (KSerializer s, out int key, out KDungeonPlayInfo item) =>
            {
                item = default;
                return s.Get(out key) && s.Get(out item);
            });

    private static bool GetEquippedItems(KSerializer serializer, Dictionary<int, KInventoryItemInfo> value) =>
        serializer.GetMap(
            value.Clear,
            (int key, KInventoryItemInfo item) =>
            {
                value.TryAdd(key, item);
                return true;
            },
            static (KSerializer s, out int key, out KInventoryItemInfo item) =>
            {
                item = default;
                return s.Get(out key) && s.Get(out item);
            });

    private static void CopyUnitSkillData(KUnitSkillData destination, KUnitSkillData source)
    {
        for (var i = 0; i < KUnitSkillData.EquippedSkillSlotCount; i++)
        {
            destination.EquippedSkills[i] = source.EquippedSkills[i];
            destination.EquippedSkillSlotB[i] = source.EquippedSkillSlotB[i];
        }

        destination.SkillSlotBEndDate = source.SkillSlotBEndDate;
        destination.SkillSlotBExpirationState = source.SkillSlotBExpirationState;
        destination.PassiveSkills.Clear();
        destination.PassiveSkills.AddRange(source.PassiveSkills);
        destination.GuildPassiveSkills.Clear();
        destination.GuildPassiveSkills.AddRange(source.GuildPassiveSkills);
        destination.SkillNotes.Clear();
        destination.SkillNotes.AddRange(source.SkillNotes);
        destination.ActiveSkillPagesNumber = source.ActiveSkillPagesNumber;
        destination.AvailableSkillPagesNumber = source.AvailableSkillPagesNumber;
    }
}
