using HNice.Service;
using HNice.Util;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace HNice.ViewModel;

public enum CheckState
{
    Idle,
    Running,
    Pass,
    Warn,
    Fail
}

/// <summary>One line of the diagnostics checklist.</summary>
public sealed class DiagnosticCheck : BaseViewModel
{
    public string Title { get; }

    private CheckState _state = CheckState.Idle;
    public CheckState State { get => _state; set { _state = value; OnPropertyChanged(); } }

    private string _detail = string.Empty;
    public string Detail { get => _detail; set { _detail = value; OnPropertyChanged(); } }

    /// <summary>A technical value shown in monospace (IP, latency, key fingerprint).</summary>
    private string _value = string.Empty;
    public string Value { get => _value; set { _value = value; OnPropertyChanged(); } }

    public DiagnosticCheck(string title, string detail)
    {
        Title = title;
        _detail = detail;
    }

    public void Set(CheckState state, string detail, string value = "")
    {
        State = state;
        Detail = detail;
        Value = value;
    }
}

/// <summary>
/// Step-by-step connection checklist: hosts redirect, server IP, reachability, local proxy,
/// client connection and the key exchange in both directions.
/// </summary>
class DiagnosticsViewModel : BaseViewModel
{
    private readonly MainWindowViewModel _main;
    private readonly DispatcherTimer _liveTimer;

    public DiagnosticCheck Hosts { get; } = new("Hosts file redirect", "Not checked yet.");
    public DiagnosticCheck ServerIp { get; } = new("Server IP", "Not checked yet.");
    public DiagnosticCheck Reachable { get; } = new("Game server reachable", "Not checked yet.");
    public DiagnosticCheck Proxy { get; } = new("Local proxy", "Press Connect to start it.");
    public DiagnosticCheck Client { get; } = new("Habbo client connected", "Waiting for the proxy.");
    public DiagnosticCheck ClientKeys { get; } = new("Key exchange: client ↔ HNice", "Waiting for the client.");
    public DiagnosticCheck ServerKeys { get; } = new("Key exchange: HNice ↔ server", "Waiting for the client.");
    public DiagnosticCheck Decrypted { get; } = new("Packets decrypted", "Waiting for the key exchange.");

    public ObservableCollection<DiagnosticCheck> NetworkChecks { get; }
    public ObservableCollection<DiagnosticCheck> SessionChecks { get; }

    private bool _isRunning;
    public bool IsRunning { get => _isRunning; private set { _isRunning = value; OnPropertyChanged(); } }

    public ICommand RunChecksCommand { get; }
    public ICommand CopyReportCommand { get; }

    public DiagnosticsViewModel(ITcpInterceptorWorker worker, MainWindowViewModel main) : base(worker)
    {
        _main = main;
        NetworkChecks = new() { Hosts, ServerIp, Reachable };
        SessionChecks = new() { Proxy, Client, ClientKeys, ServerKeys, Decrypted };

        RunChecksCommand = new RelayCommand(async _ => await RunNetworkChecksAsync(), _ => !IsRunning);
        CopyReportCommand = new RelayCommand(_ => Clipboard.SetText(BuildReport()));

        // Session state is cheap to read: keep it live.
        _liveTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => RefreshSession(), Dispatcher.CurrentDispatcher);
        _liveTimer.Start();
        RefreshSession();
    }

    #region Network checks (on demand)
    private async Task RunNetworkChecksAsync()
    {
        IsRunning = true;
        try
        {
            CheckHosts();
            var ip = await CheckServerIpAsync();
            await CheckReachableAsync(ip);
        }
        finally
        {
            IsRunning = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void CheckHosts()
    {
        var redirected = HostEditor.IsRedirected(_main.LocalHost, _main.HotelAddress);
        if (redirected)
        {
            Hosts.Set(CheckState.Pass, $"{_main.HotelAddress} points to {_main.LocalHost}, so the client connects through HNice.");
        }
        else if (_main.IsConnected)
        {
            Hosts.Set(CheckState.Fail, $"No '{_main.LocalHost} {_main.HotelAddress}' entry. Run HNice as administrator so it can edit the hosts file.");
        }
        else
        {
            Hosts.Set(CheckState.Warn, "Not redirected yet. HNice adds the entry when you press Connect and removes it on Disconnect.");
        }
    }

    private async Task<string?> CheckServerIpAsync()
    {
        ServerIp.Set(CheckState.Running, _main.UseAutoIp ? $"Looking up {_main.HotelAddress} through public DNS..." : "Checking the IP you entered...");

        if (!_main.UseAutoIp)
        {
            var manual = _main.HotelIP.Trim();
            if (System.Net.IPAddress.TryParse(manual, out _))
            {
                ServerIp.Set(CheckState.Pass, "Using the IP entered manually.", manual);
                return manual;
            }
            ServerIp.Set(CheckState.Fail, "The manual IP is empty or not a valid IPv4 address.");
            return null;
        }

        var resolved = await PublicDnsResolver.ResolveAsync(_main.HotelAddress.Trim());
        if (string.IsNullOrEmpty(resolved))
        {
            ServerIp.Set(CheckState.Fail, "Public DNS did not answer for this host. Check the address or switch to Manual.");
            return null;
        }

        // The proxy keeps using the IP resolved at connect time; flag if the hotel has moved since.
        if (_main.IsConnected && !string.IsNullOrEmpty(_main.HotelIP) && _main.HotelIP != resolved)
        {
            ServerIp.Set(CheckState.Warn, $"DNS now answers {resolved}, but this session uses {_main.HotelIP}. Reconnect if the game fails.", resolved);
        }
        else
        {
            ServerIp.Set(CheckState.Pass, "Resolved through public DNS (bypasses the hosts redirect).", resolved);
        }
        return resolved;
    }

    private async Task CheckReachableAsync(string? ip)
    {
        if (ip is null)
        {
            Reachable.Set(CheckState.Idle, "Skipped: no server IP.");
            return;
        }

        Reachable.Set(CheckState.Running, $"Opening a TCP connection to {ip}:{_main.InfoPort}...");
        var watch = Stopwatch.StartNew();
        try
        {
            using var probe = new TcpClient();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            await probe.ConnectAsync(ip, _main.InfoPort, timeout.Token);
            Reachable.Set(CheckState.Pass, $"The server accepts connections on port {_main.InfoPort}.", $"{watch.ElapsedMilliseconds} ms");
        }
        catch (OperationCanceledException)
        {
            Reachable.Set(CheckState.Fail, $"No answer from {ip}:{_main.InfoPort} within 4 s. The IP may be outdated; try Auto, or check the port.");
        }
        catch (SocketException ex)
        {
            Reachable.Set(CheckState.Fail, $"Connection refused or unreachable: {ex.SocketErrorCode}.");
        }
    }
    #endregion

    #region Session checks (live)
    private void RefreshSession()
    {
        var status = _main.Status;

        switch (status)
        {
            case ProxyStatus.Stopped:
                Proxy.Set(CheckState.Idle, "Not running. Press Connect to start it.");
                break;
            case ProxyStatus.ConnectionFailed:
                Proxy.Set(CheckState.Fail, "Could not reach the game server from the proxy. Run the checks above.");
                break;
            default:
                Proxy.Set(CheckState.Pass, "Listening for the Habbo client.", $"{_main.LocalHost}:{_main.InfoPort}");
                break;
        }

        var clientConnected = status is ProxyStatus.Connected or ProxyStatus.Encrypted;
        Client.Set(clientConnected ? CheckState.Pass : CheckState.Idle,
            clientConnected
                ? "The client connected through HNice and HNice connected to the server."
                : status == ProxyStatus.Listening ? "Log in through the Habbo launcher." : "Waiting for the proxy.");

        if (!_main.DecryptPackets)
        {
            const string off = "Decryption is off: traffic is passed through untouched.";
            ClientKeys.Set(CheckState.Idle, off);
            ServerKeys.Set(CheckState.Idle, off);
            Decrypted.Set(CheckState.Idle, off);
            return;
        }

        var crypto = Worker.CryptoDiagnostics;
        if (crypto is null || !clientConnected)
        {
            ClientKeys.Set(CheckState.Idle, "Waiting for the client.");
            ServerKeys.Set(CheckState.Idle, "Waiting for the client.");
            Decrypted.Set(CheckState.Idle, "Waiting for the key exchange.");
            return;
        }

        ClientKeys.Set(
            crypto.ClientCryptoEnabled ? CheckState.Pass : crypto.ClientKeyReceived ? CheckState.Running : CheckState.Idle,
            crypto.ClientCryptoEnabled ? "Keys agreed with the client; its traffic is decrypted."
                : crypto.ClientKeyReceived ? "Client public key received, waiting for the server's key."
                : "Waiting for the client's GENERATEKEY packet.",
            crypto.ClientLinkFingerprint ?? string.Empty);

        ServerKeys.Set(
            crypto.ServerCryptoEnabled ? CheckState.Pass : crypto.ServerKeyReceived ? CheckState.Running : CheckState.Idle,
            crypto.ServerCryptoEnabled ? "Keys agreed with the server; its traffic is decrypted."
                : "Waiting for the server's SECRET_KEY packet.",
            crypto.ServerLinkFingerprint ?? string.Empty);

        var total = crypto.ClientPacketsDecrypted + crypto.ServerPacketsDecrypted;
        Decrypted.Set(
            total > 0 ? CheckState.Pass : crypto.ClientCryptoEnabled || crypto.ServerCryptoEnabled ? CheckState.Running : CheckState.Idle,
            total > 0 ? "Decrypted traffic is flowing. If the log shows readable packets, the keys are right."
                : "No encrypted packets decrypted yet.",
            $"→ {crypto.ClientPacketsDecrypted}   ← {crypto.ServerPacketsDecrypted}");
    }
    #endregion

    private string BuildReport()
    {
        var report = new StringBuilder();
        report.AppendLine($"HNice diagnostics, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"Host {_main.HotelAddress}, port {_main.InfoPort}, IP mode {(_main.UseAutoIp ? "auto" : "manual")}, decrypt {_main.DecryptPackets}");
        foreach (var check in NetworkChecks.Concat(SessionChecks))
        {
            report.Append($"[{check.State.ToString().ToUpperInvariant()}] {check.Title}: {check.Detail}");
            if (!string.IsNullOrEmpty(check.Value)) report.Append($" ({check.Value})");
            report.AppendLine();
        }
        return report.ToString();
    }
}
