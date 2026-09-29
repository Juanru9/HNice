using FluentAssertions;
using HNice.Model.Packets;
using HNice.Service;

namespace HNice.Test.Service;

public class TestsForBotPlacement
{
    [Fact]
    public void ShouldFillFreeTilesAroundMeFacingMe()
    {
        var spots = BotPlacement.Spots((5, 5), 8, includeCenter: false, (_, _) => true, faceToward: (5, 5));

        spots.Should().HaveCount(8);
        spots.Should().NotContain(s => s.X == 5 && s.Y == 5, "I stand there");
        spots.Should().OnlyContain(s => Math.Max(Math.Abs(s.X - 5), Math.Abs(s.Y - 5)) == 1, "the first ring fits eight");
        spots.Single(s => (s.X, s.Y) == (5, 4)).Rotation.Should().Be(4, "north of me looks south");
        spots.Single(s => (s.X, s.Y) == (4, 5)).Rotation.Should().Be(2, "west of me looks east");
    }

    [Fact]
    public void ShouldSkipWallsAndTakenTilesAndGoFurther()
    {
        // Captured room: floor only on y 5..10, x 5..10 (x 4 on row 7).
        bool Floor(int x, int y) => y is >= 5 and <= 10 && (x is >= 5 and <= 10 || (y == 7 && x == 4));
        var taken = new HashSet<(int, int)> { (6, 6) };

        var spots = BotPlacement.Spots((5, 5), 10, includeCenter: false, (x, y) => Floor(x, y) && !taken.Contains((x, y)), faceToward: (5, 5));

        spots.Should().HaveCount(10);
        spots.All(s => Floor(s.X, s.Y) && !taken.Contains((s.X, s.Y))).Should().BeTrue("walls and taken tiles are skipped");
        spots.Select(s => (s.X, s.Y)).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ShouldStopWhenNothingIsFree()
    {
        BotPlacement.Spots((5, 5), 3, includeCenter: true, (_, _) => false, null).Should().BeEmpty();
    }

    [Fact]
    public void ShouldBuildSeveralUsersInOnePacket()
    {
        var bots = new[]
        {
            new RoomUser(100, 999000100, "Bot 1", "hd-180-1", "M", "", "", 4, 5),
            new RoomUser(101, 999000101, "Bot 2", "hd-180-1", "M", "", "", 6, 5),
        };

        var packet = RoomUsers.Build(bots);

        RoomUsers.TryParse(packet[2..], out var parsed).Should().BeTrue();
        parsed.Should().Equal(bots.Select(b => b with { Sex = "m" }));
    }
}
