using System.Net.Sockets;
using KncWX2Server.Core.Common;
using KncWX2Server.Core.Common.Security;
using KncWX2Server.Core.Common.Serialization;

namespace KncWX2Server.Host;

/// <summary>
/// Owns one accepted TCP connection. All socket, security-association, receive,
/// and send state belongs to this session instance.
/// </summary>
public sealed class KncServerSession
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly ServerOptions _options;
    private readonly SecurityAssociationDatabase _security = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly byte[] _frameHeader = new byte[KncProtocol.FrameLengthFieldSize];
    private readonly Func<KncServerSession, KEvent, ValueTask> _eventHandler;
    private readonly CancellationTokenSource _sessionCancellation;

    private ushort _spi;
    private int _packetAuthFailCount;
    private long _lastReceiveTimestamp;

    public KncServerSession(
        long sessionId,
        TcpClient client,
        ServerOptions options,
        Func<KncServerSession, KEvent, ValueTask> eventHandler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(eventHandler);

        SessionId = sessionId;
        _client = client;
        _stream = client.GetStream();
        _options = options;
        _eventHandler = eventHandler;
        _sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _lastReceiveTimestamp = Environment.TickCount64;
    }

    public long SessionId { get; }
    public ushort Spi => _spi;
    public int PacketAuthFailCount => _packetAuthFailCount;
    public EndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint;

    public async Task RunAsync()
    {
        try
        {
            await SendSecurityAssociationAsync(_sessionCancellation.Token).ConfigureAwait(false);
            await RunReceiveLoopAsync(_sessionCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_sessionCancellation.IsCancellationRequested)
        {
        }
        catch (EndOfStreamException)
        {
        }
        catch (IOException)
        {
        }
        finally
        {
            _sessionCancellation.Cancel();
            try
            {
                _client.Close();
            }
            catch (SocketException)
            {
            }

            _sendGate.Dispose();
            _sessionCancellation.Dispose();
        }
    }

    public void Disconnect() => _sessionCancellation.Cancel();

    public async ValueTask SendEventAsync(KEvent @event, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);

        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var serialized = new SerBuffer();
            var serializer = new KSerializer();

            if (!serializer.BeginWriting(serialized)
                || !serializer.Put(@event)
                || !serializer.EndWriting())
                throw new InvalidDataException("Failed to serialize KEvent.");

            var payload = serialized.ToArray();
            var secure = new SecureBuffer(_spi, _security);
            if (!secure.CreateNoReplayWindow(payload))
                throw new InvalidDataException("Failed to create a legacy secure packet.");

            var frame = KncProtocol.CreateFrame(secure.Data);
            await _stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private async Task SendSecurityAssociationAsync(CancellationToken cancellationToken)
    {
        var association = _security.CreateNew(out var newSpi);
        var eventPacket = new KEvent();
        eventPacket.SetData(0, ReadOnlySpan<long>.Empty, SystemEventIds.AcceptConnectionNot);

        var serializer = new KSerializer();
        if (!serializer.BeginWriting(eventPacket.Buffer)
            || !serializer.Put(newSpi)
            || !serializer.Put(association)
            || !serializer.EndWriting())
            throw new InvalidDataException("Failed to serialize the security-association handshake.");

        // The legacy server sends the handshake under SPI 0/default keys and
        // only then switches the session to the new association.
        await SendEventWithSpiAsync(eventPacket, spi: 0, cancellationToken).ConfigureAwait(false);
        _spi = newSpi;
    }

    private async ValueTask SendEventWithSpiAsync(KEvent @event, ushort spi, CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var serialized = new SerBuffer();
            var serializer = new KSerializer();
            if (!serializer.BeginWriting(serialized)
                || !serializer.Put(@event)
                || !serializer.EndWriting())
                throw new InvalidDataException("Failed to serialize KEvent.");

            var secure = new SecureBuffer(spi, _security);
            if (!secure.CreateNoReplayWindow(serialized.Data))
                throw new InvalidDataException("Failed to create a legacy secure packet.");

            var frame = KncProtocol.CreateFrame(secure.Data);
            await _stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private async Task RunReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var secureBytes = await KncProtocol.ReadSecureFrameAsync(_stream, _frameHeader, cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref _lastReceiveTimestamp, Environment.TickCount64);

            var secure = new SecureBuffer(_spi, secureBytes, _security);
            if (!secure.IsAuthenticNoReplayWindow(checkSequenceNumber: _options.CheckSequenceNumbers))
            {
                HandleAuthenticationFailure();
                continue;
            }

            var payload = new ByteStream();
            if (!secure.GetPayloadNoReplayWindow(payload, authentic: false, checkSequenceNumber: false))
            {
                HandleAuthenticationFailure();
                continue;
            }

            if (!TryDeserializeEvent(payload, out var @event))
            {
                Disconnect();
                return;
            }

            if (@event.EventId == SystemEventIds.HeartBeat)
                continue;

            if (@event.EventId == SystemEventIds.AcceptConnectionNot)
            {
                Disconnect();
                return;
            }

            await _eventHandler(this, @event).ConfigureAwait(false);
        }
    }

    private bool TryDeserializeEvent(ByteStream payload, out KEvent @event)
    {
        @event = new KEvent();
        var serialized = new SerBuffer();
        serialized.LoadSerialized(payload.Span, compressed: false);

        var serializer = new KSerializer();
        if (!serializer.BeginReading(serialized))
            return false;

        if (!serializer.Get(@event) || !serializer.EndReading())
            return false;

        if (@event.Buffer.IsCompressed && !@event.Buffer.Uncompress())
            return false;

        return true;
    }

    private void HandleAuthenticationFailure()
    {
        var count = ++_packetAuthFailCount;
        if (count > _options.PacketAuthFailLimit)
            Disconnect();
    }

    public bool IsHeartbeatTimedOut()
    {
        if (_sessionCancellation.IsCancellationRequested)
            return true;

        return Environment.TickCount64 - Volatile.Read(ref _lastReceiveTimestamp) > 60_000;
    }
}
