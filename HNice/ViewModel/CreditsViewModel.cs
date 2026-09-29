using HNice.Model.Packets;
using HNice.Service;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>Shows a custom balance in your own purse (PURSE packet to the client). Local only.</summary>
class CreditsViewModel : BaseViewModel
{
    public IReadOnlyList<string> Presets { get; } = new[] { "100", "1000", "25000", "999999" };

    private string _credits = "288";
    public string Credits
    {
        get => _credits;
        set { _credits = value; OnPropertyChanged(); }
    }

    public ICommand CreditsCommand { get; }
    public ICommand PresetCommand { get; }

    public CreditsViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        CreditsCommand = new RelayCommand(async _ => await OnAddCredits(), _ => int.TryParse(Credits, out var c) && c >= 0);
        PresetCommand = new RelayCommand(p => { if (p is string s) Credits = s; });
    }

    private async Task OnAddCredits()
    {
        var addCreditVoucherPacket = new IncomingPacket(IncomingPacketMessage.PURSE, new List<string>() { $"{_credits}.0" });
        await OnSendToClient(addCreditVoucherPacket.SerializePacketData());
    }
}
