using HNice.Model;
using HNice.Model.Packets;
using HNice.Service;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace HNice.ViewModel;

/// <summary>What the server did with the last drink request.</summary>
public enum OrderState
{
    None,
    Waiting,
    Accepted,
    Failed
}

/// <summary>
/// Orders a real drink from the server, or drops a drink machine (ACTIVEOBJECTS) on your own screen.
/// The machine's position follows your avatar automatically while you walk, so it lands at your feet.
/// </summary>
class DrinksViewModel : BaseViewModel
{
    /// <summary>Every hand item the hotel names (handitemN in the game's external texts), except 20, the camera.</summary>
    public Dictionary<string, int> Drinks { get; } = new()
    {
        { "Habbo Cola", 19 },
        { "Lime Habbo Soda", 22 },
        { "Beetroot Habbo Soda", 23 },
        { "1978 Fizzy Drink", 24 },
        { "Love Potion", 25 },
        { "Glukko Pop", 26 },
        { "Fesh", 46 },
        { "Tea", 1 },
        { "Juice", 2 },
        { "Carrot", 3 },
        { "Ice cream", 4 },
        { "Milk", 5 },
        { "Blackcurrant", 6 },
        { "Water", 7 },
        { "Black coffee", 8 },
        { "Water (glass)", 9 },
        { "Cream", 10 },
        { "Mocha", 11 },
        { "Macchiato", 12 },
        { "Espresso", 13 },
        { "Filter coffee", 14 },
        { "Iced coffee", 15 },
        { "Cappuccino", 16 },
        { "Java", 17 },
        { "Tap water", 18 },
        { "Hamburger", 21 },
    };

    private int _selectedDrink = 19;
    public int SelectedDrink
    {
        get => _selectedDrink;
        set { if (_selectedDrink != value) { _selectedDrink = value; OnPropertyChanged(); } }
    }

    private bool _keepDrink;
    /// <summary>Orders the drink again before the server takes it away (~5 minutes).</summary>
    public bool KeepDrink
    {
        get => _keepDrink;
        set
        {
            if (_keepDrink == value) return;
            _keepDrink = value;
            OnPropertyChanged();
            if (value) _renewTimer.Start(); else _renewTimer.Stop();
        }
    }

    private string _orderStatus = string.Empty;
    public string OrderStatus
    {
        get => _orderStatus;
        private set { _orderStatus = value; OnPropertyChanged(); }
    }

    private OrderState _orderState;
    public OrderState OrderState
    {
        get => _orderState;
        private set { _orderState = value; OnPropertyChanged(); }
    }

    public ICommand OrderDrinkCommand { get; }

    // Captured: the server answers in under half a second; drinks are taken away after 300 s.
    private static readonly TimeSpan ServerAnswerWindow = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan RenewEvery = TimeSpan.FromSeconds(270);

    private readonly DispatcherTimer _renewTimer;
    private readonly DispatcherTimer _answerTimer;
    private int? _awaitedDrink;

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

        OrderDrinkCommand = new RelayCommand(async _ => await OrderDrink());
        _renewTimer = new DispatcherTimer { Interval = RenewEvery };
        _renewTimer.Tick += async (_, _) => await OrderDrink();
        _answerTimer = new DispatcherTimer { Interval = ServerAnswerWindow };
        _answerTimer.Tick += (_, _) => NoServerAnswer();
        worker.ServerGaveDrink += OnServerGaveDrink;
    }

    private Task OrderDrink()
    {
        _awaitedDrink = SelectedDrink;
        _answerTimer.Stop();
        _answerTimer.Start();
        Show(OrderState.Waiting, $"Asked for {DrinkName(SelectedDrink)}, waiting for the server…");
        return OnSendToServer(ServerPacketBuilder.CarryDrink(SelectedDrink));
    }

    // Raised from the network thread.
    private void OnServerGaveDrink(string drink)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_awaitedDrink is not { } awaited || drink != awaited.ToString()) return;
            _awaitedDrink = null;
            _answerTimer.Stop();
            Show(OrderState.Accepted, $"Accepted by the server at {DateTime.Now:HH:mm:ss}: {DrinkName(awaited)} is in your hand, everyone sees it."
                                      + (KeepDrink ? " Ordered again in 4½ minutes." : ""));
        });
    }

    private void NoServerAnswer()
    {
        _answerTimer.Stop();
        if (_awaitedDrink is not { } awaited) return;
        _awaitedDrink = null;
        Show(OrderState.Failed, $"The server ignored it: no {DrinkName(awaited)} in your hand after {ServerAnswerWindow.TotalSeconds:0} s.");
    }

    private void Show(OrderState state, string text)
    {
        OrderState = state;
        OrderStatus = text;
    }

    private string DrinkName(int id) => Drinks.FirstOrDefault(d => d.Value == id).Key is { } name ? $"{name} ({id})" : $"drink {id}";

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
