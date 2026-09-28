using HNice.Service;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Sends a raw packet straight to the client or the server. Header + body, no length prefix and no
/// chr(1) ender (both are added by the worker). Port of SnG Fun v1's packet sender / quick send.
/// </summary>
class PacketSenderViewModel : BaseViewModel
{
    private string _packet = string.Empty;
    public string Packet
    {
        get => _packet;
        set { _packet = value; OnPropertyChanged(nameof(Packet)); }
    }

    public ICommand SendToClientCommand { get; }
    public ICommand SendToServerCommand { get; }

    public PacketSenderViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        SendToClientCommand = new RelayCommand(async _ => await OnSendToClient(Packet));
        SendToServerCommand = new RelayCommand(async _ => await OnSendToServer(Packet));
    }
}
