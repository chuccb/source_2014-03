using System.Buffers.Binary;
using System.Net.Sockets;

namespace KncWX2Server.Core;

/// <summary>Legacy KNC TCP frame: a little-endian USHORT containing the total frame size, including the size field itself.</summary>
public static class KncProtocol
{
    public const int FrameLengthFieldSize = sizeof(ushort);
    public const int MaxFrameSize = 32768;
    public const int MinSecureFrameSize = FrameLengthFieldSize + 2 + 4 + 8 + 8 + 10;

    public static ushort ReadFrameLength(ReadOnlySpan<byte> header)
    {
        if (header.Length < FrameLengthFieldSize)
            throw new InvalidDataException("The legacy frame header is incomplete.");

        return BinaryPrimitives.ReadUInt16LittleEndian(header);
    }

    public static int ValidateFrameLength(ushort frameLength)
    {
        if (frameLength < MinSecureFrameSize)
            throw new InvalidDataException($"Legacy frame length {frameLength} is smaller than the secure-buffer minimum.");
        if (frameLength > MaxFrameSize)
            throw new InvalidDataException($"Legacy frame length {frameLength} exceeds {MaxFrameSize} bytes.");
        return frameLength - FrameLengthFieldSize;
    }

    public static async ValueTask<byte[]> ReadSecureFrameAsync(
        NetworkStream stream,
        byte[] headerBuffer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(headerBuffer);
        if (headerBuffer.Length < FrameLengthFieldSize)
            throw new ArgumentException("Header buffer must contain at least two bytes.", nameof(headerBuffer));

        await stream.ReadExactlyAsync(headerBuffer.AsMemory(0, FrameLengthFieldSize), cancellationToken).ConfigureAwait(false);
        var frameLength = ReadFrameLength(headerBuffer);
        var secureLength = ValidateFrameLength(frameLength);

        var secureBuffer = GC.AllocateUninitializedArray<byte>(secureLength);
        await stream.ReadExactlyAsync(secureBuffer, cancellationToken).ConfigureAwait(false);
        return secureBuffer;
    }

    public static byte[] CreateFrame(ReadOnlySpan<byte> secureBuffer)
    {
        var totalLength = checked(FrameLengthFieldSize + secureBuffer.Length);
        if (totalLength < MinSecureFrameSize || totalLength > MaxFrameSize)
            throw new InvalidDataException($"Legacy frame length {totalLength} is outside the supported range.");

        var frame = GC.AllocateUninitializedArray<byte>(totalLength);
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)totalLength));
        secureBuffer.CopyTo(frame.AsSpan(FrameLengthFieldSize));
        return frame;
    }
}
