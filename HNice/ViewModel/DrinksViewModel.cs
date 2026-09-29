using HNice.Model;
using HNice.Model.Packets;
using HNice.Service;
using System.Windows;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Drops a drink machine (ACTIVEOBJECTS) on your own screen. The position follows your avatar
/// automatically while you walk, so the machine lands at your feet.
/// </summary>
class DrinksViewModel : BaseViewModel
{
    public Dictionary<string, string> FurniName { get; } = new()
    {
        { "Habbo Cola", "md_limukaappi" },
        { "Fridge", "fridge" },
        { "Minibar", "bar_polyfon" },
        { "Mochamaster", "mocchamaster" },
        { "Barrel", "bar_armas" }
    };

    private string _selectedFurni;
    public string SelectedFurni
    {
        get => _selectedFurni;
        set { if (_selectedFurni != value) { _selectedFurni = value; OnPropertyChanged(); } }
    }

    private string _customFurniName = string.Empty;
    public string CustomFurniName
    {
        get => _customFurniName;
        set { _customFurniName = value; OnPropertyChanged(); }
    }

    private int _xCoord = 10;
    public int XCoord
    {
        get => _xCoord;
        set { if (_xCoord != value) { _xCoord = value; OnPropertyChanged(); } }
    }

    private int _yCoord = 6;
    public int YCoord
    {
        get => _yCoord;
        set { if (_yCoord != value) { _yCoord = value; OnPropertyChanged(); } }
    }

    private int _rotation = 2;
    public int Rotation
    {
        get => _rotation;
        set { if (_rotation != value) { _rotation = value; OnPropertyChanged(); } }
    }

    private bool _isTracking;
    /// <summary>True once your avatar's position has been read from the room.</summary>
    public bool IsTracking
    {
        get => _isTracking;
        private set { _isTracking = value; OnPropertyChanged(); }
    }

    public ICommand DrinkMachineGeneratorCommand { get; }
    public ICommand CustomDrinkMachineGeneratorCommand { get; }

    public DrinksViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        DrinkMachineGeneratorCommand = new RelayCommand(async _ => await Place(SelectedFurni), _ => !string.IsNullOrEmpty(SelectedFurni));
        CustomDrinkMachineGeneratorCommand = new RelayCommand(async _ => await Place(CustomFurniName), _ => !string.IsNullOrWhiteSpace(CustomFurniName));
        worker.OnUpdateCoords += UpdateMachineCoords;
        _selectedFurni = FurniName.First().Value;
    }

    private Task Place(string sprite) =>
        OnSendToClient(ClientPacketBuilder.ActiveObject("999000002", Worker.CurrentPlayer?.UserId ?? 1, sprite.Trim(), XCoord, YCoord, Rotation));

    // Raised from the network thread.
    private void UpdateMachineCoords(Coordinate coords)
    {
        if (coords is null || !coords.AreValidCoords()) return;
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            XCoord = coords.X!.Value;
            YCoord = coords.Y!.Value;
            IsTracking = true;
        });
    }
}
