using System.Runtime.InteropServices;

namespace KncWX2Server.Core.Common;

/// <summary>
/// Bit-packed byte storage equivalent to the legacy KncBitStream implementation.
/// Bits are numbered from the least-significant bit of byte zero.
/// </summary>
public sealed class KncBitStream
{
    private readonly List<byte> _bytes = [];

    public int Count => _bytes.Count;

    public byte this[int index]
    {
        get => _bytes[index];
        set => _bytes[index] = value;
    }

    public bool Get(int bitIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bitIndex);

        var byteIndex = bitIndex >> 3;
        if ((uint)byteIndex >= (uint)_bytes.Count)
            return false;

        var bit = bitIndex & 7;
        return (_bytes[byteIndex] & (1 << bit)) != 0;
    }

    public void Set(int bitIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bitIndex);

        var byteIndex = bitIndex >> 3;
        var bit = bitIndex & 7;

        while (_bytes.Count <= byteIndex)
            _bytes.Add(0);

        _bytes[byteIndex] |= (byte)(1 << bit);
    }

    public void Reset(int bitIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bitIndex);

        var byteIndex = bitIndex >> 3;
        if ((uint)byteIndex >= (uint)_bytes.Count)
            return;

        var bit = bitIndex & 7;
        _bytes[byteIndex] &= (byte)~(1 << bit);
        TrimTrailingZeroes();
    }

    public int GetBitSize() => checked(_bytes.Count * 8);

    public int GetByteSize() => _bytes.Count;

    public void Clear() => _bytes.Clear();

    public void Add(byte value) => _bytes.Add(value);

    public byte[] ToArray() => [.. _bytes];

    public ReadOnlySpan<byte> AsSpan() => CollectionsMarshal.AsSpan(_bytes);

    public void Dump(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        foreach (var value in _bytes)
            writer.Write($"{value:x2} ");
        writer.WriteLine();
    }

    private void TrimTrailingZeroes()
    {
        while (_bytes.Count > 0 && _bytes[^1] == 0)
            _bytes.RemoveAt(_bytes.Count - 1);
    }
}
