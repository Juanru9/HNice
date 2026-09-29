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

    private const string MachineId = "999000002";

    // Drink handed out per machine. Cola = 19 was captured from the client's own request; the real minibar gave 1.
    private static readonly Dictionary<string, string> DrinkBySprite = new(StringComparer.OrdinalIgnoreCase)
    {
        { "md_limukaappi", "19" },
    };
    private const string DefaultDrink = "1";

    private bool _placeInFrontOfMe = true;
    /// <summary>Put the machine on the tile you face, turned toward you, so you can use it without walking.</summary>
    public bool PlaceInFrontOfMe
    {
        get => _placeInFrontOfMe;
        set { _placeInFrontOfMe = value; OnPropertyChanged(); }
    }

    private Task Place(string sprite)
    {
        // Standing on the machine's tile confuses the client (it keeps walking you to the front and never uses it),
        // so by default it goes on the tile in front of you, facing you.
        if (PlaceInFrontOfMe && Worker.MyStatus is { } me)
        {
            // Drink machines are only drawn facing 2 or 4 (any other direction renders a placeholder box),
            // so they can only face you from your west (facing 2) or your north (facing 4).
            // Pick the one closest to where you look.
            var lookingNorth = me.BodyRotation is 0 or 1 or 7;
            XCoord = lookingNorth ? me.X : me.X - 1;
            YCoord = lookingNorth ? me.Y - 1 : me.Y;
            Rotation = lookingNorth ? 4 : 2;
        }
        else if (Rotation is not (2 or 4))
        {
            Rotation = 2;
        }

        var name = sprite.Trim();
        // Registered so using it is answered locally: animation + drink in your hand.
        Worker.RegisterFakeMachine(MachineId, XCoord, YCoord, Rotation, DrinkBySprite.GetValueOrDefault(name, DefaultDrink));
        return OnSendToClient(ClientPacketBuilder.ActiveObject(MachineId, Worker.CurrentPlayer?.UserId ?? 1, name, XCoord, YCoord, Rotation));
    }

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
