using HNice.Model.Encryption;
using System.Collections;
using System.Globalization;
using System.Text;

namespace HNice.Util.Extensions;

public static class EncryptionExtension
{
    // Converts a numerical value into a specific two-character string representation
    // VL64 is most used in server > client packets.
    // What's VL64? Well, it's an encoding for numbers, it makes numbers 'understandable' for the Habbo client.
    public static int DecodeB64(this string value)
    {
        var result = 0;
        for (var i = 0; i < value.Length; i++)
        {
            result += value[i] - 0x40 << 6 * (value.Length - 1 - i);
        }
        return result;
    }

    public static string EncodeB64(this int value, int length = 2)
    {
        var result = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            var subValue = value >> 6 * (length - 1 - i) & 0x3F;
            result.Append((char)(subValue + 0x40));
        }
        return result.ToString();
    }

    private static byte NEGATIVE = 72;
    private static byte POSITIVE = 73;
    private static int MAX_INTEGER_BYTE_AMOUNT = 6;
    
    public static string EncodeVL64(this int i)
    {
        byte[] wf = new byte[MAX_INTEGER_BYTE_AMOUNT];

        int pos = 0;
        int numBytes = 1;
        int startPos = pos;
        int negativeMask = i >= 0 ? 0 : 4;

        i = Math.Abs(i);

        wf[pos++] = (byte)(64 + (i & 3));

        for (i >>= 2; i != 0; i >>= MAX_INTEGER_BYTE_AMOUNT)
        {
            numBytes++;
            wf[pos++] = (byte)(64 + (i & 0x3f));
        }
        wf[startPos] = (byte)(wf[startPos] | (numBytes << 3) | negativeMask);

        byte[] bzData = new byte[numBytes];
        Array.Copy(wf, 0, bzData, 0, numBytes);
        return System.Text.Encoding.UTF8.GetString(bzData, 0, numBytes);
    }

    public static IEnumerable<DecodedVL64> DecodeVL64(this string bzData)
    {
        var encodedString = bzData;
        var decodedString = new List<DecodedVL64>();
        while (encodedString.Length > 0) 
        {
            var currentNumber = DecodeVL64(Encoding.ASCII.GetBytes(encodedString), 0);
            // Encode the number to it's VL64 equivalent to see what it would be if you only encoded that number to VL64, and get it's length
            int currentNumberLength = EncodeVL64(currentNumber).ToString(CultureInfo.InvariantCulture).Length;
            //Sav String-Integer deco:
            decodedString.Add(new DecodedVL64(encodedString.Substring(0, currentNumberLength), currentNumber));
            // Only keep the part of SomeInput after the string
            encodedString = encodedString.Substring(currentNumberLength);
        }
        return decodedString;
    }

    private static int DecodeVL64(this byte[] bzData, int pos)
    {
        int v = 0;

        bool negative = (bzData[pos] & 4) == 4;
        int totalBytes = bzData[pos] >> 3 & 7;

        v = bzData[pos] & 3;

        pos++;

        int shiftAmount = 2;

        for (int b = 1; b < totalBytes; b++)
        {
            v |= (bzData[pos] & 0x3f) << shiftAmount;
            shiftAmount = 2 + 6 * b;
            pos++;
        }

        if (negative)
        {
            v *= -1;
        }

        return v;
    }
}
