namespace HNice.Model.Packets;

/// <summary>
/// Reads the body of a server to client (Shockwave) packet field by field:
/// integers are VL64, strings are terminated by chr(2).
/// </summary>
public sealed class IncomingPacketReader
{
    private readonly string _body;
    private int _position;

    public IncomingPacketReader(string body)
    {
        _body = body ?? throw new ArgumentNullException(nameof(body));
    }

    public bool HasMore => _position < _body.Length;

    public string ReadString()
    {
        var end = _body.IndexOf(Constants.PACKET_SPLITTER, _position);
        if (end < 0) end = _body.Length;

        var value = _body[_position..end];
        _position = Math.Min(end + 1, _body.Length);
        return value;
    }

    // VL64: the first byte holds the 2 lowest bits, the sign (bit 2) and the byte count (bits 3..5),
    // every following byte adds 6 more bits.
    public int ReadInt()
    {
        if (!HasMore) throw new FormatException("No data left to read an integer.");

        int first = _body[_position];
        var byteCount = (first >> 3) & 7;
        if (byteCount == 0 || _position + byteCount > _body.Length)
        {
            throw new FormatException($"Invalid VL64 value at position {_position}.");
        }

        var value = first & 3;
        for (var i = 1; i < byteCount; i++)
        {
            value |= (_body[_position + i] & 0x3F) << (2 + 6 * (i - 1));
        }

        _position += byteCount;
        return (first & 4) != 0 ? -value : value;
    }

    public bool ReadBool() => ReadInt() == 1;
}
