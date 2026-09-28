using HNice.Service;
using HNice.ViewModel;
using System.Windows;

namespace HNice.View;

public partial class WarpView : Window
{
    public WarpView(ITcpInterceptorWorker worker)
    {
        InitializeComponent();
        DataContext = new WarpViewModel(worker);
    }
}
