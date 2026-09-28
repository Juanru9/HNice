using HNice.Service;
using HNice.ViewModel;
using System.Windows;

namespace HNice.View;

public partial class PacketSenderView : Window
{
    public PacketSenderView(ITcpInterceptorWorker worker)
    {
        InitializeComponent();
        DataContext = new PacketSenderViewModel(worker);
    }
}
