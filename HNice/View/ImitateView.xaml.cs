using HNice.Service;
using HNice.ViewModel;
using System.Windows;

namespace HNice.View;

public partial class ImitateView : Window
{
    public ImitateView(ITcpInterceptorWorker worker)
    {
        InitializeComponent();
        DataContext = new ImitateViewModel(worker);
    }
}
