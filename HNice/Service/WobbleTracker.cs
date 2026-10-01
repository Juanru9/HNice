using HNice.Model.Packets;
using HNice.Util.Extensions;
using System.Text;

namespace HNice.Service;

/// <summary>One player on the plank: their slot (0 = left), room slot, place on the plank, balance and last move.</summary>
public sealed record WobblePlayer(int Slot, int RoomIndex, int Position, int Balance, string Move);

/// <summary>A finished round. <paramref name="WinnerSlot"/> is null when both lost (time out).</summary>
public sealed record WobbleResult(DateTime EndedAt, int LeftRoomIndex, int RightRoomIndex, int? WinnerSlot);

/// <summary>
/// Follows Wobble Squabble rounds in the pool. It only reads; your moves go out as PTM (see <see cref="ServerPacketBuilder.WobbleMove"/>).
///
/// Captured in md_a (2026-10-02 01:06-01:10):
///   ← PT_PREPARE  As 0:3[13]1:4                   slot:room slot for both players, starts in 3 s
///   ← PT_START    Ar 0:3[13]1:4
///   ← PT_STATUS   Av -3[9]0[9]-[9][13]4[9]0[9]-[9][13]   per slot: position[9]balance[9]move[9][13], every 0.3 s
///   ← PT_WIN      Aw 0                            the winning slot
///   ← PT_END      At
/// Kepler also sends PT_BOTHLOSE / PT_TIMEOUT after 30 s; not seen live yet.
/// A player falls at balance -100 or 100 (Kepler <c>isBalancing</c>; seen live: 101, then PT_WIN for the other).
/// </summary>
public sealed class WobbleTracker
{
    private const int Prepare = (int)IncomingPacketMessage.PT_PREPARE;
    private const int Start = (int)IncomingPacketMessage.PT_START;
    private const int Status = (int)IncomingPacketMessage.PT_STATUS;
    private const int Win = (int)IncomingPacketMessage.PT_WIN;
    private const int End = (int)IncomingPacketMessage.PT_END;
    private const int BothLose = (int)IncomingPacketMessage.PT_BOTHLOSE;
    private const int Timeout = (int)IncomingPacketMessage.PT_TIMEOUT;

    /// <summary>Move letters (Kepler WobbleSquabbleMove).</summary>
    public static string MoveName(string letter) => letter switch
    {
        "A" => "lean left",
        "D" => "lean right",
        "W" => "hit left",
        "E" => "hit right",
        "X" => "step forward",
        "S" => "step back",
        "0" => "rebalance",
        _ => "—",
    };

    private readonly object _sync = new();
    private int[]? _roomIndexes;
    private WobblePlayer[] _players = Array.Empty<WobblePlayer>();
    private bool _ended;
    private bool _live;

    /// <summary>The players of the round being played, or an empty list between rounds.</summary>
    public IReadOnlyList<WobblePlayer> Players { get { lock (_sync) return _players; } }

    /// <summary>Between PT_START and the round being decided: the only time moves count.</summary>
    public bool IsLive { get { lock (_sync) return _live; } }

    /// <summary>Your slot (0 = left, 1 = right) when you are on the plank, otherwise null.</summary>
    public int? SlotOf(int? roomIndex)
    {
        if (roomIndex is null) return null;
        lock (_sync) return _players.FirstOrDefault(p => p.RoomIndex == roomIndex)?.Slot;
    }

    /// <summary>Players, balance or moves changed. Raised on a network thread.</summary>
    public event Action? Changed;

    /// <summary>A round was decided. Raised on a network thread.</summary>
    public event Action<WobbleResult>? RoundEnded;

    /// <param name="packet">Plain server packet (header + body). Only the game's packets are read past the header.</param>
    public void Observe(byte[] packet)
    {
        if (packet.Length < 2) return;
        var header = ((ReadOnlySpan<byte>)packet)[..2].DecodeB64();
        if (header is not (Prepare or Start or Status or Win or End or BothLose or Timeout)) return;
        var body = Encoding.Latin1.GetString(packet, 2, packet.Length - 2);

        WobbleResult? result = null;
        lock (_sync)
        {
            switch (header)
            {
                case Prepare or Start when TryReadSlots(body, out var rooms):
                    if (header == Prepare || _roomIndexes is null || !_roomIndexes.SequenceEqual(rooms))
                    {
                        _roomIndexes = rooms;
                        // Kepler: slot 0 starts at -3, slot 1 at 4, both balanced.
                        _players = new[] { new WobblePlayer(0, rooms[0], -3, 0, "-"), new WobblePlayer(1, rooms[1], 4, 0, "-") };
                        _ended = false;
                    }
                    _live = header == Start && !_ended;
                    break;

                case Status when _roomIndexes is { } slots && TryReadStatus(body, slots, out var players):
                    _players = players;
                    break;

                case Win when _roomIndexes is { } slots && !_ended && int.TryParse(body.Trim(), out var winner) && winner is 0 or 1:
                    _ended = true;
                    _live = false;
                    result = new WobbleResult(DateTime.Now, slots[0], slots[1], winner);
                    break;

                case BothLose or Timeout when _roomIndexes is { } slots && !_ended:
                    _ended = true;
                    _live = false;
                    result = new WobbleResult(DateTime.Now, slots[0], slots[1], null);
                    break;

                case End:
                    _roomIndexes = null;
                    _live = false;
                    _players = Array.Empty<WobblePlayer>();
                    break;

                default:
                    return;
            }
        }

        Changed?.Invoke();
        if (result is not null) RoundEnded?.Invoke(result);
    }

    // "0:3\r1:4"
    private static bool TryReadSlots(string body, out int[] rooms)
    {
        rooms = new int[2];
        var found = 0;
        foreach (var part in body.Split('\r', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split(':');
            if (pair.Length == 2 && int.TryParse(pair[0], out var slot) && slot is 0 or 1 && int.TryParse(pair[1], out var room))
            {
                rooms[slot] = room;
                found++;
            }
        }
        return found == 2;
    }

    // "-3\t-21\tA\t\r4\t0\t-\t\r": slot 0 then slot 1.
    private static bool TryReadStatus(string body, int[] slots, out WobblePlayer[] players)
    {
        players = Array.Empty<WobblePlayer>();
        var lines = body.Split('\r', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return false;
        var read = new WobblePlayer[2];
        for (var slot = 0; slot < 2; slot++)
        {
            var fields = lines[slot].Split('\t');
            if (fields.Length < 3 || !int.TryParse(fields[0], out var position) || !int.TryParse(fields[1], out var balance)) return false;
            read[slot] = new WobblePlayer(slot, slots[slot], position, balance, fields[2]);
        }
        players = read;
        return true;
    }
}
