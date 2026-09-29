using FluentAssertions;
using HNice.Model.Packets;
using HNice.Service;

namespace HNice.Test.Service;

public class TestsForMimic
{
    private static readonly DateTime Now = new(2026, 9, 29, 18, 0, 0, DateTimeKind.Utc);

    // Mikee, room index 2 (J), standing on 5,2.
    private static readonly RoomUser Mikee = new(2, 1, "Mikee", "hd-180-1", "M", "", "", 5, 2);

    private static StatusEntry Me(int x, int y, string actions = "/") => new(0, x, y, "0.0", 2, 2, actions);

    private static Mimic Started(MimicParts parts = MimicParts.All, MimicPosition position = MimicPosition.KeepDistance)
    {
        var mimic = new Mimic { Parts = parts };
        mimic.Start(Mikee, Me(7, 2), position, Now);
        return mimic;
    }

    [Fact]
    public void ShouldWalkInStepKeepingTheDistance()
    {
        var mimic = Started();

        // Mikee (index 2) steps toward 6,2.
        var actions = mimic.Plan("@bIJ" + "QAJ" + "0.0\u0002JJ/mv 6,2,0.0/\u0002", Me(7, 2), Now);

        // Walk request 1269 ("Su") to 8,2 (VL64 8 = "PB", 2 = "J").
        actions.Should().ContainSingle().Which.Packet.Should().Be("Su" + "PB" + "J");
    }

    [Fact]
    public void ShouldTryAnotherTileWhenFurniBlocksTheWay()
    {
        // Captured with Yooco: they stood in column 6, the tile beside them (column 7) had furni, and the
        // server ignored the walk; the mime stood still for 13-23 s.
        var mimic = Started(position: MimicPosition.Beside);
        mimic.Plan("@bIJQAK0.0\u0002JJ/\u0002", Me(7, 2), Now).Should().ContainSingle()   // Mikee stands on 5,3
            .Which.Packet.Should().Be("Su" + "RA" + "K");                                  // walk to 6,3 (VL64 6 = "RA")

        mimic.Tick(Me(7, 2), Now.AddMilliseconds(600)).Should().BeEmpty("give the server a moment");

        var retry = mimic.Tick(Me(7, 2), Now.AddMilliseconds(1500));
        retry.Should().ContainSingle();
        retry[0].Packet.Should().NotBe("SuRAK", "6,3 is blocked now");
        retry[0].Activity.Should().Contain("blocked");

        mimic.Tick(Me(6, 2), Now.AddMilliseconds(2000)).Should().BeEmpty("on the way");
    }

    [Fact]
    public void ShouldSkipTilesThatAreNotFreeRightAway()
    {
        var mimic = Started(position: MimicPosition.Beside);
        mimic.IsFree = (x, y) => (x, y) != (6, 3);   // someone stands beside Mikee already

        var walk = mimic.Plan("@bIJQAK0.0\u0002JJ/\u0002", Me(7, 2), Now).Should().ContainSingle().Which;
        walk.Packet.Should().NotBe("SuRAK");
    }

    [Fact]
    public void ShouldGiveUpWhenTheyCannotBeReached()
    {
        // Captured: walled in by gold bars at 9,9, every walk was ignored and the mime tried a new tile every 1.5 s.
        var mimic = Started(position: MimicPosition.Beside);
        string? notice = null;
        mimic.Notice += n => notice = n;
        var me = Me(9, 9);
        mimic.Plan("@bIJQAK0.0\u0002JJ/\u0002", me, Now);

        var sent = 0;
        for (var i = 1; i <= 10; i++)
        {
            sent += mimic.Tick(me, Now.AddMilliseconds(1500 * i)).Count;
        }

        sent.Should().Be(2, "two other tiles are tried, then it stops");
        notice.Should().Contain("Can't reach");

        // They move: copying goes on as usual.
        mimic.Plan("@bIJQAPA0.0\u0002JJ/mv 5,5,0.0/\u0002", me, Now.AddSeconds(20)).Should().ContainSingle();
    }

    [Fact]
    public void ShouldGoToThemRightAwayWhenStarting()
    {
        // Captured: after switching to someone standing still, the mime waited 10 s for them to move.
        var mimic = new Mimic();

        var first = mimic.Start(Mikee, Me(9, 9), MimicPosition.Beside, Now);

        first.Should().ContainSingle().Which.Packet.Should().Be("Su" + "RA" + "J", "the tile beside Mikee (5,2) is 6,2");
    }

    [Fact]
    public void ShouldNotRepeatTheSameWalk()
    {
        var mimic = Started();
        mimic.Plan("@bIJQAJ0.0\u0002JJ/mv 6,2,0.0/\u0002", Me(7, 2), Now);

        mimic.Plan("@bIJQAJ0.0\u0002JJ/mv 6,2,0.0/\u0002", Me(7, 2), Now).Should().BeEmpty();
    }

    [Fact]
    public void ShouldIgnoreOtherPeople()
    {
        var mimic = Started();
        // Index 3 (K) is someone else.
        mimic.Plan("@bIKQAJ0.0\u0002JJ/mv 6,2,0.0/wave/\u0002", Me(7, 2), Now).Should().BeEmpty();
        mimic.Plan("@XKhola\u0002", null, Now).Should().BeEmpty();
    }

    [Fact]
    public void ShouldWaveAndDanceOnce()
    {
        var mimic = Started(MimicParts.Gestures);

        var first = mimic.Plan("@bIJQAJ0.0\u0002JJ/wave/dance/\u0002", Me(7, 2), Now);
        first.Where(a => a.Delay == TimeSpan.Zero).Select(a => a.Packet).Should().Equal("A^", "A]");

        mimic.Plan("@bIJQAJ0.0\u0002JJ/wave/dance/\u0002", Me(7, 2), Now).Should().BeEmpty("the gesture is still the same one");

        // Captured: → STOP AXDance, then the status loses "/dance/".
        mimic.Plan("@bIJQAJ0.0\u0002JJ/\u0002", Me(7, 2), Now).Select(a => a.Packet).Should().Equal("AXDance");
    }

    [Fact]
    public void ShouldKeepWavingWhileTheyKeepWaving()
    {
        var mimic = Started(MimicParts.Gestures);

        // Waving several times in a row shows as one long "/wave/" (captured: 6 s for 3 waves).
        var repeats = mimic.Plan("@bIJQAJ0.0\u0002JJ/wave/\u0002", Me(7, 2), Now).Where(a => a.Delay > TimeSpan.Zero).ToList();
        repeats.Should().NotBeEmpty().And.OnlyContain(a => a.Packet == "A^");
        mimic.IsStillWanted(repeats[0]).Should().BeTrue("they are still waving");

        // Their wave ends: the pending repeats are dropped.
        mimic.Plan("@bIJQAJ0.0\u0002JJ/\u0002", Me(7, 2), Now);
        repeats.Should().OnlyContain(a => !mimic.IsStillWanted(a));

        // A new wave later starts its own repeats; the old ones stay dropped.
        var next = mimic.Plan("@bIJQAJ0.0\u0002JJ/wave/\u0002", Me(7, 2), Now).Where(a => a.Delay > TimeSpan.Zero).ToList();
        mimic.IsStillWanted(next[0]).Should().BeTrue();
        mimic.IsStillWanted(repeats[0]).Should().BeFalse();
    }

    [Fact]
    public void ShouldRepeatChatAndShoutsWithTheSameText()
    {
        var mimic = Started(MimicParts.Speech);

        // Captured shapes: ← CHAT @X index text[2], ← CHAT_3 (shout) @Z index text[2]; → CHAT @t, → SHOUT @w.
        var said = mimic.Plan("@XJhola\u0002", null, Now);
        said.Should().ContainSingle().Which.Packet.Should().Be("@t@Dhola");

        var shouted = mimic.Plan("@ZJque tal\u0002", null, Now);
        shouted.Should().ContainSingle().Which.Packet.Should().Be("@w@Gque tal");
        shouted[0].Delay.Should().BeGreaterThan(said[0].Delay, "messages are spaced out");
    }

    [Fact]
    public void ShouldNotRepeatCommandsOrFloods()
    {
        var mimic = Started(MimicParts.Speech);
        mimic.Plan("@XJ:sit\u0002", null, Now).Should().BeEmpty();

        var sent = Enumerable.Range(0, 10).SelectMany(i => mimic.Plan($"@XJmsg {i}\u0002", null, Now)).ToList();
        sent.Count.Should().BeLessThan(10, "a long backlog is dropped");
    }

    [Fact]
    public void ShouldSkipLinksAndLinesAboutMe()
    {
        var mimic = Started(MimicParts.Speech);
        mimic.MyName = "Bateman";
        string? skipped = null;
        mimic.Skipped += line => skipped = line;

        // Captured: the copied person made the mime shout this.
        mimic.Plan("@ZJme llamo bateman y como mocos\u0002", null, Now).Should().BeEmpty();
        skipped.Should().Be("me llamo bateman y como mocos");
        mimic.Plan("@XJmira www.free-credits.example\u0002", null, Now).Should().BeEmpty();
        mimic.Plan("@XJhola a todos\u0002", null, Now).Should().ContainSingle();

        mimic.SkipRiskyLines = false;
        mimic.Plan("@XJhola bateman\u0002", null, Now.AddSeconds(5)).Should().ContainSingle();
    }

    [Fact]
    public void PartsCanBeSwitchedOffWhileRunning()
    {
        var mimic = Started();
        mimic.Parts = MimicParts.Speech;

        mimic.Plan("@bIJQAJ0.0\u0002JJ/mv 6,2,0.0/wave/\u0002", Me(7, 2), Now).Should().BeEmpty();
    }

    [Fact]
    public void ShouldStopWhenTheyLeave()
    {
        var mimic = Started();
        string? reason = null;
        mimic.Stopped += r => reason = r;

        mimic.Plan("@]2", null, Now);

        mimic.IsRunning.Should().BeFalse();
        reason.Should().Contain("Mikee");
    }
}
