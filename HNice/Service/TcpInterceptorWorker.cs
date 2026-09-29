using System.Net.Sockets;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using HNice.Model;
using static HNice.Service.TcpInterceptorWorker;
using HNice.Model.Packets;
using HNice.Util;
using HNice.Model.Encryption;
using HNice.Model.Encryption.ShockWaveBuffer;
using HNice.Util.Extensions;

namespace HNice.Service;

public enum TrafficDirection
{
    ClientToServer,
    ServerToClient
}

/// <summary>What the proxy is doing right now, for the status bar.</summary>
public enum ProxyStatus
{
    Stopped,
    Listening,
    Connected,
    Encrypted,
    ConnectionFailed
}

public interface ITcpInterceptorWorker
{
    event AddLog OnAddOutboundPacketLog;
    event AddLog OnAddInboundPacketLog;
    event UpdateCoords OnUpdateCoords;
    event Action<ProxyStatus>? StatusChanged;
    event Action<HabboPlayer>? PlayerChanged;
    HabboPlayer? CurrentPlayer { get; }

    /// <summary>Key exchange state of the active session; null when not decrypting or not connected.</summary>
    CryptoDiagnostics? CryptoDiagnostics { get; }
    Task ExecuteAsync(string serverIp, int serverPort, int localPort, bool decryptPackets, CancellationToken cancellationToken);
    Task SendPacketToClientAsync(string message);
    Task SendPacketToServerAsync(string message);
}

public class TcpInterceptorWorker : IDisposable, ITcpInterceptorWorker
{
    private const int BufferSize = 8192;

    // Latin1 maps every byte to one char and back, so packets survive the string round trip untouched.
    private static readonly Encoding PacketEncoding = Encoding.Latin1;

    private readonly ILogger<TcpInterceptorWorker> _logger;
    private readonly object _sessionLock = new();

    private ProxySession? _session;
    private HabboPlayer? _playerInfo;
    private bool _decryptPackets;

    // Furni-placement packets to capture once each, to learn the server's real format.
    private static readonly HashSet<IncomingPacketMessage> _furniCaptureHeaders = new()
    {
        IncomingPacketMessage.ACTIVEOBJECTS,
        IncomingPacketMessage.OBJECTS,
        IncomingPacketMessage.ITEMS,
        IncomingPacketMessage.ITEMS_2,
        IncomingPacketMessage.ACTIVEOBJECT_ADD,
    };

    #region Event & Delegate region
    public delegate void AddLog(string log);
    public event AddLog OnAddOutboundPacketLog;
    public event AddLog OnAddInboundPacketLog;
    public delegate void UpdateCoords(Coordinate coords);
    public event UpdateCoords OnUpdateCoords;
    public event Action<ProxyStatus>? StatusChanged;
    public event Action<HabboPlayer>? PlayerChanged;
    #endregion

    public HabboPlayer? CurrentPlayer => _playerInfo;

    public CryptoDiagnostics? CryptoDiagnostics => _session?.Modifier?.GetDiagnostics();

    public TcpInterceptorWorker(ILogger<TcpInterceptorWorker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(string serverIp, int serverPort, int localPort, bool decryptPackets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serverIp);
        var serverEndPoint = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
        _decryptPackets = decryptPackets;

        // The hosts file points the hotel to 127.0.0.1, there is no need to expose the proxy to the network.
        var listener = new TcpListener(IPAddress.Loopback, localPort);
        listener.Start();
        _logger.LogInformation("Listening on port {Port} (decrypt packets: {Decrypt})...", localPort, decryptPackets);
        StatusChanged?.Invoke(ProxyStatus.Listening);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Accepted connection from local application");

                // Keep accepting: the client reconnects when the hotel connection drops.
                _ = HandleClientAsync(client, serverEndPoint, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Listener error");
        }
        finally
        {
            listener.Stop();
            CloseSession(null);
            _logger.LogInformation("Listener stopped.");
            StatusChanged?.Invoke(ProxyStatus.Stopped);
        }
    }

    private async Task HandleClientAsync(TcpClient client, IPEndPoint serverEndPoint, CancellationToken cancellationToken)
    {
        ProxySession? session = null;
        try
        {
            client.NoDelay = true;
            var server = new TcpClient { NoDelay = true };
            try
            {
                await server.ConnectAsync(serverEndPoint, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                server.Dispose();
                _logger.LogError("Could not connect to remote server {Server}: {Error}", serverEndPoint, ex.Message);
                StatusChanged?.Invoke(ProxyStatus.ConnectionFailed);
                return;
            }
            catch
            {
                server.Dispose();
                throw;
            }
            _logger.LogInformation("Connected to remote server {Server}", serverEndPoint);

            session = new ProxySession(client, server, _decryptPackets ? new ShockwavePacketModifier() : null);
            ReplaceSession(session);
            StatusChanged?.Invoke(ProxyStatus.Connected);

            var clientToServer = RelayClientToServerAsync(session, cancellationToken);
            var serverToClient = RelayServerToClientAsync(session, cancellationToken);

            await Task.WhenAny(clientToServer, serverToClient).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException || cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client");
        }
        finally
        {
            if (session is null)
            {
                client.Dispose();
            }
            else
            {
                // Closing the sockets also stops the relay that is still running.
                var wasCurrent = ReferenceEquals(_session, session);
                CloseSession(session);
                // Back to waiting for the client to reconnect (the listener itself reports Stopped).
                if (wasCurrent && !cancellationToken.IsCancellationRequested)
                {
                    StatusChanged?.Invoke(ProxyStatus.Listening);
                }
            }
        }
    }

    private async Task RelayClientToServerAsync(ProxySession session, CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        try
        {
            int bytesRead;
            while ((bytesRead = await session.ClientStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                if (session.Modifier is null)
                {
                    // Forward first, log afterwards: logging must never delay the game traffic.
                    await session.WriteRawToServerAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    AddOutboundPacketLog(PacketEncoding.GetString(buffer, 0, bytesRead));
                    continue;
                }

                var packets = session.Modifier.ClientToProxy(buffer.AsSpan(0, bytesRead));
                if (packets.Length == 0) continue;

                await session.SendToServerAsync(packets, cancellationToken).ConfigureAwait(false);

                foreach (var packet in packets)
                {
                    AddOutboundPacketLog(PacketEncoding.GetString(packet));
                }
            }
            _logger.LogInformation("Client closed the connection");
        }
        // Once the session is closed the pending read fails, that is expected and not worth logging.
        catch (Exception ex) when (ex is not OperationCanceledException && !cancellationToken.IsCancellationRequested && !session.IsDisposed)
        {
            _logger.LogError(ex, "{Direction} error", TrafficDirection.ClientToServer);
        }
    }

    private async Task RelayServerToClientAsync(ProxySession session, CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        try
        {
            int bytesRead;
            while ((bytesRead = await session.ServerStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                byte[][] packets;

                if (session.Modifier is null)
                {
                    await session.WriteRawToClientAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    AddInboundPacketLog(PacketEncoding.GetString(buffer, 0, bytesRead));
                    packets = session.ReadPlainServerPackets(buffer.AsSpan(0, bytesRead));
                }
                else
                {
                    packets = session.Modifier.ServerToProxy(buffer.AsSpan(0, bytesRead));
                    if (packets.Length == 0) continue;

                    await session.SendToClientAsync(packets, cancellationToken).ConfigureAwait(false);

                    foreach (var packet in packets)
                    {
                        AddInboundPacketLog(PacketEncoding.GetString(packet));
                    }
                }

                foreach (var packet in packets)
                {
                    await HandleIncomingPacketAsync(packet).ConfigureAwait(false);
                }
            }
            _logger.LogInformation("Server closed the connection");
        }
        catch (Exception ex) when (ex is not OperationCanceledException && !cancellationToken.IsCancellationRequested && !session.IsDisposed)
        {
            _logger.LogError(ex, "{Direction} error", TrafficDirection.ServerToClient);
        }
    }

    public async Task SendPacketToClientAsync(string data)
    {
        var session = _session;
        if (session is null || !session.CanSendToClient)
        {
            _logger.LogWarning("Client stream is not available.");
            return;
        }

        try
        {
            // The ender (chr 1) is added when the packet is written.
            var packet = data.EndsWith(Constants.PACKET_ENDER) ? data[..^1] : data;
            await session.SendToClientAsync([PacketEncoding.GetBytes(packet)], CancellationToken.None).ConfigureAwait(false);
            AddInboundPacketLog(packet);
            _logger.LogInformation("Sent to client: {Packet}", packet);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending packet to client");
        }
    }

    /// <param name="data">Header + body, without the 3 bytes length prefix (it is added when the packet is written).</param>
    public async Task SendPacketToServerAsync(string data)
    {
        var session = _session;
        if (session is null || !session.CanSendToServer)
        {
            _logger.LogWarning("Server stream is not available.");
            return;
        }

        try
        {
            await session.SendToServerAsync([PacketEncoding.GetBytes(data)], CancellationToken.None).ConfigureAwait(false);
            AddOutboundPacketLog(data);
            _logger.LogInformation("Sent to server: {Packet}", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending packet to server");
        }
    }

    private async Task HandleIncomingPacketAsync(byte[] packetBytes)
    {
        try
        {
            var header = ShockwavePacketModifier.GetHeader(packetBytes);

            // Capture the real furni-placement packets (every occurrence) so their exact format can be mirrored.
            // Control chars are escaped so the invisible field separators (chr 2), enders (chr 1) and tabs show.
            if (_furniCaptureHeaders.Contains((IncomingPacketMessage)header))
            {
                _logger.LogInformation("Server {Message} packet: {Packet}", (IncomingPacketMessage)header, EscapeControls(PacketEncoding.GetString(packetBytes)));
            }

            switch ((IncomingPacketMessage)header)
            {
                case IncomingPacketMessage.SECRET_KEY:
                    _logger.LogInformation("Server sent its public key, traffic is encrypted from now on.");
                    StatusChanged?.Invoke(ProxyStatus.Encrypted);
                    break;
                case IncomingPacketMessage.RIGHTS:
                    // Log the real fuse-rights packet so its exact format/permission names can be mirrored.
                    _logger.LogInformation("Server RIGHTS packet: {Packet}", PacketEncoding.GetString(packetBytes));
                    break;
                case IncomingPacketMessage.USER_OBJ:
                    if (_playerInfo is not null) break;
                    _playerInfo = new HabboPlayer(PacketEncoding.GetString(packetBytes, 2, packetBytes.Length - 2));
                    PlayerChanged?.Invoke(_playerInfo);
                    await SendPacketToClientAsync("BK" + "Welcome to Habbo Nice [" + _playerInfo.HabboName + "] by Samus").ConfigureAwait(false);
                    break;
                case IncomingPacketMessage.STATUS:
                    var roomId = _playerInfo?.DynamicRoomID;
                    if (roomId is null) break;
                    var status = ParseIncoming(packetBytes);
                    if (!status.PacketContent.Any(content => content.Contains(roomId))) break;
                    // Case when we have our own player room ID to get its real time coords:
                    var coordinates = PacketExtractor.ExtractCoordinates(status.SerializePacketData());
                    if (coordinates is not null && coordinates.AreValidCoords())
                    {
                        //Set walking coordinates
                        OnUpdateCoords?.Invoke(coordinates);
                        _logger.LogDebug("Walking to ({X},{Y}) coords.", coordinates.X, coordinates.Y);
                    }
                    break;
                case IncomingPacketMessage.USERS:
                    // Set Dynamic user ID set in a new room
                    var player = _playerInfo;
                    if (player is null) break;
                    var userData = ParseIncoming(packetBytes).PacketContent.FirstOrDefault(content => content.Contains(player.HabboName));
                    if (userData is not null && userData.Length >= 2)
                    {
                        player.DynamicRoomID = userData.Substring(0, 2);
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            // A packet we fail to understand must never break the connection.
            _logger.LogWarning(ex, "Failed to handle incoming packet {Packet}", PacketEncoding.GetString(packetBytes));
        }
    }

    private static IncomingPacket ParseIncoming(byte[] packet) => new(PacketEncoding.GetString(packet));

    // Makes the invisible Habbo separators visible for packet-format capture.
    private static string EscapeControls(string s) => s
        .Replace("\u0001", "[1]")
        .Replace("\u0002", "[2]")
        .Replace("\t", "[9]")
        .Replace("\r", "[13]");

    // The newest connection receives the packets sent from the UI. Older connections keep relaying until they close.
    private void ReplaceSession(ProxySession session)
    {
        lock (_sessionLock)
        {
            _session = session;
            _playerInfo = null;
        }
    }

    /// <param name="session">Session to close, or null to close whatever session is active.</param>
    private void CloseSession(ProxySession? session)
    {
        ProxySession? toClose;
        lock (_sessionLock)
        {
            toClose = session ?? _session;
            if (toClose is null) return;
            if (ReferenceEquals(_session, toClose))
            {
                _session = null;
                _playerInfo = null;
            }
        }
        toClose.Dispose();
        _logger.LogInformation("Resources disposed.");
    }

    // Implementing IDisposable
    public void Dispose()
    {
        CloseSession(null);
        GC.SuppressFinalize(this);
    }

    // Raw packet text; the UI formats rows itself.
    private void AddInboundPacketLog(string logEntry) => OnAddInboundPacketLog?.Invoke(logEntry);

    private void AddOutboundPacketLog(string logEntry) => OnAddOutboundPacketLog?.Invoke(logEntry);

    /// <summary>One intercepted client connection and its connection to the real server.</summary>
    private sealed class ProxySession : IDisposable
    {
        private readonly TcpClient _client;
        private readonly TcpClient _server;

        // Writes are serialized per direction so injected packets never interleave with relayed ones.
        // With encryption this also keeps the nonce order in sync with the order on the wire.
        private readonly SemaphoreSlim _clientWriteLock = new(1, 1);
        private readonly SemaphoreSlim _serverWriteLock = new(1, 1);

        // Passthrough mode only: plain server packets, until the traffic becomes encrypted.
        private readonly ShockwaveBuffer _plainServerBuffer = new();
        private volatile bool _passthroughEncrypted;
        private int _disposed;

        public NetworkStream ClientStream { get; }
        public NetworkStream ServerStream { get; }

        /// <summary>Null when packets are just forwarded (no decryption).</summary>
        public ShockwavePacketModifier? Modifier { get; }

        // Without the modifier we cannot produce valid packets once the traffic is encrypted.
        public bool CanSendToClient => _disposed == 0 && !_passthroughEncrypted;
        public bool CanSendToServer => _disposed == 0 && !_passthroughEncrypted;

        public bool IsDisposed => _disposed != 0;

        public ProxySession(TcpClient client, TcpClient server, ShockwavePacketModifier? modifier)
        {
            _client = client;
            _server = server;
            ClientStream = client.GetStream();
            ServerStream = server.GetStream();
            Modifier = modifier;
        }

        public byte[][] ReadPlainServerPackets(ReadOnlySpan<byte> data)
        {
            if (_passthroughEncrypted) return [];

            _plainServerBuffer.Push(data);
            var packets = new List<byte[]>();
            while (_plainServerBuffer.TryReceive(out var packet))
            {
                packets.Add(packet);
                if (ShockwavePacketModifier.GetHeader(packet) == (int)IncomingPacketMessage.SECRET_KEY)
                {
                    _passthroughEncrypted = true;
                    _plainServerBuffer.TakeAll();
                    break;
                }
            }
            return packets.ToArray();
        }

        public Task WriteRawToServerAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken) =>
            WriteAsync(ServerStream, _serverWriteLock, () => data, cancellationToken);

        public Task WriteRawToClientAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken) =>
            WriteAsync(ClientStream, _clientWriteLock, () => data, cancellationToken);

        public Task SendToServerAsync(IReadOnlyList<byte[]> packets, CancellationToken cancellationToken) =>
            WriteAsync(ServerStream, _serverWriteLock, () => Join(packets, packet => Modifier is null
                ? [.. packet.Length.EncodeB64Bytes(ShockwaveProperBuffer.PACKET_LENGTH_SIZE), .. packet]
                : Modifier.ProxyToServer(packet)), cancellationToken);

        public Task SendToClientAsync(IReadOnlyList<byte[]> packets, CancellationToken cancellationToken) =>
            WriteAsync(ClientStream, _clientWriteLock, () => Join(packets, packet => Modifier is null
                ? [.. packet, Constants.PACKET_ENDER_BYTE]
                : Modifier.ProxyToClient(packet)), cancellationToken);

        // The payload is built inside the lock: encryption must happen in the same order as the writes.
        private static async Task WriteAsync(NetworkStream stream, SemaphoreSlim writeLock, Func<ReadOnlyMemory<byte>> buildPayload, CancellationToken cancellationToken)
        {
            await writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await stream.WriteAsync(buildPayload(), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                writeLock.Release();
            }
        }

        // One write per batch instead of one per packet.
        private static byte[] Join(IReadOnlyList<byte[]> packets, Func<byte[], byte[]> encode)
        {
            if (packets.Count == 1) return encode(packets[0]);

            var encoded = packets.Select(encode).ToArray();
            var result = new byte[encoded.Sum(chunk => chunk.Length)];
            var offset = 0;
            foreach (var chunk in encoded)
            {
                chunk.CopyTo(result, offset);
                offset += chunk.Length;
            }
            return result;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            _client.Dispose();
            _server.Dispose();
        }
    }
}
