using HNice.Util.Extensions;
using System.Text;

namespace HNice.Model.Packets;

/// <summary>
/// Guest rooms as the navigator lists them. The same entry layout appears in ← 351 (rooms list) and
/// ← NAVNODEINFO (a category's rooms), captured live:
///   flatId(VL64) name[2] owner[2] state[2] users(VL64) maxUsers(VL64) description[2]
/// e.g. asGD "02. Mi rincón de pesca" "JouJouJou" "open" I QF "…" = 1 of 25 users.
/// </summary>
public static class NavigatorRooms
{
    private const char Splitter = '\u0002';
    private static readonly string[] States = { "open", "closed", "password", "locked" };

    /// <summary>
    /// Adds <paramref name="extra"/> to the user count of room <paramref name="flatId"/> wherever it is listed in
    /// <paramref name="body"/>. Returns false (and the body unchanged) when the room is not listed.
    /// </summary>
    public static bool TryAddUsers(string body, int flatId, int extra, out string rewritten)
    {
        rewritten = body;
        if (extra == 0) return false;

        var id = flatId.EncodeVL64();
        var sb = new StringBuilder(body.Length + 4);
        var changed = false;
        var pos = 0;
        while (pos < body.Length)
        {
            var at = body.IndexOf(id, pos, StringComparison.Ordinal);
            if (at < 0) break;

            // An entry starts right after a field end (or at the very start of the list data).
            if (IsEntryStart(body, at) && TryReadCounts(body, at + id.Length, out var countAt, out var countLength, out var users))
            {
                sb.Append(body, pos, countAt - pos).Append(Math.Max(0, users + extra).EncodeVL64());
                pos = countAt + countLength;
                changed = true;
            }
            else
            {
                sb.Append(body, pos, at + 1 - pos);
                pos = at + 1;
            }
        }

        if (!changed) return false;
        sb.Append(body, pos, body.Length - pos);
        rewritten = sb.ToString();
        return true;
    }

    // Before an entry: the end of the previous entry's description ([2]), or VL64 header fields.
    private static bool IsEntryStart(string body, int at) => at == 0 || body[at - 1] == Splitter || body[at - 1] >= '@';

    /// <summary>After the id: name, owner, a known state, then the user count.</summary>
    private static bool TryReadCounts(string body, int start, out int countAt, out int countLength, out int users)
    {
        countAt = countLength = users = 0;
        var pos = start;
        string state = string.Empty;
        for (var field = 0; field < 3; field++)
        {
            var end = body.IndexOf(Splitter, pos);
            if (end < 0) return false;
            if (field == 2) state = body[pos..end];
            pos = end + 1;
        }
        if (!States.Contains(state, StringComparer.OrdinalIgnoreCase) || pos >= body.Length) return false;

        var reader = new IncomingPacketReader(body[pos..]);
        try
        {
            users = reader.ReadInt();
            countLength = (body[pos] >> 3) & 7;
            reader.ReadInt(); // max users: must be there too
        }
        catch (FormatException)
        {
            return false;
        }
        countAt = pos;
        return users >= 0;
    }
}
