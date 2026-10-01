using HNice.Service;
using System.Collections.ObjectModel;
using System.Windows;

namespace HNice.ViewModel;

/// <summary>One side of the plank, as shown in the tool.</summary>
public sealed class WobblePlayerRow
{
    public required string Side { get; init; }
    public required string Name { get; init; }
    public required int Position { get; init; }
    public required int Balance { get; init; }
    public required string Move { get; init; }
    public bool IsMe { get; init; }

    /// <summary>How close to falling, 0-100 (a player falls at -100 or 100).</summary>
    public int Danger => Math.Min(100, Math.Abs(Balance));

    public string Lean => Balance switch
    {
        < 0 => $"{-Balance} to the left",
        > 0 => $"{Balance} to the right",
        _ => "balanced",
    };
}

/// <summary>Shows Wobble Squabble rounds as they are played: both players' balance and moves, then the results. Can play your rounds for you.</summary>
class WobbleViewModel : BaseViewModel
{
    public ObservableCollection<WobblePlayerRow> Players { get; } = new();
    public ObservableCollection<string> Results { get; } = new();

    private string _roundState = "No round on the plank.";
    public string RoundState
    {
        get => _roundState;
        private set { _roundState = value; OnPropertyChanged(); }
    }

    public bool AutoPlay
    {
        get => Worker.AutoWobble;
        set
        {
            if (Worker.AutoWobble == value) return;
            Worker.AutoWobble = value;
            OnPropertyChanged();
        }
    }

    private string _lastMove = string.Empty;
    public string LastMove
    {
        get => _lastMove;
        private set { _lastMove = value; OnPropertyChanged(); }
    }

    private const int MaxResults = 30;

    public WobbleViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        worker.Wobble.Changed += () => Application.Current?.Dispatcher.BeginInvoke(Refresh);
        worker.Wobble.RoundEnded += result => Application.Current?.Dispatcher.BeginInvoke(() => AddResult(result));
        worker.WobbleMoveSent += move => Application.Current?.Dispatcher.BeginInvoke(() =>
            LastMove = $"Sent at {DateTime.Now:HH:mm:ss.f}: {WobbleTracker.MoveName(move.ToString())}");
    }

    private void Refresh()
    {
        var players = Worker.Wobble.Players;
        Players.Clear();
        foreach (var p in players)
        {
            Players.Add(new WobblePlayerRow
            {
                Side = p.Slot == 0 ? "Left" : "Right",
                Name = NameOf(p.RoomIndex),
                Position = p.Position,
                Balance = p.Balance,
                Move = WobbleTracker.MoveName(p.Move),
                IsMe = p.RoomIndex == Worker.MyRoomIndex,
            });
        }
        RoundState = players.Count == 0 ? "No round on the plank." : $"{NameOf(players[0].RoomIndex)} vs {NameOf(players[1].RoomIndex)}";
    }

    private void AddResult(WobbleResult result)
    {
        var left = NameOf(result.LeftRoomIndex);
        var right = NameOf(result.RightRoomIndex);
        var line = result.WinnerSlot switch
        {
            0 => $"{result.EndedAt:HH:mm:ss}  {left} beat {right}",
            1 => $"{result.EndedAt:HH:mm:ss}  {right} beat {left}",
            _ => $"{result.EndedAt:HH:mm:ss}  {left} and {right}: both lost (time out)",
        };
        Results.Insert(0, line);
        while (Results.Count > MaxResults) Results.RemoveAt(Results.Count - 1);
    }

    private string NameOf(int roomIndex)
    {
        if (roomIndex == Worker.MyRoomIndex) return (Worker.CurrentPlayer?.HabboName ?? "You") + " (you)";
        return Worker.RoomUsers.FirstOrDefault(u => u.Index == roomIndex)?.DisplayName ?? $"slot {roomIndex}";
    }
}
