using System.Buffers.Binary;
using System.Text;
using KncWX2Server.Core;
using KncWX2Server.Core.Common;
using KncWX2Server.Core.Common.Security;
using KncWX2Server.Core.Common.Serialization;

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
        await PerformerRoutingPreservesLegacyDispatchSemantics();
        LoginDispatchRegression.RejectsUnprovenLoginPayloads();
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