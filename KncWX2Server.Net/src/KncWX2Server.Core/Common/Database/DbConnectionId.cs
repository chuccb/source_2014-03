namespace KncWX2Server.Core.Common.Database;

/// <summary>Logical DB-agent identifiers from DBConnectionInfo_def.h.</summary>
public enum DbConnectionId
{
    Account,
    Game,
    Log,
    NxWeb,
    Sms,
    Game2nd,
    Log2nd,
    KogBilling,
    PublisherBilling,
    ChatLog,
    IdPcbangAuth,
    JpRelayDb,
    Event,
    PublisherAuth,
    Script,
}

public static class DbConnectionIdExtensions
{
    public static bool TryMapPerformerClass(uint performerClass, out DbConnectionId connectionId) =>
        performerClass switch
        {
            Common.Routing.PerformerRouting.PerformerAccountDb => Set(DbConnectionId.Account, out connectionId),
            Common.Routing.PerformerRouting.PerformerGameDb => Set(DbConnectionId.Game, out connectionId),
            Common.Routing.PerformerRouting.PerformerLogDb => Set(DbConnectionId.Log, out connectionId),
            Common.Routing.PerformerRouting.PerformerNxWebDb => Set(DbConnectionId.NxWeb, out connectionId),
            Common.Routing.PerformerRouting.PerformerSmsDb => Set(DbConnectionId.Sms, out connectionId),
            Common.Routing.PerformerRouting.PerformerGameDb2nd => Set(DbConnectionId.Game2nd, out connectionId),
            Common.Routing.PerformerRouting.PerformerLogDb2nd => Set(DbConnectionId.Log2nd, out connectionId),
            Common.Routing.PerformerRouting.PerformerKogBillingDb => Set(DbConnectionId.KogBilling, out connectionId),
            Common.Routing.PerformerRouting.PerformerPublisherBillingDb => Set(DbConnectionId.PublisherBilling, out connectionId),
            Common.Routing.PerformerRouting.PerformerChatLogDb => Set(DbConnectionId.ChatLog, out connectionId),
            Common.Routing.PerformerRouting.PerformerIdPublisherPcbangAuthDb => Set(DbConnectionId.IdPcbangAuth, out connectionId),
            Common.Routing.PerformerRouting.PerformerJpRelayDb => Set(DbConnectionId.JpRelayDb, out connectionId),
            Common.Routing.PerformerRouting.PerformerEventDb => Set(DbConnectionId.Event, out connectionId),
            Common.Routing.PerformerRouting.PerformerPublisherAuthDb => Set(DbConnectionId.PublisherAuth, out connectionId),
            Common.Routing.PerformerRouting.PerformerScriptDb => Set(DbConnectionId.Script, out connectionId),
            _ => Fail(out connectionId),
        };

    private static bool Set(DbConnectionId value, out DbConnectionId connectionId)
    {
        connectionId = value;
        return true;
    }

    private static bool Fail(out DbConnectionId connectionId)
    {
        connectionId = default;
        return false;
    }
}
