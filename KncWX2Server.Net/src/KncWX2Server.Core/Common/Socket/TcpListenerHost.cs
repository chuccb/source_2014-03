using System.Net;
using System.Net.Sockets;

namespace KncWX2Server.Core.Common.Socket;

public sealed class TcpListenerHost : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly bool _tcpNoDelay;
    private CancellationTokenSource? _lifetime;
    private Task? _acceptTask;
    private int _started;

    public TcpListenerHost(IPEndPoint endpoint, bool tcpNoDelay = false)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        _listener = new TcpListener(endpoint);
        _tcpNoDelay = tcpNoDelay;
    }

    public bool IsStarted => Volatile.Read(ref _started) != 0;
    public event Func<TcpSocketConnection, ValueTask>? Accepted;

    public void Start(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("Listener has already been started.");

        _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listener.Start();
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptTask = AcceptLoopAsync(_lifetime.Token);
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime?.Cancel();
        _listener.Stop();
        if (_acceptTask is not null)
        {
            try { await _acceptTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (SocketException) { }
        }
        _lifetime?.Dispose();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var socket = await _listener.AcceptSocketAsync(cancellationToken).ConfigureAwait(false);
                socket.NoDelay = _tcpNoDelay;
                var connection = new TcpSocketConnection(socket);
                var accepted = Accepted;
                if (accepted is null)
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                    continue;
                }

                try { await accepted(connection).ConfigureAwait(false); }
                catch
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (ObjectDisposedException) { }
        catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
    }
}
