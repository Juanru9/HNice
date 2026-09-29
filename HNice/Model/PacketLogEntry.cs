using HNice.Model.Packets;
using HNice.Util;
using HNice.Util.Extensions;

namespace HNice.Model;

public enum PacketDirection
{
    /// <summary>Server to client.</summary>
    Inbound,
    /// <summary>Client to server.</summary>
    Outbound
}

/// <summary>One row of the unified packet log.</summary>
public sealed class PacketLogEntry
{
    public DateTime Time { get; }
    public PacketDirection Direction { get; }
    public bool IsInbound => Direction == PacketDirection.Inbound;

    /// <summary>The raw packet text (Latin1), used when copying or resending.</summary>
    public string Raw { get; }

    /// <summary>Two-char B64 header, e.g. "@E".</summary>
    public string Header { get; }

    /// <summary>Known message name for the header, e.g. "USER_OBJ", or the numeric id when unknown.</summary>
    public string HeaderName { get; }

    /// <summary>The packet after the header for display: UTF-8 text decoded, control chars shown as [1] [2] [9] [13].</summary>
    public string Body { get; }

    // Byte-exact escaped body, for the composer.
    private readonly string _rawBody;

    public PacketLogEntry(PacketDirection direction, string raw)
    {
        Time = DateTime.Now;
        Direction = direction;
        Raw = raw;

        if (raw.Length >= 2 && IsB64(raw[0]) && IsB64(raw[1]))
        {
            Header = raw[..2];
            var id = Header.DecodeB64();
            HeaderName = (direction == PacketDirection.Inbound
                ? Enum.GetName(typeof(IncomingPacketMessage), id)
                : Enum.GetName(typeof(OutcomingPacketMessage), id)) ?? id.ToString();
            _rawBody = PacketText.Escape(raw[2..]);
            Body = PacketText.Escape(PacketText.FromWire(raw[2..]));
        }
        else
        {
            // Encrypted or partial data (passthrough mode): no readable header.
            Header = string.Empty;
            HeaderName = "raw";
            _rawBody = PacketText.Escape(raw);
            Body = _rawBody;
        }
    }

    public string TimeText => Time.ToString("HH:mm:ss.fff");

    /// <summary>Escaped header + body, byte-exact, ready for the composer.</summary>
    public string Escaped => Header + _rawBody;

    /// <summary>Line used by Copy: time, direction arrow, message name and the readable packet.</summary>
    public override string ToString() => $"{TimeText} {(IsInbound ? "←" : "→")} {HeaderName,-18} {Header}{Body}";

    private static bool IsB64(char c) => c >= 0x40 && c <= 0x7F;
}
