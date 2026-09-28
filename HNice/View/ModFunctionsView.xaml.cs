using HNice.Service;
using HNice.ViewModel;
using System.Windows;

namespace HNice.View;

public partial class ModFunctionsView : Window
{
    public ModFunctionsView(ITcpInterceptorWorker worker)
    {
        InitializeComponent();
        DataContext = new ModFunctionsViewModel(worker);
    }
}
