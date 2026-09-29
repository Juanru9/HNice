using HNice.Model.Packets;
using HNice.Service;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Furni browser: search the catalogue harvested from the game, pick an item and drop it next to you.
/// Placing injects an ACTIVEOBJECTS packet toward the client, so the item appears on your own screen only.
/// </summary>
class FurnitureViewModel : BaseViewModel
{
    private const int MaxRecent = 8;
    private static readonly string RecentPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HNice", "recent-furni.txt");

    /// <summary>A catalogue page in the page picker; null PageName = every page.</summary>
    public sealed record PageFilter(string? PageName, string Caption, string Category);

    private static readonly PageFilter AllPages = new(null, "All pages", " ");

    #region Browse
    public ObservableCollection<PageFilter> Pages { get; } = new() { AllPages };
    public ListCollectionView PagesView { get; }

    private PageFilter _selectedPage = AllPages;
    public PageFilter SelectedPage
    {
        get => _selectedPage;
        set { _selectedPage = value ?? AllPages; OnPropertyChanged(); ApplyFilter(); }
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value ?? string.Empty; OnPropertyChanged(); ApplyFilter(); }
    }

    private List<CatalogItem> _all = new();
    public ObservableCollection<CatalogItem> Results { get; } = new();

    private string _resultSummary = string.Empty;
    public string ResultSummary { get => _resultSummary; private set { _resultSummary = value; OnPropertyChanged(); } }

    /// <summary>Nothing harvested yet: the empty state explains how to fill the catalogue.</summary>
    public bool IsCatalogEmpty => _all.Count == 0;

    public ObservableCollection<CatalogItem> Recent { get; } = new();
    public bool HasRecent => Recent.Count > 0;

    private CatalogItem? _selectedItem;
    public CatalogItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            if (value is null) return;
            SpriteName = value.Sprite;
            Colors = value.Colors;
        }
    }

    public bool HasSelection => _selectedItem is not null;
    #endregion

    #region Load the whole catalogue
    private bool _isEncrypted;
    private bool _isFetching;
    public bool IsFetching { get => _isFetching; private set { _isFetching = value; OnPropertyChanged(); OnPropertyChanged(nameof(FetchButtonText)); } }

    public string FetchButtonText => IsFetching ? "Stop" : _all.Count == 0 ? "Load whole catalogue" : "Update catalogue";

    private string _catalogStatus = string.Empty;
    public string CatalogStatus { get => _catalogStatus; private set { _catalogStatus = value; OnPropertyChanged(); } }

    private CancellationTokenSource? _fetchCts;
    #endregion

    #region Place
    private bool _placeNextToMe = true;
    /// <summary>Drop it on the tile you are facing instead of the X/Y below.</summary>
    public bool PlaceNextToMe { get => _placeNextToMe; set { _placeNextToMe = value; OnPropertyChanged(); } }

    private int _xCoord = 10;
    public int XCoord { get => _xCoord; set { _xCoord = value; OnPropertyChanged(); } }

    private int _yCoord = 6;
    public int YCoord { get => _yCoord; set { _yCoord = value; OnPropertyChanged(); } }

    // Most furni are only drawn facing 2 or 4; other directions can render a placeholder box.
    private int _rotation = 2;
    public int Rotation { get => _rotation; set { _rotation = value; OnPropertyChanged(); } }

    private string _placeMessage = string.Empty;
    public string PlaceMessage { get => _placeMessage; private set { _placeMessage = value; OnPropertyChanged(); } }
    #endregion

    #region Advanced
    private string _spriteName = string.Empty;
    public string SpriteName { get => _spriteName; set { _spriteName = value; OnPropertyChanged(); } }

    private string _colors = string.Empty;
    public string Colors { get => _colors; set { _colors = value; OnPropertyChanged(); } }

    // Each placement takes the next id, so several furni can stand side by side.
    private long _furniId = 999000100;
    public string FurniId { get => _furniId.ToString(); set { if (long.TryParse(value, out var id)) _furniId = id; OnPropertyChanged(); } }

    // 0 = use your own user id once known.
    private int _ownerId;
    public int OwnerId { get => _ownerId; set { _ownerId = value; OnPropertyChanged(); } }

    // Optional trailing state (e.g. the on/off digit some furni carry); empty for plain furni.
    private string _extraData = "";
    public string ExtraData { get => _extraData; set { _extraData = value; OnPropertyChanged(); } }
    #endregion

    public ICommand PlaceFurniCommand { get; }
    public ICommand PickRecentCommand { get; }
    public ICommand FetchCatalogCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public FurnitureViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        PagesView = new ListCollectionView(Pages);
        PagesView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PageFilter.Category)));

        PlaceFurniCommand = new RelayCommand(async _ => await OnPlaceFurni(), _ => CanPlace);
        PickRecentCommand = new RelayCommand(p => { if (p is CatalogItem item) Select(item); });
        FetchCatalogCommand = new RelayCommand(async _ => await OnFetchCatalog(), _ => IsFetching || (_isEncrypted && worker.Catalog.Pages.Count > 0));
        ClearSearchCommand = new RelayCommand(_ => SearchText = string.Empty);

        worker.StatusChanged += status =>
        {
            _isEncrypted = status == ProxyStatus.Encrypted;
            Application.Current?.Dispatcher.BeginInvoke(CommandManager.InvalidateRequerySuggested);
        };
        worker.Catalog.Changed += () => Application.Current?.Dispatcher.BeginInvoke(Reload);

        Reload();
        LoadRecent();
    }

    private bool CanPlace => !string.IsNullOrWhiteSpace(SpriteName) && SelectedItem is not { IsWallItem: true };

    #region Catalogue
    private void Reload()
    {
        var catalog = Worker.Catalog;

        // One entry per sprite: the same furni is sometimes sold on two pages.
        _all = catalog.Items.GroupBy(i => i.Sprite, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();

        var pageNames = _all.Select(i => i.PageName).ToHashSet(StringComparer.Ordinal);
        var pages = catalog.Pages.Where(p => pageNames.Contains(p.PageName))
            .Select(p => new PageFilter(p.PageName, p.Caption, string.IsNullOrEmpty(p.Category) ? "Pages" : p.Category))
            .ToList();
        if (!pages.Select(p => p.PageName).SequenceEqual(Pages.Skip(1).Select(p => p.PageName)))
        {
            var keep = SelectedPage.PageName;
            Pages.Clear();
            Pages.Add(AllPages);
            foreach (var page in pages) Pages.Add(page);
            _selectedPage = Pages.FirstOrDefault(p => p.PageName == keep) ?? AllPages;
            OnPropertyChanged(nameof(SelectedPage));
        }

        OnPropertyChanged(nameof(IsCatalogEmpty));
        OnPropertyChanged(nameof(FetchButtonText));
        if (!IsFetching)
        {
            CatalogStatus = _all.Count == 0
                ? catalog.Pages.Count == 0 ? string.Empty : $"{catalog.Pages.Count} pages found. Load them to browse the furni."
                : $"{_all.Count} furni from {catalog.PagesLoaded} pages";
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var terms = SearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matches = _all
            .Where(i => SelectedPage.PageName is null || i.PageName == SelectedPage.PageName)
            .Where(i => terms.All(t =>
                i.DisplayName.Contains(t, StringComparison.CurrentCultureIgnoreCase) ||
                i.Sprite.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(t, StringComparison.CurrentCultureIgnoreCase)))
            .OrderBy(i => i.IsWallItem)
            .ThenBy(i => i.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var selected = SelectedItem;
        Results.Clear();
        foreach (var item in matches) Results.Add(item);
        // Catalogue items are records: a reloaded copy of the selection is equal to it and stays selected.
        if (selected is not null && matches.Contains(selected)) SelectedItem = matches.First(m => m == selected);

        ResultSummary = _all.Count == 0 ? string.Empty
            : matches.Count == _all.Count ? $"{matches.Count} furni"
            : matches.Count == 0 ? "No furni match" : $"{matches.Count} of {_all.Count}";
    }

    private async Task OnFetchCatalog()
    {
        if (IsFetching)
        {
            _fetchCts?.Cancel();
            return;
        }

        _fetchCts = new CancellationTokenSource();
        IsFetching = true;
        var progress = new Progress<string>(p => CatalogStatus = $"Loading page {p.Split(' ')[0]}...");
        try
        {
            var fetched = await Worker.FetchCatalogAsync(null, progress, _fetchCts.Token);
            // The last answer can land just after the last request.
            await Task.Delay(1500);
            CatalogStatus = $"{_all.Count} furni from {Worker.Catalog.PagesLoaded} pages";
        }
        catch (OperationCanceledException)
        {
            CatalogStatus = $"Stopped. {_all.Count} furni so far.";
        }
        finally
        {
            IsFetching = false;
            _fetchCts.Dispose();
            _fetchCts = null;
            Reload();
        }
    }
    #endregion

    #region Place
    private void Select(CatalogItem item)
    {
        SearchText = string.Empty;
        SelectedPage = AllPages;
        SelectedItem = Results.FirstOrDefault(r => r.Sprite == item.Sprite) ?? item;
    }

    private async Task OnPlaceFurni()
    {
        var sprite = SpriteName.Trim();
        var item = SelectedItem is { } selected && selected.Sprite == sprite
            ? selected
            : _all.FirstOrDefault(i => i.Sprite.Equals(sprite, StringComparison.OrdinalIgnoreCase));

        if (PlaceNextToMe)
        {
            if (Worker.MyStatus is not { } me)
            {
                PlaceMessage = "Your avatar was not found in the room yet. Walk one step, or turn off \"Next to me\" and give a tile.";
                return;
            }
            // The tile you are facing (0 = north, clockwise).
            var (dx, dy) = me.BodyRotation switch
            {
                0 => (0, -1), 1 => (1, -1), 2 => (1, 0), 3 => (1, 1),
                4 => (0, 1), 5 => (-1, 1), 6 => (-1, 0), _ => (-1, -1),
            };
            XCoord = me.X + dx;
            YCoord = me.Y + dy;
        }

        var id = FurniId;
        var owner = OwnerId > 0 ? OwnerId : Worker.CurrentPlayer?.UserId ?? 1;
        // Clicks on it (lamps, switches, machines) are then answered locally.
        Worker.RegisterFakeItem(id);
        var packet = ClientPacketBuilder.ActiveObject(id, owner, sprite, XCoord, YCoord, Rotation,
            colors: Colors.Trim(), state: ExtraData, width: item?.Width ?? 1, length: item?.Length ?? 1);
        await OnSendToClient(packet);

        _furniId++;
        OnPropertyChanged(nameof(FurniId));
        PlaceMessage = $"Placed {item?.DisplayName ?? sprite} at {XCoord},{YCoord}.";
        if (item is not null) Remember(item);
    }

    private void Remember(CatalogItem item)
    {
        var existing = Recent.FirstOrDefault(r => r.Sprite == item.Sprite);
        if (existing is not null) Recent.Remove(existing);
        Recent.Insert(0, item);
        while (Recent.Count > MaxRecent) Recent.RemoveAt(Recent.Count - 1);
        OnPropertyChanged(nameof(HasRecent));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RecentPath)!);
            File.WriteAllLines(RecentPath, Recent.Select(r => r.Sprite));
        }
        catch (IOException)
        {
            // Remembering picks is a convenience.
        }
    }

    private void LoadRecent()
    {
        try
        {
            if (!File.Exists(RecentPath)) return;
            var bySprite = _all.ToDictionary(i => i.Sprite, StringComparer.OrdinalIgnoreCase);
            foreach (var sprite in File.ReadLines(RecentPath).Take(MaxRecent))
            {
                if (bySprite.TryGetValue(sprite, out var item)) Recent.Add(item);
            }
            OnPropertyChanged(nameof(HasRecent));
        }
        catch (IOException)
        {
        }
    }
    #endregion
}
