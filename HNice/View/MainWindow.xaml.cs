using HNice.Service;
using HNice.ViewModel;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace HNice.View;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainWindowViewModel _viewModel;

    public MainWindow(ITcpInterceptorWorker worker, ILogger<MainWindowViewModel> logger)
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel(worker, logger);
        this.DataContext = _viewModel;
    }

    private void getCredits_Click(object sender, RoutedEventArgs e)
    {
        var _ = new CreditsView(_viewModel.Worker);
        _.Show();
    }

    private void getDrinks_Click(object sender, RoutedEventArgs e)
    {
        var _ = new DrinksView(_viewModel.Worker);
        _.Show();
    }

    private void encoderDecoder_Click(object sender, RoutedEventArgs e)
    {
        var _ = new EncoderDecoderView();
        _.Show();
    }

    private void about_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("This software is completely free and should not be used for purposes contrary to Sulake © policies.\nThe author is not responsible for any misuse of the tool.\n\nAuthor: github.com/Juanru9");
    }
}
