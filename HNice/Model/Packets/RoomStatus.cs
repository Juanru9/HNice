using HNice.Util.Extensions;
using System.Text;

namespace HNice.Model.Packets;

/// <summary>One avatar line of a STATUS (@b) packet.</summary>
public sealed record StatusEntry(int Index, int X, int Y, string Z, int HeadRotation, int BodyRotation, string Actions);

/// <summary>
/// Reads and writes STATUS (@b) packets. Layout captured from the live server:
/// count(VL64) then per avatar: index(VL64) x(VL64) y(VL64) z[2] headRot(VL64) bodyRot(VL64) "/action/action/"[2].
/// Example: @b I H SB RA 0.0[2] H H /flatctrl useradmin/carryd 1/[2]
/// </summary>
public static class RoomStatus
{
    public const string Header = "@b";

    /// <param name="body">Packet text after the 2-char header.</param>
    public static bool TryParse(string body, out List<StatusEntry> entries)
    {
        entries = new List<StatusEntry>();
        try
        {
            var reader = new IncomingPacketReader(body);
            var count = reader.ReadInt();
            if (count < 0 || count > 500) return false;

            for (var i = 0; i < count; i++)
            {
                entries.Add(new StatusEntry(
                    Index: reader.ReadInt(),
                    X: reader.ReadInt(),
                    Y: reader.ReadInt(),
                    Z: reader.ReadString(),
                    HeadRotation: reader.ReadInt(),
                    BodyRotation: reader.ReadInt(),
                    Actions: reader.ReadString()));
            }

            // Anything left over means this was not the layout we expect; leave the packet alone.
            return !reader.HasMore;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string Build(IReadOnlyCollection<StatusEntry> entries)
    {
        var sb = new StringBuilder(Header);
        sb.Append(entries.Count.EncodeVL64());
        foreach (var e in entries)
        {
            sb.Append(e.Index.EncodeVL64())
              .Append(e.X.EncodeVL64())
              .Append(e.Y.EncodeVL64())
              .Append(e.Z).Append(Constants.PACKET_SPLITTER)
              .Append(e.HeadRotation.EncodeVL64())
              .Append(e.BodyRotation.EncodeVL64())
              .Append(e.Actions).Append(Constants.PACKET_SPLITTER);
        }
        return sb.ToString();
    }

    /// <summary>Replaces any carry/drink action with "carryd &lt;drink&gt;", keeping the others (flatctrl, mv, sit...).</summary>
    public static string WithCarriedDrink(string actions, string drink)
    {
        var kept = actions.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(a => !a.StartsWith("carryd ", StringComparison.Ordinal) && !a.StartsWith("drink ", StringComparison.Ordinal))
            .Append("carryd " + drink);
        return "/" + string.Join('/', kept) + "/";
    }
}
