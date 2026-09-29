using HNice.Model.Packets;
using HNice.Service;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Spawns a fake avatar (bot / clone / pet) in the room the user is viewing, on their own screen only.
/// Port of SnG Fun v1's Bots / Cloning / Pets / Mutants forms, which are the same USERS packet.
/// </summary>
class SpawnUserViewModel : BaseViewModel
{
    private int _roomIndex = 100;
    /// <summary>Room slot of the new avatar; use a high number so it does not replace a real user.</summary>
    public int RoomIndex { get => _roomIndex; set { _roomIndex = value; OnPropertyChanged(); } }

    private string _name = "Bot";
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

    private string _figure = "hd-180-1.ch-255-66.lg-280-110.sh-305-62";
    public string Figure { get => _figure; set { _figure = value; OnPropertyChanged(); } }

    private int _xCoord = 4;
    public int XCoord { get => _xCoord; set { _xCoord = value; OnPropertyChanged(); } }

    private int _yCoord = 12;
    public int YCoord { get => _yCoord; set { _yCoord = value; OnPropertyChanged(); } }

    private string _sex = "M";
    public string Sex { get => _sex; set { _sex = value; OnPropertyChanged(); } }

    public ICommand SpawnCommand { get; }
    public ICommand CloneMeCommand { get; }

    public SpawnUserViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        SpawnCommand = new RelayCommand(async _ => await Spawn(), _ => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Figure));
        CloneMeCommand = new RelayCommand(_ => CloneMe(), _ => Worker.CurrentPlayer is not null);
    }

    // Copies your own look, the old "Cloning" form.
    private void CloneMe()
    {
        var me = Worker.CurrentPlayer;
        if (me is null) return;
        Figure = me.HabboFigure ?? Figure;
        Sex = me.HabboSex ?? Sex;
        Name = (me.HabboName ?? "Clone") + " 2";
    }

    private Task Spawn() =>
        OnSendToClient(ClientPacketBuilder.SpawnUser(RoomIndex, Name, Figure, XCoord, YCoord, Sex));
}
