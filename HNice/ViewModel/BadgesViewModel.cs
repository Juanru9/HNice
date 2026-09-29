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
    /// <summary>Common classic badge codes, one click to pick.</summary>
    public IReadOnlyList<string> PresetBadges { get; } = new[]
    {
        "ADM", "HC1", "HC2", "NWB", "VIP", "XXX", "EXH", "DU1", "UK1", "US1", "ES1", "FI1", "NL1", "DE1", "SE1", "PIR",
    };

    private string _badgeCode = "ADM";
    public string BadgeCode
    {
        get => _badgeCode;
        set { _badgeCode = value; OnPropertyChanged(); }
    }

    public ICommand AddBadgeCommand { get; }
    public ICommand AddAllBadgesCommand { get; }
    public ICommand PickBadgeCommand { get; }

    public BadgesViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        AddBadgeCommand = new RelayCommand(async _ => await AddBadge(), _ => !string.IsNullOrWhiteSpace(BadgeCode));
        AddAllBadgesCommand = new RelayCommand(async _ => await OnSendToClient(ClientPacketBuilder.AllBadges()));
        PickBadgeCommand = new RelayCommand(p => { if (p is string s) BadgeCode = s; });
    }

    private Task AddBadge() =>
        string.IsNullOrWhiteSpace(BadgeCode) ? Task.CompletedTask : OnSendToClient(ClientPacketBuilder.Badge(BadgeCode.Trim().ToUpperInvariant()));
}
