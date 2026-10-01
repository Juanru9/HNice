using HNice.Service;
using System.Windows;

namespace HNice.ViewModel;

/// <summary>
/// Takes you back into a room when you get kicked from it: your own, someone else's or a public room.
/// Your client is told to go to the room and enters it through its normal requests, so the server
/// still applies bans, doorbells, passwords and full rooms.
/// </summary>
class RejoinViewModel : BaseViewModel
{
    public bool AutoRejoin
    {
        get => Worker.AutoRejoin;
        set
        {
            if (Worker.AutoRejoin == value) return;
            Worker.AutoRejoin = value;
            OnPropertyChanged();
        }
    }

    private string _lastRejoin = string.Empty;
    public string LastRejoin
    {
        get => _lastRejoin;
        private set { _lastRejoin = value; OnPropertyChanged(); }
    }

    public RejoinViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        worker.Rejoining += OnRejoining;
    }

    // Raised from the network thread.
    private void OnRejoining(int roomId) =>
        Application.Current?.Dispatcher.BeginInvoke(() =>
            LastRejoin = $"Kicked at {DateTime.Now:HH:mm:ss}, sending you back to room {roomId}.");
}
