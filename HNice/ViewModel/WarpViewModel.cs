using HNice.Model.Packets;
using HNice.Service;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Moves the user's own avatar to a tile on their own screen by injecting a STATUS packet.
/// Port of SnG Fun v1's "Warp". Local only; the server keeps the real position.
/// </summary>
class WarpViewModel : BaseViewModel
{
    private int _roomIndex = 1;
    public int RoomIndex
    {
        get => _roomIndex;
        set { _roomIndex = value; OnPropertyChanged(nameof(RoomIndex)); }
    }

    private int _xCoord = 5;
    public int XCoord
    {
        get => _xCoord;
        set { _xCoord = value; OnPropertyChanged(nameof(XCoord)); }
    }

    private int _yCoord = 5;
    public int YCoord
    {
        get => _yCoord;
        set { _yCoord = value; OnPropertyChanged(nameof(YCoord)); }
    }

    public ICommand WarpCommand { get; }

    public WarpViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        WarpCommand = new RelayCommand(async _ => await Warp());
    }

    // Body format mirrors the room STATUS layout: "<x>,<y>,0.0,<dir>,<dir>/".
    private Task Warp() =>
        OnSendToClient(ClientPacketBuilder.Status(RoomIndex, $" {XCoord},{YCoord},0.0,6,6/"));
}
