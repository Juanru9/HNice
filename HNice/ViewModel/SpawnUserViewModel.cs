using HNice.Model.Packets;
using HNice.Service;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Spawns a fake avatar (bot / clone / pet) in the room the user is viewing, on their own screen only.
/// Port of SnG Fun v1's Bots / Cloning / Pets / Mutants forms, which are the same USERS packet with
/// different inputs. Optionally follows up with a chat line from the spawned avatar.
/// </summary>
class SpawnUserViewModel : BaseViewModel
{
    private int _roomIndex = 100;
    public int RoomIndex
    {
        get => _roomIndex;
        set { _roomIndex = value; OnPropertyChanged(nameof(RoomIndex)); }
    }

    private string _name = "Bot";
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    private string _figure = "hd-180-1.ch-255-66.lg-280-110.sh-305-62";
    public string Figure
    {
        get => _figure;
        set { _figure = value; OnPropertyChanged(nameof(Figure)); }
    }

    private int _xCoord = 4;
    public int XCoord
    {
        get => _xCoord;
        set { _xCoord = value; OnPropertyChanged(nameof(XCoord)); }
    }

    private int _yCoord = 12;
    public int YCoord
    {
        get => _yCoord;
        set { _yCoord = value; OnPropertyChanged(nameof(YCoord)); }
    }

    private string _sex = "M";
    public string Sex
    {
        get => _sex;
        set { _sex = value; OnPropertyChanged(nameof(Sex)); }
    }

    public ICommand SpawnCommand { get; }

    public SpawnUserViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        SpawnCommand = new RelayCommand(async _ => await Spawn());
    }

    private Task Spawn() =>
        string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Figure)
            ? Task.CompletedTask
            : OnSendToClient(ClientPacketBuilder.SpawnUser(RoomIndex, Name, Figure, XCoord, YCoord, Sex));
}
