using HNice.Model.Packets;
using HNice.Service;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>One fuse permission in the checklist.</summary>
public sealed class FusePermission : BaseViewModel
{
    public string Name { get; }

    /// <summary>Granted to every account by the server; kept so the client does not lose them.</summary>
    public bool IsBase { get; }

    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

    public FusePermission(string name, bool isBase, bool isSelected)
    {
        Name = name;
        IsBase = isBase;
        _isSelected = isSelected;
    }
}

/// <summary>
/// Sends a fake RIGHTS packet so the client shows staff UI. Client-side only: the server still
/// enforces the real permissions, so staff actions themselves are rejected.
/// </summary>
class FuseViewModel : BaseViewModel
{
    public ObservableCollection<FusePermission> BasePermissions { get; } = new();
    public ObservableCollection<FusePermission> StaffPermissions { get; } = new();

    private string _customPermission = string.Empty;
    public string CustomPermission { get => _customPermission; set { _customPermission = value; OnPropertyChanged(); } }

    public ICommand SendFuseCommand { get; }
    public ICommand AddCustomCommand { get; }

    public FuseViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        // Base rights captured from the live RIGHTS packet.
        foreach (var name in new[] { "fuse_login", "fuse_buy_credits", "fuse_trade", "fuse_create_flat", "fuse_ignore_room_owner", "fuse_habbo_chooser", "fuse_furni_chooser" })
        {
            BasePermissions.Add(new FusePermission(name, isBase: true, isSelected: true));
        }
        foreach (var name in new[] { "fuse_mod", "fuse_receive_calls_for_help", "fuse_alert", "fuse_kick", "fuse_ban", "fuse_room_mute", "fuse_super_ban", "fuse_supporter" })
        {
            StaffPermissions.Add(new FusePermission(name, isBase: false, isSelected: true));
        }

        SendFuseCommand = new RelayCommand(async _ => await SendFuse());
        AddCustomCommand = new RelayCommand(_ => AddCustom(), _ => !string.IsNullOrWhiteSpace(CustomPermission));
    }

    private void AddCustom()
    {
        var name = CustomPermission.Trim();
        if (!name.StartsWith("fuse_")) name = "fuse_" + name;
        if (StaffPermissions.All(p => p.Name != name) && BasePermissions.All(p => p.Name != name))
        {
            StaffPermissions.Add(new FusePermission(name, isBase: false, isSelected: true));
        }
        CustomPermission = string.Empty;
    }

    private Task SendFuse()
    {
        var permissions = BasePermissions.Concat(StaffPermissions).Where(p => p.IsSelected).Select(p => p.Name).ToList();
        return permissions.Count == 0 ? Task.CompletedTask : OnSendToClient(ClientPacketBuilder.FuseRights(permissions));
    }
}
