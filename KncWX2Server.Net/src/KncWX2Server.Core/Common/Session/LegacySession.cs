using System.Net;
using System.Net.Sockets;
using KncWX2Server.Core.Common.Security;
using KncWX2Server.Core.Common.Serialization;
using KncWX2Server.Core.Common.Socket;

namespace KncWX2Server.Core.Common.Session;

/// <summary>
/// Managed counterpart of the legacy KSession transport, security handshake, heartbeat,
/// and destruction lifecycle. Server-specific event dispatch remains virtual.
/// </summary>
public abstract class LegacySession : KPerformer, IAsyncDisposable
{
    public const int SecureKeyWaitingTimeMs = 5_000;
    public const uint DefaultPacketAuthFailLimit = 100;
    public const ushort HeartbeatEventId = 0;
    public const ushort AcceptConnectionEventId = 1;
    public const ushort ReserveDestroyEventId = 6;

    private readonly SecurityAssociationDatabase _securityDatabase;
    private readonly LegacyPacketFramer _framer = new();
    private readonly object _stateGate = new();
    private readonly TaskCompletionSource<bool> _securityKeyReceived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private TcpSocketConnection? _connection;
    private LegacyPacketCodec? _codec;
    private long _heartbeatTick = Environment.TickCount64;
    private int _destroyReserved;
    private int _authKeyReceived;
    private int _packetAuthFailureCount;
    private uint _packetAuthFailureLimit = DefaultPacketAuthFailLimit;
    private bool _checkSequenceNumbers;
    private bool _checkHeartbeat = true;
    private bool _disposing;

    protected LegacySession(bool isProxy, SecurityAssociationDatabase securityDatabase)
    {
        IsProxy = isProxy;
        _securityDatabase = securityDatabase ?? throw new ArgumentNullException(nameof(securityDatabase));
    }

    public bool IsProxy { get; }
    public bool IsConnected => _connection?.IsConnected == true;
    public bool IsSecurityKeyReceived => Volatile.Read(ref _authKeyReceived) != 0;
    public ushort SecuritySpi { get; private set; }
    public int PacketAuthenticationFailureCount => Volatile.Read(ref _packetAuthFailureCount);
    public uint PacketAuthenticationFailureLimit => _packetAuthFailureLimit;
    public bool CheckSequenceNumbers => _checkSequenceNumbers;
    public EndPoint? RemoteEndPoint => _connection?.RemoteEndPoint;
    public SocketDisconnectReason DisconnectReason => _connection?.DisconnectReason ?? SocketDisconnectReason.None;
    public uint CompletionKey => _connection?.CompletionKey ?? 0;
    public long HeartbeatTick => Interlocked.Read(ref _heartbeatTick);

    public void ConfigurePacketAuthentication(bool checkSequenceNumbers, uint failureLimit = DefaultPacketAuthFailLimit)
    {
        _checkSequenceNumbers = checkSequenceNumbers;
        _packetAuthFailureLimit = failureLimit;
    }

    public void SetHeartbeatCheckEnabled(bool enabled) => _checkHeartbeat = enabled;

    public Task AttachAsync(
        TcpSocketConnection connection,
        bool acceptedConnection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        lock (_stateGate)
        {
            if (_connection is not null)
                throw new InvalidOperationException("Session is already attached to a socket.");

            _connection = connection;
            _codec = new LegacyPacketCodec(_securityDatabase, SecuritySpi);
        }

        connection.Received += OnTransportReceivedAsync;
        connection.Disconnected += OnTransportDisconnectedAsync;
        connection.Start(cancellationToken);

        if (acceptedConnection && !IsProxy)
            SendAcceptConnectionHandshake();

        return Task.CompletedTask;
    }

    /// <summary>Proxy-side connect path. It waits for the legacy E_ACCEPT_CONNECTION_NOT handshake.</summary>
    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        if (!IsProxy)
            throw new InvalidOperationException("Only proxy sessions can initiate a connection.");

        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        if (addresses.Length == 0)
            throw new SocketException((int)SocketError.HostNotFound);

        var socket = new Socket(addresses[0].AddressFamily, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
        };

        try
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            await socket.ConnectAsync(addresses[0], port, cancellationToken).ConfigureAwait(false);
            await AttachAsync(new TcpSocketConnection(socket), acceptedConnection: false, cancellationToken).ConfigureAwait(false);
            await _securityKeyReceived.Task
                .WaitAsync(TimeSpan.FromMilliseconds(SecureKeyWaitingTimeMs), cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    public bool SendPacket(KEvent value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var connection = _connection;
        var codec = _codec;
        if (connection is null || codec is null || !connection.IsConnected)
            return false;

        var frame = codec.Encode(value);
        return connection.QueueSend(frame);
    }

    public bool SendId(uint destinationPerformerId, long uid, ReadOnlySpan<long> trace, ushort eventId)
    {
        var value = new KEvent();
        value.SetData(destinationPerformerId, trace, eventId);
        return value.Destination.AddUid(uid) && SendPacket(value);
    }

    public void ReserveDestroy()
    {
        if (Interlocked.Exchange(ref _destroyReserved, 1) != 0)
            return;

        var value = new KEvent();
        value.SetData((uint)GetPfId(), [], ReserveDestroyEventId);
        QueueingEvent(value);
    }

    public override void Tick()
    {
        base.Tick();

        if (Volatile.Read(ref _destroyReserved) != 0)
        {
            Interlocked.Exchange(ref _destroyReserved, 0);
            OnDestroy();
            return;
        }

        var now = Environment.TickCount64;
        var elapsed = now - Interlocked.Read(ref _heartbeatTick);

        if (IsProxy)
        {
            if (!IsConnected || !IsSecurityKeyReceived || elapsed <= 15_000)
                return;

            Interlocked.Exchange(ref _heartbeatTick, now);
            var heartbeat = new KEvent();
            heartbeat.SetData((uint)GetPfId(), [], HeartbeatEventId);
            SendPacket(heartbeat);
            return;
        }

        if (_checkHeartbeat && elapsed > 60_000)
            ReserveDestroy();
    }

    protected override void ProcessEvent(KEvent eventObject) => OnEvent(eventObject);

    protected virtual void OnEvent(KEvent eventObject)
    {
    }

    protected virtual ValueTask OnDisconnectedAsync(SocketDisconnectReason reason) => ValueTask.CompletedTask;

    protected virtual void OnDestroy()
    {
    }

    protected virtual void OnPacketAuthenticationFailure(int count)
    {
    }

    protected virtual void OnPacketAuthenticationLimitExceeded()
    {
    }

    private void SendAcceptConnectionHandshake()
    {
        if (SecuritySpi != 0)
            return;

        var association = _securityDatabase.CreateNew(out var serverSpi);

        var value = new KEvent();
        value.SetData((uint)GetPfId(), [], AcceptConnectionEventId);

        var serializer = new KSerializer();
        if (!serializer.BeginWriting(value.Buffer) ||
            !serializer.Put(serverSpi) ||
            !serializer.Put(association) ||
            !serializer.EndWriting())
            throw new InvalidDataException("Failed to build security handshake event.");

        // The handshake is protected with SPI 0 / default SA. Only after it is queued successfully
        // does this session switch to the new server-side SPI.
        if (!SendPacket(value))
        {
            _securityDatabase.Delete(serverSpi);
            throw new IOException("Failed to send security handshake event.");
        }

        SecuritySpi = serverSpi;
        _codec?.SetSecuritySpi(serverSpi);
        Volatile.Write(ref _authKeyReceived, 1);
    }

    private async ValueTask OnTransportReceivedAsync(ReadOnlyMemory<byte> data)
    {
        if (_codec is null)
            return;

        if (!_framer.Append(data.Span, DecodeFrame))
            await StopAsync(SocketDisconnectReason.ReceiveFailed).ConfigureAwait(false);
    }

    private void DecodeFrame(ReadOnlyMemory<byte> frame)
    {
        if (_codec is null)
            return;

        _codec.AppendReceived(frame.Span);

        while (_codec.TryDecode(_checkSequenceNumbers, out var value))
        {
            if (value is not null)
                ProcessIncomingEvent(value);
        }

        var failures = _codec.AuthenticationFailureCount;
        Interlocked.Exchange(ref _packetAuthFailureCount, failures);

        if (failures > _packetAuthFailureLimit)
        {
            OnPacketAuthenticationLimitExceeded();
            _ = StopAsync(SocketDisconnectReason.ReceiveFailed);
        }
        else if (failures > 0)
        {
            OnPacketAuthenticationFailure(failures);
        }
    }

    private void ProcessIncomingEvent(KEvent value)
    {
        switch (value.EventId)
        {
            case HeartbeatEventId when !IsProxy:
                break;

            case AcceptConnectionEventId when IsProxy:
                HandleAcceptConnection(value);
                break;

            default:
                QueueingEvent(value);
                break;
        }

        if (!IsProxy)
            Interlocked.Exchange(ref _heartbeatTick, Environment.TickCount64);
    }

    private void HandleAcceptConnection(KEvent value)
    {
        var serializer = new KSerializer();
        if (!serializer.BeginReading(value.Buffer) || !serializer.Get(out ushort serverSpi))
            throw new InvalidDataException("Accept-connection packet is missing its SPI.");

        var association = new SecurityAssociation();
        if (!serializer.Get(association) || !serializer.EndReading())
            throw new InvalidDataException("Accept-connection packet is missing its security association.");

        // The legacy client intentionally chooses a local/random SPI for its outbound packets.
        // The received server SPI is embedded in the handshake, but the server validates its own expected SPI.
        _securityDatabase.Insert(out var clientSpi, association);
        SecuritySpi = clientSpi;
        _codec?.SetSecuritySpi(clientSpi);
        Volatile.Write(ref _authKeyReceived, 1);
        _securityKeyReceived.TrySetResult(true);

        _ = serverSpi;
        Interlocked.Exchange(ref _heartbeatTick, Environment.TickCount64);
    }

    private ValueTask OnTransportDisconnectedAsync(SocketDisconnectReason reason)
    {
        if (!_disposing)
            ReserveDestroy();

        if (!IsSecurityKeyReceived)
            _securityKeyReceived.TrySetException(new IOException($"Security handshake failed: {reason}."));

        return OnDisconnectedAsync(reason);
    }

    private Task StopAsync(SocketDisconnectReason reason) =>
        _connection?.StopAsync(reason) ?? Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        _disposing = true;

        var connection = _connection;
        if (connection is not null)
            await connection.DisposeAsync().ConfigureAwait(false);

        if (!IsSecurityKeyReceived)
            _securityKeyReceived.TrySetCanceled();

        if (SecuritySpi != 0)
        {
            _securityDatabase.Delete(SecuritySpi);
            SecuritySpi = 0;
        }
    }
}
