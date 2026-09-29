using HNice.Model.Packets;
using HNice.Util;
using HNice.Util.Extensions;
namespace HNice.Service;

/// <summary>What the mime copies. Each part can be switched on or off while it runs.</summary>
[Flags]
public enum MimicParts
{
    None = 0,
    Moves = 1,
    Gestures = 2,
    Speech = 4,
    All = Moves | Gestures | Speech,
}

/// <summary>Where your avatar stands while copying someone's walk.</summary>
public enum MimicPosition
{
    /// <summary>Keep the distance you had when you started.</summary>
    KeepDistance,
    /// <summary>Stand on the tile next to them (their east side).</summary>
    Beside,
}

/// <summary>One thing the mime sends to the server.</summary>
/// <param name="Delay">Wait this long before sending (speech is spaced out so the server does not see a flood).</param>
/// <param name="Activity">Short line for the activity feed.</param>
/// <param name="WaveGeneration">Set on repeated waves: sent only if that same wave is still going on.</param>
public sealed record MimicAction(string Packet, TimeSpan Delay, string Activity, int? WaveGeneration = null);

/// <summary>
/// Copies another Habbo: walks where they walk, waves and dances when they do, and repeats what they say.
/// Everything is sent through your own connection as the client would send it, so everyone in the room sees it.
///
/// Watched (live captures):  ← STATUS (@b)  their tile, next step "mv x,y,z", rotation, "/wave/", "/dance/"
///                           ← CHAT (@X) and ← CHAT_3 (@Z, shout): index(VL64) text[2]
///                           ← LOGOUT (@]) and ← ROOM_READY: they left, stop
/// Sent (captured from the client):  1269 "Su" x y (walk), LOOKTO "AO" "x y", WAVE "A^", DANCE "A]", STOP "AXDance",
///                           CHAT "@t" and SHOUT "@w" + B64 length + text.
/// </summary>
public sealed class Mimic
{
    private const string StatusHeader = "@b";
    private const int ChatHeader = (int)IncomingPacketMessage.CHAT;
    private const int ShoutHeader = (int)IncomingPacketMessage.CHAT_3;
    private const int LogoutHeader = (int)IncomingPacketMessage.LOGOUT;
    private const int RoomReadyHeader = (int)IncomingPacketMessage.ROOM_READY;
    private const int WalkHeader = 1269;

    // Chat is spaced out and a long backlog dropped: the server mutes floods.
    private static readonly TimeSpan SpeechGap = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan SpeechDelay = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan MaxSpeechBacklog = TimeSpan.FromSeconds(4);

    // A wave lasts 2 s. Waving again while waving only makes it longer: the status shows one long wave
    // (captured: 3 waves = "/wave/" for 6 s). So we wave again just after ours ends, while theirs goes on.
    private static readonly TimeSpan WaveRepeat = TimeSpan.FromMilliseconds(2100);
    private const int MaxWaveRepeats = 10;

    private readonly object _sync = new();

    private RoomUser? _target;
    private (int X, int Y) _offset;
    private (int X, int Y)? _lastWalk;
    private int? _lastRotation;

    // The server ignores a walk to a tile furni stands on, so a walk that does not move you within
    // BlockedAfter marks that tile blocked (for this room) and the nearest free tile is tried instead.
    private static readonly TimeSpan BlockedAfter = TimeSpan.FromMilliseconds(1200);
    private readonly HashSet<(int X, int Y)> _blocked = new();
    private (int X, int Y)? _desired;      // their tile + offset
    private (int X, int Y) _theirTile;
    private (int X, int Y)? _walkFrom;     // where you stood when the last walk was sent
    private DateTime _walkSentAt;

    // The server also ignores a walk when there is no path (captured: furni walls you in), so tiles that
    // fail in a row are "unreachable from here", not blocked: after MaxFailStreak the mime stops trying
    // and forgets them, until they or you move.
    private const int MaxFailStreak = 3;
    private readonly List<(int X, int Y)> _failStreak = new();

    /// <summary>Floor and nobody standing there (set by the worker from the room map and users).</summary>
    public Func<int, int, bool> IsFree { get; set; } = (_, _) => true;
    private bool _wasWaving;
    private bool _wasDancing;
    private int _waveGeneration;
    private DateTime _nextSpeechAt = DateTime.MinValue;

    public MimicParts Parts { get; set; } = MimicParts.All;

    /// <summary>Do not repeat links or lines that mention you: the person copied could make you say anything.</summary>
    public bool SkipRiskyLines { get; set; } = true;

    /// <summary>Your name as the server sends it, for <see cref="SkipRiskyLines"/>.</summary>
    public string? MyName { get; set; }

    private static readonly string[] LinkMarkers = { "http", "www.", ".com", ".net", ".org", ".es", ".gg", ".ly", "discord" };

    public bool IsRunning { get { lock (_sync) return _target is not null; } }

    public RoomUser? Target { get { lock (_sync) return _target; } }

    /// <summary>The mime stopped by itself (the person left, you changed rooms). Carries the reason.</summary>
    public event Action<string>? Stopped;

    /// <summary>A line was not repeated because of <see cref="SkipRiskyLines"/>. Raised on a network thread.</summary>
    public event Action<string>? Skipped;

    /// <summary>Copying started (or switched) to this person.</summary>
    public event Action<RoomUser>? Started;

    /// <summary>Something worth showing in the activity feed that is not a sent packet.</summary>
    public event Action<string>? Notice;

    /// <summary>A copied action was sent to the server. Raised on a network thread.</summary>
    public event Action<MimicAction>? Sent;

    public void NotifySent(MimicAction action) => Sent?.Invoke(action);

    /// <summary>Starts copying <paramref name="target"/>. <paramref name="me"/> sets the distance to keep.</summary>
    /// <param name="now">Time of the first walk, for noticing a blocked tile.</param>
    /// <returns>
    /// The first walk: straight to your place next to them (from their current tile, as the room tracker knows it),
    /// without waiting for them to move.
    /// </returns>
    public List<MimicAction> Start(RoomUser target, StatusEntry? me, MimicPosition position, DateTime now)
    {
        var actions = new List<MimicAction>();
        lock (_sync)
        {
            _target = target;
            _offset = position == MimicPosition.Beside || me is null ? (1, 0) : (me.X - target.X, me.Y - target.Y);
            _lastWalk = null;
            _lastRotation = null;
            _blocked.Clear();
            _failStreak.Clear();
            _desired = null;
            _walkFrom = null;
            _wasWaving = false;
            _wasDancing = false;

            if ((Parts & MimicParts.Moves) != 0 && me is not null)
            {
                _theirTile = (target.X, target.Y);
                _desired = (target.X + _offset.X, target.Y + _offset.Y);
                var destination = PickDestination() ?? _desired.Value;
                if (destination != (me.X, me.Y) && destination.X >= 0 && destination.Y >= 0)
                {
                    WalkTo(destination, me, now, actions);
                }
                else
                {
                    _lastWalk = destination;
                }
            }
        }
        Started?.Invoke(target);
        return actions;
    }

    /// <param name="reason">When given, <see cref="Stopped"/> is raised with it.</param>
    public void Stop(string? reason = null)
    {
        bool wasRunning;
        lock (_sync)
        {
            wasRunning = _target is not null;
            _target = null;
        }
        if (wasRunning && reason is not null) Stopped?.Invoke(reason);
    }

    /// <summary>Checked right before a delayed action is sent: repeated waves stop when their wave has ended.</summary>
    public bool IsStillWanted(MimicAction action)
    {
        lock (_sync)
        {
            if (_target is null) return false;
            if (action.WaveGeneration is not { } generation) return true;
            return _wasWaving && generation == _waveGeneration && (Parts & MimicParts.Gestures) != 0;
        }
    }

    /// <summary>What to send in answer to one server packet. Empty when nothing is copied.</summary>
    /// <param name="packet">Plain server packet (header + body).</param>
    /// <param name="me">Your latest status (for turning in place).</param>
    public List<MimicAction> Plan(string packet, StatusEntry? me, DateTime now)
    {
        var actions = new List<MimicAction>();
        if (packet.Length < 2) return actions;

        string? stoppedBecause = null;
        string? skipped = null;
        lock (_sync)
        {
            if (_target is not { } target) return actions;

            var header = packet[..2];
            var body = packet[2..];
            var id = header.DecodeB64();

            if (header == StatusHeader && RoomStatus.TryParse(body, out var entries))
            {
                foreach (var entry in entries.Where(e => e.Index == target.Index))
                {
                    CopyStatus(entry, me, now, actions);
                }
            }
            else if ((id == ChatHeader || id == ShoutHeader) && (Parts & MimicParts.Speech) != 0)
            {
                skipped = CopySpeech(body, target, shout: id == ShoutHeader, now, actions);
            }
            else if (id == LogoutHeader && int.TryParse(body.Trim(), out var leftIndex) && leftIndex == target.Index)
            {
                stoppedBecause = $"{target.DisplayName} left the room.";
            }
            else if (id == RoomReadyHeader)
            {
                stoppedBecause = "You changed rooms.";
            }

            if (stoppedBecause is not null) _target = null;
        }

        if (stoppedBecause is not null) Stopped?.Invoke(stoppedBecause);
        if (skipped is not null) Skipped?.Invoke(skipped);
        return actions;
    }

    private void CopyStatus(StatusEntry entry, StatusEntry? me, DateTime now, List<MimicAction> actions)
    {
        var parts = entry.Actions.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if ((Parts & MimicParts.Moves) != 0)
        {
            // While walking the status names the next tile ("mv x,y,z"); standing still, their own tile.
            var step = parts.FirstOrDefault(p => p.StartsWith("mv ", StringComparison.Ordinal));
            var tile = step is not null && TryReadStep(step, out var next) ? next : (entry.X, entry.Y);
            _theirTile = tile;
            _desired = (tile.X + _offset.X, tile.Y + _offset.Y);
            var destination = PickDestination() ?? _desired.Value;

            if (destination != _lastWalk && destination.X >= 0 && destination.Y >= 0)
            {
                WalkTo(destination, me, now, actions);
            }
            else if (step is null && entry.BodyRotation != _lastRotation && me is not null && (me.X, me.Y) == _lastWalk)
            {
                // Standing still and turning: face the same way.
                var (dx, dy) = Direction(entry.BodyRotation);
                actions.Add(new MimicAction(LookTo(me.X + dx, me.Y + dy), TimeSpan.Zero, $"Turn to {entry.BodyRotation}"));
            }
            if (step is null) _lastRotation = entry.BodyRotation;
        }

        var waving = parts.Contains("wave");
        var dancing = parts.Any(p => p == "dance" || p.StartsWith("dance ", StringComparison.Ordinal));
        if ((Parts & MimicParts.Gestures) != 0)
        {
            if (waving && !_wasWaving)
            {
                var generation = ++_waveGeneration;
                actions.Add(new MimicAction("A^", TimeSpan.Zero, "Wave"));
                for (var i = 1; i <= MaxWaveRepeats; i++)
                {
                    actions.Add(new MimicAction("A^", WaveRepeat * i, "Wave again", generation));
                }
            }
            if (dancing && !_wasDancing) actions.Add(new MimicAction("A]", TimeSpan.Zero, "Dance"));
            // Captured: stopping sends STOP "Dance". Walking also stops it, but copy the stop either way.
            if (!dancing && _wasDancing) actions.Add(new MimicAction(StopDancing, TimeSpan.Zero, "Stop dancing"));
        }
        _wasWaving = waving;
        _wasDancing = dancing;
    }

    /// <returns>The line when it was skipped as risky, otherwise null.</returns>
    private string? CopySpeech(string body, RoomUser target, bool shout, DateTime now, List<MimicAction> actions)
    {
        string text;
        try
        {
            var reader = new IncomingPacketReader(body);
            if (reader.ReadInt() != target.Index) return null;
            text = reader.ReadString();
        }
        catch (FormatException)
        {
            return null;
        }

        // ":commands" are for the server, not chat.
        if (text.Length == 0 || text.StartsWith(':')) return null;

        if (SkipRiskyLines && IsRisky(text)) return PacketText.FromWire(text);

        var sendAt = Max(now + SpeechDelay, _nextSpeechAt);
        if (sendAt - now > MaxSpeechBacklog) return null;
        _nextSpeechAt = sendAt + SpeechGap;

        var header = shout ? "@w" : "@t";
        actions.Add(new MimicAction(header + text.Length.EncodeB64() + text, sendAt - now,
            (shout ? "Shout: " : "Say: ") + PacketText.FromWire(text)));
        return null;
    }

    private bool IsRisky(string text) =>
        LinkMarkers.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase))
        || (!string.IsNullOrWhiteSpace(MyName) && text.Contains(MyName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Called every half second while copying: a walk that did not move you means furni blocks that tile,
    /// so the nearest free tile next to them is tried instead.
    /// </summary>
    public List<MimicAction> Tick(StatusEntry? me, DateTime now)
    {
        var actions = new List<MimicAction>();
        string? notice = null;
        lock (_sync)
        {
            if (_target is null || (Parts & MimicParts.Moves) == 0 || me is null) return actions;
            if (_lastWalk is not { } walk || _walkFrom is not { } from) return actions;

            var here = (me.X, me.Y);
            if (here == walk || here != from)
            {
                // Arrived, or on the way: the path works.
                if (here == walk) _walkFrom = null;
                _failStreak.Clear();
                return actions;
            }
            if (now - _walkSentAt < BlockedAfter) return actions;

            _blocked.Add(walk);
            _failStreak.Add(walk);
            if (_failStreak.Count >= MaxFailStreak)
            {
                foreach (var tile in _failStreak) _blocked.Remove(tile);
                _failStreak.Clear();
                _walkFrom = null;
                notice = $"Can't reach {_target.DisplayName} from here: waiting for one of you to move.";
            }
            else if (PickDestination() is { } next && next != walk)
            {
                WalkTo(next, me, now, actions, $"Walk to {next.X},{next.Y} ({walk.X},{walk.Y} is blocked)");
            }
            else
            {
                _walkFrom = null; // nowhere free: wait for their next move
            }
        }
        if (notice is not null) Notice?.Invoke(notice);
        return actions;
    }

    private void WalkTo((int X, int Y) tile, StatusEntry? me, DateTime now, List<MimicAction> actions, string? activity = null)
    {
        _lastWalk = tile;
        _walkFrom = me is null ? null : (me.X, me.Y);
        _walkSentAt = now;
        actions.Add(new MimicAction(Walk(tile.X, tile.Y), TimeSpan.Zero, activity ?? $"Walk to {tile.X},{tile.Y}"));
    }

    /// <summary>The wanted tile, or the nearest free one around it that is not blocked and not their own tile.</summary>
    private (int X, int Y)? PickDestination()
    {
        if (_desired is not { } desired) return null;
        var candidates = new List<(int X, int Y)>();
        for (var radius = 0; radius <= 2; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                    candidates.Add((desired.X + dx, desired.Y + dy));
                }
            }
        }
        return candidates
            .Where(t => t.X >= 0 && t.Y >= 0 && t != _theirTile && !_blocked.Contains(t) && IsFree(t.X, t.Y))
            .OrderBy(t => Math.Max(Math.Abs(t.X - desired.X), Math.Abs(t.Y - desired.Y)))
            .ThenBy(t => Math.Abs(t.X - _theirTile.X) + Math.Abs(t.Y - _theirTile.Y))
            .Cast<(int X, int Y)?>()
            .FirstOrDefault();
    }

    // "mv x,y,z"
    private static bool TryReadStep(string step, out (int X, int Y) tile)
    {
        tile = default;
        var coords = step[3..].Split(',');
        if (coords.Length < 2 || !int.TryParse(coords[0], out var x) || !int.TryParse(coords[1], out var y)) return false;
        tile = (x, y);
        return true;
    }

    // 0 = north, clockwise.
    private static (int X, int Y) Direction(int rotation) => rotation switch
    {
        0 => (0, -1), 1 => (1, -1), 2 => (1, 0), 3 => (1, 1),
        4 => (0, 1), 5 => (-1, 1), 6 => (-1, 0), _ => (-1, -1),
    };

    private static readonly string StopDancing = ((int)OutcomingPacketMessage.STOP).EncodeB64() + "Dance";

    private static string Walk(int x, int y) => WalkHeader.EncodeB64() + x.EncodeVL64() + y.EncodeVL64();

    private static string LookTo(int x, int y) => ((int)OutcomingPacketMessage.LOOKTO).EncodeB64() + x + " " + y;

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
}
