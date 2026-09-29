using HNice.Model;
using HNice.Service;
using HNice.Util;
using HNice.ViewModel;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Input;

namespace HNice.View;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow(ITcpInterceptorWorker worker, ILogger<MainWindowViewModel> logger)
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);

        _viewModel = new MainWindowViewModel(worker, logger);
        _viewModel.EntriesAppended += OnEntriesAppended;
        DataContext = _viewModel;
    }

    private void OnEntriesAppended()
    {
        if (!_viewModel.FollowLog || LogList.Items.Count == 0) return;
        LogList.ScrollIntoView(LogList.Items[^1]);
    }

    private void LogList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => UseSelectedInComposer();

    private void LogList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CopySelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            UseSelectedInComposer();
            e.Handled = true;
        }
    }

    private void CopySelected_Click(object sender, RoutedEventArgs e) => CopySelected();

    private void UseInComposer_Click(object sender, RoutedEventArgs e) => UseSelectedInComposer();

    // Copies one line per packet, in log order: time, direction, message name and escaped packet.
    // (Double-click / "Edit in composer" is the way to reuse the raw packet.)
    private void CopySelected()
    {
        var lines = LogList.SelectedItems.Cast<PacketLogEntry>()
            .OrderBy(entry => LogList.Items.IndexOf(entry))
            .Select(entry => entry.ToString())
            .ToList();
        if (lines.Count > 0)
        {
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
        }
    }

    private void UseSelectedInComposer()
    {
        if (LogList.SelectedItem is not PacketLogEntry entry) return;
        _viewModel.UseInComposerCommand.Execute(entry);
        Composer.Focus();
        Composer.CaretIndex = Composer.Text.Length;
    }
}
