using HNice.Model.Packets;
using HNice.Util.Extensions;
using System.IO;
using System.Text;
using System.Text.Json;

namespace HNice.Service;

/// <summary>
/// The furni catalogue, built from the game's own catalogue packets as they pass through HNice:
///   ← CATALOGINDEX (126) the page tree, ← CATALOGPAGE (127) one page with its items.
/// Pages can also be requested by HNice itself (<see cref="FetchAsync"/>); their answers are recorded but kept
/// from the game window. Everything is saved to %LOCALAPPDATA%\HNice\catalog.json, so it survives restarts.
/// Until that file exists, the snapshot shipped in Assets\catalog.json is used.
/// </summary>
public sealed class FurniCatalog
{
    private const int CatalogIndexHeader = (int)IncomingPacketMessage.CATALOGINDEX;
    private const int CatalogPageHeader = (int)IncomingPacketMessage.CATALOGPAGE;
    private static readonly TimeSpan PagePace = TimeSpan.FromMilliseconds(1000);
    // A page the server never answers must not swallow the player's own click on it later.
    private static readonly TimeSpan RequestExpiry = TimeSpan.FromSeconds(10);
    private static readonly Encoding Latin1 = Encoding.Latin1;

    private readonly object _sync = new();
    private readonly string _path;
    private readonly Dictionary<string, DateTime> _selfRequested = new(StringComparer.Ordinal);

    private List<CatalogPageRef> _pages = new();
    private Dictionary<string, string> _rawPages = new(StringComparer.Ordinal);
    private Dictionary<string, List<CatalogItem>> _items = new(StringComparer.Ordinal);

    /// <summary>Pages or items changed. Raised on a network thread.</summary>
    public event Action? Changed;

    // Catalogue snapshot kept in the repo, copied next to the exe.
    private static readonly string BundledPath = Path.Combine(AppContext.BaseDirectory, "Assets", "catalog.json");

    private FurniCatalog(string path) => _path = path;

    public static FurniCatalog LoadDefault()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HNice");
        var catalog = new FurniCatalog(Path.Combine(dir, "catalog.json"));
        catalog.Load();
        return catalog;
    }

    public IReadOnlyList<CatalogPageRef> Pages { get { lock (_sync) return _pages.ToList(); } }

    public IReadOnlyList<CatalogItem> Items { get { lock (_sync) return _items.Values.SelectMany(i => i).ToList(); } }

    /// <summary>Pages received so far (raw), for inspection.</summary>
    public int PagesLoaded { get { lock (_sync) return _rawPages.Count; } }

    #region Observe traffic
    /// <summary>Records catalogue packets. Returns true when the packet answers HNice's own request and must not reach the client.</summary>
    public bool Observe(byte[] packet)
    {
        if (packet.Length < 2) return false;
        var text = Latin1.GetString(packet);
        var header = text[..2].DecodeB64();

        if (header == CatalogIndexHeader && CatalogIndex.TryParse(text[2..], out var pages))
        {
            lock (_sync)
            {
                _pages = pages;
                _items = _rawPages.ToDictionary(p => p.Key, p => ParseItems(p.Key, p.Value), StringComparer.Ordinal);
            }
            SaveAndNotify();
            return false;
        }

        if (header != CatalogPageHeader || CatalogPage.PageId(text[2..]) is not { } pageId) return false;

        bool swallow;
        lock (_sync)
        {
            _rawPages[pageId] = text[2..];
            _items[pageId] = ParseItems(pageId, text[2..]);
            swallow = _selfRequested.Remove(pageId, out var requestedAt) && DateTime.UtcNow - requestedAt < RequestExpiry;
        }
        SaveAndNotify();
        return swallow;
    }

    // Callers hold _sync.
    private List<CatalogItem> ParseItems(string pageId, string body)
    {
        var page = _pages.FirstOrDefault(p => p.PageName == pageId) ?? new CatalogPageRef(pageId, pageId, string.Empty);
        return CatalogPage.Items(body, page);
    }
    #endregion

    #region Fetch
    /// <summary>
    /// Requests catalogue pages from the server, one per second, like clicking through the tabs.
    /// </summary>
    /// <param name="pages">Page names; null = every page in the index.</param>
    /// <param name="send">Sends a packet to the server.</param>
    public async Task<int> FetchAsync(IEnumerable<string>? pages, Func<string, Task> send, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var names = (pages ?? Pages.Select(p => p.PageName)).Distinct().ToList();
        var done = 0;
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_sync) _selfRequested[name] = DateTime.UtcNow;
            await send(CatalogIndex.PageRequest(name)).ConfigureAwait(false);
            done++;
            progress?.Report($"{done}/{names.Count} {name}");
            await Task.Delay(PagePace, cancellationToken).ConfigureAwait(false);
        }
        return done;
    }
    #endregion

    #region Persistence
    private sealed class Snapshot
    {
        public List<CatalogPageRef> Pages { get; set; } = new();
        public Dictionary<string, string> RawPages { get; set; } = new();
    }

    private void Load()
    {
        try
        {
            var source = File.Exists(_path) ? _path : BundledPath;
            if (!File.Exists(source)) return;
            var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(source));
            if (snapshot is null) return;
            _pages = snapshot.Pages;
            _rawPages = new Dictionary<string, string>(snapshot.RawPages, StringComparer.Ordinal);
            _items = _rawPages.ToDictionary(p => p.Key, p => ParseItems(p.Key, p.Value), StringComparer.Ordinal);
        }
        catch (Exception)
        {
            // A damaged cache is simply rebuilt from traffic.
        }
    }

    private void SaveAndNotify()
    {
        try
        {
            Snapshot snapshot;
            lock (_sync) snapshot = new Snapshot { Pages = _pages.ToList(), RawPages = new Dictionary<string, string>(_rawPages) };
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(snapshot));
        }
        catch (Exception)
        {
            // Saving is best effort; the catalogue stays in memory.
        }
        Changed?.Invoke();
    }
    #endregion
}
