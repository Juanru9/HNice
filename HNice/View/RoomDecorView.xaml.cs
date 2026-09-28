using HNice.Service;
using HNice.ViewModel;
using System.Windows;

namespace HNice.View;

public partial class RoomDecorView : Window
{
    public RoomDecorView(ITcpInterceptorWorker worker)
    {
        InitializeComponent();
        DataContext = new RoomDecorViewModel(worker);
    }
}
