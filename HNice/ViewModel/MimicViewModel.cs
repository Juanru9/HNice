using HNice.Model.Packets;
using HNice.Service;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Mime: copy another Habbo's walking, gestures and chat, each part on its own or all together.
/// Sent through your real connection, so everyone in the room sees it.
/// </summary>
class MimicViewModel : BaseViewModel
{
    private const int MaxActivity = 8;

    public ObservableCollection<RoomUser> People { get; } = new();

    private RoomUser? _selectedPerson;
    public RoomUser? SelectedPerson
    {
        get => _selectedPerson;
        set
        {
            _selectedPerson = value;
            OnPropertyChanged();
            // Switching person while running starts copying the new one.
            if (value is not null && IsRunning && Worker.Mimic.Target?.Index != value.Index) StartMimic();
        }
    }

    private bool _pickFromGame = true;
    /// <summary>Clicking a Habbo in the game selects them.</summary>
    public bool PickFromGame { get => _pickFromGame; set { _pickFromGame = value; OnPropertyChanged(); } }

    private bool _switchOnClick;
    /// <summary>
    /// While copying, clicking another Habbo switches to them. Off by default: clicking someone to look at them
    /// (captured: a click on 11,8 silently moved the mime from Yooco to someone else) should not change the person.
    /// </summary>
    public bool SwitchOnClick { get => _switchOnClick; set { _switchOnClick = value; OnPropertyChanged(); } }

    #region What to copy
    public bool CopyMoves { get => Has(MimicParts.Moves); set => Set(MimicParts.Moves, value); }
    public bool CopyGestures { get => Has(MimicParts.Gestures); set => Set(MimicParts.Gestures, value); }
    public bool CopySpeech { get => Has(MimicParts.Speech); set => Set(MimicParts.Speech, value); }

    public bool SkipRiskyLines
    {
        get => Worker.Mimic.SkipRiskyLines;
        set { Worker.Mimic.SkipRiskyLines = value; OnPropertyChanged(); }
    }

    private MimicPosition _position = MimicPosition.KeepDistance;
    public MimicPosition Position
    {
        get => _position;
        set { _position = value; OnPropertyChanged(); if (IsRunning) StartMimic(); }
    }

    private bool Has(MimicParts part) => (Worker.Mimic.Parts & part) != 0;

    // Parts change live: no need to stop and start again.
    private void Set(MimicParts part, bool on, [System.Runtime.CompilerServices.CallerMemberName] string name = "")
    {
        Worker.Mimic.Parts = on ? Worker.Mimic.Parts | part : Worker.Mimic.Parts & ~part;
        OnPropertyChanged(name);
        OnPropertyChanged(nameof(Summary));
        CommandManager.InvalidateRequerySuggested();
    }
    #endregion

    #region State
    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(ToggleText)); OnPropertyChanged(nameof(Summary)); }
    }

    public string ToggleText => IsRunning ? "Stop" : "Start mimicking";

    /// <summary>One line: who is copied and what.</summary>
    public string Summary
    {
        get
        {
            var parts = new[] { (CopyMoves, "walking"), (CopyGestures, "gestures"), (CopySpeech, "chat") }
                .Where(p => p.Item1).Select(p => p.Item2).ToList();
            var what = parts.Count == 0 ? "nothing (switch something on)" : string.Join(", ", parts);
            return IsRunning && Worker.Mimic.Target is { } target
                ? $"Copying {target.DisplayName}: {what}."
                : $"Will copy: {what}.";
        }
    }

    private string _message = "Click a Habbo in the game, or choose someone from the list.";
    public string Message { get => _message; private set { _message = value; OnPropertyChanged(); } }

    public ObservableCollection<string> Activity { get; } = new();
    #endregion

    public ICommand ToggleCommand { get; }

    public MimicViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        ToggleCommand = new RelayCommand(_ => { if (IsRunning) StopMimic("Stopped."); else StartMimic(); },
            _ => IsRunning || (SelectedPerson is not null && Worker.Mimic.Parts != MimicParts.None));

        Worker.RoomUsersChanged += () => OnUi(RefreshPeople);
        Worker.UserPicked += user => OnUi(() => OnUserPicked(user));
        Worker.Mimic.Sent += action => OnUi(() => AddActivity(action.Activity));
        Worker.Mimic.Skipped += line => OnUi(() => AddActivity("Not repeated: " + line));
        Worker.Mimic.Notice += text => OnUi(() => AddActivity(text));
        Worker.Mimic.Stopped += reason => OnUi(() => { IsRunning = false; Message = reason; });
        RefreshPeople();
    }

    private async void StartMimic()
    {
        if (SelectedPerson is not { } picked) return;
        // Their latest tile (the room tracker follows everyone), not the one from when the list was filled.
        var target = Worker.RoomUsers.FirstOrDefault(u => u.Index == picked.Index) ?? picked;
        if (Worker.MyStatus is null && Position == MimicPosition.KeepDistance)
        {
            Message = "Your avatar was not found in the room yet. Walk one step and try again, or choose Beside them.";
            return;
        }

        Worker.Mimic.MyName = Worker.CurrentPlayer?.HabboName;
        var first = Worker.Mimic.Start(target, Worker.MyStatus, Position, DateTime.UtcNow);
        Activity.Clear();
        IsRunning = true;
        Message = Position == MimicPosition.Beside
            ? $"Walk next to {target.DisplayName} and copy what they do."
            : $"Keeping your distance from {target.DisplayName}.";

        foreach (var action in first)
        {
            await OnSendToServer(action.Packet);
            Worker.Mimic.NotifySent(action);
        }
    }

    private void StopMimic(string reason)
    {
        Worker.Mimic.Stop();
        IsRunning = false;
        Message = reason;
    }

    private void RefreshPeople()
    {
        var selectedIndex = _selectedPerson?.Index;
        People.Clear();
        foreach (var user in Worker.RoomUsers) People.Add(user);
        _selectedPerson = People.FirstOrDefault(u => u.Index == selectedIndex);
        OnPropertyChanged(nameof(SelectedPerson));
        CommandManager.InvalidateRequerySuggested();
    }

    private void OnUserPicked(RoomUser user)
    {
        if (!PickFromGame) return;
        if (IsRunning && !SwitchOnClick && Worker.Mimic.Target is { } current && current.Index != user.Index)
        {
            Message = $"Still copying {current.DisplayName}. You clicked {user.ListName}: choose them in the list, or turn on switching by click.";
            return;
        }
        SelectedPerson = People.FirstOrDefault(u => u.Index == user.Index) ?? user;
        if (!IsRunning) Message = $"Picked {user.DisplayName}. Press Start mimicking.";
        CommandManager.InvalidateRequerySuggested();
    }

    private void AddActivity(string line)
    {
        Activity.Insert(0, $"{DateTime.Now:HH:mm:ss}  {line}");
        while (Activity.Count > MaxActivity) Activity.RemoveAt(Activity.Count - 1);
    }

    private static void OnUi(Action action) => Application.Current?.Dispatcher.BeginInvoke(action);
}
