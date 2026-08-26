using System.Security.Cryptography;

namespace KncWX2Server.Core.Common.Security;

public readonly record struct SecurityAssociationState(
    byte[] AuthKey,
    byte[] CryptoKey,
    uint SequenceNumber,
    uint LastSequenceNumber,
    uint ReplayWindowMask);

/// <summary>
/// Direct managed counterpart of the legacy KSecurityAssociation.
/// The cryptographic parameters and sequence-number semantics mirror the 2014 source.
/// </summary>
public sealed class SecurityAssociation
{
    public const int AuthKeySize = 8;
    public const int CryptoKeySize = 8;
    public const int IvSize = 8;
    public const int IcvSize = 10;
    public const int BlockSize = 8;
    public const int MaxExtraPadBlocks = 1;
    public const uint MaxSequenceNumber = uint.MaxValue;

    // KncSecurity71.vcproj declares the source file as ks_c_5601-1987.
    // These are the exact first 8 bytes of the two legacy narrow string literals under CP949.
    private static readonly byte[] DefaultAuthKey = [0xC0, 0xD3, 0xBD, 0xC3, 0xB7, 0xCE, 0xB8, 0xB8];
    private static readonly byte[] DefaultCryptoKey = [0xC7, 0xD8, 0xC4, 0xBF, 0xB5, 0xE9, 0xC0, 0xFD];

    public byte[] AuthKey { get; private set; } = [.. DefaultAuthKey];
    public byte[] CryptoKey { get; private set; } = [.. DefaultCryptoKey];
    public uint SequenceNumber { get; private set; } = 1;
    public uint LastSequenceNumber { get; private set; }
    public uint ReplayWindowMask { get; private set; }

    public void ResetRandomizeKey()
    {
        AuthKey = [.. Enumerable.Range(0, AuthKeySize).Select(static _ => (byte)RandomNumberGenerator.GetInt32(1, byte.MaxValue + 1))];
        CryptoKey = [.. Enumerable.Range(0, CryptoKeySize).Select(static _ => (byte)RandomNumberGenerator.GetInt32(1, byte.MaxValue + 1))];
    }

    public void SetAuthKey(ReadOnlySpan<byte> key) => AuthKey = key.ToArray();
    public void SetCryptoKey(ReadOnlySpan<byte> key) => CryptoKey = key.ToArray();

    public void IncrementSequenceNumber() => SequenceNumber++;

    public void IncrementSequenceNumberNoReplayWindow()
    {
        SequenceNumber++;
        if (SequenceNumber == 0)
            SequenceNumber = 1;
    }

    public bool IsValidSequenceNumberNoReplayWindow(uint sequenceNumber)
    {
        if (sequenceNumber == 0)
            return false;

        if (sequenceNumber > LastSequenceNumber)
        {
            LastSequenceNumber = sequenceNumber;
            return true;
        }

        if (sequenceNumber == LastSequenceNumber)
            return false;

        if (LastSequenceNumber != MaxSequenceNumber)
            return false;

        LastSequenceNumber = sequenceNumber;
        return true;
    }

    public bool IsValidSequenceNumber(uint sequenceNumber)
    {
        if (sequenceNumber == 0)
            return false;

        if (sequenceNumber > LastSequenceNumber)
            return true;

        const uint windowSize = sizeof(uint) * 8;
        var difference = LastSequenceNumber - sequenceNumber;
        if (difference >= windowSize)
            return false;

        return (ReplayWindowMask & (1u << (int)difference)) == 0;
    }

    public void UpdateReplayWindow(uint sequenceNumber)
    {
        if (!IsValidSequenceNumber(sequenceNumber))
            throw new InvalidOperationException("Invalid sequence number for replay-window update.");

        const uint windowSize = sizeof(uint) * 8;

        if (sequenceNumber > LastSequenceNumber)
        {
            var difference = sequenceNumber - LastSequenceNumber;
            ReplayWindowMask = difference < windowSize
                ? (ReplayWindowMask << (int)difference) | 1u
                : 1u;
            LastSequenceNumber = sequenceNumber;
            return;
        }

        var oldDifference = LastSequenceNumber - sequenceNumber;
        ReplayWindowMask |= 1u << (int)oldDifference;
    }

    public byte[] Encrypt(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> iv) =>
        DesCbc.Transform(CryptoKey, iv, payload, encrypt: true);

    public byte[] Decrypt(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> iv) =>
        DesCbc.Transform(CryptoKey, iv, ciphertext, encrypt: false);

    public byte[] GenerateIcv(ReadOnlySpan<byte> authenticatedData)
    {
        using var hmac = new HMACMD5(AuthKey);
        return hmac.ComputeHash(authenticatedData.ToArray())[..IcvSize];
    }

    public SecurityAssociationState Snapshot() =>
        new([.. AuthKey], [.. CryptoKey], SequenceNumber, LastSequenceNumber, ReplayWindowMask);

    public void Restore(SecurityAssociationState state)
    {
        if (state.AuthKey.Length != AuthKeySize || state.CryptoKey.Length != CryptoKeySize)
            throw new ArgumentException("Invalid security-association key size.", nameof(state));

        AuthKey = [.. state.AuthKey];
        CryptoKey = [.. state.CryptoKey];
        SequenceNumber = state.SequenceNumber;
        LastSequenceNumber = state.LastSequenceNumber;
        ReplayWindowMask = state.ReplayWindowMask;
    }

    private static class DesCbc
    {
        public static byte[] Transform(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, ReadOnlySpan<byte> input, bool encrypt)
        {
            if (key.Length != CryptoKeySize)
                throw new ArgumentException("DES requires an 8-byte key.", nameof(key));
            if (iv.Length != IvSize)
                throw new ArgumentException("DES CBC requires an 8-byte IV.", nameof(iv));
            if (input.Length == 0 || input.Length % BlockSize != 0)
                throw new ArgumentException("DES CBC input must contain complete blocks.", nameof(input));

            using var des = DES.Create();
            des.Key = key.ToArray();
            des.IV = iv.ToArray();
            des.Mode = CipherMode.CBC;
            des.Padding = PaddingMode.None;

            using var transform = encrypt ? des.CreateEncryptor() : des.CreateDecryptor();
            return transform.TransformFinalBlock(input.ToArray(), 0, input.Length);
        }
    }
}
