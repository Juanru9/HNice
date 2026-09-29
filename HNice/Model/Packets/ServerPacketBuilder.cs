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
    public static string WearFigure(string figure) =>
        ((int)OutcomingPacketMessage.UPDATE).EncodeB64() +
        FigureField.EncodeB64() + figure.Length.EncodeB64() + figure +
        WardrobeSaveTail;
}
