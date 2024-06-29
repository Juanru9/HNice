using HNice.Util.Extensions;
using HNice.ViewModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace HNice.View;

/// <summary>
/// Interaction logic for CreditsView.xaml
/// </summary>
/// 
public partial class EncoderDecoderView : Window
{
    private EncodeDecodeViewModel _viewModel;
    public EncoderDecoderView()
    {
        InitializeComponent();
        _viewModel = new EncodeDecodeViewModel();
        this.DataContext = _viewModel;
    }

    private void encoderDecoderB64_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }
        var checkBox = sender as RadioButton;

        try
        {
            if (checkBox is not null && checkBox.IsChecked is not null && checkBox.IsChecked.Value)
            {
                switch (checkBox.Name)
                {
                    case "EncodeB64":
                        if (!Int32.TryParse(_viewModel.B64, out var b64Decoded))
                        {
                            this.B64Result.Text = "Invalid integer to encode!";
                            return;
                        }
                        this.B64Result.Text = b64Decoded.EncodeB64();
                        break;
                    case "DecodeB64":
                        var b64Encoded = _viewModel.B64;
                        if (string.IsNullOrEmpty(b64Encoded))
                        {
                            this.B64Result.Text = "Invalid string to decode!";
                            return;
                        }
                        this.B64Result.Text = b64Encoded.DecodeB64().ToString(CultureInfo.InvariantCulture);
                        break;
                    default:
                        return;
                }
            }
        }
        catch (Exception ex)
        {
            return;
        }
    }

    private void encoderDecoderLV64_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) 
        {
            return;
        }
        var checkBox = sender as RadioButton;
        try 
        {
            if (checkBox is not null && checkBox.IsChecked is not null && checkBox.IsChecked.Value)
            {
                switch (checkBox.Name)
                {
                    case "EncodeLV64":
                        if (!Int32.TryParse(_viewModel.LV64, out var lV64Decoded))
                        {
                            this.LV64Result.Text = "Invalid integer to encode!";
                            return;
                        }
                        this.LV64Result.Text = lV64Decoded.EncodeVL64();
                        break;
                    case "DecodeLV64":
                        var lv64Encoded = _viewModel.LV64;
                        if (string.IsNullOrEmpty(lv64Encoded))
                        {
                            this.LV64Result.Text = "Invalid string to decode!";
                            return;
                        }
                        this.LV64Result.Text = string.Join(Environment.NewLine, lv64Encoded.DecodeVL64()
                            .Select(value => $"{value.StringCodeValue} -> {value.IntCodeValue}"));
                        break;
                    default:
                        return;
                }
            }
        }
        catch( Exception ex) 
        {
            return;
        }
    }
}
