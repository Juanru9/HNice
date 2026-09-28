using HNice.Service;
using HNice.ViewModel;
using System.Windows;

namespace HNice.View;

public partial class SpawnUserView : Window
{
    public SpawnUserView(ITcpInterceptorWorker worker)
    {
        InitializeComponent();
        DataContext = new SpawnUserViewModel(worker);
    }
}
