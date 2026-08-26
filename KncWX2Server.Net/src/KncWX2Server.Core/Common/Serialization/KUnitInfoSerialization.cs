using KncWX2Server.Core.Common;

namespace KncWX2Server.Core.Common.Serialization;

/// <summary>Explicit TWHK 2014-03 wire serializer for KUnitInfo.</summary>
public static class KUnitInfoSerialization
{
    public static bool Put(this KSerializer serializer, KUnitInfo value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return serializer.Put(value.OwnerUserUid)
            && serializer.Put(value.AuthLevel)
            && serializer.Put(value.UnitUid)
            && serializer.Put(value.KnmSerialNum)
            && serializer.Put(value.UnitClass)
            && serializer.PutWString(value.NickName)
            && serializer.PutWString(value.Ip)
            && serializer.Put(value.Port)
            && serializer.Put(value.Ed)
            && serializer.Put(value.Level)
            && serializer.Put(value.Exp)
            && serializer.Put(value.OfficialMatchCount)
            && serializer.Put(value.Rating)
            && serializer.Put(value.MaxRating)
            && serializer.Put(value.RPoint)
            && serializer.Put(value.APoint)
            && serializer.Put(value.IsWinBeforeMatch)
            && serializer.Put(value.Rank)
            && serializer.Put(value.KFactor)
            && serializer.Put(value.IsRedistributionUser)
            && serializer.Put(value.PastSeasonWin)
            && serializer.Put(value.SPoint)
            && serializer.Put(value.CsPoint)
            && serializer.Put(value.MaxCsPoint)
            && serializer.PutWString(value.CsPointEndDate)
            && serializer.Put(value.NowBaseLevelExp)
            && serializer.Put(value.NextBaseLevelExp)
            && serializer.Put(value.StraightVictories)
            && serializer.Put(value.Stat)
            && serializer.Put(value.GameStat)
            && serializer.Put(value.LastPosition)
            && serializer.PutVector(value.BuffInfo, static (KSerializer s, KBuffInfo item) => s.Put(item))
            && serializer.Put(value.Win)
            && serializer.Put(value.Lose)
            && serializer.PutMap(value.DungeonClear, static (KSerializer s, int key) => s.Put(key), static (KSerializer s, KDungeonClearInfo item) => s.Put(item))
            && serializer.PutMap(value.TcClear, static (KSerializer s, int key) => s.Put(key), static (KSerializer s, KTCClearInfo item) => s.Put(item))
            && serializer.PutMap(value.DungeonPlay, static (KSerializer s, int key) => s.Put(key), static (KSerializer s, KDungeonPlayInfo item) => s.Put(item))
            && serializer.PutMap(value.EquippedItems, static (KSerializer s, int key) => s.Put(key), static (KSerializer s, KInventoryItemInfo item) => s.Put(item))
            && serializer.Put(value.UnitSkillData)
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
            && serializer.Put(value.ExchangeCount)
            && serializer.Put(value.OldYearMissionRewardedLevel)
            && serializer.Put(value.NewYearMissionStepId)
            && serializer.Put(value.CheckPowerCount)
            && serializer.Put(value.CheckPowerTime)
            && serializer.Put(value.CheckPowerShowPopUp)
            && serializer.Put(value.CheckPowerScore);
    }

    public static bool Get(this KSerializer serializer, out KUnitInfo value)
    {
        value = new();

        if (!serializer.Get(out long ownerUserUid)
            || !serializer.Get(out sbyte authLevel)
            || !serializer.Get(out long unitUid)
            || !serializer.Get(out uint knmSerialNum)
            || !serializer.Get(out sbyte unitClass)
            || !serializer.GetWString(out string nickName)
            || !serializer.GetWString(out string ip)
            || !serializer.Get(out ushort port)
            || !serializer.Get(out int ed)
            || !serializer.Get(out byte level)
            || !serializer.Get(out int exp)
            || !serializer.Get(out int officialMatchCount)
            || !serializer.Get(out int rating)
            || !serializer.Get(out int maxRating)
            || !serializer.Get(out int rPoint)
            || !serializer.Get(out int aPoint)
            || !serializer.Get(out bool isWinBeforeMatch)
            || !serializer.Get(out sbyte rank)
            || !serializer.Get(out float kFactor)
            || !serializer.Get(out bool isRedistributionUser)
            || !serializer.Get(out int pastSeasonWin)
            || !serializer.Get(out int sPoint)
            || !serializer.Get(out int csPoint)
            || !serializer.Get(out int maxCsPoint)
            || !serializer.GetWString(out string csPointEndDate)
            || !serializer.Get(out int nowBaseLevelExp)
            || !serializer.Get(out int nextBaseLevelExp)
            || !serializer.Get(out int straightVictories)
            || !serializer.Get(out KStat stat)
            || !serializer.Get(out KStat gameStat)
            || !serializer.Get(out KLastPositionInfo lastPosition)
            || !serializer.GetVector(value.BuffInfo, static (KSerializer s, out KBuffInfo item) => s.Get(out item))
            || !serializer.Get(out int win)
            || !serializer.Get(out int lose)
            || !serializer.GetMap(value.DungeonClear.Clear, (int key, KDungeonClearInfo item) => { value.DungeonClear.TryAdd(key, item); return true; }, static (KSerializer s, out int key, out KDungeonClearInfo item) => s.Get(out key) && s.Get(out item))
            || !serializer.GetMap(value.TcClear.Clear, (int key, KTCClearInfo item) => { value.TcClear.TryAdd(key, item); return true; }, static (KSerializer s, out int key, out KTCClearInfo item) => s.Get(out key) && s.Get(out item))
            || !serializer.GetMap(value.DungeonPlay.Clear, (int key, KDungeonPlayInfo item) => { value.DungeonPlay.TryAdd(key, item); return true; }, static (KSerializer s, out int key, out KDungeonPlayInfo item) => s.Get(out key) && s.Get(out item))
            || !serializer.GetMap(value.EquippedItems.Clear, (int key, KInventoryItemInfo item) => { value.EquippedItems.TryAdd(key, item); return true; }, static (KSerializer s, out int key, out KInventoryItemInfo item) => s.Get(out key) && s.Get(out item))
            || !serializer.Get(out KUnitSkillData unitSkillData)
            || !serializer.Get(out bool isParty)
            || !serializer.Get(out int spiritMax)
            || !serializer.Get(out int spirit)
            || !serializer.Get(out bool isGameBang)
            || !serializer.Get(out int pcBangType)
            || !serializer.Get(out int titleId)
            || !serializer.Get(out KUserGuildInfo userGuildInfo)
            || !serializer.GetWString(out string lastLoginTime)
            || !serializer.Get(out long warpVipEndTime)
            || !serializer.Get(out int eventQuestClearCount)
            || !serializer.Get(out int exchangeCount)
            || !serializer.Get(out byte oldYearMissionRewardedLevel)
            || !serializer.Get(out int newYearMissionStepId)
            || !serializer.Get(out byte checkPowerCount)
            || !serializer.Get(out long checkPowerTime)
            || !serializer.Get(out bool checkPowerShowPopUp)
            || !serializer.Get(out byte checkPowerScore))
        {
            return false;
        }

        value.OwnerUserUid = ownerUserUid;
        value.AuthLevel = authLevel;
        value.UnitUid = unitUid;
        value.KnmSerialNum = knmSerialNum;
        value.UnitClass = unitClass;
        value.NickName = nickName;
        value.Ip = ip;
        value.Port = port;
        value.Ed = ed;
        value.Level = level;
        value.Exp = exp;
        value.OfficialMatchCount = officialMatchCount;
        value.Rating = rating;
        value.MaxRating = maxRating;
        value.RPoint = rPoint;
        value.APoint = aPoint;
        value.IsWinBeforeMatch = isWinBeforeMatch;
        value.Rank = rank;
        value.KFactor = kFactor;
        value.IsRedistributionUser = isRedistributionUser;
        value.PastSeasonWin = pastSeasonWin;
        value.SPoint = sPoint;
        value.CsPoint = csPoint;
        value.MaxCsPoint = maxCsPoint;
        value.CsPointEndDate = csPointEndDate;
        value.NowBaseLevelExp = nowBaseLevelExp;
        value.NextBaseLevelExp = nextBaseLevelExp;
        value.StraightVictories = straightVictories;
        value.Stat = stat;
        value.GameStat = gameStat;
        value.LastPosition = lastPosition;
        value.Win = win;
        value.Lose = lose;
        value.IsParty = isParty;
        value.SpiritMax = spiritMax;
        value.Spirit = spirit;
        value.IsGameBang = isGameBang;
        value.PcBangType = pcBangType;
        value.TitleId = titleId;
        value.UserGuildInfo = userGuildInfo;
        value.LastLoginTime = lastLoginTime;
        value.WarpVipEndTime = warpVipEndTime;
        value.EventQuestClearCount = eventQuestClearCount;
        value.ExchangeCount = exchangeCount;
        value.OldYearMissionRewardedLevel = oldYearMissionRewardedLevel;
        value.NewYearMissionStepId = newYearMissionStepId;
        value.CheckPowerCount = checkPowerCount;
        value.CheckPowerTime = checkPowerTime;
        value.CheckPowerShowPopUp = checkPowerShowPopUp;
        value.CheckPowerScore = checkPowerScore;
        CopyUnitSkillData(value.UnitSkillData, unitSkillData);
        return true;
    }

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
