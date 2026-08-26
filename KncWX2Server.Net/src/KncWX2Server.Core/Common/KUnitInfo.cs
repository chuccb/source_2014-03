namespace KncWX2Server.Core.Common;

public sealed class KUnitInfo
{
    public long OwnerUserUid { get; set; }
    public sbyte AuthLevel { get; set; }
    public long UnitUid { get; set; }
    public uint KnmSerialNum { get; set; }
    public sbyte UnitClass { get; set; }
    public string NickName { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public ushort Port { get; set; }
    public int Ed { get; set; }
    public byte Level { get; set; }
    public int Exp { get; set; }

    public int OfficialMatchCount { get; set; }
    public int Rating { get; set; }
    public int MaxRating { get; set; }
    public int RPoint { get; set; }
    public int APoint { get; set; }
    public bool IsWinBeforeMatch { get; set; }
    public sbyte Rank { get; set; }
    public float KFactor { get; set; }
    public bool IsRedistributionUser { get; set; }
    public int PastSeasonWin { get; set; }

    public int SPoint { get; set; }
    public int CsPoint { get; set; }
    public int MaxCsPoint { get; set; }
    public string CsPointEndDate { get; set; } = string.Empty;
    public int NowBaseLevelExp { get; set; }
    public int NextBaseLevelExp { get; set; }
    public int StraightVictories { get; set; }

    public KStat Stat { get; set; }
    public KStat GameStat { get; set; }
    public KLastPositionInfo LastPosition { get; set; }

    public List<KBuffInfo> BuffInfo { get; } = [];

    public int Win { get; set; }
    public int Lose { get; set; }
    public Dictionary<int, KDungeonClearInfo> DungeonClear { get; } = [];
    public Dictionary<int, KTCClearInfo> TcClear { get; } = [];
    public Dictionary<int, KDungeonPlayInfo> DungeonPlay { get; } = [];
    public Dictionary<int, KInventoryItemInfo> EquippedItems { get; } = [];

    public KUnitSkillData UnitSkillData { get; } = new();
    public bool IsParty { get; set; }
    public int SpiritMax { get; set; }
    public int Spirit { get; set; }
    public bool IsGameBang { get; set; }
    public int PcBangType { get; set; } = -1;
    public int TitleId { get; set; }
    public KUserGuildInfo UserGuildInfo { get; set; }
    public string LastLoginTime { get; set; } = string.Empty;

    public long WarpVipEndTime { get; set; }
    public int EventQuestClearCount { get; set; }
    public int ExchangeCount { get; set; }

    public byte OldYearMissionRewardedLevel { get; set; }
    public int NewYearMissionStepId { get; set; } = -1;

    public byte CheckPowerCount { get; set; }
    public long CheckPowerTime { get; set; }
    public bool CheckPowerShowPopUp { get; set; }
    public byte CheckPowerScore { get; set; }
}
