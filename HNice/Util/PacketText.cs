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
        .Replace("\r", "[13]");

    public static string Unescape(string text) => text
        .Replace("[1]", "\u0001")
        .Replace("[2]", "\u0002")
        .Replace("[9]", "\t")
        .Replace("[13]", "\r");
}
