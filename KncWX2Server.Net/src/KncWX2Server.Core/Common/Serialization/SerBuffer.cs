using System.Buffers.Binary;
using System.IO.Compression;

namespace KncWX2Server.Core.Common.Serialization;

/// <summary>Managed equivalent of legacy KSerBuffer.</summary>
public sealed class SerBuffer
{
    private byte[] _buffer = [];
    private int _readOffset;
    private bool _compressed;

    public bool IsCompressed => _compressed;
    public int Length => _buffer.Length;
    public int ReadLength => _buffer.Length - _readOffset;
    public ReadOnlySpan<byte> Data => _buffer;

    public void Write(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return;

        var oldLength = _buffer.Length;
        Array.Resize(ref _buffer, checked(oldLength + data.Length));
        data.CopyTo(_buffer.AsSpan(oldLength));
    }

    public bool Read(Span<byte> destination)
    {
        if (destination.Length > ReadLength)
            return false;

        _buffer.AsSpan(_readOffset, destination.Length).CopyTo(destination);
        _readOffset += destination.Length;
        return true;
    }

    public void Clear()
    {
        _buffer = [];
        _readOffset = 0;
        _compressed = false;
    }

    public void Reset() => _readOffset = 0;

    public byte[] ToArray() => [.. _buffer];

    internal void LoadSerialized(ReadOnlySpan<byte> data, bool compressed)
    {
        _buffer = data.ToArray();
        _readOffset = 0;
        _compressed = compressed;
    }

    public void Swap(SerBuffer other)
    {
        ArgumentNullException.ThrowIfNull(other);
        (_buffer, other._buffer) = (other._buffer, _buffer);
        (_readOffset, other._readOffset) = (other._readOffset, _readOffset);
        (_compressed, other._compressed) = (other._compressed, _compressed);
    }

    public bool Compress()
    {
        if (_compressed)
            return true;

        var originalLength = checked((uint)_buffer.Length);
        using var output = new MemoryStream();
        Span<byte> length = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(length, originalLength);
        output.Write(length);

        using (var zlib = new ZLibStream(output, CompressionLevel.Fastest, leaveOpen: true))
            zlib.Write(_buffer);

        _buffer = output.ToArray();
        _readOffset = 0;
        _compressed = true;
        return true;
    }

    public bool Uncompress()
    {
        if (!_compressed)
            return true;

        if (_buffer.Length < sizeof(uint))
            return false;

        var originalLength = BinaryPrimitives.ReadUInt32LittleEndian(_buffer);
        if (originalLength > int.MaxValue)
            return false;

        using var input = new MemoryStream(_buffer, sizeof(uint), _buffer.Length - sizeof(uint), writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        var result = new byte[checked((int)originalLength)];
        var offset = 0;

        while (offset < result.Length)
        {
            var read = zlib.Read(result, offset, result.Length - offset);
            if (read == 0)
                return false;
            offset += read;
        }

        if (zlib.ReadByte() != -1)
            return false;

        _buffer = result;
        _readOffset = 0;
        _compressed = false;
        return true;
    }

    public override bool Equals(object? obj) =>
        obj is SerBuffer other && _buffer.AsSpan().SequenceEqual(other._buffer);

    public override int GetHashCode() => HashCode.Combine(_buffer.Length, _compressed);
}
