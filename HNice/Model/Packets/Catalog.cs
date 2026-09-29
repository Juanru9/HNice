using HNice.Util;
using HNice.Util.Extensions;
using System.Text;

namespace HNice.Model.Packets;

/// <summary>A catalogue page as listed in the index.</summary>
public sealed record CatalogPageRef(string PageName, string Caption, string Category);

/// <summary>A furni offered on a catalogue page.</summary>
/// <param name="Sprite">Class as rooms send it, colour variant included (e.g. "chair_plasto*9").</param>
/// <param name="Colors">Part colours as rooms send them ("#ffffff,#533e10,..." or "0,0,0").</param>
/// <param name="IsStateful">Carries a state the client toggles (lamps, drink machines, teleports).</param>
public sealed record CatalogItem(
    string Sprite, string Name, string Description, int Price, bool IsWallItem, int Width, int Length,
    string Colors, bool IsStateful, string DefinitionId, string PageName, string PageCaption, string Category)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Sprite : Name;

    public string SizeText => IsWallItem ? "wall" : $"{Width}×{Length}";

    /// <summary>Colour swatches (hex only).</summary>
    public IReadOnlyList<string> Swatches => Colors.Split(',').Where(c => c.StartsWith('#')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>
/// CATALOGINDEX (126): the catalogue tree, depth first. Layout validated on a live capture:
///   node = visible(VL64) icon(VL64) color(VL64) pageName[2] caption[2] unknown(VL64) childCount(VL64), then its children.
/// Folders have pageName "-1"; the root caption is "root".
/// </summary>
public static class CatalogIndex
{
    /// <param name="body">Packet text after the 2-char header.</param>
    public static bool TryParse(string body, out List<CatalogPageRef> pages)
    {
        pages = new List<CatalogPageRef>();
        try
        {
            var reader = new IncomingPacketReader(body);
            ReadNode(reader, category: string.Empty, isRoot: true, pages, depth: 0);
            return !reader.HasMore;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void ReadNode(IncomingPacketReader reader, string category, bool isRoot, List<CatalogPageRef> pages, int depth)
    {
        if (depth > 8) throw new FormatException("Catalogue tree too deep.");

        reader.ReadInt();                                  // visible
        reader.ReadInt();                                  // icon
        reader.ReadInt();                                  // color
        var pageName = reader.ReadString();
        var caption = PacketText.FromWire(reader.ReadString());
        reader.ReadInt();                                  // unknown (always 0 so far)
        var children = reader.ReadInt();

        var isFolder = pageName == "-1";
        if (!isFolder) pages.Add(new CatalogPageRef(pageName, caption, category));

        var childCategory = isRoot ? string.Empty : isFolder ? caption : category;
        for (var i = 0; i < children; i++)
        {
            ReadNode(reader, childCategory, isRoot: false, pages, depth + 1);
        }
    }

    /// <summary>GET_CATALOG_PAGE (102), as the client sends it: "production", page name, language, each as a B64-length string.</summary>
    public static string PageRequest(string pageName, string language = "en")
    {
        var sb = new StringBuilder(((int)OutcomingPacketMessage.GET_CATALOG_PAGE).EncodeB64());
        foreach (var value in new[] { "production", pageName, language })
        {
            sb.Append(value.Length.EncodeB64()).Append(value);
        }
        return sb.ToString();
    }
}

/// <summary>
/// CATALOGPAGE (127): old-style "key:value" lines separated by CR. Example head:
///   i:front_page / n:front_page / l:ctlg_frontpage2 / g:catal_fp_header / b:0 / h:text...
/// Items are "p:" lines, tab-separated (captured live):
///   name, description, special price+chr(2), credits, false, type (s floor / i wall / d deal), class, stateful (0/2),
///   "w,l", purchase code, colours, definition id  [deals: + part count, then class, amount, colour per part]
/// </summary>
public static class CatalogPage
{
    /// <summary>The page id ("i:" line), or null when this is not a catalogue page body.</summary>
    public static string? PageId(string body)
    {
        var firstLine = body.Split('\r', 2)[0];
        return firstLine.StartsWith("i:", StringComparison.Ordinal) ? firstLine[2..] : null;
    }

    public static IEnumerable<(string Key, string Value)> Lines(string body) =>
        body.Split('\r', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.IndexOf(':') is var colon and > 0 ? (line[..colon], line[(colon + 1)..]) : (string.Empty, line));

    /// <summary>The floor and wall furni on a page. Deals (bundles) are skipped: their parts are sold on their own pages too.</summary>
    public static List<CatalogItem> Items(string body, CatalogPageRef page)
    {
        var items = new List<CatalogItem>();
        foreach (var (key, value) in Lines(body))
        {
            if (key != "p") continue;
            var f = value.Split('\t');
            if (f.Length < 12 || f[5] is not ("s" or "i") || string.IsNullOrWhiteSpace(f[6])) continue;

            var size = f[8].Split(',');
            items.Add(new CatalogItem(
                Sprite: f[6],
                Name: PacketText.FromWire(f[0]),
                Description: PacketText.FromWire(f[1]),
                Price: int.TryParse(f[3], out var price) ? price : 0,
                IsWallItem: f[5] == "i",
                Width: size.Length == 2 && int.TryParse(size[0], out var w) && w > 0 ? w : 1,
                Length: size.Length == 2 && int.TryParse(size[1], out var l) && l > 0 ? l : 1,
                Colors: f[10],
                IsStateful: f[7] == "2",
                DefinitionId: f[11],
                PageName: page.PageName,
                PageCaption: page.Caption,
                Category: page.Category));
        }
        return items;
    }
}
