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

    [Theory]
    [InlineData(19, "AP@B19")] // captured, accepted in a public room
    [InlineData(1, "AP@A1")]
    public void CarryDrinkShouldMatchTheCapturedPacket(int drink, string expected)
    {
        ServerPacketBuilder.CarryDrink(drink).Should().Be(expected);
    }

    [Fact]
    public void WobbleMoveShouldMatchTheCapturedPacket()
    {
        ServerPacketBuilder.WobbleMove('A').Should().Be("ArA");
    }
}
