using System.Text;

namespace KncWX2Server.Common;

public static class Base64
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    public static string Encode(ReadOnlySpan<byte> data) =>
        Convert.ToBase64String(data);

    public static byte[] Decode(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        // The original implementation stops at '=' or the first non-Base64 character
        // and decodes only the prefix it accepted. Keep that behavior rather than
        // changing callers to the stricter Convert.FromBase64String semantics.
        var acceptedLength = 0;
        while (acceptedLength < encoded.Length && encoded[acceptedLength] != '=' && IsBase64(encoded[acceptedLength]))
            acceptedLength++;

        if (acceptedLength == 0)
            return [];

        var source = encoded.AsSpan(0, acceptedLength);
        var output = new byte[(source.Length / 4) * 3 + Math.Max(0, source.Length % 4 - 1)];
        var outputIndex = 0;

        Span<byte> quartet = stackalloc byte[4];
        for (var sourceIndex = 0; sourceIndex < source.Length; sourceIndex += 4)
        {
            var remaining = Math.Min(4, source.Length - sourceIndex);
            quartet.Clear();
            for (var i = 0; i < remaining; i++)
                quartet[i] = (byte)Alphabet.IndexOf(source[sourceIndex + i]);

            output[outputIndex++] = (byte)((quartet[0] << 2) | ((quartet[1] & 0x30) >> 4));
            if (remaining >= 3)
                output[outputIndex++] = (byte)(((quartet[1] & 0x0f) << 4) | ((quartet[2] & 0x3c) >> 2));
            if (remaining == 4)
                output[outputIndex++] = (byte)(((quartet[2] & 0x03) << 6) | quartet[3]);
        }

        return output[..outputIndex];
    }

    public static string DecodeToString(string encoded, Encoding? encoding = null) =>
        (encoding ?? Encoding.UTF8).GetString(Decode(encoded));

    private static bool IsBase64(char value) =>
        value is >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or >= '0' and <= '9'
            or '+' or '/';
}
