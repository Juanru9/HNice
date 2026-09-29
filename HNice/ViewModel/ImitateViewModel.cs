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
    public int UserId { get => _userId; set { _userId = value; OnPropertyChanged(); } }

    private string _name = "Guest";
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

    private string _figure = "hd-180-1.ch-255-66.lg-280-110.sh-305-62";
    public string Figure { get => _figure; set { _figure = value; OnPropertyChanged(); } }

    private string _sex = "M";
    public string Sex { get => _sex; set { _sex = value; OnPropertyChanged(); } }

    private string _mission = "";
    public string Mission { get => _mission; set { _mission = value; OnPropertyChanged(); } }

    public ICommand ImitateCommand { get; }
    public ICommand UseMyAvatarCommand { get; }

    public ImitateViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        ImitateCommand = new RelayCommand(async _ => await Imitate(), _ => !string.IsNullOrWhiteSpace(Figure));
        UseMyAvatarCommand = new RelayCommand(_ => LoadMyAvatar(), _ => Worker.CurrentPlayer is not null);
    }

    // Start from your real avatar, then tweak a field or two.
    private void LoadMyAvatar()
    {
        var me = Worker.CurrentPlayer;
        if (me is null) return;
        UserId = me.UserId ?? UserId;
        Name = me.HabboName ?? Name;
        Figure = me.HabboFigure ?? Figure;
        Sex = me.HabboSex ?? Sex;
        Mission = me.HabboMission ?? Mission;
    }

    private Task Imitate() =>
        OnSendToClient(ClientPacketBuilder.UserObject(UserId, Name, Figure, Sex, Mission));
}
