using HNice.Util.Extensions;
using System.IO;

namespace HNice.Model.Encryption.ShockWaveBuffer;

/// <summary>Decrypts the 6 byte header of an encrypted chunk into its 4 plain bytes.</summary>
public delegate byte[] HeaderDecipher(ReadOnlySpan<byte> encryptedHeader);

/// <summary>
/// Length prefixed stream, used by the client to server traffic and by all encrypted traffic.
/// Plain:     [length: 3 bytes Habbo B64][payload]
/// Encrypted: [header: 6 bytes Base64 of 4 encrypted bytes (random, length as 3 bytes Habbo B64)][payload]
/// The returned chunks contain only the payload.
/// </summary>
public sealed class ShockwaveProperBuffer : PayloadBuffer
{
    public const int PACKET_HEADER_SIZE = 2;
    public const int PACKET_LENGTH_SIZE_ENCRYPTED = 6;
    public const int PACKET_LENGTH_SIZE = 3;
    public const int PACKET_SIZE_MIN = PACKET_HEADER_SIZE + PACKET_LENGTH_SIZE;
    public const int PACKET_SIZE_MIN_ENCRYPTED = PACKET_HEADER_SIZE + PACKET_LENGTH_SIZE_ENCRYPTED;

    private HeaderDecipher? _cipher;

    // The header cipher consumes a nonce per call, so a header must be decrypted exactly once even when
    // its payload has not fully arrived yet.
    private int _pendingLength = -1;

    public void SetCipher(HeaderDecipher cipher)
    {
        _cipher = cipher;
    }

    public byte[][] Receive()
    {
        var lengthSize = _cipher is not null ? PACKET_LENGTH_SIZE_ENCRYPTED : PACKET_LENGTH_SIZE;
        var minPacketSize = _cipher is not null ? PACKET_SIZE_MIN_ENCRYPTED : PACKET_SIZE_MIN;

        var chunks = new List<byte[]>();

        while (Count >= minPacketSize || (_pendingLength >= 0 && Count >= lengthSize + _pendingLength))
        {
            int length;

            if (_cipher is null)
            {
                length = Data[..PACKET_LENGTH_SIZE].DecodeB64();
            }
            else if (_pendingLength >= 0)
            {
                length = _pendingLength;
            }
            else
            {
                var header = _cipher(Data[..PACKET_LENGTH_SIZE_ENCRYPTED]);
                if (header.Length < 4)
                {
                    throw new InvalidDataException("Decrypted packet header is shorter than 4 bytes.");
                }
                length = _pendingLength = ((ReadOnlySpan<byte>)header.AsSpan(1, 3)).DecodeB64();
            }

            if (length < 0)
            {
                throw new InvalidDataException("Decoded packet length is negative.");
            }

            if (Count < lengthSize + length)
            {
                break;
            }

            chunks.Add(Data.Slice(lengthSize, length).ToArray());
            Consume(lengthSize + length);
            _pendingLength = -1;
        }

        return chunks.ToArray();
    }
}
