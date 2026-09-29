using HNice.Model.Packets;

namespace HNice.Model;

public sealed class HabboPlayer
{
    public int? UserId { get; set; }

    /// <summary>The USER_OBJ body as received (after the header), used to rebuild it with another look.</summary>
    public string RawBody { get; }
    public string HabboName { get; set; }
    public string HabboFigure { get; set; }
    public string HabboSex { get; set; }
    public string HabboMission { get; set; }
    public int PhTickets { get; set; }
    public string PhFigure { get; set; }
    public int PhotoFilm { get; set; }
    public int DirectMail { get; set; }
    public int OnlineStatus { get; set; }
    public int PublicProfileEnabled { get; set; }
    public int FriendRequestsEnabled { get; set; }
    public int OfflineMessagingEnabled { get; set; }

    public string? DynamicRoomID { get; set; }

    public HabboPlayer(string packetData)
    {
        RawBody = packetData;
        if (!packetData.Contains('='))
        {
            ParseUserObject(packetData);
            return;
        }

        var lines = packetData.Split(new[] { '\r' }, StringSplitOptions.None);

        foreach (var line in lines)
        {
            if (!line.Contains('=')) continue;
            var key = line.Substring(0, line.IndexOf('='));
            var value = line.Substring(line.IndexOf('=') + 1);

            switch (key)
            {
                case "name":
                    HabboName = value;
                    break;
                case "figure":
                    HabboFigure = value;
                    break;
                case "sex":
                    HabboSex = value;
                    break;
                case "customData":
                    HabboMission = value;
                    break;
                case "ph_tickets":
                    PhTickets = int.Parse(value);
                    break;
                case "ph_figure":
                    PhFigure = value;
                    break;
                case "photo_film":
                    PhotoFilm = int.Parse(value);
                    break;
                case "directMail":
                    DirectMail = int.Parse(value);
                    break;
                case "onlineStatus":
                    OnlineStatus = int.Parse(value);
                    break;
                case "publicProfileEnabled":
                    PublicProfileEnabled = int.Parse(value);
                    break;
                case "friendRequestsEnabled":
                    FriendRequestsEnabled = int.Parse(value);
                    break;
                case "offlineMessagingEnabled":
                    OfflineMessagingEnabled = int.Parse(value);
                    break;
            }
        }
    }

    // Current USER_OBJ body, same fields as the old key=value format but typed:
    // id:int, name, figure, sex, mission, ph_tickets:int, ph_figure, photo_film:int, directMail:int, then more flags.
    private void ParseUserObject(string packetBody)
    {
        var reader = new IncomingPacketReader(packetBody);

        UserId = reader.ReadInt();
        HabboName = reader.ReadString();
        HabboFigure = reader.ReadString();
        HabboSex = reader.ReadString();
        HabboMission = reader.ReadString();

        // Optional for us: a shorter packet must not lose the fields above.
        if (!reader.HasMore) return;
        PhTickets = reader.ReadInt();
        PhFigure = reader.ReadString();
        if (!reader.HasMore) return;
        PhotoFilm = reader.ReadInt();
        if (!reader.HasMore) return;
        DirectMail = reader.ReadInt();
    }
}
