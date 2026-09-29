using FluentAssertions;
using HNice.Service;
using System.Text;

namespace HNice.Test.Service;

public class TestsForFloorMap
{
    [Fact]
    public void ShouldKnowWhichTilesAreFloor()
    {
        var local = new LocalInteractions();
        local.IsFloorTile(4, 12).Should().BeNull("no room map yet");

        // Captured HEIGHTMAP of the room where a bot was sent to 4,12 (outside the floor).
        var rows = new[] { "xxxxxxxxxxxx", "xxxxxxxxxxxx", "xxxxxxxxxxxx", "xxxxxxxxxxxx", "xxxxxxxxxxxx",
                           "xxxxx000000x", "xxxxx000000x", "xxxx0000000x", "xxxxx000000x", "xxxxx000000x", "xxxxx000000x",
                           "xxxxxxxxxxxx", "xxxxxxxxxxxx" };
        local.RewriteInbound(Encoding.Latin1.GetBytes("@_" + string.Join('\r', rows) + "\r"));

        local.IsFloorTile(4, 12).Should().BeFalse();
        local.IsFloorTile(5, 5).Should().BeTrue();
        local.IsFloorTile(4, 7).Should().BeTrue();
        local.IsFloorTile(40, 40).Should().BeFalse();

        local.RewriteInbound(Encoding.Latin1.GetBytes("AEmodel_a 581"));
        local.IsFloorTile(5, 5).Should().BeNull("a new room forgets the old map");
    }
}
