using HNice.Model.Packets;
using HNice.Service;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Rewrites how the user's own avatar looks on their own screen by injecting a USER_OBJ packet.
/// Port of SnG Fun v1's "Imitate". Purely local: other players see the real figure.
/// </summary>
class ImitateViewModel : BaseViewModel
{
    private int _userId = 1;
    public int UserId
    {
        get => _userId;
        set { _userId = value; OnPropertyChanged(nameof(UserId)); }
    }

    private string _name = "Guest";
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    private string _figure = "hd-180-1.ch-255-66.lg-280-110.sh-305-62";
    public string Figure
    {
        get => _figure;
        set { _figure = value; OnPropertyChanged(nameof(Figure)); }
    }

    private string _sex = "M";
    public string Sex
    {
        get => _sex;
        set { _sex = value; OnPropertyChanged(nameof(Sex)); }
    }

    private string _mission = "";
    public string Mission
    {
        get => _mission;
        set { _mission = value; OnPropertyChanged(nameof(Mission)); }
    }

    public ICommand ImitateCommand { get; }

    public ImitateViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        ImitateCommand = new RelayCommand(async _ => await Imitate());
    }

    private Task Imitate() =>
        string.IsNullOrWhiteSpace(Figure)
            ? Task.CompletedTask
            : OnSendToClient(ClientPacketBuilder.UserObject(UserId, Name, Figure, Sex, Mission));
}
