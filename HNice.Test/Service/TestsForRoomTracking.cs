using FluentAssertions;
using HNice.Model.Packets;
using HNice.Service;
using HNice.Util;
using System.Text;

namespace HNice.Test.Service;

/// <summary>Replays the room captured live: 7 people in trackgamer's room, then clicking keeera.</summary>
public class TestsForRoomTracking
{
    private static byte[] P(string text) => Encoding.Latin1.GetBytes(text.Replace("[2]", "\u0002"));

    private const string UsersPacket =
        "@\\SAH`ROE11trackgamer11[2]hr-165-1113.hd-208-1014.ch-260-1248.lg-285-1231.sh-300-1298[2]m[2][2]RAPB0.45[2][2][2]Istd[2]std[2]PAHHH" +
        "IaROEDaniMz[2]hr-170-1150.hd-190-1022.ch-250-1314.lg-270-1314.sh-295-1314.ea-1401-1314[2]m[2][2]QBQB0.0[2][2][2]Istd[2]std[2]PAHHH" +
        "KcaJEJouJouJou[2]hr-155-1053.hd-180-1014.ch-250-1208.lg-285-1281.sh-906-1308.ea-1401-1189[2]m[2]Pues eso[2]SBPB0.0[2][2]OBD01[2]Istd[2]crr.6[2]PAHSFH" +
        "QAXI|.Jorge[2]hr-831-1051.hd-205-1009.ch-886-1223.lg-275-1208.sh-305-1314.ha-1013-1194.fa-1201-1.ca-1811-1[2]m[2]Dudas[2]QBRB0.0[2][2]HBA[2]Istd[2]std[2]PAPARUH" +
        "RAb\\GDLeluan[2]hr-833-1061.hd-600-1020.ch-885-1257-92.lg-715-1197.sh-725-1206[2]f[2]Convertir a dos en tres.[2]RAQB0.0[2][2]FSH[2]Istd[2]std[2]PAPFQPH" +
        "QB`aQDkeeera[2]hr-836-1082.hd-626-1022.ch-665-1231.lg-715-1189.sh-905-1314.ha-1026-1.he-1609-1189.ea-1401-62.ca-1803-1189[2]f[2]CafÃ© y cigarro, habbo de barro.[2]PBSA0.0[2][2]BLN[2]Istd[2]crr.73[2]PAPFQOH" +
        "RBYCmRichik13[2]hd-195-1021.ch-255-1314.lg-281-1301.sh-295-1314.ha-1003-1301.ca-1802-1[2]m[2][2]QAQA0.5[2][2]X24[2]Istd[2]std[2]PAHHH";

    private const string MyUsersPacket =
        "@\\IPC`j]ABateman[2]hd-180-1001.ch-210-1189.lg-270-1189[2]m[2][2]HQA0.0[2][2][2]Istd[2]std[2]PAHHH";

    [Fact]
    public void ShouldParseTheCapturedUserList()
    {
        RoomUsers.TryParse(UsersPacket[2..].Replace("[2]", "\u0002"), out var users).Should().BeTrue();

        users.Select(u => u.Name).Should().Equal("11trackgamer11", "DaniMz", "JouJouJou", ".Jorge", "Leluan", "keeera", "Richik13");
        var keeera = users.Single(u => u.Name == "keeera");
        keeera.Index.Should().Be(9);
        keeera.Gender.Should().Be("F");
        keeera.Badge.Should().Be("BLN");
        (keeera.X, keeera.Y).Should().Be((8, 7));
        keeera.DisplayMotto.Should().Be("Café y cigarro, habbo de barro.");
    }

    [Fact]
    public void ShouldParseAUserWithACompanion()
    {
        // Captured when Sance entered with a Sporeling pet: the last flag is 1 and three extra fields follow.
        const string packet = "@\\IQCbOMASance[2]hr-828-1035.hd-180-1005.ch-215-1200.lg-275-1314.sh-305-1314.ca-1814-1[2]m[2]omg[2]IQA2.6[2][2]EXE[2]Istd[2]std[2]PAHHImushroom_sporeling[2][color=#78A84B]Sporeling[/color][2]I [2]";

        RoomUsers.TryParse(packet[2..].Replace("[2]", "\u0002"), out var users).Should().BeTrue();
        users.Should().ContainSingle().Which.Should().Match<RoomUser>(u => u.Name == "Sance" && u.Index == 13 && u.Motto == "omg");

        var local = new LocalInteractions();
        var changed = false;
        local.UsersChanged += () => changed = true;
        local.RewriteInbound(P(packet));
        changed.Should().BeTrue();
        local.OtherUsers.Should().ContainSingle(u => u.Name == "Sance");
    }

    [Fact]
    public void ShouldPickTheHabboYouClicked()
    {
        var local = new LocalInteractions();
        RoomUser? picked = null;
        local.UserPicked += u => picked = u;

        local.RewriteInbound(P("AEmodel_b 67624"));      // ROOM_READY
        local.RewriteInbound(P(UsersPacket));
        local.RewriteInbound(P(MyUsersPacket));
        local.SetMe(23976, "Bateman");

        // Clicking keeera (standing on 8,7) makes the client turn toward her tile.
        local.HandleOutbound(P("AO8 7")).Drop.Should().BeFalse();

        picked.Should().NotBeNull();
        picked!.Name.Should().Be("keeera");
        local.OtherUsers.Should().HaveCount(7).And.NotContain(u => u.Name == "Bateman");
    }

    [Fact]
    public void ShouldFollowMovesLeavesAndLookChanges()
    {
        var local = new LocalInteractions();
        local.RewriteInbound(P(UsersPacket));

        // keeera (index 9 = QB) walks to 3,3; Leluan (index 6 = RA) changes clothes; DaniMz (index 1) leaves.
        local.RewriteInbound(P("@bIQBKK0.0[2]PAPA/[2]"));
        local.RewriteInbound(P("DJRAhr-115-1070.hd-209-1007[2]m[2]New motto[2]"));
        local.RewriteInbound(P("@]1"));

        var users = local.OtherUsers;
        users.Should().NotContain(u => u.Name == "DaniMz");
        users.Single(u => u.Name == "keeera").Should().Match<RoomUser>(u => u.X == 3 && u.Y == 3);
        users.Single(u => u.Name == "Leluan").Figure.Should().Be("hr-115-1070.hd-209-1007");
    }

    [Fact]
    public void ShouldReadRoomsWithServerBotsAndPets()
    {
        // Captured in the park (Fishing Derby): Bob is a server bot, user type 3 ("K"), and his entry ends there.
        var withBob = "K" + "HPZBobsd=001&sh=003/30,30,30&hd=001/190,140,95nullFishing DerbyQGPD0.0K"
            + "QAbgBAmarinel4hr-890-1083.hd-629-1022fPERD0.0IstdstdPAHHH"
            + "I`j]ABatemanhd-180-1021mKQA0.0IstdstdPAHHH";
        RoomUsers.TryParse(withBob, out var users).Should().BeTrue();
        users.Select(u => (u.Name, u.Kind)).Should().Equal(
            ("Bob", RoomUserKind.Bot), ("marinel4", RoomUserKind.Player), ("Bateman", RoomUserKind.Player));
        users[0].ListName.Should().Be("Bob (bot)");

        // Captured: a pet is user type 2 ("J"), figure "species race colour".
        RoomUsers.TryParse("IHZVq9qypanchito0 14 F59500nullSBQB0.1J", out var pets).Should().BeTrue();
        pets.Should().ContainSingle().Which.Kind.Should().Be(RoomUserKind.Pet);
    }

    [Fact]
    public void ShouldBuildASpawnedBotLikeBob()
    {
        var bot = new RoomUser(100, 999000100, "Bob 2", "hd-180-1", "M", "hi", "", 4, 12, RoomUserKind.Bot);

        var packet = RoomUsers.BuildSingle(bot);

        packet.Should().EndWith("" + "K", "a bot entry ends with its type");
        RoomUsers.TryParse(packet[2..], out var parsed).Should().BeTrue();
        parsed.Should().ContainSingle().Which.Should().Be(bot with { Sex = "m" });
    }

    [Fact]
    public void ShouldBuildASpawnedUserInTheLiveLayout()
    {
        var bot = new RoomUser(100, 999000100, "Bot", "hd-180-1", "M", "hi", "", 4, 12);

        var packet = RoomUsers.BuildSingle(bot);

        packet.Should().StartWith("@\\");
        RoomUsers.TryParse(packet[2..], out var parsed).Should().BeTrue();
        parsed.Should().ContainSingle().Which.Should().Be(bot with { Sex = "m" });
    }

    [Fact]
    public void ShouldBuildTheLookChangeLikeTheServer()
    {
        // Captured: DJRAhr-115-1070.hd-209-1007.ch-230-1293.lg-275-1266.sh-295-1310[2]m[2]Convertir a dos en tres.[2]
        ClientPacketBuilder.UserLook(6, "hr-115-1070.hd-209-1007.ch-230-1293.lg-275-1266.sh-295-1310", "M", "Convertir a dos en tres.")
            .Should().Be("DJRAhr-115-1070.hd-209-1007.ch-230-1293.lg-275-1266.sh-295-1310\u0002m\u0002Convertir a dos en tres.\u0002");
    }

    [Fact]
    public void ShouldFindMyRoomSlotFromTheUserList()
    {
        var local = new LocalInteractions();
        local.SetMe(23976, "Bateman");

        // From the trace: in trackgamer's room you are index QD = 17.
        local.RewriteInbound(P("@\\IQD`j]ABateman[2]hd-180-1001.ch-210-1189.lg-270-1189[2]m[2][2]HQA0.0[2][2][2]Istd[2]std[2]PAHHH"));

        local.MyRoomIndex.Should().Be(17);
    }

    [Fact]
    public void ShouldRoundTripUtf8Text()
    {
        var wire = PacketText.ToWire("Café");
        wire.Should().Be("CafÃ©");
        PacketText.FromWire(wire).Should().Be("Café");
        PacketText.FromWire("ÿþ").Should().Be("ÿþ"); // not UTF-8: left alone
    }
}
