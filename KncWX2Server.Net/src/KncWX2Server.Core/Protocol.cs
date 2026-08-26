using System.Buffers.Binary;
using System.Net.Sockets;

namespace KncWX2Server.Core;

public readonly record struct Packet(ushort Opcode, ushort Flags, ReadOnlyMemory<byte> Payload);

public static class KncProtocol
{
    public const int HeaderSize = sizeof(int) + sizeof(ushort) + sizeof(ushort);
    public const int MaxPayloadBytes = 1024 * 1024;

    public static async ValueTask<Packet> ReadAsync(NetworkStream stream, int maxPayloadBytes, CancellationToken cancellationToken)
    {
        var header = new byte[HeaderSize];
        await stream.ReadExactlyAsync(header, cancellationToken);

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        var opcode = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(sizeof(int)));
        var flags = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(sizeof(int) + sizeof(ushort)));

        if ((uint)payloadLength > (uint)maxPayloadBytes)
            throw new InvalidDataException($"Packet payload {payloadLength} exceeds configured limit {maxPayloadBytes}.");

        var payload = GC.AllocateUninitializedArray<byte>(payloadLength);
        if (payloadLength != 0)
            await stream.ReadExactlyAsync(payload, cancellationToken);

        return new(opcode, flags, payload);
    }

    public static async ValueTask WriteAsync(NetworkStream stream, Packet packet, CancellationToken cancellationToken)
    {
        if (packet.Payload.Length > MaxPayloadBytes)
            throw new InvalidDataException($"Packet payload {packet.Payload.Length} exceeds the protocol limit {MaxPayloadBytes}.");

        var buffer = GC.AllocateUninitializedArray<byte>(HeaderSize + packet.Payload.Length);
        var span = buffer.AsSpan();

        BinaryPrimitives.WriteInt32LittleEndian(span, packet.Payload.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(span[sizeof(int)..], packet.Opcode);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(sizeof(int) + sizeof(ushort))..], packet.Flags);
        packet.Payload.Span.CopyTo(span[HeaderSize..]);

        await stream.WriteAsync(buffer, cancellationToken);
    }

    public static bool TryReadHeader(ReadOnlySpan<byte> header, out int payloadLength, out ushort opcode, out ushort flags)
    {
        payloadLength = 0;
        opcode = 0;
        flags = 0;

        if (header.Length < HeaderSize)
            return false;

        payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        opcode = BinaryPrimitives.ReadUInt16LittleEndian(header[sizeof(int)..]);
        flags = BinaryPrimitives.ReadUInt16LittleEndian(header[(sizeof(int) + sizeof(ushort))..]);
        return payloadLength >= 0;
    }
}
