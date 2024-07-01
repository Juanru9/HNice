namespace HNice.Model;

public sealed class HabboPlayer
{
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
        var lines = packetData.Split(new[] { '\r' }, StringSplitOptions.None);

        foreach (var line in lines)
        {
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
}
