using HNice.Service;

namespace HNice.ViewModel;

class EncodeDecodeViewModel : BaseViewModel
{
    #region Properties
    private string _b64;
    public string B64
    {
        get => _b64;
        set
        {
            if (_b64 == value)
            {
                return;
            }
            _b64 = value;
            OnPropertyChanged(nameof(B64));
        }
    }
    private string _lV64;
    public string LV64
    {
        get => _lV64;
        set
        {
            if (_lV64 == value)
            {
                return;
            }
            _lV64 = value;
            OnPropertyChanged(nameof(LV64));
        }
    }
    private readonly ITcpInterceptorWorker _worker;
    #endregion

    public EncodeDecodeViewModel() : base()
    {
    }
}
