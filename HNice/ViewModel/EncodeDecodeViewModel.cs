using HNice.Util.Extensions;
using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace HNice.ViewModel;

/// <summary>
/// Live converter between numbers and the two Habbo encodings: B64 (headers, lengths) and VL64 (ints).
/// Results update as you type; no buttons to press.
/// </summary>
class EncodeDecodeViewModel : BaseViewModel
{
    public ICommand CopyCommand { get; }

    public EncodeDecodeViewModel() : base()
    {
        CopyCommand = new RelayCommand(p => { if (p is string s && s.Length > 0) Clipboard.SetText(s); });
        NumberInput = "202";
        EncodedInput = "CJ";
    }

    #region Number -> encoded
    private string _numberInput = string.Empty;
    public string NumberInput
    {
        get => _numberInput;
        set
        {
            _numberInput = value;
            OnPropertyChanged();
            var ok = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n);
            NumberError = ok || string.IsNullOrWhiteSpace(value) ? string.Empty : "Enter a whole number.";
            B64Header = ok && n >= 0 ? n.EncodeB64(2) : string.Empty;
            B64Length = ok && n >= 0 ? n.EncodeB64(3) : string.Empty;
            Vl64 = ok ? n.EncodeVL64() : string.Empty;
        }
    }

    private string _numberError = string.Empty;
    public string NumberError { get => _numberError; private set { _numberError = value; OnPropertyChanged(); } }

    private string _b64Header = string.Empty;
    public string B64Header { get => _b64Header; private set { _b64Header = value; OnPropertyChanged(); } }

    private string _b64Length = string.Empty;
    public string B64Length { get => _b64Length; private set { _b64Length = value; OnPropertyChanged(); } }

    private string _vl64 = string.Empty;
    public string Vl64 { get => _vl64; private set { _vl64 = value; OnPropertyChanged(); } }
    #endregion

    #region Encoded -> number
    private string _encodedInput = string.Empty;
    public string EncodedInput
    {
        get => _encodedInput;
        set
        {
            _encodedInput = value;
            OnPropertyChanged();
            B64Value = string.IsNullOrEmpty(value) ? string.Empty : value.DecodeB64().ToString(CultureInfo.InvariantCulture);
            try
            {
                Vl64Values = string.IsNullOrEmpty(value)
                    ? string.Empty
                    : string.Join("   ", value.DecodeVL64().Select(v => $"{v.StringCodeValue} = {v.IntCodeValue}"));
            }
            catch (Exception)
            {
                Vl64Values = "Not valid VL64";
            }
        }
    }

    private string _b64Value = string.Empty;
    public string B64Value { get => _b64Value; private set { _b64Value = value; OnPropertyChanged(); } }

    private string _vl64Values = string.Empty;
    public string Vl64Values { get => _vl64Values; private set { _vl64Values = value; OnPropertyChanged(); } }
    #endregion
}
