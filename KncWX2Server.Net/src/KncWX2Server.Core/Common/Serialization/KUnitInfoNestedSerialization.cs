using KncWX2Server.Core.Common;

namespace KncWX2Server.Core.Common.Serialization;

/// <summary>Explicit wire serializers for the scalar nested KUnitInfo models.</summary>
public static class KUnitInfoNestedSerialization
{
    public static bool Put(this KSerializer serializer, in KDungeonClearInfo value) =>
        serializer.Put(value.DungeonId)
        && serializer.Put(value.MaxScore)
        && serializer.Put(value.MaxTotalRank)
        && serializer.PutWString(value.ClearTime)
        && serializer.Put(value.IsNew);

    public static bool Get(this KSerializer serializer, out KDungeonClearInfo value)
    {
        value = default;

        if (!serializer.Get(out int dungeonId)
            || !serializer.Get(out int maxScore)
            || !serializer.Get(out sbyte maxTotalRank)
            || !serializer.GetWString(out string clearTime)
            || !serializer.Get(out bool isNew))
            return false;

        value = new(dungeonId, maxScore, maxTotalRank, clearTime, isNew);
        return true;
    }

    public static bool Put(this KSerializer serializer, in KDungeonPlayInfo value) =>
        serializer.Put(value.DungeonId)
        && serializer.Put(value.PlayTimes)
        && serializer.Put(value.ClearTimes)
        && serializer.Put(value.IsNew);

    public static bool Get(this KSerializer serializer, out KDungeonPlayInfo value)
    {
        value = default;

        if (!serializer.Get(out int dungeonId)
            || !serializer.Get(out int playTimes)
            || !serializer.Get(out int clearTimes)
            || !serializer.Get(out bool isNew))
            return false;

        value = new(dungeonId, playTimes, clearTimes, isNew);
        return true;
    }

    public static bool Put(this KSerializer serializer, in KTCClearInfo value) =>
        serializer.Put(value.TcId)
        && serializer.PutWString(value.ClearTime)
        && serializer.Put(value.IsNew);

    public static bool Get(this KSerializer serializer, out KTCClearInfo value)
    {
        value = default;

        if (!serializer.Get(out int tcId)
            || !serializer.GetWString(out string clearTime)
            || !serializer.Get(out bool isNew))
            return false;

        value = new(tcId, clearTime, isNew);
        return true;
    }

    public static bool Put(this KSerializer serializer, in KLastPositionInfo value) =>
        serializer.Put(value.MapId)
        && serializer.Put(value.LastTouchLineIndex)
        && serializer.Put(value.LastPosValue)
        && serializer.Put(value.IgnoreLastTouch);

    public static bool Get(this KSerializer serializer, out KLastPositionInfo value)
    {
        value = default;

        if (!serializer.Get(out int mapId)
            || !serializer.Get(out byte lastTouchLineIndex)
            || !serializer.Get(out ushort lastPosValue)
            || !serializer.Get(out bool ignoreLastTouch))
            return false;

        value = new(mapId, lastTouchLineIndex, lastPosValue, ignoreLastTouch);
        return true;
    }

    public static bool Put(this KSerializer serializer, in KUserGuildInfo value) =>
        serializer.Put(value.GuildUid)
        && serializer.PutWString(value.GuildName)
        && serializer.Put(value.MembershipGrade)
        && serializer.Put(value.HonorPoint);

    public static bool Get(this KSerializer serializer, out KUserGuildInfo value)
    {
        value = default;

        if (!serializer.Get(out int guildUid)
            || !serializer.GetWString(out string guildName)
            || !serializer.Get(out byte membershipGrade)
            || !serializer.Get(out int honorPoint))
            return false;

        value = new(guildUid, guildName, membershipGrade, honorPoint);
        return true;
    }
}
