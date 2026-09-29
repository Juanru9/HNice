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

    // Real STATUS layout (captured live): your avatar index, tile, height and rotation. Your index is
    // learned automatically after you move or turn once; the Room slot field is only the fallback.
    private Task Warp()
    {
        var me = Worker.MyStatus;
        var entry = new StatusEntry(me?.Index ?? RoomIndex, XCoord, YCoord, me?.Z ?? "0.0", me?.HeadRotation ?? 2, me?.BodyRotation ?? 2, me?.Actions ?? "/");
        return OnSendToClient(RoomStatus.Build(new[] { entry }));
    }
}
