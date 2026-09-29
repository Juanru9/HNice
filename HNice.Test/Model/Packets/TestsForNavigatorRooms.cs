using FluentAssertions;
using HNice.Model.Packets;

namespace HNice.Test.Model.Packets;

public class TestsForNavigatorRooms
{
    // ← 351 captured live (three rooms, [2] = chr 2): "02. Mi rincón de pesca" (flat 67533) has 2 of 25 users.
    private const string RoomsList = "K" + "asGD" + "02. Mi rincÃ³n de pesca\u0002JouJouJou\u0002OPEN\u0002" + "JQF" + "Si estoy aquÃ­\u0002"
        + "[]r" + "00. Rinconcito especial de Venus\u0002.:Venus82:.\u0002OPEN\u0002" + "KQF" + "\u0002";

    [Fact]
    public void ShouldAddUsersToTheCurrentRoomOnly()
    {
        NavigatorRooms.TryAddUsers(RoomsList, 67533, 5, out var rewritten).Should().BeTrue();

        // 2 + 5 = 7 (VL64 "SA"), max stays 25 ("QF"); Venus keeps 3 ("K").
        rewritten.Should().Contain("OPEN\u0002" + "SAQF" + "Si estoy");
        rewritten.Should().Contain("OPEN\u0002" + "KQF" + "\u0002");
    }

    [Fact]
    public void ShouldLeaveOtherListsAlone()
    {
        NavigatorRooms.TryAddUsers(RoomsList, 12345, 5, out var same).Should().BeFalse();
        same.Should().Be(RoomsList);
        NavigatorRooms.TryAddUsers(RoomsList, 67533, 0, out _).Should().BeFalse();
    }
}
