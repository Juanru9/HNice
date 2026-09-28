using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace HNice.Model.Encryption;

/// <summary>
/// Habbo Origins (Shockwave) key exchange: Diffie-Hellman, then HKDF-SHA256 to derive four ChaCha20 keys
/// (data and header, one pair per direction). Port of G-Earth's BobbaCrypto.
/// See https://github.com/G-Realm/G-Earth/tree/master/G-Earth/src/main/java/gearth/app/protocol/crypto
/// </summary>
public sealed class BobbaCrypto
{
    private const int PrivateKeyBytes = 8;

    private static readonly BigInteger G = BigInteger.Parse("23786635532332886537261431906453031264918297", CultureInfo.InvariantCulture);
    private static readonly BigInteger P = BigInteger.Parse("632158881801130885249042417232212770524741295422564233061391190031954228421232913648184592218883487397503624904102572293826728806813079", CultureInfo.InvariantCulture);

    private static readonly byte[] HkdfSalt = Encoding.UTF8.GetBytes("BobbaXtraHKDFSalt");
    private const string HkdfInfoPrefix = "BobbaXtra|";

    private readonly BigInteger _privateKey;

    /// <summary>Our public key, as the decimal string sent inside the handshake packets.</summary>
    public string PublicKey { get; }

    public BobbaChaChaKey? C2sData { get; private set; }
    public BobbaChaChaKey? C2sHeader { get; private set; }
    public BobbaChaChaKey? S2cData { get; private set; }
    public BobbaChaChaKey? S2cHeader { get; private set; }

    public bool HasKeys => C2sData is not null && C2sHeader is not null && S2cData is not null && S2cHeader is not null;

    public BobbaCrypto() : this(GeneratePrivateKey())
    {
    }

    public BobbaCrypto(BigInteger privateKey)
    {
        _privateKey = privateKey;
        PublicKey = BigInteger.ModPow(G, privateKey, P).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Completes the exchange with the other side's public key and derives the session keys.</summary>
    public void SetRemotePublicKey(string remotePublicKey)
    {
        var remoteKey = BigInteger.Parse(remotePublicKey.Trim(), NumberStyles.None, CultureInfo.InvariantCulture);
        var sharedKey = BigInteger.ModPow(remoteKey, _privateKey, P);
        var sharedKeyBytes = sharedKey.ToByteArray(isUnsigned: true, isBigEndian: true);

        C2sData = CreateKey(sharedKeyBytes, "bobba-c2s-data");
        C2sHeader = CreateKey(sharedKeyBytes, "bobba-c2s-header");
        S2cData = CreateKey(sharedKeyBytes, "bobba-s2c-data");
        S2cHeader = CreateKey(sharedKeyBytes, "bobba-s2c-header");
    }

    /// <summary>Encrypts or decrypts one message, consuming the next nonce of <paramref name="key"/>.</summary>
    public static byte[] ApplyChaCha(ReadOnlySpan<byte> data, BobbaChaChaKey key)
    {
        var result = new byte[data.Length];
        ChaCha20.Xor(key.Key, key.GetNextNonce(), 0, data, result);
        return result;
    }

    private static BigInteger GeneratePrivateKey()
    {
        Span<byte> bytes = stackalloc byte[PrivateKeyBytes];
        BigInteger privateKey;
        do
        {
            RandomNumberGenerator.Fill(bytes);
            privateKey = new BigInteger(bytes, isUnsigned: true);
        } while (privateKey.IsZero);
        return privateKey;
    }

    private static BobbaChaChaKey CreateKey(byte[] sharedKey, string type)
    {
        var info = Encoding.UTF8.GetBytes(HkdfInfoPrefix + type);
        var output = HKDF.DeriveKey(HashAlgorithmName.SHA256, sharedKey, ChaCha20.KeySize + ChaCha20.NonceSize, HkdfSalt, info);

        return new BobbaChaChaKey(output[..ChaCha20.KeySize], output[ChaCha20.KeySize..]);
    }
}
