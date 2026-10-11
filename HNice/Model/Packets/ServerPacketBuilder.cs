using HNice.Util.Extensions;

namespace HNice.Model.Packets;

/// <summary>
/// Builds real client-to-server requests, exactly as the game client sends them.
/// The server validates each one like any normal request from the client.
/// </summary>
public static class ServerPacketBuilder
{
    /// <summary>
    /// The field list your client sent after the figure when saving the wardrobe (captured live):
    /// @J H, @A H, @R @@ (settings flags and an empty field). Kept byte-exact.
    /// </summary>
    private const string WardrobeSaveTail = "@JH@AH@R@@";

    private const int FigureField = 4;

    /// <summary>
    /// UPDATE (@l): the wardrobe "save" request. Captured live:
    ///   @l @D @c hd-205-1009.ch-215-1189.lg-285-1281 @J H @A H @R @@
    /// i.e. field 4 = figure as a B64-length string, then the tail above. Gender and motto are not part of it.
    /// </summary>
    /// <summary>
    /// CARRYDRINK (AP): asks for a drink by its hand item id. Captured live: AP @B 19 (drink "19" as a B64-length string).
    /// Captured: public rooms hand it over wherever you stand; the guest rooms tried ignored it.
    /// </summary>
    public static string CarryDrink(int drinkId)
    {
        var id = drinkId.ToString();
        return ((int)OutcomingPacketMessage.CARRYDRINK).EncodeB64() + id.Length.EncodeB64() + id;
    }

    /// <summary>
    /// PTM (Ar): your Wobble Squabble move, one plain letter. Captured live in md_a: ArA (lean left).
    /// Letters (effects measured live, see FINDINGS.md): A lean left (balance down), D lean right (balance up),
    /// W hit pushing the other player right (up), E hit pushing them left (down), X step down the plank (position - 1),
    /// S step up (position + 1), 0 rebalance.
    /// The server works out balance, hits and the winner.
    /// </summary>
    public static string WobbleMove(char move) => ((int)OutcomingPacketMessage.PTM).EncodeB64() + move;

    public static string WearFigure(string figure) =>
        ((int)OutcomingPacketMessage.UPDATE).EncodeB64() +
        FigureField.EncodeB64() + figure.Length.EncodeB64() + figure +
        WardrobeSaveTail;
}
