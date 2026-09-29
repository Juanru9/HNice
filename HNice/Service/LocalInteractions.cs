using HNice.Model.Packets;
using HNice.Util.Extensions;
using System.Text;

namespace HNice.Service;

/// <summary>What to do with one outgoing client packet.</summary>
public sealed class OutboundDecision
{
    /// <summary>True when the packet must not reach the server (it targets an item the server does not have).</summary>
    public bool Drop { get; init; }

    /// <summary>Packets to show on the client right away.</summary>
    public List<string> InjectNow { get; } = new();

    /// <summary>Packets to show on the client after a delay (e.g. the machine animation ending).</summary>
    public List<(string Packet, TimeSpan Delay)> InjectLater { get; } = new();

    public static readonly OutboundDecision Forward = new();
}

/// <summary>
/// Follows the room as the client sees it and answers clicks on HNice's own furni.
///
/// Room tracking (all from live captures):
///   ← USERS (@\)    people in the room, including you (matched by user id)
///   ← STATUS (@b)   positions; ← LOGOUT (@]) someone left; ← 266 someone changed clothes
///   ← ROOM_READY    a new room: start over
///   → LOOKTO x y    sent when you click a Habbo: the person on that tile is "picked"
///
/// Fake furni (the server rejects requests for items it does not have, so they are answered locally):
///   → SETSTUFFDATA &lt;item&gt; TRUE, → CARRYDRINK &lt;drink&gt;
///   ← STUFFDATAUPDATE &lt;item&gt; TRUE … FALSE, ← STATUS …/carryd &lt;drink&gt;/
/// Only your client sees the result.
/// </summary>
public sealed class LocalInteractions
{
    private const int SetStuffData = (int)OutcomingPacketMessage.SETSTUFFDATA;
    private const int CarryDrink = (int)OutcomingPacketMessage.CARRYDRINK;
    private const int LookTo = (int)OutcomingPacketMessage.LOOKTO;
    private const int Move = (int)OutcomingPacketMessage.MOVE;
    private const int Status = (int)IncomingPacketMessage.STATUS;
    private const int Users = (int)IncomingPacketMessage.USERS;
    private const int Logout = (int)IncomingPacketMessage.LOGOUT;
    private const int RoomReady = (int)IncomingPacketMessage.ROOM_READY;
    private const int Heightmap = (int)IncomingPacketMessage.HEIGHTMAP; // rows separated by CR, "x" = no floor
    private const int RoomsList = 351;                                   // navigator room lists (see NavigatorRooms)
    private const int NavNode = (int)IncomingPacketMessage.NAVNODEINFO;
    private const int UserLookChanged = 266; // index(VL64) figure[2] sex[2] motto[2]
    private const int WalkTo = 1269;         // the client's walk request: x(VL64) y(VL64)

    private static readonly TimeSpan PendingUseWindow = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan SelfStatusWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan DrinkWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CarryDuration = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan MachineAnimation = TimeSpan.FromMilliseconds(1500);

    private static readonly Encoding Latin1 = Encoding.Latin1;

    private readonly object _sync = new();
    private readonly HashSet<string> _fakeItems = new();
    private readonly Dictionary<int, RoomUser> _users = new();
    private string[] _heightmap = Array.Empty<string>();

    // ROOM_READY body: "model_e 67533" = the room's model and its flat id.
    private int? _flatId;
    private int _extraUsers;

    /// <summary>A drink machine placed by HNice: where it stands and the tile you use it from.</summary>
    private sealed record FakeMachine(string Id, int X, int Y, int Rotation, string Drink)
    {
        public (int X, int Y) Front => Rotation switch
        {
            0 => (X, Y - 1),
            2 => (X + 1, Y),
            4 => (X, Y + 1),
            _ => (X - 1, Y),
        };
    }

    private readonly Dictionary<string, FakeMachine> _machines = new();

    // You clicked a machine from afar: the client walks you to its front tile first.
    private FakeMachine? _pendingUse;
    private DateTime _pendingUseUntil;

    private int? _myUserId;
    private string? _myName;
    private int? _myIndex;
    private StatusEntry? _myStatus;
    private DateTime _expectSelfStatusUntil;

    private string? _lastFakeItem;
    private DateTime _lastFakeUseAt;

    private string? _carriedDrink;
    private DateTime _carryUntil;

    /// <summary>You clicked this Habbo in the game.</summary>
    public event Action<RoomUser>? UserPicked;

    /// <summary>People entered, left, moved rooms or changed clothes.</summary>
    public event Action? UsersChanged;

    /// <summary>Your avatar's slot in the current room (from USERS), once identified.</summary>
    public int? MyRoomIndex { get { lock (_sync) return _myIndex; } }

    /// <summary>Your avatar's latest position/rotation, once it has been identified in the room.</summary>
    public StatusEntry? MyStatus { get { lock (_sync) return _myStatus; } }

    /// <summary>
    /// Added to the current room's user count in the navigator, on your screen only (e.g. the bots you spawned).
    /// Reset when you change rooms.
    /// </summary>
    public int ExtraUsers
    {
        get { lock (_sync) return _extraUsers; }
        set { lock (_sync) _extraUsers = Math.Max(0, value); }
    }

    /// <summary>True when the tile is floor in the current room, false when it is not, null before the room map arrived.</summary>
    public bool? IsFloorTile(int x, int y)
    {
        lock (_sync)
        {
            if (_heightmap.Length == 0) return null;
            return y >= 0 && y < _heightmap.Length && x >= 0 && x < _heightmap[y].Length
                && char.ToLowerInvariant(_heightmap[y][x]) != 'x';
        }
    }

    /// <summary>Everyone in the room except you, sorted by name.</summary>
    public IReadOnlyList<RoomUser> OtherUsers
    {
        get
        {
            lock (_sync)
            {
                return _users.Values.Where(u => u.Index != _myIndex)
                    .OrderBy(u => u.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
    }

    public void RegisterFakeItem(string itemId)
    {
        lock (_sync) _fakeItems.Add(itemId);
    }

    /// <summary>A drink machine placed by HNice. <paramref name="drink"/> is handed out when you use it.</summary>
    public void RegisterFakeMachine(string itemId, int x, int y, int rotation, string drink)
    {
        lock (_sync)
        {
            _fakeItems.Add(itemId);
            _machines[itemId] = new FakeMachine(itemId, x, y, rotation, drink);
        }
    }

    /// <summary>Who you are (from USER_OBJ), so your own entry in USERS can be recognised.</summary>
    public void SetMe(int? userId, string? name)
    {
        lock (_sync)
        {
            _myUserId = userId;
            _myName = name;
            foreach (var user in _users.Values)
            {
                if (IsMe(user)) _myIndex = user.Index;
            }
        }
        UsersChanged?.Invoke();
    }

    /// <summary>New connection: new room, nothing known yet.</summary>
    public void Reset()
    {
        lock (_sync)
        {
            ClearRoom();
            _lastFakeItem = null;
        }
        UsersChanged?.Invoke();
    }

    #region Outbound
    /// <param name="packet">Plain client packet (header + body).</param>
    public OutboundDecision HandleOutbound(byte[] packet)
    {
        if (packet.Length < 2) return OutboundDecision.Forward;
        var text = Latin1.GetString(packet);
        var header = text[..2].DecodeB64();

        RoomUser? picked = null;
        OutboundDecision decision;

        lock (_sync)
        {
            switch (header)
            {
                case LookTo:
                    // Clicking a Habbo makes you turn toward their tile: that is who was clicked.
                    picked = UserOnTile(text[2..]);
                    _expectSelfStatusUntil = DateTime.UtcNow + SelfStatusWindow;
                    decision = OutboundDecision.Forward;
                    break;

                case Move:
                    // The server answers with a STATUS about you: a fallback way to learn which avatar is yours.
                    _expectSelfStatusUntil = DateTime.UtcNow + SelfStatusWindow;
                    decision = OutboundDecision.Forward;
                    break;

                case WalkTo when TryReadTile(text[2..], out var target) && MachineUsedFrom(target) is { } machine:
                    // Clicking a machine makes the client walk you to its front tile before using it.
                    // Captured: with a fake machine the client stops there and never sends the use request.
                    if (_myStatus is { } me && (me.X, me.Y) == target)
                    {
                        decision = UseMachine(machine);
                    }
                    else
                    {
                        _pendingUse = machine;
                        _pendingUseUntil = DateTime.UtcNow + PendingUseWindow;
                        decision = OutboundDecision.Forward;
                    }
                    break;

                case SetStuffData when TryReadB64Strings(text, 2, out var args) && args.Count >= 2 && _fakeItems.Contains(args[0]):
                    _lastFakeItem = args[0];
                    _lastFakeUseAt = DateTime.UtcNow;
                    decision = new OutboundDecision { Drop = true };
                    decision.InjectNow.Add(StuffDataUpdate(args[0], args[1]));
                    break;

                case CarryDrink when TryReadB64Strings(text, 2, out var drink) && drink.Count >= 1:
                    decision = HandleCarryDrink(drink[0]);
                    break;

                default:
                    decision = OutboundDecision.Forward;
                    break;
            }
        }

        if (picked is not null) UserPicked?.Invoke(picked);
        return decision;
    }

    /// <summary>What a real machine does, played locally: animation, then the drink in your hand.</summary>
    private OutboundDecision UseMachine(FakeMachine machine)
    {
        _pendingUse = null;
        _carriedDrink = machine.Drink;
        _carryUntil = DateTime.UtcNow + CarryDuration;

        var decision = new OutboundDecision();
        decision.InjectNow.Add(StuffDataUpdate(machine.Id, "TRUE"));
        if (_myStatus is not null)
        {
            decision.InjectNow.Add(RoomStatus.Build(new[] { _myStatus with { Actions = RoomStatus.WithCarriedDrink(_myStatus.Actions, machine.Drink) } }));
        }
        decision.InjectLater.Add((StuffDataUpdate(machine.Id, "FALSE"), MachineAnimation));
        return decision;
    }

    private FakeMachine? MachineUsedFrom((int X, int Y) tile) =>
        _machines.Values.FirstOrDefault(m => m.Front == tile);

    // Walk request body: x(VL64) y(VL64)
    private static bool TryReadTile(string body, out (int X, int Y) tile)
    {
        tile = default;
        try
        {
            var reader = new IncomingPacketReader(body);
            tile = (reader.ReadInt(), reader.ReadInt());
            return !reader.HasMore;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private OutboundDecision HandleCarryDrink(string drink)
    {
        var fromFakeMachine = _lastFakeItem is not null && DateTime.UtcNow - _lastFakeUseAt <= DrinkWindow;
        if (!fromFakeMachine)
        {
            // A real machine: the server hands the drink out, stop pretending.
            _carriedDrink = null;
            return OutboundDecision.Forward;
        }

        _carriedDrink = drink;
        _carryUntil = DateTime.UtcNow + CarryDuration;

        var decision = new OutboundDecision { Drop = true };
        decision.InjectLater.Add((StuffDataUpdate(_lastFakeItem!, "FALSE"), MachineAnimation));
        if (_myStatus is not null)
        {
            decision.InjectNow.Add(RoomStatus.Build(new[] { _myStatus with { Actions = RoomStatus.WithCarriedDrink(_myStatus.Actions, drink) } }));
        }
        _lastFakeItem = null;
        return decision;
    }

    // LOOKTO body: "x y"
    private RoomUser? UserOnTile(string body)
    {
        var parts = body.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var x) || !int.TryParse(parts[1], out var y)) return null;
        return _users.Values.FirstOrDefault(u => u.X == x && u.Y == y && u.Index != _myIndex);
    }
    #endregion

    #region Inbound
    /// <summary>Updates the room picture and keeps a locally given drink in your hand.</summary>
    /// <param name="inject">Receives packets to show on the client (delay zero = right after this one).</param>
    public byte[] RewriteInbound(byte[] packet, List<(string Packet, TimeSpan Delay)>? inject = null)
    {
        if (packet.Length < 2) return packet;
        var text = Latin1.GetString(packet);
        var header = text[..2].DecodeB64();
        var body = text[2..];

        var usersChanged = false;
        byte[] result = packet;

        lock (_sync)
        {
            switch (header)
            {
                case RoomReady:
                    ClearRoom();
                    _flatId = body.Split(' ') is [_, var id] && int.TryParse(id, out var flatId) ? flatId : null;
                    usersChanged = true;
                    break;

                case RoomsList or NavNode when _extraUsers > 0 && _flatId is { } current
                                               && NavigatorRooms.TryAddUsers(body, current, _extraUsers, out var rewritten):
                    result = Latin1.GetBytes(text[..2] + rewritten);
                    break;

                case Heightmap:
                    _heightmap = body.Split('\r', StringSplitOptions.RemoveEmptyEntries);
                    break;

                case Users when RoomUsers.TryParse(body, out var users):
                    foreach (var user in users)
                    {
                        _users[user.Index] = user;
                        if (IsMe(user)) _myIndex = user.Index;
                    }
                    usersChanged = users.Count > 0;
                    break;

                case Logout when int.TryParse(body.Trim(), out var leftIndex):
                    usersChanged = _users.Remove(leftIndex);
                    break;

                case UserLookChanged:
                    usersChanged = ApplyLookChange(body);
                    break;

                case Status when RoomStatus.TryParse(body, out var entries):
                    result = ApplyStatus(packet, entries, inject);
                    break;
            }
        }

        if (usersChanged) UsersChanged?.Invoke();
        return result;
    }

    private byte[] ApplyStatus(byte[] packet, List<StatusEntry> entries, List<(string Packet, TimeSpan Delay)>? inject)
    {
        if (entries.Count == 1 && DateTime.UtcNow <= _expectSelfStatusUntil && _myIndex is null)
        {
            _myIndex = entries[0].Index;
        }
        _expectSelfStatusUntil = DateTime.MinValue;

        // Positions of everyone, so a click on a tile can be matched to a person.
        foreach (var entry in entries)
        {
            if (_users.TryGetValue(entry.Index, out var user))
            {
                _users[entry.Index] = user with { X = entry.X, Y = entry.Y };
            }
        }

        if (_myIndex is null) return packet;

        // You clicked a fake machine and have now arrived at its front tile: use it, as the client would.
        var mine = entries.FirstOrDefault(e => e.Index == _myIndex);
        if (_pendingUse is { } machine && mine is not null && DateTime.UtcNow <= _pendingUseUntil
            && (mine.X, mine.Y) == machine.Front && !mine.Actions.Contains("/mv ", StringComparison.Ordinal))
        {
            _pendingUse = null;
            _carriedDrink = machine.Drink;
            _carryUntil = DateTime.UtcNow + CarryDuration;
            inject?.Add((StuffDataUpdate(machine.Id, "TRUE"), TimeSpan.Zero));
            inject?.Add((StuffDataUpdate(machine.Id, "FALSE"), MachineAnimation));
        }

        var carrying = _carriedDrink is not null && DateTime.UtcNow <= _carryUntil;
        var changed = false;
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Index != _myIndex) continue;

            if (carrying)
            {
                entries[i] = entries[i] with { Actions = RoomStatus.WithCarriedDrink(entries[i].Actions, _carriedDrink!) };
                changed = true;
            }
            _myStatus = entries[i] with { Actions = StripMovement(entries[i].Actions) };
        }

        if (!carrying) _carriedDrink = null;
        return changed ? Latin1.GetBytes(RoomStatus.Build(entries)) : packet;
    }

    private bool ApplyLookChange(string body)
    {
        try
        {
            var reader = new IncomingPacketReader(body);
            var index = reader.ReadInt();
            var figure = reader.ReadString();
            var sex = reader.ReadString();
            var motto = reader.ReadString();
            if (!_users.TryGetValue(index, out var user)) return false;
            _users[index] = user with { Figure = figure, Sex = sex, Motto = motto };
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
    #endregion

    private void ClearRoom()
    {
        _users.Clear();
        _heightmap = Array.Empty<string>();
        _extraUsers = 0;
        _machines.Clear();
        _pendingUse = null;
        _myIndex = null;
        _myStatus = null;
        _carriedDrink = null;
    }

    private bool IsMe(RoomUser user) =>
        (_myUserId is not null && user.UserId == _myUserId)
        || (_myName is not null && string.Equals(user.Name, _myName, StringComparison.Ordinal));

    // STUFFDATAUPDATE (AX): item[2]value[2]
    private static string StuffDataUpdate(string item, string value) =>
        ((int)IncomingPacketMessage.STUFFDATAUPDATE).EncodeB64() + item + Constants.PACKET_SPLITTER + value + Constants.PACKET_SPLITTER;

    // A remembered status must not replay a walk ("mv x,y,z") when reused later.
    private static string StripMovement(string actions)
    {
        var kept = actions.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(a => !a.StartsWith("mv ", StringComparison.Ordinal)).ToList();
        return kept.Count == 0 ? "/" : "/" + string.Join('/', kept) + "/";
    }

    // Client strings: [length: 2 chars Habbo B64][text], repeated.
    private static bool TryReadB64Strings(string text, int start, out List<string> values)
    {
        values = new List<string>();
        var pos = start;
        while (pos + 2 <= text.Length)
        {
            var length = text.Substring(pos, 2).DecodeB64();
            if (length < 0 || pos + 2 + length > text.Length) return values.Count > 0;
            values.Add(text.Substring(pos + 2, length));
            pos += 2 + length;
        }
        return values.Count > 0;
    }
}
