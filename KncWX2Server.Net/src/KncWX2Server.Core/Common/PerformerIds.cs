namespace KncWX2Server.Core.Common;

public static class PerformerIds
{
    public const uint PcMask = 0x000000FF;
    public const uint ServerClassMask = 0x00000F00;
    public const uint SendTypeMask = 0x0000F000;

    public const uint PcProxy = 0x00000001;
    public const uint PcUser = 0x00000002;
    public const uint PcCharacter = 0x00000003;
    public const uint PcServer = 0x00000004;
    public const uint PcRoom = 0x00000005;
    public const uint PcAccountDb = 0x00000006;
    public const uint PcGameDb = 0x00000007;
    public const uint PcLogDb = 0x00000008;
    public const uint PcSmsDb = 0x00000009;
    public const uint PcGameDb2 = 0x00000012;
    public const uint PcLogDb2 = 0x00000013;

    public const uint ScClient = 0x00000100;
    public const uint ScGame = 0x00000200;
    public const uint ScCenter = 0x00000300;
    public const uint ScChannel = 0x00000400;
    public const uint ScLogin = 0x00000500;
    public const uint ScSimulation = 0x00000600;
    public const uint ScGlobal = 0x00000700;

    public const uint SendSameServerGroup = 0x00001000;

    public const uint PiGsUser = ScGame | PcUser;
    public const uint PiGsServer = ScGame | PcServer;
    public const uint PiGsServerSame = ScGame | PcServer | SendSameServerGroup;
    public const uint PiGsGameDb = ScGame | PcGameDb;
    public const uint PiGsGameDb2 = ScGame | PcGameDb2;
    public const uint PiGsLogDb = ScGame | PcLogDb;
    public const uint PiGsLogDb2 = ScGame | PcLogDb2;
    public const uint PiGsAccountDb = ScGame | PcAccountDb;

    public const uint PiLoginUser = ScLogin | PcUser;
    public const uint PiLoginServer = ScLogin | PcServer;
    public const uint PiLoginGameDb = ScLogin | PcGameDb;
    public const uint PiLoginAccountDb = ScLogin | PcAccountDb;

    public const uint PiCnUser = ScCenter | PcUser;
    public const uint PiCnServer = ScCenter | PcServer;
    public const uint PiCnGameDb = ScCenter | PcGameDb;
    public const uint PiChannelUser = ScChannel | PcUser;
    public const uint PiChannelServer = ScChannel | PcServer;
    public const uint PiChannelGameDb = ScChannel | PcGameDb;

    public static uint GetServerClass(uint performerId) => performerId & ServerClassMask;
    public static uint GetPerformerClass(uint performerId) => performerId & PcMask;
    public static uint GetSendType(uint performerId) => performerId & SendTypeMask;
    public static int CompareServerLevel(uint destination, uint current) =>
        unchecked((int)(GetServerClass(destination) - GetServerClass(current)));
}
