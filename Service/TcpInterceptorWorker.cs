using System.Net.Sockets;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using HNice.Model;
using System.Windows;
using System.Threading;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using static HNice.Service.TcpInterceptorWorker;

namespace HNice.Service;

public enum TrafficDirection
{
    ClientToServer,
    ServerToClient
}

public interface ITcpInterceptorWorker 
{
    event AddLog OnAddEncryptedLog;
    event AddLog OnAddDecryptedLog;
    Task ExecuteAsync(string serverIp, int serverPort, int localPort, bool isEncrypted, CancellationToken cancellationToken);
    Task SendPacketToClientAsync(string message);
    Task SendPacketToServerAsync(string message);
}

public class TcpInterceptorWorker : IDisposable, ITcpInterceptorWorker
{
    private readonly ILogger<TcpInterceptorWorker> _logger;

    private string _serverIp;
    private int _serverPort;
    private int _localPort;
    private CancellationToken _cancellationToken;
    private NetworkStream _clientStream;
    private NetworkStream _serverStream;
    private TcpClient? _client;
    private TcpClient? _server;
    private TcpListener? _listener;
    private string _publicKey = string.Empty;
    private readonly IHabboRC4 _decryptCipher;
    private readonly IHabboRC4 _encryptCipher;
    private bool _isEncrypted = false;
    public static readonly byte[] ARTIFICIAL_KEY = StringToByteArray("14d288cdb0bc08c274809a7802962af98b41dec8");
    public readonly IPacketSplitter _packetSplitter;
    private bool _packetSplitted = false;
    private bool _sentData = false;

    public delegate void AddLog(string log);
    public event AddLog OnAddEncryptedLog;
    public event AddLog OnAddDecryptedLog;

    private static byte[] StringToByteArray(string hex)
    {
        return Enumerable.Range(0, hex.Length)
                         .Where(x => x % 2 == 0)
                         .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                         .ToArray();
    }

        public TcpInterceptorWorker(IPacketSplitter packetSplitter, ILogger<TcpInterceptorWorker> logger)
    {
        _packetSplitter = packetSplitter ?? throw new ArgumentNullException(nameof(packetSplitter));
        _logger = logger ?? throw new ArgumentNullException(nameof(_logger));
        _encryptCipher = new HabboRC4();
        _decryptCipher = new HabboRC4();
    }

    public async Task ExecuteAsync(string serverIp, int serverPort, int localPort, bool isEncrypted, CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _serverIp = serverIp ?? throw new ArgumentNullException(nameof(_serverIp));
        _serverPort = serverPort;
        _localPort = localPort;
        _isEncrypted = isEncrypted;

        _listener = new TcpListener(IPAddress.Any, _localPort);
        _logger.LogInformation($"Listening on port {_localPort}...");

        while (!_cancellationToken.IsCancellationRequested)
        {
            try
            {
                _listener.Start();
                if (_listener.Pending())
                {
                    _client = await _listener.AcceptTcpClientAsync();
                    _logger.LogInformation("Accepted connection from local application");
                    await HandleClientAsync();
                }
                else
                {
                    await Task.Delay(100, _cancellationToken); // Prevent tight loop
                }
            }
            catch (Exception e)
            {
                _logger.LogInformation("Operation cancelled.",e);
            }
        }
        _listener?.Stop();
        _logger.LogInformation("Listener stopped.");
    }

    private async Task HandleClientAsync()
    {
        try
        {
            _server = new TcpClient();
            await _server.ConnectAsync(IPAddress.Parse(_serverIp), _serverPort);
            _logger.LogInformation($"Connected to remote server!!! {_serverIp}:{_serverPort}");

            _clientStream = _client.GetStream();
            _serverStream = _server.GetStream();

            Task clientToServer = RelayTraffic(_clientStream, _serverStream, TrafficDirection.ClientToServer);
            Task serverToClient = RelayTraffic(_serverStream, _clientStream, TrafficDirection.ServerToClient);

            await Task.WhenAny(clientToServer, serverToClient);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error handling client: {ex.Message}");
            DisposeResources();
        }
    }

    private async Task RelayTraffic(NetworkStream fromStream, NetworkStream toStream, TrafficDirection direction)
    {
        var buffer = new byte[4096];
        int bytesRead = 0;

        try
        {
            while ((bytesRead = await fromStream.ReadAsync(buffer, 0, buffer.Length, _cancellationToken)) > 0)
            {
                // Read the packets:
                switch (direction)
                {
                    case TrafficDirection.ServerToClient:
                        await ServerToClientPackets(buffer, bytesRead);
                        break;
                    case TrafficDirection.ClientToServer:
                        await ClientToServerPackets(buffer, bytesRead);
                        break;
                }
                await toStream.WriteAsync(buffer, 0, bytesRead, _cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{direction} error: {ex.Message}");
            DisposeResources();
        }
    }

    public async Task SendPacketToClientAsync(string data)
    {
        if (_client is not null)
        {
            try
            {
                var buffer = Encoding.ASCII.GetBytes(data);
                await _clientStream.WriteAsync(buffer, 0, buffer.Length, _cancellationToken);
                AddDecryptedLog(data);
                _logger.LogInformation($"Sent to client: {data}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending packet to client: {ex.Message}", ex);
                DisposeResources();
            }
        }
        else
        {
            _logger.LogWarning("Client stream is not available.");
        }
    }

    public async Task SendPacketToServerAsync(string data)
    {
        if (_server is not null)
        {
            try
            {
                if (data.Length >= 65)
                {
                    _logger.LogInformation($"Packet lenght limit exceeded: {data.Length}");
                }
                else
                {
                    var packetEncrypted = _encryptCipher.Encipher(data);
                    var buffer = Encoding.ASCII.GetBytes(packetEncrypted);
                    AddDecryptedLog($"{data}");
                    AddEncryptedLog($"{packetEncrypted}");
                    _logger.LogInformation($"Sent to server: {data} as {packetEncrypted}");
                    await _serverStream.WriteAsync(buffer, 0, buffer.Length, _cancellationToken);
                   
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending packet to server: {ex.Message}", ex);
                DisposeResources();
            }
        }
        else
        {
            _logger.LogWarning("Server stream is not available.");
        }
    }

    private async Task ServerToClientPackets(byte[] buffer, int bytesRead) 
    {
        if ((_client is not null && !_server.Connected) || buffer.Length == 0)
        {
            _logger.LogInformation("Server NOT connected");
            return;
        }

        string serverPacket = Encoding.ASCII.GetString(buffer, 0, bytesRead);
        if (string.IsNullOrEmpty(serverPacket))
        {
            return;
        }

        var splitterData = _packetSplitter.SplitData(serverPacket, TrafficDirection.ServerToClient, ref _packetSplitted);

        // Set public key from handshake to server in order to decrypt packets
        if (!string.IsNullOrEmpty(splitterData?.PublicKey))
        {
            _publicKey = splitterData.PublicKey;
            _decryptCipher.SetKey(_publicKey);
            _encryptCipher.SetKey(_publicKey);
            _logger.LogInformation($"Server packet: {serverPacket} | --> PUBLICKEY: {this._publicKey} <-- |");
        }

        if (_client is not null)
        {
            if (splitterData.HasDataToSend()) 
            {
                await _client.GetStream().WriteAsync(Encoding.ASCII.GetBytes(splitterData.DataToSend));
            }
        }

        AddDecryptedLog($"{serverPacket}");
        _logger.LogInformation($"{TrafficDirection.ServerToClient} (decrypted): {serverPacket}");

    }

    private async Task ClientToServerPackets(byte[] buffer, int bytesRead)
    {
        if ((_server is not null && !_client.Connected) || buffer.Length == 0)
        {
            _logger.LogInformation("Client NOT connected");
            return;
        }

        string clientPacket = Encoding.ASCII.GetString(buffer, 0, bytesRead);

        if (string.IsNullOrEmpty(clientPacket))
        {
            return;
        }

        var splitterData = _packetSplitter.SplitData(clientPacket, TrafficDirection.ClientToServer, ref _packetSplitted);

        if (_isEncrypted)
        {
            if (clientPacket == "@@BCJ" || clientPacket == "@@BCN" || string.IsNullOrEmpty(_publicKey)) return;

            // We are going to decrypt the clients packets that are going to the server
            _logger.LogInformation($"{TrafficDirection.ClientToServer} (decrypted): {_decryptCipher.Decipher(clientPacket)}");
            AddDecryptedLog($"{_decryptCipher.Decipher(clientPacket)}");
            
            return;
        }
        AddEncryptedLog($"{clientPacket}");
        _logger.LogInformation($"{TrafficDirection.ClientToServer} (encrypted): {clientPacket}");
    }

    public void Dispose()
    {
        DisposeResources();
    }
    private void DisposeResources()
    {
        try
        {
            _listener?.Dispose();
            _client?.Close();
            _client?.Dispose();
            _server?.Close();
            _server?.Dispose();
            _clientStream?.Dispose();
            _serverStream?.Dispose();
            _logger.LogInformation("Resources disposed.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error disposing resources: {ex.Message}");
        }
    }

    private void AddDecryptedLog(string logEntry)
    {
        // Use the Dispatcher to update the ObservableCollection on the UI thread
        App.Current.Dispatcher.Invoke(() =>
        {
            OnAddEncryptedLog?.Invoke(logEntry);
        });
    }

    private void AddEncryptedLog(string logEntry)
    {
        // Use the Dispatcher to update the ObservableCollection on the UI thread
        App.Current.Dispatcher.Invoke(() =>
        {
            OnAddDecryptedLog?.Invoke(logEntry);
        });
    }
}

