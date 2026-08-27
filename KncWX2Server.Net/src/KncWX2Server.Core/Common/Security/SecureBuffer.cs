using System.Buffers.Binary;
using System.Security.Cryptography;

namespace KncWX2Server.Core.Common.Security;

/// <summary>Managed equivalent of legacy KSecureBuffer.</summary>
public sealed class SecureBuffer
{
    private readonly SecurityAssociationDatabase _database;
    private byte[] _buffer = [];
    private readonly ushort _spi;

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

        var headerAndCrypt = new byte[checked(sizeof(ushort) + sizeof(uint) + iv.Length + crypt.Length)];
        BinaryPrimitives.WriteUInt16LittleEndian(headerAndCrypt, _spi);
        BinaryPrimitives.WriteUInt32LittleEndian(headerAndCrypt.AsSpan(sizeof(ushort)), association.SequenceNumber);
        iv.CopyTo(headerAndCrypt.AsSpan(sizeof(ushort) + sizeof(uint)));
        crypt.CopyTo(headerAndCrypt.AsSpan(sizeof(ushort) + sizeof(uint) + iv.Length));

        var packet = new byte[checked(headerAndCrypt.Length + SecurityAssociation.IcvSize)];
        headerAndCrypt.CopyTo(packet);
        if (!association.TryGenerateIcv(headerAndCrypt, packet.AsSpan(headerAndCrypt.Length)))
            return false;

        _buffer = packet;
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
        if (cryptLength == 0 || (cryptLength & (SecurityAssociation.BlockSize - 1)) != 0)
            return false;

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

        // The legacy implementation validates the Session's expected SPI,
        // not the wire SPI value, because sender/receiver SPIs may differ.
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
        Span<byte> expected = stackalloc byte[SecurityAssociation.IcvSize];
        if (!association.TryGenerateIcv(_buffer.AsSpan(0, icvOffset), expected))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            _buffer.AsSpan(icvOffset, SecurityAssociation.IcvSize), expected);
    }

    private uint ReadSequenceNumber() =>
        BinaryPrimitives.ReadUInt32LittleEndian(_buffer.AsSpan(sizeof(ushort)));

    private SecurityAssociation GetAssociation() => _database.Get(_spi);

    private static byte[] GenerateIv(int length)
    {
        var iv = new byte[length];
        for (var i = 0; i < iv.Length; i++)
            iv[i] = (byte)('A' + Random.Shared.Next(0, 40));
        return iv;
    }

    private static byte[] GeneratePadding(int payloadLength)
    {
        var padLength = SecurityAssociation.BlockSize - ((payloadLength + 1) % SecurityAssociation.BlockSize);
        // The active legacy source sets nRand to zero, so no extra block is added.
        var padding = new byte[padLength];
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
        {
            if (payload[padStart + i] != i + 1)
                return false;
        }

        actualLength = padStart;
        return true;
    }
}
