using System.Buffers.Binary;
using System.Security.Cryptography;

namespace KncWX2Server.Core.Common;

/// <summary>
/// Describes the two UID layouts present in the original source tree.
/// The legacy implementation selects the layout at compile time for CN/US builds.
/// The rewrite keeps both layouts explicit so no country-specific behavior is guessed.
/// </summary>
public readonly record struct KncUidLayout(bool ExtendedServerGroupMask)
{
    public static KncUidLayout Standard { get; } = new(false);
    public static KncUidLayout Extended { get; } = new(true);

    public long PureMask => ExtendedServerGroupMask
        ? 0x0000000fffffffffL
        : 0x000000ffffffffffL;

    public int ServerGroupBits => ExtendedServerGroupMask ? 10 : 6;
}

public static class KncUid
{
    private const long TempUidBit = 0x4000000000000000L;

    public static uint Die32()
    {
        while (true)
        {
            Span<byte> bytes = stackalloc byte[4];
            RandomNumberGenerator.Fill(bytes);
            var value = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
            if (value != 0)
                return value;
        }
    }

    public static long GetTempUid(KncUidLayout layout)
    {
        while (true)
        {
            Span<byte> bytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(bytes);
            var value = BinaryPrimitives.ReadUInt64LittleEndian(bytes) & unchecked((ulong)layout.PureMask);
            if (value != 0)
                return unchecked((long)(value | TempUidBit));
        }
    }

    public static long ExtractPureUid(long uid, KncUidLayout layout) =>
        uid & layout.PureMask;

    public static long ExtractServerGroupId(long uid, KncUidLayout layout) =>
        layout.ExtendedServerGroupMask
            ? (uid >> 52) & 0x3ff
            : (uid >> 56) & 0x3f;

    public static long ExtractServerId(long uid, KncUidLayout layout) =>
        layout.ExtendedServerGroupMask
            ? (uid >> 44) & 0xff
            : (uid >> 48) & 0xff;

    public static long ExtractReservedId(long uid, KncUidLayout layout) =>
        layout.ExtendedServerGroupMask
            ? (uid >> 36) & 0xff
            : (uid >> 40) & 0xff;

    public static long ExtractCodeId(long uid, KncUidLayout layout) =>
        layout.ExtendedServerGroupMask
            ? (uid >> 36) & 0xffff
            : (uid >> 40) & 0xffff;

    public static long SetTempUid(long uid) =>
        unchecked((long)((unchecked((ulong)uid) & 0xbfffffffffffffffUL) | (ulong)TempUidBit));

    public static long SetPureUid(long destination, long source, KncUidLayout layout) =>
        Replace(destination, unchecked((ulong)source) & unchecked((ulong)layout.PureMask),
            layout.ExtendedServerGroupMask ? 0x0000000fffffffffUL : 0x000000ffffffffffUL);

    public static long SetServerGroupId(long destination, long source, KncUidLayout layout) =>
        layout.ExtendedServerGroupMask
            ? Replace(destination, unchecked((ulong)(source & 0x3ff)) << 52, 0x3ff0000000000000UL)
            : Replace(destination, unchecked((ulong)(source & 0x3f)) << 56, 0x3f00000000000000UL);

    public static long SetServerId(long destination, long source, KncUidLayout layout) =>
        layout.ExtendedServerGroupMask
            ? Replace(destination, unchecked((ulong)(source & 0xff)) << 44, 0x000ff00000000000UL)
            : Replace(destination, unchecked((ulong)(source & 0xff)) << 48, 0x00ff000000000000UL);

    public static long SetReservedId(long destination, long source, KncUidLayout layout) =>
        layout.ExtendedServerGroupMask
            ? Replace(destination, unchecked((ulong)(source & 0xff)) << 36, 0x00000ff000000000UL)
            : Replace(destination, unchecked((ulong)(source & 0xff)) << 40, 0x0000ff0000000000UL);

    public static long SetCodeId(long destination, long source, KncUidLayout layout) =>
        layout.ExtendedServerGroupMask
            ? Replace(destination, unchecked((ulong)(source & 0xffff)) << 36, 0x000ffff000000000UL)
            : Replace(destination, unchecked((ulong)(source & 0xffff)) << 40, 0x00ffff0000000000UL);

    private static long Replace(long destination, ulong value, ulong mask) =>
        unchecked((long)((unchecked((ulong)destination) & ~mask) | (value & mask)));
}
