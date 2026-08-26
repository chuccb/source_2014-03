using System.Buffers.Binary;
using KncWX2Server.Core.Common.Security;
using KncWX2Server.Core.Common.Serialization;

namespace KncWX2Server.Core.Common.Socket;

/// <summary>
/// Legacy TCP packet framing and KEvent protection used by KSession.
/// Frame length includes the two-byte length field itself.
/// </summary>
public sealed class LegacyPacketCodec
{
    public const int MaxPacketSize = 32768;
    public const int LengthFieldSize = sizeof(ushort);

    private readonly SecurityAssociationDatabase _securityDatabase;
    private byte[] _receiveBuffer = [];
    private ushort _securitySpi;
    private int _authFailureCount;

    public LegacyPacketCodec(SecurityAssociationDatabase securityDatabase, ushort securitySpi = 0)
    {
        _securityDatabase = securityDatabase ?? throw new ArgumentNullException(nameof(securityDatabase));
        _securitySpi = securitySpi;
    }

    public ushort SecuritySpi => _securitySpi;
    public int AuthenticationFailureCount => _authFailureCount;

    public void SetSecuritySpi(ushort spi) => _securitySpi = spi;

    public byte[] Encode(KEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var serialized = new SerBuffer();
        var serializer = new KSerializer();
        if (!serializer.BeginWriting(serialized) || !serializer.Put(value) || !serializer.EndWriting())
            throw new InvalidDataException("Failed to serialize KEvent.");

        var secure = new SecureBuffer(_securitySpi, _securityDatabase);
        if (!secure.CreateNoReplayWindow(ByteStream.From(serialized.Data)))
            throw new InvalidDataException("Failed to protect KEvent payload.");

        var frameLength = checked(secure.Size + LengthFieldSize);
        if (frameLength > MaxPacketSize || frameLength > ushort.MaxValue)
            throw new InvalidDataException($"Legacy packet is too large: {frameLength} bytes.");

        var frame = new byte[frameLength];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)frameLength));
        secure.Data.CopyTo(frame.AsSpan(LengthFieldSize));
        return frame;
    }

    public void AppendReceived(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;

        var oldLength = _receiveBuffer.Length;
        Array.Resize(ref _receiveBuffer, checked(oldLength + data.Length));
        data.CopyTo(_receiveBuffer.AsSpan(oldLength));
    }

    public bool TryDecode(bool checkSequenceNumber, out KEvent? value)
    {
        value = null;
        if (_receiveBuffer.Length < LengthFieldSize)
            return false;

        var frameLength = BinaryPrimitives.ReadUInt16LittleEndian(_receiveBuffer);
        if (frameLength < LengthFieldSize || frameLength > MaxPacketSize)
            throw new InvalidDataException($"Invalid legacy packet length: {frameLength}.");

        if (_receiveBuffer.Length < frameLength)
            return false;

        var secureData = _receiveBuffer.AsSpan(LengthFieldSize, frameLength - LengthFieldSize);
        var secure = new SecureBuffer(_securitySpi, secureData, _securityDatabase);
        if (!secure.IsAuthenticNoReplayWindow(checkSequenceNumber))
        {
            _authFailureCount++;
            ConsumeFrame(frameLength);
            return false;
        }

        var payload = new ByteStream();
        if (!secure.GetPayloadNoReplayWindow(payload, authentic: false, checkSequenceNumber: false))
        {
            _authFailureCount++;
            ConsumeFrame(frameLength);
            return false;
        }

        var packetBuffer = new SerBuffer();
        packetBuffer.Write(payload.Span);
        var serializer = new KSerializer();
        if (!serializer.BeginReading(packetBuffer))
            throw new InvalidDataException("Unable to begin legacy packet deserialization.");

        var packet = new KEvent();
        var ok = serializer.Get(packet) && serializer.EndReading();
        ConsumeFrame(frameLength);

        if (!ok)
            throw new InvalidDataException("Legacy packet payload is not a valid KEvent.");

        value = packet;
        return true;
    }

    public bool TryDecode(out KEvent? value) => TryDecode(checkSequenceNumber: true, out value);

    private void ConsumeFrame(int frameLength)
    {
        var remaining = _receiveBuffer.Length - frameLength;
        if (remaining == 0)
        {
            _receiveBuffer = [];
            return;
        }

        _receiveBuffer.AsSpan(frameLength, remaining).CopyTo(_receiveBuffer);
        Array.Resize(ref _receiveBuffer, remaining);
    }
}
