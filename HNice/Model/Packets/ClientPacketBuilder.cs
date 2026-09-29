using HNice.Util.Extensions;
using System.Text;

namespace HNice.Model.Packets;

/// <summary>
/// Builds fake server-to-client packets to inject toward the user's own client (client-side illusions).
/// Ported from SnG Fun v1, translated from the old 1999/v1 wire format (CR separated key:value) to the
/// current Origins format (chr(2) field separator), which is what the modern client and this app use.
///
/// Header is 2 chars of Habbo B64 (see <see cref="Packet{T}"/>); the packet ender chr(1) is added by the
/// worker when it writes the packet, so it is not included here.
/// </summary>
public static class ClientPacketBuilder
{
    private const char FieldSeparator = Constants.PACKET_SPLITTER; // chr(2)

    /// <summary>Two-character B64 header for an incoming message id.</summary>
    public static string Header(IncomingPacketMessage message) => ((int)message).EncodeB64();

    /// <summary>Header + fields joined by chr(2). Mirrors <see cref="Packet{T}.SerializePacketData"/> without the ender.</summary>
    public static string Build(IncomingPacketMessage message, params string[] fields) =>
        Header(message) + string.Join(FieldSeparator, fields);

    /// <summary>SYSTEM_BROADCAST: a hotel notification shown on the user's own screen.</summary>
    public static string Broadcast(string text) => Header(IncomingPacketMessage.SYSTEM_BROADCAST) + text;

    /// <summary>ERROR/alert wrapper (e.g. "mod_warn/&lt;text&gt;").</summary>
    public static string Alert(string text) => Header(IncomingPacketMessage.ERROR) + text;

    /// <summary>RIGHTS with fuse_* permissions, so the client renders the matching UI. Server still enforces the real rights.</summary>
    /// <remarks>
    /// Real format captured from the live server: header + VL64(count) + [permission + chr(2)] per permission.
    /// The count prefix is required; without it the client cannot parse the list.
    /// </remarks>
    public static string FuseRights(IEnumerable<string> permissions)
    {
        var list = permissions.Select(p => p.StartsWith("fuse_") ? p : "fuse_" + p).ToList();
        return Header(IncomingPacketMessage.RIGHTS) + list.Count.EncodeVL64() +
            string.Concat(list.Select(p => p + FieldSeparator));
    }

    /// <summary>Everything after the motto in a live USER_OBJ (tickets, film, mail and flags).</summary>
    public const string DefaultUserObjectTail = "HHHIIIIIIHHI";

    /// <summary>
    /// USER_OBJ: overwrites the client's idea of its own avatar (name, figure, sex, motto).
    /// Live layout: id(VL64) name[2] figure[2] sex[2] motto[2] then <paramref name="tail"/>. Pass the tail of your
    /// real USER_OBJ to keep the other fields unchanged.
    /// </summary>
    public static string UserObject(int userId, string name, string figure, string sex, string mission = "", string? tail = null)
    {
        var sb = new StringBuilder();
        sb.Append(Header(IncomingPacketMessage.USER_OBJ));
        sb.Append(userId.EncodeVL64());
        sb.Append(name).Append(FieldSeparator);
        sb.Append(figure).Append(FieldSeparator);
        sb.Append(sex).Append(FieldSeparator);
        sb.Append(mission).Append(FieldSeparator);
        sb.Append(tail ?? DefaultUserObjectTail);
        return sb.ToString();
    }

    /// <summary>Live header of the "someone changed clothes" packet (266).</summary>
    public const int UserLookChangedHeader = 266;

    /// <summary>
    /// 266 (DJ): changes how an avatar in the room looks, as the server does when someone changes clothes.
    /// Live layout: index(VL64) figure[2] sex[2] motto[2], sex in lowercase ("m"/"f").
    /// </summary>
    public static string UserLook(int roomIndex, string figure, string sex, string motto) =>
        UserLookChangedHeader.EncodeB64() + roomIndex.EncodeVL64() +
        figure + FieldSeparator + sex.ToLowerInvariant() + FieldSeparator + motto + FieldSeparator;

    /// <summary>
    /// ACTIVEOBJECTS (@`): one floor furni. Format reverse-engineered from the live server:
    /// header + VL64(count) + [ id [2] ownerId(VL64)+sprite [2] location [2] colors [2] (empty) [2] "HHH"+state ].
    /// Location is x(VL64) y(VL64) width(VL64) length(VL64) dir(VL64) "0.0". Purely a local visual; the server has no such object.
    /// </summary>
    public static string ActiveObject(string id, int ownerId, string sprite, int x, int y, int direction, string colors = "", string state = "", int width = 1, int length = 1)
    {
        var location = x.EncodeVL64() + y.EncodeVL64() + width.EncodeVL64() + length.EncodeVL64() + direction.EncodeVL64() + "0.0";
        return Header(IncomingPacketMessage.ACTIVEOBJECTS) + 1.EncodeVL64() +
            id + FieldSeparator +
            ownerId.EncodeVL64() + sprite + FieldSeparator +
            location + FieldSeparator +
            colors + FieldSeparator +
            string.Empty + FieldSeparator +
            "HHH" + state;
    }

    /// <summary>AVAILABLE_BADGES: shows a badge in the client's badge list. Local only.</summary>
    public static string Badge(string code) => Header(IncomingPacketMessage.AVAILABLE_BADGES) + "QA" + code;

    /// <summary>AVAILABLE_BADGES with the classic SnG preset list of country/staff badges.</summary>
    public static string AllBadges() => Header(IncomingPacketMessage.AVAILABLE_BADGES) +
        "SMHC1HC2NWBHBAADMEXHVIPHWBXM1XM2XM3VA1VA2DU1DU2DU3CA1CA2CA3CA4CA5CA6CA7CA8CA9CH1CH2DE1ES1" +
        "FI1FI2FI3FI4FI5FI6NL1NL2NL3NL4NO1TC1FANUK1UK2UK3 US1US2US3US4 US5US6SE1XXXPIRLLLHI";

    private const char Tab = '\t';
    private const char Cr = '\r';

    /// <summary>
    /// ITEMS (@m): a wall item such as a poster. Format captured from the live server:
    /// header + [id, sprite, owner, ":w=&lt;wall&gt; l=&lt;loc&gt; &lt;dir&gt;", type] tab-separated, terminated by CR.
    /// Multiple items are joined by chr(2); one item needs no separator.
    /// </summary>
    public static string WallItem(string id, string sprite, string owner, string wall, string location, string direction, string type) =>
        Header(IncomingPacketMessage.ITEMS) + id + Tab + sprite + Tab + owner + Tab +
        ":w=" + wall + " l=" + location + " " + direction + Tab + type + Cr;

    /// <summary>OBJECTS (@^): sets the room wallpaper/floor on the client. Values are space separated as the client expects.</summary>
    public static string Wallpaper(string values) => Header(IncomingPacketMessage.OBJECTS) + values;
}
