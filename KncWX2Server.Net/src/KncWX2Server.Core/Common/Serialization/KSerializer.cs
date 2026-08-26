using System.Buffers.Binary;
using System.Text;

namespace KncWX2Server.Core.Common.Serialization;

/// <summary>Primitive-compatible implementation of the legacy KSerializer wire format.</summary>
public sealed class KSerializer
{
    private SerBuffer? _buffer;
    private bool _tagsEnabled;

    public bool BeginWriting(SerBuffer buffer, bool tagging = false)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _buffer = buffer;
        _tagsEnabled = tagging;
        return true;
    }

    public bool EndWriting()
    {
        _buffer = null;
        return true;
    }

    public bool BeginReading(SerBuffer buffer, bool tagging = false)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        _buffer = buffer;
        _tagsEnabled = tagging;
        return true;
    }

    public bool EndReading()
    {
        _buffer = null;
        return true;
    }

    public int ReadLength => _buffer?.ReadLength ?? 0;

    public bool Put(char value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return WriteTagged(SerializeTag.WChar, bytes);
    }

    public bool Get(out char value)
    {
        value = default;
        Span<byte> bytes = stackalloc byte[2];
        if (!ReadTagged(SerializeTag.WChar, bytes))
            return false;
        value = (char)BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        return true;
    }

    public bool Put(byte value) => WriteTagged(SerializeTag.UChar, [value]);

    public bool Get(out byte value)
    {
        value = default;
        Span<byte> bytes = stackalloc byte[1];
        if (!ReadTagged(SerializeTag.UChar, bytes))
            return false;
        value = bytes[0];
        return true;
    }

    public bool Put(short value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(bytes, value);
        return WriteTagged(SerializeTag.Short, bytes);
    }

    public bool Get(out short value)
    {
        value = default;
        Span<byte> bytes = stackalloc byte[2];
        if (!ReadTagged(SerializeTag.Short, bytes))
            return false;
        value = BinaryPrimitives.ReadInt16BigEndian(bytes);
        return true;
    }

    public bool Put(ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return WriteTagged(SerializeTag.UShort, bytes);
    }

    public bool Get(out ushort value)
    {
        value = default;
        Span<byte> bytes = stackalloc byte[2];
        if (!ReadTagged(SerializeTag.UShort, bytes))
            return false;
        value = BinaryPrimitives.ReadUInt16BigEndian(bytes);
        return true;
    }

    public bool Put(int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);
        return WriteTagged(SerializeTag.Int, bytes);
    }

    public bool Get(out int value)
    {
        value = default;
        Span<byte> bytes = stackalloc byte[4];
        if (!ReadTagged(SerializeTag.Int, bytes))
            return false;
        value = BinaryPrimitives.ReadInt32BigEndian(bytes);
        return true;
    }

    public bool Put(uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return WriteTagged(SerializeTag.DWord, bytes);
    }

    public bool Get(out uint value)
    {
        value = default;
        Span<byte> bytes = stackalloc byte[4];
        if (!ReadTagged(SerializeTag.DWord, bytes))
            return false;
        value = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        return true;
    }

    public bool Put(long value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(bytes, value);
        return WriteTagged(SerializeTag.Int64, bytes);
    }

    public bool Get(out long value)
    {
        value = default;
        Span<byte> bytes = stackalloc byte[8];
        if (!ReadTagged(SerializeTag.Int64, bytes))
            return false;
        value = BinaryPrimitives.ReadInt64BigEndian(bytes);
        return true;
    }

    public bool Put(ulong value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, value);
        return WriteTagged(SerializeTag.UInt64, bytes);
    }

    public bool Get(out ulong value)
    {
        value = default;
        Span<byte> bytes = stackalloc byte[8];
        if (!ReadTagged(SerializeTag.UInt64, bytes))
            return false;
        value = BinaryPrimitives.ReadUInt64BigEndian(bytes);
        return true;
    }

    public bool Put(float value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, unchecked((uint)BitConverter.SingleToInt32Bits(value)));
        return WriteTagged(SerializeTag.Float, bytes);
    }

    public bool Get(out float value)
    {
        value = default;
        Span<byte> bytes = stackalloc byte[4];
        if (!ReadTagged(SerializeTag.Float, bytes))
            return false;
        value = BitConverter.Int32BitsToSingle(unchecked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes)));
        return true;
    }

    public bool Put(double value)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(bytes, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
        return WriteTagged(SerializeTag.Double, bytes);
    }

    public bool Get(out double value)
    {
        value = default;
        Span<byte> bytes = stackalloc byte[8];
        if (!ReadTagged(SerializeTag.Double, bytes))
            return false;
        value = BitConverter.Int64BitsToDouble(unchecked((long)BinaryPrimitives.ReadUInt64BigEndian(bytes)));
        return true;
    }

    public bool Put(bool value) => PutTaggedByte(value ? (byte)1 : (byte)0, SerializeTag.Bool);

    public bool Get(out bool value)
    {
        value = false;
        if (!GetTaggedByte(out var raw, SerializeTag.Bool))
            return false;
        value = raw == 1;
        return true;
    }

    public bool PutString(ReadOnlySpan<byte> value)
    {
        if (!WriteTag(SerializeTag.String) || !Put((uint)value.Length))
            return false;
        return value.IsEmpty || WriteBytes(value);
    }

    public bool PutString(string value, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(encoding);
        return PutString(encoding.GetBytes(value));
    }

    public bool GetString(Span<byte> destination, out int length)
    {
        length = 0;
        if (!ReadAndCheckTag(SerializeTag.String) || !Get(out uint size) || size > (uint)destination.Length || size > (uint)ReadLength)
            return false;
        length = checked((int)size);
        return size == 0 || ReadBytes(destination[..length]);
    }

    public bool PutWString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var bytes = Encoding.Unicode.GetBytes(value);
        if (!WriteTag(SerializeTag.WString) || !Put((uint)bytes.Length))
            return false;
        return bytes.Length == 0 || WriteBytes(bytes);
    }

    public bool GetWString(out string value)
    {
        value = string.Empty;
        if (!ReadAndCheckTag(SerializeTag.WString) || !Get(out uint byteLength) || (byteLength & 1) != 0 || byteLength > (uint)ReadLength)
            return false;
        var bytes = new byte[checked((int)byteLength)];
        if (!bytes.AsSpan().IsEmpty && !ReadBytes(bytes))
            return false;
        value = Encoding.Unicode.GetString(bytes);
        return true;
    }

    public bool PutRaw(ReadOnlySpan<byte> value) =>
        !value.IsEmpty && WriteTag(SerializeTag.RawBytes) && WriteBytes(value);

    public bool GetRaw(Span<byte> destination) =>
        !destination.IsEmpty && ReadAndCheckTag(SerializeTag.RawBytes) && ReadBytes(destination);

    public bool Put(SerBuffer value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!WriteTag(SerializeTag.Buffer) || !Put((uint)value.Length))
            return false;
        if (value.Length == 0)
            return true;
        return Put(value.IsCompressed) && PutRaw(value.Data);
    }

    public bool Get(SerBuffer value)
    {
        ArgumentNullException.ThrowIfNull(value);
        value.Clear();
        if (!ReadAndCheckTag(SerializeTag.Buffer) || !Get(out uint length))
            return false;
        if (length == 0)
            return true;
        if (length > (uint)ReadLength || !Get(out bool compressed))
            return false;
        var bytes = new byte[checked((int)length)];
        if (!GetRaw(bytes))
            return false;
        value.LoadSerialized(bytes, compressed);
        return true;
    }

    public bool WriteTag(SerializeTag tag)
    {
        if (!_tagsEnabled)
            return true;
        Span<byte> one = stackalloc byte[1];
        one[0] = (byte)tag;
        return WriteBytes(one);
    }

    public bool ReadAndCheckTag(SerializeTag expected)
    {
        if (!_tagsEnabled)
            return true;
        Span<byte> one = stackalloc byte[1];
        return ReadBytes(one) && one[0] == (byte)expected;
    }

    private bool PutTaggedByte(byte value, SerializeTag tag) => WriteTagged(tag, [value]);

    private bool GetTaggedByte(out byte value, SerializeTag tag)
    {
        value = default;
        Span<byte> one = stackalloc byte[1];
        if (!ReadTagged(tag, one))
            return false;
        value = one[0];
        return true;
    }

    private bool WriteTagged(SerializeTag tag, ReadOnlySpan<byte> data) =>
        WriteTag(tag) && WriteBytes(data);

    private bool ReadTagged(SerializeTag tag, Span<byte> destination) =>
        ReadAndCheckTag(tag) && ReadBytes(destination);

    private bool WriteBytes(ReadOnlySpan<byte> data)
    {
        if (_buffer is null || data.IsEmpty)
            return false;
        _buffer.Write(data);
        return true;
    }

    private bool ReadBytes(Span<byte> data) =>
        _buffer is not null && !data.IsEmpty && _buffer.Read(data);
}
