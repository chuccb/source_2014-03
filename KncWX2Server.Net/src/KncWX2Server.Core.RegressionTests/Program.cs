using System.Buffers.Binary;
using System.Text;
using KncWX2Server.Core.Common.Security;
using KncWX2Server.Core.Common.Serialization;

static class Program
{
    public static int Main()
    {
        SerializerUsesLegacyNetworkByteOrder();
        SerializerUsesWin32Utf16LeWStringBytes();
        SerBufferCompressionRoundTrips();
        SecureBufferRoundTripsAndAuthenticates();
        SecureBufferRejectsTamperingAndDuplicates();
        ReplayWindowMatchesLegacySemantics();
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

        var bytes = buffer.Data.ToArray();
        AssertSequence(
            [
                (byte)SerializeTag.DWord, 0x11, 0x22, 0x33, 0x44,
                (byte)SerializeTag.Short, 0x12, 0x34,
                (byte)SerializeTag.Bool, 0x01,
            ],
            bytes,
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
            buffer.Data.ToArray(),
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
        AssertSequence(original, buffer.Data.ToArray(), "SerBuffer compression round trip");
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
        AssertSequence(payloadBytes, output.ToArray(), "secure payload round trip");
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
