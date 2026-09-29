using HNice.Model.Packets;
using HNice.Service;
using System.Globalization;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Client-side wall items (posters), using the real ITEMS (@m) format captured from the live server:
/// id [tab] sprite [tab] owner [tab] ":w=&lt;wall x,y&gt; l=&lt;offset x,y&gt; &lt;side&gt;" [tab] type [CR].
/// Port of SnG Fun v1's "Posters". Local only.
/// </summary>
class RoomDecorViewModel : BaseViewModel
{
    private string _posterType = "13";
    /// <summary>Poster design number (the "17"/"13" seen in live rooms).</summary>
    public string PosterType { get => _posterType; set { _posterType = value; OnPropertyChanged(); } }

    private int _wallX = 4;
    public int WallX { get => _wallX; set { _wallX = value; OnPropertyChanged(); } }

    private int _wallY = 2;
    public int WallY { get => _wallY; set { _wallY = value; OnPropertyChanged(); } }

    private int _offsetX = 29;
    public int OffsetX { get => _offsetX; set { _offsetX = value; OnPropertyChanged(); } }

    private int _offsetY = 45;
    public int OffsetY { get => _offsetY; set { _offsetY = value; OnPropertyChanged(); } }

    // "l" (left wall) or "r" (right wall).
    private string _direction = "l";
    public string Direction { get => _direction; set { _direction = value; OnPropertyChanged(); } }

    #region Advanced
    private string _posterId = "999000001";
    public string PosterId { get => _posterId; set { _posterId = value; OnPropertyChanged(); } }

    private string _sprite = "poster";
    public string Sprite { get => _sprite; set { _sprite = value; OnPropertyChanged(); } }

    // Empty = your own name once known.
    private string _owner = string.Empty;
    public string Owner { get => _owner; set { _owner = value; OnPropertyChanged(); } }
    #endregion

    public ICommand AddPosterCommand { get; }

    public RoomDecorViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        AddPosterCommand = new RelayCommand(async _ => await AddPoster());
    }

    private Task AddPoster()
    {
        var owner = !string.IsNullOrWhiteSpace(Owner) ? Owner : Worker.CurrentPlayer?.HabboName ?? "HNice";
        var wall = string.Create(CultureInfo.InvariantCulture, $"{WallX},{WallY}");
        var offset = string.Create(CultureInfo.InvariantCulture, $"{OffsetX},{OffsetY}");
        return OnSendToClient(ClientPacketBuilder.WallItem(PosterId, Sprite, owner, wall, offset, Direction, PosterType));
    }
}
