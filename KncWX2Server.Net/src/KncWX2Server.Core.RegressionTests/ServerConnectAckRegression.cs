using KncWX2Server.Core.Common;
using KncWX2Server.Core.Common.Fsm;
using KncWX2Server.Core.Common.Serialization;
using KncWX2Server.LoginServer;

static class ServerConnectAckRegression
{
    public static void SerializerRoundTripsEffectiveIntegrationLayout()
    {
        var source = new ServerConnectAck(
            Ok: 0,
            Uid: 0x0102030405060708,
            DbRegisteredServerGroupId: 17,
            LocalServerGroupId: 23,
            ServerUid: 0x1122334455667788,
            Name: "Login",
            MaxNum: 1200,
            IpAddress: "127.0.0.1",
            Port: 9400,
            Version: 20240517);

        var buffer = new SerBuffer();
        var writer = new KSerializer();
        Check(writer.BeginWriting(buffer), "begin ACK write");
        Check(ServerConnectAckSerialization.Write(writer, source), "write ACK");
        Check(writer.EndWriting(), "end ACK write");

        buffer.Reset();
        var reader = new KSerializer();
        Check(reader.BeginReading(buffer), "begin ACK read");
        Check(ServerConnectAckSerialization.Read(reader, out var decoded), "read ACK");
        Check(reader.EndReading(), "end ACK read");
        Check(decoded == source, "ACK round trip");
    }

    public static async Task AuthenticatesAndUpdatesActorUid()
    {
        var actors = new ServerActorManager();
        var actor = actors.Create(1, static (_, _) => ValueTask.CompletedTask);
        await actors.TickAsync();
        actor.SetFsm(new DefaultFsm(), DefaultFsm.StateInit);

        var packet = new ServerConnectAck(
            0,
            9001,
            3,
            4,
            7777,
            "Game-1",
            1000,
            "10.0.0.2",
            9300,
            42);

        var outcome = LoginServerConnectAckProcessor.Process(actor, actors, in packet);
        Check(outcome.Result == LoginServerConnectAckResult.Authenticated, "ACK authenticates actor");
        Check(!outcome.DestroyActor, "successful ACK keeps actor");
        Check(actor.Uid == packet.Uid, "actor UID updated");
        Check(actor.Name == packet.Name, "actor name updated");
        Check(actor.Fsm is { CurrentState: DefaultFsm.StateLogined }, "FSM enters logged-in state");
        Check(outcome.Response == packet, "outbound ACK preserved");
    }

    public static async Task RejectsDuplicateUidAfterSendingSuccessResponse()
    {
        var actors = new ServerActorManager();
        var existing = actors.Create(1, static (_, _) => ValueTask.CompletedTask);
        var candidate = actors.Create(2, static (_, _) => ValueTask.CompletedTask);
        await actors.TickAsync();
        candidate.SetFsm(new DefaultFsm(), DefaultFsm.StateInit);

        Check(actors.UpdateUid(existing.Uid, 12345), "prepare duplicate UID");
        var packet = new ServerConnectAck(0, 12345, 1, 1, 1, "Duplicate", 100, "127.0.0.1", 9000, 1);
        var outcome = LoginServerConnectAckProcessor.Process(candidate, actors, in packet);

        Check(outcome.Result == LoginServerConnectAckResult.DuplicateUid, "duplicate UID detected");
        Check(outcome.DestroyActor, "duplicate UID destroys actor");
        Check(outcome.Response == packet && outcome.Response.Ok == 0, "success ACK remains the already-sent response");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException($"Regression check failed: {name}");
    }
}
