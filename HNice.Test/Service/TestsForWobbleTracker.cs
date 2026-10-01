using FluentAssertions;
using HNice.Service;
using System.Text;

namespace HNice.Test.Service;

/// <summary>Replays a round captured in the pool (md_a, 2026-10-02 01:10:33-01:10:44).</summary>
public class TestsForWobbleTracker
{
    private static byte[] P(string text) => Encoding.Latin1.GetBytes(text);

    [Fact]
    public void ShouldFollowACapturedRound()
    {
        var wobble = new WobbleTracker();
        var results = new List<WobbleResult>();
        wobble.RoundEnded += results.Add;

        wobble.Observe(P("As0:3\r1:4"));                       // PT_PREPARE: avatar 3 left, avatar 4 right
        wobble.Players.Select(p => (p.RoomIndex, p.Position)).Should().Equal((3, -3), (4, 4));

        wobble.Observe(P("Ar0:3\r1:4"));                       // PT_START
        wobble.Observe(P("Av-3\t-21\tA\t\r4\t0\t-\t\r"));      // PT_STATUS
        wobble.Players[0].Should().Be(new WobblePlayer(0, 3, -3, -21, "A"));
        wobble.Players[1].Should().Be(new WobblePlayer(1, 4, 4, 0, "-"));

        wobble.Observe(P("Aw0"));                              // PT_WIN: left wins
        results.Should().ContainSingle().Which.Should().Match<WobbleResult>(r =>
            r.LeftRoomIndex == 3 && r.RightRoomIndex == 4 && r.WinnerSlot == 0);

        wobble.Observe(P("At"));                               // PT_END
        wobble.Players.Should().BeEmpty();
    }

    [Fact]
    public void ShouldIgnoreOtherPacketsAndBrokenStatus()
    {
        var wobble = new WobbleTracker();
        var changed = 0;
        wobble.Changed += () => changed++;

        wobble.Observe(P("@bIJQAJ0.0\u0002JJ/\u0002"));         // STATUS, not the game
        wobble.Observe(P("Av-3\t0\t-\t\r4\t0\t-\t\r"));        // game status before any round
        wobble.Observe(P("Aw1"));

        changed.Should().Be(0);
        wobble.Players.Should().BeEmpty();
    }

    [Fact]
    public void ShouldOnlyBeLiveBetweenStartAndTheWin()
    {
        var wobble = new WobbleTracker();

        wobble.Observe(P("As0:3\r1:4"));
        wobble.IsLive.Should().BeFalse();
        wobble.SlotOf(4).Should().Be(1);
        wobble.SlotOf(7).Should().BeNull();

        wobble.Observe(P("Ar0:3\r1:4"));
        wobble.IsLive.Should().BeTrue();

        wobble.Observe(P("Aw1"));
        wobble.IsLive.Should().BeFalse();
        wobble.Observe(P("Ar0:3\r1:4"));                       // a late PT_START does not reopen a decided round
        wobble.IsLive.Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 0, 4, 0, 'X')]      // gap on the plank: walk up
    [InlineData(-21, 0, 1, 0, 'D')]    // tilted left: lean right
    [InlineData(30, 0, 1, 0, 'A')]     // tilted right: lean left
    [InlineData(5, 0, 1, -40, 'W')]    // next to them, they lean left: hit left
    [InlineData(5, 0, 1, 40, 'E')]
    public void AutoPlayerShouldBalanceThenCloseInThenHit(int myBalance, int myPosition, int theirPosition, int theirBalance, char expected)
    {
        var me = new WobblePlayer(0, 3, myPosition, myBalance, "-");
        var them = new WobblePlayer(1, 4, theirPosition, theirBalance, "-");
        new WobbleAutoPlayer().Choose(me, them).Should().Be(expected);
    }

    [Fact]
    public void AutoPlayerShouldRebalanceOncePerRound()
    {
        var player = new WobbleAutoPlayer();
        var me = new WobblePlayer(0, 3, 0, 80, "-");
        var them = new WobblePlayer(1, 4, 1, 0, "-");

        player.Choose(me, them).Should().Be('0');
        player.Choose(me, them).Should().Be('A');
        player.NewRound();
        player.Choose(me, them).Should().Be('0');
    }

    [Theory]
    [InlineData("A", "lean left")]
    [InlineData("E", "hit right")]
    [InlineData("0", "rebalance")]
    [InlineData("-", "—")]
    public void ShouldNameTheMoves(string letter, string name) => WobbleTracker.MoveName(letter).Should().Be(name);
}
