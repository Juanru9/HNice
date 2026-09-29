using HNice.Model.Packets;
using HNice.Service;
using HNice.Util;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Copies someone's look onto your own avatar, on your own screen (USER_OBJ to the client).
/// Pick a person by clicking them in the game or from the room list. Port of SnG Fun v1's "Imitate".
/// Purely local: other players see your real avatar.
/// </summary>
class ImitateViewModel : BaseViewModel
{
    public ObservableCollection<RoomUser> People { get; } = new();

    private RoomUser? _selectedPerson;
    public RoomUser? SelectedPerson
    {
        get => _selectedPerson;
        set
        {
            _selectedPerson = value;
            OnPropertyChanged();
            if (value is not null) LoadLook(value, clickedInGame: false);
        }
    }

    private bool _pickFromGame = true;
    /// <summary>Clicking a Habbo in the game loads their look here.</summary>
    public bool PickFromGame { get => _pickFromGame; set { _pickFromGame = value; OnPropertyChanged(); } }

    private bool _applyOnPick;
    /// <summary>Wear the look straight away when a Habbo is clicked.</summary>
    public bool ApplyOnPick { get => _applyOnPick; set { _applyOnPick = value; OnPropertyChanged(); } }

    private string _pickMessage = "Click a Habbo in the game, or choose someone from the list.";
    public string PickMessage { get => _pickMessage; private set { _pickMessage = value; OnPropertyChanged(); } }

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

    /// <summary>Sends the real wardrobe save with this figure: everyone sees it if the server accepts it.</summary>
    public ICommand WearForRealCommand { get; }

    // Set while waiting for the server's answer to a wardrobe save.
    private string? _pendingRealFigure;

    public ImitateViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        ImitateCommand = new RelayCommand(async _ => await Imitate(), _ => !string.IsNullOrWhiteSpace(Figure));
        UseMyAvatarCommand = new RelayCommand(_ => LoadMyAvatar(), _ => Worker.CurrentPlayer is not null);
        WearForRealCommand = new RelayCommand(async _ => await WearForReal(), _ => CanWearForReal());
        Worker.PlayerChanged += player => OnUi(() => OnServerLook(player.HabboFigure));

        Worker.RoomUsersChanged += () => OnUi(RefreshPeople);
        Worker.UserPicked += user => OnUi(() => OnUserPicked(user));
        RefreshPeople();
    }

    private void RefreshPeople()
    {
        var selectedIndex = _selectedPerson?.Index;
        People.Clear();
        foreach (var user in Worker.RoomUsers.Where(u => u.IsPlayer)) People.Add(user);
        _selectedPerson = People.FirstOrDefault(u => u.Index == selectedIndex);
        OnPropertyChanged(nameof(SelectedPerson));
    }

    private async void OnUserPicked(RoomUser user)
    {
        if (!PickFromGame) return;
        if (!user.IsPlayer)
        {
            PickMessage = $"{user.ListName} has no Habbo look to copy.";
            return;
        }

        _selectedPerson = People.FirstOrDefault(u => u.Index == user.Index);
        OnPropertyChanged(nameof(SelectedPerson));
        LoadLook(user, clickedInGame: true);

        if (ApplyOnPick) await Imitate();
    }

    private void LoadLook(RoomUser user, bool clickedInGame)
    {
        Name = user.DisplayName;
        Figure = user.Figure;
        Sex = user.Gender;
        Mission = user.DisplayMotto;
        if (Worker.CurrentPlayer?.UserId is int me) UserId = me;

        PickMessage = clickedInGame
            ? $"Picked {user.DisplayName} from the game." + (ApplyOnPick ? " Look applied." : " Press Apply look to wear it.")
            : $"Loaded {user.DisplayName}'s look.";
    }

    // Start from your real avatar, then tweak a field or two.
    private void LoadMyAvatar()
    {
        var me = Worker.CurrentPlayer;
        if (me is null) return;
        UserId = me.UserId ?? UserId;
        Name = PacketText.FromWire(me.HabboName ?? Name);
        Figure = me.HabboFigure ?? Figure;
        Sex = me.HabboSex ?? Sex;
        Mission = PacketText.FromWire(me.HabboMission ?? Mission);
        PickMessage = "Loaded your own look.";
    }

    private async Task Imitate()
    {
        // USER_OBJ updates your own panels (info, hand); your id keeps it applied to you and the rest of
        // your real USER_OBJ is kept as-is.
        var userId = UserId == 1 && Worker.CurrentPlayer?.UserId is int me ? me : UserId;
        await OnSendToClient(ClientPacketBuilder.UserObject(
            userId, PacketText.ToWire(Name), Figure, Sex.ToUpperInvariant(), PacketText.ToWire(Mission), MyUserObjectTail()));

        // The avatar standing in the room only changes with the "changed clothes" packet for your room slot.
        if (Worker.MyRoomIndex is int slot)
        {
            await OnSendToClient(ClientPacketBuilder.UserLook(slot, Figure, Sex, PacketText.ToWire(Mission)));
        }
        else
        {
            PickMessage = "Your panels changed, but your avatar in the room was not found yet. Re-enter the room and try again.";
        }
    }

    // Everything after the motto in your real USER_OBJ, so tickets, film and flags stay untouched.
    private string? MyUserObjectTail()
    {
        var raw = Worker.CurrentPlayer?.RawBody;
        if (string.IsNullOrEmpty(raw) || raw.Contains('=')) return null;
        try
        {
            var reader = new IncomingPacketReader(raw);
            reader.ReadInt();
            for (var i = 0; i < 4; i++) reader.ReadString();
            return reader.Remaining;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    #region Wear for real
    // The wardrobe save carries only the figure; your gender stays as it is, so only same-gender looks fit.
    private bool CanWearForReal() =>
        Worker.CurrentPlayer is { } me
        && !string.IsNullOrWhiteSpace(Figure)
        && string.Equals(me.HabboSex, Sex, StringComparison.OrdinalIgnoreCase)
        && _pendingRealFigure is null;

    private async Task WearForReal()
    {
        _pendingRealFigure = Figure.Trim();
        PickMessage = "Sent to the server as a wardrobe save. Waiting for its answer...";
        await OnSendToServer(ServerPacketBuilder.WearFigure(_pendingRealFigure));

        // No answer within a few seconds: say so instead of waiting forever.
        await Task.Delay(TimeSpan.FromSeconds(5));
        if (_pendingRealFigure is not null)
        {
            _pendingRealFigure = null;
            PickMessage = "The server did not answer the wardrobe save. Your look is unchanged.";
            CommandManager.InvalidateRequerySuggested();
        }
    }

    // The server answers a wardrobe save with your USER_OBJ: that is the look everyone sees now.
    private void OnServerLook(string? serverFigure)
    {
        if (_pendingRealFigure is null || serverFigure is null) return;

        var wanted = FigureParts(_pendingRealFigure);
        var applied = FigureParts(serverFigure);
        var missing = wanted.Except(applied).ToList();

        PickMessage = missing.Count == 0
            ? "The server accepted it: everyone now sees this look."
            : $"The server applied it without: {string.Join(", ", missing)} (not available to your account).";
        _pendingRealFigure = null;
        CommandManager.InvalidateRequerySuggested();
    }

    private static HashSet<string> FigureParts(string figure) =>
        figure.Split('.', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
    #endregion

    private static void OnUi(Action action) => Application.Current?.Dispatcher.BeginInvoke(action);
}
