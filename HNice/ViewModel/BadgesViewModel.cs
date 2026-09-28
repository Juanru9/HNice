using HNice.Model.Packets;
using HNice.Service;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Shows badges in the client's own badge list (AVAILABLE_BADGES). Local only.
/// Port of SnG Fun v1's "Badges".
/// </summary>
class BadgesViewModel : BaseViewModel
{
    private string _badgeCode = "ADM";
    public string BadgeCode
    {
        get => _badgeCode;
        set { _badgeCode = value; OnPropertyChanged(nameof(BadgeCode)); }
    }

    public ICommand AddBadgeCommand { get; }
    public ICommand AddAllBadgesCommand { get; }

    public BadgesViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        AddBadgeCommand = new RelayCommand(async _ => await AddBadge());
        AddAllBadgesCommand = new RelayCommand(async _ => await OnSendToClient(ClientPacketBuilder.AllBadges()));
    }

    private Task AddBadge() =>
        string.IsNullOrWhiteSpace(BadgeCode) ? Task.CompletedTask : OnSendToClient(ClientPacketBuilder.Badge(BadgeCode));
}
