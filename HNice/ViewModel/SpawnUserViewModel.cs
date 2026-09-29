using HNice.Model.Packets;
using HNice.Service;
using HNice.Util;
using HNice.Util.Extensions;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Spawns fake avatars (bots / clones) in the room the user is viewing, on their own screen only.
/// Uses the real USERS layout captured from the live server, plus a STATUS so they face you.
/// One or many at once, on free floor tiles around you. Port of SnG Fun v1's Bots / Cloning / Pets / Mutants.
/// </summary>
class SpawnUserViewModel : BaseViewModel
{
    private const int MaxBots = 20;
    private const int FirstBotIndex = 100;

    public ObservableCollection<RoomUser> People { get; } = new();

    private RoomUser? _copyFrom;
    /// <summary>Picking someone copies their look into the form.</summary>
    public RoomUser? CopyFrom
    {
        get => _copyFrom;
        set
        {
            _copyFrom = value;
            OnPropertyChanged();
            if (value is null) return;
            Name = value.DisplayName + " 2";
            Figure = value.Figure;
            Sex = value.Gender;
            Motto = value.DisplayMotto;
        }
    }

    private string _name = "Bot";
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }

    private string _figure = "hd-180-1.ch-255-66.lg-280-110.sh-305-62";
    public string Figure { get => _figure; set { _figure = value; OnPropertyChanged(); } }

    private string _motto = "";
    public string Motto { get => _motto; set { _motto = value; OnPropertyChanged(); } }

    private string _sex = "M";
    public string Sex { get => _sex; set { _sex = value; OnPropertyChanged(); } }

    private int _count = 1;
    /// <summary>How many to spawn at once. More than one get numbered names.</summary>
    public int Count { get => _count; set { _count = Math.Clamp(value, 1, MaxBots); OnPropertyChanged(); } }

    private bool _aroundMe = true;
    /// <summary>Gather them on free tiles around you, facing you. Off: around the tile below.</summary>
    public bool AroundMe { get => _aroundMe; set { _aroundMe = value; OnPropertyChanged(); } }

    private int _xCoord = 4;
    public int XCoord { get => _xCoord; set { _xCoord = value; OnPropertyChanged(); } }

    private int _yCoord = 6;
    public int YCoord { get => _yCoord; set { _yCoord = value; OnPropertyChanged(); } }

    private int _roomIndex = FirstBotIndex;
    /// <summary>Room slot for the next bot; goes up with each one so they do not replace each other.</summary>
    public int RoomIndex { get => _roomIndex; set { _roomIndex = value; OnPropertyChanged(); } }

    private bool _countInNavigator = true;
    /// <summary>Your bots are added to this room's user count in the navigator (your screen only).</summary>
    public bool CountInNavigator
    {
        get => _countInNavigator;
        set { _countInNavigator = value; OnPropertyChanged(); Worker.ExtraUsersInRoom = value ? _spawned.Count : 0; }
    }

    private bool _asBot;
    /// <summary>Spawn as a bot (user type 3), the way the server sends NPCs such as Bob. Otherwise as a Habbo.</summary>
    public bool AsBot { get => _asBot; set { _asBot = value; OnPropertyChanged(); } }

    #region Talk
    /// <summary>Bots on screen, for the speaker list. Null speaker = all of them, one after another.</summary>
    public ObservableCollection<RoomUser> Spawned { get; } = new();

    private RoomUser? _speaker;
    public RoomUser? Speaker { get => _speaker; set { _speaker = value; OnPropertyChanged(); } }

    private string _sayText = string.Empty;
    public string SayText { get => _sayText; set { _sayText = value; OnPropertyChanged(); } }

    public ICommand SayCommand { get; }
    public ICommand ShoutCommand { get; }
    public ICommand ClearSpeakerCommand { get; }
    #endregion

    private string _message = string.Empty;
    public string Message { get => _message; private set { _message = value; OnPropertyChanged(); } }

    // Bots on screen now: their slot and tile.
    private readonly List<RoomUser> _spawned = new();
    public int SpawnedCount => _spawned.Count;

    public ICommand SpawnCommand { get; }
    public ICommand CloneMeCommand { get; }
    public ICommand RemoveBotsCommand { get; }
    public ICommand SetCountCommand { get; }

    public SpawnUserViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        SpawnCommand = new RelayCommand(async _ => await Spawn(), _ => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Figure));
        CloneMeCommand = new RelayCommand(_ => CloneMe(), _ => Worker.CurrentPlayer is not null);
        RemoveBotsCommand = new RelayCommand(async _ => await RemoveBots(), _ => _spawned.Count > 0);
        SetCountCommand = new RelayCommand(p => { if (int.TryParse(p as string, out var n)) Count = n; });
        SayCommand = new RelayCommand(async _ => await Say(shout: false), _ => CanSay);
        ShoutCommand = new RelayCommand(async _ => await Say(shout: true), _ => CanSay);
        ClearSpeakerCommand = new RelayCommand(_ => Speaker = null);

        Worker.RoomUsersChanged += () => Application.Current?.Dispatcher.BeginInvoke(RefreshPeople);
        RefreshPeople();
    }

    private void RefreshPeople()
    {
        People.Clear();
        foreach (var user in Worker.RoomUsers.Where(u => u.IsPlayer)) People.Add(user);

        // A new room clears the client's avatars, ours included.
        if (Worker.MyStatus is null && _spawned.Count > 0)
        {
            _spawned.Clear();
            Spawned.Clear();
            RoomIndex = FirstBotIndex;
            OnPropertyChanged(nameof(SpawnedCount));
        }
    }

    // Copies your own look, the old "Cloning" form.
    private void CloneMe()
    {
        var me = Worker.CurrentPlayer;
        if (me is null) return;
        Figure = me.HabboFigure ?? Figure;
        Sex = me.HabboSex ?? Sex;
        Name = PacketText.FromWire(me.HabboName ?? "Clone") + " 2";
        Motto = PacketText.FromWire(me.HabboMission ?? string.Empty);
    }

    private async Task Spawn()
    {
        var me = Worker.MyStatus;
        if (AroundMe && me is null)
        {
            Message = "Your avatar was not found in the room yet. Walk one step, or turn off Around me and give a tile.";
            return;
        }

        var center = AroundMe ? (me!.X, me.Y) : (XCoord, YCoord);
        if (!AroundMe && Count == 1 && Worker.IsFloorTile(XCoord, YCoord) == false)
        {
            Message = $"Tile {XCoord},{YCoord} is not floor in this room. Pick another one, or use Around me.";
            return;
        }

        // Free = floor (or map not known yet) and nobody standing there, bots included.
        var taken = Worker.RoomUsers.Select(u => (u.X, u.Y)).Concat(_spawned.Select(b => (b.X, b.Y))).ToHashSet();
        if (me is not null) taken.Add((me.X, me.Y));
        bool IsFree(int x, int y) => Worker.IsFloorTile(x, y) != false && !taken.Contains((x, y));

        var spots = BotPlacement.Spots(center, Count, includeCenter: !AroundMe, IsFree, me is null ? null : (me.X, me.Y));
        if (spots.Count == 0)
        {
            Message = "No free floor tiles around there.";
            return;
        }

        var bots = spots.Select((spot, i) =>
        {
            var name = Count == 1 ? Name : $"{Name} {i + 1}";
            var index = RoomIndex + i;
            return new RoomUser(index, 999000000 + index, PacketText.ToWire(name), Figure, Sex, PacketText.ToWire(Motto), string.Empty, spot.X, spot.Y,
                AsBot ? RoomUserKind.Bot : RoomUserKind.Player);
        }).ToList();

        await OnSendToClient(RoomUsers.Build(bots));
        // The status gives each one its direction, like a real user standing still.
        await OnSendToClient(RoomStatus.Build(bots.Zip(spots, (b, s) => new StatusEntry(b.Index, b.X, b.Y, "0.0", s.Rotation, s.Rotation, "/")).ToList()));

        _spawned.AddRange(bots);
        foreach (var bot in bots) Spawned.Add(bot);
        RoomIndex += bots.Count;
        if (CountInNavigator) Worker.ExtraUsersInRoom = _spawned.Count;
        OnPropertyChanged(nameof(SpawnedCount));
        CommandManager.InvalidateRequerySuggested();

        var where = bots.Count == 1 ? $"at {bots[0].X},{bots[0].Y}" : AroundMe ? "around you" : $"around {XCoord},{YCoord}";
        Message = bots.Count < Count
            ? $"Only {bots.Count} free tiles nearby: spawned {bots.Count} of {Count} {where}."
            : $"Spawned {(bots.Count == 1 ? bots[0].DisplayName : $"{bots.Count} bots")} {where}.";
    }

    private bool CanSay => _spawned.Count > 0 && !string.IsNullOrWhiteSpace(SayText);

    // CHAT (@X) or shout (@Z, CHAT_3): index(VL64) text[2], exactly as the server sends it for Bob.
    // The "talk" status moves the mouth while the bubble is up.
    private async Task Say(bool shout)
    {
        var speakers = Speaker is { } one ? new List<RoomUser> { one } : _spawned.ToList();
        var header = shout ? "@Z" : "@X";
        var text = PacketText.ToWire(SayText.Trim());

        foreach (var bot in speakers)
        {
            await OnSendToClient(header + bot.Index.EncodeVL64() + text + Constants.PACKET_SPLITTER);
            await OnSendToClient(RoomStatus.Build(new[] { new StatusEntry(bot.Index, bot.X, bot.Y, "0.0", FacingOf(bot), FacingOf(bot), "/talk/") }));
            _ = QuietLaterAsync(bot);
            if (speakers.Count > 1) await Task.Delay(TimeSpan.FromMilliseconds(400));
        }
        Message = speakers.Count == 1 ? $"{speakers[0].DisplayName} said it." : $"{speakers.Count} bots said it.";
        SayText = string.Empty;
    }

    private async Task QuietLaterAsync(RoomUser bot)
    {
        await Task.Delay(TimeSpan.FromSeconds(3));
        if (!_spawned.Contains(bot)) return;
        await OnSendToClient(RoomStatus.Build(new[] { new StatusEntry(bot.Index, bot.X, bot.Y, "0.0", FacingOf(bot), FacingOf(bot), "/") }));
    }

    // Spawned bots face you; keep that when their status is sent again.
    private int FacingOf(RoomUser bot) => Worker.MyStatus is { } me ? BotPlacement.Facing((bot.X, bot.Y), (me.X, me.Y)) : 2;

    private async Task RemoveBots()
    {
        // LOGOUT (@]) with the slot as text, as the server sends when someone leaves.
        foreach (var bot in _spawned)
        {
            await OnSendToClient("@]" + bot.Index);
        }
        Message = $"Removed {_spawned.Count} bot{(_spawned.Count == 1 ? "" : "s")}.";
        _spawned.Clear();
        Spawned.Clear();
        RoomIndex = FirstBotIndex;
        Worker.ExtraUsersInRoom = 0;
        OnPropertyChanged(nameof(SpawnedCount));
        CommandManager.InvalidateRequerySuggested();
    }
}
