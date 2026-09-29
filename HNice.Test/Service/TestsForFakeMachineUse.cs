using FluentAssertions;
using HNice.Service;
using System.Text;

namespace HNice.Test.Service;

/// <summary>
/// Replays the captured case: Cola machine on 6,7 facing 2 (front tile 7,7), you are slot 6 (RA).
/// The client only sent "walk to 7,7" (1269 SuSASA) and never the use request.
/// </summary>
public class TestsForFakeMachineUse
{
    private static byte[] P(string text) => Encoding.Latin1.GetBytes(text.Replace("[2]", "\u0002"));
    private static string S(byte[] bytes) => Encoding.Latin1.GetString(bytes).Replace("\u0002", "[2]");

    private static LocalInteractions RoomWithMachine()
    {
        var local = new LocalInteractions();
        local.SetMe(23976, "Bateman");
        local.RewriteInbound(P("@\\IRA`j]ABateman[2]hd-180-1001.ch-210-1189.lg-270-1189[2]m[2][2]RASA0.0[2][2][2]Istd[2]std[2]PAHHH"));
        local.RegisterFakeMachine("999000002", 6, 7, 2, "19");
        return local;
    }

    [Fact]
    public void ClickingWhileStandingInFrontShouldHandOutTheDrink()
    {
        var local = RoomWithMachine();
        local.RewriteInbound(P("@bIRASASA0.0[2]PAPA/[2]"));          // you stand on 7,7

        var decision = local.HandleOutbound(P("SuSASA"));             // click: "walk to 7,7"

        decision.InjectNow.Should().Contain("AX999000002\u0002TRUE\u0002");
        decision.InjectNow.Should().Contain(p => p.StartsWith("@b") && p.Contains("carryd 19/"));
        decision.InjectLater.Should().ContainSingle().Which.Packet.Should().Be("AX999000002\u0002FALSE\u0002");
    }

    [Fact]
    public void ClickingFromAfarShouldHandOutTheDrinkOnArrival()
    {
        var local = RoomWithMachine();
        local.RewriteInbound(P("@bIRARASA0.0[2]JJ/[2]"));             // you stand on the machine tile 6,7

        local.HandleOutbound(P("SuSASA")).InjectNow.Should().BeEmpty(); // the client walks you to 7,7 first

        var replies = new List<(string Packet, TimeSpan Delay)>();
        S(local.RewriteInbound(P("@bIRARASA0.0[2]JJ/mv 7,7,0.0/[2]"), replies)).Should().NotContain("carryd");
        replies.Should().BeEmpty();                                     // still walking

        var arrived = S(local.RewriteInbound(P("@bIRASASA0.0[2]JJ/[2]"), replies));
        arrived.Should().Contain("carryd 19/");
        replies.Select(r => r.Packet).Should().Equal("AX999000002\u0002TRUE\u0002", "AX999000002\u0002FALSE\u0002");
    }

    [Fact]
    public void WalkingElsewhereShouldNotTouchAnything()
    {
        var local = RoomWithMachine();
        local.RewriteInbound(P("@bIRASASA0.0[2]PAPA/[2]"));

        local.HandleOutbound(P("SuKK")).InjectNow.Should().BeEmpty();  // walk to 3,3
    }
}
