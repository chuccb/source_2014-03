namespace KncWX2Server.Core;

using System.Net;

public enum ServerRole
{
    Login,
    Center,
    Channel,
    Game,
}

public readonly record struct GameUser(
    string Login,
    string Passwd,
    string Sex,
    long LoginUid,
    DateTime FirstLogin,
    DateTime LastConnect,
    DateTime LastLogin,
    int PlayTime,
    int GamePoint,
    string IpAddress,
    bool Connecting,
    byte[] ModeLevel,
    int Grade);

public readonly record struct GUser(
    long UserUid,
    string UserId,
    byte UsSize,
    DateTime RegDate,
    DateTime DelDate)
{
    public bool Deleted => RegDate != DelDate;
}

public readonly record struct GUnit(
    long UnitUid,
    long UserUid,
    byte UnitClass,
    int Exp,
    byte Level,
    int GamePoint,
    int VsPoint,
    int? VsPointMax,
    short BaseHp,
    byte AtkPhysic,
    byte AtkMagic,
    byte DefPhysic,
    byte DefMagic,
    int SPoint,
    int Win,
    int Lose,
    int Seceder,
    DateTime RegDate,
    DateTime DelDate,
    DateTime LastDate,
    int LastPosition,
    int PlayDayCnt,
    int LoginCount)
{
    public bool Deleted => RegDate != DelDate;
}

public readonly record struct GItem(
    long ItemUid,
    long UnitUid,
    int ItemId,
    byte InventoryCategory,
    byte SlotId,
    DateTime RegDate,
    DateTime DelDate)
{
    public bool Deleted => RegDate != DelDate;
}

public readonly record struct GSkill(long UnitUid, int SkillId, DateTime RegDate);

public sealed record ServerOptions
{
    public required ServerRole Role { get; init; }
    public required IPAddress BindAddress { get; init; }
    public required int Port { get; init; }
    public required string DatabasePath { get; init; }
    public int Backlog { get; init; } = 256;
    public int MaxFrameSize { get; init; } = 32768;
    public int WorkerCount { get; init; } = 6;
    public int PacketAuthFailLimit { get; init; } = 100;
    // Legacy KNetLayer starts with sequence validation disabled.
    public bool CheckSequenceNumbers { get; init; }
    // Legacy accepter has Nagle enabled by default.
    public bool NoDelay { get; init; }

    public static ServerOptions Defaults(ServerRole role, int port) => new()
    {
        Role = role,
        BindAddress = IPAddress.Any,
        Port = port,
        DatabasePath = Path.Combine(AppContext.BaseDirectory, "data", "kncwx2.db"),
    };

    public static ServerOptions Parse(string[] args, ServerRole role, int defaultPort)
    {
        ArgumentNullException.ThrowIfNull(args);
        var options = Defaults(role, defaultPort);

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--bind" when TryRead(args, ref i, out var bind):
                    options = options with { BindAddress = IPAddress.Parse(bind) };
                    break;
                case "--port" when TryReadInt(args, ref i, out var port) && port is > 0 and <= ushort.MaxValue:
                    options = options with { Port = port };
                    break;
                case "--db" when TryRead(args, ref i, out var db):
                    options = options with { DatabasePath = Path.GetFullPath(db) };
                    break;
                case "--backlog" when TryReadInt(args, ref i, out var backlog) && backlog > 0:
                    options = options with { Backlog = backlog };
                    break;
                case "--workers" when TryReadInt(args, ref i, out var workers) && workers > 0:
                    options = options with { WorkerCount = workers };
                    break;
                case "--auth-fail-limit" when TryReadInt(args, ref i, out var limit) && limit >= 0:
                    options = options with { PacketAuthFailLimit = limit };
                    break;
                case "--sequence-check":
                    options = options with { CheckSequenceNumbers = true };
                    break;
                case "--no-sequence-check":
                    options = options with { CheckSequenceNumbers = false };
                    break;
                case "--no-delay":
                    options = options with { NoDelay = true };
                    break;
                case "--max-frame" when TryReadInt(args, ref i, out var maxFrame) && maxFrame >= KncProtocol.MinSecureFrameSize && maxFrame <= ushort.MaxValue:
                    options = options with { MaxFrameSize = maxFrame };
                    break;
            }
        }

        return options;
    }

    private static bool TryRead(string[] args, ref int index, out string value)
    {
        if (++index >= args.Length)
        {
            value = string.Empty;
            return false;
        }

        value = args[index];
        return true;
    }

    private static bool TryReadInt(string[] args, ref int index, out int value)
    {
        if (++index >= args.Length || !int.TryParse(args[index], out value))
        {
            value = default;
            return false;
        }

        return true;
    }
}
