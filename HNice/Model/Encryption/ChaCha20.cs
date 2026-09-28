using System.Buffers.Binary;
using System.Numerics;

namespace HNice.Model.Encryption;

/// <summary>
/// ChaCha20 stream cipher as specified in RFC 7539 (96-bit nonce, 32-bit block counter).
/// Equivalent to BouncyCastle's ChaCha7539Engine used by G-Earth.
/// </summary>
public static class ChaCha20
{
    public const int KeySize = 32;
    public const int NonceSize = 12;
    private const int BlockSize = 64;

    /// <summary>
    /// XORs <paramref name="input"/> with the key stream into <paramref name="output"/>. Encryption and decryption are the same operation.
    /// </summary>
    public static void Xor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce, uint counter, ReadOnlySpan<byte> input, Span<byte> output)
    {
        if (key.Length != KeySize) throw new ArgumentException($"Key must be {KeySize} bytes long.", nameof(key));
        if (nonce.Length != NonceSize) throw new ArgumentException($"Nonce must be {NonceSize} bytes long.", nameof(nonce));
        if (output.Length < input.Length) throw new ArgumentException("Output is smaller than input.", nameof(output));

        Span<uint> state = stackalloc uint[16];
        state[0] = 0x61707865;
        state[1] = 0x3320646e;
        state[2] = 0x79622d32;
        state[3] = 0x6b206574;
        for (var i = 0; i < 8; i++)
        {
            state[4 + i] = BinaryPrimitives.ReadUInt32LittleEndian(key.Slice(i * 4));
        }
        state[12] = counter;
        for (var i = 0; i < 3; i++)
        {
            state[13 + i] = BinaryPrimitives.ReadUInt32LittleEndian(nonce.Slice(i * 4));
        }

        Span<uint> working = stackalloc uint[16];
        Span<byte> keyStream = stackalloc byte[BlockSize];

        for (var offset = 0; offset < input.Length; offset += BlockSize)
        {
            state.CopyTo(working);
            for (var round = 0; round < 10; round++)
            {
                QuarterRound(working, 0, 4, 8, 12);
                QuarterRound(working, 1, 5, 9, 13);
                QuarterRound(working, 2, 6, 10, 14);
                QuarterRound(working, 3, 7, 11, 15);
                QuarterRound(working, 0, 5, 10, 15);
                QuarterRound(working, 1, 6, 11, 12);
                QuarterRound(working, 2, 7, 8, 13);
                QuarterRound(working, 3, 4, 9, 14);
            }
            for (var i = 0; i < 16; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(keyStream.Slice(i * 4), working[i] + state[i]);
            }

            var blockLength = Math.Min(BlockSize, input.Length - offset);
            for (var i = 0; i < blockLength; i++)
            {
                output[offset + i] = (byte)(input[offset + i] ^ keyStream[i]);
            }

            state[12]++;
        }
    }

    private static void QuarterRound(Span<uint> x, int a, int b, int c, int d)
    {
        x[a] += x[b]; x[d] = BitOperations.RotateLeft(x[d] ^ x[a], 16);
        x[c] += x[d]; x[b] = BitOperations.RotateLeft(x[b] ^ x[c], 12);
        x[a] += x[b]; x[d] = BitOperations.RotateLeft(x[d] ^ x[a], 8);
        x[c] += x[d]; x[b] = BitOperations.RotateLeft(x[b] ^ x[c], 7);
    }
}
