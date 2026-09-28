namespace HNice.Model.Encryption;

/// <summary>
/// Standard Base64 alphabet without '=' padding. Encrypted Shockwave traffic is sent as this text.
/// Not to be confused with Habbo B64 (see <see cref="Util.Extensions.EncryptionExtension"/>).
/// </summary>
public static class CipherBase64
{
    private static readonly byte[] EncodingMap = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/"u8.ToArray();
    private static readonly sbyte[] DecodingMap = BuildDecodingMap();

    public static byte[] Encode(ReadOnlySpan<byte> data)
    {
        var result = new byte[(data.Length * 4 + 2) / 3];
        var pos = 0;

        for (var i = 0; i < data.Length; i += 3)
        {
            var remaining = data.Length - i;
            int first = data[i];
            int second = remaining > 1 ? data[i + 1] : 0;
            int third = remaining > 2 ? data[i + 2] : 0;

            result[pos++] = EncodingMap[first >> 2];
            result[pos++] = EncodingMap[((first & 0x03) << 4) | (second >> 4)];
            if (remaining > 1)
            {
                result[pos++] = EncodingMap[((second & 0x0F) << 2) | (third >> 6)];
            }
            if (remaining > 2)
            {
                result[pos++] = EncodingMap[third & 0x3F];
            }
        }

        return result;
    }

    public static byte[] Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length % 4 == 1)
        {
            throw new FormatException($"Invalid unpadded Base64 length: {data.Length}");
        }

        var result = new byte[data.Length * 3 / 4];
        var pos = 0;

        for (var i = 0; i < data.Length; i += 4)
        {
            var remaining = data.Length - i;
            var first = DecodeChar(data[i]);
            var second = DecodeChar(data[i + 1]);

            result[pos++] = (byte)((first << 2) | ((second & 0x30) >> 4));
            if (remaining > 2)
            {
                var third = DecodeChar(data[i + 2]);
                result[pos++] = (byte)(((second & 0x0F) << 4) | ((third & 0x3C) >> 2));
                if (remaining > 3)
                {
                    var fourth = DecodeChar(data[i + 3]);
                    result[pos++] = (byte)(((third & 0x03) << 6) | fourth);
                }
            }
        }

        return result;
    }

    private static int DecodeChar(byte value)
    {
        var decoded = value < DecodingMap.Length ? DecodingMap[value] : -1;
        if (decoded < 0)
        {
            throw new FormatException($"Invalid Base64 character: 0x{value:X2}");
        }
        return decoded;
    }

    private static sbyte[] BuildDecodingMap()
    {
        var map = new sbyte[128];
        Array.Fill(map, (sbyte)-1);
        for (var i = 0; i < EncodingMap.Length; i++)
        {
            map[EncodingMap[i]] = (sbyte)i;
        }
        return map;
    }
}
