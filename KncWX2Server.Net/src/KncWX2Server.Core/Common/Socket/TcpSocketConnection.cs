using System.Net;
using System.Net.Sockets;

namespace KncWX2Server.Core.Common.Socket;

public enum SocketDisconnectReason
{
    None = 0,
    RemoteClosed,
    ReceiveFailed,
    SendFailed,
    SendBufferFull,
    Cancelled,
    LocalShutdown,
}

public sealed class TcpSocketConnection : IAsyncDisposable
{
    public const int MaxPacketSize = 32 * 1024;

    private readonly Socket _socket;
    private readonly Lock _sendGate = new();
    private readonly SendBuffer _sendBuffer;
    private readonly SemaphoreSlim _sendWake = new(0);
    private CancellationTokenSource? _lifetime;
    private int _started;
    private int _closed;
    private int _disconnectRaised;

    public TcpSocketConnection(Socket socket, int sendQueueSize = MaxPacketSize)
    {
        ArgumentNullException.ThrowIfNull(socket);
        _socket = socket;
        _sendBuffer = new(MaxPacketSize, sendQueueSize);
    }

    public Socket Socket => _socket;
    public bool IsConnected => Volatile.Read(ref _closed) == 0 && _socket.Connected;
    public bool IsSending { get; private set; }
    public int SendQueueLength { get { lock (_sendGate) return _sendBuffer.QueueLength; } }
    public EndPoint? RemoteEndPoint => _socket.RemoteEndPoint;
    public SocketDisconnectReason DisconnectReason { get; private set; }
    public uint CompletionKey { get; private set; }

    public event Func<ReadOnlyMemory<byte>, ValueTask>? Received;
    public event Func<SocketDisconnectReason, ValueTask>? Disconnected;

    public void SetCompletionKey(uint key) => CompletionKey = key;
    public void SetTcpNoDelay(bool enabled) => _socket.NoDelay = enabled;

    public void Start(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("Connection already started.");

        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _ = ReceiveLoopAsync(_lifetime.Token);
        _ = SendLoopAsync(_lifetime.Token);
    }

    public bool QueueSend(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty || Volatile.Read(ref _closed) != 0)
            return false;

        lock (_sendGate)
        {
            if (!_sendBuffer.Enqueue(data))
            {
                DisconnectReason = SocketDisconnectReason.SendBufferFull;
                return false;
            }
        }

        _sendWake.Release();
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        CloseTransport(SocketDisconnectReason.LocalShutdown);
        _lifetime?.Dispose();
        _sendWake.Dispose();
        await ValueTask.CompletedTask;
    }

    public Task StopAsync(SocketDisconnectReason reason = SocketDisconnectReason.LocalShutdown)
    {
        CloseTransport(reason);
        return Task.CompletedTask;
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[MaxPacketSize];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var received = await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
                if (received == 0)
                {
                    CloseTransport(SocketDisconnectReason.RemoteClosed);
                    return;
                }

                var handler = Received;
                if (handler is not null)
                    await handler(buffer.AsMemory(0, received)).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (SocketException)
        {
            CloseTransport(SocketDisconnectReason.ReceiveFailed);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task SendLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await _sendWake.WaitAsync(cancellationToken).ConfigureAwait(false);

                while (!cancellationToken.IsCancellationRequested)
                {
                    ReadOnlyMemory<byte> current;
                    lock (_sendGate)
                    {
                        _sendBuffer.CopyQueuedToActive();
                        current = _sendBuffer.ActiveMemory;
                        IsSending = !current.IsEmpty;
                    }

                    if (current.IsEmpty)
                        break;

                    int sent;
                    try
                    {
                        sent = await _socket.SendAsync(current, SocketFlags.None, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (SocketException)
                    {
                        CloseTransport(SocketDisconnectReason.SendFailed);
                        return;
                    }
                    catch (ObjectDisposedException)
                    {
                        return;
                    }

                    if (sent <= 0)
                    {
                        CloseTransport(SocketDisconnectReason.SendFailed);
                        return;
                    }

                    lock (_sendGate)
                    {
                        _sendBuffer.ConsumeActive(sent);
                        _sendBuffer.CopyQueuedToActive();
                        IsSending = !_sendBuffer.IsEmpty;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void CloseTransport(SocketDisconnectReason reason)
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
            return;

        DisconnectReason = reason;
        _lifetime?.Cancel();

        try { _socket.Shutdown(SocketShutdown.Both); }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }

        _socket.Dispose();

        if (Interlocked.Exchange(ref _disconnectRaised, 1) == 0)
            _ = RaiseDisconnectedAsync(reason);
    }

    private async Task RaiseDisconnectedAsync(SocketDisconnectReason reason)
    {
        var handler = Disconnected;
        if (handler is not null)
            await handler(reason).ConfigureAwait(false);
    }
}
