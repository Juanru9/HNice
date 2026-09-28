using FluentAssertions;
using HNice.Model.Packets;

namespace HNice.Test.Model.Packets;

public class TestsForClientPacketBuilder
{
    [Fact]
    public void BroadcastShouldUseTheSystemBroadcastHeader()
    {
        // SYSTEM_BROADCAST = 139, encoded as 2 chars of Habbo B64.
        var packet = ClientPacketBuilder.Broadcast("hi");

        packet.Should().StartWith("BK");
        packet.Should().Be("BKhi");
    }

    [Fact]
    public void FuseShouldPrefixPermissionsAndUseTheRightsHeader()
    {
        var packet = ClientPacketBuilder.FuseRights(new[] { "use_client_admin", "fuse_room_mute" });

        // RIGHTS = 2 -> "@B". Missing "fuse_" prefixes are added; existing ones are kept.
        packet.Should().StartWith("@B");
        packet.Should().Contain("fuse_use_client_admin");
        packet.Should().Contain("fuse_room_mute");
        packet.Should().NotContain("fuse_fuse_");
    }

    [Fact]
    public void UserObjectShouldRoundTripThroughTheReader()
    {
        var packet = ClientPacketBuilder.UserObject(23976, "Bateman", "hd-180-1", "M", "motto");

        packet.Should().StartWith("@E");
        var player = new HNice.Model.HabboPlayer(packet.Substring(2));
        player.UserId.Should().Be(23976);
        player.HabboName.Should().Be("Bateman");
        player.HabboFigure.Should().Be("hd-180-1");
        player.HabboSex.Should().Be("M");
        player.HabboMission.Should().Be("motto");
    }
}
