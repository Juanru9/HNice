using HNice.Model.Packets;
using HNice.Service;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Sends a fake RIGHTS packet listing fuse_* permissions so the client shows the matching UI (e.g. mod tools).
/// Port of SnG Fun v1's "Custom Fuse". Client-side only: the server still enforces real permissions, so
/// any action attempted through the revealed UI is rejected server-side.
/// </summary>
class FuseViewModel : BaseViewModel
{
    // The real base rights the server grants a normal account (captured live), plus common mod-tool rights.
    // The client replaces its whole rights set from this packet, so the base ones are kept to avoid losing them.
    private string _permissions = string.Join(Environment.NewLine, new[]
    {
        // Base rights (from the live RIGHTS packet):
        "fuse_login",
        "fuse_buy_credits",
        "fuse_trade",
        "fuse_create_flat",
        "fuse_ignore_room_owner",
        "fuse_habbo_chooser",
        "fuse_furni_chooser",
        // Mod / staff rights that reveal extra client UI:
        "fuse_mod",
        "fuse_receive_calls_for_help",
        "fuse_alert",
        "fuse_kick",
        "fuse_ban",
        "fuse_room_mute",
        "fuse_super_ban",
        "fuse_supporter",
    });
    public string Permissions
    {
        get => _permissions;
        set { _permissions = value; OnPropertyChanged(nameof(Permissions)); }
    }

    public ICommand SendFuseCommand { get; }

    public FuseViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        SendFuseCommand = new RelayCommand(async _ => await SendFuse());
    }

    private Task SendFuse()
    {
        var permissions = Permissions
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (permissions.Length == 0)
            return Task.CompletedTask;

        return OnSendToClient(ClientPacketBuilder.FuseRights(permissions));
    }
}
