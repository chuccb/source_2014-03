using System.Buffers.Binary;
using System.Text;
using KncWX2Server.Core;
using KncWX2Server.Core.Common;
using KncWX2Server.Core.Common.Routing;
using KncWX2Server.Core.Common.Security;
using KncWX2Server.Core.Common.Serialization;
using PerformerIds = RegressionPerformerIdAliases;

static class Program
{
    public static async Task<int> Main()
    {
        SerializerUsesLegacyNetworkByteOrder();
        SerializerUsesWin32Utf16LeWStringBytes();
        SerBufferCompressionRoundTrips();
        PerformerInfoUsesLegacyUidLimit();
        EventRoundTripsInLegacyFieldOrder();
        LegacyFrameUsesTwoByteTotalLength();
        SecureBufferRoundTripsAndAuthenticates();
        SecureBufferRejectsTamperingAndDuplicates();
        ReplayWindowMatchesLegacySemantics();
        await ActorQueueAndDeferredManagerLifecycle();
        await ActorManagerRegression.PreservesNativeInsertionOrder();
        await ActorManagerRegression.TickUsesStableSnapshot();
        await ActorManagerRegression.UpdateUidMatchesNativeDuplicateFailure();
        await ActorManagerRegression.GetsMinimumUidLikeNativeMapBegin();
        await PerformerRoutingPreservesLegacyDispatchSemantics();
        await ServerRolePerformerRegression.MapsRolesToNativeServerPerformers();
        LoginDispatchRegression.RejectsUnprovenLoginPayloads();
        ServerIdentityRegression.AppliesNativeKServerInfoFields();
        ServerListRegression.ReplacesAndSnapshotsServerGroups();
        Console.WriteLine("All KncWX2Server core regression checks passed.");
        return 0;
    }

    private static void SerializerUsesLegacyNetworkByteOrder()
    {
        var buffer = new SerBuffer();
        var serializer = new KSerializer();
        Check(serializer.BeginWriting(buffer, tagging: true), "begin write");
        Check(serializer.Put((uint)0x11223344), "write uint");
        Check(serializer.Put((short)0x1234), "write short");
        Check(serializer.Put(true), "write bool");
        Check(serializer.EndWriting(), "end write");

        AssertSequence(
            [
                (byte)SerializeTag.DWord, 0x11, 0x22, 0x33, 0x44,
                (byte)SerializeTag.Short, 0x12, 0x34,
                (byte)SerializeTag.Bool, 0x01,
            ],
            buffer.Data,
            "serializer primitive wire order");

        buffer.Reset();
        Check(serializer.BeginReading(buffer, tagging: true), "begin read");
        Check(serializer.Get(out uint value) && value == 0x11223344, "read uint");
        Check(serializer.Get(out short shortValue) && shortValue == 0x1234, "read short");
        Check(serializer.Get(out bool flag) && flag, "read bool");
        Check(serializer.EndReading(), "end read");
    }

    private static void SerializerUsesWin32Utf16LeWStringBytes()
    {
        var buffer = new SerBuffer();
        var serializer = new KSerializer();
        const string value = "AB";
        Check(serializer.BeginWriting(buffer, tagging: true), "begin wstring write");
        Check(serializer.PutWString(value), "write wstring");
        Check(serializer.EndWriting(), "end wstring write");

        AssertSequence(
            [
                (byte)SerializeTag.WString,
                0x00, 0x00, 0x00, 0x04,
                0x41, 0x00, 0x42, 0x00,
            ],
            buffer.Data,
            "wstring wire bytes");

        buffer.Reset();
        Check(serializer.BeginReading(buffer, tagging: true), "begin wstring read");
        Check(serializer.GetWString(out var decoded) && decoded == value, "read wstring");
        Check(serializer.EndReading(), "end wstring read");
    }

    private static void SerBufferCompressionRoundTrips()
    {
        var buffer = new SerBuffer();
        var original = Encoding.ASCII.GetBytes(new string('K', 4096));
        buffer.Write(original);
        Check(buffer.Compress(), "compress SerBuffer");
        Check(buffer.IsCompressed, "compressed flag");
        Check(buffer.Uncompress(), "uncompress SerBuffer");
        Check(!buffer.IsCompressed, "uncompressed flag");
        AssertSequence(original, buffer.Data, "SerBuffer compression round trip");
    }

    private static void PerformerInfoUsesLegacyUidLimit()
    {
        var performer = new KPerformerInfo();
        for (var i = 0; i < KPerformerInfo.MaxUidCount; i++)
            Check(performer.AddUid(i), $"add uid {i}");

        Check(performer.UidListSize == 2000, "legacy max UID count");
        Check(!performer.AddUid(2000), "reject UID above legacy limit");
        Check(performer.GetFirstUid() == 0, "first sorted UID");
    }

    private static void EventRoundTripsInLegacyFieldOrder()
    {
        var source = new KEvent();
        source.SetData(0x12345678, [100, 50], 0x2233);
        Check(source.Destination.AddUid(9001), "event destination uid");
        source.Buffer.Write([0xAA, 0xBB, 0xCC]);

        var buffer = new SerBuffer();
        var serializer = new KSerializer();
        Check(serializer.BeginWriting(buffer), "event begin write");
        Check(serializer.Put(source), "event write");
        Check(serializer.EndWriting(), "event end write");

        var expected = new SerBuffer();
        var expectedSerializer = new KSerializer();
        Check(expectedSerializer.BeginWriting(expected), "expected event begin write");
        Check(expectedSerializer.Put(source.Destination), "expected destination");
        Check(expectedSerializer.Put((long)100), "expected first trace");
        Check(expectedSerializer.Put((long)50), "expected last trace");
        Check(expectedSerializer.Put((ushort)0x2233), "expected event id");
        Check(expectedSerializer.Put(source.Buffer), "expected buffer");
        Check(expectedSerializer.EndWriting(), "expected event end write");
        AssertSequence(expected.Data, buffer.Data, "KEvent field order");

        buffer.Reset();
        var decoded = new KEvent();
        Check(serializer.BeginReading(buffer), "event begin read");
        Check(serializer.Get(decoded), "event read");
        Check(serializer.EndReading(), "event end read");
        Check(decoded.Destination.PerformerId == source.Destination.PerformerId, "event performer id");
        Check(decoded.Destination.FindUid(9001), "event destination uid round trip");
        Check(decoded.FirstTrace == 100 && decoded.LastTrace == 50, "event trace round trip");
        Check(decoded.EventId == 0x2233, "event id round trip");
        AssertSequence([0xAA, 0xBB, 0xCC], decoded.Buffer.Data, "event payload round trip");
    }

    private static void LegacyFrameUsesTwoByteTotalLength()
    {
        var secure = new byte[SecurityAssociation.IcvSize + SecurityAssociation.IvSize + SecurityAssociation.BlockSize + 2 + 4];
        for (var i = 0; i < secure.Length; i++)
            secure[i] = (byte)i;

        var frame = KncProtocol.CreateFrame(secure);
        Check(BinaryPrimitives.ReadUInt16LittleEndian(frame) == frame.Length, "legacy total frame length");
        Check(KncProtocol.ReadFrameLength(frame) == frame.Length, "legacy frame header parse");
        Check(KncProtocol.ValidateFrameLength((ushort)frame.Length) == secure.Length, "legacy frame payload length");
        AssertSequence(secure, frame.AsSpan(KncProtocol.FrameLengthFieldSize), "legacy secure frame contents");

        Check(KncProtocol.ValidateFrameLength(20) == 18, "short complete frame reaches security layer");
    }

    private static void SecureBufferRoundTripsAndAuthenticates()
    {
        var database = new SecurityAssociationDatabase();
        var association = database.CreateNew(out var spi);
        var payloadBytes = Encoding.ASCII.GetBytes("legacy security payload");
        var sender = new SecureBuffer(spi, database);
        Check(sender.Create(payloadBytes), "secure create");
        Check(sender.Size >= 2 + 4 + 8 + 8 + 10, "secure minimum size");

        var packet = sender.Data.ToArray();
        Check(BinaryPrimitives.ReadUInt16LittleEndian(packet) == spi, "wire SPI little endian");
        Check(BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(2)) == 1, "wire sequence little endian");

        var receiver = new SecureBuffer(spi, packet, database);
        Check(receiver.IsAuthentic(), "secure authentication");
        var output = new ByteStream();
        Check(receiver.GetPayload(output), "secure payload decode");
        AssertSequence(payloadBytes, output.Span, "secure payload round trip");
        receiver.SetAccepted();

        Check(association.SequenceNumber == 2, "sender sequence increment");
    }

    private static void SecureBufferRejectsTamperingAndDuplicates()
    {
        var database = new SecurityAssociationDatabase();
        database.CreateNew(out var spi);
        var payload = Encoding.ASCII.GetBytes("authenticated");
        var sender = new SecureBuffer(spi, database);
        Check(sender.CreateNoReplayWindow(payload), "create no-RWM");
        var packet = sender.Data.ToArray();

        var receiver = new SecureBuffer(spi, packet, database);
        Check(receiver.IsAuthenticNoReplayWindow(), "no-RWM authentication");
        Check(!receiver.IsAuthenticNoReplayWindow(), "duplicate no-RWM packet rejected");

        packet[^1] ^= 0x01;
        var tampered = new SecureBuffer(spi, packet, database);
        Check(!tampered.IsAuthenticNoReplayWindow(checkSequenceNumber: false), "tampered ICV rejected");
    }

    private static void ReplayWindowMatchesLegacySemantics()
    {
        var association = new SecurityAssociation();
        Check(association.IsValidSequenceNumber(5), "sequence 5 initially valid");
        association.UpdateReplayWindow(5);
        Check(!association.IsValidSequenceNumber(5), "duplicate sequence rejected");
        Check(association.IsValidSequenceNumber(4), "older sequence inside window is valid");
        association.UpdateReplayWindow(4);
        Check(!association.IsValidSequenceNumber(4), "accepted old sequence becomes duplicate");
        Check(association.IsValidSequenceNumber(3), "another sequence inside window is valid");
        association.UpdateReplayWindow(3);
        Check(!association.IsValidSequenceNumber(0), "zero sequence rejected");
    }

    private static async Task ActorQueueAndDeferredManagerLifecycle()
    {
        var processed = new List<ushort>();
        var manager = new ServerActorManager();
        var actor = manager.Create(
            42,
            (_, @event) =>
            {
                processed.Add(@event.EventId);
                return ValueTask.CompletedTask;
            });

        var first = new KEvent();
        first.SetData(0, ReadOnlySpan<long>.Empty, 10);
        var second = new KEvent();
        second.SetData(0, ReadOnlySpan<long>.Empty, 20);
        actor.QueueingEvent(first);
        actor.QueueingEvent(second);

        Check(manager.Count == 0, "actor add is deferred");
        await manager.TickAsync();
        Check(manager.Count == 1, "actor registered after tick");
        Check(actor.Uid != 0 && (actor.Uid & 0x4000000000000000L) != 0, "temporary UID marker");
        Check(processed.Count == 0, "new actor not processed in insertion tick");

        await manager.TickAsync();
        Check(processed.Count == 2 && processed[0] == 10 && processed[1] == 20, "FIFO actor event processing");

        manager.ReserveDelete(actor);
        Check(manager.Count == 1, "actor deletion is deferred");
        await manager.TickAsync();
        Check(manager.Count == 0, "actor removed after tick");

        var disconnectedBeforeAdd = manager.Create(
            43,
            static (_, _) => ValueTask.CompletedTask);
        manager.ReserveDelete(disconnectedBeforeAdd);
        await manager.TickAsync();
        Check(manager.Count == 0, "pre-add actor cancellation leaves no ghost actor");
    }

    private static async Task PerformerRoutingPreservesLegacyDispatchSemantics()
    {
        var actors = new ServerActorManager();
        var performers = new ServerPerformerManager();
        var router = new ServerEventRouter(PerformerIds.PiGsServer, actors, performers);

        var actor = actors.Create(1, static (_, _) => ValueTask.CompletedTask);
        await actors.TickAsync();

        var local = new KEvent();
        local.SetData(PerformerIds.PiGsUser, ReadOnlySpan<long>.Empty, 100);
        Check(local.Destination.AddUid(actor.Uid), "local route destination");
        Check(router.Route(local) == ServerEventRouteResult.Routed, "local user route");
        Check(actor.QueueSize == 1, "local user queueing");

        var remoteCandidate = new KEvent();
        remoteCandidate.SetData(PerformerIds.PiGsUser, [actor.Uid], 101);
        Check(remoteCandidate.Destination.AddUid(9_999_999), "remote route destination");
        Check(router.Route(remoteCandidate) == ServerEventRouteResult.RemoteRouteRequired, "mixed local/remote route");
        Check(actor.QueueSize == 2, "mixed route still queues local target");

        var internalEvents = new List<ushort>();
        Check(performers.Register(new ServerPerformer(
            PerformerIds.PiGsGameDb2,
            (_, @event) =>
            {
                internalEvents.Add(@event.EventId);
                return ValueTask.CompletedTask;
            })), "register internal performer");

        var internalEvent = new KEvent();
        internalEvent.SetData(PerformerIds.PiGsGameDb2, ReadOnlySpan<long>.Empty, 200);
        Check(router.Route(internalEvent) == ServerEventRouteResult.Routed, "internal performer route");
        await performers.TickAsync();
        Check(internalEvents.Count == 1 && internalEvents[0] == 200, "internal performer FIFO dispatch");

        var serverEvents = new List<ushort>();
        Check(performers.Register(new ServerPerformer(
            (uint)PerformerId.LoginServer,
            (_, @event) =>
            {
                serverEvents.Add(@event.EventId);
                return ValueTask.CompletedTask;
            })), "register local Login server performer");

        var serverEvent = new KEvent();
        serverEvent.SetData((uint)PerformerId.LoginServer, ReadOnlySpan<long>.Empty, 300);
        Check(router.Route(serverEvent) == ServerEventRouteResult.Routed, "local Login server performer route");
        await performers.TickAsync();
        Check(serverEvents.Count == 1 && serverEvents[0] == 300, "local Login server performer dispatch");
    }

    private static void AssertSequence(ReadOnlySpan<byte> expected, ReadOnlySpan<byte> actual, string name)
    {
        Check(expected.SequenceEqual(actual), name);
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException($"Regression check failed: {name}");
    }
}
