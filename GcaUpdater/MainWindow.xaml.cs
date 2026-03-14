using System;
using System.Windows;
using GcaUpdater.Services;

namespace GcaUpdater;

public partial class MainWindow : Window, IUpdateUi
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public void SetStatus(string message)
    {
        Dispatcher.Invoke(() => StatusTextBlock.Text = message);
    }

    public void SetProgress(double value)
    {
        Dispatcher.Invoke(() => MainProgressBar.Value = Math.Max(0, Math.Min(100, value)));
    }

    public void AppendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            if (!string.IsNullOrWhiteSpace(LogTextBox.Text))
            {
                LogTextBox.AppendText(Environment.NewLine);
            }

            LogTextBox.AppendText(message);
            LogTextBox.ScrollToEnd();
        });
    }

    public void EnableCloseButton()
    {
        Dispatcher.Invoke(() => CloseButton.IsEnabled = true);
    }

    public void ShowError(string message)
    {
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show(this, message, "Update failed", MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    public void ShowInfo(string message)
    {
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show(this, message, "Update", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
