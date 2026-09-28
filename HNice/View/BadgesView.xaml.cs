using HNice.Service;
using HNice.ViewModel;
using System.Windows;

namespace HNice.View;

public partial class BadgesView : Window
{
    public BadgesView(ITcpInterceptorWorker worker)
    {
        InitializeComponent();
        DataContext = new BadgesViewModel(worker);
    }
}
