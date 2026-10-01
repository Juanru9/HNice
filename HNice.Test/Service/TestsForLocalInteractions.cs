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

    // Your client's room directory request, then the server's ROOM_READY (both captured).
    private static LocalInteractions InRoom(string directory, string roomReady, bool autoRejoin = true)
    {
        var local = new LocalInteractions { AutoRejoin = autoRejoin };
        local.HandleOutbound(P(directory));
        local.RewriteInbound(P(roomReady));
        return local;
    }

    [Theory]
    [InlineData("@BH`nGDH", "AEmodel_b 67512", "D^H`nGDH", 67512)]  // guest room
    [InlineData("@BIQHH", "AEnewbie_lobby 33", "D^IQHH", 33)]        // public room
    [InlineData("@BISPH", "AEtheater 67", "D^ISPH", 67)]             // public room
    public void ShouldSendYouBackToTheRoomWhenKicked(string directory, string roomReady, string forward, int roomId)
    {
        var local = InRoom(directory, roomReady);
        var rejoined = new List<int>();
        local.Rejoining += rejoined.Add;

        // Captured kick in room 67708: walked to the door, then LOGOUT and HOTEL_VIEW.
        var inject = new List<(string Packet, TimeSpan Delay)>();
        local.RewriteInbound(P("@]1"), inject);
        local.RewriteInbound(P("@R"), inject);

        inject.Should().ContainSingle().Which.Packet.Should().Be(forward); // ROOMFORWARD
        rejoined.Should().Equal(roomId);
    }

    [Theory]
    [InlineData("@u")]        // QUIT: hotel view button
    [InlineData("@BIQHH")]    // room directory: going to another room
    public void ShouldNotSendYouBackWhenYouLeaveOnYourOwn(string leave)
    {
        var local = InRoom("@BH`nGDH", "AEmodel_b 67512");
        local.HandleOutbound(P(leave));

        var inject = new List<(string Packet, TimeSpan Delay)>();
        local.RewriteInbound(P("@R"), inject);

        inject.Should().BeEmpty();
    }

    [Fact]
    public void ShouldNotSendYouBackWhenOff()
    {
        var local = InRoom("@BH`nGDH", "AEmodel_b 67512", autoRejoin: false);
        var inject = new List<(string Packet, TimeSpan Delay)>();
        local.RewriteInbound(P("@R"), inject);
        inject.Should().BeEmpty();
    }

    [Fact]
    public void ShouldReportADrinkTheServerPutInYourHand()
    {
        var local = new LocalInteractions();
        var given = new List<string>();
        local.ServerGaveDrink += given.Add;

        // Learn which avatar is ours (slot 2), then replay the server's answer to CARRYDRINK 19 in a public room.
        local.HandleOutbound(P("AO4 7"));
        local.RewriteInbound(P("@bIJSAQB0.0[2]PAPA/[2]"));
        local.RewriteInbound(P("@bIJSAQB0.0[2]PAPA/drink 19/[2]"));
        local.RewriteInbound(P("@bIJSAQB0.0[2]PAPA/carryd 19/[2]"));

        // Someone else's drink is not ours.
        local.RewriteInbound(P("@bJHPBJ0.0[2]PAPA/carryd 5/[2]JSAQB0.0[2]PAPA/[2]"));

        given.Should().Equal("19", "19");
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
