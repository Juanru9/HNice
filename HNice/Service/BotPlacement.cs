namespace HNice.Service;

/// <summary>A tile for a spawned avatar and the direction it faces (0 = north, clockwise).</summary>
public readonly record struct BotSpot(int X, int Y, int Rotation);

/// <summary>Finds free floor tiles for spawned avatars: rings around a centre tile, nearest first.</summary>
public static class BotPlacement
{
    private const int MaxRadius = 8;

    /// <param name="center">Where to gather the avatars (usually your own tile).</param>
    /// <param name="includeCenter">True when the centre tile itself may be used (not when you stand on it).</param>
    /// <param name="isFree">Floor and nobody standing there.</param>
    /// <param name="faceToward">The tile they all look at, or null to face 2 (south-east).</param>
    public static List<BotSpot> Spots((int X, int Y) center, int count, bool includeCenter, Func<int, int, bool> isFree, (int X, int Y)? faceToward)
    {
        var spots = new List<BotSpot>();
        for (var radius = includeCenter ? 0 : 1; radius <= MaxRadius && spots.Count < count; radius++)
        {
            // Tiles on this ring, the ones straight in front first so small groups look tidy.
            var ring = RingTiles(center, radius)
                .OrderBy(t => Math.Abs(t.X - center.X) + Math.Abs(t.Y - center.Y))
                .ThenBy(t => t.Y).ThenBy(t => t.X);
            foreach (var (x, y) in ring)
            {
                if (spots.Count == count) break;
                if (!isFree(x, y)) continue;
                spots.Add(new BotSpot(x, y, faceToward is { } target ? Facing((x, y), target) : 2));
            }
        }
        return spots;
    }

    /// <summary>Direction from <paramref name="from"/> toward <paramref name="to"/>.</summary>
    public static int Facing((int X, int Y) from, (int X, int Y) to) => (Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y)) switch
    {
        (0, -1) => 0, (1, -1) => 1, (1, 0) => 2, (1, 1) => 3,
        (0, 1) => 4, (-1, 1) => 5, (-1, 0) => 6, (-1, -1) => 7,
        _ => 2,
    };

    private static IEnumerable<(int X, int Y)> RingTiles((int X, int Y) center, int radius)
    {
        if (radius == 0)
        {
            yield return center;
            yield break;
        }
        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                var (x, y) = (center.X + dx, center.Y + dy);
                if (x >= 0 && y >= 0) yield return (x, y);
            }
        }
    }
}
