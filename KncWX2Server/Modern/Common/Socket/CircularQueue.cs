namespace KncWX2Server.Common.Socket;

/// <summary>
/// Fixed-size byte ring buffer matching the legacy KCircularQueue behavior.
/// One byte is intentionally left unused so full and empty states remain distinct.
/// </summary>
public sealed class CircularQueue(int capacity)
{
    private byte[] _buffer = new byte[Math.Max(0, capacity)];
    private int _head;
    private int _tail;

    public int Capacity => _buffer.Length;
    public bool IsEmpty => _buffer.Length == 0;
    public int Count => GetDataSize();

    public void Resize(int size)
    {
        if (size < 0)
            throw new ArgumentOutOfRangeException(nameof(size));

        _buffer = new byte[size];
        _head = 0;
        _tail = 0;
    }

    public int Push(ReadOnlySpan<byte> data)
    {
        if (_buffer.Length == 0)
            return 0;

        var size = Math.Min(data.Length, GetFreeSize());
        if (_buffer.Length - _tail <= size)
        {
            var rearSize = _buffer.Length - _tail;
            data[..rearSize].CopyTo(_buffer.AsSpan(_tail, rearSize));
            data[rearSize..size].CopyTo(_buffer.AsSpan(0, size - rearSize));
            _tail = size - rearSize;
        }
        else
        {
            data[..size].CopyTo(_buffer.AsSpan(_tail, size));
            _tail += size;
        }

        return size;
    }

    public int Pop(Span<byte> destination)
    {
        if (_buffer.Length == 0)
            return 0;

        var rearSize = Math.Min(destination.Length, GetRearDataSize());
        var frontSize = Math.Min(destination.Length - rearSize, GetFrontDataSize());

        _buffer.AsSpan(_head, rearSize).CopyTo(destination);
        _head += rearSize;
        if (_head == _buffer.Length)
            _head = 0;

        if (rearSize < destination.Length)
        {
            _buffer.AsSpan(_head, frontSize).CopyTo(destination[rearSize..]);
            _head += frontSize;
        }

        return rearSize + frontSize;
    }

    public int GetDataSize() =>
        _buffer.Length == 0 ? 0 : (_tail - _head + _buffer.Length) % _buffer.Length;

    private int GetFreeSize() =>
        _buffer.Length == 0 ? 0 : (_head - _tail + _buffer.Length - 1) % _buffer.Length;

    private int GetRearDataSize() =>
        _head <= _tail ? _tail - _head : _buffer.Length - _head;

    private int GetFrontDataSize() =>
        _head <= _tail ? 0 : _tail;
}
