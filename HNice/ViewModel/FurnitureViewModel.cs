using HNice.Model.Packets;
using HNice.Service;
using HNice.Util.Extensions;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Places a client-side furni: injects an ACTIVEOBJECTS packet toward the client so the item appears
/// on the user's own screen only. Generalizes <see cref="DrinksViewModel"/> to any sprite name.
/// Port of SnG Fun v1's "Furniture" form (send to the embedded client, not the server).
/// </summary>
class FurnitureViewModel : BaseViewModel
{
    #region Properties
    private string _furniId = "999000001";
    public string FurniId
    {
        get => _furniId;
        set { _furniId = value; OnPropertyChanged(nameof(FurniId)); }
    }

    private int _ownerId = 1;
    public int OwnerId
    {
        get => _ownerId;
        set { _ownerId = value; OnPropertyChanged(nameof(OwnerId)); }
    }

    private string _spriteName = "chair_norja";
    public string SpriteName
    {
        get => _spriteName;
        set { _spriteName = value; OnPropertyChanged(nameof(SpriteName)); }
    }

    private int _xCoord = 10;
    public int XCoord
    {
        get => _xCoord;
        set { _xCoord = value; OnPropertyChanged(nameof(XCoord)); }
    }

    private int _yCoord = 6;
    public int YCoord
    {
        get => _yCoord;
        set { _yCoord = value; OnPropertyChanged(nameof(YCoord)); }
    }

    private int _rotation = 2;
    public int Rotation
    {
        get => _rotation;
        set { _rotation = value; OnPropertyChanged(nameof(Rotation)); }
    }

    // Optional trailing state (e.g. the on/off or value digit some furni carry after "HHH"); empty for plain furni.
    private string _extraData = "";
    public string ExtraData
    {
        get => _extraData;
        set { _extraData = value; OnPropertyChanged(nameof(ExtraData)); }
    }
    #endregion

    #region Commands
    public ICommand PlaceFurniCommand { get; }
    #endregion

    public FurnitureViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        PlaceFurniCommand = new RelayCommand(async _ => await OnPlaceFurni());
    }

    private async Task OnPlaceFurni()
    {
        if (string.IsNullOrWhiteSpace(SpriteName))
            return;

        // Uses the real ACTIVEOBJECTS single-object format reverse-engineered from the live server.
        var packet = ClientPacketBuilder.ActiveObject(FurniId, OwnerId, SpriteName, XCoord, YCoord, Rotation, state: ExtraData);
        await OnSendToClient(packet);
    }
}
