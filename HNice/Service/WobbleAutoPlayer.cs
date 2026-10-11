namespace HNice.Service;

/// <summary>
/// Picks your next Wobble Squabble move from the latest PT_STATUS, one move per status (every 0.3 s).
///
/// Rules measured in md_a (FINDINGS.md, rounds of 2026-10-02 01:10 and 01:33-01:34):
///   - Balance holds still on its own. Leaning moves it 25-34 per status: "A" down, "D" up. A player falls past ±100.
///   - "X" moves you one place down the plank, "S" one place up. Players stop 2 places apart.
///   - A hit only lands from 2 places away: "W" pushes the other player up, "E" down, 10-20 per hit.
///     A landed hit nudges the hitter 1-4 the other way; a missed "W" pushed the hitter up 16-20 instead.
/// The Kepler emulator (Quackster/Kepler, WobbleSquabbleManager) agrees on the directions and adds: "0" sets balance
/// to exactly 0, once per round; only the last move sent in a tick counts; a round left undecided ends with both
/// players in the water (PT_BOTHLOSE). Its amounts (lean 35-44, 0.2 s ticks) differ from the live server.
///
/// Order: rebalance once when about to fall, lean back past ±<see cref="LeanAt"/>, walk up to the other player,
/// then hit them further the way they already lean.
/// </summary>
public sealed class WobbleAutoPlayer
{
    /// <summary>Lean back from here: a lean (25-34) then lands within ±14, so it never swings past the other side.</summary>
    public const int LeanAt = 20;

    /// <summary>Use the one rebalance (back to 0) from here; a player falls at ±100.</summary>
    public const int RebalanceAt = 70;

    /// <summary>Hits only land from this many places away, which is also as close as players get.</summary>
    public const int HitReach = 2;

    /// <summary>Below this lean the other player is as good as straight: pick the hit by your own balance instead.</summary>
    public const int StraightWithin = 10;

    private bool _rebalanced;

    /// <summary>Forget the rebalance used last round.</summary>
    public void NewRound() => _rebalanced = false;

    public char Choose(WobblePlayer me, WobblePlayer opponent)
    {
        if (!_rebalanced && Math.Abs(me.Balance) >= RebalanceAt)
        {
            _rebalanced = true;
            return '0';
        }
        if (me.Balance >= LeanAt) return 'A';
        if (me.Balance <= -LeanAt) return 'D';

        var gap = opponent.Position - me.Position;
        if (Math.Abs(gap) > HitReach) return gap > 0 ? 'S' : 'X';

        // Push them further the way they lean. When they stand about straight, pick the hit whose nudge on you
        // (a landed W moves the hitter down, a landed E up) brings you back towards 0.
        if (Math.Abs(opponent.Balance) >= StraightWithin) return opponent.Balance > 0 ? 'W' : 'E';
        return me.Balance > 0 ? 'W' : 'E';
    }
}
