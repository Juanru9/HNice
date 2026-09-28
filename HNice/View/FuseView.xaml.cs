using HNice.Service;
using HNice.ViewModel;
using System.Windows;

namespace HNice.View;

public partial class FuseView : Window
{
    public FuseView(ITcpInterceptorWorker worker)
    {
        InitializeComponent();
        DataContext = new FuseViewModel(worker);
    }
}
