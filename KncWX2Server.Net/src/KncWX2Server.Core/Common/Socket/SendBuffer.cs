using System.Buffers;

namespace KncWX2Server.Core.Common.Socket;

/// <summary>
/// Legacy send-side buffering: a fixed active buffer plus a circular overflow queue.
/// The circular queue intentionally leaves one slot unused, matching KCircularQueue.
/// </summary>
public sealed class SendBuffer(int activeBufferSize = 32 * 1024, int queueSize = 32 * 1024)
{
    private readonly byte[] _active = new byte[Math.Max(1, activeBufferSize)];
    private int _activeLength;
    private readonly CircularQueue _queue = new(Math.Max(0, queueSize));

    public int ActiveLength => _activeLength;
    public int QueueLength => _queue.Count;
    public int Length => checked(_activeLength + _queue.Count);
    public bool IsEmpty => Length == 0;

    public int QueueCapacity => _queue.Capacity;

    public void ResizeQueue(int size) => _queue.Resize(size);

    public bool Enqueue(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return true;

        var copiedToActive = CopyToActive(data);
        var remaining = data[copiedToActive..];
        if (remaining.IsEmpty)
            return true;

        return _queue.Push(remaining) == remaining.Length;
    }

    public int CopyQueuedToActive()
    {
        if (_activeLength == _active.Length || _queue.Count == 0)
            return 0;

        var writable = _active.Length - _activeLength;
        var temp = ArrayPool<byte>.Shared.Rent(writable);
        try
        {
            var count = _queue.Pop(temp.AsSpan(0, writable));
            temp.AsSpan(0, count).CopyTo(_active.AsSpan(_activeLength));
            _activeLength += count;
            return count;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(temp);
        }
    }

    public ReadOnlyMemory<byte> ActiveMemory => _active.AsMemory(0, _activeLength);

    public void ConsumeActive(int bytesSent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytesSent);
        if (bytesSent > _activeLength)
            throw new ArgumentOutOfRangeException(nameof(bytesSent));

        if (bytesSent == _activeLength)
        {
            _activeLength = 0;
            return;
        }

        _active.AsSpan(bytesSent, _activeLength - bytesSent).CopyTo(_active);
        _activeLength -= bytesSent;
    }

    public void Clear()
    {
        _activeLength = 0;
        _queue.Resize(_queue.Capacity);
    }

    private int CopyToActive(ReadOnlySpan<byte> data)
    {
        var writable = _active.Length - _activeLength;
        var count = Math.Min(writable, data.Length);
        if (count == 0)
            return 0;

        data[..count].CopyTo(_active.AsSpan(_activeLength));
        _activeLength += count;
        return count;
    }
}
