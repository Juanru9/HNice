using System.Buffers.Binary;

namespace HNice.Model.Encryption;

/// <summary>
/// A ChaCha20 key with its base nonce. Every message uses a fresh nonce: the base nonce with
/// the message counter added to bytes 4..11 (little endian).
/// </summary>
public sealed class BobbaChaChaKey
{
    private long _counter;

    public byte[] Key { get; }
    public byte[] Nonce { get; }

    public BobbaChaChaKey(byte[] key, byte[] nonce)
    {
        if (key.Length != ChaCha20.KeySize) throw new ArgumentException($"Key must be {ChaCha20.KeySize} bytes long.", nameof(key));
        if (nonce.Length != ChaCha20.NonceSize) throw new ArgumentException($"Nonce must be {ChaCha20.NonceSize} bytes long.", nameof(nonce));

        Key = key;
        Nonce = nonce;
    }

    public byte[] GetNextNonce()
    {
        var current = Interlocked.Increment(ref _counter) - 1;
        var next = (byte[])Nonce.Clone();
        var counterBytes = next.AsSpan(4, 8);

        var baseValue = BinaryPrimitives.ReadInt64LittleEndian(counterBytes);
        BinaryPrimitives.WriteInt64LittleEndian(counterBytes, unchecked(baseValue + current));

        return next;
    }
}
