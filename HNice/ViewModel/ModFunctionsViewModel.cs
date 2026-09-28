using HNice.Model.Packets;
using HNice.Service;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Fake staff messages shown on the user's OWN client only (broadcasts, alerts, mod warnings).
/// Port of SnG Fun v1's "MOD Functions". These inject incoming packets toward the client; they do not
/// touch the server and grant no real moderator power.
/// </summary>
class ModFunctionsViewModel : BaseViewModel
{
    private string _broadcast = "Hello from HNice!";
    public string Broadcast
    {
        get => _broadcast;
        set { _broadcast = value; OnPropertyChanged(nameof(Broadcast)); }
    }

    private string _modWarning = "Please mind the rules.";
    public string ModWarning
    {
        get => _modWarning;
        set { _modWarning = value; OnPropertyChanged(nameof(ModWarning)); }
    }

    public ICommand SendBroadcastCommand { get; }
    public ICommand SendModWarningCommand { get; }

    public ModFunctionsViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        SendBroadcastCommand = new RelayCommand(async _ => await SendBroadcast());
        SendModWarningCommand = new RelayCommand(async _ => await SendModWarning());
    }

    private Task SendBroadcast() =>
        string.IsNullOrEmpty(Broadcast) ? Task.CompletedTask : OnSendToClient(ClientPacketBuilder.Broadcast(Broadcast));

    private Task SendModWarning() =>
        string.IsNullOrEmpty(ModWarning) ? Task.CompletedTask : OnSendToClient(ClientPacketBuilder.Alert("mod_warn/" + ModWarning));
}
