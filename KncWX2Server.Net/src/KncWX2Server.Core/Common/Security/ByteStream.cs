using KncWX2Server.Core.Common.Serialization;

namespace KncWX2Server.Core.Common.Security;

/// <summary>Byte container used by the legacy KncSecurity layer.</summary>
public sealed class ByteStream
{
    private byte[] _buffer = [];

    public int Length => _buffer.Length;
    public bool IsEmpty => _buffer.Length == 0;
    public ReadOnlySpan<byte> Span => _buffer;
    public byte[] ToArray() => [.. _buffer];

    public byte this[int index]
    {
        get => _buffer[index];
        set => _buffer[index] = value;
    }

    public void Clear() => _buffer = [];

    public void Resize(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        Array.Resize(ref _buffer, length);
    }

    public void Assign(ReadOnlySpan<byte> value) => _buffer = value.ToArray();

    public void Append(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
            return;

        var oldLength = _buffer.Length;
        Array.Resize(ref _buffer, checked(oldLength + value.Length));
        value.CopyTo(_buffer.AsSpan(oldLength));
    }

    public void Append<T>(T value) where T : unmanaged
    {
        Span<T> one = stackalloc T[1];
        one[0] = value;
        Append(System.Runtime.InteropServices.MemoryMarshal.AsBytes(one));
    }

    public ByteStream Slice(int offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (offset > _buffer.Length || length > _buffer.Length - offset)
            throw new ArgumentOutOfRangeException(nameof(length));

        var result = new ByteStream();
        result.Assign(_buffer.AsSpan(offset, length));
        return result;
    }

    public static ByteStream From(ReadOnlySpan<byte> value)
    {
        var result = new ByteStream();
        result.Assign(value);
        return result;
    }

    public void Swap(ByteStream other)
    {
        ArgumentNullException.ThrowIfNull(other);
        (_buffer, other._buffer) = (other._buffer, _buffer);
    }

    public override string ToString() => Convert.ToHexString(_buffer);
}

public static class SecuritySerialization
{
    public static bool Write(KSerializer serializer, ByteStream value)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(value);
        return serializer.Put((uint)value.Length) && (value.Length == 0 || serializer.PutRaw(value.Span));
    }

    public static bool Read(KSerializer serializer, ByteStream value)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(value);

        value.Clear();
        if (!serializer.Get(out uint length) || length > int.MaxValue)
            return false;

        value.Resize((int)length);
        return length == 0 || serializer.GetRaw(value.AsSpan());
    }
}
