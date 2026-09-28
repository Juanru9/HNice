using HNice.Model;
using HNice.Model.Packets;
using HNice.Service;
using HNice.Util.Extensions;
using System.Windows.Input;

namespace HNice.ViewModel;

class DrinksViewModel : BaseViewModel
{
    #region Properties
    public Dictionary<string, string> FurniName { get; set; } = new Dictionary<string, string>
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
        set
        {
            if (_selectedFurni != value)
            {
                _selectedFurni = value;
                OnPropertyChanged(nameof(SelectedFurni));
            }
        }
    }
    private string _customFurniName = "md_limukaappi";
    public string CustomFurniName
    {
        get => _customFurniName;
        set
        {
            _customFurniName = value;
            OnPropertyChanged(nameof(CustomFurniName));
        }
    }
    private int _xCoord = 10;
    public int XCoord
    {
        get => _xCoord;
        set
        {
            if (_xCoord == value) 
            {
                return;
            }
            _xCoord = value;
            OnPropertyChanged(nameof(XCoord));
        }
    }
    private int _yCoord = 6;
    public int YCoord
    {
        get => _yCoord;
        set
        {
            if (_yCoord == value)
            {
                return;
            }
            _yCoord = value;
            OnPropertyChanged(nameof(YCoord));
        }
    }
    private int _rotation = 2;
    public int Rotation
    {
        get => _rotation;
        set
        {
            if (_rotation == value)
            {
                return;
            }
            _rotation = value;
            OnPropertyChanged(nameof(Rotation));
        }
    }
    private readonly ITcpInterceptorWorker _worker;
    #endregion

    #region Commands
    public ICommand DrinkMachineGeneratorCommand { get; }
    public ICommand CustomDrinkMachineGeneratorCommand { get; }
    #endregion

    public DrinksViewModel(ITcpInterceptorWorker worker) : base(worker)
    {
        DrinkMachineGeneratorCommand = new RelayCommand(async param => await OnGenerateDrinkMachine());
        CustomDrinkMachineGeneratorCommand = new RelayCommand(async param => await OnGenerateCustomDrinkMachine());
        worker.OnUpdateCoords += UpdateMachineCoords;
        SelectedFurni = FurniName.FirstOrDefault().Value;
    }
    ~DrinksViewModel() 
    {
        _worker.OnUpdateCoords -= UpdateMachineCoords;
    }

    private async Task OnGenerateDrinkMachine() 
    {
        await OnSendToClient(ClientPacketBuilder.ActiveObject("999000002", 1, SelectedFurni, XCoord, YCoord, Rotation));
    }
    private async Task OnGenerateCustomDrinkMachine() 
    {
        await OnSendToClient(ClientPacketBuilder.ActiveObject("999000002", 1, CustomFurniName, XCoord, YCoord, Rotation));
    }

    private void UpdateMachineCoords(Coordinate coords) 
    {
        if (coords is null || !coords.AreValidCoords())
            return;

        XCoord = coords.X.Value;
        YCoord = coords.Y.Value;
    }
}
