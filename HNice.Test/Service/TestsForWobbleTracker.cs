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

    [Fact]
    public void ShouldSignalOnlyStatusesAndCountRounds()
    {
        var wobble = new WobbleTracker();
        var statuses = 0;
        wobble.StatusRead += () => statuses++;

        wobble.Observe(P("As0:3\r1:4"));
        wobble.Observe(P("Ar0:3\r1:4"));
        wobble.Observe(P("Ar0:3\r1:4"));                       // PT_START twice within 2 ms, as at 01:33:17.158
        statuses.Should().Be(0);
        wobble.Round.Should().Be(1);

        wobble.Observe(P("Av-3\t0\t-\t\r4\t0\t-\t\r"));
        statuses.Should().Be(1);

        wobble.Observe(P("Aw1"));
        wobble.Observe(P("At"));
        wobble.Observe(P("As0:3\r1:4"));
        wobble.Round.Should().Be(2);
    }

    // Positions and balances from the 01:33-01:34 rounds; "me" is the right player (slot 1) at 4.
    [Theory]
    [InlineData(0, 4, -3, 0, 'X')]     // gap on the plank: walk down towards them
    [InlineData(0, -3, 4, 0, 'S')]     // as the left player: walk up towards them
    [InlineData(0, 4, 1, 0, 'X')]      // 3 apart is still out of reach (01:10: a hit from 3 missed)
    [InlineData(-31, 4, -3, 0, 'D')]   // tilted down: lean up
    [InlineData(-21, 4, 2, 0, 'D')]    // being hit (01:33:40): balance first
    [InlineData(20, 4, 2, 0, 'A')]     // tilted up: lean down
    [InlineData(-15, 4, 2, 19, 'W')]   // 2 apart, they lean up: W pushes them further up (01:34:19)
    [InlineData(5, 4, 2, -40, 'E')]    // they lean down: E pushes them further down (01:33:40)
    [InlineData(5, 4, 2, 0, 'W')]      // they stand straight: a landed W nudges you back down
    [InlineData(-19, 4, 2, 1, 'E')]    // barely leaning: a landed E nudges you back up
    [InlineData(-5, 4, 2, -10, 'E')]   // leaning enough: push them
    public void AutoPlayerShouldBalanceThenCloseInThenHit(int myBalance, int myPosition, int theirPosition, int theirBalance, char expected)
    {
        var me = new WobblePlayer(1, 4, myPosition, myBalance, "-");
        var them = new WobblePlayer(0, 3, theirPosition, theirBalance, "-");
        new WobbleAutoPlayer().Choose(me, them).Should().Be(expected);
    }

    [Fact]
    public void ShouldFindYouOnThePlankFromThePoolUserList()
    {
        // Captured in md_a at 01:32:56: Bateman is room avatar 4, slot 1 in the rounds that followed.
        const string users = "@\\IPA`j]ABateman[2]hd-195-1021.ch-260-1314.lg-281-1189.sh-300-1314.ha-1003-1189[2]m[2][2]IQA7.0[2]ch=s02/238,238,238[2][2]Istd[2]std[2]PAHHH";
        var local = new LocalInteractions();
        local.SetMe(null, "Bateman");
        local.RewriteInbound(Encoding.Latin1.GetBytes(users.Replace("[2]", "\u0002")));

        var wobble = new WobbleTracker();
        wobble.Observe(P("As0:3\r1:4"));
        wobble.Observe(P("Ar0:3\r1:4"));

        local.MyRoomIndex.Should().Be(4);
        wobble.SlotOf(local.MyRoomIndex).Should().Be(1);
    }

    [Fact]
    public void AutoPlayerShouldRebalanceOncePerRound()
    {
        var player = new WobbleAutoPlayer();
        var me = new WobblePlayer(1, 4, 4, 80, "-");
        var them = new WobblePlayer(0, 3, 2, 0, "-");

        player.Choose(me, them).Should().Be('0');
        player.Choose(me, them).Should().Be('A');
        player.NewRound();
        player.Choose(me, them).Should().Be('0');
    }

    [Theory]
    [InlineData("A", "lean left")]
    [InlineData("W", "hit, pushing right")]
    [InlineData("E", "hit, pushing left")]
    [InlineData("X", "step left")]
    [InlineData("S", "step right")]
    [InlineData("0", "rebalance")]
    [InlineData("-", "—")]
    public void ShouldNameTheMoves(string letter, string name) => WobbleTracker.MoveName(letter).Should().Be(name);
}
