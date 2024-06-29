using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace HNice.Controls;

public partial class LogControl : UserControl, INotifyPropertyChanged
{
    public int MaxLines { get; set; } = 1000;

    public static readonly DependencyProperty LogsProperty =
        DependencyProperty.Register("Logs", typeof(ObservableCollection<string>), typeof(LogControl),
            new PropertyMetadata(new ObservableCollection<string>(), OnLogsChanged));

    public ObservableCollection<string> Logs
    {
        get => (ObservableCollection<string>)GetValue(LogsProperty);
        set => SetValue(LogsProperty, value);
    }

    private string _logsText;
    public string LogsText
    {
        get => _logsText;
        set
        {
            _logsText = value;
            OnPropertyChanged(nameof(LogsText));
        }
    }

    public LogControl()
    {
        InitializeComponent();
        Logs.CollectionChanged += (s, e) => UpdateLogsText();
    }

    private static void OnLogsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (LogControl)d;
        if (e.OldValue is ObservableCollection<string> oldLogs)
        {
            oldLogs.CollectionChanged -= (s, ev) => control.UpdateLogsText();
        }

        if (e.NewValue is ObservableCollection<string> newLogs)
        {
            newLogs.CollectionChanged += (s, ev) => control.UpdateLogsText();
        }
        control.UpdateLogsText();
    }

    private void UpdateLogsText()
    {
        LogsText = string.Join(Environment.NewLine, Logs);
        Dispatcher.Invoke(() =>
        {
            TextBox.ScrollToEnd();
        });
    }

    public void Log(string str)
    {
        Dispatcher.Invoke(() =>
        {
            Logs.Add(str);
            while (Logs.Count > MaxLines)
            {
                Logs.RemoveAt(0);
            }
        });
    }

    public void Clear()
    {
        Dispatcher.Invoke(() => Logs.Clear());
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
