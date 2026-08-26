namespace KncWX2Server.Core;

public enum PerformerClass : byte
{
    Proxy = 0x01,
    User = 0x02,
    Character = 0x03,
    Server = 0x04,
    Room = 0x05,
    AccountDb = 0x06,
    GameDb = 0x07,
    LogDb = 0x08,
}

public enum ServerClass : ushort
{
    Client = 0x0100,
    Game = 0x0200,
    Center = 0x0300,
    Channel = 0x0400,
}

public enum PerformerId : int
{
    GsProxy = 0x0101,
    GsUser = 0x0202,
    GsCharacter = 0x0203,
    GsServer = 0x0204,
    GsGameDb = 0x0207,
    GsAccountDb = 0x0206,
    GsLogDb = 0x0208,
    CnProxy = 0x0201,
    CnUser = 0x0302,
    CnServer = 0x0304,
    CnRoom = 0x0305,
    CnAccountDb = 0x0306,
    CnLogDb = 0x0308,
    ChannelUser = 0x0402,
    ChannelServer = 0x0404,
    ChannelLogDb = 0x0408,
    AccountDb = 0x0006,
    LogDb = 0x0008,
    Null = -1,
}
