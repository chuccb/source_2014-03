namespace KncWX2Server.Core.Common.Routing;

/// <summary>
/// Bit layout used by the legacy KncSend routing layer.
/// These values come directly from KncSend.h; they are protocol identifiers,
/// so their numeric values are intentionally explicit.
/// </summary>
public static class PerformerRouting
{
    public const uint PerformerMask = 0x000000FF;
    public const uint ServerMask = 0x00000F00;
    public const uint SendTypeMask = 0x0000F000;

    public const uint SameServerGroupSendType = 0x00001000;

    public const uint ServerClassClient = 0x00000100;
    public const uint ServerClassGame = 0x00000200;
    public const uint ServerClassCenter = 0x00000300;
    public const uint ServerClassChannel = 0x00000400;
    public const uint ServerClassLogin = 0x00000500;
    public const uint ServerClassSimulation = 0x00000600;
    public const uint ServerClassGlobal = 0x00000700;

    public const uint PerformerProxy = 0x01;
    public const uint PerformerUser = 0x02;
    public const uint PerformerCharacter = 0x03;
    public const uint PerformerServer = 0x04;
    public const uint PerformerRoom = 0x05;
    public const uint PerformerAccountDb = 0x06;
    public const uint PerformerGameDb = 0x07;
    public const uint PerformerLogDb = 0x08;
    public const uint PerformerSmsDb = 0x09;
    public const uint PerformerNxWebDb = 0x0A;
    public const uint PerformerNxAuth = 0x0B;
    public const uint PerformerParty = 0x0C;
    public const uint PerformerNxBilling = 0x0D;
    public const uint PerformerNxBillingTcp = 0x0E;
    public const uint PerformerMatchMaking = 0x0F;
    public const uint PerformerNxSoap = 0x10;
    public const uint PerformerAutoParty = 0x11;
    public const uint PerformerGameDb2nd = 0x12;
    public const uint PerformerLogDb2nd = 0x13;
    public const uint PerformerNxGmtoolTcp = 0x14;
    public const uint PerformerChatLogDb = 0x15;

    public const uint PerformerKogBillingManager = 0x51;
    public const uint PerformerKogBillingDb = 0x52;
    public const uint PerformerPublisherAuth = 0x53;
    public const uint PerformerPublisherBilling = 0x54;
    public const uint PerformerPublisherBillingDb = 0x55;
    public const uint PerformerPublisherAuthDb = 0x56;
    public const uint PerformerAsOtpThread = 0x71;
    public const uint PerformerGiantAuth = 0x83;
    public const uint PerformerGiantRoleRegistration = 0x84;
    public const uint PerformerGiantInfo = 0x85;
    public const uint PerformerGiantBilling = 0x86;
    public const uint PerformerGiantCoupon = 0x87;
    public const uint PerformerIdPublisherPcbangAuthDb = 0x90;
    public const uint PerformerEventDb = 0x91;
    public const uint PerformerScriptDb = 0x92;
    public const uint PerformerJpRelayDb = 0xA0;

    public static int CompareServerLevel(uint destinationPerformerId, uint currentPerformerId) =>
        checked((int)GetServerClass(destinationPerformerId) - (int)GetServerClass(currentPerformerId));

    public static uint GetServerClass(uint performerId) => performerId & ServerMask;

    public static uint GetPerformerClass(uint performerId) => performerId & PerformerMask;

    public static uint GetSendType(uint performerId) => performerId & SendTypeMask;

    public static bool IsSameServerGroup(uint performerId) =>
        GetSendType(performerId) == SameServerGroupSendType;
}
