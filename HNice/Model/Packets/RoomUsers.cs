using HNice.Util;
using HNice.Util.Extensions;
using System.Text;

namespace HNice.Model.Packets;

/// <summary>
/// A person in the current room. Text fields are kept in wire form (Latin1 bytes of UTF-8);
/// use the Display* properties for the UI.
/// </summary>
/// <param name="Kind">Player, pet or server bot (the user type in USERS).</param>
public sealed record RoomUser(
    int Index,
    int UserId,
    string Name,
    string Figure,
    string Sex,
    string Motto,
    string Badge,
    int X,
    int Y,
    RoomUserKind Kind = RoomUserKind.Player)
{
    public bool IsPlayer => Kind == RoomUserKind.Player;

    /// <summary>Name for people lists, with pets and bots marked.</summary>
    public string ListName => Kind switch
    {
        RoomUserKind.Bot => DisplayName + " (bot)",
        RoomUserKind.Pet => DisplayName + " (pet)",
        _ => DisplayName,
    };

    public string DisplayName => PacketText.FromWire(Name);
    public string DisplayMotto => PacketText.FromWire(Motto);

    /// <summary>"M" or "F", as USER_OBJ expects (USERS sends lowercase).</summary>
    public string Gender => Sex.StartsWith("f", StringComparison.OrdinalIgnoreCase) ? "F" : "M";

    public override string ToString() => DisplayName;
}

/// <summary>User type in USERS (captured): 1 player, 2 pet (figure "0 14 F59500"), 3 server bot such as Bob.</summary>
public enum RoomUserKind
{
    Player = 1,
    Pet = 2,
    Bot = 3,
}

/// <summary>
/// Reads and writes USERS (@\) packets. Layout captured from the live server:
/// count(VL64) then per user:
///   index(VL64) userId(VL64) name[2] figure[2] sex[2] motto[2] x(VL64) y(VL64) z[2]
///   poolFigure[2] badge[2] type(VL64), then for players (type 1): stance[2] stance2[2] and 4 x VL64 flags,
///   plus a companion block (3 strings) when the last flag is 1. Pets (type 2) and bots (type 3) end right after the type.
/// Example: @\ I PC `j]A Bateman[2] hd-180-1001...[2] m[2] [2] H QA 0.0[2] [2] [2] I std[2] std[2] PA H H H
/// Bot (captured, the Fishing Derby host): H PZ Bob[2] sd=001&amp;sh=003/30,30,30&amp;…[2] null[2] Fishing Derby[2] QG PD 0.0[2] [2] [2] K
/// </summary>
public static class RoomUsers
{

    /// <param name="body">Packet text after the 2-char header.</param>
    public static bool TryParse(string body, out List<RoomUser> users)
    {
        users = new List<RoomUser>();
        try
        {
            var reader = new IncomingPacketReader(body);
            var count = reader.ReadInt();
            if (count < 0 || count > 500) return false;

            for (var i = 0; i < count; i++)
            {
                var index = reader.ReadInt();
                var userId = reader.ReadInt();
                var name = reader.ReadString();
                var figure = reader.ReadString();
                var sex = reader.ReadString();
                var motto = reader.ReadString();
                var x = reader.ReadInt();
                var y = reader.ReadInt();
                reader.ReadString();              // z
                reader.ReadString();              // pool figure
                var badge = reader.ReadString();
                var type = (RoomUserKind)reader.ReadInt();
                if (type is RoomUserKind.Pet or RoomUserKind.Bot)
                {
                    users.Add(new RoomUser(index, userId, name, figure, sex, motto, badge, x, y, type));
                    continue;
                }
                if (type != RoomUserKind.Player) return false;

                reader.ReadString();              // stance ("std")
                reader.ReadString();              // stance 2 ("std", "crr.6")
                var flags = new int[4];
                for (var f = 0; f < 4; f++) flags[f] = reader.ReadInt();

                // Last flag 1 = the user walks with a companion (captured: a "Sporeling" pet):
                // sprite[2] display name[2] extra[2], e.g. mushroom_sporeling[2][color=#78A84B]Sporeling[/color][2]I [2]
                if (flags[3] == 1)
                {
                    reader.ReadString();
                    reader.ReadString();
                    reader.ReadString();
                }

                users.Add(new RoomUser(index, userId, name, figure, sex, motto, badge, x, y));
            }

            return !reader.HasMore;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>One USERS packet adding a single avatar, in the exact live layout.</summary>
    public static string BuildSingle(RoomUser user, string z = "0.0") => Build(new[] { user }, z);

    /// <summary>One USERS packet adding several avatars, in the exact live layout.</summary>
    public static string Build(IReadOnlyCollection<RoomUser> users, string z = "0.0")
    {
        var sb = new StringBuilder(((int)IncomingPacketMessage.USERS).EncodeB64());
        sb.Append(users.Count.EncodeVL64());
        foreach (var user in users) AppendUser(sb, user, z);
        return sb.ToString();
    }

    private static void AppendUser(StringBuilder sb, RoomUser user, string z)
    {
        sb.Append(user.Index.EncodeVL64())
          .Append(user.UserId.EncodeVL64())
          .Append(user.Name).Append(Constants.PACKET_SPLITTER)
          .Append(user.Figure).Append(Constants.PACKET_SPLITTER)
          .Append(user.Sex.ToLowerInvariant()).Append(Constants.PACKET_SPLITTER)
          .Append(user.Motto).Append(Constants.PACKET_SPLITTER)
          .Append(user.X.EncodeVL64())
          .Append(user.Y.EncodeVL64())
          .Append(z).Append(Constants.PACKET_SPLITTER)
          .Append(Constants.PACKET_SPLITTER)                      // pool figure
          .Append(user.Badge).Append(Constants.PACKET_SPLITTER);
        if (!user.IsPlayer)
        {
            sb.Append(((int)user.Kind).EncodeVL64());
            return;
        }
        sb.Append(((int)RoomUserKind.Player).EncodeVL64())
          .Append("std").Append(Constants.PACKET_SPLITTER)
          .Append("std").Append(Constants.PACKET_SPLITTER)
          .Append(4.EncodeVL64()).Append(0.EncodeVL64()).Append(0.EncodeVL64()).Append(0.EncodeVL64());
    }
}
