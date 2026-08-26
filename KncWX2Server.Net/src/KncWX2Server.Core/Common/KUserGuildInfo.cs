namespace KncWX2Server.Core.Common;

/// <summary>Legacy guild information embedded in KUnitInfo.</summary>
public readonly record struct KUserGuildInfo(
    int GuildUid,
    string GuildName,
    byte MembershipGrade,
    int HonorPoint);
