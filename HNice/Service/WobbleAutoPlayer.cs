namespace HNice.Service;

/// <summary>
/// Picks your next Wobble Squabble move from the latest PT_STATUS. One move per status (every 0.3 s), in this order:
///   1. rebalance ("0") once per round when about to fall,
///   2. lean against your own tilt ("A" when tilted right, "D" when tilted left),
///   3. walk up to the other player ("X") while there is a gap,
///   4. hit them toward the side they already lean to ("W" left, "E" right).
/// Balance signs match the capture: "A" (lean left) pushed slot 0 to -21. The hit direction and the
/// one-tile reach are taken from Kepler and not seen live yet.
/// </summary>
public sealed class WobbleAutoPlayer
{
    /// <summary>Lean back once your balance is this far from 0.</summary>
    public const int LeanAt = 15;

    /// <summary>Use the one rebalance at this point (a player falls at 100).</summary>
    public const int RebalanceAt = 70;

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
        if (Math.Abs(me.Position - opponent.Position) > 1) return 'X';
        return opponent.Balance < 0 ? 'W' : 'E';
    }
}
