using System.Buffers.Binary;
using System.Security.Cryptography;

namespace KncWX2Server.Core.Common.Security;

/// <summary>
/// Managed equivalent of legacy KSecureBuffer.
/// TCP uses the no-replay-window sequence validation path from the 2014 source.
/// </summary>
public sealed class SecureBuffer
{
    private readonly SecurityAssociationDatabase _database;
    private byte[] _buffer = [];
    private ushort _spi;

    public SecureBuffer(ushort spi, SecurityAssociationDatabase database)
    {
        _spi = spi;
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public SecureBuffer(ushort spi, ReadOnlySpan<byte> receivedData, SecurityAssociationDatabase database)
        : this(spi, database) => _buffer = receivedData.ToArray();

    public ushort Spi => _spi;
    public ReadOnlySpan<byte> Data => _buffer;
    public int Size => _buffer.Length;

    public bool Create(ByteStream payload) => Create(payload.Span);

    public bool Create(ReadOnlySpan<byte> payload) => CreateCore(payload, incrementSequence: true);

    public bool CreateNoReplayWindow(ByteStream payload) => CreateNoReplayWindow(payload.Span);

    public bool CreateNoReplayWindow(ReadOnlySpan<byte> payload) => CreateCore(payload, incrementSequence: _spi != 0);

    private bool CreateCore(ReadOnlySpan<byte> payload, bool incrementSequence)
    {
        var association = _database.Get(_spi);
        var pad = GeneratePadding(payload.Length);
        var plainLength = checked(payload.Length + pad.Length + 1);
        if ((plainLength & (SecurityAssociation.BlockSize - 1)) != 0)
            return false;

        var plain = new byte[plainLength];
        payload.CopyTo(plain);
        pad.CopyTo(plain.AsSpan(payload.Length));
        plain[^1] = checked((byte)pad.Length);

        var iv = GenerateIv(SecurityAssociation.IvSize);
        var crypt = association.Encrypt(plain, iv);

        var secureWithoutIcv = new byte[checked(sizeof(ushort) + sizeof(uint) + iv.Length + crypt.Length)];
        BinaryPrimitives.WriteUInt16LittleEndian(secureWithoutIcv, _spi);
        BinaryPrimitives.WriteUInt32LittleEndian(secureWithoutIcv.AsSpan(sizeof(ushort)), association.SequenceNumber);
        iv.CopyTo(secureWithoutIcv.AsSpan(sizeof(ushort) + sizeof(uint)));
        crypt.CopyTo(secureWithoutIcv.AsSpan(sizeof(ushort) + sizeof(uint) + iv.Length));

        var icv = association.GenerateIcv(secureWithoutIcv);
        _buffer = [.. secureWithoutIcv, .. icv];

        if (incrementSequence)
            association.IncrementSequenceNumber();

        return true;
    }

    public bool IsAuthentic() => IsAuthenticNoReplayWindow(checkSequenceNumber: true, updateReplayWindow: false);

    public bool IsAuthenticNoReplayWindow(bool checkSequenceNumber = true) =>
        IsAuthenticNoReplayWindow(checkSequenceNumber, updateReplayWindow: false);

    private bool IsAuthenticNoReplayWindow(bool checkSequenceNumber, bool updateReplayWindow)
    {
        if (!IsValidSpi() || !IsValidSize() || !IsValidIcv())
            return false;

        if (_spi != 0 && checkSequenceNumber)
        {
            var sequenceNumber = ReadSequenceNumber();
            if (!GetAssociation().IsValidSequenceNumberNoReplayWindow(sequenceNumber))
                return false;
        }

        if (updateReplayWindow)
            GetAssociation().UpdateReplayWindow(ReadSequenceNumber());

        return true;
    }

    public bool GetPayload(ByteStream payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!IsAuthentic())
            return false;
        return DecryptPayload(payload);
    }

    public bool GetPayloadNoReplayWindow(ByteStream payload, bool authentic = true, bool checkSequenceNumber = true)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (authentic && !IsAuthenticNoReplayWindow(checkSequenceNumber))
            return false;
        return DecryptPayload(payload);
    }

    public bool GetPayload(Span<byte> destination, out int bytesWritten)
    {
        bytesWritten = 0;
        var payload = new ByteStream();
        if (!GetPayload(payload) || payload.Length > destination.Length)
            return false;
        payload.Span.CopyTo(destination);
        bytesWritten = payload.Length;
        return true;
    }

    public bool GetPayloadNoReplayWindow(Span<byte> destination, out int bytesWritten, bool authentic = true, bool checkSequenceNumber = true)
    {
        bytesWritten = 0;
        var payload = new ByteStream();
        if (!GetPayloadNoReplayWindow(payload, authentic, checkSequenceNumber) || payload.Length > destination.Length)
            return false;
        payload.Span.CopyTo(destination);
        bytesWritten = payload.Length;
        return true;
    }

    public void SetAccepted()
    {
        var sequenceNumber = ReadSequenceNumber();
        GetAssociation().UpdateReplayWindow(sequenceNumber);
    }

    public string GetDataHex() => Convert.ToHexString(_buffer);

    private bool DecryptPayload(ByteStream payload)
    {
        var association = GetAssociation();
        var ivOffset = sizeof(ushort) + sizeof(uint);
        if (ivOffset + SecurityAssociation.IvSize > _buffer.Length)
            return false;

        var iv = _buffer.AsSpan(ivOffset, SecurityAssociation.IvSize).ToArray();
        var cryptOffset = ivOffset + SecurityAssociation.IvSize;
        if (cryptOffset + SecurityAssociation.IcvSize > _buffer.Length)
            return false;

        var cryptLength = _buffer.Length - cryptOffset - SecurityAssociation.IcvSize;
        var crypt = _buffer.AsSpan(cryptOffset, cryptLength);
        var decrypted = association.Decrypt(crypt, iv);
        if (decrypted.Length == 0 || !TryRemovePadding(decrypted, out var actualLength))
            return false;

        payload.Assign(decrypted.AsSpan(0, actualLength));
        return true;
    }

    private bool IsValidSpi()
    {
        if (_buffer.Length < sizeof(ushort))
            return false;

        // The legacy implementation reads the packet SPI but validates the Session's expected SPI.
        _ = BinaryPrimitives.ReadUInt16LittleEndian(_buffer);
        return _database.Find(_spi);
    }

    private bool IsValidSize()
    {
        var minimum = sizeof(ushort) + sizeof(uint) + SecurityAssociation.IvSize + sizeof(byte) + SecurityAssociation.IcvSize;
        return _buffer.Length >= minimum;
    }

    private bool IsValidIcv()
    {
        var association = GetAssociation();
        if (_buffer.Length < SecurityAssociation.IcvSize)
            return false;

        var icvOffset = _buffer.Length - SecurityAssociation.IcvSize;
        var expected = association.GenerateIcv(_buffer.AsSpan(0, icvOffset));
        var actual = _buffer.AsSpan(icvOffset, SecurityAssociation.IcvSize);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private uint ReadSequenceNumber() =>
        BinaryPrimitives.ReadUInt32LittleEndian(_buffer.AsSpan(sizeof(ushort)));

    private SecurityAssociation GetAssociation() => _database.Get(_spi);

    private static byte[] GenerateIv(int length)
    {
        var iv = new byte[length];
        Random.Shared.NextBytes(iv);
        for (var i = 0; i < iv.Length; i++)
            iv[i] = (byte)('A' + Random.Shared.Next(0, 40));
        return iv;
    }

    private static byte[] GeneratePadding(int payloadLength)
    {
        var nPadBytes = SecurityAssociation.BlockSize - ((payloadLength + 1) % SecurityAssociation.BlockSize);
        // The 2014 source currently has nRand == 0, so no extra random blocks are appended.
        nPadBytes += SecurityAssociation.BlockSize * 0;

        var padding = new byte[nPadBytes];
        for (var i = 0; i < padding.Length; i++)
            padding[i] = checked((byte)(i + 1));
        return padding;
    }

    private static bool TryRemovePadding(ReadOnlySpan<byte> payload, out int actualLength)
    {
        actualLength = 0;
        if (payload.IsEmpty)
            return false;

        var padLength = payload[^1];
        if (padLength + 1 > payload.Length)
            return false;

        var padStart = payload.Length - padLength - 1;
        for (var i = 0; i < padLength; i++)
            if (payload[padStart + i] != i + 1)
                return false;

        actualLength = padStart;
        return true;
    }
}
