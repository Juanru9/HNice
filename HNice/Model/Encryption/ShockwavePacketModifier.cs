using HNice.Model.Encryption.ShockWaveBuffer;
using HNice.Model.Packets;
using HNice.Util.Extensions;
using System.Text;

namespace HNice.Model.Encryption;

/// <summary>
/// Man in the middle for the Habbo Origins encryption. Port of G-Earth's ShockwavePacketModifier.
/// See https://github.com/G-Realm/G-Earth/blob/master/G-Earth/src/main/java/gearth/app/protocol/packethandler/shockwave/ShockwavePacketModifier.java
///
/// The client and the server agree on the keys with an unauthenticated Diffie-Hellman exchange:
///   client -> server  GENERATEKEY (202) with the client public key
///   server -> client  SECRET_KEY (1) with the server public key
/// Both public keys are swapped for our own, so we end up with one set of keys shared with the client
/// and another one shared with the server. Every packet is decrypted with one set and re-encrypted with the other.
///
/// Packets returned by this class are plain: header (2 bytes Habbo B64) + body, without length prefix or chr(1) ender.
/// </summary>
public sealed class ShockwavePacketModifier
{
    private const int C2S_GENERATEKEY = (int)OutcomingPacketMessage.GENERATEKEY;
    private const int S2C_SECRETKEY = (int)IncomingPacketMessage.SECRET_KEY;

    private static readonly Encoding PacketEncoding = Encoding.Latin1;

    private readonly object _sync = new();

    private readonly BobbaCrypto _client;
    private readonly BobbaCrypto _server;

    private readonly ShockwaveProperBuffer _clientBuffer = new();
    private readonly ShockwaveProperBuffer _serverBuffer = new();
    private readonly ShockwaveBuffer _serverBufferPlain = new();

    private bool _clientCryptoEnabled;
    private bool _serverCryptoEnabled;
    private long _clientPacketsDecrypted;
    private long _serverPacketsDecrypted;

    public ShockwavePacketModifier() : this(new BobbaCrypto(), new BobbaCrypto())
    {
    }

    /// <param name="client">Our side of the key exchange with the real client.</param>
    /// <param name="server">Our side of the key exchange with the real server.</param>
    public ShockwavePacketModifier(BobbaCrypto client, BobbaCrypto server)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _server = server ?? throw new ArgumentNullException(nameof(server));
    }

    public bool IsClientCryptoEnabled { get { lock (_sync) return _clientCryptoEnabled; } }
    public bool IsServerCryptoEnabled { get { lock (_sync) return _serverCryptoEnabled; } }

    /// <summary>Key exchange state for the Diagnostics panel (fingerprints only, no key material).</summary>
    public CryptoDiagnostics GetDiagnostics()
    {
        lock (_sync)
        {
            return new CryptoDiagnostics(
                ClientKeyReceived: _client.HasKeys,
                ServerKeyReceived: _server.HasKeys,
                ClientCryptoEnabled: _clientCryptoEnabled,
                ServerCryptoEnabled: _serverCryptoEnabled,
                ClientLinkFingerprint: CryptoDiagnostics.Fingerprint(_client),
                ServerLinkFingerprint: CryptoDiagnostics.Fingerprint(_server),
                ClientPacketsDecrypted: _clientPacketsDecrypted,
                ServerPacketsDecrypted: _serverPacketsDecrypted);
        }
    }

    /// <param name="data">Raw data read from the client.</param>
    /// <returns>Plain packets sent by the client.</returns>
    public byte[][] ClientToProxy(ReadOnlySpan<byte> data)
    {
        lock (_sync)
        {
            _clientBuffer.Push(data);
            var packets = _clientBuffer.Receive();

            if (_clientCryptoEnabled)
            {
                _clientPacketsDecrypted += packets.Length;
                return DecryptChunks(packets, _client.C2sData!);
            }

            foreach (var packet in packets)
            {
                if (GetHeader(packet) == C2S_GENERATEKEY)
                {
                    _client.SetRemotePublicKey(ReadOutgoingString(packet));
                }
            }

            return packets;
        }
    }

    /// <param name="packet">Plain packet to send to the server.</param>
    /// <returns>Raw data to write to the server.</returns>
    public byte[] ProxyToServer(ReadOnlySpan<byte> packet)
    {
        lock (_sync)
        {
            if (_serverCryptoEnabled)
            {
                return EncryptChunk(packet, _server.C2sHeader!, _server.C2sData!);
            }

            if (GetHeader(packet) == C2S_GENERATEKEY)
            {
                packet = BuildOutgoingStringPacket(C2S_GENERATEKEY, _server.PublicKey);
            }

            return Concat(packet.Length.EncodeB64Bytes(ShockwaveProperBuffer.PACKET_LENGTH_SIZE), packet);
        }
    }

    /// <param name="data">Raw data read from the server.</param>
    /// <returns>Plain packets sent by the server.</returns>
    public byte[][] ServerToProxy(ReadOnlySpan<byte> data)
    {
        lock (_sync)
        {
            if (_serverCryptoEnabled)
            {
                return DecryptServerData(data);
            }

            _serverBufferPlain.Push(data);

            var packets = new List<byte[]>();
            while (_serverBufferPlain.TryReceive(out var packet))
            {
                packets.Add(packet);

                if (GetHeader(packet) == S2C_SECRETKEY)
                {
                    _server.SetRemotePublicKey(ReadIncomingString(packet));
                    EnableServerCrypto();

                    // Whatever the server sent after the key is already encrypted.
                    var remaining = _serverBufferPlain.TakeAll();
                    packets.AddRange(DecryptServerData(remaining));
                    break;
                }
            }

            return packets.ToArray();
        }
    }

    /// <param name="packet">Plain packet to send to the client.</param>
    /// <returns>Raw data to write to the client.</returns>
    public byte[] ProxyToClient(ReadOnlySpan<byte> packet)
    {
        lock (_sync)
        {
            if (_clientCryptoEnabled)
            {
                return EncryptChunk(Concat(packet, [Constants.PACKET_ENDER_BYTE]), _client.S2cHeader!, _client.S2cData!);
            }

            if (GetHeader(packet) == S2C_SECRETKEY)
            {
                packet = BuildIncomingStringPacket(S2C_SECRETKEY, _client.PublicKey);

                // The key itself goes out in plain text, the client encrypts everything after it.
                EnableClientCrypto();
            }

            return Concat(packet, [Constants.PACKET_ENDER_BYTE]);
        }
    }

    private byte[][] DecryptServerData(ReadOnlySpan<byte> data)
    {
        _serverBuffer.Push(data);

        // A decrypted chunk may hold several chr(1) terminated packets, or only part of one.
        foreach (var chunk in DecryptChunks(_serverBuffer.Receive(), _server.S2cData!))
        {
            _serverBufferPlain.Push(chunk);
        }

        var packets = _serverBufferPlain.Receive();
        _serverPacketsDecrypted += packets.Length;
        return packets;
    }

    private void EnableClientCrypto()
    {
        if (!_client.HasKeys)
        {
            throw new InvalidOperationException("Cannot enable client crypto before the client sent its public key.");
        }

        _clientBuffer.SetCipher(CreateHeaderDecipher(_client.C2sHeader!));
        _clientCryptoEnabled = true;
    }

    private void EnableServerCrypto()
    {
        if (!_server.HasKeys)
        {
            throw new InvalidOperationException("Cannot enable server crypto without the server public key.");
        }

        _serverBuffer.SetCipher(CreateHeaderDecipher(_server.S2cHeader!));
        _serverCryptoEnabled = true;
    }

    private static HeaderDecipher CreateHeaderDecipher(BobbaChaChaKey headerKey) =>
        encryptedHeader => BobbaCrypto.ApplyChaCha(CipherBase64.Decode(encryptedHeader), headerKey);

    internal static byte[] EncryptChunk(ReadOnlySpan<byte> packet, BobbaChaChaKey headerKey, BobbaChaChaKey dataKey, byte? headerPrefix = null)
    {
        var payload = CipherBase64.Encode(BobbaCrypto.ApplyChaCha(packet, dataKey));

        var payloadLength = payload.Length.EncodeB64Bytes(ShockwaveProperBuffer.PACKET_LENGTH_SIZE);
        byte[] header = [headerPrefix ?? (byte)Random.Shared.Next(1, 127), payloadLength[0], payloadLength[1], payloadLength[2]];
        header = CipherBase64.Encode(BobbaCrypto.ApplyChaCha(header, headerKey));

        return Concat(header, payload);
    }

    internal static byte[][] DecryptChunks(byte[][] chunks, BobbaChaChaKey dataKey)
    {
        var packets = new byte[chunks.Length][];
        for (var i = 0; i < chunks.Length; i++)
        {
            packets[i] = BobbaCrypto.ApplyChaCha(CipherBase64.Decode(chunks[i]), dataKey);
        }
        return packets;
    }

    internal static int GetHeader(ReadOnlySpan<byte> packet) =>
        packet.Length < ShockwaveProperBuffer.PACKET_HEADER_SIZE ? -1 : packet[..ShockwaveProperBuffer.PACKET_HEADER_SIZE].DecodeB64();

    // Client strings: [length: 2 bytes Habbo B64][bytes]
    private static string ReadOutgoingString(ReadOnlySpan<byte> packet)
    {
        var body = packet[ShockwaveProperBuffer.PACKET_HEADER_SIZE..];
        var length = body[..2].DecodeB64();
        return PacketEncoding.GetString(body.Slice(2, length));
    }

    // Server strings: [bytes] terminated by chr(2)
    private static string ReadIncomingString(ReadOnlySpan<byte> packet)
    {
        var body = packet[ShockwaveProperBuffer.PACKET_HEADER_SIZE..];
        var end = body.IndexOf(Constants.PACKET_SPLITTER_BYTE);
        return PacketEncoding.GetString(end >= 0 ? body[..end] : body);
    }

    private static byte[] BuildOutgoingStringPacket(int header, string value)
    {
        var bytes = PacketEncoding.GetBytes(value);
        return Concat(Concat(header.EncodeB64Bytes(), bytes.Length.EncodeB64Bytes()), bytes);
    }

    private static byte[] BuildIncomingStringPacket(int header, string value) =>
        Concat(Concat(header.EncodeB64Bytes(), PacketEncoding.GetBytes(value)), [Constants.PACKET_SPLITTER_BYTE]);

    private static byte[] Concat(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        var result = new byte[first.Length + second.Length];
        first.CopyTo(result);
        second.CopyTo(result.AsSpan(first.Length));
        return result;
    }
}
