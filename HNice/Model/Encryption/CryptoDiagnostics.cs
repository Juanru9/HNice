using System.Security.Cryptography;

namespace HNice.Model.Encryption;

/// <summary>
/// Read-only snapshot of the key exchange, for the Diagnostics panel.
/// Keys are never exposed: each link only carries a short fingerprint (first bytes of a SHA-256 over
/// its derived keys), enough to see that keys were found and that they change every session.
/// </summary>
public sealed record CryptoDiagnostics(
    bool ClientKeyReceived,
    bool ServerKeyReceived,
    bool ClientCryptoEnabled,
    bool ServerCryptoEnabled,
    string? ClientLinkFingerprint,
    string? ServerLinkFingerprint,
    long ClientPacketsDecrypted,
    long ServerPacketsDecrypted)
{
    internal static string? Fingerprint(BobbaCrypto crypto)
    {
        if (!crypto.HasKeys) return null;

        var material = crypto.C2sData!.Key
            .Concat(crypto.C2sHeader!.Key)
            .Concat(crypto.S2cData!.Key)
            .Concat(crypto.S2cHeader!.Key)
            .ToArray();
        var hash = SHA256.HashData(material);
        return string.Join(':', hash.Take(6).Select(b => b.ToString("x2")));
    }
}
