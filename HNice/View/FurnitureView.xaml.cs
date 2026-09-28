using HNice.Service;
using HNice.ViewModel;
using System.Windows;

namespace HNice.View;

public partial class FurnitureView : Window
{
    private readonly FurnitureViewModel _viewModel;

    public FurnitureView(ITcpInterceptorWorker worker)
    {
        InitializeComponent();
        _viewModel = new FurnitureViewModel(worker);
        this.DataContext = _viewModel;
    }
}
