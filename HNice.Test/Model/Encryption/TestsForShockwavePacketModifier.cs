using FluentAssertions;
using HNice.Model.Encryption;
using HNice.Model.Encryption.ShockWaveBuffer;
using HNice.Util.Extensions;
using System.Text;

namespace HNice.Test.Model.Encryption;

/// <summary>
/// Puts the modifier between a fake Habbo client and a fake Habbo server that speak the real protocol,
/// and checks both ends can talk to each other while the proxy reads everything in plain text.
/// </summary>
public class TestsForShockwavePacketModifier
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    private readonly ShockwavePacketModifier _proxy = new();
    private readonly BobbaCrypto _client = new();
    private readonly BobbaCrypto _server = new();
    private readonly ShockwaveProperBuffer _serverReceiveBuffer = new();
    private readonly ShockwaveProperBuffer _clientReceiveBuffer = new();

    [Fact]
    public void ShouldReplaceThePublicKeysDuringTheHandshake()
    {
        var (keySeenByServer, keySeenByClient) = Handshake();

        keySeenByServer.Should().NotBe(_client.PublicKey);
        keySeenByClient.Should().NotBe(_server.PublicKey);
        _proxy.IsClientCryptoEnabled.Should().BeTrue();
        _proxy.IsServerCryptoEnabled.Should().BeTrue();
    }

    [Fact]
    public void ShouldDecryptAndReEncryptClientPackets()
    {
        Handshake();
        var chat = OutgoingStringPacket("@t", "Hello from the client");
        var move = OutgoingStringPacket("AK", "10 6");

        var wire = Concat(ClientEncrypt(chat), ClientEncrypt(move));

        // Deliver byte by byte: headers must be decrypted exactly once even when a packet is incomplete.
        var received = wire.SelectMany(b => _proxy.ClientToProxy([b])).ToArray();

        received.Should().HaveCount(2);
        received[0].Should().Equal(chat);
        received[1].Should().Equal(move);

        var toServer = Concat(_proxy.ProxyToServer(received[0]), _proxy.ProxyToServer(received[1]));
        var serverPackets = ServerReceive(toServer);

        serverPackets.Should().HaveCount(2);
        serverPackets[0].Should().Equal(chat);
        serverPackets[1].Should().Equal(move);
    }

    [Fact]
    public void ShouldSplitServerChunksIntoPackets()
    {
        Handshake();

        // Packets can be spread over several encrypted chunks.
        var wire = Concat(
            ServerEncrypt(Latin1.GetBytes("@Bfoo\u0002\u0001@Cbar\u0001@D")),
            ServerEncrypt(Latin1.GetBytes("baz\u0001")));

        var received = wire.Chunk(5).SelectMany(part => _proxy.ServerToProxy(part)).ToArray();

        received.Select(Latin1.GetString).Should().Equal("@Bfoo\u0002", "@Cbar", "@Dbaz");

        var toClient = received.Select(packet => _proxy.ProxyToClient(packet)).SelectMany(bytes => bytes).ToArray();
        var clientData = ClientReceive(toClient).SelectMany(chunk => chunk).ToArray();

        Latin1.GetString(clientData).Should().Be("@Bfoo\u0002\u0001@Cbar\u0001@Dbaz\u0001");
    }

    [Fact]
    public void ShouldDecryptServerDataReceivedTogetherWithTheSecretKey()
    {
        ClientSendsGenerateKey();

        _server.SetRemotePublicKey(ReadOutgoingString(ServerReceivePlain(_lastToServer!)));
        _serverReceiveBuffer.SetCipher(h => BobbaCrypto.ApplyChaCha(CipherBase64.Decode(h), _server.C2sHeader!));

        var secretKey = Latin1.GetBytes("@A" + _server.PublicKey + "\u0002\u0001");
        var wire = Concat(secretKey, ServerEncrypt(Latin1.GetBytes("@Bfirst encrypted\u0001")));

        var received = _proxy.ServerToProxy(wire);

        received.Select(Latin1.GetString).Should().Equal("@A" + _server.PublicKey + "\u0002", "@Bfirst encrypted");
    }

    [Fact]
    public void ShouldAddTheLengthPrefixToPlainClientPackets()
    {
        var packet = Latin1.GetBytes("CN");

        Latin1.GetString(_proxy.ProxyToServer(packet)).Should().Be("@@BCN");
        _proxy.ClientToProxy(Latin1.GetBytes("@@BCN")).Should().ContainSingle().Which.Should().Equal(packet);
    }

    private byte[]? _lastToServer;

    private void ClientSendsGenerateKey()
    {
        var generateKey = OutgoingStringPacket("CJ", _client.PublicKey);
        var packets = _proxy.ClientToProxy(Concat(generateKey.Length.EncodeB64Bytes(3), generateKey));

        packets.Should().ContainSingle().Which.Should().Equal(generateKey);
        _lastToServer = _proxy.ProxyToServer(packets[0]);
    }

    private (string KeySeenByServer, string KeySeenByClient) Handshake()
    {
        ClientSendsGenerateKey();

        var keySeenByServer = ReadOutgoingString(ServerReceivePlain(_lastToServer!));
        _server.SetRemotePublicKey(keySeenByServer);
        _serverReceiveBuffer.SetCipher(h => BobbaCrypto.ApplyChaCha(CipherBase64.Decode(h), _server.C2sHeader!));

        var fromServer = _proxy.ServerToProxy(Latin1.GetBytes("@A" + _server.PublicKey + "\u0002\u0001"));
        fromServer.Should().ContainSingle();

        var toClient = Latin1.GetString(_proxy.ProxyToClient(fromServer[0]));
        toClient.Should().StartWith("@A").And.EndWith("\u0002\u0001");
        var keySeenByClient = toClient[2..^2];

        _client.SetRemotePublicKey(keySeenByClient);
        _clientReceiveBuffer.SetCipher(h => BobbaCrypto.ApplyChaCha(CipherBase64.Decode(h), _client.S2cHeader!));

        return (keySeenByServer, keySeenByClient);
    }

    private byte[] ClientEncrypt(byte[] packet) => ShockwavePacketModifier.EncryptChunk(packet, _client.C2sHeader!, _client.C2sData!);

    private byte[] ServerEncrypt(byte[] data) => ShockwavePacketModifier.EncryptChunk(data, _server.S2cHeader!, _server.S2cData!);

    private byte[][] ServerReceive(byte[] data)
    {
        _serverReceiveBuffer.Push(data);
        return ShockwavePacketModifier.DecryptChunks(_serverReceiveBuffer.Receive(), _server.C2sData!);
    }

    private byte[][] ClientReceive(byte[] data)
    {
        _clientReceiveBuffer.Push(data);
        return ShockwavePacketModifier.DecryptChunks(_clientReceiveBuffer.Receive(), _client.S2cData!);
    }

    private static byte[] ServerReceivePlain(byte[] data)
    {
        var buffer = new ShockwaveProperBuffer();
        buffer.Push(data);
        return buffer.Receive().Should().ContainSingle().Subject;
    }

    private static byte[] OutgoingStringPacket(string header, string value)
    {
        var bytes = Latin1.GetBytes(value);
        return Concat(Concat(Latin1.GetBytes(header), bytes.Length.EncodeB64Bytes()), bytes);
    }

    private static string ReadOutgoingString(byte[] packet)
    {
        var length = packet.AsSpan(2, 2).ToArray().DecodeB64();
        return Latin1.GetString(packet, 4, length);
    }

    private static byte[] Concat(byte[] first, byte[] second) => [.. first, .. second];
}
