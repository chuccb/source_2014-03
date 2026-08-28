using KncWX2Server.Core.Common;
using KncWX2Server.Core.Common.Serialization;
using KncWX2Server.LoginServer;

static class ServerConnectRequestRegression
{
    public static void SerializerUsesExactNativeFieldOrderAndWidths()
    {
        var source = new ServerConnectRequest("10.0.0.1", 0x1234, 0x01020304);
        var buffer = new SerBuffer();
        var serializer = new KSerializer();

        Check(serializer.BeginWriting(buffer), "begin request write");
        Check(ServerConnectRequestSerialization.Write(serializer, in source), "write request");
        Check(serializer.EndWriting(), "end request write");

        var expected = new byte[]
        {
            0x00, 0x00, 0x00, 0x12,
            0x31, 0x00, 0x30, 0x00, 0x2E, 0x00, 0x30, 0x00,
            0x2E, 0x00, 0x30, 0x00, 0x2E, 0x00, 0x31, 0x00,
            0x12, 0x34,
            0x01, 0x02, 0x03, 0x04,
        };
        Check(expected.AsSpan().SequenceEqual(buffer.Data), "request exact wire bytes");

        buffer.Reset();
        Check(serializer.BeginReading(buffer), "begin request read");
        Check(ServerConnectRequestSerialization.Read(serializer, out var decoded), "read request");
        Check(serializer.EndReading(), "end request read");
        Check(decoded == source, "request round trip");
    }

    public static void RejectsMismatchedPeerIpWithNativeErrorCode()
    {
        var request = new ServerConnectRequest("10.0.0.2", 9300, 7);
        var outcome = LoginServerConnectRequestProcessor.Process(in request, "10.0.0.3");

        Check(outcome.Result == LoginServerConnectRequestResult.InvalidPeerIp, "invalid IP result");
        Check(outcome.Response.Ok == 8, "native ERR_VERIFY_02 value");
        Check(outcome.Response.Name.Length == 0, "error ACK uses default name");
    }

    public static void ForwardsExactRequestAfterPeerIpValidation()
    {
        var request = new ServerConnectRequest("10.0.0.2", 9300, 7);
        var outcome = LoginServerConnectRequestProcessor.Process(in request, "10.0.0.2");

        Check(outcome.Result == LoginServerConnectRequestResult.ForwardToLogDb, "valid IP forwards");
        Check(outcome.Request == request, "forwarded request unchanged");
        Check(outcome.Response == default, "forward path does not fabricate DB ACK");
    }

    private static void Check(bool condition, string name)
    {
        if (!condition)
            throw new InvalidOperationException($"Regression check failed: {name}");
    }
}
