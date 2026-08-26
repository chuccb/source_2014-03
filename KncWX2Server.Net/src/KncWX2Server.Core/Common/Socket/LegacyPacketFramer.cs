using System.Buffers.Binary;

namespace KncWX2Server.Core.Common.Socket;

/// <summary>Legacy KSession-style TCP framing: one read may contain partial or multiple frames.</summary>
public sealed class LegacyPacketFramer(int bufferSize = 32 * 1024)
{
    private readonly byte[] _buffer = new byte[Math.Max(2, bufferSize)];
    private int _length;

    public int BufferedBytes => _length;

    public void Reset() => _length = 0;

    public bool Append(ReadOnlySpan<byte> data, Action<ReadOnlyMemory<byte>> packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (data.IsEmpty)
            return true;
        if (data.Length > _buffer.Length - _length)
            return false;

        data.CopyTo(_buffer.AsSpan(_length));
        _length += data.Length;

        while (_length >= sizeof(ushort))
        {
            var packetLength = BinaryPrimitives.ReadUInt16LittleEndian(_buffer);
            if (packetLength < sizeof(ushort) || packetLength > _buffer.Length)
                return false;
            if (_length < packetLength)
                break;

            packet(_buffer.AsMemory(0, packetLength));
            _length -= packetLength;
            if (_length != 0)
                _buffer.AsSpan(packetLength, _length).CopyTo(_buffer);
        }

        return true;
    }
}
