using FluentAssertions;
using HNice.Model;

namespace HNice.Test.Model;

public class TestsForHabboPlayer
{
    [Fact]
    public void ShouldParseTheCurrentUserObjectFormat()
    {
        // USER_OBJ body as sent by the Origins server (header "@E" removed).
        var body = "`j]ABateman\u0002hd-180-1001.ch-210-1189.lg-270-1189\u0002M\u0002\u0002H\u0002HHIIIIIIHHI";

        var player = new HabboPlayer(body);

        player.UserId.Should().Be(23976);
        player.HabboName.Should().Be("Bateman");
        player.HabboFigure.Should().Be("hd-180-1001.ch-210-1189.lg-270-1189");
        player.HabboSex.Should().Be("M");
        player.HabboMission.Should().BeEmpty();
        player.PhTickets.Should().Be(0);
        player.PhFigure.Should().BeEmpty();
        player.PhotoFilm.Should().Be(0);
        player.DirectMail.Should().Be(0);
    }

    [Theory]
    [InlineData("hUuoG", 8123732)]
    [InlineData("RB", 10)]
    [InlineData("SA", 7)]
    [InlineData("M", -1)]
    [InlineData("H", 0)]
    [InlineData("I", 1)]
    public void ShouldReadVL64Integers(string encoded, int expected)
    {
        new HNice.Model.Packets.IncomingPacketReader(encoded).ReadInt().Should().Be(expected);
    }

    [Fact]
    public void ShouldReadMixedFields()
    {
        var reader = new HNice.Model.Packets.IncomingPacketReader("RBhello\u0002\u0002I");

        reader.ReadInt().Should().Be(10);
        reader.ReadString().Should().Be("hello");
        reader.ReadString().Should().BeEmpty();
        reader.ReadBool().Should().BeTrue();
        reader.HasMore.Should().BeFalse();
    }

    [Fact]
    public void ShouldStillParseTheKeyValueFormat()
    {
        var player = new HabboPlayer("name=Bateman\rfigure=hd-180-1001\rsex=M\r");

        player.HabboName.Should().Be("Bateman");
        player.HabboFigure.Should().Be("hd-180-1001");
        player.HabboSex.Should().Be("M");
    }
}
