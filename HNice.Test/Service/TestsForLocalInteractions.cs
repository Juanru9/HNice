using FluentAssertions;
using HNice.Model.Packets;
using HNice.Service;
using System.Text;

namespace HNice.Test.Service;

/// <summary>Replays the packets captured from a real minibar and a fake Habbo Cola machine.</summary>
public class TestsForLocalInteractions
{
    private static byte[] P(string text) => Encoding.Latin1.GetBytes(text.Replace("[2]", "\u0002"));
    private static string S(byte[] bytes) => Encoding.Latin1.GetString(bytes).Replace("\u0002", "[2]");

    [Fact]
    public void ShouldParseAndRebuildTheCapturedStatus()
    {
        const string captured = "IHQASA0.0\u0002RARA/flatctrl useradmin/drink 1/\u0002";

        RoomStatus.TryParse(captured, out var entries).Should().BeTrue();
        entries.Should().ContainSingle();
        entries[0].Should().Be(new StatusEntry(0, 5, 7, "0.0", 6, 6, "/flatctrl useradmin/drink 1/"));

        RoomStatus.Build(entries).Should().Be("@b" + captured);
    }

    [Fact]
    public void ShouldAnswerAFakeDrinkMachineLocally()
    {
        var local = new LocalInteractions();
        local.RegisterFakeItem("999000002");

        // Learn which avatar is ours: we turned, and the server answered with our status.
        local.HandleOutbound(P("AO4 7")).Drop.Should().BeFalse();
        local.RewriteInbound(P("@bIHQASA0.0[2]RARA/flatctrl useradmin/[2]"));

        // Click on the fake machine: kept from the server, animation shown locally.
        var click = local.HandleOutbound(P("AJ@I999000002@DTRUE"));
        click.Drop.Should().BeTrue();
        click.InjectNow.Should().ContainSingle().Which.Should().Be("AX999000002\u0002TRUE\u0002");

        // Drink request: kept from the server, the drink appears in our hand, the machine animation ends later.
        var drink = local.HandleOutbound(P("AP@B19"));
        drink.Drop.Should().BeTrue();
        drink.InjectNow.Should().ContainSingle().Which.Should().Contain("carryd 19/").And.StartWith("@bI");
        drink.InjectLater.Should().ContainSingle().Which.Packet.Should().Be("AX999000002\u0002FALSE\u0002");

        // Later server statuses about us keep the drink (replacing the leftover real one).
        S(local.RewriteInbound(P("@bIHQASA0.0[2]JJ/flatctrl useradmin/mv 6,7,0.45/carryd 1/[2]")))
            .Should().Be("@bIHQASA0.0[2]JJ/flatctrl useradmin/mv 6,7,0.45/carryd 19/[2]");
    }

    [Fact]
    public void ShouldLeaveRealMachinesToTheServer()
    {
        var local = new LocalInteractions();
        local.RegisterFakeItem("999000002");

        local.HandleOutbound(P("AJ@J2147418128@DTRUE")).Drop.Should().BeFalse();
        local.HandleOutbound(P("AP@A3")).Drop.Should().BeFalse();

        var status = P("@bIHSBRA0.0[2]HH/flatctrl useradmin/carryd 1/[2]");
        local.RewriteInbound(status).Should().BeSameAs(status);
    }
}
