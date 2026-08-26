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
    public int MaxPayloadBytes { get; init; } = KncProtocol.MaxPayloadBytes;
    public int WorkerCount { get; init; } = 6;
    public int PacketAuthFailLimit { get; init; } = 100;
    public bool CheckSequenceNumbers { get; init; } = true;

    public static ServerOptions Defaults(ServerRole role, int port) => new()
    {
        Role = role,
        BindAddress = IPAddress.Any,
        Port = port,
        DatabasePath = Path.Combine(AppContext.BaseDirectory, "data", "kncwx2.db"),
    };

    public static ServerOptions Parse(string[] args, ServerRole role, int defaultPort)
    {
        var options = Defaults(role, defaultPort);

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "--bind" when i + 1 < args.Length:
                    options = options with { BindAddress = IPAddress.Parse(args[++i]) };
                    break;
                case "--port" when i + 1 < args.Length && int.TryParse(args[++i], out var port):
                    options = options with { Port = port };
                    break;
                case "--db" when i + 1 < args.Length:
                    options = options with { DatabasePath = Path.GetFullPath(args[++i]) };
                    break;
                case "--backlog" when i + 1 < args.Length && int.TryParse(args[++i], out var backlog) && backlog > 0:
                    options = options with { Backlog = backlog };
                    break;
                case "--workers" when i + 1 < args.Length && int.TryParse(args[++i], out var workers) && workers > 0:
                    options = options with { WorkerCount = workers };
                    break;
                case "--auth-fail-limit" when i + 1 < args.Length && int.TryParse(args[++i], out var limit) && limit > 0:
                    options = options with { PacketAuthFailLimit = limit };
                    break;
                case "--no-sequence-check":
                    options = options with { CheckSequenceNumbers = false };
                    break;
                case "--max-payload" when i + 1 < args.Length && int.TryParse(args[++i], out var maxPayload) && maxPayload > 0:
                    options = options with { MaxPayloadBytes = maxPayload };
                    break;
            }
        }

        return options;
    }
}
