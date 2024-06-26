using HNice.Service;
using HNice.ViewModel;
using System.Windows;

namespace HNice.View;

/// <summary>
/// Interaction logic for CreditsView.xaml
/// </summary>
/// 
public partial class CreditsView : Window
{
    private CreditsViewModel _viewModel;
    public CreditsView(ITcpInterceptorWorker worker)
    {
        InitializeComponent();
        _viewModel = new CreditsViewModel(worker);
        this.DataContext = _viewModel;
    }
}
