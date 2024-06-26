﻿using HNice.Model.Packets;
using HNice.Service;
using System.Windows.Input;

namespace HNice.ViewModel;

class CreditsViewModel : BaseViewModel
{
    #region Properties
    private string _credits = "288";
    public string Credits
    {
        get => _credits;
        set
        {
            _credits = value;
            OnPropertyChanged(nameof(Credits));
        }
    }
    private readonly ITcpInterceptorWorker _worker;
    #endregion

    #region Commands
    public ICommand CreditsCommand { get; }
    #endregion

    public CreditsViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        CreditsCommand = new RelayCommand(async param => await OnAddCredits());
    }

    private async Task OnAddCredits() 
    {
        var addCreditVoucherPacket = new IncomingPacket(IncomingPacketMessage.PURSE, new List<string>() { $"{_credits}.0" });
        await OnSendToClient(addCreditVoucherPacket.SerializePacketData());
    }

}
