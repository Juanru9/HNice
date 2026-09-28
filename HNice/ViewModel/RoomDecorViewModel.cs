using HNice.Model.Packets;
using HNice.Service;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Client-side wall items (posters), using the real ITEMS (@m) format captured from the live server:
/// id [tab] sprite [tab] owner [tab] ":w=&lt;wall&gt; l=&lt;loc&gt; &lt;dir&gt;" [tab] type [CR].
/// Port of SnG Fun v1's "Posters". Local only.
/// </summary>
class RoomDecorViewModel : BaseViewModel
{
    private string _posterId = "999000001";
    public string PosterId
    {
        get => _posterId;
        set { _posterId = value; OnPropertyChanged(nameof(PosterId)); }
    }

    private string _sprite = "poster";
    public string Sprite
    {
        get => _sprite;
        set { _sprite = value; OnPropertyChanged(nameof(Sprite)); }
    }

    private string _owner = "HNice";
    public string Owner
    {
        get => _owner;
        set { _owner = value; OnPropertyChanged(nameof(Owner)); }
    }

    // Wall coordinate (e.g. "2,0") and pixel location (e.g. "10,20"), matching :w= and l= in the packet.
    private string _wall = "2,0";
    public string Wall
    {
        get => _wall;
        set { _wall = value; OnPropertyChanged(nameof(Wall)); }
    }

    private string _location = "10,20";
    public string Location
    {
        get => _location;
        set { _location = value; OnPropertyChanged(nameof(Location)); }
    }

    // Wall direction: 'l' (left wall) or 'r' (right wall).
    private string _direction = "l";
    public string Direction
    {
        get => _direction;
        set { _direction = value; OnPropertyChanged(nameof(Direction)); }
    }

    // Poster type number (e.g. the "17"/"13" seen in the captured packet).
    private string _posterType = "1";
    public string PosterType
    {
        get => _posterType;
        set { _posterType = value; OnPropertyChanged(nameof(PosterType)); }
    }

    public ICommand AddPosterCommand { get; }

    public RoomDecorViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        AddPosterCommand = new RelayCommand(async _ => await OnSendToClient(
            ClientPacketBuilder.WallItem(PosterId, Sprite, Owner, Wall, Location, Direction, PosterType)));
    }
}
