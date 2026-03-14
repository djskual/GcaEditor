using System;
using System.Windows;
using GcaUpdater.Services;

namespace GcaUpdater;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var options = UpdaterOptions.Parse(e.Args);
            var logger = new AppLogger(options.LogFilePath);
            var window = new MainWindow();
            var orchestrator = new UpdateOrchestrator(options, logger, window);

            MainWindow = window;
            window.Show();
            _ = orchestrator.RunAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Updater startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }
}
