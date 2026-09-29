using HNice.Model.Packets;
using HNice.Service;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Places a client-side furni: injects an ACTIVEOBJECTS packet toward the client so the item appears
/// on the user's own screen only. Port of SnG Fun v1's "Furniture" form.
/// </summary>
class FurnitureViewModel : BaseViewModel
{
    /// <summary>Sprites seen in live rooms; a quick starting point.</summary>
    public IReadOnlyList<string> SuggestedSprites { get; } = new[]
    {
        "pizza", "ham", "chair_plasto*2", "sofa_silo", "lamp_armas", "deepgrove_duck", "hc_chr", "divider_silo2", "small_table_autumn",
    };

    private string _spriteName = "pizza";
    public string SpriteName
    {
        get => _spriteName;
        set { _spriteName = value; OnPropertyChanged(); }
    }

    private int _xCoord = 10;
    public int XCoord { get => _xCoord; set { _xCoord = value; OnPropertyChanged(); } }

    private int _yCoord = 6;
    public int YCoord { get => _yCoord; set { _yCoord = value; OnPropertyChanged(); } }

    private int _rotation = 2;
    public int Rotation { get => _rotation; set { _rotation = value; OnPropertyChanged(); } }

    #region Advanced
    private string _furniId = "999000001";
    public string FurniId { get => _furniId; set { _furniId = value; OnPropertyChanged(); } }

    // 0 = use your own user id once known.
    private int _ownerId;
    public int OwnerId { get => _ownerId; set { _ownerId = value; OnPropertyChanged(); } }

    // Optional trailing state (e.g. the on/off digit some furni carry); empty for plain furni.
    private string _extraData = "";
    public string ExtraData { get => _extraData; set { _extraData = value; OnPropertyChanged(); } }
    #endregion

    public ICommand PlaceFurniCommand { get; }
    public ICommand PickSpriteCommand { get; }

    public FurnitureViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        PlaceFurniCommand = new RelayCommand(async _ => await OnPlaceFurni(), _ => !string.IsNullOrWhiteSpace(SpriteName));
        PickSpriteCommand = new RelayCommand(p => { if (p is string s) SpriteName = s; });
    }

    private Task OnPlaceFurni()
    {
        var owner = OwnerId > 0 ? OwnerId : Worker.CurrentPlayer?.UserId ?? 1;
        var packet = ClientPacketBuilder.ActiveObject(FurniId, owner, SpriteName.Trim(), XCoord, YCoord, Rotation, state: ExtraData);
        return OnSendToClient(packet);
    }
}
