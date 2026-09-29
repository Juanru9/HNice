namespace HNice.Util;

/// <summary>
/// Converts between raw packet text and the readable "[n]" notation used across the Habbo scene
/// (chr(1) = [1], chr(2) = [2], tab = [9], CR = [13]). The log shows packets escaped and the composer
/// accepts the same notation, so a packet can be copied from the log and resent unchanged.
/// </summary>
public static class PacketText
{
    public static string Escape(string raw) => raw
        .Replace("\u0001", "[1]")
        .Replace("\u0002", "[2]")
        .Replace("\t", "[9]")
        .Replace("\n", "[10]")
        .Replace("\r", "[13]");

    public static string Unescape(string text) => text
        .Replace("[1]", "\u0001")
        .Replace("[2]", "\u0002")
        .Replace("[9]", "\t")
        .Replace("[10]", "\n")
        .Replace("[13]", "\r");

    private static readonly System.Text.Encoding StrictUtf8 = new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true);

    /// <summary>
    /// Packets are handled as Latin1 (one char per byte) but names and mottos travel as UTF-8.
    /// Returns the readable text ("Café" instead of "CafÃ©"), or the input unchanged when it is not valid UTF-8.
    /// </summary>
    public static string FromWire(string latin1)
    {
        if (latin1.All(c => c < 0x80)) return latin1;
        try
        {
            return StrictUtf8.GetString(System.Text.Encoding.Latin1.GetBytes(latin1));
        }
        catch (System.Text.DecoderFallbackException)
        {
            return latin1;
        }
    }

    /// <summary>Inverse of <see cref="FromWire"/>: readable text back to the byte-per-char form packets use.</summary>
    public static string ToWire(string text) =>
        text.All(c => c < 0x80) ? text : System.Text.Encoding.Latin1.GetString(System.Text.Encoding.UTF8.GetBytes(text));
}
