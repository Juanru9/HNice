using HNice.Service;
using HNice.Util;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace HNice.ViewModel
{
    public class MainWindowViewModel : BaseViewModel
    {
        #region Properties
        private string _hotelAddress = "game-oes.habbo.com"; // By default we set Spain Address
        public string HotelAddress
        {
            get => _hotelAddress;
            set
            {
                _hotelAddress = value;
                OnPropertyChanged(nameof(HotelAddress));
            }
        }
        private string _hotelIP = "18.199.57.67"; // By default we set Spain IP
        public string HotelIP
        {
            get => _hotelIP;
            set
            {
                _hotelIP = value;
                OnPropertyChanged(nameof(HotelIP));
            }
        }
        private int _infoPort = 40001;
        public int InfoPort
        {
            get => _infoPort;
            set
            {
                _infoPort = value;
                OnPropertyChanged(nameof(InfoPort));
            }
        }

        private int _musPort = 40002;
        public int MusPort
        {
            get => _musPort;
            set
            {
                _musPort = value;
                OnPropertyChanged(nameof(MusPort));
            }
        }
        private bool _decryptPackets = false;
        public bool DecryptPackets
        {
            get => _decryptPackets;
            set
            {
                _decryptPackets = value;
                OnPropertyChanged(nameof(DecryptPackets));
            }
        }
        
        private bool _pauseInboundPackets = false;
        public bool PauseInboundPackets
        {
            get => _pauseInboundPackets;
            set
            {
                _pauseInboundPackets= value;
                OnPropertyChanged(nameof(PauseInboundPackets));
            }
        }
        private bool _pauseOutboundPackets = false;
        public bool PauseOutboundPackets
        {
            get => _pauseOutboundPackets;
            set
            {
                _pauseOutboundPackets = value;
                OnPropertyChanged(nameof(PauseOutboundPackets));
            }
        }
        
        private bool _isConnected = false;
        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                _isConnected = value;
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(IsNotConnected));
            }
        }
        public bool IsNotConnected { get => !_isConnected; }

        public string LocalHost { get; } = "127.0.0.1";

        private string _packetsToSend = string.Empty;
        public string PacketsToSend
        {
            get => _packetsToSend;
            set
            {
                _packetsToSend = value;
                OnPropertyChanged(nameof(PacketsToSend));
            }
        }
       
        // We store here the incoming/outgoing habbo packets and flush if necessary
        private ObservableCollection<string> _packetLogInboundForUI = new ObservableCollection<string>();
        public ObservableCollection<string> PacketLogInboundForUI
        {
            get => _packetLogInboundForUI;
            set
            {
                    _packetLogInboundForUI = value;
                OnPropertyChanged(nameof(PacketLogInboundForUI));
            }
        }
        private ObservableCollection<string> _packetLogOutboundForUI = new ObservableCollection<string>();
        public ObservableCollection<string> PacketLogOutboundForUI
        {
            get => _packetLogOutboundForUI;
            set
            {
                _packetLogOutboundForUI = value;
                OnPropertyChanged(nameof(PacketLogOutboundForUI));
            }
        }
        // ---------------------------------------------------------------------------

        private CancellationTokenSource _cts;
        private readonly ILogger<MainWindowViewModel> _logger;
        #endregion

        #region Commands
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand SendToClientCommand { get; }
        public ICommand SendToServerCommand { get; }
        #endregion

        public void AddInboundLog(string log) 
        {
            PacketLogOutboundForUI.Add(log);
        }
        public void AddOutbounddLog(string log)
        {
            PacketLogInboundForUI.Add(log);
        }

        public MainWindowViewModel(ITcpInterceptorWorker worker, ILogger<MainWindowViewModel> logger) : base(worker)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ConnectCommand = new RelayCommand(async param => await OnConnect());
            DisconnectCommand = new RelayCommand(param => OnDisconnect());
            SendToClientCommand = new RelayCommand(async param => await OnSendToClient());
            SendToServerCommand = new RelayCommand(async param => await OnSendToServer());
        }

        private async Task OnConnect()
        {
            _cts = new CancellationTokenSource();
            //First pair hotel address to localhost for packet hijacking
            HostEditor.UpdateHostsFile(LocalHost, HotelAddress);
            IsConnected = true;
            Worker.OnAddInboundPacketLog += AddInboundLog;
            Worker.OnAddOutboundPacketLog += AddOutbounddLog;
            await Worker.ExecuteAsync(HotelIP, InfoPort, InfoPort, _decryptPackets, _cts.Token);
        }

        private void OnDisconnect()
        {
            //Restore the hostfile as original
            HostEditor.RestoreHostsFile(LocalHost, HotelAddress);
            _cts.Cancel();
            IsConnected = false;
            Worker.OnAddInboundPacketLog -= AddInboundLog;
            Worker.OnAddOutboundPacketLog -= AddOutbounddLog;
        }

        private async Task OnSendToClient()
        {
            await OnSendToClient(_packetsToSend);
            PacketsToSend = string.Empty;
        }

        private async Task OnSendToServer()
        {
            await OnSendToServer(_packetsToSend);
            PacketsToSend = string.Empty;
        }

    }
}
