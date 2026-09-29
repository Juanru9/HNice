using HNice.Model;
using HNice.Service;
using HNice.Util;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace HNice.ViewModel
{
    public class MainWindowViewModel : BaseViewModel, IDisposable
    {
        private const int MaxLogEntries = 5000;

        private readonly ILogger<MainWindowViewModel> _logger;
        private readonly ConcurrentQueue<PacketLogEntry> _pendingEntries = new();
        private readonly DispatcherTimer _flushTimer;
        private CancellationTokenSource? _cts;

        // Full packet trace to a file, when started with --packet-trace <file>.
        private readonly PacketTrace? _trace;

        // Console-driven commands, when started with --command-file <file>.
        private readonly CommandChannel? _commands;

        #region Connection
        private string _hotelAddress = "game-oes.habbo.com"; // By default we set Spain Address
        public string HotelAddress
        {
            get => _hotelAddress;
            set { _hotelAddress = value; OnPropertyChanged(); }
        }

        private string _hotelIP = string.Empty;
        /// <summary>In Auto mode this shows the last resolved address; in Manual mode the user types it.</summary>
        public string HotelIP
        {
            get => _hotelIP;
            set { _hotelIP = value; OnPropertyChanged(); ValidateManualIp(); }
        }

        private bool _useAutoIp = true;
        /// <summary>Auto = resolve the hotel address through public DNS; Manual = use the typed IP.</summary>
        public bool UseAutoIp
        {
            get => _useAutoIp;
            set
            {
                _useAutoIp = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(UseManualIp));
                IpMessage = value ? AutoIpHint : string.Empty;
                IpMessageIsError = false;
                ValidateManualIp();
            }
        }
        public bool UseManualIp
        {
            get => !_useAutoIp;
            set => UseAutoIp = !value;
        }

        private const string AutoIpHint = "Found through public DNS when you connect.";

        private string _ipMessage = AutoIpHint;
        /// <summary>Inline feedback under the IP field (resolution result or validation error).</summary>
        public string IpMessage
        {
            get => _ipMessage;
            private set { _ipMessage = value; OnPropertyChanged(); }
        }

        private bool _ipMessageIsError;
        public bool IpMessageIsError
        {
            get => _ipMessageIsError;
            private set { _ipMessageIsError = value; OnPropertyChanged(); }
        }

        private bool _isResolving;
        public bool IsResolving
        {
            get => _isResolving;
            private set { _isResolving = value; OnPropertyChanged(); }
        }

        private int _infoPort = 40001;
        public int InfoPort
        {
            get => _infoPort;
            set { _infoPort = value; OnPropertyChanged(); }
        }

        private bool _decryptPackets = true;
        public bool DecryptPackets
        {
            get => _decryptPackets;
            set { _decryptPackets = value; OnPropertyChanged(); }
        }

        private bool _isConnected;
        /// <summary>True while the proxy is running (listening or relaying).</summary>
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotConnected)); }
        }
        public bool IsNotConnected => !_isConnected;

        private ProxyStatus _status = ProxyStatus.Stopped;
        public ProxyStatus Status
        {
            get => _status;
            private set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        public string StatusText => Status switch
        {
            ProxyStatus.Listening => $"Waiting for the Habbo client on port {InfoPort}",
            ProxyStatus.Connected => $"Client connected to {HotelIP}",
            ProxyStatus.Encrypted => DecryptPackets ? "Connected, decrypting traffic" : "Connected, traffic is encrypted",
            ProxyStatus.ConnectionFailed => $"Could not reach {HotelIP}:{InfoPort}",
            _ => "Offline",
        };

        private string _playerName = string.Empty;
        public string PlayerName
        {
            get => _playerName;
            private set { _playerName = value; OnPropertyChanged(); }
        }

        public string LocalHost { get; } = "127.0.0.1";
        #endregion

        #region Packet log
        public ObservableCollection<PacketLogEntry> Entries { get; } = new();
        public ICollectionView EntriesView { get; }

        private bool _captureInbound = true;
        public bool CaptureInbound
        {
            get => _captureInbound;
            set { _captureInbound = value; OnPropertyChanged(); }
        }

        private bool _captureOutbound = true;
        public bool CaptureOutbound
        {
            get => _captureOutbound;
            set { _captureOutbound = value; OnPropertyChanged(); }
        }

        private bool _followLog = true;
        public bool FollowLog
        {
            get => _followLog;
            set { _followLog = value; OnPropertyChanged(); }
        }

        private string _filterText = string.Empty;
        public string FilterText
        {
            get => _filterText;
            set { _filterText = value; OnPropertyChanged(); EntriesView.Refresh(); }
        }

        private int _inboundCount;
        public int InboundCount
        {
            get => _inboundCount;
            private set { _inboundCount = value; OnPropertyChanged(); }
        }

        private int _outboundCount;
        public int OutboundCount
        {
            get => _outboundCount;
            private set { _outboundCount = value; OnPropertyChanged(); }
        }

        /// <summary>Raised after a batch of rows was appended (the view scrolls when following).</summary>
        public event Action? EntriesAppended;
        #endregion

        #region Composer
        private string _composerText = string.Empty;
        /// <summary>Header + body. Accepts [1] [2] [9] [13] for control characters.</summary>
        public string ComposerText
        {
            get => _composerText;
            set { _composerText = value; OnPropertyChanged(); }
        }
        #endregion

        #region Tools
        public ObservableCollection<ToolItem> Tools { get; } = new();

        private ToolItem? _selectedTool;
        public ToolItem? SelectedTool
        {
            get => _selectedTool;
            set { _selectedTool = value; OnPropertyChanged(); }
        }
        #endregion

        #region Commands
        public ICommand ConnectCommand { get; }
        public ICommand ResolveCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand SendToClientCommand { get; }
        public ICommand SendToServerCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand UseInComposerCommand { get; }
        #endregion

        public MainWindowViewModel(ITcpInterceptorWorker worker, ILogger<MainWindowViewModel> logger) : base(worker)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _trace = PacketTrace.FromStartup(logger);
            _commands = CommandChannel.FromStartup(logger, RunCommandAsync);

            EntriesView = CollectionViewSource.GetDefaultView(Entries);
            EntriesView.Filter = MatchesFilter;

            ConnectCommand = new RelayCommand(async _ => await OnConnect(), _ => IsNotConnected && !IsResolving && (UseAutoIp || IsValidIp(HotelIP)));
            ResolveCommand = new RelayCommand(async _ => await ResolveAsync(CancellationToken.None), _ => UseAutoIp && IsNotConnected && !IsResolving);
            DisconnectCommand = new RelayCommand(_ => OnDisconnect(), _ => IsConnected);
            SendToClientCommand = new RelayCommand(async _ => await SendComposer(toServer: false), _ => CanSend());
            SendToServerCommand = new RelayCommand(async _ => await SendComposer(toServer: true), _ => CanSend());
            ClearLogCommand = new RelayCommand(_ => ClearLog());
            UseInComposerCommand = new RelayCommand(p => { if (p is PacketLogEntry e) ComposerText = e.Escaped; });

            Worker.OnAddInboundPacketLog += OnInboundPacket;
            Worker.OnAddOutboundPacketLog += OnOutboundPacket;
            Worker.StatusChanged += OnStatusChanged;
            Worker.PlayerChanged += OnPlayerChanged;

            // Rows are queued from the network threads and flushed in batches: the UI never blocks the relay.
            _flushTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Background, (_, _) => FlushEntries(), Dispatcher.CurrentDispatcher);
            _flushTimer.Start();

            BuildTools();
        }

        private void BuildTools()
        {
            Tools.Add(new ToolItem("Credits", "Hotel", "", "Show any balance in your purse.", true, new CreditsViewModel(Worker)));
            Tools.Add(new ToolItem("Badges", "Hotel", "", "Add badges to your badge list.", true, new BadgesViewModel(Worker)));
            Tools.Add(new ToolItem("Drinks", "Room", "", "Drop a drink machine at your feet.", true, new DrinksViewModel(Worker)));
            Tools.Add(new ToolItem("Furni", "Room", "", "Browse the game's furni catalogue and place any item next to you.", true, new FurnitureViewModel(Worker)));
            Tools.Add(new ToolItem("Posters", "Room", "", "Hang a poster on a wall.", true, new RoomDecorViewModel(Worker)));
            Tools.Add(new ToolItem("Warp", "Room", "", "Move your avatar to any tile.", true, new WarpViewModel(Worker)));
            Tools.Add(new ToolItem("Imitate", "People", "", "Change how your own avatar looks.", true, new ImitateViewModel(Worker)));
            Tools.Add(new ToolItem("Mime", "People", "", "Copy another Habbo's walking, gestures and chat. Everyone sees it.", false, new MimicViewModel(Worker)));
            Tools.Add(new ToolItem("Spawn user", "People", "", "Add a fake user or bot to the room.", true, new SpawnUserViewModel(Worker)));
            Tools.Add(new ToolItem("Messages", "Staff", "", "Fake hotel alerts and mod warnings.", true, new ModFunctionsViewModel(Worker)));
            Tools.Add(new ToolItem("Fuse rights", "Staff", "", "Unlock staff UI in your client.", true, new FuseViewModel(Worker)));
            Tools.Add(new ToolItem("Diagnostics", "Utilities", "", "Check the redirect, the server, the proxy and the key exchange.", false, new DiagnosticsViewModel(Worker, this)));
            Tools.Add(new ToolItem("Encoder", "Utilities", "", "Convert numbers to B64 and VL64 and back.", false, new EncodeDecodeViewModel()));
            Tools.Add(new ToolItem("About", "Utilities", "", "Version, credits and fair-use note.", false, new AboutViewModel()));
            SelectedTool = Tools[0];
        }

        #region Connection flow
        private async Task OnConnect()
        {
            _cts = new CancellationTokenSource();

            if (UseAutoIp)
            {
                // Resolve through public DNS before hijacking the hosts file: once the hosts entry exists
                // the OS resolver would answer 127.0.0.1.
                var resolved = await ResolveAsync(_cts.Token);
                if (!resolved)
                {
                    Status = ProxyStatus.ConnectionFailed;
                    return;
                }
            }
            else if (!IsValidIp(HotelIP))
            {
                ValidateManualIp();
                return;
            }

            //First pair hotel address to localhost for packet hijacking
            HostEditor.UpdateHostsFile(LocalHost, HotelAddress);
            IsConnected = true;
            try
            {
                // Run the proxy on the thread pool: awaiting it from the UI thread would resume every socket read on the UI thread.
                var token = _cts.Token;
                await Task.Run(() => Worker.ExecuteAsync(HotelIP.Trim(), InfoPort, InfoPort, _decryptPackets, token));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Interceptor stopped unexpectedly");
                Status = ProxyStatus.ConnectionFailed;
                OnDisconnect();
            }
        }

        /// <summary>Looks the hotel up through public DNS and shows the result inline.</summary>
        private async Task<bool> ResolveAsync(CancellationToken token)
        {
            IsResolving = true;
            IpMessageIsError = false;
            IpMessage = $"Looking up {HotelAddress}...";
            try
            {
                var resolved = await PublicDnsResolver.ResolveAsync(HotelAddress.Trim(), token);
                if (!string.IsNullOrEmpty(resolved))
                {
                    _hotelIP = resolved;
                    OnPropertyChanged(nameof(HotelIP));
                    IpMessage = $"Resolved {HotelAddress} through public DNS.";
                    _logger.LogInformation("Resolved {Address} to {Ip} via public DNS", HotelAddress, resolved);
                    return true;
                }

                IpMessageIsError = true;
                IpMessage = "Could not resolve the host. Check the address, or switch to Manual and enter the IP.";
                _logger.LogWarning("Could not resolve {Address} via public DNS", HotelAddress);
                return false;
            }
            finally
            {
                IsResolving = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void ValidateManualIp()
        {
            if (_useAutoIp) return;
            if (string.IsNullOrWhiteSpace(_hotelIP))
            {
                IpMessageIsError = false;
                IpMessage = "Enter the hotel's IPv4 address, e.g. 18.185.210.35.";
            }
            else if (!IsValidIp(_hotelIP))
            {
                IpMessageIsError = true;
                IpMessage = "That is not a valid IPv4 address.";
            }
            else
            {
                IpMessageIsError = false;
                IpMessage = "Using this IP as entered.";
            }
        }

        private static bool IsValidIp(string ip) =>
            System.Net.IPAddress.TryParse(ip?.Trim(), out var address)
            && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            && ip!.Trim().Count(c => c == '.') == 3;

        private void OnDisconnect()
        {
            //Restore the hostfile as original
            HostEditor.RestoreHostsFile(LocalHost, HotelAddress);
            _cts?.Cancel();
            IsConnected = false;
        }

        private void OnStatusChanged(ProxyStatus status)
        {
            _trace?.Note($"status: {status}");
            Application.Current?.Dispatcher.BeginInvoke(() => Status = status);
        }

        private void OnPlayerChanged(HabboPlayer player) =>
            Application.Current?.Dispatcher.BeginInvoke(() => PlayerName = player.HabboName ?? string.Empty);
        #endregion

        #region Log
        // Raised from the network threads: only enqueue here.
        // The trace records everything; the capture toggles only filter what the UI list shows.
        private void OnInboundPacket(string packet)
        {
            var entry = new PacketLogEntry(PacketDirection.Inbound, packet);
            _trace?.Write(entry);
            if (_captureInbound) _pendingEntries.Enqueue(entry);
        }

        private void OnOutboundPacket(string packet)
        {
            var entry = new PacketLogEntry(PacketDirection.Outbound, packet);
            _trace?.Write(entry);
            if (_captureOutbound) _pendingEntries.Enqueue(entry);
        }

        private void FlushEntries()
        {
            if (_pendingEntries.IsEmpty) return;

            var inbound = 0;
            var outbound = 0;
            while (_pendingEntries.TryDequeue(out var entry))
            {
                Entries.Add(entry);
                if (entry.IsInbound) inbound++; else outbound++;
            }

            while (Entries.Count > MaxLogEntries)
            {
                Entries.RemoveAt(0);
            }

            InboundCount += inbound;
            OutboundCount += outbound;
            EntriesAppended?.Invoke();
        }

        private bool MatchesFilter(object item)
        {
            if (string.IsNullOrWhiteSpace(_filterText) || item is not PacketLogEntry entry) return true;
            return entry.HeaderName.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                || entry.Body.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
                || entry.Escaped.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
        }

        private void ClearLog()
        {
            Entries.Clear();
            InboundCount = 0;
            OutboundCount = 0;
        }
        #endregion

        #region Composer
        private bool CanSend() => IsConnected && !string.IsNullOrWhiteSpace(ComposerText);

        // Text is kept after sending, so the same packet can be fired repeatedly.
        private Task SendComposer(bool toServer)
        {
            var packet = PacketText.Unescape(ComposerText.Trim());
            return toServer ? OnSendToServer(packet) : OnSendToClient(packet);
        }
        #endregion

        /// <summary>Runs one line from the command channel.</summary>
        private async Task RunCommandAsync(string command)
        {
            _trace?.Note($"command: {command}");
            var parts = command.Split(' ', 2);
            var verb = parts[0].ToLowerInvariant();
            var argument = parts.Length > 1 ? parts[1] : string.Empty;

            switch (verb)
            {
                case "server":
                    await Worker.SendPacketToServerAsync(PacketText.Unescape(argument));
                    break;
                case "client":
                    await Worker.SendPacketToClientAsync(PacketText.Unescape(argument));
                    break;
                case "catalog" when argument.StartsWith("fetch", StringComparison.OrdinalIgnoreCase):
                    var pages = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
                    var progress = new Progress<string>(p => _trace?.Note($"catalog: {p}"));
                    var fetched = await Worker.FetchCatalogAsync(pages.Count > 0 ? pages : null, progress, _cts?.Token ?? CancellationToken.None);
                    _trace?.Note($"catalog: fetched {fetched} pages, {Worker.Catalog.PagesLoaded} stored");
                    break;
                default:
                    _trace?.Note($"unknown command: {verb}");
                    break;
            }
        }

        public void Dispose()
        {
            _commands?.Dispose();
            _flushTimer.Stop();
            Worker.OnAddInboundPacketLog -= OnInboundPacket;
            Worker.OnAddOutboundPacketLog -= OnOutboundPacket;
            Worker.StatusChanged -= OnStatusChanged;
            Worker.PlayerChanged -= OnPlayerChanged;
            HostEditor.RestoreHostsFile(LocalHost, HotelAddress);
            _trace?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
