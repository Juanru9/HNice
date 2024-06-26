using HNice.Service;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HNice.ViewModel
{
    public class MainWindowViewModel : BaseViewModel
    {
        #region Events and delegates
        public EventHandler OnScrollDownDec;
        public EventHandler OnScrollDownEnc;
        #endregion
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
        private bool _encryptPackets = true;
        public bool EncryptPackets
        {
            get => _encryptPackets;
            set
            {
                _encryptPackets = value;
                OnPropertyChanged(nameof(EncryptPackets));
            }
        }
        private bool _pauseEncryptedPackets = false;
        public bool PauseEncryptedPackets
        {
            get => _pauseEncryptedPackets;
            set
            {
                _pauseEncryptedPackets= value;
                OnPropertyChanged(nameof(PauseEncryptedPackets));
            }
        }
        private bool _pauseDecryptedPackets = false;
        public bool PauseDecryptedPackets
        {
            get => _pauseDecryptedPackets;
            set
            {
                _pauseDecryptedPackets = value;
                OnPropertyChanged(nameof(PauseDecryptedPackets));
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

        public string? LocalHost { get; } = "127.0.0.1";

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
        public string PacketLogDecryptedForUI
        {
            get => _packetLogDecryptedForUI;
            set
            {
                if (_packetLogDecryptedForUI.Length > 12000)
                {
                    _packetLogDecryptedForUI = string.Empty;
                }
                _packetLogDecryptedForUI = value;
                OnPropertyChanged(nameof(PacketLogDecryptedForUI));
            }
        }
        private string _packetLogEncryptedForUI = string.Empty;
        public string PacketLogEncryptedForUI
        {
            get => _packetLogEncryptedForUI;
            set
            {
                if (_packetLogEncryptedForUI.Length > 12000)
                {
                    _packetLogEncryptedForUI = string.Empty;
                }
                _packetLogEncryptedForUI = value;
                OnPropertyChanged(nameof(PacketLogEncryptedForUI));
            }
        }
        // ---------------------------------------------------------------------------

        private CancellationTokenSource _cts;
        private readonly ILogger<MainWindowViewModel> _logger;

        private string _packetLogDecryptedForUI = string.Empty;
        #endregion

        #region Commands
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand SendToClientCommand { get; }
        public ICommand SendToServerCommand { get; }
        #endregion

        public void AddEncryptedLog(string log) 
        {
            PacketLogEncryptedForUI += log + Environment.NewLine + "---------------------------------------------------------------------------------" + Environment.NewLine;
            OnScrollDownEnc?.Invoke(null, null);
        }
        public void AddDecryptedLog(string log)
        {
            PacketLogDecryptedForUI += log + Environment.NewLine + "---------------------------------------------------------------------------------" + Environment.NewLine;
            OnScrollDownDec?.Invoke(null, null);
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
            UpdateHostsFile();
            IsConnected = true;
            Worker.OnAddEncryptedLog += AddEncryptedLog;
            Worker.OnAddDecryptedLog += AddDecryptedLog;
            await Worker.ExecuteAsync(HotelIP, InfoPort, InfoPort, _encryptPackets, _cts.Token);
        }

        private void OnDisconnect()
        {
            //Restore the hostfile as original
            RestoreHostsFile();
            _cts.Cancel();
            IsConnected = false;
            Worker.OnAddEncryptedLog -= AddEncryptedLog;
            Worker.OnAddDecryptedLog -= AddDecryptedLog;
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

        //Utils, should move to another static class
        public void UpdateHostsFile()
        {
            try
            {
                string hostsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers/etc/hosts");
                string newLine = $"{LocalHost} {HotelAddress}";

                if (!File.Exists(hostsFilePath))
                {
                    _logger.LogInformation("Hosts file not found.");
                }

                string[] lines = File.ReadAllLines(hostsFilePath);
                if (Array.Exists(lines, line => line.Equals(newLine)))
                {
                    return;
                }

                using (StreamWriter sw = File.AppendText(hostsFilePath))
                {
                    sw.WriteLine(newLine);
                }
                _logger.LogInformation("Hosts file updated successfully.");

            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Error updating hosts file: {ex.Message}");
            }
        }
        public void RestoreHostsFile()
        {
            try
            {
                string hostsFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers/etc/hosts");
                string newLine = $"{LocalHost} {HotelAddress}";

                if (!File.Exists(hostsFilePath))
                {
                    _logger.LogInformation("Hosts file not found.");
                    return;
                }

                string[] lines = File.ReadAllLines(hostsFilePath);
                bool lineExists = lines.Any(line => line.Trim().Equals(newLine, StringComparison.OrdinalIgnoreCase));

                if (lineExists)
                {
                    lines = lines.Where(line => !line.Trim().Equals(newLine, StringComparison.OrdinalIgnoreCase)).ToArray();
                    File.WriteAllLines(hostsFilePath, lines);
                    _logger.LogInformation("Line removed from hosts file.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"Error updating hosts file: {ex.Message}");
            }
        }

    }
}
