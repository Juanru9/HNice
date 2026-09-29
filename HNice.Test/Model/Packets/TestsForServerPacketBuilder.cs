using FluentAssertions;
using HNice.Model.Packets;

namespace HNice.Test.Model.Packets;

public class TestsForServerPacketBuilder
{
    [Fact]
    public void WardrobeSaveShouldMatchTheCapturedPacket()
    {
        // Captured when saving the wardrobe: → UPDATE @l@D@chd-205-1009.ch-215-1189.lg-285-1281@JH@AH@R@@
        ServerPacketBuilder.WearFigure("hd-205-1009.ch-215-1189.lg-285-1281")
            .Should().Be("@l@D@chd-205-1009.ch-215-1189.lg-285-1281@JH@AH@R@@");
    }
}
