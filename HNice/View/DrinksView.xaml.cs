using HNice.Service;
using HNice.ViewModel;
using System.Windows;

namespace HNice.View;

/// <summary>
/// Interaction logic for CreditsView.xaml
/// </summary>
/// 
public partial class DrinksView : Window
{
    private DrinksViewModel _viewModel;
    public DrinksView(ITcpInterceptorWorker worker)
    {
        InitializeComponent();
        _viewModel = new DrinksViewModel(worker);
        this.DataContext = _viewModel;
    }

}
