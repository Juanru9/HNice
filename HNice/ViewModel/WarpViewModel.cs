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
    public int RoomIndex { get => _roomIndex; set { _roomIndex = value; OnPropertyChanged(); } }

    private int _xCoord = 5;
    public int XCoord { get => _xCoord; set { _xCoord = value; OnPropertyChanged(); } }

    private int _yCoord = 5;
    public int YCoord { get => _yCoord; set { _yCoord = value; OnPropertyChanged(); } }

    public ICommand WarpCommand { get; }

    /// <summary>Steps one tile in a direction ("N", "E", "S", "W") and warps immediately.</summary>
    public ICommand NudgeCommand { get; }

    public WarpViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        WarpCommand = new RelayCommand(async _ => await Warp());
        NudgeCommand = new RelayCommand(async p => await Nudge(p as string));
    }

    private Task Nudge(string? direction)
    {
        switch (direction)
        {
            case "N": YCoord = Math.Max(0, YCoord - 1); break;
            case "S": YCoord++; break;
            case "W": XCoord = Math.Max(0, XCoord - 1); break;
            case "E": XCoord++; break;
            default: return Task.CompletedTask;
        }
        return Warp();
    }

    // Body format mirrors the room STATUS layout: "<x>,<y>,0.0,<dir>,<dir>/".
    private Task Warp() =>
        OnSendToClient(ClientPacketBuilder.Status(RoomIndex, $" {XCoord},{YCoord},0.0,6,6/"));
}
