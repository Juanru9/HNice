using HNice.Model;
using System.Text.RegularExpressions;

namespace HNice.Util;

public static class PacketExtractor
{
    public static Coordinate ExtractCoordinates(string input)
    {
        // Define a regular expression to match the coordinates pattern
        var match = Regex.Match(input, @"mv\s+(\d+),(\d+),");

        if (match.Success)
        {
            // Parse the matched coordinates
            if (int.TryParse(match.Groups[1].Value, out int x) && int.TryParse(match.Groups[2].Value, out int y))
            {
                return new Coordinate(x,y);
            }
        }

        return new Coordinate();
    }
}
